// VeilRaymarch.shader — Full-Screen Volumetric Raymarch for Horizon Veil
// Screen-space raymarch using camera matrices (NOT mesh-dependent)
// 8-16 raymarch steps, FBM noise, Beer-Lambert absorption, height gradient
//
// Key features:
// - "Клубящаяся завеса со своими впадинами каньонами" via multi-octave FBM
// - Height gradient for curtain layer (Y=800-1200 base)
// - Purple-green dark color with lightning (#2d1b4e base, #b366ff lightning)
// - ~1-1.5ms GPU cost (full-screen)
// - REQUIRES: URP ScriptableRenderPass to render full-screen quad
//
// Integration: Use VeilRaymarchRenderPass.cs to render this as full-screen effect

Shader "Project C/Clouds/VeilRaymarch"
{
    Properties
    {
        // Color settings (matching spec: #2d1b4e base, #b366ff lightning)
        _VeilColor ("Veil Base Color", Color) = (0.176, 0.106, 0.306, 1.0)  // #2d1b4e (spec)
        _LightningColor ("Lightning Color", Color) = (0.7, 0.4, 1.0, 1.0)   // #b366ff (spec)
        _LightningIntensity ("Lightning Intensity", Range(0, 1)) = 0.0

        // Noise settings
        _NoiseScale ("Noise Scale", Float) = 0.002
        _NoiseSpeed ("Noise Animation Speed", Float) = 0.01
        _NoiseOctaves ("FBM Octaves", Range(1, 6)) = 3

        // Veil layer settings
        _VeilBottom ("Veil Bottom Height", Float) = 800.0
        _VeilTop ("Veil Top Height", Float) = 1200.0

        // Raymarch settings
        _RaymarchSteps ("Raymarch Steps", Range(4, 32)) = 12
        _RaymarchMaxDist ("Max Ray Distance", Float) = 8000.0

        // Wind settings
        _WindX ("Wind X Offset", Float) = 0.0
        _WindZ ("Wind Z Offset", Float) = 0.0

        // Density settings
        _DensityMultiplier ("Density Multiplier", Float) = 1.0
        _LightAbsorption ("Light Absorption", Float) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            Name "VeilRaymarchPass"

            // Full-screen blending - always render to entire screen
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always     // Disable depth test - full-screen effect
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---- Camera Matrices (set by URP automatically during fullscreen render) ----
            // Using Unity built-in matrices for screen-space ray reconstruction:
            // - UNITY_MATRIX_I_P: Inverse projection matrix
            // - UNITY_MATRIX_I_V: Inverse view matrix (camera-to-world)

            // ---- Properties ----
            CBUFFER_START(UnityPerMaterial)
                half4 _VeilColor;
                half4 _LightningColor;
                half _LightningIntensity;
                half _NoiseScale;
                half _NoiseSpeed;
                int _NoiseOctaves;
                half _VeilBottom;
                half _VeilTop;
                int _RaymarchSteps;
                half _RaymarchMaxDist;
                half _WindX;
                half _WindZ;
                half _DensityMultiplier;
                half _LightAbsorption;
            CBUFFER_END

            // ---- Hash Functions (Value Noise) ----
            half Hash31(half3 p)
            {
                p = frac(p * half3(123.34, 456.21, 789.87));
                p += dot(p, p.yzx + half3(45.32, 78.91, 34.56));
                return frac(p.x * p.y * p.z);
            }

            half Hash21(half2 p)
            {
                p = frac(p * half2(123.34, 456.21));
                p += dot(p, p + 78.91);
                return frac(p.x * p.y);
            }

            // ---- 3D Value Noise ----
            half Noise3D(half3 p)
            {
                half3 id = floor(p);
                half3 lf = frac(p);
                lf = lf * lf * (3.0 - 2.0 * lf);  // Smoothstep interpolation

                half tl = Hash31(id);
                half tr = Hash31(id + half3(1, 0, 0));
                half bl = Hash31(id + half3(0, 1, 0));
                half br = Hash31(id + half3(1, 1, 0));
                half tlf = Hash31(id + half3(0, 0, 1));
                half trf = Hash31(id + half3(1, 0, 1));
                half blf = Hash31(id + half3(0, 1, 1));
                half brf = Hash31(id + half3(1, 1, 1));

                half top = lerp(tl, tr, lf.x);
                half bot = lerp(bl, br, lf.x);
                half topF = lerp(tlf, trf, lf.x);
                half botF = lerp(blf, brf, lf.x);

                return lerp(lerp(top, bot, lf.y), lerp(topF, botF, lf.y), lf.z);
            }

            // ---- 3D FBM (Fractal Brownian Motion) ----
            half FBm3D(half3 p, int octaves)
            {
                half val = 0.0;
                half amp = 0.5;
                half freq = 1.0;
                half norm = 0.0;

                for (int i = 0; i < octaves; i++)
                {
                    val += amp * Noise3D(p * freq);
                    norm += amp;
                    freq *= 2.0;
                    amp *= 0.5;
                }

                return val / norm;
            }

            // ---- 2D FBM for height map ----
            half FBm2D(half2 uv, int octaves)
            {
                half val = 0.0;
                half amp = 0.5;
                half freq = 1.0;
                half norm = 0.0;

                for (int i = 0; i < octaves; i++)
                {
                    val += amp * Hash21(uv * freq);
                    norm += amp;
                    freq *= 2.0;
                    amp *= 0.5;
                }

                return val / norm;
            }

            // ---- Veil Density Function ----
            // Creates "клубящаяся завеса со своими впадинами каньонами"
            half VeilDensity(half3 pos)
            {
                // Height gradient - denser in middle of layer
                half h = (pos.y - _VeilBottom) / (_VeilTop - _VeilBottom);
                half heightFactor = 1.0 - abs(h - 0.5) * 2.0;
                heightFactor = smoothstep(0.0, 0.4, heightFactor);

                // Wind animation offset
                half time = _Time.y;
                half3 windOffset = half3(time * _WindX, 0.0, time * _WindZ);

                half3 samplePos = pos * _NoiseScale + windOffset;

                // Large-scale shape (canyon valleys) - 2 octaves
                half shape = FBm3D(samplePos, 2);

                // Medium-scale detail (boiling effect) - 2 octaves at 4x frequency
                half detail = FBm3D(samplePos * 4.0 + half3(100.0, 0.0, 50.0), 2);

                // Fine-scale turbulence - 1 octave at 8x frequency
                half turbulence = FBm3D(samplePos * 8.0 + half3(200.0, 100.0, 0.0), 1) * 0.3;

                // Combine: 60% shape, 30% detail, 10% turbulence
                half density = shape * 0.6 + detail * 0.3 + turbulence * 0.1;

                // Apply height gradient and density multiplier
                density *= heightFactor * _DensityMultiplier;

                // Non-linear remap for more defined valleys and peaks
                density = smoothstep(0.15, 0.85, density);

                return density;
            }

            // ---- Vertex Input/Output ----
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Full-screen quad: position is already in clip space [-1, 1]
                // Pass through directly - no transformation needed
                output.positionHCS = float4(input.positionOS.xyz, 1.0);
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // =============================================
                // SCREEN-SPACE RAY RECONSTRUCTION
                // =============================================

                // Convert UV [0,1] to NDC [-1,1]
                float2 uv = input.uv;
                float2 ndc = uv * 2.0 - 1.0;

                // Reconstruct view-space ray at far plane
                // clipRay = (ndc.x, ndc.y, 1.0, 1.0) for far plane
                float4 clipRay = float4(ndc, 1.0, 1.0);
                float4 viewRay = mul(UNITY_MATRIX_I_P, clipRay);  // Inverse projection
                viewRay.xyz /= viewRay.w;  // Perspective divide

                // Transform to world space and normalize = ray direction
                float3 rayDir = normalize(mul((float3x3)UNITY_MATRIX_I_V, viewRay.xyz));
                float3 rayOrigin = _WorldSpaceCameraPos;

                // =============================================
                // VEIL LAYER INTERSECTION
                // =============================================

                float veilBottom = _VeilBottom;
                float veilTop = _VeilTop;

                float tMin = 0;
                float tMax = _RaymarchMaxDist;

                if (abs(rayDir.y) < 0.0001)
                {
                    // Looking horizontal - check if camera is inside veil
                    if (rayOrigin.y > veilBottom && rayOrigin.y < veilTop)
                    {
                        tMin = 0;
                        tMax = _RaymarchMaxDist;
                    }
                    else
                    {
                        return half4(0, 0, 0, 0);  // Camera outside veil, looking horizontal
                    }
                }
                else
                {
                    // Calculate intersection with veil layer (horizontal planes at Y=bottom/top)
                    float t1 = (veilTop - rayOrigin.y) / rayDir.y;
                    float t2 = (veilBottom - rayOrigin.y) / rayDir.y;
                    tMin = min(t1, t2);
                    tMax = max(t1, t2);
                }

                // Clamp to valid range
                if (tMax < 0 || tMin > _RaymarchMaxDist) return half4(0, 0, 0, 0);
                tMin = max(0, tMin);
                tMax = min(tMax, _RaymarchMaxDist);

                if (tMin >= tMax) return half4(0, 0, 0, 0);

                // =============================================
                // RAYMARCH THROUGH VEIL LAYER
                // =============================================

                float stepSize = (tMax - tMin) / (float)_RaymarchSteps;
                half4 accumulatedColor = half4(0, 0, 0, 0);

                [loop]
                for (int i = 0; i < _RaymarchSteps && accumulatedColor.a < 0.99; i++)
                {
                    float t = tMin + ((float)i + 0.5) * stepSize;
                    float3 samplePos = rayOrigin + rayDir * t;

                    half density = VeilDensity(samplePos);

                    if (density > 0.01)
                    {
                        // Beer-Lambert absorption model
                        half absorption = density * stepSize * _LightAbsorption * 20.0;

                        // Front-to-back accumulation
                        half transmittance = 1.0 - accumulatedColor.a;
                        accumulatedColor.rgb += _VeilColor.rgb * transmittance * absorption;
                        accumulatedColor.a += absorption * 0.5;
                    }
                }

                // =============================================
                // LIGHTNING GLOW
                // =============================================

                if (_LightningIntensity > 0.01)
                {
                    half4 lightningGlow = _LightningColor * _LightningIntensity * 0.3;
                    half transmittance = 1.0 - accumulatedColor.a;
                    accumulatedColor.rgb += lightningGlow.rgb * transmittance;
                    accumulatedColor.a = max(accumulatedColor.a, lightningGlow.a * _LightningIntensity);
                }

                return accumulatedColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
