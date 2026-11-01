// PrimumImpostor.shader — ProjectC/PrimumImpostor (T-CULL01 / Stage 7, H7: L2-импостор города, 12+ км).
//
// Один квад на город (4 вершины в [-1..1]), разворачивается к камере в вершинном шейдере.
// Атлас: _Grid.x азимутов × _Grid.y высот (до 4), запечён в редакторе из L1-мешей (PrimumHlodBaker).
// Кадр выбирается по направлению «центр города → камера» (ближайший азимут и высота) — одно и то же
// для всех 4 вершин, поэтому состояния нет: FO-класс 🟢, рантайм хранит только скаляры.
//   _AlbedoTex: RGB — альбедо (sRGB), A — покрытие (силуэт, alpha-test);
//   _NightTex:  RGB — ночные огни (линейно, × _NightScale), A — нормаль.y·0.5+0.5 (освещение).
// Освещение — UniversalFragmentPBR (как L0/L1) по приближённой нормали (крыши вверх, стены к зрителю),
// metallic/smoothness — среднее по городу, туман URP;
// огни добавляются после тумана с ослаблением _LightsFogKeep, чтобы читались сквозь дымку.
Shader "ProjectC/PrimumImpostor"
{
    Properties
    {
        _AlbedoTex("Albedo Atlas", 2D) = "black" {}
        _NightTex("Night Atlas", 2D) = "black" {}
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.05, 0.95)) = 0.5
        _Grid("Grid (azimuths, rows)", Vector) = (8, 4, 0, 0)
        _RowElev("Row elevations, deg", Vector) = (0, 8, 20, 45)
        _RowHalfH("Row half heights, m", Vector) = (100, 200, 400, 700)
        _HalfW("Half width, m", Float) = 900
        _NightScale("Night Scale", Float) = 4
        _LightsFogKeep("Lights fog keep", Range(0, 1)) = 0.6
        _Metallic("Metallic (bake average)", Range(0, 1)) = 0
        _Smoothness("Smoothness (bake average)", Range(0, 1)) = 0.3
        _NightOverride("Night Override (-1 = global)", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _AlbedoTex_ST;
            float4 _NightTex_ST;
            half4 _Tint;
            half _Cutoff;
            float4 _Grid;
            float4 _RowElev;
            float4 _RowHalfH;
            float _HalfW;
            half _NightScale;
            half _LightsFogKeep;
            half _NightOverride;
            half _Metallic;
            half _Smoothness;
        CBUFFER_END

        TEXTURE2D(_AlbedoTex); SAMPLER(sampler_AlbedoTex);
        TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);

        float _PrimumHlodNight;

        struct ImpAttributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct ImpFrame
        {
            float3 positionWS;
            float2 uv;
            float3 viewHorizontal;
        };

        float RowValue(float4 v, float row)
        {
            return row < 0.5 ? v.x : (row < 1.5 ? v.y : (row < 2.5 ? v.z : v.w));
        }

        ImpFrame ImpostorFrame(float2 corner)
        {
            ImpFrame f;
            float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
            float3 toCam = _WorldSpaceCameraPos.xyz - centerWS;
            float dist = max(length(toCam), 1e-3);
            float3 v = toCam / dist;

            // Кадр атласа: ближайший азимут (0 = камера на +Z от города) и ближайшая высота.
            float azSteps = max(_Grid.x, 1.0);
            float az = atan2(v.x, v.z);
            float col = fmod(round(az / (2.0 * PI) * azSteps) + azSteps, azSteps);
            float el = degrees(asin(clamp(v.y, -1.0, 1.0)));
            float row = 0.0;
            float best = abs(el - _RowElev.x);
            if (_Grid.y > 1.5 && abs(el - _RowElev.y) < best) { row = 1.0; best = abs(el - _RowElev.y); }
            if (_Grid.y > 2.5 && abs(el - _RowElev.z) < best) { row = 2.0; best = abs(el - _RowElev.z); }
            if (_Grid.y > 3.5 && abs(el - _RowElev.w) < best) { row = 3.0; }
            float halfH = RowValue(_RowHalfH, row);

            // Базис как у LookRotation(-v, up) в bake: квад смотрит в камеру.
            float3 fwd = -v;
            float3 up0 = abs(fwd.y) > 0.995 ? float3(0, 0, 1) : float3(0, 1, 0);
            float3 right = normalize(cross(up0, fwd));
            float3 up = cross(fwd, right);

            f.positionWS = centerWS + right * (corner.x * _HalfW) + up * (corner.y * halfH);
            f.uv = float2((col + corner.x * 0.5 + 0.5) / azSteps, (row + corner.y * 0.5 + 0.5) / max(_Grid.y, 1.0));
            float3 h = float3(v.x, 0, v.z);
            f.viewHorizontal = dot(h, h) > 1e-6 ? normalize(h) : float3(0, 0, 1);
            return f;
        }

        half ImpNight()
        {
            return _NightOverride >= 0.0h ? (half)saturate(_NightOverride) : (half)saturate(_PrimumHlodNight);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend Off
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ImpVert
            #pragma fragment ImpFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct ImpVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewH : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ImpVaryings ImpVert(ImpAttributes input)
            {
                ImpVaryings o = (ImpVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                ImpFrame f = ImpostorFrame(input.positionOS.xy);
                o.positionWS = f.positionWS;
                o.positionCS = TransformWorldToHClip(f.positionWS);
                o.uv = f.uv;
                o.viewH = f.viewHorizontal;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 ImpFrag(ImpVaryings input) : SV_Target
            {
                half4 a = SAMPLE_TEXTURE2D(_AlbedoTex, sampler_AlbedoTex, input.uv);
                clip(a.a - _Cutoff);
                half4 nt = SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, input.uv);
                half ny = saturate(nt.a) * 2.0h - 1.0h;
                half3 n = normalize(half3(0, 1, 0) * ny + (half3)input.viewH * sqrt(saturate(1.0h - ny * ny)));

                // То же освещение, что у L0/L1 (UniversalFragmentPBR), metallic/smoothness — среднее по городу.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = a.rgb * _Tint.rgb;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1.0h;
                surface.alpha = 1.0h;
                surface.normalTS = half3(0, 0, 1);

                half3 lit = UniversalFragmentPBR(inputData, surface).rgb;
                lit = MixFog(lit, input.fogFactor);
                half3 lights = nt.rgb * _NightScale * ImpNight();
                // ComputeFogIntensity: 1 = тумана нет, 0 = полностью туман.
                half fogKeep = IsFogEnabled() ? (half)ComputeFogIntensity(input.fogFactor) : 1.0h;
                lit += lights * lerp(_LightsFogKeep, 1.0h, fogKeep);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ImpDepthVert
            #pragma fragment ImpDepthFrag
            #pragma multi_compile_instancing

            struct ImpDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ImpDepthVaryings ImpDepthVert(ImpAttributes input)
            {
                ImpDepthVaryings o = (ImpDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                ImpFrame f = ImpostorFrame(input.positionOS.xy);
                o.positionCS = TransformWorldToHClip(f.positionWS);
                o.uv = f.uv;
                return o;
            }

            half ImpDepthFrag(ImpDepthVaryings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_AlbedoTex, sampler_AlbedoTex, input.uv).a;
                clip(a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ImpNormalsVert
            #pragma fragment ImpNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct ImpNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewH : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ImpNormalsVaryings ImpNormalsVert(ImpAttributes input)
            {
                ImpNormalsVaryings o = (ImpNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                ImpFrame f = ImpostorFrame(input.positionOS.xy);
                o.positionCS = TransformWorldToHClip(f.positionWS);
                o.uv = f.uv;
                o.viewH = f.viewHorizontal;
                return o;
            }

            half4 ImpNormalsFrag(ImpNormalsVaryings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_AlbedoTex, sampler_AlbedoTex, input.uv).a;
                clip(a - _Cutoff);
                half ny = saturate(SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, input.uv).a) * 2.0h - 1.0h;
                float3 n = normalize(float3(0, 1, 0) * ny + input.viewH * sqrt(saturate(1.0 - ny * ny)));
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
            #else
                return half4(NormalizeNormalPerPixel(n), 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
