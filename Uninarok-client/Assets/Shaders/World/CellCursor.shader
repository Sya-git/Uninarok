// Uninarok/CellCursor (PLAN-0010 4.11, D21, D29): the marked cell, a texture and its colour. _DepthBias moves the depth the pixel is
// tested with toward the camera, in world units along the view axis (as Uninarok/RoSprite, PLAN-0009 D7): the cell's triangles
// and the ground mesh's are split differently, so parts of a sloping cell lie under the ground.
Shader "Uninarok/CellCursor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _DepthBias ("Depth Bias (world units toward the camera)", Float) = 0.4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex Vertex
            #pragma fragment Fragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _DepthBias;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(positionWS);
                float3 positionVS = TransformWorldToView(positionWS);
                positionVS.z += _DepthBias; // the view looks along -z
                float4 biased = TransformWViewToHClip(positionVS);
                o.positionCS.z = biased.z / biased.w * o.positionCS.w;
                o.uv = input.uv;
                return o;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
            }
            ENDHLSL
        }
    }
}
