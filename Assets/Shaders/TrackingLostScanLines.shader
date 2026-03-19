Shader "ZedGames/TrackingLostScanLines"
{
    Properties
    {
        _Color("Stripe Color", Color) = (1, 0, 0, 0.8)
        _StripeFrequency("Stripe Frequency", Float) = 8.0
        _StripeWidth("Stripe Width", Range(0.0, 1.0)) = 0.4
        _ScrollSpeed("Scroll Speed", Float) = 2.0
        _PulseSpeed("Pulse Speed", Float) = 3.0
        _PulseMin("Pulse Min Alpha", Range(0.0, 1.0)) = 0.2
        _PulseMax("Pulse Max Alpha", Range(0.0, 1.0)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "TrackingLostPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _StripeFrequency;
                float _StripeWidth;
                float _ScrollSpeed;
                float _PulseSpeed;
                float _PulseMin;
                float _PulseMax;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Diagonal coordinate: sum of UV axes gives 45-degree lines
                float diag = (IN.uv.x + IN.uv.y) * _StripeFrequency;

                // Scroll the stripes over time
                diag += _Time.y * _ScrollSpeed;

                // Generate stripe pattern: frac gives sawtooth, step makes hard edges
                float stripe = frac(diag);
                // stripe is 1 inside the colored band, 0 in the transparent gap
                stripe = step(stripe, _StripeWidth);

                // If we're in the transparent gap, discard
                if (stripe < 0.5)
                    discard;

                // Pulsing alpha via sin wave
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5; // [0,1]
                float alpha = lerp(_PulseMin, _PulseMax, pulse);

                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
