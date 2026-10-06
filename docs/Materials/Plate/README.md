# Plate — процедурные металлические пластины (клёпаная обшивка: швы, тон листа, заклёпки, патина, царапины)

> Статус: **PROTOTYPE v1**. Граф собран под клёпаный лист металла; механизмы
> патины и царапин взяты **у бронзы** (тот же принцип, своя реализация с
> префиксом `PCPlate_`), сетка пластин — приём Brick. От бронзы **не взято**:
> вертикальный bias патины (привязан к мировой оси), «износ до подложки» и маска
> кромок — у листа кромка это край меша, а не признак материала.
> Импорт OK, `ShaderHasError = false`, `isSupported = true`, `GetShaderMessages = 0`,
> структурный аудит графа чистый (двойных входов 0, висячих концов рёбер 0,
> висячих ссылок на слоты 0), **все три обязательные проверки рельефа пройдены** —
> Plate второй материал в проекте после Brick, где «Известная ошибка сборки»
> (`Normal From Height`) отсутствует (см. раздел «Рельеф»).
> **Визуал и Play Mode — `NOT RUN`** (тесты и скриншоты делает только пользователь).

Файлы:

| Что | Путь | GUID |
|---|---|---|
| Граф | `Assets/_Project/Materials/Plate/Plate.shadergraph` | `0650d1f2a2798274ca016d0d015821c2` |
| Пресет | `Assets/_Project/Materials/Plate/M_PC_Plate.mat` | `0961a6f3854d5a848acdcd8521daaaf9` |
| Ядро шума | `Assets/_Project/Shaders/ProceduralPlateNoise.hlsl` | `fbe4d1903b0ed544e8318c34e9c6ae49` |
| Папка материала | `Assets/_Project/Materials/Plate/` | `8b77561b623d0244582cbdabc0f618d7` |
| Инструменты сборки | `Temp/PlateGraphBuilder.cs`, `PlateLayers.txt`, `PlateMaterial.cs`, `PlateVerify.cs`, `PlateDump.cs`, `PlateIds.cs` (вне `Assets`, вне git) | — |

Цифры графа: **487** JSON-документов, **129** нод (120 обычных + 9 блоков
мастер-стека), **173** ребра, **54** свойства блэкборда (49 float + 5 color),
441 452 байта, 17 861 строка, переводы строк — **чистый LF** (`CR = 0`).
Ноды по типам: Add 14, Block 9, CustomFunction 9, Lerp 8, Multiply 21,
Normal From Height 1, Position 1, Property 54, Saturate 4, Smoothstep 6, Subtract 2.
Слоты: `Vector1` 104, `DynamicVector` 112, `DynamicValue` 63, `Vector3` 11,
`Vector4` 5, `ColorRGB` 2, `NormalMaterialSlot` 2, `PositionMaterialSlot` 1,
`TangentMaterialSlot` 1. В шейдере 59 свойств (51 float + 5 color + queue/lightmap).

Ядро `ProceduralPlateNoise.hlsl` — **17 133** символа, **9** экспонированных
одно-выходных функций + **9** `_half`-обёрток.

---

## Чем Plate отличается от бронзы (и что взято у Brick)

**От бронзы** взяты три механизма: микрошум листа (fbm, смешанный с Worley),
патина-пятно (fbm без растяжки) и три набора царапин с гасящей маской. Все три
переписаны с префиксом `PCPlate_` — существующие функции
`ProceduralBronzeNoise.hlsl` не правились и в графе не упоминаются
(`plateRefsInBronze = 0`, ссылок на guid бронзового HLSL в графе Plate — 0).

**От Brick** взят приём, который и создаёт узнаваемость: **поверхность разбита
на пластины регулярной арифметической сеткой** (`floor`/`frac`), а не свободным
шумом. Отсюда три следствия:

1. **Швы между пластинами** — полоса у границы ячейки. Шов = сварной валик
   (в рельефе вверх), а не впадина, как раствор в Brick.
2. **Тон каждой пластины считается той же сеткой** (`floor(q)` → хеш ячейки),
   поэтому тон и его границы физически не могут разъехаться со швами.
3. **Заклёпки** — диски, вписанные в **углы** тех же ячеек, со сдвигом внутрь
   листа, чтобы не тонуть в сварном валике.

Отличия от Brick по существу: у пластин нет ни пор, ни зерна, зато есть
заклёпки, микрошум листа, патина двух уровней и металличность (`Metallic`
**подключён** — в Brick он не подключён).

Отличия от бронзы по существу: патина садится на пластины **по-разному**
(множитель по тону листа) и **жмётся к швам** — влага держится на сварке. Это то,
что читает обшивку как набор отдельных листов, а не как один кусок металла.

```
p  = ObjectPos * PlateScale + PlateSeed            // ObjectPos — object-space
q  = float2(p.x, p.y * PlateAspect)                // Aspect = длина листа / высота
q += (noise2(p*0.6) − 0.5) * SeamWarp              // листы варили руками — шов ведёт
row = floor(q.y)
q.x += row * RowOffset                             // перевязка рядов (0.5 = в пол-листа)
f  = frac(q);  d = min(f, 1 − f)
edge  = min(d.x / SeamWidthX, d.y / SeamWidthY)
Seams = 1 − smoothstep(0, 1, edge)
Tone  = hash(floor(q), PlateSeed)                  // постоянный внутри листа
```

Две ширины шва (`_Seam_Width_X` 0.03 против `_Seam_Width_Y` 0.06) — не
украшательство: у листа 2:1 одна общая ширина даёт вдвое разную долю по длинной
и по короткой стороне, как у кладки в Brick.

---

## Слои

```
Position (object-space, m_Space = 0, нода 2e3f5c85)
 ├─ ШВЫ          ProceduralPlateSeams      (b80b5726)  сетка + перевязка, две ширины шва
 ├─ ТОН ЛИСТА    ProceduralPlateTone       (78c27049)  хеш ячейки ТОЙ ЖЕ сетки
 ├─ ЗАКЛЁПКИ     ProceduralPlateRivets     (de4a6d74)  диски в углах листов, сдвинуты внутрь
 ├─ МИКРОШУМ     ProceduralPlateMetal      (68fcc064)  fbm, смешанный с Worley (механизм бронзы)
 ├─ ПАТИНА       ProceduralPlatePatina     (3ae2feac)  пятна, вытянуты вдоль локальной Y
 ├─ ЦАРАПИНЫ     ProceduralPlateScratchA   (e2b68058)  линии вдоль локальной Y
 │               ProceduralPlateScratchB   (caac2185)  линии в плоскости XZ
 │               ProceduralPlateScratchC   (3fed5276)  линии под углом _Scratch_Angle
 └─ МАСКА        ProceduralPlateScratchMask(f56c5481)  пятна-гаситель царапин

BaseColor  = 7 последовательных Lerp
Smoothness = Saturate( _Smoothness + заклёпки×_Rivet_Gloss + царапины×_Scratch_Gloss
                       + патина×_Patina_Gloss + швы×_Seam_Gloss )
Metallic   = Saturate( _Metallic − патина×_Patina_Metallic )
NormalTS   = Normal From Height( швы×_Seam_Bump + заклёпки×_Rivet_Bump
                                 + тон×_Plate_Lift + микрошум×_Metal_Bump +
                                 патина×_Patina_Bump ), сила = _Bump_Strength
```

Блоки `Emission` (`f0ba7f9c`) и `Occlusion` (`716de3c1`) **не подключены** —
эмиссии нет, окклюзия = 1. Три блока `VertexDescription.*` стоят на дефолтах.

### Пояснение по слоям

- **Швы.** `edge` — «во сколько раз ширина шва», `0` на границе листа. Валик
  вверх задаёт `_Seam_Bump = 0.45` (плюс — сварка выступает).
- **Тон листа.** Отдельная величина, постоянная внутри ячейки. Именно она, а не
  патина, продаёт «обшивка собрана из того, что было под рукой»: смешивание
  `_Plate_Color → _Bare_Color` по тону (Lerp №1) даёт видимый разброс оттенков
  между соседними листами.
- **Заклёпки.** Метрика анизотропная (смещение по Y делится на `_Plate_Aspect`),
  иначе на вытянутом листе заклёпка вышла бы овалом. `_Rivet_Width`/`_Rivet_Inset`
  заданы в долях **длины** листа, поэтому радиус заклёпки в метрах = `_Rivet_Width / _Plate_Scale`.
- **Микрошум листа.** `lerp(fbm, worley, 0.30)` — неровность проката и локальные
  вмятины. По этому же рисунку ложится патина, поэтому он задаёт и «зернистость»
  коррозии.
- **Патина.** Пятна вытянуты вдоль **локальной** Y (`p.y /= _Patina_Stretch`):
  коррозия стекает вниз. Мировой оси нигде нет — это принципиальное отличие от бронзы.
- **Царапины.** Ось, которую **не** сжали, и есть направление линии: A сжимает X и
  Z (`SX = SZ = _Scratch_Stretch`) — линии вдоль Y; B сжимает только Y — линии в
  плоскости XZ; C поворачивает локальную XY на `_Scratch_Angle` и сжимает
  повёрнутую X — линии идут диагонально.

---

## Ядро шума — `Assets/_Project/Shaders/ProceduralPlateNoise.hlsl`

Правило **T-BRONZE01** соблюдено: все экспонированные функции — **одно-выходные**
(File-режим, ровно один trailing `out`), **сигнатуры совпадают с порядком слотов
CF-нод** (слоты связываются по номеру). Новое поведение добавляется **новой**
функцией, существующие сигнатуры не меняются.

| CF-нода | id | Функция (сигнатура = порядок слотов) |
|---|---|---|
| `ProceduralPlateSeams` | `b80b5726` | `(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, SeamWidthX, SeamWidthY, PlateSeed, out Seams)` |
| `ProceduralPlateTone` | `78c27049` | `(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, PlateSeed, out PlateTone)` |
| `ProceduralPlateRivets` | `de4a6d74` | `(ObjectPos, PlateScale, PlateAspect, RowOffset, SeamWarp, RivetWidth, RivetInset, RivetSeed, out Rivets)` |
| `ProceduralPlateMetal` | `68fcc064` | `(ObjectPos, MetalScale, MetalDetail, MetalWarp, MetalSeed, out Metal)` |
| `ProceduralPlatePatina` | `3ae2feac` | `(ObjectPos, PatinaScale, PatinaStretch, PatinaSeed, out Patina)` |
| `ProceduralPlateScratchA` | `e2b68058` | `(ObjectPos, ScratchScale, ScratchStretch, ScratchDetail, ScratchSeed, out Scratch)` |
| `ProceduralPlateScratchB` | `caac2185` | `(ObjectPos, ScratchScale, ScratchStretch, ScratchDetail, ScratchSeed, out ScratchB)` |
| `ProceduralPlateScratchC` | `3fed5276` | `(ObjectPos, ScratchScale, ScratchStretch, ScratchDetail, ScratchSeed, ScratchAngle, out ScratchC)` |
| `ProceduralPlateScratchMask` | `f56c5481` | `(ObjectPos, ScratchMaskScale, ScratchMaskSeed, out ScratchMask)` |

Внутренние хелперы (CF-нод нет, снаружи недоступны): `PCPlate_Hash13`,
`PCPlate_ValueNoise3`, `PCPlate_Fbm3`, `PCPlate_WorleyF1`, `PCPlate_Ridge`,
`PCPlate_Grid`, `PCPlate_ScratchDir`, `PCPlate_Blotch`. Хеш без `sin`
(Hoskins-style), warp гладкий (интерполированный), октав 3.

Грабли ядра: `_Metal_Detail = 0` в пресете означает «октав меньше одной» —
`freq *= max(1.1, detail)` даёт пол 1.1, поэтому ступеней фактически не видно;
детализация чешуек аналога здесь нет, `MetalDetail` влияет только на fbm.

---

## Свойства (54) и боевые значения пресета `M_PC_Plate.mat`

В блэкборде имена с пробелами (`Plate Scale`, `Patina Spot Threshold`), в шейдере —
с подчёркиваниями (`_Plate_Scale`, `_Patina_Spot_Threshold`). Дефолты блэкборда и
значения пресета **совпадают** (проверено чтением обоих; `missingRefNames = 0`,
свойств вне таблицы — 0).

### Цвета (5)

| Свойство | Значение | Смысл |
|---|---|---|
| `_Plate_Color` | 0.50 / 0.32 / 0.16 | базовый цвет листа (уходит в `_Bare_Color` по тону) |
| `_Bare_Color` | 0.75 / 0.60 / 0.38 | «голый» металл: заклёпки, царапины, микрошум, выцветший лист |
| `_Seam_Color` | 0.20 / 0.17 / 0.14 | сварной шов (тёмнее листа) |
| `_Patina_Deep_Color` | 0.23 / 0.12 / 0.06 | тёмная подложка патины |
| `_Patina_Green_Color` | 0.25 / 0.55 / 0.45 | яркая зелень оксида (прижата к швам) |

### Сетка пластин (12)

| Свойство | Значение | Смысл |
|---|---|---|
| `_Plate_Scale` | 2.5 | пластин на метр вдоль ряда |
| `_Plate_Aspect` | 2 | длина листа / высота (2 = лист 2:1) |
| `_Row_Offset` | 0.5 | перевязка рядов (0.5 = в пол-листа) |
| `_Seam_Warp` | 0.05 | насколько ведёт сетку (0 = «наклеенные обои») |
| `_Seam_Width_X` | 0.03 | ширина шва вдоль длинной стороны |
| `_Seam_Width_Y` | 0.06 | ширина шва по высоте |
| `_Plate_Seed` | 0 | сид сетки (свойство, не мировая позиция) |
| `_Plate_Tone` | 0.35 | сила разброса тона листов |
| `_Plate_Vary` | 0.65 | разброс «насколько патина любит этот лист» |
| `_Seam_Amount` | 0.9 | сила слоя шва в альбедо |
| `_Seam_Bump` | 0.45 | рельеф шва (плюс — сварной валик вверх) |
| `_Plate_Lift` | 0.12 | подъём листа над швом (рельеф по тону) |

### Заклёпки (6)

| Свойство | Значение | Смысл |
|---|---|---|
| `_Rivet_Width` | 0.05 | радиус заклёпки в долях длины листа |
| `_Rivet_Inset` | 0.11 | сдвиг заклёпки внутрь листа (доля длины) |
| `_Rivet_Seed` | 0 | сид сетки заклёпок |
| `_Rivet_Amount` | 0.7 | сила заклёпок в альбедо |
| `_Rivet_Bump` | 0.5 | рельеф заклёпки (плюс — вверх) |
| `_Rivet_Gloss` | 0.22 | добавка глянца на заклёпке |

### Микрошум листа (6)

| Свойство | Значение | Смысл |
|---|---|---|
| `_Metal_Scale` | 6 | частота микрошума |
| `_Metal_Detail` | 0 | детализация (0 → пол 1.1, см. грабли ядра) |
| `_Metal_Warp` | 1.2 | искривление домена |
| `_Metal_Seed` | 1.38 | сид |
| `_Metal_Amount` | 0.3 | сила слоя в альбедо |
| `_Metal_Bump` | 0.12 | рельеф микрошума |

### Патина (9)

| Свойство | Значение | Смысл |
|---|---|---|
| `_Patina_Scale` | 6.3 | частота пятен |
| `_Patina_Stretch` | 2.5 | растяжение пятен вдоль локальной Y |
| `_Patina_Seed` | 1.59 | сид |
| `_Patina_Threshold` | 0.45 | порог **тёмной подложки** |
| `_Patina_Spot_Threshold` | 0.62 | порог **яркой зелени** (второй уровень того же поля) |
| `_Patina_Softness` | 0.22 | ширина мягкого края обоих порогов |
| `_Patina_Amount` | 0.85 | сила обоих уровней в альбедо |
| `_Patina_Bump` | **−0.25** | рельеф патины (минус — коррозия **вниз**) |
| `_Patina_Gloss` | **−0.35** | патина **матирует** |

### Царапины (11)

| Свойство | Значение | Смысл |
|---|---|---|
| `_Scratch_Scale` | 3.5 | частота царапин |
| `_Scratch_Stretch` | 12 | сила вытягивания (сжатие по осям) |
| `_Scratch_Detail` | 3.5 | детализация fbm царапин |
| `_Scratch_Seed` | 0.3 | сид (B использует `+17.3`, C — `+41.1`) |
| `_Scratch_Angle` | 0.73 | угол набора C (радианы) |
| `_Scratch_Threshold` | 0.92 | нижний порог: где царапина есть |
| `_Scratch_Band` | 0.2 | ширина полосы поверх порога |
| `_Scratch_Mask` | 0.49 | порог гасящей маски (она же Seed-аргумент маски) |
| `_Scratch_Mask_Scale` | 2 | частота пятен маски |
| `_Scratch_Amount` | 0.6 | сила царапин в альбедо |
| `_Scratch_Gloss` | 0.3 | царапина добавляет глянца (задиры блестят) |

### Поверхность и рельеф (5)

| Свойство | Значение | Смысл |
|---|---|---|
| `_Metallic` | 0.9 | металличность голого листа |
| `_Smoothness` | 0.5 | базовый глянец |
| `_Patina_Metallic` | 0.85 | насколько патина гасит металличность |
| `_Seam_Gloss` | **−0.3** | шов **матирует** (сварка шершавая) |
| `_Bump_Strength` | 1 | сила рельефа; подключён к слоту `Strength` ноды NFH |

---

## Цепочки (все проверены по рёбрам графа)

### Альбедо — 7 последовательных `Lerp`

| # | Нода | A (из чего) | B (во что) | T (по чему) |
|---|---|---|---|---|
| 1 | `839c4730` | `_Plate_Color` | `_Bare_Color` | тон листа × `_Plate_Tone` (`ee26f093`) |
| 2 | `b01ec12c` | №1 | `_Bare_Color` | микрошум × `_Metal_Amount` (`7deb847e`) |
| 3 | `7d377556` | №2 | `_Seam_Color` | швы × `_Seam_Amount` (`bfe7e938`) |
| 4 | `56df0b1d` | №3 | `_Patina_Deep_Color` | патина-подложка × `_Patina_Amount` (`f31e5c26`) |
| 5 | `68f80e4b` | №4 | `_Patina_Green_Color` | зелень × `_Patina_Amount` (`3fa37ad2`) |
| 6 | `1f970fd4` | №5 | `_Bare_Color` | царапины × `_Scratch_Amount` (`b2fcd7ef`) |
| 7 | `a644f909` | №6 | `_Bare_Color` | заклёпки × `_Rivet_Amount` (`5ce4b1d8`) |

Порядок не произвольный: шов (3) ложится **поверх** микрошума — иначе шум
разъедал бы сварку; патина (4–5) поверх шва, потому что коррозия ложится и на
валик; царапины (6) и заклёпки (7) — последними, это свежие механические
поражения, они перекрывают всё. Выход №7 (`a644f909[3]`) — блок `BaseColor`.

### Патина: два уровня из одного поля + прижатие к швам

```
patMain = Smoothstep( Edge1 = _Patina_Threshold,          // 21d02805
                      Edge2 = _Patina_Threshold + _Patina_Softness,   // 5e4bf384
                      In    = ProceduralPlatePatina.Out )   // 0ca6b21e
patSpot = Smoothstep( Edge1 = _Patina_Spot_Threshold,     // 15297651
                      Edge2 = _Patina_Spot_Threshold + _Patina_Softness, // a74c8a5a
                      In    = ProceduralPlatePatina.Out )   // 631bc105

gate    = Lerp( A = (1 − _Plate_Vary),  B = 1,  T = тон листа )   // a06aa01d → 23650d5f
patT    = (patMain × gate) × _Patina_Amount                       // 019a7191 → f31e5c26
spotT   = (patSpot × gate × Saturate(швы + 0.5)) × _Patina_Amount // 2ef9da7b, ed1dcd81,
                                                                  // cfd2ea72, d56d3282 → 3fa37ad2
```

Одно поле патины, два порога — тёмная подложка (0.45) и яркая зелень (0.62).
`gate` — множитель от 1−`_Plate_Vary` до 1: часть листов патина берёт почти
целиком, часть почти не трогает. Зелень дополнительно умножается на
`Saturate(швы + 0.5)` — константа **0.5 занимает слот `B`** у `Add` (`ed1dcd81`),
входящего ребра там нет; это и есть «влага держится на сварке».

### Царапины: три набора, одна общая полоса, гасящая маска

```
band    = _Scratch_Threshold + _Scratch_Band              // fc5776fd
scrA    = Smoothstep(_Scratch_Threshold, band, ScratchA.Out)  // 12ff2f06
scrB    = Smoothstep(_Scratch_Threshold, band, ScratchB.Out)  // 8fe4adbf
scrC    = Smoothstep(_Scratch_Threshold, band, ScratchC.Out)  // 09cf8042
sum     = Saturate( scrA + scrB + scrC )                  // df45f2b0, bed53b68, 259f58e6
mask    = Smoothstep(_Scratch_Mask, band, ScratchMask.Out)    // 60f9d856
scrT    = (sum × mask) × _Scratch_Amount                  // 5e667b77 → b2fcd7ef
```

Верхняя граница полосы общая для царапин и для маски — добавлять её дважды не
нужно; `_Scratch_Mask` (0.49) ниже порога царапин (0.92), поэтому маска «вырезает»
области, а не отдельные штрихи.

### Глянец

```
455107ca = _Smoothness + заклёпки×_Rivet_Gloss + царапины×_Scratch_Gloss   // 5a49839b, c9474561, cd6f2a15
bd93a353 = патина×_Patina_Gloss + швы×_Seam_Gloss                          // 011e99ab, 8cb213b4
Smoothness = Saturate( 455107ca + bd93a353 )                               // c337d195 → 3f031e8f
```

`_Patina_Gloss` (−0.35) и `_Seam_Gloss` (−0.3) **отрицательные** — патина и сварка
матируют; заклёпки и царапины прибавляют блеска. Отличие от Brick: там швы
матировали только через `_Joint_Amount`, здесь работают два независимых минуса.

### Металличность (подключена — отличие от Brick)

```
Metallic = Saturate( _Metallic − (патина × _Patina_Metallic) )   // 0b474aa0, 98b78f05 → 176c7c4f
```

Голый лист — 0.9, под патиной металличность падает до ~0.17: оксид диэлектрик.

### Порядок входов `Smoothstep` — естественный

ShaderGraph собирает `SmoothstepNode` как `Out = smoothstep(Edge1, Edge2, In)`
(слот 0 — нижний порог, 1 — верхний, 2 — значение). Во **всех шести** нодах этого
графа значение подано в слот 2 (`In`), а пороги — в 0 и 1: режущее поле везде
стоит `In`. Поэтому, как и в Brick/Concrete и в отличие от бронзы, маска садится
**где признак сильнее**, а не «серединой грани».

---

## Рельеф: почему здесь он работает

Вход высоты — **пятичленная сумма, у каждого вида свой коэффициент**:

| Вид | Нода × свойство | Знак |
|---|---|---|
| Швы (сварной валик) | `34e12e01` = Seams × `_Seam_Bump` | + вверх |
| Заклёпки | `d7e8ea68` = Rivets × `_Rivet_Bump` | + вверх |
| Тон листа (посадка) | `e980db48` = Tone × `_Plate_Lift` | + вверх |
| Микрошум проката | `6043dea5` = Metal × `_Metal_Bump` | + вверх |
| Патина | `1974a100` = Patina.Out × `_Patina_Bump` | **− вниз** |

Сумма: `82cc378f` = швы + заклёпки, `d3678d76` = тон + микрошум,
`cfa9d272` = патина + `d3678d76`, `740d9174` = `82cc378f` + `cfa9d272` → слот `In`
ноды `Normal From Height` (`ccf9e04f`).

Важная деталь: рельеф патины берёт **сырое поле** `ProceduralPlatePatina.Out`
(слот 4), а не маскированную `patT`. Если бы в рельеф шла `patT`, коррозия
продавливала бы металл ровно там же, где она видна в альбедо, — и рельеф «съедал»
бы собственную маску; сейчас края пятен читаются объёмнее, чем их заливка.

**Все три обязательные проверки (см. «Известная ошибка сборки» в каталоге) пройдены:**

1. Слоты NFH: `In m_Id = 0`, `Strength m_Id = 2`, `Out m_Id = 1` с `m_SlotType = 1`.
2. Второй аргумент вызова в **сгенерированном** шейдере — не `float(0.01)`:

```
Unity_NormalFromHeight_Tangent_float(
    _Add_740d9174e1bb4053beee0c79cc4dd0f4_Out_2_Float,           ← In  (сумма пяти видов)
    _BumpStrength_e9b1b830106d42e6af49ab06e04c19ca_Out_0_Float,  ← Strength (свойство!)
    _NormalFromHeight_ccf9e04f…_Position,
    _NormalFromHeight_ccf9e04f…_TangentMatrix,
    _NormalFromHeight_ccf9e04f…_Out_1_Vector3);
```

3. `surface.NormalTS = _NormalFromHeight_ccf9e04fabe045069cc4c9266aa9bb77_Out_1_Vector3;`

`float(0.01)` в сгенерированном тексте — **0 вхождений**. Это второй материал
проекта (после Brick) без «Известной ошибки сборки».

---

## Требования к геометрии

1. **Локальная Y — вверх.** По ней ориентированы ряды пластин (сетка берёт `p.x`
   и `p.y`), перевязка `_Row_Offset`, потёки патины (`_Patina_Stretch`) и набор
   царапин A. Лист, повёрнутый на 90°, получит вертикальные ряды.
2. **Локальная X — вдоль ряда.** `_Plate_Aspect` делит именно Y, поэтому длина
   листа читается по X.
3. **Пивот и единичный габарит не важны** для рисунка (сетка арифметическая от
   object-space), но важны для предсказуемости `_Plate_Scale`: 2.5 пластин на
   метр — это 40 см на лист при `_Plate_Aspect = 2`.
4. **Фаски на кромках не нужны.** Маски кромок в этом материале нет по
   построению: край листа — это край меша, а не признак материала.
5. **UV не нужны** — ни один слой их не читает.
6. **Поверхность должна быть плоской или цилиндрической.** Сетка разворачивается
   в плоскости локальных XY; на сильно искривлённой геометрии ряды «зальют»
   кривизну, а заклёпки начнут ползти по поверхности.

---

## FO-safe (Floating Origin)

- Весь шум — от `Position (Object)`: нода `2e3f5c85`, `m_Space = 0`; вход `ObjectPos`
  у всех 9 CF-нод — это её выход (рёбра 1, 9, 15, 23, 28, 32, 37, 42, 48).
- В графе `m_Space` принимает только значения `0` и `3` (`3` — `Tangent` у
  `NormalMaterialSlot`, штатное поле слота). `m_Space = 4` (`AbsoluteWorld`) — **0 вхождений**.
- Слово `AbsoluteWorld` в графе — **0**, в сгенерированном шейдере — **0**.
  В `ProceduralPlateNoise.hlsl` оно встречается **один раз — в комментарии**,
  который прямо говорит, что мировая позиция не используется.
- Сиды (`_Plate_Seed`, `_Rivet_Seed`, `_Metal_Seed`, `_Patina_Seed`,
  `_Scratch_Seed`, `_Scratch_Mask`) — **свойства материала**, а не мировая позиция.
- Вертикального bias «от мировой оси» нет: `_Patina_Stretch` работает по
  **локальной** Y, поэтому при переносе объекта рисунок едет вместе с ним.
- Ожидаемое поведение при ребейзе (F8/F9): рисунок стоит на месте, потому что
  ни одно слагаемое не зависит от мировых координат.

---

## Грабли формата `.shadergraph` (для будущих правок)

1. Файл — поток JSON-документов, разделитель — `}` + **пустая строка** + `{`.
   Пустая строка внутри документа обрывает его; разделитель `\n` (без пустой
   строки) тоже ломает импорт (`The document root must not follow by other values`).
2. Рёбра и `m_Nodes` лежат внутри **одного** документа `GraphData`; ноды и слоты —
   отдельные документы. Искать рёбра `Matches` по всему тексту, а не по документу.
3. Ребро ссылается на `m_ObjectId` **ноды** (не слота); `m_SlotId` — номер слота.
4. Plate записан **чистым LF** (как Brick). У `Concrete.shadergraph` — CRLF:
   такой файл резать по `}\s*\n\s*{` либо нормализовать.
5. `SetSlotValue` правит `m_Value` **и** `m_DefaultValue`; динамические слоты
   (`Add`/`Multiply`/`Lerp`/`Subtract`/`Smoothstep`) имеют форму `{x,y,z,w}`.
   Константы слотов доезжают до шейдера как `float(0.5)` (5 вхождений — ровно
   константы этого графа) и `float(1)` (12 вхождений).
6. `NormalFromHeightNode`: `m_Slots` лежит как `[In, Strength, Out]`, а нативные
   id — `In = 0`, `Strength = 2`, `Out = 1`. Выход брать по id 1, `Strength`
   обязательно подключать.
7. Целевой рендер — URP Lit (`UniversalTarget` + `UniversalLitSubTarget`), Forward.

---

## Проверено (агент, без визуала)

Метод: `Temp/PlateVerify.cs`, секции 1–8; сгенерированный текст — через отражение
`UnityEditor.ShaderGraph.ShaderGraphImporter.GetShaderText` (перегрузка выбирается
по **наибольшей длине** результата: узкая отдаёт 1022-символьную заглушку, полная —
237 690 символов).

| Пункт | Результат |
|---|---|
| Импорт графа | `shader = Plate`, `hasError = false`, `supported = true`, `shaderMessages = 0` |
| Документы / ноды / рёбра / свойства | 487 / 129 (120 + 9 блоков) / 173 / 54 (49 float + 5 color) |
| Рёбра в JSON против вхождений `m_OutputSlot` | 173 = 173 |
| Целостность связей | **двойных входов 0**, **висячих концов рёбер 0**, **висячих ссылок на слоты 0** |
| CF-ноды на ядро Plate | 9 из 9 (`cfOnPlateHlsl = 9`), ссылок на бронзовый HLSL — 0 |
| FO-safe | `m_Space = 4` — 0; `AbsoluteWorld` в графе 0 и в шейдере 0 |
| Свойства против пресета | `missingRefNames = 0`, вне таблицы — 0; дефолты == пресет |
| Подключённые блоки | `BaseColor ← a644f909`, `Metallic ← 176c7c4f`, `NormalTS ← ccf9e04f`, `Smoothness ← 3f031e8f` |
| Рельеф, пункт 1 (нативные id NFH) | `In = 0`, `Strength = 2`, `Out = 1`, `m_SlotType(Out) = 1` |
| Рельеф, пункт 2 (второй аргумент) | `_BumpStrength_e9b1b830…_Out_0_Float`, `float(0.01)` — **0** |
| Рельеф, пункт 3 (`surface.NormalTS`) | `= _NormalFromHeight_ccf9e04fabe045069cc4c9266aa9bb77_Out_1_Vector3` |
| Пресет | `rq = 2000`, `kw = 0`, `inst = False`, `floatsPresent = 49/49`, `problems = 0`, `_Metallic = 0.9`, `_Plate_Scale = 2.5`, `_Bump_Strength = 1` |
| Бронза не тронута | 475 документов / 149 рёбер / HLSL 8 883 символа; `plateRefsInBronze = 0` |

---

## Не проверено (`NOT RUN`)

- **Визуал**: читается ли обшивка как клёпаные листы (а не как шахматная заливка);
  видно ли заклёпки в углах; не «затирает» ли патина сетку швов при
  `_Patina_Amount = 0.85`.
- **Рельеф глазами**: не переворачивает ли знак суммы (`_Seam_Bump +0.45`,
  `_Patina_Bump −0.25`) восприятие плоскости; различимы ли микрошум проката при
  `_Metal_Amount = 0.3` / `_Metal_Bump = 0.12`.
- **Царапины**: читаются ли три набора как разные направления при
  `_Scratch_Angle = 0.73`, и не превращается ли `_Scratch_Mask = 0.49` в «прорехи».
- **Металличность под патиной**: `_Metallic 0.9 → ~0.17` — не выглядит ли оксид
  «пластиковым» на фоне `_Patina_Gloss = −0.35`.
- **F8/F9 (Floating Origin)**: рисунок стоит на месте после сдвига, 0 ошибок в
  консоли.
- **ALU / овердрав**: не замерялись. Ядро считает fbm (3 октавы) в 6 слоях плюс
  Worley 3×3×3 в микрошуме — это самый тяжёлый материал проекта после Brick.
- **Порядок сглаживания**: `Render Face = Front` (дефолт), двусторонность не
  включалась.

Порядок ручной проверки: сцена с тестовыми примитивами (плоскость + цилиндр) →
назначить `M_PC_Plate.mat` → F8 (сдвиг) → объект на месте, шум стоит, 0 errors →
F9 (возврат).

---

## Возможные пресеты-варианты (после визуального теста)

| Задача | Подстройка |
|---|---|
| Днище/борт корабля, сильно корродированный | `_Patina_Amount` 0.85 → 1.0, `_Patina_Threshold` 0.45 → 0.32, `_Patina_Metallic` 0.85 → 0.95 |
| Ровный лист без коррозии (обшивка рабочих зон) | `_Patina_Amount` → 0.1, `_Plate_Vary` 0.65 → 0.35 |
| Крупные листы (крупнее модуль) | `_Plate_Scale` 2.5 → 1.2, `_Seam_Width_X/Y` ÷2, `_Rivet_Width` ×2 |
| Мелкая клёпка, «клепаная авиация» | `_Plate_Scale` 2.5 → 5, `_Rivet_Amount` 0.7 → 0.9, `_Rivet_Gloss` 0.22 → 0.35 |
| Свежий металл с царапинами | `_Patina_Amount` → 0.15, `_Plate_Color` → ближе к `_Bare_Color`, `_Scratch_Amount` 0.6 → 0.8 |
| Патина только по сварке | `_Patina_Spot_Threshold` 0.62 → 0.5, `_Scratch_Mask` не трогать; зелень и так умножается на `Saturate(швы + 0.5)` |
