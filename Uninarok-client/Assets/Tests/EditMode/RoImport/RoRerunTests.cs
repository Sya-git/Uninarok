using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;   // the class name equals our root namespace

namespace Uninarok.RoImport.Tests
{
    /// <summary>Shared setup of the rerun tests: the throwaway root, a temporary ledger folder, the fixture source.</summary>
    public abstract class RoRunFixture
    {
        protected RoOutputRoots Roots;
        protected string Temp;

        [SetUp]
        public void SetUpRun()
        {
            Roots = RoImportTestRoot.Create();
            Temp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDownRun()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(Temp)) Directory.Delete(Temp, recursive: true);
        }

        protected RoImportOptions Options(bool force = false) => new RoImportOptions
        {
            Force = force,
            ShowProgress = false,
            Roots = Roots,
            Source = new RoFolderSource(RoFolderSourceTests.FixtureRoot),
            LedgerFolder = Path.Combine(Temp, "ledger"),
        };

        protected static RoJob Job(string path) => new RoJob(RoJobKind.Texture, RoPath.Parse(path));

        protected static readonly string[] Good = { "texture/a.bmp", "texture/key8.bmp", "texture/alpha32.tga", "texture/rgba.png", "texture/유저인터페이스/button.bmp" };

        protected IEnumerable<RoJob> GoodJobs() => Good.Select(Job);

        protected string RootDisk => RoImportTestRoot.FullPath(RoImportTestRoot.Folder);

        /// <summary>Relative path → SHA-256 of every file under the root; .meta files only when asked.</summary>
        protected SortedDictionary<string, string> Snapshot(bool metas, bool maskGuids = false)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(RootDisk, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.Ordinal) != metas) continue;
                byte[] bytes = File.ReadAllBytes(file);
                if (maskGuids && file.EndsWith(".mat", StringComparison.Ordinal))
                    bytes = Encoding.UTF8.GetBytes(Regex.Replace(Encoding.UTF8.GetString(bytes), "guid: [0-9a-f]{32}", "guid: <masked>"));
                using (var sha = SHA256.Create())
                    result[file.Substring(RootDisk.Length).Replace('\\', '/')] = string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
            return result;
        }

        protected Dictionary<string, string> Guids() =>
            AssetDatabase.FindAssets("", new[] { RoImportTestRoot.Folder })
                .Distinct()
                .ToDictionary(AssetDatabase.GUIDToAssetPath, g => g);
    }

    // SPEC-0002 FR-5 (idempotent, skipped, references kept), FR-6 (dependencies on demand). Plan 4.7, 4.8, 4.10. TASKS T6.3.
    [Ignore("pending T6.4")]
    public class RoIdempotencyTests : RoRunFixture
    {
        [Test]
        public void ConvertingTwice_LeavesTheSameAssets_AndReportsSkipped()
        {
            var first = RoImportApi.Run(GoodJobs(), Options());
            var material = RoImportApi.Material(RoPath.Parse("texture/a.bmp"), RoSurface.Cutout, false, Options()).Value;
            string materialPath = AssetDatabase.GetAssetPath(material);
            string textureGuid = AssetDatabase.AssetPathToGUID(RoOutputPaths.Texture(Roots, RoPath.Parse("texture/a.bmp")));
            var files = Snapshot(metas: false);
            var guids = Guids();

            var second = RoImportApi.Run(GoodJobs(), Options());

            Assert.AreEqual(Good.Length, first.Created);
            Assert.AreEqual(Good.Length, second.Skipped);
            Assert.AreEqual(0, second.Created + second.Updated + second.Failed);
            CollectionAssert.AreEqual(files, Snapshot(metas: false), "same files, same content");
            CollectionAssert.AreEquivalent(guids, Guids(), "same assets, same GUIDs");
            StringAssert.Contains("guid: " + textureGuid, File.ReadAllText(RoImportTestRoot.FullPath(materialPath)),
                "the material still references the same texture");
        }

        [Test]
        public void DeletedOutput_IsRecreated_WithTheSameContent()
        {
            RoImportApi.Run(GoodJobs(), Options());
            string output = RoOutputPaths.Texture(Roots, RoPath.Parse("texture/a.bmp"));
            byte[] before = File.ReadAllBytes(RoImportTestRoot.FullPath(output));
            AssetDatabase.DeleteAsset(output);

            var report = RoImportApi.Run(GoodJobs(), Options());

            Assert.AreEqual(1, report.Created);
            Assert.AreEqual(Good.Length - 1, report.Skipped);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(RoImportTestRoot.FullPath(output)));
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(output), "Unity assigns a new GUID (D4)");
        }

        [Test]
        public void MaterialInAnEmptyRoot_ConvertsItsTextureFirst()
        {
            string texturePath = RoOutputPaths.Texture(Roots, RoPath.Parse("texture/a.bmp"));
            Assume.That(File.Exists(RoImportTestRoot.FullPath(texturePath)), Is.False);

            var material = RoImportApi.Material(RoPath.Parse("texture/a.bmp"), RoSurface.Cutout, false, Options());

            Assert.IsTrue(File.Exists(RoImportTestRoot.FullPath(texturePath)));
            Assert.IsTrue(material.Value.GetTexture("_BaseMap") == AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        }

        [Test]
        public void TextureByPath_WorksWithoutAnyUi()
        {
            var result = RoImportApi.Texture(@"DATA\Texture\A.BMP", Options());

            Assert.IsNotNull(result.Value);
            Assert.AreEqual(RoOutputPaths.Texture(Roots, RoPath.Parse("texture/a.bmp")), AssetDatabase.GetAssetPath(result.Value));
        }
    }

    // SPEC-0002 FR-16 (identical files when inputs and rules are unchanged), FR-4. Plan 4.7, 4.8. TASKS T6.3.
    [Ignore("pending T6.4")]
    public class RoDeterminismTests : RoRunFixture
    {
        void ConvertEverything()
        {
            RoImportApi.Run(GoodJobs(), Options());
            RoImportApi.Material(RoPath.Parse("texture/a.bmp"), RoSurface.Cutout, true, Options());
            RoImportApi.Material(RoPath.Parse("texture/alpha32.tga"), RoSurface.Blend, false, Options());
            RoImportApi.Material(RoPath.Parse("texture/nope.bmp"), RoSurface.Opaque, false, Options());   // placeholder-backed
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void ConvertDeleteConvert_GivesIdenticalFiles_ExceptMetas()
        {
            ConvertEverything();
            var first = Snapshot(metas: false, maskGuids: true);

            RoImportTestRoot.Delete();
            RoImportTestRoot.Create();
            ConvertEverything();

            CollectionAssert.AreEqual(first, Snapshot(metas: false, maskGuids: true),
                "byte-identical outputs; inside .mat files only the texture GUID (new after deletion, D4) may differ");
            Assert.Greater(first.Count, Good.Length, "textures, materials and the placeholder were compared");
        }

        [Test]
        public void SecondRunInPlace_LeavesEveryFileAndMetaUnchanged()
        {
            ConvertEverything();
            var files = Snapshot(metas: false);
            var metas = Snapshot(metas: true);

            ConvertEverything();

            CollectionAssert.AreEqual(files, Snapshot(metas: false));
            CollectionAssert.AreEqual(metas, Snapshot(metas: true), "no .meta changed: no reimport, no settings rewrite");
        }
    }
}
