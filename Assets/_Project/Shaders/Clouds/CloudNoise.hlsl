// CloudNoise.hlsl — HLSL port of CloudMath.cs v7.0 (double/hash-seeded variant)
// Phase 1.1: Perlin3D + Fbm + Worley3D + InvertedWorley
// Uses float32 arithmetic with uint-based multiply-shift hash.
// Periodic (seamless) noise: cell indices modded by texSize for tileable output.
//
// Original source: Assets/CloudGenerator/CloudGenerator_v7.0/CloudGenerator_v7.0/CloudMath.cs

#ifndef PROJECTC_CLOUD_NOISE_INCLUDED
#define PROJECTC_CLOUD_NOISE_INCLUDED

// ---------------------------------------------------------------------------
// Hash3 — uint multiply-shift hash (no 64-bit needed)
// C# uses long+double-mod; HLSL uses uint overflow wrapping (same as C# unchecked).
// ---------------------------------------------------------------------------
uint Hash3(uint3 p, uint seed)
{
    uint u = p.x * 374761393u ^ p.y * 668265263u ^ p.z * 2147483647u;
    u = (u ^ (u >> 13)) * 1274126177u;
    u ^= seed * 123456789u;
    u = (u ^ (u >> 16)) * 2246822507u;
    u ^= (u >> 13);
    return u & 0x7FFFFFFFu; // 31-bit positive
}

// Periodic variant: mod cell index by period before hashing
uint Hash3Periodic(uint3 p, uint seed, uint period)
{
    uint3 mp = p % period;
    return Hash3(mp, seed);
}

// ---------------------------------------------------------------------------
// Fade3 — quintic smoothstep (same curve as C# double version)
// ---------------------------------------------------------------------------
float Fade3(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// ---------------------------------------------------------------------------
// Lerp3 — standard linear interpolation
// ---------------------------------------------------------------------------
float Lerp3(float a, float b, float t)
{
    return a + (b - a) * t;
}

// ---------------------------------------------------------------------------
// Grad3 — Perlin gradient based on hash value (same logic as C#)
// ---------------------------------------------------------------------------
float Grad3(uint h, float3 p)
{
    uint g = h & 15u;
    float u = (g < 8u) ? p.x : p.y;
    float v;
    if (g < 4u)
        v = p.y;
    else if (g == 12u || g == 14u)
        v = p.x;
    else
        v = p.z;
    return (((g & 1u) != 0u) ? -u : u) + (((g & 2u) != 0u) ? -v : v);
}

// ---------------------------------------------------------------------------
// Perlin3D — classic 3D Perlin noise (-1..1)
// With optional period for seamless tiling (0 = no periodicity).
// ---------------------------------------------------------------------------
float Perlin3D(float3 p, uint seed, uint period)
{
    int xi = (int)floor(p.x);
    int yi = (int)floor(p.y);
    int zi = (int)floor(p.z);

    float xf = p.x - floor(p.x);
    float yf = p.y - floor(p.y);
    float zf = p.z - floor(p.z);

    float u = Fade3(xf);
    float v = Fade3(yf);
    float w = Fade3(zf);

    uint3 c000 = uint3(xi, yi, zi);
    uint3 c100 = uint3(xi + 1, yi, zi);
    uint3 c010 = uint3(xi, yi + 1, zi);
    uint3 c110 = uint3(xi + 1, yi + 1, zi);
    uint3 c001 = uint3(xi, yi, zi + 1);
    uint3 c101 = uint3(xi + 1, yi, zi + 1);
    uint3 c011 = uint3(xi, yi + 1, zi + 1);
    uint3 c111 = uint3(xi + 1, yi + 1, zi + 1);

    uint aaa, baa, aba, bba, aab, bab, abb, bbb;
    if (period > 0u)
    {
        aaa = Hash3Periodic(c000, seed, period) % 12u;
        baa = Hash3Periodic(c100, seed, period) % 12u;
        aba = Hash3Periodic(c010, seed, period) % 12u;
        bba = Hash3Periodic(c110, seed, period) % 12u;
        aab = Hash3Periodic(c001, seed, period) % 12u;
        bab = Hash3Periodic(c101, seed, period) % 12u;
        abb = Hash3Periodic(c011, seed, period) % 12u;
        bbb = Hash3Periodic(c111, seed, period) % 12u;
    }
    else
    {
        aaa = Hash3(c000, seed) % 12u;
        baa = Hash3(c100, seed) % 12u;
        aba = Hash3(c010, seed) % 12u;
        bba = Hash3(c110, seed) % 12u;
        aab = Hash3(c001, seed) % 12u;
        bab = Hash3(c101, seed) % 12u;
        abb = Hash3(c011, seed) % 12u;
        bbb = Hash3(c111, seed) % 12u;
    }

    float x1 = Lerp3(Grad3(aaa, float3(xf, yf, zf)), Grad3(baa, float3(xf - 1.0, yf, zf)), u);
    float x2 = Lerp3(Grad3(aba, float3(xf, yf - 1.0, zf)), Grad3(bba, float3(xf - 1.0, yf - 1.0, zf)), u);
    float y1 = Lerp3(x1, x2, v);

    float x3 = Lerp3(Grad3(aab, float3(xf, yf, zf - 1.0)), Grad3(bab, float3(xf - 1.0, yf, zf - 1.0)), u);
    float x4 = Lerp3(Grad3(abb, float3(xf, yf - 1.0, zf - 1.0)), Grad3(bbb, float3(xf - 1.0, yf - 1.0, zf - 1.0)), u);
    float y2 = Lerp3(x3, x4, v);

    return Lerp3(y1, y2, w);
}

// ---------------------------------------------------------------------------
// Perlin3D_noPeriod — convenience wrapper
// ---------------------------------------------------------------------------
float Perlin3D_noPeriod(float3 p, uint seed)
{
    return Perlin3D(p, seed, 0u);
}

// ---------------------------------------------------------------------------
// Fbm — Fractal Brownian Motion (Perlin-based)
// Returns normalized [-1..1], remap to [0..1] with: fbm * 0.5 + 0.5
// ---------------------------------------------------------------------------
float Fbm(float3 p, int octaves, float persistence, float lacunarity, uint seed, uint period)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float maxValue = 0.0;

    [unroll(8)]
    for (int i = 0; i < 8; i++)
    {
        if (i >= octaves) break;
        value += amplitude * Perlin3D(p * frequency, seed + (uint)i, period);
        maxValue += amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }

    return value / maxValue;
}

// ---------------------------------------------------------------------------
// Worley3D — cellular noise (returns distance to nearest feature point)
// freq controls feature density. period = 0 for non-seamless.
// ---------------------------------------------------------------------------
float Worley3D(float3 p, float freq, uint seed, uint period)
{
    float3 pf = p * freq;
    int ix = (int)floor(pf.x);
    int iy = (int)floor(pf.y);
    int iz = (int)floor(pf.z);

    float minDist = 1e9;

    [unroll]
    for (int dx = -1; dx <= 1; dx++)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                int cx = ix + dx;
                int cy = iy + dy;
                int cz = iz + dz;

                uint3 cell = uint3((uint)(cx + 65536), (uint)(cy + 65536), (uint)(cz + 65536));
                uint h;
                if (period > 0u)
                    h = Hash3Periodic(cell, seed, period);
                else
                    h = Hash3(cell, seed);

                float sx = (float)((h % 1000u)) / 1000.0;
                float sy = (float)(((h / 1000u) % 1000u)) / 1000.0;
                float sz = (float)(((h / 1000000u) % 1000u)) / 1000.0;

                float dx2 = pf.x - ((float)cx + sx);
                float dy2 = pf.y - ((float)cy + sy);
                float dz2 = pf.z - ((float)cz + sz);

                float dist = sqrt(dx2 * dx2 + dy2 * dy2 + dz2 * dz2);
                if (dist < minDist) minDist = dist;
            }
        }
    }

    return minDist;
}

// ---------------------------------------------------------------------------
// Worley3D_noPeriod — convenience wrapper
// ---------------------------------------------------------------------------
float Worley3D_noPeriod(float3 p, float freq, uint seed)
{
    return Worley3D(p, freq, seed, 0u);
}

// ---------------------------------------------------------------------------
// InvertedWorley — 1 - min(worley, 1.0) for erosion texture channel
// ---------------------------------------------------------------------------
float InvertedWorley(float3 p, float freq, uint seed, uint period)
{
    return 1.0 - min(Worley3D(p, freq, seed, period), 1.0);
}

float InvertedWorley_noPeriod(float3 p, float freq, uint seed)
{
    return 1.0 - min(Worley3D_noPeriod(p, freq, seed), 1.0);
}

// ---------------------------------------------------------------------------
// SampleCloudNoise3D — combined Perlin+Worley channels from baked 3D texture
// Used after 1.2 texture is baked. For now, direct function calls during bake.
// channels: R=PerlinFBM, G=WorleyLow(freq=4), B=WorleyHigh(freq=16), A=InvertedWorley
// ---------------------------------------------------------------------------
float4 SampleCloudNoise3D(Texture3D<float4> noiseTex, SamplerState noiseSampler, float3 uvw)
{
    return noiseTex.SampleLevel(noiseSampler, uvw, 0);
}

#endif // PROJECTC_CLOUD_NOISE_INCLUDED
