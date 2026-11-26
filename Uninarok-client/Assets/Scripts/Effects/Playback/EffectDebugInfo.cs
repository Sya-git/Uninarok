#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>One line of the dev effect list (SPEC-0012 FR-8, plan 4.8): what <c>EffectSystem.FillDebug</c> reports per active effect. Dev only.</summary>
    public struct EffectDebugInfo
    {
        /// <summary>Canonical catalog key (the path given, normalised, for an unknown path).</summary>
        public string Key;
        public EffectPlace Place;
        /// <summary>World position of the instance.</summary>
        public Vector3 Position;
        /// <summary>Map cell of a <see cref="EffectPlace.Cell"/> play.</summary>
        public int CellX, CellY;
        /// <summary>Name of the followed unit for <see cref="EffectPlace.Unit"/>; "(gone)" once it was destroyed.</summary>
        public string UnitName;
        /// <summary>Parts drawn: the STR layer count, 1 for the other kinds and the placeholder, 0 while the asset loads.</summary>
        public int Parts;
        /// <summary>Effect seconds into the current cycle.</summary>
        public float Age;
        public bool Loop;
        /// <summary>The asset is still loading.</summary>
        public bool Waiting;
    }
}
#endif
