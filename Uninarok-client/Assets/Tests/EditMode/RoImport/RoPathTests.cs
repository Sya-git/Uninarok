using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-1: a file is found by its Ragnarok path whatever the case, slash direction or Korean form.
    // Plan 4.1 (rules applied in order: slashes and trim, NFC, lowercase, strip one data/, reject). TASKS T1.2.
    public class RoPathTests
    {
        const string KoreanUi = "유저인터페이스";   // composed (NFC) Hangul syllables

        [TestCase(@"data\texture\a.bmp")]
        [TestCase("DATA/Texture/a.bmp")]
        [TestCase("texture/a.bmp")]
        [TestCase(@"Data\TEXTURE\A.BMP")]
        public void ThreeSpellingsOfOnePath_AreEqual(string raw)
        {
            var path = RoPath.Parse(raw);
            var reference = RoPath.Parse("texture/a.bmp");

            Assert.AreEqual("texture/a.bmp", path.Canonical);
            Assert.IsTrue(path.Equals(reference));
            Assert.IsTrue(path == reference);
            Assert.IsFalse(path != reference);
            Assert.AreEqual(reference.GetHashCode(), path.GetHashCode());
        }

        [Test]
        public void KoreanComposedAndDecomposed_AreEqual()
        {
            string composed = $"texture/{KoreanUi}/a.bmp";
            string decomposed = composed.Normalize(NormalizationForm.FormD);
            Assume.That(decomposed, Is.Not.EqualTo(composed), "the decomposed form must differ for the test to mean anything");

            var a = RoPath.Parse(composed);
            var b = RoPath.Parse(decomposed);

            Assert.AreEqual(a, b);
            Assert.AreEqual(composed, b.Canonical, "the canonical form is NFC");
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void KoreanSyllables_AreUnchangedByCaseFolding()
        {
            Assert.AreEqual($"texture/{KoreanUi}/a.bmp", RoPath.Parse($@"DATA\Texture\{KoreanUi}\A.BMP").Canonical);
        }

        [TestCase("data/texture/a.bmp", "texture/a.bmp")]
        [TestCase("data/data/a.bmp", "data/a.bmp")]                 // only one leading data/ is stripped
        [TestCase("texture/data/a.bmp", "texture/data/a.bmp")]      // only a leading one
        [TestCase("database/a.bmp", "database/a.bmp")]              // a whole segment, not a prefix
        public void OneLeadingDataSegment_IsStripped(string raw, string expected)
        {
            Assert.AreEqual(expected, RoPath.Parse(raw).Canonical);
        }

        [TestCase("Texture/Sub/A.BMP", "texture/sub/a.bmp")]
        [TestCase(@"texture\\sub//a.bmp", "texture/sub/a.bmp")]       // repeated slashes collapse
        [TestCase("  /texture/a.bmp/  ", "texture/a.bmp")]             // whitespace and outer slashes trimmed
        [TestCase(@"\\texture\a.bmp", "texture/a.bmp")]
        public void SlashesWhitespaceAndCase_AreNormalized(string raw, string expected)
        {
            Assert.AreEqual(expected, RoPath.Parse(raw).Canonical);
        }

        public static IEnumerable<string> Invalid() => new[]
        {
            null, "", "   ", "/", @"\\",
            "data", "data/", "DATA\\",                                  // empty once data/ is stripped
            "texture/../a.bmp", "../a.bmp", "texture/..",                // parent segment
            "./a.bmp", "texture/./a.bmp",                               // current segment
            "C:/a.bmp", @"c:\texture\a.bmp", "c:texture/a.bmp",         // drive letter
            "texture/a\u0001.bmp", "texture/a\t.bmp", "texture/\u007Fa.bmp",   // control characters
        };

        [TestCaseSource(nameof(Invalid))]
        public void InvalidPath_ThrowsRoPathException(string raw)
        {
            var ex = Assert.Throws<RoPathException>(() => RoPath.Parse(raw));
            Assert.AreEqual(raw, ex.Raw);
        }

        [TestCaseSource(nameof(Invalid))]
        public void InvalidPath_TryParseReturnsFalse(string raw)
        {
            Assert.IsFalse(RoPath.TryParse(raw, out _));
        }

        [Test]
        public void TryParse_ValidPath_ReturnsTrueAndThePath()
        {
            Assert.IsTrue(RoPath.TryParse(@"DATA\Texture\A.BMP", out var path));
            Assert.AreEqual("texture/a.bmp", path.Canonical);
        }

        [Test]
        public void Parts_AreDerivedFromTheCanonicalForm()
        {
            var path = RoPath.Parse(@"Texture\Sub\Tree.Leaf.BMP");

            Assert.AreEqual(".bmp", path.Extension);
            Assert.AreEqual("tree.leaf", path.FileNameWithoutExtension);
            Assert.AreEqual("texture/sub", path.Folder.Canonical);
            Assert.AreEqual("texture", path.Folder.Folder.Canonical);
        }

        [Test]
        public void TopLevelItem_HasAnEmptyFolder_AndNoExtensionIsEmpty()
        {
            var path = RoPath.Parse("readme");

            Assert.AreEqual("", path.Extension);
            Assert.AreEqual("readme", path.FileNameWithoutExtension);
            Assert.AreEqual("", path.Folder.Canonical);
            Assert.AreEqual("", default(RoPath).Canonical, "a default RoPath is the empty root, never null");
        }

        [TestCase(".png", "texture/sub/a.png")]
        [TestCase("PNG", "texture/sub/a.png")]
        [TestCase(".Tga", "texture/sub/a.tga")]
        [TestCase("", "texture/sub/a")]
        public void WithExtension_ReplacesTheExtension(string extension, string expected)
        {
            Assert.AreEqual(expected, RoPath.Parse("texture/sub/a.bmp").WithExtension(extension).Canonical);
        }

        [Test]
        public void Combine_AppliesTheSameRulesToTheRelativePart()
        {
            var folder = RoPath.Parse("Texture");

            Assert.AreEqual("texture/sub/b.bmp", folder.Combine(@"Sub\B.BMP").Canonical);
            Assert.AreEqual("texture/b.bmp", default(RoPath).Combine("Texture/B.bmp").Canonical, "combining onto the root is Parse");
            Assert.Throws<RoPathException>(() => folder.Combine("../b.bmp"));
            Assert.Throws<RoPathException>(() => folder.Combine(""));
        }

        [Test]
        public void Ordering_IsOrdinalOnTheCanonicalForm()
        {
            var paths = new[] { "texture/b.bmp", "Texture/A.bmp", "model/z.rsm", $"texture/{KoreanUi}/a.bmp", "texture/_a.bmp" }
                .Select(RoPath.Parse).ToList();
            paths.Sort();

            CollectionAssert.AreEqual(
                new[] { "model/z.rsm", "texture/_a.bmp", "texture/a.bmp", "texture/b.bmp", $"texture/{KoreanUi}/a.bmp" },
                paths.Select(p => p.Canonical).ToArray());
            Assert.AreNotEqual(RoPath.Parse("texture/a.bmp"), RoPath.Parse("texture/b.bmp"));
        }
    }
}
