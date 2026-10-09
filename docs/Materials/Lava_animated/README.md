# Lava_animated — та же лава, но домены жара, потока и корки дрейфуют по времени

> **Статус: PROTOTYPE v1.** Граф — **производная от `Lava/`**: рисунок, слои и
> эмиссия переиспользованы как есть, новыми являются дрейф домена (стадии 5b/5c)
> и три свойства скорости. Импорт OK, `ShaderHasError = false`,
> `isSupported = true`, `GetShaderMessages = 0`, 372 документа / 102 ноды /
> 122 ребра / 41 свойство блэкборда (34 float + 7 color), `CR = 0` (LF-only),
> 41 параметр в пресете. **HLSL не менялся ни на байт.**
> **Визуальная приёмка и Play Mode — `NOT RUN`** (шаг пользователя).

| Что | Путь | GUID |
|---|---|---|
| Ядро шума (общее с `Lava/`, **не менялось**) | `Assets/_Project/Shaders/ProceduralLavaNoise.hlsl` | `403fdfb6b3d1ffe4fbeb9bcfb9357039` |
| Граф | `Assets/_Project/Materials/Lava_animated/Lava_animated.shadergraph` | `528c05c5d26165742b788825e1a1f64b` |
| Пресет | `Assets/_Project/Materials/Lava_animated/Lava_animated.mat` | `3d3637dc13044d44a99aea8b25846fac` |
| Сборщик графа | `Tools/LavaAnimatedBuilder.cs` | вне git |
| Патч 5b (жара/потока) | `Tools/LavaAnimatedPatch.cs` + `_lavaanim_block_{props,drift,helpers}.txt` | вне git |
| Патч 5c (корки) | `Tools/LavaCrustPatch.cs` + `_lavaanim_block_crust{props,drift}.txt` | вне git |
| Бэкап сборщика до патча 5c | `Tools/_backup_LavaAnimatedBuilder.cs.txt` | вне git |
| База `Lava/` | `Assets/_Project/Materials/Lava/Lava.shadergraph` | **не трогалась** |

---

## 1. Отличие от `Lava/`

Всё из `Lava/README.md` §1–§5 действует без изменений: те же пять CF-функций,
тот же HLSL, та же Lerp-цепочка альбедо, та же эмиссия, тот же таргет
(`Opaque`, `RenderFace = Both`, `CastShadows = true`).

Добавлено **только** одно: время сдвигает домен **трёх** слоёв вдоль локальной Y.

| Слой | Дрейфует? | Стадия | Скорость (ячейки домена / с) |
|---|---|---|---|
| Жар (`Heat`) | да | 5b | `_Heat_Speed × 0.85 / 1.5` = **0.34** |
| Поток (`Flow`) | да | 5b | `_Flow_Speed × 2.6 / 5.0` = **0.78** |
| **Корка** (`Fissures`, `PlateTone`, `Skin`) | **да** | **5c** | `_Crust_Speed × 54.74 / 2.4` = **0.09** |
| Всё остальное | нет | — | — |

Цифры посчитаны для пресетных значений. Формула видимой скорости:

```
ячейки/с = _Speed × Scale / Stretch
```

потому что смещение добавляется к object-space позиции **до** умножения на
масштаб слоя (`p = ObjectPos × Scale + Seed`, `p.y /= Stretch`).

## 2. Как сделан дрейф (HLSL не тронут)

Время подаётся **не в сид** CF-ноды, а в её `ObjectPos`:

```
Time ──×── _Speed ──×── (0,1,0) ──+ OBJP ──> CF.ObjectPos
```

Сид — скаляр: он увёл бы домен **по всем трём осям сразу**. Вектор `(0,1,0)`
смещает только локальную Y, то есть вдоль потока. Положительная скорость уводит
рисунок **вниз** по локальной Y — туда, куда течёт лава.

Геометрия при этом **не едет**: `VertexDescription.Position` по-прежнему получает
прямой `OBJP` (узел `Position`, `3cd14d24`, `m_Space = 0`), смещается только проба.

### Почему у корки один общий сдвиг, а не три

`Fissures` и `PlateTone` считаются **одним и тем же Worley** и **одним доменом**
(см. `Lava/README.md` §2): `CrustScale`/`CrustStretch`/`CrustSeed` подаются в обе
ноды из одних свойств. Если бы они дрейфовали разными сдвигами, **тон плиты
поехал бы по границам трещин** — тон перестал бы принадлежать своей плите.

Поэтому у корки **один** узел `Crust Domain Drift`, а его выход разведён на все
три корковые CF-ноды. `Skin` кормится тем же сдвигом — корка едет как одно целое,
и мелкий рельеф не расходится с рисунком.

### Почему `_Crust_Speed` на два порядка меньше `_Heat_Speed`

Отношение `Scale / Stretch` у корки радикально больше: при пресетных
`_Crust_Scale 54.74` и `_Crust_Stretch 2.4` оно равно **22.8** против **0.57** у
жара. Одинаковое число `_Speed` дало бы корке в 40 раз более быстрый дрейф.
Поэтому `_Crust_Speed = 0.004` и даёт 0.09 ячейки/с — примерно **вчетверо
медленнее жара** и **вдевятеро медленнее потока**: корка ползёт, расплав течёт.

> **Важно.** Дефолт графа у `_Crust_Scale` — `1.5`, а в пресете — `54.74`. Число
> `_Crust_Speed` осмысленно **только против пресета**. Свежий материал из шейдера
> `Lava_animated`, у которого `_Crust_Scale` остался `1.5`, при `_Crust_Speed = 0.004`
> даст 0.0025 ячейки/с — корка будет практически стоять. Это цена расхождения
> дефолта и пресета, а не дефект дрейфа.

## 3. Граф

102 ноды. Пять CF-нод:

| CF-нода | id | Слоты |
|---|---|---|
| Fissures | `1de3ad5d` | `0 ObjectPos, 1 CrustScale, 2 CrustStretch, 3 CrustWidth, 4 CrustSeed, 5 Fissures(out)` |
| Plate Tone | `aa2edb46` | `0 ObjectPos, 1 CrustScale, 2 CrustStretch, 3 CrustSeed, 4 PlateTone(out)` |
| Heat | `1c060505` | `0 ObjectPos, 1 HeatScale, 2 HeatStretch, 3 HeatSeed, 4 Heat(out)` |
| Flow | `4cdd2e38` | `0 ObjectPos, 1 FlowScale, 2 FlowStretch, 3 FlowWidth, 4 FlowSeed, 5 Flow(out)` |
| Skin | `52aa4204` | `0 ObjectPos, 1 SkinScale, 2 SkinDetail, 3 SkinSeed, 4 Skin(out)` |

Узлы анимации (проверено чтением **импортированного** графа):

| Узел | id | Тип |
|---|---|---|
| Time | `1b7ebf4d` | `TimeNode` (выход `0` — секунды) |
| Vector 3 (0,1,0) | `98a3db7e` | `Vector3Node` |
| Time x Heat Speed | `700c60f5` | `MultiplyNode` |
| Heat Drift Y | `038f17b2` | `MultiplyNode` |
| Heat Domain Drift | `e77dcd7d` | `AddNode` |
| Time x Flow Speed | `06cb753f` | `MultiplyNode` |
| Flow Drift Y | `ff3b32aa` | `MultiplyNode` |
| Flow Domain Drift | `91b71fa9` | `AddNode` |
| **Time x Crust Speed** | `e4046ed2` | `MultiplyNode` |
| **Crust Drift Y** | `64b91992` | `MultiplyNode` |
| **Crust Domain Drift** | `b487eaa3` | `AddNode` |
| Crust Speed (свойство) | `a21e0ef1` | `PropertyNode` |

### Цепочки дрейфа (фактические рёбра импортированного графа)

```
Жар:   1b7ebf4d.0 -> 700c60f5.0     (Time)
       a?_Heat_Speed -> 700c60f5.1
       700c60f5.2 -> 038f17b2.0     (Time x Heat Speed -> Heat Drift Y)
       98a3db7e.0 -> 038f17b2.1     (dir)
       038f17b2.2 -> e77dcd7d.1     (Heat Drift Y -> Heat Domain Drift)
       3cd14d24.0 -> e77dcd7d.0     (OBJP)
       e77dcd7d.2 -> 1c060505.0     -> Heat CF.ObjectPos

Поток: аналогично, отдельными узлами, -> 4cdd2e38.0 (Flow CF.ObjectPos)

Корка: 1b7ebf4d.0 -> e4046ed2.0     (Time)
       a21e0ef1.0 -> e4046ed2.1     (_Crust_Speed)
       e4046ed2.2 -> 64b91992.0     (Time x Crust Speed -> Crust Drift Y)
       98a3db7e.0 -> 64b91992.1     (dir)
       64b91992.2 -> b487eaa3.1     (Crust Drift Y -> Crust Domain Drift)
       3cd14d24.0 -> b487eaa3.0     (OBJP)
       b487eaa3.2 -> 1de3ad5d.0     (Fissures)
       b487eaa3.2 -> aa2edb46.0     (Plate Tone)
       b487eaa3.2 -> 52aa4204.0     (Skin)
```

`ObjectPos` (слот 0) каждой из пяти CF-нод получает **ровно одно** входящее ребро.
Прямых рёбер `OBJP -> CF.ObjectPos` в графе **0**: все пять идут через дрейф.

## 4. Параметры (41)

33 общих с `Lava/` (см. `Lava/README.md` §6) + три новых:

| Свойство | Дефолт графа | Пресет | Назначение |
|---|---|---|---|
| `_Heat_Speed` | 0.60 | 0.60 | скорость увода домена жара вдоль локальной Y |
| `_Flow_Speed` | 1.50 | 1.50 | скорость увода домена потока |
| **`_Crust_Speed`** | **0.004** | **0.004** | скорость увода домена **корки** (Fissures + PlateTone + Skin) |

**Ноль в любом из трёх = слой стоит.** Это не «дефект», а рабочий переключатель:
`_Crust_Speed = 0` возвращает поведение базовой `Lava/`.

Пресет `Lava_animated.mat` **не равен** `M_PC_Lava.mat`: в нём живёт тюнинг
пользователя, которого в базе нет —

| Свойство | `M_PC_Lava.mat` | `Lava_animated.mat` |
|---|---|---|
| `_Crust_Scale` | 49.5 | **54.74** |
| `_Crust_Seed` | 5.26 | **5.54** |
| `_Smoothness` | 0.34 | **0.50** |
| `_Plate_Vary` | 0.79 | **0.55** (дефолт графа) |
| `_Glow_Amount` | 2.04 | **1.0** (дефолт графа) |
| `_Molten_Threshold` | 0.57 | **0.62** (дефолт графа) |
| `_Flow_*`, `_Heat_*`, `_Skin_*`, `_Plate_Bump`, `_Fissure_Bump`, `_Bump_Strength` | совпадают | совпадают |

Рендер-очередь пресета — `2000` (`m_CustomRenderQueue: -1`, то есть берётся
дефолт шейдера). Ключевых слов — 0, как и у базовой `Lava/` (эмиссия включается
самим фактом подключения блока `Emission`, см. `Lava/README.md` §5).

## 5. Floating Origin

Правило №1 соблюдено тем же способом, что в `Lava/`, плюс проверка **самого
дрейфа**:

- смещение берётся от `TimeNode` (секунды с запуска) — **это не позиция и не
  мировая координата**, ребейз его не трогает;
- единственный вход координат — `Position` (`3cd14d24`, `m_Space = 0`,
  object space);
- `AbsoluteWorld` — 0 вхождений;
- смещение добавляется к **object-space** позиции, поэтому после ребейза рисунок
  остаётся на объекте, а не «уплывает» относительно него.

Цена та же, что у всех object-space материалов: масштаб объекта масштабирует и
рисунок, и **видимую скорость** дрейфа (она в ячейках домена, а домен привязан к
локальным координатам).

## 6. Требования и грабли

**Требования** — как у `Lava/` (локальная Y вверх, один объект = один поток или
валун, UV не нужны, плоские нормали не обязательны, материал двусторонний и
непрозрачный).

**Грабли, специфичные для анимации:**

- **рисунок «плывёт» целиком, как декорация за стеклом** — значит `_Crust_Speed`
  слишком велик: корка перестаёт читаться твёрдой плитой. Начните с 0.002–0.004;
- **корка стоит, а расплав течёт** — так и задумано; если нужно наоборот,
  уменьшайте `_Heat_Speed`/`_Flow_Speed`, а не поднимайте `_Crust_Speed`;
- **свечение трещин «дышит» рывками** — `_Heat_Speed × _Heat_Scale / _Heat_Stretch`
  велико, и `HeatMask` перескакивает между состояниями быстрее, чем читается
  рисунок. Снижайте `_Heat_Speed`;
- **течёт не туда** — направление задано вектором `(0,1,0)`; отрицательная скорость
  уводит рисунок **вверх**, то есть против направления потока;
- **после ребейза F8/F9 ничего не поплыло** — это ожидаемо и правильно;
- **`Lava_animated.mat` показывает URP/Lit на диске**, хотя в редакторе он уже
  настроен — см. §7 про незакоммиченные изменения.

## 7. Инцидент: незакоммиченный тюнинг пресета

На момент правки `Lava_animated.mat` был **изменён в памяти и не сохранён**
(`EditorUtility.GetDirtyCount = 625`): в редакторе он уже стоял на шейдере
`Lava_animated` с `queue = 2000` и с тюнингом (`_Crust_Scale 54.74`,
`_Crust_Seed 5.54`, `_Smoothness 0.5`, `_Heat_Speed 0.6`, `_Flow_Speed 1.5`),
**а на диске всё ещё лежал URP/Lit** (`m_Shader` → `.../universal/Shaders/Lit.shader`,
`m_CustomRenderQueue: -1`, только 30 lit-свойств).

Пересборка графа вызывает реимпорт шейдера, а он сбрасывает несохранённые
правки материалов — то есть **тюнинг был бы потерян**. Поэтому материал был
сначала сохранён на диск (`AssetDatabase.SaveAssetIfDirty`), и только потом
пересобирался граф.

**На будущее:** перед любой пересборкой `.shadergraph` сохраняйте материалы,
которые на него ссылаются; `dirty != 0` — это сигнал, а не мелочь.

## 8. Проверка

**Подтверждено (чтением, на импортированном состоянии):**

- граф: 372 документа / **122 ребра** (было 116 — ровно +6: 9 новых связей минус
  3 убранных прямых `OBJP → корковые CF`), 102 ноды, 41 свойство
  блэкборда (34 float + 7 color), CR = 0 (LF-only, без CRLF);
- шейдер `Lava_animated`: `hasError = False`, `msgs = 0`, `supported = True`;
  `Logs/Editor.log`: `Shader error in 'Lava_animated'` — **0**;
- `_Crust_Speed`: свойство `Vector1ShaderProperty` «Crust Speed» (документ
  `9eef52f4`) + `PropertyNode` `a21e0ef1`, реально участвует в цепочке;
- дрейф корки: `Crust Domain Drift` (`b487eaa3`) имеет **ровно 3** исходящих
  ребра — в `Fissures.0`, `Plate Tone.0`, `Skin.0`;
- **дублей входов нет**: у каждой из трёх корковых CF-нод слот `ObjectPos (0)`
  получает **ровно одно** ребро, все три — от `b487eaa3.2`;
- прямых рёбер `OBJP → CF.ObjectPos` — **0**; `OBJP` (`3cd14d24`) идёт в
  `Heat Domain Drift`, `Flow Domain Drift` и `Crust Domain Drift`;
- жар и поток **не сломаны**: `Heat Domain Drift → Heat CF.ObjectPos`,
  `Flow Domain Drift → Flow CF.ObjectPos`;
- материал: `shader = Lava_animated`, `queue = 2000`, 45 свойств у шейдера,
  `_Crust_Speed` прочитан и записан (`0.004`), тюнинг пользователя сохранён;
- **база `Lava/` не тронута**: `Lava.shadergraph` — 316 документов / 104 ребра /
  `TimeNode = 0` / `_Crust_Speed = 0`, то есть ровно как было;
- HLSL `ProceduralLavaNoise.hlsl` — **не менялся** (гуид `403fdfb6…` тот же,
  сигнатуры и порядок слотов CF сохранены, T-BRONZE01 соблюдён).

**`NOT RUN` — делает пользователь:**

- визуальная приёмка: читается ли дрейф как течение лавы, а не как «плывущая
  текстура»; видно ли, что корка ползёт **медленнее** расплава;
- подбор `_Crust_Speed` / `_Heat_Speed` / `_Flow_Speed` под сцену и под
  фактический `_Crust_Scale` (см. предупреждение в §2);
- поведение на сглаженных нормалях и на цилиндрических объектах;
- **Play Mode, F8/F9 (ребейз Floating Origin)**, 0 errors — дрейф на `TimeNode`
  от ребейза не зависит, но это надо увидеть;
- совместимость с `EdgeDetection`;
- демо-объект в сцене **не выставлялся**: ни `BootstrapScene.unity`, ни
  `WorldScene_0_0.unity` не менялись.
