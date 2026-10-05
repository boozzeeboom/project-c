#ifndef PROCEDURAL_BRONZE_NOISE_INCLUDED
#define PROCEDURAL_BRONZE_NOISE_INCLUDED

// ProceduralBronzeNoise — object-space 3D noise for Bronze.
// ПРАВИЛО (T-BRONZE01): только ОДНО-выходные функции File-режима.
// Мульти-выходные функции ломаются при перезаписи графа из MCP
// (слоты CF-ноды перегенерируются как N входов + 1 выход).
// Сигнатуры НЕ МЕНЯТЬ (слоты нод привязаны номерами слотов).
// Хеш без sin (Hoskins-style), warp гладкий (интерполированный).

float PCBronze_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCBronze_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCBronze_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCBronze_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCBronze_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCBronze_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCBronze_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCBronze_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCBronze_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCBronze_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCBronze_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCBronze_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

float PCBronze_Voronoi3(float3 p)
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
                    PCBronze_Hash13(cell + 0.0),
                    PCBronze_Hash13(cell + 17.0),
                    PCBronze_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return saturate(minD);
}

// Ridged-преобразование: тонкие линии там, где fbm пересекает 0.5.
float PCBronze_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

// Тон: fbm + Worley из object-координат (FO-safe).
float PCBronze_Base(float3 ObjectPos, float Scale, float Detail, float Warp, float Seed)
{
    float3 p = ObjectPos * max(0.001, Scale) + Seed;
    float3 w = float3(
        PCBronze_ValueNoise3(p * 0.5 + 11.3),
        PCBronze_ValueNoise3(p * 0.5 + 27.1),
        PCBronze_ValueNoise3(p * 0.5 + 43.7)) - 0.5;
    p += w * Warp;
    float f = PCBronze_Fbm3(p, Detail);
    float v = PCBronze_Voronoi3(p);
    return saturate(lerp(f, v, 0.30));
}

// Царапины A: растяжка X/Z -> полосы вдоль Y (вертикальные).
float PCBronze_ScratchDir(float3 ObjectPos, float ScratchScale, float SX, float SY, float SZ, float ScratchDetail, float ScratchSeed)
{
    float3 sp = ObjectPos * max(0.001, ScratchScale) + ScratchSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    return PCBronze_Ridge(PCBronze_Fbm3(sp, ScratchDetail));
}

// Пятна: крупные низкочастотные области (патина и маска царапин).
float PCBronze_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return PCBronze_Fbm3(pp, 2.0);
}

// LEGACY-шим (T-BRONZE01): исходная 14-параметровая сигнатура первой версии.
// Оставлен намеренно: старые сгенерированные варианты шейдера могут звать
// именно её из кэша. Новое развитие — только одно-выходные функции ниже.
void ProceduralBronzeNoise_float(
    float3 ObjectPos,
    float Scale, float Detail, float Warp, float Seed,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    float PatinaScale, float PatinaSeed,
    out float Noise, out float Scratch, out float Patina)
{
    Noise = PCBronze_Base(ObjectPos, Scale, Detail, Warp, Seed);
    Scratch = PCBronze_ScratchDir(ObjectPos, ScratchScale, ScratchStretch, 1.0, ScratchStretch, ScratchDetail, ScratchSeed);
    Patina = PCBronze_Blotch(ObjectPos, PatinaScale, PatinaSeed);
}

void ProceduralBronzeNoise_half(
    half3 ObjectPos,
    half Scale, half Detail, half Warp, half Seed,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    half PatinaScale, half PatinaSeed,
    out half Noise, out half Scratch, out half Patina)
{
    float n;
    float s;
    float t;
    ProceduralBronzeNoise_float(
        (float3)ObjectPos,
        (float)Scale, (float)Detail, (float)Warp, (float)Seed,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed,
        (float)PatinaScale, (float)PatinaSeed, n, s, t);
    Noise = (half)n;
    Scratch = (half)s;
    Patina = (half)t;
}

void ProceduralBronzeBase_float(
    float3 ObjectPos, float Scale, float Detail, float Warp, float Seed,
    out float Noise)
{
    Noise = PCBronze_Base(ObjectPos, Scale, Detail, Warp, Seed);
}

void ProceduralBronzeBase_half(
    half3 ObjectPos, half Scale, half Detail, half Warp, half Seed,
    out half Noise)
{
    float n;
    ProceduralBronzeBase_float(
        (float3)ObjectPos, (float)Scale, (float)Detail, (float)Warp, (float)Seed, n);
    Noise = (half)n;
}

void ProceduralBronzeScratchA_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float Scratch)
{
    Scratch = PCBronze_ScratchDir(ObjectPos, ScratchScale, ScratchStretch, 1.0, ScratchStretch, ScratchDetail, ScratchSeed);
}

void ProceduralBronzeScratchA_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half Scratch)
{
    float s;
    ProceduralBronzeScratchA_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, s);
    Scratch = (half)s;
}

void ProceduralBronzePatina_float(
    float3 ObjectPos, float PatinaScale, float PatinaSeed,
    out float Patina)
{
    Patina = PCBronze_Blotch(ObjectPos, PatinaScale, PatinaSeed);
}

void ProceduralBronzePatina_half(
    half3 ObjectPos, half PatinaScale, half PatinaSeed,
    out half Patina)
{
    float t;
    ProceduralBronzePatina_float((float3)ObjectPos, (float)PatinaScale, (float)PatinaSeed, t);
    Patina = (half)t;
}

void ProceduralBronzeScratchB_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float ScratchB)
{
    ScratchB = PCBronze_ScratchDir(ObjectPos, ScratchScale, 1.0, ScratchStretch, 1.0, ScratchDetail, ScratchSeed + 17.3);
}

void ProceduralBronzeScratchB_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half ScratchB)
{
    float b;
    ProceduralBronzeScratchB_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, b);
    ScratchB = (half)b;
}

void ProceduralBronzeScratchC_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    float ScratchAngle,
    out float ScratchC)
{
    float ca = cos(ScratchAngle);
    float sn = sin(ScratchAngle);
    float3 sc = ObjectPos * max(0.001, ScratchScale) + ScratchSeed + 41.1;
    sc.xy = float2(sc.x * ca - sc.y * sn, sc.x * sn + sc.y * ca);
    sc.x *= max(0.05, ScratchStretch);
    ScratchC = PCBronze_Ridge(PCBronze_Fbm3(sc, ScratchDetail));
}

void ProceduralBronzeScratchC_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    half ScratchAngle,
    out half ScratchC)
{
    float c;
    ProceduralBronzeScratchC_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed,
        (float)ScratchAngle, c);
    ScratchC = (half)c;
}

void ProceduralBronzeScratchMask_float(
    float3 ObjectPos,
    float ScratchMaskScale, float ScratchMaskSeed,
    out float ScratchMask)
{
    ScratchMask = PCBronze_Blotch(ObjectPos, ScratchMaskScale, ScratchMaskSeed);
}

void ProceduralBronzeScratchMask_half(
    half3 ObjectPos,
    half ScratchMaskScale, half ScratchMaskSeed,
    out half ScratchMask)
{
    float m;
    ProceduralBronzeScratchMask_float(
        (float3)ObjectPos,
        (float)ScratchMaskScale, (float)ScratchMaskSeed, m);
    ScratchMask = (half)m;
}

#endif
