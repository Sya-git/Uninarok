// Uninarok/RoSprite (PLAN-0007 4.7, SPEC-0007 FR-1, FR-7): URP unlit sprite shader with an optional palette swap.
// Built on URP's Sprite-Unlit-Default (flip, renderer colour, instancing). When _PaletteOn is set (SpritePalette, through
// a property block) a pixel whose palette index (the sprite's secondary texture _IndexTex, R8) is above 0 takes its colour
// from _PaletteTex (256 x 1, point) and keeps its alpha. True-colour pixels have index 0 and are unchanged. Hand-written.
// _DepthBias (PLAN-0009 D7, SPEC-0009 FR-10) moves the depth the pixel is tested with toward the camera, in world units
// along the view axis; the position on screen and the size do not change. 0 (default) renders as before.
// With a bias, the depth is also never farther than that of an upright picture (PLAN-0009 4.10, D28, D29, SPEC-0009 FR-16):
// where the pixel's view ray meets the vertical plane through the ground point that faces the camera. The layer's height
// above its ground point comes in the z scale of its matrix (SpriteActor.SetGround); one that was not set changes nothing.
Shader "Uninarok/RoSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _IndexTex ("Palette Index (secondary texture)", 2D) = "black" {}
        [NoScaleOffset] _PaletteTex ("Palette (256 x 1)", 2D) = "black" {}
        [Toggle] _PaletteOn ("Use Palette", Float) = 0
        _DepthBias ("Depth Bias (world units toward the camera)", Float) = 0

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
                float _DepthBias;
            CBUFFER_END

            // SpriteActor.HeightOffset: keeps the z scale that carries the height positive.
            #define HEIGHT_OFFSET 16.0

            // View-space z of the upright plane on this point's view ray, or plainZ where that is nearer (perspective only).
            float NearerOfUpright(float3 positionVS, float plainZ)
            {
                float4x4 m = GetObjectToWorldMatrix();
                float height = length(float3(m._m02, m._m12, m._m22)) - HEIGHT_OFFSET;
                float3 groundVS = TransformWorldToView(float3(m._m03, m._m13, m._m23));
                groundVS.y -= height; // the sprite's up is the screen's
                float3 upVS = float3(UNITY_MATRIX_V._m01, UNITY_MATRIX_V._m11, UNITY_MATRIX_V._m21);
                float3 n = float3(0.0, 0.0, -1.0) + upVS.z * upVS; // the view direction made horizontal
                float along = dot(n, positionVS);
                float ground = dot(n, groundVS);
                // Straight down, or a ray that does not meet the plane in front of the camera.
                if (unity_OrthoParams.w > 0.5 || dot(n, n) < 1e-4 || along < 1e-4 || ground < 1e-4) return plainZ;
                // Not past the near plane: a tall unit close to the camera would be clipped.
                float uprightZ = min(positionVS.z * ground / along, -_ProjectionParams.y * 1.01);
                return max(plainZ, uprightZ); // the view looks along -z
            }

            Varyings RoSpriteVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                if (_DepthBias != 0.0)
                {
                    // Only z / w changes: the clip-space z of the point moved along the view axis, kept on this vertex's w.
                    float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS));
                    positionVS.z = NearerOfUpright(positionVS, positionVS.z + _DepthBias);
                    float4 biased = TransformWViewToHClip(positionVS);
                    o.positionCS.z = biased.z / biased.w * o.positionCS.w;
                }
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
