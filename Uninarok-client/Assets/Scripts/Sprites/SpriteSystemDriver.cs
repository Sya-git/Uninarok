using UnityEngine;

namespace Uninarok.Sprites
{
    /// <summary>The hidden object that runs <see cref="SpriteSystem"/> once per frame in play mode (PLAN-0007 4.6).</summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(1000)]
    internal sealed class SpriteSystemDriver : MonoBehaviour
    {
        internal static SpriteSystemDriver Create()
        {
            var go = new GameObject("SpriteSystem") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            return go.AddComponent<SpriteSystemDriver>();
        }

        void LateUpdate() => SpriteSystem.Tick(Time.deltaTime);
    }
}
