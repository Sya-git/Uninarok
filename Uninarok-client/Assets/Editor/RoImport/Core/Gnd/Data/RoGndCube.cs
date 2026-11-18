namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// A cube as stored in the file (PLAN-0004 4.1): four corner heights in Ragnarok units, unconverted (Y points down, so
    /// higher ground has a lower number), and three tile ids. <c>H00</c> is the corner at the low column and low row,
    /// <c>H10</c> the next column, <c>H01</c> the next row, <c>H11</c> both. <see cref="TileFront"/> is the file's north
    /// wall (toward the next row), <see cref="TileSide"/> its east wall (toward the next column). A tile id of
    /// <see cref="None"/> means no surface; <c>0</c> is a valid tile, so every test is <c>&gt;= 0</c>.
    /// </summary>
    public readonly struct RoGndCube
    {
        public const int None = -1;

        public readonly float H00, H10, H01, H11;
        public readonly int TileUp, TileFront, TileSide;

        public RoGndCube(float h00, float h10, float h01, float h11, int tileUp = None, int tileFront = None, int tileSide = None)
        {
            H00 = h00;
            H10 = h10;
            H01 = h01;
            H11 = h11;
            TileUp = tileUp;
            TileFront = tileFront;
            TileSide = tileSide;
        }
    }
}
