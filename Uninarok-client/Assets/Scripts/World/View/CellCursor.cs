using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.Map;

namespace Uninarok.World.View
{
    /// <summary>
    /// Marks the cell under the mouse on its slope, in a colour that says whether a walk there is possible (SPEC-0010 FR-11;
    /// PLAN-0010 4.11). One quad mesh, rewritten when the cell changes; no allocation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CellCursor : MonoBehaviour
    {
        /// <summary>Above the ground, so the two do not fight for depth.</summary>
        const float Lift = 0.01f;

        /// <summary><c>cell_cursor.mat</c>: transparent, vertex colours.</summary>
        public Material Material;
        public Color CanWalkColour = new Color(0.3f, 1f, 0.4f, 0.45f);
        public Color CanNotWalkColour = new Color(1f, 0.25f, 0.2f, 0.45f);

        readonly Vector3[] _vertices = new Vector3[4];
        readonly Color32[] _colours = new Color32[4];
        // The two triangles of the cell, seen from above, by the map's row sign and diagonal.
        readonly int[] _triangles = new int[6];
        MapData _map;
        Mesh _mesh;
        MeshRenderer _renderer;
        int _x = -1, _y = -1;
        bool _canWalk;

        public bool Visible => _renderer != null && _renderer.enabled;
        public int CellX => _x;
        public int CellY => _y;
        public bool CanWalk => _canWalk;
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

        public void Show(int x, int y, bool canWalk)
        {
            if (_map == null || !_map.InBounds(x, y))
            {
                Hide();
                return;
            }
            Ensure();
            if (x != _x || y != _y) WriteCell(x, y);
            if (canWalk != _canWalk || !_renderer.enabled) WriteColour(canWalk);
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

        void WriteColour(bool canWalk)
        {
            _canWalk = canWalk;
            Color32 colour = canWalk ? CanWalkColour : CanNotWalkColour;
            for (int i = 0; i < 4; i++) _colours[i] = colour;
            _mesh.SetColors(_colours);
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
            WriteVerticesOnce();
        }

        // The colours need as many vertices as the mesh has.
        void WriteVerticesOnce() => _mesh.SetVertices(_vertices);

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
