using System.IO;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Output;

namespace Uninarok.RoImport.Tests
{
    /// <summary>
    /// The throwaway output root of the asset-writing tests (plan 6): <c>Assets/__RoImportTests</c>, created in SetUp and
    /// deleted, with its .meta, in TearDown, so git status stays clean.
    /// </summary>
    static class RoImportTestRoot
    {
        public const string Folder = "Assets/__RoImportTests";

        public static RoOutputRoots Create()
        {
            Delete();
            AssetDatabase.CreateFolder("Assets", "__RoImportTests");
            return RoOutputRoots.Under(Folder);
        }

        public static void Delete()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            string disk = FullPath(Folder);
            if (Directory.Exists(disk)) Directory.Delete(disk, recursive: true);
            if (File.Exists(disk + ".meta")) File.Delete(disk + ".meta");
            AssetDatabase.Refresh();
        }

        public static string FullPath(string projectPath) =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", projectPath));
    }
}
