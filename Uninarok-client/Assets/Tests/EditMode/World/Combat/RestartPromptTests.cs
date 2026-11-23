using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Map;
using static Uninarok.World.Tests.WorldSessionTests;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-8 (PLAN-0011 4.11): the death prompt through the simulated connection, no view.
    public class RestartPromptTests
    {
        const uint Monster = 0x068EB5EC;

        Rig _rig;
        CombatSettings _settings;
        int _changes;

        [SetUp]
        public void SetUp()
        {
            _rig = new Rig().ToInGame(OwnCharacterTests.Character());
            _settings = ScriptableObject.CreateInstance<CombatSettings>();
            _rig.Session.UseCombat(_settings);
            Prompt.Changed += () => _changes++;
            _changes = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
            Object.DestroyImmediate(_settings);
        }

        RestartPrompt Prompt => _rig.Session.Restart;
        uint Own => _rig.Flow.Context.AccountId;
        uint Now => _rig.Clock.Now;
        int Restarts => _rig.Client.SentCount(PacketIds.RESTART);

        void Frame(int ms = 50)
        {
            _rig.Local.AdvanceMs(ms);
            _rig.Model?.Advance(Now);
            _rig.Session.Timeline.Advance(Now);
            Prompt.Update(Now);
        }

        void Die() => _rig.Client.Deliver(VanishPacket(Own, 1));

        void Raise() => _rig.Client.Deliver(new RESURRECTION(Hex(Le(Own) + "0000")));

        [Test]
        public void RestartPrompt_OpensOnDeath()
        {
            Frame();
            Assert.IsFalse(Prompt.IsOpen, "alive");
            _rig.Client.Deliver(Stand(Monster, 151, 200));
            // The killing blow lands at 500 ms; a player's death is shown at its hit (FR-9, CombatTimeline_OwnDeath).
            uint sent = Now;
            _rig.Client.Deliver(new DAMAGE(Hex(Le(Monster) + Le(Own) + Le(Now) + Le(500u) + Le(300u) + Le(10u) + "00" + Le16(1) + "00" + Le(0u))));
            Die();
            int opened = -1;
            while (opened < 0 && ServerClock.Diff(Now, sent) < 1500)
            {
                Frame();
                if (Prompt.IsOpen) opened = ServerClock.Diff(Now, sent);
            }
            Assert.That(opened, Is.InRange(500, 550), "dead and shown: at the hit, within one frame of 50 ms");
            Assert.AreEqual(1, _changes);
            Assert.AreEqual(0, Restarts, "it sends nothing by itself");
        }

        [Test]
        public void RestartPrompt_EntersTheMapDead_Opens()
        {
            Die();
            Frame();
            Assert.IsTrue(Prompt.IsOpen, "no blow to wait for");
        }

        [Test]
        public void RestartPrompt_CloseThenEsc()
        {
            Prompt.ToggleByEscape();
            Frame();
            Assert.IsFalse(Prompt.IsOpen, "Esc does nothing while alive");
            Die();
            Frame();
            Prompt.Close();
            Frame(1000);
            Assert.IsFalse(Prompt.IsOpen, "closed stays closed");
            Prompt.ToggleByEscape();
            Assert.IsTrue(Prompt.IsOpen, "Esc opens it again");
            Frame();
            Prompt.ToggleByEscape();
            Frame();
            Assert.IsFalse(Prompt.IsOpen, "and closes it");
            Prompt.ToggleByEscape();
            Frame();
            Assert.IsTrue(Prompt.IsOpen);
            Assert.AreEqual(0, Restarts);
        }

        [Test]
        public void RestartPrompt_SendsOnce()
        {
            Die();
            Frame();
            Assert.IsTrue(Prompt.ReturnToSavePoint());
            Assert.AreEqual(1, Restarts);
            Assert.AreEqual(Hex("B200" + "00"), _rig.Client.Sent[_rig.Client.Sent.Count - 1].Bytes);
            Assert.IsFalse(Prompt.IsOpen, "hidden while it waits");
            Assert.IsFalse(Prompt.ReturnToSavePoint(), "a second press sends nothing");
            Prompt.ToggleByEscape();
            Frame();
            Assert.IsFalse(Prompt.IsOpen, "Esc does not open it while it waits");
            Assert.AreEqual(1, Restarts);
        }

        [Test]
        public void RestartPrompt_ResurrectionCloses()
        {
            Die();
            Frame();
            Assert.IsTrue(Prompt.IsOpen);
            Raise();
            Frame();
            Assert.IsFalse(Prompt.IsOpen, "standing again");
            Die();
            Frame();
            Assert.IsTrue(Prompt.IsOpen, "a new death opens it");
            Prompt.Close();
            Raise();
            Frame();
            Die();
            Frame();
            Assert.IsTrue(Prompt.IsOpen, "closing lasts one death");
        }

        [Test]
        public void RestartPrompt_FeignedDeathDoesNotOpen()
        {
            _rig.Client.Deliver(VanishPacket(Own, 4));
            Frame();
            Assert.IsFalse(Prompt.IsOpen);
            Prompt.ToggleByEscape();
            Frame();
            Assert.IsFalse(Prompt.IsOpen, "nor with Esc");
        }

        [Test]
        public void RestartPrompt_TimeoutReopens()
        {
            Die();
            Frame();
            Prompt.ReturnToSavePoint();
            for (int t = 0; t < 2950; t += 50) Frame();
            Assert.IsFalse(Prompt.IsOpen, "2,950 ms: still waiting");
            Frame(100);
            Assert.IsTrue(Prompt.IsOpen, "no answer within 3,000 ms: open again");
            Assert.IsTrue(Prompt.ReturnToSavePoint());
            Assert.AreEqual(2, Restarts, "it may ask again");
        }

        [Test]
        public void RestartPrompt_LeavingTheMap_Closes()
        {
            Die();
            Frame();
            Prompt.ReturnToSavePoint();
            _rig.Session.EndMap(); // the server moves the character to its save point
            Prompt.Update(Now);
            Assert.IsFalse(Prompt.IsOpen);
            Frame(5000);
            Assert.IsFalse(Prompt.IsOpen, "no own character: nothing to wait for");
        }
    }
}
