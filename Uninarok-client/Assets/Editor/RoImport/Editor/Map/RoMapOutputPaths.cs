using System;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Editor.Map
{
    /// <summary>
    /// The paths of one map (PLAN-0006 §3, D5, D16): for the source <c>&lt;name&gt;.rsw</c> at the data root, the generated
    /// scene <c>Scenes/&lt;name&gt;.unity</c> (ledger-owned) and the hand-made additive scene
    /// <c>Scenes/&lt;name&gt;.additive.unity</c> beside it, which the importer never writes; and the ground and map data
    /// sources beside the RSW.
    /// </summary>
    public sealed class RoMapOutputPaths
    {
        public string MapName { get; private set; }
        public string Scene { get; private set; }
        public string AdditiveScene { get; private set; }
        public RoPath Gnd { get; private set; }
        public RoPath Gat { get; private set; }

        public static RoMapOutputPaths For(RoOutputRoots roots, RoPath source)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (source.IsEmpty) throw new ArgumentException("A map needs a source path.", nameof(source));
            string name = source.FileNameWithoutExtension;
            return new RoMapOutputPaths
            {
                MapName = name,
                Scene = RoOutputPaths.Scene(roots, name),
                AdditiveScene = RoOutputPaths.AdditiveScene(roots, name),
                Gnd = source.WithExtension(".gnd"),
                Gat = source.WithExtension(".gat")
            };
        }
    }
}
