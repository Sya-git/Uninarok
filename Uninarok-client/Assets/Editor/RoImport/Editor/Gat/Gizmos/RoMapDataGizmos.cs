using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Editor.Gat.Gizmos
{
    /// <summary>
    /// The map data gizmos (PLAN-0005 4.6, FR-7, D6): when a <see cref="MapRoot"/>, one of its ancestors or one of its
    /// children is selected, draws its <see cref="MapData"/> cells coloured by type, the cell grid and corner height labels,
    /// limited by <see cref="RoMapDataGizmoSelector"/> to a radius around the scene view's focus (or the picked region) and
    /// a cap. Nothing runs when no map is selected. Reads the asset only, never the source.
    /// </summary>
    public static class RoMapDataGizmos
    {
        /// <summary>Lift above the surface so the cells do not fight with the ground, in Unity units.</summary>
        const float Lift = 0.02f;

        /// <summary>Corner height labels are drawn for at most this many cells (the nearest).</summary>
        const int MaxLabelledCells = 64;

        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MapRoot))]
        static void Draw(MapRoot root, GizmoType gizmoType)
        {
            var data = root.Data;
            if (data == null || data.CellsX <= 0 || Event.current == null || Event.current.type != EventType.Repaint) return;

            var cells = new List<int>(1024);
            var matrix = root.transform.localToWorldMatrix;
            SelectCells(root, data, cells);
            if (cells.Count == 0) return;

            var material = ColouredMaterial();
            try
            {
                if (RoMapDataGizmoSettings.ShowCells) DrawCells(data, cells, matrix, material);
                if (RoMapDataGizmoSettings.ShowGrid) DrawGrid(data, cells, matrix, material);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
            if (RoMapDataGizmoSettings.ShowHeights) DrawHeights(data, cells, matrix);
        }

        /// <summary>
        /// A vertex-coloured, alpha-blended, depth-tested material for GL drawing, made per draw and destroyed after it, so
        /// no material is kept in a static field.
        /// </summary>
        static Material ColouredMaterial()
        {
            var material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            return material;
        }

        /// <summary>The cells to draw: the picked region, or the radius around the scene view's pivot.</summary>
        internal static bool SelectCells(MapRoot root, MapData data, List<int> cells)
        {
            var focus = FocusCell(root, data);
            RectInt region;
            if (RoMapDataGizmoSettings.UseRegion && RoMapDataGizmoSettings.Region.width > 0 && RoMapDataGizmoSettings.Region.height > 0)
            {
                region = RoMapDataGizmoSettings.Region;
                focus = new Vector2Int(region.x + region.width / 2, region.y + region.height / 2);
            }
            else
            {
                region = RoMapDataGizmoSelector.RadiusRegion(data, focus, RoMapDataGizmoSettings.Radius);
            }
            return RoMapDataGizmoSelector.Select(data, region, focus, RoMapDataGizmoSettings.VisibleGroups, RoMapDataGizmoSettings.Cap, cells);
        }

        /// <summary>The cell under the scene view's pivot (clamped to the map), in the map root's space.</summary>
        static Vector2Int FocusCell(MapRoot root, MapData data)
        {
            var view = SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
            Vector3 pivot = view != null ? view.pivot : root.transform.position;
            return CellOf(data, root.transform.InverseTransformPoint(pivot), clamp: true);
        }

        /// <summary>The cell of a local position; clamped into the map when <paramref name="clamp"/>.</summary>
        internal static Vector2Int CellOf(MapData data, Vector3 local, bool clamp)
        {
            float fx = (local.x - data.Origin.x) / data.CellSize;
            float fz = (local.z - data.Origin.y) * data.RowSign / data.CellSize;
            var cell = new Vector2Int(Mathf.FloorToInt(fx), Mathf.FloorToInt(fz));
            if (clamp) cell = new Vector2Int(Mathf.Clamp(cell.x, 0, data.CellsX - 1), Mathf.Clamp(cell.y, 0, data.CellsZ - 1));
            return cell;
        }

        /// <summary>The four corners of a cell in local space: 00, 10, 11, 01 (a quad's order).</summary>
        static void Corners(MapData data, int x, int z, Vector3[] quad)
        {
            var h = data.CornerHeights(x, z);
            float s = data.CellSize, rs = data.RowSign;
            float x0 = data.Origin.x + x * s, x1 = x0 + s;
            float z0 = data.Origin.y + z * s * rs, z1 = z0 + s * rs;
            quad[0] = new Vector3(x0, h.x + Lift, z0);
            quad[1] = new Vector3(x1, h.y + Lift, z0);
            quad[2] = new Vector3(x1, h.w + Lift, z1);
            quad[3] = new Vector3(x0, h.z + Lift, z1);
        }

        static void DrawCells(MapData data, List<int> cells, Matrix4x4 matrix, Material material)
        {
            var quad = new Vector3[4];
            material.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(matrix);
            GL.Begin(GL.TRIANGLES);
            foreach (int i in cells)
            {
                int x = i % data.CellsX, z = i / data.CellsX;
                var c = (Color)RoGatImage.ColorOf(data.TypeAt(x, z));
                c.a = 0.55f;
                GL.Color(c);
                Corners(data, x, z, quad);
                Tri(quad[0], quad[1], quad[2]);   // culling is off: one winding shows from both sides
                Tri(quad[0], quad[2], quad[3]);
            }
            GL.End();
            GL.PopMatrix();
        }

        static void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            GL.Vertex(a);
            GL.Vertex(b);
            GL.Vertex(c);
        }

        static void DrawGrid(MapData data, List<int> cells, Matrix4x4 matrix, Material material)
        {
            var quad = new Vector3[4];
            material.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(matrix);
            GL.Begin(GL.LINES);
            GL.Color(new Color(0f, 0f, 0f, 0.6f));
            foreach (int i in cells)
            {
                Corners(data, i % data.CellsX, i / data.CellsX, quad);
                for (int k = 0; k < 4; k++)
                {
                    GL.Vertex(quad[k]);
                    GL.Vertex(quad[(k + 1) % 4]);
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        static void DrawHeights(MapData data, List<int> cells, Matrix4x4 matrix)
        {
            var quad = new Vector3[4];
            int count = Mathf.Min(cells.Count, MaxLabelledCells);
            for (int n = 0; n < count; n++)
            {
                int i = cells[n], x = i % data.CellsX, z = i / data.CellsX;
                Corners(data, x, z, quad);
                var h = data.CornerHeights(x, z);
                // the corner H00 of every cell: together they label every corner of the drawn area
                Handles.Label(matrix.MultiplyPoint3x4(quad[0]), h.x.ToString("0.##", CultureInfo.InvariantCulture), EditorStyles.miniLabel);
            }
        }
    }
}
