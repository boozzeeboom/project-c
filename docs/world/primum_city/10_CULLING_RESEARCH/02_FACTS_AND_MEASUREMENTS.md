# 02. Факты и замеры: что именно мы оптимизируем

**Дата:** 2026-10-30 · **Обновлено:** 2026-11-01 · **Сцена:** `WorldScene_0_0` (+ аддитивная `BootstrapScene`)
**Источники:** живая сцена (Unity MCP read-only), YAML/префабы (парсинг), исходники URP 17.5
(`Library/PackageCache`), git, проектные документы. Все противоречия сведены в §11.

> **Текущее состояние (01.11):** GRD + `CityShadowBudget` + `VisibilityDirector` + HLOD L1/L2.
> **L0 P1 = 2 229 DC**; двери >150 м = 0; L1 46 DC, L2 2 DC; F8/F9 PASS. **Stage 8 PASS.**
> Следующий шаг — финальная приёмка T-CULL01. Числа ниже — исторические.

> «Паспорт сцены»: числа, из которых считаются оценки в `03_MERGE_VS_INDEPENDENT.md`,
> `04_ARCHITECTURE_OPTIONS.md`, `05_RECOMMENDED_ARCHITECTURE.md`.
> **[runtime]** — снято с открытой сцены; **[yaml]** — из файлов; **[doc]** — из документов; **[pkg]** — из пакета.

---

## 1. Сцена и мир

| Параметр | Значение | Источник |
|---|---|---|
| Сцена фокуса | `Assets/_Project/Scenes/World/WorldScene_0_0.unity`, **62 869 056 Б**, = HEAD `5b41dd7c` | [yaml]/git |
| Build 0 | `BootstrapScene` (аддитивно; `NetworkManager`, камеры, FO-транзакция) | [doc] |
| Мир | 24 сцены `WorldScene_X_Z` (6×4), `SCENE_SIZE = 79 999 м`; **контент только в `WorldScene_0_0`**, остальные 23 — заглушки 37–46 КБ (12×~37–40 КБ/12 GO; 11×~46 КБ/14 GO), имена корней транспонированы относительно файлов | [runtime]/[doc] |
| Террейн | `WorldRoot_0_0/Terrain_0_0`: 80×80 км, `TerrainData` 1025, `drawInstanced=true`, `basemapDistance=20000`, `heightmapPixelError=200`; рендерится `Terrain`, **не MeshRenderer** | [doc] |
| Город | корень `WorldRoot_0_0/PRIMUM` — scene-инстанс `PRIMUM_CITY.prefab`; позиция `(39 460, 2 475.7, 40 000)`, rotY 180°, scale 1 | [yaml] |
| Габарит города | ~1.4 × 1.1 км (`PRIMUM` ≈ 1 466 × 73 × 1 059 м) | [doc] |
| Стриминг сцен | `ClientSceneLoader`: `preloadDistance=10000`, `unloadDistance=10000`, `maxLoadedScenes=4`, старт 3×3; загрузка `SceneManager.LoadSceneAsync(Additive)` | [doc] `chunk_system/01_CURRENT_STATE.md` |
| FO | ребейз корней сцен: F8, авто >256 м (опрос 5 с, кулдаун 35 с), F9; static-флаги запрещены; **пилот грузит одну мир-сцену** | [doc] + D |

**Следствие для culling:** визуальная нагрузка L0 сосредоточена в пятне ~1.4×1.1 км; остальной мир —
пустой террейн/дальние города. Стриминг объектов внутри сцены отсутствует; culling — отдельный слой.

---

## 2. Рантайм-инвентарь (ground truth)

MCP read-only (2026-10-30, редактор не в Play Mode):

| Показатель | Значение |
|---|---|
| GameObject'ов | **23 028** |
| Компонентов | 65 578 |
| **MeshRenderer** | **8 891** live (8 803 activeInHierarchy; 8 863 — более раннее снятие) |
| MeshFilter | 8 861 |
| Transform | 22 956 |
| Треугольников (сумма мешей) | **6 838 859** |
| Вершин | 12 898 819 |
| Коллайдеров | 18 358 (BoxCollider 18 225) |
| `DoorController` | **2 630** |
| `Light` | 2 238 |
| `Rigidbody` | 24 |
| Загруженные сцены | `WorldScene_0_0` + `BootstrapScene`; `scene.isDirty = false` |
| Камер в сцене | 1 (`BootstrapScene/MainCamera`, far 1000, в 240 км от города); геймплейная — runtime-rig |

**Декомпозиция рендереров (документы + аудит):**

| Группа | MeshRenderer | Комментарий |
|---|---|---|
| `WorldRoot_0_0/PRIMUM` | **1 207** | 761 батч × материал + 446 прочих; 1 806 slots; ~5.44 млн тр.; enabled 1 133 |
| `PRIMUM_DOORS` | **4 949** | 2 363 инстанса × ~2.09 MR; 12 189 slots |
| `Primum_farms` | 142 | батчи двух ферм + прочее |
| `Primum_farms_DOORS` | 270 | 101 инстанс |
| ADMIN-двери (`ADMIN_DOOR_PREFABS`) | 266 | 133 инстанса |
| `RuinsValleys` | 496 | 60 хамлетов, инстансинг, тени off |
| `PRIMUM_LIGHTS` | 2 238 `Light` | пул ≤96; корпуса emissive-рендереры |
| `PRIMUM_COLLIDERS` | 0 MR | 10 809 боксов на 1 382 холдерах + 2 MeshCollider |
| Прочее (пады, маркеры, пикапы, корабли) | ~850 standalone MR | не влияет на бюджет города |

> В сцене **849 standalone MeshRenderer** (вне prefab-инстансов) и 4 906 PrefabInstance. Город и двери
> живут внутри prefab-инстансов; YAML «m_Mesh overrides» = **16 704 записи**, это НЕ число рендереров.

---

## 3. Склейка города: структура и гранулярность

| Параметр | Значение | Источник |
|---|---|---|
| Объектов до join | 147 715 | [doc] `ROADMAP` §1 |
| После join (Э4) | **1 207** рендереров: **761 батч × материал + 446 прочих** | [doc] |
| Material slots | 26 566 → **1 806** (23 чанка) | [doc] |
| Слотов на рендерер | **1.5**; 356 рендереров с >1 сабмешем | [doc] |
| Геометрия | 23 FBX-чанка, 16 809 мешей в Rebased-ассетах, **5 844 674 тр.** (сумма) | [yaml] |
| Крупнейший чанк | `S9_TRD-S2`: 6 388 исходных мешей → 104 рендерера | [doc] |
| Материалы | 49 уникальных в городе (53 с фермами), 92 в сцене; все городские — Shader Graph | [yaml] |

**Два класса объектов и гранулярность:**

| Класс | Кол-во | AABB (max extent) | Комментарий |
|---|---|---|---|
| Структурные батчи `<чанк>_M_M_<материал>` / `_INT_` | ~600–1 016 | **медиана 130 м**, p90 167, p99 226, max 1 466 | один батч = целый квартал |
| «Прочие» (лампы, фурнитура, окна, вывески, декор) | **16 125** | **медиана 0.4 м** (p90 1.8, p99 11) | 19 635 slots (94 % слотов при 25 % тр.) |

**Механика:** culling оперирует `Renderer.bounds` (один AABB). Батч 130 м почти всегда во фрустуме →
рисуется целиком, включая дома за спиной; попадает во все каскады теней, чей bounds пересекает;
внутри батча нельзя отсечь дом. Это не ошибка join'а, а **выбранная гранулярность**; она лечится
пере-склейкой по блоку/дому, а не расклейкой.

### 3.1 Где физически живёт мерж (важно)

- **Prefab-ассет** `Assets/_Project/Prefabs/City/PRIMUM_CITY.prefab` — **немёрженный источник**:
  **16 468 MR / 20 694 слота / 5 834 138 тр.**, 23 чанка. **Он никогда не коммитился** (`*.prefab`
  в `.gitignore:153`; `git log --all` пуст) — в git его нет, как и FBX (`Assets/_Project/Art/*`).
- **Scene-инстанс** `WorldRoot_0_0/PRIMUM` — **1 207 MR / 1 806 слотов / 5 438 062 тр.**; мерж сделан
  scene-level prefab-instance overrides (26 детей вместо 23, оригинальные рендереры перекрыты/удалены).
- Отсюда **174 001 override-запись** в сцене: 16 704 `m_Mesh`, 20 955 material-slot,
  50 112 `m_LocalScale`, 2 307 `m_IsActive` → **34 321 170 Б = 54.6 % файла сцены**.
- `WorldScene_0_0.unity` tracked, blob на HEAD — `52e4be48` (62 869 056 Б), live `isDirty=false` →
  **мерж закоммичен и воспроизводим**. Но он **хрупкий**: reimport FBX / Apply prefab / повторный rebase
  снесут overrides; а **prefab-источник вне git**, поэтому пересобрать его из репозитория нельзя —
  только из локального диска/Lore VCS. Это главный технический риск текущей схемы.

---

## 4. Двери: главный источник draw calls

### 4.1 Устройство префабов (аудит C, `C_doors.csv`)

| Префаб | Инстансов | MR/инст | Slots/инст | Слотов всего |
|---|---|---|---|---|
| `PM_DOOR_SINGLE_PLANK_A` | 956 | 2 | 5 | 4 780 |
| `PM_DOOR_SINGLE_PANEL_B` | 812 | 2 | 5 | 4 060 |
| `PM_DOOR_SINGLE_ARCH_C` | 419 | 2 | 4 | 1 676 |
| `PM_DOOR_DOUBLE_A` | 110 | 3 | 10 | 1 100 |
| `PM_DOOR_PALACE_DOUBLE_A/C/B` | 143/77/7 | 3 | 7 | 1 589 |
| `PM_DOOR_GATE_A/B` | 36/10 | 1 | 2 | 92 |
| `PM_DOOR_SINGLE_WIDE_A` | 27 | 2 | 6 | 162 |
| `PM_ADM_ENTRANCE_PORTAL` | 1 | 8 | 12 | 12 |
| **Итого** | **2 598** | **2.11** | **5.19** | **13 471** |

- 2 598 = `PRIMUM_DOORS` 2 363 + ADMIN 133 + фермы 101 + портал 1; **ADMIN/фермы в 4 949 не входят**.
- В сцене **2 630 `DoorController`** — двери интерактивны (створки открываются), не декоративны.
- 6 уникальных материалов на все двери (`M_PC_Bronze`, `M_PC_Wood_Dark`, `M_PC_IronRust`,
  `M_PC_Concrete`, `M_PC_Glass_Green`, `M_PC_Wood`).

### 4.2 Модель draw calls (сходится с замером)

`DC ≈ Σ_видимых [slots × (1 opaque + 1 depth-prepass + K каскадов)]`, SRP Batcher вызовы не сливает.

| Конфиг | Всего DC | Двери DC | Доля | DC/слот | DC/инстанс |
|---|---|---|---|---|---|
| A: shadow 1000 / 2 каскада | 26 981 | **22 027** | 82 % | 1.81 | 9.32 |
| B: shadow 250 / 1 каскад | 21 118 | **17 766** | 84 % | 1.46 | 7.52 |

Разложение C (12 189 DC «цвет» + 5 577/9 838 «тени») — **реконструкция-подгонка**, а не измерение:
она предполагает 100 % дверей во фрустуме и отсутствие prepass. Верификация: при 2 881 рендерере
дверей в фрустуме (прокси 04 §5) и 7 095 слотах наблюдаемое `17 766/7 095 = 2.50 DC/слот`; в конфиге B
один каскад, поэтому без prepass максимум был бы 2.0 → **prepass почти доказан**, но конкретный split
(opaque/prepass/тени) — только Frame Debugger. Для синтеза используем **измеренную разницу**
17 766/22 027, а не «цвет/тени». Не-дверная часть: B 3 352 DC на ~3 204 слота `WorldRoot` (~1.05/слот).
**Главные рычаги без переделки:** shadows off дальним дверям, `layerCullDistances` для слоя дверей,
инстансинг/GRD (десятки, ≤~72 пары mesh×material), 1 материал/слот на дверь (атлас).

---

## 5. Draw calls: замеры

Камера в центре города, fov 60, far 1000, Scene View в небо; `UnityStats`; равный `frameCount`
(`04_OCCLUSION_CULLING_RESEARCH.md` §3):

| Конфиг | Двери | Draw calls | SetPass | Треугольники, млн | FPS |
|---|---|---|---|---|---|
| A: shadow 1000 / 2 | вкл | 26 981–26 996 | 232 | 15.86–16.57 | 27.8 |
| B: shadow 250 / 1 | вкл | 21 118 | 216 | 11.61 | 34.1 |
| A | выкл | 4 954 | 195 | 15.68 | 38.8 |
| B | выкл | 3 352 | 182 | 10.88 | 35.7 |

Ограничения: FPS первых ~1 500 кадров врёт; треугольники в A/B не сходятся (перемерить);
far 1000 — не игровой far (30–120 км); одна точка.

**Условие сравнения (Stage 0):** все строки таблицы сняты в **дневной фазе с включёнными тенями**.
`DayNightController.cs:434,438` в фазах `Night (Noch)` и `Twilight (Sumerki)` ставит `sunLight.shadows = None`
и выключает главный свет → URP не строит `Directional Shadow Map` и `UnityStats.shadowCasters = 0`; тот же кадр
даёт **17 020** DC (−37 %). Перемер Stage 0 с принудительным `Midday` дал 27 131 DC (+0.56 % к 26 981) —
`08_DATA/K_stage0_report.md` §2. Замер в другой фазе суток с ориентиром 26 981 несравним.

---

## 6. Настройки рендера

### 6.1 URP Asset `ProjectC_URP.asset`

> **Актуально с 2026-10-31 (T-LIGHT01):** `400 м / 4 каскада / soft on`. Всё, что ниже в §7–§8
> про `shadow 1000 / 2`, — **исторические замеры**; они несравнимы с текущими (DC, тени, baseline).

| Параметр | Значение | Комментарий |
|---|---|---|
| `m_ShadowDistance` | **400** | URP читает из ассета; было 1000 |
| `m_ShadowCascadeCount` | **4** | `m_Cascade4Split: {0.04, 0.1, 0.3}` → **0–16 / 16–40 / 40–120 / 120–400 м** |
| `m_CascadeBorder` | **0.1** | было 0.2 |
| `m_SoftShadowsSupported` | **1** | Sun.shadows = Soft теперь реально работает |
| `m_MainLightShadowmapResolution` | 2048 | |
| `m_AdditionalLightShadowsSupported` | **0** | additional-shadow проходов нет |
| `m_UseSRPBatcher` | **1** | |
| `m_SupportsLightLayers` | 0 | rendering layers для света не работают |
| `m_SupportsHDR` / `m_MSAA` | 0 / 1 | |
| `m_GPUResidentDrawerMode` | **1 = InstancedDrawing** (Stage 4) | GPU-путь включён; до 31.10 было 0 |
| `m_GPUResidentDrawerEnableOcclusionCullingInCameras` | **1** | включён, но фактический вклад ≈0 (3 029 без vs 3 031 с) |
| `m_SmallMeshScreenPercentage` | **0** | встроенный GRD small-mesh culling не включён; кандидат Stage 4b |

### 6.2 Renderer / глобальные настройки

| Параметр | Значение | Комментарий |
|---|---|---|
| `m_RenderingMode` | **2 = ForwardPlus** | `UniversalRenderer.cs`: Forward=0, Deferred=1, ForwardPlus=2, DeferredPlus=3. Не Forward |
| `m_DepthPrimingMode` | 0 | depth priming off |
| Renderer features | EdgeDetection (`Normal|Depth`), VolumetricClouds (`Depth`), DistantFocus (`Active:0` внутри) | **prepass-путь реален**; фактическое наличие в измеренном кадре — только Frame Debugger/A-B |
| `m_StaticBatching` (ProjectSettings) | **1** | но 2 076 static GO — **без MeshRenderer** (BC_* коллайдеры, Root, Terrain) → батчить нечего |
| `m_BrgStripping` (GraphicsSettings) | **2 = KeepAll** (Stage 4) | enum: KeepIfEntitiesGraphics=0, StripAll=1, **KeepAll=2**; для GPU RD был блокер, снят 31.10 |
| `m_SmallMeshScreenPercentage` (URP) | 0 | GRD small-mesh culling выключен; см. Stage 4b |
| `QualitySettings.shadowDistance` | 15–70 | URP **игнорирует** → `ViewDistanceApplier` shadow-пресет = no-op |

### 6.3 Камеры (live + код)

| Камера | far | Где | Замечание |
|---|---|---|---|
| `BootstrapScene/MainCamera` | **1000** | статичная, `(239 997, 3 000, 159 998)` | единственная в редакторе; в 240 км от города |
| `ThirdPersonCamera` | 1000 → пресет 30/60/120 км | runtime-rig (`SpringArmCamera.Awake`) | вторая камера с тегом `MainCamera` → **`Camera.main`-мина** |
| `AtmosphereCamera` | **10 м** | меню-мод (`ProjectCMainMenuAtmosphere.cs:155`) | **не мировая**; в брифе ошибочно «120 000» |
| `ShipObservationCamera` | 1 000 000 | отдельный режим наблюдения | не влияет на обычный кадр |
| `DistantFocus` (Volume+GazeAutofocus) | — | BootstrapScene | DoF, не culling |

---

## 7. Корабли (исправлено 30.10, живой MCP-аудит)

| Факт | Значение | Источник |
|---|---|---|
| Тяжёлый `Ship_havyII_base.prefab` | **973 MR / 973 slot / 250 Collider / 1 452 Transform**; в сцене 1 инстанс, AABB **325 × 42 × 74 м** | [runtime] |
| Лёгкий `Ship_Light_root_fbx_test.prefab` | **428 MR / 429 slot** / 17 collider / 476 Transform; 1 инстанс | [runtime] |
| Средний `Ship_medium_base.prefab` | **118 MR / 123 slot** / 6 collider / 145 Transform; + cargo внутри 80/85 | [runtime] |
| 20 NPC-кораблей | по **6 MR** / 6 slots (6 кубов-заглушек, до 273 м) | [yaml] |
| Сцена всего | 23 корабля = **1 639 MR = 18.4 %** рендереров сцены (8 891) при **2.0 %** треугольников | [runtime] |
| Тяжёлый FBX `HeavyII_base` | **936 Model / 935 Geometry / 37 128 тр. / 10 материалов**; медиана 18 тр./объект, 470 мешей ≤24 тр. | [yaml] |
| Что уже склеено | фактически ничего: из 8 `*_Combined.asset` используются 2, оба в тестовом `Ship_Light_root_fbx_test`; локальные 2 MR префаб-файла — это лишь `ContrailVFX`/`PilotSeat`, а 935 MR живут во **вложенном FBX-инстансе** | [runtime] |
| Хрупкость пайплайна | **238 из 250 BoxCollider и 9 `DoorController` лежат внутри FBX-инстанса** — реимпорт модели их снесёт; `CombineMeshesToCollider` ставит `convex=false` и к динамическому Rigidbody неприменим | [yaml] |
| Оценка DC (модель, не замер) | тяжёлый ~**2 919 DC** (973 slot × 3 прохода), все корабли ~**4 917 DC**; после мержа по «ячейка разрушения × материал» → ~92 slot / ~276 DC (все → ~480) | [модель I] |
| Вывод | Корабли — **второй источник DC после дверей** (18.4 % рендереров!), а не «уже склеенная мелочь». «Не клеить, а куллить» неверно: 973 независимых рендерера culling не спасёт. Нужен **edit-time мерж в коммитируемые mesh-ассеты** (`.asset` в git; prefab/FBX — Lore), с выносом коллайдеров/дверей из FBX-инстанса. | [runtime]/[модель] |

---

## 8. Floating Origin и мир (ограничения)

1. Ребейз двигает **корни одной сцены** `WorldScene_0_0` (`position += T`) + `Physics.SyncTransforms()`
   + O(N)-обходы; second world scene не поедет.
2. Первый авто-ребейз после спавна: `T ≈ −(39 424, 2 560, 39 936)`, **|T| ≈ 56.4 км**; далее
   0.26–1.5 км каждые ~35 с (пешком/корабль 40 м/с).
3. **Нет публичного события** завершения ребейза; есть no-op `TryRebuild`, статические
   `CumulativeRebaseOffset/FrameGeneration`.
4. Static-флаги запрещены; world-space baked PVS умирает. Только frame-local или локальные координаты
   ячеек/корней.
5. Стриминг: формально 24 сцены, контент — в одной; 23 заглушки; `WorldStreamingManager` в сцене
   отсутствует (мёртвый прототип).
6. Бюджет: 16.67 мс кадр, **culling ≤2 мс** (`Landscape_TechnicalDesign.md`).
7. `Camera.main` — мина (две камеры с тегом); `DetailDistanceCuller` использует её; `PrimumLightPool`
   обходит вручную.

---

## 9. Материалы, меши, шейдеры

| Параметр | Значение | Источник |
|---|---|---|
| Уникальных материалов в сцене / городе | **92 / 49** | [doc]/[yaml] |
| Слотов в сцене | 15 403 (URP/Lit 3 202, null 51) | [doc] |
| Сумма треугольников мешей | 6 838 859 | [runtime] |
| Кастомные шейдеры / материалы | 393 shader-like файла (187 `.shader`, 115 `.shadergraph`, 79 `.hlsl`); **0** с `DOTS_INSTANCING_ON`. 375 `.mat`, из них 143 в `Materials`: 81 на 15+ Shader Graph, 21 на 12 рукописных, 38 URP Lit/Unlit, 3 builtin. **Город: 49 Shader Graph (15 графов) + 1 Unlit; двери: 6 Shader Graph (5 графов)**. ProceduralSteel — 4 материала в `Steel/archive/`, **0 ссылок** в `WorldScene_0_0` | [yaml] |
| DOTS-блокер | не «8 кастомных шейдеров», а **~20 Shader Graph** (15 город + 5 двери): Shader Graph 17.5 умеет DOTS-инстансинг, но требует настройки графов; в текущих `.shadergraph` ключей instancing/DOTS нет → объём работы **[UNVERIFIED]**, не «0.5–1 день» | [yaml] |
| `m_Mesh` overrides в сцене | 16 704 записи (prefab-instance overrides, НЕ число рендереров) | [yaml] |
| Prefab-ассет `PRIMUM_CITY` | 16 468 MR (немёрженный источник) | [yaml] |

---

## 10. Бюджет и ориентиры

| Ситуация | Draw calls | Комментарий |
|---|---|---|
| Сейчас, A | 26 981–26 996 | двери ~22 000 |
| Сейчас, B | 21 118 | двери 17 766 |
| Без дверей, A/B | 4 954 / 3 352 | город + фон |
| Индустрия PC 60 fps | 1 331 (DOOM 2016) — 4 155 (GTA V) | [VERIFIED, frame studies] |
| Старый DoD «L0 ≤120» | 120 | нереалистичен; это L1/L2 |
| Предлагаемый L0 | **1 500–3 000** | после Ф0+Ф1 |
| Предлагаемый L1/L2 | ≤500 / ≤10 | HLOD/импостор |

---

## 11. Разрешённые противоречия

1. **16 704 `m_Mesh`-записей ≠ рендереры.** Это override-записи scene-инстанса PRIMUM_CITY; live —
   8 891 MR. Prefab-ассет — 16 468 MR / 20 694 слота, **не в git** (`*.prefab` в `.gitignore`);
   мерж — в scene-overrides, закоммичен в сцену.
2. **Двери: 2 363/4 949 (доки) vs 2 598/5 493/13 471 (аудит).** Второе — все инстансы (включая
   ADMIN/фермы/портал); для модели используем 5.19 slots/инстанс; вывод о 84 % не меняется.
3. **«Forward» в `01_INDUSTRY_PRACTICE` §8 → устарело:** сейчас Forward+ (`m_RenderingMode: 2`).
4. **«Static Batching off» → неточно:** `m_StaticBatching: 1`, но статик-рендереров нет.
5. **`QualitySettings.shadowDistance` → no-op** в URP 17.5 (читается ассет).
6. **`AtmosphereCamera far 120 000` → неверно:** меню-камера far 10 м, `cullingMask=1<<9`.
7. **`far 1 000 000` → устарело:** геймплейные камеры — пресеты 30/60/120 км; замер — far 1000.
8. **Треугольники A/B не сходятся** — старые замеры перепроверить; сумма live-сцены **6.845 млн**
   (6 838 859 — более раннее снятие); для решений используем DC.
9. **`WorldStreamingManager` «живой» в доке → в сцене отсутствует.** Реальный стриминг — сцены.
10. **`m_BrgStripping: 0` = `KeepIfEntitiesGraphics`, не `Strip`** (enum: KeepIfEntitiesGraphics=0,
    StripAll=1, KeepAll=2); блокер GRD — нужно `KeepAll (2)`.
11. **Дверной DC-split (цвет/тени/opaque/prepass) — не измерен.** В синтез идёт только измеренная
    разница 17 766/22 027; реконструкция «2.50 DC/слот при 2 881 рендерере» — гипотеза до Frame Debugger.

---

## 12. Что из этого следует

- **Двери — 84 % счёта**; стоимость = slots × проходы, а не число уникальных дверей.
- **Город после склейки — 1 806 slots** (~1.8k DC/проход); гранулярность 130 м — главный дефект
  для окклюзии/LOD; лечится блок/дом, не расклейкой.
- **Расклейка = 25–40 тыс. DC** (верхняя оценка модели C; verifier: диапазон, не точка); тест E4 — контрольный.
- **GPU RD** — единственный встроенный FO-safe путь; **с 31.10 включён** (`BrgStripping=2`, GRD mode=1),
  DOTS-варианты есть; остаточный потолок — **unique (mesh,material)-пары** (P1 3 031), а не рендереры.
  GPU occlusion в этом мире ≈0.
- **Low-code двери** — самый дешёвый выигрыш; **Umbra** — закрыта; **корабли** — не проблема.
