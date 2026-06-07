using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Xiaomi_Flash.Compression
{
    /// <summary>
    /// Stream de descompresión XZ/LZMA usando liblzma.dll incluido en el proyecto.
    /// </summary>
    public sealed class LzmaInputStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly bool _leaveOpen;
        private LzmaStream _lzma;
        private bool _decoderInitialized;
        private readonly byte[] _inputBuffer = new byte[65536];
        private readonly byte[] _outputBuffer = new byte[65536];
        private int _outputPos;
        private int _outputAvail;
        private bool _inputFinished;
        private GCHandle _inputPin;
        private bool _disposed;

        public LzmaInputStream(Stream stream, bool leaveOpen = false)
        {
            _baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;
            _lzma = default;
            LzmaNative.AutoDecoder(ref _lzma, ulong.MaxValue, 0);
            _decoderInitialized = true;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LzmaInputStream));

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();
            if (count == 0)
                return 0;

            int totalRead = 0;
            while (totalRead < count)
            {
                if (_outputAvail > 0)
                {
                    int toCopy = Math.Min(_outputAvail, count - totalRead);
                    Buffer.BlockCopy(_outputBuffer, _outputPos, buffer, offset + totalRead, toCopy);
                    _outputPos += toCopy;
                    _outputAvail -= toCopy;
                    totalRead += toCopy;
                    continue;
                }

                if (_inputFinished)
                    return totalRead;

                if (_lzma.avail_in == UIntPtr.Zero)
                {
                    int bytesRead = _baseStream.Read(_inputBuffer, 0, _inputBuffer.Length);
                    if (bytesRead == 0)
                    {
                        _inputFinished = true;
                    }
                    else
                    {
                        if (_inputPin.IsAllocated)
                            _inputPin.Free();

                        _inputPin = GCHandle.Alloc(_inputBuffer, GCHandleType.Pinned);
                        _lzma.next_in = _inputPin.AddrOfPinnedObject();
                        _lzma.avail_in = (UIntPtr)(uint)bytesRead;
                    }
                }

                if (_inputFinished && _lzma.avail_in == UIntPtr.Zero)
                    return totalRead;

                GCHandle outputPin = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
                try
                {
                    _lzma.next_out = outputPin.AddrOfPinnedObject();
                    _lzma.avail_out = (UIntPtr)(uint)_outputBuffer.Length;

                    LzmaAction action = _inputFinished && _lzma.avail_in == UIntPtr.Zero
                        ? LzmaAction.Finish
                        : LzmaAction.Run;

                    LzmaResult result = LzmaNative.Code(ref _lzma, action);
                    int produced = _outputBuffer.Length - (int)_lzma.avail_out.ToUInt32();
                    _outputPos = 0;
                    _outputAvail = produced;

                    if (_lzma.avail_in == UIntPtr.Zero && _inputPin.IsAllocated)
                    {
                        _inputPin.Free();
                    }

                    if (result == LzmaResult.StreamEnd)
                        _inputFinished = true;
                    else if (result != LzmaResult.Ok && result != LzmaResult.BufError)
                        throw new InvalidDataException("lzma_code failed: " + result);

                    if (produced == 0 && _inputFinished)
                        return totalRead;
                }
                finally
                {
                    if (outputPin.IsAllocated)
                        outputPin.Free();
                }
            }

            return totalRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_decoderInitialized)
                {
                    LzmaNative.End(ref _lzma);
                    _decoderInitialized = false;
                }

                if (_inputPin.IsAllocated)
                    _inputPin.Free();

                if (disposing && !_leaveOpen)
                    _baseStream.Dispose();

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
