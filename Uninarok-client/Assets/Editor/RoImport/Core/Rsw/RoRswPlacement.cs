using System;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Space;

namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>
    /// Placement of an RSW model (PLAN-0006 4.3, D1, D13; SPEC-0006 FR-3). One matrix chain in Ragnarok space,
    /// <c>M = T(FromRswRo(position · PositionScale)) · R · S</c>, then one conversion to Unity through <see cref="RoSpace"/>
    /// and one decomposition. No component is converted on its own. Pure.
    /// </summary>
    public static class RoRswPlacement
    {
        /// <summary>The model matrix in Ragnarok space (column vectors), including the offset to the map centre.</summary>
        public static Matrix4x4 ModelMatrixRo(in RoRswModel m, RoMapSpace space, in RoRswRules rules)
        {
            var position = space.FromRswRo(m.Position * rules.PositionScale);
            var scale = new Vector3(m.Scale.x, m.Scale.y * rules.ScaleYSign, m.Scale.z);
            return Matrix4x4.Translate(position) * Rotation(m.Rotation, rules) * Matrix4x4.Scale(scale);
        }

        /// <summary>The rotation of the rules table, built from the file's angles in degrees.</summary>
        public static Matrix4x4 Rotation(Vector3 degrees, in RoRswRules rules)
        {
            var x = Axis(Vector3.right, degrees.x * rules.RotationSignX);
            var y = Axis(Vector3.up, degrees.y * rules.RotationSignY);
            var z = Axis(Vector3.forward, degrees.z * rules.RotationSignZ);
            switch (rules.RotationOrder)
            {
                case RoAxisOrder.ZXY: return z * x * y;
                default: throw new ArgumentOutOfRangeException(nameof(rules), rules.RotationOrder, "unknown rotation order");
            }
        }

        // Numerically the standard rotation matrix about the axis (x -> (cos, sin) for Z), the same in both spaces.
        static Matrix4x4 Axis(Vector3 axis, float degrees) => Matrix4x4.Rotate(Quaternion.AngleAxis(degrees, axis));

        /// <summary>
        /// Converts a Ragnarok-space model matrix with <see cref="RoSpace.Matrix"/> and decomposes it for a Transform. A
        /// reflection (negative determinant, a mirrored model) keeps its sign on the X scale; the rotation is then the
        /// rest, so <c>TRS(position, rotation, scale)</c> rebuilds the matrix and Unity flips the culling itself.
        /// </summary>
        public static void ToUnity(Matrix4x4 ro, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            var u = RoSpace.Matrix(ro);
            Vector3 c0 = u.GetColumn(0), c1 = u.GetColumn(1), c2 = u.GetColumn(2);
            scale = new Vector3(c0.magnitude, c1.magnitude, c2.magnitude);
            if (u.determinant < 0f) scale.x = -scale.x;
            position = u.GetColumn(3);
            rotation = scale.y > 0f && scale.z > 0f
                ? Quaternion.LookRotation(c2 / scale.z, c1 / scale.y)
                : Quaternion.identity;
        }
    }
}
