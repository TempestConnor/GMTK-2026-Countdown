Shader "Universal Render Pipeline/2D/Sprite-Lit-Saturation"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        _Saturation("Saturation", Range(0, 1)) = 1
        [MaterialToggle] _PreviewHideEligible("Hide in Plane Preview (while on Plane A)", Float) = 1

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
        [HideInInspector] _SafeSeamRole("Safe Seam Role", Float) = 0
        [HideInInspector] _SafeSeamPlane("Safe Seam Plane", Float) = 0
    }

    // Plane-A instances of this shader (_Saturation high, i.e. not desaturated to represent Plane B)
    // go transparent inside the plane-preview circle (hold E), same as Sprite-Lit-PlaneAHide does for
    // terrain -- so boxes/doors/switches that only exist on Plane A don't visually clutter the reveal.
    // Objects exempt from this (the player) get _PreviewHideEligible=0 via a MaterialPropertyBlock override.
    HLSLINCLUDE
    float3 _PreviewCenter;
    float _PreviewRadius;
    float _PreviewEdgeSoftness;

    half ComputePreviewHideMask(float3 positionWS, half saturation, half hideEligible)
    {
        if (hideEligible < 0.5 || saturation < 0.5) return 1;
        float dist = length(positionWS.xy - _PreviewCenter.xy);
        return smoothstep(_PreviewRadius - _PreviewEdgeSoftness, _PreviewRadius, dist);
    }
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
                float3 previewPositionWS : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            #include "SafeSeams.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Saturation;
                half _PreviewHideEligible;
                half _SafeSeamRole;
                half _SafeSeamPlane;            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.previewPositionWS = TransformObjectToWorld(input.positionOS);

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 color = ApplySafeSeam(input.color, input.uv, input.previewPositionWS.xy, _SafeSeamRole, _SafeSeamPlane);
                half4 col = CommonLitFragment(input, color);
                half luminance = dot(col.rgb, half3(0.299, 0.587, 0.114));
                col.rgb = lerp(half3(luminance, luminance, luminance), col.rgb, _Saturation);
                col.a *= ComputePreviewHideMask(input.previewPositionWS, _Saturation, _PreviewHideEligible);
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
                half _Saturation;
                half _SafeSeamRole;
                half _SafeSeamPlane;            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                // Setup instancing for SpriteFlip is used in NormalsRenderingShared
                SetUpSpriteInstanceProperties();

                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float3 previewPositionWS : TEXCOORD5;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Saturation;
                half _PreviewHideEligible;
                half _SafeSeamRole;
                half _SafeSeamPlane;            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                o.previewPositionWS = TransformObjectToWorld(input.positionOS);
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 col = CommonUnlitFragment(input, input.color);
                half luminance = dot(col.rgb, half3(0.299, 0.587, 0.114));
                col.rgb = lerp(half3(luminance, luminance, luminance), col.rgb, _Saturation);
                col.a *= ComputePreviewHideMask(input.previewPositionWS, _Saturation, _PreviewHideEligible);
                return col;
            }
            ENDHLSL
        }
    }
}
