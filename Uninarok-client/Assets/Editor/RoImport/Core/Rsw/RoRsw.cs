using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Rsw
{
    /// <summary>
    /// A map's RSW file with its values as GRF reads them, unconverted (PLAN-0006 4.1, SPEC-0006 FR-1). Every rule after
    /// the reader (placement, lighting, settings) works on this type, so the rules are testable without the library and
    /// without data files. Each object list keeps the file order, which makes the scene deterministic.
    /// </summary>
    public sealed class RoRsw
    {
        public int VersionMajor, VersionMinor;
        /// <summary>The build number; 0 below version 2.2, where the file has none.</summary>
        public int Build;
        /// <summary>The ground and walkability files named in the header, as stored.</summary>
        public string GndFile = string.Empty, GatFile = string.Empty;
        public RoRswWater Water;
        public RoRswLight Light;
        public IReadOnlyList<RoRswModel> Models = Array.Empty<RoRswModel>();
        public IReadOnlyList<RoRswPointLight> Lights = Array.Empty<RoRswPointLight>();
        public IReadOnlyList<RoRswSound> Sounds = Array.Empty<RoRswSound>();
        public IReadOnlyList<RoRswEffect> Effects = Array.Empty<RoRswEffect>();

        public override string ToString() =>
            $"RoRsw {VersionMajor}.{VersionMinor} ({Models.Count} models, {Lights.Count} lights, {Sounds.Count} sounds, {Effects.Count} effects)";
    }
}
