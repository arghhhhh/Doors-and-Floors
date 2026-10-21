// Used by ZEDSpoutFeed: copies the ZED image with optional vertical flip and red/blue swap.
Shader "Hidden/ZEDSpoutBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Flip ("Flip", Float) = 1
        _Swap ("Swap", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Flip;
            float _Swap;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                if (_Flip > 0.5) uv.y = 1.0 - uv.y;
                fixed4 c = tex2D(_MainTex, uv);
                if (_Swap > 0.5) c.rgb = c.bgr;
                return fixed4(c.rgb, 1.0);
            }
            ENDCG
        }
    }
}
