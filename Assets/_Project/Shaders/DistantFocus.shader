// DistantFocus.shader — DF-001 rev.7: far-field фокус своим проходом.
// Дальнее (depth > FarStart) мылится, пока взгляд не держит его LockTime (FarLock=1).
// Персонаж (depth < CharMax) возвращается бит-в-бит: исключение конструкцией.
// Fullscreen overwrite-проход: читает копию цвета, пишет поверх. Blend One Zero.

Shader "Hidden/ProjectC/DistantFocusFar"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D(_FarFocusSource);
        SAMPLER(sampler_FarFocusSource);

        // Размер цели: (w, h, 1/w, 1/h). Считаем UV через него, не через _ScreenParams
        // (правило half-res ловушки из volumetric-отладки).
        float4 _FarFocusTexel;

        float _FarStart;
        float _FarEnd;
        float _FarStrength;
        float _NearStrength;
        float _MaxRadius;
        float _CharMax;

        // Динамика из FarFocusController (глобал, пишется каждый кадр).
        float _FarFocusLock;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
        };

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
            return OUT;
        };

        float SceneDepthMeters(float2 uv)
        {
            return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        }

        // 12 тапов Пуассона в кольце радиуса ~1 (масштабируются radius).
        static const float2 Poisson12[12] = {
            float2(-0.94, -0.34), float2(-0.61, -0.79), float2(-0.16, -0.99),
            float2( 0.34, -0.94), float2( 0.79, -0.61), float2( 0.99, -0.16),
            float2( 0.94,  0.34), float2( 0.61,  0.79), float2( 0.16,  0.99),
            float2(-0.34,  0.94), float2(-0.79,  0.61), float2(-0.99,  0.16)
        };
        ENDHLSL

        Pass
        {
            Name "DistantFocusFar"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionCS.xy * _FarFocusTexel.zw;
                float2 texel = _FarFocusTexel.zw;

                half3 centerCol = SAMPLE_TEXTURE2D(_FarFocusSource, sampler_FarFocusSource, uv).rgb;
                float depth = SceneDepthMeters(uv);

                // Дальняя зона: растёт к горизонту, гаснет при локе взгляда.
                float farZone = smoothstep(_FarStart, _FarEnd, depth)
                    * (1.0 - saturate(_FarFocusLock)) * _FarStrength;
                // Ближняя зона: только средний план (выше CharMax!), только при локе вдаль.
                float nearZone = smoothstep(_CharMax, _CharMax * 2.0, depth)
                    * (1.0 - smoothstep(_FarStart * 0.5, _FarStart, depth))
                    * _NearStrength * saturate(_FarFocusLock);

                float coc = max(farZone, nearZone);
                float radius = coc * _MaxRadius;
                // Персонаж и резкое: точный возврат, ноль размешивания соседей.
                if (radius < 0.75) return half4(centerCol, 1.0);

                float3 sum = centerCol;
                float wsum = 1.0;
                for (int i = 0; i < 12; i++)
                {
                    float2 tapUV = uv + Poisson12[i] * texel * radius;
                    half3 tapCol = SAMPLE_TEXTURE2D(_FarFocusSource, sampler_FarFocusSource, tapUV).rgb;
                    float tapDepth = SceneDepthMeters(tapUV);
                    // Персонаж не течёт в размытый фон: тапы с его глубины давятся.
                    float charW = smoothstep(_CharMax * 0.8, _CharMax * 1.2, tapDepth);
                    float w = lerp(1.0, max(charW, 0.15), step(0.01, coc));
                    sum += tapCol * w;
                    wsum += w;
                }
                return half4(sum / wsum, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
