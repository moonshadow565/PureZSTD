using System;
using System.Collections.Generic;

namespace PureZSTD.Impl
{
    public class Dictionary
    {
        private readonly uint _id = 0;
        private readonly byte[] _content = new byte[0];
        private readonly HUFTable _tableLiterals = null;
        private readonly FSETable _tableSequenceLiteralLength = null;
        private readonly FSETable _tableSequenceOffset = null;
        private readonly FSETable _tableSquenceMatchLength = null;
        private readonly long[] _previousOffsets = new long[0];

        public uint ID => _id;
        public ReadOnlyMemory<byte> Content => _content;
        public HUFTable TableLiterals => _tableLiterals;
        public FSETable TableSequenceLiteralLength => _tableSequenceLiteralLength;
        public FSETable TableSequenceOffset => _tableSequenceOffset;
        public FSETable TableSequenceMatchLength => _tableSquenceMatchLength;
        public ReadOnlyMemory<long> PreviousOffsets => _previousOffsets;
    }
}