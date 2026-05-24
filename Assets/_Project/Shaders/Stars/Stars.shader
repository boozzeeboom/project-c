Shader "Project C/Stars/Stars"
{
    Properties
    {
        _StarColor ("Star Color", Color) = (1, 1, 1, 1)
        _StarSize ("Star Size", Float) = 2.0
        _TwinkleSpeed ("Twinkle Speed", Float) = 2.0
        _TwinkleAmount ("Twinkle Amount", Float) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            fixed4 _StarColor;
            float _StarSize;
            float _TwinkleSpeed;
            float _TwinkleAmount;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;

                float twinkle = sin(_Time.y * _TwinkleSpeed + v.vertex.x * 0.1) * _TwinkleAmount + (1.0 - _TwinkleAmount);
                o.color.a *= twinkle;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = i.color;
                col.a *= _StarColor.a;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}