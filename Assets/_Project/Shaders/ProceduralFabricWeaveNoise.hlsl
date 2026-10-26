#ifndef PROCEDURAL_FABRIC_WEAVE_NOISE_INCLUDED
#define PROCEDURAL_FABRIC_WEAVE_NOISE_INCLUDED

// ProceduralFabricWeaveNoise — object-space процедурная ткань для графа
// Assets/_Project/Materials/Fabric/Fabric.shadergraph (M_PC_Fabric / SMALL_M_PC_Fabric).
// НЕ путать с ProceduralFabricNoise.hlsl (отдельный граф Fabric_v2, свои функции).
//
// ПРАВИЛО (T-BRONZE01): только ОДНО-выходные функции File-режима.
// Сигнатуры = порядок слотов CF-нод в графе Fabric.shadergraph. Менять только вместе с графом;
// новое поведение — новой функцией, существующие сигнатуры не трогать.
//
// Слои ткани:
//   ProceduralFabricSlub        — неравномерность пряжи (утолщения нити), знаковая ±SlubAmount
//   ProceduralFabricPlainWeave  — полотняное переплетение 1/1: маска просветов/подныривания нитей
//                                 (1 = просвет между нитями, 0 = вершина нити)
//   ProceduralFabricFuzz        — меланж/ворс: волокнистая вариация тона вдоль нитей
//   ProceduralFabricPilling     — катышки: редкие мелкие точки
//   ProceduralFabricSnagA/B/C + SnagMask — затяжки (механизм царапин бронзы/дерева, 1:1)
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe).
// ddx/ddy позиции используется в PlainWeave (ориентация грани + анти-муар) и в полосе
// всех шумовых слоёв (анти-муар) — только fragment-стадия.

#define PCFABW_PI  3.14159265
#define PCFABW_TAU 6.28318531

float PCFabW_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCFabW_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCFabW_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCFabW_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCFabW_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCFabW_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCFabW_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCFabW_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCFabW_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCFabW_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

// ── Полоса (анти-муар) ───────────────────────────────────────────────────────
// Порог 0.22 / 0.55 периода на пиксель — стандарт проекта (PCCobble_Band).
// Полоса считается от ТЕХ ЖЕ координат, что получает шум: у SnagA/B/C они уже
// анизотропно растянуты по осям, и брать сырой ObjectPos значило бы считать
// полосу от другого рисунка. ddx/ddy — только fragment (PlainWeave уже так живёт).
#define PCFABW_BAND_LO 0.22
#define PCFABW_BAND_HI 0.55

float PCFabW_Footprint(float3 p)
{
    return max(length(ddx(p)), length(ddy(p)));
}

float PCFabW_Band(float3 p)
{
    return 1.0 - smoothstep(PCFABW_BAND_LO, PCFABW_BAND_HI, PCFabW_Footprint(p));
}

float PCFabW_Fbm3(float3 p, float detail)
{
    float px = PCFabW_Footprint(p);

    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        // Гаснет именно неразрешимая октава, а не вся сумма: у каждой свой
        // множитель частоты и, значит, своя полоса в пиксельном масштабе.
        // Среднее шума — 0.5, поэтому lerp именно к нему.
        float w = 1.0 - smoothstep(PCFABW_BAND_LO, PCFABW_BAND_HI, px * freq);
        sum += amp * lerp(0.5, PCFabW_ValueNoise3(p * freq), w);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Скруглённый хребет. У 1−|2s−1| производная меняет знак ровно на гребне, и
// ddx там даёт ступеньку — вблизи это мерцающий пунктир по затяжке.
// k = 0.0025: дно 0.05, на глаз не отличимо от нуля, но производная непрерывна.
float PCFabW_Ridge(float s)
{
    float x = s * 2.0 - 1.0;
    return saturate(1.0 - sqrt(x * x + 0.0025));
}

// Круглое сечение нити: 1 на оси нити (t = 0.5), 0 на её краю (|t − 0.5| = hw).
float PCFabW_ThreadProfile(float t, float hw)
{
    float d = (t - 0.5) / hw;
    return sqrt(saturate(1.0 - d * d));
}

// ── Неравномерность пряжи ────────────────────────────────────────────────────
float PCFabW_Slub(float3 ObjectPos, float SlubScale, float SlubAmount, float SlubSeed)
{
    float3 p = ObjectPos * max(0.001, SlubScale) + SlubSeed;
    return (PCFabW_Fbm3(p, 2.3) - 0.5) * 2.0 * max(0.0, SlubAmount);
}

// ── Переплетение (полотняное, 1/1) ───────────────────────────────────────────
// Плоскость рисунка выбирается по ориентации грани в object-space (плоская нормаль
// из производных позиции): грань ⊥X -> (Z,Y), грань ⊥Z -> (X,Y), грань ⊥Y -> (X,Z).
// На стенах основа (warp) идёт по локальной Y, уток (weft) — горизонтально.
float PCFabW_PlainWeave(float3 ObjectPos, float WeaveScale, float WeaveGap, float WeaveWobble, float Slub, float WeaveSeed)
{
    float3 p = ObjectPos * max(0.001, WeaveScale);

    float3 dpx = ddx(p);
    float3 dpy = ddy(p);
    float3 n = abs(cross(dpy, dpx));
    float2 uv = (n.x >= n.y && n.x >= n.z) ? p.zy : ((n.z >= n.y) ? p.xy : p.xz);

    // Ширина пикселя в шагах нити: рисунок гасится к среднему, пока не начался муар.
    float fw = max(length(dpx), length(dpy));
    float fade = saturate(2.0 - fw * 3.0);

    uv += WeaveSeed * 7.31;
    float2 wob = float2(
        PCFabW_ValueNoise3(float3(uv * 0.07, WeaveSeed)),
        PCFabW_ValueNoise3(float3(uv.yx * 0.07, WeaveSeed + 5.3))) - 0.5;
    uv += wob * max(0.0, WeaveWobble) * 2.0;

    float2 cell = floor(uv);
    float2 f = frac(uv);
    float over = step(0.5, frac((cell.x + cell.y) * 0.5)); // 1 = основа сверху

    float hw = clamp(0.5 * (1.0 - clamp(WeaveGap, 0.0, 0.9)) * (1.0 + Slub * 0.5), 0.05, 0.5);

    float sx = sin(PCFABW_PI * f.x);
    float sy = sin(PCFABW_PI * f.y);
    float warpLift = lerp(0.55 - 0.45 * sy, 0.55 + 0.45 * sy, over);
    float weftLift = lerp(0.55 + 0.45 * sx, 0.55 - 0.45 * sx, over);

    // Крутка нити — мелкие косые валики вдоль нити.
    float twistW = 0.9 + 0.1 * sin(PCFABW_TAU * (f.y * 3.0 + f.x));
    float twistF = 0.9 + 0.1 * sin(PCFABW_TAU * (f.x * 3.0 + f.y));

    float warpH = PCFabW_ThreadProfile(f.x, hw) * warpLift * twistW;
    float weftH = PCFabW_ThreadProfile(f.y, hw) * weftLift * twistF;
    float gap = saturate(1.0 - max(warpH, weftH));
    return lerp(0.45, gap, fade);
}

// ── Меланж / ворс ────────────────────────────────────────────────────────────
// Два набора волокон: вдоль локальной Y и горизонтальный (XZ), FuzzStretch — анизотропия.
float PCFabW_Fuzz(float3 ObjectPos, float FuzzScale, float FuzzStretch, float FuzzDetail, float FuzzSeed)
{
    float3 p = ObjectPos * max(0.001, FuzzScale) + FuzzSeed;
    float s = max(1.0, FuzzStretch);
    float d = max(1.1, FuzzDetail);
    float a = PCFabW_Fbm3(float3(p.x * s, p.y, p.z * s), d);
    float b = PCFabW_Fbm3(float3(p.x, p.y * s, p.z) + 13.7, d);
    return saturate(smoothstep(0.3, 0.7, 0.5 * (a + b)));
}

// ── Катышки ──────────────────────────────────────────────────────────────────
// Worley F1 по разреженным ячейкам (≈35% ячеек содержат катышек).
float PCFabW_Pilling(float3 ObjectPos, float PillingScale, float PillingSeed)
{
    float3 p = ObjectPos * max(0.001, PillingScale) + PillingSeed;
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
                    PCFabW_Hash13(cell + 0.0),
                    PCFabW_Hash13(cell + 17.0),
                    PCFabW_Hash13(cell + 31.0));
                float empty = step(0.35, PCFabW_Hash13(cell + 53.7)) * 8.0;
                minD = min(minD, length(cell + rnd - p) + empty);
            }
        }
    }
    // Катышки — редкие мелкие точки: измеренное среднее 0.0096, вдали гаснут в ноль.
    return saturate(1.0 - smoothstep(0.10, 0.26, minD)) * PCFabW_Band(p);
}

void ProceduralFabricSlub_float(
    float3 ObjectPos, float SlubScale, float SlubAmount, float SlubSeed,
    out float Slub)
{
    Slub = PCFabW_Slub(ObjectPos, SlubScale, SlubAmount, SlubSeed);
}

void ProceduralFabricSlub_half(
    half3 ObjectPos, half SlubScale, half SlubAmount, half SlubSeed,
    out half Slub)
{
    float s;
    ProceduralFabricSlub_float(
        (float3)ObjectPos, (float)SlubScale, (float)SlubAmount, (float)SlubSeed, s);
    Slub = (half)s;
}

void ProceduralFabricPlainWeave_float(
    float3 ObjectPos, float WeaveScale, float WeaveGap, float WeaveWobble, float Slub, float WeaveSeed,
    out float Weave)
{
    Weave = PCFabW_PlainWeave(ObjectPos, WeaveScale, WeaveGap, WeaveWobble, Slub, WeaveSeed);
}

void ProceduralFabricPlainWeave_half(
    half3 ObjectPos, half WeaveScale, half WeaveGap, half WeaveWobble, half Slub, half WeaveSeed,
    out half Weave)
{
    float w;
    ProceduralFabricPlainWeave_float(
        (float3)ObjectPos, (float)WeaveScale, (float)WeaveGap, (float)WeaveWobble, (float)Slub, (float)WeaveSeed, w);
    Weave = (half)w;
}

void ProceduralFabricFuzz_float(
    float3 ObjectPos, float FuzzScale, float FuzzStretch, float FuzzDetail, float FuzzSeed,
    out float Fuzz)
{
    Fuzz = PCFabW_Fuzz(ObjectPos, FuzzScale, FuzzStretch, FuzzDetail, FuzzSeed);
}

void ProceduralFabricFuzz_half(
    half3 ObjectPos, half FuzzScale, half FuzzStretch, half FuzzDetail, half FuzzSeed,
    out half Fuzz)
{
    float z;
    ProceduralFabricFuzz_float(
        (float3)ObjectPos, (float)FuzzScale, (float)FuzzStretch, (float)FuzzDetail, (float)FuzzSeed, z);
    Fuzz = (half)z;
}

void ProceduralFabricPilling_float(
    float3 ObjectPos, float PillingScale, float PillingSeed,
    out float Pilling)
{
    Pilling = PCFabW_Pilling(ObjectPos, PillingScale, PillingSeed);
}

void ProceduralFabricPilling_half(
    half3 ObjectPos, half PillingScale, half PillingSeed,
    out half Pilling)
{
    float p;
    ProceduralFabricPilling_float((float3)ObjectPos, (float)PillingScale, (float)PillingSeed, p);
    Pilling = (half)p;
}

// ── Затяжки (механизм царапин бронзы/дерева, 1:1) ───────────────────────────
float PCFabW_SnagDir(float3 ObjectPos, float SnagScale, float SX, float SY, float SZ, float SnagDetail, float SnagSeed)
{
    float3 sp = ObjectPos * max(0.001, SnagScale) + SnagSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    // Хребет затяжки вдали уходит в своё среднее 0.5: маска «1 − хребет» тогда
    // читается как «затяжки нет», а не как разлитая царапина по всей ткани.
    // Гасить в ноль нельзя — 0 это как раз гребень, то есть сплошная затяжка.
    return lerp(0.5, PCFabW_Ridge(PCFabW_Fbm3(sp, SnagDetail)), PCFabW_Band(sp));
}

float PCFabW_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return PCFabW_Fbm3(pp, 2.0);
}

void ProceduralFabricSnagA_float(
    float3 ObjectPos,
    float SnagScale, float SnagStretch, float SnagDetail, float SnagSeed,
    out float Snag)
{
    Snag = PCFabW_SnagDir(ObjectPos, SnagScale, SnagStretch, 1.0, SnagStretch, SnagDetail, SnagSeed);
}

void ProceduralFabricSnagA_half(
    half3 ObjectPos,
    half SnagScale, half SnagStretch, half SnagDetail, half SnagSeed,
    out half Snag)
{
    float s;
    ProceduralFabricSnagA_float(
        (float3)ObjectPos,
        (float)SnagScale, (float)SnagStretch, (float)SnagDetail, (float)SnagSeed, s);
    Snag = (half)s;
}

void ProceduralFabricSnagB_float(
    float3 ObjectPos,
    float SnagScale, float SnagStretch, float SnagDetail, float SnagSeed,
    out float SnagB)
{
    SnagB = PCFabW_SnagDir(ObjectPos, SnagScale, 1.0, SnagStretch, 1.0, SnagDetail, SnagSeed + 17.3);
}

void ProceduralFabricSnagB_half(
    half3 ObjectPos,
    half SnagScale, half SnagStretch, half SnagDetail, half SnagSeed,
    out half SnagB)
{
    float b;
    ProceduralFabricSnagB_float(
        (float3)ObjectPos,
        (float)SnagScale, (float)SnagStretch, (float)SnagDetail, (float)SnagSeed, b);
    SnagB = (half)b;
}

void ProceduralFabricSnagC_float(
    float3 ObjectPos,
    float SnagScale, float SnagStretch, float SnagDetail, float SnagSeed,
    float SnagAngle,
    out float SnagC)
{
    float ca = cos(SnagAngle);
    float sn = sin(SnagAngle);
    float3 sc = ObjectPos * max(0.001, SnagScale) + SnagSeed + 41.1;
    sc.xy = float2(sc.x * ca - sc.y * sn, sc.x * sn + sc.y * ca);
    sc.x *= max(0.05, SnagStretch);
    SnagC = lerp(0.5, PCFabW_Ridge(PCFabW_Fbm3(sc, SnagDetail)), PCFabW_Band(sc));
}

void ProceduralFabricSnagC_half(
    half3 ObjectPos,
    half SnagScale, half SnagStretch, half SnagDetail, half SnagSeed,
    half SnagAngle,
    out half SnagC)
{
    float c;
    ProceduralFabricSnagC_float(
        (float3)ObjectPos,
        (float)SnagScale, (float)SnagStretch, (float)SnagDetail, (float)SnagSeed,
        (float)SnagAngle, c);
    SnagC = (half)c;
}

void ProceduralFabricSnagMask_float(
    float3 ObjectPos,
    float SnagMaskScale, float SnagMaskSeed,
    out float SnagMask)
{
    SnagMask = PCFabW_Blotch(ObjectPos, SnagMaskScale, SnagMaskSeed);
}

void ProceduralFabricSnagMask_half(
    half3 ObjectPos,
    half SnagMaskScale, half SnagMaskSeed,
    out half SnagMask)
{
    float m;
    ProceduralFabricSnagMask_float(
        (float3)ObjectPos,
        (float)SnagMaskScale, (float)SnagMaskSeed, m);
    SnagMask = (half)m;
}

#endif
