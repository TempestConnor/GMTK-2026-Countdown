Shader "Universal Render Pipeline/2D/Sprite-Lit-PlaneAHide"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
        [HideInInspector] _SafeSeamRole("Safe Seam Role", Float) = 0
        [HideInInspector] _SafeSeamPlane("Safe Seam Plane", Float) = 0

        // Conveyor scroll: moves the texels inside _ConveyorRect (UV min xy, max zw) along V, wrapping within
        // the rect. Used by tiled/9-sliced sprites whose centre slice repeats, e.g. the gravity area chevrons.
        [Toggle(_CONVEYOR_SCROLL)] _ConveyorScroll("Conveyor Scroll", Float) = 0
        _ConveyorRect("Conveyor Rect (UV min xy, max zw)", Vector) = (0.25, 0.25, 0.75, 0.75)
        _ConveyorSpeed("Conveyor Speed (rects per second, + is up)", Float) = 0.5
        // Plane B copies of Plane A-hidden art (e.g. the conveyor material) must stay visible in the preview circle.
        [Toggle(_PREVIEW_HIDE_OFF)] _PreviewHideOff("Ignore Plane Preview (Plane B)", Float) = 0
    }

    // Plane A geometry using this shader goes transparent inside the plane-preview circle (hold E),
    // letting Plane B -- which sits behind Plane A in the Sorting Layers list and renders normally,
    // unmodified -- show through. _PreviewCenter/_PreviewRadius/_PreviewEdgeSoftness are pushed
    // globally from playerController2.Preview.cs, shared with any material using this shader.
    HLSLINCLUDE
    float3 _PreviewCenter;
    float _PreviewRadius;
    float _PreviewEdgeSoftness;
    // 1 while previewing from Plane A: Plane B art is then clipped to the inside of the circle
    // instead of popping in everywhere the moment the camera starts rendering Plane B layers.
    float _PreviewRevealB;

    half ComputeHideMask(float3 positionWS)
    {
        float dist = length(positionWS.xy - _PreviewCenter.xy);
        half outside = smoothstep(_PreviewRadius - _PreviewEdgeSoftness, _PreviewRadius, dist);
    #if defined(_PREVIEW_HIDE_OFF)
        return lerp(1, 1 - outside, _PreviewRevealB);
    #else
        return outside;
    #endif
    }

    // Pure per-fragment UV math, so the animation costs no CPU time, Animator, or mesh rebuild.
    float2 ConveyorUV(float2 uv, float4 rect, float speed, float time)
    {
    #if defined(_CONVEYOR_SCROLL)
        if (all(uv >= rect.xy) && all(uv < rect.zw))
        {
            float height = rect.w - rect.y;
            uv.y = rect.y + frac((uv.y - rect.y) / height - time * speed) * height;
        }
    #endif
        return uv;
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
            #pragma shader_feature_local_fragment _ _CONVEYOR_SCROLL
            #pragma shader_feature_local_fragment _ _PREVIEW_HIDE_OFF

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
                half _SafeSeamRole;
                half _SafeSeamPlane;
                float4 _ConveyorRect;
                float _ConveyorSpeed;
            CBUFFER_END

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
                input.uv = ConveyorUV(input.uv, _ConveyorRect, _ConveyorSpeed, _Time.y);
                half4 col = CommonLitFragment(input, color);
                col.a *= ComputeHideMask(input.previewPositionWS);
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
                half _SafeSeamRole;
                half _SafeSeamPlane;
                float4 _ConveyorRect;
                float _ConveyorSpeed;
            CBUFFER_END

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
            #pragma shader_feature_local_fragment _ _CONVEYOR_SCROLL
            #pragma shader_feature_local_fragment _ _PREVIEW_HIDE_OFF

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
                half _SafeSeamRole;
                half _SafeSeamPlane;
                float4 _ConveyorRect;
                float _ConveyorSpeed;
            CBUFFER_END

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
                input.uv = ConveyorUV(input.uv, _ConveyorRect, _ConveyorSpeed, _Time.y);
                half4 col = CommonUnlitFragment(input, input.color);
                col.a *= ComputeHideMask(input.previewPositionWS);
                return col;
            }
            ENDHLSL
        }
    }
}
