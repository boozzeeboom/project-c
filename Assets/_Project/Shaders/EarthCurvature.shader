Shader "ProjectC/EarthCurvature"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "EarthCurvature"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _EarthCurvatureStrength;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                float2 pos = float2(
                    input.vertexID == 2 ? 3.0 : -1.0,
                    input.vertexID == 1 ? 3.0 : -1.0
                );
                float2 uv = float2(
                    input.vertexID == 2 ? 2.0 : 0.0,
                    input.vertexID == 1 ? 2.0 : 0.0
                );

                Varyings output;
                output.positionCS = float4(pos, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Pincushion distortion (Earth curvature lens effect):
                // pushes pixels toward center, stronger at edges
                // factor = 1 / (1 + strength * r²) — physically correct lens distortion
                float2 delta = uv - 0.5;
                float r2 = dot(delta, delta);
                float factor = rcp(1.0 + _EarthCurvatureStrength * r2);
                float2 distortedUV = 0.5 + delta * factor;

                if (any(distortedUV < 0.0) || any(distortedUV > 1.0))
                    return float4(0, 0, 0, 1);

                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV);
            }
            ENDHLSL
        }
    }
}
