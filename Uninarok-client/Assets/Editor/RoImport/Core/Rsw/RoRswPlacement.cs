using System;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>Placement of RSW models (PLAN-0006 4.3). Implemented by TASKS-0006 T3.2.</summary>
    public static class RoRswPlacement
    {
        public static Matrix4x4 ModelMatrixRo(in RoRswModel m, RoMapSpace space, in RoRswRules rules) =>
            throw new NotImplementedException("TASKS-0006 T3.2");

        public static void ToUnity(Matrix4x4 ro, out Vector3 position, out Quaternion rotation, out Vector3 scale) =>
            throw new NotImplementedException("TASKS-0006 T3.2");
    }
}
