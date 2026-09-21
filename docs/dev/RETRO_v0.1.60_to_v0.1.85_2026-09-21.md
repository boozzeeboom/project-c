# Ретроспектива: v0.1.60 → v0.1.85

**Период:** 14.09.2026 → 21.09.2026  
**Начало:** `2e998329` (v0.1.60, «Floating Origin»)  
**Конец:** `28772325` (v0.1.85, HEAD)  
**Сгенерировано:** 21.09.2026

---

## 1. Метрики

| Метрика | Значение |
|---|---|
| **Всего коммитов** в диапазоне | **41** (от 2e998329 до 28772325) |
| **Файлов изменено** | 2811 (по stat-дифу e7e0a5fa → 2e998329, полный разворот) |
| **Из них за период v0.1.60→v0.1.85** | 174 файлов, +97324 / −39330 строк |
| **Версий:** | 0.1.60 → 0.1.66 → 0.1.70 → 0.1.75 → 0.1.77 → 0.1.78 → 0.1.80 → 0.1.85 |
| **Тикетов в коммитах** | PAROM-01/02/03, FO09S/P/R/Q, LOD01/01b, TERR-01/02, SHIP-FIX01…12, WORLD-MAP, WORLD-COMPASS, NS-P2, ADM-00…08a, T-ADM-00…05, DF-001 rev.1…10 |

---

## 2. Что сделали — по подсистемам

### 2.1 Паромная ветка (T-PAROM-01/02/03)

**Тикеты:** T-PAROM-01, T-PAROM-02, T-PAROM-03  
**Коммиты:** `ebdc9958`, `734177b0`, `098ca47c`, `f319b4f8`

- **T-PAROM-01:** Менеджер маршрута парома, кабинка, тросы — всё в WorldScene_0_0. Заготовка паромной ветки. Регистрация ParomRoute_01 в closed-world каталоге пилота (маркер + Unmanaged/ShipOrRigidbodyRoot + digest) — `ebdc9958`.
- **T-PAROM-02:** Кабинка едет по провисшей кривой тросов — SaggedPoint + касательная. Гизмо повторяет профиль троса — `098ca47c`.
- **T-PAROM-03:** Документация и планы. Минимизация мира — `f319b4f8`.

### 2.2 Пайплайн нового контента (FO09S)

**Тикеты:** T-FO09S, T-FO09R  
**Коммиты:** `df4f1918`, `26791a4c`, `e79c2208`

- **Bake Pilot Catalog (1-клик):** скан сцен, превью diff, маркер + запись + хэши + digest. DryRun-режим — `df4f1918`.
- **Пайплайн нового контента:** ретроспектива парома, две подсистемы FO, отмена регламента 09R для NO-объектов — `26791a4c`.
- **Документация:** снята пометка о реализованном бейке в 09R, строка 09S в README — `e79c2208`.

### 2.3 Пресеты дальности прорисовки (T-LOD01)

**Тикеты:** T-LOD01, T-LOD01b  
**Коммиты:** `c2cf4ec6`, `aec2a7b0`, `80745bc5`

- **ViewDistanceConfig** — конфиг дальности прорисовки. ESC-Видео интеграция. Логи, защита reload.
- **T-LOD01b:** fogScale в пресетах — Far открывает панораму.

### 2.4 Veil → CLOUD_system (T-FO09P)

**Тикеты:** T-FO09P  
**Коммиты:** `4e61650e`, `8ced077c`, `cb69da82`

- Veil вынесен из CloudManager в отдельный **VeilController**.
- FO-адаптация: `ApplyRebaseTranslation`, `VeilShifted` в success/rollback/broadcast.
- Убран `GlobalSceneSourceMarker` с VeilController — лишний baked-маркер вне каталога ронял pilot startup gate (extra_baked_marker).
- Документация: authored `BaseVeilHeight` 1200→2200.

### 2.5 Террейн (T-TERR-01/02)

**Тикеты:** T-TERR-01, T-TERR-02  
**Коммиты:** `77d6c1eb`, `5b467b55`

- **T-TERR-01:** Единый террейн WorldScene_0_0 вместо FBX-массивов — база 600, пики по FBX до 5000, хребты, поселения открыты.
- **T-TERR-02:** Генеративные руины низин — 60 хамлетов, 496 инстансов, GPU instancing.

### 2.6 Корабль: серия фиксов (T-SHIP-FIX01…12)

**Тикеты:** T-SHIP-FIX01…T-SHIP-FIX12 + T-SHIP-DOC01…10 + T-SHIP-REVIEW  
**Коммиты:** `df70c32e`, `c2aceb04`, `9a0f2452`, `116457f2`, `df70c32e`…`d4a72f34` (см. full log выше)

Это **серьёзная серия** — server-authoritative корабль:

- **FIX01:** пилотный ввод через RPC (meziy/roll/refuel), нет host keyboard.
- **FIX02:** server-authoritative pilоt membership (Add/Remove guards).
- **FIX03:** server-authoritative цены в ShipModuleServer (sell/repaint/hull).
- **FIX04:** server-authoritative Recall (ownership + price + pad validation).
- **FIX05:** убран double dt на meziy thrust (ForceMode.Force integrates).
- **FIX06:** HUD fuel из telemetry (+refuel flag), local fallback.
- **FIX07:** cargoDetail → event-driven NetworkVariable (снят 5Hz snapshot).
- **FIX08:** module ClientRpc via Manager + TargetRpc notifications.
- **FIX09:** meziy activator survives runtime module changes (Refresh).
- **FIX10:** cargo console stacks + delayed refresh after result.
- **FIX11:** store path enforces effective cargo limits (module bonuses).
- **FIX12:** safe small fixes (visuals poses, getter, configs, triggers).
- **DOC01…10:** серия документов по коду корабля — перепроверка P1, Key architecture, cargo guard scope, engine vs broken, repair manager, bootstrap rule, Recall vs Persist/Dock/Ownership, Cargo P3 stale (фаза A ЗАКРЫТА).
- **T-SHIP-REVIEW 2026-09-18:** код-ревью корабля + план фиксов.

### 2.7 Мирная карта (WORLD-MAP)

**Коммиты:** `34d124ae` → `63a86d0b` (цепочка из ~17 коммитов)

Полноценная морская карта на M:
- Каркас морской карты (пергамент, порты, циркуль, слои-заглушки).
- Свободный осмотр карты (drag, зум колесом, слежение).
- Ручные метки (4 типа, постановка, выбор, удаление, FO-хук).
- Журнал треков (запись, выцветание, FO-хук сдвига).
- Персист журнала (файл, кумулятив FO, разрывы телепортов).
- Пеленг на выбранную метку в HUD (TGT-строка).
- Легенда в скролле + пергаментный стиль кнопок.
- **De-sea: КАРТА вместо морской**, журнал полётов, без моряков.

### 2.8 Compass Rose (WORLD-COMPASS)

**Коммиты:** `6afc24c8`, `2808ed76`, `ebe29f20`

- Север через розу ветров + HDG-компас в HUD корабля.
- CompassRose_North в WorldScene_0_0 + ленивый резолв севера.
- Калибровка розы Y=28.8.

### 2.9 Admin-панель (T-ADM-00…08a, T-ADM-04)

**Коммиты:** `cda1a50e`, `f474556a`, `fbe2490e`, `380974ed`, `ce099a3f`, `ea58d022`, `d9728de1`, `ef283542`, `fbe2490e`

- **AdminRuntimeWindow** (F12, 8 tabs, code-built UI Toolkit).
- **AdminFacade + MoveCheats + LogBus**, debug setters, god/speed/teleport hooks.
- **ProjectCPerfHUD** (dev/editor), panel-only toggle.
- Фиксы: noclip flight flattened to horizon (E/Q vertical), wrap/scroll layout, cursor unlock/lock on open/close (CraftingWindow pattern), player peak teleports (no WorldCamera in scenes), PerfHUD/NGO auto-create, readable text.
- **Fix persistence total loss on rejoin (FO rebase frame)** — `d9728de1`.
- Удалены legacy Ship/Meziy debug HUDs.

### 2.10 Calendar / DayNight

**Коммиты:** `8d8e946a`, `7d8448b6`

- Calendar: seasonal temperature from month + diurnal swing.
- DayNight: daily hueShift variety (-10..+10, tuned via profile).

### 2.11 NPC-корабли (T-NS серия)

**Коммиты:** `5f2663f3`, `d4f9d70c`, `9cd2756a`, `b1e5d087`, `42c41233`, `db42252a`, `2bc05532`, `dfe7eac9`, `84f5b711`, `7b410134`, `68606060`

- Снос мёртвого блока NpcShipWorld (TickNpc + хелперы).
- Инвентаризация мёртвого кода NPC-кораблей — вердикт мёртв/резерв/живое.
- **NPC-корабли ревью:** вердикт по prior-анализу и план курсирования.
- Berthing watchdog + holding-точка + divert.
- Учёт падов для NPC — used при доке, progress-refresh окна, stale-guard.
- Departure-Chimney — набор 60 м над падом перед Cruising.
- Avoidance петля разорвана — cooldown, достижимый clear, эскалация набором.
- Мульти-leg advance с guard станций + cap dwell HeavyII 6000→600.
- Berthing-коридор — Overhead-ворота + вертикальный спуск в трубе.
- Второй проход ревью NPC-кораблей — построчная допроверка.

### 2.12 NPC-корабли: T-CREW серия (крейса)

**Коммиты:** `e1da0115`, `86b93dad`, `57c82125`, `a6a73418`, `e973463f`

- Gorgona crew manifest, anchors, spawner.
- Explicit ship crew attachment.
- Configure captains for NPC ships.

### 2.13 T-ESC05: Esc-Графика — эффекты

**Коммит:** `436d5353`  
Секция Эффекты (фокус/едж/температура) в Esc-меню.

### 2.14 DistantFocus (DF-001 rev.1…10)

**Коммиты:** `7633e1cb` → `e40af8d0` (10 ревизий)

- DistantFocus Bokeh DoF + GazeAutofocus (Volume prio 300, focus-рейкаст взгляда).
- Якорь-фокус на цель SpringArmCamera.
- Фикс пустого FocusVolumeProfile (AddObjectToAsset) + fallback-DoF.
- Ленивая привязка к камере игрока + HasAnchor + debugLog.
- Авто-диафрагма + база профиля 85мм f/2.
- Гистерезис фокуса + мягкое боке 65мм f/2.8 HQ.
- Свой far-field проход фокуса дальних (шейдер+фича+контроллер), Bokeh Off.
- DebugView самодиагностики прохода (ForceBlur/ShowDepth).
- DebugView принудительно Off (футган ForceBlur).
- Фокус через Volume-Gaussian (свой проход Active=false) + живые ридонли.

### 2.15 ShipPresetCreator (T-SHIP01)

**Коммит:** `82447b2e`

Универсальный Editor-тул создания кораблей.

---

## 3. Моделинг — что съмействовали

### 3.1 Фермерское поселение 0-1 (КРС → шкуры)

**Тикет/коммит:** `9e8bf9b6` (MKT-DOM-002)

В WorldScene_0_0:
- **12 MarketZone** в WorldScene подтверждено: основные рынки, **farm-зоны** и road-зоны.
- MarketZone_Primium уже имел правильную ссылку.
- DockStation_Farm_0_0.locationId изменён: `DockStation_Farm_0_0` → `PRIMIUM_FARM_0_0`.
- В NpcShipSchedule_Courier и NpcShipSchedule_Trader route endpoints приведены к `PRIMIUM_FARM_0_0`.
- Объект `WorldRoot_0_0/Primum_farms/Средняя 0_0`: `OuterCommZone.stationId = DockStation_Farm_0_0`.
- **MarketConfig_Primium_farm_0_0** — 910 строк, полный ассет рынка фермы.
- Ещё 6 farm-рынков: `MarketConfig_Primium_farm_0_1…0_4`, `MarketConfig_Primium_farm_1_1` (по 30 строк каждый — базовые).

**Итого:** фермерские зоны (ферма 0-1 и др.) интегрированы в экономику — рынки, станции, маршруты NPC-кораблей. Инфраструктура для производства шкур КРС подключена к торговой системе.

### 3.2 Корабль тяжёлый (Heavy)

**Тикет/коммит:** `e9a9d72c` (T-SHIP06)

- **Heavy preset:** `thrustForce=100k`, `verticalForce=50k`, `massMultiplier=25`.
- Нет отдельного пресета Heavy — вписан в существующую систему кораблей как Heavy-класс.

### 3.3 Корабль средний (Medium)

**Тикет/коммит:** `a456d9b5` (T-SHIP06 fix)

- **Medium preset:** `thrust=30000`, `yaw=150000`, `vertical=30000`.
- Коллизия палубы (ShipDeck layer), NavMeshSurface Volume.

### 3.4 Корабль лёгкий / яхта прогулочная (Light)

**Тикеты/коммиты:** `f8d441f9` (T-SHIP05), `e911f625` (T-SHIP08)

- **T-SHIP05:** фикс ходьбы по палубе (NavMesh печётся при создании), Key ItemData заполняется корректно.
- **T-SHIP08:** правка light пресета + правка лёгких кораблей.

**NPC-префабы лёгких кораблей** из `Assets/_Project/Prefabs/Ships/`:
Альбатрос, Вавилон, Гигант, Горгона, Мастодонт, Сильфида — все с NavMesh-DeckNavSurface и полным набором компонентов.

### 3.5 Префабы NPC-кораблей (HeavyII)

- `NPC_Ship_HeavyII_03.prefab` — HeavyII NPC-корабль с NavMesh-DeckNavSurface (7572 байт).
- `NPC_Ship_Heavy_Default.asset`, `NpcShipSchedule_Heavy_Default.asset` — расписание.
- `NpcShipSchedule_HeavyII_Default.asset` — расписание для HeavyII.
- `DONT-DELETE-NPC REFERENCE SHIP.prefab` —{Reference} NPC-корабля (2316 строк).

### 3.6 Крейса (T-CREW)

- Gorgona crew manifest + anchors + spawner.
- ShipCrewManifest для 17 NPC-кораблей (albatros, bereg, citadel, gigant, letuchiy, lorein, mastodont, npc_reference_ship, olimp, peshchera, reka, shmel, silfida, skat, strannik, torrens, ugolshchik, vavilon, vetrovorot, zhuk).
- Автопilot на Gorgona — T-CREW anchor fixes: DriveDeckNav абсолютный телепорт NPC в позицию прокси → при неидеальном bake NPC ставился мимо палубы → probe терял платформу → EndRide → сдув.

### 3.7 Engine Visual System (T-ENG02)

**Коммиты:** `cfbc954b`, `0016d74a`…`e211e658`, `6c6f50c4`

- _pivotTransform вращается, _pivotOffset для точной настройки.
- Два независимых трансформа: _pivotPoint + _visuals.
- Визуалы двигателя реагируют на WASD после выхода из корабля.
- FBX engine pivot и propeller deflection исправлены.

### 3.8 ShipPartShake (T-SHIP-SHAKE)

**Коммиты:** `dda0f7e6`, `8271a5a4`, `49621a87`

- Сглаживание thrust через SmoothDamp (_smoothTime) — плавная атака/затухание дрожи.
- Плоская кривая по умолчанию → синусоида через EnsureSineCurve().
- Визуальный дребезг частей корабля при тяге (W/S).

### 3.9 RepairManager (T-REPAIR-MANAGER)

**Коммиты:** `bd139e20`, `a4537a02`, `94353a2c`, `MKT-DOM-002`

- Рефакторинг цен — все цены на NPC RepairManager.
- Ship recall — кнопка «Вызвать» в RepairManagerWindow.
- Корабль наблюдения + left-aligned UI.
- Teleport на ближайший свободный пад за кредиты.
- Сохранять isEngineRunning в DTO, восстанавливать двигатель + freeze при рестарте сервера.
- Player-ship position persistence — freeze, save, restore, ship-proximity respawn.

---

## 4. Экономика / Trade (MKT серия)

**Коммиты:** `cae0591b`, `d972adad`, `34db10d9`, `811066c6`, `339ee385`…`9e8bf9b6`, `c4dd4fd8`…`aeea67ba`, `MKT-DOM-001/002`, `T-MKT01…12`, `MKT-UI-003`

- ContractCatalog — централизованный каталог контрактов.
- MarketConfigCollector — нормализация locationId + авто-сбор из сцен.
- MarketConfig_Primium_farm_0_0 — 910 строк, полный ассет.
- ContractWorld — 1255 строк, runtime-сервис контрактов.
- ContractServer — 201 строки, серверная часть.
- MarketServer — 96 строк, авто-сбор MarketConfig из сцен.
- MarketWindow — 2002 строки, UI.
- ShipCargoConsole — 646 строк, UI + сервер (ShipCargoServer 453 строки).
- ContractDto, ContractSaveData, MarketSaveData, ShipCargoResultDto — DTO слой.
- Persistence: IPlayerDataRepository + PlayerPrefsRepository + ServerFileRepository.
- Recovery protocol для PlayerPrefs (T-MKT10).
- Recovery и migration persistence (T-MKT07).
- Terminal history ограничена (T-MKT09).
- Statuses: пустые vs отсутствующие snapshot (T-MKT08).
- Receipt flow заблокирован неполный (T-MKT06).
- Delivery-контракты защищены списанием груза (T-MKT05).
- Network feedback унифицирован (T-MKT03).
- Contract integrity исправлена (T-MKT02).
- Contract timer настройки (T-TRADE02).
- Dead RPC рынков и контрактов удалены (T-MKT12).
- Мутации экономики сериализованы (T-MKT11).
- MarketZoneEditor — inline редактирование MarketConfig из сцены.
- Custom Editors: GlobalBuyPriceConfig, TradeDatabase, MarketConfig, MarketZone.

---

## 5. Документация — что документировали

### 5.1 В docs/dev/ (родительская папка)

- `RETRO_FO_LIGHT_2026-09-14.md` — ретроспектива FO-интеграция + свет (57572dc0 → 26f1cb75). Создан в `2e998329`.
- `NPC_SHIP_ROUNDTRIP_REVIEW_2026-09-17.md` — review NPC-кораблей roundtrip.
- `PICKUP_BOB_INTEGRATOR_FIX.md` — фикс pickup bob integrator (trampoline bounce).
- `DESIGN_T-ADM-00-01.md` — L1 admin-panel design doc + final decisions D1-D6.
- `T-Q22_QUEST_TRACKER_ALL_OBJECTIVES.md` — квест-трекер.

### 5.2 В docs/world/floatingorigin/

- `09R_BAKE_TOOL_GAP.md` — документация недостающего bake tool.
- `09S_NEW_CONTENT_PIPELINE.md` — пайплайн нового контента (161 строка).
- `README.md` — обновлён.

### 5.3 В docs/world/compas/

- `WORLD_COMPASS_NORTH_2026-09-17.md` — дока компаса (76 строк). Переехала из docs/Ships/.

### 5.4 В docs/world/distantfocus/

- `DESIGN_farfocus.md` — дизайн farfocus (81 строка).
- `README.md` — читаемый README (139 строк).

### 5.5 В docs/world/optimization/

- `IMPLEMENTATION_PLAN.md` — план внедрения (119 строк).
- `ITERATIONS.md` — итерации (60 строк).
- `LOD_VIEW_DISTANCE_DESIGN.md` — дизайн ViewDistance (97 строк).
- `README.md` — обновлённый (28 строк).

### 5.6 В docs/world/parom_road/

- `00_PAROM_DESIGN.md` — дизайн парома (153 строки).
- `01_PAROM_V2_PLAN.md` — план V2 (192 строки).

### 5.7 В docs/world/terrain/

- `ITERATIONS.md` — итерации террейна (58 строк).
- `README.md` — обновлённый (55 строк).

### 5.8 В docs/world/world_map/

- `00_CHART_CONCEPT_2026-09-17.md` — концепт карты (113 строки).
- `01_M_INTERFACE_2026-09-17.md` — интерфейс M (54 строки).
- `02_TRACKS_2026-09-17.md` — треки (42 строки).
- `03_MARKS_2026-09-17.md` — метки (47 строки).
- `04_BEARING_2026-09-17.md` — пеленг (30 строки).
- `05_PANZOOM_2026-09-17.md` — пан-зуум (36 строки).
- `06_PERSIST_2026-09-17.md` — персист (39 строки).

### 5.9 В docs/world/Calendar/

- `SEASONAL_TEMPERATURE.md` — сезонная температура (56 строк).
- `Day-Night/DAILY_HUE_VARIETY.md` — суточный hueShift (70 строк).

### 5.10 В docs/world/admin_tool/

- `00_ADMIN_PANEL_SURVEY.md` — обзор панели (339 строк).
- `01_TOGGLE_INVENTORY.md` — инвентарь (131 строка).
- `L1_DESIGN.md` — L1 дизайн (131 строка).

### 5.11 В docs/Ships/

- `ENGINE_POWER_STATE.md` — состояние двигателя (5 строк).
- `ITERATIONS.md` — итерации корабля (422 строки).
- `Key-subsystem/00_OVERVIEW.md`, `21_SHIP_OWNERSHIP_MODEL.md`, `28_KEY_ARCHITECTURE_REVIEW.md` — обновлены.
- `Modul_system/02_REPAIR_MANAGER.md`, `cargo_system/CARGO_OWNERSHIP_DESIGN.md`, `customisation/00_SUMMARY.md`, `damage_subsystem/00_DESIGN.md`, `UI/HUD/00_OVERVIEW.md` — обновлены.
- **Тикеты T-SHIP-DOC01…10** — 10 файлов документации по коду корабля.
- `SHIP_CODE_REVIEW_2026-09-18.md` — код-ревью (167 строк).
- `SHIP_FIX_PLAN_2026-09-18.md` — план фиксов (99 строк).
- `SHIP_REFACTOR_PLAN_2026-07-21.md` — план рефакторинга (11 строк).

### 5.12 В docs/Ships/fix/ (12 тикетов фиксов)

T-SHIP-FIX01…12 — по 30…71 строка каждый.

### 5.13 В docs/Character/Other/ (суммарно)

- `RETROSPECTIVE_2026-08-26.md` — ретроспектива (9770 строк).
- `RETROSPECTIVE_v0.1.31-v0.1.45_2026-09-08.md` — ретроспектива (12514 строк).

---

## 6. Файлы конфига/сцены — изменения

- **WorldScene_0_0.unity** — +121706 / − изменений (большая переработка: террейн, паром, руины, поселения, фермы, рынки, NPC-корабли, compass rose).
- **BootstrapScene.unity** — +308 / − изменений.
- **ViewDistanceConfig.asset** — новый конфиг.
- **GlobalMotionPilotProfile.asset**, **GlobalMotionPilotSceneCatalog.asset** — FO пилот профиль.
- **LightingSettings_World.asset** — освещение мира.
- **ProjectC_URP.asset, ProjectC_URP_Renderer.asset** — URP-настройки.
- **DayNightProfile.asset, DayVolumeProfile.asset, FocusVolumeProfile.asset** — профили.
- **CalendarConfig.asset** — календарь.
- **NpcShipSchedule_Light/Medium/Heavy/HeavyII_Default.asset** — расписания NPC-кораблей.
- **ShipCrewManifest_*.asset** — манифесты крейса (17 файлов).
- **MarketConfig_Primium_farm_0_0.asset** — 910 строк, фермерский рынок.
- **MarketConfig_Primium_farm_0_1…0_4.asset, farm_1_1.asset** — фермерские рынки.
- **MarketConfig_Road to Secund/Tertius/Quartus.asset** — дорожные рынки.
- **GlobalBuyPriceConfig.asset** — 354 строки, глобальные цены.
- **ContractCatalog.asset** — 270 строк, каталог контрактов.

---

## 7. Итог

За период с v0.1.60 по v0.1.85 (7 дней, 41 коммит) сделано:

| Подсистема | Статус |
|---|---|
| Паром (T-PAROM-01/02/03) | ✅ Реализовано: менеджер маршрута, кабинка, тросы, SaggedPoint |
| Bake Pilot Catalog (FO09S) | ✅ 1-клик скан + diff + маркер + DryRun |
| ViewDistance presets (LOD01) | ✅ Конфиг, ESC-Видео, fogScale |
| Veil → CLOUD_system (FO09P) | ✅ Вынесен, FO-адаптирован, документирован |
| Террейн (TERR-01/02) | ✅ Единый террейн, генеративные руины (60 хамлетов, 496 инстансов) |
| Корабль: server-auth fixes (FIX01…12) | ✅ 12 фиксов, DOC01…10, код-ревью |
| Мирная карта (WORLD-MAP) | ✅ Полноценная морская карта, метки, треки, легенда, de-sea |
| Compass Rose (WORLD-COMPASS) | ✅ Север через розу ветров, HDG в HUD |
| Admin-панель (ADM-00…08a) | ✅ F12 окно, 8 tabs, god/speed/teleport, PerfHUD |
| Calendar / DayNight | ✅ Seasonal temp + diurnal swing, daily hueShift |
| NPC-корабли ревью (NS) | ✅ Вердикт, план курсирования, Berthing, Avoidance, Departure, Pads |
| Крейс (T-CREW) | ✅ Gorgona crew: manifest, anchors, spawner, captain config |
| Esc-Эффекты (ESC05) | ✅ Секция Эффекты |
| DistantFocus (DF-001) | ✅ 10 ревизий: Bokeh DoF, autofocus, auto-diaphragm, far-field, debug |
| Engine Visual (ENG02) | ✅ Pivot + visuals, WASD, FBX fix |
| ShipPartShake (SHAKE) | ✅ SmoothDamp, синусоида, дребезг |
| RepairManager (REPAIR) | ✅ Цены, recall, observation cam, teleport, persistence |
| Экономика (MKT) | ✅ ContractCatalog, MarketConfigCollector, MarketWindow, ShipCargoConsole, persistence, recovery |
| Фермерское поселение 0-1 | ✅ 12 MarketZone, DockStation_Farm_0_0, MarketConfig_Primium_farm_0_0 (+ ещё 6 ферм), маршруты NPC |
| Корабль Heavy | ✅ Preset: thrust=100k, vertical=50k, massMultiplier=25 |
| Корабль Medium | ✅ Preset: thrust=30000, yaw=150000, vertical=30000 |
| Корабль Light / яхта | ✅ Preset правки, ходьба по палубе, Key ItemData |

**Документация:** ~30 новых/обновлённых .md файлов в docs/dev/, docs/world/, docs/Ships/, docs/Character/.

**Версия:** 0.1.85 — текущий HEAD.

---

*Диапазон: `2e998329` (v0.1.60) → `28772325` (v0.1.85). Сгенерировано по git-прошам и файлам проекта.*
