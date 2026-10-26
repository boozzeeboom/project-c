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

// ── Полоса пропускания (26.10, по образцу Cobble/Concrete/Wood/Glass/Iron) ─────────────
// Пиксель сцены ≈ 1.37 мм × дистанция: на дальнем плане шов/тон кирпича/зерно/пора/трещина мельче
// пикселя, и в рельефе это случайный наклон в каждом блоке 2×2 («шум при любом бампе»).
// Слой с периодом ≤ ~1.8 пикс сводится к среднему, ≥ ~4.5 пикс отдаётся целиком.
// Пороги = значения пользователя из PCCobble_Band (эталон). Сигнатуры CF не меняются.
float PCBrick_Footprint(float3 p)
{
    return max(length(ddx(p)), length(ddy(p)));
}

#define PCBRICK_BAND_LO 0.22
#define PCBRICK_BAND_HI 0.55
float PCBrick_Band(float3 p)
{
    return 1.0 - smoothstep(PCBRICK_BAND_LO, PCBRICK_BAND_HI, PCBrick_Footprint(p));
}

float PCBrick_Fbm3(float3 p, float detail)
{
    // октава мельче пикселя плавно уходит в своё среднее (0.5)
    float px = PCBrick_Footprint(p);
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        float w = 1.0 - smoothstep(PCBRICK_BAND_LO, PCBRICK_BAND_HI, px * freq);
        sum += amp * lerp(0.5, PCBrick_ValueNoise3(p * freq), w);
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

// Координаты кладки ДО сдвига рядов — для полосы пропускания: сдвиг ряда даёт скачок
// производной на границе ряда, поэтому px по сдвинутой q считать нельзя. Сид на производные не влияет.
float3 PCBrick_GridSpace(float3 ObjectPos, float BrickScale, float BrickAspect)
{
    float3 p = ObjectPos * max(0.001, BrickScale);
    return float3(p.x, p.y * max(0.05, BrickAspect), p.z);
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

    // Шаг пикселя в единицах кладки — ПО ОСЯМ. Берётся из несдвинутого пространства
    // (PCBrick_GridSpace): в PCBrick_Grid есть q.x += row * RowOffset, и ddx по сдвинутой q
    // дал бы скачок производной на каждой границе ряда — пиксель вышел бы шире, чем он есть.
    float3 gs = PCBrick_GridSpace(ObjectPos, BrickScale, BrickAspect);
    float2 px = max(abs(ddx(gs.xy)), abs(ddy(gs.xy)));
    float2 w0 = float2(max(0.004, JointWidthX), max(0.004, JointWidthY));

    // (1) КРОМКА ШВА НЕ МОЖЕТ БЫТЬ УЖЕ ДВУХ ПИКСЕЛЕЙ.
    // Шов 13.5 мм при периоде ряда 150 мм — крупная деталь, но его СТЕНКА перестаёт
    // разрешаться уже с ~5 м: при 10 м пиксель 13.7 мм, и маска успевает пройти 1 -> 0
    // внутри одного пикселя. Высота шва (_Joint_Bump −0.9) сильно перевешивает подъём
    // кирпича (_Brick_Lift 0.15), поэтому та же маска — еще и стенка рельефа: нормаль
    // разворачивается на соседних пикселях, внутри шва чередуется светлое/тёмное, а вдоль
    // ряда это читается как «гуляющие ряды». Замер (ошибка point-sample против
    // area-average в пределах пикселя): 8 м 0.067 -> 0.022, 10 м 0.094 -> 0.016;
    // вплотную (1–3 м) результат не меняется вообще — 0.013 -> 0.013.
    float2 w = max(w0, px * 2.0);
    float mask = saturate(1.0 - smoothstep(0.0, 1.0, min(d.x / w.x, d.y / w.y)));

    // (2) ПОЛОСА СЧИТАЕТСЯ ПО ШИРИНЕ САМОГО ШВА, А НЕ ПО ПЕРИОДУ РЯДА.
    // Период (150 мм) разрешён до ~24 м, поэтому прежняя PCBrick_Band(gs) не гасила шов
    // никогда — хотя шов в 11 раз мельче ряда. Здесь ratio = 1 — стенка ровно в пиксель.
    //
    // Пороги 0.35 / 1.0 — как у Cobble / Plate / Tiles (шаг 2w/2x). Прежние 1.0 / 2.5 были
    // ошибкой: они держали шов полностью открытым ДО одного пикселя и гасили его уже на
    // 0.4 пикселя, т.е. вся полоса лежала ПО ТУ СТОРОНУ разрешения. Отсюда мельтешение на
    // боковых гранях. Причина не в цвете: как только px > w0, стенка (1) уже прижата к 2 пкс,
    // и PC_NormalFromHeightPrecise упирается в свой maxTilt 0.5 — наклон 0.9 высоты на 2 пкс
    // за потолком при ЛЮБОМ расстоянии. Шов становится переключателем ±27° шириной в 2 пкс,
    // соседние пиксели попеременно светлый/тёмный. На гранях с плохой UV этот наклон ещё и
    // доворачивается мусорным TBN, поэтому там он читается уже как чистое чёрное/белое.
    // Замер по M_PC_Brick (шов X 9.5 мм, ряд 150 мм), видимое покрытие шва против истинного
    // 0.140: было 5 м 0.166, 7 м 0.230, 10 м 0.270 (шов вдвое шире настоящего и непрозрачен);
    // стало 0.150 / 0.140 / 0.140. Ошибка point-sample против area-average: было 0.0126 (7 м),
    // 0.0135 (10 м); стало 0 / 0. При косом падении (×4) было уже на 3–4 м 0.011–0.018 и
    // покрытие 0.243–0.259; стало 0 / 0.140. Вплотную (1–2 м) не изменилось ничего: 0.0005.
    float ratio = max(px.x / w0.x, px.y / w0.y);
    float band = 1.0 - smoothstep(0.35, 1.0, ratio);

    // Гаснет к ПОКРЫТИЮ, а не к нулю: шов занимает ~14 % площади (вертикальный + горизонтальный
    // в объединении 0.055 + 0.09 − 0.055·0.09) — это не тонкая линия. Ноль сделал бы дальнюю
    // стену светлее среднего, а голая «растянутая» кромка — шире настоящего шва.
    float coverage = saturate(w0.x + w0.y - w0.x * w0.y);
    return lerp(coverage, mask, band);
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
    // тон постоянен внутри кирпича и скачет на его границе: мельче пикселя → среднее 0.5
    return lerp(0.5, saturate(PCBrick_Hash13(float3(cell, BrickSeed + 11.0))),
                PCBrick_Band(PCBrick_GridSpace(ObjectPos, BrickScale, BrickAspect)));
}

// ── Зерно ────────────────────────────────────────────────────────────────────
// Один примитив, два масштаба: песок на лице кирпича и крупа раствора.
// GrainStretch > 1 вытягивает зерно вдоль локальной X (лицо кирпича «сдирное»,
// зерно идёт вдоль формы); у раствора зерно изотропное, там Stretch = 1.
float PCBrick_Grains(float3 ObjectPos, float GrainScale, float GrainStretch, float GrainSeed)
{
    float3 p = ObjectPos * max(0.001, GrainScale) + GrainSeed;
    p.x /= max(0.05, GrainStretch);
    return lerp(0.5, saturate(PCBrick_Fbm3(p, 2.0)), PCBrick_Band(p));
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
    // пора мельче пикселя → среднее покрытие ≈ 0
    return saturate(1.0 - smoothstep(radius * 0.40, radius, f1)) * exists * PCBrick_Band(p);
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
    // трещина мельче пикселя — тонкая борозда, её среднее ≈ 0
    return saturate(crack * lerp(0.35, 1.0, opened)) * PCBrick_Band(p);
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
    return lerp(0.5, saturate(PCBrick_Fbm3(p, 2.0)), PCBrick_Band(p));
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
