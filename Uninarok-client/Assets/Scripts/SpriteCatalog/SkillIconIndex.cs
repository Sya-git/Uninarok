using System;
using UnityEngine;

namespace Uninarok.Catalog
{
    /// <summary>Skill id → icon (SPEC-0008 FR-7; PLAN-0008 4.5). Sorted by skill id.</summary>
    public sealed class SkillIconIndex : ScriptableObject
    {
        public int[] SkillIds = Array.Empty<int>();
        public SpriteRef[] Icons = Array.Empty<SpriteRef>();
    }
}
