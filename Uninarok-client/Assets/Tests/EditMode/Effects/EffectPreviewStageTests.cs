using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Effects.Editor;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// FR-11: an effect asset is loaded through <see cref="AssetDatabaseEffectSource"/> and played in an
    /// <see cref="EffectPreviewStage"/>. The firewall.str asset does not exist until the STR conversion (T3.8 to T3.12, owner gate);
    /// until then the test runs against an already imported effect asset, a sprite entry.
    /// </summary>
    public sealed class EffectPreviewStageTests
    {
        // an imported effect asset that exists today (SPEC-0008 sprite entry); replace by firewall.str when T3.11 has produced it
        const string AssetPath = "Assets/Art/Imported/effect/sprite/이팩트/05vallentine.act.asset";

        EffectPreviewStage stage;

        [TearDown] public void TearDown() { stage?.Dispose(); stage = null; }

        EffectCatalog CatalogOf(out string key)
        {
            var asset = AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null, "effect asset present at " + AssetPath);
            key = asset.Path;
            var index = ScriptableObject.CreateInstance<EffectCatalogIndex>();
            index.hideFlags = HideFlags.HideAndDontSave;
            index.Keys = new[] { key };
            index.Assets = new[] { new EffectAssetRef { Guid = AssetDatabase.AssetPathToGUID(AssetPath) } };
            index.SourceMissing = new[] { false };
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
