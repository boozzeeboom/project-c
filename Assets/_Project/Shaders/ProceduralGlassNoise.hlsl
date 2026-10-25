#ifndef PROCEDURAL_GLASS_NOISE_INCLUDED
#define PROCEDURAL_GLASS_NOISE_INCLUDED

// ProceduralGlassNoise — object-space 3D noise for Glass.
// ПРАВИЛО (T-BRONZE01): только ОДНО-выходные функции File-режима.
// Мульти-выходные функции ломаются при перезаписи графа из MCP
// (слоты CF-ноды перегенерируются как N входов + 1 выход).
// Сигнатуры НЕ МЕНЯТЬ (слоты нод привязаны номерами слотов).
// Хеш без sin (Hoskins-style), warp гладкий (интерполированный).

float PCGlass_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// ── Полоса пропускания (25.10, по образцу Wood шаги 2n–2o) ─────────────────────────────
// Размер пикселя в периодах уже масштабированной координаты узора (Scale × Stretch учтены). Царапины
// стекла при пресете `_Scratch_Scale` 35.6 × `_Scratch_Stretch` 20 ≈ 700 периодов/м поперёк, а с `_Scratch_Detail` ~4
// верхняя октава ещё ×16 — всё это мельче пикселя и на глянцевом стекле (Smoothness 0.96) искрит и
// мерцает в цвете, блике и рельефе. Слой, чей период ≤ ~4 пикс, сводится к среднему, ≥ ~12 — целиком.
float PCGlass_Footprint(float3 p)
{
    return max(length(ddx(p)), length(ddy(p)));
}

#define PCGLASS_BAND_LO 0.08
#define PCGLASS_BAND_HI 0.25
float PCGlass_Band(float3 p)
{
    return 1.0 - smoothstep(PCGLASS_BAND_LO, PCGLASS_BAND_HI, PCGlass_Footprint(p));
}

float PCGlass_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCGlass_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCGlass_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCGlass_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCGlass_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCGlass_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCGlass_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCGlass_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCGlass_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCGlass_Fbm3(float3 p, float detail)
{
    // октава мельче пикселя плавно уходит в своё среднее (0.5)
    float px = PCGlass_Footprint(p);
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        float w = 1.0 - smoothstep(PCGLASS_BAND_LO, PCGLASS_BAND_HI, px * freq);
        sum += amp * lerp(0.5, PCGlass_ValueNoise3(p * freq), w);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

float PCGlass_Voronoi3(float3 p)
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
                    PCGlass_Hash13(cell + 0.0),
                    PCGlass_Hash13(cell + 17.0),
                    PCGlass_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return saturate(minD);
}

// Ridged-преобразование: тонкие линии там, где fbm пересекает 0.5.
// Скруглённый гребень (25.10): |x| даёт излом наклона на гребне — в рельефе и блике мерцающая
// линия; sqrt(x²+k) визуально тот же штрих, но гладкий.
float PCGlass_Ridge(float s)
{
    const float k = 0.0025;
    float x = s * 2.0 - 1.0;
    float a = sqrt(x * x + k) - sqrt(k);
    return 1.0 - a / (sqrt(1.0 + k) - sqrt(k));
}

// Тон: fbm + Worley из object-координат (FO-safe).
float PCGlass_Base(float3 ObjectPos, float Scale, float Detail, float Warp, float Seed)
{
    float3 p = ObjectPos * max(0.001, Scale) + Seed;
    float3 w = float3(
        PCGlass_ValueNoise3(p * 0.5 + 11.3),
        PCGlass_ValueNoise3(p * 0.5 + 27.1),
        PCGlass_ValueNoise3(p * 0.5 + 43.7)) - 0.5;
    p += w * Warp;
    float f = PCGlass_Fbm3(p, Detail);
    float v = PCGlass_Voronoi3(p);
    // Worley-ячейки и сильный warp: при неразрешённом узоре свести к среднему (≈ 0.5)
    return lerp(0.5, saturate(lerp(f, v, 0.30)), PCGlass_Band(p));
}

// Царапины A: растяжка X/Z -> полосы вдоль Y (вертикальные).
float PCGlass_ScratchDir(float3 ObjectPos, float ScratchScale, float SX, float SY, float SZ, float ScratchDetail, float ScratchSeed)
{
    float3 sp = ObjectPos * max(0.001, ScratchScale) + ScratchSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    // царапина уже пикселя → среднее 0.5 (ниже порога царапин в графе — линия плавно исчезает)
    return lerp(0.5, PCGlass_Ridge(PCGlass_Fbm3(sp, ScratchDetail)), PCGlass_Band(sp));
}

// Пятна: крупные низкочастотные области (патина и маска царапин).
float PCGlass_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return PCGlass_Fbm3(pp, 2.0);
}

void ProceduralGlassBase_float(
    float3 ObjectPos, float Scale, float Detail, float Warp, float Seed,
    out float Noise)
{
    Noise = PCGlass_Base(ObjectPos, Scale, Detail, Warp, Seed);
}

void ProceduralGlassBase_half(
    half3 ObjectPos, half Scale, half Detail, half Warp, half Seed,
    out half Noise)
{
    float n;
    ProceduralGlassBase_float(
        (float3)ObjectPos, (float)Scale, (float)Detail, (float)Warp, (float)Seed, n);
    Noise = (half)n;
}

void ProceduralGlassScratchA_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float Scratch)
{
    Scratch = PCGlass_ScratchDir(ObjectPos, ScratchScale, ScratchStretch, 1.0, ScratchStretch, ScratchDetail, ScratchSeed);
}

void ProceduralGlassScratchA_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half Scratch)
{
    float s;
    ProceduralGlassScratchA_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, s);
    Scratch = (half)s;
}

void ProceduralGlassMurk_float(
    float3 ObjectPos, float MurkScale, float MurkSeed,
    out float Murk)
{
    Murk = PCGlass_Blotch(ObjectPos, MurkScale, MurkSeed);
}

void ProceduralGlassMurk_half(
    half3 ObjectPos, half MurkScale, half MurkSeed,
    out half Murk)
{
    float t;
    ProceduralGlassMurk_float((float3)ObjectPos, (float)MurkScale, (float)MurkSeed, t);
    Murk = (half)t;
}

void ProceduralGlassScratchB_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float ScratchB)
{
    ScratchB = PCGlass_ScratchDir(ObjectPos, ScratchScale, 1.0, ScratchStretch, 1.0, ScratchDetail, ScratchSeed + 17.3);
}

void ProceduralGlassScratchB_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half ScratchB)
{
    float b;
    ProceduralGlassScratchB_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, b);
    ScratchB = (half)b;
}

void ProceduralGlassScratchC_float(
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
    ScratchC = lerp(0.5, PCGlass_Ridge(PCGlass_Fbm3(sc, ScratchDetail)), PCGlass_Band(sc));
}

void ProceduralGlassScratchC_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    half ScratchAngle,
    out half ScratchC)
{
    float c;
    ProceduralGlassScratchC_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed,
        (float)ScratchAngle, c);
    ScratchC = (half)c;
}

void ProceduralGlassScratchMask_float(
    float3 ObjectPos,
    float ScratchMaskScale, float ScratchMaskSeed,
    out float ScratchMask)
{
    ScratchMask = PCGlass_Blotch(ObjectPos, ScratchMaskScale, ScratchMaskSeed);
}

void ProceduralGlassScratchMask_half(
    half3 ObjectPos,
    half ScratchMaskScale, half ScratchMaskSeed,
    out half ScratchMask)
{
    float m;
    ProceduralGlassScratchMask_float(
        (float3)ObjectPos,
        (float)ScratchMaskScale, (float)ScratchMaskSeed, m);
    ScratchMask = (half)m;
}

#endif
