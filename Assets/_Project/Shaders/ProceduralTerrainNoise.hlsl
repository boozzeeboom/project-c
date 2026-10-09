// ProceduralTerrainNoise.hlsl
// Шумовая библиотека процедурного материала террейна (PCTrn_*).
//
// КОНТРАКТ T-BRONZE01:
//   * каждая функция отдаёт ОДНО значение (single output);
//   * функции используются только через Custom Function нод (File mode), а нода
//     зовёт ИМЯ_float / ИМЯ_half (hlslFunctionName = m_FunctionName + "_$precision",
//     CustomFunctionNode.cs:110), передавая входы по позициям слотов, а выход —
//     последним out-параметром. Поэтому на каждую CF-ноду нужна пара
//     void ИМЯ_float(...) / void ИМЯ_half(...) — см. блок «CF-обёртки» в конце;
//   * сигнатуры заморожены: слоты CF-нод привязываются по номеру, менять
//     порядок/состав параметров существующей обёртки нельзя — новое поведение
//     идёт в НОВУЮ функцию с новым именем;
//   * хеш sin-free (Hoskins), потому что объектные координаты террейна доходят
//     до 80 000 м и sin() там даёт артефакты.
//
// Входные координаты всегда объектные (Position.Object) — правило Floating Origin:
// мировой Y в проекте переписывается рефокусом, объектный Y = высота в метрах.

#ifndef PCTRN_NOISE_INCLUDED
#define PCTRN_NOISE_INCLUDED

// ------------------------------------------------------------------ хелперы

float PCTrn_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCTrn_Hash12(float2 p)
{
    float3 q = float3(p.x, p.y, p.x * 0.37 + p.y * 0.13);
    return PCTrn_Hash13(q);
}

// Value noise, [0..1]
float PCTrn_Noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCTrn_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCTrn_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCTrn_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCTrn_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCTrn_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCTrn_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCTrn_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCTrn_Hash13(i + float3(1.0, 1.0, 1.0));

    float x00 = lerp(n000, n100, f.x);
    float x10 = lerp(n010, n110, f.x);
    float x01 = lerp(n001, n101, f.x);
    float x11 = lerp(n011, n111, f.x);
    float y0 = lerp(x00, x10, f.y);
    float y1 = lerp(x01, x11, f.y);
    return lerp(y0, y1, f.z);
}

// FBM, [0..1]. octaves <= 1.1 (пол) — защита от деления на ноль при параметре 0.
float PCTrn_Fbm3(float3 p, float octaves)
{
    float oct = max(octaves, 1.1);
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;
    float3 q = p;
    for (int k = 0; k < 6; k++)
    {
        if ((float)k >= oct) break;
        sum += PCTrn_Noise3(q) * amp;
        norm += amp;
        amp *= 0.5;
        q *= 2.03;
    }
    return saturate(sum / max(norm, 0.0001));
}

// Ridged FBM, [0..1] — резкие гребни (слои породы, жилы).
float PCTrn_Ridge3(float3 p, float octaves)
{
    float oct = max(octaves, 1.1);
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;
    float3 q = p;
    for (int k = 0; k < 6; k++)
    {
        if ((float)k >= oct) break;
        float n = PCTrn_Noise3(q);
        n = 1.0 - abs(n * 2.0 - 1.0);
        sum += n * n * amp;
        norm += amp;
        amp *= 0.5;
        q *= 2.07;
    }
    return saturate(sum / max(norm, 0.0001));
}

// Worley F2-F1 по сетке XZ (9 ячеек). 0 на границе ячеек, растёт внутрь.
float PCTrn_WorleyEdge2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float f1 = 8.0;
    float f2 = 8.0;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2((float)x, (float)y);
            float2 h = float2(PCTrn_Hash12(i + g), PCTrn_Hash12(i + g + float2(19.7, 7.3)));
            float2 o = g + h - f;
            float d = dot(o, o);
            if (d < f1) { f2 = f1; f1 = d; }
            else if (d < f2) { f2 = d; }
        }
    }
    return saturate(sqrt(f2) - sqrt(f1));
}

// Случайное значение на ячейку XZ, [0..1] — тон блока породы.
float PCTrn_WorleyCell2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float best = 8.0;
    float2 bestCell = i;
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2((float)x, (float)y);
            float2 h = float2(PCTrn_Hash12(i + g), PCTrn_Hash12(i + g + float2(19.7, 7.3)));
            float2 o = g + h - f;
            float d = dot(o, o);
            if (d < best) { best = d; bestCell = i + g; }
        }
    }
    return PCTrn_Hash12(bestCell);
}

// ------------------------------------------- функции, подключённые к CF-нодам

// Наклон поверхности в градусах: 0 — плато, 90 — стена.
// normalWS должен быть мировым нормалем; у террейна нет поворота/масштаба,
// поэтому мировой верх совпадает с объектным (Floating Origin безопасен).
float PCTrn_SlopeAngle(float3 NormalWS)
{
    float3 n = normalize(NormalWS);
    float d = length(n.xz);
    return degrees(atan2(d, max(n.y, 0.0001)));
}

// Дрожание порогов зон: возвращает [-1..1].
float PCTrn_ZoneWarp(float3 ObjectPos, float Scale, float Seed)
{
    float n = PCTrn_Fbm3(ObjectPos * Scale + Seed, 2.0);
    return n * 2.0 - 1.0;
}

// Слоистость породы: горизонтальные пласты, 0..amount.
float PCTrn_RockStrata(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float3 q = ObjectPos * Scale + Seed;
    q.xz *= 0.16;               // пласты вытянуты по горизонтали
    q.y *= 1.35;
    float r = PCTrn_Ridge3(q, 4.0);
    float band = saturate(r * 1.35);
    return saturate(Amount * band);
}

// Трещины/разломы между блоками породы, 0..amount (0 — целая порода).
float PCTrn_RockJoints(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float2 q = ObjectPos.xz * Scale + Seed;
    float row = floor(ObjectPos.y * Scale * 0.35);
    q += PCTrn_Hash13(float3(row, Seed, 0.0)) * 4.0 - 2.0;
    float e = PCTrn_WorleyEdge2D(q);
    float j = 1.0 - saturate(e * 3.2);
    return saturate(Amount * j * j);
}

// Тон отдельного блока породы, [0..amount].
float PCTrn_RockBlockTone(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float2 q = ObjectPos.xz * Scale + Seed;
    float c = PCTrn_WorleyCell2D(q);
    return saturate(Amount * c);
}

// Мелкая зернистость/выбоины породы, [0..amount].
float PCTrn_RockDetail(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float3 q = ObjectPos * Scale + Seed;
    float n = PCTrn_Fbm3(q, 4.0);
    float pit = 1.0 - saturate(PCTrn_WorleyEdge2D(q.xz * 1.7) * 2.5);
    return saturate(Amount * saturate(n * 0.75 + pit * 0.35));
}

// Осыпь/щебень: только для плоских мест (маска плоскости — в графе), [0..amount].
float PCTrn_Scree(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float3 q = ObjectPos * Scale + Seed;
    float chunk = 1.0 - saturate(PCTrn_WorleyEdge2D(q.xz) * 2.2);
    float grain = 1.0 - PCTrn_Fbm3(q * 3.5, 3.0);
    return saturate(Amount * saturate(chunk * 0.7 + grain * 0.4));
}

// Снежный надув (drift): fbm, вытянутый по горизонтали, [0..amount].
float PCTrn_SnowDrift(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float3 q = ObjectPos * Scale + Seed;
    q.y *= 0.35;
    float n = PCTrn_Fbm3(q, 3.0);
    return saturate(Amount * n);
}

// Снежная крупа: мелкая частота для блика/рельефа, [0..amount].
float PCTrn_SnowGrain(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float3 q = ObjectPos * Scale + Seed;
    float n = PCTrn_Fbm3(q, 2.0);
    float g = PCTrn_Noise3(q * 2.7);
    return saturate(Amount * saturate(n * 0.6 + g * 0.6));
}

// Сеть трещин льда, 0..amount.
float PCTrn_IceCracks(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float2 q = ObjectPos.xz * Scale + Seed;
    float warp = PCTrn_Fbm3(float3(q * 0.5, Seed), 2.0);
    float2 w = q + (warp * 2.0 - 1.0) * 0.65;
    float e = PCTrn_WorleyEdge2D(w);
    float c = 1.0 - saturate(e * 4.5);
    return saturate(Amount * c * c);
}

// Зелёная крапчатость низин, [0..amount].
float PCTrn_LowMottle(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float3 q = ObjectPos * Scale + Seed;
    float n = PCTrn_Fbm3(q, 4.0);
    float patch = 1.0 - saturate(PCTrn_WorleyEdge2D(q.xz * 0.7) * 3.0);
    return saturate(Amount * saturate(n * 0.8 + patch * 0.35));
}

// Крупные пятна (выгоревшая/обезгаженная земля), [0..amount].
float PCTrn_LowPatches(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float2 q = ObjectPos.xz * Scale + Seed;
    float p = PCTrn_Fbm3(float3(q, Seed), 2.0);
    float c = PCTrn_WorleyCell2D(q * 0.45);
    return saturate(Amount * saturate(p * 0.7 + c * 0.6));
}

// Пыль/светлый налёт: применимость (плоскость) — в графе, [0..amount].
float PCTrn_Dust(float3 ObjectPos, float Scale, float Amount, float Seed)
{
    float3 q = ObjectPos * Scale + Seed;
    float n = PCTrn_Fbm3(q, 3.0);
    float streak = PCTrn_Noise3(float3(q.x * 0.3, q.y * 1.8, q.z * 0.3));
    return saturate(Amount * saturate(n * 0.7 + streak * 0.4));
}

// ======================================== CF-обёртки (их и зовут CF-ноды)
//
// ShaderGraph (File mode) генерирует вызов
//     <m_FunctionName>_float(<входы по порядку слотов>, <выход>);
// т.е. функция обязана быть void и отдавать результат через out последним
// параметром. Порядок параметров ниже = порядок слотов CF-нод
// в Terrain.shadergraph (ObjectPos/NormalWS, Scale, Amount, Seed, Out).
// Реализация — в value-returning функциях выше; обёртки только адаптируют форму.

void PCTrn_SlopeAngle_float(float3 NormalWS, out float SlopeAngle)
{
    SlopeAngle = PCTrn_SlopeAngle(NormalWS);
}

void PCTrn_SlopeAngle_half(half3 NormalWS, out half SlopeAngle)
{
    SlopeAngle = (half)PCTrn_SlopeAngle((float3)NormalWS);
}

void PCTrn_ZoneWarp_float(float3 ObjectPos, float Scale, float Seed, out float ZoneWarp)
{
    ZoneWarp = PCTrn_ZoneWarp(ObjectPos, Scale, Seed);
}

void PCTrn_ZoneWarp_half(half3 ObjectPos, half Scale, half Seed, out half ZoneWarp)
{
    ZoneWarp = (half)PCTrn_ZoneWarp((float3)ObjectPos, (float)Scale, (float)Seed);
}

void PCTrn_RockStrata_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float RockStrata)
{
    RockStrata = PCTrn_RockStrata(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_RockStrata_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half RockStrata)
{
    RockStrata = (half)PCTrn_RockStrata((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_RockJoints_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float RockJoints)
{
    RockJoints = PCTrn_RockJoints(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_RockJoints_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half RockJoints)
{
    RockJoints = (half)PCTrn_RockJoints((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_RockBlockTone_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float RockBlockTone)
{
    RockBlockTone = PCTrn_RockBlockTone(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_RockBlockTone_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half RockBlockTone)
{
    RockBlockTone = (half)PCTrn_RockBlockTone((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_RockDetail_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float RockDetail)
{
    RockDetail = PCTrn_RockDetail(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_RockDetail_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half RockDetail)
{
    RockDetail = (half)PCTrn_RockDetail((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_Scree_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float Scree)
{
    Scree = PCTrn_Scree(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_Scree_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half Scree)
{
    Scree = (half)PCTrn_Scree((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_SnowDrift_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float SnowDrift)
{
    SnowDrift = PCTrn_SnowDrift(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_SnowDrift_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half SnowDrift)
{
    SnowDrift = (half)PCTrn_SnowDrift((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_SnowGrain_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float SnowGrain)
{
    SnowGrain = PCTrn_SnowGrain(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_SnowGrain_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half SnowGrain)
{
    SnowGrain = (half)PCTrn_SnowGrain((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_IceCracks_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float IceCracks)
{
    IceCracks = PCTrn_IceCracks(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_IceCracks_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half IceCracks)
{
    IceCracks = (half)PCTrn_IceCracks((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_LowMottle_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float LowMottle)
{
    LowMottle = PCTrn_LowMottle(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_LowMottle_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half LowMottle)
{
    LowMottle = (half)PCTrn_LowMottle((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_LowPatches_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float LowPatches)
{
    LowPatches = PCTrn_LowPatches(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_LowPatches_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half LowPatches)
{
    LowPatches = (half)PCTrn_LowPatches((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

void PCTrn_Dust_float(float3 ObjectPos, float Scale, float Amount, float Seed, out float Dust)
{
    Dust = PCTrn_Dust(ObjectPos, Scale, Amount, Seed);
}

void PCTrn_Dust_half(half3 ObjectPos, half Scale, half Amount, half Seed, out half Dust)
{
    Dust = (half)PCTrn_Dust((float3)ObjectPos, (float)Scale, (float)Amount, (float)Seed);
}

#endif
