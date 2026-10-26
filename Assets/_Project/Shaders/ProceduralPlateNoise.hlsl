#ifndef PROCEDURAL_PLATE_NOISE_INCLUDED
#define PROCEDURAL_PLATE_NOISE_INCLUDED

// ProceduralPlateNoise — object-space noise для МЕТАЛЛИЧЕСКИХ ПЛАСТИН (Plate).
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим). Сигнатуры = порядок слотов CF-нод в графе. Новое поведение —
// новой функцией; существующие сигнатуры не менять (слоты связываются по номеру).
//
// ОСНОВА МЕТАЛЛА ВЗЯТА У БРОНЗЫ (ProceduralBronzeNoise.hlsl — read-only, не
// правится): микрошум листа (fbm + Worley), патина-пятно, три набора царапин и
// гасящая маска царапин. Механизмы те же, реализация своя с префиксом PCPlate_.
// НЕ взято: вертикальный bias патины (у бронзы он привязан к мировой оси),
// «износ до подложки» и маска кромок — у листа кромка это край меша, а не
// признак материала. Патина у пластин управляется тоном ПЛАСТИНЫ, а не осью.
//
// ГЛАВНОЕ ОТЛИЧИЕ ОТ БРОНЗЫ: поверхность разбита на ПЛАСТИНЫ регулярной сеткой
// (тот же приём, что кладка в Brick), а не свободным шумом. Сетка даёт:
//   * швы между пластинами (сварные валики; две ширины — вдоль и поперёк);
//   * тон КАЖДОЙ пластины — хеш ячейки ТОЙ ЖЕ сетки, поэтому тон и его границы
//     не могут разъехаться со швами;
//   * заклёпки в углах пластин (диски, вписанные в ту же сетку).
// Патина садится на пластины ПО-РАЗНОМУ (множитель по тону пластины) и жмётся
// к швам: влага держится на сварке. Так «пласты» читаются как отдельные листы,
// а не как один кусок металла.
//
// Слои:
//   ProceduralPlateSeams       — швы между пластинами (сетка + перевязка, две ширины)
//   ProceduralPlateTone        — тон каждой пластины (постоянный внутри ячейки)
//   ProceduralPlateRivets      — заклёпки в углах пластин (диски из той же сетки)
//   ProceduralPlateMetal       — микрошум листа (fbm + Worley) — механизм бронзы
//   ProceduralPlatePatina      — патина: широкие тёмные пятна + яркие (механизм бронзы)
//   ProceduralPlateScratchA/B/C — три набора царапин под углами (механизм бронзы)
//   ProceduralPlateScratchMask — гасящая маска царапин (механизм бронзы)
//
// Хеш без sin (Hoskins-style), warp гладкий (интерполированный). Всё считается из
// object-space координат (FO-safe): AbsoluteWorld не используется, при floating
// origin пластины не «плывут».
//
// РЕЛЬЕФ (см. docs/Materials/README.md, «Известная ошибка сборки»): пять видов
// неровностей — швы (валик ВВЕРХ), заклёпки (ВВЕРХ), патина (ВНИЗ), посадка
// пластин и микрошум листа — собираются суммой, у каждого вида свой коэффициент.
// Сила суммы задаётся свойством материала _Bump_Strength, ПОДКЛЮЧЁННЫМ к слоту
// Strength ноды Normal From Height. Оставлять слот Strength неподключённым
// нельзя: тогда в шейдер уходит шаблонный дефолт 0.01 и рельеф гасится в 100 раз.

float PCPlate_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCPlate_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCPlate_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCPlate_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCPlate_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCPlate_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCPlate_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCPlate_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCPlate_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCPlate_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

// ── Полоса пропускания (26.10, по образцу Cobble/Concrete/Wood/Glass/Iron) ─────────────
// Размер пикселя в периодах уже масштабированной координаты узора. Пиксель сцены ≈ 1.37 мм × дистанция,
// поэтому на дальнем плане шов/заклёпка/тон пластины/царапина мельче пикселя: в рельефе это случайный
// наклон в каждом блоке 2×2 → «шум при любом бампе». Слой с периодом ≤ ~1.8 пикс сводится к среднему,
// ≥ ~4.5 пикс отдаётся целиком. Пороги = значения пользователя из PCCobble_Band (эталон).
float PCPlate_Footprint(float3 p)
{
    return max(length(ddx(p)), length(ddy(p)));
}

#define PCPLATE_BAND_LO 0.22
#define PCPLATE_BAND_HI 0.55
float PCPlate_Band(float3 p)
{
    return 1.0 - smoothstep(PCPLATE_BAND_LO, PCPLATE_BAND_HI, PCPlate_Footprint(p));
}

float PCPlate_Fbm3(float3 p, float detail)
{
    // октава мельче пикселя плавно уходит в своё среднее (0.5)
    float px = PCPlate_Footprint(p);
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        float w = 1.0 - smoothstep(PCPLATE_BAND_LO, PCPLATE_BAND_HI, px * freq);
        sum += amp * lerp(0.5, PCPlate_ValueNoise3(p * freq), w);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// F1-расстояние до центра ячейки Worley (0 в центре). Механизм бронзы.
float PCPlate_WorleyF1(float3 p)
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
                    PCPlate_Hash13(cell + 0.0),
                    PCPlate_Hash13(cell + 17.0),
                    PCPlate_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return saturate(minD);
}

// Ridge: 1 на линии, где fbm проходит через 0.5 (тонкие линии царапин).
// Скруглённый гребень: |x| даёт излом наклона на гребне — в рельефе это смена знака нормали
// в одном пикселе и мерцающая линия; sqrt(x²+k) визуально тот же штрих, но гладкий.
float PCPlate_Ridge(float s)
{
    const float k = 0.0025;
    float x = s * 2.0 - 1.0;
    float a = sqrt(x * x + k) - sqrt(k);
    return 1.0 - a / (sqrt(1.0 + k) - sqrt(k));
}

// ── Оси проекции сетки ───────────────────────────────────────────────────────
// Сетка пластин плоская. Раньше она ВСЕГДА ложилась на локальные XY: q = (x, y·Aspect).
// У горизонтальной грани (палуба DECK_STEEL, пол, плоская кровля) Y не меняется вообще,
// поэтому рядовой шов не срабатывал ни разу, а тон листа менялся только по X — выходила
// одна бесконечная пластина, «тянуло по одной стороне». То же у грани с нормалью ±X:
// там постоянно X. UV к этому отношения не имеют — узор всегда считался из позиции.
//
// Оси берутся из САМОЙ ГРАНИ: нормаль = cross(ddx, ddy) позиции. На плоской грани
// производные позиции постоянны, значит и нормаль постоянна — оси НЕ переключаются
// внутри грани и не мерцают; меняются они только на ребре, где и так проходит граница
// деталей. Берутся именно производные, а не нормаль вершины: та же величина, но
// гарантированно постоянная в пределах грани и не зависящая от TBN.
//   T — горизонталь в плоскости грани, вдоль ряда;
//   B — вверх по грани, поперёк ряда. B лежит В ПЛОСКОСТИ грани, поэтому на 45° скате
//       ряды идут ровно вверх по скату, без растяжения в √2 (и без вырождения
//       трипланара, где проекции гасят друг друга).
// Оба вектора канонизированы по старшинству оси (доминирующая компонента > 0), чтобы
// соседние грани не получали зеркальную сетку. Для стен с нормалью ±Z это в точности
// прежние (x, y) — поведение на основном массиве стен не меняется.
void PCPlate_ProjAxes(float3 ObjectPos, out float3 T, out float3 B)
{
    float3 n = cross(ddx(ObjectPos), ddy(ObjectPos));
    float nl = length(n);
    // пиксель строго в ребро грани — вырожденный случай, остаётся прежняя плоскость XY
    n = (nl > 1e-12) ? n / nl : float3(0.0, 0.0, 1.0);

    float3 t = cross(n, float3(0.0, 1.0, 0.0));
    float tl = length(t);
    // у почти горизонтальной грани горизонталь в её плоскости не определена — берём X
    t = (tl > 0.05) ? t / tl : float3(1.0, 0.0, 0.0);

    if (abs(t.z) > abs(t.x)) { if (t.z < 0.0) t = -t; }
    else                     { if (t.x < 0.0) t = -t; }

    float3 b = cross(n, t);     // единичный: n и t единичные и перпендикулярны
    if (abs(b.y) >= max(abs(b.x), abs(b.z))) { if (b.y < 0.0) b = -b; }
    else if (abs(b.z) > abs(b.x))            { if (b.z < 0.0) b = -b; }
    else                                     { if (b.x < 0.0) b = -b; }

    T = t;
    B = b;
}

// ── Сетка пластин (общая для швов, тона и заклёпок) ──────────────────────────
// Возвращает координаты в «пластинах»: x — вдоль ряда, y — вверх по стенке.
//   PlateScale  — сколько пластин укладывается в метр ВДОЛЬ РЯДА;
//   PlateAspect — длина пластины к её высоте (2.0 = лист 2:1);
//   RowOffset   — сдвиг каждого следующего ряда (0.5 = перевязка в пол-листа);
//   SeamWarp    — насколько ведёт сетку. Сваренные листы ставят руками, поэтому
//                 шов не бывает идеально прямым; 0 = «наклеенные обои».
float2 PCPlate_Grid(float3 ObjectPos, float PlateScale, float PlateAspect, float RowOffset, float SeamWarp, float PlateSeed)
{
    float3 T, B;
    PCPlate_ProjAxes(ObjectPos, T, B);

    float3 p = ObjectPos * max(0.001, PlateScale) + PlateSeed;
    // проекция на плоскость ГРАНИ, а не на локальные XY (см. PCPlate_ProjAxes)
    float2 q = float2(dot(p, T), dot(p, B) * max(0.05, PlateAspect));
    if (SeamWarp > 0.0001)
    {
        float2 w = float2(
            PCPlate_ValueNoise3(p * 0.6 + 5.3),
            PCPlate_ValueNoise3(p * 0.6 + 19.7)) - 0.5;
        q += w * SeamWarp;
    }
    float row = floor(q.y);
    q.x += row * RowOffset;
    return q;
}

// Координаты сетки ДО сдвига рядов — для полосы пропускания. Сдвиг ряда даёт скачок производной
// на границе ряда, поэтому считать px по уже сдвинутой q нельзя. Сид на производные не влияет.
float3 PCPlate_GridSpace(float3 ObjectPos, float PlateScale, float PlateAspect)
{
    float3 T, B;
    PCPlate_ProjAxes(ObjectPos, T, B);
    float3 p = ObjectPos * max(0.001, PlateScale);
    // оси те же, что у сетки, иначе полоса посчитает шаг пикселя не в той плоскости.
    // Третья компонента — 0: сетка строго двумерна, мерятся только её две оси.
    return float3(dot(p, T), dot(p, B) * max(0.05, PlateAspect), 0.0);
}

// ── Швы между пластинами ─────────────────────────────────────────────────────
// Шов — полоса у границы ячейки. Две ширины, как у кладки: у листа 2:1 шов одной
// толщины даёт вдвое разную долю по длинной и по короткой стороне.
//
// МЕЛЬТЕШЕНИЕ ШВА (26.10). Шов — ступенька высоты шириной SeamWidth в долях ЯЧЕЙКИ
// (0.03/0.06), то есть 13–34 мм у SMALL_M_PC_Plate. На 10 м пиксель ≈ 13.7 мм, и шов
// становится уже пикселя задолго до того, как перестаёт разрешаться САМА пластина
// (метр). Normal From Height читает не наклон, а разность соседних пикселей, поэтому
// на этом участке шов — не рельеф, а случайное число в каждом блоке 2×2.
// Плата за полосу по периоду ячейки (PCPlate_Band) тут не спасает: она включается на
// px ≈ 0.22 ячейки, то есть в 4–7 раз позже, чем шов перестаёт разрешаться.
// Лечение: полоса считается по ширине САМОГО шва, а стенка шва не даётся уже двух
// пикселей — шов на дистанции становится мягче, но не мельтешит.
float PCPlate_Seams(float3 ObjectPos, float PlateScale, float PlateAspect, float RowOffset, float SeamWarp, float SeamWidthX, float SeamWidthY, float PlateSeed)
{
    float2 q = PCPlate_Grid(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, PlateSeed);
    float2 f = frac(q);
    // расстояние до ближайшей границы пластины: 0 на границе, 0.5 в центре
    float2 d = min(f, 1.0 - f);

    // шаг пикселя в координатах сетки. PCPlate_GridSpace — сетка ДО сдвига рядов:
    // сдвиг даёт скачок производной на границе ряда, по сдвинутой q считать нельзя.
    float3 gridSpace = PCPlate_GridSpace(ObjectPos, PlateScale, PlateAspect);
    float2 px = max(abs(ddx(gridSpace.xy)), abs(ddy(gridSpace.xy)));

    float2 w0 = float2(max(0.004, SeamWidthX), max(0.004, SeamWidthY));
    // стенка шва не уже двух пикселей
    float2 w = max(w0, px * 2.0);
    float mask = saturate(1.0 - smoothstep(0.0, 1.0, min(d.x / w.x, d.y / w.y)));

    // полоса по ширине шва: ratio = пиксель / шов, 1.0 — шов ровно в пиксель
    float ratio = max(px.x / w0.x, px.y / w0.y);
    float band = 1.0 - smoothstep(0.35, 1.0, ratio);

    // неразрешённый шов сходится не в 0, а в свою долю площади: две полосы по осям
    float coverage = saturate(w0.x + w0.y - w0.x * w0.y);
    return lerp(coverage, mask, band);
}

// ── Тон пластины ─────────────────────────────────────────────────────────────
// Величина, постоянная внутри листа и скачком меняющаяся на его границе. Именно
// она (а не патина) даёт «набрано из разных листов»: обшивка собирается из того,
// что было под рукой. Считается той же сеткой, что и швы, поэтому совпадающие
// Scale/Aspect/Offset/Warp/Seed держат тон и швы в одной сетке.
float PCPlate_Tone(float3 ObjectPos, float PlateScale, float PlateAspect, float RowOffset, float SeamWarp, float PlateSeed)
{
    float2 q = PCPlate_Grid(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, PlateSeed);
    float2 cell = floor(q);
    // тон постоянен внутри листа и скачет на его границе: мельче пикселя → среднее 0.5
    return lerp(0.5, saturate(PCPlate_Hash13(float3(cell, PlateSeed + 11.0))),
                PCPlate_Band(PCPlate_GridSpace(ObjectPos, PlateScale, PlateAspect)));
}

// ── Заклёпки ─────────────────────────────────────────────────────────────────
// Диск, вписанный в угол каждой пластины, со сдвигом ВНУТРЬ листа (RivetInset),
// поэтому заклёпка не тонет в сварном валике. Метрика анизотропная: делим
// смещение по Y на PlateAspect, иначе на вытянутом листе заклёпка вышла бы
// овалом. RivetWidth/RivetInset заданы в долях ДЛИНЫ пластины, поэтому радиус
// заклёпки в метрах = RivetWidth / PlateScale.
float PCPlate_Rivets(float3 ObjectPos, float PlateScale, float PlateAspect, float RowOffset, float SeamWarp, float RivetWidth, float RivetInset, float RivetSeed)
{
    float2 q = PCPlate_Grid(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, RivetSeed);
    float2 f = frac(q);
    float aspect = max(0.05, PlateAspect);
    float inset = max(0.0, RivetInset);
    // смещение от ближайшего угла ячейки, приведённое к «единой» метрике
    float2 rel = float2(abs(f.x - round(f.x)) - inset,
                        abs(f.y - round(f.y)) - inset * aspect);
    float dist = length(float2(rel.x, rel.y / aspect));
    float w = max(0.004, RivetWidth);
    // диск заклёпки мельче пикселя → среднее покрытие ≈ 0
    return saturate(1.0 - smoothstep(w * 0.35, w, dist)) * PCPlate_Band(PCPlate_GridSpace(ObjectPos, PlateScale, PlateAspect));
}

// ── Микрошум листа (механизм бронзы PCBronze_Base) ───────────────────────────
// fbm, смешанный с Worley: неровность проката и локальные вмятины. Патина
// ложится по этому же рисунку, поэтому он же задаёт «зернистость» коррозии.
float PCPlate_Metal(float3 ObjectPos, float MetalScale, float MetalDetail, float MetalWarp, float MetalSeed)
{
    float3 p = ObjectPos * max(0.001, MetalScale) + MetalSeed;
    float3 w = float3(
        PCPlate_ValueNoise3(p * 0.5 + 11.3),
        PCPlate_ValueNoise3(p * 0.5 + 27.1),
        PCPlate_ValueNoise3(p * 0.5 + 43.7)) - 0.5;
    p += w * MetalWarp;
    float f = PCPlate_Fbm3(p, MetalDetail);
    float v = PCPlate_WorleyF1(p);
    // на неразрешённом узоре тон уходит в среднее, а не мерцает пятнами
    return saturate(lerp(0.5, lerp(f, v, 0.30), PCPlate_Band(p)));
}

// ── Патина (механизм бронзы PCBronze_Blotch + растяжка) ──────────────────────
// Крупные низкочастотные области. PatinaStretch > 1 вытягивает пятна вдоль
// ЛОКАЛЬНОЙ Y: коррозия стекает вниз, а не висит круглыми кляксами.
float PCPlate_Patina(float3 ObjectPos, float PatinaScale, float PatinaStretch, float PatinaSeed)
{
    float3 p = ObjectPos * max(0.001, PatinaScale) + PatinaSeed;
    p.y /= max(0.05, PatinaStretch);
    return lerp(0.5, saturate(PCPlate_Fbm3(p, 2.0)), PCPlate_Band(p));
}

// ── Царапины (механизм бронзы) ───────────────────────────────────────────────
// Сжатие осей задаёт направление полос: SX/SY/SZ — во сколько раз сжать по оси.
float PCPlate_ScratchDir(float3 ObjectPos, float ScratchScale, float SX, float SY, float SZ, float ScratchDetail, float ScratchSeed)
{
    float3 sp = ObjectPos * max(0.001, ScratchScale) + ScratchSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    // среднее гребня = 0.5 (ниже порога царапин в графе) — линия плавно исчезает на дистанции
    return lerp(0.5, PCPlate_Ridge(PCPlate_Fbm3(sp, ScratchDetail)), PCPlate_Band(sp));
}

// Пятна гасящей маски царапин: тот же примитив, что патина, но без растяжки.
float PCPlate_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return lerp(0.5, saturate(PCPlate_Fbm3(pp, 2.0)), PCPlate_Band(pp));
}

// ── Обёртки CF-нод (одно-выходные, File-режим) ───────────────────────────────

void ProceduralPlateSeams_float(
    float3 ObjectPos,
    float PlateScale, float PlateAspect, float RowOffset, float SeamWarp,
    float SeamWidthX, float SeamWidthY, float PlateSeed,
    out float Seams)
{
    Seams = PCPlate_Seams(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, SeamWidthX, SeamWidthY, PlateSeed);
}

void ProceduralPlateSeams_half(
    half3 ObjectPos,
    half PlateScale, half PlateAspect, half RowOffset, half SeamWarp,
    half SeamWidthX, half SeamWidthY, half PlateSeed,
    out half Seams)
{
    float s;
    ProceduralPlateSeams_float(
        (float3)ObjectPos,
        (float)PlateScale, (float)PlateAspect, (float)RowOffset, (float)SeamWarp,
        (float)SeamWidthX, (float)SeamWidthY, (float)PlateSeed, s);
    Seams = (half)s;
}

void ProceduralPlateTone_float(
    float3 ObjectPos,
    float PlateScale, float PlateAspect, float RowOffset, float SeamWarp, float PlateSeed,
    out float PlateTone)
{
    PlateTone = PCPlate_Tone(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, PlateSeed);
}

void ProceduralPlateTone_half(
    half3 ObjectPos,
    half PlateScale, half PlateAspect, half RowOffset, half SeamWarp, half PlateSeed,
    out half PlateTone)
{
    float t;
    ProceduralPlateTone_float(
        (float3)ObjectPos,
        (float)PlateScale, (float)PlateAspect, (float)RowOffset, (float)SeamWarp, (float)PlateSeed, t);
    PlateTone = (half)t;
}

void ProceduralPlateRivets_float(
    float3 ObjectPos,
    float PlateScale, float PlateAspect, float RowOffset, float SeamWarp,
    float RivetWidth, float RivetInset, float RivetSeed,
    out float Rivets)
{
    Rivets = PCPlate_Rivets(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, RivetWidth, RivetInset, RivetSeed);
}

void ProceduralPlateRivets_half(
    half3 ObjectPos,
    half PlateScale, half PlateAspect, half RowOffset, half SeamWarp,
    half RivetWidth, half RivetInset, half RivetSeed,
    out half Rivets)
{
    float r;
    ProceduralPlateRivets_float(
        (float3)ObjectPos,
        (float)PlateScale, (float)PlateAspect, (float)RowOffset, (float)SeamWarp,
        (float)RivetWidth, (float)RivetInset, (float)RivetSeed, r);
    Rivets = (half)r;
}

void ProceduralPlateMetal_float(
    float3 ObjectPos,
    float MetalScale, float MetalDetail, float MetalWarp, float MetalSeed,
    out float Metal)
{
    Metal = PCPlate_Metal(ObjectPos, MetalScale, MetalDetail, MetalWarp, MetalSeed);
}

void ProceduralPlateMetal_half(
    half3 ObjectPos,
    half MetalScale, half MetalDetail, half MetalWarp, half MetalSeed,
    out half Metal)
{
    float m;
    ProceduralPlateMetal_float(
        (float3)ObjectPos,
        (float)MetalScale, (float)MetalDetail, (float)MetalWarp, (float)MetalSeed, m);
    Metal = (half)m;
}

void ProceduralPlatePatina_float(
    float3 ObjectPos,
    float PatinaScale, float PatinaStretch, float PatinaSeed,
    out float Patina)
{
    Patina = PCPlate_Patina(ObjectPos, PatinaScale, PatinaStretch, PatinaSeed);
}

void ProceduralPlatePatina_half(
    half3 ObjectPos,
    half PatinaScale, half PatinaStretch, half PatinaSeed,
    out half Patina)
{
    float t;
    ProceduralPlatePatina_float((float3)ObjectPos, (float)PatinaScale, (float)PatinaStretch, (float)PatinaSeed, t);
    Patina = (half)t;
}

void ProceduralPlateScratchA_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float Scratch)
{
    Scratch = PCPlate_ScratchDir(ObjectPos, ScratchScale, ScratchStretch, 1.0, ScratchStretch, ScratchDetail, ScratchSeed);
}

void ProceduralPlateScratchA_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half Scratch)
{
    float s;
    ProceduralPlateScratchA_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, s);
    Scratch = (half)s;
}

void ProceduralPlateScratchB_float(
    float3 ObjectPos,
    float ScratchScale, float ScratchStretch, float ScratchDetail, float ScratchSeed,
    out float ScratchB)
{
    ScratchB = PCPlate_ScratchDir(ObjectPos, ScratchScale, 1.0, ScratchStretch, 1.0, ScratchDetail, ScratchSeed + 17.3);
}

void ProceduralPlateScratchB_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    out half ScratchB)
{
    float b;
    ProceduralPlateScratchB_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed, b);
    ScratchB = (half)b;
}

void ProceduralPlateScratchC_float(
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
    ScratchC = lerp(0.5, PCPlate_Ridge(PCPlate_Fbm3(sc, ScratchDetail)), PCPlate_Band(sc));
}

void ProceduralPlateScratchC_half(
    half3 ObjectPos,
    half ScratchScale, half ScratchStretch, half ScratchDetail, half ScratchSeed,
    half ScratchAngle,
    out half ScratchC)
{
    float c;
    ProceduralPlateScratchC_float(
        (float3)ObjectPos,
        (float)ScratchScale, (float)ScratchStretch, (float)ScratchDetail, (float)ScratchSeed,
        (float)ScratchAngle, c);
    ScratchC = (half)c;
}

void ProceduralPlateScratchMask_float(
    float3 ObjectPos,
    float ScratchMaskScale, float ScratchMaskSeed,
    out float ScratchMask)
{
    ScratchMask = PCPlate_Blotch(ObjectPos, ScratchMaskScale, ScratchMaskSeed);
}

void ProceduralPlateScratchMask_half(
    half3 ObjectPos,
    half ScratchMaskScale, half ScratchMaskSeed,
    out half ScratchMask)
{
    float m;
    ProceduralPlateScratchMask_float((float3)ObjectPos, (float)ScratchMaskScale, (float)ScratchMaskSeed, m);
    ScratchMask = (half)m;
}

#endif
