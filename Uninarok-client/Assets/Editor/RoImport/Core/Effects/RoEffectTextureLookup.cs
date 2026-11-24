using System;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>
    /// Finds the file a STR layer's texture name means (PLAN-0012 4.3, SPEC-0012 FR-2): the STR's own folder, then
    /// <see cref="EffectRoot"/>. Only names and file existence are dealt with; no file is read. Pure. Stub until T3.5.
    /// </summary>
    public static class RoEffectTextureLookup
    {
        /// <summary><c>texture/effect</c>.</summary>
        public static RoPath EffectRoot => RoPath.Parse("texture/effect");

        /// <summary>The extensions tried, in order, for a name without one (FR-2: by name alone, .tga first).</summary>
        public static string[] BareNamePreference => new[] { ".tga", ".bmp" };

        public static bool TryResolve(IRoSource src, RoPath str, string name, out RoPath texture)
        {
            throw new NotImplementedException("pending T3.5");
        }
    }
}
