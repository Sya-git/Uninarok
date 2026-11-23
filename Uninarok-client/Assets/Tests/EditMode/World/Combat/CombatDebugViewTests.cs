using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.World.Dev;
using static Uninarok.World.Tests.WorldSessionTests;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-11 (PLAN-0011 4.12): the combat HUD lists what the packets said.
    public class CombatDebugViewTests
    {
        const uint Monster = 0x068EB5EC;

        Rig _rig;
        GameObject _go;
        CombatDebugView _view;

        [SetUp]
        public void SetUp()
        {
            _rig = new Rig().ToInGame(OwnCharacterTests.Character());
            _go = new GameObject("Combat HUD");
            _view = _go.AddComponent<CombatDebugView>();
            _view.Use(_rig.Session);
            _view.Bind(_rig.Session);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            _rig.Dispose();
        }

        uint Own => _rig.Flow.Context.AccountId;

        void Blow(uint source, uint target, int damage, int type, short count, int attackMt, int attackedMt, int left = 0) =>
            _rig.Client.Deliver(new DAMAGE(Hex(Le(source) + Le(target) + Le(_rig.Clock.Now) + Le((uint)attackMt) + Le((uint)attackedMt) + Le((uint)damage) + "00" + Le16(count) + $"{type:X2}" + Le((uint)left))));

        [Test]
        public void CombatDebugView_ListsBlowsAsReceived()
        {
            _rig.Client.Deliver(Stand(Monster, 151, 200));
            Blow(Own, Monster, 37, 0, 1, 500, 300);
            Blow(Monster, Own, 5, 0, 1, 912, 288);
            Blow(Own, Monster, 120, 10, 1, 480, 310, left: 44);
            Blow(Own, Monster, 20, 8, 2, 480, 310);
            string text = _view.Text();
            StringAssert.Contains("blows 4 (last 4)", text);
            // newest first, every number as the packet had it
            int multi = text.IndexOf("dmg 20  MultiHit (type 8)  hits 2  attackMT 480  attackedMT 310");
            int crit = text.IndexOf("dmg 120 + 44  Critical (type 10)  hits 1  attackMT 480  attackedMT 310");
            int hurt = text.IndexOf(Monster + " > " + Own + "  dmg 5  Normal (type 0)  hits 1  attackMT 912  attackedMT 288");
            int first = text.IndexOf(Own + " > " + Monster + "  dmg 37  Normal (type 0)  hits 1  attackMT 500  attackedMT 300");
            Assert.That(multi, Is.GreaterThan(0));
            Assert.That(crit, Is.GreaterThan(multi));
            Assert.That(hurt, Is.GreaterThan(crit));
            Assert.That(first, Is.GreaterThan(hurt));
        }

        [Test]
        public void CombatDebugView_KeepsTheLast20Blows()
        {
            _rig.Client.Deliver(Stand(Monster, 151, 200));
            for (int i = 1; i <= 25; i++) Blow(Own, Monster, i, 0, 1, 500, 300);
            string text = _view.Text();
            StringAssert.Contains("blows 25 (last 20)", text);
            StringAssert.Contains("dmg 25 ", text);
            StringAssert.Contains("dmg 6 ", text);
            StringAssert.DoesNotContain("dmg 5 ", text);
        }

        [Test]
        public void CombatDebugView_ShowsStatusStateTooFarPickupAndExp()
        {
            _rig.Client.Deliver(Stand(Monster, 151, 200));
            _rig.Client.Deliver(Parameter(5, 77));
            _rig.Client.Deliver(new ATTACK_RANGE(Hex(Le16(3))));
            _rig.Client.Deliver(new EXP_GAIN(Hex(Le(Own) + Le(17) + "00000000" + "0100" + "0000")));
            _rig.Client.Deliver(new ATTACK_TOO_FAR(Hex(Le(Monster) + "A000BE00" + "9800BE00" + Le16(3))));
            _rig.Client.Deliver(new ITEM_PICKUP_ACK(Hex("0200" + "0100" + Le(501u) + "01" + "00" + new string('0', 32) + "00000000" + "00" + "01" + new string('0', 72))));
            string text = _view.Text();
            StringAssert.Contains("HP 77/", text);
            StringAssert.Contains("range 3", text);
            StringAssert.Contains("state Idle", text);
            StringAssert.Contains("exp base +17", text);
            StringAssert.Contains("too far from " + Monster, text);
            StringAssert.Contains("pickup of 501 answered 1", text);
        }
    }
}
