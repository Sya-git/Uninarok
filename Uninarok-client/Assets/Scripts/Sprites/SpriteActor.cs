using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.Sprites
{
    /// <summary>Plays one <see cref="SpriteAnimation"/> (PLAN-0007 4.6). Implemented by TASKS-0007 T2.3.</summary>
    public sealed class SpriteActor : MonoBehaviour
    {
        public SpriteAnimation Data;
        public SpriteMissingAction Missing = SpriteMissingAction.DrawNothing;
        public float Speed = 1f;
        public bool Billboard = true;

        public int Animation => 0;
        public int Direction => 0;
        public int CurrentFrame => 0;
        public bool HasAction => false;
        public IReadOnlyList<SpriteRenderer> LayerRenderers => Array.Empty<SpriteRenderer>();
        internal int ApplyCount => 0;

        public event Action<SpriteActor, string> FrameEvent;

        public void Play(int animation, int direction, bool restart = true) => throw new NotImplementedException("TASKS-0007 T2.3");

        public bool TryGetAnchor(int index, out Vector3 localPosition) => throw new NotImplementedException("TASKS-0007 T2.3");

        internal void RaiseFrameEvent(string name) => FrameEvent?.Invoke(this, name);
    }
}
