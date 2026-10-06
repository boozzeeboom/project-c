# Glass — стекло с процедурным налётом и царапинами (нодовый Shader Graph, alpha-blend)

> Статус: **PROTOTYPE v1. Импорт графа OK, `ShaderHasError = false`, `isSupported = true`,
> пресет сведён 39 float + 4 color. Визуал — NOT RUN (за пользователем).**
> Граф: `Assets/_Project/Materials/Glass/Glass.shadergraph`
> (guid `cc2c0ebeee3b9a546a7029bc1464e717`, 496 блоков, 104 ноды + 10 блоков мастер-стека,
> 338 слотов, 156 рёбер, 41 свойство блэкборда)
> → шейдер **`Glass`**, 46 свойств (41 блэкборд + 5 служебных URP: `_QueueControl`, `_QueueOffset`,
> `unity_Lightmaps`, `unity_LightmapsInd`, `unity_ShadowMasks`).
> Ядро шума: `Assets/_Project/Shaders/ProceduralGlassNoise.hlsl` (guid `9fa335e8969a36b448aadbe3222a0bca`).
> Пресет: `Assets/_Project/Materials/Glass/M_PC_Glass.mat` (guid `3c4c484158ad278489c350330996bb2e`).

## 1. Что это и зачем

Стекло, собранное «как бронза, но налёт вместо патины, плюс прозрачность» — тот же нодово-процедурный
механизм, что у `Assets/_Project/Materials/Bronze/BronzeEdgeNoise_forTEST.shadergraph`
(эталон не тронут), с сохранением **всей** топологии кромок/царапин/налёта и добавленной альфа-цепью.
Ни одной текстуры, UV не нужны, vertex colors не читаются.

Это **не** замена `IronRust/`: там ржавчина вместо патины, вертикальный bias снят, два независимых
рельефа и металл (Metallic = 0.9). Здесь ржавчины нет вообще, вертикальный bias **оставлен** как в
бронзе, `_Metallic = 0` (диэлектрик), а вместо переработки рельефа добавлен альфа-канал и переключён
тип поверхности на Transparent.

## 2. Как работает (слои)

Позиция одна и та же для всех слоёв — нода `Position` в **object-space** (`m_Space = 0`),
`AbsoluteWorld` (`m_Space = 4`) в графе отсутствует.

```
Position (Object) 131f1a6f
  ├─► тон: Position × _Edge_Noise_Scale (36972820) + _Edge_Noise_Seed (60be4e97)   a4885524
  │        → CF ProceduralGlassBase a13ed4ac(ObjectPos, _Noise_Scale 6ba84d95, _Noise_Detail 44215caf,
  │                                           _Noise_Warp 83579b36, _Noise_Seed 23328ac4)  (вых. s5)
  │        → × _Tone_Gain 78b7a62d   8f5cd69f
  │        → + _Tone_Base 2ab38ff3   58ffb7ac
  │        → × _Glass_Color 1d1a06d2 a7c05f68                                  = тон
  │
  │     кромки: Absolute 77e38134 → Split 8ce7f690 (|x|,|y|,|z|)
  │             Smoothstep d27b1ceb/104f826a/22ac34d8
  │               Edge1 = _Edge_Position 24e6b1f7 − _Edge_Width 7f06297b (Subtract d798be5a),
  │               Edge2 = _Edge_Position
  │             → попарные × (d7536a91: |x|·|y|; f1b0307f: |y|·|z|; 4f95d914: |z|·|x|)
  │             → Add 0fd46200 → Add 40a4f067 → Saturate 916d49fe
  │             → × d6b8e464 ← Smoothstep 157331f2(In = тон a13ed4ac.s5,
  │                                                  Edge1 = _Edge_Wear 5bed1d3e,
  │                                                  Edge2 = _Edge_Wear + _Scratch_Band d3a19a47)
  │
  ├─► налёт: CF ProceduralGlassMurk da0ebae1(ObjectPos, _Murk_Scale 0e8006d7, _Murk_Seed 87f7cb76) (вых. s3)
  │          M    = Smoothstep fb750d86(In = Murk, Edge1 = _Murk_Threshold 2c462b5a,
  │                                        Edge2 = dbb1568d = _Murk_Threshold + _Murk_Band b2624cb1)
  │          M2   = Smoothstep 10a4631e(In = Murk, Edge1 = dbb1568d,
  │                                        Edge2 = 13cb9e0e = dbb1568d + _Murk_Core d78f98dd)
  │          bias = Smoothstep a73bfc1f(In = pos.y из Split 3e2249f7.s2,
  │                                        Edge1 = _Murk_Height a1edd29d + _Murk_Softness 08163c0e (Add 23b3e2aa),
  │                                        Edge2 = _Murk_Height)          ← вертикальный bias бронзы, СОХРАНЁН
  │          murkCover d9e448f6 = M × bias (bdb3f271) × _Murk_Amount e21c4aea
  │          bright    419b2bbd = M2 × bias (a6649764) × _Murk_Amount
  │          murkVis   39c53baa = Saturate(4f036e21 = murkCover + bright)
  │
  └─► царапины: CF ProceduralGlassScratchA/B/C (96c4a3c9 / 8c4053dc / 03674358)
          (ObjectPos; _Scratch_Scale bd64202b, _Scratch_Stretch c2b75cd8, _Scratch_Detail 37860cf1,
           _Scratch_Seed 78bc22f4; C дополнительно повёрнута на _Scratch_Angle b3e71221)
          → Smoothstep a3fabd6c/4fe329af/8d05ed12
              (Edge1 = _Scratch_Threshold 2ebc6ecc, Edge2 = 387e3833 = +_Scratch_Band d3a19a47)
          → Add ab278e92 → Add 3cd4fd9d
          → × Smoothstep ab581bb8(In = выход ProceduralGlassScratchMask 3c3523d2.s3,
                                   Edge1 = _Scratch_Mask 0bd6cac7, Edge2 = eb0b852f = +_Scratch_Band)   47d75a86
          → × _Scratch_Amount af1ace75                                              = рельеф царапин c7cadc8d
     wear 6ff6bf81 = Saturate(8cea08be = d6b8e464 (зачистка кромок) + c7cadc8d (царапины))

Поверхности (SurfaceDescription.*):
Albedo     53fa4006 = Lerp(тон a7c05f68,        _Murk_Deep_Color eea3fad0, T = murkCover d9e448f6)
           83c6e24d = Lerp(53fa4006,            _Murk_Color c3e27968,      T = bright 419b2bbd)
           3aa3ef8f = Lerp(83c6e24d,            _Scratch_Color 639f0dc4,   T = wear 6ff6bf81) → BaseColor eba17197
Metallic   0a3c4a37 = Lerp(_Metallic 2e4e55c0, 0.05 (Vector1 cb13fecb),   T = murkVis 39c53baa)
           df450925 = Lerp(0a3c4a37,            _Metallic 2e4e55c0,        T = wear)            → Metallic a698307e
Smoothness 915ba90d = Lerp(_Smoothness a74842a7, _Murk_Smoothness 16e6d808, T = murkVis)
           4ccbbdf0 = Lerp(915ba90d,             _Scratch_Smoothness 8d907619, T = wear)       → Smoothness 0c4c0a07
Normal TS  f5799910 = Normal From Height(In = c7cadc8d, Strength = _Scratch_Bump 7060546e)    → NormalTS 6c0a90be
Alpha      a4e2feef = Lerp(_Glass_Alpha c2fb9462, _Murk_Alpha 09ad4bac,   T = murkVis)
           3cdd7304 = Lerp(a4e2feef,             _Wear_Alpha 4acc4b4a,     T = wear)            → Alpha 3c721b83
Emission (c40592c5), Occlusion (225b60ba) — не подключены, константы (0,0,0) и 1, как в бронзе
```

Смысл слоёв — как в бронзе: `тон` даёт базовый оттенок и рисунок по кромкам, `налёт` (бывшая патина)
двумя порогами разделён на **тёмную подложку** (`_Murk_Deep_Color` там, где `murkCover > 0`) и
**яркие пятна** (`_Murk_Color` там, где совпали оба порога), `царапины` зачищают цвет до
`_Scratch_Color` и одновременно задают `wear`, который перекрывает и налёт, и металличность, и
гладкость.

## 3. Альфа — что именно добавлено к бронзе

К топологии бронзы не удалено **ничего**; добавлены 3 свойства, 3 `PropertyNode`, 2 `LerpNode`,
1 блок мастер-стека и 7 рёбер (149 → 156).

| # | Ребро (out → in) | Смысл |
|---|---|---|
| 1 | `c2fb9462.s0 → a4e2feef.s0` | `_Glass_Alpha` → A первого `Lerp` |
| 2 | `09ad4bac.s0 → a4e2feef.s1` | `_Murk_Alpha` → B первого `Lerp` |
| 3 | `39c53baa.s1 → a4e2feef.s2` | `murkVis` → T первого `Lerp` |
| 4 | `a4e2feef.s3 → 3cdd7304.s0` | выход первого `Lerp` → A второго |
| 5 | `4acc4b4a.s0 → 3cdd7304.s1` | `_Wear_Alpha` → B второго `Lerp` |
| 6 | `6ff6bf81.s1 → 3cdd7304.s2` | `wear` → T второго `Lerp` |
| 7 | `3cdd7304.s3 → 3c721b83.s0` | выход второго `Lerp` → блок `SurfaceDescription.Alpha` |

Ноды: `a4e2feef` (Lerp «стекло ↔ налёт»), `3cdd7304` (Lerp «… ↔ зачистка»), блок `3c721b83`
(`SurfaceDescription.Alpha`). Property-документы новых свойств: `101960ea` (`_Glass_Alpha`),
`576ff8b7` (`_Murk_Alpha`), `c6f7e005` (`_Wear_Alpha`).

То есть прозрачность трёхступенчатая и считается **по тем же маскам**, что и цвет:

```
alpha = Lerp( _Glass_Alpha , _Murk_Alpha , T = murkVis )   →  Lerp( … , _Wear_Alpha , T = wear )
```

При пресете (0.18 / 0.62 / 0.92) чистое стекло почти прозрачно, налёт почти полупрозрачен,
зачищенный царапинами металл почти непрозрачен. Сгенерированный код подтверждает цепь:
`surface.Alpha = _Lerp_3cdd7304675b4c9a9b312284252caf90_Out_3_Float;` (имя Lerp-ноды — от id `3cdd7304`).

## 4. Цель шейдера (URP Lit, Transparent)

В копии графа изменены ровно два параметра цели; остальное — как в бронзе:

| Параметр цели | Бронза | Glass |
|---|---|---|
| `m_SurfaceType` | 0 (Opaque) | **1 (Transparent)** |
| `m_CastShadows` | true | **false** |
| `m_AlphaMode` | 0 (Alpha) | 0 (Alpha) |
| `m_ZWriteControl` | 0 (Auto) | 0 (Auto) |
| `m_RenderFace` | 2 (Both) | 2 (Both) |
| `m_AlphaClip` | false | false |
| `m_BlendModePreserveSpecular` | true | true |
| `m_AllowMaterialOverride` | false | false |
| Workflow Mode | 1 (Metallic) | 1 (Metallic) |

Следствия: стекло рендерится в прозрачном проходе, **не пишет глубину**, не отбрасывает тени,
двустороннее, материал очереди `renderQueue = 3000` (`_QueueControl = 0`).

## 5. Параметры

Дефолты блэкборда графа и значения пресета `M_PC_Glass.mat` (снято из файлов).
Материал переопределяет дефолт всегда, поэтому боевые значения — колонка «пресет».
Пометка **NEW** — свойство, добавленное альфа-цепью; остальные — из бронзы (переименованы).

| Свойство | Граф (дефолт) | Пресет | Что крутит |
|---|---|---|---|
| `_Glass_Color` | 0.50 / 0.32 / 0.16 | 0.62 / 0.72 / 0.74 | тон стекла (затемнение на кромках/плотности) |
| `_Murk_Deep_Color` | 0.23 / 0.12 / 0.06 | 0.30 / 0.33 / 0.34 | тёмная подложка налёта |
| `_Murk_Color` | 0.25 / 0.55 / 0.45 | 0.86 / 0.88 / 0.90 | яркий налёт (бывшая зелёная патина) |
| `_Scratch_Color` | 0.75 / 0.60 / 0.38 | 0.92 / 0.94 / 0.96 | зачищенное стекло в царапинах и на кромках |
| `_Glass_Alpha` **NEW** | 0.15 | 0.18 | прозрачность чистого стекла |
| `_Murk_Alpha` **NEW** | 0.6 | 0.62 | прозрачность налёта |
| `_Wear_Alpha` **NEW** | 0.95 | 0.92 | прозрачность зачищенных кромок/царапин |
| `_Metallic` | 0.9 | 0.0 | металличность вне налёта/царапин (0 = стекло-диэлектрик) |
| `_Smoothness` | 0.5 | 0.96 | гладкость чистого стекла |
| `_Murk_Smoothness` | 0.25 | 0.35 | гладкость в налёте |
| `_Scratch_Smoothness` | 0.8 | 0.55 | гладкость на царапинах |
| `_Murk_Amount` | 1.0 | 1.4 | плотность налёта (входит в **обе** ветви, см. §7) |
| `_Murk_Threshold` | 0.55 | 0.45 | порог появления налёта (меньше = больше налёта) |
| `_Murk_Band` | 0.18 | 0.35 | мягкость края налёта и полоса второго порога |
| `_Murk_Core` | 0.12 | 0.05 | ширина полосы яркого налёта (второй порог) |
| `_Murk_Height` | −0.05 | −1.62 | вертикальный bias налёта (object-space Y) |
| `_Murk_Softness` | 0.35 | 1.63 | ширина вертикального перехода налёта |
| `_Murk_Scale` | 1.6 | 6.3 | частота налёта (больше = мельче) |
| `_Murk_Seed` | 0 | 1.59 | сдвиг налёта |
| `_Scratch_Bump` | 0.25 | 0.25 | сила рельефа царапин (`Normal From Height.Strength`) |
| `_Scratch_Amount` | 1.0 | 1.0 | амплитуда высоты царапин перед `Normal From Height` |
| `_Scratch_Scale` | 9.0 | 2.48 | частота царапин |
| `_Scratch_Stretch` | 4.0 | 20.13 | растяжка царапин в линии |
| `_Scratch_Angle` | 0.785 | 0.73 | поворот третьего набора царапин, радианы |
| `_Scratch_Threshold` | 0.75 | 0.92 | порог «есть царапина» |
| `_Scratch_Band` | 0.15 | 0.20 | мягкость царапин, полосы кромок и второй порог налёта |
| `_Scratch_Detail` | 2.2 | 4.77 | детализация царапин |
| `_Scratch_Mask` | 0.45 | 0.49 | где царапины вообще появляются (и смещение маски, см. §7) |
| `_Scratch_Mask_Scale` | 2.0 | 1.99 | частота маски царапин |
| `_Scratch_Seed` | 0 | 0.3 | сдвиг царапин |
| `_Edge_Position` | 0.5 | 0.5 | где сидит маска кромок |
| `_Edge_Width` | 0.13 | 0.13 | ширина маски кромок |
| `_Edge_Wear` | 0.35 | 0.43 | сила зачистки кромок |
| `_Edge_Noise_Scale` | 1.0 | 0.17 | **частота базового тона** (см. §7) |
| `_Edge_Noise_Seed` | 0 | 0 | сдвиг базового тона |
| `_Noise_Scale` | 6.0 | 6.0 | второй множитель частоты базового тона |
| `_Noise_Detail` | 2.7 | 0.0 | шаг частоты октав базового тона |
| `_Noise_Warp` | 0.35 | 10.37 | искривление базового тона |
| `_Noise_Seed` | 0 | 1.38 | сдвиг базового тона (внутри HLSL) |
| `_Tone_Gain` | 0.3 | 0.3 | контраст базового тона |
| `_Tone_Base` | 0.85 | 0.71 | яркость базового тона |

Итого: **41 свойство блэкборда** (37 float + 4 color) → шейдер **46 свойств** (плюс 3 служебных
текстуры URP и 2 `_Queue*`), материал хранит **39 float + 4 color**, ключевых слов — **0**.

**Быстрая настройка:** прозрачность — `_Glass_Alpha` / `_Murk_Alpha` / `_Wear_Alpha`;
плотность налёта — `_Murk_Amount` + `_Murk_Threshold`; крупность налёта — `_Murk_Scale`;
яркие пятна — `_Murk_Core`; тёмная подложка — `_Murk_Deep_Color`; царапины — `_Scratch_Threshold` +
`_Scratch_Stretch`; рельеф — `_Scratch_Amount` (высота) и `_Scratch_Bump` (сила нормали);
зональность снизу — `_Murk_Height` / `_Murk_Softness`; уникализация — любой сид
(`_Murk_Seed`, `_Scratch_Seed`, `_Noise_Seed`, `_Edge_Noise_Seed`).

## 6. Floating Origin

Только object-space: в графе одна нода `Position` (`131f1a6f`) в пространстве `Object`
(`m_Space = 0`); `AbsoluteWorld` (`m_Space = 4`) в файле графа **не встречается** — проверено по
всем пяти полям `m_Space` (значения только `0` и `3`, последнее — у таргета). Сиды — это смещения,
прибавляемые к object-space координатам, то есть тоже не зависят от мирового положения объекта.
Свойства материала — не мировые координаты. Ожидание: после F8 (ребейз) и F9 (возврат) паттерн
(налёт, царапины, кромки) стоит на месте. **NOT RUN** (проверяет пользователь).

## 7. Известные ограничения и грабли

- **Нет рефракции/трансмиссии.** URP Lit Transparent не умеет преломление: «стекло» здесь — это
  отражения/спекуляр поверх подкрашенной альфа-смеси. Плотность и толщину можно передать только
  цветом (`_Glass_Color`) и альфой; искажений за стеклом не будет.
- **Прозрачное стекло не пишет глубину и не отбрасывает тени** (`ZWrite Auto` + `CastShadows = false`
  в копии цели). Значит: пост-эффект `EdgeDetection` (он строит контур по глубине+нормалям) **по
  стеклу контура не даст** — по проекту контур будет только у непрозрачной рамы/деталей. Тень от
  стекла получается только отдельным объектом.
- **`RenderFace = Both` (двустороннее) + alpha blend → сортировка.** Стекло должно быть одним
  объектом без самопересечений; вложенные/многослойные стёкла и пересечение с другими прозрачными
  объектами дадут артефакты порядка отрисовки (в Forward прозрачные объекты сортируются по
  дистанции до центра).
- **Дефолты блэкборда остались «бронзовыми».** `_Glass_Color` = 0.50/0.32/0.16 (**бронза**),
  `_Murk_Color` = 0.25/0.55/0.45 (**бирюзовая патина**), `_Murk_Deep_Color` = 0.23/0.12/0.06,
  `_Metallic` = 0.9, `_Smoothness` = 0.5, `_Murk_Height` = −0.05, `_Murk_Amount` = 1.0. Значит:
  **новый материал из шейдера `Glass` получит бронзовый непрозрачный металл, а не стекло.**
  Актуальные палитра и режим живут только в `M_PC_Glass.mat`.
- **Вертикальный bias налёта сохранён** (в `IronRust/` v2 его снимали): `_Murk_Height` −1.62 и
  `_Murk_Softness` 1.63 в пресете означают, что налёт нарастает **вниз** от object-space y ≈ −1.62
  (единицы меша, не метры — при другом масштабе/центре меша переход уедет). При дефолтах
  (−0.05 / 0.35) переход почти в нуле и выражен слабо.
- **`_Murk_Amount` входит в маску дважды** — по одному разу в `murkCover` (`d9e448f6`) и в `bright`
  (`419b2bbd`), которые затем складываются в `4f036e21` и клипуются `Saturate`. Рабочий диапазон
  суммы ≈ 0…2×`_Murk_Amount`; при 1.4 отличие «есть налёт / нет налёта» наступает раньше, чем
  подсказывает `_Murk_Threshold`. Унаследовано от бронзы.
- **Частота базового тона — это `_Edge_Noise_Scale` × `_Noise_Scale`, а не «шум кромок».** В графе
  `Position` сначала умножается на `_Edge_Noise_Scale` и складывается с `_Edge_Noise_Seed`
  (`bd440336`, `a4885524`), и только потом то, что получилось, уходит в `ProceduralGlassBase`
  вместе с `_Noise_Scale` (пресет: 0.17 × 6.0 ≈ 1.0). Имена свойств — наследие бронзы; отдельного
  «шума по кромкам» в этом графе нет.
- **Рельеф царапин управляется двумя ручками**: `_Scratch_Amount` — множитель высоты (`c7cadc8d`),
  `_Scratch_Bump` — `Strength` у `Normal From Height` (`f5799910.s2`). Обе входят в итоговую нормаль.
- **У маски царапин нет отдельного сида**: у CF-ноды `ProceduralGlassScratchMask` (`3c3523d2`) вход
  `Scale` подключён к `_Scratch_Mask_Scale`, а вход `Seed` — к **`_Scratch_Mask`**. То есть порог
  маски одновременно сдвигает её пятна (унаследовано от бронзы).
- `_Noise_Detail = 0` в пресете: шаг частоты октав упирается в минимум 1.1, три октавы идут почти на
  одной частоте — базовый тон почти без мелкой детализации. Унаследовано, не поломка.
- `Emission` и `Occlusion` в графе не подключены (константы 0,0,0 и 1) — как в бронзе. Свечения у
  стекла нет: «эмиссив» из задачи придётся делать отдельным материалом/эмиссивной панелью.
- **Формат файла `.shadergraph`**: поток JSON-документов, разделённых ровно одной пустой строкой
  (`}\n\n{`) — в этом файле **495** таких разделителей на **496** документов. Любая пустая строка
  **внутри** документа режет его на части, а разделитель `\n` вместо `\n\n` склеивает соседние
  документы — импорт падает с `ArgumentException: JSON parse error: The document root must not follow
  by other values`. Правки HLSL — уважать **T-BRONZE01**: `ProceduralGlassNoise.hlsl` содержит только
  одно-выходные функции File-режима, **сигнатуры и порядок слотов CF-нод менять нельзя** (слоты
  привязаны по номерам).
- **Масштаб меша влияет на вид.** Всё процедурное — в object-space, поэтому частота (`_Murk_Scale`,
  `_Scratch_Scale`, `_Noise_Scale` × `_Edge_Noise_Scale`) и `_Murk_Height` заданы в единицах
  локального пространства: тонкий иллюминатор и большой витраж на одном пресете выглядят
  по-разному, частоты придётся тюнить под типоразмер.

## 8. Требования к геометрии

Специальных нет: материал полностью попиксельный, из object-space координат.

- UV не нужны; развёртка меша шейдеру безразлична.
- Vertex colors **не читаются** (`VertexColor`-нод в графе нет) — договор R/G/B/A из
  `docs/Materials/README.md` этим материалом не используется.
- Фаски и плотность вершин не нужны (в отличие от `SteelRust/`).
- Нормали должны быть корректными (пересчёт, без перевёрнутых граней) — иначе bump поверх
  сломанной нормали даст грязь.
- Для альфы: один слой стекла на объект, без самопересечений; вложенные стёкла/витражи — отдельные
  объекты и, скорее всего, разные `renderQueue`-пресеты.

## 9. Проверка

**Проверено агентом 05.10.2026 (unity-mcp), по файлу графа и по загруженным ассетам:**

- структура файла: **496** JSON-документов, **495** разделителей `}\n\n{` (ровно один корневой
  объект на документ); в массиве рёбер присутствует один служебный символ `\r` перед запятой — это
  пробел JSON, на импорт не влияет, но при текстовых правках его можно снять;
- состав: 1 `GraphData` + 1 `UniversalTarget` + 1 `UniversalLitSubTarget` + **104** ноды
  (63 функциональных + 41 `PropertyNode`) + **10** `BlockNode` (мастер-стек) + **41** документ
  свойств + **338** слотов = 496 (сходится точно);
- типы нод: `AddNode` 14, `MultiplyNode` 13, `SmoothstepNode` 11, `LerpNode` 9,
  `CustomFunctionNode` 6, `SaturateNode` 3, `SplitNode` 2, `AbsoluteNode` 1, `SubtractNode` 1,
  `PositionNode` 1, `Vector1Node` 1, `NormalFromHeightNode` 1, `PropertyNode` 41, `BlockNode` 10;
- слоты по типам: `Vector1MaterialSlot` 112, `DynamicVectorMaterialSlot` 169,
  `DynamicValueMaterialSlot` 33, `Vector3MaterialSlot` 14, `Vector4MaterialSlot` 4,
  `NormalMaterialSlot` 2, `ColorRGBMaterialSlot` 2, `PositionMaterialSlot` 1, `TangentMaterialSlot` 1;
- свойства: **37** `Vector1ShaderProperty` + **4** `ColorShaderProperty`;
- рёбра: **156** (по числу `m_OutputSlot`), **312** ссылок `m_SlotId` (2 на ребро); альфа-цепь —
  все 7 рёбер из §3 на месте, включая `3cdd7304.s3 → 3c721b83.s0`;
- 6 CF-нод (`a13ed4ac`, `96c4a3c9`, `8c4053dc`, `03674358`, `3c3523d2`, `da0ebae1`) смотрят на
  `ProceduralGlassNoise.hlsl` (guid `9fa335e8…` — 6 совпадений `m_FunctionSource`); ссылок на
  бронзовый граф (`8ca70977…`) — **0**, на бронзовый HLSL (`0cbd6c03…`) — **0**; слов
  `Bronze`/`Patina` в файле — **0**;
- Floating Origin по графу: одна нода `Position`, `m_Space = 0` (Object); `m_Space = 4`
  (AbsoluteWorld) не встречается;
- сравнение с эталоном (`BronzeEdgeNoise_forTEST.shadergraph`, не тронут): документов 475 → 496,
  нод 99 → 104, `BlockNode` 9 → 10, свойств 38 → 41, слотов 326 → 338, рёбер 149 → 156;
- импорт: `ShaderUtil.ShaderHasError(shader) == false`, `shader.isSupported == true`, у шейдера
  **46** свойств; в сгенерированном коде пасса есть
  `#include_with_pragmas "Assets/_Project/Shaders/ProceduralGlassNoise.hlsl"`,
  объявления `float _Glass_Alpha`, `float _Murk_Alpha`, `float _Wear_Alpha`,
  `surface.Alpha = _Lerp_3cdd7304675b4c9a9b312284252caf90_Out_3_Float;`,
  `surface.Emission = float3(0,0,0);`, `surface.Occlusion = float(1);`;
- пресет: **39/39** float и **4/4** color читаются обратно из `M_PC_Glass.mat`; `shader = Glass`,
  `renderQueue = 3000`, ключевых слов — 0;
- Console → 0 errors после импорта графа и создания материала.

**Инцидент при записи (для истории):** первый вариант файла **не импортировался** —
`NullReferenceException` в `GraphData.RemoveEdgeNoValidate` (`GraphData.cs:1225`) через
`CleanupGraph`/`ValidateGraph` из `ShaderGraphImporter.OnImportAsset`. Причина: в 7 новых рёбрах в
поле `m_Node.m_Id` были положены ObjectId **слотов**, а нужны ObjectId **нод** (`a4e2feef`,
`3cdd7304`, property-ноды `c2fb9462`/`09ad4bac`/`4acc4b4a` и блок `3c721b83`). После подстановки
правильных id импорт чистый. Перед записью правка проверялась структурными прогонами
(`dangling = 0`, `doubleDriven = 0`, все 9 новых id зарегистрированы). Уборка временных копий
через `AssetDatabase.DeleteAsset` была недоступна (заблокировано фильтром безопасности MCP),
поэтому временные ассеты не создавались.

**NOT RUN (за пользователем):**

- визуал: разделение тёмного (`_Murk_Deep_Color`) и яркого (`_Murk_Color`) налёта, читаемость
  второго порога (`_Murk_Core`), прозрачность в трёх состояниях (`_Glass_Alpha` 0.18 /
  `_Murk_Alpha` 0.62 / `_Wear_Alpha` 0.92), рельеф царапин (`_Scratch_Amount` × `_Scratch_Bump`),
  вертикальный bias налёта при −1.62 / 1.63, поведение `Render Face = Both` и сортировка;
- F8/F9 — стабильность паттерна (FO);
- контур `EdgeDetection` по стеклу (ожидание: контура нет, см. §7);
- замеры (ALU/overdraw) — не делались.

Как смотреть (без Play Mode): наложить `Assets/_Project/Materials/Glass/M_PC_Glass.mat` на объект в
сцене. Стекло видно только там, где за ним что-то есть — ставить перед панелью/фоном. При
`_Metallic = 0` и `_Smoothness = 0.96` нужны скайбокс или отражения и направленный свет, иначе
поверхность читается как пустая (см. `SteelRust/README.md` §5 про свет и окружение).
