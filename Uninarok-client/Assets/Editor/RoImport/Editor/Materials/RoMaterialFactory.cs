using System;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Materials
{
    /// <summary>Shared, lit URP materials (plan 4.6). Contract only: implemented by T5.3.</summary>
    public static class RoMaterialFactory
    {
        public static RoResult<Material> GetOrCreate(RoImportContext ctx, RoMaterialKey key) => throw new NotImplementedException("T5.3");
        public static RoSurface SurfaceFor(RoTextureAlpha alpha) => throw new NotImplementedException("T5.3");
    }
}
