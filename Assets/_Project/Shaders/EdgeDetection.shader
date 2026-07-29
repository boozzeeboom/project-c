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

        // Adaptive color: bound by RenderFeature via cmd.SetGlobalTexture(_EdgeSourceTex)
        TEXTURE2D(_EdgeSourceTex);
        SAMPLER(sampler_EdgeSourceTex);

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

        // --- Depth Sobel (scalar) ---
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

        // --- Normal Sobel (scalar) ---
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

        // --- Depth Sobel with direction (X,Y gradients) for pencil taper ---
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

            // Sobel kernels: Gx = [-1 0 1; -2 0 2; -1 0 1], Gy = [-1 -2 -1; 0 0 0; 1 2 1]
            float gx = (-tl + tr - 2.0 * l + 2.0 * r - bl + br);
            float gy = (-tl - 2.0 * t - tr + bl + 2.0 * b + br);
            return float2(gx, gy);
        }

        // --- Pencil taper: sample edge magnitude along edge direction ---
        float PencilTaper(float2 uv, float2 texelSize, float thickness)
        {
            float2 g = SobelDepthDir(uv, texelSize, thickness);
            float mag = length(g);
            if (mag < 0.0001) return 1.0;

            // Edge runs perpendicular to gradient.
            // Normalize gradient (points across edge) → edge direction is rotated 90°
            float2 edgeDir = normalize(float2(-g.y, g.x));

            // Sample magnitude along edge in both directions
            float stepDist = thickness * 4.0;
            float2 alongPos = uv + edgeDir * texelSize * stepDist;
            float2 alongNeg = uv - edgeDir * texelSize * stepDist;

            float2 ga = SobelDepthDir(alongPos, texelSize, thickness);
            float2 gb = SobelDepthDir(alongNeg, texelSize, thickness);

            float magA = length(ga);
            float magB = length(gb);

            // Continuity factor: how much edge continues in both directions
            float contA = saturate(magA / (mag + 0.0001));
            float contB = saturate(magB / (mag + 0.0001));
            float continuity = min(contA, contB);

            return smoothstep(0.0, 0.6, continuity);
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

                // Distance-based thickness falloff
                float depth = SampleDepthLinear(uv);
                float thickness = _EdgeWidth * saturate(1.0 - pow(saturate(depth / (_MaxEdgeDistance * 0.01)), _DepthFalloff));
                if (thickness < 0.05) return half4(0, 0, 0, 0);

                float edge = 0;

                if (_UseDepthEdges > 0.5)
                {
                    float d = SobelDepth(uv, texelSize, thickness);
                    edge = max(edge, smoothstep(_DepthThreshold - _LineSoftness,
                                                 _DepthThreshold + _LineSoftness,
                                                 d * _DepthSensitivity));
                }

                if (_UseNormalEdges > 0.5)
                {
                    float n = SobelNormal(uv, texelSize, thickness);
                    edge = max(edge, smoothstep(_NormalThreshold - _LineSoftness,
                                                 _NormalThreshold + _LineSoftness,
                                                 n * _NormalSensitivity));
                }

                edge = saturate(edge);
                if (edge < 0.001) return half4(0, 0, 0, 0);

                // Pencil stroke: taper at endpoints + grain
                if (_UsePencilStroke > 0.5)
                {
                    float taper = PencilTaper(uv, texelSize, thickness);
                    taper = lerp(1.0, taper, _PencilTaper);
                    edge *= taper;

                    float grain = (Hash2D(uv * _ScreenParams.xy + _Time.y * 0.1) - 0.5) * _PencilGrain;
                    edge = saturate(edge + grain);
                }

                // Color
                half3 outlineColor = _EdgeColor.rgb;
                if (_UseAdaptiveColor > 0.5)
                {
                    half3 src = SAMPLE_TEXTURE2D(_EdgeSourceTex, sampler_EdgeSourceTex, uv).rgb;
                    outlineColor = lerp(_EdgeColor.rgb, src * 0.35, _AdaptiveStrength);
                }

                return half4(outlineColor, _EdgeColor.a * edge);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
