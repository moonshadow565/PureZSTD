using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public struct SequenceDecoder
    {
        private enum Mode
        {
            Predefined,
            RLE,
            FSE,
            REPEAT,
        }

        private struct Part
        {
            private FSETable _tmpTable;
            private FSETable _table;
            private readonly FSETable _predefinedTable;
            private readonly int _maxAccuracy;

            public Part(FSETable table, FSETable predefinedTable, int maxAccuracy)
            {
                _tmpTable = new FSETable();
                _table = table;
                _predefinedTable = predefinedTable;
                _maxAccuracy = maxAccuracy;
            }

            public void Init(FSETable table)
            {
                _table = table;
            }

            public FSEDecoder GetDecoder(ref ByteReader reader, Mode mode)
            {
                switch (mode)
                {
                    case Mode.Predefined:
                        _table = _predefinedTable;
                        break;
                    case Mode.RLE:
                        _tmpTable.InitWithRLE(reader.ReadUInt8());
                        _table = _tmpTable;
                        break;
                    case Mode.FSE:
                        _tmpTable.InitFromRead(ref reader, _maxAccuracy);
                        _table = _tmpTable;
                        break;
                    case Mode.REPEAT:
                        if (_table is null)
                        {
                            throw new Error.Corruption("Missing previous table!");
                        }
                        break;
                }
                return new FSEDecoder(_table);
            }
        }

        private Part _partLiteralLength;
        private Part _partOffset;
        private Part _partMatchLength;
        private long[] _previousOffsets;
        private SequenceCommand[] _commands;

        public SequenceDecoder(Dictionary dictionary)
        {
            _partLiteralLength = new Part(null, FSETable.DEFAULT_LITERAL_LENGTH, 9);
            _partOffset = new Part(null, FSETable.DEFAULT_OFFSET, 8);
            _partMatchLength = new Part(null, FSETable.DEFAULT_MATCH_LENGTH, 9);
            _previousOffsets = new long[3];
            _commands = new SequenceCommand[1];
            Init(dictionary);
        }

        public void Init(Dictionary dictionary)
        {
            _partLiteralLength.Init(dictionary?.TableSequenceLiteralLength);
            _partOffset.Init(dictionary?.TableSequenceOffset);
            _partMatchLength.Init(dictionary?.TableSequenceMatchLength);
            if (!(dictionary is null) && dictionary.PreviousOffsets.Length == 3)
            {
                Array.Copy(dictionary.PreviousOffsets.ToArray(), _previousOffsets, 3);
            }
            else
            {
                _previousOffsets[0] = 1;
                _previousOffsets[1] = 4;
                _previousOffsets[2] = 8;
            }
        }

        private void Reserve(int size)
        {
            if (_commands.Length < size)
            {
                Array.Resize(ref _commands, size);
            }
        }

        public ReadOnlySpan<SequenceCommand> DecodeCommands(ref ByteReader reader)
        {
            var header = reader.ReadUInt8();
            int num = 0;
            if (header == 0)
            {
                return new ReadOnlySpan<SequenceCommand>(_commands, 0, 0);
            }
            else if (header < 128)
            {
                num = header;
            }
            else if (header < 255)
            {
                num = header - 128;
                num <<= 8;
                num += reader.ReadUInt8();
            }
            else
            {
                num = reader.ReadUInt16();
                num += 0x7F00;
            }
            Reserve(num);
            return DecodeCommands(ref reader, num);
        }

        private ReadOnlySpan<SequenceCommand> DecodeCommands(ref ByteReader reader, int num)
        {
            var modeReserved = reader.ReadBitsInt32(2);
            if (modeReserved != 0)
            {
                throw new Error.Reserved(nameof(Mode));
            }
            var modeMatchLength = (Mode)reader.ReadBitsInt32(2);
            var modeOffset = (Mode)reader.ReadBitsInt32(2);
            var modeLiteralLength = (Mode)reader.ReadBitsInt32(2);

            var literalLengthDecoder = _partLiteralLength.GetDecoder(ref reader, modeLiteralLength);
            var offsetDecoder = _partOffset.GetDecoder(ref reader, modeOffset);
            var matchLengthDecoder = _partMatchLength.GetDecoder(ref reader, modeMatchLength);

            var bitReader = reader.SubReverseReader(reader.Remaining);
            literalLengthDecoder.InitState(ref bitReader);
            offsetDecoder.InitState(ref bitReader);
            matchLengthDecoder.InitState(ref bitReader);

            for (int i = 0; i < num; ++i)
            {
                var literalLengthCode = literalLengthDecoder.PeekState();
                var offsetCode = offsetDecoder.PeekState();
                var matchLengthCode = matchLengthDecoder.PeekState();
                var command = SequenceCommand.Read(ref bitReader, literalLengthCode, matchLengthCode, offsetCode);

                if (bitReader.Position != 0)
                {
                    literalLengthDecoder.UpdateState(ref bitReader);
                    matchLengthDecoder.UpdateState(ref bitReader);
                    offsetDecoder.UpdateState(ref bitReader);
                }
                command.UpdateOffset(_previousOffsets);
                _commands[i] = command;
            }
            return new ReadOnlySpan<SequenceCommand>(_commands, 0, num);
        }
    }
}