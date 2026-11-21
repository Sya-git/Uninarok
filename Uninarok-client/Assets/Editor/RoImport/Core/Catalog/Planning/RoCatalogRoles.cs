using System;
using System.Globalization;
using Uninarok.Catalog;
using Uninarok.Networking.Data;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>The record keys of PLAN-0008 §3 and 4.3.11: stable ASCII names built from ids.</summary>
    public static class RoCatalogKeys
    {
        public static string Body(PlayerJob baseJob) => "body-" + ((int)baseJob).ToString("D4", CultureInfo.InvariantCulture);
        public static string Head(int style) => "head-" + style.ToString("D2", CultureInfo.InvariantCulture);
        public static string Headgear(int viewId) => "headgear-" + viewId.ToString("D4", CultureInfo.InvariantCulture);
        public static string Garment(int viewId) => "garment-" + viewId.ToString("D4", CultureInfo.InvariantCulture);
        public static string Weapon(int viewId) => "weapon-" + viewId.ToString("D4", CultureInfo.InvariantCulture);
        public static string WeaponItem(int itemId) => "weapon-item-" + itemId.ToString(CultureInfo.InvariantCulture);
        public static string Shield(int viewId) => "shield-" + viewId.ToString("D4", CultureInfo.InvariantCulture);
        public static string ShieldItem(int itemId) => "shield-item-" + itemId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The pair roles and value names of PLAN-0008 4.3.11: a role names the slot a pair fills, so the entry writer
    /// (PLAN-0008 4.6) can build the slots from the plan alone.
    /// </summary>
    public static class RoCatalogRoles
    {
        public const string Any = "any";
        public const string Trail = "trail";
        public const string PalettePrefix = "palette:";
        public const string NamePrefix = "name:";
        public const string FallbackPrefix = "fallback:";

        /// <summary>The client's sex folder or file marker (<c>남</c>, <c>여</c>).</summary>
        public static string SexMarker(AccountSex sex) => sex == AccountSex.SEX_FEMALE ? "여" : "남";

        public static string Sex(AccountSex sex) => sex == AccountSex.SEX_FEMALE ? "female" : "male";
        public static string Race(SpriteRace race) => race == SpriteRace.Doram ? "doram" : "human";

        public static string Body(PlayerJob job, AccountSex sex, SpriteRace race, BodyVariantKind kind) => $"body:{job}:{Sex(sex)}:{Race(race)}:{kind}";
        public static string Head(AccountSex sex, SpriteRace race) => $"head:{Sex(sex)}:{Race(race)}";
        public static string Headgear(AccountSex sex, SpriteRace race) => $"headgear:{Any}:{Sex(sex)}:{Race(race)}";
        public static string Garment(PlayerJob job, AccountSex sex, SpriteRace race) => $"garment:{job}:{Sex(sex)}:{Race(race)}";
        public static string GarmentRoot(SpriteRace race) => $"garment:{Any}:{Any}:{Race(race)}";
        public static string Weapon(PlayerJob job, AccountSex sex, SpriteRace race) => $"weapon:{job}:{Sex(sex)}:{Race(race)}";
        public static string WeaponTrail(PlayerJob job, AccountSex sex, SpriteRace race) => $"{Trail}:{job}:{Sex(sex)}:{Race(race)}";
        public static string Shield(PlayerJob job, AccountSex sex, SpriteRace race) => $"shield:{job}:{Sex(sex)}:{Race(race)}";

        public static string Palette(string role, int colour) => PalettePrefix + role + ":" + colour.ToString(CultureInfo.InvariantCulture);
        public static string Name(string role) => NamePrefix + role;
        public static string Fallback(string role) => FallbackPrefix + role;

        /// <summary>The slot fields of a role (<c>kind:job-or-any:sex-or-any:race[:extra]</c>).</summary>
        public static bool TryParse(string role, out string kind, out string job, out string sex, out string race, out string extra)
        {
            kind = job = sex = race = extra = null;
            var parts = (role ?? string.Empty).Split(':');
            if (parts.Length == 3 && parts[0] == "head")
            {
                kind = parts[0];
                job = Any;
                sex = parts[1];
                race = parts[2];
                return true;
            }
            if (parts.Length < 4 || parts.Length > 5) return false;
            kind = parts[0];
            job = parts[1];
            sex = parts[2];
            race = parts[3];
            extra = parts.Length == 5 ? parts[4] : null;
            return true;
        }
    }
}
