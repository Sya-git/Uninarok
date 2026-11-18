using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// An RSM model with the file's values as GRF reads them, in Ragnarok space, unconverted (plan 4.1, D2). Every rule
    /// after the reader works on this type, so the rules are testable without the library and without data files.
    /// </summary>
    public sealed class RoRsm
    {
        public int VersionMajor, VersionMinor;
        /// <summary>The file's shade type; <see cref="RsmShadeType.Smooth"/> when the version has none.</summary>
        public RsmShadeType ShadeType = RsmShadeType.Smooth;
        /// <summary>False for versions without a shade type.</summary>
        public bool HasShadeType;
        /// <summary>In the file's keyframe units. Not used for playback (plan 4.8, D10).</summary>
        public int AnimationLength;
        /// <summary>Texture names as stored, relative to <c>texture/</c>.</summary>
        public IReadOnlyList<string> Textures = Array.Empty<string>();
        /// <summary>Nodes in file order.</summary>
        public IReadOnlyList<RoRsmNode> Nodes = Array.Empty<RoRsmNode>();

        public override string ToString() => $"RoRsm {VersionMajor}.{VersionMinor} ({Nodes.Count} nodes)";
    }
}
