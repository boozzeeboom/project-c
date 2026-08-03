// CloudCommon.hlsl — Common helpers for volumetric cloud rendering
// Phase 1.1: remap, height profile, phase functions, ray helpers
// Used by VolumetricClouds.shader and BakeCloudNoise.compute

#ifndef PROJECTC_CLOUD_COMMON_INCLUDED
#define PROJECTC_CLOUD_COMMON_INCLUDED

// ---------------------------------------------------------------------------
// CloudRemap: maps value from [inMin, inMax] to [outMin, outMax]
// (Renamed to avoid conflict with URP Core.hlsl Remap)
// ---------------------------------------------------------------------------
float CloudRemap(float value, float inMin, float inMax, float outMin, float outMax)
{
    float t = (value - inMin) / max(inMax - inMin, 1e-8);
    return lerp(outMin, outMax, saturate(t));
}

// ---------------------------------------------------------------------------
// HeightProfile: gradient 0 at cloudBottom, peak in middle, 0 at cloudTop
// Uses smoothstep for soft edges.
// peakPosition: fraction of layer height where peak sits (default 0.3)
// edgeSoftness: fraction of layer for fade-in/fade-out (default 0.15)
// ---------------------------------------------------------------------------
float HeightProfile(float y, float cloudBottom, float cloudTop, float peakPosition, float edgeSoftness)
{
    float h = (y - cloudBottom) / max(cloudTop - cloudBottom, 1e-4);
    float range = cloudTop - cloudBottom;
    float bottomFade = range * edgeSoftness;
    float topFade = range * edgeSoftness;

    float bottomFactor = smoothstep(cloudBottom, cloudBottom + bottomFade, y);
    float topFactor = 1.0 - smoothstep(cloudTop - topFade, cloudTop, y);
    float peakFactor = 1.0 - abs(h - peakPosition) / max(peakPosition, 0.5 - peakPosition);

    return saturate(bottomFactor * topFactor * smoothstep(0.1, 0.7, peakFactor));
}

// ---------------------------------------------------------------------------
// HeightProfileSimple: simplified version — just smoothstep in/out
// ---------------------------------------------------------------------------
float HeightProfileSimple(float y, float cloudBottom, float cloudTop, float edgeSoftness)
{
    float range = cloudTop - cloudBottom;
    // Min 100m fade so narrow layers don't have razor-sharp edges
    float fade = max(100.0, range * edgeSoftness);
    // Clamp fade to half the range (prevents full-layer fade)
    fade = min(fade, range * 0.45);
    float bottom = smoothstep(cloudBottom, cloudBottom + fade, y);
    float top = 1.0 - smoothstep(cloudTop - fade, cloudTop, y);
    return bottom * top;
}

// ---------------------------------------------------------------------------
// HG — Henyey-Greenstein phase function for Mie scattering
// g: asymmetry parameter (-1=back, 0=iso, 1=forward). Clouds ~0.5-0.8
// cosTheta: dot(viewDir, lightDir)
// ---------------------------------------------------------------------------
float HG(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 - g2) / max(denom * sqrt(denom), 1e-5);
}

// ---------------------------------------------------------------------------
// HG_DualLobe — combined forward+backward scattering lobes
// forwardWeight: mix between forward and backward (0=all back, 1=all forward)
// ---------------------------------------------------------------------------
float HG_DualLobe(float cosTheta, float gForward, float gBack, float forwardWeight)
{
    return lerp(HG(cosTheta, gBack), HG(cosTheta, gForward), forwardWeight);
}

// ---------------------------------------------------------------------------
// MultiScatterApprox — energy-conserving multi-scatter approximation
// transmittance: light transmittance through cloud (0..1)
// power: typically 0.5 (sqrt)
// ---------------------------------------------------------------------------
float MultiScatterApprox(float transmittance, float power)
{
    return pow(max(transmittance, 1e-4), power);
}

// ---------------------------------------------------------------------------
// SilverLining — rim-like bright edge when looking near the sun direction
// cosViewLight: dot(viewDir, lightDir)
// intensity: how strong the effect is
// ---------------------------------------------------------------------------
float SilverLining(float cosViewLight, float intensity)
{
    return pow(1.0 - abs(cosViewLight), 8.0) * intensity;
}

// ---------------------------------------------------------------------------
// BeerLambert — absorption along a step
// density: local density
// stepSize: step size in world units
// absorption: absorption coefficient
// ---------------------------------------------------------------------------
float BeerLambert(float density, float stepSize, float absorption)
{
    return exp(-density * stepSize * absorption);
}

// ---------------------------------------------------------------------------
// RaySlabIntersection — intersect ray with horizontal slab [yMin, yMax]
// Returns tMin, tMax for the intersection; rayDir.y near-zero handled.
// ---------------------------------------------------------------------------
bool RaySlabIntersection(float3 rayOrigin, float3 rayDir, float yMin, float yMax,
    float maxDist, out float tMin, out float tMax)
{
    tMin = 0.0;
    tMax = maxDist;

    if (abs(rayDir.y) < 0.0001)
    {
        // Horizontal ray — only if camera inside slab
        if (rayOrigin.y > yMin && rayOrigin.y < yMax)
        {
            tMin = 0.0;
            tMax = maxDist;
            return true;
        }
        return false;
    }

    float t1 = (yMax - rayOrigin.y) / rayDir.y;
    float t2 = (yMin - rayOrigin.y) / rayDir.y;
    tMin = min(t1, t2);
    tMax = max(t1, t2);

    if (tMax < 0.0 || tMin > maxDist)
        return false;

    tMin = max(0.0, tMin);
    tMax = min(tMax, maxDist);

    return tMin < tMax;
}

// ---------------------------------------------------------------------------
// CameraRelativePosition — position relative to camera, tiled for large worlds
// Prevents float32 precision issues on 80k×80k scenes.
// Tile ONLY XZ: Y stays absolute so the pattern does not pop when the camera
// crosses a tile boundary vertically (and height profile needs real Y).
// ---------------------------------------------------------------------------
float3 CameraRelativePosition(float3 worldPos, float3 cameraPos, float tileSize)
{
    float3 camTile = float3(floor(cameraPos.x / tileSize) * tileSize, 0.0,
        floor(cameraPos.z / tileSize) * tileSize);
    return worldPos - camTile;
}

// ---------------------------------------------------------------------------
// GhibliRamp — sample a Ghibli-style day/sunset color ramp
// height01: normalized height in cloud layer (0=bottom, 1=top)
// rampBlend: 0=sunset, 1=day (based on sunDir.y)
// ---------------------------------------------------------------------------
float3 GhibliRamp(float height01, float rampBlend,
    float3 dayTop, float3 dayMid, float3 dayBot,
    float3 sunsetTop, float3 sunsetMid, float3 sunsetBot)
{
    float3 dayColor = lerp(dayBot, lerp(dayMid, dayTop, smoothstep(0.3, 0.7, height01)), smoothstep(0.1, 0.5, height01));
    float3 sunsetColor = lerp(sunsetBot, lerp(sunsetMid, sunsetTop, smoothstep(0.3, 0.7, height01)), smoothstep(0.1, 0.5, height01));
    return lerp(sunsetColor, dayColor, rampBlend);
}

#endif // PROJECTC_CLOUD_COMMON_INCLUDED
