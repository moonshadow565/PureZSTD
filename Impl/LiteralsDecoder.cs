using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public struct LiteralsDecoder
    {
        private HUFTable _table;
        private HUFTable _tmpTable;
        private byte[] _literals;

        public LiteralsDecoder(Dictionary dictionary)
        {
            _table = null;
            _tmpTable = new HUFTable();
            _literals = new byte[1];
            Init(dictionary);
        }

        public void Init(Dictionary dictionary)
        {
            _table = dictionary?.TableLiterals;
        }

        private void Reserve(int size)
        {
            if (_literals.Length < size)
            {
                Array.Resize(ref _literals, size);
            }
        }

        public ReadOnlySpan<byte> DecodeLiterals(ref ByteReader reader)
        {
            var header = new LiteralsHeader(ref reader);
            Reserve(header.RegeneratedSize);
            var result = new Span<byte>(_literals, 0, header.RegeneratedSize);
            var subReader = reader.SubReader(header.CompressedSize);
            switch (header.Kind)
            {
                case LiteralsKind.Raw:
                    subReader.ReadBytes(header.RegeneratedSize).CopyTo(result);
                    return result;
                case LiteralsKind.RLE:
                    Array.Fill(_literals, subReader.ReadUInt8(), 0, header.RegeneratedSize);
                    return result;
                case LiteralsKind.HUF:
                case LiteralsKind.HUFRepeat:
                    {
                        if (header.Kind != LiteralsKind.HUFRepeat)
                        {
                            _tmpTable.InitFromRead(ref subReader);
                            _table = _tmpTable;
                        }
                        var pos = 0;
                        if (header.FormatSize == LiteralsFormatSize.Format0)
                        {
                            pos = DecompressStream1(ref subReader, pos);
                        }
                        else
                        {
                            pos = DecompressStream4(ref subReader, pos);
                        }
                        if (pos != header.RegeneratedSize)
                        {
                            throw new Error.Corruption("Literals decompres size!");
                        }
                        return result;
                    }
                default:
                    throw new Exception("Imposible state!");
            }
        }
    
        private int DecompressStream1(ref ByteReader reader, int pos)
        {
            var bitReader = reader.SubReverseReader(reader.Remaining);
            var decoder = new HUFDecoder(_table);
            decoder.InitState(ref bitReader);
            while (bitReader.Position > -_table.MaxNumBits)
            {
                _literals[pos++] = decoder.PeekState();
                decoder.UpdateState(ref bitReader);
            }
            if (bitReader.Position != -_table.MaxNumBits)
            {
                throw new Error.Corruption("Failed to decompress huffman stream!");
            }
            return pos;
        }

        private int DecompressStream4(ref ByteReader reader, int pos)
        {
            var size1 = reader.ReadUInt16();
            var size2 = reader.ReadUInt16();
            var size3 = reader.ReadUInt16();

            var subReader1 = reader.SubReader(size1);
            var subReader2 = reader.SubReader(size2);
            var subReader3 = reader.SubReader(size3);
            var subReader4 = reader.SubReader(reader.Remaining);

            pos = DecompressStream1(ref subReader1, pos);
            pos = DecompressStream1(ref subReader2, pos);
            pos = DecompressStream1(ref subReader3, pos);
            pos = DecompressStream1(ref subReader4, pos);
            return pos;
        }
    }
}