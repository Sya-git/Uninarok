namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// A triangle as stored in the file (plan 4.1): three vertex indices, three texture coordinate indices, the node-local
    /// texture slot, the two-sided flag and the smoothing group.
    /// </summary>
    public struct RoRsmFace
    {
        public int V0, V1, V2, T0, T1, T2, TextureSlot;
        public bool TwoSided;
        public int SmoothGroup;

        public RoRsmFace(int v0, int v1, int v2, int t0, int t1, int t2, int textureSlot = 0, bool twoSided = false, int smoothGroup = 0)
        {
            V0 = v0; V1 = v1; V2 = v2;
            T0 = t0; T1 = t1; T2 = t2;
            TextureSlot = textureSlot;
            TwoSided = twoSided;
            SmoothGroup = smoothGroup;
        }
    }
}
