Shader "ProjectC/CloudInstanced"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}

        [Header(Gradient Colors)]
        _LightColor ("Light Color (Top)", Color) = (1.0, 0.95, 0.85, 1.0)
        _ShadowColor ("Shadow Color (Bottom)", Color) = (0.18, 0.11, 0.31, 1.0)
        _GradientIntensity ("Gradient Intensity", Range(0, 1)) = 1.0
        _GradientCurve ("Gradient Curve", Range(0.5, 4.0)) = 2.0

        [Header(Rim Glow)]
        _RimColor ("Rim Color", Color) = (0.94, 0.76, 0.48, 0.8)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 1.0

        [Header(Comic Outline)]
        _OutlineColor ("Outline Color", Color) = (0.18, 0.11, 0.31, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 1.0)) = 0.15
        _OutlineStart ("Outline Start", Range(0.0, 0.5)) = 0.4

        [Header(Noise)]
        _NoiseTex2 ("Noise Texture 2", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 1.5
        _NoiseScrollSpeed ("Scroll Speed", Vector) = (0.02, 0.01, 0.03, 0.015)

        [Header(Alpha)]
        _AlphaBase ("Alpha Base", Range(0.0, 1.0)) = 0.5
        _Softness ("Edge Softness", Range(0.0, 1.0)) = 0.35

        [Header(Vertex Displacement)]
        _VertexDisplacement ("Displacement", Float) = 0.3
        _VertexSpeed ("Speed", Float) = 0.3

        [Header(Light Direction)]
        _LightDir ("Light Direction", Vector) = (0.5, -0.7, 0.3, 1.0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CloudInstancedPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(half4, _LightColor)
            UNITY_DEFINE_INSTANCED_PROP(half4, _ShadowColor)
            UNITY_DEFINE_INSTANCED_PROP(half, _GradientIntensity)
            UNITY_DEFINE_INSTANCED_PROP(half, _GradientCurve)
            UNITY_DEFINE_INSTANCED_PROP(half4, _RimColor)
            UNITY_DEFINE_INSTANCED_PROP(half, _RimPower)
            UNITY_DEFINE_INSTANCED_PROP(half, _RimIntensity)
            UNITY_DEFINE_INSTANCED_PROP(half4, _OutlineColor)
            UNITY_DEFINE_INSTANCED_PROP(half, _OutlineWidth)
            UNITY_DEFINE_INSTANCED_PROP(half, _OutlineStart)
            UNITY_DEFINE_INSTANCED_PROP(half, _NoiseScale)
            UNITY_DEFINE_INSTANCED_PROP(half2, _NoiseScrollSpeed)
            UNITY_DEFINE_INSTANCED_PROP(half, _AlphaBase)
            UNITY_DEFINE_INSTANCED_PROP(half, _Softness)
            UNITY_DEFINE_INSTANCED_PROP(half, _VertexDisplacement)
            UNITY_DEFINE_INSTANCED_PROP(half, _VertexSpeed)
            UNITY_DEFINE_INSTANCED_PROP(half4, _LightDir)
            UNITY_INSTANCING_BUFFER_END(Props)

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
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
                float2 uvNoise2 : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                half vertexSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _VertexSpeed);
                half vertexDisp = UNITY_ACCESS_INSTANCED_PROP(Props, _VertexDisplacement);

                float time = _Time.y * vertexSpeed;
                float displacement = sin(input.positionOS.x * 0.15 + time * 0.5) *
                                    cos(input.positionOS.z * 0.15 + time * 0.3) *
                                    vertexDisp;
                float3 posOS = input.positionOS;
                posOS.y += displacement * 0.1;
                posOS.x += displacement * 0.03;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(posOS);
                output.positionCS = vertexInput.positionCS;
                output.worldPos = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;

                half noiseScale = UNITY_ACCESS_INSTANCED_PROP(Props, _NoiseScale);
                half2 noiseScroll = UNITY_ACCESS_INSTANCED_PROP(Props, _NoiseScrollSpeed);
                output.uvNoise2 = input.uv * noiseScale * 2.5 + _Time.y * noiseScroll;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 lightColor = UNITY_ACCESS_INSTANCED_PROP(Props, _LightColor);
                half4 shadowColor = UNITY_ACCESS_INSTANCED_PROP(Props, _ShadowColor);
                half gradientIntensity = UNITY_ACCESS_INSTANCED_PROP(Props, _GradientIntensity);
                half4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);
                half rimPower = UNITY_ACCESS_INSTANCED_PROP(Props, _RimPower);
                half rimIntensity = UNITY_ACCESS_INSTANCED_PROP(Props, _RimIntensity);
                half4 outlineColor = UNITY_ACCESS_INSTANCED_PROP(Props, _OutlineColor);
                half outlineWidth = UNITY_ACCESS_INSTANCED_PROP(Props, _OutlineWidth);
                half outlineStart = UNITY_ACCESS_INSTANCED_PROP(Props, _OutlineStart);
                half alphaBase = UNITY_ACCESS_INSTANCED_PROP(Props, _AlphaBase);
                half softness = UNITY_ACCESS_INSTANCED_PROP(Props, _Softness);
                half4 lightDir = UNITY_ACCESS_INSTANCED_PROP(Props, _LightDir);

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half noise2 = SAMPLE_TEXTURE2D(_NoiseTex2, sampler_NoiseTex2, input.uvNoise2).r;
                half combinedNoise = texColor.r * 0.7 + noise2 * 0.3;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.worldPos));
                float3 lightDirNorm = normalize(lightDir.xyz);

                half NdotV = saturate(dot(normalWS, viewDirWS));
                half silhouetteEdge = 1.0 - smoothstep(0.0, outlineStart, NdotV);
                silhouetteEdge = pow(silhouetteEdge, 2.0) * outlineWidth;

                half topLight = saturate(normalWS.y * 0.5 + 0.5);
                topLight = smoothstep(0.0, 1.0, topLight);

                half gradientCurve = UNITY_ACCESS_INSTANCED_PROP(Props, _GradientCurve);
                half gradientT = pow(topLight, gradientCurve) * gradientIntensity;
                half3 gradientColor = lerp(shadowColor.rgb, lightColor.rgb, gradientT);

                half NdotL = dot(normalWS, lightDirNorm);
                half wrappedNdotL = saturate((NdotL + 0.5) / 1.5);
                half3 diffuseLight = gradientColor * (0.6 + wrappedNdotL * 0.4);

                half3 cloudColor = diffuseLight * combinedNoise;

                half rim = 1.0 - saturate(dot(viewDirWS, normalWS));
                rim = pow(rim, rimPower);
                rim = smoothstep(0.3, 1.0, rim);
                half3 rimLight = rimColor.rgb * rim * rimIntensity;
                cloudColor += rimLight;

                half3 finalColor = lerp(cloudColor, outlineColor.rgb, silhouetteEdge);

                half alpha = alphaBase * combinedNoise;
                alpha += rim * rimColor.a * 0.4;
                alpha = saturate(alpha);

                float distFromCenter = length(input.uv - 0.5) * 2.0;
                float edgeFade = 1.0 - smoothstep(1.0 - softness, 1.0, distFromCenter);
                alpha *= edgeFade;

                half finalAlpha = max(alpha, silhouetteEdge * outlineColor.a);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
