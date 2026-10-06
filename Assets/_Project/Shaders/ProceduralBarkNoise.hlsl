#ifndef PROCEDURAL_BARK_NOISE_INCLUDED
#define PROCEDURAL_BARK_NOISE_INCLUDED

// ProceduralBarkNoise — object-space 3D noise для КОРЫ ДЕРЕВА (Bark), v2.
//
// ВТОРАЯ ПОПЫТКА. v1 строился на ячейках Worley (плитки коры + тон ячейки) и на
// визуальном тесте читался как КАМЕННАЯ КЛАДКА: дискретные ячейки с разным тоном
// дают «камни», а не кору. v1 не выброшен — он сохранён отдельным материалом
// Cobble (ProceduralCobbleNoise.hlsl, тот же рисунок под именем мостовой).
// В v2 ячеек Worley нет ВООБЩЕ.
//
// Что держит кору здесь:
//   ProceduralBarkFlutes     — ПРОДОЛЬНЫЕ борозды: РАССТОЯНИЕ до линии fbm,
//                              сильно растянутой по локальной Y (ось ствола)
//   ProceduralBarkSkin       — «ШКУРКА»: обычный 3-октавный fbm как ТОН и как
//                              сильная ВЫСОТА (главный слой рельефа)
//   ProceduralBarkSkinDetail — мелкая чешуйчатость: ridged-версия ТОЙ ЖЕ шкурки,
//                              считается из той же сетки Scale/Stretch/Seed
//   ProceduralBarkBreaks     — короткие поперечные разрывы коры
//   ProceduralBarkPatch      — общий примитив «пятно fbm»: лишайник, мох,
//                              обнажённая древесина (порог ставится в графе)
//
// ЗАИМСТВОВАНИЕ У ЖЕЛЕЗА (IronRust) — ТОЛЬКО ИДЕЯ: у железа выкрученный до
// предела бамп по ржавчине (обычный fbm как поле высоты) даёт рисунок, похожий
// на шкурку дерева. Здесь взят ровно этот приём — ProceduralBarkSkin работает
// как высота с большим коэффициентом (_Skin_Bump). Ржавчины, патины,
// металличности, масок кромок и наборов царапин здесь нет.
//
// ПРАВИЛО (T-BRONZE01): функции, на которые ссылаются CF-ноды, — ОДНО-выходные
// (File-режим). Сигнатуры = порядок слотов CF-нод в графе. Новое поведение —
// новой функцией; существующие сигнатуры не менять (слоты связываются по номеру).
//
// Хеш без sin (Hoskins-style). Всё считается из object-space координат (FO-safe).
// AbsoluteWorld не используется.

float PCBark_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCBark_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCBark_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCBark_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCBark_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCBark_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCBark_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCBark_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCBark_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCBark_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCBark_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCBark_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Борозда = РАССТОЯНИЕ до «линии» fbm (там, где fbm проходит через 0.5):
// 0 на самой линии, 1 в стороне от неё. Ширина борозды задаётся отдельным
// параметром (FurrowWidth), поэтому нужна именно дистанция, а не ridge.
float PCBark_Groove(float s)
{
    return abs(s * 2.0 - 1.0);
}

// Обратная величина: 1 ровно на «линии» fbm, 0 в стороне. Тонкие гребни —
// из них собирается чешуйчатость коры (ProceduralBarkSkinDetail).
float PCBark_Ridge(float s)
{
    return 1.0 - abs(s * 2.0 - 1.0);
}

// ── Борозды вдоль ствола ─────────────────────────────────────────────────────
// Кора растрескивается ВДОЛЬ ствола: борозды длинные и почти прямые.
// Деление на FurrowStretch по локальной Y растягивает линии fbm по стволу,
// домен-warp их изгибает, а крупный fbm решает, ГДЕ борозда раскрылась — сплошная
// сеть по всей поверхности читалась бы как шум. Раскрытие модулирует глубину,
// но не обрывает борозду насовсем (нижняя граница 0.40).
float PCBark_Flutes(float3 ObjectPos, float FurrowScale, float FurrowStretch, float FurrowWidth, float FurrowWarp, float FurrowSeed)
{
    float3 p = ObjectPos * max(0.001, FurrowScale) + FurrowSeed;
    p.y /= max(0.05, FurrowStretch);
    if (FurrowWarp > 0.0001)
    {
        float3 w = float3(
            PCBark_ValueNoise3(p * 0.5 + 3.1),
            PCBark_ValueNoise3(p * 0.5 + 17.9),
            PCBark_ValueNoise3(p * 0.5 + 41.7)) - 0.5;
        p += w * FurrowWarp;
    }
    float d = PCBark_Groove(PCBark_Fbm3(p, 2.0));
    float w = max(0.004, FurrowWidth);
    float crack = saturate(1.0 - smoothstep(0.0, w, d));
    float opened = smoothstep(0.28, 0.78, PCBark_Fbm3(p * 0.30 + 7.7, 2.0));
    return saturate(crack * lerp(0.40, 1.0, opened));
}

// ── Шкурка ───────────────────────────────────────────────────────────────────
// Ядро второй попытки: не маска и не ячейка, а просто fbm — и он же главная
// ВЫСОТА (в графе идёт в сумму рельефа со своим _Skin_Bump). Тот же приём, что
// даёт «шкурку дерева» при выкрученном бампе ржавчины у железа. Деление на
// SkinStretch по локальной Y вытягивает рисунок вдоль ствола — поперёк ствола
// кора изрезана бороздами, а не этим слоем.
float PCBark_Skin(float3 ObjectPos, float SkinScale, float SkinStretch, float SkinDetail, float SkinSeed)
{
    float3 p = ObjectPos * max(0.001, SkinScale) + SkinSeed;
    p.y /= max(0.05, SkinStretch);
    return saturate(PCBark_Fbm3(p, max(1.1, SkinDetail)));
}

// ── Чешуйчатость ─────────────────────────────────────────────────────────────
// Ridged-версия той же шкурки: тонкие гребни там, где fbm проходит через 0.5.
// Считается из ТОЙ ЖЕ сетки (Scale/Stretch/Seed), поэтому гребни лежат внутри
// рисунка шкурки, а не отдельным слоем поверх.
// ВНИМАНИЕ: detail здесь жёстко 2.4 (у слоя в графе нет своего входа detail —
// структура слотов 4+3). Если менять _Skin_Detail, расхождение допустимо:
// это отдельная мелкая деталь, но расхождение надо держать в голове.
float PCBark_SkinDetail(float3 ObjectPos, float SkinScale, float SkinStretch, float SkinSeed)
{
    float3 p = ObjectPos * max(0.001, SkinScale) + SkinSeed;
    p.y /= max(0.05, SkinStretch);
    return saturate(PCBark_Ridge(PCBark_Fbm3(p, 2.4)));
}

// ── Короткие поперечные разрывы ──────────────────────────────────────────────
// Второй, более редкий рисунок коры: короткие разрывы ПОПЕРЁК ствола.
// Здесь медленно меняются XZ (BreakStretch) и быстро — Y, поэтому линии ложатся
// поперёк ствола; крупный fbm рвёт их на отдельные отрезки (patch).
float PCBark_Breaks(float3 ObjectPos, float BreakScale, float BreakStretch, float BreakWidth, float BreakSeed)
{
    float3 p = ObjectPos * max(0.001, BreakScale) + BreakSeed;
    p.x /= max(0.05, BreakStretch);
    p.z /= max(0.05, BreakStretch);
    float d = PCBark_Groove(PCBark_Fbm3(p, 2.0));
    float w = max(0.004, BreakWidth);
    float crack = saturate(1.0 - smoothstep(0.0, w, d));
    float patch = smoothstep(0.34, 0.80, PCBark_Fbm3(p * 0.5 + 4.3, 2.0));
    return crack * patch;
}

// ── Пятно (общий примитив) ───────────────────────────────────────────────────
// fbm в 0..1 без порогов: порог ставится в графе (Smoothstep), потому что один
// и тот же рисунок используется в трёх ролях с разной полярностью.
//   PatchStretch = 1  -> изотропные пятна (лишайник, обнажённая древесина)
//   PatchStretch > 1  -> рисунок вытянут вдоль ЛОКАЛЬНОЙ Y (мох по стволу)
float PCBark_Patch(float3 ObjectPos, float PatchScale, float PatchStretch, float PatchSeed)
{
    float3 p = ObjectPos * max(0.001, PatchScale) + PatchSeed;
    p.y /= max(0.05, PatchStretch);
    return saturate(PCBark_Fbm3(p, 2.0));
}

void ProceduralBarkFlutes_float(
    float3 ObjectPos,
    float FurrowScale, float FurrowStretch, float FurrowWidth, float FurrowWarp, float FurrowSeed,
    out float Flutes)
{
    Flutes = PCBark_Flutes(ObjectPos, FurrowScale, FurrowStretch, FurrowWidth, FurrowWarp, FurrowSeed);
}

void ProceduralBarkFlutes_half(
    half3 ObjectPos,
    half FurrowScale, half FurrowStretch, half FurrowWidth, half FurrowWarp, half FurrowSeed,
    out half Flutes)
{
    float f;
    ProceduralBarkFlutes_float(
        (float3)ObjectPos,
        (float)FurrowScale, (float)FurrowStretch, (float)FurrowWidth, (float)FurrowWarp, (float)FurrowSeed, f);
    Flutes = (half)f;
}

void ProceduralBarkSkin_float(
    float3 ObjectPos,
    float SkinScale, float SkinStretch, float SkinDetail, float SkinSeed,
    out float Skin)
{
    Skin = PCBark_Skin(ObjectPos, SkinScale, SkinStretch, SkinDetail, SkinSeed);
}

void ProceduralBarkSkin_half(
    half3 ObjectPos,
    half SkinScale, half SkinStretch, half SkinDetail, half SkinSeed,
    out half Skin)
{
    float s;
    ProceduralBarkSkin_float(
        (float3)ObjectPos,
        (float)SkinScale, (float)SkinStretch, (float)SkinDetail, (float)SkinSeed, s);
    Skin = (half)s;
}

void ProceduralBarkSkinDetail_float(
    float3 ObjectPos,
    float SkinScale, float SkinStretch, float SkinSeed,
    out float SkinDetail)
{
    SkinDetail = PCBark_SkinDetail(ObjectPos, SkinScale, SkinStretch, SkinSeed);
}

void ProceduralBarkSkinDetail_half(
    half3 ObjectPos,
    half SkinScale, half SkinStretch, half SkinSeed,
    out half SkinDetail)
{
    float d;
    ProceduralBarkSkinDetail_float(
        (float3)ObjectPos,
        (float)SkinScale, (float)SkinStretch, (float)SkinSeed, d);
    SkinDetail = (half)d;
}

void ProceduralBarkBreaks_float(
    float3 ObjectPos,
    float BreakScale, float BreakStretch, float BreakWidth, float BreakSeed,
    out float Breaks)
{
    Breaks = PCBark_Breaks(ObjectPos, BreakScale, BreakStretch, BreakWidth, BreakSeed);
}

void ProceduralBarkBreaks_half(
    half3 ObjectPos,
    half BreakScale, half BreakStretch, half BreakWidth, half BreakSeed,
    out half Breaks)
{
    float c;
    ProceduralBarkBreaks_float(
        (float3)ObjectPos,
        (float)BreakScale, (float)BreakStretch, (float)BreakWidth, (float)BreakSeed, c);
    Breaks = (half)c;
}

void ProceduralBarkPatch_float(
    float3 ObjectPos,
    float PatchScale, float PatchStretch, float PatchSeed,
    out float Patch)
{
    Patch = PCBark_Patch(ObjectPos, PatchScale, PatchStretch, PatchSeed);
}

void ProceduralBarkPatch_half(
    half3 ObjectPos,
    half PatchScale, half PatchStretch, half PatchSeed,
    out half Patch)
{
    float b;
    ProceduralBarkPatch_float(
        (float3)ObjectPos,
        (float)PatchScale, (float)PatchStretch, (float)PatchSeed, b);
    Patch = (half)b;
}

#endif
