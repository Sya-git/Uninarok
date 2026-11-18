namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>The diagonal that splits a top quad into two triangles (PLAN-0004 4.3).</summary>
    public enum RoGndDiagonal
    {
        /// <summary>Corner <c>H10</c> to corner <c>H01</c>: triangles <c>(00, 10, 01)</c> and <c>(10, 11, 01)</c>, as the original client.</summary>
        H10ToH01,
        /// <summary>Corner <c>H00</c> to corner <c>H11</c> (the old importer's choice; gives slope glitches).</summary>
        H00ToH11
    }

    /// <summary>Which side of a wall is its front; the back is culled (PLAN-0004 4.3).</summary>
    public enum RoGndWallFacing
    {
        /// <summary>Toward the side whose edge is lower: the sums of the two end heights are compared (Ragnarok Y down).</summary>
        TowardLowerSide
    }

    /// <summary>
    /// The ground rules verified against the reference viewer (PLAN-0004 4.3, TASKS-0004 T0.3), one row per GND version.
    /// The row direction is <see cref="RoMapSpace.RowDirection"/>; the wall UV order is fixed by <see cref="RoGndQuad"/>
    /// (corner <c>Pij</c> takes <c>Uvij</c>, as for tops); the V axis flip is the mesh builder's (<c>(u, 1 - v)</c>).
    /// </summary>
    public readonly struct RoGndRules
    {
        /// <summary>Heights closer than this, in Ragnarok units, are equal: no wall between them (PLAN-0004 4.3).</summary>
        public const float Epsilon = 0.0001f;

        public readonly int VersionMajor, VersionMinor;
        public readonly RoGndDiagonal Diagonal;
        public readonly RoGndWallFacing WallFacing;

        RoGndRules(int major, int minor, RoGndDiagonal diagonal, RoGndWallFacing wallFacing)
        {
            VersionMajor = major;
            VersionMinor = minor;
            Diagonal = diagonal;
            WallFacing = wallFacing;
        }

        /// <summary>
        /// The rules row of a GND version; false when the table has none (reported as
        /// <see cref="Results.RoIssueCode.UnsupportedGndVersion"/> by the converter). Only 1.7 is verified (T0.3: every
        /// reference map is 1.7); no other row is guessed (PLAN-0004 4.6).
        /// </summary>
        public static bool TryGet(int versionMajor, int versionMinor, out RoGndRules rules)
        {
            if (versionMajor == 1 && versionMinor == 7)
            {
                rules = new RoGndRules(1, 7, RoGndDiagonal.H10ToH01, RoGndWallFacing.TowardLowerSide);
                return true;
            }

            rules = default;
            return false;
        }
    }
}
