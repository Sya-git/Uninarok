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

        // T8.9 (FR-7, plan D38): the warp sparkle, camera-facing on pok1.tga, drew nothing because its serialized SizeScale was an
        // empty curve (size 0). At frame 38 (alpha 1) the glow must show.
        [Test]
        public void WarpSparkleFrame38_Draws_FR7()
        {
            Assert.That(CountNonBackdrop("texture/effect/_authored/warp_portal/sparkle.quad", 38), Is.GreaterThan(100),
                "the sparkle glow is drawn");
        }

        // T8.10 (FR-7, plan D39): the sparkle's glow ends smoothly: the ring just outside the glow (the clipped alpha tail of
        // pok1.tga) equals the backdrop, no square outline, while the centre is bright.
        [Test]
        public void WarpSparkleFrame38_NoSquareOutline_FR7()
        {
            var px = RenderPixels("texture/effect/_authored/warp_portal/sparkle.quad", 38, out int size);
            var bg = px[0];
            int cx = 0, cy = 0, best = -1;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var p = px[y * size + x]; int v = p.r + p.g + p.b;
                if (v > best) { best = v; cx = x; cy = y; }
            }
            Assert.That(best / 3 - (bg.r + bg.g + bg.b) / 3, Is.GreaterThan(60), "bright centre");
            // quad edge = 50 px / 35 per cell at the stage's scale; scan a square ring of half side r around the peak: the tail
            // outside the glow must stay within 3 levels of the backdrop at the far ring (where only the clipped tail lived)
            int worst = 0;
            for (int r = 20; r <= 40; r += 4)
                for (int t = -r; t <= r; t += 2)
                    foreach (var (x, y) in new[] { (cx + t, cy - r), (cx + t, cy + r), (cx - r, cy + t), (cx + r, cy + t) })
                    {
                        if (x < 0 || y < 0 || x >= size || y >= size) continue;
                        var p = px[y * size + x];
                        worst = Mathf.Max(worst, Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b));
                    }
            Debug.Log("[T8.10] worst ring deviation " + worst + " centre " + cx + "," + cy);
            Assert.That(worst, Is.LessThanOrEqualTo(9), "backdrop-equal outside the glow");
        }

        // T8.11 (FR-6, FR-11, plan D40): switching the effect leaves nothing of the previous one on the stage. The pooled slot's
        // object is reused by the next play; a sprite or model part never writes its mesh, so the last STR / authored frame stayed.
        const string StrKey = "texture/effect/angelus.str";
        const string QuadKey = "texture/effect/_authored/warp_portal/sparkle.quad";
        const string SpriteKey = "sprite/이팩트/fireball.act";

        [TestCase(StrKey, 59, SpriteKey, 10)]
        [TestCase(QuadKey, 38, SpriteKey, 10)]
        [TestCase(SpriteKey, 10, StrKey, 59)]
        [TestCase(QuadKey, 38, StrKey, 59)]
        // (a mesh effect after a mesh effect is not compared: the authored sparkle seeds its random start from the slot generation)
        public void SwitchEffect_RendersLikeAFreshStage_FR11(string first, int firstFrame, string second, int secondFrame)
        {
            var fresh = RenderPixels(second, secondFrame, out int size);
            stage.Dispose(); stage = null;

            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            stage = new EffectPreviewStage(EffectCatalog.Create(index, new AssetDatabaseEffectSource()));
            stage.Distance = 10f;
            var h1 = stage.Play(first, loop: true);
            Assert.That(stage.Runtime.IsActive(h1), Is.True, first);
            stage.Tick(0.01f);
            stage.Runtime.Seek(h1, firstFrame / 60f);
            stage.Tick(0f);
            stage.Render(new Rect(0, 0, 256, 256));
            stage.Runtime.Stop(h1);

            var after = RenderPixels(stage, second, secondFrame);
            int differing = 0;
            for (int i = 0; i < fresh.Length; i++)
            {
                var a = fresh[i]; var b = after[i];
                if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 12) differing++;
            }
            Assert.That(differing, Is.EqualTo(0), "pixels of " + first + " left on the stage after switching to " + second);
        }

        // stopped and nothing else playing: the stage is the backdrop again
        [TestCase(StrKey, 59)]
        [TestCase(QuadKey, 38)]
        public void StopEffect_LeavesOnlyTheBackdrop_FR6(string key, int frame)
        {
            Assert.That(CountNonBackdrop(key, frame), Is.GreaterThan(100), "the effect draws before the stop");
            stage.Runtime.StopAll();
            var px = RenderPixels(stage, null, 0);
            var bg = px[0];
            int n = 0;
            foreach (var p in px) if (Mathf.Abs(p.r - bg.r) + Mathf.Abs(p.g - bg.g) + Mathf.Abs(p.b - bg.b) > 12) n++;
            Assert.That(n, Is.EqualTo(0), "nothing drawn after StopAll");
        }

        // renders the stage after (optionally) playing key at frame; null key renders as is
        Color32[] RenderPixels(EffectPreviewStage s, string key, int frame)
        {
            if (key != null)
            {
                var handle = s.Play(key, loop: true);
                Assert.That(s.Runtime.IsActive(handle), Is.True, key);
                s.Tick(0.01f);
                s.Runtime.Seek(handle, frame / 60f);
                s.Tick(0f);
            }
            var window = s.Render(new Rect(0, 0, 256, 256));
            var rt = RenderTexture.GetTemporary(window.width, window.height, 0, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(window.width, window.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(window, rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, window.width, window.height), 0, 0);
                tex.Apply(false);
                return tex.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
            }
        }

        Color32[] RenderPixels(string key, int frame, out int size)
        {
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            stage = new EffectPreviewStage(EffectCatalog.Create(index, new AssetDatabaseEffectSource()));
            stage.Distance = 10f;
            var handle = stage.Play(key, loop: true);
            Assert.That(stage.Runtime.IsActive(handle), Is.True, key);
            stage.Tick(0.01f);
            stage.Runtime.Seek(handle, frame / 60f);
            stage.Tick(0f);
            var window = stage.Render(new Rect(0, 0, 256, 256));
            size = window.width;
            var rt = RenderTexture.GetTemporary(window.width, window.height, 0, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(window.width, window.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(window, rt);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, window.width, window.height), 0, 0);
                tex.Apply(false);
                return tex.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tex);
            }
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
