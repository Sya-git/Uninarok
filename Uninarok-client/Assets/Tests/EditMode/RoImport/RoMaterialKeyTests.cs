using System;
using System.Collections.Generic;
using NUnit.Framework;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-9: one texture and one surface mode resolve to one material. Plan 4.6. TASKS T5.1.
    public class RoMaterialKeyTests
    {
        static RoMaterialKey Key(string texture, RoSurface surface = RoSurface.Opaque, bool twoSided = false) =>
            new RoMaterialKey(RoPath.Parse(texture), surface, twoSided);

        [Test]
        public void SameTextureSurfaceAndSidedness_AreOneKey_HoweverThePathIsSpelled()
        {
            var a = Key(@"DATA\Texture\A.BMP", RoSurface.Cutout, true);
            var b = Key("texture/a.bmp", RoSurface.Cutout, true);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreEqual(1, new HashSet<RoMaterialKey> { a, b }.Count);
        }

        [Test]
        public void KeysDifferingInTextureSurfaceOrSidedness_AreDifferent()
        {
            var keys = new HashSet<RoMaterialKey>
            {
                Key("texture/a.bmp"), Key("texture/b.bmp"),
                Key("texture/a.bmp", RoSurface.Cutout), Key("texture/a.bmp", RoSurface.Blend),
                Key("texture/a.bmp", twoSided: true),
            };

            Assert.AreEqual(5, keys.Count);
            Assert.IsTrue(Key("texture/a.bmp") != Key("texture/a.bmp", twoSided: true));
        }

        [TestCase(RoSurface.Opaque, false, "opaque")]
        [TestCase(RoSurface.Cutout, false, "cutout")]
        [TestCase(RoSurface.Blend, false, "blend")]
        [TestCase(RoSurface.Opaque, true, "opaque_2s")]
        [TestCase(RoSurface.Cutout, true, "cutout_2s")]
        [TestCase(RoSurface.Blend, true, "blend_2s")]
        public void Suffix_NamesTheSurfaceAndSidedness(RoSurface surface, bool twoSided, string suffix)
        {
            Assert.AreEqual(suffix, Key("texture/a.bmp", surface, twoSided).Suffix);
        }

        [Test]
        public void MaterialPath_ComesFromTheKey()
        {
            Assert.AreEqual("Assets/Art/Imported/_materials/texture/sub/a__cutout_2s.mat",
                RoOutputPaths.Material(new RoOutputRoots(), Key("texture/sub/a.bmp", RoSurface.Cutout, true)));
        }

        [Test]
        public void KeyWithoutATexture_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new RoMaterialKey(default, RoSurface.Opaque, false));
        }
    }
}
