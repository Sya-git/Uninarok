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

        /// <summary>Every <c>.gat</c> in the data root, where the map files sit (PLAN-0005 4.5, D9).</summary>
        internal static System.Collections.Generic.List<RoJob> MapDataJobs(IRoSource source) =>
            RoImportJobs.Expand("/", RoJobKind.MapData, source).Jobs;

        /// <summary>Every <c>.gnd</c> in the data root, where the map files sit (PLAN-0004 4.6, D11).</summary>
        internal static System.Collections.Generic.List<RoJob> GroundJobs(IRoSource source) =>
            RoImportJobs.Expand("/", RoJobKind.Ground, source).Jobs;
    }
}
