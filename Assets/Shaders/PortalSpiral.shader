Shader "ZedGames/PortalSpiral"
{
    Properties
    {
        [MainColor] _BaseColor("Door Color", Color) = (0.2, 0.6, 1.0, 1.0)
        _EdgeColor("Edge Color", Color) = (0.12, 0.1, 0.08, 1.0)
        _Brightness("Brightness", Range(0.5, 3.0)) = 1.3
        _Speed("Animation Speed", Float) = 1.0
        _NumArms("Spiral Arms", Float) = 6.0
        _NumRings("Rings", Float) = 5.0
        _NumBands("Horizontal Bands", Float) = 10.0
        _SpiralAngle("Spiral Angle", Range(0, 3.14159)) = 1.0472
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "PortalSpiralPass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _Brightness;
                float _Speed;
                float _NumArms;
                float _NumRings;
                float _NumBands;
                float _SpiralAngle;
            CBUFFER_END

            // --- RGB <-> HSV helpers ---
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
                OUT.uv = IN.uv;
                OUT.normalOS = IN.normalOS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Edge faces (not front/back) render as the edge color
                if (abs(IN.normalOS.z) < 0.5)
                    return _EdgeColor;

                // Center UV so (0,0) is the middle of the face
                float cX = IN.uv.x - 0.5;
                float cY = IN.uv.y - 0.5;

                // Log-polar transform for spiral effect
                float newX = log(sqrt(cX * cX + cY * cY));
                float newY = atan2(cX, cY);

                float t = _Time.y * _Speed;

                // Compute each spiral component separately for color mapping
                float bandVal  = cos(_NumBands * cX - t);
                float ringVal  = cos(_NumRings * newX - t);
                // Spiral: integer arms for seamless angular wrapping,
                // _SpiralAngle controls radial tightness independently.
                float arms = round(_NumArms);
                float spiralVal = cos(arms * newY + _SpiralAngle * newX + t);

                // --- Build a high-contrast palette from the door's base color ---
                half3 baseHSV = RGBtoHSV(_BaseColor.rgb);

                // Normalize: full saturation, full brightness for the primary
                half baseHue = baseHSV.x;
                half3 primaryColor = HSVtoRGB(half3(baseHue, 1.0, 1.0));

                // Complementary: opposite hue, dark value for contrast
                half3 compColor = HSVtoRGB(half3(frac(baseHue + 0.5), 0.9, 0.35));

                // Accent: triadic hue, medium value
                half3 accentColor = HSVtoRGB(half3(frac(baseHue + 0.33), 1.0, 0.7));

                // Remap each component from [-1,1] to [0,1]
                float s = spiralVal * 0.5 + 0.5;   // spiral arms
                float r = ringVal  * 0.5 + 0.5;    // rings
                float b = bandVal  * 0.5 + 0.5;    // bands

                // Primary color fills the spiral arms (bright).
                // Dark complementary color fills between arms (high contrast).
                // Accent bleeds into the rings at medium intensity.
                // Bands add brightness variation for depth.
                half3 finalColor = lerp(compColor, primaryColor, s);
                finalColor = lerp(finalColor, accentColor, r * 0.35);
                finalColor *= (b * 0.25 + 0.75);

                finalColor *= _Brightness;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
