using Unity.Collections;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Flow
{
    /// <summary>The values the player chose for a new character (CREATE_CHARACTER, plan 4.9).</summary>
    public readonly struct CharacterDraft
    {
        public readonly FixedString32Bytes Name;
        public readonly byte Slot;
        public readonly ushort HairColor;
        public readonly ushort HairStyle;
        public readonly uint JobClass;
        public readonly AccountSex Sex;

        public CharacterDraft(in FixedString32Bytes name, byte slot, ushort hairColor, ushort hairStyle, uint jobClass, AccountSex sex)
        {
            Name = name;
            Slot = slot;
            HairColor = hairColor;
            HairStyle = hairStyle;
            JobClass = jobClass;
            Sex = sex;
        }
    }
}
