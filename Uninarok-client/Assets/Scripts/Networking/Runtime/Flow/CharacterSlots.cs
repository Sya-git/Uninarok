namespace Uninarok.Networking.Flow
{
    /// <summary>Character-slot counts reported by the character server (CHARACTER_SLOTS_INFO).</summary>
    public readonly struct CharacterSlots
    {
        public readonly byte CharSlots;
        public readonly byte AvailableCharSlots;
        public readonly byte CharSlots2;
        public readonly byte CharSlots3;

        public CharacterSlots(byte charSlots, byte availableCharSlots, byte charSlots2, byte charSlots3)
        {
            CharSlots = charSlots;
            AvailableCharSlots = availableCharSlots;
            CharSlots2 = charSlots2;
            CharSlots3 = charSlots3;
        }
    }
}
