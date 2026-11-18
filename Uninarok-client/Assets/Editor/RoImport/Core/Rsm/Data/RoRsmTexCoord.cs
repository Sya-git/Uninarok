namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>A texture coordinate as stored in the file (plan 4.1): <c>u</c>, <c>v</c> and, where the version has it, a color.</summary>
    public struct RoRsmTexCoord
    {
        public float U, V;
        public uint Color;

        public RoRsmTexCoord(float u, float v, uint color = 0)
        {
            U = u;
            V = v;
            Color = color;
        }
    }
}
