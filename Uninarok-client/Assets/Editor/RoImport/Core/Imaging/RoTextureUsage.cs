using System;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Imaging
{
    /// <summary>What a texture is used for; it picks the import settings (plan 4.4 table).</summary>
    public enum RoTextureUsage
    {
        World,
        Ui
    }

    /// <summary>Usage by source path, never by extension (plan 4.4, D8, SPEC-0002 FR-8).</summary>
    public static class RoTextureUsageRules
    {
        /// <summary>The Ragnarok UI texture folder, <c>texture/유저인터페이스/</c> (canonical form, NFC).</summary>
        public const string UiFolder = "texture/유저인터페이스/";

        /// <summary><see cref="RoTextureUsage.Ui"/> under <see cref="UiFolder"/>; <see cref="RoTextureUsage.World"/> otherwise.</summary>
        public static RoTextureUsage For(RoPath path) =>
            path.Canonical.StartsWith(UiFolder, StringComparison.Ordinal) ? RoTextureUsage.Ui : RoTextureUsage.World;
    }
}
