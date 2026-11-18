namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// The cut of a ground into square chunks of <see cref="ChunkSize"/> cubes (PLAN-0004 4.4, D5): a quad belongs to
    /// the chunk of the cube that owns it. No map-size limit: a larger map just has more chunks.
    /// </summary>
    public static class RoGndChunker
    {
        /// <summary>Cubes per chunk edge: 16 x 16 cubes keep a chunk below 65,536 vertices (16-bit indices).</summary>
        public const int ChunkSize = 16;

        /// <summary>Chunks along an edge of <paramref name="cubes"/> cubes; the last one may be partial.</summary>
        public static int ChunksAlong(int cubes) => (cubes + ChunkSize - 1) / ChunkSize;

        public static (int chunkColumn, int chunkRow) ChunkOf(int column, int row) => (column / ChunkSize, row / ChunkSize);
    }
}
