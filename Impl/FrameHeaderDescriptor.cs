using System;

namespace PureZSTD.Impl
{
    public enum FrameContentSizeFlag
    {
        Flag0,
        Flag1,
        Flag2,
        Flag3,
    }

    public enum FrameDictionaryIDFlag
    {
        Flag0,
        Flag1,
        Flag2,
        Flag3,
    }

    public struct FrameHeaderDescriptor
    {
        public FrameDictionaryIDFlag DictionaryIDFlag { get; private set; }

        public bool ContentChecksumFlag { get; private set; }

        public bool ReservedBit { get; private set; }

        public bool UnusedBit { get; private set; }

        public bool SingleSegmentFlag { get; private set; }

        public FrameContentSizeFlag ContentSizeFlag { get; private set; }

        public int HeaderSize { get; private set; }

        public static FrameHeaderDescriptor Create(int raw)
        {
            if (raw < 0 || raw > 0xFF)
            {
                throw new Error.OutOfRange(nameof(raw));
            }
            var result = new FrameHeaderDescriptor();
            result.DictionaryIDFlag = (FrameDictionaryIDFlag)((raw >> 0) & 3);
            result.ContentChecksumFlag = ((raw >> 2) & 1) != 0;
            result.ReservedBit = ((raw >> 3) & 1) != 0;
            result.UnusedBit = ((raw >> 4) & 1) != 0;
            result.SingleSegmentFlag = ((raw >> 5) & 1) != 0;
            result.ContentSizeFlag = (FrameContentSizeFlag)((raw >> 6) & 3);
            if (!result.SingleSegmentFlag)
            {
                result.HeaderSize += 1;
            }
            switch (result.DictionaryIDFlag)
            {
                case FrameDictionaryIDFlag.Flag0:
                    break;
                case FrameDictionaryIDFlag.Flag1:
                    result.HeaderSize += 1;
                    break;
                case FrameDictionaryIDFlag.Flag2:
                    result.HeaderSize += 2;
                    break;
                case FrameDictionaryIDFlag.Flag3:
                    result.HeaderSize += 4;
                    break;
            }
            switch (result.ContentSizeFlag)
            {
                case FrameContentSizeFlag.Flag0:
                    if (result.SingleSegmentFlag)
                    {
                        result.HeaderSize += 1;
                    }
                    break;
                case FrameContentSizeFlag.Flag1:
                    result.HeaderSize += 2;
                    break;
                case FrameContentSizeFlag.Flag2:
                    result.HeaderSize += 4;
                    break;
                case FrameContentSizeFlag.Flag3:
                    result.HeaderSize += 8;
                    break;
            }
            return result;
        }
    
        public static FrameHeaderDescriptor Create(ReadOnlySpan<byte> data)
        {
            if (data.Length < 1)
            {
                throw new Error.OutOfRange("Frame header descriptor must be 1 byte!");
            }
            return Create(data[0]);
        }
    }
}