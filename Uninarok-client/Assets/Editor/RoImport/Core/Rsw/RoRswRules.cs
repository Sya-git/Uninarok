namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>The order of the three rotations of a placed model (PLAN-0006 4.3).</summary>
    public enum RoAxisOrder
    {
        /// <summary>The matrix product <c>Rz·Rx·Ry</c>: Y is applied first, then X, then Z.</summary>
        ZXY
    }

    /// <summary>The placement rules of PLAN-0006 4.3. Implemented by TASKS-0006 T3.2.</summary>
    public readonly struct RoRswRules
    {
        public readonly float PositionScale;
        public readonly RoAxisOrder RotationOrder;
        public readonly float RotationSignX, RotationSignY, RotationSignZ;
        public readonly float ScaleYSign;
        public readonly bool NodeNameSelectsRoot;

        public static readonly RoRswRules V1 = default;

        public static bool TryGet(int versionMajor, int versionMinor, out RoRswRules rules)
        {
            rules = default;
            return false;
        }
    }
}
