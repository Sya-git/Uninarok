using System;
using Uninarok.Networking.Data;

namespace Uninarok.Units
{
    /// <summary>What a look is: a player's parts, or one sprite by job id (monster, NPC, homunculus, mercenary).</summary>
    public enum UnitKind : byte
    {
        Player,
        Unit,
    }

    /// <summary>
    /// How a unit looks, by ids only (PLAN-0009 4.2; SPEC-0009 FR-1, FR-2, FR-9): what the server sends. 0 is "none" for every
    /// equipment id and the sprite's own colours for the palettes.
    /// </summary>
    [Serializable]
    public struct UnitLook : IEquatable<UnitLook>
    {
        public UnitKind Kind;
        /// <summary>A <see cref="PlayerJob"/> value for players, a unit job id otherwise.</summary>
        public int JobId;
        public AccountSex Sex;
        /// <summary>Costume style, 0 = the class body.</summary>
        public int BodyStyle;
        public int HairStyle, HairColour, ClothColour;
        /// <summary>Headgear and garment view ids.</summary>
        public int HeadTop, HeadMid, HeadBottom, Garment;
        /// <summary>
        /// View ids of the two hands as the server sends them (PLAN-0009 D34): <c>Weapon</c> is the right hand, <c>Shield</c> the
        /// left hand, which holds a shield or, for the assassin family, a second weapon (FR-17). Names kept from before.
        /// </summary>
        public int Weapon, Shield;
        /// <summary>Item ids of the right (<c>WeaponItem</c>) and left (<c>ShieldItem</c>) hand, optional, for files the client names by item (PLAN-0008 D10).</summary>
        public int WeaponItem, ShieldItem;

        /// <summary>The right hand's value: the item id when given (it tells the kind), else the view id (PLAN-0009 4.11).</summary>
        public int RightValue => WeaponItem > 0 ? WeaponItem : Weapon;
        /// <summary>The left hand's value: the item id when given, else the view id; a small value is a shield (FR-17).</summary>
        public int LeftValue => ShieldItem > 0 ? ShieldItem : Shield;

        public PlayerJob PlayerJob => (PlayerJob)JobId;

        /// <summary>
        /// The look of a job id (FR-2): a player-class id gives a player with the default look (hair 1, no equipment), any other
        /// id a unit.
        /// </summary>
        public static UnitLook ForJob(int jobId, AccountSex sex = AccountSex.SEX_MALE) =>
            IsPlayerJob(jobId)
                ? new UnitLook { Kind = UnitKind.Player, JobId = jobId, Sex = sex, HairStyle = 1 }
                : new UnitLook { Kind = UnitKind.Unit, JobId = jobId, Sex = sex };

        /// <summary>True for a job id outside the unit ranges (PLAN-0008 §3.1: NPCs from 45, monsters, homunculi): 0–44 and 4001–5999.</summary>
        public static bool IsPlayerJob(int jobId) => (jobId >= 0 && jobId < 45) || (jobId > 4000 && jobId < 6000);

        /// <summary>Display scale of baby classes (PLAN-0009 D24): roBrowser draws them at size 4 where others are 5.</summary>
        public const float BabyScale = 0.8f;

        /// <summary>True for the baby classes: the same sprites as their adult class, smaller.</summary>
        public static bool IsBabyJob(int jobId)
        {
            var job = (PlayerJob)jobId;
            return (job >= PlayerJob.JOB_BABY && job <= PlayerJob.JOB_SUPER_BABY)
                || (job >= PlayerJob.JOB_BABY_RUNE && job <= PlayerJob.JOB_BABY_MECHANIC2)
                || job == PlayerJob.JOB_SUPER_BABY_E
                || job == PlayerJob.JOB_BABY_SUMMONER
                || (job >= PlayerJob.JOB_BABY_NINJA && job <= PlayerJob.JOB_BABY_REBELLION)
                || job == PlayerJob.JOB_BABY_STAR_GLADIATOR2
                || job == PlayerJob.JOB_BABY_STAR_EMPEROR
                || job == PlayerJob.JOB_BABY_SOUL_REAPER;
        }

        public bool Equals(UnitLook o) =>
            Kind == o.Kind && JobId == o.JobId && Sex == o.Sex && BodyStyle == o.BodyStyle && HairStyle == o.HairStyle && HairColour == o.HairColour
            && ClothColour == o.ClothColour && HeadTop == o.HeadTop && HeadMid == o.HeadMid && HeadBottom == o.HeadBottom && Garment == o.Garment
            && Weapon == o.Weapon && Shield == o.Shield && WeaponItem == o.WeaponItem && ShieldItem == o.ShieldItem;

        public override bool Equals(object obj) => obj is UnitLook o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = ((int)Kind * 397) ^ JobId;
                h = (h * 397) ^ (int)Sex;
                h = (h * 397) ^ HairStyle;
                h = (h * 397) ^ HeadTop;
                h = (h * 397) ^ Garment;
                return (h * 397) ^ Weapon;
            }
        }

        public static bool operator ==(in UnitLook a, in UnitLook b) => a.Equals(b);
        public static bool operator !=(in UnitLook a, in UnitLook b) => !a.Equals(b);
    }
}
