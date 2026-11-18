namespace Uninarok.RoImport.Core.Materials
{
    /// <summary>
    /// How a material draws its texture's transparency (plan 4.6, D11). Derived from the texture, never from the model:
    /// <see cref="Opaque"/>, <see cref="Cutout"/> (keyed or binary alpha) or <see cref="Blend"/> (real partial alpha).
    /// </summary>
    public enum RoSurface
    {
        Opaque,
        Cutout,
        Blend
    }
}
