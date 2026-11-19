using System.Linq;
using UnityEditor;
using UnityEngine;
using Uninarok.Sprites;

namespace Uninarok.RoImport.Editor.Sprites
{
    /// <summary>
    /// Inspector of a converted <see cref="SpriteAnimation"/> (PLAN-0007 4.8, SPEC-0007 FR-10): its counts and a button that
    /// opens the preview. The arrays themselves are not drawn (tens of thousands of entries).
    /// </summary>
    [CustomEditor(typeof(SpriteAnimation))]
    public sealed class SpriteAnimationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var a = (SpriteAnimation)target;
            EditorGUILayout.LabelField("Actions in the file", a.FileActionCount.ToString());
            EditorGUILayout.LabelField("Unique actions", a.Actions.Length.ToString());
            EditorGUILayout.LabelField("Animations", a.Directional ? $"{a.AnimationCount} × 8 directions" : $"{a.AnimationCount} (not directional)");
            EditorGUILayout.LabelField("Frames", a.Frames.Length.ToString());
            EditorGUILayout.LabelField("Layers", $"{a.Layers.Length} (up to {a.MaxLayers} per frame)");
            EditorGUILayout.LabelField("Anchors", a.Anchors.Length.ToString());
            EditorGUILayout.LabelField("Sprites", $"{a.Sprites.Count(s => s != null)} of {a.Sprites.Length}");
            EditorGUILayout.LabelField("Pixels per unit", a.PixelsPerUnit.ToString("0.##"));
            if (a.Events.Length > 0) EditorGUILayout.LabelField("Events", string.Join(", ", a.Events));
            var sheet = a.Sprites.FirstOrDefault(s => s != null);
            if (sheet != null)
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Sheet", sheet.texture, typeof(Texture2D), false);

            EditorGUILayout.Space();
            if (GUILayout.Button("Open preview")) RoSpritePreviewWindow.Open(a);
        }
    }
}
