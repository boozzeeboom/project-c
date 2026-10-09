# Lava — процедурная лава (корка + расплав), без текстур

> **Статус: PROTOTYPE v1.** Граф собран **с нуля**; у `Brick` взяты только
> шаблоны документов. Импорт OK, `ShaderHasError = false`, `isSupported = true`,
> `GetShaderMessages = 0`, 316 документов / 88 нод / 104 ребра / 38 свойств
> блэкборда (31 float + 7 color), `badSlot = 0`, `doubleDriven = 0`, висячих
> ссылок 0, дублей ref-имён 0, 38 параметров в пресете.
> **Визуальная приёмка и Play Mode — `NOT RUN`** (шаг пользователя).

| Что | Путь | GUID |
|---|---|---|
| Ядро шума | `Assets/_Project/Shaders/ProceduralLavaNoise.hlsl` | `403fdfb6b3d1ffe4fbeb9bcfb9357039` |
| Граф | `Assets/_Project/Materials/Lava/Lava.shadergraph` | `3e00b0ea2fbd3ff4595ec72d512111e2` |
| Пресет | `Assets/_Project/Materials/Lava/M_PC_Lava.mat` | `f318d32e6670fdd4fb5914a8e61f2908` |
| Сборщик графа | `Tools/LavaBuilder.cs` | вне git |
| Пресет-скрипт | `Tools/LavaMaterial.cs` | вне git |
| Приёмка | `Tools/LavaVerify.cs` (только чтение) | вне git |
| Донор шаблонов | `Assets/_Project/Materials/Brick/Brick.shadergraph` | **только чтение** |

---

## 1. Роль материала и кадр

Лава — это **два состояния одного вещества**: тёмная остывшая корка (базальт)
и расплав под ней. Корка набрана плитами, между плитами — трещины; сквозь
трещины и в тонких местах корки виден расплав. Поэтому все слои делятся на
«корку» (тон плиты, зола) и «расплав» (жар, струи, ядро), и альбедо с эмиссией
берут **одни и те же поля**.

Кадр:

- **локальная Y — вверх**; вдоль неё вытянуты и ячейки корки, и линии потока:
  лава течёт вниз по локальной Y;
- **один объект = один поток или один валун лавы**;
- UV не нужны, текстур нет, vertex colors не читаются.

Материал предназначен для: потоков лавы и лавовых полей, остывающих валунов,
трещин в полу, жерл, «раненых» скал. Лава — **эмиссивный** материал: без
подключённого блока `Emission` он не читается (см. §5).

## 2. Слои

Всё считается от **object-space** координат, хеш без `sin` (Hoskins-style).

| Функция | Входы → выход | Что даёт |
|---|---|---|
| `ProceduralLavaFissures` | `ObjectPos, CrustScale, CrustStretch, CrustWidth, CrustSeed` → `Fissures` | **1 — трещина**, 0 — тело плиты. Граница ячейки Worley (`F2 − F1`), ширина — `CrustWidth` |
| `ProceduralLavaPlateTone` | `ObjectPos, CrustScale, CrustStretch, CrustSeed` → `PlateTone` | **тон плиты**: постоянен внутри плиты, скачком меняется на её границе (хеш выигравшей ячейки) |
| `ProceduralLavaHeat` | `ObjectPos, HeatScale, HeatStretch, HeatSeed` → `Heat` | крупное поле **жара** в 0..1: где корка тонка, сквозь неё просвечивает расплав |
| `ProceduralLavaFlow` | `ObjectPos, FlowScale, FlowStretch, FlowWidth, FlowSeed` → `Flow` | **линии потока** вдоль локальной Y («верёвки»): 1 на линии, 0 в стороне |
| `ProceduralLavaSkin` | `ObjectPos, SkinScale, SkinDetail, SkinSeed` → `Skin` | мелкая **шлаковая корка** в 0..1 (пузыри, шлак) |

Все пять — одно-выходные (`float`-версии) плюс пять `_half`-обёрток; в графе
подключены только `_float`. Контракт T-BRONZE01: **порядок слотов CF-ноды =
порядок параметров HLSL, `out` — последним**, связывание позиционное, имена
косметичны.

Ключевые приёмы:

- **ton и его границы не могут разъехаться.** Трещины и тон плиты считаются
  **одним и тем же** Worley и **одним доменом**: `CrustScale`/`CrustStretch`/
  `CrustSeed` подаются в обе CF-ноды из одних и тех же свойств. Это тот же
  принцип, что у Cobble (шов/тон камня), Brick (шов/тон кирпича) и Foliage
  (лист/тон листа).
- **трещина светится только там, где под ней жар.** `Fissures × HeatMask`:
  без этого гашения светилась бы вся сетка ячеек целиком, и рисунок читался бы
  паутиной, а не лавой.
- **одно поле жара в двух ролях**: пороги `_Heat_Threshold` (корка тонка, из-под
  неё прёт тёмно-красное) и `_Molten_Threshold` (корки нет вовсе, расплав).
  Второй порог выше первого, оба — `Smoothstep(порог, порог + мягкость, Heat)`.
- **домен жара свёрнут домен-warp'ом**, амплитуда жёстко `0.35`: без него пятна
  расплава выходят ровными кляксами. Отдельного регулятора у свёртки нет
  намеренно — он был бы третьим множителем одного и того же.
- **линии потока намеренно не рвутся гейтом**: поток — непрерывная струя, а не
  сетка. Оборвал бы их только крупный fbm, как у сучьев в Foliage.

## 3. Граф

88 нод, из них: 9 блоков мастер-стека, `Object Position` (`395003fc`),
5 CF-нод, 38 PropertyNode, `Normal From Height` «Crust Relief» (`b4e36847`),
остальное — математика (`Lerp` / `Multiply` / `Add` / `Smoothstep` / `Saturate`).

CF-ноды (id и порядок слотов — как в файле графа):

| CF-нода | id | Слоты |
|---|---|---|
| Fissures | `53aee882` | `0 ObjectPos, 1 CrustScale, 2 CrustStretch, 3 CrustWidth, 4 CrustSeed, 5 Fissures(out)` |
| Plate Tone | `d1b0c41e` | `0 ObjectPos, 1 CrustScale, 2 CrustStretch, 3 CrustSeed, 4 PlateTone(out)` |
| Heat | `d2eaaa27` | `0 ObjectPos, 1 HeatScale, 2 HeatStretch, 3 HeatSeed, 4 Heat(out)` |
| Flow | `2b0c634b` | `0 ObjectPos, 1 FlowScale, 2 FlowStretch, 3 FlowWidth, 4 FlowSeed, 5 Flow(out)` |
| Skin | `7475b8b5` | `0 ObjectPos, 1 SkinScale, 2 SkinDetail, 3 SkinSeed, 4 Skin(out)` |

Блоки мастер-стека (проверено чтением графа, слот #0 каждого):

```
VertexDescription.Position [PositionMaterialSlot] <= Object Position
VertexDescription.Normal   [NormalMaterialSlot]   <= не подключён
VertexDescription.Tangent  [TangentMaterialSlot]  <= не подключён
SurfaceDescription.BaseColor  [ColorRGBMaterialSlot] <= LerpNode «Flow Ropes» (87e204c8)#3
SurfaceDescription.Smoothness [Vector1MaterialSlot]  <= SaturateNode «Smoothness Clamp» (0a467a8b)#1
SurfaceDescription.NormalTS   [NormalMaterialSlot]   <= NormalFromHeightNode «Crust Relief» (b4e36847)#1
SurfaceDescription.Emission   [ColorRGBMaterialSlot] <= MultiplyNode «x Emission Strength» (d9352f7a)#2
SurfaceDescription.Occlusion  [Vector1MaterialSlot]  <= не подключён
SurfaceDescription.Metallic   [Vector1MaterialSlot]  <= не подключён
```

`Metallic` и `Occlusion` — не подключены (константы `float(0)` / `float(1)`):
лава — диэлектрик, AO нечего задавать. `Emission` — **подключён**, это несущий
канал материала.

Таргет унаследован от `Brick` **без единой правки**: `m_SurfaceType 0` (Opaque),
`m_AlphaMode 0` (None), `m_AlphaClip false`, `m_RenderFace 2` (Both),
`m_CastShadows true`, `m_ReceiveShadows true`, `m_ZWriteControl 0`,
`m_ZTestMode 4`, `m_WorkflowMode 1` (Metallic), `m_NormalDropOffSpace 0`
(Tangent), `m_ClearCoat false`, `m_BlendModePreserveSpecular true`,
`m_AllowMaterialOverride false`.

## 4. Как собраны каналы

**АЛЬБЕДО — 7 последовательных `Lerp`**, порядок от старого к новому
(корка остывает последней, поэтому расплав рисуется поверх золы, а «верёвки»
потока — поверх всего):

| # | Нода | A → B, T | Что появляется |
|---|---|---|---|
| 1 | Crust by Plate | `_Crust_Color` → `_Plate_Color`, `PlateTone × _Plate_Vary` | разные плиты корки отличаются тоном |
| 2 | Ash | → `_Ash_Color`, `Skin × _Skin_Amount` | серая зола/шлак поверх корки |
| 3 | Ember Pools | → `_Ember_Color`, `Heat Mask` | тёмно-красные остывающие пятна |
| 4 | Melt | → `_Melt_Color`, `Molten Mask` | оранжевый расплав |
| 5 | Fissure Line | → `_Stream_Color`, `Fissures × Heat Mask` | жёлтая раскалённая трещина |
| 6 | Core | → `_Core_Color`, `Molten Mask × Fissures` | белокалёное ядро: трещина **в** расплаве |
| 7 | Flow Ropes | → `_Crust_Color`, `(Flow × Molten Mask) × _Flow_Amount` | тёмная корка на гребнях струй |

**ЭМИССИЯ** = `Saturate( (Fissures × Heat Mask + Molten Mask) × _Glow_Amount )`
умножить на **ту же** Lerp-цепочку и на `_Emission_Strength`:

```
Emission = AlbedoChain × Glow × _Emission_Strength
```

Цвет эмиссии — не отдельная палитра, а тот же цвет, что и альбедо: горячая
трещина светится своим цветом, остывшая корка — не светится вовсе (`Glow = 0`
на плитах, поэтому нулевое свечение там получается не порогом, а умножением).
`_Emission_Strength = 3.0` даёт HDR-значения (жёлтая трещина → ~`3, 2.2, 0.4`),
то есть материал готов к bloom'у, если в сцене он есть.

**ГЛЯНЕЦ** = `Saturate(_Smoothness + Molten Mask × _Molten_Gloss)`: корка
матовая, расплав глянцевый.

**РЕЛЬЕФ** — пятичленный `Add`, вход `Normal From Height`:

```
Skin × _Skin_Bump (шлак)
Plate Tone × _Plate_Vary × _Plate_Bump (плита поднята)
Fissures × _Fissure_Bump (−0.55 — канавка вниз)
Flow × _Flow_Bump (струя поднята)
Heat × _Heat_Bump (вспучивание в тонких местах)
```

Сила рельефа задаётся слотом `Strength` ноды `Normal From Height`, и он
**подключён к `_Bump_Strength`** (см. §7). Пороги `Smoothstep` поданы в
**естественном порядке** (значение в `In`, порог в `Edge1`, `Edge1 + мягкость`
в `Edge2`) — как в Concrete/Brick/Plate/Foliage, а не как в унаследованном
кирпичном шаблоне, где входы были переставлены.

## 5. Эмиссия: что именно включает свечение

У лавы нет отдельного «ключевого слова» эмиссии — она включается **самим
фактом подключения блока** `SurfaceDescription.Emission`. Проверено по исходникам
движка:

- блок `SurfaceDescription.Emission` — это `ColorRGBMaterialSlot`, то есть
  **RGB-слот** (`m_Value` из трёх компонент), с `ColorMode = HDR`
  (`m_ColorMode: 1` — единственная единица среди девяти `m_ColorMode` в файле
  графа);
- `PBRForwardPass.hlsl:146` кладёт его в `surface.emission`
  **безо всяких `#ifdef`**, `_EMISSION` в ShaderGraph-ветке URP не участвует
  (это ключевое слово рукописного `Lit.shader`, а не графа);
- поэтому у `M_PC_Lava.mat` **ноль активных ключевых слов** — и это правильно.

Так как слот — RGB, а наши цвета — `Vector4` (`ColorShaderProperty`),
ShaderGraph вставляет усечение `.xyz` (в сгенерированном тексте:
`surface.BaseColor = (_FlowRopes_…#3_Vector4.xyz);`,
`surface.Emission = (_xEmissionStrength_…#2_Vector4.xyz);`). Это тот же путь, по
которому уже работает `BaseColor` во всех материалах проекта.

## 6. Параметры (38)

«Дефолт графа» — значение свойства в блэкборде, «пресет» — значение в
`M_PC_Lava.mat`. Отличаются только **три цвета корки** (в пресете светлее
дефолтов графа — так остывшая корка читается камнем, а не чёрным вырезом);
остальные 35 совпадают.

| Свойство | Дефолт графа | Пресет | Назначение |
|---|---|---|---|
| `_Crust_Scale` | 1.5 | 1.5 | сколько плит корки укладывается в единицу длины |
| `_Crust_Stretch` | 2.4 | 2.4 | вытягивание ячеек вдоль локальной Y (плиты вдоль потока) |
| `_Crust_Width` | 0.09 | 0.09 | ширина трещины в домен-единицах |
| `_Crust_Seed` | 4.3 | 4.3 | сид домена корки (сдвиг, не мировая позиция) |
| `_Plate_Vary` | 0.55 | 0.55 | разброс тона между плитами |
| `_Plate_Bump` | 0.18 | 0.18 | подъём плиты в рельефе |
| `_Fissure_Bump` | −0.55 | −0.55 | канавка трещины в рельефе |
| `_Heat_Scale` | 0.85 | 0.85 | крупность поля жара |
| `_Heat_Stretch` | 1.5 | 1.5 | вытягивание жара вдоль локальной Y |
| `_Heat_Seed` | 12.7 | 12.7 | сид поля жара |
| `_Heat_Threshold` | 0.40 | 0.40 | порог жара (корка тонка) |
| `_Heat_Softness` | 0.34 | 0.34 | мягкость порога жара |
| `_Heat_Bump` | 0.30 | 0.30 | вспучивание в рельефе |
| `_Molten_Threshold` | 0.62 | 0.62 | порог расплава (второй порог того же поля) |
| `_Molten_Softness` | 0.22 | 0.22 | мягкость порога расплава |
| `_Flow_Scale` | 2.6 | 2.6 | крупность линий потока |
| `_Flow_Stretch` | 5.0 | 5.0 | растяжение линий вдоль локальной Y (струи) |
| `_Flow_Width` | 0.25 | 0.25 | ширина струи |
| `_Flow_Seed` | 27.1 | 27.1 | сид линий потока |
| `_Flow_Amount` | 0.55 | 0.55 | вес тёмных «верёвок» поверх расплава |
| `_Flow_Bump` | 0.35 | 0.35 | подъём струй в рельефе |
| `_Skin_Scale` | 14.0 | 14.0 | мелкость шлака |
| `_Skin_Detail` | 2.4 | 2.4 | множитель частоты октав шлака |
| `_Skin_Seed` | 8.9 | 8.9 | сид шлака |
| `_Skin_Amount` | 0.40 | 0.40 | сколько золы в тоне |
| `_Skin_Bump` | 0.28 | 0.28 | мелкий рельеф шлака |
| `_Glow_Amount` | 1.0 | 1.0 | вес свечения (трещина×жар + расплав), сверху `Saturate` |
| `_Emission_Strength` | 3.0 | 3.0 | множитель HDR-эмиссии |
| `_Molten_Gloss` | 0.55 | 0.55 | прибавка глянца в расплаве |
| `_Smoothness` | 0.34 | 0.34 | базовая гладкость корки |
| `_Bump_Strength` | 1.0 | 1.0 | сила рельефа (слот `Strength` ноды NFH) |
| `_Crust_Color` | 0.055, 0.05, 0.048 | **0.09, 0.085, 0.08** | тёмная остывшая корка (база) |
| `_Plate_Color` | 0.11, 0.10, 0.095 | **0.16, 0.15, 0.14** | поднятая плита, светлее корки |
| `_Ash_Color` | 0.17, 0.16, 0.15 | **0.26, 0.25, 0.24** | серая зола/шлак поверх корки |
| `_Ember_Color` | 0.42, 0.075, 0.02 | 0.42, 0.075, 0.02 | тёмно-красный жар |
| `_Melt_Color` | 1.0, 0.36, 0.05 | 1.0, 0.36, 0.05 | оранжевый расплав |
| `_Stream_Color` | 1.0, 0.72, 0.12 | 1.0, 0.72, 0.12 | жёлтая раскалённая линия (трещина и струя) |
| `_Core_Color` | 1.0, 0.94, 0.62 | 1.0, 0.94, 0.62 | белокалёное ядро |

Как крутить:

- **больше/меньше лавы** — `_Heat_Threshold` (сколько площади «горячее») и
  `_Molten_Threshold` относительно него (сколько из этого — открытый расплав);
  разрыв между порогами задаёт полосу тёмно-красного между коркой и расплавом;
- **крупнее/мельче плиты корки** — `_Crust_Scale` (мельче = крупнее плиты);
- **направленность потока** — `_Crust_Stretch` и `_Flow_Stretch`: оба 1.0 дают
  изотропный рисунок, 5–8 дают длинные струи вдоль локальной Y;
- **ядовитость свечения** — `_Glow_Amount` и `_Emission_Strength`;
- **«стеклянность» расплава** — `_Molten_Gloss` и `_Smoothness`.

Свойств, отключённых «в ноль», в графе нет: все 38 присутствуют и используются
(проверено по сгенерированному шейдеру — неиспользованных 0).

## 7. Floating Origin

Правило №1 каталога выполнено технически, а не по договорённости:

- единственный вход координат — узел `Position` с `m_Space = 0`
  (**object space**), `m_PositionSource` (наследие донора) на вывод не влияет;
- `AbsoluteWorld` в графе — **0 вхождений**; в сгенерированном шейдере —
  **0 вхождений**, `IN.ObjectSpacePosition` — 40;
- единственное `m_Space = 3` в файле — это слот блока `NormalTS`
  (`NormalMaterialSlot`), касательное пространство нормали; оно не имеет
  отношения к позиции и присутствует во всех материалах проекта;
- сиды (`_Crust_Seed`, `_Heat_Seed`, `_Flow_Seed`, `_Skin_Seed`) — **сдвиг
  домена**, а не мировая позиция: ребейз F8/F9 их не трогает.

Цена решения, как у всех object-space материалов проекта: **масштаб объекта
масштабирует рисунок**. Разные по размеру валуны нужно либо нормировать, либо
крутить `_Crust_Scale`/`_Heat_Scale` на материал.

## 8. Требования к геометрии и грабли

**Требования:**

- **локальная Y — вверх** (поток течёт вниз по Y); поворот объекта поворачивает
  и струи, и плиты;
- **один объект = один поток/валун**; UV и vertex colors не нужны;
- **плоские нормали не обязательны** (в отличие от Foliage и Parquet): корка —
  это 3D-сетка ячеек, нормаль поверхности в неё не входит;
- материал **двусторонний** (`Render Face = Both`) и **непрозрачный**:
  `Alpha Clip` выключен, альфа-канала у него нет;
- лава читается только вместе с эмиссией: если в сцене нет tone mapping/bloom,
  максимальные значения `_Emission_Strength` выглядят просто яркими пятнами.

**Грабли:**

- **светится вся сетка** — значит `Heat Mask` не гасит трещины: проверьте, что
  ребро `Fissure × Heat` на месте, и что `_Heat_Softness` не раздута до того,
  что `HeatMask ≈ 1` на всей поверхности;
- **рисунок читается плиткой, а не потоком** — `_Crust_Stretch` и
  `_Flow_Stretch` близки к 1.0, либо нормаль поверхности смотрит вдоль
  локальной Y (на торце потока растяжение не видно);
- **корка чернильно-чёрная** — это ожидаемо для лавы, но если нужен читаемый
  камень, поднимайте `_Crust_Color`/`_Plate_Color`/`_Ash_Color`, а не
  `_Smoothness`;
- **порог расплава ниже порога жара** (`_Molten_Threshold < _Heat_Threshold`)
  даёт область, где расплав есть, а «тонкой корки» нет; формально это не
  ломается (слои накладываются по очереди), но тёмно-красная полоса между
  коркой и расплавом пропадает;
- **`Normal From Height`**: сила рельефа берётся из материала через
  `_Bump_Strength`. Если слот `Strength` окажется не подключён, нода возьмёт
  шаблонный дефолт **0.01** и погасит рельеф стократно — это «Известная ошибка
  сборки» каталога, и Lava её **не имеет** (см. §9).

## 9. Проверка

Что **подтверждено** (`Tools/LavaVerify.cs`, только чтение):

- структура: 316 документов / 315 разделителей / 316 ObjectId,
  `m_Nodes = 88` (документов нод тоже 88), `m_Properties = 38`,
  `m_Edges = 104`, `badSlot = 0`, `doubleDriven = 0`, `danglingNode = 0`,
  `notANode = 0`, `notAProp = 0`, дублей ref-имён нет, все 38 ожидаемых
  ref-имён на месте, пустых строк внутри документов нет;
- CF-нод 5, у всех `m_FunctionSource` = guid нашего HLSL, чужих 0;
  `ProceduralLava` вхождений 5 (по одному вызову на функцию),
  guid кирпичного HLSL — 0, слово `Brick` — 0;
- порядок аргументов на местах вызова совпадает с порядком слотов:
  `ProceduralLavaFissures_float(IN.ObjectSpacePosition, _CrustScale…,
  _CrustStretch…, _CrustWidth…, _CrustSeed…, …Fissures_5_Float)` — и так для
  всех пяти;
- **все три обязательных пункта рельефа** (из «Известной ошибки сборки»):
  слоты NFH `In/Strength/Out = 0/2/1` (прочитано из графа), `NFH.In` =
  `AddNode(db9bab61)#2` (четвёртый `Add` — «+ Heat»), `NFH.Strength` =
  `PropertyNode(faee9640)` (`_Bump_Strength`), `NFH#1 → NormalTS`;
  в сгенерированном тексте второй аргумент вызова —
  `_BumpStrength_faee9640742747d6bc069921defd66f7_Out_0_Float`,
  `float(0.01)` и шаблонный `float(0.009999999776482582)` — **0 вхождений**;
- **эмиссия работает**: `surface.Emission = (_xEmissionStrength_d9352f7a…#2_Vector4.xyz)`
  в трёх местах, константой-нулём не является; `_Emission_Strength` и
  `_Glow_Amount` встречаются в тексте по 14 раз;
- по сгенерированному тексту (192 769 символов): `surface.BaseColor` = выход
  последнего `Lerp` «Flow Ropes», `surface.Smoothness` = выход `Saturate`
  «Smoothness Clamp», `surface.NormalTS` = `Out_1` ноды NFH,
  `surface.Metallic = float(0)`, `surface.Occlusion = float(1)`;
  неиспользованных свойств — 0; кирпичных имён (`_Brick_`, `_Mortar_`,
  `_Piece_`, `_Joint_`, `_Efflo`) — 0;
- шейдер: `name=Lava`, `hasError=False`, `supported=True`, `messages=0`,
  43 свойства (38 наших + `_QueueControl`/`_QueueOffset` + 3 lightmap-текстуры),
  все 38 ожидаемых на месте;
- материал: `shader=Lava`, `queue=2000`, `keywords=0`, прочитано 38/38,
  отсутствующих 0;
- донор `Brick.shadergraph` не изменён (385 документов / 122 ребра — как было).

**`NOT RUN` — делает пользователь:**

- визуальная приёмка: читается ли рисунок как лава (а не как сетка или
  черепица), заметна ли разница «корка / тонкая корка / расплав»;
- калибровка `_Heat_Threshold` / `_Molten_Threshold` / `_Glow_Amount` /
  `_Emission_Strength` под сцену (и наличие bloom'а);
- силы рельефа `_Plate_Bump` / `_Fissure_Bump` / `_Skin_Bump` / `_Flow_Bump` /
  `_Heat_Bump` и общий `_Bump_Strength` на рабочей дистанции;
- поведение на сглаженных нормалях и на цилиндрических объектах;
- **Play Mode, F8/F9 (ребейз Floating Origin)**, 0 errors;
- совместимость с пост-эффектом `EdgeDetection` (материал контур не дублирует);
- демо-объект в сцене **не выставлялся**: материал собран сам по себе, ни
  `BootstrapScene.unity`, ни `WorldScene_0_0.unity` не менялись.
