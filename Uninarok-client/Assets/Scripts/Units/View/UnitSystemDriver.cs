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
            // Not DontSave: such an object survives leaving play mode, and every later session would tick once more per frame.
            var go = new GameObject("UnitSystem") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            return go.AddComponent<UnitSystemDriver>();
        }

        void LateUpdate()
        {
            if (UnitSystem.IsDriver(this)) UnitSystem.Tick(Time.deltaTime);
            else Destroy(gameObject); // left over from an earlier session
        }
    }
}
