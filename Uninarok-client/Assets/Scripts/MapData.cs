using UnityEngine;

namespace Uninarok.Data
{
    public class MapData : ScriptableObject
    {
        public enum TileType
        {
            Walkable,
            NoWalkable
        }
        
        public int[] TileData { get; private set; }

        [SerializeField] private Vector2 _mapSize;
        [SerializeField] private Texture _mapTex;

        public void SetMapData(int[] tileData, int mapWidth, int mapHeight, Texture gatImage)
        {
            TileData = tileData;
            _mapSize = new Vector2(mapWidth, mapHeight);
            _mapTex = gatImage;
        }
    }
}