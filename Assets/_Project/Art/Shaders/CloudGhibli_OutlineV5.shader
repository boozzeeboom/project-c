// CloudGhibli_OutlineV5.shader - Ghibli cloud with silhouette outline

Shader "ProjectC/CloudGhibli_OutlineV5"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.96, 0.94, 0.91, 0.7)
        _ShadowColor ("Shadow Color", Color) = (0.18, 0.11, 0.31, 0.8)
        _TopLightColor ("Top Light Color", Color) = (1.0, 0.95, 0.85, 1.0)
        _BottomLightColor ("Bottom Light Color", Color) = (0.31, 0.76, 0.97, 1.0)
        _GradientIntensity ("Gradient Intensity", Range(0, 1)) = 0.6
        _RimColor ("Rim Color", Color) = (0.94, 0.76, 0.48, 0.8)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 1.0
        _OutlineColor ("Outline Color", Color) = (0.18, 0.11, 0.31, 1.0)
        _OutlineThickness ("Outline Thickness", Range(0.0, 1.0)) = 0.15
        _OutlineStart ("Outline Start", Range(0.0, 0.5)) = 0.4
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseTex2 ("Noise Texture 2", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 1.5
        _NoiseScrollSpeed ("Noise Scroll Speed", Vector) = (0.02, 0.01, 0.03, 0.015)
        _AlphaBase ("Alpha Base", Range(0.0, 1.0)) = 0.5
        _Softness ("Edge Softness", Range(0.0, 1.0)) = 0.35
        _VertexDisplacement ("Vertex Displacement", Float) = 0.3
        _VertexSpeed ("Vertex Speed", Float) = 0.3
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
            Name "CloudOutlinePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _TopLightColor;
            half4 _BottomLightColor;
            half _GradientIntensity;
            half4 _RimColor;
            half _RimPower;
            half _RimIntensity;
            half _NoiseScale;
            half2 _NoiseScrollSpeed;
            half _AlphaBase;
            half _Softness;
            half _VertexDisplacement;
            half _VertexSpeed;
            half4 _OutlineColor;
            half _OutlineThickness;
            half _OutlineStart;

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
                float3 viewDirWS : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float time = _Time.y * _VertexSpeed;
                float displacement = sin(input.positionOS.x * 0.15 + time * 0.5) *
                                    cos(input.positionOS.z * 0.15 + time * 0.3) *
                                    _VertexDisplacement;

                float3 positionOS = input.positionOS;
                positionOS.y += displacement * 0.1;
                positionOS.x += displacement * 0.03;

                output.positionCS = TransformObjectToHClip(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.worldPos = TransformObjectToWorld(positionOS);
                output.uv = input.uv;
                output.viewDirWS = GetWorldSpaceViewDir(output.worldPos);
                output.uvNoise = input.uv * _NoiseScale + _Time.y * _NoiseScrollSpeed;
                output.uvNoise2 = input.uv * _NoiseScale * 2.5 + _Time.y * _NoiseScrollSpeed;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half noise1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uvNoise).r;
                half noise2 = SAMPLE_TEXTURE2D(_NoiseTex2, sampler_NoiseTex2, input.uvNoise2).r;
                half combinedNoise = noise1 * 0.65 + noise2 * 0.35;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                half NdotV = saturate(dot(normalWS, viewDirWS));
                
                half silhouetteOutline = 1.0 - smoothstep(0.0, _OutlineStart, NdotV);
                silhouetteOutline = pow(silhouetteOutline, 2.0) * _OutlineThickness;
                
                half topLight = saturate(normalWS.y * 0.5 + 0.5);
                topLight = smoothstep(0.0, 1.0, topLight);
                
                half bottomLight = saturate(-normalWS.y * 0.5 + 0.5);
                bottomLight = smoothstep(0.2, 0.8, bottomLight);
                
                half3 gradientColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, topLight * _GradientIntensity);
                gradientColor = lerp(gradientColor, _BottomLightColor.rgb, bottomLight * _GradientIntensity * 0.5);

                half rim = 1.0 - saturate(dot(viewDirWS, normalWS));
                rim = pow(rim, _RimPower);
                rim = smoothstep(0.3, 1.0, rim);
                
                half3 rimColor = _RimColor.rgb * rim * _RimIntensity;

                half NdotL = dot(normalWS, normalize(float3(0.5, -0.7, 0.3)));
                half celShade = smoothstep(-0.1, 0.5, NdotL);
                celShade = lerp(0.4, 1.0, celShade);

                half3 cloudColor = gradientColor * celShade;
                cloudColor += rimColor;
                cloudColor *= (0.7 + combinedNoise * 0.3);

                half alpha = _AlphaBase * combinedNoise;
                alpha += rim * _RimColor.a * 0.4;
                alpha = saturate(alpha);

                float distFromCenter = length(input.uv - 0.5) * 2.0;
                float edgeFade = 1.0 - smoothstep(1.0 - _Softness, 1.0, distFromCenter);
                alpha *= edgeFade;

                half3 finalColor = lerp(cloudColor, _OutlineColor.rgb, silhouetteOutline);
                half finalAlpha = max(alpha, silhouetteOutline * _OutlineColor.a);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
