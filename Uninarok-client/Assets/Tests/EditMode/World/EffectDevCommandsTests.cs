using System;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.Effects;
using Uninarok.World.Dev;

namespace Uninarok.World.Tests
{
    // SPEC-0012 FR-8 (PLAN-0012 4.8): messages and the list of handles /effect started, over a fake backend.
    public class EffectDevCommandsTests
    {
        sealed class Fake : IEffectBackend
        {
            public bool IsReady { get; set; } = true;
            public bool IsLoading { get; set; }
            public bool HasCharacter { get; set; } = true;
            public bool HasMap { get; set; } = true;
            public int Width = 20, Height = 20;
            public HashSet<string> Known = new HashSet<string> { "texture/effect/firewall.str" };
            public int Next = 1;
            public List<EffectHandle> Stopped = new List<EffectHandle>();
            public int StopAllCalls;
            public int Loads;
            public Action<string> Done;
            public string LastPlay;
            public bool Fails;

            public void BeginLoad(Action<string> done) { Loads++; IsLoading = true; Done = done; }
            public bool InMap(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
            public bool Resolve(string path, out string key) { key = path; return Known.Contains(path); }
            public EffectHandle PlayOnSelf(string path, bool loop) { LastPlay = "self " + path + loop; return Fails ? default : new EffectHandle(Next, Next++); }
            public EffectHandle PlayAtCell(string path, int x, int y, bool loop) { LastPlay = $"cell {path} {x} {y} {loop}"; return Fails ? default : new EffectHandle(Next, Next++); }
            public void Stop(EffectHandle handle) => Stopped.Add(handle);
            public void StopAll() => StopAllCalls++;
        }

        Fake _fake;
        List<string> _shown;
        EffectDevCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _fake = new Fake();
            _shown = new List<string>();
            _commands = new EffectDevCommands(_fake, _shown.Add);
        }

        [Test]
        public void PlayAtCell_SaysPlayingKey_AndKeepsTheHandle()
        {
            Assert.AreEqual("playing texture/effect/firewall.str", _commands.Run("texture/effect/firewall.str 10 10 loop"));
            Assert.AreEqual("cell texture/effect/firewall.str 10 10 True", _fake.LastPlay);
            Assert.AreEqual(1, _commands.Started);
        }

        [Test]
        public void UnknownPath_SaysUnknownPath()
        {
            var message = _commands.Run("texture/effect/nope.str 1 1");
            StringAssert.StartsWith("unknown effect path: texture/effect/nope.str", message);
        }

        [Test]
        public void NoMap_NoCharacter_OutOfMap()
        {
            _fake.HasMap = false;
            Assert.AreEqual("no map", _commands.Run("a.str 1 1"));
            _fake.HasMap = true;
            Assert.AreEqual("cell -1 5 is outside the map", _commands.Run("a.str -1 5"));
            Assert.AreEqual("cell 20 0 is outside the map", _commands.Run("a.str 20 0"));
            _fake.HasCharacter = false;
            Assert.AreEqual("no character", _commands.Run("a.str"));
            Assert.AreEqual(0, _commands.Started);
        }

        [Test]
        public void Stop_StopsOnlyItsOwnHandles_AndCounts()
        {
            _commands.Run("texture/effect/firewall.str");
            _commands.Run("texture/effect/firewall.str 2 3 loop");
            Assert.AreEqual("stopped 2", _commands.Run("stop"));
            Assert.AreEqual(2, _fake.Stopped.Count);
            Assert.AreEqual(0, _commands.Started);
            Assert.AreEqual("stopped 0", _commands.Run("stop"));
        }

        [Test]
        public void NothingPlayed_IsNotKept()
        {
            _fake.Fails = true;
            StringAssert.StartsWith("nothing was played", _commands.Run("texture/effect/firewall.str"));
            Assert.AreEqual(0, _commands.Started);
        }

        [Test]
        public void MapChange_StopsAllAndForgetsTheHandles()
        {
            _commands.Run("texture/effect/firewall.str");
            _commands.OnMapChanged();
            Assert.AreEqual(1, _fake.StopAllCalls);
            Assert.AreEqual(0, _commands.Started);
        }

        [Test]
        public void FirstUse_LoadsOnce_ThenPlaysTheCommandWhenLoaded()
        {
            _fake.IsReady = false;
            StringAssert.Contains("loading", _commands.Run("texture/effect/firewall.str 10 10 loop"));
            StringAssert.Contains("loading", _commands.Run("texture/effect/firewall.str 10 10 loop"), "no second load while one runs");
            Assert.AreEqual(1, _fake.Loads);
            _fake.IsReady = true;
            _fake.IsLoading = false;
            _fake.Done(null);
            Assert.AreEqual("playing texture/effect/firewall.str", _shown[_shown.Count - 1]);
            Assert.AreEqual(1, _commands.Started);
        }

        [Test]
        public void LoadFailure_IsReported()
        {
            _fake.IsReady = false;
            _commands.Run("texture/effect/firewall.str");
            _fake.IsLoading = false;
            _fake.Done("catalog: gone");
            Assert.AreEqual("effects could not be loaded: catalog: gone", _shown[_shown.Count - 1]);
        }

        [Test]
        public void BadArguments_GiveTheUsage()
        {
            StringAssert.StartsWith("usage:", _commands.Run(""));
        }
    }
}
