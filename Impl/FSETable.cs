using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public class FSETable
    {
        private const int MAX_ACCURACY = 15;

        private const int MAX_SYMBOLS = 256;

        private int _accuracy;
        private int[] _baseLine = new int[1];
        private int[] _numBits = new int[1];
        private byte[] _symbol = new byte[1];

        public int InitState(ref BitReaderReverse bitReader)
        {
            return bitReader.ReadBitsUInt16(_accuracy);
        }

        public byte Peek(int state)
        {
            return _symbol[state];
        }

        public int Update(ref BitReaderReverse bitReader, int state)
        {
            return _baseLine[state] + bitReader.ReadBitsUInt16(_numBits[state]);
        }

        private void Reserve(int size)
        {
            Array.Fill(_baseLine, 0);
            Array.Fill(_numBits, 0);
            Array.Fill(_symbol, (byte)0);
            if (_symbol.Length < size)
            {
                Array.Resize(ref _baseLine, size);
                Array.Resize(ref _numBits, size);
                Array.Resize(ref _symbol, size);
            }
        }

        private void InitWithProbabilities(ReadOnlySpan<int> probabilities, int accuracy)
        {
            _accuracy = accuracy;
            var tableSize = 1 << accuracy;
            Reserve(tableSize);

            var negativeIndex = tableSize;
            for (var i = 0; i < probabilities.Length; ++i)
            {
                if (probabilities[i] >= 0)
                {
                    continue;
                }
                negativeIndex -= 1;
                _numBits[negativeIndex] = accuracy;
                _symbol[negativeIndex] = (byte)i;
            }

            var position = 0;
            for (var i = 0; i != probabilities.Length; ++i)
            {
                if (probabilities[i] <= 0)
                {
                    continue;
                }
                var prob = probabilities[i];
                for (var j = 0; j < prob; ++j)
                {
                    _symbol[position] = (byte)i;
                    do
                    {
                        position += (tableSize >> 1) + (tableSize >> 3) + 3;
                        position &= tableSize - 1;
                    } while (position >= negativeIndex);
                }
            }

            // TODO: probabilities should be sorted?
            var counter = new int[probabilities.Length];
            for (int i = 0; i != negativeIndex; ++i)
            {
                var symbol = _symbol[i];
                var prob = probabilities[symbol];
                var numSlices = 1 << (32 - BitOperations.LeadingZeroCount((uint)(prob - 1)));
                var numDoubleSlices = numSlices - prob;
                var sliceWidth = tableSize / numSlices;
                var numBits = 32 - BitOperations.LeadingZeroCount((uint)sliceWidth);

                var count = counter[symbol];
                counter[symbol] += 1;
                if (count < numDoubleSlices)
                {
                    var numSingleSlices = numDoubleSlices - prob;
                    _baseLine[i] = (count * 2 - numSingleSlices) * sliceWidth;
                    _numBits[i] = numBits;
                }
                else
                {
                    _baseLine[i] = (count - numDoubleSlices) * sliceWidth;
                    _numBits[i] = numBits - 1;
                }
            }
        }

        public void InitWithRLE(byte symbol)
        {
            _accuracy = 0;
            Reserve(1);
            _symbol[0] = symbol;
        }

        public void InitFromRead(ref ByteReader reader, int maxAccuracy)
        {
            if (maxAccuracy > MAX_ACCURACY)
            {
                throw new Error.OutOfRange(nameof(maxAccuracy));
            }
            var accuracy = 5 + reader.ReadBitsInt32(4);
            if (accuracy < 1 || accuracy > maxAccuracy)
            {
                throw new Error.OutOfRange(nameof(accuracy));
            }
            var remaining = 1 << accuracy;
            var frequencies = new int[256];
            var count = 0;
            while (remaining > 0 && count < MAX_SYMBOLS)
            {
                var bits = Utility.HighestBitSet(remaining + 1) + 1;
                var val = reader.ReadBitsInt32(bits);
                var lowerMask = (1 << (bits - 1)) - 1;
                var threshold = (1 << bits) - 1 - (remaining + 1);

                if ((val & lowerMask) < threshold)
                {
                    reader.RewindBits(1);
                    val = val & lowerMask;
                }
                else if (val > lowerMask)
                {
                    val = val - threshold;
                }

                var probability = val - 1;

                remaining -= probability < 0 ? -probability : probability;
                frequencies[count++] = probability;

                if (probability == 0)
                {
                    var repeat = reader.ReadBitsInt32(2);
                    var repeatTotal = repeat;
                    while (repeat == 3)
                    {
                        repeat = reader.ReadBitsInt32(2);
                        repeatTotal += repeat;
                    }
                    Array.Fill(frequencies, 0, count, repeatTotal);
                    count += repeatTotal;
                }
            }
            reader.AlignStream();
            if (remaining != 0 || count > MAX_SYMBOLS)
            {
                throw new Error.OutOfRange(nameof(remaining));
            }

            InitWithProbabilities(new ReadOnlySpan<int>(frequencies, 0, count), accuracy);
        }

        private static FSETable CreateDefault(ReadOnlySpan<int> probabilities, int accuracy)
        {
            var table = new FSETable();
            table.InitWithProbabilities(probabilities, accuracy);
            return table;
        }

        public static readonly FSETable DEFAULT_LITERAL_LENGTH = CreateDefault(
            probabilities: new int[] {
                4, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1,  1,  1,  2,  2,
                2, 2, 2, 2, 2, 2, 2, 3, 2, 1, 1, 1, 1, 1, -1, -1, -1, -1,
            },
            accuracy: 6
        );

        public static readonly FSETable DEFAULT_OFFSET = CreateDefault(
            probabilities: new int[] {
                1, 1, 1, 1, 1, 1, 2, 2, 2,  1,  1,  1,  1,  1, 1,
                1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1,
            },
            accuracy: 5
        );

        public static readonly FSETable DEFAULT_MATCH_LENGTH = CreateDefault(
            probabilities: new int[] {
                1, 4, 3, 2, 2, 2, 2, 2, 2, 1,  1,  1,  1,  1,  1,  1,  1, 1,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1,  1,  1,  1,  1,  1,  1,  1, 1,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, -1, -1, -1, -1, -1, -1, -1
            },
            accuracy: 6
        );
    }
}