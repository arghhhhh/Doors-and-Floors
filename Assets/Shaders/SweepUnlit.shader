Shader "ZedGames/SweepUnlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _HueShift("Hue Shift", Range(0, 1)) = 0.0
        _SweepProgress("Sweep Progress", Range(-0.5, 1.5)) = -0.5
        _SweepWidth("Sweep Width", Float) = 0.12
        _SweepSoftness("Sweep Edge Softness", Float) = 0.06
        _SweepAngle("Sweep Angle (degrees)", Float) = 25.0
        _SweepIntensity("Sweep Brightness", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _HueShift;
                half   _SweepProgress;
                half   _SweepWidth;
                half   _SweepSoftness;
                half   _SweepAngle;
                half   _SweepIntensity;
            CBUFFER_END

            // --- RGB <-> HSV ---
            half3 RGBtoHSV(half3 c)
            {
                half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                half d = q.x - min(q.w, q.y);
                half e = 1.0e-10;
                return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            half3 HSVtoRGB(half3 c)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Optional hue shift
                half3 color = baseMap.rgb;
                if (_HueShift > 0.001)
                {
                    half3 hsv = RGBtoHSV(color);
                    hsv.x = frac(hsv.x + _HueShift);
                    color = HSVtoRGB(hsv);
                }

                // Diagonal sweep band
                // Project UV onto angled axis, then compare to sweep position
                half angleRad = radians(_SweepAngle);
                half2 dir = half2(cos(angleRad), sin(angleRad));
                half proj = dot(IN.uv, dir);

                // Band centered at _SweepProgress along the projected axis
                half dist = abs(proj - _SweepProgress);
                half band = 1.0 - smoothstep(_SweepWidth, _SweepWidth + _SweepSoftness, dist);

                // Additive white sweep, masked to character silhouette
                color += band * _SweepIntensity * baseMap.a;

                return half4(color, baseMap.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
