// VolumetricClouds.shader — CLOUD_system 3.0 TUNING
// NO hardcoded values. All parameters via Shader.SetGlobal* uniforms.
// Pass 0: B&W density (debug)
// Pass 1: Full raymarch + depth-test → colorTarget direct

Shader "Hidden/ProjectC/VolumetricClouds"
{
    Properties
    {
        _CloudBottomY ("Cloud Bottom Y", Float) = 800.0
        _CloudTopY ("Cloud Top Y", Float) = 7000.0
        _RaymarchSteps ("Raymarch Steps", Int) = 48
        _MaxRayDistance ("Max Ray Distance", Float) = 5000.0
        _HeightEdgeSoftness ("Height Edge Softness", Range(0.01, 1.5)) = 0.3
        _CoverageScale ("Coverage Scale", Float) = 0.0008
        _DepthFadeDistance ("Depth Fade Distance", Range(10, 2000)) = 200
        [HideInInspector] _BlueNoiseTex ("Blue Noise", 2D) = "black" {}
        [HideInInspector] _WindOffset ("Wind Offset", Vector) = (0, 0, 0, 0)
        [HideInInspector] _LocalDensityRT ("Local Density", 3D) = "" {}
        [HideInInspector] _LocalDensityCenter ("LD Center", Vector) = (0,0,0,0)
        [HideInInspector] _LocalDensitySize ("LD Size", Float) = 1920
        [HideInInspector] _LocalDensityInfluence ("LD Influence", Float) = 1
        [HideInInspector] _LocalDisplacementRT ("Local Displacement", 3D) = "" {}
        [HideInInspector] _LocalDisplacementCenter ("LDisp Center", Vector) = (0,0,0,0)
        [HideInInspector] _LocalDisplacementSize ("LDisp Size", Float) = 1920
        [HideInInspector] _LocalDisplacementStrength ("LDisp Strength", Float) = 300

        // NOTE (T-CLOUD38): storm uniforms (_StormDensityMult, _StormNoiseScale, …)
        // deliberately NOT declared in Properties — only in HLSLINCLUDE below.
        // If they live in Properties, `new Material(shader)` copies their defaults
        // into material properties, which SHADOW Shader.SetGlobal* from
        // StormCellDirector → runtime tweaking stops working. Globals-only fixes it.
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always   // manual depth test via SampleSceneDepth in fragment

        HLSLINCLUDE
        #pragma target 5.0
        #pragma exclude_renderers gles

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Assets/_Project/Shaders/Clouds/CloudNoise.hlsl"
        #include "Assets/_Project/Shaders/Clouds/CloudCommon.hlsl"

        // === Material properties (in Properties, set via mat.Set*) ===
        float _CloudBottomY, _CloudTopY;   // global min/max for RaySlabIntersection
        int _RaymarchSteps;
        float _MaxRayDistance;
        float _HeightEdgeSoftness;
        float _CoverageScale;
        float _DepthFadeDistance;
        float4 _WindOffset;

        // === Multi-layer arrays (max 4 layers) ===
        float4 _LayerBounds[4];          // x=bottom, y=top, z=coverageThreshold, w=densityMult
        float4 _LayerDayTop[4];
        float4 _LayerDayMid[4];
        float4 _LayerDayBot[4];
        float4 _LayerSunsetTop[4];
        float4 _LayerSunsetMid[4];
        float4 _LayerSunsetBot[4];
        int _LayerCount;
        int _DebugLayerMask; // bit 0=layer0, bit1=layer1, … 0 = all
        int _LayerNoiseMask; // bit=0 → shared noise, bit=1 → independent noise per layer
        float _DebugDensityScale; // debug pass multiplier

        // === Global-only ===
        float _NoiseTileSize;
        float _LightAbsorption;
        float _CloudOpacity;
        float _CloudColorIntensity;
        float3 _SunDirection;

        TEXTURE3D(_CloudNoise3D);
        SAMPLER(sampler_CloudNoise3D);

        // === Phase 2.2: LocalDensityBuffer ===
        TEXTURE3D(_LocalDensityRT);
        SAMPLER(sampler_LocalDensityRT);
        float3 _LocalDensityCenter;
        float _LocalDensitySize;
        float _LocalDensityInfluence;

        // === Variant B: Displacement ===
        TEXTURE3D(_LocalDisplacementRT);
        SAMPLER(sampler_LocalDisplacementRT);
        float3 _LocalDisplacementCenter;
        float  _LocalDisplacementSize;
        float  _LocalDisplacementStrength;

        TEXTURE2D(_BlueNoiseTex);
        SAMPLER(sampler_BlueNoiseTex);
        float4 _BlueNoiseTex_TexelSize;

        // === Storm Cells (Phase 2.4) ===
        float4 _StormCellPos[8];
        float4 _StormCellParams[8];
        int _StormCellCount;
        float _StormDensityMult;
        float4 _StormColorDark;
        float4 _StormColorLight;
        float _StormEdgeSoftness;
        float _StormVerticalPeak;
        float _StormNoiseScale;
        float _StormNoiseStrength;
        int _StormNoiseOctaves;
        float _StormNoiseSpeed;
        float _StormClusterContrast;
        float _StormVerticalNoiseStr;   // vertical noise modulation strength (0–1)
        float _StormVerticalWarp;       // extra Y-axis warp multiplier (0–3)

        float4 _CloudTargetSize;
        float4x4 _Cloud_ViewToWorld;
        float4x4 _Cloud_InvProj;

        struct Attributes { uint vertexID : SV_VertexID; };
        struct Varyings { float4 positionCS : SV_POSITION; };

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
            return OUT;
        }

        float3 GetWorldRay(float2 uv)
        {
            float2 ndc = uv * 2.0 - 1.0;
            float4 clipRay = float4(ndc, 1.0, 1.0);
            float4 viewRay = mul(_Cloud_InvProj, clipRay);
            viewRay.xyz /= viewRay.w;
            return normalize(mul((float3x3)_Cloud_ViewToWorld, viewRay.xyz));
        }

        // Raw 2D coverage noise (0..1). Per-layer threshold applied in CloudDensity.
        float CloudCoverageNoise(float2 xz)
        {
            float v = 0.0; float amp = 0.5; float freq = 1.0; float norm = 0.0;
            [unroll(4)] for (int i = 0; i < 4; i++)
            {
                v += amp * Perlin3D(float3(xz.x * freq, 7.31, xz.y * freq), 4242u + (uint)i, 0u);
                norm += amp; amp *= 0.5; freq *= 2.0;
            }
            return v / norm * 0.5 + 0.5;
        }

        float SampleLocalDensity(float3 worldPos)
        {
            float halfSize = _LocalDensitySize * 0.5;
            float3 delta = worldPos - _LocalDensityCenter;
            // Clamp to toroidal window — prevent echo repeats across the world
            if (abs(delta.x) > halfSize || abs(delta.y) > halfSize || abs(delta.z) > halfSize)
                return 0;
            float3 uvw = delta / _LocalDensitySize + 0.5;
            return SAMPLE_TEXTURE3D_LOD(_LocalDensityRT, sampler_LocalDensityRT, uvw, 0).r;
        }

        float3 SampleLocalDisplacement(float3 worldPos)
        {
            float halfSize = _LocalDisplacementSize * 0.5;
            float3 delta = worldPos - _LocalDisplacementCenter;
            // Clamp to toroidal window — prevent echo repeats across the world
            if (abs(delta.x) > halfSize || abs(delta.y) > halfSize || abs(delta.z) > halfSize)
                return float3(0, 0, 0);
            float3 uvw = delta / _LocalDisplacementSize + 0.5;
            return SAMPLE_TEXTURE3D_LOD(_LocalDisplacementRT, sampler_LocalDisplacementRT, uvw, 0).rgb;
        }

        // Blend GhibliRamp across active layers by height-fade weight at this Y.
        float3 ComputeLayerColor(float y, float rampBlend)
        {
            float3 color = 0;
            float totalWeight = 0;
            for (int l = 0; l < _LayerCount; l++)
            {
                float4 b = _LayerBounds[l];
                float hFade = HeightProfileSimple(y, b.x, b.y, _HeightEdgeSoftness);
                if (hFade < 0.001) continue;
                float h01 = saturate((y - b.x) / max(b.y - b.x, 1e-4));
                float3 ramp = GhibliRamp(h01, rampBlend,
                    _LayerDayTop[l].rgb, _LayerDayMid[l].rgb, _LayerDayBot[l].rgb,
                    _LayerSunsetTop[l].rgb, _LayerSunsetMid[l].rgb, _LayerSunsetBot[l].rgb);
                color += ramp * hFade;
                totalWeight += hFade;
            }
            return totalWeight > 0.001 ? color / totalWeight : 0;
        }

        // Per-layer 3D noise shape. Each layer samples at a different offset
        // so cloud shapes are uncorrelated — lower layers fill upper-layer holes.
        float LayerNoiseShape(float3 samplePos, int layerIndex)
        {
            // LayerNoiseMask bit=0 → shared noise (same sample as layer 0 → beautiful uniform blanket)
            // LayerNoiseMask bit=1 → independent noise (unique pattern per layer)
            int useOwnNoise = (_LayerNoiseMask >> layerIndex) & 1;
            float seed = useOwnNoise ? ((float)(layerIndex) * 137.0 + 1.0) : 0.0;
            float3 layerPos = samplePos + float3(seed, seed * 1.7, seed * 0.3);
            float3 uvw = layerPos / _NoiseTileSize;
            uvw = frac(uvw);
            float4 noise = SAMPLE_TEXTURE3D_LOD(_CloudNoise3D, sampler_CloudNoise3D, uvw, 0);
            float s = noise.r * lerp(0.5, 1.0, noise.a) + (noise.g + noise.b) * 0.15;
            return saturate(s);
        }

        // ---------------------------------------------------------------------------
        // T-CLOUD39: Procedural storm noise — NO pre-baked texture.
        //
        // Root cause of "corrugated pipe" (T-CLOUD35–38):
        //   frac(uvw) on 128³ texture with periodic Worley (freq=8, period=128)
        //   creates a REPEATING GRID of exactly 8 features per tile. Even with
        //   domain warp and multi-octave, the grid dominates → гофротруба.
        //
        // Fix: use procedural abs(Perlin3D) FBM directly — no texture, no frac(),
        // no periodicity. abs(Perlin) creates natural billowy "bubbles" that
        // look like cloud clusters. Non-integer lacunarity (2.3) prevents
        // harmonic alignment across octaves.
        //
        // Perf: Perlin samples 8 grid points per octave (vs Worley's 27).
        //       Early envelope gates prevent noise evaluation outside cells.
        // ---------------------------------------------------------------------------

        // Single-octave abs(Perlin) — returns 0..1 billowy value
        float StormBillow(float3 pos, float freq, uint seed)
        {
            return abs(Perlin3D_noPeriod(pos * freq, seed));
        }

        // Multi-octave abs(Perlin) FBM with non-integer lacunarity.
        // Non-integer lacunarity (2.3 instead of 2.0) shifts octave harmonics
        // so they never align → chaotic, organic shapes.
        float StormBillowFbm(float3 pos, float baseScale, uint seed)
        {
            float val = 0; float amp = 1.0; float norm = 0;
            float freq = 1.0 / max(baseScale, 0.001);
            for (int o = 0; o < _StormNoiseOctaves; o++)
            {
                val += StormBillow(pos, freq, seed + (uint)(o * 73));
                norm += amp;
                amp *= 0.5;
                freq *= 2.3;  // non-integer → harmonics misalign → organic
            }
            return val / max(norm, 0.001);
        }

        // 3D domain warp using procedural Perlin (not texture).
        // Returns a 3D offset vector that breaks radial symmetry.
        float3 StormWarp3D(float3 pos, float scale, float strength)
        {
            float invScale = 1.0 / max(scale, 0.001);
            float wx = Perlin3D_noPeriod(pos * invScale, 42u);
            float wy = Perlin3D_noPeriod((pos + float3(0, 137, 0)) * invScale, 99u);
            float wz = Perlin3D_noPeriod((pos + float3(0, 0, 271)) * invScale, 156u);
            return float3(wx, wy * 0.35, wz) * strength;
        }

        // ---------------------------------------------------------------------------
        // StormDensity — organic storm cloud density (T-CLOUD39 rewrite).
        //
        // Pipeline per cell (cheap gates FIRST — noise only inside the cell):
        //   1. Distance envelope (XZ) — safety clip
        //   2. Vertical envelope — hard gate at bottom/top
        //   3. Vertical profile — asymmetric anvil via _StormVerticalPeak
        //   4. Procedural domain warp (3D Perlin) — breaks radial symmetry
        //   5. Procedural abs(Perlin) FBM — organические биллоу-кластеры
        //   6. Fine cauliflower octave — erodes edges, keeps core
        //
        // Key insight (T-CLOUD39): abs(Perlin3D) with non-integer lacunarity
        // produces natural organic clusters WITHOUT repeating grid artifacts.
        // No pre-baked texture involved → no frac(), no periodic hash, no tiling.
        // ---------------------------------------------------------------------------
        float StormDensity(float3 worldPos, out float3 stormColor)
        {
            float totalDensity = 0;
            float3 blendedColor = 0;
            float colorWeight = 0;

            float3 noiseWind = _WindOffset.xyz * _StormNoiseSpeed;

            for (int i = 0; i < _StormCellCount; i++)
            {
                float3 cellPos = _StormCellPos[i].xyz;
                float intensity = _StormCellPos[i].w;
                float radius = _StormCellParams[i].x;
                float bottomY = _StormCellParams[i].y;
                float topY = _StormCellParams[i].z;

                // ── Height gate ──
                if (worldPos.y < bottomY || worldPos.y > topY) continue;

                float hRange = max(topY - bottomY, 1.0);
                float h01 = saturate((worldPos.y - bottomY) / hRange);

                // ── 1. Distance envelope — softer clip to reduce visible shell ──
                float distXZ = length(worldPos.xz - cellPos.xz);
                float envelope = 1.0 - smoothstep(radius * 0.6, radius * 1.6, distXZ);
                if (envelope < 0.001) continue;

                // ── 2. Vertical profile — soft edges + noise-modulated anvil ──
                //      Removed separate vEnvelope (was creating hard horizontal bands).
                //      Soft bottom/top fade built directly into profile.
                float3 seedOff = float3((float)(i + 1) * 137.3,
                                        (float)(i + 1) * 57.1,
                                        (float)(i + 1) * 91.7);

                // Soft edge fades: _StormEdgeSoftness controls fade fraction (0.01–0.5 → 1%–50% of height)
                float vFadeFrac = _StormEdgeSoftness;
                float vFadeBot = hRange * vFadeFrac * 0.8;
                float vFadeTop = hRange * vFadeFrac;
                float vEdgeBot = smoothstep(bottomY, bottomY + vFadeBot, worldPos.y);
                float vEdgeTop = 1.0 - smoothstep(topY - vFadeTop, topY, worldPos.y);
                float vSoftEdge = vEdgeBot * vEdgeTop;
                if (vSoftEdge < 0.001) continue;

                // Asymmetric dome: peak at _StormVerticalPeak, steeper above (anvil)
                float vPeak = 1.0 - abs(h01 - _StormVerticalPeak)
                              / max(h01 > _StormVerticalPeak ? (1.0 - _StormVerticalPeak)
                                                              : _StormVerticalPeak, 0.05);
                vPeak = saturate(vPeak);
                vPeak = lerp(0.3, 1.0, vPeak * vPeak);

                // Vertical noise — breaks horizontal contour lines.
                // _StormVerticalNoiseStr: 0=flat profile, 1=max variation (±50%).
                float vnStr = _StormVerticalNoiseStr;
                float vNoise = Perlin3D_noPeriod((worldPos + seedOff) * 0.0015, 500u + (uint)(i * 37));
                float vNoise2 = Perlin3D_noPeriod((worldPos + seedOff) * 0.004, 600u + (uint)(i * 73));
                float vMod = lerp(1.0, vNoise * 0.8 + vNoise2 * 0.5 + 0.6, vnStr);
                float vProfile = saturate(vSoftEdge * vPeak * vMod);
                if (vProfile < 0.001) continue;

                // ── 3. Procedural domain warp (3D) — breaks radial + horizontal symmetry ──
                float clusterScale = max(radius * 0.25, _StormNoiseScale);
                float warpScale = clusterScale * 2.5;
                float warpStrength = clusterScale * _StormNoiseStrength * 1.5;
                float3 warpOff = StormWarp3D(worldPos + noiseWind, warpScale, warpStrength);
                // Extra vertical warp to break horizontal bands (0=off, 3=max)
                warpOff.y *= _StormVerticalWarp;
                float3 clusterPos = worldPos + warpOff + noiseWind;
                clusterPos += seedOff * 0.1;

                // ── 4. Procedural abs(Perlin) FBM — THE organic shape ──
                float bigClusters = StormBillowFbm(clusterPos, clusterScale, 1000u + (uint)(i * 211));

                float contrast = _StormClusterContrast;
                float shape = smoothstep(0.25 - contrast * 0.8,
                                         0.25 + contrast * 0.8, bigClusters);
                if (shape < 0.01) continue;

                // ── 5. Fine cauliflower ──
                float fineScale = clusterScale * 0.22;
                float fine = StormBillowFbm(clusterPos + float3(31.7, 17.3, 7.1),
                                           fineScale, 2000u + (uint)(i * 137));
                float inner = 0.55 + 0.45 * smoothstep(0.25, 0.75, fine);

                // ── Combine (NO vEnvelope — vProfile handles full vertical shape) ──
                float cellDensity = shape * inner * envelope * vProfile
                                  * intensity * _StormDensityMult;

                if (cellDensity > 0.002)
                {
                    totalDensity += cellDensity;
                    float3 cellColor = lerp(_StormColorLight.rgb, _StormColorDark.rgb,
                        saturate(cellDensity * 2.0));
                    blendedColor += cellColor * cellDensity;
                    colorWeight += cellDensity;
                }
            }

            stormColor = colorWeight > 0.001 ? blendedColor / colorWeight : _StormColorDark.rgb;
            return totalDensity;
        }

        // Density + blended color per step. Returns density; outputs blended layer color.
        float CloudDensity(float3 worldPos, float3 cameraPos, float coverageNoise,
                           float rampBlend, out float3 layerColor)
        {
        #if defined(_LOCALDENSITY_DISPLACEMENT)
            // Gate displacement to ship altitude (±400m). Upper layers unaffected.
            float shipY = _LocalDisplacementCenter.y;
            float dispRange = 400.0;
            float dispFactor = 1.0 - saturate(abs(worldPos.y - shipY) / dispRange);
            if (dispFactor > 0.001)
            {
                float3 disp = SampleLocalDisplacement(worldPos);
                disp.y *= 0.15; // suppress vertical displacement (wake is horizontal)
                worldPos = worldPos + disp * _LocalDisplacementStrength * dispFactor;
            }
        #endif

            float3 samplePos = CameraRelativePosition(worldPos, cameraPos, _NoiseTileSize);
            samplePos += _WindOffset.xyz;

            float totalDensity = 0;
            float3 blendedColor = 0;
            float colorWeight = 0;

            for (int l = 0; l < _LayerCount; l++)
            {
                // Debug layer mask: 0 = all, else bit-select
                if (_DebugLayerMask != 0 && ((_DebugLayerMask >> l) & 1) == 0) continue;

                float4 b = _LayerBounds[l];
                float hFade = HeightProfileSimple(worldPos.y, b.x, b.y, _HeightEdgeSoftness);
                if (hFade < 0.001) continue;

                // Per-layer independent noise shape
                float shape = LayerNoiseShape(samplePos, l);
                float layerCov = smoothstep(b.z, b.z + 0.15, coverageNoise);
                float ld = shape * hFade * layerCov * b.w;
                totalDensity += ld;

                // Blend color by per-layer density contribution
                float h01 = saturate((worldPos.y - b.x) / max(b.y - b.x, 1e-4));
                float3 ramp = GhibliRamp(h01, rampBlend,
                    _LayerDayTop[l].rgb, _LayerDayMid[l].rgb, _LayerDayBot[l].rgb,
                    _LayerSunsetTop[l].rgb, _LayerSunsetMid[l].rgb, _LayerSunsetBot[l].rgb);
                blendedColor += ramp * ld;
                colorWeight += ld;
            }

            layerColor = colorWeight > 0.001 ? blendedColor / colorWeight : 0;

        #if !defined(_LOCALDENSITY_DISPLACEMENT)
            float local = SampleLocalDensity(worldPos);
            totalDensity = max(0.0, totalDensity - local * _LocalDensityInfluence);
        #endif

            // Phase 2.4: Storm cell density injection
            float3 stormColor;
            float stormD = StormDensity(worldPos, stormColor);
            if (stormD > 0.001)
            {
                totalDensity += stormD;
                float stormWeight = stormD / max(totalDensity, 0.001);
                layerColor = lerp(layerColor, stormColor, stormWeight);
            }

            return totalDensity;
        }
        ENDHLSL

        // ============================================================
        // Pass 0 — B&W density (debug)
        // ============================================================
        Pass
        {
            Name "VolumetricClouds_BW"
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _LOCALDENSITY_DISPLACEMENT
            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionCS.xy * _CloudTargetSize.zw;
                float3 rayDir = GetWorldRay(uv);
                float3 rayOrigin = _WorldSpaceCameraPos;
                float tMin, tMax;
                if (!RaySlabIntersection(rayOrigin, rayDir, _CloudBottomY, _CloudTopY, _MaxRayDistance, tMin, tMax))
                    return float4(0,0,0,0);
                float2 covXZ = (rayOrigin.xz + rayDir.xz * tMin) * _CoverageScale + _WindOffset.xz * 0.25;
                float covNoise = CloudCoverageNoise(covXZ);
                float stepSize = (tMax - tMin) / (float)_RaymarchSteps;
                float totalDensity = 0.0;
                float rampBlend = saturate(_SunDirection.y * 2.0);
                [loop] for (int i = 0; i < _RaymarchSteps; i++)
                {
                    float t = tMin + ((float)i + 0.5) * stepSize;
                    if (t > tMax) break;
                    float3 c;
                    totalDensity += CloudDensity(rayOrigin + rayDir * t, rayOrigin, covNoise, rampBlend, c) * stepSize;
                }
                float d = saturate(totalDensity * _DebugDensityScale);
                return float4(d.xxx, d);
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 1 — Full raymarch + depth-test + per-layer color
        // ============================================================
        Pass
        {
            Name "VolumetricClouds_Color"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _BLUE_NOISE_ON
            #pragma multi_compile_local _ _LOCALDENSITY_DISPLACEMENT

            #define LIGHT_STEPS 6

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionCS.xy * _CloudTargetSize.zw;

                float3 rayDir = GetWorldRay(uv);
                float3 rayOrigin = _WorldSpaceCameraPos;

                float tMin, tMax;
                if (!RaySlabIntersection(rayOrigin, rayDir, _CloudBottomY, _CloudTopY, _MaxRayDistance, tMin, tMax))
                    return float4(0, 0, 0, 0);

                float sceneDepth = SampleSceneDepth(uv);
                float sceneLinear = 1e9; // no geometry = huge
                // Linear01Depth → 0=near, 1=far on ALL platforms (reversed-Z safe)
                float sceneLinear01 = Linear01Depth(sceneDepth, _ZBufferParams);
                if (sceneLinear01 < 0.999) // 1.0 = sky/far, <1.0 = geometry
                {
                    sceneLinear = LinearEyeDepth(sceneDepth, _ZBufferParams);
                    tMax = min(tMax, sceneLinear);
                    if (tMax <= tMin) return float4(0, 0, 0, 0);
                }

                float2 covXZ = (rayOrigin.xz + rayDir.xz * tMin) * _CoverageScale + _WindOffset.xz * 0.25;
                float covNoise = CloudCoverageNoise(covXZ);
                float stepSize = (tMax - tMin) / (float)_RaymarchSteps;

                float rampBlend = saturate(_SunDirection.y * 2.0);
                float4 accumulated = float4(0, 0, 0, 0);

                [loop] for (int i = 0; i < _RaymarchSteps && accumulated.a < 0.99; i++)
                {
                    float t = tMin + ((float)i + 0.5) * stepSize;
                    if (t > tMax) break;

                    float3 samplePos = rayOrigin + rayDir * t;
                    float3 cloudColor;
                    float density = CloudDensity(samplePos, rayOrigin, covNoise, rampBlend, cloudColor);

                    if (density > 0.001)
                    {
                        float lightStepSize = min(stepSize * 3.0, 200.0);
                        float lightTransmittance = 1.0;
                        for (int j = 0; j < LIGHT_STEPS; j++)
                        {
                            float3 lightPos = samplePos + _SunDirection * ((float)j + 0.5) * lightStepSize;
                            float3 dummy;
                            float lightDensity = CloudDensity(lightPos, rayOrigin, covNoise, rampBlend, dummy);
                            lightTransmittance *= exp(-lightDensity * lightStepSize * _LightAbsorption * 0.5);
                        }

                        float cosTheta = dot(rayDir, _SunDirection);
                        float hg = HG(cosTheta, 0.7);
                        float ms = MultiScatterApprox(lightTransmittance, 0.5);

                        float3 ambient = cloudColor * 0.25;
                        float silver = SilverLining(cosTheta, 0.3);
                        float3 lighting = (cloudColor * hg * ms * lightTransmittance + ambient + silver * cloudColor) * _CloudColorIntensity;

                        float stepTransmittance = BeerLambert(density, stepSize, _LightAbsorption);
                        float stepAbsorption = 1.0 - stepTransmittance;
                        float transmittance = 1.0 - accumulated.a;
                        accumulated.rgb += lighting * transmittance * stepAbsorption;
                        accumulated.a   += stepAbsorption * _CloudOpacity;
                    }
                }

                // Post-loop depth fade: cloud opacity scales with how much cloud is in front of geometry
                float cloudThickness = tMax - tMin;
                float depthFade = saturate(cloudThickness / max(_DepthFadeDistance, 1e-4));
                accumulated.rgb *= depthFade;
                accumulated.a   *= depthFade;
                return accumulated;
            }
            ENDHLSL
        }

        Pass
        {
            Name "VolumetricClouds_Composite"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings IN) : SV_Target { return half4(0,0,0,0); }
            ENDHLSL
        }

        Pass
        {
            Name "VolumetricClouds_History"
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings IN) : SV_Target { return half4(0,0,0,0); }
            ENDHLSL
        }
    }
    FallBack Off
}
