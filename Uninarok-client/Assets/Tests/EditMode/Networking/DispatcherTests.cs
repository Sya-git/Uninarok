#if PENDING_T3_2 // T3.1: enabled (guard removed) by T3.2 when PacketDispatcher exists.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Networking.Core.Dispatch;
using UnityEngine.TestTools.Constraints;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-4 (handler isolation), FR-6 (no allocation), FR-9 (subscriptions are released).
    public class DispatcherTests
    {
        private readonly struct PacketA { public readonly int Value; public PacketA(int v) { Value = v; } }
        private readonly struct PacketB { public readonly int Value; public PacketB(int v) { Value = v; } }

        private readonly List<Exception> _errors = new List<Exception>();
        private PacketDispatcher _d;

        [SetUp]
        public void SetUp()
        {
            _errors.Clear();
            _d = new PacketDispatcher(e => _errors.Add(e));
        }

        [Test]
        public void Dispatch_CallsHandlers_InSubscriptionOrder()
        {
            var log = new List<string>();
            _d.Subscribe<PacketA>((in PacketA p) => log.Add("one" + p.Value));
            _d.Subscribe<PacketA>((in PacketA p) => log.Add("two" + p.Value));
            _d.Dispatch(new PacketA(7));
            CollectionAssert.AreEqual(new[] { "one7", "two7" }, log);
        }

        [Test]
        public void Dispatch_OnlyReachesHandlersOfThatType()
        {
            int a = 0, b = 0;
            _d.Subscribe<PacketA>((in PacketA p) => a++);
            _d.Subscribe<PacketB>((in PacketB p) => b++);
            _d.Dispatch(new PacketA(1));
            Assert.AreEqual((1, 0), (a, b));
        }

        [Test]
        public void Dispatch_WithoutSubscribers_IsANoOp()
        {
            Assert.DoesNotThrow(() => _d.Dispatch(new PacketA(1)));
            Assert.AreEqual(0, _errors.Count);
        }

        [Test]
        public void ThrowingHandler_DoesNotBlockTheNextHandler_AndIsReportedOnce()
        {
            int after = 0;
            _d.Subscribe<PacketA>((in PacketA p) => throw new InvalidOperationException("boom"));
            _d.Subscribe<PacketA>((in PacketA p) => after++);

            _d.Dispatch(new PacketA(1));
            Assert.AreEqual(1, after);
            Assert.AreEqual(1, _errors.Count);
            Assert.IsInstanceOf<InvalidOperationException>(_errors[0]);

            _d.Dispatch(new PacketA(2));                   // the stream keeps flowing
            Assert.AreEqual(2, after);
            Assert.AreEqual(2, _errors.Count);
        }

        [Test]
        public void Subscribe_SameHandlerTwice_IsIgnored()
        {
            int calls = 0;
            PacketHandler<PacketA> h = (in PacketA p) => calls++;
            _d.Subscribe(h);
            _d.Subscribe(h);
            Assert.AreEqual(1, _d.HandlerCount<PacketA>());
            _d.Dispatch(new PacketA(1));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Unsubscribe_MissingHandler_IsIgnored()
        {
            PacketHandler<PacketA> h = (in PacketA p) => { };
            Assert.DoesNotThrow(() => _d.Unsubscribe(h));
            Assert.AreEqual(0, _d.HandlerCount<PacketA>());
        }

        [Test]
        public void UnsubscribeDuringDispatch_AppliesFromTheNextPacket()
        {
            int secondCalls = 0;
            PacketHandler<PacketA> second = (in PacketA p) => secondCalls++;
            PacketHandler<PacketA> first = null;
            first = (in PacketA p) => _d.Unsubscribe(second);
            _d.Subscribe(first);
            _d.Subscribe(second);

            _d.Dispatch(new PacketA(1));
            Assert.AreEqual(1, secondCalls, "the callback list read at the start of the dispatch is still used");
            _d.Dispatch(new PacketA(2));
            Assert.AreEqual(1, secondCalls, "removed handler is not called from the next packet on");
        }

        [Test]
        public void SubscribeDuringDispatch_AppliesFromTheNextPacket()
        {
            int lateCalls = 0;
            PacketHandler<PacketA> late = (in PacketA p) => lateCalls++;
            bool added = false;
            _d.Subscribe<PacketA>((in PacketA p) => { if (!added) { added = true; _d.Subscribe(late); } });

            _d.Dispatch(new PacketA(1));
            Assert.AreEqual(0, lateCalls, "not called in the dispatch that added it");
            _d.Dispatch(new PacketA(2));
            Assert.AreEqual(1, lateCalls);
        }

        [Test]
        public void HundredSubscribeUnsubscribeCycles_LeaveTheCountUnchanged()
        {
            PacketHandler<PacketA> h = (in PacketA p) => { };
            PacketHandler<PacketB> g = (in PacketB p) => { };
            _d.Subscribe(h);
            int before = _d.HandlerCount<PacketA>();
            for (int i = 0; i < 100; i++)
            {
                _d.Subscribe(g);
                _d.Unsubscribe(g);
            }
            Assert.AreEqual(before, _d.HandlerCount<PacketA>());
            Assert.AreEqual(0, _d.HandlerCount<PacketB>());
        }

        [Test]
        public void Dispatch_WithThreeSubscribers_AllocatesNothing()
        {
            int sum = 0;
            _d.Subscribe<PacketA>((in PacketA p) => sum += p.Value);
            _d.Subscribe<PacketA>((in PacketA p) => sum += p.Value);
            _d.Subscribe<PacketA>((in PacketA p) => sum += p.Value);
            var packet = new PacketA(1);
            _d.Dispatch(in packet);                         // warm-up

            Assert.That(() => { _d.Dispatch(in packet); }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(6, sum);
        }

        [Test]
        public void TwoDispatchersAreIndependent()
        {
            int calls = 0;
            var other = new PacketDispatcher(_ => { });
            other.Subscribe<PacketA>((in PacketA p) => calls++);
            _d.Dispatch(new PacketA(1));
            Assert.AreEqual(0, calls);
            Assert.AreEqual(0, _d.HandlerCount<PacketA>());
            Assert.AreEqual(1, other.HandlerCount<PacketA>());
        }
    }
}
#endif
