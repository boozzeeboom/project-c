# Marble — процедурный мрамор (сеть прожилок + облачность камня)

> Статус: **PROTOTYPE v1** (граф — производная от `Fabric.shadergraph`: топология, каркас
> кромок и трещин унаследованы, все слои заменены мраморными).
> Проверено агентом: импорт OK, `ShaderHasError = false`, `isSupported = true`,
> `GetShaderMessages = 0` после принудительной GPU-компиляции (offscreen-рендер куба —
> 0 magenta-пикселей), структурный аудит графа чистый, обязательная проверка рельефа пройдена,
> порядок аргументов CF-вызовов совпадает с сигнатурами HLSL.
> **Визуал и Play Mode — `NOT RUN`** (тесты и скриншоты делает только пользователь).

## Файлы

| Файл | Что это |
|---|---|
| `Assets/_Project/Materials/Marble/Marble.shadergraph` | граф, шейдер `Marble` (URP Lit, Opaque, Metallic workflow) |
| `Assets/_Project/Materials/Marble/M_PC_Marble.mat` | пресет для крупных поверхностей (полы, колонны, панели, столешницы-плиты) |
| `Assets/_Project/Materials/Marble/SMALL_M_PC_Marble.mat` | Material Variant от `M_PC_Marble` для мелких предметов; переопределений нет — как у `SMALL_M_PC_Wood` / `SMALL_M_PC_Fabric` |
| `Assets/_Project/Shaders/ProceduralMarbleVeinNoise.hlsl` | ядро шума (guid `5edf78ec3c4639e42b6465a085d3b63e`) |

## Что построено

```
Position (object-space, m_Space = 0)
 ├─ ОБЛАЧНОСТЬ ProceduralMarbleMottle → крупный знаковый шум ±_Mottle_Amount
 ├─ ПРОЖИЛКИ  ProceduralMarbleVeins   ← облачность уходит во вход Mottle (сдвигает прожилки)
 │              искажённое турбулентностью fbm; прожилка = изолиния s ≈ 0.5,
 │              d = |2s − 1|, полоса задаётся _Vein_Width и сглаживается по fwidth;
 │              второй набор — вдвое чаще и вдвое тоньше (сеть, а не одиночные линии)
 ├─ ТОН КАМНЯ ProceduralMarbleCloud   → два fbm, вытянутые по распилу, усреднены
 ├─ КАВЕРНЫ   ProceduralMarblePits    → разрежённый Worley F1 (≈20% ячеек), узкий радиус
 ├─ КРОМКИ (каркас): |x|,|y|,|z| → Smoothstep(_Edge_Position − _Edge_Width, _Edge_Position, |c|)
 │              → попарные × → Add → Add → Saturate → × _Edge_Wear
 ├─ ТРЕЩИНЫ (каркас царапин): CrackA/B/C → Smoothstep(_Crack_Threshold, +_Crack_Band, c)
 │              → Add → × Smoothstep(_Crack_Mask, +_Crack_Band, mask) → × _Crack_Amount
 └─ wear = Saturate(кромки + трещины)

BaseColor  = Lerp(Lerp(Lerp(Lerp(_Marble_Color, _Vein_Color, прожилки × _Vein_Amount),
                                                _Cloud_Color, тон × _Cloud_Amount),
                                                _Pit_Color, каверны × _Pit_Amount),
                                                _Wear_Color, wear)
Smoothness = Lerp(Lerp(_Smoothness, _Vein_Smoothness, прожилки × _Vein_Amount), _Wear_Smoothness, wear)
NormalTS   = Normal From Height(тон×_Cloud_Bump + каверны×_Pit_Bump + трещины×_Crack_Bump
                                + прожилки×_Vein_Bump, Strength = float(1))
Metallic / Emission / Occlusion — не подключены (диэлектрик)
```

Итог графа: 379 JSON-документов, 101 нода (92 + 9 блоков мастер-стека), **132 ребра**,
41 свойство блэкборда (36 float + 5 color), `doubleDriven = 0`, висячих ссылок 0, дублей
`m_ObjectId` 0, `AbsoluteWorld` (`m_Space = 4`) — 0. Файл LF-only. Зависимость от HLSL — одна:
`ProceduralMarbleVeinNoise.hlsl` (`Fabric` и `Wood` в сгенерированном шейдере — 0 вхождений).

### Почему прожилки именно так

Приём классического мрамора: поле `fbm` покрывает объект, прожилка — **изолиния** уровня 0.5,
а не пятно. Поэтому `d = |2s − 1|` — это «расстояние до прожилки», и ширина полосы задаётся
одним числом (`_Vein_Width`), а не порогом по самому шуму. Турбулентность (крупный fbm в
смещении домена) делает линии извилистыми, а `_Mottle` сдвигает домен — облака камня «ведут»
прожилки, как в настоящем мраморе. Второй набор (`s2`, частота ×2.3, ширина ×0.45, вес 0.6)
даёт сеть и мелкие прожилки — без него рисунок читается одиночными линиями.

Сглаживание края — аналитическое: `PCMar_Band` расширяет порог на `fwidth(d)`.
**Важно (исправлено 26.10, шаг 2t):** этого недостаточно. На дистанции, где пиксель шире прожилки,
`d = |2s − 1|` в среднем ≈ 0, поэтому fwidth-полоса вырождается не в ноль, а в сплошную половину —
прожилки превращаются в шум. Поэтому слой дополнительно гасится полосой пропускания (см. раздел
«Полоса пропускания рельефа»).

### Отличие от Fabric и Wood

1. **Ни одного волокна/дерева**: `ProceduralWood*`, `ProceduralFabric*` — 0 вхождений.
2. **Пороги `Smoothstep` в естественном порядке** (наследие `Fabric`, у `Wood` они перевёрнуты):
   подтверждено в сгенерированном шейдере —
   `Unity_Smoothstep_float(_Subtract…, _EdgePosition…, _Split…R, …)` и
   `Unity_Smoothstep_float(_CrackThreshold…, _Add…, …CrackA…, …)`.
3. **Рельеф почти нулевой по смыслу**: полированный мрамор — плоский камень, прожилки в нём
   заподлицо, а не канавки. Поэтому `_Vein_Bump = −0.006` (едва заметная впадина для
   читаемости), основной рельеф дают каверны (`_Pit_Bump = −0.0015`, вниз) и трещины
   (`_Crack_Bump = −0.008`). Облачность в рельеф входит слабо (`_Cloud_Bump = +0.003`).
4. **Глянец высокий**: `_Smoothness = 0.88` — плита полированная; прожилки матовее (0.6),
   выцветшая/сколотая кромка — 0.35.

## Требования к геометрии

1. **UV и vertex colors не нужны.** Прожилки — 3D-поле, швов между гранями нет.
2. **Единичный габарит для кромочной маски**: `|x|,|y|,|z|` доходят до 1.0 на габарите 2×2×2;
   для куба 1×1×1 `_Edge_Position ≈ 0.5`.
3. **Локальная ось Y — «верх» плиты** для вытянутости облачности (`_Cloud_Stretch`), но
   критичной привязки нет — облачность изотропна, если растяжение 1.0.
4. **Масштаб объекта масштабирует рисунок** (object-space, FO-safe — осознанный компромисс).
5. `ProceduralMarbleVeins` использует `fwidth` → только fragment-стадия (в графе она и не
   подключена ни к чему, кроме фрагментных блоков).

## Ядро шума — `ProceduralMarbleVeinNoise.hlsl`

Правило **T-BRONZE01** соблюдено: 8 одно-выходных CF-функций + `_half`-обёртки, сигнатуры =
порядок слотов. Порядок аргументов подтверждён в сгенерированном шейдере:
`ProceduralMarbleVeins_float(IN.ObjectSpacePosition, …_VeinScale…, …_VeinWidth…, …_VeinWarp…, …Mottle_4…, …_VeinSeed…, …)`.

| Функция (имя CF-ноды) | Входы | Выход | Что считает |
|---|---|---|---|
| `ProceduralMarbleMottle` | `ObjectPos, MottleScale, MottleAmount, MottleSeed` | `Mottle` | `(fbm − 0.5)·2·Amount` — облачность камня в роли сдвига домена прожилок |
| `ProceduralMarbleVeins` | `ObjectPos, VeinScale, VeinWidth, VeinWarp, Mottle, VeinSeed` | `Veins` | турбулентность домена → `d = |2·fbm − 1|` → полоса с AA по `fwidth`; второй набор чаще/тоньше, `max` |
| `ProceduralMarbleCloud` | `ObjectPos, CloudScale, CloudStretch, CloudDetail, CloudSeed` | `Cloud` | два fbm (по XZ и по Y), `smoothstep(0.3, 0.7, среднее)` |
| `ProceduralMarblePits` | `ObjectPos, PitScale, PitSeed` | `Pits` | Worley F1, разрежённый (20% ячеек), `1 − smoothstep(0.06, 0.18, d)` |
| `ProceduralMarbleCrackA/B/C` | `ObjectPos, CrackScale, CrackStretch, CrackDetail, CrackSeed` (+ `CrackAngle` у C) | `Crack` | механизм царапин бронзы/дерева 1:1 (A — по Y, B — горизонтально, C — под углом) |
| `ProceduralMarbleCrackMask` | `ObjectPos, CrackMaskScale, CrackMaskSeed` | `CrackMask` | fbm-пятно, гасящее трещины на участках (вход `Seed` питается `_Crack_Mask`) |

Хеш без `sin` (Hoskins), value-noise + 3-октавный fbm.

## Свойства (41) и значения пресета `M_PC_Marble.mat`

Значения пресета = дефолты графа (свежий материал от шейдера `Marble` выглядит так же).

### Цвета

| Свойство | Значение | Смысл |
|---|---|---|
| `_Marble_Color` | 0.86 / 0.85 / 0.82 | тёплый белый камень (база) |
| `_Vein_Color` | 0.30 / 0.31 / 0.33 | серо-синяя прожилка |
| `_Cloud_Color` | 0.72 / 0.72 / 0.70 | облачность тона (сероватая тень камня) |
| `_Pit_Color` | 0.55 / 0.55 / 0.53 | каверны и поры |
| `_Wear_Color` | 0.92 / 0.91 / 0.88 | сколотая/затёртая кромка — свежий светлый камень |

### Прожилки и облачность

| Свойство | Значение | Смысл |
|---|---|---|
| `_Vein_Scale` | 4.5 | частота сети прожилок |
| `_Vein_Width` | 0.16 | ширина прожилки (0.005 — волосок, 0.6 — широкие полосы) |
| `_Vein_Warp` | 0.35 | извилистость (турбулентность домена) |
| `_Vein_Seed` | 0 | сдвиг сети |
| `_Vein_Amount` | 0.9 | контраст прожилок в альбедо и гладкости (0 — чистый белый мрамор) |
| `_Mottle_Scale` | 1.6 | частота облачности |
| `_Mottle_Amount` | 0.45 | насколько облака сдвигают прожилки |
| `_Mottle_Seed` | 0 | сдвиг |

### Тон камня и каверны

| Свойство | Значение | Смысл |
|---|---|---|
| `_Cloud_Scale` | 3.2 | частота облачности тона |
| `_Cloud_Stretch` | 1.8 | вытянутость по направлению распила |
| `_Cloud_Detail` | 2.2 | детализация fbm |
| `_Cloud_Seed` | 0 | сдвиг |
| `_Cloud_Amount` | 0.35 | сила облачности |
| `_Pit_Scale` | 90 | частота каверн |
| `_Pit_Seed` | 0 | сдвиг |
| `_Pit_Amount` | 0.15 | заметность каверн |

### Кромки и трещины

| Свойство | Значение | Смысл |
|---|---|---|
| `_Edge_Position` | 1.0 | положение кромочной маски (габарит 2×2×2) |
| `_Edge_Width` | 0.12 | ширина сколотой полосы |
| `_Edge_Wear` | 0.25 | сила скола/затёртости кромки |
| `_Crack_Scale` | 7 | частота трещин |
| `_Crack_Stretch` | 12 | вытянутость трещин |
| `_Crack_Detail` | 2 | детализация fbm |
| `_Crack_Seed` | 0 | сдвиг |
| `_Crack_Angle` | 0.785 (45°) | направление набора C |
| `_Crack_Threshold` | 0.93 | порог (выше — трещины реже) |
| `_Crack_Band` | 0.05 | мягкость порога |
| `_Crack_Mask` | 0.6 | порог гасящей маски (и её сид) |
| `_Crack_Mask_Scale` | 1.7 | частота маски |
| `_Crack_Amount` | 0.35 | итоговая сила трещин |

### Поверхность

| Свойство | Значение | Смысл |
|---|---|---|
| `_Smoothness` | 0.88 | полированная плита |
| `_Vein_Smoothness` | 0.6 | прожилка матовее камня |
| `_Wear_Smoothness` | 0.35 | сколотая кромка глухая |
| `_Vein_Bump` | −0.006 | рельеф прожилок (едва заметная впадина) |
| `_Cloud_Bump` | 0.003 | рельеф облачности |
| `_Pit_Bump` | −0.0015 | каверны вниз |
| `_Crack_Bump` | −0.008 | трещины вниз |

Коэффициенты рельефа малы потому, что `Strength = 1.0`, а градиент высоты считается в мировых
единицах (порядок величин тот же, что у пользовательской настройки `M_PC_Wood`: 0.0065 при
масштабе колец 84).

## Рельеф: обязательная проверка (см. `../README.md`, «Известная ошибка сборки»)

| Пункт | Результат |
|---|---|
| Слоты `NormalFromHeightNode` | унаследованы (`In = 0`, `Strength = 2`, `Out = 1`) |
| Вызов в сгенерированном шейдере | `Unity_NormalFromHeight_Tangent_float(_Add_a2af56b2…_Out_2_Float, float(1), …)` |
| `float(0.01)` в тексте | 0 вхождений |
| `surface.NormalTS` | `= _NormalFromHeight_7348b341…_Out_1_Vector3;` |

## FO-safe (Floating Origin)

- Весь шум — от `Position (Object)`, `m_Space = 0`; `AbsoluteWorld` — 0 вхождений.
- Вариативность между объектами — сид-свойствами (`_Vein_Seed`, `_Mottle_Seed`, `_Cloud_Seed`,
  `_Pit_Seed`, `_Crack_Seed`).
- `fwidth` считается от object-space величины — ребейз на неё не влияет.
- **Проверка F8/F9 — `NOT RUN`** (за пользователем).

## Как собран граф

In-editor скриптом (`execute_code`, файлов-инструментов в проекте не оставлено):
`Fabric.shadergraph` (только чтение) → замена guid HLSL на guid `ProceduralMarbleVeinNoise.hlsl`
(8 CF-нод) → переименование функций, ref/display-имён свойств и имён слотов CF-нод по точным
строковым совпадениям (249 вхождений) → новые дефолты 36 float + 5 color → запись тем же
разделителем `}\n\n{` → импорт. Пресет и вариант созданы через `AssetDatabase.CreateAsset`
(`SMALL_M_PC_Marble.parent = M_PC_Marble`).

## Проверено

| Пункт | Результат |
|---|---|
| Импорт графа | OK, ImportLog пуст |
| `ShaderUtil.ShaderHasError` / `GetShaderMessages` | `false` / 0 (после offscreen-рендера GPU) |
| `shader.isSupported` / проходов | `true` / 10 |
| Свойства шейдера | 46 = 41 своих + `_QueueOffset`, `_QueueControl` + 3 lightmap |
| Цвета в шейдере | `_Marble_Color` 0.86/0.85/0.82, `_Vein_Color` 0.30/0.31/0.33 — совпадают с пресетом |
| Зависимости графа | `ProceduralMarbleVeinNoise.hlsl` (+ служебные ShaderGraph/URP) |
| CF-функции в сгенерированном шейдере | по 6 вызовов каждой из 8 функций, `Fabric`/`Wood` — 0 |
| Пресеты | `M_PC_Marble`: shader `Marble`, queue 2000; `SMALL_M_PC_Marble`: parent `M_PC_Marble` |

## Не проверено (`NOT RUN`)

- **Визуал**: плотность и толщина прожилок на рабочей дистанции, не «рябит» ли второй набор,
  заметность облачности и каверн, читаемость полировки, сила рельефа.
- **Play Mode / F8 / F9**.
- **Near/Far**: Far-вариант — пресетом (убрать второй набор через `_Vein_Width`, `*_Bump = 0`),
  не проверено.
- **Демо-объект в сцене** не создавался; сцены не менялись.

## Полоса пропускания рельефа · 26.10.2026 (шаг 2t логики города)

Причина шума `M_PC_Marble` при движении камеры — не позиция и не DepthNormals, а **отсутствие полосы
пропускания у слоёв рельефа** (каверны, мелкая сеть прожилок, трещины мельче пикселя). Сделано в
`ProceduralMarbleVeinNoise.hlsl` (сигнатуры CF и порядок слотов не менялись):

* новые `PCMar_Footprint` / `PCMar_Fade` и `PCMAR_BAND_LO/HI` = **0.22/0.55** периода на пиксель
  (значения `PCCobble_Band` заказчика — эталон; имя `PCMar_Band` занято, поэтому `Fade`);
* затухание октав внутри `PCMar_Fbm3` — октава, чей период мельче пикселя, уходит в своё среднее (0.5);
* `PCMar_Ridge` — скруглённый гребень `sqrt(x² + k)`, k = 0.0025: без этого на гребне трещины
  производная меняет знак в пределах пикселя;
* прожилки (`main` и `fine`) умножаются на `Fade` от своих координат fbm (с warp и ×2.3) — не от исходного `p`;
* каверны → × `Fade`; облачность и маска трещин → `lerp(0.5, …, Fade)`; трещины A/B/C → `lerp(0.5, …, Fade)`
  (0.5 ниже `_Crack_Threshold` 0.93 — трещина на дистанции именно исчезает, а не превращается в шум).

Пиксель сцены (FOV 60°, 842 px по высоте) ≈ **1.37 мм × дистанция в метрах**:

| Слой | Пресет | Период | Рельеф начинает гаснуть с |
|---|---|---|---|
| Каверны `_Pit_Scale` | 94.9 /м | 10.5 мм | 1.7 м (целиком с 4.2 м) |
| Мелкая сеть прожилок | `_Vein_Scale` 30.83 × 2.3 ≈ 71 /м | 14 мм | 2.4 м |
| Трещины | `_Crack_Scale` 14.38 × `_Crack_Stretch` 12 ≈ 173 /м | 5.8 мм | 0.9 м |

Если рельеф нужен с 3–5 м, увеличивать **период**, а не амплитуду: `_Pit_Scale` и `_Crack_Scale` ≤ ≈ 160 / D,
а `*_Bump` поднимать пропорционально (высота = `*_Bump × 0.01` м, видимость бампа — уклон ≈ высота /
размер детали). Пресет агентом не менялся.

**Проверено агентом:** затронуты `M_PC_Marble` и `SMALL_M_PC_Marble` (больше `Marble.shadergraph` никто
не использует); принудительный реимпорт графа и offscreen-рендер — `isSupported = true`,
`ShaderHasError = false`, `GetShaderMessages` ошибок = 0, в сгенерированном шейдере
`TransformWorldToObject` = 0 и `PC_PositionOS` = 134. **Визуал, Play Mode, F8/F9 — `NOT RUN`.**
