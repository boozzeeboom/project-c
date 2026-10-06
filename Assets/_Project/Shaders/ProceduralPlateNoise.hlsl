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

float PCPlate_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCPlate_ValueNoise3(p * freq);
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

// Ridge: 1 на линии, где fbm проходит через 0.5 (тонкие линии царапин). Механизм бронзы.
float PCPlate_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
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
    float3 p = ObjectPos * max(0.001, PlateScale) + PlateSeed;
    float2 q = float2(p.x, p.y * max(0.05, PlateAspect));
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

// ── Швы между пластинами ─────────────────────────────────────────────────────
// Шов — полоса у границы ячейки. Две ширины, как у кладки: у листа 2:1 шов одной
// толщины даёт вдвое разную долю по длинной и по короткой стороне.
float PCPlate_Seams(float3 ObjectPos, float PlateScale, float PlateAspect, float RowOffset, float SeamWarp, float SeamWidthX, float SeamWidthY, float PlateSeed)
{
    float2 q = PCPlate_Grid(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, PlateSeed);
    float2 f = frac(q);
    // расстояние до ближайшей границы пластины: 0 на границе, 0.5 в центре
    float2 d = min(f, 1.0 - f);
    float edge = min(d.x / max(0.004, SeamWidthX), d.y / max(0.004, SeamWidthY));
    return saturate(1.0 - smoothstep(0.0, 1.0, edge));
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
    return saturate(PCPlate_Hash13(float3(cell, PlateSeed + 11.0)));
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
    return saturate(1.0 - smoothstep(w * 0.35, w, dist));
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
    return saturate(lerp(f, v, 0.30));
}

// ── Патина (механизм бронзы PCBronze_Blotch + растяжка) ──────────────────────
// Крупные низкочастотные области. PatinaStretch > 1 вытягивает пятна вдоль
// ЛОКАЛЬНОЙ Y: коррозия стекает вниз, а не висит круглыми кляксами.
float PCPlate_Patina(float3 ObjectPos, float PatinaScale, float PatinaStretch, float PatinaSeed)
{
    float3 p = ObjectPos * max(0.001, PatinaScale) + PatinaSeed;
    p.y /= max(0.05, PatinaStretch);
    return saturate(PCPlate_Fbm3(p, 2.0));
}

// ── Царапины (механизм бронзы) ───────────────────────────────────────────────
// Сжатие осей задаёт направление полос: SX/SY/SZ — во сколько раз сжать по оси.
float PCPlate_ScratchDir(float3 ObjectPos, float ScratchScale, float SX, float SY, float SZ, float ScratchDetail, float ScratchSeed)
{
    float3 sp = ObjectPos * max(0.001, ScratchScale) + ScratchSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    return PCPlate_Ridge(PCPlate_Fbm3(sp, ScratchDetail));
}

// Пятна гасящей маски царапин: тот же примитив, что патина, но без растяжки.
float PCPlate_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return saturate(PCPlate_Fbm3(pp, 2.0));
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
    ScratchC = PCPlate_Ridge(PCPlate_Fbm3(sc, ScratchDetail));
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
