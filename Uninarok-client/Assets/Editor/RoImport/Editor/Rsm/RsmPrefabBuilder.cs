using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Editor.Rsm
{
    /// <summary>
    /// The in-memory prefab of a model (PLAN-0003 4.7): a root named after the model with no transform of its own, one
    /// child per node created in tree order, parented by the tree, named by the unique names, with the rest transform of
    /// 4.5. Nodes with faces get a <see cref="MeshFilter"/> and a <see cref="MeshRenderer"/>; nothing else is added
    /// here (the animator is added by the converter for animated models). The caller saves and destroys the result.
    /// </summary>
    public static class RsmPrefabBuilder
    {
        /// <param name="meshes">The node's mesh asset, or null for a node without faces.</param>
        /// <param name="materials">The node's materials in submesh order.</param>
        public static GameObject Build(string modelName, RsmTree tree, RsmRestPose rest,
                                       IReadOnlyList<Mesh> meshes, IReadOnlyList<Material[]> materials)
        {
            var root = new GameObject(modelName);
            var objects = new Transform[tree.Parent.Length];
            foreach (int n in tree.Order)
            {
                var go = new GameObject(tree.UniqueNames[n]);
                var t = go.transform;
                t.SetParent(tree.Parent[n] < 0 ? root.transform : objects[tree.Parent[n]], false);
                t.localPosition = RsmSpace.LocalPosition(rest.Local[n]);
                t.localRotation = RsmSpace.LocalRotation(rest.Local[n]);
                t.localScale = RsmSpace.LocalScale(rest.Local[n]);
                objects[n] = t;

                if (meshes[n] == null) continue;
                go.AddComponent<MeshFilter>().sharedMesh = meshes[n];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials[n];
                renderer.lightProbeUsage = LightProbeUsage.Off;   // shadows stay at Unity's defaults (spec non-goal)
            }
            return root;
        }

        /// <summary>A Unity mesh of a node's mesh data, named after the node (not yet an asset).</summary>
        public static Mesh ToMesh(string name, RsmMeshData data)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = data.Needs32BitIndices ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
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
