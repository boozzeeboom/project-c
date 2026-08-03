Shader "ProjectC/Skybox Blend"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 1.0)
        _Exposure ("Exposure", Float) = 1.0
        _SunPosition ("Sun Position", Vector) = (0.0, 1.0, 0.0, 0)
        _Blend ("Blend", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        LOD 100

        CGINCLUDE
        #include "UnityCG.cginc"

        float4 _Tint;
        float _Exposure;
        float4 _SunPosition;
        float _Blend;
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float3 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(_Tint.rgb * _Exposure, 1.0);
            }
            ENDCG
        }
    }
}