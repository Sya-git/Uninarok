using NUnit.Framework;
using Unity.Collections;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Managers;

namespace Uninarok.Networking.Tests
{
    // T6.1 / SPEC-0001 FR-12, FR-13: the shared session record and the small types the flow is built from.
    public class SessionContextTests
    {
        [Test]
        public void Reset_ClearsEverything()
        {
            var ctx = new SessionContext
            {
                AccountId = 2000001, AuthCode = 5, UserLevel = 99, Sex = AccountSex.SEX_MALE,
                Slots = new CharacterSlots(9, 6, 0, 0), CharacterId = 140593, SelectedSlot = 2,
                Map = new MapEndpoint("10.0.0.2", 40000, new FixedString32Bytes("prontera"), new FixedString512Bytes("host")),
                Entry = new MapEntry(123, new UninarokPosition(150, 200, UninarokDirection.South), 5, 5),
            };
            ctx.Servers.Add(new ServerEndpoint("127.0.0.1", 6121, new FixedString32Bytes("s"), 1, 0, 0));
            ctx.Characters.Add(new CharInfo { CharId = 1 });

            ctx.Reset();

            Assert.AreEqual(0u, ctx.AccountId);
            Assert.AreEqual(0, ctx.AuthCode);
            Assert.AreEqual(0u, ctx.UserLevel);
            Assert.AreEqual(0, ctx.Servers.Count);
            Assert.AreEqual(0, ctx.Characters.Count);
            Assert.AreEqual(0, ctx.CharacterId);
            Assert.AreEqual(0, ctx.SelectedSlot);
            Assert.IsNull(ctx.Map.Host);
            Assert.AreEqual(0, ctx.Map.Port);
            Assert.AreEqual(0, ctx.Slots.CharSlots);
            Assert.AreEqual(0u, ctx.Entry.StartTick);
        }

        [Test]
        public void MapEndpoint_HoldsPortsAbove32767_Unchanged()
        {
            Assert.AreEqual(65535, new MapEndpoint("h", 65535, default, default).Port);
            Assert.AreEqual(40000, new MapEndpoint("h", 40000, default, default).Port);
        }

        [Test]
        public void PinState_UnknownIsMinusOne_NotADefaultZero()
        {
            Assert.AreEqual(-1, (int)PinState.Unknown);
            Assert.AreNotEqual(default(PinState), PinState.Unknown, "default(PinState) is Ok (0): the manager must set Unknown explicitly");
            Assert.AreEqual(7, (int)PinState.Unused);
        }

        [Test]
        public void FlowFailure_KeepsCodeAndMessage()
        {
            var f = new FlowFailure(FailureCode.LoginRefused, "wrong password");
            Assert.AreEqual(FailureCode.LoginRefused, f.Code);
            StringAssert.Contains("wrong password", f.ToString());
        }

        [Test]
        public void LoginRefusal_DescribesItself()
        {
            var r = new LoginRefusal(LoginRefusalSource.Refusal, 6, new FixedString64Bytes("2026-12-31"));
            StringAssert.Contains("2026-12-31", r.ToString());
            Assert.AreEqual(6, r.ErrorCode);
        }

        [Test]
        public void FakeClock_MovesOnlyWhenTold()
        {
            var clock = new FakeClock();
            Assert.AreEqual(0, clock.NowMs);
            clock.AdvanceSeconds(1.5);
            clock.AdvanceMs(500);
            Assert.AreEqual(2000, clock.NowMs);
        }
    }
}
