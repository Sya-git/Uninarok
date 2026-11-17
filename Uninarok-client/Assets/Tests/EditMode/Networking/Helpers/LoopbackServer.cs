using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Uninarok.Networking.Tests
{
    /// <summary>A throwaway TCP server on 127.0.0.1 with an OS-assigned port, for transport tests.</summary>
    public sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener _listener;

        public LoopbackServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = (ushort)((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public ushort Port { get; }

        /// <summary>Accepts the next client; fails the test if none arrives in time.</summary>
        public Socket Accept(int timeoutMs = 3000)
        {
            var task = _listener.AcceptSocketAsync();
            if (!task.Wait(timeoutMs)) throw new TimeoutException("No client connected to the loopback server");
            return task.Result;
        }

        public void Dispose() => _listener.Stop();

        /// <summary>A port nothing listens on (the listener is closed again right away).</summary>
        public static ushort ClosedPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            ushort port = (ushort)((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        /// <summary>Reads exactly <paramref name="count"/> bytes from the accepted socket.</summary>
        public static byte[] ReadExactly(Socket s, int count, int timeoutMs = 3000)
        {
            var buf = new byte[count];
            int got = 0;
            s.ReceiveTimeout = timeoutMs;
            while (got < count)
            {
                int n = s.Receive(buf, got, count - got, SocketFlags.None);
                if (n == 0) throw new InvalidOperationException($"Connection closed after {got} of {count} bytes");
                got += n;
            }
            return buf;
        }

        /// <summary>Polls until the condition holds or the time runs out.</summary>
        public static bool WaitUntil(Func<bool> condition, int timeoutMs = 3000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                Thread.Sleep(2);
            }
            return condition();
        }
    }
}
