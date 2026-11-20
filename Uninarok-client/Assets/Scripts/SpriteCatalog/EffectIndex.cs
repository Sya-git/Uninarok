using System;
using UnityEngine;

namespace Uninarok.Catalog
{
    /// <summary>
    /// Effects by name, sorted (SPEC-0008 FR-6; PLAN-0008 4.5), and the emote sprite with the action of each emote id.
    /// </summary>
    public sealed class EffectIndex : ScriptableObject
    {
        public string[] Names = Array.Empty<string>();
        public SpriteRef[] Effects = Array.Empty<SpriteRef>();
        public SpriteRef EmoteSet;
        public int[] EmoteIds = Array.Empty<int>();
        public int[] EmoteActions = Array.Empty<int>();
    }
}
