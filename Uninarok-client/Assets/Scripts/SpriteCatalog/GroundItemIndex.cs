using System;
using UnityEngine;

namespace Uninarok.Catalog
{
    /// <summary>One sprite per item resource name (SPEC-0008 FR-5; PLAN-0008 4.5): items sharing a sprite share the slot.</summary>
    public sealed class GroundItemIndex : ScriptableObject
    {
        public string[] ResourceNames = Array.Empty<string>();
        public SpriteRef[] Resources = Array.Empty<SpriteRef>();
    }
}
