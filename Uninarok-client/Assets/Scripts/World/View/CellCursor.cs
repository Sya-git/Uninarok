using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.Map;

namespace Uninarok.World.View
{
    /// <summary>
    /// Marks the walkable cell under the mouse on its slope with the client's <c>grid.tga</c> (SPEC-0010 FR-11; PLAN-0010 4.11,
    /// D29). One quad mesh, rewritten when the cell changes; no allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CellCursor : MonoBehaviour
    {
        /// <summary>Above the ground, so the two do not fight for depth.</summary>
        const float Lift = 0.01f;

        /// <summary><c>cell_cursor.mat</c>: the grid texture and its colour.</summary>
        public Material Material;

        readonly Vector3[] _vertices = new Vector3[4];
        // The two triangles of the cell, seen from above, by the map's row sign and diagonal.
        readonly int[] _triangles = new int[6];
        MapData _map;
        Mesh _mesh;
        MeshRenderer _renderer;
        int _x = -1, _y = -1;

        public bool Visible => _renderer != null && _renderer.enabled;
        public int CellX => _x;
        public int CellY => _y;
        internal Mesh Mesh => _mesh;

        /// <summary>The map whose cells are marked; null hides the cursor.</summary>
        public MapData Map
        {
            get => _map;
            set
            {
                if (_map == value) return;
                Hide();
                _map = value;
            }
        }

        public void Show(int x, int y)
        {
            if (_map == null || !_map.InBounds(x, y))
            {
                Hide();
                return;
            }
            Ensure();
            if (x != _x || y != _y) WriteCell(x, y);
            _renderer.enabled = true;
        }

        public void Hide()
        {
            _x = _y = -1;
            if (_renderer != null) _renderer.enabled = false;
        }

        void WriteCell(int x, int y)
        {
            _x = x;
            _y = y;
            var h = _map.CornerHeights(x, y);
            float size = _map.CellSize, x0 = _map.Origin.x + x * size, z0 = _map.Origin.y + y * size * _map.RowSign, dz = size * _map.RowSign;
            _vertices[0] = new Vector3(x0, h.x + Lift, z0);
            _vertices[1] = new Vector3(x0 + size, h.y + Lift, z0);
            _vertices[2] = new Vector3(x0, h.z + Lift, z0 + dz);
            _vertices[3] = new Vector3(x0 + size, h.w + Lift, z0 + dz);

            bool up = _map.RowSign > 0f;
            if (_map.DiagonalFlip) Triangles(up, 0, 3, 1, 0, 2, 3);
            else Triangles(up, 0, 2, 1, 1, 2, 3);
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0, false);
            _mesh.RecalculateBounds();
        }

        // Clockwise from above (Unity's front) when rows grow to +z; mirrored otherwise.
        void Triangles(bool up, int a, int b, int c, int d, int e, int f)
        {
            _triangles[0] = a;
            _triangles[1] = up ? b : c;
            _triangles[2] = up ? c : b;
            _triangles[3] = d;
            _triangles[4] = up ? e : f;
            _triangles[5] = up ? f : e;
        }

        void Ensure()
        {
            if (_mesh != null) return;
            _mesh = new Mesh { name = "Cell Cursor" };
            _mesh.MarkDynamic();
            var go = new GameObject("Cell Cursor");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // vertices are world positions
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = Material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingOrder = -2; // under the units' shadows (-1) and the units
            _renderer.enabled = false;
            _x = _y = -1;
            _mesh.SetVertices(_vertices); // the corners need as many vertices as the mesh has
            _mesh.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
