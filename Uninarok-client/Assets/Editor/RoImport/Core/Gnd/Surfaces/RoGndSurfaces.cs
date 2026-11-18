using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// The top and wall surfaces of a ground (PLAN-0004 4.3): tops where a cube has a top tile, walls only where a wall
    /// tile exists and the heights differ, never reading outside the grid.
    /// </summary>
    public static class RoGndSurfaces
    {
        public static IEnumerable<RoGndQuad> Enumerate(RoGnd gnd, RoGndRules rules, RoReport report) =>
            throw new NotImplementedException("TASKS-0004 T3.2");
    }
}
