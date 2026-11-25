using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Effects.Editor;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// FR-11: an effect asset is loaded through <see cref="AssetDatabaseEffectSource"/> and played in an
    /// <see cref="EffectPreviewStage"/>; the play test uses firewall.str resolved through the real effect index.
    /// </summary>
    public sealed class EffectPreviewStageTests
    {
        // an imported sprite effect asset (SPEC-0008), used by the GUID source test
        const string AssetPath = "Assets/Art/Imported/effect/sprite/이팩트/05vallentine.act.asset";

        EffectPreviewStage stage;

        [TearDown] public void TearDown() { stage?.Dispose(); stage = null; }

        // the real catalog index (T6.9) and the imported firewall.str asset (T3.14)
        const string FirewallKey = "texture/effect/firewall.str";

        EffectCatalog CatalogOf(out string key)
        {
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            Assert.That(index, Is.Not.Null, "effect index present at " + EffectCatalog.IndexKey);
            key = FirewallKey;
            Assert.That(System.Array.IndexOf(index.Keys, key), Is.GreaterThanOrEqualTo(0), "index lists " + key);
            return EffectCatalog.Create(index, new AssetDatabaseEffectSource());
        }

        [Test]
        public void SourceLoadsAssetByGuid_FR11()
        {
            var source = new AssetDatabaseEffectSource();
            EffectAsset loaded = null; bool called = false;
            source.Load(new EffectAssetRef { Guid = AssetDatabase.AssetPathToGUID(AssetPath) }, a => { called = true; loaded = a; });
            Assert.That(called, Is.True);
            Assert.That(loaded, Is.Not.Null);

            called = false; loaded = new EffectAsset();
            source.Load(new EffectAssetRef { Guid = "00000000000000000000000000000000" }, a => { called = true; loaded = a; });
            Assert.That(called, Is.True);
            Assert.That(loaded, Is.Null, "unknown GUID reports null");
        }

        [Test]
        public void PlayInStage_ActiveCountOne_FR11()
        {
            var catalog = CatalogOf(out string key);
            stage = new EffectPreviewStage(catalog);
            Assert.That(stage.Camera.fieldOfView, Is.EqualTo(15f));

            var handle = stage.Play(key, loop: true);
            Assert.That(stage.Runtime.ActiveCount, Is.EqualTo(1));
            Assert.That(stage.Runtime.IsActive(handle), Is.True);

            stage.Tick(0.1f);
            foreach (EffectPreviewStage.BackgroundKind b in System.Enum.GetValues(typeof(EffectPreviewStage.BackgroundKind)))
            {
                stage.Background = b;
                Assert.That(stage.Render(new Rect(0, 0, 64, 64)), Is.Not.Null);
            }
            stage.Runtime.Stop(handle);
            Assert.That(stage.Runtime.ActiveCount, Is.EqualTo(0));
        }
    }
}
