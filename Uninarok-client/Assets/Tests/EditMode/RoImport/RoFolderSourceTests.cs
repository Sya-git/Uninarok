using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-1: a file is found by its Ragnarok path regardless of case, slashes or Korean folder names.
    // Plan 4.2. Uses the fixture data root (Fixtures~/README.md). TASKS T2.1.
    public class RoFolderSourceTests
    {
        internal static string FixtureRoot => Path.Combine(Application.dataPath, "Tests/EditMode/RoImport/Fixtures~/data");

        const string KoreanFolder = "나무잡초꽃";
        const string KoreanUi = "유저인터페이스";

        string _temp;

        [TearDown]
        public void TearDown()
        {
            if (_temp != null && Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
            _temp = null;
        }

        [TestCase(@"data\texture\A.BMP")]
        [TestCase("DATA/Texture/a.bmp")]
        [TestCase("texture/a.bmp")]
        public void FileIsFound_ByAnySpellingOfItsPath(string raw)
        {
            var source = new RoFolderSource(FixtureRoot);

            Assert.IsTrue(source.TryGetFile(RoPath.Parse(raw), out var file));

            Assert.AreEqual("texture/a.bmp", file.Path.Canonical);
            Assert.AreEqual(Full(Path.Combine(FixtureRoot, "texture", "a.bmp")), Full(file.DiskPath));
        }

        [Test]
        public void UpperCaseFileOnDisk_IsFoundByItsLowercasePath_AndKeepsItsRealDiskName()
        {
            var source = new RoFolderSource(FixtureRoot);

            Assert.IsTrue(source.TryGetFile(RoPath.Parse("texture/key8.bmp"), out var file));

            StringAssert.EndsWith("KEY8.BMP", file.DiskPath);
        }

        [Test]
        public void KoreanNamedFolder_IsFound_ComposedOrDecomposed()
        {
            var source = new RoFolderSource(FixtureRoot);
            string composed = $"texture/{KoreanFolder}/leaf.bmp";

            Assert.IsTrue(source.TryGetFile(RoPath.Parse(composed), out var a));
            Assert.IsTrue(source.TryGetFile(RoPath.Parse(composed.Normalize(NormalizationForm.FormD)), out var b));

            Assert.AreEqual(a.DiskPath, b.DiskPath);
            Assert.IsTrue(File.Exists(a.DiskPath));
        }

        [Test]
        public void FoundFile_ReportsItsLengthBytesAndHash()
        {
            var source = new RoFolderSource(FixtureRoot);
            source.TryGetFile(RoPath.Parse("texture/a.bmp"), out var file);
            byte[] onDisk = File.ReadAllBytes(Path.Combine(FixtureRoot, "texture", "a.bmp"));

            Assert.AreEqual(onDisk.Length, file.Length);
            CollectionAssert.AreEqual(onDisk, file.ReadAllBytes());
            Assert.AreEqual(Sha256Hex(onDisk), file.ContentHash());
        }

        [Test]
        public void MissingPath_ReturnsFalse_WithoutThrowing()
        {
            var source = new RoFolderSource(FixtureRoot);

            Assert.IsFalse(source.TryGetFile(RoPath.Parse("texture/nope.bmp"), out _));
            Assert.IsFalse(source.TryGetFile(RoPath.Parse("texture"), out _), "a folder is not a file");
            Assert.IsFalse(new RoFolderSource(Path.Combine(FixtureRoot, "no-such-root")).TryGetFile(RoPath.Parse("texture/a.bmp"), out _));
        }

        [Test]
        public void Enumerate_ReturnsCanonicalPathsInOrdinalOrder()
        {
            var source = new RoFolderSource(FixtureRoot);

            var paths = source.Enumerate("texture/").Select(p => p.Canonical).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "texture/a.bmp",
                "texture/alpha32.tga",
                "texture/corrupt.bmp",
                "texture/key8.bmp",
                "texture/rgba.png",
                "texture/unsupported.dds",
                $"texture/{KoreanFolder}/leaf.bmp",
                $"texture/{KoreanUi}/button.bmp",
            }, paths);
        }

        [Test]
        public void Enumerate_NormalizesThePrefix_AndMatchesWholeSegments()
        {
            var source = new RoFolderSource(FixtureRoot);

            CollectionAssert.AreEqual(source.Enumerate("texture/"), source.Enumerate(@"Data\TEXTURE\"));
            CollectionAssert.AreEqual(new[] { $"texture/{KoreanUi}/button.bmp" },
                source.Enumerate($"texture/{KoreanUi}/").Select(p => p.Canonical));
            Assert.AreEqual(8, source.Enumerate("").Count(), "an empty prefix lists the whole root");
            CollectionAssert.IsEmpty(source.Enumerate("text/"));
            CollectionAssert.IsEmpty(new RoFolderSource(Path.Combine(FixtureRoot, "no-such-root")).Enumerate(""));
        }

        // Two names that differ only by Unicode form (or case) collide on one canonical path. NTFS keeps NFC and NFD names
        // apart but not names that differ only by case, so the collision is built from the Korean forms.
        [Test]
        public void TwoFilesWithOneCanonicalPath_GiveOneDuplicateIssue_AndADeterministicWinner()
        {
            string composedName = "잎.bmp";
            string decomposedName = composedName.Normalize(NormalizationForm.FormD);
            _temp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(_temp, "texture");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, composedName), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(folder, decomposedName), new byte[] { 2, 2 });
            Assume.That(Directory.GetFiles(folder).Length, Is.EqualTo(2), "this file system merges NFC and NFD names");

            string expectedWinner = Directory.GetFiles(folder).OrderBy(p => p, StringComparer.Ordinal).First();
            var report = new RoReport();
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[RoImport\] SourceDuplicate texture/잎\.bmp"));

            var source = new RoFolderSource(_temp, report);
            Assert.IsTrue(source.TryGetFile(RoPath.Parse("texture/잎.bmp"), out var first));
            source.Refresh();
            Assert.IsTrue(source.TryGetFile(RoPath.Parse("texture/잎.bmp"), out var again));

            Assert.AreEqual(1, report.Issues.Count(i => i.Code == RoIssueCode.SourceDuplicate), "one issue, even after a refresh");
            Assert.AreEqual(Full(expectedWinner), Full(first.DiskPath), "the first in ordinal disk-path order wins");
            Assert.AreEqual(first.DiskPath, again.DiskPath);
            Assert.AreEqual(1, source.Enumerate("").Count());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Refresh_PicksUpFilesAddedAfterTheFirstLookup()
        {
            _temp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_temp, "model"));
            var source = new RoFolderSource(_temp);
            Assert.IsFalse(source.TryGetFile(RoPath.Parse("model/a.rsm"), out _));

            File.WriteAllBytes(Path.Combine(_temp, "model", "A.rsm"), new byte[] { 0 });
            Assert.IsFalse(source.TryGetFile(RoPath.Parse("model/a.rsm"), out _), "the index is built once per session");
            source.Refresh();

            Assert.IsTrue(source.TryGetFile(RoPath.Parse("model/a.rsm"), out _));
        }

        static string Full(string path) => Path.GetFullPath(path);

        internal static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }
    }
}
