#ifndef PROCEDURAL_CONCRETE_NOISE_INCLUDED
#define PROCEDURAL_CONCRETE_NOISE_INCLUDED

// ProceduralConcreteNoise — object-space 3D noise для БЕТОНА (бетон = заполнитель +
// цементное тесто + воздух). Металлического здесь нет: ни патины, ни ржавчины,
// ни блика «до подложки».
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим). Сигнатуры = порядок слотов CF-нод в графе. Новое поведение —
// новой функцией; существующие сигнатуры не менять (слоты связываются по номеру).
//
// Слои:
//   ProceduralConcreteAggregate — зерно заполнителя: ячейки Worley F1 + свой тон
//                                 каждой ячейке (песок/щебень в цементном тесте)
//   ProceduralConcretePores     — воздушные раковины: редкие ямки в поверхности
//   ProceduralConcreteCracks    — трещины: сеть тонких линий с изломом (домен warp)
//   ProceduralConcreteBlotch    — общий примитив «крупное пятно»: облачность теста
//                                 (Stretch=1) и вертикальные потёки/высолы (Stretch>1)
//   ProceduralConcreteScratchA/B/C + ScratchMask — царапины: механизм взят у бронзы
//                                 (три направления + гасящая маска), сигнатуры те же
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe).
// AbsoluteWorld не используется.
//
// ── ВАЖНОЕ ОТЛИЧИЕ ОТ БРОНЗЫ (см. docs/Materials/Concrete/README.md) ────────
// Smoothstep в ShaderGraph считается как smoothstep(Edge1, Edge2, In), т.е.
// Edge1 = НИЖНИЙ порог, Edge2 = ВЕРХНИЙ, In = значение (SmoothstepNode.cs:28).
// Поэтому значение здесь всегда подаётся во вход In, а пороги — в Edge1/Edge2.

float PCConc_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCConc_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCConc_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCConc_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCConc_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCConc_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCConc_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCConc_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCConc_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCConc_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCConc_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCConc_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Ridged: 1 на «линии» (там, где fbm проходит через 0.5), 0 в стороне от неё.
float PCConc_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

// Worley F1 (3x3x3). Дополнительно отдаёт хеш выигравшей ячейки — это «тон зерна»:
// величина, постоянная внутри ячейки и скачком меняющаяся на её границе.
// Именно скачок тона на границе даёт на расстоянии ту соль-с-перцем крупу,
// по которой бетон и опознаётся.
float PCConc_WorleyF1(float3 p, out float cellHash)
{
    float3 g = floor(p);
    float minD = 8.0;
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
                    PCConc_Hash13(cell + 0.0),
                    PCConc_Hash13(cell + 17.0),
                    PCConc_Hash13(cell + 31.0));
                float d = length(cell + rnd - p);
                if (d < minD)
                {
                    minD = d;
                    best = PCConc_Hash13(cell + 53.0);
                }
            }
        }
    }
    cellHash = best;
    return minD;
}

// ── Заполнитель ──────────────────────────────────────────────────────────────
// Зерно песка/щебня в цементном тесте. Форма — плато в центре ячейки Worley,
// тон — хеш ячейки. AggTone = 0: все зёрна одного тона; 1: разброс 0.25…1.0
// (от почти невидимого зерна до светлой кварцевой крупинки).
float PCConc_Aggregate(float3 ObjectPos, float AggScale, float AggTone, float AggSeed)
{
    float3 p = ObjectPos * max(0.001, AggScale) + AggSeed;
    float cellTone;
    float d = PCConc_WorleyF1(p, cellTone);
    float shape = saturate(1.0 - smoothstep(0.20, 0.58, d));
    float tone = lerp(0.85, lerp(0.25, 1.0, cellTone), saturate(AggTone));
    return saturate(shape * tone);
}

// ── Воздушные раковины ───────────────────────────────────────────────────────
// Ямки от пузырьков воздуха у опалубки. Считаются как близость к центру ячейки
// Worley высокой частоты: 1 внутри ямки, 0 вне. PoresSize — радиус ямки в долях
// ячейки (покрытие растёт как 1 - exp(-4.19 r^3): 0.30 ≈ 11 %, 0.45 ≈ 30 %).
float PCConc_Pores(float3 ObjectPos, float PoresScale, float PoresSize, float PoresSeed)
{
    float3 p = ObjectPos * max(0.001, PoresScale) + PoresSeed;
    float cellTone;
    float d = PCConc_WorleyF1(p, cellTone);
    float r = clamp(PoresSize, 0.04, 0.90);
    return saturate(1.0 - smoothstep(r * 0.40, r, d));
}

// ── Трещины ──────────────────────────────────────────────────────────────────
// Линия трещины = гребень ridged-fbm; домен предварительно искривляется (warp),
// чтобы линии не читались как регулярная рябь. Второй, более крупный fbm решает,
// ГДЕ вообще есть трещины: сплошная сеть по всей поверхности выглядит как шум,
// а трещины живут участками.
float PCConc_Cracks(float3 ObjectPos, float CrackScale, float CrackWidth, float CrackWarp, float CrackSeed)
{
    float3 p = ObjectPos * max(0.001, CrackScale) + CrackSeed;
    if (CrackWarp > 0.0001)
    {
        float3 w = float3(
            PCConc_ValueNoise3(p * 0.45 + 9.1),
            PCConc_ValueNoise3(p * 0.45 + 23.7),
            PCConc_ValueNoise3(p * 0.45 + 51.3)) - 0.5;
        p += w * CrackWarp;
    }
    // 'line' — зарезервированное слово HLSL (примитив geometry-шейдера), поэтому ridge.
    float ridge = PCConc_Ridge(PCConc_Fbm3(p, 2.0));
    float w = max(0.004, CrackWidth);
    float crack = saturate(1.0 - smoothstep(0.0, w, 1.0 - ridge));
    float patch = smoothstep(0.30, 0.72, PCConc_Fbm3(p * 0.35 + 13.9, 2.0));
    return crack * patch;
}

// ── Крупное пятно (общий примитив) ───────────────────────────────────────────
// Возвращает fbm в 0..1 без порогов: порог ставится в графе (Smoothstep), потому
// что один и тот же рисунок используется в трёх ролях с разной полярностью.
//   BlotchStretch = 1  -> изотропные пятна (облачность теста, мокрые пятна)
//   BlotchStretch > 1  -> рисунок вытянут вдоль ЛОКАЛЬНОЙ Y (потёки и высолы вниз)
float PCConc_Blotch(float3 ObjectPos, float BlotchScale, float BlotchStretch, float BlotchSeed)
{
    float3 p = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    p.y /= max(0.05, BlotchStretch);
    return saturate(PCConc_Fbm3(p, 2.0));
}

// ── Царапины (механизм взят у бронзы, сигнатуры 1:1) ─────────────────────────
float PCConc_ScratchDir(float3 ObjectPos, float ScratchScale, float SX, float SY, float SZ, float ScratchDetail, float ScratchSeed)
{
    float3 sp = ObjectPos * max(0.001, ScratchScale) + ScratchSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    return PCConc_Ridge(PCConc_Fbm3(sp, ScratchDetail));
}

void ProceduralConcreteAggregate_float(
    float3 ObjectPos, float AggScale, float AggTone, float AggSeed,
    out float Aggregate)
{
    Aggregate = PCConc_Aggregate(ObjectPos, AggScale, AggTone, AggSeed);
}

void ProceduralConcreteAggregate_half(
    half3 ObjectPos, half AggScale, half AggTone, half AggSeed,
    out half Aggregate)
{
    float a;
    ProceduralConcreteAggregate_float(
        (float3)ObjectPos, (float)AggScale, (float)AggTone, (float)AggSeed, a);
    Aggregate = (half)a;
}

void ProceduralConcretePores_float(
    float3 ObjectPos, float PoresScale, float PoresSize, float PoresSeed,
    out float Pores)
{
    Pores = PCConc_Pores(ObjectPos, PoresScale, PoresSize, PoresSeed);
}

void ProceduralConcretePores_half(
    half3 ObjectPos, half PoresScale, half PoresSize, half PoresSeed,
    out half Pores)
{
    float p;
    ProceduralConcretePores_float(
        (float3)ObjectPos, (float)PoresScale, (float)PoresSize, (float)PoresSeed, p);
    Pores = (half)p;
}

void ProceduralConcreteCracks_float(
    float3 ObjectPos, float CrackScale, float CrackWidth, float CrackWarp, float CrackSeed,
    out float Cracks)
{
    Cracks = PCConc_Cracks(ObjectPos, CrackScale, CrackWidth, CrackWarp, CrackSeed);
}

void ProceduralConcreteCracks_half(
    half3 ObjectPos, half CrackScale, half CrackWidth, half CrackWarp, half CrackSeed,
    out half Cracks)
{
    float c;
    ProceduralConcreteCracks_float(
        (float3)ObjectPos, (float)CrackScale, (float)CrackWidth, (float)CrackWarp, (float)CrackSeed, c);
    Cracks = (half)c;
}

void ProceduralConcreteBlotch_float(
    float3 ObjectPos, float BlotchScale, float BlotchStretch, float BlotchSeed,
    out float Blotch)
{
    Blotch = PCConc_Blotch(ObjectPos, BlotchScale, BlotchStretch, BlotchSeed);
}

void ProceduralConcreteBlotch_half(
    half3 ObjectPos, half BlotchScale, half BlotchStretch, half BlotchSeed,
    out half Blotch)
{
    float b;
    ProceduralConcreteBlotch_float(
        (float3)ObjectPos, (float)BlotchScale, (float)BlotchStretch, (float)BlotchSeed, b);
    Blotch = (half)b;
}

void ProceduralConcreteScratchA_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float Scratch)
{
    Scratch = PCConc_ScratchDir(ObjectPos, ScratchScale, ScratchStretch, 1.0, ScratchStretch, ScratchDetail, ScratchSeed);
}

void ProceduralConcreteScratchA_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half Scratch)
{
    float s;
    ProceduralConcreteScratchA_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, s);
    Scratch = (half)s;
}

void ProceduralConcreteScratchB_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float ScratchB)
{
    ScratchB = PCConc_ScratchDir(ObjectPos, ScratchScale, 1.0, ScratchStretch, 1.0, ScratchDetail, ScratchSeed + 17.3);
}

void ProceduralConcreteScratchB_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half ScratchB)
{
    float b;
    ProceduralConcreteScratchB_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, b);
    ScratchB = (half)b;
}

void ProceduralConcreteScratchC_float(
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
    ScratchC = PCConc_Ridge(PCConc_Fbm3(sc, ScratchDetail));
}

void ProceduralConcreteScratchC_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    half ScratchAngle,
    out half ScratchC)
{
    float c;
    ProceduralConcreteScratchC_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed,
        (float)ScratchAngle, c);
    ScratchC = (half)c;
}

void ProceduralConcreteScratchMask_float(
    float3 ObjectPos,
    float ScratchMaskScale, float ScratchMaskSeed,
    out float ScratchMask)
{
    ScratchMask = PCConc_Blotch(ObjectPos, ScratchMaskScale, 1.0, ScratchMaskSeed);
}

void ProceduralConcreteScratchMask_half(
    half3 ObjectPos,
    half ScratchMaskScale, half ScratchMaskSeed,
    out half ScratchMask)
{
    float m;
    ProceduralConcreteScratchMask_float(
        (float3)ObjectPos,
        (float)ScratchMaskScale, (float)ScratchMaskSeed, m);
    ScratchMask = (half)m;
}

#endif
