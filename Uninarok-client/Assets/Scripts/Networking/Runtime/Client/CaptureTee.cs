using System;
using System.IO;
using Uninarok.Networking.Core.Framing;

namespace Uninarok.Networking.Client
{
    /// <summary>
    /// Copies every received byte to a file while forwarding it to the real sink, to record sessions for replay fixtures
    /// (SPEC-0001 T5.7, T9.1). Used only when <c>UNINAROK_NET_TRACE</c> is defined. Core stays engine-free, so the tee lives
    /// here. <see cref="Append"/> runs on the socket reader thread; the path is chosen on the main thread by the caller.
    /// A file that cannot be written is dropped silently: capturing never disturbs the connection.
    /// </summary>
    public sealed class CaptureTee : IInboundSink, IDisposable
    {
        private readonly IInboundSink _inner;
        private readonly object _lock = new object();
        private FileStream _stream;

        public CaptureTee(IInboundSink inner, string path)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Path = path;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            }
            catch (Exception)
            {
                _stream = null;
            }
        }

        public string Path { get; }

        /// <summary>True while the capture file is open.</summary>
        public bool IsCapturing { get { lock (_lock) return _stream != null; } }

        public bool Append(ReadOnlySpan<byte> data)
        {
            lock (_lock)
            {
                if (_stream != null)
                {
                    try
                    {
                        _stream.Write(data);
                        _stream.Flush();                         // a crash must not lose the interesting last bytes
                    }
                    catch (Exception)
                    {
                        CloseStream();
                    }
                }
            }
            return _inner.Append(data);
        }

        public void Dispose()
        {
            lock (_lock) CloseStream();
        }

        private void CloseStream()
        {
            try { _stream?.Dispose(); } catch (Exception) { }
            _stream = null;
        }

        /// <summary>File name for a capture: <c>{role}-{yyyyMMdd-HHmmss}.bin</c>.</summary>
        public static string FileName(string role, DateTime now) => role + "-" + now.ToString("yyyyMMdd-HHmmss") + ".bin";
    }
}
