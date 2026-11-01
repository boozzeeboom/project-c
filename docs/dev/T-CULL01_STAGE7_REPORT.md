# T-CULL01 / Stage 7 (H7) — отчёт: HLOD/L1 кварталов + L2-импостор города

**Дата:** 2026-11-01 · **База:** `95890c51` (HEAD на старте — `475442cf`, docs) · **Unity:** 6000.5.2f1
**Сцена:** `WorldScene_0_0` (не сохранялась, `sceneDirty` False→False во всех bake и прогонах) · **Push:** нет
**Scope:** L1 HLOD (3–12 км) + L2 импостор (12+ км). PVS не делался (см. §8). Корабли / B2 / Stage 6 не трогались.

**Итог: численные гейты PASS, визуальная приёмка — за пользователем.**
P1 (L0) **2 341** DC при HLOD on = off = реальная игра; город на L1 **46–48 DC** (гейт ≤ 500), на L2
**2 DC** (гейт ≤ 10); F8 PASS / F9 `PASS_rollback_restore`; rebake побайтно детерминирован; 0 errors /
0 exceptions / 0 shader errors. Попутно найдены и исправлены/задокументированы две ошибки старых
измерений (§2): бенч мерил при открытом **новом** главном меню, а GRD игнорирует 150 м culling дверей.

---

## 1. Что сделано

### 1.1. Bake в редакторе — `Assets/_Project/Editor/Culling/PrimumHlodBaker.cs` (новый)

Меню **Project C → Culling → Stage 7 HLOD → Bake Pilot Block / Bake All Blocks + L2 / Rebake Determinism Check**.

**L1 — один меш на блок** (блок = ребёнок `PRIMUM` = чанк `VisibilityDirector`, 23 блока):
1. Рендереры по правилам директора: `MeshRenderer`, активен, не слой 10, не под `DoorController`.
2. Копия каждого сабмеша через `CombineMeshes` без матриц (исходные меши не читаемы), вершины → пространство
   `WorldRoot_0_0`, зеркальные трансформы — с переворотом обхода.
3. Цвет главного свойства процедурного графа (`_Wood_Color`, `_Bronze_Color`, `_Stone_Color`…) → цвет вершины
   (sRGB); `_Metallic` / `_Smoothness` → UV0; класс эмиссии → альфа вершины (окно горит/не горит ночью —
   55 % по хэшу центра окна; лампа/фонарь — всегда).
4. **Мелочь < 0.25 м:** связные компоненты (сварка позиций 1 мм), компонента < 0.25 м удаляется (окна и
   лампы не удаляются).
5. **Интерьеры и скрытое:** внешняя видимость лучами PhysX из 41 направления (5 высот × 8 азимутов + зенит),
   сетка 0.75 м, `MeshCollider` без чистки/сварки в preview-сцене (`triangleIndex` = исходный, sanity-проверка
   попаданий **0 расхождений** из 7 648). Для ламп второй проход, где стекло не заслоняет (в L0 лампы видны
   сквозь прозрачное стекло кожухов).
6. Один сабмеш, pivot = центр AABB, вершина 24 байта (pos f32×3, normal SNorm8×4, color UNorm8×4, PBR UV0
   UNorm8×4), индексы UInt16 (макс. 42 464 вершины на блок; UInt32 включается автоматически > 65 535).

**L2 — импостор:** ортокамера по L1-мешам, **8 азимутов × 4 высоты (0/8/20/45°)**, кадр 512×256, 2×
суперсэмплинг, атлас 4096×1024: альбедо+покрытие и ночные огни (+ нормаль.y для освещения), мипы с
сохранением покрытия alpha-test, DXT5. Bake — `CommandBuffer` в RT, ничего в сцене не создаётся.

**Ассеты (git):** `Assets/_Project/Generated/HLOD/<блок>_L1.asset` ×23, `PRIMUM_L2_Impostor.asset`,
`PRIMUM_L2_Albedo.asset`, `PRIMUM_L2_Night.asset`, `PRIMUM_HLOD_L1.mat`, `PRIMUM_HLOD_L2.mat`;
манифест `Assets/_Project/Resources/HLOD/PRIMUM_HLOD.asset`. Всего ≈ 51 МБ (текстовый YAML).
`.meta` этих ассетов, шейдеров и `PrimumHlodManifest.cs` добавлены в git принудительно (как у
`Prefabs/FloatingOrigin`): ссылки манифеста и материалов идут по GUID.

### 1.2. Шейдеры — `Assets/_Project/Shaders/HLOD/`

- `PrimumHlod.shader` (`ProjectC/PrimumHlod`): `UniversalFragmentPBR` с metallic/smoothness исходного
  материала (то же освещение, что у L0 Lit-графов), туман URP, окна — эмиссия × `_PrimumHlodNight`,
  лампы — всегда; лампы сдвигаются на 1.5 м к камере (видны «сквозь» стекло, как в L0). Проходы
  ForwardLit / DepthOnly / DepthNormals (DistantFocus и SSAO видят L1 в depth) + 2 bake-прохода.
  Теней нет (L1 дальше `shadowDistance` 400 м).
- `PrimumImpostor.shader` (`ProjectC/PrimumImpostor`): 4 вершины, разворот к камере и выбор кадра атласа
  в вершинном шейдере по направлению «центр города → камера»; alpha-test, PBR по средним metallic/smoothness
  города, ночные огни поверх тумана (`_LightsFogKeep`). Состояния нет — FO-класс 🟢.

### 1.3. Переключение — `Assets/_Project/Scripts/World/VisibilityDirector.cs` (+ `PrimumHlodManifest.cs`)

- Директор при сборке читает манифест (`Resources/HLOD/PRIMUM_HLOD`) и ставит запечённые меши под
  **runtime-корень `WorldRoot_0_0/PRIMUM_HLOD`** (`HideFlags.DontSave`, не static) — ребаз переносит его
  вместе с миром. Генерации в рантайме нет; в сцену/префабы ничего не пишется. Страховка: при возврате в
  Edit Mode корень удаляется (`PrimumHlodEditModeCleanup`).
- Уровень блока — по ближайшей камере union до AABB блока: **L0 < 3 000 м < L1**; город целиком **L2 >
  12 000 м** до AABB города; обратный переход с гистерезисом 10 %. Хранятся только скаляры.
- **Единственный владелец — директор, единственное свойство — `forceRenderingOff`** (как в Stage 6):
  L0-участник гасится, если его блок на L1/L2; L1-рендерер виден только на L1; импостор — только на L2;
  **двери блока** (`PRIMUM_DOORS/<блок>`, двери/портал ADMIN) гасятся вместе с блоком на L1/L2 (§2.2).
  `DoorController`, коллайдеры, `shadowCastingMode`, `layerCullDistances`, `Renderer.enabled` не трогаются.
- `_PrimumHlodNight` (0..1) — глобальный скаляр по солнцу `DayNightController` (выключено/ниже горизонта = ночь).
- A/B: `VisibilityDirector.HlodEnabled`. Выключение директора возвращает всё к L0 (Stage 6 без изменений).

### 1.4. Бенч — `Assets/_Project/Editor/Culling/FinalValidationBench.cs` (режим Stage 7)

Меню **Project C → Culling → Stage 7 HLOD → Run Bench / Abort Bench**. Методика Stage 4c/6 (Host, Midday,
far 30 000 (120 000 для 40/80 км), fov 60, прогрев 1 500 кадров, медиана 3 `UnityStats`); точки P1 и
камера на 0.5/3/5/10/20/40/80 км от центра города; для каждой — `hlod_on` / `hlod_off` / `city_off`
(атрибуция города = DC − DC(city_off), runtime `renderer.enabled`); ночь; диагностические кадры без
`RenderSettings.fog`; после F8/F9 — повтор P1/10/20 км.

---

## 2. Найденные ошибки измерений (важно для всех прошлых этапов)

### 2.1. Бенч мерил при открытом новом главном меню

`TryStartHost` вызывал legacy `MainMenuWindow.OnHostClicked()`: игра стартовала, но **новое меню
`ProjectCMainMenuUiHost` оставалось на экране** (его `_awaitingStart` не взводился), его `AtmosphereCamera`
(depth 100) рисовала поверх игры. Гейт Stage 6 проверял только legacy-окно и писал
`mainMenuVisibleAtWarmupEnd=False`. Скриншоты прогона 1 это подтверждают (`run1_menu_shots/`).

**Исправлено:** старт через `ProjectCMainMenuUiHost.OnMenuAction("host")` — тот же путь, что кнопка
«ОДИНОЧНАЯ ИГРА»; гейт требует `pcmmMenuShown=False`, `pcmmAtmosphereCamera=False`, локального игрока, иначе
прогон останавливается (`GATE_FAIL_MENU`). В финальном прогоне: `rig=ThirdPersonCamera_0; localPlayer=True;
liveSession=True; pcmmMenuShown=False; pcmmAtmosphereCamera=False; cursorLock=Locked`.

**Следствие:** честный P1 в игре — **2 341**, а не 2 362–2 378. Доказательство — один и тот же код, разница
только в старте: прогон 1 (меню открыто) P1 = 2 378 = Stage 6, прогон 2 (меню скрыто) P1 = 2 341; разница
37 DC — камера и UI нового меню. Сравнение L0 «до/после» в этом отчёте — внутри одного прогона (HLOD on/off).

### 2.2. GPU Resident Drawer игнорирует `layerCullDistances`

`InstanceCuller` GRD проверяет только `cullingLayerMask`
(`Library/PackageCache/com.unity.render-pipelines.core@9fe9377312a8/Runtime/GPUDriven/Culling/InstanceCuller.cs:359–363`).
Поэтому с включения GRD (Stage 4) 150 м culling дверей из `DoorVisibilityBudget` для GRD-дверей **не
действует**: ≈ 25 тыс. инстансов / ≈ 137 DC / ≈ 1 млн тр. дверей рисуются на любой дистанции (видно по
`hybridInstances` ≈ 27 тыс. на 5–80 км при HLOD без дверей и ≈ 1.5 тыс. при `city_off`).

**В scope H7 исправлено только для L1/L2:** двери блока гасятся вместе с блоком (5 223 дверных рендерера,
`hlodDoorsHidden=5223/5223` на 5+ км). На L0 (< 3 км) двери по-прежнему рисуются дальше 150 м —
**решение за пользователем** (см. §9).

---

## 3. Bake: L1 по блокам (финальный bake, `U_stage7_bake_2026-11-01.csv`)

| | Значение |
|---|---:|
| блоков | 23 / 23 |
| исходных треугольников (рендереры L0 по правилам директора) | **5 407 574** |
| удалено мелочи < 0.25 м | 888 284 |
| удалено скрытых (интерьеры) | 4 252 496 |
| **L1 итого** | **266 794 тр. (−95.1 %)**, 571 905 вершин |
| время bake 23 блоков + L2 | ≈ 28 с |
| sanity `triangleIndex` | 0 расхождений из 7 648 проверок |

Самые тяжёлые блоки: ADMIN 631 694 → 10 104 (купол сохранён — `U_stage7_L1_admin_preview.png`),
S6_NQ 504 496 → 12 577, S5_NE_P2 405 132 → 9 905, S7_SE_P1 382 472 → 18 582. BASE (18 466 тр.) —
без фильтра видимости (< 20 тыс. тр.).

**Контроль потерь (каждый блок, полный блок vs L1, 16 видов, 0.26–0.74 м/px — в 4–11 раз мельче пикселя
на 3 км ≈ 2.9 м/px):** расхождение покрытия **0–0.53 %**, цвета 0–5.0 % (пилот S9_TRD-S2: 0.105 % / 2.5 %).

**L2:** центр (39 593, 2 510, 39 834) в пространстве `WorldRoot_0_0`, габарит 1 466 × 73 × 1 059 м,
halfW 919 м, halfH по рядам 41 / 165 / 355 / 692 м; атлас — `U_stage7_L2_atlas_preview.png` (видны башня
со шпилем админкорпуса и террасы базы).

---

## 4. Детерминизм

1. **Bake All → Rebake Determinism Check:** `DETERMINISTIC; mismatches=0; bakeHash=5253f70a…` (23 блока + L2,
   хэши содержимого совпали).
2. **Повторный Bake All:** SHA1 всех 29 файлов (23 меша, импостор, 2 текстуры, 2 материала, манифест) —
   **`FILES_IDENTICAL 29`** (побайтно, GUID сохранены).
Kill-условие «rebake недетерминирован» не сработало.

---

## 5. Замер (финальный прогон 4, `U_stage7_2026-11-01.csv` / `_raw.txt`, 47/47 кейсов)

Гейт «в игре»: `rig=ThirdPersonCamera_0; localPlayer=True; liveSession=True; pcmmMenuShown=False;
pcmmAtmosphereCamera=False`. GRD=1, occlusion in cameras=1, shadowDistance 400.

### 5.1. L0 (P1 — площадь)

| Кейс | DC | SetPass | tris |
|---|---:|---:|---:|
| P1_hlod_on | **2 341** | 174 | 12.33 M |
| P1_hlod_on_repeat | 2 341 | 174 | 12.33 M |
| P1_hlod_off | 2 341 | 174 | 12.33 M |
| P1_real_hlod_on (корабли on) | 2 341 | 174 | 12.33 M |
| P1_hlod_on_postFO (после F8/F9) | 2 341 | 174 | 12.33 M |

L0 не ухудшен: HLOD on = off, повтор 0.0 %. (Почему не 2 362 — §2.1.)

### 5.2. По дистанции (день; атрибуция города = DC − DC(city_off))

| Дист. | Уровень | DC hlod_on | DC hlod_off (L0) | DC city_off | **город, HLOD** | город, L0 | tris hlod_on / off |
|---|---|---:|---:|---:|---:|---:|---|
| 0.5 км | L0 | 2 359 | 2 359 | 212 | 2 147 | 2 147 | 11.86 M / 11.86 M |
| 3 км | 5×L0 + 18×L1 | 532 | 1 954 | 247 | 285 | 1 707 | 2.35 M / 11.83 M |
| 5 км | L1 | 515 | 2 100 | 467 | **48** | 1 633 | 1.87 M / 12.89 M |
| 10 км | L1 | 568 | 2 122 | 522 | **46** | 1 600 | 2.31 M / 13.32 M |
| 20 км | L2 | 503 | 2 038 | 501 | **2** | 1 537 | 1.75 M / 13.19 M |
| 40 км | L2 | 513 | 1 706 | 511 | **2** | 1 195 | 1.63 M / 10.10 M |
| 80 км | L2 | 464 | 682 | 462 | **2** | 220 | 1.02 M / 2.51 M |

- **Гейт L1 ≤ 500 DC: 46–48 DC** ✅ (город целиком: 23 блока × L1 + depth/depth-normals проходы).
- **Гейт L2 ≤ 10 DC: 2 DC** ✅ (квад + depth-проход).
- Повторы: D10k 568 → 568, D20k 503 → 503 (0.0 %). После F8/F9: 564 / 499 (−4 / −4).
- Ночь (hour 0, `night=1.00`): N5k 511, N10k 564 (L0: 2 118), N20k 501 (L0: 2 036).
- 3 км — смешанный уровень по построению (дистанция до AABB блока; ближние блоки ещё L0).

### 5.3. Culling / директор

`CullScriptable` 0.74–1.06 мс в среднем за кадр (≤ 2 мс ✅). Директор: 0.02–0.10 мс/кадр в среднем;
тик в среднем 0.48 мс, максимум 3.97 мс — кадры телепорта бенча (массовое переключение 933 участников +
5 223 дверей); в полёте переключение происходит по одному блоку.

---

## 6. F8 / F9 (финальный прогон 4, реальным вводом)

- **F8:** `frameGen 3→4`, сдвиг `WorldRoot` **512.000 м = предсказанный**; двери 2 630 / рендереры слоя 10
  5 493 до и после; `hlodRoot=WorldRoot_0_0@(0,0,0)|worldDelta=0.000` до и после — HLOD/импостор едут с миром.
- **F9:** `PASS_rollback_restore` (`restore=True`, сдвиг 0.000).
- После F8/F9 кейсы P1/10/20 км дали те же DC и уровни (L0 / L1 / L2).
- 0 errors / 0 exceptions / 0 shader errors.

Прогон 3 (тот же код, кроме PBR-шейдера): F8 PASS, но **F9 `FAILED_no_transaction`** — ни одного
`[T-FO06CY]` на 2 нажатия F9, трассировка забита `no_rebase_plan` до телепорта. Это класс отказа ввода из
Stage 6 (прогон 3), не HLOD: HLOD не трогает ввод и срез; прогоны 2 и 4 — PASS.

---

## 7. Визуальная проверка (что удалось и что нет)

- **Bake-уровень (подтверждено):** полный блок vs L1 по каждому блоку — §3; купол ADMIN и силуэт
  кварталов сохранены (`U_stage7_L1_pilot_preview.png`, `U_stage7_L1_admin_preview.png`); атлас L2 —
  `U_stage7_L2_atlas_preview.png`.
- **В игре — не подтверждено:** в текущей атмосфере (дымка/вуаль/облака, DistantFocus) город с 3+ км
  **не виден ни в L0, ни в L1/L2** с использованных ракурсов; облака дрейфуют между прогонами (в прогоне 4
  камера V5k оказалась в облаке). Выключение `RenderSettings.fog` не убирает розовую дымку — это другая
  система. Поэтому «силуэт узнаётся с 20–80 км» и «огни читаются ночью» по скриншотам бенча
  **не доказаны** — визуальная приёмка за пользователем (см. §9).
- В прогоне 3 на 5 км без тумана L0 был ярче L1 (L1 освещался Lambert без отражений, а L0 — URP Lit,
  бронза metallic 0.9). Исправлено переходом L1/L2 на `UniversalFragmentPBR` с параметрами материалов;
  в прогоне 4 этот ракурс закрыло облако, поэтому повторно сравнить кадром не удалось.
- Скриншоты: `08_DATA/U_stage7_shot_*.png` (прогон 4), `run3_shots/`, `run2_shots/`, `run1_menu_shots/`
  (PNG в git не идут).

---

## 8. Ownership map (дополнение к Stage 6)

| Свойство | Владелец | Группы | Изменение H7 |
|---|---|---|---|
| `forceRenderingOff` | **VisibilityDirector** | дома города, фермы, спутники (Stage 6) | + гашение L0-участников блока на L1/L2 |
| `forceRenderingOff` | **VisibilityDirector** | **двери блока** (`PRIMUM_DOORS/<блок>`, двери/портал ADMIN) | новое: только на L1/L2 (≥ 3 км) |
| `forceRenderingOff` | **VisibilityDirector** | **L1-рендереры и импостор** (`PRIMUM_HLOD`) | новое |
| `shadowCastingMode` двери | DoorVisibilityBudget | слой 10 | не трогается |
| `layerCullDistances[10]` | DoorVisibilityBudget | камеры | не трогается (но GRD его игнорирует — §2.2) |
| `shadowCastingMode` город | CityShadowBudget | не-корабль, не слой 10 | HLOD-рендереры созданы с Off → бюджет их не собирает |

`forceRenderingOff` по-прежнему пишет только директор — двойного toggle нет. Бюджеты, URP-ассет,
ProjectSettings, пороги `DistantFocus` 800/4000, пресеты `ViewDistanceConfig` — без изменений; L1/L2 —
метры до AABB, не зависят от `lodBias` (Near 30 км / Medium 60 км / Far 120 км: L2 виден до far камеры).

**PVS:** не делался. L0 на P1 = 2 341 (> 1 тыс.), но по брифу PVS — отдельное решение после L1/L2.

---

## 9. Что осталось / решения пользователя

1. **Визуальная приёмка (обязательна):** переходы L0↔L1 на 3 км, L1↔L2 на 12 км, силуэт с 20–80 км,
   ночные огни — в ясную погоду / с высоты, где город вообще виден. При «плывущем» силуэте — менять
   ракурсы/подсветку bake (`RowElevations`, `WindowLitPercent`, `_LampIntensity`, `_WindowColor`), не геометрию.
2. **Двери на L0 под GRD (§2.2):** 150 м culling не работает с Stage 4. Варианты — отдельная задача
   (политика директора по дистанции для слоя 10 или отказ GRD для дверей); до решения не трогал.
3. **Baseline:** прошлые P1 2 362–2 394 включали камеру нового меню; новый честный baseline — **2 341**.
4. Тик директора 3.97 мс на кадре телепорта — при необходимости разнести переключение дверей на несколько
   кадров.
5. Раскатка на фермы/спутники и PVS — вне H7.

---

## 10. Артефакты

| Файл | Что |
|---|---|
| `Assets/_Project/Editor/Culling/PrimumHlodBaker.cs` | bake L1/L2, determinism check, превью, cleanup Edit Mode |
| `Assets/_Project/Scripts/World/PrimumHlodManifest.cs` | манифест (данные bake, скаляры) |
| `Assets/_Project/Scripts/World/VisibilityDirector.cs` | переключение L0/L1/L2 + двери блока |
| `Assets/_Project/Shaders/HLOD/PrimumHlod.shader`, `PrimumImpostor.shader` | шейдеры L1/L2 |
| `Assets/_Project/Generated/HLOD/*` | 23 L1-меша, импостор, атласы, материалы |
| `Assets/_Project/Resources/HLOD/PRIMUM_HLOD.asset` | манифест |
| `Assets/_Project/Editor/Culling/FinalValidationBench.cs` | режим Stage 7 + гейт нового меню |
| `08_DATA/U_stage7_2026-11-01.csv` / `_raw.txt` | финальный прогон 4 (47 кейсов + F8/F9) |
| `08_DATA/U_stage7_2026-11-01_run3.*` | прогон 3 (до PBR; F9 без транзакции) |
| `08_DATA/U_stage7_2026-11-01_run2.*` | прогон 2 (без гашения дверей: L1 182 / L2 137 DC) |
| `08_DATA/U_stage7_2026-11-01_run1_menu.*` | прогон 1 — **невалиден**, открыто новое меню (§2.1) |
| `08_DATA/U_stage7_bake_2026-11-01.csv` | все bake / verify, сравнение блоков, параметры |

## 11. Приёмка брифа

- [x] L1 HLOD: 1 меш на блок, интерьеры и мелочь убраны, окна эмиссией; L1 города **46–48 DC ≤ 500**
- [x] L2 импостор: camera-facing, 8 азимутов, силуэт + ночные огни, скаляры; **2 DC ≤ 10**
- [x] bake только в редакторе, `.asset` в git, без scene-overrides, город не static, FO-safe
- [x] интеграция с `VisibilityDirector`, бюджеты не дублируются
- [x] L0 не хуже: P1 2 341 = HLOD off (в том же прогоне)
- [x] F8/F9 PASS (прогон 4), 0 errors / 0 shader errors
- [x] rebake детерминирован (хэши и 29 файлов побайтно)
- [ ] силуэт узнаваем / ночные огни читаются **в игре** — не подтверждено бенчем (атмосфера), за пользователем
- [x] сцена не сохранена; commit `T-CULL01/H7`; push нет
