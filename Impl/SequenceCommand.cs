using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public struct SequenceCommand
    {
        private static readonly int[] LITERAL_LENGTH_BASELINES = new int[]
        {
            0,  1,  2,   3,   4,   5,    6,    7,    8,    9,     10,    11,
            12, 13, 14,  15,  16,  18,   20,   22,   24,   28,    32,    40,
            48, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536
        };

        private static readonly int[] LITERAL_LENGTH_EXTRA_BITS = new int[]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,  0,  0,  0,  0,  1,  1,
            1, 1, 2, 2, 3, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16
        };

        private static readonly int[] MATCH_LENGTH_BASELINES = new int[]
        {
            3,  4,   5,   6,   7,    8,    9,    10,   11,    12,    13,   14, 15, 16,
            17, 18,  19,  20,  21,   22,   23,   24,   25,    26,    27,   28, 29, 30,
            31, 32,  33,  34,  35,   37,   39,   41,   43,    47,    51,   59, 67, 83,
            99, 131, 259, 515, 1027, 2051, 4099, 8195, 16387, 32771, 65539
        };

        private static readonly int[] MATCH_LENGTH_EXTRA_BITS = new int[]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,  0,  0,  0,  0,  0,  0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,  0,  0,  0,  1,  1,  1, 1,
            2, 2, 3, 3, 4, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16
        };

        private static readonly int[] MAX_CODES = new int[] { 35, 0xFF, 52 };

        public int LiteralLength { get; set; }
        public int MatchLength { get; set; }
        public long Offset { get; set; }

        public static SequenceCommand Read(ref BitReaderReverse bitReader, int literalLengthCode, int matchLengthCode, int offsetCode)
        {
            var result = new SequenceCommand();
            result.Offset = ((long)1 << offsetCode);
            result.MatchLength = MATCH_LENGTH_BASELINES[matchLengthCode];
            result.LiteralLength = LITERAL_LENGTH_BASELINES[literalLengthCode];
            result.Offset += bitReader.ReadBitsUInt32(offsetCode);
            result.MatchLength += bitReader.ReadBitsUInt16(MATCH_LENGTH_EXTRA_BITS[matchLengthCode]);
            result.LiteralLength += bitReader.ReadBitsUInt16(LITERAL_LENGTH_EXTRA_BITS[literalLengthCode]);
            return result;
        }

        public void UpdateOffset(long[] previousOffsets)
        {
            if (Offset <= 3)
            {
                var index = Offset - 1;
                if (LiteralLength == 0)
                {
                    index += 1;
                }

                if (index == 0)
                {
                    Offset = previousOffsets[0];
                }
                else
                {
                    Offset = index < 3 ? previousOffsets[(int)index] : previousOffsets[0] - 1;
                    if (index > 1)
                    {
                        previousOffsets[2] = previousOffsets[1];
                    }
                    previousOffsets[1] = previousOffsets[0];
                    previousOffsets[0] = Offset;
                }
            }
            else
            {
                Offset -= 3;
                previousOffsets[2] = previousOffsets[1];
                previousOffsets[1] = previousOffsets[0];
                previousOffsets[0] = Offset;
            }
        }
    }
}