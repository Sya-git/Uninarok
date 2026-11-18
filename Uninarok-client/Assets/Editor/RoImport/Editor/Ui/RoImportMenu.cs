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
    }
}
