using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Output;

namespace Uninarok.RoImport.Editor.Map
{
    /// <summary>
    /// Makes every converted map scene loadable at run time (SPEC-0010 FR-1, FR-15; PLAN-0010 3, D5): the Addressables group
    /// <c>maps</c> with one entry per generated scene and per additive scene beside it, addressed by <see cref="MapSceneKeys"/>.
    /// Idempotent; the converter itself is not changed.
    /// </summary>
    public static class RoMapSceneAddressables
    {
        public const string Group = "maps";

        [MenuItem("Tools/Ragnarok/Register Map Scenes", priority = 41)]
        static void RegisterFromMenu()
        {
            int changes = Ensure(AddressableAssetSettingsDefaultObject.GetSettings(true), new RoOutputRoots());
            Debug.Log($"[RoImport] map scenes registered: {changes} change(s)");
        }

        /// <summary>Adds what is missing and corrects what is not as it should be; returns the number of changes, 0 on a second run.</summary>
        public static int Ensure(AddressableAssetSettings settings, RoOutputRoots roots)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            int changes = 0;
            var group = settings.FindGroup(Group);
            if (group == null)
            {
                group = settings.CreateGroup(Group, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                changes++;
            }
            string folder = roots.Scenes.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                foreach (string path in AssetDatabase.FindAssets("t:SceneAsset", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
                {
                    if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), folder, StringComparison.Ordinal)) continue;
                    string name = Path.GetFileNameWithoutExtension(path);
                    // An additive scene of a map that was not converted is nothing to load.
                    if (name.EndsWith(RoOutputPaths.AdditiveSuffix, StringComparison.Ordinal)
                        && AssetDatabase.LoadAssetAtPath<SceneAsset>($"{folder}/{name.Substring(0, name.Length - RoOutputPaths.AdditiveSuffix.Length)}.unity") == null) continue;
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    var entry = settings.FindAssetEntry(guid);
                    if (entry == null || entry.parentGroup != group)
                    {
                        entry = settings.CreateOrMoveEntry(guid, group, false, false);
                        changes++;
                    }
                    string key = MapSceneKeys.Scene(name); // "izlude.additive" gives the additive key
                    if (entry.address != key)
                    {
                        entry.SetAddress(key, false);
                        changes++;
                    }
                }
            }
            if (changes > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
            }
            return changes;
        }
    }
}
