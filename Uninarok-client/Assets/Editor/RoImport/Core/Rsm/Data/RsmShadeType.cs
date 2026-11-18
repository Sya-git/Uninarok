namespace Uninarok.RoImport.Core.Rsm
{
    /// <summary>The model's shading type as stored in the file (plan 4.1, 4.6; SPEC-0003 FR-5).</summary>
    public enum RsmShadeType
    {
        /// <summary>No lighting: one constant normal for the whole model.</summary>
        None = 0,
        /// <summary>Every face keeps its own normal.</summary>
        Flat = 1,
        /// <summary>Normals are shared within a smoothing group. Also the rule for versions without a shade type.</summary>
        Smooth = 2
    }
}
