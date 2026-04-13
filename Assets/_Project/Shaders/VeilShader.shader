// VeilShader.shader — Шейдер завесы для Project C
// Отличается от CloudGhibli: плоский, непрозрачный, с молниями и depth fade
// URP Unlit, Exponential Fog эмуляция, Lightning overlay, Depth Fade на горизонте

Shader "Project C/Clouds/VeilShader"
{
    Properties
    {
        _VeilColor ("Цвет завесы", Color) = (0.176, 0.106, 0.306, 1.0)
        _FogDensity ("Плотность тумана", Float) = 0.003
        _LightningColor ("Цвет молний", Color) = (0.7, 0.4, 1.0, 1.0)
        _LightningIntensity ("Интенсивность молний", Range(0, 1)) = 0.0
        _DepthFadeStart ("Начало растворения", Float) = 100.0
        _DepthFadeEnd ("Конец растворения", Float) = 500.0
        _NoiseTex ("Текстура шума", 2D) = "white" {}
        _NoiseScale ("Масштаб шума", Float) = 10.0
        _NoiseSpeed ("Скорость шума", Float) = 0.1
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---- Properties ----
            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(half4, _VeilColor)
            UNITY_DEFINE_INSTANCED_PROP(half4, _LightningColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            half _FogDensity;
            half _LightningIntensity;
            half _DepthFadeStart;
            half _DepthFadeEnd;
            half _NoiseScale;
            half _NoiseSpeed;

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            // ---- Simplex-like 2D noise (для автономной работы без текстур) ----
            half Hash21(half2 p)
            {
                p = frac(p * half2(123.34, 456.21));
                p += dot(p, p + half2(45.32, 78.91));
                return frac(p.x * p.y);
            }

            half Noise(half2 uv)
            {
                // Простой value noise с 2 октавами
                half2 id = floor(uv);
                half2 lf = frac(uv) * 2.0 - 1.0;
                lf = lf * lf * (3.0 - 2.0 * lf); // smoothstep

                half tl = Hash21(id);
                half tr = Hash21(id + half2(1, 0));
                half bl = Hash21(id + half2(0, 1));
                half br = Hash21(id + half2(1, 1));

                half top = lerp(tl, tr, lf.x);
                half bot = lerp(bl, br, lf.x);
                return lerp(top, bot, lf.y);
            }

            half FBNoise(half2 uv, int octaves)
            {
                half val = 0.0;
                half amp = 0.5;
                half freq = 1.0;
                for (int i = 0; i < octaves; i++)
                {
                    val += amp * Noise(uv * freq);
                    freq *= 2.0;
                    amp *= 0.5;
                }
                return val;
            }

            // ---- Vertex Input/Output ----
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.worldPos = vertexInput.positionWS;
                output.uv = input.positionOS.xz * 0.001; // Масштаб для UV
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // ---- Depth Fade (растворение на горизонте) ----
                float dist = length(_WorldSpaceCameraPos.xz - input.worldPos.xz);
                half depthFade = saturate((dist - _DepthFadeStart) / (_DepthFadeEnd - _DepthFadeStart));

                // ---- Noise анимация ----
                half2 noiseUV = input.uv * _NoiseScale + _Time.y * _NoiseSpeed * half2(1.0, 0.3);
                half noiseVal = FBNoise(noiseUV, 3);

                // ---- Базовый цвет завесы ----
                half4 veilColor = UNITY_ACCESS_INSTANCED_PROP(Props, _VeilColor);

                // Добавляем шум к альфе — создаём "дымовую" поверхность
                half alpha = veilColor.a * (0.85 + noiseVal * 0.15);
                alpha *= depthFade; // Применяем depth fade

                // ---- Молнии ----
                half lightning = 0.0;
                if (_LightningIntensity > 0.01)
                {
                    // Молнии — яркие линии с случайной позицией
                    half2 lightningUV = input.uv * _NoiseScale * 2.0;
                    half lightningNoise = FBNoise(lightningUV + _Time.yy * 50.0, 2);
                    lightning = smoothstep(0.7, 0.95, lightningNoise) * _LightningIntensity;
                }

                // Комбинируем
                half3 finalColor = veilColor.rgb;
                finalColor = lerp(finalColor, UNITY_ACCESS_INSTANCED_PROP(Props, _LightningColor).rgb, lightning);

                // Добавляем яркость молний
                finalColor += UNITY_ACCESS_INSTANCED_PROP(Props, _LightningColor).rgb * lightning * 2.0;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
