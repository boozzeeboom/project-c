// PrimumHlod.shader — ProjectC/PrimumHlod (T-CULL01 / Stage 7, H7: L1 HLOD города, 3–12 км).
//
// Один материал на весь город: цвет исходного материала запечён в вершины (sRGB, RGB), класс эмиссии —
// в альфе вершины (PrimumHlodBaker):
//   0      — обычная поверхность;
//   85     — окно, ночью не горит;
//   170    — окно, ночью горит (_WindowColor × _PrimumHlodNight);
//   255    — лампа/фонарь (горит всегда, как исходный Unlit-материал).
// Освещение — как у L0 (UniversalFragmentPBR, metallic/smoothness исходного материала в UV0 вершины), туман URP.
// Теней нет (L1 дальше shadowDistance).
// _PrimumHlodNight — глобальный скаляр 0..1, пишет VisibilityDirector (по солнцу DayNightController).
// Проходы HlodBakeAlbedo / HlodBakeNight камера URP не рисует — их вызывает только bake L2 в редакторе.
// FO-safe: только object/world space текущего кадра, ничего не кэширует.
Shader "ProjectC/PrimumHlod"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
        [HDR] _WindowColor("Window Night Color", Color) = (2.2, 1.45, 0.75, 1)
        _LampIntensity("Lamp Intensity", Float) = 2.4
        _WindowDayDim("Window Albedo Dim", Range(0, 1)) = 0.55
        _NightOverride("Night Override (-1 = global)", Float) = -1
        _LampDepthBias("Lamp Depth Bias, m (lamps show through glass)", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            half4 _WindowColor;
            half _LampIntensity;
            half _WindowDayDim;
            half _NightOverride;
            float _LampDepthBias;
        CBUFFER_END

        float _PrimumHlodNight;

        struct HlodAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            half4 surface : TEXCOORD0; // x = metallic, y = smoothness исходного материала (UNorm8)
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Лампы (альфа 255) в L0 видны сквозь прозрачное стекло кожуха; в L1 стекло непрозрачно, поэтому
        // лампа сдвигается на _LampDepthBias к камере по оси вида (UNITY_MATRIX_V[2] — «назад» камеры;
        // так же для орто-bake). С 3+ км сдвиг 1.5 м — доли пикселя.
        float3 HlodPositionWS(HlodAttributes input)
        {
            float3 p = TransformObjectToWorld(input.positionOS.xyz);
            if (input.color.a > 0.83h) p += normalize(UNITY_MATRIX_V[2].xyz) * _LampDepthBias;
            return p;
        }

        half HlodNight()
        {
            return _NightOverride >= 0.0h ? (half)saturate(_NightOverride) : (half)saturate(_PrimumHlodNight);
        }

        void DecodeHlod(half4 c, half night, out half3 albedo, out half3 emission)
        {
            albedo = SRGBToLinear(c.rgb) * _Tint.rgb;
            emission = 0;
            half a = c.a;
            if (a > 0.83h)
            {
                emission = albedo * _LampIntensity;
                albedo *= 0.25h;
            }
            else if (a > 0.5h)
            {
                emission = _WindowColor.rgb * night;
                albedo *= _WindowDayDim;
            }
            else if (a > 0.16h)
            {
                albedo *= _WindowDayDim;
            }
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HlodVert
            #pragma fragment HlodFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct HlodVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half2 surface : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            HlodVaryings HlodVert(HlodAttributes input)
            {
                HlodVaryings o = (HlodVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = HlodPositionWS(input);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.surface = input.surface.xy;
                o.color = input.color;
                return o;
            }

            // То же освещение, что у L0 (URP Lit Shader Graph): UniversalFragmentPBR с metallic/smoothness
            // исходного материала — иначе на переходе 3 км металл и глянец (отражение неба) «гаснут».
            half4 HlodFrag(HlodVaryings input) : SV_Target
            {
                half3 albedo, emission;
                DecodeHlod(input.color, HlodNight(), albedo, emission);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = float4(0, 0, 0, 0);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = saturate(input.surface.x);
                surface.smoothness = saturate(input.surface.y);
                surface.occlusion = 1.0h;
                surface.emission = emission;
                surface.alpha = 1.0h;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return half4(color.rgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HlodDepthVert
            #pragma fragment HlodDepthFrag
            #pragma multi_compile_instancing

            struct HlodDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            HlodDepthVaryings HlodDepthVert(HlodAttributes input)
            {
                HlodDepthVaryings o = (HlodDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(HlodPositionWS(input));
                return o;
            }

            half HlodDepthFrag(HlodDepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HlodNormalsVert
            #pragma fragment HlodNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct HlodNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            HlodNormalsVaryings HlodNormalsVert(HlodAttributes input)
            {
                HlodNormalsVaryings o = (HlodNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(HlodPositionWS(input));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 HlodNormalsFrag(HlodNormalsVaryings input) : SV_Target
            {
            #if defined(_GBUFFER_NORMALS_OCT)
                float3 nn = normalize(input.normalWS);
                float2 oct = PackNormalOctQuadEncode(nn);
                float2 rem = saturate(oct * 0.5 + 0.5);
                return half4(PackFloat2To888(rem), 0.0);
            #else
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #endif
            }
            ENDHLSL
        }

        // ---- только для bake L2 в редакторе (PrimumHlodBaker), камера URP эти проходы не рисует ----

        Pass
        {
            Name "HlodBakeAlbedo"
            Tags { "LightMode" = "HlodBakeAlbedo" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HlodBakeVert
            #pragma fragment HlodBakeAlbedoFrag

            struct HlodBakeVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : COLOR;
            };

            HlodBakeVaryings HlodBakeVert(HlodAttributes input)
            {
                HlodBakeVaryings o = (HlodBakeVaryings)0;
                o.positionCS = TransformWorldToHClip(HlodPositionWS(input));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                return o;
            }

            half4 HlodBakeAlbedoFrag(HlodBakeVaryings input) : SV_Target
            {
                half3 albedo, emission;
                DecodeHlod(input.color, 0.0h, albedo, emission);
                // Лампы днём видны как светлые точки: добавляем их эмиссию в альбедо силуэта.
                return half4(saturate(albedo + emission * 0.25h), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HlodBakeNight"
            Tags { "LightMode" = "HlodBakeNight" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex HlodBakeVert
            #pragma fragment HlodBakeNightFrag

            struct HlodBakeVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : COLOR;
            };

            float _HlodBakeNightScale;

            HlodBakeVaryings HlodBakeVert(HlodAttributes input)
            {
                HlodBakeVaryings o = (HlodBakeVaryings)0;
                o.positionCS = TransformWorldToHClip(HlodPositionWS(input));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                return o;
            }

            half4 HlodBakeNightFrag(HlodBakeVaryings input) : SV_Target
            {
                half3 albedo, emission;
                DecodeHlod(input.color, 1.0h, albedo, emission);
                float scale = max(_HlodBakeNightScale, 0.001);
                half3 n = normalize(input.normalWS);
                // RGB — эмиссия / scale; A — вертикальная компонента нормали (для освещения импостора).
                return half4(saturate(emission / scale), saturate(n.y * 0.5h + 0.5h));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
