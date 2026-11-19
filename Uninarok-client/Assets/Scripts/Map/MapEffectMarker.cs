using UnityEngine;

namespace Uninarok.Map
{
    // Data-only marker for a non-model object of a map (PLAN-0006 4.6, D2; SPEC-0006 FR-9, FR-10). It holds the
    // parameters from the map file and does nothing at runtime: the behaviour is a later spec. Ranges are in Unity units
    // (10 Ragnarok units per unit); other values are as stored in the file.

    /// <summary>An effect emitter of the map: preset id, emission delay and the four launch parameters.</summary>
    [DisallowMultipleComponent]
    public sealed class MapEffectMarker : MonoBehaviour
    {
        public int preset;
        public float delay;
        public float a, b, c, d;
    }
}
