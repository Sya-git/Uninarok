using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Editor.Gnd
{
    /// <summary>
    /// The in-memory ground prefab (PLAN-0004 4.6): a root with no transform of its own and one <c>Chunk_&lt;cx&gt;_&lt;cz&gt;</c>
    /// child per chunk at its map-space origin, with the chunk mesh, its materials in submesh order, real-time shadows
    /// cast and received, light probes off, and the static flags for lightmap contribution and batching (D15). The caller
    /// saves and destroys the result.
    /// </summary>
    public static class RoGndPrefabBuilder
    {
        const StaticEditorFlags ChunkFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic;

        public static string ChunkName(int chunkColumn, int chunkRow) => $"Chunk_{chunkColumn}_{chunkRow}";

        /// <param name="meshes">The chunk mesh assets, in the order of <paramref name="chunks"/>.</param>
        /// <param name="materials">Each chunk's materials in submesh order.</param>
        public static GameObject Build(string name, IReadOnlyList<RoGndMeshData> chunks, IReadOnlyList<Mesh> meshes,
                                       IReadOnlyList<Material[]> materials)
        {
            var root = new GameObject(name);
            for (int i = 0; i < chunks.Count; i++)
            {
                var data = chunks[i];
                var go = new GameObject(ChunkName(data.ChunkColumn, data.ChunkRow));
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = data.Origin;
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials[i];
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                GameObjectUtility.SetStaticEditorFlags(go, ChunkFlags);
            }
            return root;
        }

        /// <summary>A Unity mesh of a chunk's mesh data (not yet an asset); 16-bit indices (PLAN-0004 4.4).</summary>
        public static Mesh ToMesh(string name, RoGndMeshData data)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(data.Positions);
            mesh.SetNormals(data.Normals);
            mesh.SetUVs(0, data.Uvs);
            mesh.subMeshCount = data.Submeshes.Count;
            for (int s = 0; s < data.Submeshes.Count; s++)
                mesh.SetTriangles(data.Submeshes[s].Triangles, s, calculateBounds: false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
