using System;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0010 FR-4 to FR-9, FR-13, FR-15, FR-16, FR-19 (plan 4.1): the world packets decode their sample with nothing left
    // over, and the hot ones allocate nothing. Samples are hand-built from the docs unless a capture is named.
    public partial class PacketDecodeTests
    {
        const int Decodes = 1000;

        static byte[] Hex(string s)
        {
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }

        // The fields before the position, shared by the three entries; the moving one has the start time after head bottom.
        const string EntryHead = "00" + "81841E00" + "01020000" + "9600" + "0000" + "0000" + "40000000" + "A10F" + "0300" + "B1040000" + "35080000" + "0500";
        const string EntryMiddle = "1000" + "0A00" + "0700" + "0200" + "0200" + "0300" + "11000000" + "0100" + "0200" + "04000000" + "01" + "01";
        const string EntryTail = "6300" + "0000" + "10270000" + "E8030000" + "00" + "0100" + "506F72696E6700000000000000000000" + "0000000000000000";
        // 150,200 facing south
        const string At = "258C84";
        // 150,200 to 155,198, sub 8/8
        const string Move = "258C826CC688";

        // ---- UNIT_STAND_ENTRY (0x09FF, variable, 108) ----

        [Test]
        public void UNIT_STAND_ENTRY_Decodes_AndRejectsWrongLengths()
        {
            var payload = Hex(EntryHead + EntryMiddle + At + "0505" + "02" + EntryTail);
            Assert.AreEqual(108 - 4, payload.Length);
            var p = new UNIT_STAND_ENTRY(payload);
            Assert.AreEqual(0, p.ObjectType);
            Assert.AreEqual(2000001u, p.UnitId);
            Assert.AreEqual(0x201u, p.CharacterId);
            Assert.AreEqual(150, p.Speed);
            Assert.AreEqual(0x40, p.EffectState);
            Assert.AreEqual(4001, p.Job);
            Assert.AreEqual(3, p.HairStyle);
            Assert.AreEqual(1201u, p.Weapon);
            Assert.AreEqual(2101u, p.Shield);
            Assert.AreEqual(5, p.HeadBottom);
            Assert.AreEqual(16, p.HeadTop);
            Assert.AreEqual(10, p.HeadMid);
            Assert.AreEqual(7, p.HairColour);
            Assert.AreEqual(2, p.ClothColour);
            Assert.AreEqual(2, p.HeadDirection);
            Assert.AreEqual(3, p.Robe);
            Assert.AreEqual(17u, p.GuildId);
            Assert.AreEqual(1, p.Sex);
            Assert.AreEqual((150, 200, UninarokDirection.South), ((int)p.Position.X, (int)p.Position.Y, p.Position.Direction));
            Assert.AreEqual(2, p.State);
            Assert.AreEqual(99, p.Level);
            Assert.AreEqual(10000, p.MaxHp);
            Assert.AreEqual(1000, p.Hp);
            Assert.AreEqual(1, p.BodyStyle);
            Assert.AreEqual("Poring", p.Name.ToString());
            Assert.Throws<PacketFormatException>(() => new UNIT_STAND_ENTRY(new byte[103]));
            Assert.Throws<PacketFormatException>(() => new UNIT_STAND_ENTRY(new byte[105]));
        }

        [Test]
        public void UNIT_STAND_ENTRY_DecodesTheRealCapture()
        {
            // Map-20261117-124252.bin, the warp of the novice room (job 45), without the 4-byte header
            var p = new UNIT_STAND_ENTRY(Hex(
                "06ecb58e0600000000c80000000000000000002d000000000000000000000000000000000000000000000000000000000000000000000000000000" +
                "06c1e000000000000000ffffffffffffffff0000006e6f76697368697023726f6f6d312d310000000000000000"));
            Assert.AreEqual(6, p.ObjectType);
            Assert.AreEqual(0x068EB5ECu, p.UnitId);
            Assert.AreEqual(0u, p.CharacterId);
            Assert.AreEqual(200, p.Speed);
            Assert.AreEqual(45, p.Job);
            Assert.AreEqual((27, 30), ((int)p.Position.X, (int)p.Position.Y));
            Assert.AreEqual(0, p.State);
            Assert.AreEqual(-1, p.MaxHp);
            Assert.AreEqual("noviship#room1-1", p.Name.ToString());
        }

        // ---- UNIT_NEW_ENTRY (0x09FE, variable, 107) ----

        [Test]
        public void UNIT_NEW_ENTRY_Decodes_AndRejectsWrongLengths()
        {
            var payload = Hex(EntryHead + EntryMiddle + At + "0505" + EntryTail);
            Assert.AreEqual(107 - 4, payload.Length);
            var p = new UNIT_NEW_ENTRY(payload);
            Assert.AreEqual(2000001u, p.UnitId);
            Assert.AreEqual(4001, p.Job);
            Assert.AreEqual((150, 200), ((int)p.Position.X, (int)p.Position.Y));
            Assert.AreEqual(99, p.Level);
            Assert.AreEqual(1, p.BodyStyle);
            Assert.AreEqual("Poring", p.Name.ToString());
            Assert.Throws<PacketFormatException>(() => new UNIT_NEW_ENTRY(new byte[102]));
            Assert.Throws<PacketFormatException>(() => new UNIT_NEW_ENTRY(new byte[104]));
        }

        // ---- UNIT_MOVE_ENTRY (0x09FD, variable, 114) ----

        [Test]
        public void UNIT_MOVE_ENTRY_Decodes_AndRejectsWrongLengths()
        {
            var payload = Hex(EntryHead + "04030201" + EntryMiddle + Move + "0505" + EntryTail);
            Assert.AreEqual(114 - 4, payload.Length);
            var p = new UNIT_MOVE_ENTRY(payload);
            Assert.AreEqual(2000001u, p.UnitId);
            Assert.AreEqual(0x01020304u, p.MoveStartTime);
            Assert.AreEqual(16, p.HeadTop);
            Assert.AreEqual((150, 200, 155, 198), ((int)p.Move.SourceX, (int)p.Move.SourceY, (int)p.Move.DestX, (int)p.Move.DestY));
            Assert.AreEqual(99, p.Level);
            Assert.AreEqual("Poring", p.Name.ToString());
            Assert.Throws<PacketFormatException>(() => new UNIT_MOVE_ENTRY(new byte[109]));
            Assert.Throws<PacketFormatException>(() => new UNIT_MOVE_ENTRY(new byte[111]));
        }

        // ---- the fixed ones ----

        [Test]
        public void UNIT_MOVE_Decodes_AndRejectsWrongLengths()
        {
            var p = new UNIT_MOVE(Hex("ECB58E06" + Move + "04030201"));
            Assert.AreEqual(0x068EB5ECu, p.UnitId);
            Assert.AreEqual((150, 200, 155, 198), ((int)p.Move.SourceX, (int)p.Move.SourceY, (int)p.Move.DestX, (int)p.Move.DestY));
            Assert.AreEqual((8, 8), ((int)p.Move.SubX, (int)p.Move.SubY));
            Assert.AreEqual(0x01020304u, p.StartTick);
            Assert.Throws<PacketFormatException>(() => new UNIT_MOVE(new byte[13]));
            Assert.Throws<PacketFormatException>(() => new UNIT_MOVE(new byte[15]));
        }

        [Test]
        public void PLAYER_MOVE_Decodes_AndRejectsWrongLengths()
        {
            var p = new PLAYER_MOVE(Hex("04030201" + Move));
            Assert.AreEqual(0x01020304u, p.StartTick);
            Assert.AreEqual((150, 200, 155, 198), ((int)p.Move.SourceX, (int)p.Move.SourceY, (int)p.Move.DestX, (int)p.Move.DestY));
            Assert.Throws<PacketFormatException>(() => new PLAYER_MOVE(new byte[9]));
            Assert.Throws<PacketFormatException>(() => new PLAYER_MOVE(new byte[11]));
        }

        [Test]
        public void UNIT_STOP_MOVE_And_UNIT_SLIDE_Decode_AndRejectWrongLengths()
        {
            var payload = Hex("ECB58E06" + "9600" + "2C01");
            var stop = new UNIT_STOP_MOVE(payload);
            Assert.AreEqual((0x068EB5ECu, 150, 300), (stop.UnitId, (int)stop.X, (int)stop.Y));
            var slide = new UNIT_SLIDE(payload);
            Assert.AreEqual((0x068EB5ECu, 150, 300), (slide.UnitId, (int)slide.X, (int)slide.Y));
            Assert.Throws<PacketFormatException>(() => new UNIT_STOP_MOVE(new byte[7]));
            Assert.Throws<PacketFormatException>(() => new UNIT_STOP_MOVE(new byte[9]));
            Assert.Throws<PacketFormatException>(() => new UNIT_SLIDE(new byte[7]));
            Assert.Throws<PacketFormatException>(() => new UNIT_SLIDE(new byte[9]));
        }

        [Test]
        public void UNIT_VANISH_Decodes_AndRejectsWrongLengths()
        {
            var p = new UNIT_VANISH(Hex("ECB58E06" + "03"));
            Assert.AreEqual((0x068EB5ECu, 3), (p.UnitId, (int)p.Type));
            Assert.Throws<PacketFormatException>(() => new UNIT_VANISH(new byte[4]));
            Assert.Throws<PacketFormatException>(() => new UNIT_VANISH(new byte[6]));
        }

        [Test]
        public void UnitPackets_SizesAndIds()
        {
            Assert.AreEqual((0x0080, 7), ((int)PacketIds.UNIT_VANISH, UNIT_VANISH.Size));
            Assert.AreEqual((0x0086, 16), ((int)PacketIds.UNIT_MOVE, UNIT_MOVE.Size));
            Assert.AreEqual((0x0087, 12), ((int)PacketIds.PLAYER_MOVE, PLAYER_MOVE.Size));
            Assert.AreEqual((0x0088, 10), ((int)PacketIds.UNIT_STOP_MOVE, UNIT_STOP_MOVE.Size));
            Assert.AreEqual((0x01FF, 10), ((int)PacketIds.UNIT_SLIDE, UNIT_SLIDE.Size));
            Assert.AreEqual((0x09FD, -1), ((int)PacketIds.UNIT_MOVE_ENTRY, UNIT_MOVE_ENTRY.Size));
            Assert.AreEqual((0x09FE, -1), ((int)PacketIds.UNIT_NEW_ENTRY, UNIT_NEW_ENTRY.Size));
            Assert.AreEqual((0x09FF, -1), ((int)PacketIds.UNIT_STAND_ENTRY, UNIT_STAND_ENTRY.Size));
        }

        [Test]
        public void UnitPackets_ThousandDecodes_AllocateNothing()
        {
            var stand = Hex(EntryHead + EntryMiddle + At + "0505" + "02" + EntryTail);
            var spawn = Hex(EntryHead + EntryMiddle + At + "0505" + EntryTail);
            var walk = Hex(EntryHead + "04030201" + EntryMiddle + Move + "0505" + EntryTail);
            var move = Hex("ECB58E06" + Move + "04030201");
            var own = Hex("04030201" + Move);
            var stop = Hex("ECB58E06" + "9600" + "2C01");
            var vanish = Hex("ECB58E06" + "03");
            uint sum = 0;
            Action run = () =>
            {
                for (int i = 0; i < Decodes; i++)
                    sum += new UNIT_STAND_ENTRY(stand).UnitId + new UNIT_NEW_ENTRY(spawn).UnitId + new UNIT_MOVE_ENTRY(walk).UnitId + new UNIT_MOVE(move).UnitId
                           + new PLAYER_MOVE(own).StartTick + new UNIT_STOP_MOVE(stop).UnitId + new UNIT_SLIDE(stop).UnitId + new UNIT_VANISH(vanish).UnitId;
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreNotEqual(0u, sum);
        }
    }
}
