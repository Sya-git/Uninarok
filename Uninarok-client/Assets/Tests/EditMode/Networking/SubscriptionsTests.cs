using System;
using NUnit.Framework;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Transport;
using UnityEngine.TestTools.Constraints;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-9: event subscriptions are always released when their owner stops being active.
    public class SubscriptionsTests
    {
        private readonly struct PacketA { }
        private readonly struct PacketB { }

        // Just enough of INetworkClient for Subscriptions; T5.3 adds the full FakeNetworkClient.
        private sealed class MinimalClient : INetworkClient
        {
            public readonly PacketDispatcher Dispatcher = new PacketDispatcher(_ => { });
            public bool Forward = true;
            public int SubscribeCalls, UnsubscribeCalls;

            public ConnectionStatus Status => ConnectionStatus.Idle;
            public ConnectionRole Role => ConnectionRole.Login;
            public void Connect(string host, ushort port, ConnectionRole role) { }
            public void Disconnect() { }
            public bool Send<T>(in T packet) where T : struct, IOutgoingPacket => false;
            public void SkipNextBytes(int count) { }
            public void Subscribe<T>(PacketHandler<T> handler) where T : struct { SubscribeCalls++; if (Forward) Dispatcher.Subscribe(handler); }
            public void Unsubscribe<T>(PacketHandler<T> handler) where T : struct { UnsubscribeCalls++; if (Forward) Dispatcher.Unsubscribe(handler); }
#pragma warning disable CS0067
            public event Action<ConnectionRole> Connected;
            public event Action<ConnectionRole, CloseInfo> Disconnected;
#pragma warning restore CS0067
        }

        [Test]
        public void Add_SubscribesNow_AndClearReleasesEverything()
        {
            var client = new MinimalClient();
            var subs = new Subscriptions(client);
            PacketHandler<PacketA> a = (in PacketA p) => { };
            PacketHandler<PacketB> b = (in PacketB p) => { };

            subs.Add(a);
            subs.Add(b);
            Assert.AreEqual(2, subs.Count);
            Assert.AreEqual(1, client.Dispatcher.HandlerCount<PacketA>());
            Assert.AreEqual(1, client.Dispatcher.HandlerCount<PacketB>());

            subs.Clear();
            Assert.AreEqual(0, subs.Count);
            Assert.AreEqual(0, client.Dispatcher.HandlerCount<PacketA>());
            Assert.AreEqual(0, client.Dispatcher.HandlerCount<PacketB>());
        }

        [Test]
        public void HundredAddClearCycles_LeaveTheHandlerCountsUnchanged()
        {
            var client = new MinimalClient();
            var subs = new Subscriptions(client);
            PacketHandler<PacketA> a = (in PacketA p) => { };
            PacketHandler<PacketB> b = (in PacketB p) => { };

            for (int i = 0; i < 100; i++)
            {
                subs.Add(a);
                subs.Add(b);
                Assert.AreEqual(1, client.Dispatcher.HandlerCount<PacketA>(), $"cycle {i}");
                subs.Clear();
                Assert.AreEqual(0, client.Dispatcher.HandlerCount<PacketA>(), $"cycle {i}");
                Assert.AreEqual(0, client.Dispatcher.HandlerCount<PacketB>(), $"cycle {i}");
            }
            Assert.AreEqual(0, subs.Count);
        }

        [Test]
        public void Clear_IsSafeToCallRepeatedly_AndWithNothingAdded()
        {
            var client = new MinimalClient();
            var subs = new Subscriptions(client);
            subs.Clear();
            PacketHandler<PacketA> a = (in PacketA p) => { };
            subs.Add(a);
            subs.Clear();
            subs.Clear();
            Assert.AreEqual(1, client.UnsubscribeCalls, "released exactly once");
        }

        [Test]
        public void AddingTheSameHandlerTwice_SubscribesOnce()
        {
            var client = new MinimalClient();
            var subs = new Subscriptions(client);
            PacketHandler<PacketA> a = (in PacketA p) => { };
            subs.Add(a);
            subs.Add(a);
            Assert.AreEqual(1, subs.Count);
            Assert.AreEqual(1, client.SubscribeCalls);
        }

        [Test]
        public void AddAfterClear_ResubscribesTheSameHandler()
        {
            var client = new MinimalClient();
            var subs = new Subscriptions(client);
            int calls = 0;
            PacketHandler<PacketA> a = (in PacketA p) => calls++;
            subs.Add(a);
            subs.Clear();
            subs.Add(a);
            client.Dispatcher.Dispatch(new PacketA());
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, subs.Count);
        }

        [Test]
        public void AfterTheFirstCycle_AddAndClear_AllocateNothing()
        {
            var client = new MinimalClient { Forward = false };     // measure Subscriptions itself, not the dispatcher's copy-on-write arrays
            var subs = new Subscriptions(client);
            PacketHandler<PacketA> a = (in PacketA p) => { };
            PacketHandler<PacketB> b = (in PacketB p) => { };
            subs.Add(a); subs.Add(b); subs.Clear();                 // first cycle creates the entries

            Assert.That(() => { subs.Add(a); subs.Add(b); subs.Clear(); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Null_Arguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new Subscriptions(null));
            var subs = new Subscriptions(new MinimalClient());
            Assert.Throws<ArgumentNullException>(() => subs.Add<PacketA>(null));
        }
    }
}
