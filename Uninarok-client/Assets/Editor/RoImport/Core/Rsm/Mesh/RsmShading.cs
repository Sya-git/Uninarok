namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>
    /// The normal rules of PLAN-0003 4.6 (SPEC-0003 FR-5): which face corners share a normal.
    /// </summary>
    public static class RsmShading
    {
        /// <summary>The shade type used: a version without one is smooth, as in the original client.</summary>
        public static RsmShadeType Effective(RsmShadeType shade, bool hasShadeType) => hasShadeType ? shade : RsmShadeType.Smooth;

        /// <summary>
        /// The normal group of a face corner, besides its welded position: the smoothing group (smooth), the face's
        /// flat group (flat, see <see cref="FlatKey"/>, D11) or one constant (none).
        /// </summary>
        public static int GroupKey(RsmShadeType effective, int flatGroup, int smoothGroup)
        {
            switch (effective)
            {
                case RsmShadeType.None: return 0;
                case RsmShadeType.Flat: return flatGroup;
                default: return smoothGroup;
            }
        }

        /// <summary>
        /// Flat shading groups faces by their normal rounded to 1e-4 (D11): coplanar faces share corners, faces of
        /// different normals keep a hard edge.
        /// </summary>
        public static (int, int, int) FlatKey(UnityEngine.Vector3 faceNormal) =>
            (UnityEngine.Mathf.RoundToInt(faceNormal.x * 10000f),
             UnityEngine.Mathf.RoundToInt(faceNormal.y * 10000f),
             UnityEngine.Mathf.RoundToInt(faceNormal.z * 10000f));

        /// <summary>Smoothing-group equality: GRF reports one integer group id per face (T0.2), not a bit mask.</summary>
        public static bool SameGroup(int a, int b) => a == b;
    }
}
