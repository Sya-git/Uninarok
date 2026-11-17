using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Core.Dispatch;
using Uninarok.Networking.Core.Framing;
using Uninarok.Networking.Core.Protocol;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Character;
using Uninarok.Networking.Packets.In.Login;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-18, T9.x: the implemented incoming packets decode REAL server bytes (a Hercules built for PACKETVER 20220406)
    // with nothing left over. The captures were recorded with the capture tee during a real login to map and 62 s in game.
    public class RealCaptureDecodeTests
    {
        static string CapturePath(string prefix)
        {
            string dir = Path.Combine(Application.dataPath, "Tests/EditMode/Networking/Captures");
            foreach (string f in Directory.GetFiles(dir, prefix + "-*.bin")) return f;
            Assert.Inconclusive("no " + prefix + " capture");
            return null;
        }

        private sealed class Run
        {
            public readonly PacketDispatcher Dispatcher;
            public readonly List<string> DecodeErrors = new List<string>();
            public readonly Dictionary<Type, int> Counts = new Dictionary<Type, int>();
            public PacketFramer Framer;

            public Run()
            {
                Dispatcher = new PacketDispatcher(e => throw e);
            }

            public void Count<T>() where T : struct
            {
                Dispatcher.Subscribe<T>((in T p) => Counts[typeof(T)] = Counts.TryGetValue(typeof(T), out int n) ? n + 1 : 1);
            }
        }

        static Run Replay(string prefix, int skip, Action<Run> subscribe)
        {
            var run = new Run();
            subscribe(run);
            var router = new GeneratedPacketRouter(run.Dispatcher, (id, e) => run.DecodeErrors.Add("0x" + id.ToString("X4") + ": " + e.Message));
            run.Framer = new PacketFramer(HerculesPacketTable.Instance, router);
            run.Framer.SkipNextBytes(skip);
            run.Framer.Append(File.ReadAllBytes(CapturePath(prefix)));
            run.Framer.Process(int.MaxValue);
            Assert.IsFalse(run.Framer.IsFaulted, run.Framer.FaultReason);
            Assert.AreEqual(0, run.Framer.PendingBytes, "every byte was consumed");
            CollectionAssert.IsEmpty(run.DecodeErrors, "no real packet failed to decode");
            return run;
        }

        [Test]
        public void RealLoginSession_AcceptLogin_Decodes()
        {
            ACCEPT_LOGIN accept = default;
            var run = Replay("Login", 0, r => { r.Count<ACCEPT_LOGIN>(); r.Dispatcher.Subscribe<ACCEPT_LOGIN>((in ACCEPT_LOGIN p) => accept = p); });

            Assert.AreEqual(1, run.Counts[typeof(ACCEPT_LOGIN)]);
            Assert.AreEqual(2000000u, accept.AccountId, "the test account");
            Assert.AreEqual(1, accept.Servers.Length);
            Assert.AreEqual("Hercules", accept.Servers[0].Name.ToString());
            Assert.AreEqual(6121, accept.Servers[0].Port);
            Assert.AreNotEqual(0, accept.AuthCode);
        }

        [Test]
        public void RealCharacterSession_EchoSkipped_ListSlotsPinAndMapEndpointDecode()
        {
            CHARACTER_DATA list = default;
            CHARACTER_SLOTS_INFO slots = default;
            PINCODE_LOGINSTATE pin = default;
            MAP_ENTER_REQUEST_ACCEPTED map = default;
            var run = Replay("Character", 4, r =>
            {
                r.Count<CHARACTER_DATA>(); r.Count<CHARACTER_SLOTS_INFO>(); r.Count<PINCODE_LOGINSTATE>(); r.Count<MAP_ENTER_REQUEST_ACCEPTED>();
                r.Dispatcher.Subscribe<CHARACTER_DATA>((in CHARACTER_DATA p) => list = p);
                r.Dispatcher.Subscribe<CHARACTER_SLOTS_INFO>((in CHARACTER_SLOTS_INFO p) => slots = p);
                r.Dispatcher.Subscribe<PINCODE_LOGINSTATE>((in PINCODE_LOGINSTATE p) => pin = p);
                r.Dispatcher.Subscribe<MAP_ENTER_REQUEST_ACCEPTED>((in MAP_ENTER_REQUEST_ACCEPTED p) => map = p);
            });

            Assert.AreEqual(1, run.Counts[typeof(CHARACTER_DATA)]);
            Assert.AreEqual(1, list.Characters.Length, "one 175-byte record (the layout that differs from older versions)");
            CharInfo c = list.Characters[0];
            Assert.AreEqual("Boneco3", c.Name.ToString());
            Assert.AreEqual("iz_int.gat", c.MapName.ToString());
            Assert.Greater(c.MaxHp, 0L, "64-bit HP decoded");
            Assert.LessOrEqual(c.Hp, c.MaxHp);
            Assert.Greater(c.CharId, 0);

            Assert.AreEqual(1, run.Counts[typeof(CHARACTER_SLOTS_INFO)]);
            Assert.Greater(slots.CharSlots, 0);
            Assert.AreEqual(PinStateOk, pin.State, "this server reports the PIN state Ok");
            Assert.AreEqual(1, run.Counts[typeof(MAP_ENTER_REQUEST_ACCEPTED)]);
            Assert.AreEqual(c.CharId, map.CharId);
            Assert.AreEqual("iz_int.gat", map.MapName.ToString());
            Assert.AreEqual("127.0.0.1", map.FormatIpAddress());
            Assert.AreEqual(5121, map.Port);
        }

        private const short PinStateOk = 0;

        [Test]
        public void RealMapSession_AcceptanceAuthenticationAndHeartbeatResponses_Decode()
        {
            MAP_ENTRY_ACCEPTANCE accept = default;
            AUTHENTICATION_ACCEPTED auth = default;
            var run = Replay("Map", 0, r =>
            {
                r.Count<MAP_ENTRY_ACCEPTANCE>(); r.Count<AUTHENTICATION_ACCEPTED>(); r.Count<SERVER_HEARTBEAT_RESPONSE>();
                r.Dispatcher.Subscribe<MAP_ENTRY_ACCEPTANCE>((in MAP_ENTRY_ACCEPTANCE p) => accept = p);
                r.Dispatcher.Subscribe<AUTHENTICATION_ACCEPTED>((in AUTHENTICATION_ACCEPTED p) => auth = p);
            });

            Assert.AreEqual(2000000u, accept.CharacterObjectId, "ZC_NOTIFY_PLAYERID carries the account ID, as the doc says");
            Assert.AreEqual(1, run.Counts[typeof(AUTHENTICATION_ACCEPTED)]);
            Assert.Greater(auth.StartTimestamp, 0u);
            Assert.AreEqual(18, auth.Position.X);
            Assert.AreEqual(27, auth.Position.Y);
            Assert.AreEqual(UninarokDirection.North, auth.Position.Direction);
            Assert.AreEqual((5, 5), ((int)auth.XSize, (int)auth.YSize), "Hercules sends 5 and 5");

            Assert.AreEqual(6, run.Counts[typeof(SERVER_HEARTBEAT_RESPONSE)], "one reply per heartbeat, every 10 s for 62 s (FR-14)");
        }
    }
}
