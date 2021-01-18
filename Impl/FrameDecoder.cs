using System;

namespace PureZSTD.Impl
{
    public struct FrameDecoder
    {
        private enum State
        {
            Done,
            ReadFrameMagic,
            ReadFrameDescriptor,
            ReadFrameHeader,
            ReadBlockHeader,
            ReadBlockData,
        }
        private Dictionary _dictionary;
        private FrameHeaderDescriptor _frameDescriptor;
        private FrameHeader _frameHeader;
        private BlockHeader _blockHeader;
        private int _windowSize;
        private int _maxBlockSize;
        private Window _window;
        private State _state;

        public uint? Checksum { get; private set; }

        public bool Done => _state == State.Done && _window.WriteLength == 0;

        public bool NeedsFlush => _window.ReadLength == 0 && _window.WriteLength != 0;

        public Span<byte> ReadBuffer => _window.ReadBuffer;
        public int ReadLength => _window.ReadLength;
        public void ReadConsume(int length)
        {
            if (length < 0 || length > _window.ReadLength)
            {
                throw new Error.IO.NotEnoughBytes();
            }
            _window.ReadConsume(length);
            ProcessState();
        }

        public ReadOnlySpan<byte> WriteBuffer => _window.WriteBuffer;
        public int WriteLength => _window.WriteLength;
        public void WriteConsume(int length)
        {
            if (length < 0 || length > _window.WriteLength)
            {
                throw new Error.IO.NotEnoughBytes();
            }
            _window.WriteConsume(length);
            ProcessState();
        }


        public FrameDecoder(int reserved)
        {
            _dictionary = null;
            _frameDescriptor = new FrameHeaderDescriptor();
            _frameHeader = new FrameHeader();
            _blockHeader = new BlockHeader();
            _windowSize = 0;
            _maxBlockSize = 0;
            _window = new Window(reserved);
            _state = State.Done;
            Checksum = null;
        }

        public void Init(Dictionary dictionary, bool skipMagic = false)
        {
            _dictionary = dictionary;
            _frameDescriptor = new FrameHeaderDescriptor();
            _frameHeader = new FrameHeader();
            _blockHeader = new BlockHeader();
            _windowSize = 0;
            _maxBlockSize = 0;
            _window.Init(32, null);
            _window.ReadCommit(skipMagic ? 1 : 4);
            _state = skipMagic ? State.ReadFrameDescriptor : State.ReadFrameMagic;
            Checksum = null;
        }

        public void InitWithFrameHeader(Dictionary dictionary, FrameHeader header)
        {
            if (header.DictionaryID != (dictionary?.ID ?? 0))
            {
                throw new Error("Dictionary ID does not match!");
            }
            _dictionary = dictionary;
            _frameDescriptor = new FrameHeaderDescriptor();
            _frameHeader = header;
            _blockHeader = new BlockHeader();
            _windowSize = header.WindowSize;
            _windowSize = _frameHeader.WindowSize;
            _maxBlockSize = Math.Min(_frameHeader.WindowSize, 128 * 1024);
            _window.Init(_windowSize + _maxBlockSize + 4, dictionary);
            _window.ReadCommit(3);
            _state = State.ReadBlockData;
            Checksum = null;
        }

        private void ProcessState()
        {
            switch (_state)
            {
                case State.Done:
                    return;
                case State.ReadFrameMagic:
                    ProcessStateReadMagic();
                    return;
                case State.ReadFrameDescriptor:
                    ProcessStateReadFrameDescriptor();
                    return;
                case State.ReadFrameHeader:
                    ProcessStateReadFrameHeader();
                    return;
                case State.ReadBlockHeader:
                    ProcessStateReadBlockHeader();
                    return;
                case State.ReadBlockData:
                    ProcessStateReadBlockData();
                    return;
            }
        }

        private void ProcessStateReadMagic()
        {
            if (_window.ReadLength != 0)
            {
                return;
            }
            var buffer = _window.ReadTakeBack(4);
            if (buffer[0] != 0x28 || buffer[1] != 0xB5 || buffer[2] != 0x2F || buffer[3] != 0xFD)
            {
                throw new Error.BadMagic("Frame magic!");
            }
            _window.ReadCommit(1);
            _state = State.ReadFrameDescriptor;
        }

        private void ProcessStateReadFrameDescriptor()
        {
            if (_window.ReadLength != 0)
            {
                return;
            }
            var buffer = _window.ReadTakeBack(1);
            _frameDescriptor = FrameHeaderDescriptor.Create(buffer[0]);
            _window.ReadCommit(_frameDescriptor.HeaderSize);
            _state = State.ReadFrameHeader;
        }

        private void ProcessStateReadFrameHeader()
        {
            if (_window.ReadLength != 0)
            {
                return;
            }
            var buffer = _window.ReadTakeBack(_frameDescriptor.HeaderSize);
            var frameHeader = FrameHeader.Create(_frameDescriptor, buffer);
            InitWithFrameHeader(_dictionary, frameHeader);
        }

        private void ProcessStateReadBlockHeader()
        {
            var prefetchLength = !_blockHeader.LastBlock ? 3 : _frameHeader.ContentChecksum ? 4 : 0;
            switch (_blockHeader.Kind)
            {
                case BlockKind.Raw:
                    if (!_window.TryFlush(_windowSize, _blockHeader.Size + prefetchLength))
                    {
                        return;
                    }
                    _window.ReadCommit(_blockHeader.Size + prefetchLength);
                    break;
                case BlockKind.RLE:
                    if (!_window.TryFlush(_windowSize, _blockHeader.Size + prefetchLength))
                    {
                        return;
                    }
                    _window.ReadCommit(1 + prefetchLength);
                    break;
                case BlockKind.Compressed:
                    if (!_window.TryFlush(_windowSize, _maxBlockSize + prefetchLength))
                    {
                        return;
                    }
                    _window.ReadCommit(_blockHeader.Size + prefetchLength);
                    break;
                default:
                    throw new Error.InvalidState();
            }
            _state = State.ReadBlockData;
        }

        private void ProcessStateReadBlockData()
        {
            if (_window.ReadLength != 0)
            {
                return;
            }
            var prefetchLength = !_blockHeader.LastBlock ? 3 : _frameHeader.ContentChecksum ? 4 : 0;
            var prefetchBuffer = _window.ReadTakeBack(prefetchLength).ToArray();
            switch (_blockHeader.Kind)
            {
                case BlockKind.Raw:
                    _window.WriteCommitRaw(_blockHeader.Size);
                    break;
                case BlockKind.RLE:
                    _window.WriteCommitRLE(_blockHeader.Size);
                    break;
                case BlockKind.Compressed:
                    _window.WriteCommitDecompress(_windowSize, _blockHeader.Size);
                    break;
                default:
                    throw new Error.InvalidState();
            }
            switch (prefetchLength)
            {
                case 4:
                    _state = State.Done;
                    Checksum = Utility.ReadUInt32(prefetchBuffer);
                    break;
                case 3:
                    _blockHeader = BlockHeader.Create(prefetchBuffer);
                    _state = State.ReadBlockHeader;
                    ProcessStateReadBlockHeader();
                    break;
                case 0:
                    _state = State.Done;
                    break;
                default:
                    throw new Error.InvalidState();
            }
        }
    }
}