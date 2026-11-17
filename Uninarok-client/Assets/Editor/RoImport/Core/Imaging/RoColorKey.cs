using System;

namespace Uninarok.RoImport.Core.Imaging
{
    /// <summary>How a texture uses transparency (plan 4.4); it picks the material surface (plan 4.6).</summary>
    public enum RoTextureAlpha
    {
        None,
        Keyed,
        Partial
    }

    /// <summary>The Ragnarok transparency color and edge bleeding (plan 4.4). Contract only: implemented by T3.4.</summary>
    public static class RoColorKey
    {
        public const byte KeyR = 255, KeyG = 0, KeyB = 255, Tolerance = 10;

        public static RoTextureAlpha Apply(RoImage image) => throw new NotImplementedException("T3.4");
        public static void BleedTransparentColor(RoImage image) => throw new NotImplementedException("T3.4");
    }
}
