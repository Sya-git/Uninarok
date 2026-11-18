// Old import pipeline, compiled out by SPEC-0002 T0.2 (plan D15). Deleted by the plan that replaces it.
#if RO_LEGACY_IMPORTERS
using GRF.Graphics;
using UnityEngine;

namespace Uninarok.Editor
{
    public static class UtilExtensions
    {
        public static Vector3 ToVector3NegativeY(this TkVector3 v3)
        {
            return new Vector3(v3.X, -v3.Y, v3.Z);
        }

        public static Vector3 ToVector3(this TkVector3 v3)
        {
            return new Vector3(v3.X, v3.Y, v3.Z);
        }
    }
}
#endif
