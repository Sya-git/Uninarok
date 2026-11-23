using System;
using Uninarok.Networking.Data;
using Uninarok.Units;

namespace Uninarok.World.View
{
    /// <summary>The server's look ids to a unit look (PLAN-0010 4.9).</summary>
    public static class EntityLookMapping
    {
        /// <summary>Job ids that no sprite belongs to (<c>FAKE_NPC</c>, <c>INVISIBLE_CLASS</c>): nothing is drawn, and nothing is missing.</summary>
        public static bool IsInvisible(in EntityLook look) => look.Job < 0 || look.Job == 32767;

        // The server sends the view id, or the item id of an item without one (T0.2). Weapon and shield views end at 102 and 4, items
        // start at 501; by item, the catalog also finds the sprite of its type (D22).
        static bool IsItem(int value) => WeaponHands.IsItemId(value);

        // The client numbers the first hair styles in its own order (D27): the head file of each style id, by sex. The ids after are
        // the file numbers, as all of a doram's.
        static ReadOnlySpan<byte> FemaleHeads => new byte[] { 2, 2, 4, 7, 1, 5, 3, 6, 12, 10, 9, 11, 8 };
        static ReadOnlySpan<byte> MaleHeads => new byte[] { 2, 2, 1, 7, 5, 4, 3, 6, 8, 9, 10, 12, 11 };

        /// <summary>The head file number (the catalog's hair style) of the server's hair style.</summary>
        internal static int HeadOf(int job, byte sex, int style)
        {
            if (job == (int)PlayerJob.JOB_SUMMONER || job == (int)PlayerJob.JOB_BABY_SUMMONER) return Math.Max(1, style);
            var heads = sex == (byte)AccountSex.SEX_FEMALE ? FemaleHeads : MaleHeads;
            return style < heads.Length ? heads[style] : style;
        }

        /// <summary>
        /// A player's parts when the job id is a player class (players, and any kind that looks like one), else one sprite by job id:
        /// monsters, NPCs, pets, and players disguised as one.
        /// </summary>
        public static UnitLook ToUnitLook(EntityKind kind, in EntityLook look)
        {
            if (!UnitLook.IsPlayerJob(look.Job)) return UnitLook.ForJob(look.Job);
            return new UnitLook
            {
                Kind = UnitKind.Player,
                JobId = look.Job,
                Sex = look.Sex == (byte)AccountSex.SEX_FEMALE ? AccountSex.SEX_FEMALE : AccountSex.SEX_MALE,
                BodyStyle = look.BodyStyle,
                HairStyle = HeadOf(look.Job, look.Sex, look.HairStyle),
                HairColour = look.HairColour,
                ClothColour = look.ClothColour,
                HeadTop = look.HeadTop,
                HeadMid = look.HeadMid,
                HeadBottom = look.HeadBottom,
                Garment = look.Robe,
                Weapon = IsItem(look.Weapon) ? 0 : look.Weapon,
                WeaponItem = IsItem(look.Weapon) ? look.Weapon : 0,
                Shield = IsItem(look.Shield) ? 0 : look.Shield,
                ShieldItem = IsItem(look.Shield) ? look.Shield : 0,
            };
        }
    }
}
