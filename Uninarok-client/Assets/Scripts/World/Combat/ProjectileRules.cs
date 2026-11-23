using System;

namespace Uninarok.World
{
    /// <summary>
    /// Which blows have a projectile, and when it flies (SPEC-0011 FR-13, FR-14; PLAN-0011 4.7). Lead: roBrowserLegacy
    /// <c>src/DB/Monsters/AttackEffectTable.js</c>, <c>src/Renderer/Effects/ArrowFlight.js</c> and <c>Engine/MapEngine/Entity.js</c>
    /// (read 2026-11-23); the rows are in <c>combat-reference.md</c>. Pure.
    /// </summary>
    public static class ProjectileRules
    {
        public const int WeaponTypeBow = 11;
        /// <summary>A motion's frames last 24 ms at an attack time of 432 ms.</summary>
        public const float FrameMs = 24f, MotionMs = 432f;
        public const int DefaultFlightMs = 192;
        public const float DefaultFullCells = 8f;

        // Monsters and mercenaries that shoot an arrow (AttackEffectTable.js, ef_arrow_projectile), sorted, as little-endian pairs.
        static ReadOnlySpan<byte> Archers => new byte[]
        {
            0xF8, 0x03,  0xA5, 0x04,  0xE5, 0x04,  0xEA, 0x04,  0xFC, 0x04,  0x02, 0x05,  0x05, 0x05,  0x8C, 0x05,  0xAD, 0x05,  0xAF, 0x05,
            0xC1, 0x05,  0x29, 0x06,  0x3D, 0x06,  0xBE, 0x07,  0xEF, 0x08,  0xA5, 0x09,  0xE0, 0x0A,  0x0A, 0x0B,  0x0B, 0x0B,  0xDA, 0x0B,
            0x3A, 0x0C,  0x48, 0x0C,  0x7D, 0x0C,  0x6B, 0x0D,  0x75, 0x0D,  0xB3, 0x0E,  0x3C, 0x0F,  0x81, 0x17,  0x82, 0x17,  0x83, 0x17,
            0x84, 0x17,  0x85, 0x17,  0x86, 0x17,  0x87, 0x17,  0x88, 0x17,  0x89, 0x17,  0x8A, 0x17,  0x56, 0x27,  0x90, 0x4F,  0x91, 0x4F,
            0x62, 0x50,  0x12, 0x52,  0x24, 0x52,  0xF0, 0x56,
        };

        // Monsters with a projectile of their own (parasite, stone and wootan shooters, peach tree, hermit, ragged zombie, poton
        // canons, lady tanee), sorted: launched and timed here, drawn by SPEC-0013.
        static ReadOnlySpan<byte> Others => new byte[]
        {
            0x82, 0x05,  0x84, 0x05,  0xD7, 0x05,  0xDA, 0x05,  0xDC, 0x05,  0xFB, 0x05,  0x0E, 0x06,  0x13, 0x06,  0x80, 0x06,  0x81, 0x06,
            0x82, 0x06,  0x83, 0x06,  0x93, 0x06,  0x98, 0x06,  0xBC, 0x06,  0x49, 0x07,  0xA0, 0x0A,  0xD7, 0x0A,  0x77, 0x0B,  0xD8, 0x0C,
            0x0E, 0x4F,  0x32, 0x4F,  0x33, 0x4F,  0x37, 0x4F,  0x93, 0x4F,  0x07, 0x52,  0x0D, 0x52,  0x1F, 0x52,  0x46, 0x54,
        };

        /// <summary>
        /// Players: an arrow for the bow types (every bow's base type is the bow). Monsters and mercenaries: by job id. Everything
        /// else, guns, throwing weapons, whips and instruments included: none (until T0.3 says otherwise).
        /// </summary>
        public static ProjectileKind Of(EntityKind kind, int job, int weaponType)
        {
            switch (kind)
            {
                case EntityKind.Player:
                    return weaponType == WeaponTypeBow ? ProjectileKind.Arrow : ProjectileKind.None;
                case EntityKind.Monster:
                case EntityKind.Mercenary:
                    if (job < 0 || job > ushort.MaxValue) return ProjectileKind.None;
                    if (Contains(Archers, job)) return ProjectileKind.Arrow;
                    return Contains(Others, job) ? ProjectileKind.Other : ProjectileKind.None;
                default:
                    return ProjectileKind.None;
            }
        }

        // Job ids as little-endian pairs (byte data needs no allocation).
        static bool Contains(ReadOnlySpan<byte> pairs, int job)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                if ((pairs[i] | (pairs[i + 1] << 8)) == job) return true;
            return false;
        }

        /// <summary><c>192 ms × min(1, cells / 8)</c>, at least 1 ms.</summary>
        public static int FlightMs(float cells) => FlightMs(cells, DefaultFlightMs, DefaultFullCells);

        public static int FlightMs(float cells, int flightMs, float fullCells)
        {
            if (float.IsNaN(cells) || cells < 0f) cells = 0f;
            float share = fullCells > 0f ? Math.Min(1f, cells / fullCells) : 1f;
            return Math.Max(1, (int)Math.Round(flightMs * share));
        }

        /// <summary>
        /// Player: launched at the hit frame (<c>hitFrame × 24 × attackMT / 432</c> after the blow), the number waits for the arrival.
        /// Monster: launched at the hit moment (<c>attackMT</c>), the number does not wait.
        /// </summary>
        public static void Times(in Blow blow, bool player, float cells, float hitFrame, out uint launches, out uint arrives, out bool numberWaits) =>
            Times(blow, player, cells, hitFrame, DefaultFlightMs, DefaultFullCells, out launches, out arrives, out numberWaits);

        public static void Times(in Blow blow, bool player, float cells, float hitFrame, int flightMs, float fullCells,
                                 out uint launches, out uint arrives, out bool numberWaits)
        {
            int attackMs = Math.Max(0, blow.AttackMs);
            int launch = player ? (int)Math.Round(hitFrame * FrameMs * attackMs / MotionMs) : attackMs;
            launches = unchecked(blow.At + (uint)launch);
            arrives = unchecked(launches + (uint)FlightMs(cells, flightMs, fullCells));
            numberWaits = player;
        }
    }
}
