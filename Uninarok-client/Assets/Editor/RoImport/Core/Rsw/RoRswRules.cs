namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>The order of the three rotations of a placed model (PLAN-0006 4.3).</summary>
    public enum RoAxisOrder
    {
        /// <summary>The matrix product <c>Rz·Rx·Ry</c>: Y is applied first, then X, then Z.</summary>
        ZXY
    }

    /// <summary>
    /// The placement rules verified against the reference viewer (PLAN-0006 4.3, D13, TASKS-0006 T0.3), one row per RSW
    /// version. Each choice is a named value so a test pins it.
    /// </summary>
    public readonly struct RoRswRules
    {
        /// <summary>RSW position unit to Ragnarok unit (1: positions are already Ragnarok units).</summary>
        public readonly float PositionScale;
        public readonly RoAxisOrder RotationOrder;
        /// <summary>Signs applied to the file's angles before building the rotations.</summary>
        public readonly float RotationSignX, RotationSignY, RotationSignZ;
        /// <summary>Sign applied to the file's Y scale (+1: the scale is used as stored).</summary>
        public readonly float ScaleYSign;
        /// <summary>Whether the RSW node name selects the model's root node (false: the whole model is drawn).</summary>
        public readonly bool NodeNameSelectsRoot;

        RoRswRules(float positionScale, RoAxisOrder order, float signX, float signY, float signZ, float scaleYSign, bool nodeNameSelectsRoot)
        {
            PositionScale = positionScale;
            RotationOrder = order;
            RotationSignX = signX;
            RotationSignY = signY;
            RotationSignZ = signZ;
            ScaleYSign = scaleYSign;
            NodeNameSelectsRoot = nodeNameSelectsRoot;
        }

        /// <summary>The rules of T0.3: unit positions, <c>Rz·Rx·Ry</c> with all signs +1, scale as stored, node name ignored.</summary>
        public static readonly RoRswRules V1 = new RoRswRules(1f, RoAxisOrder.ZXY, 1f, 1f, 1f, 1f, false);

        /// <summary>
        /// The rules row of an RSW version; false when the table has none (reported as
        /// <see cref="Results.RoIssueCode.UnsupportedRswVersion"/> by the converter). Only 2.1 is verified (T0.2: every
        /// reference map is 2.1); no other row is guessed.
        /// </summary>
        public static bool TryGet(int versionMajor, int versionMinor, out RoRswRules rules)
        {
            if (versionMajor == 2 && versionMinor == 1)
            {
                rules = V1;
                return true;
            }

            rules = default;
            return false;
        }
    }
}
