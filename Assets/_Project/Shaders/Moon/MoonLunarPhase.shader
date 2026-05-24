Shader "Project C/Moon/MoonLunarPhase"
{
    Properties
    {
        _MoonColor ("Moon Base Color", Color) = (0.9, 0.9, 0.85, 1.0)
        _ShadowColor ("Shadow Color", Color) = (0.1, 0.1, 0.15, 1.0)
        _CraterScale ("Crater Scale", Float) = 8.0
        _CraterContrast ("Crater Contrast", Float) = 0.3
        _MoonPhase ("Moon Phase (0-1)", Range(0, 1)) = 0.5
        _LitFraction ("Lit Fraction", Range(0, 1)) = 0.5
        _RimPower ("Rim Power", Float) = 3.0
        _RimIntensity ("Rim Intensity", Float) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            fixed4 _MoonColor;
            fixed4 _ShadowColor;
            float _CraterScale;
            float _CraterContrast;
            float _MoonPhase;
            float _LitFraction;
            float _RimPower;
            float _RimIntensity;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(hash(i), hash(i + float2(1.0, 0.0)), u.x),
                    lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x),
                    u.y
                );
            }

            float craterNoise(float2 p)
            {
                float n = noise(p * 2.0);
                n += noise(p * 4.0) * 0.5;
                n += noise(p * 8.0) * 0.25;
                return n;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float phaseAngle = _MoonPhase * 6.28318;
                float2 moonUV = i.uv - 0.5;

                float phaseShift = cos(phaseAngle);
                float litEdge = -cos(phaseAngle * 2.0) * 0.5 + 0.5;
                litEdge = clamp(litEdge, 0.0, 1.0);

                float xNorm = moonUV.x + phaseShift * 0.5;
                float litMask = step(xNorm, litEdge * 2.0 - 1.0);

                float n = craterNoise(i.uv * _CraterScale);
                float craterMask = lerp(1.0, 0.7 + n * _CraterContrast, litMask);

                float rim = 1.0 - max(0.0, dot(i.viewDir, i.worldNormal));
                rim = pow(rim, _RimPower) * _RimIntensity;

                fixed3 moonCol = lerp(_ShadowColor.rgb, _MoonColor.rgb * craterMask, litMask);

                fixed3 ambient = unity_AmbientSky.rgb * 0.1;
                moonCol += ambient * litMask;

                moonCol += rim * _MoonColor.rgb;

                fixed4 col;
                col.rgb = moonCol;
                col.a = 1.0;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}