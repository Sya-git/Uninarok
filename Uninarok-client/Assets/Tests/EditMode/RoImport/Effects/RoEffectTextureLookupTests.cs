using System.IO;
using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ui;
using Uninarok.RoImport.Tests.Sprites;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-2, FR-10 (PLAN-0012 4.3, 6): where a STR's texture name is found. The lookup only deals with names and
    /// file existence, so the temp source folders hold empty files; the two real STR files are read through
    /// <see cref="RoStrReader"/> (GRF) for their texture names, never by the test.
    /// </summary>
    public class RoEffectTextureLookupTests
    {
        const string Pending = "pending T3.5";
        const string StrFile = "texture/effect/sub/a.str";

        string _temp;

        [TearDown]
        public void TearDown()
        {
            if (_temp != null && Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
            _temp = null;
        }

        RoFolderSource Source(params string[] files)
        {
            _temp = Path.Combine(Path.GetTempPath(), "ro-effect-lookup-" + System.Guid.NewGuid().ToString("N"));
            foreach (var f in files)
            {
                string full = Path.Combine(_temp, f);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, new byte[0]);
            }
            Directory.CreateDirectory(_temp);
            return new RoFolderSource(_temp);
        }

        static bool Resolve(IRoSource src, string name, out RoPath texture) =>
            RoEffectTextureLookup.TryResolve(src, RoPath.Parse(StrFile), name, out texture);

        [Test, Ignore(Pending)]
        public void OwnFolder_Wins_OverTheRoot()
        {
            var src = Source("texture/effect/sub/pok1.tga", "texture/effect/pok1.tga");
            Assert.IsTrue(Resolve(src, "pok1.tga", out var texture));
            Assert.AreEqual(RoPath.Parse("texture/effect/sub/pok1.tga"), texture);
        }

        [Test, Ignore(Pending)]
        public void Root_IsFound_WhenTheNameIsAbsentFromTheOwnFolder()
        {
            var src = Source("texture/effect/pok1.tga");
            Assert.IsTrue(Resolve(src, "pok1.tga", out var texture));
            Assert.AreEqual(RoPath.Parse("texture/effect/pok1.tga"), texture);
        }

        [Test, Ignore(Pending)]
        public void NameFoundNowhere_IsFalse_SoTheCallerWarnsAndUsesThePlaceholder()
        {
            // The TextureMissing warning (once per texture and STR, referrer = the STR) and the placeholder are issued by the
            // caller from this false; the lookup is pure and has no report (PLAN-0012 4.3 signature).
            var src = Source("texture/effect/other.tga");
            Assert.IsFalse(Resolve(src, "nope.tga", out var texture));
            Assert.IsTrue(texture.IsEmpty);
        }

        [Test, Ignore(Pending)]
        public void NameWithExtension_ResolvesToThatFileOnly()
        {
            var src = Source("texture/effect/sub/pok1.tga");
            Assert.IsFalse(Resolve(src, "pok1.bmp", out _), "pok1.bmp never finds pok1.tga");
            Assert.IsTrue(Resolve(src, "pok1.tga", out var texture));
            Assert.AreEqual(RoPath.Parse("texture/effect/sub/pok1.tga"), texture);
        }

        [Test, Ignore(Pending)]
        public void BareName_WithTgaAndBmp_GivesTheTga()
        {
            var src = Source("texture/effect/sub/pok1.bmp", "texture/effect/sub/pok1.tga");
            Assert.IsTrue(Resolve(src, "pok1", out var texture));
            Assert.AreEqual(RoPath.Parse("texture/effect/sub/pok1.tga"), texture);
            CollectionAssert.AreEqual(new[] { ".tga", ".bmp" }, RoEffectTextureLookup.BareNamePreference);
        }

        [Test, Ignore(Pending)]
        public void FolderOrder_BeatsExtensionPreference()
        {
            var src = Source("texture/effect/sub/pok1.bmp", "texture/effect/pok1.tga");
            Assert.IsTrue(Resolve(src, "pok1", out var texture));
            Assert.AreEqual(RoPath.Parse("texture/effect/sub/pok1.bmp"), texture, "own-folder .bmp beats root .tga");
        }

        [Test, Ignore(Pending)]
        public void SameBaseName_TgaAndBmp_ResolveToTwoDifferentTextures()
        {
            // FR-2 (D7): extension kept, so pok1.tga and pok1.bmp are two textures; no collision.
            var src = Source("texture/effect/sub/pok1.tga", "texture/effect/sub/pok1.bmp");
            Assert.IsTrue(Resolve(src, "pok1.tga", out var tga));
            Assert.IsTrue(Resolve(src, "pok1.bmp", out var bmp));
            Assert.AreNotEqual(tga, bmp);
        }

        [Category(SpriteTestFiles.Category)]
        [TestCase("texture/effect/new_splash/new_splash.str")]
        [TestCase("sprite/이팩트/jong_mini.str")]
        [Ignore(Pending)]
        public void ReferenceStr_ResolvesAllItsTextures(string file)
        {
            var bytes = SpriteTestFiles.Bytes(file);
            Assert.IsTrue(RoStrReader.TryRead(RoPath.Parse(file), bytes, out var str, out var reason), reason);
            var root = RoSourceLocation.Resolve();
            var src = new RoFolderSource(root);
            var names = str.Layers.SelectMany(l => l.TextureNames).Distinct().ToArray();
            Assert.IsNotEmpty(names);
            foreach (var name in names)
            {
                Assert.IsTrue(RoEffectTextureLookup.TryResolve(src, RoPath.Parse(file), name, out var texture), name);
                Assert.IsTrue(src.TryGetFile(texture, out _), name);
            }
        }
    }
}
