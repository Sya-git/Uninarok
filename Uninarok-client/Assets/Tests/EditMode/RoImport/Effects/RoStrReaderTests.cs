using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Tests.Sprites;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-2, FR-10 (PLAN-0012 §6): <see cref="RoStrReader"/> against the T0.1 record of the 10 reference files
    /// (str-reference.md §4, held here as constants: RO files are read only through GRF, never by the test), and bad input
    /// (truncated, junk, wrong version made by GRF's own writer) gives false with the right issue and never throws.
    /// </summary>
    [Category(SpriteTestFiles.Category)]
    public class RoStrReaderTests
    {
        // str-reference.md §4: file, frame count, keys per layer, textures per layer. Version 0x94 and fps 60 in every file.
        // A method, not a static field (the UAL analyzer flags static fields holding objects).
        static object[] References() => new object[]
        {
            new object[] { "texture/effect/firewall.str", 40,
                new[] { 0, 11, 11, 11, 13, 13, 13, 23, 23, 27, 41 }, new[] { 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 1 } },
            new object[] { "texture/effect/cross.str", 121,
                new[] { 0, 45, 45, 34, 34, 17, 17, 15, 15, 21, 5 }, new[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new object[] { "texture/effect/angelus.str", 236,
                new[] { 0, 30, 119, 119, 3, 3, 119, 119, 3, 27, 43 }, new[] { 0, 31, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new object[] { "texture/effect/cure.str", 107,
                new[] { 0, 36, 26, 26, 73, 5, 3, 33, 33, 51, 51, 51 }, new[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new object[] { "texture/effect/freeze.str", 60,
                new[] { 0, 26, 26, 11, 8, 14, 14, 14 }, new[] { 0, 1, 1, 1, 1, 1, 1, 1 } },
            new object[] { "texture/effect/bs_refinesuccess.str", 100,
                new[] { 0, 33, 18, 15, 21, 12, 12, 12, 12, 11, 11, 3, 3, 3, 3, 7, 3 },
                new[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 } },
            new object[] { "texture/effect/dust.str", 120,
                new[] { 0, 11, 11, 11, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 5, 11, 11, 11, 5, 11, 11, 11, 11, 11, 11, 11,
                        11, 11, 11, 11, 11, 11, 5, 5, 5, 5, 5, 3, 7, 7, 7 },
                new[] { 0 }.Concat(Enumerable.Repeat(1, 45)).ToArray() },
            new object[] { "texture/effect/new_splash/new_splash.str", 38, new[] { 0, 3 }, new[] { 0, 20 } },
            new object[] { "texture/effect/mineffect/new_earthgrave/new_earthgrave_bottom/new_earthgrave_bottom.str", 48,
                new[] { 0, 5, 5 }, new[] { 0, 1, 1 } },
            new object[] { "sprite/이팩트/jong_mini.str", 100, new[] { 0, 21, 5, 5, 38, 38 }, new[] { 0, 31, 1, 1, 1, 1 } },
        };

        const string Firewall = "texture/effect/firewall.str";

        [TestCaseSource(nameof(References))]
        public void ReferenceFile_MatchesTheT01Record(string file, int frameCount, int[] keysPerLayer, int[] texturesPerLayer)
        {
            var bytes = SpriteTestFiles.Bytes(file);

            Assert.IsTrue(RoStrReader.TryReadVersion(bytes, out int version), file);
            Assert.AreEqual(0x94, version, "TryReadVersion");

            bool ok = RoStrReader.TryRead(RoPath.Parse(file), bytes, out var str, out var code, out var reason);
            Assert.IsTrue(ok, $"{file}: {code} {reason}");
            Assert.IsNull(reason);
            Assert.AreEqual(0x94, str.Version, "version");
            Assert.AreEqual(60f, str.Fps, "fps");
            Assert.AreEqual(frameCount, str.FrameCount, "frame count");
            CollectionAssert.AreEqual(keysPerLayer, str.Layers.Select(l => l.Keys.Count).ToArray(), "keys per layer");
            CollectionAssert.AreEqual(texturesPerLayer, str.Layers.Select(l => l.TextureNames.Count).ToArray(), "textures per layer");

            // str-reference.md §5: keys sorted by frame, types 0 and 1 only, no key beyond the frame count.
            foreach (var layer in str.Layers)
                for (int i = 0; i < layer.Keys.Count; i++)
                {
                    var k = layer.Keys[i];
                    Assert.That(k.Type, Is.EqualTo(0).Or.EqualTo(1), "key type");
                    Assert.That(k.Frame, Is.InRange(0, frameCount), "key frame");
                    if (i > 0) Assert.GreaterOrEqual(k.Frame, layer.Keys[i - 1].Frame, "keys sorted by frame");
                }
        }

        [Test]
        public void Firewall_FirstKey_HoldsTheRecordedCornersPositionUvAndBlend()
        {
            Assert.IsTrue(RoStrReader.TryRead(RoPath.Parse(Firewall), SpriteTestFiles.Bytes(Firewall), out var str, out var reason), reason);
            var k = str.Layers[1].Keys[0];

            // str-reference.md §3 (corners), §7 (position (320, 329), blend (5, 7)), §5 (full uv rectangle in base keys).
            Assert.AreEqual(0, k.Type);
            Assert.AreEqual(0, k.Frame);
            Assert.AreEqual(new Vector2(320, 329), k.Position);
            Assert.AreEqual(new Vector2(-32, -32), k.Xy0, "top-left");
            Assert.AreEqual(new Vector2(32, -32), k.Xy1, "top-right");
            Assert.AreEqual(new Vector2(32, 32), k.Xy2, "bottom-right");
            Assert.AreEqual(new Vector2(-32, 32), k.Xy3, "bottom-left");
            Assert.AreEqual(new Vector2(0, 0), k.Uv0);
            Assert.AreEqual(new Vector2(1, 1), k.Uv1);
            Assert.AreEqual(new Vector2(0, 0), k.Uv2);
            Assert.AreEqual(new Vector2(1, 1), k.Uv3);
            Assert.AreEqual(5, k.SrcBlend);
            Assert.AreEqual(7, k.DstBlend);
            Assert.AreEqual(0f, k.Color.w, "alpha fades in from 0 at frame 0");
        }

        [Test]
        public void Angelus_KeepsBothBlendPairsOfItsTwoBlendLayer()
        {
            const string file = "texture/effect/angelus.str";
            Assert.IsTrue(RoStrReader.TryRead(RoPath.Parse(file), SpriteTestFiles.Bytes(file), out var str, out var reason), reason);

            // str-reference.md §4: (5, 7) everywhere, plus (2, 1) in one layer, the reference layer with two blend pairs.
            var twoPairLayers = str.Layers.Count(l =>
                l.Keys.Where(k => k.Type == 0).Select(k => (k.SrcBlend, k.DstBlend)).Distinct().Count() == 2);
            Assert.AreEqual(1, twoPairLayers);
            var pairs = str.Layers.SelectMany(l => l.Keys).Where(k => k.Type == 0).Select(k => (k.SrcBlend, k.DstBlend)).Distinct().ToArray();
            CollectionAssert.AreEquivalent(new[] { (5, 7), (2, 1) }, pairs);
        }

        [Test]
        public void Ado_TextureNames_AreDecodedAsKorean()
        {
            const string file = "texture/effect/ado.str";
            Assert.IsTrue(RoStrReader.TryRead(RoPath.Parse(file), SpriteTestFiles.Bytes(file), out var str, out var reason), reason);

            // str-reference.md §3: the only 3 non-ASCII names of the data are in ado.str; with CP949 they are Hangul, not
            // Windows-1252 mojibake such as "¹ø°³1.bmp".
            var nonAscii = str.Layers.SelectMany(l => l.TextureNames).Where(n => n.Any(ch => ch > 0x7F)).ToArray();
            Assert.AreEqual(3, nonAscii.Length, string.Join(", ", nonAscii));
            foreach (var name in nonAscii)
                Assert.IsTrue(name.All(ch => ch <= 0x7F || (ch >= 0xAC00 && ch <= 0xD7A3)), $"not Hangul: {name}");
        }

        [Test]
        public void Angle_GrfDegreesBackToFileUnits()
        {
            // str-reference.md §3: GRF value = raw × 360 / 1024 (raw 36 → 12.65625, raw 1024 → 360).
            Assert.AreEqual(36f, RoStrReader.AngleFromGrfDegrees(12.65625f), 1e-4f);
            Assert.AreEqual(1024f, RoStrReader.AngleFromGrfDegrees(360f), 1e-4f);
            Assert.AreEqual(0f, RoStrReader.AngleFromGrfDegrees(0f));
        }

        [Test]
        public void TruncatedInput_IsCorruptFile_AndNeverThrows()
        {
            var bytes = SpriteTestFiles.Bytes(Firewall);
            foreach (int length in new[] { 0, 1, bytes.Length / 4, bytes.Length / 2, bytes.Length - 1 })
                AssertFails(SpriteTestFiles.Slice(bytes, length), RoIssueCode.CorruptFile, $"cut to {length}");
            AssertFails(null, RoIssueCode.CorruptFile, "null");
        }

        [Test]
        public void JunkInput_IsCorruptFile_AndNeverThrows()
        {
            var junk = new byte[64];
            new System.Random(12345).NextBytes(junk);
            AssertFails(junk, RoIssueCode.CorruptFile, "random junk");
            AssertFails(new byte[64], RoIssueCode.CorruptFile, "zeros");
            Assert.IsFalse(RoStrReader.TryReadVersion(junk, out _), "TryReadVersion on junk");
            Assert.IsFalse(RoStrReader.TryReadVersion(null, out _), "TryReadVersion on null");
        }

        [TestCase(0x95)]
        [TestCase(0x93)]
        [TestCase(0x01)]
        public void WrongVersion_IsUnsupportedStrVersion_AndNeverThrows(int major)
        {
            // The copy is written by GRF itself with its header version set through GRF (no byte is touched here).
            var copy = RoStrReader.RewriteVersionThroughGrf(SpriteTestFiles.Bytes(Firewall), (byte)major, 0);

            Assert.IsTrue(RoStrReader.TryReadVersion(copy, out int version));
            Assert.AreEqual(major, version);
            AssertFails(copy, RoIssueCode.UnsupportedStrVersion, $"version 0x{major:X}");
        }

        [Test]
        public void GrfRewrite_WithTheSupportedVersion_StillReads()
        {
            // Control for the wrong-version cases: the same GRF rewrite with 0x94 is read, so only the version makes them fail.
            var copy = RoStrReader.RewriteVersionThroughGrf(SpriteTestFiles.Bytes(Firewall), 0x94, 0);
            Assert.IsTrue(RoStrReader.TryRead(RoPath.Parse(Firewall), copy, out var str, out var code, out var reason), $"{code} {reason}");
            Assert.AreEqual(11, str.Layers.Count);
        }

        static void AssertFails(byte[] bytes, RoIssueCode expected, string label)
        {
            bool ok = true;
            RoStr str = null;
            RoIssueCode code = RoIssueCode.Unexpected;
            string reason = null;
            Assert.DoesNotThrow(() => ok = RoStrReader.TryRead(RoPath.Parse("x.str"), bytes, out str, out code, out reason), label);
            Assert.IsFalse(ok, label);
            Assert.IsNull(str, label);
            Assert.AreEqual(expected, code, $"{label}: {reason}");
            Assert.IsFalse(string.IsNullOrEmpty(reason), label);
        }
    }
}
