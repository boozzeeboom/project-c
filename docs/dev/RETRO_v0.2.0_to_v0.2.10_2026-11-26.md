# Ретроспектива: v0.2.0 → v0.2.10

**Период:** 06.11.2026 → 26.11.2026 (21 сутки)
**Начало:** `bbeb900b` (version changed 0.2.0)
**Конец:** `8c19df5c` (HEAD, version changes 0.2.10 — global optimisation)
**Коммитов в диапазоне:** **185**
**Файлов изменено:** **527** (+303 829 / −22 289 строк; из них в `Assets/` — 184 файла, +91 887 / −18 403)
**Маркеры версии:** `0.2.0` → `0.2.05` (`7f1437bc`, 14.11, текст в MainMenu) → `0.2.1` в `BootstrapScene.unity` на HEAD; версия периода по заголовку финального коммита/changelogs — **0.2.10**
**Сгенерировано:** 26.11.2026, по `git log bbeb900b..8c19df5c` + `git diff --stat` + журналам замеров, дизайн-нотам и STOP-вердиктам.

---

## 1. Метрики

| Метрика | Значение |
|---|---|
| **Коммитов** | **185** (`git rev-list --count bbeb900b..HEAD`) |
| **Автор** | 1 (моно-сессия, Indeed) |
| **Файлов изменено** | **527**; новых док-файлов — **322** |
| **Строк добавлено/удалено** | **+303 829 / −22 289** (в `Assets/`: +91 887 / −18 403) |
| **T-PERF01…25 (глобальная оптимизация)** | **53 коммита** — доминанта периода |
| **MotionBricks T-MB01…06** | **49 коммитов** — ресерч, закрыт STOP |
| **ELIO (T-ELIO01…V13)** | **19 коммитов** — ИИ-песочница/настольная игра |
| **Cloud v4 (T-CLOUD43…51)** | **9 коммитов** — новые этапы облачного рендера |
| **FENCE** | **7 коммитов** — генератор заборов |
| **NPC-корабли: вылет/палуба** | **6 коммитов** (T-NS-DEPART4/5, T-SHIPDECK-01/02) |
| **PRIMUM NPC / толпа** | **6 коммитов** (T1, 100→10000 NPC) |
| **Порт / WorldScene** | **6 коммитов** (PRIMUM_PORT v2/v3, layout) |
| **Инфра (T-BUILD01, пакеты, URP)** | **4 коммита**; Floating Origin — 2; риг персонажей — 2 |

Вывод одной строкой: период = **«глобальная оптимизация + исследование анимаций»**. v0.2.0 был контентной вехой, 0.2.05–0.2.10 — техническая: мир доведён до ума по профилировщику (десятки замеров, −99.8% по триггерным колбэкам, −85% по мусору колбэков, −77% по FO-исполнителю), облака получили v4-пайплайн, на анимационном фронте MotionBricks честно investigate-нут и закрыт без попадания в билд.

---

## 2. Что сделали — по направлениям

### 2.1 T-PERF01…25 — глобальная оптимизация

**Метод.** Маленький дифф → компиляция 0 errors → отдельный коммит → пользователь снимает Profiler `.data` → разбор с конкретными числами. Агент Play Mode не запускал: все подтверждения — по присланным захватам. Журнал — `docs/world/optimization/22112026/ITERATIONS.md` (1518 строк); корневые `IMPLEMENTATION_PLAN.md`/`ITERATIONS.md` в этой папке относятся к T-LOD01, не к T-PERF.

**База (захват `10-39-14`, 22.11):** 21 842 коллайдера, 2 800 DoorController, 20 `ShipDeckNav`, 8 NPC. В кадре `Physics.SendTriggerEvents` = **5 898.80 мс** и 30 180 КБ аллокаций; `NetworkPlayer.Update` — 11.87 мс / 44 КБ за кадр (ровно массив 2 800 дверей); `OuterCommZone.Update` — 82.77 КБ (два одинаковых `OverlapSphere`, 5 304 попадания). Первый A/B (`11-00-03`): `PlayerLoop` медиана **99.0 → 78.0 мс**, GC/кадр **171.1 → 101.8 КБ**.

**GC/аллокации.**
- T-PERF04: `TryGetComponent` в 4 триггерах — `GetComponentNullErrorMessage` **202.9 → 31.25 КБ/кадр (−85%)**; мусор четырёх колбэков **65 703 КБ → 0 КБ** при 102 925 вызовах; среднее GC сессии **376.5 → 202.9 КБ (−46%)**.
- T-PERF06/6 (Allocation Call Stacks): `NpcShipWorld.FixedUpdate` **46.0 КБ → 0 B**, весь FixedUpdate 78.9 → 4.8 КБ; медиана GC **77.6 → 20.8 КБ/кадр**.
- T-PERF24: 18.36 КБ/кадр.

**Триггеры/колбэки.**
- Сужение `commRange` 500–1000 → 60 м (`b2b1e7b9`) дало лишь −12% `SendTriggerEvents` — настоящий источник оказался в кораблях.
- T-PERF07 (`_sphere.enabled = false`): `OuterCommZone.OnTriggerEnter` **5 541 → 0 вызовов**, `SendTriggerEvents` 82.83/61.50 → 53.44/50.63 мс.
- T-PERF08 (реестр консолей + фильтр `attachedRigidbody`): `SendTriggerEvents` 38.20/37.88 → **0.189/0.068 мс/кадр (self −99.8%)**; колбэки консоли 2 082+10 786 → 0+0; `ShipController` 4 050+15 616 → 71+125.

**NavMesh / NPC-агенты.**
- T-PERF05/B (`m_Enabled: 0` в 20 префабах капитанов): нативные предупреждения **671 → 438 (−35%)**, `NotifyNavMeshAdded` 891 → 515 КБ/вызов (−42%), GC сессии 77.7 → 64.9 МБ (−17%). Остаток 438 разложен: 320 — 16 мировых NPC, 100 — `ApplyWorldRebase→Warp`, 18 нативных.
- T-PERF06: `ShipDeckNav.cs:342` **304 → 0**, `PrimumCrowdNpc.cs:410` **38 → 0**, `Failed to create agent` 56 → 18.
- T-PERF13/23: `NpcShipWorld.FixedUpdate` 1.099 → 0.749 (−31.8%), `NPC.Nav.Cruising` 0.675 → 0.383 (−43.3%).

**Реестры вместо `FindObjects`.**
- Двери (`bd0bde81`): 4 980 КБ → ~0.
- T-PERF09: broadphase дверей 5.893 → 5.101 мс/вызов (−13.4%); реестр NPC → 2.989 (−41.4%); реестр сундуков — `NetworkPlayer.Update` 3.922 → **0.886 мс**, self 2.851 → 0.033 (−98.8%).
- T-PERF10: зоны 1.443/1.183 → 0.258/0.235 мс; после кэша позиций 1.974/1.524 → 0.346/0.337 мс; T-PERF17 — 0.048/0.009 мс.
- T-PERF24: XZ-сетка дверей с шагом 24 м; `InteractionHint` 0.4484 мс/кадр исчез из top-self.

**Физика/синхронизация.** T-PERF18–20: один глобальный `Physics.SyncTransforms` на кадр (`PhysicsSyncCoordinator`) — ветер 1.616 → **0.907 мс (−43.9%)**, `NPC.Nav.Cruising` 0.589 → 0.304 (−48.4%), `Physics.Raycast` 0.271 → 0.087. Остаток 0.494 мс/кадр — движок (ParomRoute 0.419, PrepareSimulationStage 0.350).

**Тени/свет/камеры.** T-PERF13: `LightPool.Check` 0.724 → 0.225 (−68.9%). T-PERF17: `CityShadowBudget` 0.347 → 0.159, `DoorVisibilityBudget` 0.382 → 0.190, `ShipShadowBudget` 0.324 → 0.172 мс. T-PERF24/25: Δ-граница пересчёта и ленивый `Light.intensity` — City −42%, Door −45%, `LightPool` −44%.

**Двери/визуал/камера толпы.** T-PERF11–14: gizmos → `OnDrawGizmosSelected`, `EditorLoop` self 25.562 → 10.278 мс; `DockingPadTriggerBox.OnTriggerEnter` 2.573 → 0.0005 мс; невидимые DockPad пропускаются, `LateUpdate` 0.151 → 0.080 (−46.9%). T-PERF25: `PrimumNpcCrowdDirector.RefreshCamera` больше не берёт мусорный `Camera.main`; при невалидной камере LOD — **fail-open** (`sqr=0`), толпа не исчезает; `cam.layerCullSpherical` не ставится в URP (−2 warnings).

**FO-исполнитель.** T-PERF24: `SteadyAdvanceInterval = 0.25 с` после 1 с готовности, fail-closed пробы каждый кадр → `GlobalSceneNativeExecutor.Update` 0.1874 → **0.0426 мс (−77%)**.

**Консоль.** `ProjectLog.Verbose`: 7 видов info-логов → 0 вхождений, записи 1 063 → 878; экономия `LogStringToConsole` только −1 056 КБ (−5%) — прогноз −2.2 МБ **не подтвердился**. T-PERF25: 88 Warning, **0 Error** (34 `EngineThrusterVisual`, 23 `ShipCargoVisual`, 18 нативных агента), ожидается ~25 после правок.

**Итог по замерам.** Финальный захват `19-38-45`: `PlayerLoop` медиана **21.33 мс** против 24.79 у предыдущего. Но маршруты и EditorLoop различаются — строгого A/B нет, общий FPS не приписывается правкам.

**Цитируемые коммиты:** `147c4114` (разбор захвата и план), `b2b1e7b9`, `d2616aa2`, `2fb38061`, `c893fb4a`, `6ddb25f1`, `a2bc7c06`, `3459db01`, `54b12a59` (замеры), `81b7ec8d`, `d8d16c0d`, `e397fcb7`, `9b1a1e28`, `65c38419` (NavMesh/логи), `4a71ac00`, `bd0bde81`, `5db66952`, `d34bede1`, `b0bad1fb` (мусор/реестры), `2a231e50`, `8947b9b8`, `97962e8f` (триггеры), `76325281`, `3d3de448`, `9b16d000`, `c1aa098c` (Player.Update), `99a340d9`, `13d4156c`, `4d4a8520`, `d0fb0b2c` (зоны), `47dd435b`…`ebd944b6` (финальный пакет).

### 2.2 MotionBricks T-MB01…06 — исследование анимаций, закрытое STOP

**Зачем.** MotionBricks — ресерч NVIDIA (SIGGRAPH 2026, `arXiv:2604.24833`): генеративная модель движения на скелете SOMA (30 суставов, ~200M параметров, 30 Гц, 313 526 клипов). Не Unity-пакет: нужны TensorRT/ONNX (758 МБ FP32 / 380 МБ FP16), ретаргет SOMA→Humanoid и Smart Primitives. Цель — не новые циклы (в `PlayerAnimation.controller` уже 8-направленные Walk/Run, Sprint, Idle/Jump/Turn), а **переходные стадии**; единица поставки — domain pack. Рамка продукта пересмотрена с «MMO на сотни клиентов» на host-client до 4 игроков, поэтому прежний аргумент «runtime невозможен из-за MMO» был перепроверен замером.

**По тикетам.**
- **T-MB01** (`c3ea92aa`) — ресерч, fact-check, матрица применимости, ONNX-аудит (opset 17, 6 993 узла, 51 тип операторов — все поддерживаются Sentis 2.6.1, но импорт не проверен), CPU-бенч 73–77 мс N=1 (i7-8700). Решение: baked-клипы + гибрид, не runtime в MMO.
- **T-MB01A** (`cf4f8b75`, `036e79bc`, `711c70e3`, `3c2bb7d4`) — офлайн-пакет ~2.3 ГБ repo + ~1.47 ГБ SafeTensors, venv python312/uv, `sa-inbetween` CLI, Sentis tarball, checksums; `generate_once.py` PASS (RTX 3090), CPU smoke 55–71 мс.
- **T-MB01B** (`c6806dd9`) — безопасный роллаут: `baseline_hashes.tsv` (12 файлов), `check_baseline.ps1`, режимы `Disabled/BakedClips/NeuralExperimental`, 5 уровней отката.
- **T-MB02 / T-MB04a** (`934ecdf6`) — CPU-бейк SOMA→Humanoid `.anim`, `PlayableGraph`, world-delta ретаргет, 17 файлов в main. Демо `walk_to_jog` — разворот ~−197°, 22 кости, без плеч/кистей/foot IK. Рабочий клип KL `HumanM@Walk01_Forward`: 2.00 м/с, foot slide 0.07/0.11 см/кадр.
- **T-MB04b** (`ddbc51f0`…) — root motion на probe: 30.42 м / 15.21 с = **2.00 м/с**, Stop→baseline, body swap M→F→M, guard от второго `CharacterController.Move`. Найдено: реальный игрок — `NetworkPlayer_GlobalPilot.prefab`, не покрытый baseline.
- **T-MB03** (`90180758`) — discovery-бейкер (`MotionBricksBakeJob.Discover`), sidecar `.bake.config.json`, batch/validate/list, `*.bake.report.json` (SHA256, метрики, GUID). `PelvisSmooth`: таз 15.7 → 11.7 см; `applyRootMotion` guard 6/6; оба синтетических клипа FAIL валидатора — контент, не пайплайн.
- **T-MB04c** (`e33cc216`) — `MotionBricksLocomotionBridge` + `TryConsumeDisplacement`; `BakedRootMotion` на игроке не вызывает `Move`, публикует displacement; `NetworkPlayer.ProcessMovement` +11 строк, `_controller.Move` остался один. Прогон 33.13 м / 16.56 с = 2.00 м/с, 0 errors.
- **T-MB05** (`87102426`, `7ac26287`, `c6a5f57f`) — аналитический 2-bone IK ног + достижимость таза: `WalkForward_Male` 15.73 → **1.72 см**, `WalkToJog_Male` 5.10 → 2.81 см. Флаг `loop`, нециклический решатель; исправлены humanoid T-поза, stale parent (172° переворот), last-frame wrap, Arm Twist (178.8°), RootQ, ключица. Self-tests 6/6 + 4/4 + 6/6, baseline 12/12.
- **Official vertical slice** (`88613e8e`) — свой KL `Walk01_Forward [RM]` → inverse SOMA (roundtrip ≤2.4×10⁻⁶°) → официальный TensorRT vs `sa-inbetween`: 18 кадров, 2.142 м/с, yaw drift ~0, разница backend ≤13.3 мм / 2.6°. Humanoid-клип принят в Animation Window.
- **Locomotion pack v1** (вне тикета) — 22 перехода из 13 source clips; 0/22 validator PASS, 9/22 slide ≤2 см, максимум 11.32 см/кадр. Отклонён: «ничуть не лучше того, что было, и требует 100500 отладки»; артефакты удалены (~25 МБ).
- **T-MB06** (`6fd87d85` + 16 фиксов, `3e07ea6e`) — официальный `SOMACharacter` через Python/UDP sidecar: `MotionBricksRuntimeClient`, `SomaRetarget`, `RuntimePlayback` (gate `NeuralExperimental`, owner-only, Ctrl+F10), ReviewMenu. Протокол 64 B request / 2 204 B bind / 2 220 B pose, порт 49360 → 39360 (попал в исключённые Windows UDP-диапазоны). Отклик t50 idle→walk **1067 → 150 мс**, walk→run 850 → 350 мс; self-test 480 кадров/14 генераций PASS, p50/p95 5.107/32.302 мс, Unity 0 errors.

**Вердикт STOP (`f7997bf0`, 18.11).** «Вернёмся к MotionBricks как генератору переходных стадий, а в игре продолжаем использовать наш аниматор». Причины: offline pack не лучше crossfade; наш тюнинг ушёл от движка (6/7 токенов, `position_scale 0.58`, halflife 0.015 против 0.6); рассинхрон скорости/фазы (5/10, клипы 1×, KL walk 2.00 м/с → slide до 2.5×); ретаргет C#-рига против позы пакета никогда не сравнивался; визуал T-MB06 хуже превью NVIDIA; прыжок не решает (апекс 8 м / 1.79 с против authoring 0.5/0.75 м). Анализ 18 нашёл две неисправленные корневые ошибки в recorder-framework.

**Доказано / отвергнуто / отложено.** Доказано: бейк, ретаргет, валидация, root motion в игре, гейты и откат работают. Отвергнуто: offline matrix-пак и runtime-нейро как путь развития. Отложено: jump-spike, A/B `--engine-defaults`, 1/2/4 клиента, FPS/VRAM, equipment/body-swap, T-MB07/08/09.

**Влияние на продукт: фактически ноль.** В игре остался свой Animator + 286 клипов Kevin Iglesias; `MotionBricksRuntimeSettings.asset` — `mode: 0`, `allowRemotePlayers: 0`; 7 runtime-скриптов компилируются в IL (несколько КБ), Editor-скрипты не попадают в билд, модель/TensorRT/venv вне проекта, новых пакетов нет. Probe удалён из сцены (`f1339acc`); единственный продовый хук — `NetworkPlayer.cs:1399` → `TryConsumeDisplacement` при пустом реестре (инертен).

**Документы:** `docs/world/MOTIONBRICK_animations/00_STOP_VERDICT.md`, `README.md`, `HANDOFF.md`, `01_RESEARCH.md`, `06_SAFE_ROLLOUT_AND_ROLLBACK.md`, `baseline/BASELINE_MANIFEST.md`, архив 13/16/17/18; `docs/dev/MOTIONBRICKS_LOCOMOTION_PACK_DESIGN.md`, `MOTIONBRICKS_OFFICIAL_VERTICAL_SLICE_DESIGN.md`, `T-MB05_LEG_IK_LOOP_DESIGN.md`, `T-MB06_RUNTIME_SIDECAR.md`.

### 2.3 Облака v4 (T-CLOUD43…51) — семь этапов рендера

Девять коммитов (19–20 ноября) превратили volumetric-рендер в 7-этапный v4 на базе `Fishypants/cloudscape`:

| Этап | Коммит | Суть |
|---|---|---|
| 43 | `62cbdf9a` | dual-HG, 3-член Beer–Lambert, powder/crease (`CloudLightingV4`, `LightingV4Strength=0.70`) |
| 44 | `2089114a` | coverage-дом, перистый низ, разделение шума слоёв (`ShapeV4Strength=0.65`) |
| 45 | `7a5abf61` | mid→fine эрозия, early-out ≤0.003, remap (`DetailV4Strength=0.55`) |
| 46 | `aaf8044d` | mip-chain `CloudNoise3D`, footprint LOD, IGN (`StableV4Strength=0.65`) |
| 47 | `b22fd67d` | raymarch в RGBA16F при `HalfResRender=0.5` + bilateral depth-aware upscale (`DepthUpscaleSigma=0.03`) |
| 48 | `dbf35336` | temporal ping-pong history, репроекция `_PrevViewProj`, 3×3 clamp, `TemporalBlend=0.85`, сброс при F8/F9/телепорте >100 м |
| 49 | `6afb47b5` | sun-space карта пропускания R16F, self-shadowing, «солнечные лучи» без god-ray прохода |
| 50 | `0b43e56c` | мерцание ~3 Гц за кораблями: `_CenterDelta` + trilinear advection с тороидальным wrap вместо nearest |
| 51 | `75a81cc6` | `MaxDisplacement=4` против runaway-накопления (16 сплатов → амплитуда ~100 × `DisplacementStrength=150`), `FollowTarget` только у корабля с тегом `Player` |

Связанный `T-FO09T` (`d19211ce`) — authored→runtime высоты облаков/штормов через `CumulativeRebaseOffset.y`, обновление оффсета на втором клиенте до ребейза. Приёмка: 43 — «визуально лучше», 46 — «оставляем», 47/48/49 — «принимаем пока»; 0 errors, `ShaderHasError=false` на каждом этапе. Визуальная приёмка 45/47/48/49/50/51 и F8/F9 — **NOT RUN**, GPU-замеры отложены. Доки: `docs/world/CLOUD_system/3.0/STATUS.md`, `ITERATIONS.md`, `docs/world/CLOUD_system/research/`, `docs/world/floatingorigin/09T_CLOUD_RUNTIME_HEIGHTS.md`.

### 2.4 ELIO (T-ELIO01…V13) — внешняя песочница правил

Не Unity-сцена и не игра внутри игры: автономный JS/HTML-прототип правил и движка в `docs/dev/elio/`, по сцене «Год 2089. Эльо» (3 фракции — Плюсы/Минусы/Нули, 64 клетки, 10 фигур/30 очков, режимы Цикл/Стрелка/Передача; канон v1.4). 19 коммитов 6–10 ноября + сайт `index.html` и сборка `dist/` 14/18 ноября.

ИИ и баланс: v1.3c с LLM-турниром и дуэлью AI 3–0 (`81aa9761`); DeepSeek 10+1 C1 против ИИ — 1–9, два DeepSeek — 2–1; Zero v2 — 1–0–4, v2.1 на v1.3d — 1–0–5; Zero mix 40%, Beam Zero 10%; headless 100×18 = 1 800 партий, 0 contaminated; 400×2 — 27.25/31/40.5 против 25.25/34.75/40; эталон «Стрелка+Длинный» 400+400 — 35.9/33.4/29.3. V13 — защита от center hold, подсказки и поддержка Нулей. **Все балансовые цифры до v1.4 и требуют перепрогона.**

Доки: `docs/dev/elio/README.md`, `index.html` (SVG-графики, таймлайн v1.0→v1.4), `ELIO_RULES_FINAL.md`, `LLM_ARENA*.md`, `balance/README.md`, `AUDIT_V13D.md`.

### 2.5 FENCE + SplineRoad — генеративные объекты мира

FENCE (7 коммитов, 14–15 ноября): дизайн-нота 522 строки (`6064f38a`), ядро `FencePath`/`FenceMeshBuilder` со стилями Bar и Masonry (~1 907 строк), editor tool + self-check probe (~1 081 строка), пилотные ассеты `FT_Fence_Bar_Steel` / `FT_Fence_Brick_Block`, фикс краша при дублях имён, инспектор с `heightOverride`/`sectionStepOverride`, GRD-фикс двухфазной пересборки. SplineRoad (`3e902c2c`, `66785db1`) — покрытие, бордюры, коллайдеры, привязка к рельефу, зондирование поперёк ширины.

Бюджет: Bar 100 м ≈ 12 000 трис, Masonry ≈ 4 000; адаптивность по углу излома (ε=5 см): <2° прямая, 2–25° изгиб, 25–150° miter+столб, >150° шпилька. Все меши в локальных координатах (FO-безопасность), коллайдеры — BoxCollider в `<Fence>_COLLIDER`. Self-check **25/25**, Play Mode **NOT RUN**. Док: `docs/world/spline_assets/00_DESIGN_SplineFence.md` (v0.4).

### 2.6 Порт PRIMUM v2/v3 и Floating Origin (T-FO06L/09T)

`ecf908b7` (v2 pipeline + отчёт + material map), `a2b96315` (v3), `0f71a6d0` (разметка `WorldScene_0_0`: `primum_port_v3`, `40_HUB_WAREHOUSE`, `PASSENGER_CANOPY_WAITING_AREA`, +387/−51 строка). v3 — `PRIMUM_PORT_V3` 216 рендереров/216 слотов/15 материалов, 217 объектов (merged 26, standalone 3), габарит 1608.24×222.85×1600.41, `urpLit=0`, `null=0`, root scale 1.060, layer 8. Предыстория: 5 444 трансформа → палитра из 22 материалов (`docs/dev/PORT_PIPELINE_STATUS.md`). Отчёты: `docs/world/primum_city/09_MATERIALS/port_v2_import_report.csv`, `port_v3_import_report.csv`.

`T-FO06L` (`1f71e225`) — авто-починка репарентов в `BakePilotSceneCatalog` + перебик пилотного каталога; симптом `scene_preparation:authored_parent_binding_mismatch` у `DockStation_Primium`, устарело 50 layout/dependency-хешей; round-trip фикстуры `reparent=1`, `entries=146`, `TryCompile/TryMatchDigest=True`. Док: `docs/world/floatingorigin/04H_SCENE_CATALOG_AND_LIFECYCLE.md` §7.

### 2.7 Персонажи — T-CHAR-RIG01/02

`f5c7de09` (пилот) + `775552dd` (масштаб). Пилот `PC_Char_Adjudicator_Female_APOSE_v01`: 39 998 трианглов, 19 988 вершин, рост 1.7837 м; `rig_character_v2.py` выбрал концевые кости пальцев (thumb=4, index=0, middle=1, ring=2, pinky=7); 50 деформирующих костей, 0 невзвешенных вершин, 0 нулевых костей, ≤4 влияний, `hands.p95=0.00720 < 0.008`. В Unity `isValid=true`, `isHuman=true`, 51 Humanoid-маппинг (включая 30 костей пальцев).

Массовый прогон: **106/106 OK** (58 со строгими пальцами, 48 в перчаточном режиме); NPC 12k — 106/106, 40k — 106/106 Humanoid-аватаров, 0 errors; расхождение NPC↔40k — максимум 2.8 мм. Скрипты `Tools/Blender/CharacterRig/`; док `docs/dev/character_rig_automation.md`.

### 2.8 NPC-корабли: вылет из дока и палуба игрока

**T-NS-DEPART4/5** (`dfa44cda`, `36ddcf6c`, `961aa2a1`, `f2af1ce8`): найдены P0 — мьютекс вылета ждал один тик (20 мс); P1 — слот отпускался рано, центр брался по станции маршрута, не было FIFO; P1 — взлёт не держал уровень (нос оставался, хвост задирался); P2 — фиксированный chimney 60 м. Внедрены `TryAcquireDeparture` (TTL 5 с, FIFO по `NpcInstanceId`), удержание слота, адаптивный chimney, выравнивание `TickLift`, sphere-vs-sphere fast path. DEPART5 — 8 курс-гейтов радиусом 500 м, разведение 150 м, подавление взаимного avoidance между скоординированными кораблями, ожидание прибывающих до 45 с. Док: `docs/NPC_others_peacfull/npc_ship/20_DEPARTURE_COORDINATION.md`.

**T-SHIPDECK-01/02** (`7d8b6aec`, `1ed630a5`): сейв пешего игрока на палубе (`platformShipId`), проба палубы SphereCast вниз 6 м с `pos+0.5` без требования `attachedRigidbody` (старый probe при +3.48 м промахивался); после F-выхода — `ReactivateGlobalMotionAfterExit`, т.к. NGO `StopServer()` при `SetParent` глушил WASD/F/E и гравитацию. Док: `docs/Character/respawn/ITERATIONS.md`.

### 2.9 DayNight, билд и инфра

**DayNight** (`e922c9a6`): старые `colorFilter` гасили каналы (Night 0.14/0.16/0.96), temperature overlay (0.75 при рекомендации 0.2–0.4) перезаписывал fog/ambient. Главная причина дрейфа — `Instantiate(VolumeProfile)` не клонировал компоненты: `hueShift` писался прямо в ассет и каждый домен-релоад давал −4.196556 → −180. Фикс `CreateIsolatedProfileCopy`, новые фильтры Day (1.00/0.92/0.78), Twilight (0.72/0.62/1.00), Night (0.50/0.56/1.00), `hueShift=0`; изоляция подтверждена в edit mode.

**Инфра.** T-BUILD01 (`654cc939`) — guard PerfHUD и вынос editor-only скриптов в `Assets/Editor/` (20 файлов, +1 151 строка), исправление компиляции player-сборки. `2f2a2866` — `com.ramenvr.autopilot` с локального tarball (723 КБ удалён) на git-источник с pin; `0cf7504e` — `.gitignore` +11 строк; `cf682f80` — URP prefilter-флаги (`AdditionalLight 3→0`, `ForwardPlus 0→2`, `UseLegacyLightmaps 0→1`); финальный `8c19df5c` — version bump + обновление pin autopilot до `e08ca126…`.

---

## 3. Документация — что документировали

Всего в диапазоне добавлено **322 новых док-файла**.

| Папка | Что добавлено/обновлено |
|---|---|
| `docs/dev/elio/` (новое, **220 файлов**) | исследовательский сайт ELIO, правила v1.4, LLM-арена, баланс, AUDIT_V13D |
| `docs/world/MOTIONBRICKS_animations/` (новое, **38 файлов**) | ресерч, пайплайн, STOP-вердикт, архив анализов 13–18, baseline-манифест |
| `docs/Fun/` (новое, **~31 файл**) | CloudTrader 2.0 — сайт (`public/`), `dist.zip`, vite-конфиг (`fun_CloudTrader_2.0.x`) |
| `docs/world/optimization/22112026/` (новое, **9 файлов**) | разбор захвата, план, 7 отчётов T-PERF03…09, 1518-строчный ITERATIONS |
| `docs/NPC_others_peacfull/PRIMUM_NPC/` (7 файлов) | T1-чеклист, PERF_BUDGET, поведение после Floating Origin |
| `docs/world/CLOUD_system/` (5 файлов) | облака v4, статус/итерации, research cloudscape, FO09T |
| `docs/world/spline_assets/`, `docs/world/primum_city/` | дизайн FENCE/SplineRoad, отчёты порта v2/v3 и material map |
| `docs/dev/` | 4 дизайн-ноты MotionBricks, `character_rig_automation.md`, хендоверы/вердикты T-MB |
| `docs/dev/iterations.md` | журнал ноября 2026 (замеры T-PERF, ELIO, MB) |
| `docs/Character/respawn/`, `docs/NPC_others_peacfull/npc_ship/` | T-SHIPDECK-01/02, координация вылетов DEPART4/5 |

Принцип периода, унаследованный от T-PERF: **сначала замер/лог, потом код; каждый разбор лога — в ITERATIONS с номером захвата и выводом**; STOP-решения фиксируются отдельным вердиктом, а не молча.

---

## 4. Файлы сцены/конфига

- **`BootstrapScene.unity`** — `versionText` 0.2.0 → 0.2.05 → 0.2.1; поле версии в MainMenu; инстансы `PRIMUM_PORT_V3`, `40_HUB_WAREHOUSE`, `PASSENGER_CANOPY_WAITING_AREA`. NetworkManager не тронут.
- **`WorldScene_0_0.unity`** — layout-правки (+387/−51 строка): инстансы порта v3, заборы/сплайн-дороги, пилотная толпа 100 NPC и стресс-тиры, облака/FO; `NavMesh-WorldRoot_0_0.asset` перебейкан.
- **Префабы (23 файла)** — 20 captain-префабов NPC (`NavMeshAgent m_Enabled=0`, T-PERF05/B), `NetworkPlayer_GlobalPilot.prefab` (platform step, rescue drop, female avatar), FO `GlobalMotionPilotProfile.asset` / `GlobalMotionPilotSceneCatalog.asset` (авто-починка репарентов T-FO06L).
- **Скрипты (104 файла в 14 директориях):** World 31, Editor 16, Ship 15, Animation 10, Core 10, PeacefulShip 6, Docking 4, AI 3, Player 3, Admin 2, Combat/Debug/Network/Rendering по 1. Editor-only вынесен в `Assets/Editor/` (20 файлов) ради player-сборки.
- **Settings:** `ProjectC_URP.asset`, `ProjectC_URP_Renderer.asset` — prefilter-флаги и Δ-границы теней (T-PERF14/24).
- **Пакеты/инфра:** `Packages/com.ramenvr.autopilot-1.0.1.tgz` удалён (723 КБ), `manifest.json` + `packages-lock.json` переведены на git-источник с pin (`9e0fbb0f…` → `e08ca126…`); `.gitignore` +11 строк; `Assets/Editor/_AuraTemp/*`, `aura_export/` исключены.

---

## 5. Итог: статус на 0.2.10

| Подсистема | Статус на 0.2.10 |
|---|---|
| T-PERF01…25 (глобальная оптимизация) | ✅ 53 коммита; SendTriggerEvents self −99.8%, мусор колбэков 65 703 КБ → 0, нативные NavMesh- warning 671 → 18, FO-исполнитель −77%; ⚠️ верификация после 24/25 NOT RUN, строгого A/B нет |
| MotionBricks T-MB01…06 | ✅ Ресерч завершён STOP-вердиктом (`f7997bf0`); в билд не попало, продовый хук инертен |
| Облака v4 (T-CLOUD43…51) | 🔄 7 этапов введены, 0 errors; 43/46 приняты, 47–49 «пока»; визуальная приёмка остальных и GPU-замеры NOT RUN |
| ELIO | ✅ Внешний JS-прототип + сайт; канон v1.4, баланс собран до v1.4 — нужен перепрогон |
| FENCE / SplineRoad | ✅ Генератор + 2 стиля + editor tool + инспектор, self-check 25/25; Play Mode NOT RUN |
| PRIMUM_PORT v2/v3 | ✅ v3 разметен в `WorldScene_0_0`: 216 рендереров/15 материалов, `urpLit=0` |
| Персонажи (T-CHAR-RIG01/02) | ✅ 106/106 Humanoid-ригов, NPC 12k/40k — 106/106, 0 errors |
| NPC-вылеты / палуба | ✅ DEPART4/5 (FIFO, TTL, гейты, adaptive chimney), SHIPDECK-01/02 (сейв на палубе, возврат управления после F-выхода) |
| DayNight | ✅ Изоляция Volume-профилей (`CreateIsolatedProfileCopy`), новые colorFilter, hueShift=0 |
| Билд/инфра | ✅ Player-сборка компилируется (T-BUILD01), autopilot на git-pinned источнике |
| Floating Origin | ⚠️ `runtimeRebaseReadiness = NOT_READY` без изменений; точечные фиксы T-FO06L/T-FO09T, каталог перепечён |

---

## 6. Открытые хвосты

- **Верификация NOT RUN** после T-PERF24/25: видимость толпы, вход/выход из корабля, 18 нативных warning, навигация палубных NPC. Ручные Play Mode-проверки — за пользователем (скилл `manual-playtest-only`).
- Строгого A/B финального захвата нет: 21.33 мс против 24.79 при разных маршрутах и EditorLoop — общий FPS **не приписывается** правкам.
- Ленивая регистрация палуб (`ShipDeckNav`) заблокирована FO-манифестом; restore толпы после F8 не подтверждён прогоном; 16 мировых NPC вне навмеша — правка сцены/бейка.
- Отдельные задачи: `ContractWorld.TickCore` (−1.63 КБ/кадр) и boot `PrepareAndStartHost` (13 290 КБ / 4 вызова); `verboseLogging: 1→0` в `Assets/Resources/` не закоммичен (папка в `.gitignore`).
- Облака v4: визуальная приёмка 45/47/48/49/50/51 и F8/F9 NOT RUN; GPU-замеры отложены.
- ELIO: все балансовые таблицы — до канона v1.4, требуют перепрогона.
- MotionBricks отложено: jump-spike, A/B `--engine-defaults`, 1/2/4 клиента, FPS/VRAM, equipment/body-swap, T-MB07/08/09.
- Мегаферма по-прежнему pending FO-размещение в сцене (хвост из 0.2.0).
- Версионный рассинхрон: маркер в сцене `0.2.1` против `0.2.10` в заголовке финального коммита и узле changelog — синхронизировать при финальном коммите.

---

*Диапазон: `bbeb900b` (v0.2.0) → `8c19df5c` (HEAD, v0.2.10). 185 коммитов, 527 файлов. Маркер в `BootstrapScene.unity` = `0.2.1`; версия периода по заголовку финального коммита — 0.2.10.*
