using System.Text;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Tests;
using Uninarok.World.Dev;
using Uninarok.World.View;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-13, FR-14, FR-19 (PLAN-0010 4.12).
    public class DevToolsTests
    {
        WorldSessionTests.Rig _rig;
        GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _rig = new WorldSessionTests.Rig();
            _rig.ToInGame(OwnCharacterTests.Character());
            _go = new GameObject("Dev");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            _rig.Dispose();
        }

        [Test]
        public void Commands_CallTheOwnCharacter()
        {
            var line = _go.AddComponent<DevCommandLine>();
            line.Register(() => _rig.Session.Own);
            Assert.IsNull(line.Execute("/doridori"));
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.CHANGE_DIRECTION));
            Assert.IsNull(line.Execute(" /BANGBANG "));
            Assert.AreEqual(2, _rig.Client.SentCount(PacketIds.CHANGE_DIRECTION));
            Assert.IsNull(line.Execute("/sit"));
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.REQUEST_ACT));
            Assert.AreEqual("not now", line.Execute("/stand"), "it stands already");
            _rig.Client.Deliver(WorldSessionTests.Act(_rig.Flow.Context.AccountId, 2));
            Assert.IsNull(line.Execute("/stand"));
            Assert.AreEqual(2, _rig.Client.SentCount(PacketIds.REQUEST_ACT));
        }

        // SPEC-0011 T7.3 (FR-2; PLAN-0011 4.10)
        [Test]
        public void NoCtrl_TogglesAPlainClickToContinuous_AndSaysSo()
        {
            const uint monster = 0x068EB5EC;
            var line = _go.AddComponent<DevCommandLine>();
            line.Register(() => _rig.Session.Own, () => _rig.Session.Combat);
            _rig.Client.Deliver(WorldSessionTests.Stand(monster, 151, 200));
            var combat = _rig.Session.Combat;

            Assert.AreEqual("/noctrl on: a click attacks without end", line.Execute("/noctrl"));
            Assert.IsTrue(combat.NoCtrl);
            combat.Attack(monster, false); // a plain click
            Assert.AreEqual(7, _rig.Client.Sent[_rig.Client.Sent.Count - 1].Bytes[6], "continuous");
            Assert.IsTrue(combat.Continuous);
            combat.Cancel();

            Assert.AreEqual("/noctrl off: ctrl or a held button attacks without end", line.Execute("/NOCTRL"));
            Assert.IsFalse(combat.NoCtrl);
            combat.Attack(monster, false);
            Assert.AreEqual(0, _rig.Client.Sent[_rig.Client.Sent.Count - 1].Bytes[6], "single again");
            Assert.AreEqual("/noctrl takes nothing", line.Execute("/noctrl 1"));
        }

        // FR-20
        [Test]
        public void Drop_SendsTheSlotTheServerNamed()
        {
            var line = _go.AddComponent<DevCommandLine>();
            line.Register(() => _rig.Session.Own);
            Assert.AreEqual("no character", line.Execute("/drop 501"), "no inventory yet");
            line.UseInventory(new DevInventory(_rig.Client));
            Assert.AreEqual("no items", line.Execute("/drop"));

            string rest = "00000000" + new string('0', 32) + "00000000" + "01";
            _rig.Client.Deliver(new INVENTORY_LIST_NORMAL(WorldSessionTests.Hex("00" + "0700" + "F5010000" + "00" + "0300" + rest)));
            _rig.Client.Deliver(new INVENTORY_LIST_NORMAL(WorldSessionTests.Hex("01" + "0200" + "F6010000" + "00" + "0300" + rest))); // the cart's
            string gear = new string('0', 2 * (INVENTORY_LIST_EQUIP.RecordSize - 15));
            _rig.Client.Deliver(new INVENTORY_LIST_EQUIP(WorldSessionTests.Hex("00" + "0800" + "B1040000" + "05" + "02000000" + "00000000" + gear + "0600" + "FD080000" + "04" + "10000000" + "10000000" + gear))); // the second is worn
            var added = new byte[ITEM_PICKUP_ACK.Size - 2];
            WorldSessionTests.Hex("0900" + "0200" + "8F030000" + "01").CopyTo(added, 0);
            _rig.Client.Deliver(new ITEM_PICKUP_ACK(added));
            added[31] = 2;
            _rig.Client.Deliver(new ITEM_PICKUP_ACK(added)); // refused
            Assert.AreEqual("501 x3, 1201 x1, 911 x2", line.Execute("/drop"));

            Assert.IsNull(line.Execute("/drop 501"));
            Assert.IsNull(line.Execute("/drop  911 2"));
            Assert.AreEqual(WorldSessionTests.Hex("630307000100"), _rig.Client.Sent[0].Bytes);
            Assert.AreEqual(WorldSessionTests.Hex("630309000200"), _rig.Client.Sent[1].Bytes);
            Assert.AreEqual("501 x3, 1201 x1, 911 x2", line.Execute("/drop"), "not before the server says so");
            _rig.Client.Deliver(new DROP_ITEM_ACK(WorldSessionTests.Hex("07000100")));
            _rig.Client.Deliver(new DROP_ITEM_ACK(WorldSessionTests.Hex("09000200")));
            Assert.AreEqual("501 x2, 1201 x1", line.Execute("/drop"));

            Assert.AreEqual("no item 911", line.Execute("/drop 911"));
            Assert.AreEqual("only 2 of item 501", line.Execute("/drop 501 3"));
            foreach (string wrong in new[] { "/drop potion", "/drop 501 0", "/drop 501 1 1", "/drop 501 x" }) Assert.AreEqual("/drop item [amount]", line.Execute(wrong));
            Assert.AreEqual(2, _rig.Client.Sent.Count);

            _rig.Client.Deliver(TestPackets.AuthenticationAccepted());
            Assert.AreEqual("no items", line.Execute("/drop"), "another session");
        }

        // FR-20
        [Test]
        public void ServerCommands_AreChatLines_AndTheirAnswersAreShown()
        {
            var line = _go.AddComponent<DevCommandLine>();
            line.Register(() => _rig.Session.Own);
            line.UseMessages(new DevMessages(_rig.Client, line.Show));
            Assert.IsNull(line.Execute("@item 501 5"));
            string sent = System.Text.Encoding.ASCII.GetString(_rig.Client.Sent[0].Bytes, 4, _rig.Client.Sent[0].Bytes.Length - 5);
            StringAssert.EndsWith(" : @item 501 5", sent);
            Assert.AreEqual(1, _rig.Client.SentCount(PacketIds.GLOBAL_CHAT));

            _rig.Client.Deliver(new OWN_MESSAGE(System.Text.Encoding.ASCII.GetBytes("Item created.\0")));
            Assert.AreEqual("Item created.", line.Message);
            _rig.Client.Deliver(new OWN_MESSAGE(System.Text.Encoding.ASCII.GetBytes(sent + "\0")));
            StringAssert.Contains("this account has no such command", line.Message);

            line.Register(() => null);
            Assert.AreEqual("not in game", line.Execute("@item 501"));
        }

        [Test]
        public void UnknownCommands_AndEmptyLines()
        {
            var line = _go.AddComponent<DevCommandLine>();
            line.Register(() => null);
            Assert.AreEqual("unknown command: /where", line.Execute("/where"));
            Assert.IsNull(line.Execute("  "));
            Assert.IsNull(line.Execute(null));
            Assert.AreEqual("no character", line.Execute("/sit"));
            line.Add("/where", () => "here");
            Assert.AreEqual("here", line.Execute("/where"), "later specs add theirs");
        }

        [Test]
        public void AnOpenLine_TakesTheGamesInput()
        {
            var input = _go.AddComponent<OwnInput>();
            var line = _go.AddComponent<DevCommandLine>();
            line.Use(null, input);
            line.Open();
            Assert.IsFalse(input.InputEnabled);
            line.Close();
            Assert.IsTrue(input.InputEnabled);
        }

        [Test]
        public void TheReadout_HoldsClockMapOwnCellAndEntities()
        {
            _rig.Client.Deliver(WorldSessionTests.Stand(77, 10, 11));
            _rig.Client.Deliver(WorldSessionTests.Move(77, 10, 11, 15, 11, _rig.Clock.Now));
            var b = new StringBuilder();
            WorldDebugView.Write(b, _rig.Session, "prontera");
            string text = b.ToString();
            StringAssert.Contains("server time " + _rig.Clock.Now, text);
            StringAssert.Contains("map prontera  entities 2", text);
            StringAssert.Contains("own cell 150 200", text);
            StringAssert.Contains("77  Monster 1002  10 11  Standing walking", text);
        }
    }
}
