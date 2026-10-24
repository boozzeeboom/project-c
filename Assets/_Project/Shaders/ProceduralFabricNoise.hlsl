#ifndef PROCEDURAL_FABRIC_NOISE_INCLUDED
#define PROCEDURAL_FABRIC_NOISE_INCLUDED

// ProceduralFabricNoise — object-space procedural fabric (ткань) for Fabric_v2.
// ПРАВИЛО (T-BRONZE01): только ОДНО-выходные функции File-режима.
// Мульти-выходные функции ломаются при перезаписи графа из MCP.
// Сигнатуры = порядок слотов CF-нод в графе Fabric_v2 (ObjectPos, ObjectNormal, ...).
// Менять сигнатуры только вместе с графом.
//
// Слои ткани:
//   ProceduralFabricSlubs  — слабы/утолщения пряжи (близость к центру ячейки)
//   ProceduralFabricWeave  — плетение: трипланарные нити warp/weft + перекрытие over/under
//   ProceduralFabricFibers — волокна: тонкие штрихи вдоль нити
//   ProceduralFabricNeps   — неps/катышки: редкие высокочастотные точки
//   ProceduralFabricFrayA/B/C + FrayMask — потёртости/бахрома (механизм как у царапин
//                                          дерева/бронзы, но со своей частотой)
//
// Всё считается из object-space координат (FO-safe), без текстур и UV.
// Нити ткани выбираются трипланарно по object-space нормали, чтобы плетение
// читалось на любой ориентации поверхности.

float PCFab_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCFab_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCFab_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCFab_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCFab_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCFab_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCFab_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCFab_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCFab_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCFab_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCFab_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCFab_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Ridged-преобразование: тонкие линии там, где fbm пересекает 0.5.
float PCFab_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

// Worley F1 (3x3x3) — ячейки для слабов и непсов.
float PCFab_Worley(float3 p)
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
                    PCFab_Hash13(cell + 0.0),
                    PCFab_Hash13(cell + 17.0),
                    PCFab_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return minD;
}

// ── Трипланарные веса ────────────────────────────────────────────────────────
// abs(n)^8: одна плоскость доминирует, поэтому нити не «замыливаются» на стыках.
float3 PCFab_TriWeights(float3 n)
{
    float3 w = abs(normalize(n + float3(1e-5, 1e-5, 1e-5)));
    w = pow(w, 8.0);
    return w / max(1e-4, w.x + w.y + w.z);
}

// Размер одного пикселя в object-space, в шагах узора: >1 — узор мельче пикселя.
// Нужен, чтобы дальние планы не «искрили» процедурной рябью (аналог mip-перехода).
float PCFab_Footprint(float3 ObjectPos, float scale)
{
    float px = max(length(ddx(ObjectPos)), length(ddy(ObjectPos)));
    return px * max(0.001, scale);
}

// ── Нить ─────────────────────────────────────────────────────────────────────
// 1 в середине нити, 0 в просвете. sharp = доля просвета по ширине шага.
float PCFab_ThreadBand(float x, float sharp)
{
    float d = abs(frac(x) - 0.5) * 2.0;
    return 1.0 - smoothstep(1.0 - clamp(sharp, 0.02, 0.95), 1.0, d);
}

// ── Плетение в одной плоскости ───────────────────────────────────────────────
// Две семьи нитей (warp вдоль U, weft вдоль V) + шахматное перекрытие over/under.
float PCFab_WeavePlane(float3 p, float scale, float sharp, float seed, float wobble)
{
    float2 q = p.xy * max(0.001, scale) + seed;

    // низкочастотный увод нитей (слабы пряжи / усадка ткани)
    float2 drift;
    drift.x = PCFab_Fbm3(float3(q * 0.11, seed + 3.1), 2.0) - 0.5;
    drift.y = PCFab_Fbm3(float3(q * 0.11 + 23.7, seed + 9.4), 2.0) - 0.5;
    q += clamp(drift * (wobble * 1.8), -0.35, 0.35);

    float warpB = PCFab_ThreadBand(q.y, sharp); // нить вдоль U
    float weftB = PCFab_ThreadBand(q.x, sharp); // нить вдоль V

    float2 cell = floor(q);
    float parity = frac((cell.x + cell.y) * 0.5) * 2.0; // 0/1 — кто сверху

    // Непрерывные гребни нитей + чередующиеся «примятости» на перекрёстках:
    // так перекрытие over/under читается, но нить не рвётся на клетки.
    float overT = lerp(warpB, weftB, parity);
    float underT = lerp(weftB, warpB, parity);
    float weave = max(overT, underT * 0.55);

    // косая крутка внутри нити (диагональные штрихи пряжи)
    float twist = 0.5 + 0.5 * sin((q.x + q.y) * 6.2831853);
    weave *= lerp(1.0, 0.94 + 0.06 * twist, saturate(overT + underT));

    return saturate(weave);
}

// ── Слабы пряжи ──────────────────────────────────────────────────────────────
// 0 вдали от утолщения, SlubWarp в его центре. Идёт в граф как «Knots»-аналог.
float PC_FabricSlubs(float3 ObjectPos, float SlubScale, float SlubWarp, float SlubSeed)
{
    float3 p = ObjectPos * max(0.001, SlubScale) + SlubSeed;
    float d = PCFab_Worley(p);
    float k = saturate(1.0 - d * 1.35);
    return k * k * max(0.0, SlubWarp);
}

// ── Плетение ─────────────────────────────────────────────────────────────────
float PC_FabricWeave(float3 ObjectPos, float3 ObjectNormal, float WeaveScale, float WeaveSharpness, float WeaveWarp, float Slub, float WeaveSeed)
{
    float3 w = PCFab_TriWeights(ObjectNormal);
    float wob = max(0.0, WeaveWarp) * (0.35 + saturate(Slub));

    float mx = PCFab_WeavePlane(ObjectPos.yzx, WeaveScale, WeaveSharpness, WeaveSeed, wob);
    float my = PCFab_WeavePlane(ObjectPos.xzy, WeaveScale, WeaveSharpness, WeaveSeed + 2.7, wob);
    float mz = PCFab_WeavePlane(ObjectPos.xyz, WeaveScale, WeaveSharpness, WeaveSeed + 5.3, wob);

    float weave = mx * w.x + my * w.y + mz * w.z;
    float fade = saturate(1.0 - PCFab_Footprint(ObjectPos, WeaveScale) * 1.6);
    return saturate(lerp(0.42, weave, fade));
}

// ── Волокна ──────────────────────────────────────────────────────────────────
// fbm, растянутый вдоль первой оси плоскости -> длинные штрихи вдоль нити.
float PCFab_FibersPlane(float3 p, float scale, float stretch, float detail, float seed)
{
    float3 q = p * max(0.001, scale) + seed;
    q.x *= max(0.05, stretch);
    return saturate(PCFab_Ridge(PCFab_Fbm3(q, max(1.1, detail))));
}

float PC_FabricFibers(float3 ObjectPos, float3 ObjectNormal, float FiberScale, float FiberStretch, float FiberDetail, float FiberSeed)
{
    float3 w = PCFab_TriWeights(ObjectNormal);

    float mx = PCFab_FibersPlane(ObjectPos.yzx, FiberScale, FiberStretch, FiberDetail, FiberSeed);
    float my = PCFab_FibersPlane(ObjectPos.xzy, FiberScale, FiberStretch, FiberDetail, FiberSeed + 4.3);
    float mz = PCFab_FibersPlane(ObjectPos.xyz, FiberScale, FiberStretch, FiberDetail, FiberSeed + 8.9);

    float fibers = mx * w.x + my * w.y + mz * w.z;
    float fade = saturate(1.0 - PCFab_Footprint(ObjectPos, FiberScale) * 2.5);
    return saturate(lerp(0.45, fibers, fade));
}

// ── Непсы / катышки ──────────────────────────────────────────────────────────
float PC_FabricNeps(float3 ObjectPos, float NepScale, float NepSeed)
{
    float3 p = ObjectPos * max(0.001, NepScale) + NepSeed;
    float d = PCFab_Worley(p);
    float neps = saturate(1.0 - smoothstep(0.16, 0.42, d));
    float fade = saturate(1.0 - PCFab_Footprint(ObjectPos, NepScale) * 2.5);
    return saturate(lerp(0.0, neps, fade));
}

// ── Потёртости / бахрома (механизм царапин, частота своя) ─────────────────────
float PCFab_FrayDir(float3 ObjectPos, float FrayScale, float SX, float SY, float SZ, float FrayDetail, float FraySeed)
{
    float3 sp = ObjectPos * max(0.001, FrayScale) + FraySeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    return PCFab_Ridge(PCFab_Fbm3(sp, FrayDetail));
}

float PCFab_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return PCFab_Fbm3(pp, 2.0);
}

// ── Обёртки _float / _half (один выход) ──────────────────────────────────────
void ProceduralFabricSlubs_float(
    float3 ObjectPos, float SlubScale, float SlubWarp, float SlubSeed,
    out float Slub)
{
    Slub = PC_FabricSlubs(ObjectPos, SlubScale, SlubWarp, SlubSeed);
}

void ProceduralFabricSlubs_half(
    half3 ObjectPos, half SlubScale, half SlubWarp, half SlubSeed,
    out half Slub)
{
    float s;
    ProceduralFabricSlubs_float(
        (float3)ObjectPos, (float)SlubScale, (float)SlubWarp, (float)SlubSeed, s);
    Slub = (half)s;
}

void ProceduralFabricWeave_float(
    float3 ObjectPos, float3 ObjectNormal,
    float WeaveScale, float WeaveSharpness, float WeaveWarp, float Slub, float WeaveSeed,
    out float Weave)
{
    Weave = PC_FabricWeave(ObjectPos, ObjectNormal, WeaveScale, WeaveSharpness, WeaveWarp, Slub, WeaveSeed);
}

void ProceduralFabricWeave_half(
    half3 ObjectPos, half3 ObjectNormal,
    half WeaveScale, half WeaveSharpness, half WeaveWarp, half Slub, half WeaveSeed,
    out half Weave)
{
    float w;
    ProceduralFabricWeave_float(
        (float3)ObjectPos, (float3)ObjectNormal,
        (float)WeaveScale, (float)WeaveSharpness, (float)WeaveWarp, (float)Slub, (float)WeaveSeed, w);
    Weave = (half)w;
}

void ProceduralFabricFibers_float(
    float3 ObjectPos, float3 ObjectNormal,
    float FiberScale, float FiberStretch, float FiberDetail, float FiberSeed,
    out float Fibers)
{
    Fibers = PC_FabricFibers(ObjectPos, ObjectNormal, FiberScale, FiberStretch, FiberDetail, FiberSeed);
}

void ProceduralFabricFibers_half(
    half3 ObjectPos, half3 ObjectNormal,
    half FiberScale, half FiberStretch, half FiberDetail, half FiberSeed,
    out half Fibers)
{
    float f;
    ProceduralFabricFibers_float(
        (float3)ObjectPos, (float3)ObjectNormal,
        (float)FiberScale, (float)FiberStretch, (float)FiberDetail, (float)FiberSeed, f);
    Fibers = (half)f;
}

void ProceduralFabricNeps_float(
    float3 ObjectPos, float NepScale, float NepSeed,
    out float Neps)
{
    Neps = PC_FabricNeps(ObjectPos, NepScale, NepSeed);
}

void ProceduralFabricNeps_half(
    half3 ObjectPos, half NepScale, half NepSeed,
    out half Neps)
{
    float n;
    ProceduralFabricNeps_float((float3)ObjectPos, (float)NepScale, (float)NepSeed, n);
    Neps = (half)n;
}

void ProceduralFabricFrayA_float(
    float3 ObjectPos,
    float FrayScale, float FrayStretch, float FrayDetail, float FraySeed,
    out float FrayA)
{
    FrayA = PCFab_FrayDir(ObjectPos, FrayScale, FrayStretch, 1.0, FrayStretch, FrayDetail, FraySeed);
}

void ProceduralFabricFrayA_half(
    half3 ObjectPos,
    half FrayScale, half FrayStretch, half FrayDetail, half FraySeed,
    out half FrayA)
{
    float s;
    ProceduralFabricFrayA_float(
        (float3)ObjectPos,
        (float)FrayScale, (float)FrayStretch, (float)FrayDetail, (float)FraySeed, s);
    FrayA = (half)s;
}

void ProceduralFabricFrayB_float(
    float3 ObjectPos,
    float FrayScale, float FrayStretch, float FrayDetail, float FraySeed,
    out float FrayB)
{
    FrayB = PCFab_FrayDir(ObjectPos, FrayScale, 1.0, FrayStretch, 1.0, FrayDetail, FraySeed + 17.3);
}

void ProceduralFabricFrayB_half(
    half3 ObjectPos,
    half FrayScale, half FrayStretch, half FrayDetail, half FraySeed,
    out half FrayB)
{
    float b;
    ProceduralFabricFrayB_float(
        (float3)ObjectPos,
        (float)FrayScale, (float)FrayStretch, (float)FrayDetail, (float)FraySeed, b);
    FrayB = (half)b;
}

void ProceduralFabricFrayC_float(
    float3 ObjectPos,
    float FrayScale, float FrayStretch, float FrayDetail, float FraySeed,
    float FrayAngle,
    out float FrayC)
{
    float ca = cos(FrayAngle);
    float sn = sin(FrayAngle);
    float3 sc = ObjectPos * max(0.001, FrayScale) + FraySeed + 41.1;
    sc.xy = float2(sc.x * ca - sc.y * sn, sc.x * sn + sc.y * ca);
    sc.x *= max(0.05, FrayStretch);
    FrayC = PCFab_Ridge(PCFab_Fbm3(sc, FrayDetail));
}

void ProceduralFabricFrayC_half(
    half3 ObjectPos,
    half FrayScale, half FrayStretch, half FrayDetail, half FraySeed,
    half FrayAngle,
    out half FrayC)
{
    float c;
    ProceduralFabricFrayC_float(
        (float3)ObjectPos,
        (float)FrayScale, (float)FrayStretch, (float)FrayDetail, (float)FraySeed,
        (float)FrayAngle, c);
    FrayC = (half)c;
}

void ProceduralFabricFrayMask_float(
    float3 ObjectPos,
    float FrayMaskScale, float FrayMaskSeed,
    out float FrayMask)
{
    FrayMask = PCFab_Blotch(ObjectPos, FrayMaskScale, FrayMaskSeed);
}

void ProceduralFabricFrayMask_half(
    half3 ObjectPos,
    half FrayMaskScale, half FrayMaskSeed,
    out half FrayMask)
{
    float m;
    ProceduralFabricFrayMask_float(
        (float3)ObjectPos,
        (float)FrayMaskScale, (float)FrayMaskSeed, m);
    FrayMask = (half)m;
}

#endif










