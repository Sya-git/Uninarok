using System;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Gnd
{
    /// <summary>
    /// The output paths of one ground (PLAN-0004 §3): for the source <c>&lt;name&gt;.gnd</c>, the prefab
    /// <c>Art/map/&lt;name&gt;/&lt;name&gt;_ground.prefab</c> and the chunk meshes
    /// <c>Art/map/&lt;name&gt;/ground/chunk_&lt;cx&gt;_&lt;cz&gt;.asset</c>. The map files sit at the data root (D11), so the
    /// folder is built from the map name, not mirrored from the source path.
    /// </summary>
    public sealed class RoGndOutputPaths
    {
        public const string MapFolder = "map";

        public string Prefab { get; private set; }
        public string Folder { get; private set; }

        public static RoGndOutputPaths For(RoOutputRoots roots, RoPath source)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (source.IsEmpty) throw new ArgumentException("A ground needs a source path.", nameof(source));
            string name = source.FileNameWithoutExtension;
            string map = roots.Art + "/" + MapFolder + "/" + name;
            return new RoGndOutputPaths { Prefab = map + "/" + name + "_ground.prefab", Folder = map + "/ground" };
        }

        public string Chunk(int chunkColumn, int chunkRow) => $"{Folder}/chunk_{chunkColumn}_{chunkRow}.asset";
    }
}
