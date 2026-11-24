using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// The Addressables layout of the catalog (SPEC-0008 FR-8; PLAN-0008 4.6, D13): groups holding <b>folder entries</b> only,
    /// whose assets are addressable by their own project path. <see cref="Ensure"/> creates what is missing and is idempotent;
    /// <see cref="Verify"/> checks that every typed reference in catalog assets resolves inside one of those folder entries.
    /// Groups use the package's default schemas; pack mode, compression and hosting are left to the loading-strategy spec.
    /// </summary>
    public static class RoCatalogAddressables
    {
        /// <summary>The mirrored sprite folders and their group suffix (PLAN-0008 4.6).</summary>
        static (string group, string folder)[] SpriteFolders => new[]
        {
            ("sprites-monster", "몬스터"), ("sprites-npc", "npc"), ("sprites-homun", "homun"), ("sprites-human", "인간족"),
            ("sprites-doram", "도람족"), ("sprites-garment", "로브"), ("sprites-headgear", "악세사리"), ("sprites-shield", "방패"),
            ("sprites-item", "아이템"), ("sprites-effect", "이팩트"),
        };

        /// <summary>Every (group, folder entry) of PLAN-0008 4.6 for these roots. Catalog folders are ours and always listed.</summary>
        public static IReadOnlyList<(string group, string folder, bool ours)> Layout(RoOutputRoots roots)
        {
            string art = roots.Art.TrimEnd('/');
            var list = new List<(string, string, bool)>();
            foreach (var c in RoCatalogOutputPaths.IndexedCategories)
                list.Add(("catalog-" + SpriteCatalogKeys.Folder(c), RoCatalogOutputPaths.Folder(roots, c), true));
            list.Add(("catalog-tables", RoCatalogOutputPaths.Root(roots) + "/" + RoCatalogOutputPaths.ItemsFolder, true));
            list.Add(("catalog-tables", RoCatalogOutputPaths.Root(roots) + "/" + RoCatalogOutputPaths.ClassesFolder, true));
            list.Add(("sprite-sheets", art + "/" + Sprites.RoSpriteOutputPaths.SheetsFolder, false));
            foreach (var (group, folder) in SpriteFolders) list.Add((group, art + "/sprite/" + folder, false));
            list.Add(("palettes", art + "/palette", false));
            // D27: the generated effect folder is written by the effect importer (not created here); the hand-made authored data folder is ours.
            list.Add(("effects", art + "/effect", false));
            list.Add(("effects", roots.Data.TrimEnd('/') + "/Effects", true));
            return list;
        }

        /// <summary>The project's settings, created by the package when missing (D13).</summary>
        public static AddressableAssetSettings ProjectSettings() => AddressableAssetSettingsDefaultObject.GetSettings(true);

        /// <summary>
        /// Creates the groups and folder entries of <see cref="Layout"/> that are missing (our catalog folders are created too;
        /// a sprite folder SPEC-0007 has not written yet gets its group and no entry). Returns the number of changes: 0 on a
        /// second run.
        /// </summary>
        public static int Ensure(AddressableAssetSettings settings, RoOutputRoots roots)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            int changes = 0;
            foreach (var (groupName, folder, ours) in Layout(roots))
            {
                var group = settings.FindGroup(groupName);
                if (group == null)
                {
                    group = settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                    changes++;
                }
                if (ours) CreateFolder(folder);
                string guid = AssetDatabase.AssetPathToGUID(folder);
                if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(folder)) continue;
                var entry = settings.FindAssetEntry(guid);
                if (entry == null || entry.parentGroup != group)
                {
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    changes++;
                }
                if (entry.address != folder)
                {
                    entry.SetAddress(folder, false);
                    changes++;
                }
            }
            if (changes > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
            }
            return changes;
        }

        /// <summary>
        /// Checks every typed Addressables reference serialized in <paramref name="assets"/> (PLAN-0008 4.6): a set reference
        /// must point at an existing asset inside a folder entry of the layout and, for a sprite, name a sub-asset the sheet
        /// has. Each one that does not is one <see cref="RoIssueCode.CatalogStrayFile"/> warning. Returns the references checked.
        /// </summary>
        public static int Verify(AddressableAssetSettings settings, RoOutputRoots roots, IEnumerable<Object> assets, RoReport report)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var folders = Layout(roots).Select(l => l.folder + "/").ToArray();
            int checkedCount = 0;
            foreach (var asset in assets ?? Enumerable.Empty<Object>())
            {
                if (asset == null) continue;
                string owner = AssetDatabase.GetAssetPath(asset);
                using (var so = new SerializedObject(asset))
                {
                    var it = so.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.String || it.name != "m_AssetGUID") continue;
                        string guid = it.stringValue;
                        if (string.IsNullOrEmpty(guid)) continue;
                        checkedCount++;
                        string where = $"{it.propertyPath} in {owner}";
                        string sub = so.FindProperty(it.propertyPath.Substring(0, it.propertyPath.Length - "m_AssetGUID".Length) + "m_SubObjectName")?.stringValue;
                        string problem = Check(settings, folders, guid, sub);
                        if (problem != null) report.Warn(RoIssueCode.CatalogStrayFile, guid + (string.IsNullOrEmpty(sub) ? "" : "[" + sub + "]"), owner, $"{where}: {problem}");
                    }
                }
            }
            return checkedCount;
        }

        static string Check(AddressableAssetSettings settings, string[] folders, string guid, string subObject)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null) return "the reference points at no asset";
            if (!folders.Any(f => path.StartsWith(f, StringComparison.Ordinal))) return $"{path} is outside the catalog's folder entries";
            var entry = settings.FindAssetEntry(guid, true);
            if (entry == null) return $"{path} is in no Addressables entry (run Ensure)";
            if (!string.IsNullOrEmpty(subObject)
                && !AssetDatabase.LoadAllAssetRepresentationsAtPath(path).Any(o => o != null && o.name == subObject))
                return $"{path} has no sub-asset {subObject}";
            return null;
        }

        static void CreateFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) CreateFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
