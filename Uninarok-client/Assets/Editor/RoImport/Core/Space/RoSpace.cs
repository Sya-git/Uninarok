using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Space
{
    /// <summary>Ragnarok to Unity coordinate change (plan 4.3). Contract only: implemented by T1.5.</summary>
    public static class RoSpace
    {
        public static readonly Matrix4x4 Mirror = Matrix4x4.zero;

        public static Vector3 Point(Vector3 ro) => throw new NotImplementedException("T1.5");
        public static Vector3 Direction(Vector3 ro) => throw new NotImplementedException("T1.5");
        public static Matrix4x4 Matrix(Matrix4x4 roLocal) => throw new NotImplementedException("T1.5");
        public static Quaternion Rotation(Matrix4x4 roRotation) => throw new NotImplementedException("T1.5");
        public static Vector3 Scale(Vector3 roScale) => throw new NotImplementedException("T1.5");
        public static void ReverseWinding(int[] triangleIndices) => throw new NotImplementedException("T1.5");
    }
}
