using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests
{
    /// <summary>The editor <see cref="RoPngCodec"/>, counting decodes so tests can see when PNG went through the codec.</summary>
    sealed class TestPngCodec : IRoPngCodec
    {
        readonly RoPngCodec _codec = new RoPngCodec();

        public int Decodes;

        public bool TryDecode(byte[] png, out RoImage image)
        {
            Decodes++;
            return _codec.TryDecode(png, out image);
        }

        public byte[] Encode(RoImage image) => _codec.Encode(image);
    }
}
