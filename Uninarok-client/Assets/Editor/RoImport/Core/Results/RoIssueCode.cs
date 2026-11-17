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
        Unexpected
    }
}
