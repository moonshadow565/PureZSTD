using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{

    public enum LiteralsKind
    {
        Raw,
        RLE,
        HUF,
        HUFRepeat,
    }

    public enum LiteralsFormatSize
    {
        Format0,
        Format1,
        Format2,
        Format3,
    }

    public struct LiteralsHeader
    {
        public LiteralsKind Kind { get; set; }
        public LiteralsFormatSize FormatSize { get; set; }
        public int RegeneratedSize { get; set; }
        public int CompressedSize { get; set; }

        public LiteralsHeader(ref ByteReader reader)
        {
            Kind = (LiteralsKind)reader.ReadBitsInt32(2);
            FormatSize = (LiteralsFormatSize)reader.ReadBitsInt32(2);
            RegeneratedSize = 0;
            CompressedSize = 0;
            switch (Kind)
            {
                case LiteralsKind.Raw:
                    switch (FormatSize)
                    {
                        case LiteralsFormatSize.Format0:
                        case LiteralsFormatSize.Format2:
                            reader.RewindBits(1);
                            RegeneratedSize = reader.ReadBitsInt32(5);
                            CompressedSize = RegeneratedSize;
                            break;
                        case LiteralsFormatSize.Format1:
                            RegeneratedSize = reader.ReadBitsInt32(12);
                            CompressedSize = RegeneratedSize;
                            break;
                        case LiteralsFormatSize.Format3:
                            RegeneratedSize = reader.ReadBitsInt32(20);
                            CompressedSize = RegeneratedSize;
                            break;
                    }
                    break;
                case LiteralsKind.RLE:
                    switch (FormatSize)
                    {
                        case LiteralsFormatSize.Format0:
                        case LiteralsFormatSize.Format2:
                            reader.RewindBits(1);
                            RegeneratedSize = reader.ReadBitsInt32(5);
                            CompressedSize = 1;
                            break;
                        case LiteralsFormatSize.Format1:
                            RegeneratedSize = reader.ReadBitsInt32(12);
                            CompressedSize = 1;
                            break;
                        case LiteralsFormatSize.Format3:
                            RegeneratedSize = reader.ReadBitsInt32(20);
                            CompressedSize = 1;
                            break;
                    }
                    break;
                case LiteralsKind.HUF:
                case LiteralsKind.HUFRepeat:
                    switch (FormatSize)
                    {
                        case LiteralsFormatSize.Format0:
                        case LiteralsFormatSize.Format1:
                            RegeneratedSize = reader.ReadBitsInt32(10);
                            CompressedSize = reader.ReadBitsInt32(10);
                            break;
                        case LiteralsFormatSize.Format2:
                            RegeneratedSize = reader.ReadBitsInt32(14);
                            CompressedSize = reader.ReadBitsInt32(14);
                            break;
                        case LiteralsFormatSize.Format3:
                            RegeneratedSize = reader.ReadBitsInt32(18);
                            CompressedSize = reader.ReadBitsInt32(18);
                            break;
                    }
                    break;
            }
        }
    }
}