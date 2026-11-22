using UnityEngine;

namespace Uninarok.Units
{
    /// <summary>
    /// The hidden object that runs <see cref="UnitSystem"/> once per frame in play mode (PLAN-0009 4.6, §5): after
    /// <c>CinemachineBrain</c> (order 100) has moved the camera, before <c>SpriteSystem</c> (1000).
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(900)]
    internal sealed class UnitSystemDriver : MonoBehaviour
    {
        internal static UnitSystemDriver Create()
        {
            var go = new GameObject("UnitSystem") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            return go.AddComponent<UnitSystemDriver>();
        }

        void LateUpdate() => UnitSystem.Tick(Time.deltaTime);
    }
}
