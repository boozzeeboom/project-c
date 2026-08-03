Shader "Project C/Clouds/DistantCloudHSV"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _Hue ("Hue", Range(-1, 1)) = 0
        _Saturation ("Saturation", Range(-1, 1)) = 0
        _Value ("Value", Range(-1, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+200" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TintColor;
            float _Hue;
            float _Saturation;
            float _Value;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed3 RGBtoHSV(fixed3 c)
            {
                fixed4 K = fixed4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                fixed4 p = lerp(fixed4(c.bg, K.wz), fixed4(c.gb, K.xy), step(c.b, c.g));
                fixed4 q = lerp(fixed4(p.xyw, c.r), fixed4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return fixed3(abs(q.z + (q.w - q.y)/(6.0*d + e)), d/(q.x + e), q.x);
            }

            fixed3 HSVtoRGB(fixed3 c)
            {
                fixed4 K = fixed4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                fixed3 p = abs(frac(c.xxx + K.xyz)*6.0-K.www);
                return c.z * lerp(K.xxx, clamp(p-K.xxx, 0.0, 1.0), c.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tc = tex2D(_MainTex, i.uv);
                
                // Apply HSV modification
                fixed3 hsv = RGBtoHSV(tc.rgb);
                hsv.x = frac(hsv.x + _Hue);
                hsv.y = clamp(hsv.y + _Saturation, 0.0, 1.0);
                hsv.z = clamp(hsv.z + _Value, 0.0, 1.0);
                fixed3 resultRGB = HSVtoRGB(hsv);
                
                fixed4 result = fixed4(resultRGB, tc.a);
                
                return result;
            }
            ENDCG
        }
    }

    FallBack Off
}
