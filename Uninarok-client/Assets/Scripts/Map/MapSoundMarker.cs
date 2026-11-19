using UnityEngine;

namespace Uninarok.Map
{
    // Data-only marker for a non-model object of a map (PLAN-0006 4.6, D2; SPEC-0006 FR-9, FR-10). It holds the
    // parameters from the map file and does nothing at runtime: the behaviour is a later spec. Ranges are in Unity units
    // (10 Ragnarok units per unit); other values are as stored in the file.

    /// <summary>A sound source of the map: wave file, volume, range, repeat interval in seconds and area size.</summary>
    [DisallowMultipleComponent]
    public sealed class MapSoundMarker : MonoBehaviour
    {
        public string file = string.Empty;
        public float volume;
        public float range;
        public float interval;
        public int width, height;
    }
}
