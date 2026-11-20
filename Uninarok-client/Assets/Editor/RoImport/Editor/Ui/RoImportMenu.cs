using System;
using System.IO;
using UnityEditor;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;   // the class name equals our root namespace

namespace Uninarok.RoImport.Editor.Ui
{
    /// <summary>
    /// Editor menu (plan 4.11; SPEC-0002 FR-2). Conversions run only when chosen here or called from code (FR-3): nothing
    /// in the importer reacts to files being copied or imported.
    /// </summary>
    public static class RoImportMenu
    {
        [MenuItem("Tools/Ragnarok/Import Window", priority = 0)]
        static void OpenWindow() => RoImportWindow.Open();

        [MenuItem("Tools/Ragnarok/Convert All Textures", priority = 20)]
        static void ConvertAllTextures()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = RoImportJobs.Expand("texture/", RoJobKind.Texture, source).Jobs;
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No textures under {source.Root}.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Ragnarok import", $"Convert {jobs.Count} textures from {source.Root}?", "Convert", "Cancel"))
                return;
            RoImportApi.Run(jobs);
        }

        [MenuItem("Tools/Ragnarok/Convert All Models", priority = 21)]
        static void ConvertAllModels()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = RoImportJobs.Expand("model/", RoJobKind.Model, source).Jobs;   // .rsm and .rsm2 (PLAN-0003 4.9)
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No models under {source.Root}.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Ragnarok import", $"Convert {jobs.Count} models from {source.Root}?", "Convert", "Cancel"))
                return;
            RoImportApi.Run(jobs);
        }

        [MenuItem("Tools/Ragnarok/Convert All Grounds", priority = 22)]
        static void ConvertAllGrounds()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = GroundJobs(source);
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No grounds (.gnd) in {source.Root}.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Ragnarok import", $"Convert {jobs.Count} grounds from {source.Root}?", "Convert", "Cancel"))
                return;
            RoImportApi.Run(jobs);
        }

        [MenuItem("Tools/Ragnarok/Convert All Map Data", priority = 23)]
        static void ConvertAllMapData()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = MapDataJobs(source);
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No map data (.gat) in {source.Root}.", "OK");
                return;
            }
            int choice = EditorUtility.DisplayDialogComplex("Ragnarok import",
                $"Convert {jobs.Count} map data files from {source.Root}?", "Convert", "Cancel", "Convert with images");
            if (choice == 1) return;
            RoImportApi.Run(jobs, new RoImportOptions { GatImage = choice == 2 });
        }

        [MenuItem("Tools/Ragnarok/Convert All Maps", priority = 24)]
        static void ConvertAllMaps()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = MapJobs(source);
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No maps (.rsw) in {source.Root}.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Ragnarok import",
                    $"Convert {jobs.Count} maps from {source.Root}? Their grounds, map data and models are converted as needed.", "Convert", "Cancel"))
                return;
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RoImportApi.Run(jobs);
        }

        [MenuItem("Tools/Ragnarok/Convert Sprite Folder…", priority = 25)]
        static void ConvertSpriteFolder()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            string start = Path.Combine(source.Root, "sprite");
            string chosen = EditorUtility.OpenFolderPanel("Sprite folder (under the data folder)", Directory.Exists(start) ? start : source.Root, "");
            if (string.IsNullOrEmpty(chosen)) return;
            if (!TryRelativeFolder(source.Root, chosen, out string folder))
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"Choose a folder inside {source.Root}.", "OK");
                return;
            }
            var jobs = SpriteJobs(source, folder);
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No sprites (.act) under {folder}.", "OK");
                return;
            }
            int choice = EditorUtility.DisplayDialogComplex("Ragnarok import",
                $"Convert {jobs.Count} sprites under {folder}? Each ACT uses the SPR of the same name.", "Convert", "Cancel", "Convert with palettes");
            if (choice == 1) return;
            RoImportApi.Run(jobs, new RoImportOptions { SpritePalettes = choice == 2 });
        }

        [MenuItem("Tools/Ragnarok/Convert All Palettes", priority = 26)]
        static void ConvertAllPalettes()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = PaletteJobs(source);
            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No palettes under {source.Root}.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Ragnarok import", $"Convert {jobs.Count} palettes from {source.Root}?", "Convert", "Cancel"))
                return;
            RoImportApi.Run(jobs);
        }

        /// <summary>Every <c>.act</c> under a folder of the data root (PLAN-0007 4.4).</summary>
        internal static System.Collections.Generic.List<RoJob> SpriteJobs(IRoSource source, string folder) =>
            RoImportJobs.Expand(folder.TrimEnd('/') + "/", RoJobKind.Sprite, source).Jobs;

        /// <summary>Every <c>.pal</c> under <c>palette/</c> (PLAN-0007 4.4).</summary>
        internal static System.Collections.Generic.List<RoJob> PaletteJobs(IRoSource source) =>
            RoImportJobs.Expand("palette/", RoJobKind.Palette, source).Jobs;

        /// <summary>A disk folder as a folder of the data root (forward slashes, no leading slash); false outside it.</summary>
        internal static bool TryRelativeFolder(string root, string chosen, out string folder)
        {
            folder = null;
            string r = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/') + "/";
            string c = Path.GetFullPath(chosen).Replace('\\', '/').TrimEnd('/') + "/";
            if (!c.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return false;
            folder = c.Substring(r.Length);
            return folder.Length > 0;
        }

        /// <summary>Every <c>.rsw</c> in the data root, where the map files sit (PLAN-0006 4.8, D16).</summary>
        internal static System.Collections.Generic.List<RoJob> MapJobs(IRoSource source) =>
            RoImportJobs.Expand("/", RoJobKind.Map, source).Jobs;

        /// <summary>Every <c>.gat</c> in the data root, where the map files sit (PLAN-0005 4.5, D9).</summary>
        internal static System.Collections.Generic.List<RoJob> MapDataJobs(IRoSource source) =>
            RoImportJobs.Expand("/", RoJobKind.MapData, source).Jobs;

        /// <summary>Every <c>.gnd</c> in the data root, where the map files sit (PLAN-0004 4.6, D11).</summary>
        internal static System.Collections.Generic.List<RoJob> GroundJobs(IRoSource source) =>
            RoImportJobs.Expand("/", RoJobKind.Ground, source).Jobs;
    }
}
