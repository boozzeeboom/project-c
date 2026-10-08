# Parquet — процедурный паркетный настил (пласты + швы)

> Статус: **PROTOTYPE v1.1** — раскладка выбирает плоскость **по нормали поверхности**
> (пол / стена ±Z / стена ±X), поэтому настил читается и на полу, и на вертикальной
> стене. Граф — **производная от `Wood`**: деревянные слои (кольца / волокно / поры /
> сучки), кромки и царапины переиспользованы **как есть**, новым является только слой
> раскладки пластов.
> Проверено агентом: импорт OK, `ShaderHasError = false`, `isSupported = true`,
> `GetShaderMessages = 0`, структурный аудит графа чистый (`badSlot = 0`,
> `doubleDriven = 0`, висячих ссылок 0), все **три обязательных пункта** проверки
> рельефа (`Normal From Height`) пройдены — см. §9.
> **Визуал и Play Mode — `NOT RUN`** (тесты и скриншоты делает только пользователь).

## 1. Идентификация

| Что | Путь | GUID / имя |
|---|---|---|
| Граф | `Assets/_Project/Materials/Parquet/Parquet.shadergraph` | `3a0834dfa4675bb4aa05e611881c4eff` |
| Шейдер | генерируется из графа, имя `Parquet` | саб-ассеты графа: `Shader::Parquet`, `Material::Parquet`, `ShaderGraphIndexedData`, `ShaderGraphMetadata`, `UniversalMetadata` |
| Пресет | `Assets/_Project/Materials/Parquet/M_PC_Parquet.mat` | `23e90849602e0d44a9fac7896bc64c61` |
| Ядро раскладки | `Assets/_Project/Shaders/ProceduralParquetNoise.hlsl` | `f6d1426069bfc314a9ae3890873e524e` |
| Ядро дерева (не изменялось) | `Assets/_Project/Shaders/ProceduralWoodNoise.hlsl` | `098cd609c8a01814fb739c9437c887b6` |
| Донор значений пресета (только чтение) | `Assets/_Project/Materials/Wood/M_PC_Wood.mat` | — |
| Донор структуры графа (только чтение) | `Assets/_Project/Materials/Wood/Wood.shadergraph` | — |

Всё новое лежит в двух местах: `Assets/_Project/Materials/Parquet/` и
`Assets/_Project/Shaders/ProceduralParquetNoise.hlsl`. Ни `Wood.shadergraph`,
ни `ProceduralWoodNoise.hlsl`, ни `M_PC_Wood.mat` не изменялись.

## 2. Идея: что взято у Wood, а что новое

Паркет — это **дерево плюс раскладка**. Порода, кольца, волокно, поры и сучки у
паркета те же, что у доски, поэтому весь деревянный конвейер `Wood` сохранён
без единой правки: те же CF-функции, тот же порядок слоёв, те же модификаторы
кромок и царапин (механизм бронзы), те же цвета-свойства.

Новыми являются четыре вещи:

1. **Сетка пластов** — параллельные пласты с перевязкой рядов и случайным целым
   сдвигом торцов (`PCParq_PlaneCell`). Механизм тот же, что в `Brick/` и `Plate/`
   (регулярная сетка `floor`/`frac` + перевязка `_Row_Offset`), а не Worley —
   планки паркета всегда прямоугольные.
2. **Выбор плоскости по нормали поверхности** (`PCParq_Axes`) — раскладка больше
   не привязана к одной локальной плоскости: нормаль в объектном пространстве
   выбирает пару осей, по которой строится сетка. Пол/потолок → XZ (как раньше),
   стена ±Z → XY, стена ±X → ZY. **Длина пласта всегда ложится на горизонталь**
   поверхности, а рядами становится вертикальная ось — поэтому на стене доски
   горизонтальны и швы идут горизонтальными линиями.
3. **Подмена входа координат дерева.** Деревянные слои получают не object-space
   позицию, а `PlankLocal` — координаты **внутри пласта**: длинная ось пласта
   становится локальной Y дерева (волокно идёт вдоль пласта), а каждая планка
   получает своё случайное «окно породы» (±6 единиц дерева), поэтому пласты
   выглядят как разные куски одного бревна, а не как одна и та же текстура.
   Ядро `ProceduralWoodNoise.hlsl` при этом не менялось — ему просто подан
   другой вход (правило T-BRONZE01: сигнатуры CF-функций не трогаются).
4. **Швы и тон пласта.** Шов считается от той же сетки: `Joints` = 1 на стыке и
   0 в теле пласта. Тон пласта берётся **хешем той же ячейки**, поэтому тон и
   его границы физически не могут разъехаться (тот же приём, что у кирпича в
   `Brick/`).

Кромки и царапины остались на **object-space** координатах, а не на `PlankLocal`:
они описывают сам предмет (потёртости по габариту и кромкам), а не отдельную
планку — иначе царапины повторялись бы в каждой планке одинаково.

### 2.1 Диагностированный дефект, из-за которого появилась нормаль

Первая версия считала сетку **только** по локальным `x` и `z`. На полу это
работает, но на грани с постоянной `Z` (вертикальная стена) координата ряда
`c.y = z × Aspect × Scale + Seed` **не меняется вообще**: `frac(c.y)` одинаков
по всей грани, `dy = min(local.y, 1 − local.y) / JointWidthY` уходит далеко за
единицу, `min(dx, dy)` определяется только торцевым членом — и на стене
остаются **одни вертикальные торцевые полосы**, боковые (горизонтальные) швы
вырождаются в ноль. Внешне это выглядело как «бесконечно длинные доски».

Замер на тестовом объекте (`WorldRoot_0_0/Cube`, `localScale = 24.378 / 10 / 10`,
примитив с расщеплёнными нормалями): просматривалась грань ±Z, при
`_Plank_Aspect = 4.5` и `_Plank_Scale = 8.2` получалось `c.y = 18.45`,
`local.y = 0.45`, `dy = 9` — **ни одного бокового шва на всей грани** при
9 торцевых полосах, то есть ровно то, что видел пользователь.

Вывод: дефект был **геометрическим, а не арифметическим** — сама сетка
симметрична, ей просто не давали плоскости, в которой ряды существуют. Отсюда
решение: не менять сетку, а **выбирать плоскость по нормали** и вызывать ту же
математику на выбранной паре осей (`PCParq_PlaneCell` — копия `PCParq_Cell`,
работающая от `float2 uv`).

## 3. Что построено

```
Position      (object-space, m_Space = 0) ─┐
                                            ├─ LAYOUT  ProceduralParquetLayoutFaces(...) → PlankLocal   [новое]
Normal Vector (object-space, m_Space = 0) ─┘    = локальная позиция ВНУТРИ пласта + случайное окно породы
      ├─ СУЧКИ/СВИЛЬ   ProceduralWoodKnots(ObjectPos = PlankLocal)   ← было Position
      ├─ КОЛЬЦА        ProceduralWoodRings(... ← Knots)              ← было Position
      ├─ ВОЛОКНО       ProceduralWoodGrain(...)                      ← было Position
      └─ ПОРЫ          ProceduralWoodPores(...)                      ← было Position

КРОМКИ (из Wood/бронзы): |x|,|y|,|z| → Smoothstep(_Edge_Position − _Edge_Width, _Edge_Position)
           → попарные × → Add → Add → Saturate → × _Edge_Wear   ← вход НЕ менялся (Position)
ЦАРАПИНЫ (из Wood/бронзы): ScratchA/B/C → Smoothstep(_Scratch_Threshold, +_Scratch_Band)
           → Add → × Smoothstep(mask) → × _Scratch_Amount        ← вход НЕ менялся (Position)
wear = Saturate(кромки + царапины)

JOINTS   ProceduralParquetJointsFaces(...) → Joints    (1 = шов, 0 = тело пласта)   [новое]
TONE     ProceduralParquetToneFaces(...)   → PlankTone (0..1, хеш ячейки)           [новое]

АЛЬБЕДО
  Wood-цепочка = Lerp(
        Lerp(_Wood_Color → _Plank_Color, T = PlankTone × _Plank_Vary),   ← [новое] порода пласта
        _LateWood_Color, T = кольца)
      → _Grain_Color (волокно) → _Pore_Color (поры) → _Wear_Color (wear)
  BaseColor = Lerp(Wood-цепочка → _Joint_Color, T = Joints × _Joint_Amount)  ← [новое] швы

ГЛАДКОСТЬ
  Wood = Lerp(Lerp(_Smoothness, _LateWood_Smoothness, кольца), _Wear_Smoothness, wear)
  Smoothness = Saturate(Wood − Joints × _Joint_Gloss)                        ← [новое] шов матовый

РЕЛЬЕФ
  NormalTS = Normal From Height(
        Wood-высота(волокно×_Grain_Bump + поры×_Pore_Bump + царапины×_Scratch_Bump + кольца×_Rings_Bump)
        + Joints × _Joint_Bump,                                              ← [новое] шов вниз (−0.6)
        Strength = 1.0 — константа, как в Wood)

Metallic / Emission / Occlusion — не подключены (диэлектрик; блок Metallic = дефолт 0)
```

Итог графа: **492 JSON-документа**, **128 документов нод** (119 с читаемым `m_Type`
+ 9 блоков мастер-стека; из них 101 от `Wood`, 27 новых), **306 слотов**,
**177 рёбер** (132 − 8 заменённых + 53 новых), **55 свойств блэкборда**
(48 float + 7 color); в скомпилированном шейдере 60 свойств.
`badSrc = 0`, `badDst = 0`, `badSlot = 0`, `doubleDriven = 0`, висячих ссылок на слоты 0.

Идентификаторы новых нод (8 символов) для поиска в графе:
`Normal Vector` = `4676c0fb`, `ProceduralParquetLayoutFaces` = `386b9a6a`,
`ProceduralParquetJointsFaces` = `8de2f3cb`, `ProceduralParquetToneFaces` = `546c839b`,
`Tone × _Plank_Vary` = `cee511b8`, `Lerp(_Wood_Color → _Plank_Color)` = `5bf04723`,
`Lerp(… → _Joint_Color)` = `425f7d83`,
`Wood-высота + Joints × _Joint_Bump` = `aabebc55`,
`Saturate(гладкость − шов)` = `c9a70405`, `Normal From Height` = `7348b341`,
`Position` = `08687076`. Два независимых входа раскладки — `Position` (позиция) и
`Normal Vector` (`m_Space = 0`, объектное пространство).

## 4. Функции раскладки (HLSL)

`Assets/_Project/Shaders/ProceduralParquetNoise.hlsl` — 265 строк, **6 логических
функций** (12 объявлений `_float` / `_half`, File-режим, один `out` последним —
T-BRONZE01) и **6 локальных хелперов**: `PCParq_Hash13` (строка 21),
`PCParq_ValueNoise3` (28), `PCParq_Cell` (57), `PCParq_Window` (68),
`PCParq_Axes` (165), `PCParq_PlaneCell` (173).

«Плоские» функции (жёстко по локальным X/Z) сохранены **без изменений** — в графе
они больше не подключены, но правило T-BRONZE01 запрещает менять существующие
сигнатуры, поэтому новое поведение добавлено **новыми именами**.

| Функция | Входы (порядок = порядок слотов) | Выход |
|---|---|---|
| `ProceduralParquetLayout_float` (80) | `ObjectPos`, `PlankScale`, `PlankAspect`, `RowOffset`, `Stagger`, `PlankWarp`, `PlankSeed` | `float3 PlankLocal` |
| `ProceduralParquetJoints_float` (111) | `ObjectPos`, `PlankScale`, `PlankAspect`, `RowOffset`, `Stagger`, `PlankWarp`, `JointWidthX`, `JointWidthY`, `PlankSeed` | `float Joints` |
| `ProceduralParquetTone_float` (139) | `ObjectPos`, `PlankScale`, `PlankAspect`, `RowOffset`, `Stagger`, `PlankWarp`, `PlankSeed` | `float PlankTone` |
| **`ProceduralParquetLayoutFaces_float` (183)** | **`ObjectPos`, `ObjectNormal`**, `PlankScale`, `PlankAspect`, `RowOffset`, `Stagger`, `PlankWarp`, `PlankSeed` | `float3 PlankLocal` |
| **`ProceduralParquetJointsFaces_float` (214)** | **`ObjectPos`, `ObjectNormal`**, `PlankScale`, `PlankAspect`, `RowOffset`, `Stagger`, `PlankWarp`, `JointWidthX`, `JointWidthY`, `PlankSeed` | `float Joints` |
| **`ProceduralParquetToneFaces_float` (243)** | **`ObjectPos`, `ObjectNormal`**, `PlankScale`, `PlankAspect`, `RowOffset`, `Stagger`, `PlankWarp`, `PlankSeed` | `float PlankTone` |

Формулы:

```
── выбор плоскости по нормали (объектное пространство) ──────────────────────
a = abs(ObjectNormal)
|n.y| ≥ |n.x| и ≥ |n.z|  →  uv = (p.x, p.z),  depth = p.y     // пол / потолок
иначе |n.z| ≥ |n.x|      →  uv = (p.x, p.y),  depth = p.z     // стена ±Z
иначе                    →  uv = (p.z, p.y),  depth = p.x     // стена ±X

── сетка на выбранной плоскости ────────────────────────────────────────────
c        = float2(uv.x, uv.y × PlankAspect) × PlankScale + PlankSeed  // X — длина, Y — ряды
c       += (ValueNoise3(c × 0.6, seed) − 0.5) × PlankWarp             // свиль границ
row      = floor(c.y);  c.x += row × RowOffset                        // перевязка рядов (0.5 = пол-пласта)
c.x     += floor(Hash13(row) × Stagger)                               // целый сдвиг торцов ряда (0..Stagger пластов)
local    = frac(c);  cell = floor(c)

PlankLocal = float3( local.y × (1/(Scale·Aspect)) + win.x,            // X дерева — поперёк пласта
                     local.x × (1/Scale)          + win.y,            // Y дерева — ВДОЛЬ пласта (волокно)
                     depth × 0.5                  + win.z × 0.25 )    // Z дерева — толщина + сдвиг сердцевины
win        = (Hash13(cell, seed + {11.3 / 27.7 / 43.1}) − 0.5) × 12   // окно породы: до ±6 единиц дерева

dx = min(local.x, 1−local.x) / JointWidthX                            // нормированное расстояние до торца
dy = min(local.y, 1−local.y) / JointWidthY                            // … и до боковой кромки
Joints   = 1 − smoothstep(0, 1, min(dx, dy))                          // 1 на стыке, 0 в теле
PlankTone = Hash13(floor(c), seed + 5.9)                              // 0..1 на пласт
```

Семантика швов: `Joints = 1` **ровно на линии стыка** и гаснет на расстоянии
`JointWidth` от неё, поэтому толщина шва в единицах = `2 × ширина_пласта × JointWidth`:
при пресетных значениях торцевой стык ≈ 0.053 единицы, боковой шов ≈ 0.015 —
**торцевые стыки в 3.6 раза шире боковых** (`_Joint_Width_X` 0.04 против
`_Joint_Width_Y` 0.05 в долях пласта, а сам пласт по длине в 4.5 раза больше ширины).
Это осознанно: боковые кромки паркета подогнаны плотно, торцы — заметнее.

## 5. Параметры

### 5.1 Новые свойства раскладки (14)

| Свойство | Тип | Значение в пресете | Смысл |
|---|---|---|---|
| `_Plank_Scale` | Float | `1.5` | пластов на единицу объекта по длине (клетка = `1/_Plank_Scale` единиц) |
| `_Plank_Aspect` | Float | `4.5` | длина пласта / ширина (частота по рядам = `Scale × Aspect`) |
| `_Plank_Row_Offset` | Float | `0.5` | непрерывный сдвиг ряда в долях пласта (0.5 = перевязка «в пол-пласта») |
| `_Plank_Stagger` | Float | `3` | разброс **целого** сдвига торцов ряда (0..3 пласта) |
| `_Plank_Warp` | Float | `0.06` | свиль границ сетки (границы «рукодельные», шов и тон искажаются вместе) |
| `_Plank_Seed` | Float | `0` | сид сетки/окон/тона (не мировая позиция — см. §6) |
| `_Plank_Vary` | Float | `0.65` | насколько тон пласта уводит альбедо от `_Wood_Color` к `_Plank_Color` |
| `_Joint_Width_X` | Float | `0.04` | полуширина **торцевого** стыка в долях длины пласта |
| `_Joint_Width_Y` | Float | `0.05` | полуширина **бокового** шва в долях ширины пласта |
| `_Joint_Amount` | Float | `1` | сила шва в альбедо (0 — швов не видно) |
| `_Joint_Gloss` | Float | `0.15` | насколько шов **матовее** тела пласта (`Smoothness − Joints × Gloss`) |
| `_Joint_Bump` | Float | `-0.6` | шов в рельефе: **отрицательный** = канавка вниз |
| `_Plank_Color` | Color | `0.50 / 0.34 / 0.19` | порода пласта (то, к чему уводит `_Plank_Vary`) |
| `_Joint_Color` | Color | `0.06 / 0.04 / 0.02` | цвет шва (грязь/тень стыка) |

### 5.2 Перенесённые из `M_PC_Wood.mat` (46 значений, копия 1:1)

Значения ниже — **фактические из материала Wood** (прочитаны через Material API при
создании пресета), а не дефолты блэкборда:

| Свойство | Значение | Свойство | Значение |
|---|---|---|---|
| `_Rings_Scale` | `83.98` | `_Scratch_Scale` | `7.24` |
| `_Rings_Sharpness` | `0.41` | `_Scratch_Stretch` | `6` |
| `_Rings_Warp` | `0.1` | `_Scratch_Detail` | `2.3` |
| `_Rings_Seed` | `1.87` | `_Scratch_Seed` | `0` |
| `_Ring_Amount` | `1.67` | `_Scratch_Angle` | `0.785` |
| `_Knots_Scale` | `3.57` | `_Scratch_Threshold` | `0.85` |
| `_Knots_Warp` | `2.49` | `_Scratch_Band` | `0.13` |
| `_Knots_Seed` | `0.14` | `_Scratch_Mask` | `0.32` |
| `_Grain_Scale` | `2.5` | `_Scratch_Mask_Scale` | `2.47` |
| `_Grain_Stretch` | `7.09` | `_Scratch_Amount` | `0.64` |
| `_Grain_Detail` | `4.14` | `_Smoothness` | `0.54` |
| `_Grain_Seed` | `0.33` | `_LateWood_Smoothness` | `0.99` |
| `_Grain_Amount` | `0.52` | `_Wear_Smoothness` | `1.47` |
| `_Pores_Scale` | `116.8` | `_Grain_Bump` | `0` |
| `_Pores_Seed` | `0` | `_Pore_Bump` | `0` |
| `_Pores_Amount` | `0.62` | `_Scratch_Bump` | `0.001` |
| `_Edge_Position` | `0.81` | `_Rings_Bump` | `0.0065` |
| `_Edge_Width` | `0.2` | `_Wear_Color` | `0.78 / 0.68 / 0.52` |
| `_Edge_Wear` | `0.3` | `_Wood_Color` | `0.62 / 0.43 / 0.24` |
| | | `_LateWood_Color` | `0.26 / 0.14 / 0.07` |
| | | `_Grain_Color` | `0.44 / 0.28 / 0.14` |
| | | `_Pore_Color` | `0.16 / 0.09 / 0.05` |

Итого в шейдере 60 свойств: 55 из блэкборда (48 float + 7 color) + 5 служебных
URP (`_QueueOffset`, `_QueueControl`, `unity_Lightmaps`, `unity_LightmapsInd`,
`unity_ShadowMasks`). Пресет: `renderQueue = 2000`, ключевых слов — **0**
(Near/Far-различие пока не введено: вариативность только свойствами, как требует
правило проекта «один шейдер — много пресетов»).

**Наблюдение по рельефу дерева (не дефект сборки):** перенесённые значения
`_Grain_Bump = 0`, `_Pore_Bump = 0`, `_Rings_Bump = 0.0065`, `_Scratch_Bump = 0.001`
означают, что собственный рельеф дерева в Wood практически выключен — вся
геометрическая рельефность настила приходит от швов (`_Joint_Bump = -0.6`).
Это состояние Wood перенесено «как есть»; поднимать `*_Bump` — вопрос визуальной
калибровки, а визуал пока `NOT RUN`.

`_Plank_Scale` в **пресете** равен `1.5`, но на тестовом объекте в сцене
`BootstrapScene` у материала выставлено **8.2** (значение живёт в сцене; не
проверено, сохранён ли этот материал как ассет или это правка экземпляра в
открытой сцене).

## 6. Floating Origin (FO-safe)

- Шум считается **только** в object-space: `Position` с `m_Space = 0`, и
  `Normal Vector` (нода `UnityEditor.ShaderGraph.NormalVectorNode`) тоже с
  `m_Space = 0`. Вхождений `AbsoluteWorld` (значение `4`) в графе — **0**.
- Нормаль — величина **объектная**: объект вращается вместе со своей раскладкой,
  а ребейз (сдвиг) её вообще не касается (нормаль трансляционно-инвариантна).
- `_Plank_Seed` — это **смещение сетки**, а не мировая позиция: пласты «стоят на
  месте» и не поплывут относительно объекта после ребейза (F8/авто/F9).
- В новом HLSL `AbsoluteWorld` не используется вообще.
- Масштаб объекта масштабирует и рисунок настила (клетка = `1/_Plank_Scale`
  **локальных** единиц) — это осознанный компромисс object-space шума, а не баг.

## 7. Требования к геометрии

1. **Плоскость раскладки берётся из нормали в объектном пространстве, грани
   должны быть плоскими (hard normals).** Нода `Normal Vector` отдаёт нормаль
   вершин, интерполированную по треугольнику. На меше с расщеплёнными (flat)
   нормалями, как у примитива Cube, выбор плоскости точен на каждой грани. На
   **сглаженном** меше нормаль меняется внутри треугольника, и раскладка
   «поворачивается» вместе с ней: на цилиндре доски начнут закручиваться вокруг
   оси. Для стабильного настила нужны плоские нормали у грани или
   геометрия, где сглаженность допустима по замыслу.
2. **Ориентация пластов задаётся выбранной парой осей:**

   | Грань (нормаль) | Плоскость | Длина пласта | Ряды / боковые швы |
   |---|---|---|---|
   | ±Y (пол, потолок) | `x`, `z` | по локальной X | по локальной Z |
   | ±Z (стена) | `x`, `y` | **по локальной X (горизонталь)** | **по локальной Y (вверх по стене)** |
   | ±X (стена) | `z`, `y` | **по локальной Z (горизонталь)** | по локальной Y |

   То есть на вертикальной стене доски ложатся **горизонтально**, а рядом
   становится высота. Развернуть настил «на 90°» (вертикальная вагонка) штатно
   нельзя — это потребовало бы четвёртой ветви выбора осей.
3. **Волокно идёт вдоль пласта.** Функция раскладки переставляет оси так, что
   локальная Y дерева = длина пласта; поэтому волокно `Wood` тянется, а кольца
   `ProceduralWoodRings` читаются как продольные полосы на пласти.
4. **Сучки/кольца не выглядят как срез бревна.** В `PlankLocal` ось «сердцевины»
   сдвинута на `win.z × 0.25` (а по толщине берётся `depth × 0.5`, где `depth` —
   координата, перпендикулярная грани), поэтому кольца у пластов несимметричны —
   это ожидаемое поведение паркета, а не нарушение геометрии.
5. **Кромки и царапины привязаны к габариту объекта** (`_Edge_Position = 0.81`
   рассчитан на единичный габарит ~2×2×2, как в Wood). Для меша другого размера
   подбирается `_Edge_Position` либо нормируется геометрия.
6. **UV и vertex colors не нужны.** Ни одна функция не читает UV; vertex colors
   граф v1 тоже не читает (договор по каналам остаётся на будущее).
7. **Смены плоскости внутри одной грани не бывает только у плоской грани.** На
   грани, нормаль которой близка к «диагонали» между двумя ветвями, выбор может
   переключиться по её площади и разорвать настил — на реальных объектах
   (пол, стены, борта) нормали осевые, поэтому это не встречается.

## 8. Инцидент сборки: `"$1" + число` в `Regex.Replace`

Первая сборка графа **не импортировалась вообще** — в консоли:

```
Asset import failed, "Assets/_Project/Materials/Parquet/Parquet.shadergraph"
 > ArgumentException: JSON parse error: Missing a name for object member.
   UnityEditor.ShaderGraph.Serialization.MultiJsonInternal.Parse
```

Причина оказалась не в структуре графа (счётчики документов/рёбер были верные),
а в **строке замены регулярного выражения** в билдере. Хелперы правки JSON были
написаны как `Regex.Replace(doc, @"(""m_Value"":\s*)-?[0-9.]+", "$1" + value)`.
При числовом значении строка замены склеивается в `$11.5`, и .NET читает это как
ссылку на **группу 11**, а не «группа 1 + текст»: группа не существует, и в файл
попадает литерал. В документе свойства получалось `$11.5,` вместо `"m_Value": 1.5,` —
JSON ломается на безымянном члене объекта.

Тем же дефектом были испорчены `m_Id`/`m_SlotType` у клонированных слотов
(`$10`, `$11`, …) и `m_Position` у клонированных нод.

**Лечение:** все ссылки на группы писать в скобках — **`${1}` / `${2}`**, тогда
число значения не приклеивается к номеру группы. После правки семи хелперов
(`SetObjId`, `SetStr`, `SetNum`, `SetNumF`, `SetPropertyRef`, `Pos`, `ReplaceArray`)
граф импортировался с первого раза.

**Диагностика без Unity:** сообщение импортёра не показывает, какой документ
плохой, а `execute_script` на ru-RU культуре маскирует ошибки компиляции
(`MissingManifestResourceException`);
рабочий приём — собственный JSON-валидатор (проверка «имя члена объекта»)
по документу за документом, плюс компиляция текста скрипта через Roslyn API
внутри `execute_code` (`GetDiagnostics()` печатает **Id** и позицию, без
локализованного текста).

## 9. Проверка (что измерено)

Импорт и шейдер:

- саб-ассеты графа: `Shader::Parquet`, `Material::Parquet`, `ShaderGraphIndexedData`,
  `ShaderGraphMetadata`, `UniversalMetadata`;
- `ShaderHasError = false`, `isSupported = true`, `GetShaderMessages = 0`,
  в шейдере **60 свойств**.

Структура графа (аудит по файлу и по живому `GraphData`):

```
docs = 492      m_Properties = 55      m_Slots = 306      edges = 177
nodes: всего документов нод = 128, из них с читаемым m_Type = 119 (+9 блоков мастер-стека)
badSlot = 0     doubleDriven = 0       висячих ссылок на слоты = 0
Position:     m_Space = 0 (1 нода)
NormalVector: m_Space = 0 (1 нода, id 4676c0fb, 1 слот Vector3MaterialSlot "Out", m_Id 0, m_SlotType 1)
AbsoluteWorld: 0 вхождений
```

Новые CF-ноды и слоты (порядок = порядок аргументов, `out` последним):

```
LayoutFaces: #0 ObjectPos(i) #1 ObjectNormal(i) #2 PlankScale(i) #3 PlankAspect(i) #4 RowOffset(i)
             #5 Stagger(i) #6 PlankWarp(i) #7 PlankSeed(i) #8 PlankLocal(o)
JointsFaces: #0 ObjectPos #1 ObjectNormal … #6 PlankWarp, #7 JointWidthX, #8 JointWidthY,
             #9 PlankSeed, #10 Joints(o)
ToneFaces  : #0 ObjectPos #1 ObjectNormal … #6 PlankWarp, #7 PlankSeed, #8 PlankTone(o)
```

Проводка (аудит рёбер): `ObjectPos` всех четырёх деревянных CF
(`ProceduralWoodKnots/Rings/Grain/Pores`) приходит от `ProceduralParquetLayoutFaces`
(`386b9a6a`), а `ProceduralWoodScratchA`/`ScratchMask` — по-прежнему от `Position`
(`08687076`); `BaseColor ← Lerp 425f7d83`; `Smoothness ← Saturate c9a70405`;
`NormalTS ← NormalFromHeight 7348b341`; `Metallic`/`Emission` не подключены.

**Три обязательных пункта проверки рельефа (см. `docs/Materials/README.md`):**

1. `m_Id` слотов `NormalFromHeightNode`: **`In = 0`, `Strength = 2`, `Out = 1`**
   (`m_SlotType` выхода = 1). Нода унаследована от Wood **без правок** — ObjectId
   слотов совпадают с Wood побайтово (`be5db055` / `73e3f541` / `35cac65b`),
   `m_Value` слота `Strength = 1.0` (не шаблонное `0.01`).
2. В сгенерированном шейдере (`ShaderGraphImporter.GetShaderText`, перегрузка
   `(path, ref configuredTextures, AssetCollection, ref GraphData)`):
   `Unity_NormalFromHeight_Tangent_float(_Add_aabebc557b784fcf97246f3897dabc62_Out_2_Float, float(1), …);`
   — первый аргумент = новый узел «Wood-высота + Joints × _Joint_Bump»,
   **второй = `float(1)`, не `float(0.01)`**; вхождений `float(0.01)` — 0.
3. `surface.NormalTS = _NormalFromHeight_7348b341840346a19bca1ad2410a446a_Out_1_Vector3;`

Прочие присваивания в сгенерированном шейдере:

```hlsl
surface.BaseColor   = (_Lerp_425f7d83457749e18d0bc468cd36f30c_Out_3_Vector4.xyz);   // швы
surface.Smoothness  = _Saturate_c9a70405d28b4bb9ac10d7e7c3fcc317_Out_1_Float;        // − Joints × _Joint_Gloss
surface.Metallic    = float(0);
surface.Emission    = float3(0, 0, 0);
surface.Occlusion   = float(1);
```

Вызовы функций раскладки в сгенерированном тексте (длина текста `255 588`):
`ProceduralParquetLayoutFaces_float` — 6 вхождений, `ProceduralParquetJointsFaces_float` — 6,
`ProceduralParquetToneFaces_float` — 5. Старых имён (`…Layout_float` / `…Joints_float` /
`…Tone_float`) — **0**: «плоские» функции остаются в HLSL, но граф их больше не вызывает.

Ключевые подстановки в местах вызова:

```
ProceduralParquetLayoutFaces_float(IN.ObjectSpacePosition, IN.ObjectSpaceNormal,
    _PlankScale_3228ad78…, _PlankAspect_58a63b1c…, _PlankRowOffset_6bdfcd78…,
    _PlankStagger_87486abf…, _PlankWarp_8fe6570e…, _PlankSeed_aa6ebb2d…)
ProceduralParquetJointsFaces_float(IN.ObjectSpacePosition, IN.ObjectSpaceNormal,
    … , _JointWidthX_f968819d…, _JointWidthY_…, _PlankSeed_…)
ProceduralParquetToneFaces_float(IN.ObjectSpacePosition, IN.ObjectSpaceNormal,
    _PlankScale_…, _PlankAspect_…, _PlankRowOffset_…, _PlankStagger_…, _PlankWarp_…, _PlankSeed_…)
```

То есть **вторым аргументом во все три Functions идёт `IN.ObjectSpaceNormal`** —
нормаль действительно доходит до HLSL. Первый аргумент деревянных CF в генерации —
`_ProceduralParquetLayoutFacesCustomFunction_386b9a6a…` (то есть `PlankLocal`, а не
`IN.ObjectSpacePosition`).

Пресет (чтение обратно из `M_PC_Parquet.mat`): 60 свойств, `renderQueue = 2000`,
`shaderKeywords.Length = 0`, 46 значений перенесены из `M_PC_Wood.mat`
(совпадение проверено по `_Rings_*`, `_Knots_*`, `_Grain_*`, `_Pores_*`, `_Edge_*`,
`_Scratch_*`, `_Smoothness*`, `*_Bump`, `_Wood_Color`), 14 новых выставлены
(§5.1 — значения совпадают с записанными), `_Metallic` в пресете отсутствует.

### `NOT RUN` (делает пользователь)

- **Главная незакрытая проверка:** видно ли после пересборки **оба** семейства швов
  на вертикальной грани (горизонтальные боковые швы + торцевые), и читается ли
  настил на стене как паркет. Проверка агента — только по структуре и
  сгенерированному тексту, глазами на объект агент не смотрит.
- Внешний вид настила на объекте: читается ли раскладка как паркет, а не как
  сетка; различимы ли отдельные пласты по тону; швы (альбедо + канавка +
  матовость) на нужной дистанции и на нужном масштабе.
- Поведение на **сглаженных** мешах (см. §7.1) — на тестовом кубе нормали плоские.
- Подбор `_Plank_Scale` / `_Plank_Aspect` под реальный размер плана объекта
  (в пресете `_Plank_Scale = 1.5` — клетка 0.667 локальных единицы по длине;
  на тестовом объекте в сцене выставлено `8.2`, планка ≈ 0.122 × 0.0271
  локальных единицы).
- `_Joint_Bump = -0.6` — достаточна ли канавка при `Strength = 1.0`.
- Нужно ли поднимать рельеф дерева (`_Grain_Bump` / `_Pore_Bump` / `_Rings_Bump`
  перенесены из Wood и практически нулевые).
- Play Mode, F8/авто/F9 (ребейз), совместимость с пост-эффектом `EdgeDetection`
  и с `VolumetricClouds`/`DistantFocus`.
- Near/Far пресеты (ключевые слова пока не вводились).

## 10. Связки и границы

- Ядро `ProceduralWoodNoise.hlsl` **не изменялось**: деревянные слои получают
  `PlankLocal` на вход, поэтому паркет и доска (`Wood/`) остаются одним кодом
  с разной раскладкой.
- Сетка пластов и «тон из той же ячейки, что и шов» — приём `Brick/` и `Plate/`
  (регулярная сетка вместо Worley). В отличие от них, **общего множителя рельефа
  (`_Bump_Strength`) здесь нет** — он отсутствует и в Wood: сила рельефа задаётся
  видом слоя, а `Strength` ноды `Normal From Height` стоит константой `1.0`.
- Выбор плоскости по нормали — приём, которого нет у других материалов проекта
  (`Brick/` жёстко требует плоскость XY, `Plate/` — «плоская или цилиндрическая
  поверхность, Y вверх»). Паркет — первый материал с **самоориентирующейся**
  раскладкой; его цена — требование плоских нормалей (§7.1).
- «Плоские» функции (`ProceduralParquetLayout/Joints/Tone`) оставлены в HLSL как
  рабочая база для сравнения и по правилу T-BRONZE01 (нельзя менять существующие
  сигнатуры). Настил только по XZ можно вернуть, переключив три CF-ноды графа
  обратно на них — но тогда на стене снова останутся одни торцевые полосы (§2.1).
- `EdgeDetection` контур поверх настила работает штатно (материал непрозрачный,
  пишет глубину) — в отличие от `Glass/`.
- Постобработка и облака не затрагивались; никаких изменений вне
  `Assets/_Project/Materials/Parquet/` и `Assets/_Project/Shaders/ProceduralParquetNoise.hlsl`
  не делалось.
