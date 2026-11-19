using UnityEngine;

namespace Uninarok.Map
{
    // Data-only marker for a non-model object of a map (PLAN-0006 4.6, D2; SPEC-0006 FR-9, FR-10). It holds the
    // parameters from the map file and does nothing at runtime: the behaviour is a later spec. Ranges are in Unity units
    // (10 Ragnarok units per unit); other values are as stored in the file.

    /// <summary>Stands in for a model the map places but that could not be converted; keeps the instance's place.</summary>
    [DisallowMultipleComponent]
    public sealed class MapMissingModelMarker : MonoBehaviour
    {
        public string modelFile = string.Empty;
    }
}
