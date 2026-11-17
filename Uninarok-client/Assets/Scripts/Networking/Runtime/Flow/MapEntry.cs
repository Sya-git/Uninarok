using Uninarok.Networking.Data;

namespace Uninarok.Networking.Flow
{
    /// <summary>What the map server reported when it accepted the entry (AUTHENTICATION_ACCEPTED, plan 4.9).</summary>
    public readonly struct MapEntry
    {
        public readonly uint StartTick;
        public readonly UninarokPosition Position;
        public readonly byte XSize;
        public readonly byte YSize;

        public MapEntry(uint startTick, in UninarokPosition position, byte xSize, byte ySize)
        {
            StartTick = startTick;
            Position = position;
            XSize = xSize;
            YSize = ySize;
        }
    }
}
