using System;

namespace PureZSTD.Impl
{
    public struct FrameHeader
    {
        private const int MAX_WINDOW_SIZE = 8 * 1024 * 1024;

        public int WindowSize { get; set; }

        public long ContentSize { get; set; }

        public uint DictionaryID { get; set; }

        public bool ContentChecksum { get; set; }

        public static FrameHeader Create(FrameHeaderDescriptor descriptor, ReadOnlySpan<byte> data)
        {
            if (data.Length < descriptor.HeaderSize)
            {
                throw new Error.OutOfRange("Not enough data to decode header!");
            }

            var reader = new ByteReader(data);
            var result = new FrameHeader();

            if (descriptor.ReservedBit)
            {
                throw new Error.Reserved(nameof(FrameHeader));
            }

            result.ContentChecksum = descriptor.ContentChecksumFlag;

            if (!descriptor.SingleSegmentFlag)
            {
                var mantissa = reader.ReadBitsInt32(3);
                var exponent = reader.ReadBitsInt32(5);

                var windowBase = (long)1 << (10 + exponent);
                var windowAdd = (windowBase / 8u) * mantissa;
                var windowSize = windowBase + windowAdd;
                if (windowSize > MAX_WINDOW_SIZE)
                {
                    throw new Error.IO.OutOfRange(nameof(WindowSize));
                }
                result.WindowSize = (int)windowSize;
            }

            switch (descriptor.DictionaryIDFlag)
            {
                case FrameDictionaryIDFlag.Flag0:
                    result.DictionaryID = 0;
                    break;
                case FrameDictionaryIDFlag.Flag1:
                    result.DictionaryID = reader.ReadUInt8();
                    break;
                case FrameDictionaryIDFlag.Flag2:
                    result.DictionaryID = reader.ReadUInt16();
                    break;
                case FrameDictionaryIDFlag.Flag3:
                    result.DictionaryID = reader.ReadUInt32();
                    break;
            }

            switch (descriptor.ContentSizeFlag)
            {
                case FrameContentSizeFlag.Flag0:
                    if (descriptor.SingleSegmentFlag)
                    {
                        result.ContentSize = reader.ReadUInt8();
                    }
                    break;
                case FrameContentSizeFlag.Flag1:
                    result.ContentSize = reader.ReadUInt16();
                    result.ContentSize += 256;
                    break;
                case FrameContentSizeFlag.Flag2:
                    result.ContentSize = reader.ReadUInt32();
                    break;
                case FrameContentSizeFlag.Flag3:
                    result.ContentSize = reader.ReadInt64();
                    break;
            }

            if (result.ContentSize < 0)
            {
                throw new Error.OutOfRange(nameof(result.ContentSize));
            }

            if (descriptor.SingleSegmentFlag)
            {
                if (result.ContentSize > MAX_WINDOW_SIZE)
                {
                    throw new Error.OutOfRange(nameof(result.ContentSize));
                }
                result.WindowSize = (int)result.ContentSize;
            }

            return result;
        }
    }
}