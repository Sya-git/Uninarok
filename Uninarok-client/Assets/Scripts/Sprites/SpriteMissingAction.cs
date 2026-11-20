namespace Uninarok.Sprites
{
    /// <summary>
    /// What to show when a sprite is asked for an action its file does not have (SPEC-0007 FR-5). An action the file has but
    /// whose layers are all empty is not missing: it draws nothing whatever this policy says.
    /// </summary>
    public enum SpriteMissingAction
    {
        /// <summary>Draw nothing.</summary>
        DrawNothing,
        /// <summary>The same animation in direction 0, else idle in the requested direction, else the first action.</summary>
        Fallback
    }
}
