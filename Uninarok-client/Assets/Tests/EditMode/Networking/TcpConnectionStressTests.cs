using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Uninarok.Networking.Core.Transport;

namespace Uninarok.Networking.Tests
{
    // T3.8 / SPEC-0001 FR-7: closing never leaks a socket thread and never throws on a thread.
    public class TcpConnectionStressTests
    {
        private sealed class BusySink : Uninarok.Networking.Core.Framing.IInboundSink
        {
            public bool Append(ReadOnlySpan<byte> data) => true;
        }

        [Test]
        public void HundredConnectDisposeCycles_NoExceptionAndNoLeakedThread()
        {
            var unhandled = new List<Exception>();
            UnhandledExceptionEventHandler onUnhandled = (_, e) => { lock (unhandled) unhandled.Add((Exception)e.ExceptionObject); };
            AppDomain.CurrentDomain.UnhandledException += onUnhandled;
            try
            {
                using var server = new LoopbackServer();
                var all = new List<TcpConnection>();
                for (int i = 0; i < 100; i++)
                {
                    var conn = TcpConnection.ConnectAsync("127.0.0.1", server.Port, ConnectionRole.Map, new BusySink(), CancellationToken.None)
                                            .GetAwaiter().GetResult();
                    var peer = server.Accept();
                    all.Add(conn);

                    switch (i % 4)
                    {
                        case 0:                                        // plain dispose
                            break;
                        case 1:                                        // dispose while traffic flows in both directions
                            peer.Send(new byte[1024]);
                            conn.Send(new byte[1024]);
                            break;
                        case 2:                                        // remote close first, dispose afterwards
                            peer.Close();
                            LoopbackServer.WaitUntil(() => conn.IsClosed, 1000);
                            break;
                        case 3:                                        // dispose with a large unsent backlog
                            for (int k = 0; k < 20; k++) conn.Send(new byte[32 * 1024]);
                            break;
                    }

                    conn.Dispose();
                    conn.Dispose();                                    // idempotent
                    Assert.IsTrue(conn.IsClosed);
                    peer.Close();
                }

                foreach (var c in all)
                    Assert.IsTrue(LoopbackServer.WaitUntil(() => c.LiveThreads == 0, 5000), "a socket thread did not exit");

                Thread.Sleep(50);                                      // let any late thread exception surface
                lock (unhandled) Assert.AreEqual(0, unhandled.Count, "unhandled exception on a socket thread: " + (unhandled.Count > 0 ? unhandled[0] : null));
            }
            finally
            {
                AppDomain.CurrentDomain.UnhandledException -= onUnhandled;
            }
        }

        [Test]
        public void SendFromManyThreads_WhileDisposing_NeverThrows()
        {
            using var server = new LoopbackServer();
            var conn = TcpConnection.ConnectAsync("127.0.0.1", server.Port, ConnectionRole.Map, new BusySink(), CancellationToken.None)
                                    .GetAwaiter().GetResult();
            using var peer = server.Accept();

            Exception failure = null;
            var senders = new List<Thread>();
            for (int t = 0; t < 4; t++)
            {
                var th = new Thread(() =>
                {
                    try { for (int i = 0; i < 5000; i++) conn.Send(new byte[16]); }
                    catch (Exception e) { failure = e; }
                });
                senders.Add(th);
                th.Start();
            }
            Thread.Sleep(5);
            conn.Dispose();
            foreach (var th in senders) th.Join();

            Assert.IsNull(failure, "Send must never throw because of connection state: " + failure);
            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.LiveThreads == 0, 5000));
        }
    }
}
