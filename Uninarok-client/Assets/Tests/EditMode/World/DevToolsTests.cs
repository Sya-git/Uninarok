using System.Text;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Packets;
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
