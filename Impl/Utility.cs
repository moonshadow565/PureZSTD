using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public static class Utility
    {
        public static int HighestBitSet(uint value)
        {
            return 32 - BitOperations.LeadingZeroCount(value) - 1;
        }

        public static int HighestBitSet(int value) => HighestBitSet((uint)value);

        public static uint ReadBitsUInt32(ReadOnlySpan<byte> data, int pos, int bits)
        {
            var shift = pos % 8;
            var byteCount = (shift + bits + 7) / 8;
            data = data.Slice((pos / 8), byteCount);
            uint result;
            switch (byteCount)
            {
                case 1:
                    result = data[0];
                    result >>= shift;
                    break;
                case 2:
                    result = data[0] | ((uint)data[1] << 8);
                    result >>= shift;
                    break;
                case 3:
                    result = data[0] | ((uint)data[1] << 8) | ((uint)data[2] << 16);
                    result >>= shift;
                    break;
                case 4:
                    result = data[0] | ((uint)data[1] << 8) | ((uint)data[2] << 16) | ((uint)data[3] << 24);
                    result >>= shift;
                    break;
                case 5:
                    result = data[0] | ((uint)data[1] << 8) | ((uint)data[2] << 16) | ((uint)data[3] << 24);
                    result >>= shift;
                    result |= (uint)data[4] << (32 - shift);
                    break;
                default:
                    throw new Error.InvalidState();
            }
            result &= (0xffffffffu >> (32 - bits));
            return result;
        }

        public static ushort ReadBitsUInt16(ReadOnlySpan<byte> data, int pos, int bits)
        {
            var shift = pos % 8;
            var byteCount = (shift + bits + 7) / 8;
            data = data.Slice((pos / 8), byteCount);
            uint result;
            switch (byteCount)
            {
                case 1:
                    result = data[0];
                    result >>= shift;
                    break;
                case 2:
                    result = data[0] | ((uint)data[1] << 8);
                    result >>= shift;
                    break;
                case 3:
                    result = data[0] | ((uint)data[1] << 8) | ((uint)data[2] << 16);
                    result >>= shift;
                    break;
                default:
                    throw new Error.InvalidState();
            }
            result &= (0xffffu >> (16 - bits));
            return (ushort)result;
        }

        public static ushort ReadUInt16(ReadOnlySpan<byte> data)
        {
            return (ushort)(data[0] | ((uint)data[1] << 8));
        }

        public static uint ReadUInt32(ReadOnlySpan<byte> data)
        {
            return data[0] | ((uint)data[1] << 8) | ((uint)data[2] << 16) | ((uint)data[3] << 24);
        }

        public static ulong ReadUInt64(ReadOnlySpan<byte> data)
        {
            return ReadUInt32(data) | ((ulong)ReadUInt32(data.Slice(4)) << 32);
        }
    }
}