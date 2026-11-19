// Uninarok/RoSprite (PLAN-0007 4.7, SPEC-0007 FR-1, FR-7): URP unlit sprite shader with an optional palette swap.
// Built on URP's Sprite-Unlit-Default (flip, renderer colour, instancing). When _PaletteOn is set (SpritePalette, through
// a property block) a pixel whose palette index (the sprite's secondary texture _IndexTex, R8) is above 0 takes its colour
// from _PaletteTex (256 x 1, point) and keeps its alpha. True-colour pixels have index 0 and are unchanged. Hand-written.
Shader "Uninarok/RoSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _IndexTex ("Palette Index (secondary texture)", 2D) = "black" {}
        [NoScaleOffset] _PaletteTex ("Palette (256 x 1)", 2D) = "black" {}
        [Toggle] _PaletteOn ("Use Palette", Float) = 0

        // Legacy properties, as in URP's sprite shaders, so the material falls back gracefully.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex RoSpriteVertex
            #pragma fragment RoSpriteFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS_SHARED
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_IndexTex);
            SAMPLER(sampler_IndexTex);
            TEXTURE2D(_PaletteTex);
            SAMPLER(sampler_PaletteTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _PaletteOn;
            CBUFFER_END

            Varyings RoSpriteVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 RoSpriteFragment(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_PaletteOn > 0.5)
                {
                    float index = floor(SAMPLE_TEXTURE2D(_IndexTex, sampler_IndexTex, input.uv).r * 255.0 + 0.5);
                    if (index > 0.5)
                        c.rgb = SAMPLE_TEXTURE2D_LOD(_PaletteTex, sampler_PaletteTex, float2((index + 0.5) / 256.0, 0.5), 0).rgb;
                }
                return c * input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
