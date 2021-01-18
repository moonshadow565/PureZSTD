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

        public static int DecompressInterleaved2(FSETable table, ref BitReaderReverse bitReader, Span<int> output)
        {
            var count = 0;
            var decoder1 = new FSEDecoder(table);
            var decoder2 = new FSEDecoder(table);

            decoder1.InitState(ref bitReader);
            decoder2.InitState(ref bitReader);
            while (true)
            {
                output[count++] = decoder1.PeekState();
                decoder1.UpdateState(ref bitReader);
                if (bitReader.Position < 0)
                {
                    output[count++] = decoder2.PeekState();
                    break;
                }

                output[count++] = decoder2.PeekState();
                decoder2.UpdateState(ref bitReader);
                if (bitReader.Position < 0)
                {
                    output[count++] = decoder1.PeekState();
                    break;
                }
            }

            return count;
        }
    }
}