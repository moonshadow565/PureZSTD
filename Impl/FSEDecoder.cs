using System;
using System.Collections.Generic;
using System.Numerics;

namespace PureZSTD.Impl
{
    public ref struct FSEDecoder
    {
        public readonly FSETable Table;
        private int _state;

        public FSEDecoder(FSETable table)
        {
            Table = table;
            _state = 0;
        }

        public void InitState(ref BitReaderReverse reader)
        {
            _state = Table.InitState(ref reader);
        }

        public void UpdateState(ref BitReaderReverse reader)
        {
            _state = Table.Update(ref reader, _state);
        }

        public byte PeekState()
        {
            return Table.Peek(_state);
        }
    }
}