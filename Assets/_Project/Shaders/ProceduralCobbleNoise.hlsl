#ifndef PROCEDURAL_COBBLE_NOISE_INCLUDED
#define PROCEDURAL_COBBLE_NOISE_INCLUDED

// ProceduralCobbleNoise — object-space 3D noise для МОСТОВОЙ (Cobble).
//
// Материал собирался как «кора дерева», но по визуальному тесту читается как
// каменная кладка: разные камни (ячейки Worley + тон ячейки), швы между ними,
// мох в швах, грязь и выбоины. Перерисован НЕ был — переименован (Bark -> Cobble),
// рисунок, значения и сигнатуры те же. Первоисточник: вторая попытка коры
// делается отдельно (ProceduralBarkNoise.hlsl, v2).
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим). Сигнатуры = порядок слотов CF-нод в графе. Новое поведение —
// новой функцией; существующие сигнатуры не менять (слоты связываются по номеру).
//
// У бронзы не заимствовано НИЧЕГО: ни маски кромок, ни наборов царапин.
// У плоского покрытия кромки меша — это края стенки, а не признак материала;
// износа «до подложки» и металличности у камня не бывает.
//
// Слои:
//   ProceduralCobbleCracks    — трещины в камне: борозды ВДОЛЬ локальной Y
//   ProceduralCobbleJoints    — швы кладки (анизотропный Worley, F2 − F1)
//   ProceduralCobbleStoneTone — тон каждого камня (хеш выигравшей ячейки Worley)
//   ProceduralCobbleFlaws     — сколы и поперечные трещины: короткие разрывы ПОПЕРЁК
//   ProceduralCobblePatch     — общий примитив «пятно fbm»: лишайник, мох,
//                               грязь и выбоины (порог ставится в графе)
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe).
// AbsoluteWorld не используется.

float PCCobble_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCCobble_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCCobble_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCCobble_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCCobble_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCCobble_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCCobble_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCCobble_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCCobble_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCCobble_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCCobble_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCCobble_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Борозда = РАССТОЯНИЕ до «линии» fbm (там, где fbm проходит через 0.5):
// 0 на самой линии, 1 в стороне от неё. В других материалах используется
// обратная величина (ridged: 1 − |2s − 1|); здесь нужно именно расстояние,
// потому что ширина разрыва задаётся отдельным параметром (Width).
float PCCobble_Groove(float s)
{
    return abs(s * 2.0 - 1.0);
}

// Worley (3x3x3): F1 — до ближайшего центра ячейки, F2 — до второго.
// Возвращает F2 − F1, то есть расстояние ДО ГРАНИЦЫ ячейки: 0 на шве кладки
// и растёт внутрь камня. cellTone — хеш выигравшей ячейки: величина,
// постоянная внутри камня и скачком меняющаяся на его границе (тон камня).
float PCCobble_WorleyF2F1(float3 p, out float cellTone)
{
    float3 g = floor(p);
    float f1 = 8.0;
    float f2 = 8.0;
    float best = 0.0;
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
                    PCCobble_Hash13(cell + 0.0),
                    PCCobble_Hash13(cell + 17.0),
                    PCCobble_Hash13(cell + 31.0));
                float d = length(cell + rnd - p);
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                    best = PCCobble_Hash13(cell + 53.0);
                }
                else if (d < f2)
                {
                    f2 = d;
                }
            }
        }
    }
    cellTone = best;
    return f2 - f1;
}

// ── Трещины в камне ──────────────────────────────────────────────────────────
// Камень трескается ВДОЛЬ поверхности: трещины длинные и почти прямые.
// Деление на CrackStretch по локальной Y растягивает линии fbm вдоль стены,
// домен-warp их изгибает, а крупный fbm решает, ГДЕ трещина раскрылась —
// сплошная сеть по всей поверхности читалась бы как шум. Раскрытие модулирует
// глубину, но не обрывает трещину насовсем (нижняя граница 0.40).
float PCCobble_Cracks(float3 ObjectPos, float CrackScale, float CrackStretch, float CrackWidth, float CrackWarp, float CrackSeed)
{
    float3 p = ObjectPos * max(0.001, CrackScale) + CrackSeed;
    p.y /= max(0.05, CrackStretch);
    if (CrackWarp > 0.0001)
    {
        float3 w = float3(
            PCCobble_ValueNoise3(p * 0.5 + 3.1),
            PCCobble_ValueNoise3(p * 0.5 + 17.9),
            PCCobble_ValueNoise3(p * 0.5 + 41.7)) - 0.5;
        p += w * CrackWarp;
    }
    float d = PCCobble_Groove(PCCobble_Fbm3(p, 2.0));
    float w = max(0.004, CrackWidth);
    float crack = saturate(1.0 - smoothstep(0.0, w, d));
    float opened = smoothstep(0.28, 0.78, PCCobble_Fbm3(p * 0.30 + 7.7, 2.0));
    return saturate(crack * lerp(0.40, 1.0, opened));
}

// ── Швы кладки ───────────────────────────────────────────────────────────────
// Шов = граница ячейки Worley. Ячейки вытянуты вдоль локальной Y (StoneStretch),
// поэтому швы идут вдоль стены, а не кольцами.
float PCCobble_Joints(float3 ObjectPos, float StoneScale, float StoneStretch, float JointWidth, float StoneSeed)
{
    float3 p = ObjectPos * max(0.001, StoneScale) + StoneSeed;
    p.y /= max(0.05, StoneStretch);
    float cellTone;
    float border = PCCobble_WorleyF2F1(p, cellTone);
    float w = max(0.004, JointWidth);
    return saturate(1.0 - smoothstep(0.0, w, border));
}

// ── Тон камня ────────────────────────────────────────────────────────────────
// Постоянная внутри камня величина: соседние камни отличаются оттенком —
// именно это даёт «набрано из разного камня». Считается тем же Worley, что и швы,
// поэтому при совпадающих Scale/Stretch/Seed камни и их границы лежат в одной сетке.
float PCCobble_StoneTone(float3 ObjectPos, float StoneScale, float StoneStretch, float StoneSeed)
{
    float3 p = ObjectPos * max(0.001, StoneScale) + StoneSeed;
    p.y /= max(0.05, StoneStretch);
    float cellTone;
    PCCobble_WorleyF2F1(p, cellTone);
    return saturate(cellTone);
}

// ── Сколы и поперечные трещины ───────────────────────────────────────────────
// Второй, более редкий рисунок: короткие разрывы ПОПЕРЁК стены.
// Здесь медленно меняются XZ (FlawStretch) и быстро — Y, поэтому линии ложатся
// поперёк; крупный fbm рвёт их на отдельные отрезки (patch).
float PCCobble_Flaws(float3 ObjectPos, float FlawScale, float FlawStretch, float FlawWidth, float FlawSeed)
{
    float3 p = ObjectPos * max(0.001, FlawScale) + FlawSeed;
    p.x /= max(0.05, FlawStretch);
    p.z /= max(0.05, FlawStretch);
    float d = PCCobble_Groove(PCCobble_Fbm3(p, 2.0));
    float w = max(0.004, FlawWidth);
    float crack = saturate(1.0 - smoothstep(0.0, w, d));
    float patch = smoothstep(0.34, 0.80, PCCobble_Fbm3(p * 0.5 + 4.3, 2.0));
    return crack * patch;
}

// ── Пятно (общий примитив) ───────────────────────────────────────────────────
// fbm в 0..1 без порогов: порог ставится в графе (Smoothstep), потому что один
// и тот же рисунок используется в трёх ролях с разной полярностью.
//   PatchStretch = 1  -> изотропные пятна (лишайник, грязь и выбоины)
//   PatchStretch > 1  -> рисунок вытянут вдоль ЛОКАЛЬНОЙ Y (мох вниз по стене)
float PCCobble_Patch(float3 ObjectPos, float PatchScale, float PatchStretch, float PatchSeed)
{
    float3 p = ObjectPos * max(0.001, PatchScale) + PatchSeed;
    p.y /= max(0.05, PatchStretch);
    return saturate(PCCobble_Fbm3(p, 2.0));
}

void ProceduralCobbleCracks_float(
    float3 ObjectPos,
    float CrackScale, float CrackStretch, float CrackWidth, float CrackWarp, float CrackSeed,
    out float Cracks)
{
    Cracks = PCCobble_Cracks(ObjectPos, CrackScale, CrackStretch, CrackWidth, CrackWarp, CrackSeed);
}

void ProceduralCobbleCracks_half(
    half3 ObjectPos,
    half CrackScale, half CrackStretch, half CrackWidth, half CrackWarp, half CrackSeed,
    out half Cracks)
{
    float f;
    ProceduralCobbleCracks_float(
        (float3)ObjectPos,
        (float)CrackScale, (float)CrackStretch, (float)CrackWidth, (float)CrackWarp, (float)CrackSeed, f);
    Cracks = (half)f;
}

void ProceduralCobbleJoints_float(
    float3 ObjectPos,
    float StoneScale, float StoneStretch, float JointWidth, float StoneSeed,
    out float Joints)
{
    Joints = PCCobble_Joints(ObjectPos, StoneScale, StoneStretch, JointWidth, StoneSeed);
}

void ProceduralCobbleJoints_half(
    half3 ObjectPos,
    half StoneScale, half StoneStretch, half JointWidth, half StoneSeed,
    out half Joints)
{
    float p;
    ProceduralCobbleJoints_float(
        (float3)ObjectPos,
        (float)StoneScale, (float)StoneStretch, (float)JointWidth, (float)StoneSeed, p);
    Joints = (half)p;
}

void ProceduralCobbleStoneTone_float(
    float3 ObjectPos,
    float StoneScale, float StoneStretch, float StoneSeed,
    out float StoneTone)
{
    StoneTone = PCCobble_StoneTone(ObjectPos, StoneScale, StoneStretch, StoneSeed);
}

void ProceduralCobbleStoneTone_half(
    half3 ObjectPos,
    half StoneScale, half StoneStretch, half StoneSeed,
    out half StoneTone)
{
    float t;
    ProceduralCobbleStoneTone_float(
        (float3)ObjectPos,
        (float)StoneScale, (float)StoneStretch, (float)StoneSeed, t);
    StoneTone = (half)t;
}

void ProceduralCobbleFlaws_float(
    float3 ObjectPos,
    float FlawScale, float FlawStretch, float FlawWidth, float FlawSeed,
    out float Flaws)
{
    Flaws = PCCobble_Flaws(ObjectPos, FlawScale, FlawStretch, FlawWidth, FlawSeed);
}

void ProceduralCobbleFlaws_half(
    half3 ObjectPos,
    half FlawScale, half FlawStretch, half FlawWidth, half FlawSeed,
    out half Flaws)
{
    float c;
    ProceduralCobbleFlaws_float(
        (float3)ObjectPos,
        (float)FlawScale, (float)FlawStretch, (float)FlawWidth, (float)FlawSeed, c);
    Flaws = (half)c;
}

void ProceduralCobblePatch_float(
    float3 ObjectPos,
    float PatchScale, float PatchStretch, float PatchSeed,
    out float Patch)
{
    Patch = PCCobble_Patch(ObjectPos, PatchScale, PatchStretch, PatchSeed);
}

void ProceduralCobblePatch_half(
    half3 ObjectPos,
    half PatchScale, half PatchStretch, half PatchSeed,
    out half Patch)
{
    float b;
    ProceduralCobblePatch_float(
        (float3)ObjectPos,
        (float)PatchScale, (float)PatchStretch, (float)PatchSeed, b);
    Patch = (half)b;
}

#endif
