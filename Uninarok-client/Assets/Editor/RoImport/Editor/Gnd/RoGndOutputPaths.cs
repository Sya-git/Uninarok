using System;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Gnd
{
    /// <summary>
    /// The output paths of one ground (PLAN-0004 §3): for the source <c>&lt;name&gt;.gnd</c>, the prefab
    /// <c>Art/map/&lt;name&gt;/&lt;name&gt;_ground.prefab</c> and the chunk meshes
    /// <c>Art/map/&lt;name&gt;/ground/chunk_&lt;cx&gt;_&lt;cz&gt;.asset</c>.
    /// </summary>
    public sealed class RoGndOutputPaths
    {
        public string Prefab { get; private set; }
        public string Folder { get; private set; }

        public static RoGndOutputPaths For(RoOutputRoots roots, RoPath source) =>
            throw new NotImplementedException("TASKS-0004 T5.2");

        public string Chunk(int chunkColumn, int chunkRow) => throw new NotImplementedException("TASKS-0004 T5.2");
    }
}
