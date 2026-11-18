using UnityEngine;

namespace Uninarok.Map
{
    /// <summary>
    /// Links a map in a scene to its <see cref="MapData"/> (PLAN-0005 4.3). SPEC-0006 puts it on the map object; the map
    /// data gizmos attach to it. It holds no logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRoot : MonoBehaviour
    {
        [SerializeField] string mapName;
        [SerializeField] MapData mapData;

        public string MapName => mapName;
        public MapData Data => mapData;

        /// <summary>Sets the map name and its data (used by the map converter of PLAN-0006 and by tests).</summary>
        public void Set(string name, MapData data)
        {
            mapName = name;
            mapData = data;
        }
    }
}
