using UnityEngine;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// A surface (tile) as stored in the file (PLAN-0004 4.1): four texture coordinates, named by the grid corner they
    /// belong to (<c>Uv00</c> at the cube's <c>H00</c> corner and so on), and the index into <see cref="RoGnd.Textures"/>.
    /// The coordinates are unconverted (the file's V axis points down).
    /// </summary>
    public readonly struct RoGndTile
    {
        public readonly Vector2 Uv00, Uv10, Uv01, Uv11;
        public readonly int Texture;

        public RoGndTile(Vector2 uv00, Vector2 uv10, Vector2 uv01, Vector2 uv11, int texture)
        {
            Uv00 = uv00;
            Uv10 = uv10;
            Uv01 = uv01;
            Uv11 = uv11;
            Texture = texture;
        }
    }
}
