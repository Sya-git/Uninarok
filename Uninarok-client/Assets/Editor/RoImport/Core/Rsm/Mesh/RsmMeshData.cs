using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>One node's mesh in Unity space, node-local (PLAN-0003 4.6). Plain arrays, no Unity objects.</summary>
    public sealed class RsmMeshData
    {
        public Vector3[] Positions = Array.Empty<Vector3>();
        public Vector3[] Normals = Array.Empty<Vector3>();
        public Vector2[] Uvs = Array.Empty<Vector2>();
        /// <summary>Sorted by (texture index, two-sided), ascending.</summary>
        public IReadOnlyList<RsmSubmesh> Submeshes = Array.Empty<RsmSubmesh>();

        /// <summary>More than 65,535 vertices need 32-bit indices (4.6 step 4).</summary>
        public bool Needs32BitIndices => Positions.Length > 65535;

        public int TriangleCount
        {
            get
            {
                int count = 0;
                foreach (var s in Submeshes) count += s.Triangles.Length / 3;
                return count;
            }
        }
    }

    /// <summary>The faces of one (texture, two-sided) pair (PLAN-0003 4.6 "Submeshes").</summary>
    public sealed class RsmSubmesh
    {
        /// <summary>Index into <see cref="RoRsm.Textures"/>.</summary>
        public int TextureIndex;
        public bool TwoSided;
        public int[] Triangles = Array.Empty<int>();

        /// <summary>The shared material's key for this submesh (PLAN-0003 4.3).</summary>
        public RoMaterialKey MaterialKey(RoPath texture, RoSurface surface) => new RoMaterialKey(texture, surface, TwoSided);
    }
}
