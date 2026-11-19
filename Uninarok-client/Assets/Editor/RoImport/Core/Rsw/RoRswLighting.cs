using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>The scene light from the RSW's global light (PLAN-0006 4.4). Implemented by TASKS-0006 T3.2.</summary>
    public static class RoRswLighting
    {
        public static Color Ambient(in RoRswLight l) => throw new NotImplementedException("TASKS-0006 T3.2");
        public static Color Sun(in RoRswLight l) => throw new NotImplementedException("TASKS-0006 T3.2");
        public static Quaternion SunRotation(in RoRswLight l) => throw new NotImplementedException("TASKS-0006 T3.2");
        public static float ShadowStrength(in RoRswLight l) => throw new NotImplementedException("TASKS-0006 T3.2");
    }
}
