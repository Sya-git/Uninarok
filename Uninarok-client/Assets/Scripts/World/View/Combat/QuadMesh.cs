using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.World.View
{
    /// <summary>
    /// One dynamic mesh of up to a fixed number of quads (PLAN-0011 4.8, D6; SPEC-0011 FR-6, FR-7, FR-12): vertices in persistent
    /// native arrays, written each frame with <see cref="Mesh.SetVertexBufferData{T}(NativeArray{T}, int, int, int, int, MeshUpdateFlags)"/>
    /// and one sub-mesh, with validation and bounds recalculation off (fixed large bounds). The index buffer is written once. Nothing
    /// allocates after construction. Main thread.
    /// </summary>
    sealed class QuadMesh : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 Position;
            public Color32 Colour;
            public Vector2 Uv;
        }

        const MeshUpdateFlags Quiet = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds |
                                      MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers;

        readonly int _capacity;
        NativeArray<Vertex> _vertices;
        int _count;
        bool _warnedFull;

        public QuadMesh(string name, int capacity)
        {
            if (capacity <= 0 || capacity > 16383) throw new ArgumentOutOfRangeException(nameof(capacity)); // 16-bit indices
            _capacity = capacity;
            _vertices = new NativeArray<Vertex>(capacity * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            Mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            Mesh.MarkDynamic();
            Mesh.SetVertexBufferParams(capacity * 4,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            Mesh.SetIndexBufferParams(capacity * 6, IndexFormat.UInt16);
            var indices = new NativeArray<ushort>(capacity * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int q = 0; q < capacity; q++)
            {
                int v = q * 4, i = q * 6;
                indices[i] = (ushort)v;
                indices[i + 1] = (ushort)(v + 1);
                indices[i + 2] = (ushort)(v + 2);
                indices[i + 3] = (ushort)v;
                indices[i + 4] = (ushort)(v + 2);
                indices[i + 5] = (ushort)(v + 3);
            }
            Mesh.SetIndexBufferData(indices, 0, 0, indices.Length, Quiet);
            indices.Dispose();
            Mesh.subMeshCount = 1;
            Mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));
            Mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), Quiet);
        }

        public Mesh Mesh { get; private set; }

        public int Capacity => _capacity;

        /// <summary>Quads written since <see cref="Begin"/>.</summary>
        public int Count => _count;

        public void Begin() => _count = 0;

        /// <summary>One quad, corners in order around it; false when the mesh is full (one warning).</summary>
        public bool Quad(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Rect uv, Color32 colour)
        {
            if (_count >= _capacity)
            {
                if (!_warnedFull)
                {
                    _warnedFull = true;
                    Debug.LogWarning("[Combat] overlay mesh " + Mesh.name + " is full: quads are dropped");
                }
                return false;
            }
            int v = _count * 4;
            _vertices[v] = new Vertex { Position = a, Colour = colour, Uv = new Vector2(uv.xMin, uv.yMin) };
            _vertices[v + 1] = new Vertex { Position = b, Colour = colour, Uv = new Vector2(uv.xMax, uv.yMin) };
            _vertices[v + 2] = new Vertex { Position = c, Colour = colour, Uv = new Vector2(uv.xMax, uv.yMax) };
            _vertices[v + 3] = new Vertex { Position = d, Colour = colour, Uv = new Vector2(uv.xMin, uv.yMax) };
            _count++;
            return true;
        }

        /// <summary>Uploads the quads written; the mesh draws only those.</summary>
        public void End()
        {
            if (Mesh == null) return;
            if (_count > 0) Mesh.SetVertexBufferData(_vertices, 0, 0, _count * 4, 0, Quiet);
            Mesh.SetSubMesh(0, new SubMeshDescriptor(0, _count * 6) { bounds = Mesh.bounds, vertexCount = _count * 4 }, Quiet);
        }

        public void Dispose()
        {
            if (_vertices.IsCreated) _vertices.Dispose();
            if (Mesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Mesh);
                else UnityEngine.Object.DestroyImmediate(Mesh);
            }
            Mesh = null;
        }
    }
}
