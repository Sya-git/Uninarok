using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// Hidden driver of <see cref="EffectSystem"/> (plan 5): created on the first play, one <c>DontDestroyOnLoad</c> root that
    /// also parents the pooled effect objects. Ticks in <c>LateUpdate</c> after units are placed (900) and before
    /// <c>SpriteSystem</c> (1000).
    /// </summary>
    [DefaultExecutionOrder(950)]
    internal sealed class EffectSystemDriver : MonoBehaviour
    {
        internal static EffectSystemDriver Create()
        {
            var go = new GameObject("EffectSystem") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            return go.AddComponent<EffectSystemDriver>();
        }

        void LateUpdate() => EffectSystem.Tick(Time.deltaTime);

        void OnDestroy() => EffectSystem.DriverDestroyed(this);
    }
}
