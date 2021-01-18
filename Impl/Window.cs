using System;
using System.Collections.Generic;


namespace PureZSTD.Impl
{
    public struct Window
    {
        private byte[] _buffer;
        private int _writeStart;
        private int _writeEnd;
        private int _readStart;
        private int _readEnd;
        private int _drained;
        private LiteralsDecoder _literalsDecoder;
        private SequenceDecoder _sequenceDecoder;
        private ReadOnlyMemory<byte> _dictionaryContent;

        public Window(int reserved)
        {
            _buffer = new byte[reserved + 4];
            _writeStart = 0;
            _writeEnd = 0;
            _readStart = 0;
            _readEnd = 0;
            _drained = 0;
            _literalsDecoder = new LiteralsDecoder(null);
            _sequenceDecoder = new SequenceDecoder(null);
            _dictionaryContent = new byte[0];
        }

        public void Init(int reserved, Dictionary dictionary)
        {
            if (reserved > _buffer.Length)
            {
                Array.Resize(ref _buffer, reserved);
            }
            _writeStart = 0;
            _writeEnd = 0;
            _readStart = 0;
            _readEnd = 0;
            _drained = 0;
            _literalsDecoder.Init(dictionary);
            _sequenceDecoder.Init(dictionary);
            _dictionaryContent = dictionary?.Content ?? new byte[0];
        }


        public int WriteLength => _writeEnd - _writeStart;

        public ReadOnlySpan<byte> WriteBuffer => new(_buffer, _writeStart, WriteLength);

        public void WriteConsume(int length) => _writeStart += length;


        public int ReadLength => _readEnd - _readStart;

        public Span<byte> ReadBuffer => new(_buffer, _readStart, ReadLength);

        public void ReadConsume(int length) => _readStart += length;


        public ReadOnlySpan<byte> ReadTakeBack(int length)
        {
            var result = new ReadOnlySpan<byte>(_buffer, _readEnd - length, length);
            _readStart -= length;
            _readEnd -= length;
            return result;
        }

        public void ReadCommit(int length)
        {
            _readStart = _writeEnd;
            _readEnd = _readStart + length;
        }

        public bool TryFlush(int windowSize, int readSize)
        {
            var spaceNeeded = readSize;
            if (_buffer.Length - _writeEnd < spaceNeeded)
            {
                var writeLength = WriteLength;
                if (writeLength > windowSize)
                {
                    return false;
                }
                var drained = _writeEnd - windowSize;
                Array.Copy(_buffer, drained, _buffer, 0, windowSize);
                _writeEnd = (int)windowSize;
                _writeStart = _writeEnd - writeLength;
                _drained += drained;
            }
            return true;
        }


        public void WriteCommitRaw(int length)
        {
            _writeEnd += length;
            _readStart = _writeEnd;
            _readEnd = _writeEnd;
        }

        public void WriteCommitRLE(int length)
        {
            Array.Fill(_buffer, _buffer[_writeEnd], _writeEnd, length);
            _writeEnd += length;
            _readStart = _writeEnd;
            _readEnd = _writeEnd;
        }

        public void WriteCommitDecompress(int windowSize, int length)
        {
            var src = new ReadOnlySpan<byte>(_buffer, _writeEnd, length);
            var reader = new ByteReader(src);
            var literals = _literalsDecoder.DecodeLiterals(ref reader);
            var commands = _sequenceDecoder.DecodeCommands(ref reader);
            var writeOffset = _writeEnd;
            var literalsOffset = 0;

            foreach (var command in commands)
            {
                var dst = new Span<byte>(_buffer, writeOffset, command.LiteralLength);
                literals.Slice(literalsOffset, command.LiteralLength).CopyTo(dst);
                writeOffset += command.LiteralLength;
                literalsOffset += command.LiteralLength;
                writeOffset = WriteMatchCopy(windowSize, writeOffset, command.Offset, command.MatchLength);
            }

            var remaining = literals.Length - literalsOffset;
            if (remaining > 0)
            {
                var dst = new Span<byte>(_buffer, writeOffset, remaining);
                literals.Slice(literalsOffset, remaining).CopyTo(dst);
                writeOffset += remaining;
            }

            _writeEnd = writeOffset;
            _readStart = _writeEnd;
            _readEnd = _writeEnd;
        }

        private int WriteMatchCopy(int windowSize, int writeOffset, long offset, int match)
        {
            var totalWriten = _drained + writeOffset;
            if (totalWriten <= windowSize)
            {
                // FIXME: check if this actually works for dictionaries
                var wrappOffset = offset - totalWriten;
                if (wrappOffset > 0)
                {
                    var dictOffset = _dictionaryContent.Length - wrappOffset;
                    if (dictOffset < 0)
                    {
                        throw new Error.OutOfRange("Offset goes beyond dictionary!");
                    }
                    var dictCopy = (int)Math.Min(wrappOffset, match);
                    var src = _dictionaryContent.Slice((int)dictOffset, dictCopy).Span;
                    var dst = new Span<byte>(_buffer, writeOffset, dictCopy);
                    src.CopyTo(dst);
                    writeOffset += dictCopy;
                    match -= dictCopy;
                }
            }
            else if (offset > windowSize)
            {
                throw new Error.OutOfRange(nameof(offset));
            }

            while (match > 0 && match > offset)
            {
                Array.Copy(_buffer, writeOffset - offset, _buffer, writeOffset, offset);
                writeOffset += (int)offset;
                match -= (int)offset;
            }

            if (match > 0)
            {
                Array.Copy(_buffer, writeOffset - offset, _buffer, writeOffset, match);
                writeOffset += match;
            }

            return writeOffset;
        }
    }
}
