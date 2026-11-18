using UnityEngine;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>The kind of a ground face (PLAN-0004 4.3): a top, a front wall (the file's north wall) or a side wall (east).</summary>
    public enum RoGndFace { Top, FrontWall, SideWall }

    /// <summary>
    /// One ground surface in Ragnarok space (PLAN-0004 4.3), owned by the cube at (<see cref="Column"/>, <see cref="Row"/>)
    /// and drawn with <see cref="Tile"/>. Corner <c>Pij</c> takes the tile's <c>Uvij</c>.
    /// <list type="bullet">
    /// <item>Top: <c>P00..P11</c> are the cube's corners <c>H00..H11</c> at their grid corners.</item>
    /// <item>Front wall: <c>P00</c>, <c>P10</c> are this cube's <c>H01</c>, <c>H11</c>; <c>P01</c>, <c>P11</c> the next row's
    /// <c>H00</c>, <c>H10</c>, all on the row edge <c>Row + 1</c>.</item>
    /// <item>Side wall: <c>P00</c>, <c>P10</c> are this cube's <c>H10</c>, <c>H11</c>; <c>P01</c>, <c>P11</c> the next column's
    /// <c>H00</c>, <c>H01</c>, all on the column edge <c>Column + 1</c>.</item>
    /// </list>
    /// A wall whose heights are equal at one end has two equal corners: it is a triangle.
    /// </summary>
    public readonly struct RoGndQuad
    {
        public readonly RoGndFace Face;
        public readonly int Column, Row;
        public readonly int Tile;
        public readonly Vector3 P00, P10, P01, P11;

        public RoGndQuad(RoGndFace face, int column, int row, int tile, Vector3 p00, Vector3 p10, Vector3 p01, Vector3 p11)
        {
            Face = face;
            Column = column;
            Row = row;
            Tile = tile;
            P00 = p00;
            P10 = p10;
            P01 = p01;
            P11 = p11;
        }

        public override string ToString() => $"{Face} ({Column},{Row}) tile {Tile}: {P00} {P10} {P01} {P11}";
    }
}
