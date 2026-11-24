namespace Uninarok.Effects
{
    /// <summary>Handle of one catalog entry (index into <see cref="EffectCatalogIndex"/>). SPEC-0012 plan 4.4.</summary>
    public readonly struct EffectRef
    {
        public readonly int Index;
        public EffectRef(int index) { Index = index; }
    }

    /// <summary>Loading state of one catalog entry's asset.</summary>
    public enum EffectAssetState : byte
    {
        NotLoaded = 0,
        Loading,
        Loaded,
        /// <summary>The source reported no asset, or the index marks its source file as gone.</summary>
        Failed
    }
}
