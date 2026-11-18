namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// The placement rules of one RSM version (PLAN-0003 4.4, D9). Nothing else in the code branches on the version.
    /// Only versions verified against the reference viewer have a row (TASKS-0003 T0.3).
    /// </summary>
    public sealed class RsmRules
    {
        public int Major, Minor;
        /// <summary>The rest pose takes the first key of a node that has keys (4.4 step 1).</summary>
        public bool RestFromFirstKey;
        /// <summary>The version has scale keys.</summary>
        public bool HasScaleKeys;
        /// <summary>The model's origin is the bottom center of its rest-pose bounds (4.4 step 5).</summary>
        public bool Recenter;
        /// <summary>The node's offset vector is part of its vertex matrix (4.4 step 4).</summary>
        public bool OffsetVectorInVertexMatrix;

        /// <summary>The rules of a version, or <c>false</c> when the version has no row (UnsupportedRsmVersion).</summary>
        public static bool TryGet(int major, int minor, out RsmRules rules)
        {
            // One row per version verified against the reference viewer (PLAN-0003 4.4 table, TASKS-0003 T0.3).
            if (major == 1 && minor == 4)
            {
                rules = new RsmRules
                {
                    Major = 1,
                    Minor = 4,
                    RestFromFirstKey = true,
                    HasScaleKeys = false,
                    Recenter = true,
                    OffsetVectorInVertexMatrix = true
                };
                return true;
            }

            rules = null;
            return false;
        }
    }
}
