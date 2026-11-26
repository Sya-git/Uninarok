using System;
using UnityEngine;

namespace Uninarok.Effects.Editor
{
    /// <summary>
    /// The play state of the effect preview window (SPEC-0012 FR-11, plan 4.11) without any GUI: play, pause (runtime
    /// <c>TimeScale</c> 0), stop (seek 0, paused), loop, step by frames, seek, and the speed through
    /// <see cref="EffectRuntime.SetSpeed"/>. The window draws it; tests drive it. The stage belongs to the caller.
    /// </summary>
    public sealed class EffectPreviewController
    {
        readonly EffectPreviewStage stage;
        EffectHandle handle;
        bool paused, ended;
        float length, frameRate, endedAt;

        public EffectPreviewController(EffectPreviewStage stage)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
        }

        /// <summary>The handle of the current play (invalid before the first play; stale once a one-shot has ended).</summary>
        public EffectHandle Handle => handle;
        /// <summary>The key last played; null before the first play.</summary>
        public string Key { get; private set; }
        public bool Loop { get; private set; } = true;
        public bool Paused => paused;
        public float Speed { get; private set; } = 1f;
        /// <summary>True when a one-shot played to its end (nothing is active until the next play, step or seek).</summary>
        public bool Ended => ended;
        public bool HasEffect => Key != null && (ended || stage.Runtime.IsActive(handle));
        /// <summary>Seconds in one cycle at speed 1; 0 until an effect has played.</summary>
        public float Length => length;
        /// <summary>Frames per second of the shown frame number: the STR fps, else the authored frame rate.</summary>
        public float FrameRate => frameRate;

        /// <summary>Effect seconds into the cycle; the length once a one-shot has ended.</summary>
        public float Seconds
        {
            get
            {
                if (ended) return endedAt;
                var inst = stage.Runtime.InstanceOf(handle);
                return inst != null && inst.State == EffectInstanceState.Playing ? inst.Age : 0f;
            }
        }

        public float Frame => Seconds * frameRate;
        public int ShownFrame => EffectPreviewMath.ShownFrame(Frame);
        public int LastFrame => EffectPreviewMath.LastFrame(length, frameRate, Loop);

        /// <summary>Starts the key from 0, running; false (and nothing playing) when the key is not in the catalog.</summary>
        public bool Play(string key)
        {
            Key = key;
            return Start(0f, false);
        }

        /// <summary>Resumes; a one-shot that ended starts again from 0.</summary>
        public void Resume()
        {
            if (Key == null) return;
            if (ended || !stage.Runtime.IsActive(handle)) { Start(0f, false); return; }
            SetPaused(false);
        }

        public void Pause()
        {
            if (Key != null) SetPaused(true);
        }

        public void TogglePause()
        {
            if (paused || ended || (Key != null && !stage.Runtime.IsActive(handle))) Resume(); else Pause();
        }

        /// <summary>Seek to 0 and pause.</summary>
        public void Stop()
        {
            if (Key == null) return;
            SeekSeconds(0f);
            SetPaused(true);
        }

        /// <summary>Loop flag; it takes effect at once by replaying at the same time position (a loop is set at play time).</summary>
        public void SetLoop(bool loop)
        {
            if (Loop == loop) return;
            Loop = loop;
            if (Key == null || ended || !stage.Runtime.IsActive(handle)) return;
            Start(Math.Min(Seconds, length), paused);
        }

        /// <summary>Speed multiplier clamped to 0.1..4; applied to the playing instance (the FR-4 multiplier). Returns the clamped value.</summary>
        public float SetSpeed(float speed)
        {
            Speed = EffectPreviewMath.ClampSpeed(speed);
            stage.Runtime.SetSpeed(handle, Speed);
            return Speed;
        }

        /// <summary>Steps by whole frames (pausing first); clamped at frame 0 and at <see cref="LastFrame"/>.</summary>
        public void Step(int frames)
        {
            if (Key == null) return;
            int target = EffectPreviewMath.StepFrame(Frame, frames, LastFrame);
            SetPaused(true);
            SeekSeconds(EffectPreviewMath.FrameToSeconds(target, frameRate));
        }

        public void SeekFrame(int frame) => SeekSeconds(EffectPreviewMath.FrameToSeconds(frame, frameRate));

        /// <summary>Exact seek (clamped to [0, length]); an ended one-shot is started again first.</summary>
        public void SeekSeconds(float seconds)
        {
            if (Key == null) return;
            if (!stage.Runtime.IsActive(handle) && !Start(0f, true)) return;
            seconds = float.IsNaN(seconds) ? 0f : Mathf.Clamp(seconds, 0f, length);
            stage.Runtime.Seek(handle, seconds);
            stage.Tick(0f);   // the part draws the sought age now, without the clock moving
            ended = false;
        }

        /// <summary>Advances by wall seconds; notices a one-shot that ended.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Key == null) return;
            bool wasActive = stage.Runtime.IsActive(handle);
            stage.Tick(deltaSeconds);
            if (wasActive && !stage.Runtime.IsActive(handle) && !ended)
            {
                ended = true;
                endedAt = length;
            }
        }

        // Plays Key from `seconds` and applies the controller's loop, speed and pause; false when the key is unknown.
        bool Start(float seconds, bool startPaused)
        {
            if (stage.Runtime.IsActive(handle)) stage.Runtime.Stop(handle);
            ended = false;
            length = 0f; frameRate = 0f;
            handle = stage.Play(Key, Loop);
            if (!handle.IsValid) { SetPaused(false); return false; }

            stage.Runtime.SetSpeed(handle, Speed);
            var inst = stage.Runtime.InstanceOf(handle);
            if (inst != null && inst.State == EffectInstanceState.Playing)
            {
                length = inst.Length;
                frameRate = inst.FrameRate;
            }
            if (seconds > 0f) stage.Runtime.Seek(handle, Mathf.Min(seconds, length));
            SetPaused(startPaused);
            stage.Tick(0f);
            return true;
        }

        void SetPaused(bool value)
        {
            paused = value;
            stage.Runtime.TimeScale = value ? 0f : 1f;
        }
    }
}
