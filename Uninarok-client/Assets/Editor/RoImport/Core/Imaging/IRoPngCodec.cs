namespace Uninarok.RoImport.Core.Imaging
{
    /// <summary>
    /// PNG through Unity's <c>ImageConversion</c> (plan 4.4, D16): encodes every output texture and decodes PNG sources,
    /// which GRF cannot decode inside Unity. Implemented in the editor layer (<c>RoPngCodec</c>); Core only sees this.
    /// </summary>
    public interface IRoPngCodec
    {
        /// <summary>Decodes a PNG into an <see cref="RoImage"/> (top row first); false for bytes that are not a valid PNG.</summary>
        bool TryDecode(byte[] png, out RoImage image);

        /// <summary>Encodes an image as PNG.</summary>
        byte[] Encode(RoImage image);
    }
}
