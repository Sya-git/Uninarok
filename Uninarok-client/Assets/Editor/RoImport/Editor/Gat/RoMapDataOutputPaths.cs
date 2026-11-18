using System;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Editor.Gnd;

namespace Uninarok.RoImport.Editor.Gat
{
    /// <summary>
    /// The output paths of one map data (PLAN-0005 §3): for the source <c>&lt;name&gt;.gat</c>, the asset
    /// <c>Data/map/&lt;name&gt;.asset</c> and the optional cell image <c>Art/map/&lt;name&gt;/&lt;name&gt;_cells.png</c>, next to
    /// the ground of the same map (PLAN-0004 D11).
    /// </summary>
    public sealed class RoMapDataOutputPaths
    {
        public string Asset { get; private set; }
        public string Image { get; private set; }

        public static RoMapDataOutputPaths For(RoOutputRoots roots, RoPath source)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (source.IsEmpty) throw new ArgumentException("Map data needs a source path.", nameof(source));
            string name = source.FileNameWithoutExtension;
            return new RoMapDataOutputPaths
            {
                Asset = RoOutputPaths.Data(roots, RoGndOutputPaths.MapFolder, name, ".asset"),
                Image = roots.Art + "/" + RoGndOutputPaths.MapFolder + "/" + name + "/" + name + "_cells.png"
            };
        }
    }
}
