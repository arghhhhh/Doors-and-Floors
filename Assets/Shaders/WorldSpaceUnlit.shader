Shader "ZedGames/WorldSpaceUnlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Tiling("Tiling (X/Y)", Vector) = (1, 1, 0, 0)
        _Offset("Offset (X/Y)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "WorldSpaceUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                // World-space XY passed to fragment for sampling
                float2 worldXY     : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;   // kept for SRP batcher compatibility
                half4  _BaseColor;
                float4 _Tiling;
                float4 _Offset;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS    = TransformWorldToHClip(worldPos);
                // Use world X and Y as UV, then apply tiling/offset
                OUT.worldXY        = worldPos.xy * _Tiling.xy + _Offset.xy;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.worldXY);
                return tex * _BaseColor;
            }
            ENDHLSL
        }

        // Shadow caster so the geometry still casts shadows if needed
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float4 _Tiling;
                float4 _Offset;
                half   _Cutoff;
            CBUFFER_END
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
