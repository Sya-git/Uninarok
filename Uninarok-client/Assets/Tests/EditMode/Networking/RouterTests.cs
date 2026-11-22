using System;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Framing;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Login;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-4, FR-19; plan 4.6. LOGIN_SERVER_NOTICE (0x0081, 3 bytes, one field) is the probe packet.
    //
    // Plan 4.6 said the generator also runs on the test assembly; in Unity it does not: an analyzer applies to the
    // assemblies at or below its folder, so GeneratedPacketRouter exists only in Uninarok.Networking. These tests use it from there.
    public class RouterTests
    {
        private PacketDispatcher _dispatcher;
        private GeneratedPacketRouter _router;
        private readonly List<byte> _noticeCodes = new List<byte>();
        private readonly List<(ushort Id, PacketFormatException Error)> _decodeErrors = new List<(ushort, PacketFormatException)>();
        private readonly List<Exception> _handlerErrors = new List<Exception>();

        [SetUp]
        public void SetUp()
        {
            _noticeCodes.Clear();
            _decodeErrors.Clear();
            _handlerErrors.Clear();
            _dispatcher = new PacketDispatcher(e => _handlerErrors.Add(e));
            _router = new GeneratedPacketRouter(_dispatcher, (id, e) => _decodeErrors.Add((id, e)));
            _dispatcher.Subscribe<LOGIN_SERVER_NOTICE>((in LOGIN_SERVER_NOTICE p) => _noticeCodes.Add(p.ErrorCode));
        }

        [Test]
        public void ValidPacket_IsDecoded_AndDispatched()
        {
            Assert.IsTrue(_router.Route(PacketIds.LOGIN_SERVER_NOTICE, new byte[] { 8 }));
            CollectionAssert.AreEqual(new byte[] { 8 }, _noticeCodes);
            Assert.AreEqual(0, _decodeErrors.Count);
        }

        [Test]
        public void MalformedPayload_ReportsDecodeErrorOnce_DispatchesNothing_AndNextPacketIsHandled()
        {
            Assert.IsTrue(_router.Route(PacketIds.LOGIN_SERVER_NOTICE, ReadOnlySpan<byte>.Empty));   // one byte is missing
            Assert.AreEqual(1, _decodeErrors.Count);
            Assert.AreEqual(PacketIds.LOGIN_SERVER_NOTICE, _decodeErrors[0].Id);
            Assert.AreEqual(0, _noticeCodes.Count, "handlers never see a half-built struct");

            Assert.IsTrue(_router.Route(PacketIds.LOGIN_SERVER_NOTICE, new byte[] { 1 }));
            CollectionAssert.AreEqual(new byte[] { 1 }, _noticeCodes);
            Assert.AreEqual(1, _decodeErrors.Count, "still exactly one decode error");
        }

        [Test]
        public void UnhandledId_ReturnsFalse_WithoutAnyError()
        {
            Assert.IsFalse(_router.Route(0x0999, new byte[] { 1, 2, 3 }));
            Assert.AreEqual(0, _decodeErrors.Count);
            Assert.AreEqual(0, _handlerErrors.Count);
        }

        [Test]
        public void ThrowingHandler_IsIsolatedByTheDispatcher_NotReportedAsDecodeError()
        {
            _dispatcher.Subscribe<LOGIN_SERVER_NOTICE>((in LOGIN_SERVER_NOTICE p) => throw new InvalidOperationException("boom"));
            Assert.IsTrue(_router.Route(PacketIds.LOGIN_SERVER_NOTICE, new byte[] { 3 }));
            Assert.AreEqual(1, _handlerErrors.Count);
            Assert.AreEqual(0, _decodeErrors.Count);
            CollectionAssert.AreEqual(new byte[] { 3 }, _noticeCodes, "the handler before the throwing one still ran");
        }

        [Test]
        public void HandledIds_AreSortedAscending_AndContainTheProbe()
        {
            var ids = GeneratedPacketRouter.HandledIds.ToArray();
            CollectionAssert.IsOrdered(ids);
            CollectionAssert.Contains(ids, PacketIds.LOGIN_SERVER_NOTICE);
            CollectionAssert.AllItemsAreUnique(ids);
        }

        [Test]
        public void EveryIncomingPacketInScope_IsRouted()
        {
            ushort[] expected =
            {
                PacketIds.LOGIN_REFUSAL, PacketIds.LOGIN_SERVER_NOTICE, PacketIds.ACCEPT_LOGIN, PacketIds.REFUSE_LOGIN_3,
                PacketIds.CHARACTER_DATA, PacketIds.MAP_ENTER_REQUEST_REFUSED, PacketIds.CHARACTER_CREATION_SUCCESS,
                PacketIds.CHARACTER_CREATION_FAILED, PacketIds.CHARACTER_SLOTS_INFO, PacketIds.PINCODE_LOGINSTATE,
                PacketIds.MAP_ENTER_REQUEST_ACCEPTED,
                PacketIds.MAP_ENTRY_REFUSAL, PacketIds.SERVER_HEARTBEAT_RESPONSE, PacketIds.MAP_ENTRY_ACCEPTANCE,
                PacketIds.AUTHENTICATION_ACCEPTED,
                // SPEC-0010
                PacketIds.UNIT_VANISH, PacketIds.UNIT_MOVE, PacketIds.PLAYER_MOVE, PacketIds.UNIT_STOP_MOVE, PacketIds.UNIT_SLIDE,
                PacketIds.UNIT_MOVE_ENTRY, PacketIds.UNIT_NEW_ENTRY, PacketIds.UNIT_STAND_ENTRY,
            };
            var handled = GeneratedPacketRouter.HandledIds.ToArray();
            CollectionAssert.AreEquivalent(expected, handled);
        }

        [Test]
        public void EveryHandledId_IsKnownToTheHerculesTable()
        {
            foreach (ushort id in GeneratedPacketRouter.HandledIds.ToArray())
                Assert.AreNotEqual(-2, HerculesPacketTable.Instance.GetLength(id), $"0x{id:X4} is routed but absent from the length table");
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new GeneratedPacketRouter(null, (id, e) => { }));
            Assert.Throws<ArgumentNullException>(() => new GeneratedPacketRouter(_dispatcher, null));
        }

        [Test]
        public void ThroughTheFramer_AKnownButUnhandledPacketIsSkipped_AndTheNextOneIsHandled()
        {
            // 0x0066 is in the Hercules table (3 bytes) but has no decoder: FR-19.
            var framer = new PacketFramer(HerculesPacketTable.Instance, _router);
            framer.Append(Wire.Concat(Wire.Fixed(0x0066, 9), Wire.Fixed(PacketIds.LOGIN_SERVER_NOTICE, 8)));

            Assert.AreEqual(2, framer.Process(10));
            Assert.IsFalse(framer.IsFaulted);
            CollectionAssert.AreEqual(new byte[] { 8 }, _noticeCodes);
        }
    }
}
