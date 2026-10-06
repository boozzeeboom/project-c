#ifndef PROCEDURAL_PLASTIC_NOISE_INCLUDED
#define PROCEDURAL_PLASTIC_NOISE_INCLUDED

// ProceduralPlasticNoise — object-space 3D noise для ЛИТОГО ПЛАСТИКА (ABS / PP / ABC-пластик).
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные (File-режим).
// Сигнатуры = порядок слотов CF-нод в графе. Новое поведение — новой функцией;
// существующие сигнатуры не менять (слоты в графе связываются по номеру).
//
// Слои — про пластик; металла, ржавчины и патины здесь нет:
//   ProceduralPlasticGrain  — шагрень формы: ячейки Worley, 1 в центре ячейки, 0 в канавке
//   ProceduralPlasticFlow   — линии спая / границы потоков расплава (Worley F2 - F1)
//   ProceduralPlasticSeam   — линия разъёма формы: тонкая полоса по локальной Y
//   ProceduralPlasticChalk  — мелование/выцветание: крупные пятна (fbm)
//   ProceduralPlasticSmudge — засаливание и налёт от рук: крупные пятна (fbm, другой рисунок)
//   ProceduralPlasticScruff — потёртости: тонкие штрихи, повёрнутые на угол
//   ProceduralPlasticEdge   — кромка: вторая по величине |координата| (рёбра и вершины)
//
// Хеш без sin. Всё считается из object-space координат (FO-safe).
// AbsoluteWorld не используется.

float PCPlast_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCPlast_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCPlast_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCPlast_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCPlast_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCPlast_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCPlast_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCPlast_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCPlast_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCPlast_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCPlast_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCPlast_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Ridged: тонкие линии там, где fbm пересекает 0.5.
float PCPlast_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

// Worley F1 (3x3x3) — ячейки шагрени.
float PCPlast_Worley1(float3 p)
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
                    PCPlast_Hash13(cell + 0.0),
                    PCPlast_Hash13(cell + 17.0),
                    PCPlast_Hash13(cell + 31.0));
                minD = min(minD, length(cell + rnd - p));
            }
        }
    }
    return minD;
}

// Worley F2 - F1 (3x3x3): 0 на границе ячеек, растёт внутрь ячейки.
// Границы ячеек = линии спая / стыки потоков расплава при литье под давлением.
float PCPlast_WorleyEdge(float3 p)
{
    float3 g = floor(p);
    float d1 = 8.0;
    float d2 = 8.0;
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
                    PCPlast_Hash13(cell + 0.0),
                    PCPlast_Hash13(cell + 17.0),
                    PCPlast_Hash13(cell + 31.0));
                float d = length(cell + rnd - p);
                if (d < d1) { d2 = d1; d1 = d; }
                else if (d < d2) { d2 = d; }
            }
        }
    }
    return max(0.0, d2 - d1);
}

// ── Шагрень формы ────────────────────────────────────────────────────────────
// Плато ячейки = 1, канавка между ячейками = 0. Рисунок формы (матовая зернистая
// поверхность пресс-формы) плюс медленная неровность самой накатки.
float PCPlast_Grain(float3 ObjectPos, float GrainScale, float GrainDetail, float GrainSeed)
{
    float3 p = ObjectPos * max(0.001, GrainScale) + GrainSeed;
    float d = PCPlast_Worley1(p);
    float cell = saturate(1.0 - smoothstep(0.10, 0.62, d));
    float uneven = PCPlast_Fbm3(p * 0.06, max(1.1, GrainDetail)) - 0.5;
    return saturate(cell + uneven * 0.3);
}

// ── Линии спая / границы потоков расплава ────────────────────────────────────
// 1 на линии, 0 вне её. Ширина линии задана внутренней константой (0.06 в единицах
// F2 - F1), сила регулируется множителем в графе.
float PCPlast_Flow(float3 ObjectPos, float FlowScale, float FlowSeed)
{
    float3 p = ObjectPos * max(0.001, FlowScale) + FlowSeed;
    float e = PCPlast_WorleyEdge(p);
    return saturate(1.0 - smoothstep(0.0, 0.06, e));
}

// ── Линия разъёма формы ──────────────────────────────────────────────────────
// Тонкая прямая полоса на высоте SeamHeight по локальной Y (плоскость разъёма
// пресс-формы). Единицы — локальные единицы меша, как у EdgePosition.
float PCPlast_Seam(float3 ObjectPos, float SeamHeight, float SeamWidth)
{
    float w = max(0.0005, SeamWidth);
    return saturate(1.0 - smoothstep(0.0, w, abs(ObjectPos.y - SeamHeight)));
}

// ── Мелование / выцветание ───────────────────────────────────────────────────
// Крупные пятна с рваной границей: 0 на «сохранных» участках, 1 на выгоревших.
float PCPlast_Chalk(float3 ObjectPos, float ChalkScale, float ChalkSeed)
{
    float3 p = ObjectPos * max(0.001, ChalkScale) + ChalkSeed;
    return saturate(PCPlast_Fbm3(p, 2.0) * 1.6 - 0.3);
}

// ── Засаливание / налёт от рук ───────────────────────────────────────────────
// Пятна залоснившегося пластика (лаковая дорожка от касаний) — отдельная
// маска со своим сидом, чтобы не совпадала с выцветанием.
float PCPlast_Smudge(float3 ObjectPos, float SmudgeScale, float SmudgeSeed)
{
    float3 p = ObjectPos * max(0.001, SmudgeScale) + SmudgeSeed;
    return saturate(smoothstep(0.42, 0.72, PCPlast_Fbm3(p, 2.0)));
}

// ── Потёртости ───────────────────────────────────────────────────────────────
// Тонкие штрихи. Домен поворачивается на ScruffAngle в локальной плоскости XY,
// затем сжимается вдоль X -> штрихи тянутся в повёрнутом направлении.
float PCPlast_Scruff(float3 ObjectPos, float ScruffScale, float ScruffStretch,
                     float ScruffDetail, float ScruffAngle, float ScruffSeed)
{
    float3 p = ObjectPos * max(0.001, ScruffScale) + ScruffSeed;
    float ca = cos(ScruffAngle);
    float sn = sin(ScruffAngle);
    float3 q = p;
    q.xy = float2(p.x * ca - p.y * sn, p.x * sn + p.y * ca);
    q.x *= max(0.05, ScruffStretch);
    return saturate(PCPlast_Ridge(PCPlast_Fbm3(q, max(1.1, ScruffDetail))));
}

// ── Кромка ───────────────────────────────────────────────────────────────────
// На поверхности габарита ровно одна координата по модулю максимальна.
// Вторая по величине |координата| = мера близости к ребру/вершине:
//   центр грани -> 0, ребро -> 1, вершина -> 1.
// (Отличие от металлических материалов: кромка здесь ПОЛИРУЕТСЯ и белеет,
//  а не стирается до подложки.)
float PCPlast_Edge(float3 ObjectPos, float EdgePosition, float EdgeWidth)
{
    float3 a = abs(ObjectPos);
    float mx = max(a.x, max(a.y, a.z));
    float mn = min(a.x, min(a.y, a.z));
    float mid = a.x + a.y + a.z - mx - mn;
    float w = max(0.0005, EdgeWidth);
    return saturate(smoothstep(max(0.0, EdgePosition - w), EdgePosition, mid));
}

void ProceduralPlasticGrain_float(
    float3 ObjectPos, float GrainScale, float GrainDetail, float GrainSeed,
    out float Grain)
{
    Grain = PCPlast_Grain(ObjectPos, GrainScale, GrainDetail, GrainSeed);
}

void ProceduralPlasticGrain_half(
    half3 ObjectPos, half GrainScale, half GrainDetail, half GrainSeed,
    out half Grain)
{
    float g;
    ProceduralPlasticGrain_float(
        (float3)ObjectPos, (float)GrainScale, (float)GrainDetail, (float)GrainSeed, g);
    Grain = (half)g;
}

void ProceduralPlasticFlow_float(
    float3 ObjectPos, float FlowScale, float FlowSeed,
    out float Flow)
{
    Flow = PCPlast_Flow(ObjectPos, FlowScale, FlowSeed);
}

void ProceduralPlasticFlow_half(
    half3 ObjectPos, half FlowScale, half FlowSeed,
    out half Flow)
{
    float f;
    ProceduralPlasticFlow_float((float3)ObjectPos, (float)FlowScale, (float)FlowSeed, f);
    Flow = (half)f;
}

void ProceduralPlasticSeam_float(
    float3 ObjectPos, float SeamHeight, float SeamWidth,
    out float Seam)
{
    Seam = PCPlast_Seam(ObjectPos, SeamHeight, SeamWidth);
}

void ProceduralPlasticSeam_half(
    half3 ObjectPos, half SeamHeight, half SeamWidth,
    out half Seam)
{
    float s;
    ProceduralPlasticSeam_float((float3)ObjectPos, (float)SeamHeight, (float)SeamWidth, s);
    Seam = (half)s;
}

void ProceduralPlasticChalk_float(
    float3 ObjectPos, float ChalkScale, float ChalkSeed,
    out float Chalk)
{
    Chalk = PCPlast_Chalk(ObjectPos, ChalkScale, ChalkSeed);
}

void ProceduralPlasticChalk_half(
    half3 ObjectPos, half ChalkScale, half ChalkSeed,
    out half Chalk)
{
    float c;
    ProceduralPlasticChalk_float((float3)ObjectPos, (float)ChalkScale, (float)ChalkSeed, c);
    Chalk = (half)c;
}

void ProceduralPlasticSmudge_float(
    float3 ObjectPos, float SmudgeScale, float SmudgeSeed,
    out float Smudge)
{
    Smudge = PCPlast_Smudge(ObjectPos, SmudgeScale, SmudgeSeed);
}

void ProceduralPlasticSmudge_half(
    half3 ObjectPos, half SmudgeScale, half SmudgeSeed,
    out half Smudge)
{
    float s;
    ProceduralPlasticSmudge_float((float3)ObjectPos, (float)SmudgeScale, (float)SmudgeSeed, s);
    Smudge = (half)s;
}

void ProceduralPlasticScruff_float(
    float3 ObjectPos,
    float ScruffScale, float ScruffStretch, float ScruffDetail,
    float ScruffAngle, float ScruffSeed,
    out float Scruff)
{
    Scruff = PCPlast_Scruff(ObjectPos, ScruffScale, ScruffStretch, ScruffDetail, ScruffAngle, ScruffSeed);
}

void ProceduralPlasticScruff_half(
    half3 ObjectPos,
    half ScruffScale, half ScruffStretch, half ScruffDetail,
    half ScruffAngle, half ScruffSeed,
    out half Scruff)
{
    float s;
    ProceduralPlasticScruff_float(
        (float3)ObjectPos,
        (float)ScruffScale, (float)ScruffStretch, (float)ScruffDetail,
        (float)ScruffAngle, (float)ScruffSeed, s);
    Scruff = (half)s;
}

void ProceduralPlasticEdge_float(
    float3 ObjectPos, float EdgePosition, float EdgeWidth,
    out float Edge)
{
    Edge = PCPlast_Edge(ObjectPos, EdgePosition, EdgeWidth);
}

void ProceduralPlasticEdge_half(
    half3 ObjectPos, half EdgePosition, half EdgeWidth,
    out half Edge)
{
    float e;
    ProceduralPlasticEdge_float((float3)ObjectPos, (float)EdgePosition, (float)EdgeWidth, e);
    Edge = (half)e;
}

#endif
