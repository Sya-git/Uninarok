using System;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Packets.Out.Map;

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

        // ---- direction, look, act, parameter (T1.3) ----

        [Test]
        public void UNIT_DIRECTION_Decodes_AndRejectsWrongLengths()
        {
            var p = new UNIT_DIRECTION(Hex("ECB58E06" + "0200" + "05"));
            Assert.AreEqual((0x068EB5ECu, 2, 5), (p.UnitId, (int)p.HeadDirection, (int)p.Direction));
            Assert.Throws<PacketFormatException>(() => new UNIT_DIRECTION(new byte[6]));
            Assert.Throws<PacketFormatException>(() => new UNIT_DIRECTION(new byte[8]));
        }

        [Test]
        public void UNIT_LOOK_CHANGE_DecodesTheRealCapture_AndRejectsWrongLengths()
        {
            // Map-20261117-124252.bin: the own weapon (Knife, item 1201); the shield is set here to check the order
            var p = new UNIT_LOOK_CHANGE(Hex("80841e00" + "02" + "b1040000" + "35080000"));
            Assert.AreEqual((0x001E8480u, 2, 1201u, 2101u), (p.UnitId, (int)p.Type, p.Value, p.Value2));
            Assert.Throws<PacketFormatException>(() => new UNIT_LOOK_CHANGE(new byte[12]));
            Assert.Throws<PacketFormatException>(() => new UNIT_LOOK_CHANGE(new byte[14]));
        }

        [Test]
        public void UNIT_ACT_Decodes_WhateverTheUnusedBytesHold()
        {
            var zero = new UNIT_ACT(Hex("ECB58E06" + new string('0', 40) + "02" + "0000"));
            var dirty = new UNIT_ACT(Hex("ECB58E06" + new string('F', 40) + "03" + "FFFF"));
            Assert.AreEqual((0x068EB5ECu, 2), (zero.UnitId, (int)zero.Action));
            Assert.AreEqual((0x068EB5ECu, 3), (dirty.UnitId, (int)dirty.Action));
            Assert.Throws<PacketFormatException>(() => new UNIT_ACT(new byte[26]));
            Assert.Throws<PacketFormatException>(() => new UNIT_ACT(new byte[28]));
        }

        [Test]
        public void PARAMETER_CHANGE_Decodes_AndRejectsWrongLengths()
        {
            var p = new PARAMETER_CHANGE(Hex("0000" + "96000000"));
            Assert.AreEqual((0, 150), ((int)p.VarId, p.Value));
            Assert.Throws<PacketFormatException>(() => new PARAMETER_CHANGE(new byte[5]));
            Assert.Throws<PacketFormatException>(() => new PARAMETER_CHANGE(new byte[7]));
        }

        [Test]
        public void StatePackets_SizesAndIds()
        {
            Assert.AreEqual((0x008A, 29), ((int)PacketIds.UNIT_ACT, UNIT_ACT.Size));
            Assert.AreEqual((0x009C, 9), ((int)PacketIds.UNIT_DIRECTION, UNIT_DIRECTION.Size));
            Assert.AreEqual((0x00B0, 8), ((int)PacketIds.PARAMETER_CHANGE, PARAMETER_CHANGE.Size));
            Assert.AreEqual((0x01D7, 15), ((int)PacketIds.UNIT_LOOK_CHANGE, UNIT_LOOK_CHANGE.Size));
        }

        [Test]
        public void StatePackets_ThousandDecodes_AllocateNothing()
        {
            var direction = Hex("ECB58E06" + "0200" + "05");
            var look = Hex("80841e00" + "02" + "b1040000" + "35080000");
            var act = new byte[27];
            var parameter = Hex("0000" + "96000000");
            uint sum = 0;
            Action run = () =>
            {
                for (int i = 0; i < Decodes; i++)
                    sum += new UNIT_DIRECTION(direction).UnitId + new UNIT_LOOK_CHANGE(look).UnitId + new UNIT_ACT(act).UnitId + (uint)new PARAMETER_CHANGE(parameter).Value;
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreNotEqual(0u, sum);
        }

        // ---- ground items (T1.4) ----

        [Test]
        public void ITEM_ENTRY_Decodes_AndRejectsWrongLengths()
        {
            // amount before the sub-cell
            var p = new ITEM_ENTRY(Hex("11000000" + "F5010000" + "01" + "9600" + "C800" + "0300" + "06" + "0C"));
            Assert.AreEqual((17u, 501u, true), (p.ItemObjectId, p.ItemId, p.Identified));
            Assert.AreEqual((150, 200, 3, 6, 12), ((int)p.X, (int)p.Y, (int)p.Amount, (int)p.SubX, (int)p.SubY));
            Assert.Throws<PacketFormatException>(() => new ITEM_ENTRY(new byte[16]));
            Assert.Throws<PacketFormatException>(() => new ITEM_ENTRY(new byte[18]));
        }

        [Test]
        public void ITEM_FALL_ENTRY_Decodes_AndRejectsWrongLengths()
        {
            // the sub-cell before the amount
            var p = new ITEM_FALL_ENTRY(Hex("11000000" + "F5010000" + "0200" + "00" + "9600" + "C800" + "06" + "0C" + "0300" + "01" + "0400"));
            Assert.AreEqual((17u, 501u, false), (p.ItemObjectId, p.ItemId, p.Identified));
            Assert.AreEqual((2, 150, 200, 6, 12, 3), ((int)p.ItemType, (int)p.X, (int)p.Y, (int)p.SubX, (int)p.SubY, (int)p.Amount));
            Assert.AreEqual((1, 4), ((int)p.ShowDropEffect, (int)p.DropEffectMode));
            Assert.Throws<PacketFormatException>(() => new ITEM_FALL_ENTRY(new byte[21]));
            Assert.Throws<PacketFormatException>(() => new ITEM_FALL_ENTRY(new byte[23]));
        }

        [Test]
        public void ITEM_DISAPPEAR_Decodes_AndRejectsWrongLengths()
        {
            Assert.AreEqual(17u, new ITEM_DISAPPEAR(Hex("11000000")).ItemObjectId);
            Assert.Throws<PacketFormatException>(() => new ITEM_DISAPPEAR(new byte[3]));
            Assert.Throws<PacketFormatException>(() => new ITEM_DISAPPEAR(new byte[5]));
        }

        [Test]
        public void ItemPackets_SizesAndIds()
        {
            Assert.AreEqual((0x009D, 19), ((int)PacketIds.ITEM_ENTRY, ITEM_ENTRY.Size));
            Assert.AreEqual((0x00A1, 6), ((int)PacketIds.ITEM_DISAPPEAR, ITEM_DISAPPEAR.Size));
            Assert.AreEqual((0x0ADD, 24), ((int)PacketIds.ITEM_FALL_ENTRY, ITEM_FALL_ENTRY.Size));
        }

        [Test]
        public void ItemPackets_ThousandDecodes_AllocateNothing()
        {
            var entry = Hex("11000000" + "F5010000" + "01" + "9600" + "C800" + "0300" + "06" + "0C");
            var fall = Hex("11000000" + "F5010000" + "0200" + "00" + "9600" + "C800" + "06" + "0C" + "0300" + "01" + "0400");
            var gone = Hex("11000000");
            uint sum = 0;
            Action run = () =>
            {
                for (int i = 0; i < Decodes; i++)
                    sum += new ITEM_ENTRY(entry).ItemId + new ITEM_FALL_ENTRY(fall).ItemId + new ITEM_DISAPPEAR(gone).ItemObjectId;
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreNotEqual(0u, sum);
        }

        // ---- the items a dev command drops (T8.5) ----

        [Test]
        public void INVENTORY_LIST_NORMAL_Decodes_AndRejectsWrongLengths()
        {
            string rest = "00000000" + new string('0', 32) + "00000000" + "01";
            var p = new INVENTORY_LIST_NORMAL(Hex("00" + "0200" + "F5010000" + "00" + "0300" + rest + "0500" + "8F030000" + "03" + "0A00" + rest));
            Assert.AreEqual((0, 2), ((int)p.ListType, p.Items.Length));
            Assert.AreEqual((2, 501u, 0, 3), ((int)p.Items[0].Index, p.Items[0].ItemId, (int)p.Items[0].ItemType, (int)p.Items[0].Amount));
            Assert.AreEqual((5, 911u, 3, 10), ((int)p.Items[1].Index, p.Items[1].ItemId, (int)p.Items[1].ItemType, (int)p.Items[1].Amount));
            Assert.AreEqual(0, new INVENTORY_LIST_NORMAL(new byte[1]).Items.Length);
            Assert.Throws<PacketFormatException>(() => new INVENTORY_LIST_NORMAL(new byte[34]));
        }

        [Test]
        public void INVENTORY_LIST_EQUIP_Decodes_AndRejectsWrongLengths()
        {
            string rest = new string('0', 2 * (INVENTORY_LIST_EQUIP.RecordSize - 15));
            var p = new INVENTORY_LIST_EQUIP(Hex("00" + "0300" + "B1040000" + "05" + "02000000" + "00000000" + rest + "0400" + "FD080000" + "04" + "10000000" + "10000000" + rest));
            Assert.AreEqual((0, 2), ((int)p.ListType, p.Items.Length));
            Assert.AreEqual((3, 1201u, 5, 2u, 0u), ((int)p.Items[0].Index, p.Items[0].ItemId, (int)p.Items[0].ItemType, p.Items[0].Location, p.Items[0].WornAt));
            Assert.AreEqual((4, 2301u, 4, 16u, 16u), ((int)p.Items[1].Index, p.Items[1].ItemId, (int)p.Items[1].ItemType, p.Items[1].Location, p.Items[1].WornAt));
            Assert.Throws<PacketFormatException>(() => new INVENTORY_LIST_EQUIP(new byte[68]));
            Assert.AreEqual((0x0B39, -1), ((int)PacketIds.INVENTORY_LIST_EQUIP, INVENTORY_LIST_EQUIP.Size));
        }

        [Test]
        public void ITEM_PICKUP_ACK_Decodes_AndRejectsWrongLengths()
        {
            var payload = new byte[ITEM_PICKUP_ACK.Size - 2];
            Hex("0400" + "0300" + "F5010000" + "01").CopyTo(payload, 0);
            payload[30] = 2;
            payload[31] = 6;
            var p = new ITEM_PICKUP_ACK(payload);
            Assert.AreEqual((4, 3, 501u, true), ((int)p.InventoryIndex, (int)p.Amount, p.ItemId, p.Identified));
            Assert.AreEqual((2, 6), ((int)p.ItemType, (int)p.Result));
            Assert.Throws<PacketFormatException>(() => new ITEM_PICKUP_ACK(new byte[67]));
            Assert.Throws<PacketFormatException>(() => new ITEM_PICKUP_ACK(new byte[69]));
        }

        [Test]
        public void DropPackets_GoldenBytes_SizesAndIds()
        {
            var p = new DROP_ITEM_ACK(Hex("0400" + "0200"));
            Assert.AreEqual((4, 2), ((int)p.InventoryIndex, (int)p.Amount));
            Assert.Throws<PacketFormatException>(() => new DROP_ITEM_ACK(new byte[5]));
            Assert.AreEqual(Hex("6303" + "0400" + "0200"), Serialized(new DROP_ITEM(4, 2)));
            Assert.AreEqual((0x0363, 6, 0x00AF, 6), ((int)PacketIds.DROP_ITEM, DROP_ITEM.Size, (int)PacketIds.DROP_ITEM_ACK, DROP_ITEM_ACK.Size));
            Assert.AreEqual((0x0B41, 70, 0x0B09, -1), ((int)PacketIds.ITEM_PICKUP_ACK, ITEM_PICKUP_ACK.Size, (int)PacketIds.INVENTORY_LIST_NORMAL, INVENTORY_LIST_NORMAL.Size));
        }

        // ---- map change (T1.5) ----

        [Test]
        public void MAP_CHANGE_DecodesTheRealCapture_AndRejectsWrongLengths()
        {
            // Map-20261117-124252.bin: iz_int.gat 18,27
            var p = new MAP_CHANGE(Hex("697a5f696e742e67617400000000000012001b00"));
            Assert.AreEqual("iz_int.gat", p.MapName.ToString());
            Assert.AreEqual((18, 27), ((int)p.X, (int)p.Y));
            Assert.Throws<PacketFormatException>(() => new MAP_CHANGE(new byte[19]));
            Assert.Throws<PacketFormatException>(() => new MAP_CHANGE(new byte[21]));
        }

        [Test]
        public void MAP_SERVER_CHANGE_DecodesTheIpBigEndianAndThePortLittleEndian()
        {
            // as clif_changemapserver writes it: htonl(127.0.0.1), port 50121 in little-endian order
            var payload = new byte[MAP_SERVER_CHANGE.Size - 2];
            Hex("7072745f66696c6430382e6761740000").CopyTo(payload, 0);
            Hex("AA00" + "7B01" + "7F000001" + "C9C3").CopyTo(payload, 16);
            System.Text.Encoding.ASCII.GetBytes("map.example").CopyTo(payload, 26);
            var p = new MAP_SERVER_CHANGE(payload);
            Assert.AreEqual("prt_fild08.gat", p.MapName.ToString());
            Assert.AreEqual((170, 379), ((int)p.X, (int)p.Y));
            Assert.AreEqual("127.0.0.1", p.FormatIpAddress());
            Assert.AreEqual(50121, p.Port);
            Assert.AreEqual("map.example", p.DnsHost.ToString());
            Assert.Throws<PacketFormatException>(() => new MAP_SERVER_CHANGE(new byte[153]));
            Assert.Throws<PacketFormatException>(() => new MAP_SERVER_CHANGE(new byte[155]));
        }

        [Test]
        public void MapChangePackets_SizesAndIds()
        {
            Assert.AreEqual((0x0091, 22), ((int)PacketIds.MAP_CHANGE, MAP_CHANGE.Size));
            Assert.AreEqual((0x0AC7, 156), ((int)PacketIds.MAP_SERVER_CHANGE, MAP_SERVER_CHANGE.Size));
        }

        // ---- outgoing (T1.6) ----

        static byte[] Serialized<T>(in T packet) where T : struct, IOutgoingPacket
        {
            var b = new byte[packet.WireSize];
            packet.Serialize(b);
            return b;
        }

        [Test]
        public void REQUEST_MOVE_WritesGoldenBytes()
        {
            Assert.AreEqual(Hex("5F03" + "258C80"), Serialized(new REQUEST_MOVE(150, 200)));
            Assert.AreEqual(Hex("5F03" + "4B04D0"), Serialized(new REQUEST_MOVE(300, 77)));
        }

        [Test]
        public void CHANGE_DIRECTION_WritesGoldenBytes() =>
            Assert.AreEqual(Hex("6103" + "0200" + "05"), Serialized(new CHANGE_DIRECTION(2, UninarokDirection.SouthEast)));

        [Test]
        public void REQUEST_ACT_WritesGoldenBytes()
        {
            Assert.AreEqual(Hex("3704" + "00000000" + "02"), Serialized(new REQUEST_ACT(0, REQUEST_ACT.Sit)));
            Assert.AreEqual(Hex("3704" + "00000000" + "03"), Serialized(new REQUEST_ACT(0, REQUEST_ACT.Stand)));
        }

        [Test]
        public void OutgoingWorldPackets_SizesAndIds_AndAllocateNothing()
        {
            Assert.AreEqual((0x035F, 5), ((int)PacketIds.REQUEST_MOVE, REQUEST_MOVE.Size));
            Assert.AreEqual((0x0361, 5), ((int)PacketIds.CHANGE_DIRECTION, CHANGE_DIRECTION.Size));
            Assert.AreEqual((0x0437, 7), ((int)PacketIds.REQUEST_ACT, REQUEST_ACT.Size));
            var client = new FakeNetworkClient { Status = Client.ConnectionStatus.Connected, RecordSends = false };
            var buffer = new byte[16];
            Action run = () =>
            {
                for (int i = 0; i < Decodes; i++)
                {
                    var move = new REQUEST_MOVE((ushort)i, 5);
                    var turn = new CHANGE_DIRECTION(1, UninarokDirection.East);
                    var act = new REQUEST_ACT(0, REQUEST_ACT.Sit);
                    move.Serialize(new Span<byte>(buffer, 0, move.WireSize));
                    turn.Serialize(new Span<byte>(buffer, 0, turn.WireSize));
                    act.Serialize(new Span<byte>(buffer, 0, act.WireSize));
                    client.Send(move);
                    client.Send(turn);
                    client.Send(act);
                }
            };
            run();
            Assert.That(() => run(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
            Assert.AreEqual(2 * 3 * Decodes, client.UncountedSends);
        }
    }
}
