using Uninarok.Sprites;

namespace Uninarok.Units
{
    /// <summary>
    /// The one clock of a unit (PLAN-0009 4.4, D9, D19; SPEC-0009 FR-5, FR-7, FR-8): the body's frame time drives it and every
    /// part shows the body's frame. Pure, no allocation. The elapsed time is kept inside one frame and the ticks inside one
    /// loop, so nothing drifts.
    /// </summary>
    public struct UnitClock
    {
        public int Animation;
        public PlayMode Mode;
        /// <summary>Multiplies the body's frame time: 2 plays at half speed (walk and attack speed, later specs).</summary>
        public float Speed;

        float _elapsedMs;   // into the current frame
        int _ticks;         // whole frames since the animation started, wrapped (see Advance)
        bool _finished;

        /// <summary>Frame times passed since the animation started; wraps at <see cref="TickWrap"/>.</summary>
        public int Ticks => _ticks;
        /// <summary>True once a play-once animation reached its end.</summary>
        public bool Finished => _finished;

        /// <summary>
        /// Ticks wrap at this value: a multiple of every frame count up to 10, and of 12, 16, 24 and 48 (the group sizes of
        /// animated headgears, D19), so a wrap never shows.
        /// </summary>
        public const int TickWrap = 5040 * 2;

        public void Play(int animation, PlayMode mode)
        {
            Animation = animation;
            Mode = mode;
            if (!(Speed > 0f)) Speed = 1f;
            _elapsedMs = 0f;
            _ticks = 0;
            _finished = false;
        }

        /// <summary>
        /// Advances by the body action's frame time and returns the body's frame. With <paramref name="headFrame"/> ≥ 0 (a
        /// player idle or sitting) the frame is the head facing and time only moves <see cref="Ticks"/>. A play-once animation
        /// stops on its last frame; <paramref name="ended"/> is true once, when an <see cref="PlayMode.OnceEnd"/> animation has
        /// shown its last frame for its full time.
        /// </summary>
        public int Advance(float deltaMs, in SpriteAnimation.ActionData body, int headFrame, out bool ended)
        {
            ended = false;
            int count = body.FrameCount;
            float frameMs = body.FrameMs * (Speed > 0f ? Speed : 1f);
            if (frameMs < 0.001f) frameMs = 0.001f;

            if (!_finished)
            {
                _elapsedMs += deltaMs;
                if (_elapsedMs >= frameMs)
                {
                    int passed = (int)(_elapsedMs / frameMs);
                    _elapsedMs -= passed * frameMs;
                    if (Mode == PlayMode.Loop || count <= 0) _ticks = Wrap(_ticks, passed, headFrame >= 0 ? TickWrap : LoopWrap(count));
                    else
                    {
                        // Once: the last frame is shown for its own time, then the animation is over.
                        _ticks += passed;
                        if (_ticks >= count)
                        {
                            _ticks = count - 1;
                            _elapsedMs = 0f;
                            _finished = true;
                            ended = Mode == PlayMode.OnceEnd;
                        }
                    }
                }
            }

            if (headFrame >= 0) return count > 0 ? headFrame % count : 0;
            return FrameOf(count);
        }

        /// <summary>The frame of an action with <paramref name="count"/> frames, without advancing.</summary>
        public int FrameOf(int count)
        {
            if (count <= 0) return 0;
            if (Mode == PlayMode.Loop) return _ticks % count;
            return _ticks < count ? _ticks : count - 1;
        }

        /// <summary>Moves the clock to a frame of the current animation (tools: step and scrub).</summary>
        public void SetFrame(int frame)
        {
            _ticks = frame < 0 ? 0 : frame;
            _elapsedMs = 0f;
            _finished = false;
        }

        /// <summary>
        /// The frame a part with <paramref name="partCount"/> frames shows for the body's <paramref name="bodyFrame"/> (D19): while
        /// the frame is the head facing, a part with 3 × k frames plays its group of k on the clock's ticks; otherwise the
        /// body's frame, wrapped.
        /// </summary>
        public static int PartFrame(int bodyFrame, int partCount, int headFrame, int ticks)
        {
            if (partCount <= 0) return 0;
            if (headFrame >= 0 && partCount > 3 && partCount % 3 == 0)
            {
                int group = partCount / 3;
                return (headFrame % 3) * group + ticks % group;
            }
            return bodyFrame % partCount;
        }

        // A loop's own count times itself would do; a whole number of loops keeps every part's wrap equal to the body's.
        static int LoopWrap(int count) => count > 0 ? count : 1;

        static int Wrap(int ticks, int passed, int wrap) => (int)(((long)ticks + passed) % wrap);
    }
}
