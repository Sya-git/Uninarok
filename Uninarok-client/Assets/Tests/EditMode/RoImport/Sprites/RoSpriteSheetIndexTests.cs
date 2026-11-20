using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Editor.Sprites;

namespace Uninarok.RoImport.Tests.Sprites
{
    // TASKS-0007 T3.4 (FR-2, FR-9, plan 4.2, D2): SPR content hash → sheet, a committed deterministic JSON file.
    public class RoSpriteSheetIndexTests
    {
        string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "uninarok-sheetindex-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        static RoSheetEntry Entry(string hash, string name, bool index = false) => new RoSheetEntry
        {
            Hash = hash,
            Sheet = "Assets/Art/Imported/sprite/_sheets/" + hash.Substring(0, 2) + "/" + name + "_" + hash.Substring(0, 8) + ".png",
            Source = "sprite/" + name + ".spr",
            HasIndex = index,
            Rules = 1
        };

        const string A = "aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11aa11";
        const string B = "0b22bb22bb22bb22bb22bb22bb22bb22bb22bb22bb22bb22bb22bb22bb22bb22";

        [Test]
        public void AnEmptyFolder_IsAnEmptyIndex()
        {
            var index = RoSpriteSheetIndex.Load(_folder);

            Assert.AreEqual(0, index.Count);
            Assert.IsNull(index.Find(A));
        }

        [Test]
        public void ARecordedSheet_IsFoundByItsHash_InAnyCase()
        {
            var index = RoSpriteSheetIndex.Load(_folder);
            index.Record(Entry(A, "poring"));

            Assert.AreEqual("sprite/poring.spr", index.Find(A).Source);
            Assert.AreEqual("sprite/poring.spr", index.Find(A.ToUpperInvariant()).Source);
            Assert.IsNull(index.Find(B));
        }

        [Test]
        public void ASheet_CanBeUpgradedToHaveAnIndexTexture()
        {
            var index = RoSpriteSheetIndex.Load(_folder);
            index.Record(Entry(A, "body"));
            index.Save();

            var e = index.Find(A);
            Assert.IsFalse(e.HasIndex);
            index.Record(Entry(A, "body", index: true));
            index.Save();

            Assert.IsTrue(RoSpriteSheetIndex.Load(_folder).Find(A).HasIndex);
        }

        [Test]
        public void TheFile_IsSortedAndDeterministic_AndWrittenOnlyWhenItChanged()
        {
            var first = RoSpriteSheetIndex.Load(_folder);
            first.Record(Entry(A, "poring"));
            first.Record(Entry(B, "shield"));
            Assert.IsTrue(first.Save());
            string text = File.ReadAllText(first.FilePath);

            var again = RoSpriteSheetIndex.Load(_folder);
            Assert.AreEqual(2, again.Count);
            Assert.IsFalse(again.Save(), "nothing changed: not written");

            string otherFolder = _folder + "-b";
            try
            {
                Directory.CreateDirectory(otherFolder);
                var reversed = RoSpriteSheetIndex.Load(otherFolder);
                reversed.Record(Entry(B, "shield"));
                reversed.Record(Entry(A, "poring"));
                reversed.Save();
                Assert.AreEqual(text, File.ReadAllText(reversed.FilePath), "insertion order does not matter");
            }
            finally
            {
                Directory.Delete(otherFolder, true);
            }

            Assert.Less(text.IndexOf(B, StringComparison.Ordinal), text.IndexOf(A, StringComparison.Ordinal), "sorted by hash");
            Assert.IsFalse(text.Contains("\r"), "LF only");
            CollectionAssert.AreEqual(new[] { B, A }, again.Entries.Select(e => e.Hash).ToArray());
        }
    }
}
