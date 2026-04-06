Shader "ProjectC/CloudGhibli"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 0.5)
        _RimColor ("Rim Color (Ghibli Glow)", Color) = (1, 0.85, 0.6, 0.8)
        _RimPower ("Rim Power", Range(0.5, 4.0)) = 2.0
        _NoiseTex ("Noise Texture (for volume)", 2D) = "white" {}
        _NoiseTex2 ("Noise Texture 2 (detail)", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 1.0
        _NoiseScrollSpeed ("Noise Scroll Speed", Vector) = (0.02, 0.01, 0.03, 0.015)
        _AlphaBase ("Alpha Base", Range(0.0, 1.0)) = 0.4
        _Softness ("Edge Softness", Range(0.0, 1.0)) = 0.3
        _VertexDisplacement ("Vertex Displacement Amount", Float) = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

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

            // --- Properties ---
            half4 _BaseColor;
            half4 _RimColor;
            half _RimPower;
            half _AlphaBase;
            half _Softness;
            half _NoiseScale;
            half _VertexDisplacement;
            half2 _NoiseScrollSpeed;
            half2 _NoiseScrollSpeed2;

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_NoiseTex2);
            SAMPLER(sampler_NoiseTex2);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 uvNoise : TEXCOORD3;
                float2 uvNoise2 : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Vertex displacement для морфинга формы (Ghibli "дыхание")
                float time = _Time.y;
                float displacement = sin(input.positionOS.x * 0.1 + time * 0.5) *
                                    cos(input.positionOS.z * 0.1 + time * 0.3) *
                                    _VertexDisplacement;

                float3 positionOS = input.positionOS;
                positionOS.y += displacement * 0.3;
                positionOS.x += displacement * 0.1;

                output.positionCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.worldPos = TransformObjectToWorld(input.positionOS);
                output.uv = input.uv;

                // UV для noise (tiling + scroll)
                output.uvNoise = input.uv * _NoiseScale + _Time.y * _NoiseScrollSpeed;
                output.uvNoise2 = input.uv * _NoiseScale * 2.0 + _Time.y * _NoiseScrollSpeed2;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- Noise для объёма ---
                half noise1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uvNoise).r;
                half noise2 = SAMPLE_TEXTURE2D(_NoiseTex2, sampler_NoiseTex2, input.uvNoise2).r;
                half combinedNoise = noise1 * 0.7 + noise2 * 0.3;

                // --- Rim glow (Ghibli signature) ---
                float3 viewDirWS = GetWorldSpaceViewDir(input.worldPos);
                float3 normalWS = normalize(input.normalWS);
                float rim = 1.0 - saturate(dot(normalize(viewDirWS), normalWS));
                rim = pow(rim, _RimPower);

                // --- Цвет с noise ---
                half3 cloudColor = _BaseColor.rgb * combinedNoise;

                // --- Rim light ---
                half3 rimColor = _RimColor.rgb * rim * _RimColor.a;

                // --- Alpha с soft edges ---
                half alpha = _AlphaBase * combinedNoise;
                // Rim добавляет прозрачность по краям
                alpha += rim * _RimColor.a * 0.5;
                alpha = saturate(alpha);

                // --- Soft edge через distance от центра ---
                float distFromCenter = length(input.uv - 0.5) * 2.0; // 0..1
                float edgeFade = 1.0 - smoothstep(1.0 - _Softness, 1.0, distFromCenter);
                alpha *= edgeFade;

                // --- Финальный цвет ---
                half3 finalColor = cloudColor + rimColor;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
