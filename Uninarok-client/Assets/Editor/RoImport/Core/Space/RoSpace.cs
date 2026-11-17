using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Space
{
    /// <summary>
    /// Ragnarok to Unity coordinate change (plan 4.3, D6, SPEC-0002 FR-7). Ragnarok's Y axis points down and Unity's up,
    /// so the change of basis is the reflection F = diag(1, -1, 1).
    /// <list type="bullet">
    /// <item>Points and directions are vectors: <c>F*v</c>; points are also scaled by <see cref="RoUnits.Scale"/>.</item>
    /// <item>Rotations and local matrices are conjugated, <c>F*M*F</c>, so a rotation stays a proper rotation
    /// (determinant +1): no Euler angle is negated and no axis swapped by hand.</item>
    /// <item>A reflection reverses triangle orientation: mesh builders call <see cref="ReverseWinding"/> once per mesh,
    /// and no other code reverses winding.</item>
    /// </list>
    /// Pure: no AssetDatabase, no scene.
    /// </summary>
    public static class RoSpace
    {
        const float FlipY = -1f;

        /// <summary>The reflection F = diag(1, -1, 1, 1).</summary>
        public static readonly Matrix4x4 Mirror = new Matrix4x4(
            new Vector4(1f, 0f, 0f, 0f),
            new Vector4(0f, FlipY, 0f, 0f),
            new Vector4(0f, 0f, 1f, 0f),
            new Vector4(0f, 0f, 0f, 1f));

        /// <summary>A position: <c>F*p*Scale</c>.</summary>
        public static Vector3 Point(Vector3 ro) => Direction(ro) * RoUnits.Scale;

        /// <summary>A direction, normal or axis: <c>F*v</c>, not scaled.</summary>
        public static Vector3 Direction(Vector3 ro) => new Vector3(ro.x, ro.y * FlipY, ro.z);

        /// <summary>A local transform: <c>F*M*F</c>, with the translation then scaled like a point.</summary>
        public static Matrix4x4 Matrix(Matrix4x4 roLocal)
        {
            var m = Conjugate(roLocal);
            m.m03 *= RoUnits.Scale;
            m.m13 *= RoUnits.Scale;
            m.m23 *= RoUnits.Scale;
            return m;
        }

        /// <summary>The rotation of the conjugated matrix <c>F*R*F</c>.</summary>
        public static Quaternion Rotation(Matrix4x4 roRotation) => Conjugate(roRotation).rotation;

        /// <summary>A scale is unchanged by the conjugation; a negative component (a mirrored model) keeps its sign.</summary>
        public static Vector3 Scale(Vector3 roScale) => roScale;

        /// <summary>Swaps the 2nd and 3rd index of every triangle. Called once per mesh by mesh builders only.</summary>
        public static void ReverseWinding(int[] triangleIndices)
        {
            if (triangleIndices == null) throw new ArgumentNullException(nameof(triangleIndices));
            if (triangleIndices.Length % 3 != 0)
                throw new ArgumentException($"A triangle list needs a multiple of 3 indices, got {triangleIndices.Length}.", nameof(triangleIndices));

            for (int i = 0; i < triangleIndices.Length; i += 3)
                (triangleIndices[i + 1], triangleIndices[i + 2]) = (triangleIndices[i + 2], triangleIndices[i + 1]);
        }

        static Matrix4x4 Conjugate(Matrix4x4 m) => Mirror * m * Mirror;
    }
}
