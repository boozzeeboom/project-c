#ifndef PROCEDURAL_BRICK_NOISE_INCLUDED
#define PROCEDURAL_BRICK_NOISE_INCLUDED

// ProceduralBrickNoise — object-space noise для КИРПИЧНОЙ СТЕНЫ (Brick).
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим). Сигнатуры = порядок слотов CF-нод в графе. Новое поведение —
// новой функцией; существующие сигнатуры не менять (слоты связываются по номеру).
//
// У бронзы не заимствовано ничего: ни патины, ни Metallic, ни масок кромок
// (кромка стены — это край меша, а не признак материала; износа «до подложки»
// у кирпича не бывает).
//
// ГЛАВНОЕ ОТЛИЧИЕ ОТ МОСТОВОЙ (Cobble): кладка — РЕГУЛЯРНАЯ сетка, а не Worley.
// Кирпич имеет постоянный размер, ряды перевязаны, поэтому ячейки считаются
// floor/frac по масштабированным координатам. «Живость» даёт не разброс формы,
// а искривление сетки (JointWarp — кладку ведёт рука каменщика) и разброс тона
// кирпичей. Worley здесь отвечает ТОЛЬКО за поры и раковины.
//
// Слои:
//   ProceduralBrickJoints — швы раствора: ячейки кладки + перевязка, два шва
//                           разной ширины (вертикальный и горизонтальный)
//   ProceduralBrickTone   — тон каждого кирпича (хеш ячейки, постоянный внутри)
//   ProceduralBrickGrains — зерно: песок на лице кирпича и крупа раствора
//   ProceduralBrickPores  — поры и раковины (Worley F1, свой радиус у ячейки)
//   ProceduralBrickCracks — трещинки: тонкие борозды, идущие ВВЕРХ по стене
//   ProceduralBrickPatch  — общий примитив «пятно fbm»: высолы и грязь
//                           (порог ставится в графе — один рисунок, две роли)
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe):
// AbsoluteWorld не используется, при floating origin стена не «плывёт».
//
// РЕЛЬЕФ (см. docs/Materials/README.md, «Известная ошибка сборки»): три вида
// неровностей — швы, поры, трещины — плюс подъём кирпичей (BrickLift) собираются
// суммой с индивидуальным коэффициентом у каждого вида. Сила этой суммы задаётся
// свойством материала _Bump_Strength, ПОДКЛЮЧЁННЫМ к слоту Strength ноды
// Normal From Height. Оставлять слот Strength неподключённым нельзя: тогда в
// шейдер уходит шаблонный дефолт 0.01 и рельеф гасится в 100 раз.

float PCBrick_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCBrick_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCBrick_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCBrick_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCBrick_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCBrick_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCBrick_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCBrick_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCBrick_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCBrick_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCBrick_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCBrick_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Борозда = РАССТОЯНИЕ до «линии» fbm (там, где fbm проходит через 0.5):
// 0 на самой линии, 1 в стороне от неё. Нужно именно расстояние, а не обратная
// величина (1 − |2s − 1| в Ridge), потому что ширина борозды задаётся отдельным
// параметром Width.
float PCBrick_Groove(float s)
{
    return abs(s * 2.0 - 1.0);
}

// ── Сетка кладки (общая для швов и тона) ─────────────────────────────────────
// Возвращает координаты в «кирпичах»: x — вдоль ряда, y — вверх по стене.
//   BrickScale  — сколько кирпичей укладывается в метр ВДОЛЬ РЯДА;
//   BrickAspect — длина кирпича к его высоте (2.2 = стандартный одинарный).
// Масштаб по Y умножается на Aspect: без этого кирпич вышел бы квадратным,
// потому что координата Y мерится той же линейкой, что и X.
//   RowOffset   — сдвиг каждого следующего ряда (0.5 = перевязка в полкирпича);
//   JointWarp   — насколько ведёт сетку. Прямых швов у кладки не бывает, и без
//                 этого параметра стена читается как обоями наклеенная.
float2 PCBrick_Grid(float3 ObjectPos, float BrickScale, float BrickAspect, float RowOffset, float JointWarp, float BrickSeed)
{
    float3 p = ObjectPos * max(0.001, BrickScale) + BrickSeed;
    float2 q = float2(p.x, p.y * max(0.05, BrickAspect));
    if (JointWarp > 0.0001)
    {
        float2 w = float2(
            PCBrick_ValueNoise3(p * 0.6 + 5.3),
            PCBrick_ValueNoise3(p * 0.6 + 19.7)) - 0.5;
        q += w * JointWarp;
    }
    float row = floor(q.y);
    q.x += row * RowOffset;
    return q;
}

// ── Швы раствора ─────────────────────────────────────────────────────────────
// Шов — полоса у границы ячейки. Два шва РАЗНОЙ ширины, и это не украшательство:
// у реальной кладки шов 1 см при кирпиче 21.5 x 6.5 см даёт 0.046 доли длины,
// но 0.154 доли высоты. Одна общая ширина сделала бы либо вертикальный шов
// вдвое толще горизонтального, либо наоборот.
float PCBrick_Joints(float3 ObjectPos, float BrickScale, float BrickAspect, float RowOffset, float JointWarp, float JointWidthX, float JointWidthY, float BrickSeed)
{
    float2 q = PCBrick_Grid(ObjectPos, BrickScale, BrickAspect, RowOffset, JointWarp, BrickSeed);
    float2 f = frac(q);
    // расстояние до ближайшей границы кирпича: 0 на границе, 0.5 в центре
    float2 d = min(f, 1.0 - f);
    float edge = min(d.x / max(0.004, JointWidthX), d.y / max(0.004, JointWidthY));
    return saturate(1.0 - smoothstep(0.0, 1.0, edge));
}

// ── Тон кирпича ──────────────────────────────────────────────────────────────
// Величина, постоянная внутри кирпича и скачком меняющаяся на его границе.
// Именно она (а не поры) даёт «набрано из разного кирпича»: в одной стене
// всегда есть пережжённые и недожжённые кирпичи. Считается той же сеткой, что и
// швы, поэтому при совпадающих Scale/Aspect/Offset/Warp/Seed тон и его границы
// лежат в одной сетке — разъехаться они не могут.
float PCBrick_Tone(float3 ObjectPos, float BrickScale, float BrickAspect, float RowOffset, float JointWarp, float BrickSeed)
{
    float2 q = PCBrick_Grid(ObjectPos, BrickScale, BrickAspect, RowOffset, JointWarp, BrickSeed);
    float2 cell = floor(q);
    return saturate(PCBrick_Hash13(float3(cell, BrickSeed + 11.0)));
}

// ── Зерно ────────────────────────────────────────────────────────────────────
// Один примитив, два масштаба: песок на лице кирпича и крупа раствора.
// GrainStretch > 1 вытягивает зерно вдоль локальной X (лицо кирпича «сдирное»,
// зерно идёт вдоль формы); у раствора зерно изотропное, там Stretch = 1.
float PCBrick_Grains(float3 ObjectPos, float GrainScale, float GrainStretch, float GrainSeed)
{
    float3 p = ObjectPos * max(0.001, GrainScale) + GrainSeed;
    p.x /= max(0.05, GrainStretch);
    return saturate(PCBrick_Fbm3(p, 2.0));
}

// ── Поры и раковины ──────────────────────────────────────────────────────────
// F1-расстояние до центра ячейки Worley: 0 в центре поры. Радиус у каждой ячейки
// свой, и часть ячеек остаётся без поры совсем — иначе поверхность читается
// ситом, а у кирпича поры редкие и разного размера.
float PCBrick_Pores(float3 ObjectPos, float PoreScale, float PoreWidth, float PoreSeed)
{
    float3 p = ObjectPos * max(0.001, PoreScale) + PoreSeed;
    float3 g = floor(p);
    float f1 = 8.0;
    float rnd = 0.0;
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
                float3 o = float3(
                    PCBrick_Hash13(cell),
                    PCBrick_Hash13(cell + 17.0),
                    PCBrick_Hash13(cell + 31.0));
                float d = length(cell + o - p);
                if (d < f1)
                {
                    f1 = d;
                    rnd = PCBrick_Hash13(cell + 53.0);
                }
            }
        }
    }
    float w = max(0.004, PoreWidth);
    float radius = w * lerp(0.35, 1.0, rnd);
    // smoothstep от 0.22: примерно треть ячеек без поры
    float exists = smoothstep(0.22, 0.42, PCBrick_Hash13(g + 71.0));
    return saturate(1.0 - smoothstep(radius * 0.40, radius, f1)) * exists;
}

// ── Трещинки ─────────────────────────────────────────────────────────────────
// Борозда = расстояние до линии fbm. Сетка растянута по локальной Y
// (CrackStretch), поэтому трещины идут ВВЕРХ по стене — так раскалывается кладка
// при осадке. Домен-warp их изгибает, а крупный fbm решает, ГДЕ трещина
// раскрылась: сплошная сеть по всей стене читалась бы как шум. Раскрытие
// модулирует глубину, но не обрывает трещину насовсем (нижняя граница 0.35).
float PCBrick_Cracks(float3 ObjectPos, float CrackScale, float CrackStretch, float CrackWidth, float CrackWarp, float CrackSeed)
{
    float3 p = ObjectPos * max(0.001, CrackScale) + CrackSeed;
    p.y /= max(0.05, CrackStretch);
    if (CrackWarp > 0.0001)
    {
        float3 w = float3(
            PCBrick_ValueNoise3(p * 0.5 + 3.1),
            PCBrick_ValueNoise3(p * 0.5 + 17.9),
            PCBrick_ValueNoise3(p * 0.5 + 41.7)) - 0.5;
        p += w * CrackWarp;
    }
    float d = PCBrick_Groove(PCBrick_Fbm3(p, 2.0));
    float w = max(0.004, CrackWidth);
    float crack = saturate(1.0 - smoothstep(0.0, w, d));
    float opened = smoothstep(0.30, 0.80, PCBrick_Fbm3(p * 0.30 + 7.7, 2.0));
    return saturate(crack * lerp(0.35, 1.0, opened));
}

// ── Пятно (общий примитив) ───────────────────────────────────────────────────
// fbm в 0..1 без порогов: порог ставится в графе (Smoothstep), потому что один
// и тот же рисунок используется в двух ролях с разной полярностью.
//   PatchStretch = 1  -> изотропные пятна (высолы)
//   PatchStretch > 1  -> рисунок вытянут вдоль ЛОКАЛЬНОЙ Y (грязь стекает вниз)
float PCBrick_Patch(float3 ObjectPos, float PatchScale, float PatchStretch, float PatchSeed)
{
    float3 p = ObjectPos * max(0.001, PatchScale) + PatchSeed;
    p.y /= max(0.05, PatchStretch);
    return saturate(PCBrick_Fbm3(p, 2.0));
}

// ── Обёртки CF-нод (одно-выходные, File-режим) ───────────────────────────────

void ProceduralBrickJoints_float(
    float3 ObjectPos,
    float BrickScale, float BrickAspect, float RowOffset, float JointWarp,
    float JointWidthX, float JointWidthY, float BrickSeed,
    out float Joints)
{
    Joints = PCBrick_Joints(ObjectPos, BrickScale, BrickAspect, RowOffset, JointWarp, JointWidthX, JointWidthY, BrickSeed);
}

void ProceduralBrickJoints_half(
    half3 ObjectPos,
    half BrickScale, half BrickAspect, half RowOffset, half JointWarp,
    half JointWidthX, half JointWidthY, half BrickSeed,
    out half Joints)
{
    float j;
    ProceduralBrickJoints_float(
        (float3)ObjectPos,
        (float)BrickScale, (float)BrickAspect, (float)RowOffset, (float)JointWarp,
        (float)JointWidthX, (float)JointWidthY, (float)BrickSeed, j);
    Joints = (half)j;
}

void ProceduralBrickTone_float(
    float3 ObjectPos,
    float BrickScale, float BrickAspect, float RowOffset, float JointWarp, float BrickSeed,
    out float BrickTone)
{
    BrickTone = PCBrick_Tone(ObjectPos, BrickScale, BrickAspect, RowOffset, JointWarp, BrickSeed);
}

void ProceduralBrickTone_half(
    half3 ObjectPos,
    half BrickScale, half BrickAspect, half RowOffset, half JointWarp, half BrickSeed,
    out half BrickTone)
{
    float t;
    ProceduralBrickTone_float(
        (float3)ObjectPos,
        (float)BrickScale, (float)BrickAspect, (float)RowOffset, (float)JointWarp, (float)BrickSeed, t);
    BrickTone = (half)t;
}

void ProceduralBrickGrains_float(
    float3 ObjectPos,
    float GrainScale, float GrainStretch, float GrainSeed,
    out float Grains)
{
    Grains = PCBrick_Grains(ObjectPos, GrainScale, GrainStretch, GrainSeed);
}

void ProceduralBrickGrains_half(
    half3 ObjectPos,
    half GrainScale, half GrainStretch, half GrainSeed,
    out half Grains)
{
    float g;
    ProceduralBrickGrains_float((float3)ObjectPos, (float)GrainScale, (float)GrainStretch, (float)GrainSeed, g);
    Grains = (half)g;
}

void ProceduralBrickPores_float(
    float3 ObjectPos,
    float PoreScale, float PoreWidth, float PoreSeed,
    out float Pores)
{
    Pores = PCBrick_Pores(ObjectPos, PoreScale, PoreWidth, PoreSeed);
}

void ProceduralBrickPores_half(
    half3 ObjectPos,
    half PoreScale, half PoreWidth, half PoreSeed,
    out half Pores)
{
    float v;
    ProceduralBrickPores_float((float3)ObjectPos, (float)PoreScale, (float)PoreWidth, (float)PoreSeed, v);
    Pores = (half)v;
}

void ProceduralBrickCracks_float(
    float3 ObjectPos,
    float CrackScale, float CrackStretch, float CrackWidth, float CrackWarp, float CrackSeed,
    out float Cracks)
{
    Cracks = PCBrick_Cracks(ObjectPos, CrackScale, CrackStretch, CrackWidth, CrackWarp, CrackSeed);
}

void ProceduralBrickCracks_half(
    half3 ObjectPos,
    half CrackScale, half CrackStretch, half CrackWidth, half CrackWarp, half CrackSeed,
    out half Cracks)
{
    float c;
    ProceduralBrickCracks_float(
        (float3)ObjectPos,
        (float)CrackScale, (float)CrackStretch, (float)CrackWidth, (float)CrackWarp, (float)CrackSeed, c);
    Cracks = (half)c;
}

void ProceduralBrickPatch_float(
    float3 ObjectPos,
    float PatchScale, float PatchStretch, float PatchSeed,
    out float Patch)
{
    Patch = PCBrick_Patch(ObjectPos, PatchScale, PatchStretch, PatchSeed);
}

void ProceduralBrickPatch_half(
    half3 ObjectPos,
    half PatchScale, half PatchStretch, half PatchSeed,
    out half Patch)
{
    float b;
    ProceduralBrickPatch_float((float3)ObjectPos, (float)PatchScale, (float)PatchStretch, (float)PatchSeed, b);
    Patch = (half)b;
}

#endif
