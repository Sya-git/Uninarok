using System.Linq;
using UnityEditor;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;   // the class name equals our root namespace

namespace Uninarok.RoImport.Editor.Effects
{
    /// <summary>Editor menu of the effect importers (PLAN-0012 4.10). Conversions run only when chosen here or called from code.</summary>
    public static class RoEffectMenu
    {
        [MenuItem("Tools/Ragnarok/Effects/Convert Effect Sprites", priority = 40)]
        static void ConvertEffectSprites()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            int count = RoEffectSpriteEntryConverter.Jobs(source).Count;
            if (count == 0)
            {
                EditorUtility.DisplayDialog("Ragnarok import", $"No effect sprites under {RoEffectSpriteEntryConverter.Prefix} in {source.Root}.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Ragnarok import", $"Make {count} effect sprite entries from {source.Root}?", "Convert", "Cancel"))
                return;
            RoImportApi.EffectSprites(new RoImportOptions { Source = source });
        }

        [MenuItem("Tools/Ragnarok/Effects/Convert Effect Folder", priority = 39)]
        static void ConvertEffectFolder()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            const string prefixes = "texture/effect/\nsprite/이팩트/";
            int strs = RoImportJobs.Expand(prefixes, RoJobKind.Effect, source).Jobs.Count;
            int authored = RoEffectFolderImport.LoadAuthored(null).Sum(a => a.Names.Count);
            if (!EditorUtility.DisplayDialog("Ragnarok import",
                    $"Convert {strs} effect STRs and the {authored} texture names of the authored entries from {source.Root}?", "Convert", "Cancel"))
                return;
            RoEffectFolderImport.Run(new RoImportOptions { Source = source }, prefixes);
        }

        [MenuItem("Tools/Ragnarok/Effects/Rebuild Effect Index", priority = 41)]
        static void RebuildEffectIndex() =>
            RoImportApi.EffectIndex(new RoImportOptions { Source = new RoFolderSource(RoSourceLocation.Resolve()) });

        [MenuItem("Tools/Ragnarok/Effects/Check Effect Data", priority = 42)]
        static void CheckEffectData() => UnityEngine.Debug.Log(RunEffectDataCheck().ToText());

        /// <summary>The effect data check on the project's data source, the generated index and the authored assets (PLAN-0012 4.9). Reads only.</summary>
        public static RoEffectDataReport RunEffectDataCheck()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>("Assets/Art/Imported/effect/_index.asset")
                        ?? UnityEngine.ScriptableObject.CreateInstance<EffectCatalogIndex>();
            var authored = AssetDatabase.FindAssets("t:EffectAsset", new[] { "Assets/Data/Effects/Authored" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();
            return RoEffectDataCheck.Run(source, index,
                r => AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetDatabase.GUIDToAssetPath(r.Guid)), authored);
        }
    }
}
