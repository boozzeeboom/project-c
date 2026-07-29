// EdgeDetection.shader — Borderlands-style post-process edge detection (URP)
// Sobel filter on depth + normals with pencil jitter.
// Fullscreen blend pass — UV computed from SV_POSITION (no Y-flip issues).

Shader "Hidden/ProjectC/EdgeDetection"
{
    Properties
    {
        [Header(Edge)]
        _EdgeColor ("Edge Color", Color) = (0.05, 0.05, 0.07, 1.0)
        _EdgeWidth ("Edge Width", Range(1, 8)) = 2

        [Header(Depth Edges)]
        _DepthSensitivity ("Depth Sensitivity", Range(0.1, 8.0)) = 2.5
        _DepthThreshold ("Depth Threshold", Range(0.0, 0.5)) = 0.06

        [Header(Normal Edges)]
        _NormalSensitivity ("Normal Sensitivity", Range(0.1, 8.0)) = 1.5
        _NormalThreshold ("Normal Threshold", Range(0.0, 0.5)) = 0.08

        [Header(Pencil Style)]
        _JitterAmount ("Jitter Amount", Range(0.0, 0.5)) = 0.0
        _JitterScale ("Jitter Scale", Range(1.0, 20.0)) = 8.0
        _LineSoftness ("Line Softness", Range(0.01, 0.3)) = 0.06
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

        half4 _EdgeColor;
        float _DepthSensitivity;
        float _DepthThreshold;
        float _NormalSensitivity;
        float _NormalThreshold;
        float _JitterAmount;
        float _JitterScale;
        float _LineSoftness;
        int _EdgeWidth;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
        };

        // Fullscreen triangle — clip-space positions
        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionCS = float4(
                (IN.vertexID == 2) ? 3.0 : -1.0,
                (IN.vertexID == 1) ? 3.0 : -1.0,
                0.0, 1.0
            );
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
            float rawDepth = SampleSceneDepth(uv);
            return Linear01Depth(rawDepth, _ZBufferParams);
        }

        float SobelDepth(float2 uv, float2 texelSize)
        {
            float2 offsets[8] = {
                float2(-1,  1), float2(0,  1), float2(1,  1),
                float2(-1,  0),                float2(1,  0),
                float2(-1, -1), float2(0, -1), float2(1, -1)
            };
            float center = SampleDepthLinear(uv);
            float maxDiff = 0;
            for (int i = 0; i < 8; i++)
            {
                float s = SampleDepthLinear(uv + offsets[i] * texelSize * _EdgeWidth);
                maxDiff = max(maxDiff, abs(center - s));
            }
            return maxDiff;
        }

        float SobelNormal(float2 uv, float2 texelSize)
        {
            float2 offsets[8] = {
                float2(-1,  1), float2(0,  1), float2(1,  1),
                float2(-1,  0),                float2(1,  0),
                float2(-1, -1), float2(0, -1), float2(1, -1)
            };
            float3 center = SampleSceneNormals(uv);
            if (dot(center, center) < 0.001) return 0;
            float maxDiff = 0;
            for (int i = 0; i < 8; i++)
            {
                float3 s = SampleSceneNormals(uv + offsets[i] * texelSize * _EdgeWidth);
                if (dot(s, s) < 0.001) continue;
                maxDiff = max(maxDiff, 1.0 - abs(dot(center, s)));
            }
            return maxDiff;
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
                // Compute UV from screen position — automatically corrects Y-flip
                float2 uv = IN.positionCS.xy * rcp(_ScreenParams.xy);
                float2 texelSize = rcp(_ScreenParams.xy);

                float depthEdge = SobelDepth(uv, texelSize);
                depthEdge = smoothstep(_DepthThreshold - _LineSoftness,
                                        _DepthThreshold + _LineSoftness,
                                        depthEdge * _DepthSensitivity);

                float normalEdge = SobelNormal(uv, texelSize);
                normalEdge = smoothstep(_NormalThreshold - _LineSoftness,
                                         _NormalThreshold + _LineSoftness,
                                         normalEdge * _NormalSensitivity);

                float combinedEdge = saturate(max(depthEdge, normalEdge));

                float jitter = (Hash2D(uv * _ScreenParams.xy * _JitterScale + _Time.y * 0.3) - 0.5) * _JitterAmount;
                combinedEdge = saturate(combinedEdge + jitter);

                return half4(_EdgeColor.rgb, _EdgeColor.a * combinedEdge);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
