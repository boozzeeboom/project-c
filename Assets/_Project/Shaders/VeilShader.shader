// VeilShader.shader — Шейдер завесы для Project C
// Key fix: Depth fade affects COLOR (fog blend), NOT alpha
// This ensures veil is visible at ALL distances, including near player
// Based on CLOUDENGINE approach: noise creates texture, fog at distance

Shader "Project C/Clouds/VeilShader"
{
    Properties
    {
        _VeilColor ("Цвет завесы", Color) = (0.176, 0.106, 0.306, 1.0)
        _LightningColor ("Цвет молний", Color) = (0.7, 0.4, 1.0, 1.0)
        _LightningIntensity ("Интенсивность молний", Range(0, 1)) = 0.0
        _FogDistance ("Дистанция тумана", Float) = 3000.0
        _FogColor ("Цвет тумана", Color) = (0.05, 0.05, 0.08, 1.0)
        _NoiseScale ("Масштаб шума", Float) = 0.002
        _NoiseSpeed ("Скорость шума", Float) = 0.01
        _NoiseOctaves ("Октавы шума", Range(1, 6)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            Name "VeilPass"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _VeilColor;
                half4 _LightningColor;
                half _LightningIntensity;
                half _FogDistance;
                half4 _FogColor;
                half _NoiseScale;
                half _NoiseSpeed;
                int _NoiseOctaves;
            CBUFFER_END

            // ---- Hash Functions ----
            half Hash21(half2 p)
            {
                p = frac(p * half2(123.34, 456.21));
                p += dot(p, p + half2(45.32, 78.91));
                return frac(p.x * p.y);
            }

            half Hash31(half3 p)
            {
                p = frac(p * half3(123.34, 456.21, 789.87));
                p += dot(p, p.yzx + half3(45.32, 78.91, 34.56));
                return frac(p.x * p.y * p.z);
            }

            // ---- 3D Value Noise (for volumetric effect) ----
            half Noise3D(half3 p)
            {
                half3 id = floor(p);
                half3 lf = frac(p);
                lf = lf * lf * (3.0 - 2.0 * lf);

                half tl = Hash31(id);
                half tr = Hash31(id + half3(1, 0, 0));
                half bl = Hash31(id + half3(0, 1, 0));
                half br = Hash31(id + half3(1, 1, 0));
                half tlf = Hash31(id + half3(0, 0, 1));
                half trf = Hash31(id + half3(1, 0, 1));
                half blf = Hash31(id + half3(0, 1, 1));
                half brf = Hash31(id + half3(1, 1, 1));

                half top = lerp(tl, tr, lf.x);
                half bot = lerp(bl, br, lf.x);
                half topF = lerp(tlf, trf, lf.x);
                half botF = lerp(blf, brf, lf.x);

                return lerp(lerp(top, bot, lf.y), lerp(topF, botF, lf.y), lf.z);
            }

            // ---- 3D FBM for "клубящаяся завеса со своими впадинами каньонами" ----
            half FBm3D(half3 p, int octaves)
            {
                half val = 0.0;
                half amp = 0.5;
                half freq = 1.0;
                half norm = 0.0;

                for (int i = 0; i < 6; i++)
                {
                    if (i >= octaves) break;
                    val += amp * Noise3D(p * freq);
                    norm += amp;
                    freq *= 2.0;
                    amp *= 0.5;
                }

                return val / norm;
            }

            // ---- Vertex Input/Output ----
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.worldPos = vertexInput.positionWS;
                output.uv = float3(input.uv * 0.001, 0); // Scale UVs for world-scale noise

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ---- Animated wind offset ----
                half time = _Time.y;
                half3 windOffset = half3(time * _NoiseSpeed, 0.0, time * _NoiseSpeed * 0.3);

                // ---- Sample 3D FBM for volumetric density ----
                // This creates "клубящаяся завеса со своими впадинами каньонами"
                half3 samplePos = input.worldPos * _NoiseScale + windOffset;

                // Large-scale shape (canyon valleys)
                half shape = FBm3D(samplePos, 2);

                // Medium-scale detail (boiling effect)
                half detail = FBm3D(samplePos * 4.0 + half3(100.0, 0.0, 50.0), 2);

                // Fine turbulence
                half turbulence = FBm3D(samplePos * 8.0 + half3(200.0, 100.0, 0.0), 1) * 0.3;

                // Combine: 60% shape, 30% detail, 10% turbulence
                half density = shape * 0.6 + detail * 0.3 + turbulence * 0.1;

                // Non-linear remap for more defined valleys
                density = smoothstep(0.15, 0.85, density);

                // ---- Base alpha from noise (always visible where noise > 0) ----
                half alpha = density * _VeilColor.a;

                // ---- Distance-based fog (blends COLOR, not alpha!) ----
                // This ensures veil is visible at all distances
                float dist = length(_WorldSpaceCameraPos - input.worldPos);
                half fogFactor = saturate(dist / _FogDistance);
                half fogAmount = smoothstep(0.0, 1.0, fogFactor);

                // ---- Base color ----
                half3 finalColor = _VeilColor.rgb;

                // ---- Lightning ----
                if (_LightningIntensity > 0.01)
                {
                    // Animated lightning noise
                    half3 lightningPos = samplePos * 2.0 + half3(0, time * 5.0, 0);
                    half lightningNoise = FBm3D(lightningPos, 1);

                    // Threshold for bright streaks
                    half lightning = smoothstep(0.7, 0.9, lightningNoise) * _LightningIntensity;

                    // Add lightning glow to color
                    finalColor += _LightningColor.rgb * lightning * 2.0;
                }

                // ---- Apply fog (blend with fog color, not alpha!) ----
                // This is the KEY fix: fog affects color, alpha stays noise-based
                finalColor = lerp(finalColor, _FogColor.rgb, fogAmount * 0.7);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}