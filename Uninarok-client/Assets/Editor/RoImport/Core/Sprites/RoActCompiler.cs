using System;
using UnityEngine;
using Uninarok.Sprites;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>An ACT flattened into the arrays of a <see cref="SpriteAnimation"/> (PLAN-0007 4.5). Implemented by TASKS-0007 T4.2.</summary>
    public sealed class RoCompiledAct
    {
        public int FileActionCount;
        public bool Directional;
        public short[] ActionMap = Array.Empty<short>();
        public SpriteAnimation.ActionData[] Actions = Array.Empty<SpriteAnimation.ActionData>();
        public SpriteAnimation.FrameData[] Frames = Array.Empty<SpriteAnimation.FrameData>();
        public SpriteAnimation.LayerData[] Layers = Array.Empty<SpriteAnimation.LayerData>();
        public Vector2Int[] Anchors = Array.Empty<Vector2Int>();
        public string[] Events = Array.Empty<string>();
        public int MaxLayers;
    }

    /// <summary>Compiles an ACT (PLAN-0007 4.5). Implemented by TASKS-0007 T4.2.</summary>
    public static class RoActCompiler
    {
        public static bool TryCompile(RoAct act, int indexedCount, int trueColorCount, out RoCompiledAct compiled, out string reason)
        {
            compiled = null;
            reason = "not implemented (TASKS-0007 T4.2)";
            return false;
        }
    }
}
