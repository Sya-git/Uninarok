using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// One node of an RSM model with the file's values, in Ragnarok space, unconverted (plan 4.1). No derived values.
    /// </summary>
    public sealed class RoRsmNode
    {
        /// <summary>The node's name.</summary>
        public string Name = string.Empty;
        /// <summary>The parent's name; <c>null</c> or empty for a root.</summary>
        public string ParentName = string.Empty;
        /// <summary>Node-local slot to index in <see cref="RoRsm.Textures"/>.</summary>
        public int[] TextureSlots = Array.Empty<int>();
        /// <summary>
        /// The 3x3 offset matrix, nine floats in file order. Each group of three is a <b>column</b> (plan 4.4, D9).
        /// </summary>
        public float[] OffsetMatrix = { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
        /// <summary>The file's per-node offset vector (applied to this node's vertices only, plan 4.4).</summary>
        public Vector3 Offset;
        public Vector3 Position;
        /// <summary>Radians.</summary>
        public float RotationAngle;
        public Vector3 RotationAxis;
        public Vector3 Scale = Vector3.one;
        public Vector3[] Vertices = Array.Empty<Vector3>();
        public RoRsmTexCoord[] TexCoords = Array.Empty<RoRsmTexCoord>();
        public RoRsmFace[] Faces = Array.Empty<RoRsmFace>();
        public RoRsmPosKey[] PosKeys = Array.Empty<RoRsmPosKey>();
        public RoRsmRotKey[] RotKeys = Array.Empty<RoRsmRotKey>();
        public RoRsmScaleKey[] ScaleKeys = Array.Empty<RoRsmScaleKey>();

        /// <summary>True when the node has position, rotation or scale keys.</summary>
        public bool IsAnimated => PosKeys.Length > 0 || RotKeys.Length > 0 || ScaleKeys.Length > 0;

        public override string ToString() => $"RoRsmNode '{Name}' (parent '{ParentName}')";
    }
}
