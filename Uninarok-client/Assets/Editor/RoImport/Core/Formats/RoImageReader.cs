using System;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>Adapter over the GRF image classes (plan 4.4). Contract only: implemented by T3.2.</summary>
    public static class RoImageReader
    {
        public static bool IsSupportedExtension(string extension) => throw new NotImplementedException("T3.2");

        public static bool TryRead(RoPath path, byte[] bytes, IRoPngCodec png, out RoImage image, out string reason) =>
            throw new NotImplementedException("T3.2");
    }
}
