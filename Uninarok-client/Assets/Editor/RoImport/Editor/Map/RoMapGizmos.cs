using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Editor.Map
{
    /// <summary>
    /// Gizmos of a selected map (PLAN-0006 4.9, SPEC-0006 FR-13): the map rectangle from <see cref="MapData.Settings"/>,
    /// the origin (grid corner 0, 0), the labelled axes (X along columns, up, Z along rows) and the centre that RSW
    /// positions are relative to. Drawn only while the map object or something under it is selected, so an unselected
    /// map costs nothing; it reads only the map data already in the project. Editor only.
    /// </summary>
    public static class RoMapGizmos
    {
        static readonly Color Rectangle = new Color(1f, 0.85f, 0.2f, 1f);

        /// <summary>Profiler marker of one draw, so the Profiler shows the gizmo's cost (and none when nothing is selected).</summary>
        internal static readonly ProfilerMarker DrawMarker = new ProfilerMarker("RoMapGizmos.Draw");

        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MapRoot))]
        static void Draw(MapRoot root, GizmoType gizmoType)
        {
            using (DrawMarker.Auto()) DrawMap(root);
        }

        static void DrawMap(MapRoot root)
        {
            var data = root.Data;
            if (data == null || !data.HasSettings) return;
            var s = data.Settings;
            if (s.WidthCubes <= 0 || s.HeightCubes <= 0) return;

            var space = new RoMapSpace(s.WidthCubes, s.HeightCubes, s.Zoom);
            var m = root.transform.localToWorldMatrix;
            Vector3 P(Vector3 local) => m.MultiplyPoint3x4(local);
            Vector3 origin = space.CornerUnity(0, 0, 0f);
            Vector3 east = space.CornerUnity(s.WidthCubes, 0, 0f);
            Vector3 north = space.CornerUnity(0, s.HeightCubes, 0f);
            Vector3 far = space.CornerUnity(s.WidthCubes, s.HeightCubes, 0f);
            Vector3 centre = (origin + far) / 2f;
            float axis = Mathf.Max(2f, Mathf.Min(far.x, far.z) * 0.1f);

            using (new Handles.DrawingScope(Rectangle))
            {
                Handles.DrawAAPolyLine(3f, P(origin), P(east), P(far), P(north), P(origin));
                Handles.Label(P(far), $"{data.MapName}  {s.WidthCubes} x {s.HeightCubes} cubes", EditorStyles.boldLabel);
            }

            DrawAxis(P(origin), P(origin + Vector3.right * axis), Handles.xAxisColor, "X (columns)");
            DrawAxis(P(origin), P(origin + Vector3.up * axis), Handles.yAxisColor, "up");
            DrawAxis(P(origin), P(origin + Vector3.forward * axis), Handles.zAxisColor, "Z (rows, north)");
            Handles.Label(P(origin), "origin (0, 0)", EditorStyles.miniBoldLabel);

            using (new Handles.DrawingScope(Rectangle))
            {
                float size = axis * 0.25f;
                Handles.DrawLine(P(centre - Vector3.right * size), P(centre + Vector3.right * size));
                Handles.DrawLine(P(centre - Vector3.forward * size), P(centre + Vector3.forward * size));
                Handles.Label(P(centre), "centre (RSW 0, 0)", EditorStyles.miniLabel);
            }
        }

        static void DrawAxis(Vector3 from, Vector3 to, Color color, string label)
        {
            using (new Handles.DrawingScope(color))
            {
                Handles.DrawAAPolyLine(4f, from, to);
                Handles.Label(to, label, EditorStyles.miniBoldLabel);
            }
        }
    }
}
