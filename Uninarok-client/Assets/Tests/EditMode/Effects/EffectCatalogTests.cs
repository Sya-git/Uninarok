using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Uninarok.Effects.Tests
{
    /// <summary>EffectCatalog lookup, prefix forms, warning and allocation rules (SPEC-0012 FR-5, FR-9, plan 4.4 / 6).</summary>
    public class EffectCatalogTests
    {
        sealed class FakeSource : IEffectAssetSource
        {
            public readonly List<string> Loads = new List<string>();
            public readonly List<string> Releases = new List<string>();
            public readonly Dictionary<string, EffectAsset> Assets = new Dictionary<string, EffectAsset>();
            public bool Deferred;
            public Action<EffectAsset> Pending;

            public void Load(EffectAssetRef r, Action<EffectAsset> done)
            {
                Loads.Add(r.Guid);
                if (Deferred) { Pending = done; return; }
                Assets.TryGetValue(r.Guid, out var a);
                done(a);
            }

            public void Release(EffectAssetRef r) { Releases.Add(r.Guid); }
        }

        static readonly string BS = ((char)92).ToString();

        static readonly string[] Keys =
        {
            "texture/effect/firewall.str",
            "texture/effect/ring_blue.tga",
            "texture/effect/_authored/warp_portal/ring_outer.cylinder",
            "sprite/이팩트/fireball.act",
            "texture/loose.str",
        };

        FakeSource source;
        EffectCatalogIndex index;
        EffectCatalog catalog;
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            source = new FakeSource();
            index = ScriptableObject.CreateInstance<EffectCatalogIndex>();
            created.Add(index);
            index.Keys = (string[])Keys.Clone();
            index.Assets = new EffectAssetRef[Keys.Length];
            index.SourceMissing = new bool[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                index.Assets[i] = new EffectAssetRef { Guid = "guid" + i };
                var a = ScriptableObject.CreateInstance<EffectAsset>();
                a.Path = Keys[i];
                created.Add(a);
                source.Assets["guid" + i] = a;
            }
            catalog = EffectCatalog.Create(index, source);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void ExactKey_Resolves()
        {
            Assert.IsTrue(catalog.TryResolve("texture/effect/firewall.str", out var e));
            Assert.AreEqual(0, e.Index);
        }

        [Test]
        [TestCase("effect/firewall.str")]
        [TestCase("FIREWALL.STR")]
        [TestCase("Data/Texture/Effect/FireWall.str")]
        [TestCase("firewall.str")]
        public void AllForms_ReachOneEntry(string raw)
        {
            Assert.IsTrue(catalog.TryResolve(raw, out var e), raw);
            Assert.AreEqual(0, e.Index, raw);
        }

        [Test]
        public void DataBackslashForm_ReachesTheSameEntry()
        {
            string raw = "data" + BS + "texture" + BS + "effect" + BS + "firewall.str";
            Assert.IsTrue(catalog.TryResolve(raw, out var e));
            Assert.AreEqual(0, e.Index);
        }

        [Test]
        public void PrefixForms_ReachTheirKeys()
        {
            Assert.IsTrue(catalog.TryResolve("ring_blue.tga", out var ring));      // texture/effect/
            Assert.AreEqual(1, ring.Index);
            Assert.IsTrue(catalog.TryResolve("loose.str", out var loose));         // texture/
            Assert.AreEqual(4, loose.Index);
            Assert.IsTrue(catalog.TryResolve("이팩트/fireball.act", out var sprite));   // sprite/
            Assert.AreEqual(3, sprite.Index);
            Assert.IsTrue(catalog.TryResolve("data" + BS + "sprite" + BS + "이팩트" + BS + "fireball.act", out var sprite2));
            Assert.AreEqual(3, sprite2.Index);
        }

        [Test]
        public void UnknownPath_FalseWithOneWarning_EvenOnRepeat()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown path.*nope[.]str"));
            Assert.IsFalse(catalog.TryResolve("nope.str", out _));
            Assert.IsFalse(catalog.TryResolve("nope.str", out _));
            Assert.IsFalse(catalog.TryResolve("NOPE.STR", out _));   // same key, other raw form: still one warning
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void RepeatedTryResolve_AllocatesNothing()
        {
            string hit = "effect/firewall.str";
            string miss = "nope_alloc.str";
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("nope_alloc"));
            catalog.TryResolve(hit, out _);
            catalog.TryResolve(miss, out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                catalog.TryResolve(hit, out _);
                catalog.TryResolve(miss, out _);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before);
        }

        [Test]
        public void RequestAsset_LoadsOnceAndExposesAsset()
        {
            catalog.TryResolve("firewall.str", out var e);
            Assert.AreEqual(EffectAssetState.NotLoaded, catalog.GetState(e));
            EffectAsset got = null;
            catalog.RequestAsset(e, a => got = a);
            catalog.RequestAsset(e);
            Assert.AreEqual(1, source.Loads.Count);
            Assert.AreEqual(EffectAssetState.Loaded, catalog.GetState(e));
            Assert.IsTrue(catalog.TryGetAsset(e, out var asset));
            Assert.AreSame(asset, got);
            Assert.AreEqual("texture/effect/firewall.str", asset.Path);
        }

        [Test]
        public void RequestAsset_Deferred_IsLoadingUntilCallback()
        {
            source.Deferred = true;
            catalog.TryResolve("firewall.str", out var e);
            catalog.RequestAsset(e);
            Assert.AreEqual(EffectAssetState.Loading, catalog.GetState(e));
            Assert.IsFalse(catalog.TryGetAsset(e, out _));
            source.Pending(source.Assets["guid0"]);
            Assert.AreEqual(EffectAssetState.Loaded, catalog.GetState(e));
        }

        [Test]
        public void RequestAsset_SourceMissing_FailsWithoutCallingSource()
        {
            index.SourceMissing[1] = true;
            catalog.TryResolve("ring_blue.tga", out var e);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("source file is missing"));
            catalog.RequestAsset(e);
            Assert.AreEqual(EffectAssetState.Failed, catalog.GetState(e));
            Assert.AreEqual(0, source.Loads.Count);
        }

        [Test]
        public void Release_CallsTheSourceForLoadedEntries()
        {
            catalog.TryResolve("firewall.str", out var e);
            catalog.TryResolve("loose.str", out var other);
            catalog.RequestAsset(e);
            catalog.Release();
            CollectionAssert.AreEqual(new[] { "guid0" }, source.Releases);
            Assert.AreEqual(EffectAssetState.NotLoaded, catalog.GetState(e));
            Assert.AreEqual(EffectAssetState.NotLoaded, catalog.GetState(other));
            Assert.IsFalse(catalog.TryGetAsset(e, out _));
        }
    }
}
