#ifndef PROCEDURAL_WOOD_NOISE_INCLUDED
#define PROCEDURAL_WOOD_NOISE_INCLUDED

// ProceduralWoodNoise — object-space 3D noise for Wood (дерево, не металл).
// ПРАВИЛО (T-BRONZE01): только ОДНО-выходные функции File-режима.
// Мульти-выходные функции ломаются при перезаписи графа из MCP.
// Сигнатуры = порядок слотов CF-нод в графе. Менять только вместе с графом.
//
// Слои дерева:
//   ProceduralWoodKnots  — сучки/свиль: близость к центру сучка (0..1)
//   ProceduralWoodRings  — годичные кольца: поздняя древесина (узкая тёмная полоса)
//   ProceduralWoodGrain  — волокно: штрихи вдоль локальной оси Y
//   ProceduralWoodPores  — поры: редкие высокочастотные точки
//   ProceduralWoodScratchA/B/C + ScratchMask — царапины (заимствовано у бронзы,
//                                                механизм не изменён)
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe).

float PCWood_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCWood_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCWood_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCWood_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCWood_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCWood_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCWood_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCWood_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCWood_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCWood_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCWood_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCWood_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Ridged-преобразование: тонкие линии там, где fbm пересекает 0.5.
float PCWood_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

// Worley F1 (3x3x3) — ячейки для сучков и пор.
float PCWood_Worley(float3 p)
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
                    PCWood_Hash13(cell + 0.0),
                    PCWood_Hash13(cell + 17.0),
                    PCWood_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return minD;
}

// ── Сучки ────────────────────────────────────────────────────────────────────
// Возвращает величину «сдвига колец»: 0 вдали от сучка, KnotWarp у его центра.
// Входа: ObjectPos, Scale, Warp, Seed.
float PCWood_Knots(float3 ObjectPos, float KnotScale, float KnotWarp, float KnotSeed)
{
    float3 p = ObjectPos * max(0.001, KnotScale) + KnotSeed;
    float d = PCWood_Worley(p);
    float k = saturate(1.0 - d * 1.35);
    return k * k * max(0.0, KnotWarp);
}

// ── Годичные кольца ──────────────────────────────────────────────────────────
// r = радиус в сечении XZ (ось ствола — локальная Y), сдвигается свилью и сучками.
// Поздняя древесина = узкая полоса в конце периода кольца (RingSharpness).
float PCWood_Rings(float3 ObjectPos, float RingScale, float RingSharpness, float WarpAmount, float Knots, float RingSeed)
{
    float3 p = ObjectPos * max(0.001, RingScale) + RingSeed;
    float2 cs = p.xz;
    float r = length(cs);
    float sway = PCWood_Fbm3(float3(cs.x * 1.7, p.y * 0.22, cs.y * 1.7), 2.0) - 0.5;
    r += sway * max(0.0, WarpAmount);
    r += Knots;
    float t = frac(r);
    float sharp = clamp(RingSharpness, 0.01, 1.0);
    return saturate(smoothstep(1.0 - sharp, 1.0, t));
}

// ── Волокно ──────────────────────────────────────────────────────────────────
// fbm, сильно растянутый вдоль локальной Y -> тонкие штрихи вдоль волокна.
float PCWood_Grain(float3 ObjectPos, float GrainScale, float GrainStretch, float GrainDetail, float GrainSeed)
{
    float3 p = ObjectPos * max(0.001, GrainScale) + GrainSeed;
    float3 g = float3(p.x * max(0.05, GrainStretch), p.y * 0.12, p.z * max(0.05, GrainStretch));
    return saturate(PCWood_Ridge(PCWood_Fbm3(g, max(1.1, GrainDetail))));
}

// ── Поры ─────────────────────────────────────────────────────────────────────
// Редкие точки в центрах ячеек Worley (дуб/ясень).
float PCWood_Pores(float3 ObjectPos, float PoresScale, float PoresSeed)
{
    float3 p = ObjectPos * max(0.001, PoresScale) + PoresSeed;
    float d = PCWood_Worley(p);
    return saturate(1.0 - smoothstep(0.18, 0.42, d));
}

void ProceduralWoodKnots_float(
    float3 ObjectPos, float KnotScale, float KnotWarp, float KnotSeed,
    out float Knots)
{
    Knots = PCWood_Knots(ObjectPos, KnotScale, KnotWarp, KnotSeed);
}

void ProceduralWoodKnots_half(
    half3 ObjectPos, half KnotScale, half KnotWarp, half KnotSeed,
    out half Knots)
{
    float k;
    ProceduralWoodKnots_float(
        (float3)ObjectPos, (float)KnotScale, (float)KnotWarp, (float)KnotSeed, k);
    Knots = (half)k;
}

void ProceduralWoodRings_float(
    float3 ObjectPos, float RingScale, float RingSharpness, float WarpAmount, float Knots, float RingSeed,
    out float Rings)
{
    Rings = PCWood_Rings(ObjectPos, RingScale, RingSharpness, WarpAmount, Knots, RingSeed);
}

void ProceduralWoodRings_half(
    half3 ObjectPos, half RingScale, half RingSharpness, half WarpAmount, half Knots, half RingSeed,
    out half Rings)
{
    float r;
    ProceduralWoodRings_float(
        (float3)ObjectPos, (float)RingScale, (float)RingSharpness, (float)WarpAmount, (float)Knots, (float)RingSeed, r);
    Rings = (half)r;
}

void ProceduralWoodGrain_float(
    float3 ObjectPos, float GrainScale, float GrainStretch, float GrainDetail, float GrainSeed,
    out float Grain)
{
    Grain = PCWood_Grain(ObjectPos, GrainScale, GrainStretch, GrainDetail, GrainSeed);
}

void ProceduralWoodGrain_half(
    half3 ObjectPos, half GrainScale, half GrainStretch, half GrainDetail, half GrainSeed,
    out half Grain)
{
    float g;
    ProceduralWoodGrain_float(
        (float3)ObjectPos, (float)GrainScale, (float)GrainStretch, (float)GrainDetail, (float)GrainSeed, g);
    Grain = (half)g;
}

void ProceduralWoodPores_float(
    float3 ObjectPos, float PoresScale, float PoresSeed,
    out float Pores)
{
    Pores = PCWood_Pores(ObjectPos, PoresScale, PoresSeed);
}

void ProceduralWoodPores_half(
    half3 ObjectPos, half PoresScale, half PoresSeed,
    out half Pores)
{
    float p;
    ProceduralWoodPores_float((float3)ObjectPos, (float)PoresScale, (float)PoresSeed, p);
    Pores = (half)p;
}

// ── Царапины (заимствовано у бронзы, механизм 1:1) ───────────────────────────
float PCWood_ScratchDir(float3 ObjectPos, float ScratchScale, float SX, float SY, float SZ, float ScratchDetail, float ScratchSeed)
{
    float3 sp = ObjectPos * max(0.001, ScratchScale) + ScratchSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    return PCWood_Ridge(PCWood_Fbm3(sp, ScratchDetail));
}

float PCWood_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return PCWood_Fbm3(pp, 2.0);
}

void ProceduralWoodScratchA_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float Scratch)
{
    Scratch = PCWood_ScratchDir(ObjectPos, ScratchScale, ScratchStretch, 1.0, ScratchStretch, ScratchDetail, ScratchSeed);
}

void ProceduralWoodScratchA_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half Scratch)
{
    float s;
    ProceduralWoodScratchA_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, s);
    Scratch = (half)s;
}

void ProceduralWoodScratchB_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float ScratchB)
{
    ScratchB = PCWood_ScratchDir(ObjectPos, ScratchScale, 1.0, ScratchStretch, 1.0, ScratchDetail, ScratchSeed + 17.3);
}

void ProceduralWoodScratchB_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half ScratchB)
{
    float b;
    ProceduralWoodScratchB_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, b);
    ScratchB = (half)b;
}

void ProceduralWoodScratchC_float(
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
    ScratchC = PCWood_Ridge(PCWood_Fbm3(sc, ScratchDetail));
}

void ProceduralWoodScratchC_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    half ScratchAngle,
    out half ScratchC)
{
    float c;
    ProceduralWoodScratchC_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed,
        (float)ScratchAngle, c);
    ScratchC = (half)c;
}

void ProceduralWoodScratchMask_float(
    float3 ObjectPos,
    float ScratchMaskScale, float ScratchMaskSeed,
    out float ScratchMask)
{
    ScratchMask = PCWood_Blotch(ObjectPos, ScratchMaskScale, ScratchMaskSeed);
}

void ProceduralWoodScratchMask_half(
    half3 ObjectPos,
    half ScratchMaskScale, half ScratchMaskSeed,
    out half ScratchMask)
{
    float m;
    ProceduralWoodScratchMask_float(
        (float3)ObjectPos,
        (float)ScratchMaskScale, (float)ScratchMaskSeed, m);
    ScratchMask = (half)m;
}

#endif
