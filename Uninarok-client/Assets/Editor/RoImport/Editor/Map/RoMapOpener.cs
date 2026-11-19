using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Map
{
    /// <summary>
    /// Opens a converted map in the editor (PLAN-0006 4.7, SPEC-0006 FR-11): the generated scene as the single scene, then
    /// the hand-made additive scene on top when it exists. Loading both at runtime is the game's scene loading (out of scope).
    /// </summary>
    public static class RoMapOpener
    {
        [MenuItem("Tools/Ragnarok/Open Map", priority = 40)]
        static void OpenFromMenu()
        {
            var roots = new RoOutputRoots();
            string folder = Path.GetFullPath(roots.Scenes);
            string file = EditorUtility.OpenFilePanel("Open map", Directory.Exists(folder) ? folder : Application.dataPath, "unity");
            if (string.IsNullOrEmpty(file)) return;
            string name = Path.GetFileNameWithoutExtension(file);
            if (name.EndsWith(RoOutputPaths.AdditiveSuffix)) name = name.Substring(0, name.Length - RoOutputPaths.AdditiveSuffix.Length);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Open(name, roots))
                EditorUtility.DisplayDialog("Ragnarok import", $"No converted map '{name}' in {roots.Scenes}.", "OK");
        }

        /// <summary>Opens the map's scenes; false when the map has no generated scene. Unsaved changes are not asked about.</summary>
        public static bool Open(string mapName, RoOutputRoots roots)
        {
            var paths = RoMapOutputPaths.For(roots, RoPath.Parse(mapName + ".rsw"));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(paths.Scene) == null) return false;
            EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(paths.AdditiveScene) != null)
                EditorSceneManager.OpenScene(paths.AdditiveScene, OpenSceneMode.Additive);
            return true;
        }
    }
}
