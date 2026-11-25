using System;

namespace Uninarok.Effects.Editor
{
    /// <summary>
    /// Pure arithmetic of the effect preview window (SPEC-0012 FR-11, plan 4.11): frame steps, speed clamp and the visible
    /// range of the virtualised list. No Unity state, so EditMode tests cover it without a window.
    /// </summary>
    public static class EffectPreviewMath
    {
        public const float MinSpeed = 0.1f;
        public const float MaxSpeed = 4f;
        const float FrameEpsilon = 1e-3f;

        /// <summary>The speed field's value clamped to 0.1..4; NaN or infinity gives 1.</summary>
        public static float ClampSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed)) return 1f;
            return Math.Min(MaxSpeed, Math.Max(MinSpeed, speed));
        }

        /// <summary>The frame index shown for a fractional frame (floor, with a tolerance for float noise: 2.9999 shows 3).</summary>
        public static int ShownFrame(float frame)
        {
            if (float.IsNaN(frame) || frame < 0f) return 0;
            return (int)Math.Floor(frame + FrameEpsilon);
        }

        /// <summary>
        /// The last frame a cycle shows: a one-shot reaches <c>length x fps</c> (its end), a loop wraps there so its last frame is
        /// the one before. Never below 0.
        /// </summary>
        public static int LastFrame(float length, float frameRate, bool loop)
        {
            if (!(length > 0f) || !(frameRate > 0f) || float.IsInfinity(length) || float.IsInfinity(frameRate)) return 0;
            double frames = (double)length * frameRate;
            int last = loop ? (int)Math.Ceiling(frames - FrameEpsilon) - 1 : (int)Math.Floor(frames + FrameEpsilon);
            return Math.Max(0, last);
        }

        /// <summary>The frames in one cycle (<c>length x fps</c>, rounded up with float tolerance): 70 for an STR of 70 frames. 0 when unknown.</summary>
        public static int FrameCount(float length, float frameRate)
        {
            if (!(length > 0f) || !(frameRate > 0f) || float.IsInfinity(length) || float.IsInfinity(frameRate)) return 0;
            return (int)Math.Ceiling((double)length * frameRate - FrameEpsilon);
        }

        /// <summary>The scrubber's frame text in the GRF Editor form <c>frame n / total</c>: n is the 0-based shown frame, total the frame count.</summary>
        public static string FrameLabel(int shownFrame, float length, float frameRate) => $"frame {shownFrame} / {FrameCount(length, frameRate)}";

        /// <summary>
        /// The frame a step of <paramref name="delta"/> frames reaches, clamped to [0, <paramref name="lastFrame"/>]. From a
        /// fractional frame a forward step goes to the next whole frame and a backward step to the whole frame at or before
        /// it, so every step moves the shown frame (<see cref="ShownFrame"/>) by one while inside the range.
        /// </summary>
        public static int StepFrame(float frame, int delta, int lastFrame)
        {
            if (float.IsNaN(frame) || frame < 0f) frame = 0f;
            int from = delta >= 0
                ? (int)Math.Floor(frame + FrameEpsilon)
                : (int)Math.Ceiling(frame - FrameEpsilon);
            long target = (long)from + delta;
            if (target < 0) target = 0;
            if (target > lastFrame) target = Math.Max(0, lastFrame);
            return (int)target;
        }

        public static float FrameToSeconds(float frame, float frameRate) => frameRate > 0f ? frame / frameRate : 0f;

        /// <summary>
        /// The rows <c>[first, endExclusive)</c> to draw for a scroll offset: those that intersect the view, plus
        /// <paramref name="overscan"/> rows on each side.
        /// </summary>
        public static void VisibleRange(int count, float rowHeight, float scrollY, float viewHeight, int overscan, out int first, out int endExclusive)
        {
            first = 0; endExclusive = 0;
            if (count <= 0 || !(rowHeight > 0f) || !(viewHeight > 0f)) return;
            if (!(scrollY > 0f)) scrollY = 0f;
            int top = (int)Math.Floor(scrollY / rowHeight) - Math.Max(0, overscan);
            int bottom = (int)Math.Ceiling((scrollY + viewHeight) / rowHeight) + Math.Max(0, overscan);
            first = Math.Min(count, Math.Max(0, top));
            endExclusive = Math.Min(count, Math.Max(first, bottom));
        }
    }
}
