using System;
using Uninarok.Networking.Data;

namespace Uninarok.Units.Editor
{
    /// <summary>One hand or headgear slot as typed in the composer: a view id or an item id.</summary>
    [Serializable]
    public struct PresetHand
    {
        public bool ByItem;
        public int Id;
    }

    /// <summary>A named composer look (PLAN-0009 4.12, FR-18). Not held: camera, frame, play state, head facing, speed, view toggles.</summary>
    [Serializable]
    public sealed class UnitComposerPreset
    {
        public string Name;
        public UnitKind Kind;
        public int UnitJob;
        public int Class;
        public AccountSex Sex;
        public int BodyStyle, HairStyle, HairColour, ClothColour;
        public PresetHand Top, Mid, Low, Garment, Right, Left;
        public string Animation;
        public PlayMode Mode;
        public UninarokDirection Facing;
    }
}
