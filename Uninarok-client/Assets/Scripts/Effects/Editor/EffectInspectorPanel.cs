using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Uninarok.Effects.Editor
{
    /// <summary>The IMGUI drawing of an <see cref="EffectInspectorModel"/>, shared by the preview window and <see cref="EffectAssetEditor"/>.</summary>
    public static class EffectInspectorPanel
    {
        const float PlotHeight = 36f;
        const float ThumbSize = 48f;

        public static void Draw(EffectInspectorModel m)
        {
            var inv = CultureInfo.InvariantCulture;
            EditorGUILayout.LabelField(m.Path, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"kind {m.Kind}   {m.Fps.ToString("0.##", inv)} fps   length {m.FrameCount} frames, {m.LengthSeconds.ToString("0.###", inv)} s   {m.KeyCount} keys   speed {m.Speed.ToString("0.##", inv)}",
                EditorStyles.miniLabel);

            if (m.Layers.Count > 0)
            {
                EditorGUILayout.LabelField("Layers (" + m.Layers.Count + ")", EditorStyles.boldLabel);
                foreach (var l in m.Layers)
                    EditorGUILayout.LabelField($"#{l.Index}  {l.KeyCount} keys, {l.TextureCount} tex   " + string.Join("; ", l.BlendPairs), EditorStyles.miniLabel);
            }

            if (m.AuthoredValues.Count > 0)
            {
                EditorGUILayout.LabelField("Authored values", EditorStyles.boldLabel);
                foreach (var kv in m.AuthoredValues) EditorGUILayout.LabelField(kv.Key, kv.Value);
                foreach (var c in m.Curves) DrawCurve(c, inv);
            }

            if (m.Textures.Count > 0)
            {
                EditorGUILayout.LabelField("Textures (" + m.Textures.Count + ")", EditorStyles.boldLabel);
                foreach (var t in m.Textures) DrawTexture(t);
            }

            DrawLines("Warnings", m.Warnings);
            DrawLines("Doubtful", m.Doubtful);
        }

        static void DrawLines(string title, System.Collections.Generic.List<string> lines)
        {
            if (lines.Count == 0) return;
            EditorGUILayout.LabelField(title + " (" + lines.Count + ")", EditorStyles.boldLabel);
            foreach (string l in lines) EditorGUILayout.HelpBox(l, title == "Warnings" ? MessageType.Warning : MessageType.Info);
        }

        static void DrawTexture(EffectInspectorModel.TextureInfo t)
        {
            EditorGUILayout.BeginHorizontal();
            var rect = GUILayoutUtility.GetRect(ThumbSize, ThumbSize, GUILayout.Width(ThumbSize), GUILayout.Height(ThumbSize));
            if (t.Texture != null) GUI.DrawTexture(rect, t.Texture, ScaleMode.ScaleToFit, true);
            else EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(t.Name ?? "(unnamed)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("key: " + (t.AppliedKey ?? "none recorded"), EditorStyles.miniLabel);
            if (t.MissingOrPlaceholder) EditorGUILayout.LabelField("MISSING / placeholder", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        static void DrawCurve(EffectInspectorModel.CurveInfo c, CultureInfo inv)
        {
            if (!c.Authored) { EditorGUILayout.LabelField(c.Name, "not authored (default)"); return; }
            EditorGUILayout.LabelField(c.Name, "min " + c.Min.ToString("0.###", inv) + "  max " + c.Max.ToString("0.###", inv));
            Rect r = GUILayoutUtility.GetRect(10f, 100000f, PlotHeight, PlotHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f));
            float lo = c.Min, hi = c.Max;
            if (hi - lo < 1e-6f) { lo -= 0.5f; hi += 0.5f; }
            var pts = new Vector3[c.Samples.Length];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new Vector3(r.x + r.width * i / (pts.Length - 1), r.yMax - 2f - (r.height - 4f) * (c.Samples[i] - lo) / (hi - lo), 0f);
            Handles.color = new Color(0.4f, 0.8f, 1f);
            Handles.DrawAAPolyLine(1.5f, pts);
        }
    }
}
