// EdgeDetection.shader — Borderlands-style post-process edge detection (URP)
// Sobel on depth + normals. Adaptive color. Distance-based falloff.
// Pencil stroke: tapered ends via edge-direction analysis.
// Fullscreen blend pass — UV from SV_POSITION (no Y-flip).

Shader "Hidden/ProjectC/EdgeDetection"
{
    Properties
    {
        [Header(Edge)]
        _EdgeColor ("Edge Color", Color) = (0.02, 0.02, 0.04, 1.0)
        _EdgeWidth ("Edge Width", Range(0.1, 8.0)) = 1.5

        [Header(Distance Falloff)]
        _MaxEdgeDistance ("Max Edge Distance", Range(1.0, 500.0)) = 80.0
        _DepthFalloff ("Depth Falloff", Range(0.0, 2.0)) = 0.8

        [Header(Depth Edges)]
        [ToggleUI] _UseDepthEdges ("Use Depth Edges", Float) = 1
        _DepthSensitivity ("Depth Sensitivity", Range(0.1, 8.0)) = 2.0
        _DepthThreshold ("Depth Threshold", Range(0.0, 0.5)) = 0.04

        [Header(Normal Edges)]
        [ToggleUI] _UseNormalEdges ("Use Normal Edges", Float) = 1
        _NormalSensitivity ("Normal Sensitivity", Range(0.1, 4.0)) = 0.8
        _NormalThreshold ("Normal Threshold", Range(0.0, 0.8)) = 0.25

        [Header(Adaptive Color)]
        [ToggleUI] _UseAdaptiveColor ("Adaptive Color", Float) = 0
        _AdaptiveStrength ("Adaptive Strength", Range(0.0, 1.0)) = 0.6

        [Header(Pencil Stroke)]
        [ToggleUI] _UsePencilStroke ("Pencil Stroke", Float) = 0
        _PencilTaper ("Taper Amount", Range(0.0, 1.0)) = 0.7
        _PencilGrain ("Grain Strength", Range(0.0, 0.3)) = 0.08

        [Header(Softness)]
        _LineSoftness ("Line Softness", Range(0.005, 0.2)) = 0.03
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        // Adaptive color: bound by RenderFeature via cmd.SetGlobalTexture(_EdgeSourceTex).
        TEXTURE2D(_EdgeSourceTex);
        SAMPLER(sampler_EdgeSourceTex);

        // Optional per-object mask rendered by EdgeDetectionRenderFeature.
        TEXTURE2D(_EdgeTargetMask);
        SAMPLER(sampler_EdgeTargetMask);

        half4 _EdgeColor;
        float _EdgeWidth;
        float _MaxEdgeDistance;
        float _DepthFalloff;
        float _UseDepthEdges;
        float _DepthSensitivity;
        float _DepthThreshold;
        float _UseNormalEdges;
        float _NormalSensitivity;
        float _NormalThreshold;
        float _UseAdaptiveColor;
        float _AdaptiveStrength;
        float _UsePencilStroke;
        float _PencilTaper;
        float _PencilGrain;
        float _LineSoftness;

        half4 _TargetEdgeColor;
        float _UseEdgeTargetMask;
        float _TargetExcludeFromGlobal;
        float _TargetUseSettings;
        float _TargetEdgeWidth;
        float _TargetMaxEdgeDistance;
        float _TargetDepthFalloff;
        float _TargetUseDepthEdges;
        float _TargetDepthSensitivity;
        float _TargetDepthThreshold;
        float _TargetUseNormalEdges;
        float _TargetNormalSensitivity;
        float _TargetNormalThreshold;
        float _TargetUseAdaptiveColor;
        float _TargetAdaptiveStrength;
        float _TargetUsePencilStroke;
        float _TargetPencilTaper;
        float _TargetPencilGrain;
        float _TargetLineSoftness;

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
        }

        float Hash2D(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float SampleDepthLinear(float2 uv)
        {
            return Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
        }

        float SobelDepth(float2 uv, float2 texelSize, float thickness)
        {
            float2 offsets[8] = {
                float2(-1, 1), float2(0, 1), float2(1, 1),
                float2(-1, 0),               float2(1, 0),
                float2(-1,-1), float2(0,-1), float2(1,-1)
            };
            float c = SampleDepthLinear(uv);
            float m = 0;
            for (int i = 0; i < 8; i++)
                m = max(m, abs(c - SampleDepthLinear(uv + offsets[i] * texelSize * thickness)));
            return m;
        }

        float SobelNormal(float2 uv, float2 texelSize, float thickness)
        {
            float2 offsets[8] = {
                float2(-1, 1), float2(0, 1), float2(1, 1),
                float2(-1, 0),               float2(1, 0),
                float2(-1,-1), float2(0,-1), float2(1,-1)
            };
            float3 c = SampleSceneNormals(uv);
            if (dot(c, c) < 0.001) return 0;
            float m = 0;
            for (int i = 0; i < 8; i++)
            {
                float3 s = SampleSceneNormals(uv + offsets[i] * texelSize * thickness);
                if (dot(s, s) < 0.001) continue;
                m = max(m, 1.0 - abs(dot(c, s)));
            }
            return m;
        }

        float2 SobelDepthDir(float2 uv, float2 texelSize, float thickness)
        {
            float tl = SampleDepthLinear(uv + float2(-1,  1) * texelSize * thickness);
            float t  = SampleDepthLinear(uv + float2( 0,  1) * texelSize * thickness);
            float tr = SampleDepthLinear(uv + float2( 1,  1) * texelSize * thickness);
            float l  = SampleDepthLinear(uv + float2(-1,  0) * texelSize * thickness);
            float r  = SampleDepthLinear(uv + float2( 1,  0) * texelSize * thickness);
            float bl = SampleDepthLinear(uv + float2(-1, -1) * texelSize * thickness);
            float b  = SampleDepthLinear(uv + float2( 0, -1) * texelSize * thickness);
            float br = SampleDepthLinear(uv + float2( 1, -1) * texelSize * thickness);

            float gx = (-tl + tr - 2.0 * l + 2.0 * r - bl + br);
            float gy = (-tl - 2.0 * t - tr + bl + 2.0 * b + br);
            return float2(gx, gy);
        }

        float PencilTaper(float2 uv, float2 texelSize, float thickness)
        {
            float2 g = SobelDepthDir(uv, texelSize, thickness);
            float mag = length(g);
            if (mag < 0.0001) return 1.0;

            float2 edgeDir = normalize(float2(-g.y, g.x));
            float stepDist = thickness * 4.0;
            float2 alongPos = uv + edgeDir * texelSize * stepDist;
            float2 alongNeg = uv - edgeDir * texelSize * stepDist;

            float2 ga = SobelDepthDir(alongPos, texelSize, thickness);
            float2 gb = SobelDepthDir(alongNeg, texelSize, thickness);

            float magA = length(ga);
            float magB = length(gb);
            float contA = saturate(magA / (mag + 0.0001));
            float contB = saturate(magB / (mag + 0.0001));
            float continuity = min(contA, contB);

            return smoothstep(0.0, 0.6, continuity);
        }

        float SampleTargetMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_EdgeTargetMask, sampler_EdgeTargetMask, uv).r;
        }

        float TargetMaskCoverage(float2 uv, float2 texelSize, float radius)
        {
            if (_UseEdgeTargetMask < 0.5) return 0.0;

            float2 offset = texelSize * max(1.0, radius);
            float coverage = SampleTargetMask(uv);
            coverage = max(coverage, SampleTargetMask(uv + float2( offset.x, 0)));
            coverage = max(coverage, SampleTargetMask(uv + float2(-offset.x, 0)));
            coverage = max(coverage, SampleTargetMask(uv + float2(0,  offset.y)));
            coverage = max(coverage, SampleTargetMask(uv + float2(0, -offset.y)));
            coverage = max(coverage, SampleTargetMask(uv + offset));
            coverage = max(coverage, SampleTargetMask(uv - offset));
            coverage = max(coverage, SampleTargetMask(uv + float2( offset.x, -offset.y)));
            coverage = max(coverage, SampleTargetMask(uv + float2(-offset.x,  offset.y)));
            return step(0.01, coverage);
        }
        ENDHLSL

        Pass
        {
            Name "EdgeDetection"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionCS.xy * rcp(_ScreenParams.xy);
                float2 texelSize = rcp(_ScreenParams.xy);

                float targetCoverage = TargetMaskCoverage(
                    uv,
                    texelSize,
                    max(_EdgeWidth, _TargetEdgeWidth));

                if (targetCoverage > 0.5 &&
                    _TargetUseSettings < 0.5 &&
                    _TargetExcludeFromGlobal > 0.5)
                    return half4(0, 0, 0, 0);

                float useTarget = (targetCoverage > 0.5 && _TargetUseSettings > 0.5) ? 1.0 : 0.0;
                float edgeWidth = lerp(_EdgeWidth, _TargetEdgeWidth, useTarget);
                float maxEdgeDistance = lerp(_MaxEdgeDistance, _TargetMaxEdgeDistance, useTarget);
                float depthFalloff = lerp(_DepthFalloff, _TargetDepthFalloff, useTarget);
                float useDepthEdges = lerp(_UseDepthEdges, _TargetUseDepthEdges, useTarget);
                float depthSensitivity = lerp(_DepthSensitivity, _TargetDepthSensitivity, useTarget);
                float depthThreshold = lerp(_DepthThreshold, _TargetDepthThreshold, useTarget);
                float useNormalEdges = lerp(_UseNormalEdges, _TargetUseNormalEdges, useTarget);
                float normalSensitivity = lerp(_NormalSensitivity, _TargetNormalSensitivity, useTarget);
                float normalThreshold = lerp(_NormalThreshold, _TargetNormalThreshold, useTarget);
                float useAdaptiveColor = lerp(_UseAdaptiveColor, _TargetUseAdaptiveColor, useTarget);
                float adaptiveStrength = lerp(_AdaptiveStrength, _TargetAdaptiveStrength, useTarget);
                float usePencilStroke = lerp(_UsePencilStroke, _TargetUsePencilStroke, useTarget);
                float pencilTaper = lerp(_PencilTaper, _TargetPencilTaper, useTarget);
                float pencilGrain = lerp(_PencilGrain, _TargetPencilGrain, useTarget);
                float lineSoftness = lerp(_LineSoftness, _TargetLineSoftness, useTarget);
                half4 edgeColor = lerp(_EdgeColor, _TargetEdgeColor, useTarget);

                float depth = SampleDepthLinear(uv);
                float thickness = edgeWidth * saturate(1.0 - pow(
                    saturate(depth / (maxEdgeDistance * 0.01)), depthFalloff));
                if (thickness < 0.05) return half4(0, 0, 0, 0);

                float edge = 0;

                if (useDepthEdges > 0.5)
                {
                    float d = SobelDepth(uv, texelSize, thickness);
                    edge = max(edge, smoothstep(
                        depthThreshold - lineSoftness,
                        depthThreshold + lineSoftness,
                        d * depthSensitivity));
                }

                if (useNormalEdges > 0.5)
                {
                    float n = SobelNormal(uv, texelSize, thickness);
                    edge = max(edge, smoothstep(
                        normalThreshold - lineSoftness,
                        normalThreshold + lineSoftness,
                        n * normalSensitivity));
                }

                edge = saturate(edge);
                if (edge < 0.001) return half4(0, 0, 0, 0);

                if (usePencilStroke > 0.5)
                {
                    float taper = PencilTaper(uv, texelSize, thickness);
                    taper = lerp(1.0, taper, pencilTaper);
                    edge *= taper;

                    float grain = (Hash2D(uv * _ScreenParams.xy + _Time.y * 0.1) - 0.5) * pencilGrain;
                    edge = saturate(edge + grain);
                }

                half3 outlineColor = edgeColor.rgb;
                if (useAdaptiveColor > 0.5)
                {
                    half3 src = SAMPLE_TEXTURE2D(_EdgeSourceTex, sampler_EdgeSourceTex, uv).rgb;
                    outlineColor = lerp(edgeColor.rgb, src * 0.35, adaptiveStrength);
                }

                return half4(outlineColor, edgeColor.a * edge);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
