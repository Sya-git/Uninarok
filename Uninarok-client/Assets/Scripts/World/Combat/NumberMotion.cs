using System;
using UnityEngine;

namespace Uninarok.World
{
    /// <summary>Where a number is at a moment of its life: offset in cells (map x and y), height in cells, scale, alpha.</summary>
    public struct NumberPose
    {
        public float X, Y, Height, Scale, Alpha;
    }

    /// <summary>
    /// How a number or a message moves over its life (SPEC-0011 FR-6; PLAN-0011 4.8). Pure. The values are the settings', each a
    /// row of <c>combat-reference.md</c> (lead: roBrowserLegacy <c>Damage.js</c>).
    /// </summary>
    public static class NumberMotion
    {
        /// <summary>The colour of a number (FR-6, D28): white over a monster, red over a player, the total yellow-green, a critical its own.</summary>
        public static Color ColourOf(NumberKind kind, bool onPlayer, CombatSettings settings)
        {
            if (kind == NumberKind.Critical) return settings.NumberCritical;
            if (kind == NumberKind.Total) return settings.NumberTotal;
            return onPlayer ? settings.NumberOnPlayer : settings.NumberOnMonster;
        }

        /// <summary>The life of a number of this kind, in ms.</summary>
        public static int LifeMs(NumberKind kind, CombatSettings settings)
        {
            switch (kind)
            {
                case NumberKind.Miss: return settings.MessageMs;
                case NumberKind.Lucky: return settings.LuckyMs;
                case NumberKind.Total: return settings.TotalMs;
                default: return settings.NumberMs;
            }
        }

        /// <summary>The pose at <paramref name="t"/>, 0 (start) to 1 (end of its life).</summary>
        public static NumberPose At(NumberKind kind, float t, CombatSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var pose = new NumberPose { Alpha = 1f - t };
            switch (kind)
            {
                case NumberKind.Miss:
                    // Footage A (D31): a constant rise, no drift, constant size.
                    pose.Height = settings.MessageHeightStart + (settings.MessageHeightEnd - settings.MessageHeightStart) * t;
                    pose.Scale = settings.MessageScale;
                    break;
                case NumberKind.Lucky:
                    pose.Height = settings.LuckyHeightStart + (settings.LuckyHeightEnd - settings.LuckyHeightStart) * t;
                    pose.Scale = settings.MessageScale;
                    break;
                case NumberKind.Total:
                    // Footage C (D32): each update grows quickly, then stays.
                    pose.Height = settings.TotalHeight;
                    float grow = Share(t * settings.TotalMs, settings.TotalGrowMs);
                    pose.Scale = settings.TotalScaleStart + (settings.TotalScale - settings.TotalScaleStart) * grow;
                    break;
                default:
                    // Footage A: x decelerates; the scale goes start -> end smooth-step over NumberShrinkMs and stays there.
                    pose.X = settings.NumberDriftX * (float)Math.Sin(Math.PI / 2 * t);
                    pose.Y = -settings.NumberDriftY * t;
                    pose.Height = settings.NumberHeightBase + settings.NumberHeightAmplitude * (float)Math.Sin(-Math.PI / 2 + Math.PI * (0.5 + 1.5 * t));
                    float s = Share(t * settings.NumberMs, settings.NumberShrinkMs);
                    pose.Scale = settings.NumberScaleStart + (settings.NumberScaleEnd - settings.NumberScaleStart) * (s * s * (3f - 2f * s));
                    break;
            }
            return pose;
        }

        // How far through a phase of durationMs an age is, 0 to 1; a phase of 0 or less is over at once.
        static float Share(float ageMs, int durationMs) => durationMs <= 0 ? 1f : Math.Min(1f, Math.Max(0f, ageMs / durationMs));
    }
}
