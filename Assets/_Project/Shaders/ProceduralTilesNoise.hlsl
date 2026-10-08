#ifndef PROCEDURAL_TILES_NOISE_INCLUDED
#define PROCEDURAL_TILES_NOISE_INCLUDED

// ProceduralTilesNoise — object-space шум РАСКЛАДКИ ЧЕРЕПИЦЫ (кровля: скаты,
// навесы, навершия башен, козырьки). Материал Tiles — производная от Brick.
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим). Сигнатуры = порядок слотов CF-нод в графе. Новое поведение —
// новой функцией; существующие сигнатуры не менять (слоты связываются по номеру).
//
// КАДР (как у кирпича, не как у паркета). Скат — это объект в локальной системе:
//   X — вдоль ряда (поперёк ската, вдоль конька),
//   Y — ВВЕРХ ПО СКАТУ,
//   Z — наружу из кровли (толщина).
// Оси объявлены, а не выводятся из нормали: у наклонного ската нормаль
// диагональная, и выбор плоскости по нормали (приём Parquet) на 45° кровле
// переключался бы по площади грани. Двускатная кровля — два объекта (или один
// меш, где второй скат зеркален по X); конёк — отдельная деталь, здесь его нет.
//
// ГЛАВНОЕ ОТЛИЧИЕ ОТ КИРПИЧА: граница ряда — не прямая, а ДУГА («носок»
// черепицы), и ряды перевязаны в пол-тайла. Поэтому шов ряда считается как
// расстояние до кривой: down = 1 - frac(q.y) — сколько вниз по скату от верхней
// границы клетки, а сама кромка лежит на down = Tail * arc(fx). Дуга гасится к
// боковым кромкам тайла, поэтому кромка ряда получается волнистой (чешуйчатой),
// а не прямой. При Tail = 0 дуга вырождается в прямую и раскладка становится
// кирпичной — это осознанное вырождение, а не отдельный режим.
//
// Слои:
//   ProceduralTilesJoints  — швы: боковой желобок между черепицами ряда и
//                            дугообразная кромка «носка» верхнего ряда
//   ProceduralTilesTone    — тон черепицы (хеш ячейки, постоянный внутри тайла)
//   ProceduralTilesCrown   — поперечная арка тайла (0 у кромок, 1 в центре):
//                            из неё и волнистость ряда, и подъём в рельефе
//   ProceduralTilesGrains  — зерно: песок на лице тайла и крупа теста в зазоре
//   ProceduralTilesPores   — поры и раковины обжига (Worley F1, свой радиус)
//   ProceduralTilesCrazing — кракле глазури: сеть тонких борозд по поверхности
//   ProceduralTilesPatch   — общий примитив «пятно fbm»: высолы и мох-потёки
//                            (порог ставится в графе — один рисунок, две роли)
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe):
// AbsoluteWorld не используется, при floating origin кровля не «плывёт».
//
// РЕЛЬЕФ (см. docs/Materials/README.md, «Известная ошибка сборки»): четыре вида
// неровностей — швы, поры, кракле и поперечная арка (Crown) — собираются суммой с
// индивидуальным коэффициентом у каждого вида. Сила суммы задаётся свойством
// материала _Bump_Strength, ПОДКЛЮЧЁННЫМ к слоту Strength ноды Normal From
// Height. Оставлять слот Strength неподключённым нельзя: тогда в шейдер уходит
// шаблонный дефолт 0.01 и рельеф гасится в 100 раз.

float PCTile_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCTile_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCTile_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCTile_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCTile_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCTile_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCTile_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCTile_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCTile_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCTile_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCTile_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCTile_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Борозда = РАССТОЯНИЕ до «линии» fbm (там, где fbm проходит через 0.5):
// 0 на самой линии, 1 в стороне от неё. Нужно именно расстояние, а не обратная
// величина (1 − |2s − 1| в Ridge), потому что ширина борозды задаётся отдельным
// параметром Width.
float PCTile_Groove(float s)
{
    return abs(s * 2.0 - 1.0);
}

// Поперечный профиль тайла: 1 в центре, 0 у боковых кромок (полудуга).
// Одна и та же функция задаёт и форму «носка» (кромка ряда), и арку сечения:
// у черепицы это один и тот же профиль, повёрнутый на 90°.
float PCTile_Arc(float fx)
{
    float c = fx * 2.0 - 1.0;
    return sqrt(max(0.0, 1.0 - c * c));
}

// ── Сетка черепицы (общая для швов, тона и арки) ──────────────────────────────
// Возвращает координаты в «тайлах»: x — вдоль ряда, y — вверх по скату.
//   TileScale   — сколько черепиц укладывается в единицу ВДОЛЬ РЯДА;
//   TileAspect  — ширина черепицы к её ВИДИМОЙ высоте (1.28 ≈ тайл 0.32 x 0.25);
//   RowOffset   — сдвиг каждого следующего ряда (0.5 = перевязка в пол-тайла);
//   TileWarp    — насколько ведёт сетку (ручная укладка: прямых рядов не бывает).
// Масштаб по Y умножается на Aspect: без этого тайл вышел бы квадратным,
// потому что координата Y мерится той же линейкой, что и X.
float2 PCTile_Grid(float3 ObjectPos, float TileScale, float TileAspect, float RowOffset, float TileWarp, float TileSeed)
{
    float3 p = ObjectPos * max(0.001, TileScale) + TileSeed;
    float2 q = float2(p.x, p.y * max(0.05, TileAspect));
    if (TileWarp > 0.0001)
    {
        float2 w = float2(
            PCTile_ValueNoise3(p * 0.6 + 5.3),
            PCTile_ValueNoise3(p * 0.6 + 19.7)) - 0.5;
        q += w * TileWarp;
    }
    float row = floor(q.y);
    q.x += row * RowOffset;
    return q;
}

// Локальная X ТОЙ черепицы, чей «носок» лежит над этой клеткой: у верхнего ряда
// своя перевязка, поэтому в полосе перекрытия видна не своя черепица, а верхняя.
float PCTile_UpperX(float2 f, float RowOffset)
{
    return frac(f.x + max(0.0, RowOffset));
}

// Перекрыта ли точка «носком» верхнего ряда. Верхняя черепица нависает вниз по
// скату на глубину Tail * arc — на глубину паза, а не на всю клетку, и именно
// дуга делает кромку волнистой. 1 = точка принадлежит верхней черепице.
float PCTile_Covered(float2 f, float RowOffset, float TileTail)
{
    float down = 1.0 - f.y;
    return step(down, max(0.0, TileTail) * PCTile_Arc(PCTile_UpperX(f, RowOffset)));
}

// ── Швы ──────────────────────────────────────────────────────────────────────
// Шов — полоса у границы тайла, и границ две разных:
//   боковая (по X) — прямая: черепицы ряда стыкуются впритык, желобок узкий;
//   рядовая (по Y) — ДУГА: видимая кромка принадлежит верхнему ряду и повторяет
//       форму его «носка», поэтому ширина шва по Y задаётся отдельным параметром.
// Одна общая ширина сделала бы либо желобок вдвое шире кромки, либо наоборот.
void ProceduralTilesJoints_float(
    float3 ObjectPos,
    float TileScale, float TileAspect, float RowOffset, float TileWarp,
    float JointWidthX, float JointWidthY, float TileSeed, float TileTail,
    out float Joints)
{
    float2 q = PCTile_Grid(ObjectPos, TileScale, TileAspect, RowOffset, TileWarp, TileSeed);
    float2 f = frac(q);
    // расстояние до ближайшей боковой границы тайла: 0 на границе, 0.5 в центре
    float2 d = min(f, 1.0 - f);
    float side = d.x / max(0.004, JointWidthX);

    float down = 1.0 - f.y;
    float rowd = abs(down - max(0.0, TileTail) * PCTile_Arc(PCTile_UpperX(f, RowOffset)))
        / max(0.004, JointWidthY);

    Joints = saturate(1.0 - smoothstep(0.0, 1.0, min(side, rowd)));
}

// ── Тон черепицы ─────────────────────────────────────────────────────────────
// Величина, постоянная внутри тайла и скачком меняющаяся на его границе. У
// обожжённой глины разброс огромный (пережог/недожог, разная партия), поэтому
// именно тон, а не поры, даёт «кровля набрана из разной черепицы».
// Считается той же сеткой, что и швы, поэтому при совпадающих параметрах тон и
// его границы лежат в одной сетке — разъехаться они не могут. В полосе
// перекрытия тон берётся у ВЕРХНЕЙ черепицы: иначе скачок тона пришёлся бы на
// прямую границу клетки посреди видимой поверхности тайла.
void ProceduralTilesTone_float(
    float3 ObjectPos,
    float TileScale, float TileAspect, float RowOffset, float TileWarp, float TileSeed, float TileTail,
    out float TileTone)
{
    float2 q = PCTile_Grid(ObjectPos, TileScale, TileAspect, RowOffset, TileWarp, TileSeed);
    float2 f = frac(q);
    float2 cell = floor(q);
    if (PCTile_Covered(f, RowOffset, TileTail) > 0.5)
        cell = floor(float2(q.x + max(0.0, RowOffset), q.y + 1.0));
    TileTone = saturate(PCTile_Hash13(float3(cell, TileSeed + 11.0)));
}

// ── Поперечная арка тайла ────────────────────────────────────────────────────
// 0 у боковых кромок, 1 по центру. Даёт волнистость ряда и подъём тайла в
// рельефе (в графе уходит в множитель подъёма вместо константы). Считается у ТОЙ
// черепицы, которая видна: в полосе перекрытия это верхний ряд со своим сдвигом,
// иначе арка ломалась бы по прямой границе клетки вместо того, чтобы следовать
// волнистой кромке.
void ProceduralTilesCrown_float(
    float3 ObjectPos,
    float TileScale, float TileAspect, float RowOffset, float TileWarp, float TileSeed, float TileTail,
    out float TileCrown)
{
    float2 q = PCTile_Grid(ObjectPos, TileScale, TileAspect, RowOffset, TileWarp, TileSeed);
    float2 f = frac(q);
    float cov = PCTile_Covered(f, RowOffset, TileTail);
    float fx = lerp(f.x, PCTile_UpperX(f, RowOffset), cov);
    TileCrown = PCTile_Arc(fx);
}

// ── Зерно ────────────────────────────────────────────────────────────────────
// Один примитив, два масштаба: песок на лице тайла (вытянут вдоль формы) и крупа
// глиняного теста в зазоре (изотропная деталь, без растяжения).
// GrainStretch > 1 вытягивает зерно вдоль локальной X.
//
// ВАЖНО про 0. Неподключённый вход CF-ноды уходит в шейдер нулём (таково
// сериализованное значение слота), поэтому Stretch защищён снизу ЕДИНИЦЕЙ, а не
// 0.05: у кирпичной стены защита 0.05 превращала неподключённый вход в
// двадцатикратное растяжение, то есть в смазанные полосы вместо изотропного
// зерна. Здесь 0 честно означает «изотропно».
void ProceduralTilesGrains_float(
    float3 ObjectPos,
    float GrainScale, float GrainStretch, float GrainSeed,
    out float Grains)
{
    float3 p = ObjectPos * max(0.001, GrainScale) + GrainSeed;
    p.x /= max(1.0, GrainStretch);
    Grains = saturate(PCTile_Fbm3(p, 2.0));
}

// ── Поры и раковины обжига ───────────────────────────────────────────────────
// F1-расстояние до центра ячейки Worley: 0 в центре поры. Радиус у каждой ячейки
// свой, и часть ячеек остаётся без поры совсем — иначе поверхность читается
// ситом, а у черепицы поры редкие и разного размера.
void ProceduralTilesPores_float(
    float3 ObjectPos,
    float PoreScale, float PoreWidth, float PoreSeed,
    out float Pores)
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
                    PCTile_Hash13(cell),
                    PCTile_Hash13(cell + 17.0),
                    PCTile_Hash13(cell + 31.0));
                float d = length(cell + o - p);
                if (d < f1)
                {
                    f1 = d;
                    rnd = PCTile_Hash13(cell + 53.0);
                }
            }
        }
    }
    float w = max(0.004, PoreWidth);
    float radius = w * lerp(0.35, 1.0, rnd);
    // smoothstep от 0.22: примерно треть ячеек без поры
    float exists = smoothstep(0.22, 0.42, PCTile_Hash13(g + 71.0));
    Pores = saturate(1.0 - smoothstep(radius * 0.40, radius, f1)) * exists;
}

// ── Кракле глазури ───────────────────────────────────────────────────────────
// Сеть тонких трещин глазури (цек) — борозда = расстояние до линии fbm. Сетка
// слегка растянута вдоль ската (CrazeStretch), домен-warp её изгибает, а крупный
// fbm решает, ГДЕ трещина раскрылась: сплошная сеть по всей кровле читалась бы
// как шум. Раскрытие модулирует глубину, но не обрывает трещину насовсем
// (нижняя граница 0.35): цек по глазури — это именно непрерывная сеть.
void ProceduralTilesCrazing_float(
    float3 ObjectPos,
    float CrazeScale, float CrazeStretch, float CrazeWidth, float CrazeWarp, float CrazeSeed,
    out float Crazing)
{
    float3 p = ObjectPos * max(0.001, CrazeScale) + CrazeSeed;
    p.y /= max(1.0, CrazeStretch);
    if (CrazeWarp > 0.0001)
    {
        float3 w = float3(
            PCTile_ValueNoise3(p * 0.5 + 3.1),
            PCTile_ValueNoise3(p * 0.5 + 17.9),
            PCTile_ValueNoise3(p * 0.5 + 41.7)) - 0.5;
        p += w * CrazeWarp;
    }
    float d = PCTile_Groove(PCTile_Fbm3(p, 2.0));
    float w = max(0.004, CrazeWidth);
    float craze = saturate(1.0 - smoothstep(0.0, w, d));
    float opened = smoothstep(0.30, 0.80, PCTile_Fbm3(p * 0.30 + 7.7, 2.0));
    Crazing = saturate(craze * lerp(0.35, 1.0, opened));
}

// ── Пятно (общий примитив) ───────────────────────────────────────────────────
// fbm в 0..1 без порогов: порог ставится в графе (Smoothstep), потому что один
// и тот же рисунок используется в двух ролях с разной полярностью.
//   PatchStretch = 1  -> изотропные пятна (высолы)
//   PatchStretch > 1  -> рисунок вытянут вдоль ЛОКАЛЬНОЙ Y, то есть ВНИЗ ПО СКАТУ
//                        (мох и грязь тянутся по потёку)
// Защита снизу единицей, а не 0.05: неподключённый вход приходит нулём и должен
// означать «изотропно», а не предельное растяжение (см. ProceduralTilesGrains).
void ProceduralTilesPatch_float(
    float3 ObjectPos,
    float PatchScale, float PatchStretch, float PatchSeed,
    out float Patch)
{
    float3 p = ObjectPos * max(0.001, PatchScale) + PatchSeed;
    p.y /= max(1.0, PatchStretch);
    Patch = saturate(PCTile_Fbm3(p, 2.0));
}

// ── Обёртки CF-нод (одно-выходные, File-режим) ───────────────────────────────

void ProceduralTilesJoints_half(
    half3 ObjectPos,
    half TileScale, half TileAspect, half RowOffset, half TileWarp,
    half JointWidthX, half JointWidthY, half TileSeed, half TileTail,
    out half Joints)
{
    float j;
    ProceduralTilesJoints_float(
        (float3)ObjectPos,
        (float)TileScale, (float)TileAspect, (float)RowOffset, (float)TileWarp,
        (float)JointWidthX, (float)JointWidthY, (float)TileSeed, (float)TileTail, j);
    Joints = (half)j;
}

void ProceduralTilesTone_half(
    half3 ObjectPos,
    half TileScale, half TileAspect, half RowOffset, half TileWarp, half TileSeed, half TileTail,
    out half TileTone)
{
    float t;
    ProceduralTilesTone_float(
        (float3)ObjectPos,
        (float)TileScale, (float)TileAspect, (float)RowOffset, (float)TileWarp, (float)TileSeed, (float)TileTail, t);
    TileTone = (half)t;
}

void ProceduralTilesCrown_half(
    half3 ObjectPos,
    half TileScale, half TileAspect, half RowOffset, half TileWarp, half TileSeed, half TileTail,
    out half TileCrown)
{
    float c;
    ProceduralTilesCrown_float(
        (float3)ObjectPos,
        (float)TileScale, (float)TileAspect, (float)RowOffset, (float)TileWarp, (float)TileSeed, (float)TileTail, c);
    TileCrown = (half)c;
}

void ProceduralTilesGrains_half(
    half3 ObjectPos,
    half GrainScale, half GrainStretch, half GrainSeed,
    out half Grains)
{
    float g;
    ProceduralTilesGrains_float((float3)ObjectPos, (float)GrainScale, (float)GrainStretch, (float)GrainSeed, g);
    Grains = (half)g;
}

void ProceduralTilesPores_half(
    half3 ObjectPos,
    half PoreScale, half PoreWidth, half PoreSeed,
    out half Pores)
{
    float v;
    ProceduralTilesPores_float((float3)ObjectPos, (float)PoreScale, (float)PoreWidth, (float)PoreSeed, v);
    Pores = (half)v;
}

void ProceduralTilesCrazing_half(
    half3 ObjectPos,
    half CrazeScale, half CrazeStretch, half CrazeWidth, half CrazeWarp, half CrazeSeed,
    out half Crazing)
{
    float c;
    ProceduralTilesCrazing_float(
        (float3)ObjectPos,
        (float)CrazeScale, (float)CrazeStretch, (float)CrazeWidth, (float)CrazeWarp, (float)CrazeSeed, c);
    Crazing = (half)c;
}

void ProceduralTilesPatch_half(
    half3 ObjectPos,
    half PatchScale, half PatchStretch, half PatchSeed,
    out half Patch)
{
    float b;
    ProceduralTilesPatch_float((float3)ObjectPos, (float)PatchScale, (float)PatchStretch, (float)PatchSeed, b);
    Patch = (half)b;
}

#endif
