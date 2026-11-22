using System;
using NUnit.Framework;
using Unity.Collections;
using Uninarok.Networking.Core.Wire;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Map;
using Uninarok.Networking.Tests;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-1, FR-9, FR-13, FR-19 (PLAN-0010 4.6).
    public class OwnCharacterTests
    {
        WorldSessionTests.Rig _rig;

        [SetUp]
        public void SetUp() => _rig = new WorldSessionTests.Rig();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        static byte[] Character(Func<CharInfo, CharInfo> change = null)
        {
            var c = new CharInfo
            {
                CharId = 140593, Hp = 40, MaxHp = 40, DefaultWalkSpeed = 150, Class = 7, Hair = 5, HairColor = 3, ClothesColor = 2, Body = 1,
                Weapon = 1201, LookShield = 2101, LookHeadTop = 16, LookHeadMid = 10, LookHeadBottom = 8, LookRobe = 1,
                Name = new FixedString32Bytes("One"), MapName = new FixedString32Bytes("prontera.gat"), Sex = AccountSex.SEX_MALE,
            };
            if (change != null) c = change(c);
            var b = new byte[CharInfo.WireSize];
            var w = new PacketWriter(b);
            c.Write(ref w);
            return b;
        }

        uint Id => _rig.Flow.Context.AccountId;
        Entity Own => _rig.Get(Id);

        // ---- entry (FR-1, T5.2) ----

        [Test]
        public void OwnCharacter_AtEntry_HasTheSelectedLook_AndTheServersCellAndFacing()
        {
            _rig.ToLoadingMap(Character());
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(Id, _rig.Model.OwnId);
            var e = Own;
            Assert.AreEqual((EntityKind.Player, EntityState.Standing, 150), (e.Kind, e.State, (int)e.Speed));
            Assert.AreEqual((150, 200, UninarokDirection.South), (e.CellX, e.CellY, e.Facing), "AUTHENTICATION_ACCEPTED");
            var l = e.Look;
            Assert.AreEqual((7, 5, 3, 2, 1), ((int)l.Job, (int)l.HairStyle, (int)l.HairColour, (int)l.ClothColour, (int)l.BodyStyle));
            Assert.AreEqual((1201, 2101, 16, 10, 8, 1, 1), (l.Weapon, l.Shield, (int)l.HeadTop, (int)l.HeadMid, (int)l.HeadBottom, (int)l.Robe, (int)l.Sex));
            Assert.IsFalse(_rig.Session.Own.CanAct, "not in game before MAP_LOADED");
            _rig.Flow.MapLoaded();
            Assert.IsTrue(_rig.Session.Own.CanAct);
        }

        [Test]
        public void OwnCharacter_Speed_ComesFromTheStatusValue_AlsoBeforeTheMapIsLoaded()
        {
            _rig.ToLoadingMap(Character());
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 120));
            _rig.Client.Deliver(WorldSessionTests.Parameter(5, 999));
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(120, Own.Speed);
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 90));
            Assert.AreEqual(90, Own.Speed);
        }

        [Test]
        public void OwnCharacter_AfterAMapChange_KeepsWhatTheServerSaid_AndFacesNorth()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(new UNIT_LOOK_CHANGE(WorldSessionTests.Hex(WorldSessionTests.Le(Id) + "04" + WorldSessionTests.Le(220) + WorldSessionTests.Le(0))));
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 90));
            var change = new byte[20];
            change[16] = 30;
            change[18] = 40;
            _rig.Client.Deliver(new MAP_CHANGE(change));
            _rig.Session.BeginMap(_rig.Grid);
            _rig.Flow.MapLoaded();
            Assert.AreEqual((30, 40, UninarokDirection.North), (Own.CellX, Own.CellY, Own.Facing), "PLAN-0010 D14");
            Assert.AreEqual((220, 90), ((int)Own.Look.HeadTop, (int)Own.Speed));

            _rig.Settings.WarpKeepsDirection = true;
            _rig.Client.Deliver(new UNIT_DIRECTION(WorldSessionTests.Hex(WorldSessionTests.Le(Id) + "0000" + "02")));
            _rig.Client.Deliver(new MAP_CHANGE(change));
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(UninarokDirection.West, Own.Facing);
        }

        [Test]
        public void OwnCharacter_DeadOnEntry_IsNotDeadAfterAMapChange()
        {
            _rig.ToInGame(Character(c => { c.Hp = 0; return c; }));
            Assert.AreEqual(EntityState.Dead, Own.State);
            _rig.Client.Deliver(new MAP_CHANGE(new byte[20]));    // respawned at the save point
            _rig.Session.BeginMap(_rig.Grid);
            Assert.AreEqual(EntityState.Standing, Own.State);
        }

        [Test]
        public void OwnCharacter_DeadOnEntry_NoInput()
        {
            _rig.ToInGame(Character(c => { c.Hp = 0; return c; }));
            Assert.AreEqual(EntityState.Dead, Own.State);
            Assert.IsFalse(_rig.Session.Own.CanAct);
            Assert.AreEqual(0, _rig.Client.Sent.Count);
        }

        [Test]
        public void OwnCharacter_AnotherCharacter_StartsOver()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(WorldSessionTests.Parameter(0, 90));
            _rig.Client.RaiseDisconnected(new Uninarok.Networking.Core.Transport.CloseInfo(Uninarok.Networking.Core.Transport.CloseReason.RemoteClosed, "gone"), Uninarok.Networking.Core.Transport.ConnectionRole.Map);
            _rig.Flow.Reset();
            _rig.ToInGame(Character(c => { c.Class = 4; return c; }));
            Assert.AreEqual((4, 150), ((int)Own.Look.Job, (int)Own.Speed));
        }

        [Test]
        public void OwnCharacter_OnAMapWithoutData_IsTracked()
        {
            _rig.ToLoadingMap(Character());
            _rig.Session.BeginMap((Uninarok.Map.MapData)null);
            _rig.Flow.MapLoaded();
            Assert.AreEqual((150, 200), (Own.CellX, Own.CellY));
            _rig.Client.Deliver(WorldSessionTests.Stand(77, 10, 10));
            Assert.AreEqual(10, _rig.Get(77).CellX);
            _rig.Client.Deliver(WorldSessionTests.Move(77, 10, 10, 12, 10, _rig.Clock.Now));
            Assert.AreEqual(12, _rig.Get(77).CellX, "no cells to find a path on: the unit is placed where it goes");
        }

        [Test]
        public void OwnCharacter_Vanish_DiedPacket_LiesDead()
        {
            _rig.ToInGame(Character());
            _rig.Client.Deliver(WorldSessionTests.VanishPacket(Id, 1));
            Assert.AreEqual(EntityState.Dead, Own.State);
            Assert.IsFalse(_rig.Session.Own.CanAct);
        }
    }
}
