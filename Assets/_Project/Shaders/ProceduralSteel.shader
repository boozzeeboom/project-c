// ProceduralSteel.shader — ProjectC/ProceduralSteel
// Сталь с процедурной ржавчиной на стыках. Ноль текстур.
// v7: deriv-ядро ВЕРНУЛОСЬ как аддитив для сглаженных изгибов (рецепт
// NormalObject→DDX/DDY→Length→Add→Noise→Smoothstep→Lerp — тот самый).
// Домен честно разделён: bake=жёсткие рёбра, deriv=плавные изгибы.
// На полосу назначаются эффекты: _EdgeRust (ржавчина), _EdgeWear (блик-зачистка).
// Пятна ржавчины на плоскостях — независимый слой (pattern × cavity × flow).
// FO-safe: только object-space, сид — свойство материала.
// Освещение — стандартный URP PBR (UniversalFragmentPBR), Forward.
// T-STEEL01. SPEC: docs/Materials/SteelRust/README.md

Shader "ProjectC/ProceduralSteel"
{
    Properties
    {
        [Header(Steel Base)]
        [MainColor] _BaseColor("Steel Tint", Color) = (0.45, 0.47, 0.5, 1)
        _SteelRoughness("Steel Roughness", Range(0.05, 1.0)) = 0.42
        [Header(Rust Blotches)]
        _RustColorDark("Rust Dark", Color) = (0.13, 0.06, 0.03, 1)
        _RustColorMid("Rust Mid", Color) = (0.45, 0.2, 0.07, 1)
        _RustColorLight("Rust Light (dry edge)", Color) = (0.72, 0.45, 0.2, 1)
        _RustAmount("Rust Amount", Range(0.0, 1.0)) = 0.5
        _RustScaleLarge("Noise Scale Large (1/m)", Float) = 0.6
        _RustScaleMid("Noise Scale Mid (1/m)", Float) = 3.0
        _RustScaleFine("Noise Scale Fine (1/m)", Float) = 18.0
        _CavityTightness("Cavity Tightness", Range(0.5, 4.0)) = 1.5
        _UpBias("Up-Facing Bias", Range(0.0, 1.0)) = 0.5
        _DripStrength("Drip Streaks", Range(0.0, 1.0)) = 0.5
        _BumpStrength("Rust Bump", Range(0.0, 1.0)) = 0.5
        _Seed("Seed", Float) = 0.0
        [Header(Edge Band)]
        _EdgeBandMaster("Edge Band Master", Range(0.0, 2.0)) = 1.0
        _DerivGain("Derivative Gain (curvature)", Range(0.0, 4.0)) = 0.25
        _EdgeWidth("Edge Band Width", Range(0.0, 1.0)) = 0.35
        _EdgeNoise("Edge Band Breakup", Range(0.0, 1.0)) = 0.45
        _EdgeToCavity("Edge To Cavity", Range(0.0, 1.5)) = 1.0
        _EdgeRust("Rust On Edges", Range(0.0, 2.0)) = 0.8
        _EdgeWear("Wear Glint On Edges", Range(0.0, 1.0)) = 0.6
        [Header(Masks)]
        // 0 = чистая процедура (дефолт). ВАЖНО: незапечённые меши Unity отдаёт
        // шейдеру как БЕЛЫЕ (1,1,1,1) — с включённым тумблером это зальёт маски.
        // 1 = использовать печёные R/G (только после EdgeMaskBaker).
        [Toggle] _UseVertexColors("Use Vertex Colors (R=cavity G=edge B=var)", Float) = 0.0
        [Enum(Off, 0, Rust Mask, 1, Cavity, 2, Edge Band, 3, Pattern, 4)] _DebugView("Debug View", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

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

            #pragma vertex SteelPassVertex
            #pragma fragment SteelPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _SteelRoughness;
                half4 _RustColorDark;
                half4 _RustColorMid;
                half4 _RustColorLight;
                half _RustAmount;
                half _RustScaleLarge;
                half _RustScaleMid;
                half _RustScaleFine;
                half _CavityTightness;
                half _UpBias;
                half _DripStrength;
                half _BumpStrength;
                half _Seed;
                half _EdgeBandMaster;
                half _DerivGain;
                half _EdgeWidth;
                half _EdgeNoise;
                half _EdgeToCavity;
                half _EdgeRust;
                half _EdgeWear;
                half _UseVertexColors;
                half _DebugView;
            CBUFFER_END

            // Хэш без sin — стабилен на всех GPU.
            float PC_Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise 3D, трилинейная интерполяция.
            float PC_ValueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(PC_Hash13(i + float3(0.0, 0.0, 0.0)), PC_Hash13(i + float3(1.0, 0.0, 0.0)), u.x),
                         lerp(PC_Hash13(i + float3(0.0, 1.0, 0.0)), PC_Hash13(i + float3(1.0, 1.0, 0.0)), u.x), u.y),
                    lerp(lerp(PC_Hash13(i + float3(0.0, 0.0, 1.0)), PC_Hash13(i + float3(1.0, 0.0, 1.0)), u.x),
                         lerp(PC_Hash13(i + float3(0.0, 1.0, 1.0)), PC_Hash13(i + float3(1.0, 1.0, 1.0)), u.x), u.y), u.z);
            }

            struct SteelAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct SteelVaryings
            {
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half3 normalOS : TEXCOORD8;
                half fogFactor : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
                #ifdef DYNAMICLIGHTMAP_ON
                    float2 dynamicLightmapUV : TEXCOORD6;
                #endif
                #ifdef USE_APV_PROBE_OCCLUSION
                    float4 probeOcclusion : TEXCOORD7;
                #endif
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            SteelVaryings SteelPassVertex(SteelAttributes input)
            {
                SteelVaryings output = (SteelVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionWS = vertexInput.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.normalWS = normalInput.normalWS;
                output.color = input.color;

                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif
                output.fogFactor = fogFactor;

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
                    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

                output.positionCS = vertexInput.positionCS;
                return output;
            }

            void SteelInitializeBakedGI(SteelVaryings input, inout InputData inputData)
            {
                #if defined(_SCREEN_SPACE_IRRADIANCE)
                    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
                #elif defined(DYNAMICLIGHTMAP_ON)
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
                        GetAbsolutePositionWS(inputData.positionWS),
                        inputData.normalWS,
                        inputData.viewDirectionWS,
                        input.positionCS.xy,
                        input.probeOcclusion,
                        inputData.shadowMask);
                #else
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                #endif
            }

            half4 SteelPassFragment(SteelVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // FO-safe: весь паттерн — от object-space позиции, нормированной
                // на масштаб объекта (плотность шума в мировых метрах, сдвиг не влияет).
                float3 op = input.positionOS;
                float3 objScale = float3(length(unity_ObjectToWorld[0].xyz), length(unity_ObjectToWorld[1].xyz), length(unity_ObjectToWorld[2].xyz));
                objScale = max(objScale, float3(1e-4, 1e-4, 1e-4));
                float3 opW = op / objScale;
                float seed = (float)_Seed;

                half3 nW = normalize(input.normalWS);

                float nLarge = PC_ValueNoise3D(opW * (float)_RustScaleLarge + seed);
                float nMid = PC_ValueNoise3D(opW * (float)_RustScaleMid - seed * 1.37);
                float nFine = PC_ValueNoise3D(opW * (float)_RustScaleFine + seed * 2.13);
                float pattern = nLarge * 0.55 + nMid * 0.30 + nFine * 0.15;

                // --- КАНАЛ ГРАНЕЙ: печёный G + кривизна сглаженных изгибов.
                // Стабилен: всё в object/world-locked величинах, никакой зависимости
                // от камеры/света. Два домена дополняют друг друга:
                // bake ловит ЖЁСТКИЕ рёбра (сплит-вершины), curv — ПЛАВНЫЕ изгибы.
                // curv = |dN|/|dP| — истинная кривизна 1/м: не зависит от дистанции
                // и разрешения (футпринт сокращается), инвариантна к сдвигу FO.
                // На жёстких примитивах curv = 0 (поле постоянно) — это норма.
                // Защита от незапечённого белого: меш без цветов Unity отдаёт как
                // (1,1,1); ProBuilder красит дефолт в белый. Чистый белый = «нет данных».
                half3 vc = input.color.rgb;
                half useVC = _UseVertexColors * (1.0 - step(0.999, vc.r) * step(0.999, vc.g) * step(0.999, vc.b));
                half cut = 1.0 - (float)_EdgeWidth;
                half bakedShaped = smoothstep(cut, cut + 0.08, vc.g * useVC);
                half3 nO = normalize(input.normalOS);
                half footP = length(ddx(input.positionWS)) + length(ddy(input.positionWS));
                half curv = (length(ddx(nO)) + length(ddy(nO))) / max(footP, 1e-6);
                half derivBand = saturate(curv * _DerivGain);
                half rawBand = max(bakedShaped, derivBand);
                half edgeBand = saturate(rawBand * (1.0 - (float)_EdgeNoise * (1.0 - nMid)) * _EdgeBandMaster);
                half varIn = lerp(0.5h, vc.b, useVC);

                // Потёки: растяжка шума по вертикали (медленное изменение по Y).
                float drip = PC_ValueNoise3D(float3(opW.x * (float)_RustScaleMid, opW.y * (float)_RustScaleMid * 0.25, opW.z * (float)_RustScaleMid) - seed);

                half upFace = saturate(nW.y * 0.5 + 0.5);

                // --- ПЯТНА РЖАВЧИНЫ (независимый слой на плоскостях).
                half cavIn = lerp(0.12h, vc.r, useVC);
                cavIn = saturate(cavIn + edgeBand * _EdgeToCavity);
                half cavity = saturate(pow(cavIn, (float)_CavityTightness) * (0.55 + 0.9 * nMid));
                half flowTerm = (upFace - 0.5) * _UpBias * 0.5 + (drip - 0.5) * _DripStrength * 0.5;
                half rustField = pattern * 0.42 + cavity * 0.48 + 0.10 + flowTerm * 0.35
                    + edgeBand * _EdgeRust * 0.25;
                half t = 1.0 - _RustAmount * 0.85;
                half rustMask = smoothstep(t - 0.15, t + 0.15, rustField);

                // --- ЭФФЕКТЫ НА ГРАНЯХ: ядро полосы + рванье fine-шумом.
                half edgeCore = smoothstep(0.55, 0.95, edgeBand + (nFine - 0.5) * 0.3) * step(0.001, _EdgeWear);

                // Отладочные виды для сравнения версий.
                if (_DebugView > 0.5 && _DebugView < 1.5) return half4(rustMask.xxx, 1.0);
                if (_DebugView >= 1.5 && _DebugView < 2.5) return half4(saturate(cavity).xxx, 1.0);
                if (_DebugView >= 2.5 && _DebugView < 3.5) return half4(saturate(edgeBand).xxx, 1.0);
                if (_DebugView >= 3.5) return half4(pattern.xxx, 1.0);

                // База стали с вариацией панелей и волнистостью проката.
                half3 steel = _BaseColor.rgb * (0.82 + 0.36 * varIn);
                steel *= 0.96 + 0.08 * nMid;

                // Градиент коррозии по паттерну.
                half3 rustCol = lerp(_RustColorDark.rgb, _RustColorMid.rgb, smoothstep(0.15, 0.65, pattern));
                rustCol = lerp(rustCol, _RustColorLight.rgb, smoothstep(0.65, 0.95, pattern) * 0.6);

                half3 albedo = lerp(steel, rustCol, rustMask);
                half metallic = lerp(0.9, 0.0, rustMask);
                half smoothness = lerp(1.0 - _SteelRoughness, 0.1, rustMask);

                albedo = lerp(albedo, half3(0.78, 0.79, 0.82), edgeCore * 0.55);
                metallic = lerp(metallic, 1.0, edgeCore * 0.8);
                smoothness = lerp(smoothness, 0.6, edgeCore * 0.8);

                // Дешёвый bump ржавчины (сила 0 = Far-вариант).
                float bumpN = PC_ValueNoise3D(opW * (float)_RustScaleFine * 1.31 - seed * 0.7) - 0.5;
                nW = normalize(nW + (nFine - 0.5 + bumpN) * ((float)_BumpStrength * rustMask * 0.6));

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0.0, 0.0, 0.0);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0.0, 0.0, 1.0);
                surfaceData.emission = half3(0.0, 0.0, 0.0);
                surfaceData.occlusion = 1.0 - 0.45 * rustMask;
                surfaceData.alpha = 1.0;
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(nW);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                SteelInitializeBakedGI(input, inputData);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
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
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}