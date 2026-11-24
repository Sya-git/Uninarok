using System;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>
    /// Finds the file a STR layer's texture name means (PLAN-0012 4.3, SPEC-0012 FR-2): the STR's own folder, then
    /// <see cref="EffectRoot"/>. Only names and file existence are dealt with; no file is read. Import time only.
    /// </summary>
    public static class RoEffectTextureLookup
    {
        /// <summary><c>texture/effect</c>.</summary>
        public static RoPath EffectRoot => RoPath.Parse("texture/effect");

        /// <summary>The extensions tried, in order, for a name without one (FR-2: by name alone, .tga first).</summary>
        public static string[] BareNamePreference => new[] { ".tga", ".bmp" };

        /// <summary>
        /// A name with an extension resolves to exactly that file; a name without one tries
        /// <see cref="BareNamePreference"/> in the own folder, then in the root (folder order first). A name found nowhere
        /// gives false, an empty <paramref name="texture"/> and one <see cref="RoIssueCode.TextureMissing"/> warning per
        /// (texture, STR) on <paramref name="report"/> (referrer = the STR; PLAN-0012 D25).
        /// </summary>
        public static bool TryResolve(IRoSource src, RoPath str, string name, RoReport report, out RoPath texture)
        {
            texture = default;
            if (!string.IsNullOrWhiteSpace(name))
            {
                foreach (var folder in new[] { str.Folder, EffectRoot })
                {
                    if (TryFolder(src, folder, name, out texture)) return true;
                }
            }
            texture = default;
            // RoReport dedupes per (code, item); the STR is part of the item so a second STR gets its own warning.
            report?.Warn(RoIssueCode.TextureMissing, str.Canonical + ": " + name, str.Canonical,
                $"texture '{name}' is in neither the STR's folder nor {EffectRoot.Canonical}");
            return false;
        }

        static bool TryFolder(IRoSource src, RoPath folder, string name, out RoPath texture)
        {
            texture = default;
            try
            {
                var candidate = folder.Combine(name);
                if (!string.IsNullOrEmpty(candidate.Extension))
                    return Exists(src, candidate, out texture);
                foreach (var ext in BareNamePreference)
                    if (Exists(src, candidate.WithExtension(ext), out texture)) return true;
            }
            catch (RoPathException) { }
            return false;
        }

        static bool Exists(IRoSource src, RoPath path, out RoPath found)
        {
            found = default;
            if (!src.TryGetFile(path, out _)) return false;
            found = path;
            return true;
        }
    }
}
