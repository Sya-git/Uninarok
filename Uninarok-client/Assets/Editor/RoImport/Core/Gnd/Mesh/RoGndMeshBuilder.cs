using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// Builds the chunk meshes of a ground (PLAN-0004 4.4): welded vertices, map-wide angle-weighted normals, walls flat,
    /// winding reversed once, chunk origin and second UV set. Pure: arrays in, arrays out.
    /// </summary>
    public static class RoGndMeshBuilder
    {
        /// <summary>Cubes per chunk edge.</summary>
        public const int ChunkSize = 16;

        public static IReadOnlyList<RoGndMeshData> Build(RoGnd gnd, RoGndRules rules, RoReport report) =>
            throw new NotImplementedException("TASKS-0004 T4.3");
    }
}
