// Cloudy Navigator — подложка превью.
//
// Фон превью не может быть ни цветом камеры (тогда нет градиента), ни Graphics.Blit с
// clearFlags = Depth: URP при Depth-очистке дописывает в кадр небо сцены, и в превью появлялись
// розовое небо мира и диск солнца. Поэтому фон — обычная геометрия превью: квад на слое превью
// с этим unlit-шейдером, камера очищается в SolidColor. Ни свет, ни скайбокс сцены не участвуют.
Shader "Aura/CN/Preview Backdrop"
{
    Properties
    {
        _BaseMap ("Gradient", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Off
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
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
