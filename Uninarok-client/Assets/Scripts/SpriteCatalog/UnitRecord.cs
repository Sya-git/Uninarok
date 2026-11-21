using System;

namespace Uninarok.Catalog
{
    /// <summary>
    /// One monster, NPC or homunculus (SPEC-0008 FR-1; PLAN-0008 4.4, D5): its job id, client name, sprite and scale. The
    /// job id is a plain int (not <see cref="Uninarok.Networking.Data.PlayerJob"/>): units are a different id space.
    /// </summary>
    [Serializable]
    public struct UnitRecord
    {
        public int JobId;
        public string Name;
        public SpriteRef Sprite;
        /// <summary>The client's size factor (<c>monster_size_effect_new</c>), 1 when it gives none.</summary>
        public float Scale;
    }
}
