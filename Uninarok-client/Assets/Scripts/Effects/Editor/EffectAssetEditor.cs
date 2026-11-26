using UnityEditor;
using UnityEngine;

namespace Uninarok.Effects.Editor
{
    /// <summary>Inspector of an <see cref="EffectAsset"/> (SPEC-0012 FR-11, plan 4.11): the summary panel, the "Open preview" button, then the raw fields.</summary>
    [CustomEditor(typeof(EffectAsset))]
    public sealed class EffectAssetEditor : UnityEditor.Editor
    {
        bool showRaw;

        public override void OnInspectorGUI()
        {
            var asset = (EffectAsset)target;
            if (GUILayout.Button("Open preview")) RoEffectPreviewWindow.OpenFor(asset.Path);
            EffectInspectorPanel.Draw(EffectInspectorModel.Build(asset, RoEffectPreviewWindow.MakeInput(asset)));
            showRaw = EditorGUILayout.Foldout(showRaw, "Raw fields", true);
            if (showRaw) DrawDefaultInspector();
        }
    }
}
