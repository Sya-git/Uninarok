using Uninarok.Sprites;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// Draws a Sprite entry (SPEC-0012 FR-4, FR-6; plan 4.5 "Parts", D14, D29): one pooled <see cref="SpriteActor"/> found through
    /// the runtime's sprite lookup (<see cref="EffectSpriteAnimations"/>: <c>SpriteCatalog.TryGetEffect</c> in the game), playing
    /// the entry's action (direction 0). The actor is <c>Driven</c>, so <c>SpriteSystem</c> neither advances nor turns it; the
    /// runtime's tick does, with <c>Step(instance.Delta)</c>, so pause, speed and the player clock hold. A seek is exact: the
    /// actor restarts the action and steps to <c>Age</c> (the actor has no seek of its own). The actor turns to the camera's yaw
    /// as <c>SpriteSystem</c> would. The action length is the part's cycle length (one-shot ends there, a loop wraps with it).
    /// The actor object is created once per slot and only enabled and disabled afterwards.
    /// </summary>
    internal sealed class SpritePart : IEffectPart
    {
        readonly EffectRuntime runtime;
        SpriteActor actor;
        bool bound;

        public SpritePart(EffectRuntime runtime) { this.runtime = runtime; }

        /// <summary>False when the last bind could not draw (the runtime plays the placeholder).</summary>
        public bool CanDraw => bound;

        /// <summary>Why the last bind could not draw; null when it can.</summary>
        public string Problem { get; private set; }

        /// <summary>The pooled actor (null before the slot's first sprite bind); tests and dev tools.</summary>
        internal SpriteActor Actor => actor;

        public float Bind(EffectInstance instance)
        {
            bound = false;
            Problem = null;
            if (actor != null) actor.gameObject.SetActive(false);
            var asset = instance.Asset;
            if (asset == null) { Problem = "has no asset"; return 0f; }
            if (!runtime.SpriteAnimations.TryGet(asset.SpriteName, out var animation, out var problem)) { Problem = problem; return 0f; }

            if (animation.Actions == null || !animation.TryGetAction(asset.SpriteAction, 0, SpriteMissingAction.DrawNothing, out int unique))
            {
                Problem = "sprite '" + asset.SpriteName + "' has no action " + asset.SpriteAction;
                return 0f;
            }
            var action = animation.Actions[unique];
            float length = action.FrameCount * action.FrameMs / 1000f;
            if (!(length > 0f) || float.IsInfinity(length))
            {
                Problem = "sprite '" + asset.SpriteName + "' action " + asset.SpriteAction + " has no length";
                return 0f;
            }

            if (actor == null)
            {
                var go = new GameObject("SpriteActor");
                go.SetActive(false);
                go.transform.SetParent(instance.Object.transform, false);
                actor = go.AddComponent<SpriteActor>();
                actor.Driven = true;
                actor.Billboard = false;      // turned here, from the runtime's tick
            }
            actor.Data = animation;
            actor.Material = runtime.SpriteMaterial;
            actor.Speed = 1f;
            actor.gameObject.SetActive(true);
            actor.Play(asset.SpriteAction, 0, true);
            if (!actor.HasAction)
            {
                Problem = "sprite '" + asset.SpriteName + "' has no action " + asset.SpriteAction;
                actor.gameObject.SetActive(false);
                return 0f;
            }
            actor.Step(0f);                   // frame 0 is written now
            Turn();
            bound = true;
            return length;
        }

        public void Tick(EffectInstance instance)
        {
            if (!bound) return;
            if (instance.Seeked)
            {
                actor.Play(instance.Asset.SpriteAction, 0, true);
                actor.Step(instance.Age);
            }
            else
            {
                actor.Step(instance.Delta);
            }
            Turn();
        }

        public void Release(EffectInstance instance)
        {
            bound = false;
            if (actor != null) actor.gameObject.SetActive(false);
        }

        void Turn()
        {
            var camera = SpriteSystem.Camera != null ? SpriteSystem.Camera : Camera.main;
            if (camera != null) actor.transform.rotation = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
        }
    }
}
