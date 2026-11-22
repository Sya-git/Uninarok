namespace Uninarok.Units
{
    /// <summary>The animations of player sprites (docs/data/SpriteAction.md).</summary>
    public enum PcAnimation : byte
    {
        Idle = 0,
        Walk = 1,
        Sit = 2,
        PickUp = 3,
        Standby = 4,
        Attack1 = 5,
        Hurt = 6,
        Freeze1 = 7,
        Dead = 8,
        Freeze2 = 9,
        Attack2 = 10,
        Attack3 = 11,
        Cast = 12,
    }

    /// <summary>The animations of monsters, NPCs, homunculi and mercenaries (docs/data/SpriteAction.md).</summary>
    public enum MonsterAnimation : byte
    {
        Idle = 0,
        Walk = 1,
        Attack = 2,
        Hurt = 3,
        Die = 4,
        Extra5 = 5,
        Extra6 = 6,
        Extra7 = 7,
        Extra8 = 8,
    }

    /// <summary>How an animation plays (PLAN-0009 4.4; SPEC-0009 FR-7).</summary>
    public enum PlayMode : byte
    {
        /// <summary>Over and over.</summary>
        Loop,
        /// <summary>Once, then the last frame stays.</summary>
        OnceHold,
        /// <summary>Once, then the end is reported; the last frame stays until something else plays.</summary>
        OnceEnd,
    }

    /// <summary>The head of a player relative to its body while idle or sitting (PLAN-0009 4.3; SPEC-0009 FR-8): the frame of those animations.</summary>
    public enum HeadFacing : byte
    {
        Straight = 0,
        Right = 1,
        Left = 2,
    }

    /// <summary>The same animation in the other family (PLAN-0009 4.4; SPEC-0009 FR-7): by name where one exists, else idle.</summary>
    public static class UnitAnimations
    {
        public static MonsterAnimation ToUnit(PcAnimation a)
        {
            switch (a)
            {
                case PcAnimation.Walk: return MonsterAnimation.Walk;
                case PcAnimation.Attack1:
                case PcAnimation.Attack2:
                case PcAnimation.Attack3: return MonsterAnimation.Attack;
                case PcAnimation.Hurt: return MonsterAnimation.Hurt;
                case PcAnimation.Dead: return MonsterAnimation.Die;
                default: return MonsterAnimation.Idle;
            }
        }

        public static PcAnimation ToPlayer(MonsterAnimation a)
        {
            switch (a)
            {
                case MonsterAnimation.Walk: return PcAnimation.Walk;
                case MonsterAnimation.Attack: return PcAnimation.Attack1;
                case MonsterAnimation.Hurt: return PcAnimation.Hurt;
                case MonsterAnimation.Die: return PcAnimation.Dead;
                default: return PcAnimation.Idle;
            }
        }

        /// <summary>The player animations whose frame is the head facing (PLAN-0009 D9).</summary>
        public static bool IsHeadFrame(int pcAnimation) => pcAnimation == (int)PcAnimation.Idle || pcAnimation == (int)PcAnimation.Sit;
    }
}
