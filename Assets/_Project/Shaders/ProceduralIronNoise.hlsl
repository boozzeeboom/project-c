#ifndef PROCEDURAL_IRON_NOISE_INCLUDED
#define PROCEDURAL_IRON_NOISE_INCLUDED

// ProceduralIronNoise — object-space 3D noise for Iron + Rust.
// Копия ProceduralBronzeNoise.hlsl (правило T-BRONZE01): механизм тот же,
// патина заменена ржавчиной (Patina → Rust, Bronze → Iron).
// ПРАВИЛО: только ОДНО-выходные функции File-режима.
// Мульти-выходные функции ломаются при перезаписи графа из MCP
// (слоты CF-ноды перегенерируются как N входов + 1 выход).
// Сигнатуры НЕ МЕНЯТЬ (слоты нод привязаны номерами слотов).
// Хеш без sin (Hoskins-style), warp гладкий (интерполированный).
// Только object-space → FO-safe (после F8/F9 паттерн стоит на месте).

float PCIron_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCIron_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCIron_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCIron_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCIron_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCIron_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCIron_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCIron_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCIron_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCIron_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCIron_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCIron_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

float PCIron_Voronoi3(float3 p)
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
                    PCIron_Hash13(cell + 0.0),
                    PCIron_Hash13(cell + 17.0),
                    PCIron_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return saturate(minD);
}

// Ridged-преобразование: тонкие линии там, где fbm пересекает 0.5.
float PCIron_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

// Тон: fbm + Worley из object-координат (FO-safe).
float PCIron_Base(float3 ObjectPos, float Scale, float Detail, float Warp, float Seed)
{
    float3 p = ObjectPos * max(0.001, Scale) + Seed;
    float3 w = float3(
        PCIron_ValueNoise3(p * 0.5 + 11.3),
        PCIron_ValueNoise3(p * 0.5 + 27.1),
        PCIron_ValueNoise3(p * 0.5 + 43.7)) - 0.5;
    p += w * Warp;
    float f = PCIron_Fbm3(p, Detail);
    float v = PCIron_Voronoi3(p);
    return saturate(lerp(f, v, 0.30));
}

// Царапины: растяжка по осям -> полосы (направление задаётся SX/SY/SZ).
float PCIron_ScratchDir(float3 ObjectPos, float ScratchScale, float SX, float SY, float SZ, float ScratchDetail, float ScratchSeed)
{
    float3 sp = ObjectPos * max(0.001, ScratchScale) + ScratchSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    return PCIron_Ridge(PCIron_Fbm3(sp, ScratchDetail));
}

// Пятна: крупные низкочастотные области (ржавчина и маска царапин).
float PCIron_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return PCIron_Fbm3(pp, 2.0);
}

void ProceduralIronBase_float(
    float3 ObjectPos, float Scale, float Detail, float Warp, float Seed,
    out float Noise)
{
    Noise = PCIron_Base(ObjectPos, Scale, Detail, Warp, Seed);
}

void ProceduralIronBase_half(
    half3 ObjectPos, half Scale, half Detail, half Warp, half Seed,
    out half Noise)
{
    float n;
    ProceduralIronBase_float(
        (float3)ObjectPos, (float)Scale, (float)Detail, (float)Warp, (float)Seed, n);
    Noise = (half)n;
}

void ProceduralIronScratchA_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float Scratch)
{
    Scratch = PCIron_ScratchDir(ObjectPos, ScratchScale, ScratchStretch, 1.0, ScratchStretch, ScratchDetail, ScratchSeed);
}

void ProceduralIronScratchA_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half Scratch)
{
    float s;
    ProceduralIronScratchA_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, s);
    Scratch = (half)s;
}

void ProceduralIronRust_float(
    float3 ObjectPos, float RustScale, float RustSeed,
    out float Rust)
{
    Rust = PCIron_Blotch(ObjectPos, RustScale, RustSeed);
}

void ProceduralIronRust_half(
    half3 ObjectPos, half RustScale, half RustSeed,
    out half Rust)
{
    float t;
    ProceduralIronRust_float((float3)ObjectPos, (float)RustScale, (float)RustSeed, t);
    Rust = (half)t;
}

void ProceduralIronScratchB_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float ScratchB)
{
    ScratchB = PCIron_ScratchDir(ObjectPos, ScratchScale, 1.0, ScratchStretch, 1.0, ScratchDetail, ScratchSeed + 17.3);
}

void ProceduralIronScratchB_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half ScratchB)
{
    float b;
    ProceduralIronScratchB_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, b);
    ScratchB = (half)b;
}

void ProceduralIronScratchC_float(
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
    ScratchC = PCIron_Ridge(PCIron_Fbm3(sc, ScratchDetail));
}

void ProceduralIronScratchC_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    half ScratchAngle,
    out half ScratchC)
{
    float c;
    ProceduralIronScratchC_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed,
        (float)ScratchAngle, c);
    ScratchC = (half)c;
}

void ProceduralIronScratchMask_float(
    float3 ObjectPos,
    float ScratchMaskScale, float ScratchMaskSeed,
    out float ScratchMask)
{
    ScratchMask = PCIron_Blotch(ObjectPos, ScratchMaskScale, ScratchMaskSeed);
}

void ProceduralIronScratchMask_half(
    half3 ObjectPos,
    half ScratchMaskScale, half ScratchMaskSeed,
    out half ScratchMask)
{
    float m;
    ProceduralIronScratchMask_float(
        (float3)ObjectPos,
        (float)ScratchMaskScale, (float)ScratchMaskSeed, m);
    ScratchMask = (half)m;
}

// Ржавчина с управляемой детализацией fbm.
// ProceduralIronRust жёстко использовал PCIron_Blotch (detail = 2.0) — пятна были
// только сглаженные. Здесь detail вынесен в параметр (RustDetail).
// НОВАЯ функция (не замена): сигнатура ProceduralIronRust не изменена (T-BRONZE01).
void ProceduralIronRustD_float(
    float3 ObjectPos, float RustScale, float RustSeed, float RustDetail,
    out float Rust)
{
    float3 pp = ObjectPos * max(0.001, RustScale) + RustSeed;
    Rust = PCIron_Fbm3(pp, RustDetail);
}

void ProceduralIronRustD_half(
    half3 ObjectPos, half RustScale, half RustSeed, half RustDetail,
    out half Rust)
{
    float t;
    ProceduralIronRustD_float(
        (float3)ObjectPos, (float)RustScale, (float)RustSeed, (float)RustDetail, t);
    Rust = (half)t;
}

// Глобальная маска пятен ржавчины: крупный низкочастотный блотч со своими Scale/Seed.
// Накатывается поверх уже настроенной ржавчины, задавая пятнистость яркого слоя.
void ProceduralIronRustPatch_float(
    float3 ObjectPos, float PatchScale, float PatchSeed,
    out float Patch)
{
    Patch = PCIron_Blotch(ObjectPos, PatchScale, PatchSeed);
}

void ProceduralIronRustPatch_half(
    half3 ObjectPos, half PatchScale, half PatchSeed,
    out half Patch)
{
    float p;
    ProceduralIronRustPatch_float(
        (float3)ObjectPos, (float)PatchScale, (float)PatchSeed, p);
    Patch = (half)p;
}

#endif
