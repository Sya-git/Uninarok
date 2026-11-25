using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
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

        // T8.5, FR-11: sprite effects show their real sprites in the preview (no sprite catalog warning, no placeholder)
        [TestCase("sprite/이팩트/05vallentine.act")]
        [TestCase("sprite/이팩트/fireball.act")]
        public void SpriteEffect_DrawsRealSprite_FR11(string key)
        {
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            Assert.That(index, Is.Not.Null);
            stage = new EffectPreviewStage(EffectCatalog.Create(index, new AssetDatabaseEffectSource()));
            string warning = null;
            stage.Runtime.Warned += (k, m) => warning = m;
            var handle = stage.Play(key, loop: true);
            Assert.That(stage.Runtime.IsActive(handle), Is.True, key);
            for (int i = 0; i < 5; i++) stage.Tick(0.05f);
            Assert.That(warning, Is.Null, "no warning (placeholder) for " + key);
            Assert.That(stage.Runtime.SpriteMaterial, Is.Not.Null, "sprite material");
            Assert.That(stage.DrawsSprite(handle), Is.True, "drawable sprite part for " + key);
        }

        [Test]
        public void SpriteEffect_RenderIsNotFlat_FR11()
        {
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            stage = new EffectPreviewStage(EffectCatalog.Create(index, new AssetDatabaseEffectSource()));
            var handle = stage.Play("sprite/이팩트/fireball.act", loop: true);
            for (int i = 0; i < 10; i++) stage.Tick(0.05f);
            stage.Distance = 6f;
            byte[] png = EffectEvidence.CapturePng(stage, 512, 512);
            var tex = new Texture2D(2, 2);
            Assert.That(tex.LoadImage(png), Is.True);
            var px = tex.GetPixels32();
            var bg = px[0];
            int diff = 0;
            foreach (var p in px) if (System.Math.Abs(p.r - bg.r) + System.Math.Abs(p.g - bg.g) + System.Math.Abs(p.b - bg.b) > 24) diff++;
            Object.DestroyImmediate(tex);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "preview-sprite");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "fireball.png"), png);
            Debug.Log("[T8.5] non-background pixel ratio " + (diff / (float)px.Length).ToString("F4") + " of " + px.Length);
            Assert.That(diff, Is.GreaterThan(px.Length / 200), "something is drawn");
        }

        // T8.6, FR-3, plan D33: the most common STR pair 5 > 7 (SrcAlpha / DstAlpha) drawn into the window's HDR target
        // (ARGBHalf, alpha not clamped) must not brighten whole quads. Before D33 the destination alpha grew above 1 inside
        // earlier quads and the DstAlpha factor amplified them to white rectangles; the real client's backbuffer has no alpha.
        [Test]
        public void HastyFireFrame32_WindowTarget_HasNoWhiteQuads_FR3()
        {
            const string key = "texture/effect/night_watch/nw_hasty_fire_in_the_hole/hasty_fire_in_the_hole/hasty_fire_in_the_hole_7x7.str";
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            Assert.That(index, Is.Not.Null);
            stage = new EffectPreviewStage(EffectCatalog.Create(index, new AssetDatabaseEffectSource()));
            stage.Distance = 10f;
            var handle = stage.Play(key, loop: true);
            Assert.That(stage.Runtime.IsActive(handle), Is.True, key);
            stage.Tick(0.01f);
            stage.Runtime.Seek(handle, 32f / 60f);
            stage.Tick(0f);

            var window = stage.Render(new Rect(0, 0, 256, 256));   // the preview window's own target
            var rt = RenderTexture.GetTemporary(window.width, window.height, 0, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(window.width, window.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(window, rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, window.width, window.height), 0, 0);
                tex.Apply(false);
                int white = 0;
                foreach (var p in tex.GetPixels32()) if (p.r > 220 && p.g > 220 && p.b > 200) white++;
                Debug.Log("[T8.6] near-white pixels " + white + " of " + (window.width * window.height));
                Assert.That(white, Is.LessThan(window.width * window.height / 500), "no near-white quads at frame 32");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
            }
        }

        // T8.8 (FR-2, FR-3, plan D37): angelus.str layers 1 and 8 use One / Zero on colour-keyed BMPs; the keyed texels must draw
        // nothing, so the 64 px quad of frame 59 shows the bell only, not the texture's RGB under the key.
        [Test]
        public void AngelusFrame59_KeyedBackground_DrawsNothing_FR3()
        {
            Assert.That(CountNonBackdrop("texture/effect/angelus.str", 59), Is.LessThan(256 * 256 / 25),
                "only the bell, not the quad's background");
        }

        int CountNonBackdrop(string key, int frame)
        {
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            Assert.That(index, Is.Not.Null);
            stage = new EffectPreviewStage(EffectCatalog.Create(index, new AssetDatabaseEffectSource()));
            stage.Distance = 10f;
            var handle = stage.Play(key, loop: true);
            Assert.That(stage.Runtime.IsActive(handle), Is.True, key);
            stage.Tick(0.01f);
            stage.Runtime.Seek(handle, frame / 60f);
            stage.Tick(0f);
            var window = stage.Render(new Rect(0, 0, 256, 256));
            var rt = RenderTexture.GetTemporary(window.width, window.height, 0, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(window.width, window.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(window, rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, window.width, window.height), 0, 0);
                tex.Apply(false);
                var px = tex.GetPixels32();
                var bg = px[0];
                int n = 0;
                foreach (var p in px) if (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) > 30) n++;
                Debug.Log("[T8.8] non-backdrop pixels " + n + " of " + px.Length + " (" + key + " f" + frame + ")");
                return n;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
            }
        }
    }
}
