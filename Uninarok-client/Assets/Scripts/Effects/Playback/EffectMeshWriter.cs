using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.Effects
{
    /// <summary>
    /// One dynamic mesh per pooled effect instance (SPEC-0012 FR-3, FR-4, FR-9; plan 4.5 "Mesh", D3, D4, D28).
    /// One submesh per STR layer (in file order) or per authored part; submesh order is draw order (T0.3).
    /// <para>Bind (warm-up, when the instance starts): <see cref="BeginLayout"/>, <see cref="AddQuads"/> /
    /// <see cref="AddCylinder"/> per submesh, <see cref="EndLayout"/>. Index buffer, submesh descriptors and bounds are
    /// written there, and only when the layout differs from the bound one (bounds every time).</para>
    /// <para>Per frame: <see cref="WriteQuad"/>, <see cref="HideQuad"/>, <see cref="HideSubMesh"/>,
    /// <see cref="WriteCylinder"/>, <see cref="SetMaterial"/>, then one <see cref="Upload"/>. No managed allocation after
    /// warm-up; the vertex and index arrays are native and grow only when a larger layout is bound.</para>
    /// Main thread only (Unity mesh and renderer API). <see cref="Dispose"/> frees the native arrays and the mesh.
    /// </summary>
    public sealed class EffectMeshWriter : IDisposable
    {
        /// <summary>Largest vertex count of one mesh: 16-bit indices, 0xFFFF kept free.</summary>
        public const int MaxVertices = 65535;
        public const int QuadVertices = 4, QuadIndices = 6;
        public const int MinSides = 3;

        /// <summary>Vertices of a cylinder strip of <paramref name="sides"/> sides: (sides + 1) × 2 (plan 4.5).</summary>
        public static int CylinderVertices(int sides) => (sides + 1) * 2;
        public static int CylinderIndices(int sides) => sides * 6;

        enum Shape : byte { Quads, Cylinder }

        // Per-frame upload: no validation (indices are written at bind), no bounds (set at bind), no notification.
        const MeshUpdateFlags FrameFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds
                                           | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds;
        // Bind: indices validated (once per layout), bounds still ours.
        const MeshUpdateFlags BindFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers
                                          | MeshUpdateFlags.DontResetBoneBounds;

        static readonly ProfilerMarker WriteMarker = new ProfilerMarker("EffectMeshWriter.Write");

        readonly MeshFilter filter;
        readonly MeshRenderer renderer;
        readonly Mesh mesh;
        readonly VertexAttributeDescriptor[] attributes =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };
        readonly List<Material> materialList = new List<Material>();

        NativeArray<EffectVertex> vertices;
        NativeArray<ushort> indices;

        // Pending layout (between BeginLayout and EndLayout) and bound layout.
        Shape[] pendingShape = new Shape[4], shape = new Shape[4];
        int[] pendingCount = new int[4], count = new int[4];       // quads, or sides for a cylinder
        int pendingSubMeshes, pendingVertices, pendingIndices;
        bool pendingFailed, building;

        int[] firstVertex = new int[4], vertexCount = new int[4];
        Material[] materials = new Material[4];
        int subMeshes, usedVertices, usedIndices;
        bool bound, materialsDirty, disposed;

        public EffectMeshWriter(MeshFilter filter, MeshRenderer renderer)
        {
            this.filter = filter != null ? filter : throw new ArgumentNullException(nameof(filter));
            this.renderer = renderer != null ? renderer : throw new ArgumentNullException(nameof(renderer));
            mesh = new Mesh { name = "EffectMesh", hideFlags = HideFlags.HideAndDontSave };
            filter.sharedMesh = mesh;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public Mesh Mesh => mesh;
        public int SubMeshCount => subMeshes;
        public int VertexCount => usedVertices;
        public int IndexCount => usedIndices;
        /// <summary>How many times the index buffer and submesh descriptors were written (layout changes).</summary>
        public int LayoutUploads { get; private set; }
        /// <summary>How many times <c>Renderer.SetSharedMaterials</c> was called.</summary>
        public int MaterialUploads { get; private set; }
        /// <summary>The native vertex array (the bound vertices first); for tests and procedures.</summary>
        public NativeArray<EffectVertex> Vertices => vertices;

        // ---- bind ----

        public void BeginLayout()
        {
            ThrowIfDisposed();
            building = true;
            pendingSubMeshes = pendingVertices = pendingIndices = 0;
            pendingFailed = false;
        }

        /// <summary>Adds a submesh of <paramref name="quads"/> quads (STR layer: 1; procedure: its MaxQuads). -1 if invalid or over <see cref="MaxVertices"/>.</summary>
        public int AddQuads(int quads = 1)
        {
            if (quads < 1 || quads > MaxVertices / QuadVertices) return Fail();
            return Add(Shape.Quads, quads, quads * QuadVertices, quads * QuadIndices);
        }

        /// <summary>Adds a cylinder strip of <paramref name="sides"/> sides ((sides + 1) × 2 vertices). -1 if sides &lt; 3 or over <see cref="MaxVertices"/>.</summary>
        public int AddCylinder(int sides)
        {
            if (sides < MinSides || sides > MaxVertices / 2 - 1) return Fail();
            return Add(Shape.Cylinder, sides, CylinderVertices(sides), CylinderIndices(sides));
        }

        int Fail()
        {
            if (!building) throw new InvalidOperationException("BeginLayout first");
            pendingFailed = true;
            return -1;
        }

        int Add(Shape s, int n, int v, int i)
        {
            if (!building) throw new InvalidOperationException("BeginLayout first");
            if (pendingFailed) return -1;
            if (v > MaxVertices - pendingVertices) return Fail();
            if (pendingSubMeshes == pendingShape.Length)
            {
                Array.Resize(ref pendingShape, pendingSubMeshes * 2);
                Array.Resize(ref pendingCount, pendingSubMeshes * 2);
            }
            pendingShape[pendingSubMeshes] = s;
            pendingCount[pendingSubMeshes] = n;
            pendingVertices += v;
            pendingIndices += i;
            return pendingSubMeshes++;
        }

        /// <summary>
        /// Binds the pending layout. False if an add failed: the mesh is then empty (draws nothing) and the caller plays the
        /// placeholder. <paramref name="bounds"/> (object space, conservative for camera-facing parts) is set every time;
        /// index buffer and submeshes only when the layout changed. Vertices of the bound range are zeroed (hidden).
        /// </summary>
        public bool EndLayout(Bounds bounds)
        {
            ThrowIfDisposed();
            if (!building) throw new InvalidOperationException("BeginLayout first");
            building = false;
            if (pendingFailed)
            {
                Unbind();
                return false;
            }

            bool same = bound && pendingSubMeshes == subMeshes;
            for (int s = 0; same && s < subMeshes; s++)
                same = pendingShape[s] == shape[s] && pendingCount[s] == count[s];

            if (!same) Commit();
            for (int v = 0; v < usedVertices; v++) vertices[v] = default;    // zero-area: nothing stale from the last play
            if (usedVertices > 0) mesh.SetVertexBufferData(vertices, 0, 0, usedVertices, 0, FrameFlags);
            mesh.bounds = bounds;
            return true;
        }

        void Commit()
        {
            subMeshes = pendingSubMeshes;
            usedVertices = pendingVertices;
            usedIndices = pendingIndices;
            if (shape.Length < subMeshes)
            {
                int n = pendingShape.Length;
                shape = new Shape[n]; count = new int[n]; firstVertex = new int[n]; vertexCount = new int[n];
                Array.Resize(ref materials, n);
            }
            Array.Copy(pendingShape, shape, subMeshes);
            Array.Copy(pendingCount, count, subMeshes);
            Array.Clear(materials, 0, materials.Length);
            materialsDirty = true;
            bound = true;
            LayoutUploads++;

            Grow(ref vertices, usedVertices);
            Grow(ref indices, usedIndices);

            mesh.Clear();
            if (subMeshes == 0) return;
            int vtx = 0, idx = 0;
            for (int s = 0; s < subMeshes; s++)
            {
                firstVertex[s] = vtx;
                if (shape[s] == Shape.Quads) { WriteQuadIndices(indices, idx, vtx, count[s]); vertexCount[s] = count[s] * QuadVertices; idx += count[s] * QuadIndices; }
                else { WriteCylinderIndices(indices, idx, vtx, count[s]); vertexCount[s] = CylinderVertices(count[s]); idx += CylinderIndices(count[s]); }
                vtx += vertexCount[s];
            }

            mesh.SetVertexBufferParams(usedVertices, attributes);
            mesh.SetIndexBufferParams(usedIndices, IndexFormat.UInt16);
            mesh.SetIndexBufferData(indices, 0, 0, usedIndices, BindFlags);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);   // Clear() leaves the bounds invalid; subMeshCount converts them (EndLayout sets the real ones)
            mesh.subMeshCount = subMeshes;
            idx = 0;
            for (int s = 0; s < subMeshes; s++)
            {
                int ic = shape[s] == Shape.Quads ? count[s] * QuadIndices : CylinderIndices(count[s]);
                mesh.SetSubMesh(s, new SubMeshDescriptor(idx, ic, MeshTopology.Triangles)
                {
                    firstVertex = firstVertex[s],
                    vertexCount = vertexCount[s],
                }, BindFlags);
                idx += ic;
            }
        }

        void Unbind()
        {
            subMeshes = usedVertices = usedIndices = 0;
            bound = false;
            Array.Clear(materials, 0, materials.Length);
            materialsDirty = true;
            mesh.Clear();
        }

        static void Grow<T>(ref NativeArray<T> array, int size) where T : struct
        {
            if (array.IsCreated && array.Length >= size) return;
            if (array.IsCreated) array.Dispose();
            array = new NativeArray<T>(Math.Max(size, 4), Allocator.Persistent);
        }

        /// <summary>Quad q at vertex v + 4q: two triangles (0, 1, 2), (0, 2, 3) over corners in perimeter order (STR: TL, TR, BR, BL).</summary>
        static void WriteQuadIndices(NativeArray<ushort> ib, int at, int v, int quads)
        {
            for (int q = 0; q < quads; q++, v += QuadVertices, at += QuadIndices)
            {
                ib[at] = (ushort)v; ib[at + 1] = (ushort)(v + 1); ib[at + 2] = (ushort)(v + 2);
                ib[at + 3] = (ushort)v; ib[at + 4] = (ushort)(v + 2); ib[at + 5] = (ushort)(v + 3);
            }
        }

        /// <summary>Side i of a strip with vertex 2i bottom and 2i + 1 top: (b_i, t_i, t_i+1), (b_i, t_i+1, b_i+1).</summary>
        static void WriteCylinderIndices(NativeArray<ushort> ib, int at, int v, int sides)
        {
            for (int i = 0; i < sides; i++, at += 6)
            {
                int b0 = v + 2 * i, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                ib[at] = (ushort)b0; ib[at + 1] = (ushort)t0; ib[at + 2] = (ushort)t1;
                ib[at + 3] = (ushort)b0; ib[at + 4] = (ushort)t1; ib[at + 5] = (ushort)b1;
            }
        }

        // ---- per frame ----

        /// <summary>Writes quad <paramref name="quad"/> of a quads submesh; corners in perimeter order, object space.</summary>
        public void WriteQuad(int submesh, int quad, in Vector3 c0, in Vector3 c1, in Vector3 c2, in Vector3 c3,
            in Vector2 uv0, in Vector2 uv1, in Vector2 uv2, in Vector2 uv3, Color32 color)
        {
            int v = QuadStart(submesh, quad);
            vertices[v] = new EffectVertex(c0, color, uv0);
            vertices[v + 1] = new EffectVertex(c1, color, uv1);
            vertices[v + 2] = new EffectVertex(c2, color, uv2);
            vertices[v + 3] = new EffectVertex(c3, color, uv3);
        }

        /// <summary>A hidden layer or quad: four equal vertices (zero area), alpha 0; the submesh's material stays.</summary>
        public void HideQuad(int submesh, int quad)
        {
            int v = QuadStart(submesh, quad);
            for (int i = 0; i < QuadVertices; i++) vertices[v + i] = default;
        }

        /// <summary>Every vertex of the submesh zero-area (any shape).</summary>
        public void HideSubMesh(int submesh)
        {
            CheckSubMesh(submesh);
            int v = firstVertex[submesh], end = v + vertexCount[submesh];
            for (; v < end; v++) vertices[v] = default;
        }

        /// <summary>Every vertex of the bound layout zero-area and uploaded now: nothing of the last frame stays drawn. The layout,
        /// the materials and the index buffer stay, so the next bind of the same layout uploads none of them (D40).</summary>
        public void HideAll()
        {
            if (disposed || usedVertices == 0) return;
            for (int v = 0; v < usedVertices; v++) vertices[v] = default;
            mesh.SetVertexBufferData(vertices, 0, 0, usedVertices, 0, FrameFlags);
        }

        /// <summary>
        /// Writes a cylinder strip from <paramref name="c"/> (units, object space, plan 4.5): vertex 2i is bottom ring
        /// point i (y 0, radius Bottom), 2i + 1 the top point i (y Height, radius Top), i = 0..Sides (the last repeats
        /// the first position with u = RepeatU); point i at angle 2πi / Sides − AngleDegrees, so a positive angle turns
        /// the strip like <c>Quaternion.Euler(0, angle, 0)</c>; u = i / Sides × <paramref name="repeatU"/>, v 1 at the
        /// bottom and 0 at the top.
        /// </summary>
        public void WriteCylinder(int submesh, in CylinderSample c, float repeatU)
        {
            CheckSubMesh(submesh);
            if (shape[submesh] != Shape.Cylinder) throw new ArgumentException("not a cylinder submesh", nameof(submesh));
            int sides = count[submesh], v = firstVertex[submesh];
            Color32 color = c.Color;
            float step = 2f * Mathf.PI / sides, start = -c.AngleDegrees * Mathf.Deg2Rad;
            for (int i = 0; i <= sides; i++, v += 2)
            {
                float a = start + i * step, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                float u = (float)i / sides * repeatU;
                vertices[v] = new EffectVertex(new Vector3(cos * c.Bottom, 0f, sin * c.Bottom), color, new Vector2(u, 1f));
                vertices[v + 1] = new EffectVertex(new Vector3(cos * c.Top, c.Height, sin * c.Top), color, new Vector2(u, 0f));
            }
        }

        /// <summary>Sets a submesh's material; the renderer is updated at <see cref="Upload"/> only if some material changed.
        /// <see cref="EffectMaterials"/> gives one instance per key, so a reference change is a key change.</summary>
        public void SetMaterial(int submesh, Material material)
        {
            CheckSubMesh(submesh);
            if (ReferenceEquals(materials[submesh], material)) return;
            materials[submesh] = material;
            materialsDirty = true;
        }

        /// <summary>One vertex upload of the bound range; materials only when changed.</summary>
        public void Upload()
        {
            ThrowIfDisposed();
            using (WriteMarker.Auto())
            {
                if (usedVertices > 0) mesh.SetVertexBufferData(vertices, 0, 0, usedVertices, 0, FrameFlags);
                if (!materialsDirty) return;
                materialsDirty = false;
                materialList.Clear();
                for (int s = 0; s < subMeshes; s++) materialList.Add(materials[s]);
                renderer.SetSharedMaterials(materialList);
                MaterialUploads++;
            }
        }

        int QuadStart(int submesh, int quad)
        {
            CheckSubMesh(submesh);
            if (shape[submesh] != Shape.Quads) throw new ArgumentException("not a quads submesh", nameof(submesh));
            if ((uint)quad >= (uint)count[submesh]) throw new ArgumentOutOfRangeException(nameof(quad));
            return firstVertex[submesh] + quad * QuadVertices;
        }

        void CheckSubMesh(int submesh)
        {
            if ((uint)submesh >= (uint)subMeshes) throw new ArgumentOutOfRangeException(nameof(submesh));
        }

        void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(EffectMeshWriter));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (vertices.IsCreated) vertices.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = null;
            if (renderer != null) { materialList.Clear(); renderer.SetSharedMaterials(materialList); }
            if (mesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
