using System;

namespace PureZSTD.Impl
{
    public ref struct BitReaderReverse
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _pos;
        public ReadOnlySpan<byte> Data => _data;

        public int Position => _pos;

        public BitReaderReverse(ReadOnlySpan<byte> data)
        {
            _data = data;
            _pos = data.Length * 8;
            if (_pos > 0)
            {
                var padding = 8 - Utility.HighestBitSet(_data[_data.Length - 1]);
                _pos -= padding;
            }
        }

        public uint ReadBitsUInt32(int bits)
        {
            if (bits > 32 || bits < 0)
            {
                throw new Error.IO.InvalidBitSize();
            }
            if (bits == 0)
            {
                return 0;
            }
            _pos -= bits;
            var pos = _pos;
            if (pos < 0)
            {
                var skipCount = -pos;
                if (skipCount >= bits)
                {
                    return 0;
                }
                return Utility.ReadBitsUInt32(_data, 0, bits - skipCount) << skipCount;
            }
            return Utility.ReadBitsUInt32(_data, pos, bits);
        }

        public ushort ReadBitsUInt16(int bits)
        {
            if (bits > 16 || bits < 0)
            {
                throw new Error.IO.InvalidBitSize();
            }
            if (bits == 0)
            {
                return 0;
            }
            _pos -= bits;
            var pos = _pos;
            if (pos < 0)
            {
                var skipCount = -pos;
                if (skipCount >= bits)
                {
                    return 0;
                }
                return (ushort)(Utility.ReadBitsUInt16(_data, 0, bits - skipCount) << skipCount);
            }
            return Utility.ReadBitsUInt16(_data, pos, bits);
        }
    }
}