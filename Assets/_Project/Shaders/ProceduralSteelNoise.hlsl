#ifndef PROCEDURAL_STEEL_NOISE_INCLUDED
#define PROCEDURAL_STEEL_NOISE_INCLUDED

// ProceduralSteelNoise — object-space 3D noise for Steel v2.
// Robust sine-free hash + smooth domain warp + fbm + Worley cells.
// No UV, no world space => correct on all faces, FO-safe.

float PCSteel_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCSteel_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCSteel_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCSteel_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCSteel_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCSteel_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCSteel_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCSteel_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCSteel_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCSteel_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCSteel_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCSteel_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

float PCSteel_Voronoi3(float3 p)
{
    float3 g = floor(p);
    float minD = 8.0;
    [unroll]
    for (int x = -1; x <= 1; x++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int z = -1; z <= 1; z++)
            {
                float3 cell = g + float3((float)x, (float)y, (float)z);
                float3 rnd = float3(
                    PCSteel_Hash13(cell + 0.0),
                    PCSteel_Hash13(cell + 17.0),
                    PCSteel_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return saturate(minD);
}

void ProceduralSteelNoise_float(
    float3 ObjectPos, float Scale, float Detail, float Cell, float Warp, float Seed,
    out float Noise)
{
    float3 p = ObjectPos * max(0.001, Scale) + Seed;

    // Smooth domain warp: low-frequency value noise (interpolated) => no cell seams.
    float3 w = float3(
        PCSteel_ValueNoise3(p * 0.5 + 11.3),
        PCSteel_ValueNoise3(p * 0.5 + 27.1),
        PCSteel_ValueNoise3(p * 0.5 + 43.7)) - 0.5;
    p += w * Warp;

    float f = PCSteel_Fbm3(p, Detail);
    float v = PCSteel_Voronoi3(p * max(0.1, Cell));

    // fbm as the main pattern, cells add irregular breakup.
    Noise = saturate(lerp(f, v, 0.30));
}

void ProceduralSteelNoise_half(
    half3 ObjectPos, half Scale, half Detail, half Cell, half Warp, half Seed,
    out half Noise)
{
    float n;
    ProceduralSteelNoise_float(
        (float3)ObjectPos, (float)Scale, (float)Detail, (float)Cell, (float)Warp, (float)Seed, n);
    Noise = (half)n;
}

#endif
