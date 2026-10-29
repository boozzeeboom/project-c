// Cloudy Navigator — студийный материал превью.
//
// Зачем свой шейдер: превью 88 px не должно зависеть ни от света сцены, ни от настроек URP.
// Раньше фигура освещалась единственным Directional-светом, и он смотрел от камеры в спину
// (Quaternion.Euler(28,152,0) → forward (0.41, −0.47, −0.78)), а ambientMode = Skybox брал
// почти чёрный окружающий свет из скайбокса мира. Итог — тёмный силуэт на тёмной подложке.
//
// Поэтому свет «вшит» в шейдер тремя фиксированными направлениями (key / fill / rim) плюс
// полусферическая подсветка снизу. Ни один Light в сцене не нужен, и превью не может
// перехватить main light у игровой камеры.
//
// Камера превью стоит в −Z и смотрит в +Z, значит «спереди» для фигуры — это −Z.
Shader "Aura/CN/Preview Studio"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _StudioGain ("Studio Gain", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Studio"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _StudioGain;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 n = normalize(input.normalWS);

                // key — сверху-слева-спереди, fill — справа-спереди ниже, rim — сзади сверху.
                half3 keyDir  = normalize(half3(-0.42, 0.72, -0.55));
                half3 fillDir = normalize(half3(0.66, 0.14, -0.48));
                half3 rimDir  = normalize(half3(0.16, 0.40, 0.90));

                half key  = saturate(dot(n, keyDir));
                half fill = saturate(dot(n, fillDir));
                half rim  = saturate(dot(n, rimDir));
                rim = rim * rim;

                // Полусферика: сверху небо, снизу отражённый от пола свет — тени не чёрные.
                half hemi = saturate(n.y * 0.5h + 0.5h);
                half3 ambient = lerp(half3(0.26, 0.29, 0.27), half3(0.66, 0.74, 0.70), hemi);

                half3 light = ambient
                            + key  * half3(1.15, 1.13, 1.05)
                            + fill * half3(0.40, 0.50, 0.54);

                // Свет не поднимается выше единицы: без этого светлые ткани (кожа #d3e2e6)
                // умножались на 1.35 и клиппились в чистый белый — голова становилась пятном.
                light = min(light, half3(1.0h, 1.0h, 1.0h));

                // Подъём тёмных тканей. Брюки #1f2620 и сапоги #121711 по яркости почти равны низу
                // подложки #101814, и на 88 px нижняя половина фигуры сливалась с фоном (замер:
                // Δ к фону 0.00 на ступнях, 0.03 на голенях против 0.37 на пальто). Подъём считается
                // от яркости самой ткани, поэтому светлые части он почти не трогает.
                half lum = dot(baseCol.rgb, half3(0.299h, 0.587h, 0.114h));
                half3 lift = saturate(1.0h - lum) * 0.14h * half3(0.62h, 0.78h, 0.70h);

                half3 col = baseCol.rgb * light
                          + lift
                          + rim * half3(0.42, 0.58, 0.52) * 0.55h;

                return half4(saturate(col * _StudioGain), 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
