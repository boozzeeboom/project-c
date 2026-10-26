#ifndef PROCEDURAL_LANTERN_NOISE_INCLUDED
#define PROCEDURAL_LANTERN_NOISE_INCLUDED

// ProceduralLanternNoise — object-space 3D шум ФОНАРЯ: газовый фонарь (стекло,
// копоть, раскалённое поле, ядро пламени, стыки стёкол). Материал Lantern
// собран с нуля: у Brick взяты ТОЛЬКО шаблоны документов графа, код ни от
// одного материала не наследуется.
//
// КАДР. Фонарь читается тем, что светит изнутри: стекло тёмное и закопчённое, а
// за ним дрожит жар. Поэтому слои делятся на «стекло» (копоть, стыки) и «пламя»
// (жар, ядро). Альбедо и ЭМИССИЯ берут одни и те же поля, но с разных сторон:
// альбедо показывает, как стекло темнеет от копоти и подгорает, а эмиссия из
// альбедо НЕ выводится (у лампы своя палитра света) — поэтому копоть гасит
// свечение только через тон стекла, а не умножением.
// Порядок наложения — от старого к новому: стекло → копоть → раскалённое поле
// → ядро → стыки.
//
//   Локальная Y — ВВЕРХ, и вдоль неё же ВВЕРХ поднимается рисунок жара (жар
//   идёт вверх — в отличие от лавы, которая течёт вниз).
//   Один объект = один фонарь (его стекло). Металлический каркас — отдельный
//   объект и отдельный материал (Plate / IronRust). UV не нужны.
//
// Слои:
//   ProceduralLanternGlow — крупное поле жара: где стекло раскалено
//   ProceduralLanternCore — тонкие нити («хребет» fbm): ядро пламени
//   ProceduralLanternSoot — копоть на стекле (садится ВВЕРХ по локальной Y)
//   ProceduralLanternPane — стыки стёкол (границы ячеек Worley, F2 − F1)
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим), параметры идут в порядке слотов CF-ноды, `out` — последним.
// Новое поведение — новой функцией; существующие сигнатуры не менять (слоты
// связываются по номеру, а не по имени).
//
// ЗАЩИТЫ снизу: масштабы `max(0.001, S)`, растяжения `max(1.0, S)` — ноль в
// неподключённом слоте CF-ноды обязан означать «без эффекта» (изотропно), а не
// вырождение домена; ширины `max(0.004, S)`.
//
// Хеш без sin (Hoskins-style). Всё считается от object-space координат;
// AbsoluteWorld не используется (Floating Origin).

// ── Хелперы ──────────────────────────────────────────────────────────────────

float PCLant_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCLant_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCLant_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCLant_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCLant_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCLant_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCLant_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCLant_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCLant_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCLant_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

// ── Полоса пропускания (26.10, по образцу Cobble/Concrete/Wood/Glass/Iron) ─────────────
// Пиксель сцены ≈ 1.37 мм × дистанция. На дальнем плане нить пламени/стык стёкол/копоть мельче
// пикселя, и в рельефе (и в свечении) это случайная величина в каждом пикселе.
// Слой с периодом ≤ ~1.8 пикс сводится к среднему, ≥ ~4.5 пикс — целиком (пороги PCCobble_Band).
// Средние измерены численно на той же выборке, что и пресет: нить ядра — 0.58,
// покрытие шва ≈ 2 × Width (0.113 при Width 0.06).
float PCLant_Footprint(float3 p)
{
    return max(length(ddx(p)), length(ddy(p)));
}

#define PCLANT_BAND_LO 0.22
#define PCLANT_BAND_HI 0.55
float PCLant_Band(float3 p)
{
    return 1.0 - smoothstep(PCLANT_BAND_LO, PCLANT_BAND_HI, PCLant_Footprint(p));
}

float PCLant_Fbm3(float3 p, float detail)
{
    // октава мельче пикселя плавно уходит в своё среднее (0.5)
    float px = PCLant_Footprint(p);
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        float w = 1.0 - smoothstep(PCLANT_BAND_LO, PCLANT_BAND_HI, px * freq);
        sum += amp * lerp(0.5, PCLant_ValueNoise3(p * freq), w);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Worley (3x3x3): F1 — расстояние до ближайшего центра ячейки, F2 — до второго.
// Возвращает F2 − F1, то есть расстояние ДО ГРАНИЦЫ ячейки: 0 на стыке стёкол и
// растёт внутрь ячейки.
float PCLant_WorleyF2F1(float3 p, out float cellTone)
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
                    PCLant_Hash13(cell + 0.0),
                    PCLant_Hash13(cell + 17.0),
                    PCLant_Hash13(cell + 31.0));
                float d = length(cell + rnd - p);
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                    best = PCLant_Hash13(cell + 53.0);
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

// ── Жар (раскалённое поле за стеклом) ────────────────────────────────────────
// Крупное поле, решающее ГДЕ стекло раскалено и светится сильнее. Домен свёрнут
// домен-warp'ом; амплитуда свёртки жёстко 0.30 — без него пятна выходят ровными
// кляксами, но отдельный регулятор здесь был бы третьим множителем одного и
// того же (тот же приём, что у лавы).
// Растяжение по локальной Y (GlowStretch) вытягивает пятна вверх — рисунок
// поднимающегося жара, а не изотропных клякс.
void ProceduralLanternGlow_float(
    float3 ObjectPos,
    float GlowScale, float GlowStretch, float GlowSeed,
    out float Glow)
{
    float3 p = ObjectPos * max(0.001, GlowScale) + GlowSeed;
    p.y /= max(1.0, GlowStretch);
    float3 w = float3(
        PCLant_ValueNoise3(p * 0.5 + 5.3),
        PCLant_ValueNoise3(p * 0.5 + 13.7),
        PCLant_ValueNoise3(p * 0.5 + 29.1)) - 0.5;
    p += w * 0.30;
    Glow = lerp(0.5, saturate(PCLant_Fbm3(p, 2.0)), PCLant_Band(p));
}

// ── Ядро пламени ─────────────────────────────────────────────────────────────
// «Хребет» fbm — там, где fbm проходит через 0.5 (0 на самой линии). Даёт
// тонкие яркие нити: не пятно, а язык пламени. Маска ставится в графе
// (Smoothstep) поверх поля жара, поэтому ядро всегда лежит внутри раскалённой
// зоны и не может светиться на холодном стекле в одиночку.
void ProceduralLanternCore_float(
    float3 ObjectPos,
    float CoreScale, float CoreStretch, float CoreSeed,
    out float Core)
{
    float3 p = ObjectPos * max(0.001, CoreScale) + CoreSeed;
    p.y /= max(1.0, CoreStretch);
    float ridge = abs(PCLant_Fbm3(p, 2.2) * 2.0 - 1.0);
    // мельче пикселя нить не видна: остаётся среднее свечение нити (0.58), а не мерцание
    Core = lerp(0.58, saturate(1.0 - ridge * 2.2), PCLant_Band(p));
}

// ── Копоть на стекле ─────────────────────────────────────────────────────────
// Копоть садится ВВЕРХУ: у фонаря гарь поднимается и осаждается на верхней
// части стекла, низ (у горелки) остаётся чище. Наклон учитывается ВНУТРИ
// функции, потому что объектные границы функции неизвестны: ObjectPos.y
// домножается на тот же SootScale, что и шум, — рисунок и наклон масштабируются
// вместе с объектом (FO-safe object-space: масштаб объекта = масштаб рисунка).
//   SootTilt = 0 — копоть распределена равномерно (нулевое поведение).
//   Soot = 0 — чистое стекло, 1 — глухая копоть (маска, а не яркость).
void ProceduralLanternSoot_float(
    float3 ObjectPos,
    float SootScale, float SootDetail, float SootSeed, float SootTilt, float SootAmount,
    out float Soot)
{
    float s = max(0.001, SootScale);
    float3 p = ObjectPos * s + SootSeed;
    float grime = lerp(0.5, saturate(PCLant_Fbm3(p, max(1.1, SootDetail))), PCLant_Band(p));
    float up = saturate(0.5 + ObjectPos.y * s * 0.5 * SootTilt);
    Soot = saturate(grime * max(0.0, SootAmount) * (0.55 + 0.90 * up));
}

// ── Стыки стёкол ─────────────────────────────────────────────────────────────
// Стык = граница ячейки Worley. Ячейки вытянуты вдоль локальной Y
// (PaneStretch), поэтому стёкла фонаря длинные и стоят вдоль вертикали, а швы
// идут вдоль неё же, а не кольцами поперёк. 1 — стык, 0 — тело стекла.
// Слой НЕ дрейфует по времени: стекло фонаря стоит на месте, движется только
// жар за ним.
void ProceduralLanternPane_float(
    float3 ObjectPos,
    float PaneScale, float PaneStretch, float PaneWidth, float PaneSeed,
    out float Pane)
{
    float3 p = ObjectPos * max(0.001, PaneScale) + PaneSeed;
    p.y /= max(1.0, PaneStretch);
    float paneTone;
    float border = PCLant_WorleyF2F1(p, paneTone);
    float w = max(0.004, PaneWidth);
    Pane = lerp(saturate(2.0 * w), saturate(1.0 - smoothstep(0.0, w, border)), PCLant_Band(p));
}

// ── Обёртки CF-нод (одно-выходные, File-режим) ───────────────────────────────

void ProceduralLanternGlow_half(
    half3 ObjectPos,
    half GlowScale, half GlowStretch, half GlowSeed,
    out half Glow)
{
    float v;
    ProceduralLanternGlow_float(
        (float3)ObjectPos,
        (float)GlowScale, (float)GlowStretch, (float)GlowSeed, v);
    Glow = (half)v;
}

void ProceduralLanternCore_half(
    half3 ObjectPos,
    half CoreScale, half CoreStretch, half CoreSeed,
    out half Core)
{
    float v;
    ProceduralLanternCore_float(
        (float3)ObjectPos,
        (float)CoreScale, (float)CoreStretch, (float)CoreSeed, v);
    Core = (half)v;
}

void ProceduralLanternSoot_half(
    half3 ObjectPos,
    half SootScale, half SootDetail, half SootSeed, half SootTilt, half SootAmount,
    out half Soot)
{
    float v;
    ProceduralLanternSoot_float(
        (float3)ObjectPos,
        (float)SootScale, (float)SootDetail, (float)SootSeed, (float)SootTilt, (float)SootAmount, v);
    Soot = (half)v;
}

void ProceduralLanternPane_half(
    half3 ObjectPos,
    half PaneScale, half PaneStretch, half PaneWidth, half PaneSeed,
    out half Pane)
{
    float v;
    ProceduralLanternPane_float(
        (float3)ObjectPos,
        (float)PaneScale, (float)PaneStretch, (float)PaneWidth, (float)PaneSeed, v);
    Pane = (half)v;
}

#endif
