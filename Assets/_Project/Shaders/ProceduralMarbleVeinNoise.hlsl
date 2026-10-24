#ifndef PROCEDURAL_MARBLE_VEIN_NOISE_INCLUDED
#define PROCEDURAL_MARBLE_VEIN_NOISE_INCLUDED

// ProceduralMarbleVeinNoise — object-space процедурный мрамор для графа
// Assets/_Project/Materials/Marble/Marble.shadergraph (M_PC_Marble / SMALL_M_PC_Marble).
//
// ПРАВИЛО (T-BRONZE01): только ОДНО-выходные функции File-режима.
// Сигнатуры = порядок слотов CF-нод в графе Marble.shadergraph. Менять только вместе с графом;
// новое поведение — новой функцией, существующие сигнатуры не трогать.
//
// Слои мрамора:
//   ProceduralMarbleMottle  — крупная облачность камня (знаковая ±MottleAmount),
//                             она же сдвигает прожилки (аналог пряжи у ткани)
//   ProceduralMarbleVeins   — сеть прожилок: изолинии (level-set) искажённого fbm,
//                             два масштаба (основные + вторичные тонкие), сглаживание по fwidth
//   ProceduralMarbleCloud   — тон камня: мягкие пятна, вытянутые по направлению распила
//   ProceduralMarblePits    — мелкие каверны/поры (разрежённый Worley F1)
//   ProceduralMarbleCrackA/B/C + CrackMask — трещины (механизм царапин бронзы/дерева, 1:1)
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe).
// Veins использует fwidth — только fragment-стадия.

float PCMar_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCMar_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCMar_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCMar_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCMar_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCMar_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCMar_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCMar_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCMar_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCMar_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCMar_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCMar_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Value-noise + [0,1] -> одна 0-1 полоса вокруг заданного уровня, мягкая по fwidth.
float PCMar_Band(float d, float width)
{
    float w = max(fwidth(d), 1e-5);
    return saturate(1.0 - smoothstep(width - w, width + w, d));
}

// ── Облачность (знаковая) ────────────────────────────────────────────────────
float PCMar_Mottle(float3 ObjectPos, float MottleScale, float MottleAmount, float MottleSeed)
{
    float3 p = ObjectPos * max(0.001, MottleScale) + MottleSeed;
    return (PCMar_Fbm3(p, 2.1) - 0.5) * 2.0 * max(0.0, MottleAmount);
}

// ── Прожилки ─────────────────────────────────────────────────────────────────
// Классическая схема: искажённое крупным шумом поле fbm, прожилка = изолиния s ≈ 0.5.
// d = |2s − 1| равно 0 ровно на прожилке; полоса задаётся VeinWidth.
// Второй набор — вдвое чаще и вдвое тоньше: сеть, а не одиночные линии.
// Mottle (знаковая) сдвигает домен: облака камня «ведут» прожилки.
float PCMar_Veins(float3 ObjectPos, float VeinScale, float VeinWidth, float VeinWarp, float Mottle, float VeinSeed)
{
    float3 p = ObjectPos * max(0.001, VeinScale) + VeinSeed;
    float width = clamp(VeinWidth, 0.005, 0.6);

    // Турбулентность: смещение домена крупным fbm (по одному значению, слегка по осям по-разному).
    float3 wp = p * 0.37 + 11.3;
    float turb = (PCMar_Fbm3(wp, 2.4) - 0.5) * 2.0;
    float3 q = p + float3(turb, turb * 0.6, turb) * max(0.0, VeinWarp) * 3.0;
    q += Mottle * float3(1.0, 0.6, 1.0) * 4.0;

    float s1 = PCMar_Fbm3(q * float3(1.0, 0.45, 1.0), 2.6);
    float d1 = abs(s1 * 2.0 - 1.0);
    float main = PCMar_Band(d1, width);

    float s2 = PCMar_Fbm3(q * float3(2.3, 1.05, 2.3) + 27.7, 2.6);
    float d2 = abs(s2 * 2.0 - 1.0);
    float fine = PCMar_Band(d2, width * 0.45) * 0.6;

    return saturate(max(main, fine));
}

// ── Тон камня ────────────────────────────────────────────────────────────────
// Два fbm, вытянутые по направлению распила (CloudStretch), усреднены и приведены
// к 0..1 — мягкая облачность без резких границ.
float PCMar_Cloud(float3 ObjectPos, float CloudScale, float CloudStretch, float CloudDetail, float CloudSeed)
{
    float3 p = ObjectPos * max(0.001, CloudScale) + CloudSeed;
    float s = max(1.0, CloudStretch);
    float d = max(1.1, CloudDetail);
    float a = PCMar_Fbm3(float3(p.x * s, p.y, p.z * s), d);
    float b = PCMar_Fbm3(float3(p.x, p.y * s, p.z) + 13.7, d);
    return saturate(smoothstep(0.3, 0.7, 0.5 * (a + b)));
}

// ── Каверны ──────────────────────────────────────────────────────────────────
// Worley F1 по разрежённым ячейкам (≈20% ячеек содержат каверну), узкий радиус.
float PCMar_Pits(float3 ObjectPos, float PitScale, float PitSeed)
{
    float3 p = ObjectPos * max(0.001, PitScale) + PitSeed;
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
                    PCMar_Hash13(cell + 0.0),
                    PCMar_Hash13(cell + 17.0),
                    PCMar_Hash13(cell + 31.0));
                float empty = step(0.20, PCMar_Hash13(cell + 53.7)) * 8.0;
                minD = min(minD, length(cell + rnd - p) + empty);
            }
        }
    }
    return saturate(1.0 - smoothstep(0.06, 0.18, minD));
}

// ── Трещины (механизм царапин бронзы/дерева, 1:1) ───────────────────────────
float PCMar_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

float PCMar_CrackDir(float3 ObjectPos, float CrackScale, float SX, float SY, float SZ, float CrackDetail, float CrackSeed)
{
    float3 sp = ObjectPos * max(0.001, CrackScale) + CrackSeed;
    sp.x *= max(0.05, SX);
    sp.y *= max(0.05, SY);
    sp.z *= max(0.05, SZ);
    return PCMar_Ridge(PCMar_Fbm3(sp, CrackDetail));
}

float PCMar_Blotch(float3 ObjectPos, float BlotchScale, float BlotchSeed)
{
    float3 pp = ObjectPos * max(0.001, BlotchScale) + BlotchSeed;
    return PCMar_Fbm3(pp, 2.0);
}

void ProceduralMarbleMottle_float(
    float3 ObjectPos, float MottleScale, float MottleAmount, float MottleSeed,
    out float Mottle)
{
    Mottle = PCMar_Mottle(ObjectPos, MottleScale, MottleAmount, MottleSeed);
}

void ProceduralMarbleMottle_half(
    half3 ObjectPos, half MottleScale, half MottleAmount, half MottleSeed,
    out half Mottle)
{
    float m;
    ProceduralMarbleMottle_float(
        (float3)ObjectPos, (float)MottleScale, (float)MottleAmount, (float)MottleSeed, m);
    Mottle = (half)m;
}

void ProceduralMarbleVeins_float(
    float3 ObjectPos, float VeinScale, float VeinWidth, float VeinWarp, float Mottle, float VeinSeed,
    out float Veins)
{
    Veins = PCMar_Veins(ObjectPos, VeinScale, VeinWidth, VeinWarp, Mottle, VeinSeed);
}

void ProceduralMarbleVeins_half(
    half3 ObjectPos, half VeinScale, half VeinWidth, half VeinWarp, half Mottle, half VeinSeed,
    out half Veins)
{
    float v;
    ProceduralMarbleVeins_float(
        (float3)ObjectPos, (float)VeinScale, (float)VeinWidth, (float)VeinWarp, (float)Mottle, (float)VeinSeed, v);
    Veins = (half)v;
}

void ProceduralMarbleCloud_float(
    float3 ObjectPos, float CloudScale, float CloudStretch, float CloudDetail, float CloudSeed,
    out float Cloud)
{
    Cloud = PCMar_Cloud(ObjectPos, CloudScale, CloudStretch, CloudDetail, CloudSeed);
}

void ProceduralMarbleCloud_half(
    half3 ObjectPos, half CloudScale, half CloudStretch, half CloudDetail, half CloudSeed,
    out half Cloud)
{
    float c;
    ProceduralMarbleCloud_float(
        (float3)ObjectPos, (float)CloudScale, (float)CloudStretch, (float)CloudDetail, (float)CloudSeed, c);
    Cloud = (half)c;
}

void ProceduralMarblePits_float(
    float3 ObjectPos, float PitScale, float PitSeed,
    out float Pits)
{
    Pits = PCMar_Pits(ObjectPos, PitScale, PitSeed);
}

void ProceduralMarblePits_half(
    half3 ObjectPos, half PitScale, half PitSeed,
    out half Pits)
{
    float p;
    ProceduralMarblePits_float((float3)ObjectPos, (float)PitScale, (float)PitSeed, p);
    Pits = (half)p;
}

void ProceduralMarbleCrackA_float(
    float3 ObjectPos,
    float CrackScale, float CrackStretch, float CrackDetail, float CrackSeed,
    out float Crack)
{
    Crack = PCMar_CrackDir(ObjectPos, CrackScale, CrackStretch, 1.0, CrackStretch, CrackDetail, CrackSeed);
}

void ProceduralMarbleCrackA_half(
    half3 ObjectPos,
    half CrackScale, half CrackStretch, half CrackDetail, half CrackSeed,
    out half Crack)
{
    float c;
    ProceduralMarbleCrackA_float(
        (float3)ObjectPos,
        (float)CrackScale, (float)CrackStretch, (float)CrackDetail, (float)CrackSeed, c);
    Crack = (half)c;
}

void ProceduralMarbleCrackB_float(
    float3 ObjectPos,
    float CrackScale, float CrackStretch, float CrackDetail, float CrackSeed,
    out float CrackB)
{
    CrackB = PCMar_CrackDir(ObjectPos, CrackScale, 1.0, CrackStretch, 1.0, CrackDetail, CrackSeed + 17.3);
}

void ProceduralMarbleCrackB_half(
    half3 ObjectPos,
    half CrackScale, half CrackStretch, half CrackDetail, half CrackSeed,
    out half CrackB)
{
    float b;
    ProceduralMarbleCrackB_float(
        (float3)ObjectPos,
        (float)CrackScale, (float)CrackStretch, (float)CrackDetail, (float)CrackSeed, b);
    CrackB = (half)b;
}

void ProceduralMarbleCrackC_float(
    float3 ObjectPos,
    float CrackScale, float CrackStretch, float CrackDetail, float CrackSeed,
    float CrackAngle,
    out float CrackC)
{
    float ca = cos(CrackAngle);
    float sn = sin(CrackAngle);
    float3 sc = ObjectPos * max(0.001, CrackScale) + CrackSeed + 41.1;
    sc.xy = float2(sc.x * ca - sc.y * sn, sc.x * sn + sc.y * ca);
    sc.x *= max(0.05, CrackStretch);
    CrackC = PCMar_Ridge(PCMar_Fbm3(sc, CrackDetail));
}

void ProceduralMarbleCrackC_half(
    half3 ObjectPos,
    half CrackScale, half CrackStretch, half CrackDetail, half CrackSeed,
    half CrackAngle,
    out half CrackC)
{
    float c;
    ProceduralMarbleCrackC_float(
        (float3)ObjectPos,
        (float)CrackScale, (float)CrackStretch, (float)CrackDetail, (float)CrackSeed,
        (float)CrackAngle, c);
    CrackC = (half)c;
}

void ProceduralMarbleCrackMask_float(
    float3 ObjectPos,
    float CrackMaskScale, float CrackMaskSeed,
    out float CrackMask)
{
    CrackMask = PCMar_Blotch(ObjectPos, CrackMaskScale, CrackMaskSeed);
}

void ProceduralMarbleCrackMask_half(
    half3 ObjectPos,
    half CrackMaskScale, half CrackMaskSeed,
    out half CrackMask)
{
    float m;
    ProceduralMarbleCrackMask_float(
        (float3)ObjectPos,
        (float)CrackMaskScale, (float)CrackMaskSeed, m);
    CrackMask = (half)m;
}

#endif
