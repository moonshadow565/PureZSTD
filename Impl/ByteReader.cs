using System;

namespace PureZSTD.Impl
{
    public ref struct ByteReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _pos;
        private int _bitOffset;

        public ByteReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _pos = 0;
            _bitOffset = 0;
        }


        public int Position => _pos;

        public int Remaining => _data.Length - Position;

        public int ReadBitsInt32(int bits)
        {
            if (bits > 32 || bits <= 0)
            {
                throw new Error.IO.InvalidBitSize();
            }
            var pos = _pos * 8 + _bitOffset;
            var result = Utility.ReadBitsUInt32(_data, pos, bits);
            pos += bits;
            _pos = pos / 8;
            _bitOffset = pos % 8;
            return (int)result;
        }

        public void RewindBits(int bits)
        {
            if (bits < 0)
            {
                throw new Error.IO.InvalidBitSize();
            }
            var pos = _pos * 8 + _bitOffset;
            if (pos < bits)
            {
                throw new Error.IO.NotEnoughBits();
            }
            pos -= bits;
            _pos = pos / 8;
            _bitOffset = pos % 8;
        }

        public void AlignStream()
        {
            if (_bitOffset != 0)
            {
                if (Remaining == 0)
                {
                    throw new Error.IO.NotEnoughBytes();
                }
                _pos += 1;
                _bitOffset = 0;
            }
        }

        public byte ReadUInt8()
        {
            if (_bitOffset != 0)
            {
                throw new Error.IO.UnalignedAccess();
            }
            var result = _data[_pos];
            _pos += 1;
            return result;
        }

        public ushort ReadUInt16()
        {
            if (_bitOffset != 0)
            {
                throw new Error.IO.UnalignedAccess();
            }
            var result = Utility.ReadUInt16(_data.Slice(_pos));
            _pos += 2;
            return result;
        }

        public uint ReadUInt32()
        {
            if (_bitOffset != 0)
            {
                throw new Error.IO.UnalignedAccess();
            }
            var result = Utility.ReadUInt32(_data.Slice(_pos));
            _pos += 4;
            return result;
        }

        public long ReadInt64()
        {
            if (_bitOffset != 0)
            {
                throw new Error.IO.UnalignedAccess();
            }
            var result = (long)Utility.ReadUInt64(_data.Slice(_pos));
            _pos += 8;
            return result;
        }

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (_bitOffset != 0)
            {
                throw new Error.IO.UnalignedAccess();
            }
            var offset = _pos;
            _pos += length;
            return _data.Slice(offset, length);
        }

        public ByteReader SubReader(int length)
        {
            if (_bitOffset != 0)
            {
                throw new Error.IO.UnalignedAccess();
            }
            var offset = _pos;
            _pos += length;
            return new ByteReader(_data.Slice(offset, length));
        }

        public BitReaderReverse SubReverseReader(int length)
        {
            if (_bitOffset != 0)
            {
                throw new Error.IO.UnalignedAccess();
            }
            var offset = _pos;
            _pos += length;
            return new BitReaderReverse(_data.Slice(offset, length));
        }
    }
}