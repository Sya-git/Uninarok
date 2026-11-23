using System;
using Uninarok.Networking.Data;

namespace Uninarok.World
{
    /// <summary>What an entity is (PLAN-0010 4.5; <c>objecttype</c> of the entry packets).</summary>
    public enum EntityKind : byte
    {
        Player,
        Npc,
        Monster,
        Pet,
        Homunculus,
        Mercenary,
        Elemental,
        Item,
        Other,
    }

    public enum EntityState : byte
    {
        Standing,
        Sitting,
        Dead,
    }

    /// <summary>Why a unit disappears: the wire values of <c>UNIT_VANISH</c> (SPEC-0010 FR-5).</summary>
    public enum VanishReason : byte
    {
        OutOfSight = 0,
        Died = 1,
        LoggedOut = 2,
        Teleported = 3,
        FeignedDeath = 4,
    }

    /// <summary>The head relative to the body: the wire values of <c>UNIT_DIRECTION</c> and <c>CHANGE_DIRECTION</c>.</summary>
    public enum HeadDirection : byte
    {
        Straight = 0,
        Right = 1,
        Left = 2,
    }

    /// <summary>What a look change changes: <c>enum look</c> (UNIT_LOOK_CHANGE.md).</summary>
    public enum LookType : byte
    {
        Base = 0,
        Hair = 1,
        Weapon = 2,
        HeadBottom = 3,
        HeadTop = 4,
        HeadMid = 5,
        HairColour = 6,
        ClothColour = 7,
        Shield = 8,
        Robe = 12,
        BodyStyle = 13,
    }

    /// <summary>How a unit looks, as the server's ids (PLAN-0010 4.5); the view maps them (§4.9).</summary>
    public struct EntityLook : IEquatable<EntityLook>
    {
        public short Job;
        public ushort HairStyle, HairColour, ClothColour, BodyStyle;
        /// <summary>View ids, or item ids for items without one.</summary>
        public int Weapon, Shield;
        public ushort HeadTop, HeadMid, HeadBottom, Robe;
        public byte Sex;

        public readonly bool Equals(EntityLook o) =>
            Job == o.Job && HairStyle == o.HairStyle && HairColour == o.HairColour && ClothColour == o.ClothColour && BodyStyle == o.BodyStyle
            && Weapon == o.Weapon && Shield == o.Shield && HeadTop == o.HeadTop && HeadMid == o.HeadMid && HeadBottom == o.HeadBottom
            && Robe == o.Robe && Sex == o.Sex;

        public override readonly bool Equals(object obj) => obj is EntityLook o && Equals(o);

        public override readonly int GetHashCode() => unchecked((((Job * 397) ^ HairStyle) * 397 ^ Weapon) * 397 ^ HeadTop);
    }

    /// <summary>One entity of the world model (PLAN-0010 4.5). Views read it; only <see cref="WorldModel"/> writes it.</summary>
    public struct Entity
    {
        /// <summary>The server's id; 0: a free slot.</summary>
        public uint Id;
        public EntityKind Kind;
        public EntityState State;
        public EntityLook Look;

        /// <summary>Items.</summary>
        public int ItemId, ItemAmount;
        /// <summary>Items.</summary>
        public bool Identified;
        /// <summary>Items: how high above the ground it is, in cells; 0 once it fell (PLAN-0010 D37).</summary>
        public float Height;
        internal bool Falling;
        internal uint FallStarts;

        /// <summary>Milliseconds per straight cell.</summary>
        public short Speed;
        /// <summary>The server's cell: it changes when a step ends (PLAN-0010 D4).</summary>
        public int CellX, CellY;
        /// <summary>The shown position in cells; a whole number is the centre of a cell.</summary>
        public float PosX, PosY;
        public UninarokDirection Facing;
        public HeadDirection Head;

        public bool Walking;
        /// <summary>The step being walked (an index into the path) and when it starts and ends, in server ticks.</summary>
        public int StepIndex;
        public uint StepStarts, StepEnds;
        /// <summary>It was walking when this walk was ordered: the first step runs in from before its start (PLAN-0010 D36).</summary>
        internal bool RunsIn;
        /// <summary>How far it was shown from where the walk had it when it was ordered; gone at <see cref="OffEnds"/> (D36).</summary>
        internal float OffX, OffY;
        internal uint OffStarts, OffEnds;

        /// <summary>HP and max HP when the server gave them; −1: not known (SPEC-0011 FR-1). Death is never taken from them.</summary>
        public int Hp, MaxHp;
        /// <summary>Who it attacks; 0: nobody (SPEC-0011 FR-9).</summary>
        public uint TargetId;
        /// <summary>The last blow it struck.</summary>
        public Blow LastBlow;
        /// <summary>Dead by <see cref="VanishReason.FeignedDeath"/> (Trick Dead), not by a death.</summary>
        public bool Feigning;

        /// <summary>A dead unit that is removed at <see cref="RemoveAt"/>.</summary>
        internal bool Removing;
        internal uint RemoveAt;

        /// <summary>Changes when anything but a walking position changed.</summary>
        public int Version;
        /// <summary>Changes when the animation must be played again: a walk starts or ends, sits, stands, dies.</summary>
        public int AnimationVersion;
        /// <summary>Changes when the slot gets another entity (PLAN-0010 D19).</summary>
        public int Generation;

        public readonly bool IsItem => Kind == EntityKind.Item;
    }

    /// <summary>A disappearance (SPEC-0010 FR-5): the effects spec attaches its visual to it.</summary>
    public readonly struct VanishInfo
    {
        public readonly uint Id;
        public readonly EntityKind Kind;
        public readonly VanishReason Reason;
        public readonly int X, Y;

        public VanishInfo(uint id, EntityKind kind, VanishReason reason, int x, int y)
        {
            Id = id;
            Kind = kind;
            Reason = reason;
            X = x;
            Y = y;
        }
    }
}
