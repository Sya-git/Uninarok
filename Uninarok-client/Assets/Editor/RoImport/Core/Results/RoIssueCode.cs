namespace Uninarok.RoImport.Core.Results
{
    /// <summary>
    /// Kinds of issue (plan 4.9). Every code except <see cref="Unexpected"/> is an expected condition
    /// and is reported as a warning (SPEC-0002 FR-11); <see cref="Unexpected"/> is a bug.
    /// </summary>
    public enum RoIssueCode
    {
        SourceMissing,
        TextureMissing,
        UnsupportedTextureFormat,
        CorruptFile,
        OutputCollision,
        SourceDuplicate,
        ShaderMissing,
        DependencyCycle,
        /// <summary>An RSM version that the rules table has no row for (SPEC-0003 FR-8, PLAN-0003 4.1).</summary>
        UnsupportedRsmVersion,
        /// <summary>An RSM node names a parent that no node has; the node becomes a root (PLAN-0003 4.2).</summary>
        RsmParentMissing,
        /// <summary>RSM nodes whose parents form a cycle; the model is not converted (PLAN-0003 4.2).</summary>
        RsmCycle,
        /// <summary>Two RSM nodes share a name; later ones are renamed <c>_2</c>, <c>_3</c> (PLAN-0003 4.2).</summary>
        RsmDuplicateName,
        /// <summary>An RSM face refers to a vertex, texture coordinate or texture slot that does not exist (PLAN-0003 4.3).</summary>
        RsmBadIndex,
        Unexpected
    }
}
