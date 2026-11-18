using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// The mesh of one ground chunk as plain arrays (PLAN-0004 4.4): Unity space, positions relative to
    /// <see cref="Origin"/>, one submesh per texture used in the chunk in ascending texture order. There is no second UV
    /// set (PLAN-0004 D6).
    /// </summary>
    public sealed class RoGndMeshData
    {
        public int ChunkColumn, ChunkRow;
        public Vector3[] Positions = Array.Empty<Vector3>();
        public Vector3[] Normals = Array.Empty<Vector3>();
        public Vector2[] Uvs = Array.Empty<Vector2>();
        public IReadOnlyList<RoGndSubmesh> Submeshes = Array.Empty<RoGndSubmesh>();
        /// <summary>The chunk object's position in Unity space.</summary>
        public Vector3 Origin;
    }

    /// <summary>The triangles of one texture in a chunk (PLAN-0004 4.4).</summary>
    public sealed class RoGndSubmesh
    {
        public int Texture;
        public int[] Triangles = Array.Empty<int>();
    }
}
