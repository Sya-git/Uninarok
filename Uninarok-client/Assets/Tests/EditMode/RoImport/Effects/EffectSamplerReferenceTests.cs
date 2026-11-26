using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Tests.Sprites;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-3, FR-10 (PLAN-0012 4.2, 6): <see cref="EffectSampler"/> on the 10 reference STR files, read through GRF
    /// (<see cref="RoStrReader"/>, then <see cref="RoStrCompiler"/>; the test never parses RO bytes). At the first, a middle and
    /// the last frame of every layer, corners, position, colour, angle, texture frame and blend equal an oracle written here
    /// from the <see cref="RoStr"/> keys with the str-reference.md section 6 formulas (owner decision O1: GRF's interpolation
    /// does not apply delta keys, so the formulas on RoStr are the oracle). The oracle does not call EffectSampler.
    ///
    /// Owner decisions of 2026-11-25 (str-reference.md section 8):
    ///  - O2: length is FrameCount / Fps and the last frame is FrameCount - 1; a layer's last compared frame is the frame
    ///    of its last key strictly below FrameCount.
    ///  - O3: anitype 0 holds the base key's aniframe.
    ///  - O4: anitypes 2..4 use the base key's delay.
    ///  - O5: anitypes 5 and 6 hold the base key's aniframe (none exists in the reference files, so those layers are skipped).
    /// </summary>
    [Category(SpriteTestFiles.Category)]
    public class EffectSamplerReferenceTests
    {
        const double Tol = 1e-3;

        static string[] References() => new[]
        {
            "texture/effect/firewall.str",
            "texture/effect/cross.str",
            "texture/effect/angelus.str",
            "texture/effect/cure.str",
            "texture/effect/freeze.str",
            "texture/effect/bs_refinesuccess.str",
            "texture/effect/dust.str",
            "texture/effect/new_splash/new_splash.str",
            "texture/effect/mineffect/new_earthgrave/new_earthgrave_bottom/new_earthgrave_bottom.str",
            "sprite/이팩트/jong_mini.str",
        };

        [TestCaseSource(nameof(References))]
        public void Sampler_MatchesTheStrReferenceFormulas_AtFirstMiddleAndLastFrame(string file)
        {
            var bytes = SpriteTestFiles.Bytes(file);
            Assert.IsTrue(RoStrReader.TryRead(RoPath.Parse(file), bytes, out RoStr str, out var code, out var reason),
                $"{file}: {code} {reason}");
            RoCompiledStr c = RoStrCompiler.Compile(str);

            var asset = ScriptableObject.CreateInstance<EffectAsset>();
            var settings = ScriptableObject.CreateInstance<EffectSettings>();
            try
            {
                asset.Kind = EffectKind.Str;
                asset.Fps = c.Fps;
                asset.FrameCount = c.FrameCount;
                asset.Layers = c.Layers;
                asset.Keys = c.Keys;
                asset.TextureNames = c.TextureNames;
                asset.Textures = new Texture2D[c.TextureNames.Length];

                // FR-3: length = frames / fps (O2: the plan's formula).
                Assert.AreEqual((double)str.FrameCount / str.Fps, EffectSampler.Length(asset), Tol, $"{file}: length");

                int checkedLayers = 0, checkedFrames = 0;
                for (int li = 0; li < str.Layers.Count; li++)
                {
                    RoStrLayer layer = str.Layers[li];
                    if (layer.Keys.Count == 0) continue;
                    if (layer.Keys.Any(k => k.AniType == 5 || k.AniType == 6)) continue;      // O5

                    // O2: last = frame of the last key strictly below FrameCount.
                    var inRange = layer.Keys.Where(k => k.Frame < str.FrameCount).Select(k => k.Frame).ToList();
                    var baseFrames = layer.Keys.Where(k => k.Type == 0).Select(k => k.Frame).ToList();
                    if (inRange.Count == 0 || baseFrames.Count == 0) continue;
                    int first = baseFrames.Min(), last = inRange.Max();
                    if (last < first) continue;
                    foreach (int frame in new[] { first, (first + last) / 2, last }.Distinct())
                    {
                        CompareLayerFrame(file, str, asset, settings, li, frame);
                        checkedFrames++;
                    }
                    checkedLayers++;
                }
                Assert.Greater(checkedLayers, 0, $"{file}: no layer was compared");
                Assert.Greater(checkedFrames, 0, $"{file}: no frame was compared");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        static void CompareLayerFrame(string file, RoStr str, EffectAsset asset, EffectSettings settings, int li, int frame)
        {
            string where = $"{file} layer {li} frame {frame}";
            IReadOnlyList<RoStrKey> keys = str.Layers[li].Keys;

            // Oracle (str-reference.md section 6): base = last type-0 key with Frame <= f; its delta key is the key right
            // after it in the same frame; delta present -> base + delta * (f - base.Frame), else the base key is held.
            int b = -1;
            for (int i = 0; i < keys.Count; i++)
                if (keys[i].Type == 0 && keys[i].Frame <= frame) b = i;
            Assert.GreaterOrEqual(b, 0, $"{where}: no base key (test frame choice)");
            RoStrKey bk = keys[b];
            bool hasDelta = b + 1 < keys.Count && keys[b + 1].Type == 1 && keys[b + 1].Frame == bk.Frame;
            RoStrKey dk = hasDelta ? keys[b + 1] : default;
            double d = hasDelta ? frame - bk.Frame : 0.0;

            int cursor = 0;
            Assert.IsTrue(EffectSampler.Sample(asset, settings, li, frame, ref cursor, out QuadSample q), $"{where}: shown");

            AssertVec(Mix(bk.Position, dk.Position, d, hasDelta), q.Position, where + " position");
            AssertVec(Mix(bk.Xy0, dk.Xy0, d, hasDelta), q.C0, where + " corner 0");
            AssertVec(Mix(bk.Xy1, dk.Xy1, d, hasDelta), q.C1, where + " corner 1");
            AssertVec(Mix(bk.Xy2, dk.Xy2, d, hasDelta), q.C2, where + " corner 2");
            AssertVec(Mix(bk.Xy3, dk.Xy3, d, hasDelta), q.C3, where + " corner 3");

            double angleUnits = bk.Angle + (hasDelta ? dk.Angle * d : 0.0);
            Assert.AreEqual(angleUnits * 360.0 / 1024.0, q.AngleDegrees, Tol, where + " angle");

            Assert.AreEqual((bk.Color.x + (hasDelta ? dk.Color.x * d : 0.0)) / 255.0, q.Color.r, Tol, where + " r");
            Assert.AreEqual((bk.Color.y + (hasDelta ? dk.Color.y * d : 0.0)) / 255.0, q.Color.g, Tol, where + " g");
            Assert.AreEqual((bk.Color.z + (hasDelta ? dk.Color.z * d : 0.0)) / 255.0, q.Color.b, Tol, where + " b");
            Assert.AreEqual((bk.Color.w + (hasDelta ? dk.Color.w * d : 0.0)) / 255.0, q.Color.a, Tol, where + " a");

            Assert.AreEqual(bk.SrcBlend, q.Src, where + " src blend");
            Assert.AreEqual(bk.DstBlend, q.Dst, where + " dst blend");

            // Texture frame.
            RoStrLayer layer = str.Layers[li];
            int n = layer.TextureNames.Count;
            EffectAsset.LayerData ld = asset.Layers[li];
            if (n == 0)
            {
                Assert.AreEqual(-1, q.Texture, where + " texture (none)");
                return;
            }
            if (!hasDelta)
            {
                Assert.AreEqual(ld.FirstTexture + TextureIndex(bk.AniFrame, n), q.Texture, where + " texture (held)");
                return;
            }
            double ani;
            switch (dk.AniType)
            {
                case 0: ani = bk.AniFrame; break;                                   // O3
                case 1: ani = bk.AniFrame + dk.AniFrame * d; break;
                case 2: ani = Math.Min(bk.AniFrame + bk.Delay * d, n - 1); break;     // O4: the base key's delay
                case 3: ani = FloorMod(bk.AniFrame + bk.Delay * d, n); break;
                case 4: ani = FloorMod(bk.AniFrame - bk.Delay * d, n); break;
                default: return;                                                          // O5 (unknown type)
            }
            Assert.AreEqual(ld.FirstTexture + TextureIndex(ani, n), q.Texture, where + " texture");
        }

        static int TextureIndex(double ani, int n)
        {
            int i = (int)Math.Floor(ani);
            return Math.Max(0, Math.Min(n - 1, i));
        }

        static double FloorMod(double v, int n) => v - n * Math.Floor(v / n);

        static Vector2 Mix(Vector2 b, Vector2 dl, double d, bool hasDelta) =>
            hasDelta ? new Vector2((float)(b.x + dl.x * d), (float)(b.y + dl.y * d)) : b;

        static void AssertVec(Vector2 expected, Vector2 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Tol, what + " x");
            Assert.AreEqual(expected.y, actual.y, Tol, what + " y");
        }
    }
}
