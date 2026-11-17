using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Framing;
using Uninarok.Networking.Packets;

namespace Uninarok.Networking.Tests
{
    // Packet tracing switch on NetworkClient (log of sent and received packets).
    public class PacketTraceTests
    {
        private sealed class Inner : IPacketRouter
        {
            public bool Result = true;
            public int Calls;
            public bool Route(ushort packetId, ReadOnlySpan<byte> payload) { Calls++; return Result; }
        }

        [Test]
        public void TracingRouter_LogsTheIdNameAndSize_AndForwardsTheResult()
        {
            var inner = new Inner();
            var router = new TracingRouter(inner);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[Net\.Packet\] <- 0x0360 HEARTBEAT_REQUEST \(payload 4 B\)$"));
            Assert.IsTrue(router.Route(PacketIds.HEARTBEAT_REQUEST, new byte[4]));
            Assert.AreEqual(1, inner.Calls);
        }

        [Test]
        public void TracingRouter_MarksAPacketWithoutHandler()
        {
            var router = new TracingRouter(new Inner { Result = false });
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"no handler"));
            Assert.IsFalse(router.Route(0x7777, new byte[0]));
        }

        [Test]
        public void NameOf_KnownIdsUseTheirConstantName_UnknownIdsSayUnknown()
        {
            Assert.AreEqual(nameof(PacketIds.MAP_LOADED), PacketTrace.NameOf(PacketIds.MAP_LOADED));
            Assert.AreEqual("UNKNOWN", PacketTrace.NameOf(0x7777));
        }

        [Test]
        public void TracePackets_IsOffByDefault()
        {
            var go = new GameObject("trace-default");
            try { Assert.IsFalse(go.AddComponent<NetworkClient>().TracePackets); }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
