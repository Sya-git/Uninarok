using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;

namespace Uninarok.Networking.Tests
{
    // T8.1 / SPEC-0001 FR-6: the socket threads announce themselves so the Runtime can register them with the Profiler.
    public class SocketThreadHookTests
    {
        private Action<string> _savedStart;
        private Action _savedStop;

        [SetUp]
        public void SetUp()
        {
            _savedStart = TcpConnection.SocketThreadStarted;
            _savedStop = TcpConnection.SocketThreadStopping;
        }

        [TearDown]
        public void TearDown()
        {
            TcpConnection.SocketThreadStarted = _savedStart;
            TcpConnection.SocketThreadStopping = _savedStop;
        }

        private static TcpConnection Connect(LoopbackServer server, ConnectionRole role)
            => TcpConnection.ConnectAsync("127.0.0.1", server.Port, role, new CollectingSink(), CancellationToken.None).GetAwaiter().GetResult();

        [Test]
        public void BothThreads_ReportTheirNames_OnStart_AndStopOnClose()
        {
            var started = new List<string>();
            int stopped = 0;
            TcpConnection.SocketThreadStarted = name => { lock (started) started.Add(name); };
            TcpConnection.SocketThreadStopping = () => Interlocked.Increment(ref stopped);

            using var server = new LoopbackServer();
            var conn = Connect(server, ConnectionRole.Character);
            using var peer = server.Accept();
            Assert.IsTrue(LoopbackServer.WaitUntil(() => { lock (started) return started.Count == 2; }), "both threads announce themselves");

            lock (started) CollectionAssert.AreEquivalent(new[] { "Uninarok.Net.Character.Read", "Uninarok.Net.Character.Write" }, started);

            conn.Dispose();
            Assert.IsTrue(LoopbackServer.WaitUntil(() => Volatile.Read(ref stopped) == 2), "both threads announce that they stop");
        }

        [Test]
        public void TheHooksRunOnTheSocketThreads_NotOnTheCallingThread()
        {
            var threadIds = new List<int>();
            TcpConnection.SocketThreadStarted = name => { lock (threadIds) threadIds.Add(Thread.CurrentThread.ManagedThreadId); };

            using var server = new LoopbackServer();
            var conn = Connect(server, ConnectionRole.Login);
            using var peer = server.Accept();
            Assert.IsTrue(LoopbackServer.WaitUntil(() => { lock (threadIds) return threadIds.Count == 2; }));
            lock (threadIds)
            {
                CollectionAssert.DoesNotContain(threadIds, Thread.CurrentThread.ManagedThreadId);
                CollectionAssert.AllItemsAreUnique(threadIds);
            }
            conn.Dispose();
        }

        [Test]
        public void AHookThatThrows_DoesNotTakeTheConnectionDown()
        {
            TcpConnection.SocketThreadStarted = name => throw new InvalidOperationException("profiler broke");
            TcpConnection.SocketThreadStopping = () => throw new InvalidOperationException("profiler broke");

            using var server = new LoopbackServer();
            var conn = Connect(server, ConnectionRole.Map);
            using var peer = server.Accept();
            peer.Send(new byte[] { 1, 2, 3 });
            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.LiveThreads == 2));
            Assert.IsFalse(conn.IsClosed, "a throwing hook must not close the connection");

            conn.Dispose();
            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.LiveThreads == 0), "and both threads still exit cleanly");
        }

        [Test]
        public void NoHooksInstalled_IsTheDefault_AndWorks()
        {
            TcpConnection.SocketThreadStarted = null;
            TcpConnection.SocketThreadStopping = null;
            using var server = new LoopbackServer();
            var conn = Connect(server, ConnectionRole.Login);
            using var peer = server.Accept();
            conn.Dispose();
            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.LiveThreads == 0));
        }

        [Test]
        public void SocketThreadCounters_StartedAndStopped_ReturnToZeroRunning_AfterEveryConnectionIsClosed()
        {
            NetProfiling.Install();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int startedBefore = NetProfiling.SocketThreadsStarted, stoppedBefore = NetProfiling.SocketThreadsStopped;
            using var server = new LoopbackServer();
            var peers = new System.Collections.Generic.List<System.Net.Sockets.Socket>();
            for (int i = 0; i < 5; i++)
            {
                var conn = Connect(server, ConnectionRole.Map);
                peers.Add(server.Accept());
                Assert.IsTrue(LoopbackServer.WaitUntil(() => NetProfiling.SocketThreadsRunning >= 2));
                conn.Dispose();
            }
            Assert.IsTrue(LoopbackServer.WaitUntil(() => NetProfiling.SocketThreadsStopped - stoppedBefore == 10), "10 threads announced that they stop");
            Assert.AreEqual(10, NetProfiling.SocketThreadsStarted - startedBefore, "two threads per connection");
            Assert.AreEqual(startedBefore - stoppedBefore, NetProfiling.SocketThreadsRunning, "nothing from these connections is still running");
            foreach (var p in peers) p.Close();
#endif
        }

        // ---- the Runtime side ----

        [TestCase("Uninarok.Net.Login.Read", "Login.Read")]
        [TestCase("Uninarok.Net.Map.Write", "Map.Write")]
        [TestCase("Something.Else", "Something.Else")]
        [TestCase("", "NetThread")]
        [TestCase(null, "NetThread")]
        public void ProfilerThreadName_DropsTheCommonPrefix(string threadName, string expected)
        {
            Assert.AreEqual(expected, NetProfiling.ProfilerThreadName(threadName));
        }

        [Test]
        public void Install_IsIdempotent_AndWiresTheHooksInTheEditor()
        {
            NetProfiling.Install();
            NetProfiling.Install();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.IsNotNull(TcpConnection.SocketThreadStarted);
            Assert.IsNotNull(TcpConnection.SocketThreadStopping);
#endif
            Assert.AreEqual("Uninarok.Net", NetProfiling.ThreadGroup);
        }

        [Test]
        public void ARealConnection_WithTheProfilerHooksInstalled_RunsAndClosesCleanly()
        {
            NetProfiling.Install();
            using var server = new LoopbackServer();
            var conn = Connect(server, ConnectionRole.Login);
            using var peer = server.Accept();
            peer.Send(new byte[] { 9 });
            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.LiveThreads == 2));
            conn.Dispose();
            Assert.IsTrue(LoopbackServer.WaitUntil(() => conn.LiveThreads == 0), "BeginThreadProfiling/EndThreadProfiling pair up without errors");
        }
    }
}
