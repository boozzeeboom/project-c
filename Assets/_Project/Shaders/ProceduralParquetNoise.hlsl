#ifndef PROCEDURAL_PARQUET_NOISE_INCLUDED
#define PROCEDURAL_PARQUET_NOISE_INCLUDED

// ProceduralParquetNoise — object-space шум РАСКЛАДКИ пластов для паркета /
// деревянного настила (палуба, вагонка, доска пола).
//
// ПРАВИЛО (T-BRONZE01): только ОДНО-выходные функции File-режима.
// Сигнатуры = порядок слотов CF-нод графа Parquet. Менять только вместе с графом.
//
// Здесь только СЕТКА ПЛАСТОВ. Сам рисунок дерева не дублируется: ноды
// Кольца / Волокно / Поры / Сучки остаются на ядре ProceduralWoodNoise.hlsl,
// но получают на вход PlankLocal вместо Position. Поэтому у каждого пласта
// своё окно породы и рисунок не перетекает через шов.
//
// Оси раскладки: пласты лежат в плоскости локальных XZ (длина — по X, ряд — по Z,
// толщина — по Y). В PlankLocal оси переставлены так, чтобы локальная Y дерева
// (= вдоль волокна) совпала с ДЛИНОЙ пласта, а X — с шириной.
//
// Всё считается из object-space координат (FO-safe), AbsoluteWorld не используется.

float PCParq_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCParq_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCParq_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCParq_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCParq_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCParq_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCParq_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCParq_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCParq_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCParq_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

// ── Сетка пластов ────────────────────────────────────────────────────────────
// c.x — вдоль ДЛИНЫ пласта, c.y — поперёк (локальная Z).
// PlankAspect = длина/ширина: частота по Z умножается на него, поэтому пласт
// вытянут по X. Ряды сдвигаются на RowOffset (непрерывно, 0.5 = пол-пласта)
// и на целое число пластов Stagger, чтобы торцевые стыки не выстраивались.
float2 PCParq_Cell(float3 ObjectPos, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp, float PlankSeed)
{
    float2 c = float2(ObjectPos.x, ObjectPos.z * max(0.01, PlankAspect)) * max(0.0001, PlankScale) + PlankSeed;
    c += (PCParq_ValueNoise3(float3(c * 0.6, PlankSeed * 0.37 + 5.1)) - 0.5) * PlankWarp;
    float row = floor(c.y);
    c.x += row * RowOffset;
    c.x += floor(PCParq_Hash13(float3(row, 3.7, PlankSeed)) * max(0.0, Stagger));
    return c;
}

// Случайное окно породы на каждый пласт (в единицах дерева, не в единицах сетки).
float3 PCParq_Window(float2 cell, float PlankSeed)
{
    float3 r = float3(
        PCParq_Hash13(float3(cell, 11.3 + PlankSeed)),
        PCParq_Hash13(float3(cell, 27.7 + PlankSeed)),
        PCParq_Hash13(float3(cell, 43.1 + PlankSeed)));
    return (r - 0.5) * 12.0;
}

// ── Раскладка → позиция для дерева ───────────────────────────────────────────
// Y = вдоль волокна (длина пласта), X = поперёк ширины, Z = толщина + сдвиг
// «сердцевины» пласта, поэтому кольца у пластов несимметричны.
void ProceduralParquetLayout_float(
    float3 ObjectPos, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp, float PlankSeed,
    out float3 PlankLocal)
{
    float2 c = PCParq_Cell(ObjectPos, PlankScale, PlankAspect, RowOffset, Stagger, PlankWarp, PlankSeed);
    float2 cell = floor(c);
    float2 local = frac(c);
    float3 win = PCParq_Window(cell, PlankSeed);

    float invLen = 1.0 / max(0.0001, PlankScale);      // длина пласта в единицах дерева
    float invWid = invLen / max(0.01, PlankAspect);    // ширина пласта в единицах дерева

    PlankLocal = float3(local.y * invWid + win.x,
                        local.x * invLen + win.y,
                        ObjectPos.y * 0.5 + win.z * 0.25);
}

void ProceduralParquetLayout_half(
    half3 ObjectPos, half PlankScale, half PlankAspect, half RowOffset, half Stagger, half PlankWarp, half PlankSeed,
    out half3 PlankLocal)
{
    float3 v;
    ProceduralParquetLayout_float(
        (float3)ObjectPos, (float)PlankScale, (float)PlankAspect, (float)RowOffset, (float)Stagger, (float)PlankWarp, (float)PlankSeed,
        v);
    PlankLocal = (half3)v;
}

// ── Швы между пластами ───────────────────────────────────────────────────────
// 1 на стыке, 0 в середине пласта. JointWidthX — торцевой стык (по длине),
// JointWidthY — боковой шов (по ширине).
void ProceduralParquetJoints_float(
    float3 ObjectPos, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp,
    float JointWidthX, float JointWidthY, float PlankSeed,
    out float Joints)
{
    float2 c = PCParq_Cell(ObjectPos, PlankScale, PlankAspect, RowOffset, Stagger, PlankWarp, PlankSeed);
    float2 local = frac(c);

    float dx = min(local.x, 1.0 - local.x) / max(0.0001, JointWidthX);
    float dy = min(local.y, 1.0 - local.y) / max(0.0001, JointWidthY);

    Joints = 1.0 - smoothstep(0.0, 1.0, min(dx, dy));
}

void ProceduralParquetJoints_half(
    half3 ObjectPos, half PlankScale, half PlankAspect, half RowOffset, half Stagger, half PlankWarp,
    half JointWidthX, half JointWidthY, half PlankSeed,
    out half Joints)
{
    float j;
    ProceduralParquetJoints_float(
        (float3)ObjectPos, (float)PlankScale, (float)PlankAspect, (float)RowOffset, (float)Stagger, (float)PlankWarp,
        (float)JointWidthX, (float)JointWidthY, (float)PlankSeed, j);
    Joints = (half)j;
}

// ── Тон пласта ───────────────────────────────────────────────────────────────
// Хеш ячейки раскладки: 0..1 на пласт, чтобы пласты различались по светлоте.
void ProceduralParquetTone_float(
    float3 ObjectPos, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp, float PlankSeed,
    out float PlankTone)
{
    float2 c = PCParq_Cell(ObjectPos, PlankScale, PlankAspect, RowOffset, Stagger, PlankWarp, PlankSeed);
    PlankTone = PCParq_Hash13(float3(floor(c), 5.9 + PlankSeed));
}

void ProceduralParquetTone_half(
    half3 ObjectPos, half PlankScale, half PlankAspect, half RowOffset, half Stagger, half PlankWarp, half PlankSeed,
    out half PlankTone)
{
    float t;
    ProceduralParquetTone_float(
        (float3)ObjectPos, (float)PlankScale, (float)PlankAspect, (float)RowOffset, (float)Stagger, (float)PlankWarp, (float)PlankSeed,
        t);
    PlankTone = (half)t;
}

// ── Раскладка с выбором плоскости по нормали (стены, вагонка, борта) ─────────
// Раскладка выше жёстко привязана к локальной плоскости XZ: на грани с
// постоянной Z (вертикальная стена) координата ряда не меняется, и боковые швы
// вырождаются в ноль — видны только торцевые полосы. Здесь плоскость выбирается
// по нормали поверхности в ОБЪЕКТНОМ пространстве: пол — XZ, стена ±Z — XY,
// стена ±X — ZY. Длина пласта всегда ложится на горизонталь поверхности.
// Старые функции ProceduralParquetLayout/Joints/Tone не меняются (T-BRONZE01).
void PCParq_Axes(float3 n, float3 p, out float2 uv, out float depth)
{
    float3 a = abs(n);
    if (a.y >= a.x && a.y >= a.z) { uv = float2(p.x, p.z); depth = p.y; }   // пол / потолок
    else if (a.z >= a.x)          { uv = float2(p.x, p.y); depth = p.z; }   // стена ±Z
    else                          { uv = float2(p.z, p.y); depth = p.x; }   // стена ±X
}

float2 PCParq_PlaneCell(float2 uv, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp, float PlankSeed)
{
    float2 c = float2(uv.x, uv.y * max(0.01, PlankAspect)) * max(0.0001, PlankScale) + PlankSeed;
    c += (PCParq_ValueNoise3(float3(c * 0.6, PlankSeed * 0.37 + 5.1)) - 0.5) * PlankWarp;
    float row = floor(c.y);
    c.x += row * RowOffset;
    c.x += floor(PCParq_Hash13(float3(row, 3.7, PlankSeed)) * max(0.0, Stagger));
    return c;
}

void ProceduralParquetLayoutFaces_float(
    float3 ObjectPos, float3 ObjectNormal, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp, float PlankSeed,
    out float3 PlankLocal)
{
    float2 uv; float depth;
    PCParq_Axes(ObjectNormal, ObjectPos, uv, depth);

    float2 c = PCParq_PlaneCell(uv, PlankScale, PlankAspect, RowOffset, Stagger, PlankWarp, PlankSeed);
    float2 cell = floor(c);
    float2 local = frac(c);
    float3 win = PCParq_Window(cell, PlankSeed);

    float invLen = 1.0 / max(0.0001, PlankScale);
    float invWid = invLen / max(0.01, PlankAspect);

    PlankLocal = float3(local.y * invWid + win.x,
                        local.x * invLen + win.y,
                        depth * 0.5 + win.z * 0.25);
}

void ProceduralParquetLayoutFaces_half(
    half3 ObjectPos, half3 ObjectNormal, half PlankScale, half PlankAspect, half RowOffset, half Stagger, half PlankWarp, half PlankSeed,
    out half3 PlankLocal)
{
    float3 v;
    ProceduralParquetLayoutFaces_float(
        (float3)ObjectPos, (float3)ObjectNormal, (float)PlankScale, (float)PlankAspect, (float)RowOffset, (float)Stagger, (float)PlankWarp, (float)PlankSeed,
        v);
    PlankLocal = (half3)v;
}

void ProceduralParquetJointsFaces_float(
    float3 ObjectPos, float3 ObjectNormal, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp,
    float JointWidthX, float JointWidthY, float PlankSeed,
    out float Joints)
{
    float2 uv; float depth;
    PCParq_Axes(ObjectNormal, ObjectPos, uv, depth);

    float2 c = PCParq_PlaneCell(uv, PlankScale, PlankAspect, RowOffset, Stagger, PlankWarp, PlankSeed);
    float2 local = frac(c);

    float dx = min(local.x, 1.0 - local.x) / max(0.0001, JointWidthX);
    float dy = min(local.y, 1.0 - local.y) / max(0.0001, JointWidthY);

    Joints = 1.0 - smoothstep(0.0, 1.0, min(dx, dy));
}

void ProceduralParquetJointsFaces_half(
    half3 ObjectPos, half3 ObjectNormal, half PlankScale, half PlankAspect, half RowOffset, half Stagger, half PlankWarp,
    half JointWidthX, half JointWidthY, half PlankSeed,
    out half Joints)
{
    float j;
    ProceduralParquetJointsFaces_float(
        (float3)ObjectPos, (float3)ObjectNormal, (float)PlankScale, (float)PlankAspect, (float)RowOffset, (float)Stagger, (float)PlankWarp,
        (float)JointWidthX, (float)JointWidthY, (float)PlankSeed, j);
    Joints = (half)j;
}

void ProceduralParquetToneFaces_float(
    float3 ObjectPos, float3 ObjectNormal, float PlankScale, float PlankAspect, float RowOffset, float Stagger, float PlankWarp, float PlankSeed,
    out float PlankTone)
{
    float2 uv; float depth;
    PCParq_Axes(ObjectNormal, ObjectPos, uv, depth);

    float2 c = PCParq_PlaneCell(uv, PlankScale, PlankAspect, RowOffset, Stagger, PlankWarp, PlankSeed);
    PlankTone = PCParq_Hash13(float3(floor(c), 5.9 + PlankSeed));
}

void ProceduralParquetToneFaces_half(
    half3 ObjectPos, half3 ObjectNormal, half PlankScale, half PlankAspect, half RowOffset, half Stagger, half PlankWarp, half PlankSeed,
    out half PlankTone)
{
    float t;
    ProceduralParquetToneFaces_float(
        (float3)ObjectPos, (float3)ObjectNormal, (float)PlankScale, (float)PlankAspect, (float)RowOffset, (float)Stagger, (float)PlankWarp, (float)PlankSeed,
        t);
    PlankTone = (half)t;
}

#endif
