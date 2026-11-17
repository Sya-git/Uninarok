using System;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>Source texture to PNG asset (plan 4.4). Contract only: implemented by T6.2.</summary>
    public sealed class RoTextureConverter : IRoConverter
    {
        public const string Category = "texture";
        public const int Rules = 1;

        public RoJobKind Kind => RoJobKind.Texture;
        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path) => throw new NotImplementedException("T6.2");
    }
}
