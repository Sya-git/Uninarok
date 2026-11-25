// Uninarok/RoEffect (PLAN-0012 4.5, SPEC-0012 FR-3, FR-4, FR-9): URP unlit shader for effect meshes (STR layers and authored parts).
// Blend comes from the material (_SrcBlend, _DstBlend), ZWrite Off, Cull Off, ZTest LEqual, vertex colour x texture.
// SRP Batcher compatible: every per-material property is in UnityPerMaterial, no property blocks. Hand-written.
// Vertex layout: the shader reads the POSITION, COLOR and TEXCOORD0 semantics, so the byte order of the vertex does not matter
// to HLSL. Unity requires vertex attributes in VertexAttribute enum order, so EffectVertex (24 bytes) is float3 position,
// Color32 color, float2 uv, in that order (T0.3 spike, evidence/submesh-order.md).
// _FACING_CAMERA: the vertex x and y are offsets along the camera's right and up axes from the object origin (spherical
// billboard, as units, PLAN-0009 4.6); z is ignored. Without it the position is object space.
// _UV_REPEAT: the texture is sampled through an inline repeat sampler state (cylinder strips with RepeatU > 1); converted effect
// textures are clamp, so the default samples with the texture's own sampler.
// _DepthBias: as Uninarok/RoSprite (PLAN-0009 D7): the depth the pixel is tested with moves toward the camera along the view
// axis, in world units; the position on screen does not change. 0 renders without a bias.
// _Cutoff: texels whose texture alpha is below it are discarded (the colour-keyed background of the texture draws nothing, as in
// the client). EffectMaterials sets 0.5 for pairs whose source factor is not SrcAlpha (for example One / Zero), where the
// converted texture's RGB under a keyed texel would otherwise draw (plan D37); 0 keeps every texel.
Shader "Uninarok/RoEffect"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
        _DepthBias ("Depth Bias (world units toward the camera)", Float) = 0
        _Cutoff ("Alpha cutoff (texture alpha below it is discarded)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RoEffect"
            Tags { "LightMode" = "UniversalForward" }

            // Colour by the material's pair; the target's alpha is kept as is (Zero One), as the client's backbuffer has no
            // alpha: the destination-alpha codes read the constant 1 (EffectBlend, plan D33), never this channel.
            Blend [_SrcBlend] [_DstBlend], Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex RoEffectVertex
            #pragma fragment RoEffectFragment
            #pragma multi_compile_local _ _FACING_CAMERA
            #pragma multi_compile_local _ _UV_REPEAT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            SAMPLER(sampler_linear_repeat); // inline sampler state (name = filter and wrap), used with _UV_REPEAT

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _SrcBlend;
                float _DstBlend;
                float _DepthBias;
                float _Cutoff;
            CBUFFER_END

            Varyings RoEffectVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
            #if defined(_FACING_CAMERA)
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = GetObjectToWorldMatrix()._m03_m13_m23 + right * input.positionOS.x + up * input.positionOS.y;
            #else
                float3 positionWS = TransformObjectToWorld(input.positionOS);
            #endif
                o.positionCS = TransformWorldToHClip(positionWS);
                if (_DepthBias != 0.0)
                {
                    // Only z / w changes: the clip-space z of the point moved along the view axis, kept on this vertex's w.
                    float3 positionVS = TransformWorldToView(positionWS);
                    positionVS.z += _DepthBias; // the view looks along -z
                    float4 biased = TransformWViewToHClip(positionVS);
                    o.positionCS.z = biased.z / biased.w * o.positionCS.w;
                }
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                return o;
            }

            half4 RoEffectFragment(Varyings input) : SV_Target
            {
            #if defined(_UV_REPEAT)
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_linear_repeat, input.uv);
            #else
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            #endif
                if (_Cutoff > 0.0) clip(c.a - _Cutoff);
                return c * input.color;
            }
            ENDHLSL
        }
    }
}
