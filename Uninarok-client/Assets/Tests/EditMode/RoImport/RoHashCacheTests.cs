using System;
using System.IO;
using NUnit.Framework;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-5/FR-16 support: unchanged sources are not rehashed (plan 4.2 RoHashCache). TASKS T2.2.
    public class RoHashCacheTests
    {
        string _temp;
        int _opens;

        [SetUp]
        public void SetUp()
        {
            _temp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_temp, "data", "texture"));
            _opens = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }

        RoHashCache CountingCache(string file = null)
        {
            var cache = new RoHashCache(file);
            var open = cache.OpenRead;
            cache.OpenRead = p => { _opens++; return open(p); };
            return cache;
        }

        string WriteSource(byte[] bytes, DateTime writeTimeUtc)
        {
            string disk = Path.Combine(_temp, "data", "texture", "a.bmp");
            File.WriteAllBytes(disk, bytes);
            File.SetLastWriteTimeUtc(disk, writeTimeUtc);
            return disk;
        }

        RoSourceFile Lookup(RoHashCache cache)
        {
            var source = new RoFolderSource(Path.Combine(_temp, "data"), hashes: cache);
            Assert.IsTrue(source.TryGetFile(RoPath.Parse("texture/a.bmp"), out var file));
            return file;
        }

        [Test]
        public void SecondHashOfAnUnchangedFile_DoesNotReadTheFile()
        {
            var bytes = new byte[] { 1, 2, 3 };
            WriteSource(bytes, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var cache = CountingCache();

            string first = Lookup(cache).ContentHash();
            string second = Lookup(cache).ContentHash();

            Assert.AreEqual(1, _opens);
            Assert.AreEqual(first, second);
            Assert.AreEqual(RoFolderSourceTests.Sha256Hex(bytes), first);
        }

        [Test]
        public void ChangedFile_IsRehashed()
        {
            WriteSource(new byte[] { 1, 2, 3 }, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var cache = CountingCache();
            string before = Lookup(cache).ContentHash();

            var after = new byte[] { 9, 9, 9 };                     // same length, new write time
            WriteSource(after, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            string changed = Lookup(cache).ContentHash();

            Assert.AreEqual(2, _opens);
            Assert.AreNotEqual(before, changed);
            Assert.AreEqual(RoFolderSourceTests.Sha256Hex(after), changed);
        }

        [Test]
        public void SavedCache_IsUsedAfterReload_WithoutReadingTheFile()
        {
            WriteSource(new byte[] { 4, 5, 6 }, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            string cacheFile = Path.Combine(_temp, "Library", "RoImport", "hashcache.json");
            var first = CountingCache(cacheFile);
            string hash = Lookup(first).ContentHash();
            first.Save();
            string saved = File.ReadAllText(cacheFile);

            var reloaded = CountingCache(cacheFile);
            reloaded.Load();
            string again = Lookup(reloaded).ContentHash();
            reloaded.Save();

            Assert.AreEqual(1, _opens, "only the first cache read the file");
            Assert.AreEqual(hash, again);
            Assert.AreEqual(saved, File.ReadAllText(cacheFile), "an unchanged cache is not rewritten");
            StringAssert.DoesNotContain("\r\n", saved);
        }

        [Test]
        public void UnreadableCacheFile_GivesAnEmptyCache()
        {
            string cacheFile = Path.Combine(_temp, "hashcache.json");
            File.WriteAllText(cacheFile, "{ not json");
            var cache = new RoHashCache(cacheFile);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Hash cache .* rebuilt"));
            cache.Load();

            Assert.AreEqual(0, cache.Count);
        }

        [Test]
        public void DefaultFile_IsInTheProjectLibraryFolder()
        {
            string expected = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "RoImport", "hashcache.json"));
            Assert.AreEqual(expected, Path.GetFullPath(RoHashCache.DefaultFile));
        }
    }
}
