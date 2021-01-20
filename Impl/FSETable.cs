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
            if (probabilities.Length > MAX_SYMBOLS) 
            {
                throw new Error.OutOfRange(nameof(probabilities.Length));
            }
            if (accuracy > MAX_ACCURACY)
            {
                throw new Error.OutOfRange(nameof(accuracy));
            }

            var tableSize = 1 << accuracy;
            _accuracy = accuracy;
            Reserve(tableSize);

            Span<int> stateDesc = stackalloc int[MAX_SYMBOLS];
            
            var highThreshold = tableSize;
            for (var s = 0; s < probabilities.Length; s++) 
            {
                if (probabilities[s] >= 0) 
                {  
                    continue;
                }
                highThreshold -= 1;
                _symbol[highThreshold] = (byte)s;
                stateDesc[s] = 1;
            }

            var step = (tableSize >> 1) + (tableSize >> 3) + 3;
            var mask = tableSize - 1;
            var pos = 0;
            for (var s = 0; s < probabilities.Length; s++) 
            {
                if (probabilities[s] <= 0) 
                {
                    continue;
                }
                stateDesc[s] = probabilities[s];
                for (var i = 0; i < probabilities[s]; i++)
                {
                    _symbol[pos] = (byte)s;
                    do {
                        pos += step;
                        pos &= mask;
                    } while (pos >= highThreshold);
                }
            }

            if (pos != 0) 
            {
                throw new Error.Corruption("pos != 0");
            }

            for (var i = 0; i < tableSize; i++) 
            {
                var symbol = _symbol[i];
                var nextStateDesc = (ushort)stateDesc[symbol]++;
                _numBits[i] = (byte)(accuracy - Utility.HighestBitSet(nextStateDesc));
                _baseLine[i] = (nextStateDesc << _numBits[i]) - tableSize;
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
            Span<int> frequencies = stackalloc int[MAX_SYMBOLS];
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
                    frequencies.Slice(count, repeatTotal).Fill(0);
                    count += repeatTotal;
                }
            }
            reader.AlignStream();
            if (remaining != 0 || count > MAX_SYMBOLS)
            {
                throw new Error.OutOfRange(nameof(remaining));
            }

            InitWithProbabilities(frequencies.Slice(0, count), accuracy);
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