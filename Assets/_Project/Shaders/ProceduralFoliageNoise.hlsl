#ifndef PROCEDURAL_FOLIAGE_NOISE_INCLUDED
#define PROCEDURAL_FOLIAGE_NOISE_INCLUDED

// ProceduralFoliageNoise — object-space шум ЛИСТВЫ (кроны деревьев, кусты,
// живые изгороди, вьющиеся растения). Материал Foliage собран с нуля:
// у Brick/Tiles/Concrete/Plate не взято ничего.
//
// КАДР. Листва — не поверхность с рисунком, а МАССА отдельных листьев:
//   X, Z — поперёк кроны, Y — ВВЕРХ.
// Один объект = одна крона (или один куст). Ствол и толстые ветви — отдельный
// объект с материалом Bark. Крона — сплошной «ком» (low-poly), а НЕ карточки с
// альфа-вырезом: в проекте нет ни альфа-клипа, ни текстур, поэтому лист
// рисуется прямо на поверхности кома, а не вырезается из квада.
//
// ГЛАВНЫЙ ПРИЁМ, которого нет у братьев: сетка листьев строится в 3D-домене,
// СПЛЮЩЕННОМ ПО ОБЪЕКТНОЙ НОРМАЛИ. Ячейка Worley, растянутая вдоль нормали,
// возвращается на поверхность ПЛОСКИМ эллипсоидом — лист ложится плашмя в
// касательную плоскость и не торчит шаром. Без этого крона читается как
// плитка, а не как листва, поэтому вход ObjectNormal есть у всех трёх функций
// листа (нормаль — величина объектная, от начала координат не зависит, FO-safe).
//
// Слои:
//   ProceduralFoliageLeaf     — отдельный лист: 1 в теле листа, 0 за кромкой
//   ProceduralFoliageLeafTone — тон листа: постоянен ВНУТРИ листа (хеш ячейки)
//   ProceduralFoliageLeafVein — жилки внутри листа (центральная + боковые)
//   ProceduralFoliagePatch    — общий примитив «пятно fbm»; в графе берётся
//                               трижды: масса, сухость, выгорание (порог
//                               ставится в графе — один рисунок, три роли)
//   ProceduralFoliageTwigs    — ветки и сучья, просвечивающие сквозь массу
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим), параметры идут в порядке слотов CF-ноды, `out` — последним.
// Новое поведение — новой функцией; существующие сигнатуры не менять (слоты
// связываются по номеру, а не по имени).
//
// РАСТЯЖЕНИЕ И СПЛЮЩИВАНИЕ снизу защищены ЕДИНИЦЕЙ (`max(1.0, S)`), а не 0.05:
// неподключённый вход CF-ноды уходит в шейдер нулём (таково сериализованное
// значение слота) и обязан означать «без эффекта» — изотропно, не сплющено.
// Защита 0.05, как у кирпичной стены, давала бы двадцатикратное растяжение
// (разбор — ProceduralTilesGrains).
//
// Хеш без sin (Hoskins-style). Всё считается от object-space координат и
// объектной нормали; AbsoluteWorld не используется.

// ── Хелперы ──────────────────────────────────────────────────────────────────

float PCFol_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCFol_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCFol_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCFol_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCFol_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCFol_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCFol_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCFol_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCFol_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCFol_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCFol_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCFol_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Нормаль, пригодная к употреблению. Неподключённый вход CF-ноды приходит
// нулевым вектором, а normalize(0) — это NaN; подставляем «вверх».
float3 PCFol_SafeNormal(float3 ObjectNormal)
{
    float len = length(ObjectNormal);
    return len > 1e-4 ? ObjectNormal / len : float3(0.0, 1.0, 0.0);
}

// Домен листа: масштаб → вытягивание вдоль локальной Y → сплющивание по нормали.
//   LeafScale   — сколько листьев укладывается в единицу длины;
//   LeafStretch — >1 вытягивает лист ВНИЗ (листва свисает вдоль локальной Y);
//   LeafFlatten — >1 сплющивает ячейку по нормали: 1 = шар, 4…8 = лист плашмя.
// Обе защиты снизу — единица: 0 (неподключённый слот) = «без эффекта».
float3 PCFol_LeafDomain(
    float3 ObjectPos, float3 ObjectNormal,
    float LeafScale, float LeafFlatten, float LeafStretch, float LeafSeed)
{
    float3 p = ObjectPos * max(0.001, LeafScale) + LeafSeed;
    p.y /= max(1.0, LeafStretch);
    float3 n = PCFol_SafeNormal(ObjectNormal);
    p += n * (dot(p, n) * (max(1.0, LeafFlatten) - 1.0));
    return p;
}

// Ячейка-лист, в которую попала точка:
//   F1     — расстояние до центра победившей ячейки (в домен-единицах);
//   Center — центр победившей ячейки, он же её идентификатор (для тона).
// Центры ячеек лежат в центральной половине клетки ([0.25, 0.75]^3), поэтому
// по каждой оси выиграть может только клетка со стороны frac(p): 8 выборок
// вместо 27 — при том же рисунке.
void PCFol_LeafCell(float3 p, out float F1, out float3 Center)
{
    float3 g = floor(p);
    float3 f = frac(p);
    float3 side = step(0.5, f) * 2.0 - 1.0;

    F1 = 8.0;
    Center = g + 0.5;

    [unroll]
    for (int a = 0; a < 2; a++)
    {
        [unroll]
        for (int b = 0; b < 2; b++)
        {
            [unroll]
            for (int c = 0; c < 2; c++)
            {
                float3 cell = g + side * float3((float)a, (float)b, (float)c);
                float3 j = float3(
                    PCFol_Hash13(cell),
                    PCFol_Hash13(cell + 17.0),
                    PCFol_Hash13(cell + 31.0));
                float3 center = cell + 0.25 + 0.5 * j;
                float d = length(center - p);
                if (d < F1)
                {
                    F1 = d;
                    Center = center;
                }
            }
        }
    }
}

// Радиус листа по хешу его ячейки: в кроне листья разного размера, ровная
// плитка читалась бы как чешуя. Одна и та же формула нужна и маске, и жилкам —
// поэтому вынесена, чтобы радиус не разъехался между функциями.
float PCFol_LeafRadius(float3 Center)
{
    return 0.52 * lerp(0.80, 1.25, PCFol_Hash13(Center * 1.31 + 5.7));
}

// ── Отдельный лист ───────────────────────────────────────────────────────────
// 1 в теле листа, 0 за кромкой. Всё, что вне листа, — просвет в глубину кроны,
// именно на нём крона и читается как набор листьев, а не как сплошной ком.
// Кромка гаснет не ступенькой, а smoothstep от LeafEdge: у листа мягкий край.
void ProceduralFoliageLeaf_float(
    float3 ObjectPos,
    float3 ObjectNormal,
    float LeafScale, float LeafFlatten, float LeafStretch, float LeafEdge, float LeafSeed,
    out float Leaf)
{
    float3 p = PCFol_LeafDomain(ObjectPos, ObjectNormal, LeafScale, LeafFlatten, LeafStretch, LeafSeed);
    float f1;
    float3 center;
    PCFol_LeafCell(p, f1, center);

    float radius = PCFol_LeafRadius(center);
    float edge = saturate(LeafEdge);
    Leaf = saturate(1.0 - smoothstep(radius * (1.0 - edge), radius, f1));
}

// ── Тон листа ────────────────────────────────────────────────────────────────
// Постоянен внутри листа и скачком меняется на его границе: в кроне соседние
// листья разного возраста и разной освещённости, и именно этот скачок, а не
// шум, даёт «крона набрана из листьев». Считается тем же доменом и той же
// ячейкой, что и маска, поэтому тон и его границы разъехаться не могут.
void ProceduralFoliageLeafTone_float(
    float3 ObjectPos,
    float3 ObjectNormal,
    float LeafScale, float LeafFlatten, float LeafStretch, float LeafSeed,
    out float LeafTone)
{
    float3 p = PCFol_LeafDomain(ObjectPos, ObjectNormal, LeafScale, LeafFlatten, LeafStretch, LeafSeed);
    float f1;
    float3 center;
    PCFol_LeafCell(p, f1, center);

    LeafTone = saturate(PCFol_Hash13(center * 1.31 + 91.3));
}

// ── Жилки листа ──────────────────────────────────────────────────────────────
// Центральная жилка (линия вдоль одной локальной оси листа) и боковые,
// отходящие под углом. Локальные оси строятся в касательной плоскости по
// объектной нормали, поэтому жилки лежат В листе, а не в его ячейке.
// Кромка листа гасит жилки — иначе они выползали бы на просвет.
void ProceduralFoliageLeafVein_float(
    float3 ObjectPos,
    float3 ObjectNormal,
    float LeafScale, float LeafFlatten, float LeafStretch,
    float LeafVeinWidth, float LeafVeinCount, float LeafSeed,
    out float LeafVein)
{
    float3 n = PCFol_SafeNormal(ObjectNormal);
    float3 p = PCFol_LeafDomain(ObjectPos, ObjectNormal, LeafScale, LeafFlatten, LeafStretch, LeafSeed);
    float f1;
    float3 center;
    PCFol_LeafCell(p, f1, center);

    // смещение от центра листа, спроецированное в плоскость листа
    float3 t = p - center;
    t -= n * dot(t, n);

    // базис в плоскости листа. Нормаль, совпавшая с осью подстановки, даёт
    // нулевой cross — поэтому ось подстановки выбирается по наклону нормали.
    float3 up = abs(n.y) < 0.9 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 a1 = normalize(cross(n, up));
    float3 a2 = cross(n, a1);
    float2 uv = float2(dot(t, a1), dot(t, a2));

    float w = max(0.004, LeafVeinWidth);
    float spine = 1.0 - smoothstep(0.0, w, abs(uv.y));

    float k = max(1.0, floor(LeafVeinCount + 0.5));
    float branch = 1.0 - smoothstep(0.0, w * 0.8, abs(frac((uv.x + uv.y) * k) - 0.5));

    // жилки живут только в теле листа: то же гашение, что у маски, но мягче
    float radius = PCFol_LeafRadius(center);
    float inside = saturate(1.0 - smoothstep(radius * 0.55, radius * 0.90, f1));

    LeafVein = saturate(max(spine, branch) * inside);
}

// ── Пятно (общий примитив) ───────────────────────────────────────────────────
// fbm в 0..1 без порогов: порог ставится в графе (Smoothstep), потому что один
// и тот же рисунок берётся в трёх ролях с разной полярностью — масса (просветы
// в глубину кроны), сухость (бурые языки по кромкам листьев) и выгорание
// (светлые выгоревшие участки сверху).
//   PatchStretch = 1  -> изотропные пятна
//   PatchStretch > 1  -> рисунок вытянут вдоль ЛОКАЛЬНОЙ Y (в кроне — вниз)
void ProceduralFoliagePatch_float(
    float3 ObjectPos,
    float PatchScale, float PatchStretch, float PatchSeed,
    out float Patch)
{
    float3 p = ObjectPos * max(0.001, PatchScale) + PatchSeed;
    p.y /= max(1.0, PatchStretch);
    Patch = saturate(PCFol_Fbm3(p, 2.0));
}

// ── Ветки сквозь массу ───────────────────────────────────────────────────────
// Тонкая борозда = расстояние до «хребта» fbm (0 на линии, дальше в сторону).
// Глубина задаётся в графе своим коэффициентом рельефа и цветом-подложкой.
// Ветки редкие: их раскрывает крупный fbm, иначе крона была бы исполосована
// сеткой, а не ветвлением.
void ProceduralFoliageTwigs_float(
    float3 ObjectPos,
    float TwigScale, float TwigWidth, float TwigSeed,
    out float Twigs)
{
    float3 p = ObjectPos * max(0.001, TwigScale) + TwigSeed;
    float ridge = abs(PCFol_Fbm3(p, 2.0) * 2.0 - 1.0);

    float w = max(0.004, TwigWidth);
    float core = 1.0 - smoothstep(0.0, w, ridge);
    float gate = smoothstep(0.52, 0.72, PCFol_Fbm3(p * 0.35 + 11.3, 2.0));

    Twigs = saturate(core * gate);
}

// ── Обёртки CF-нод (одно-выходные, File-режим) ───────────────────────────────

void ProceduralFoliageLeaf_half(
    half3 ObjectPos,
    half3 ObjectNormal,
    half LeafScale, half LeafFlatten, half LeafStretch, half LeafEdge, half LeafSeed,
    out half Leaf)
{
    float v;
    ProceduralFoliageLeaf_float(
        (float3)ObjectPos, (float3)ObjectNormal,
        (float)LeafScale, (float)LeafFlatten, (float)LeafStretch, (float)LeafEdge, (float)LeafSeed, v);
    Leaf = (half)v;
}

void ProceduralFoliageLeafTone_half(
    half3 ObjectPos,
    half3 ObjectNormal,
    half LeafScale, half LeafFlatten, half LeafStretch, half LeafSeed,
    out half LeafTone)
{
    float v;
    ProceduralFoliageLeafTone_float(
        (float3)ObjectPos, (float3)ObjectNormal,
        (float)LeafScale, (float)LeafFlatten, (float)LeafStretch, (float)LeafSeed, v);
    LeafTone = (half)v;
}

void ProceduralFoliageLeafVein_half(
    half3 ObjectPos,
    half3 ObjectNormal,
    half LeafScale, half LeafFlatten, half LeafStretch,
    half LeafVeinWidth, half LeafVeinCount, half LeafSeed,
    out half LeafVein)
{
    float v;
    ProceduralFoliageLeafVein_float(
        (float3)ObjectPos, (float3)ObjectNormal,
        (float)LeafScale, (float)LeafFlatten, (float)LeafStretch,
        (float)LeafVeinWidth, (float)LeafVeinCount, (float)LeafSeed, v);
    LeafVein = (half)v;
}

void ProceduralFoliagePatch_half(
    half3 ObjectPos,
    half PatchScale, half PatchStretch, half PatchSeed,
    out half Patch)
{
    float v;
    ProceduralFoliagePatch_float((float3)ObjectPos, (float)PatchScale, (float)PatchStretch, (float)PatchSeed, v);
    Patch = (half)v;
}

void ProceduralFoliageTwigs_half(
    half3 ObjectPos,
    half TwigScale, half TwigWidth, half TwigSeed,
    out half Twigs)
{
    float v;
    ProceduralFoliageTwigs_float((float3)ObjectPos, (float)TwigScale, (float)TwigWidth, (float)TwigSeed, v);
    Twigs = (half)v;
}

#endif
