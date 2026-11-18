using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Map.Tests
{
    // TASKS-0005 T1.1, T1.3 (FR-4, PLAN-0005 4.3, D3): the map data lives in a runtime assembly that references no editor
    // and no importer code, so it exists in player builds without the source files.
    [Ignore("pending T1.3")]
    public class MapDataAssemblyTests
    {
        const string AssemblyName = "Uninarok.Map";

        [Test]
        public void TheAssetClasses_AreInTheRuntimeAssembly()
        {
            Assert.AreEqual(AssemblyName, typeof(MapData).Assembly.GetName().Name);
            Assert.AreEqual(AssemblyName, typeof(MapRoot).Assembly.GetName().Name);
            Assert.AreEqual(AssemblyName, typeof(MapCellType).Assembly.GetName().Name);
        }

        [Test]
        public void TheRuntimeAssembly_ReferencesNoEditorAndNoImporterAssembly()
        {
            var references = typeof(MapData).Assembly.GetReferencedAssemblies().Select(r => r.Name).ToArray();

            var offenders = references.Where(n => n.StartsWith("UnityEditor", StringComparison.Ordinal)
                                                  || n.StartsWith("Uninarok.RoImport", StringComparison.Ordinal)
                                                  || n == "GRF").ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void TheAssemblyDefinition_IsForAllPlatforms_WithNoReferences()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Map/Uninarok.Map.asmdef"));

            var asmdef = JsonUtility.FromJson<Asmdef>(json);

            Assert.AreEqual(AssemblyName, asmdef.name);
            CollectionAssert.IsEmpty(asmdef.includePlatforms ?? new string[0], "all platforms, so players get the asset");
            CollectionAssert.IsEmpty(asmdef.excludePlatforms ?? new string[0]);
            CollectionAssert.IsEmpty(asmdef.references ?? new string[0]);
        }

        [Serializable]
        sealed class Asmdef
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
        }
    }
}
