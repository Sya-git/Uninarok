using System.Runtime.InteropServices;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// The one vertex of every effect mesh (SPEC-0012 plan 4.5, D28): 24 bytes, float3 position at 0, <see cref="Color32"/>
    /// at 12, float2 uv at 16. Declared in Unity's <c>VertexAttribute</c> order (Position, Color, TexCoord0), which
    /// <c>Mesh.SetVertexBufferParams</c> requires within one stream (T0.3 evidence/submesh-order.md).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EffectVertex
    {
        public Vector3 Position;
        public Color32 Color;
        public Vector2 Uv;

        public EffectVertex(Vector3 position, Color32 color, Vector2 uv) { Position = position; Color = color; Uv = uv; }
    }
}
