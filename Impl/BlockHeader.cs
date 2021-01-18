using System;

namespace PureZSTD.Impl
{
    public enum BlockKind
    {
        Raw,
        RLE,
        Compressed,
    }

    public struct BlockHeader
    {
        public bool LastBlock { get; set; }
        public BlockKind Kind { get; set; }
        public int Size { get; set; }

        public static BlockHeader Create(int raw)
        {
            if (raw < 0 || raw > 0xFFFFFF)
            {
                throw new Error.OutOfRange(nameof(raw));
            }
            var result = new BlockHeader();
            result.LastBlock = (raw & 1) != 0;
            var kind = (raw >> 1) & 3;
            if (kind == 3)
            {
                throw new Error.Reserved(nameof(BlockKind));
            }
            result.Kind = (BlockKind)kind;
            result.Size = raw >> 3;
            return result;
        }

        public static BlockHeader Create(ReadOnlySpan<byte> data)
        {
            if (data.Length < 3)
            {
                throw new Error.OutOfRange("Block header must be 3 bytes!");
            }
            var raw = data[0] | (data[1] << 8) | (data[2] << 16);
            return Create(raw);
        }
    }
}