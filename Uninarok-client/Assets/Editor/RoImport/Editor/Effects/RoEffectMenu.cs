using UnityEditor;
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
    }
}
