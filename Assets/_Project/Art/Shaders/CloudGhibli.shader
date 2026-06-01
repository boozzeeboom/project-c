Shader "ProjectC/CloudGhibli"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 0.5)
        _RimColor ("Rim Color", Color) = (1, 0.85, 0.6, 0.8)
        _RimPower ("Rim Power", Range(0.5, 4.0)) = 2.0
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseTex2 ("Noise Texture 2", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 1.0
        _NoiseScrollSpeed ("Noise Scroll Speed", Vector) = (0.02, 0.01, 0.03, 0.015)
        _AlphaBase ("Alpha Base", Range(0.0, 1.0)) = 0.4
        _Softness ("Edge Softness", Range(0.0, 1.0)) = 0.3
        _VertexDisplacement ("Vertex Displacement", Float) = 0.3
        _VertexSpeed ("Vertex Speed", Float) = 0.3
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _RimColor;
            half _RimPower;
            half _AlphaBase;
            half _Softness;
            half _NoiseScale;
            half _VertexDisplacement;
            half _VertexSpeed;
            half2 _NoiseScrollSpeed;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_NoiseTex2);
            SAMPLER(sampler_NoiseTex2);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 uvNoise : TEXCOORD3;
                float2 uvNoise2 : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float time = _Time.y * _VertexSpeed;
                float displacement = sin(input.positionOS.x * 0.1 + time * 0.5) * cos(input.positionOS.z * 0.1 + time * 0.3) * _VertexDisplacement;
                float3 positionOS = input.positionOS;
                positionOS.y += displacement * 0.1;
                positionOS.x += displacement * 0.03;

                output.positionCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.worldPos = TransformObjectToWorld(positionOS);
                output.uv = input.uv;
                output.uvNoise = input.uv * _NoiseScale + _Time.y * _NoiseScrollSpeed;
                output.uvNoise2 = input.uv * _NoiseScale * 2.0 + _Time.y * _NoiseScrollSpeed;

                return output;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half noise1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uvNoise).r;
                half noise2 = SAMPLE_TEXTURE2D(_NoiseTex2, sampler_NoiseTex2, input.uvNoise2).r;
                half combinedNoise = noise1 * 0.7 + noise2 * 0.3;

                float3 viewDirWS = GetWorldSpaceViewDir(input.worldPos);
                float3 normalWS = normalize(input.normalWS);
                float rim = 1.0 - saturate(dot(normalize(viewDirWS), normalWS));
                rim = pow(rim, _RimPower);

                half3 cloudColor = _BaseColor.rgb * combinedNoise;
                half3 rimColor = _RimColor.rgb * rim * _RimColor.a;

                half alpha = _AlphaBase * combinedNoise;
                alpha += rim * _RimColor.a * 0.5;
                alpha = saturate(alpha);

                float distFromCenter = length(input.uv - 0.5) * 2.0;
                float edgeFade = 1.0 - smoothstep(1.0 - _Softness, 1.0, distFromCenter);
                alpha *= edgeFade;

                half3 finalColor = cloudColor + rimColor;
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
