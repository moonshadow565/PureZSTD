using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public class HUFTable
    {
        private int[] _numBits = new int[1];
        private byte[] _symbol = new byte[1];
        private int _maxNumBits;
        private FSETable _tmpTable = new FSETable();
        public int MaxNumBits => _maxNumBits;

        private const int MAX_BITS = 16;

        private const int MAX_SYMBOLS = 256;

        private const int MAX_ACCURACY = 7;

        public int Init(ref BitReaderReverse bitReader)
        {
            return bitReader.ReadBitsUInt16(_maxNumBits);
        }

        public byte Peek(int state)
        {
            return _symbol[state];
        }

        public int Update(ref BitReaderReverse bitReader, int state)
        {
            var bits = _numBits[state];
            var rest = bitReader.ReadBitsUInt16(bits);
            return ((state << bits) + rest) & ((1 << _maxNumBits) - 1);
        }

        private void Reserve(int size)
        {
            Array.Fill(_numBits, 0);
            Array.Fill(_symbol, (byte)0);
            if (_symbol.Length < size)
            {
                Array.Resize(ref _numBits, size);
                Array.Resize(ref _symbol, size);
            }
        }

        private void InitWithWeights(ReadOnlySpan<int> weights)
        {
            if (weights.Length > MAX_SYMBOLS)
            {
                throw new Error.OutOfRange(nameof(weights.Length));
            }

            _maxNumBits = 0;
            var rankCount = new int[MAX_BITS + 1];
            foreach (var weight in weights)
            {
                if (weight > MAX_BITS)
                {
                    throw new Error.OutOfRange(nameof(weight));
                }
                _maxNumBits = Math.Max(_maxNumBits, weight);
                rankCount[weight] += 1;
            }

            var tableSize = 1 << _maxNumBits;
            Reserve(tableSize);
            var rankIndex = new int[MAX_BITS + 1];
            for (int i = _maxNumBits; i >= 1; i--)
            {
                var current = rankIndex[i];
                var previous = current + rankCount[i] * (1 << (_maxNumBits - i));
                rankIndex[i - 1] = previous;
                Array.Fill(_numBits, i, rankIndex[i], previous - current);
            }

            if (rankIndex[0] != tableSize)
            {
                throw new Error.OutOfRange(nameof(tableSize));
            }

            for (int i = 0; i < weights.Length; ++i)
            {
                if (weights[i] == 0)
                {
                    continue;
                }
                var code = rankIndex[weights[i]];
                var len = 1 << (_maxNumBits - weights[i]);
                Array.Fill(_symbol, (byte)i, code, len);
                rankIndex[weights[i]] += len;
            }
        }

        public void InitFromRead(ref ByteReader reader)
        {
            var header = reader.ReadUInt8();
            var weights = new int [MAX_SYMBOLS];
            var count = 0;
            if (header >= 128)
            {
                var numSymbols = header - 127;
                var numBytes = (numSymbols + 1) / 2;
                var subReader = reader.ReadBytes(numBytes);
                for (int i = 0; i < numSymbols; ++i)
                {
                    if (i % 2 == 0)
                    {
                        weights[count++] = (byte)(subReader[i / 2] >> 4);
                    }
                    else
                    {
                        weights[count++] = (byte)(subReader[i / 2] & 0xF);
                    }
                }
            }
            else
            {
                var subReader = reader.SubReader(header);
                _tmpTable.InitFromRead(ref subReader, MAX_ACCURACY);
                var bitReader = subReader.SubReverseReader(subReader.Remaining);
                count += FSEDecoder.DecompressInterleaved2(_tmpTable, ref bitReader, weights);
            }

            if (count >= MAX_SYMBOLS)
            {
                throw new Error.OutOfRange(nameof(count));
            }

            var weightSum = 0;
            for (var i = 0; i < count; ++i)
            {
                var weight = weights[i];
                if (weight > MAX_BITS)
                {
                    throw new Error.OutOfRange(nameof(weight));
                }
                weightSum += weight > 0 ? 1 << (weight - 1) : 0;
            }

            var maxBits = Utility.HighestBitSet(weightSum) + 1;
            var leftOver = (1 << maxBits) - weightSum;
            if ((leftOver & (leftOver - 1)) != 0)
            {
                throw new Error.OutOfRange(nameof(leftOver));
            }

            var lastWeight = Utility.HighestBitSet(leftOver) + 1;
            for (var i = 0; i < count; ++i)
            {
                var weight = weights[i];
                weights[i] = weight > 0 ? (maxBits + 1 - weight) : 0;
            }
            weights[count++] = (maxBits + 1 - lastWeight);

            InitWithWeights(new ReadOnlySpan<int>(weights, 0, count));
        }
    }
}