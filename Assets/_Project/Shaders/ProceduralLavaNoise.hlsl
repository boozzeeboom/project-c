#ifndef PROCEDURAL_LAVA_NOISE_INCLUDED
#define PROCEDURAL_LAVA_NOISE_INCLUDED

// ProceduralLavaNoise — object-space 3D шум ЛАВЫ (потоки магмы, остывающая
// корка, раскалённые трещины). Материал Lava собран с нуля: у Brick взяты
// ТОЛЬКО шаблоны документов графа, код ни от одного материала не наследуется.
//
// КАДР. Лава — это ДВА состояния одного вещества: тёмная остывшая корка
// (базальт) и расплав под ней. Корка набрана плитами (ячейки Worley), между
// плитами — трещины; сквозь трещины и в тонких местах корки виден расплав.
// Поэтому слои делятся на «корку» (тон плиты, зола) и «расплав» (жар, струи,
// ядро), а альбедо и ЭМИССИЯ берут одни и те же поля. Порядок наложения —
// от старого к новому: корка → зола → жар → расплав → трещина → ядро → верёвки.
//
//   Локальная Y — ВВЕРХ, вдоль неё вытянуты и ячейки, и линии потока: лава
//   течёт вниз по локальной Y.
//   Один объект = один поток/валун лавы. UV не нужны.
//
// Слои:
//   ProceduralLavaFissures  — трещины между плитами корки (Worley F2 − F1)
//   ProceduralLavaPlateTone — тон плиты корки (хеш той же ячейки Worley:
//                             тон и его границы физически не могут разъехаться)
//   ProceduralLavaHeat      — крупное поле жара: где корка тонка, сквозь неё
//                             просвечивает расплав
//   ProceduralLavaFlow      — линии потока вдоль локальной Y («верёвки» пахоэхоэ)
//   ProceduralLavaSkin      — мелкая шлаковая корка (пузыри): тон и рельеф
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

float PCLava_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float PCLava_ValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = PCLava_Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = PCLava_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = PCLava_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = PCLava_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = PCLava_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = PCLava_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = PCLava_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = PCLava_Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float ny0 = lerp(nx00, nx10, f.y);
    float ny1 = lerp(nx01, nx11, f.y);
    return lerp(ny0, ny1, f.z);
}

float PCLava_Fbm3(float3 p, float detail)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    [unroll]
    for (int o = 0; o < 3; o++)
    {
        sum += amp * PCLava_ValueNoise3(p * freq);
        freq *= max(1.1, detail);
        amp *= 0.5;
    }
    return sum / 0.875;
}

// Worley (3x3x3): F1 — расстояние до ближайшего центра ячейки, F2 — до второго.
// Возвращает F2 − F1, то есть расстояние ДО ГРАНИЦЫ ячейки: 0 на трещине между
// плитами и растёт внутрь плиты. cellTone — хеш выигравшей ячейки: величина,
// постоянная внутри плиты и скачком меняющаяся на её границе (тон плиты).
float PCLava_WorleyF2F1(float3 p, out float cellTone)
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
                    PCLava_Hash13(cell + 0.0),
                    PCLava_Hash13(cell + 17.0),
                    PCLava_Hash13(cell + 31.0));
                float d = length(cell + rnd - p);
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                    best = PCLava_Hash13(cell + 53.0);
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

// ── Трещины корки ────────────────────────────────────────────────────────────
// Трещина = граница ячейки Worley. Ячейки вытянуты вдоль локальной Y
// (CrustStretch), поэтому плиты корки длинные и лежат вдоль потока, а трещины
// идут вдоль него же, а не кольцами.
// 1 — трещина, 0 — тело плиты.
void ProceduralLavaFissures_float(
    float3 ObjectPos,
    float CrustScale, float CrustStretch, float CrustWidth, float CrustSeed,
    out float Fissures)
{
    float3 p = ObjectPos * max(0.001, CrustScale) + CrustSeed;
    p.y /= max(1.0, CrustStretch);
    float plateTone;
    float border = PCLava_WorleyF2F1(p, plateTone);
    float w = max(0.004, CrustWidth);
    Fissures = saturate(1.0 - smoothstep(0.0, w, border));
}

// ── Тон плиты корки ──────────────────────────────────────────────────────────
// Постоянен внутри плиты и скачком меняется на её границе: остывшие плиты
// отличаются друг от друга толщиной корки и цветом. Считается тем же Worley и
// тем же доменом, что и трещины, поэтому при совпадающих Scale/Stretch/Seed
// плиты и их границы лежат в одной сетке.
void ProceduralLavaPlateTone_float(
    float3 ObjectPos,
    float CrustScale, float CrustStretch, float CrustSeed,
    out float PlateTone)
{
    float3 p = ObjectPos * max(0.001, CrustScale) + CrustSeed;
    p.y /= max(1.0, CrustStretch);
    float plateTone;
    PCLava_WorleyF2F1(p, plateTone);
    PlateTone = saturate(plateTone);
}

// ── Жар ──────────────────────────────────────────────────────────────────────
// Крупное поле, решающее ГДЕ корка тонка и сквозь неё виден расплав. Пороги
// ставятся в графе (Smoothstep): один рисунок берётся в двух ролях — «жар»
// (тонкая корка, тёмно-красное свечение) и «расплав» (корки нет вовсе).
// Домен свёрнут домен-warp'ом; амплитуда свёртки жёстко 0.35 — без него пятна
// расплава выходят ровными кляксами, но отдельный регулятор здесь был бы
// третьим множителем одного и того же.
void ProceduralLavaHeat_float(
    float3 ObjectPos,
    float HeatScale, float HeatStretch, float HeatSeed,
    out float Heat)
{
    float3 p = ObjectPos * max(0.001, HeatScale) + HeatSeed;
    p.y /= max(1.0, HeatStretch);
    float3 w = float3(
        PCLava_ValueNoise3(p * 0.5 + 3.1),
        PCLava_ValueNoise3(p * 0.5 + 17.9),
        PCLava_ValueNoise3(p * 0.5 + 41.7)) - 0.5;
    p += w * 0.35;
    Heat = saturate(PCLava_Fbm3(p, 2.0));
}

// ── Линии потока ─────────────────────────────────────────────────────────────
// «Хребет» fbm — там, где fbm проходит через 0.5 (0 на линии, дальше в
// сторону). Сильное растяжение по локальной Y (FlowStretch) превращает эти
// линии в струи вдоль потока: лава течёт вниз, корка на гребнях струй
// остывает первой — отсюда и тёмные «верёвки» в альбедо, и подъём в рельефе.
// Линии намеренно НЕ рвутся гейтом: поток — непрерывная струя, а не сетка.
void ProceduralLavaFlow_float(
    float3 ObjectPos,
    float FlowScale, float FlowStretch, float FlowWidth, float FlowSeed,
    out float Flow)
{
    float3 p = ObjectPos * max(0.001, FlowScale) + FlowSeed;
    p.y /= max(1.0, FlowStretch);
    float ridge = abs(PCLava_Fbm3(p, 2.0) * 2.0 - 1.0);
    float w = max(0.004, FlowWidth);
    Flow = saturate(1.0 - smoothstep(0.0, w, ridge));
}

// ── Шлаковая корка ───────────────────────────────────────────────────────────
// Мелкий fbm без порогов: в графе он и зола в тоне (гасит свечение), и мелкий
// рельеф (пузыри и шлак на поверхности плиты). Detail передаётся в fbm как
// множитель частоты октав: 0 (неподключённый слот) упирается в 1.1.
void ProceduralLavaSkin_float(
    float3 ObjectPos,
    float SkinScale, float SkinDetail, float SkinSeed,
    out float Skin)
{
    float3 p = ObjectPos * max(0.001, SkinScale) + SkinSeed;
    Skin = saturate(PCLava_Fbm3(p, max(1.1, SkinDetail)));
}

// ── Обёртки CF-нод (одно-выходные, File-режим) ───────────────────────────────

void ProceduralLavaFissures_half(
    half3 ObjectPos,
    half CrustScale, half CrustStretch, half CrustWidth, half CrustSeed,
    out half Fissures)
{
    float v;
    ProceduralLavaFissures_float(
        (float3)ObjectPos,
        (float)CrustScale, (float)CrustStretch, (float)CrustWidth, (float)CrustSeed, v);
    Fissures = (half)v;
}

void ProceduralLavaPlateTone_half(
    half3 ObjectPos,
    half CrustScale, half CrustStretch, half CrustSeed,
    out half PlateTone)
{
    float v;
    ProceduralLavaPlateTone_float(
        (float3)ObjectPos,
        (float)CrustScale, (float)CrustStretch, (float)CrustSeed, v);
    PlateTone = (half)v;
}

void ProceduralLavaHeat_half(
    half3 ObjectPos,
    half HeatScale, half HeatStretch, half HeatSeed,
    out half Heat)
{
    float v;
    ProceduralLavaHeat_float(
        (float3)ObjectPos,
        (float)HeatScale, (float)HeatStretch, (float)HeatSeed, v);
    Heat = (half)v;
}

void ProceduralLavaFlow_half(
    half3 ObjectPos,
    half FlowScale, half FlowStretch, half FlowWidth, half FlowSeed,
    out half Flow)
{
    float v;
    ProceduralLavaFlow_float(
        (float3)ObjectPos,
        (float)FlowScale, (float)FlowStretch, (float)FlowWidth, (float)FlowSeed, v);
    Flow = (half)v;
}

void ProceduralLavaSkin_half(
    half3 ObjectPos,
    half SkinScale, half SkinDetail, half SkinSeed,
    out half Skin)
{
    float v;
    ProceduralLavaSkin_float(
        (float3)ObjectPos,
        (float)SkinScale, (float)SkinDetail, (float)SkinSeed, v);
    Skin = (half)v;
}

#endif
