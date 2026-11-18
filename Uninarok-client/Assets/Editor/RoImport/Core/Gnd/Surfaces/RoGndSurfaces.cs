using System;
using System.Collections.Generic;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// The top and wall surfaces of a ground (PLAN-0004 4.3), in Ragnarok space, in cube index order (top, front wall,
    /// side wall per cube):
    /// <list type="bullet">
    /// <item>a top where the cube has a top tile (FR-1); none is a gap;</item>
    /// <item>a wall only where the cube has the wall tile, a neighbour exists toward it and the heights at the shared
    /// edge differ by more than <see cref="RoGndRules.Epsilon"/> at one end or both (FR-2); the last row and column have
    /// no neighbour and are skipped without reading outside the grid and without a warning.</item>
    /// </list>
    /// Every corner comes from <see cref="RoMapSpace.CornerRo"/> with the height of the cube that owns it, so a shared
    /// corner always has the same bit pattern (FR-3, D3).
    /// </summary>
    public static class RoGndSurfaces
    {
        public static IEnumerable<RoGndQuad> Enumerate(RoGnd gnd, RoGndRules rules, RoReport report)
        {
            if (gnd == null) throw new ArgumentNullException(nameof(gnd));
            return EnumerateCubes(gnd);
        }

        static IEnumerable<RoGndQuad> EnumerateCubes(RoGnd gnd)
        {
            var space = new RoMapSpace(gnd.Width, gnd.Height, gnd.Zoom);
            for (int row = 0; row < gnd.Height; row++)
            for (int column = 0; column < gnd.Width; column++)
            {
                var cube = gnd.CubeAt(column, row);

                if (cube.TileUp >= 0)
                    yield return new RoGndQuad(RoGndFace.Top, column, row, cube.TileUp,
                        space.CornerRo(column, row, cube.H00), space.CornerRo(column + 1, row, cube.H10),
                        space.CornerRo(column, row + 1, cube.H01), space.CornerRo(column + 1, row + 1, cube.H11));

                if (cube.TileFront >= 0 && row + 1 < gnd.Height)
                {
                    var next = gnd.CubeAt(column, row + 1);
                    if (Differs(cube.H01, next.H00) || Differs(cube.H11, next.H10))
                        yield return new RoGndQuad(RoGndFace.FrontWall, column, row, cube.TileFront,
                            space.CornerRo(column, row + 1, cube.H01), space.CornerRo(column + 1, row + 1, cube.H11),
                            space.CornerRo(column, row + 1, next.H00), space.CornerRo(column + 1, row + 1, next.H10));
                }

                if (cube.TileSide >= 0 && column + 1 < gnd.Width)
                {
                    var next = gnd.CubeAt(column + 1, row);
                    if (Differs(cube.H10, next.H00) || Differs(cube.H11, next.H01))
                        yield return new RoGndQuad(RoGndFace.SideWall, column, row, cube.TileSide,
                            space.CornerRo(column + 1, row, cube.H10), space.CornerRo(column + 1, row + 1, cube.H11),
                            space.CornerRo(column + 1, row, next.H00), space.CornerRo(column + 1, row + 1, next.H01));
                }
            }
        }

        static bool Differs(float a, float b) => Math.Abs(a - b) > RoGndRules.Epsilon;
    }
}
