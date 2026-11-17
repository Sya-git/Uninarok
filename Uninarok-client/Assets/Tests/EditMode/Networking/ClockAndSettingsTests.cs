using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Client;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-10, FR-14: defaults of the timeouts and keepalives (spec section 6), and the clock contract.
    public class ClockAndSettingsTests
    {
        [Test]
        public void Settings_Defaults_MatchTheSpec()
        {
            var s = new NetworkSettings();
            Assert.AreEqual(15f, s.ConnectSeconds);
            Assert.AreEqual(15f, s.ReplySeconds);
            Assert.AreEqual(60f, s.MapLoadSeconds);
            Assert.AreEqual(10f, s.MapKeepaliveSeconds, "map keepalive every 10 s (FR-14)");
            Assert.AreEqual(12f, s.CharacterKeepaliveSeconds, "character keepalive every 12 s (FR-14)");
        }

        [Test]
        public void Settings_AreSerializable()
        {
            var json = JsonUtility.ToJson(new NetworkSettings { MapKeepaliveSeconds = 7f });
            var back = JsonUtility.FromJson<NetworkSettings>(json);
            Assert.AreEqual(7f, back.MapKeepaliveSeconds);
        }

        [Test]
        public void UnityClock_IsMonotonic_InMilliseconds()
        {
            IClock clock = new UnityClock();
            long a = clock.NowMs;
            Thread.Sleep(30);
            long b = clock.NowMs;
            Assert.GreaterOrEqual(b, a);
            Assert.GreaterOrEqual(b - a, 20, "about 30 ms passed");
            Assert.Less(b - a, 2000);
        }
    }
}
