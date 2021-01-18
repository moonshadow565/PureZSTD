using System;

namespace PureZSTD.Impl
{
    public class Error : Exception
    {
        public Error(string message) : base(message) { }

        public class IO : Error
        {
            public IO(string message) : base(message) { }

            public class UnalignedAccess : IO
            {
                public UnalignedAccess() : base("Unaligned access") { }
            }

            public class InvalidBitSize : IO
            {
                public InvalidBitSize() : base("Invalid bit size") { }
            }

            public class NotEnoughBytes : IO
            {
                public NotEnoughBytes() : base("Not enough bytes") { }
            }

            public class NotEnoughBits : IO
            {
                public NotEnoughBits() : base("Not enough bits") { }
            }
        }

        public class Reserved : Error
        {
            public Reserved(string name) : base($"Reserved bits used for {name}") { }
        }

        public class BadMagic : Error
        {
            public BadMagic(string name) : base($"Bad magic for {name}") { }
        }

        public class OutOfRange : Error
        {
            public OutOfRange(string name) : base($"Out of range for {name}") { }
        }

        public class Corruption : Error
        {
            public Corruption(string message) : base(message) { }
        }

        public class InvalidState : Exception
        {
            public InvalidState() : base("If you see this something has gone teribly wrong!") { }
        }
    }
}