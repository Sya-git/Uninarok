using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Uninarok.Sprites
{
    /// <summary>The single update loop of every <see cref="SpriteActor"/> (PLAN-0007 4.6). Implemented by TASKS-0007 T2.3.</summary>
    [AutoStaticsCleanup]
    public static partial class SpriteSystem
    {
        public static Camera Camera { get; set; }
        public static int Count => 0;

        internal static void Tick(float deltaSeconds) => throw new NotImplementedException("TASKS-0007 T2.3");

        internal static void ResetForTests() { }
    }
}
