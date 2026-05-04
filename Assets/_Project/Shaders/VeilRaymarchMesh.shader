// VeilRaymarchMesh.shader — Volumetric Raymarch for Plane Mesh
// Based on CLOUDENGINE cloud_advanced.frag approach
//
// Key features (from CLOUDENGINE):
// - Volumetric accumulation over ray steps (depth perception)
// - Density gradient normals (3D lighting)
// - Ghibli-style cel-shading
// - Rim lighting from view direction
//
// Works with plane mesh (no screen-space matrices needed).
// Ray direction = normalize(worldPos - cameraPos)
//
// Usage: Attach to large plane mesh that follows player (XZ).

Shader "Project C/Clouds/VeilRaymarchMesh"
{
    Properties
    {
        // Color
        _VeilColor ("Veil Base Color", Color) = (0.176, 0.106, 0.306, 1.0)
        _LightningColor ("Lightning Color", Color) = (0.7, 0.4, 1.0, 1.0)
        _LightningIntensity ("Lightning Intensity", Range(0, 1)) = 0.0

        // Noise
        _NoiseScale ("Noise Scale", Float) = 0.002
        _NoiseSpeed ("Noise Animation Speed", Float) = 0.01
        _NoiseOctaves ("FBM Octaves", Range(1, 6)) = 3

        // Veil layer (height range)
        _VeilBottom ("Veil Bottom Height", Float) = 800.0
        _VeilTop ("Veil Top Height", Float) = 1200.0

        // Raymarch
        _RaymarchSteps ("Raymarch Steps", Range(4, 64)) = 24
        _RaymarchMaxDist ("Max Ray Distance", Float) = 8000.0

        // Wind (noise animation)
        _WindX ("Wind X Speed", Float) = 0.01
        _WindZ ("Wind Z Speed", Float) = 0.003

        // Density
        _DensityMultiplier ("Density Multiplier", Float) = 1.0

        // Lighting (from CLOUDENGINE)
        _LightDir ("Light Direction", Vector) = (0.5, -0.5, 0.3, 0)
        _DayFactor ("Day Factor", Range(0, 1)) = 0.5
        _RimPower ("Rim Power", Float) = 3.0
        _RimIntensity ("Rim Intensity", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            Name "VeilRaymarchMeshPass"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4 _LightDir;
                half _DayFactor;
                half _RimPower;
                half _RimIntensity;
            CBUFFER_END

            // ---- Hash Functions ----
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
                lf = lf * lf * (3.0 - 2.0 * lf);

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

                for (int i = 0; i < 6; i++)
                {
                    if (i >= octaves) break;
                    val += amp * Noise3D(p * freq);
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
                // Height gradient - denser in middle of layer (like CLOUDENGINE)
                half h = (pos.y - _VeilBottom) / (_VeilTop - _VeilBottom);
                half heightFactor = 1.0 - abs(h - 0.5) * 2.0;
                heightFactor = smoothstep(0.0, 0.3, heightFactor);

                // Wind animation
                half time = _Time.y;
                half3 windOffset = half3(time * _WindX, 0.0, time * _WindZ);

                half3 samplePos = pos * _NoiseScale + windOffset;

                // Large-scale shape (canyon valleys) - 2 octaves
                half shape = FBm3D(samplePos, 2);

                // Medium-scale detail (boiling effect)
                half detail = FBm3D(samplePos * 4.0 + half3(100.0, 0.0, 50.0), 2);

                // Fine turbulence
                half turbulence = FBm3D(samplePos * 8.0 + half3(200.0, 100.0, 0.0), 1) * 0.3;

                // Combine
                half density = shape * 0.6 + detail * 0.3 + turbulence * 0.1;

                // Apply height gradient and density multiplier
                density *= heightFactor * _DensityMultiplier;

                // Non-linear remap for more defined valleys
                density = smoothstep(0.15, 0.85, density);

                return density;
            }

            // ---- Density Gradient Normal (from CLOUDENGINE) ----
            // THIS is what creates the 3D volumetric lighting look
            half3 CalcNormal(half3 pos)
            {
                half eps = 50.0; // Sample distance for gradient
                half d = VeilDensity(pos);
                half dx = VeilDensity(pos + half3(eps, 0, 0)) - d;
                half dy = VeilDensity(pos + half3(0, eps, 0)) - d;
                half dz = VeilDensity(pos + half3(0, 0, eps)) - d;
                return normalize(half3(dx, dy, dz));
            }

            // ---- Cel-Shading (from CLOUDENGINE Ghibli style) ----
            half CelShade(half value, half steps)
            {
                return floor(value * steps) / steps;
            }

            // ---- Vertex Input/Output ----
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.worldPos = vertexInput.positionWS;
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // =============================================
                // RAY CONSTRUCTION (from mesh vertex to camera)
                // =============================================

                // Ray from camera → vertex (NOT camera → fragment direction)
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(input.worldPos - rayOrigin);

                // =============================================
                // VEIL LAYER INTERSECTION (same as VeilRaymarch)
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
                        return half4(0, 0, 0, 0);
                    }
                }
                else
                {
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
                // RAYMARCH WITH VOLUMETRIC LIGHTING (CLOUDENGINE style)
                // =============================================

                float stepSize = (tMax - tMin) / (float)_RaymarchSteps;
                half4 accumulatedColor = half4(0, 0, 0, 0);
                float3 viewDir = -rayDir;

                // Light direction (normalized)
                half3 lightDir = normalize(half3(-0.5, 0.5, -0.3));

                for (int i = 0; i < _RaymarchSteps && accumulatedColor.a < 0.99; i++)
                {
                    float t = tMin + ((float)i + 0.5) * stepSize;
                    float3 samplePos = rayOrigin + rayDir * t;

                    half density = VeilDensity(samplePos);

                    if (density > 0.01)
                    {
                        // ---- CLOUDENGINE: Calculate normal from density gradient ----
                        half3 normal = CalcNormal(samplePos);

                        // ---- CLOUDENGINE: Cel-shaded diffuse ----
                        half NdotL = max(dot(normal, lightDir), 0.0);
                        half diffuse = CelShade(NdotL, 3.0);

                        // ---- CLOUDENGINE: Color from shadow/base blend ----
                        half3 shadowColor = _VeilColor.rgb * 0.4;
                        half3 baseColor = _VeilColor.rgb;
                        half3 cloudColor = lerp(shadowColor, baseColor, diffuse);

                        // ---- CLOUDENGINE: Rim lighting (Ghibli style) ----
                        half rim = 1.0 - saturate(dot(viewDir, normal));
                        rim = pow(rim, _RimPower);
                        rim = smoothstep(0.4, 1.0, rim);
                        cloudColor += _VeilColor.rgb * rim * _RimIntensity * _DayFactor;

                        // ---- Ambient ----
                        cloudColor += half3(0.1, 0.08, 0.15) * 0.15 * _DayFactor;

                        // ---- Volumetric accumulation (Beer-Lambert) ----
                        half absorption = density * stepSize * 0.05;
                        half transmittance = 1.0 - accumulatedColor.a;
                        accumulatedColor.rgb += cloudColor * transmittance * absorption;
                        accumulatedColor.a += absorption * 0.5;
                    }
                }

                // =============================================
                // LIGHTNING GLOW
                // =============================================

                if (_LightningIntensity > 0.01)
                {
                    half3 lightningPos = input.worldPos * _NoiseScale;
                    lightningPos.xz += _Time.y * 0.1;
                    half lightningNoise = FBm3D(lightningPos * 2.0, 1);
                    half lightning = smoothstep(0.7, 0.9, lightningNoise) * _LightningIntensity;

                    half3 lightningColor = _LightningColor.rgb * lightning * 2.0;
                    accumulatedColor.rgb += lightningColor * (1.0 - accumulatedColor.a);
                    accumulatedColor.a = max(accumulatedColor.a, lightning * 0.5);
                }

                return accumulatedColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}