# Fabric — процедурная ткань (полотняное переплетение + пряжа + ворс)

> Статус: **PROTOTYPE v1** (граф — производная от `Wood.shadergraph`: топология и каркас
> кромок/затяжек унаследованы, все слои заменены тканевыми, пороги `Smoothstep` переведены
> в естественный порядок).
> Проверено агентом: импорт OK, `ShaderHasError = false`, `isSupported = true`,
> `GetShaderMessages = 0` после принудительной GPU-компиляции (offscreen-рендер куба —
> 0 magenta-пикселей), структурный аудит графа чистый, обязательная проверка рельефа пройдена.
> **Визуал и Play Mode — `NOT RUN`** (тесты и скриншоты делает только пользователь).

## Файлы

| Файл | Что это |
|---|---|
| `Assets/_Project/Materials/Fabric/Fabric.shadergraph` | граф, шейдер `Fabric` (URP Lit, Opaque, Metallic workflow) |
| `Assets/_Project/Materials/Fabric/M_PC_Fabric.mat` | пресет для крупных поверхностей (ковры, драпировки, тенты, знамёна) |
| `Assets/_Project/Materials/Fabric/SMALL_M_PC_Fabric.mat` | Material Variant от `M_PC_Fabric` для мелких предметов (подушки, мешки, одежда пропсов); переопределений пока нет — как у `SMALL_M_PC_Wood` |
| `Assets/_Project/Shaders/ProceduralFabricWeaveNoise.hlsl` | ядро шума (guid `3f19c6f27f242d345a3083b6c1fa292e`) |

⚠️ `Assets/_Project/Shaders/ProceduralFabricNoise.hlsl` и `Assets/_Project/Materials/Fabric_v2/`
— **другой, параллельно созданный материал** (свои функции `ProceduralFabricSlubs/Fibers/Neps/Fray*`,
свой guid `b7e1…`). С этим графом он не связан; граф `Fabric` его не включает
(`ProceduralFabricNoise.hlsl` в сгенерированном шейдере — 0 вхождений).

## Что построено

```
Position (object-space, m_Space = 0)
 ├─ ПРЯЖА     ProceduralFabricSlub        → утолщения нити, знаковое ±_Slub_Amount
 ├─ ПЕРЕПЛЕТЕНИЕ ProceduralFabricPlainWeave ← пряжа уходит во вход Slub (ширина нити)
 │              полотняное 1/1: основа/уток через клетку, круглое сечение нити,
 │              подъём над/под нитью, крутка; выход = маска просвета (1 = просвет, 0 = вершина нити)
 ├─ ВОРС/МЕЛАНЖ ProceduralFabricFuzz      → два набора волокон (вдоль Y и горизонтально)
 ├─ КАТЫШКИ   ProceduralFabricPilling     → редкие точки Worley (≈35% ячеек)
 ├─ КРОМКИ (каркас бронзы/дерева): |x|,|y|,|z| → Smoothstep(_Edge_Position − _Edge_Width, _Edge_Position, |c|)
 │              → попарные × → Add → Add → Saturate → × _Edge_Wear
 ├─ ЗАТЯЖКИ (каркас царапин): SnagA/B/C → Smoothstep(_Snag_Threshold, +_Snag_Band, snag)
 │              → Add → × Smoothstep(_Snag_Mask, +_Snag_Band, mask) → × _Snag_Amount
 └─ wear = Saturate(кромки + затяжки)

BaseColor  = Lerp(Lerp(Lerp(Lerp(_Fabric_Color, _Gap_Color, просвет × _Weave_Amount),
                                                _Heather_Color, ворс × _Fuzz_Amount),
                                                _Pilling_Color, катышки × _Pilling_Amount),
                                                _Wear_Color, wear)
Smoothness = Lerp(Lerp(_Smoothness, _Gap_Smoothness, просвет × _Weave_Amount), _Wear_Smoothness, wear)
NormalTS   = Normal From Height(ворс×_Fuzz_Bump + катышки×_Pilling_Bump + затяжки×_Snag_Bump
                                + просвет×_Weave_Bump, Strength = float(1))
Metallic / Emission / Occlusion — не подключены (диэлектрик)
```

Итог графа: 379 JSON-документов, 101 нода (92 + 9 блоков мастер-стека), **132 ребра**,
41 свойство блэкборда (36 float + 5 color), `doubleDriven = 0`, висячих ссылок 0,
дублей `m_ObjectId` 0, `AbsoluteWorld` (`m_Space = 4`) — 0. Файл LF-only.

### Отличие от Wood по существу

1. **Ни одного деревянного слоя**: функции `ProceduralWood*` и guid `ProceduralWoodNoise.hlsl`
   в графе — 0 вхождений; кольца/волокно/поры/сучки заменены переплетением/ворсом/катышками/пряжей.
2. **Пороги `Smoothstep` в естественном порядке** (`Edge1` = нижний порог, `Edge2` = верхний,
   `In` = сигнал) — у всех 7 нод переставлены входные слоты рёбер. В Wood входы перевёрнуты
   (наследие бронзы, там не трогается), из-за чего износ садился бы на середину граней;
   здесь потёртость — у рёбер, затяжки — по гребням шума. Подтверждено в сгенерированном шейдере:
   `Unity_Smoothstep_float(_Subtract…, _EdgePosition…, _Split…R, …)` и
   `Unity_Smoothstep_float(_SnagThreshold…, _Add…, …SnagA…, …)`.
3. **Рельеф переплетения отрицательный** (`_Weave_Bump = −0.005`): функция отдаёт маску
   просвета, поэтому знак «минус» делает просветы впадинами, а нити — выпуклыми.

## Требования к геометрии

1. **Плоскость рисунка берётся из ориентации грани** (плоская нормаль из `ddx/ddy` object-space
   позиции внутри `PlainWeave`): грань ⊥X → `(Z, Y)`, ⊥Z → `(X, Y)`, ⊥Y (пол/потолок) → `(X, Z)`.
   На стенах **основа идёт по локальной Y**, уток — горизонтально. На изогнутых мешах смена
   плоскости проходит по рёбрам треугольников (как жёсткий triplanar).
2. **UV и vertex colors не нужны.**
3. **Единичный габарит для кромочной маски**: `|x|,|y|,|z|` доходят до 1.0 на габарите 2×2×2;
   для куба 1×1×1 `_Edge_Position ≈ 0.5`.
4. **Масштаб объекта масштабирует рисунок** (object-space, FO-safe — осознанный компромисс).
5. **Анти-муар встроен**: когда пиксель шире ~1/3 шага нити, рисунок плавно гасится к среднему
   (0.45), вместе с ним гаснет и рельеф переплетения.

## Ядро шума — `ProceduralFabricWeaveNoise.hlsl`

Правило **T-BRONZE01** соблюдено: все функции одно-выходные (File-режим), сигнатуры = порядок
слотов CF-нод (порядок аргументов подтверждён в сгенерированном вызове `ProceduralFabricPlainWeave_float(IN.ObjectSpacePosition, _WeaveScale…, _WeaveGap…, _WeaveWobble…, …Slub_4…, _WeaveSeed…, …)`).

| Функция (имя CF-ноды) | Входы | Выход | Что считает |
|---|---|---|---|
| `ProceduralFabricSlub` | `ObjectPos, SlubScale, SlubAmount, SlubSeed` | `Slub` | `(fbm − 0.5)·2·Amount` — утолщение/утоньшение нити |
| `ProceduralFabricPlainWeave` | `ObjectPos, WeaveScale, WeaveGap, WeaveWobble, Slub, WeaveSeed` | `Weave` | выбор плоскости по грани → клетка `floor/frac`, чётность клетки = какая нить сверху, сечение `sqrt(1 − d²)`, подъём `0.55 ± 0.45·sin(πt)`, крутка ±10%, `1 − max(основа, уток)`, анти-муар по `fwidth` |
| `ProceduralFabricFuzz` | `ObjectPos, FuzzScale, FuzzStretch, FuzzDetail, FuzzSeed` | `Fuzz` | два fbm, вытянутые вдоль Y и вдоль XZ, `smoothstep(0.3, 0.7, среднее)` |
| `ProceduralFabricPilling` | `ObjectPos, PillingScale, PillingSeed` | `Pilling` | Worley F1 по разреженным ячейкам, `1 − smoothstep(0.10, 0.26, d)` |
| `ProceduralFabricSnagA/B/C` | `ObjectPos, SnagScale, SnagStretch, SnagDetail, SnagSeed` (+ `SnagAngle` у C) | `Snag` | механизм царапин бронзы/дерева 1:1: A — вдоль Y, B — горизонтально, C — под углом |
| `ProceduralFabricSnagMask` | `ObjectPos, SnagMaskScale, SnagMaskSeed` | `SnagMask` | fbm-пятно, гасящее затяжки на участках (вход `Seed` питается `_Snag_Mask`, как в Wood) |

Хеш без `sin` (Hoskins), value-noise + 3-октавный fbm. `PlainWeave` использует производные —
работает только во fragment-стадии (подключён только к фрагментным блокам).

## Свойства (41) и значения пресета `M_PC_Fabric.mat`

Значения пресета = дефолты графа (свежий материал от шейдера `Fabric` выглядит так же).

### Цвета

| Свойство | Значение | Смысл |
|---|---|---|
| `_Fabric_Color` | 0.45 / 0.10 / 0.08 | основной цвет нити (приглушённый красный) |
| `_Gap_Color` | 0.16 / 0.035 / 0.03 | просветы и подныривающая нить (тень переплетения) |
| `_Heather_Color` | 0.56 / 0.17 / 0.13 | меланж/ворс — светлые волокна |
| `_Pilling_Color` | 0.62 / 0.32 / 0.28 | катышки |
| `_Wear_Color` | 0.62 / 0.34 / 0.30 | выцветшие кромки и затяжки |

### Переплетение и пряжа

| Свойство | Значение | Смысл |
|---|---|---|
| `_Weave_Scale` | 60 | нитей на единицу локального меша |
| `_Weave_Gap` | 0.25 | ширина просвета между нитями (0 — плотная, 0.9 — сетка/мешковина) |
| `_Weave_Wobble` | 0.15 | кривизна нитей крупным шумом |
| `_Weave_Seed` | 0 | сдвиг рисунка |
| `_Weave_Amount` | 0.8 | контраст переплетения в альбедо и гладкости (0 — гладкое полотно) |
| `_Slub_Scale` | 3 | частота утолщений пряжи |
| `_Slub_Amount` | 0.4 | сила утолщений (меняет ширину нити → просветы) |
| `_Slub_Seed` | 0 | сдвиг |

### Ворс и катышки

| Свойство | Значение | Смысл |
|---|---|---|
| `_Fuzz_Scale` | 25 | частота волокон |
| `_Fuzz_Stretch` | 6 | вытянутость волокон вдоль нитей |
| `_Fuzz_Detail` | 2.5 | детализация fbm |
| `_Fuzz_Seed` | 0 | сдвиг |
| `_Fuzz_Amount` | 0.35 | сила меланжа |
| `_Pilling_Scale` | 40 | частота катышков |
| `_Pilling_Seed` | 0 | сдвиг |
| `_Pilling_Amount` | 0.3 | заметность катышков |

### Кромки и затяжки

| Свойство | Значение | Смысл |
|---|---|---|
| `_Edge_Position` | 1.0 | положение кромочной маски (габарит 2×2×2) |
| `_Edge_Width` | 0.15 | ширина выцветшей полосы |
| `_Edge_Wear` | 0.35 | сила выцветания кромок |
| `_Snag_Scale` | 6 | частота затяжек |
| `_Snag_Stretch` | 8 | вытянутость затяжек |
| `_Snag_Detail` | 2.2 | детализация fbm |
| `_Snag_Seed` | 0 | сдвиг |
| `_Snag_Angle` | 0.785 (45°) | направление набора C |
| `_Snag_Threshold` | 0.88 | порог (выше — реже) |
| `_Snag_Band` | 0.06 | мягкость порога |
| `_Snag_Mask` | 0.55 | порог гасящей маски (и её сид) |
| `_Snag_Mask_Scale` | 2 | частота маски |
| `_Snag_Amount` | 0.5 | итоговая сила затяжек |

### Поверхность

| Свойство | Значение | Смысл |
|---|---|---|
| `_Smoothness` | 0.18 | гладкость вершин нитей |
| `_Gap_Smoothness` | 0.05 | гладкость просветов (матовые) |
| `_Wear_Smoothness` | 0.32 | залоснившиеся потёртые места |
| `_Weave_Bump` | −0.005 | рельеф переплетения (минус — просветы вниз) |
| `_Fuzz_Bump` | 0.001 | рельеф ворса |
| `_Pilling_Bump` | 0.002 | рельеф катышков (вверх) |
| `_Snag_Bump` | 0.001 | рельеф затяжек |

Коэффициенты рельефа малы потому, что `Strength = 1.0`, а градиент высоты считается в мировых
единицах: при 60 нитях/ед. высота меняется на 1 за ~1/120 ед. (порядок величин — как у
пользовательской настройки `M_PC_Wood`: `_Rings_Bump = 0.0065` при `_Rings_Scale ≈ 84`).

## Рельеф: обязательная проверка (см. `../README.md`, «Известная ошибка сборки»)

| Пункт | Результат |
|---|---|
| Слоты `NormalFromHeightNode` | унаследованы от Wood без правок (`In = 0`, `Strength = 2`, `Out = 1`) |
| Вызов в сгенерированном шейдере | `Unity_NormalFromHeight_Tangent_float(_Add_a2af56b2…_Out_2_Float, float(1), …)` |
| `float(0.01)` в тексте | 0 вхождений |
| `surface.NormalTS` | `= _NormalFromHeight_7348b341…_Out_1_Vector3;` |

## FO-safe (Floating Origin)

- Весь шум — от `Position (Object)`, `m_Space = 0`; `AbsoluteWorld` — 0 вхождений.
- Вариативность между объектами — сид-свойствами (`_Weave_Seed`, `_Slub_Seed`, `_Fuzz_Seed`,
  `_Pilling_Seed`, `_Snag_Seed`).
- Производные `ddx/ddy` берутся от object-space позиции — ребейз на них не влияет.
- **Проверка F8/F9 — `NOT RUN`** (за пользователем).

## Как собран граф

Одноразовым in-editor скриптом (`execute_code`, файлов-инструментов в проекте не оставлено):
текст `Wood.shadergraph` (только чтение, файл не изменён) → замена guid HLSL на guid
`ProceduralFabricWeaveNoise.hlsl` (8 CF-нод) → переименование функций, ref/display-имён свойств
и имён слотов CF-нод по точным строковым совпадениям → перестановка входных слотов в рёбрах
7 `Smoothstep`-нод (`0→2, 1→0, 2→1`) → новые дефолты 36 float + 5 color → запись тем же
разделителем `}\n\n{` → импорт. Пресет и вариант созданы через `AssetDatabase.CreateAsset`
(`SMALL_M_PC_Fabric.parent = M_PC_Fabric`).

## Проверено

| Пункт | Результат |
|---|---|
| Импорт графа | OK, ImportLog пуст |
| `ShaderUtil.ShaderHasError` / `GetShaderMessages` | `false` / 0 (после GPU-компиляции ForwardLit offscreen-рендером) |
| `shader.isSupported` / проходов | `true` / 10 |
| Свойства шейдера | 46 = 41 своих + `_QueueOffset`, `_QueueControl` + 3 lightmap |
| Зависимости графа | `ProceduralFabricWeaveNoise.hlsl` (+ служебные ShaderGraph/URP) |
| CF-функции в сгенерированном шейдере | по 6 вызовов каждой из 8 функций |
| Пресеты | `M_PC_Fabric`: shader `Fabric`, queue 2000; `SMALL_M_PC_Fabric`: parent `M_PC_Fabric` |

## Не проверено (`NOT RUN`)

- **Визуал**: читаемость переплетения на рабочей дистанции, сила анти-муара, контраст
  просветов, не «шумит» ли ворс, заметность катышков/затяжек, знак и сила рельефа.
- **Play Mode / F8 / F9**.
- **Изогнутые меши**: вид швов смены плоскости на сглаженной геометрии (подушки, складки).
- **Near/Far**: Far-вариант — пресетом (`_Weave_Amount` ниже, `*_Bump = 0`), не проверено.
- **Демо-объект в сцене** не создавался; сцены не менялись.
