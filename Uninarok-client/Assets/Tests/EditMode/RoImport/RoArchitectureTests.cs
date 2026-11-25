using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-3 (nothing converts implicitly), FR-13 (rules testable without the editor), FR-14 (editor-only).
    // Plan 3, 4.4, 4.14, D1, D3, D7. TASKS T7.2.
    public class RoArchitectureTests
    {
        const string CoreAssembly = "Uninarok.RoImport.Core";
        const string EditorAssembly = "Uninarok.RoImport.Editor";
        const string TestAssembly = "Uninarok.RoImport.Tests";

        /// <summary>The importer assemblies, plus the project's own default editor assembly where the old importers lived.</summary>
        static readonly string[] ImplicitHookAssemblies = { CoreAssembly, EditorAssembly, "Assembly-CSharp-Editor" };

        static string ImporterRoot => Path.Combine(Application.dataPath, "Editor", "RoImport");

        // Every hook Unity would call on import or on load, from Unity's own type cache.
        static IEnumerable<(string assembly, string what)> Hooks() =>
            TypeCache.GetTypesDerivedFrom<AssetPostprocessor>().Select(t => (t.Assembly.GetName().Name, t.FullName + " is an AssetPostprocessor"))
                .Concat(TypeCache.GetTypesDerivedFrom<ScriptedImporter>().Select(t => (t.Assembly.GetName().Name, t.FullName + " is a ScriptedImporter")))
                .Concat(TypeCache.GetTypesWithAttribute<InitializeOnLoadAttribute>().Select(t => (t.Assembly.GetName().Name, t.FullName + " is [InitializeOnLoad]")))
                .Concat(TypeCache.GetMethodsWithAttribute<InitializeOnLoadMethodAttribute>()
                    .Select(m => (m.DeclaringType.Assembly.GetName().Name, $"{m.DeclaringType.FullName}.{m.Name} is [InitializeOnLoadMethod]")));

        // ---- FR-3: no implicit conversion ----

        [TestCaseSource(nameof(ImplicitHookAssemblies))]
        public void NoTypeHooksIntoImport_OrRunsOnLoad(string assemblyName)
        {
            var offenders = Hooks().Where(h => h.assembly == assemblyName).Select(h => h.what).ToArray();

            CollectionAssert.IsEmpty(offenders, "a conversion may only start from RoImport or the menu (FR-3, D3)");
        }

        [Test]
        public void TheHookScan_SeesHooksWhereTheyExist()
        {
            Assert.IsTrue(Hooks().Any(h => h.assembly == "Unity.RenderPipelines.Universal.Editor"),
                "URP's editor has postprocessors, so an empty result for our assemblies means something");
        }

        // ---- FR-13 / D1: Core holds no AssetDatabase code ----

        [Test]
        public void CoreAssembly_DoesNotReferenceUnityEditor()
        {
            var core = typeof(Uninarok.RoImport.Core.Results.RoReport).Assembly;
            Assert.AreEqual(CoreAssembly, core.GetName().Name);

            var editorReferences = core.GetReferencedAssemblies().Select(r => r.Name)
                .Where(n => n.StartsWith("UnityEditor", StringComparison.Ordinal)).ToArray();

            CollectionAssert.IsEmpty(editorReferences);
        }

        // ---- FR-14: editor only ----

        [TestCase("Editor/RoImport/Core/Uninarok.RoImport.Core.asmdef")]
        [TestCase("Editor/RoImport/Editor/Uninarok.RoImport.Editor.asmdef")]
        [TestCase("Tests/EditMode/RoImport/Uninarok.RoImport.Tests.asmdef")]
        [TestCase("Tests/EditMode/Sprites/Uninarok.Sprites.Tests.asmdef")]
        [TestCase("Tests/EditMode/SpriteCatalog/Uninarok.SpriteCatalog.Tests.asmdef")]
        public void AssemblyDefinition_IsEditorOnly(string relative)
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, relative));

            var asmdef = JsonUtility.FromJson<AsmdefPlatforms>(json);

            CollectionAssert.AreEqual(new[] { "Editor" }, asmdef.includePlatforms, relative);
            CollectionAssert.IsEmpty(asmdef.excludePlatforms ?? new string[0], relative);
        }

        [Serializable]
        sealed class AsmdefPlatforms
        {
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public string[] references;
        }

        // ---- PLAN-0007 §1, §3: the sprite runtime ships in players and knows nothing of the importer ----

        [Test]
        public void SpritesRuntimeAssembly_IsForAllPlatforms_WithNoReferenceOfOurs()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Sprites/Uninarok.Sprites.asmdef"));

            var asmdef = JsonUtility.FromJson<AsmdefPlatforms>(json);

            CollectionAssert.IsEmpty(asmdef.includePlatforms ?? new string[0]);
            CollectionAssert.IsEmpty(asmdef.excludePlatforms ?? new string[0]);
            CollectionAssert.IsEmpty(asmdef.references ?? new string[0]);
        }

        [Test]
        public void SpritesRuntimeAssembly_ReferencesNoEditorNoImporterAndNoGrf()
        {
            var sprites = typeof(Uninarok.Sprites.SpriteAnimation).Assembly;
            Assert.AreEqual("Uninarok.Sprites", sprites.GetName().Name);

            var offenders = sprites.GetReferencedAssemblies().Select(r => r.Name)
                .Where(n => n.StartsWith("UnityEditor", StringComparison.Ordinal) || n.StartsWith("Uninarok.RoImport", StringComparison.Ordinal) || n == "GRF")
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        // ---- PLAN-0008 §3, D19 (TASKS-0008 T3.1, FR-8): the catalog runtime ships in players, on the sprite runtime ----

        static readonly string[] CatalogRuntimeReferences = { "Uninarok.Sprites", "Uninarok.Networking", "Unity.Addressables", "Unity.ResourceManager" };

        [Test]
        public void CatalogRuntimeAssembly_IsForAllPlatforms_WithOnlyItsPlannedReferences()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/SpriteCatalog/Uninarok.SpriteCatalog.asmdef"));

            var asmdef = JsonUtility.FromJson<AsmdefPlatforms>(json);

            CollectionAssert.IsEmpty(asmdef.includePlatforms ?? new string[0]);
            CollectionAssert.IsEmpty(asmdef.excludePlatforms ?? new string[0]);
            CollectionAssert.AreEquivalent(CatalogRuntimeReferences, asmdef.references ?? new string[0]);
        }

        [Test]
        public void CatalogRuntimeAssembly_ReferencesNoEditorNoImporterAndNoGrf()
        {
            var catalog = Type.GetType("Uninarok.Catalog.SpriteRef, Uninarok.SpriteCatalog")?.Assembly;
            Assert.IsNotNull(catalog, "the catalog runtime assembly is loaded");

            var offenders = catalog.GetReferencedAssemblies().Select(r => r.Name)
                .Where(n => n.StartsWith("UnityEditor", StringComparison.Ordinal) || n.StartsWith("Uninarok.RoImport", StringComparison.Ordinal)
                            || n == "GRF" || n.StartsWith("Unity.Addressables.Editor", StringComparison.Ordinal))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        // ---- D7 / plan 4.4: no file format parsed outside GRF and its adapters ----

        static readonly Regex Forbidden = new Regex(@"\bBinaryReader\b|\bBitConverter\b|\bMemoryStream\b|\busing\s+GRF\b|\bGRF\s*\.");

        [Test]
        public void OutsideCoreFormats_NoFileIsParsedByHand_AndNoGrfTypeIsUsed()
        {
            string formats = Path.Combine(ImporterRoot, "Core", "Formats") + Path.DirectorySeparatorChar;
            var files = Directory.GetFiles(ImporterRoot, "*.cs", SearchOption.AllDirectories);
            Assume.That(files.Length, Is.GreaterThan(20), "the importer sources were found");

            var offenders = new List<string>();
            foreach (string file in files)
            {
                if (file.StartsWith(formats, StringComparison.OrdinalIgnoreCase)) continue;
                string code = CodeOnly(File.ReadAllText(file));
                foreach (Match m in Forbidden.Matches(code))
                    offenders.Add($"{file.Substring(ImporterRoot.Length + 1)}: {m.Value}");
            }

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void TheAdapter_IsWhereGrfIsUsed()
        {
            string reader = File.ReadAllText(Path.Combine(ImporterRoot, "Core", "Formats", "RoImageReader.cs"));

            Assert.IsTrue(Forbidden.IsMatch(CodeOnly(reader)), "the scan recognizes GRF use, so a clean result elsewhere means something");
        }

        [Test]
        public void CodeOnly_IgnoresCommentsAndStrings()
        {
            Assert.IsFalse(Forbidden.IsMatch(CodeOnly("// uses GRF.Image\n/* BinaryReader */ var s = \"GRF.dll\"; var v = @\"MemoryStream\";")));
            Assert.IsTrue(Forbidden.IsMatch(CodeOnly("var r = new BinaryReader(stream); // read")));
        }

        // ---- SPEC-0012 FR-2, FR-4 (TASKS-0012 T9.4): the effects assemblies ----

        static string AssetsPath(string relative) => Path.Combine(Application.dataPath, relative);

        static string[] ReferencesOf(string asmdefRelative) =>
            JsonUtility.FromJson<AsmdefPlatforms>(File.ReadAllText(AssetsPath(asmdefRelative))).references ?? new string[0];

        [Test]
        public void EffectsRuntimeAssembly_IsForAllPlatforms_WithOnlyItsPlannedReferences()
        {
            var asmdef = JsonUtility.FromJson<AsmdefPlatforms>(File.ReadAllText(AssetsPath("Scripts/Effects/Uninarok.Effects.asmdef")));

            CollectionAssert.IsEmpty(asmdef.includePlatforms ?? new string[0]);
            CollectionAssert.IsEmpty(asmdef.excludePlatforms ?? new string[0]);
            CollectionAssert.AreEquivalent(
                new[] { "Uninarok.Sprites", "Uninarok.SpriteCatalog", "Uninarok.Map", "Unity.Addressables", "Unity.ResourceManager" },
                asmdef.references ?? new string[0]);
        }

        [Test]
        public void EffectsEditorAssembly_IsEditorOnly_WithOnlyItsPlannedReferences()
        {
            var asmdef = JsonUtility.FromJson<AsmdefPlatforms>(File.ReadAllText(AssetsPath("Scripts/Effects/Editor/Uninarok.Effects.Editor.asmdef")));

            CollectionAssert.AreEqual(new[] { "Editor" }, asmdef.includePlatforms);
            CollectionAssert.IsEmpty(asmdef.excludePlatforms ?? new string[0]);
            // No GRF DLL reference: GRF stays in Core/Formats.
            CollectionAssert.AreEquivalent(
                new[] { "Uninarok.Effects", "Uninarok.Sprites", "Uninarok.SpriteCatalog", "Uninarok.RoImport.Core", "Uninarok.RoImport.Editor" },
                asmdef.references ?? new string[0]);
        }

        [Test]
        public void EffectsTestAssemblies_HaveTheirJustifiedReferences()
        {
            // Edit mode: the editor assembly, and the importer core (tests build STR fixtures through it).
            var edit = JsonUtility.FromJson<AsmdefPlatforms>(File.ReadAllText(AssetsPath("Tests/EditMode/Effects/Uninarok.Effects.Tests.asmdef")));
            CollectionAssert.AreEqual(new[] { "Editor" }, edit.includePlatforms);
            CollectionAssert.AreEquivalent(
                new[] { "UnityEngine.TestRunner", "UnityEditor.TestRunner", "Uninarok.Effects", "Uninarok.Effects.Editor", "Uninarok.RoImport.Core" },
                edit.references ?? new string[0]);

            // Play mode (editor runs only, UNITY_EDITOR constraint): units and networking drive the walking-unit tests,
            // the Addressables editor assembly builds the test catalog.
            var play = JsonUtility.FromJson<AsmdefPlatforms>(File.ReadAllText(AssetsPath("Tests/PlayMode/Effects/Uninarok.Effects.PlayTests.asmdef")));
            CollectionAssert.IsEmpty(play.includePlatforms ?? new string[0]);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "UnityEngine.TestRunner", "UnityEditor.TestRunner", "Uninarok.Effects", "Uninarok.Map", "Uninarok.Sprites", "Uninarok.SpriteCatalog",
                    "Uninarok.Units", "Uninarok.Networking", "Unity.Addressables", "Unity.ResourceManager", "Unity.Addressables.Editor"
                },
                play.references ?? new string[0]);
        }

        [Serializable]
        sealed class AsmdefDefineConstraints
        {
            public string[] references;
            public string[] defineConstraints;
        }

        [Test]
        public void WorldDevAssembly_ReachesEffectsAndUnits_UnderTheDevGuard()
        {
            var asmdef = JsonUtility.FromJson<AsmdefDefineConstraints>(File.ReadAllText(AssetsPath("Scripts/World/Dev/Uninarok.World.Dev.asmdef")));

            CollectionAssert.Contains(asmdef.references, "Uninarok.Effects");
            CollectionAssert.Contains(asmdef.references, "Uninarok.Units");
            CollectionAssert.AreEqual(new[] { "UNITY_EDITOR || DEVELOPMENT_BUILD" }, asmdef.defineConstraints);
        }

        [Test]
        public void EffectsRuntimeAssembly_ReferencesNoEditorAssembly()
        {
            foreach (string reference in ReferencesOf("Scripts/Effects/Uninarok.Effects.asmdef"))
                Assert.IsFalse(reference.Contains("Editor") || reference.StartsWith("Uninarok.RoImport", StringComparison.Ordinal) || reference.StartsWith("GRF", StringComparison.Ordinal),
                    "Uninarok.Effects ships in players: " + reference);

            var effects = Assembly.Load("Uninarok.Effects");
            var offenders = effects.GetReferencedAssemblies().Select(r => r.Name)
                .Where(n => n.StartsWith("UnityEditor", StringComparison.Ordinal) || n.Contains(".Editor") || n.StartsWith("Uninarok.RoImport", StringComparison.Ordinal) || n == "GRF")
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        // FR-4: no particle system, no visual effect graph, no compute.
        static readonly Regex ForbiddenEffectApi = new Regex(@"\bParticleSystem\w*\b|\bUnityEngine\.VFX\b|\bVisualEffect\b|\bComputeShader\b|\bComputeBuffer\b");

        static string[] EffectsRuntimeSources() =>
            Directory.GetFiles(AssetsPath("Scripts/Effects"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/Scripts/Effects/Editor/")).ToArray();

        [Test]
        public void EffectsRuntimeTypes_UseNoVfxParticleOrComputeApi()
        {
            var files = EffectsRuntimeSources();
            Assume.That(files.Length, Is.GreaterThan(10), "the effects runtime sources were found");

            var offenders = new List<string>();
            foreach (string file in files)
                foreach (Match m in ForbiddenEffectApi.Matches(CodeOnly(File.ReadAllText(file))))
                    offenders.Add($"{Path.GetFileName(file)}: {m.Value}");

            CollectionAssert.IsEmpty(offenders);
            Assert.IsTrue(ForbiddenEffectApi.IsMatch("new ParticleSystem(); using UnityEngine.VFX; ComputeShader c;"), "the scan recognizes the APIs");
        }

        [Test]
        public void EffectsRuntimeSources_UseNoEditorApi_AndNoGrf()
        {
            var forbidden = new Regex(@"\busing\s+UnityEditor\b|\bUnityEditor\s*\.|\busing\s+GRF\b|\bGRF\s*\.|\busing\s+Uninarok\.RoImport\b");
            var offenders = new List<string>();
            foreach (string file in EffectsRuntimeSources())
                foreach (Match m in forbidden.Matches(CodeOnly(File.ReadAllText(file))))
                    offenders.Add($"{Path.GetFileName(file)}: {m.Value}");

            CollectionAssert.IsEmpty(offenders);
        }

        // FR-2: the header check reads 8 bytes with BitConverter; a BinaryReader is never the tool, even in Core/Formats.
        [Test]
        public void ImporterCode_UsesNoBinaryReader_AnywhereIncludingFormats()
        {
            var files = Directory.GetFiles(ImporterRoot, "*.cs", SearchOption.AllDirectories);
            Assume.That(files.Length, Is.GreaterThan(20), "the importer sources were found");

            var binaryReader = new Regex(@"\bBinaryReader\b");
            var offenders = files.Where(f => binaryReader.IsMatch(CodeOnly(File.ReadAllText(f)))).Select(f => f.Substring(ImporterRoot.Length + 1)).ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        /// <summary>Source text without comments and string literals, so names in prose or paths do not count.</summary>
        static string CodeOnly(string source) =>
            Regex.Replace(source, @"//[^\n]*|/\*.*?\*/|@""(?:[^""]|"""")*""|""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])'", " ", RegexOptions.Singleline);
    }
}
