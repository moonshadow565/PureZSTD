using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public ref struct HUFDecoder
    {
        public readonly HUFTable Table;
        private int _state;

        public HUFDecoder(HUFTable table)
        {
            Table = table;
            _state = 0;
        }

        public void InitState(ref BitReaderReverse bitReader)
        {
            _state = Table.Init(ref bitReader);
        }

        public byte PeekState()
        {
            return Table.Peek(_state);
        }

        public void UpdateState(ref BitReaderReverse bitReader)
        {
            _state = Table.Update(ref bitReader, _state);
        }
    }
}