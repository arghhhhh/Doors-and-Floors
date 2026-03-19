Shader "ZedGames/FinishLineCheckered"
{
    Properties
    {
        _ColorA("Color A", Color) = (0, 0, 0, 1)
        _ColorB("Color B", Color) = (1, 1, 1, 1)
        _CheckerScale("Checker Scale", Float) = 2.0
        _WindStrength("Wind Strength", Float) = 0.03
        _WindSpeed("Wind Speed", Float) = 2.0
        _WindFrequency("Wind Frequency", Float) = 1.0
        _ClipX("Clip X Position", Float) = 0
        _ClipSide("Clip Side (0=off, 1=keep left, 2=keep right)", Float) = 0
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
            Name "FinishLinePass"
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;      // deformed (for clipping)
                float3 restPositionWS : TEXCOORD2;   // pre-deformation (for checker)
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                float _CheckerScale;
                float _WindStrength;
                float _WindSpeed;
                float _WindFrequency;
                float _ClipX;
                float _ClipSide;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 pos = IN.positionOS.xyz;

                // Store rest position before deformation (for checker pattern)
                OUT.restPositionWS = mul(unity_ObjectToWorld, float4(pos, 1.0)).xyz;

                // Wind deformation: sine wave along X, varies over time
                float worldX = OUT.restPositionWS.x;
                pos.z += sin(worldX * _WindFrequency + _Time.y * _WindSpeed) * _WindStrength;
                pos.y += sin(worldX * _WindFrequency * 0.7 + _Time.y * _WindSpeed * 1.3) * _WindStrength * 0.3;

                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv = IN.uv;
                OUT.positionWS = mul(unity_ObjectToWorld, float4(pos, 1.0)).xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Clip for slice effect
                if (_ClipSide > 0.5 && _ClipSide < 1.5)
                {
                    // Keep left side: discard pixels right of _ClipX
                    clip(_ClipX - IN.positionWS.x);
                }
                else if (_ClipSide > 1.5)
                {
                    // Keep right side: discard pixels left of _ClipX
                    clip(IN.positionWS.x - _ClipX);
                }

                // Checkered pattern using pre-deformation world position
                // (stable since wind only displaces Z and barely Y)
                float2 c = floor(IN.restPositionWS.xy * _CheckerScale);
                float checker = fmod(abs(c.x + c.y), 2.0);

                half4 color = checker < 1.0 ? _ColorA : _ColorB;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
