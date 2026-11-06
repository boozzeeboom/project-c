# Ретроспектива: v0.1.91 → 0.2.0

**Период:** 29.09.2026 → 06.11.2026 (39 суток)
**Начало:** `c7e11be9` (v0.1.91, Camera adaptive fix)
**Конец:** `0c45f778` (HEAD, T-HOUSE01)
**Коммитов в диапазоне:** **366**
**Файлов изменено:** **1950** (+~302k строк; C# +78.8k в 188 файлах; доки +172k в 600 файлах)
**Маркер версии в сцене:** `BootstrapScene.unity` → `0.1.94` на HEAD; целевая версия по родмапу/changelogs — **0.2.0**
**Сгенерировано:** 06.11.2026, по `git log c7e11be9..HEAD` + git diff --stat + дизайн-нотам + арт-архиву `Q:\Yandex.disk\Yandex.Disk\Project-C`.

---

## 1. Метрики

| Метрика | Значение |
|---|---|
| **Коммитов** | **366** (`git rev-list --count c7e11be9..HEAD`) |
| **Автор** | 1 (моно-сессия) |
| **Файлов изменено** | **1950** |
| **Строк добавлено/удалено** | **+~302 000 / −~?** (C# +78.8k в 188 файлах; доки +172k в 600 файлах) |
| **Подсистема-лидер** | PRIMUM (город/материалы/коллайдеры/двери/свет/HLOD) — **182 коммита** |
| **Culling/Visibility** | 29 коммитов (T-CULL01 0…H8 + final) |
| **Фермы** | 14 коммитов (0-0…0-4 + MEGA_REFINERY_1) |
| **NPC-корабли** | 9 коммитов (визуальный грейфт + навмеш) |
| **Паром** | 7 коммитов (T-PAROM-23…27) |
| **Камера** | 9 коммитов (T-CAM22…25 + PERF/CAM) |
| **Загрузка мира** | 3 коммита (T-LOAD01) |
| **UIv2** | 63 коммита (MainMenu + Cloudy Navigator + иконки) |
| **T-HOUSE01** | 3 коммита (нумерация домов + discovery) |
| **Инфра** | 1 коммит (пакеты/Addressables) + FO save-guard |

Вывод одной строкой: период = **наполнение мира контентом 0.2.0**: город Примум доведён до merged-префабов/коллайдеров/HLOD/процедурных материалов, фермы 0-0…0-4 + мегаферма, NPC-корабли получили визуал и rebake навмешей, паром и камера стабилизированы, MainMenu и Cloudy Navigator портированы (UIv2), свет и каллинг доведены до финальной приёмки, добавлена нумерация домов и discovery на карте.

---

## 2. Что сделали — по подсистемам

### 2.1 PRIMUM — город, материалы, коллайдеры, двери, свет, HLOD

**Материалы.** Переход на процедурные шейдер-графы `M_PC_*` (Bronze, Parquet, Wood, Wood_White, Glass, Marble, Tiles, Brick, Cobble, Concrete, Plate, Steel, Fabric, Plastic и др.). Пилотный чанк S9_TRD-S2 переведён в Y-up/метры с тангенсами из нормали; маппинг `PrimumMaterialMap` (меню 380–385), 31 цветовой вариант, правила по имени объекта. Закрыт остаток: **0 слотов URP/Lit в городе и на фермах** (Э3.1). Тюнинг бампа/полос пропускания, точный `Normal From Height`, `PC_PositionOS`.

**Коллайдеры.** BASE (плиты, марши, мощение из реальной геометрии), дома (MeshCollider из RAW вместо 7182 боксов → 177 точных коллайдеров; затем боксы из RAW с проёмами — 10 355), ADMIN (92 внутренние + главный вход из `ADMIN_FOR_COLLIDER`), фермы. Тулза `SettlementColliders` апгрейжена (slice, merge, capsules, ship mode). Честный статус попыток зафиксирован в доках.

**Двери.** Массовая расстановка (2148 одиночных префабов, затем 2363 дворцовых/двустворчатых/узких/широких через `DoorPrefabBuilder` + `PrimumDoorPlacement`), корректная ориентация, коллайдеры + `DoorController`, префабы в git. Главный вход ADMIN пересобран (`PM_ADM_ENTRANCE_PORTAL`).

**Свет.** 5 888 мешей-светильников, 0 Light-компонентов → источники + пул ближайших + группы день/ночь с множителем (1453), Forward+ тест вместо лимита 4 источника на объект, настройка яркости дня/экспозиции (T-LIGHT10), группы с переопределением range/тип/угол/цвет/тени/bounce, случайное мерцание каждой лампы. Э4 «лампы» закрыт по замерам.

**HLOD.** Детерминированный бейк L1+L2 для PRIMUM (23 блока: 5.41M → 267k трианглов), L2 — 8×4 ortho-atlas, переключение в `VisibilityDirector` через `forceRenderingOff` (L1 >3 км, L2 >12 км, 10% гистерезис, FO-safe корень под WorldRoot). Rebake byte-identical.

**Итог.** Город превращён из RAW-импорта в оптимизированный набор merged-префабов/коллайдеров/HLOD с процедурными материалами; F8/F9 PASS.

Метрики PRIMUM (из архива `Q:\Yandex.disk\Yandex.Disk\Project-C\models\primum`):
- 27 арт-чанков (25 жилых + BASE + ADMIN), 23 HLOD-блока.
- `PRIMUM_COLLIDERS` = 10 811 (дома 10 355 / BASE 74 / ADMIN 382).
- Двери: 2 363 размещённых инстанса (2 051 одиночных + 110 двойных + 133 админ + 202 дворцовых); `PRIMUM_DOORS` 4 949 рендереров, 4 432 box-коллайдера.
- Свет: 5 888 мешей-ламп → 2 087 источников (1 557 город + 530 админ), пул ≤96 в радиусе 140 м.
- Рендер-гранулярность: 19 944 → 1 207 рендереров (761 material-batched), 26 566 → 1 806 слотов, 5.44M трианглов.
- Y-up rebase: 22 чанка (20 жилых + BASE + пилот S9_TRD-S2); пилот 769 мешей, 1 149 985 вершин, вырожденные тангенсы 274 126 → 0, max Δ 3.15e-5 м, вердикт 1:1 PASS.
- Материалы: 105 имён → 45 целей + 31 вариант; слоты 24 378 → 7 081; итог 53 материала (DoD ≤60), 0 `URP/Lit` в городе и на фермах.

### 2.2 Фермы

Фермы 0-0, 0-2 (TIMBER), 0-3 (RICE), 0-4 (CEREAL) и MEGA_REFINERY_1: палитра `M_PC_*`, двери и лампы префабами, мерж статики (90–121 батчей), слой PRIMUM, коллайдеры (в т.ч. Farm 0-0 rebuild, MEGA_REFINERY_1: 89 мешей + door plugs + floor-gap patch).

| Ферма | Источник | Результат |
|---|---|---|
| 0-0 (spacebunny_1) | 84 merged batches (77 MAIN + 7 LAMP), 502 120 трианглов 1:1 | 104 двери (96 → `PM_DOOR_*`, 8 SHAFT), 101 светильник `PRIMUM_FARMLIGHT` |
| 0-2 TIMBER | 4 940 мешей → 79 merged + 30 дверей + 43 лампы | 387 BoxCollider, 30 DoorController, префаб 14.7 → 1.71 MB |
| 0-3 RICE | 962 мешей → 90 merged + 33 дверей + 24 лампы | 469 коллайдеров, 90 merged asset (22.72 MB) |
| 0-4 CEREAL | 1 766 мешей → 121 merged + 35 дверей + 76 ламп | 521 коллайдер, 121 merged asset (30.32 MB), 0 орфанов |
| MEGA_REFINERY_1 | 10 106 мешей → 32 merged (`PM_FRM_*`, 1.27M → 32 батча) | 89 коллайдеров (482 518 трианглов, 0 дегенератных) + 6 COL_FIX, 500 door-префабов (62 single / 429 double), 278 ламп, 9 `M_GIANTFARM_M_PC_*` вариантов |

Батчинг: `CombineMeshes` по (группа × материал), cap 60 000 вершин/меш, `mergeGap 0.1`, `mergeSubMeshes=false`, `RecalculateBounds()`. Вывод в `Prefabs/City/Farms_Merged/PM_FRM_<GROUP>_<MAT>_NN.asset`. Коллайдеры: concave MeshCollider (convex=false, CookForFasterSimulation + EnableMeshCleaning + WeldColocatedVertices + UseFastMidphase) для ферм; `BuildSettlementColliders` (minWorldSize 0.25, minThickness 0.5, capsule-токены для PIPE/REBAR/TUBE, slab-slicing 1.0). Двери: `PM_DOOR_*` инстансы, растянутые под проёмы через `PrimumDoorPlacement`, `DoorController` на layer `PRIMUM_DOORS` (10) / `Primum_farms_DOORS`. Свет: `PRIMUM_FARMLIGHT`, Point, shadows=None, enabled=false, layer 8, ranges 6–14, intensity 1.6–3.0. Static flags = 0 на всех объектах ферм (статический батчинг выключен; GRD/visibility рантайм). Мегаферма пока не размещена в сцене (FO-пайплайн pending).

Фермы стали полигоном для навигации NPC (airway-боксы primum-farm) и доминантой перфа (`OverlapSphere r=1000`).

### 2.3 NPC-корабли: визуальный грейфт + навмеш

`SHIPS/NPC`: грейфт визуала эталонов и пере-бейк навмешей палубы **17 кораблей** (Heavy 6, Medium 6, Light 2, HeavyII 3). `NpcShipVisualGraft` заменил стабы `Platform` на визуалы корпусов; `NpcDeckNavRebaker` пересобрал навмеш палубы в не масштабированных локальных единицах, размеры агента пересчитаны по осям корня, `agentTypeID` остаётся Humanoid, геометрия собирается вручную из коллайдеров префаба (`NavMeshBuilder.CollectSources` возвращает 0), `DeckNavSurface.localPosition=0`, `m_LayerMask=Default|ShipDeck (65)`, Surface выключен. CrewAnchors: XZ PilotSeat на высоте палубы, 5 точек на кольце 0.5 м, имена сохранены для `ShipCrewSpawner`. Verify: трианглы 397/1171/16/29 по классам, max R 0.50, point→deck Δ 0.00, mask 65, Surface off. Дефект runtime proxy-агента на корнях (6, 0.9, 12) задокументирован отдельно.

`SHIPS/LOR` («Лорейн», класс S) и `SHIPS/ALB` («Альбатрос»): палитра `M_PC_*`, мерж статики, LOD. `SHIPS/PAROM`: визуальный merge парома + паром-фермы, сгенерированные `M_PC_*` ассеты. `T-SHIPPIPE01`: ремонт префабов light/medium и процедурная палитра кораблей. `SHIPS/DOORS`: `EnsureCollider()` fallback вместо `RequireComponent(Collider)`. Док `DOCS/NPC-SHIP`: контракт локального пространства навмеша палубы.

Связь с оптимизацией: shadow budget для `Ship_havyII_base` — LOD0 1538 / LOD1 235 / LOD2 104 DC (замеры `ship_dc`), scope ограничен тяжёлым пилотом.

### 2.4 Паром / камера / загрузка

**Паром (T-PAROM-23…27):** Trolley Prefab приоритетнее запечённой кабинки; предвизуал кабинки на станциях маршрута (editor, `HideFlags.DontSave`, коллайдеры выключены, без `ParomTrolley` в рантайме); клиентская интерполяция `s` (кабинка, персонаж, камера едут плавно); синк физики после переноса кабинки + приоритет CharacterController в зонде платформы; гашение краёв маршрута в метрах (кабина больше не шла по прямой над провисающим тросом). В `WorldScene_0_0` — 2 `ParomRoute`.

**Камера (T-CAM22…25 + PERF/CAM):** SpringArm — цель через историю сэмплов + anti-pop относительно цели; камера едет с платформой 1:1, лаг только по локальному движению; история сэмплов цели копится всегда + численная модель палубы-физики; ревизия форматирования секции Moving Platform + честный `platStepMax`; камера на высокой скорости не отстаёт и не «прыгает» (админ-бег/noclip: отставание 12.88 → 6.43 м, снапов 9 → 0); не строить payload эвиденс-пробника каждый кадр (горячий путь).

**Загрузка (T-LOAD01):** экран загрузки мира — активация сцены после 0.9 и кадры на отрисовку; MainMenu отключает фоновую предзагрузку мира; предзагрузка мира за меню; вход в игру с первого клика.

### 2.5 UIv2

**UIv2 Milestone 1:** туллинг, дизайн-система, FigCraft pipeline, sandbox-пример. **Визуальный порт прототипа:** цвета v4-2, шрифт Inter, hover-переходы, фикс `EnsureBuilt`. **Milestone 1.5:** agent tooling, интеграция Rive, шейдеры, VFX, новые экраны.

**MainMenu (порт мода v4-2):** шаги 1–9 — палитра и процедурные листы дыма, 3D-фон (atmosphere: небо, завеса, тросы с паромом, пыль, сутки), USS/дерево (197 правил, 221 элемент), поле дыма (пул пуфов gather/disperse), выдувание букв, алтиметр-лента (29 тиков), поведение окна (120 проверок), блоки проекта на `.pc-proj-layer`, префаб в BootstrapScene, вынос из-под игнора. Фиксы: читаемость неактивных вкладок, крестик в настройках, приглушение белых облаков, удаление `filter: blur()` (каскад «texture not allocated»), гамма-эквивалентное смешивание атмосферы, Inter, свёртка панелей, ускорение суток, второй запуск меню, вспышка при наведении.

**Cloudy Navigator («Облачная карта»):** порт шагов 0–9 — DTO-имена, каркас окна, `CnDataFeed`, USS оболочки, слой движения `CnMotion` (пул 48), `CnPreviewStage` (своя камера на слое 30, RenderTexture), `CnActions` (квесты, контракты, перенос в зоне обменника), префаб `CloudyNavigatorUI` в BootstrapScene. Далее A7–A13: слоты экипировки, превью персонажа со студийным светом/фоном, изоляция превью от сцены, тросы графа связей с провисом, 3D-вьюер снят → фигура рисуется средствами UI Toolkit, заметки-стикеры/тросы/рваный край, календарь с заметками, кнопка внешности, управление графом. C3-1: внутренние ореолы, канты, кольца. Навыки: изучение/забывание/привязка к слотам, шесть боевых слотов, два графа, входы в календарь/связи/корабль/багаж/задания.

**Иконки:** импорт 168 PNG в Sprite, расстановка по Sprite-полям ассетов (батчи B1–B6, реестр групп, контекст-промпты). Всего в `Assets/_Project/UI/Icons/` — **379 файлов (169 иконок-ассетов) в 17 категориях** (Actions, Categories, Combat, Contracts, Crafting, Equipment, Factions, Gathering, Knowledge, Map, NPC, Quests, Ship, Skills, Stats, Trade, Weapons).

**Generated_Models.** `Assets/Generated_Models/`: 1 025 файлов — `_Legacy_PreAugust` 39, `01_Characters` 399 (Archetypes 158, City 14, CreationChem 16, Engineers 16, Explorers 16, Guilds 80, Medics 24, Pilots 48, Winter 27), `02_Armor` 331 (Chest 52, Legs 104, Shoulders 45, Backpacks_Airtanks 32, Feet 47, Hands 19, Head_Helmet 16, Utility_Belts 16), `03_Weapons` 148, `04_Gadgets` 64, `05_Residents_EarlyOct` 44.

### 2.6 Culling / T-CULL01

Stage 0 baseline + fixes → H1 ремонт префабов кораблей → H2/H2b замеры DC для `Ship_havyII_base` (LOD0 1538 / LOD1 235 / LOD2 104) + shadow budget → H3 PRIMUM_DOORS слой, layer cull distance, shadow budget (P1 27131 → 6094, −77.5%) → H5/H5b city shadow budget 200 м (P1 3047 → 2423, −20.5%) → H5c фикс union камер (AtmosphereCamera на origin держал 105 casters, P1 2811 → 2394) → H6 `VisibilityDirector` (groups/cells, multi-camera union, sub-pixel forceRenderingOff 2px / 12% гистерезис / 3 Гц, FO-safe offsets; P1 2394 → 2378) → H7 HLOD L1+L2 для PRIMUM (L1 46–48 DC, L2 2 DC, P1 2341 = hlod off) → H8 door culling под GRD через VisibilityDirector.

Финальная приёмка: **Stage 8 PASS, P1 2229 DC**. L1–L6 свет закрыт (fade in ALIC 0.15 с принят, fade-out 0.30 с отклонён hold-gate). Runbook и acceptance зафиксированы в `docs/dev/T-CULL01_FINAL_ACCEPTANCE_REPORT.md`.

### 2.7 T-HOUSE01

Нумерация домов Примума + discovery на карте (WorldScene_0_0): 3 коммита, интервал discovery уменьшен 1.5 → 0.5 с, убран debug-лог. `HouseMarker` (MonoBehaviour), `HouseDiscovery` + `HouseDiscoveryStore` (per-player список открытых домов, радиус 15 м, save/load, Floating Origin hook), `ChartWindow` — слой «Дома» (открытые) + «Фермы» (всегда), `HouseNumberingGenerator` (Editor) — парсит `PRIMUM_DOORS`, группирует по дому, создаёт `HOUSE_SIGNS` (534 маркера с плавающим текстом). Документация: `docs/world/world_map/house_numbering/00_HOUSE_NUMBERING_DISCOVERY.md`.

### 2.8 Инфра

- `INFRA`: пакеты (`rive-unity`, autopilot reorder), BootstrapScene (ALIC fade + preload), terrain tweak, Addressables hash.
- `T-FO-SAVE-GUARD-FIX`: ребейс не отравляет сейв игрока.
- FO bake tools: обновление, удаление items.
- Дополнительно: `T-CHAR-UVRB` (универсальная адаптивная переразвёртка UV + перепечка материалов/BaseColor AI-персонажей в Blender headless), `T-CROWD-00` (теория подключаемых мозгов толпы), `T-ENG02` (multi-pivot EngineThrusterVisual).

---

## 3. Документация — что документировали

| Папка | Что добавлено/обновлено |
|---|---|
| `docs/world/primum_city/` (новое) | ROADMAP Э1…Э8, коллайдеры/двери/свет/материалы/Y-up/HLOD/culling research, reports по чанкам |
| `docs/world/primum_city/09_MATERIALS/` | material_map.csv (105 → 45 + 31), reports mesh_rebase, Y-up подмена |
| `docs/world/primum_city/10_CULLING_RESEARCH/` | execution plan, beam comparison, decisions |
| `docs/dev/` | T-CULL01_* (stage reports/briefs/final acceptance), T-LIGHT16–21, T-PAROM-23–27, T-CAM22–25, T-HOUSE01, NPC_DECKNAV_REPORT, NPC_VISUAL_GRAFT_REPORT, SETTLEMENT_COLLIDERS_TOOL |
| `docs/world/parom_road/` | 00_PAROM_DESIGN, 02_PAROM_RIDE_POSTMORTEM (обновлён) |
| `docs/Ships/` | NPC decknav contract, door/ship visual reports |
| `docs/UI/` (новое) | документы порта MainMenu/Cloudy Navigator (мод v4-2), 2D-иконки (168 PNG B1–B6) |
| `docs/Art/` | правила стойки и силуэта для референсных листов персонажей |
| `docs/Materials/` | Steel v2 shader graph, procedural materials docs |
| `Assets/_Project/Docs/` | итерации, связки |
| `iterations.md` | записи 04–06 ноября (T-PAROM-23…27, T-CAM22…25, T-LOAD01, T-HOUSE01) |

Принцип периода, унаследованный от паромного постмортема: **сначала замер/лог, потом код; каждая разборка лога — в ITERATIONS с номером лога и выводом**.

---

## 4. Файлы конфига/сцены

- **`WorldScene_0_0.unity`** — основная сцена периода: PRIMUM (merged-префабы, коллайдеры, двери, свет, HLOD), фермы 0-0/0-2/0-3/0-4, паромные маршруты (2), NPC-корабли (17), airway-боксы, террейн-правки. Крупный дифф по PRIMUM (Э3.1 Y-up, Э4 свет, Э7 коллайдеры, Э8 двери).
- **`BootstrapScene.unity`** — versionText 0.1.91 → 0.1.94; MainMenu префаб (UIv2), CloudyNavigatorUI префаб, ALIC fade + preload, Addressables hash; NetworkManager не тронут.
- **Префабы:** `Prefabs/City/Doors/` (12 door-префабов), `Prefabs/City/Farms_Merged/` (PM_FRM_* merged assets), `Prefabs/Ships/` (NPC ship prefabs с визуалом), `Prefabs/ThirdPersonCamera.prefab` (zoomStepPercent 50→0.2).
- **Ассеты террейна:** точечные правки, бэкапы.
- **Generated:** `Generated/HLOD/` (23 L1 + L2 impostor assets), `Generated_Models/` (1025 файлов).

---

## 5. Итог: статус на 0.2.0

| Подсистема | Статус на 0.2.0 |
|---|---|
| PRIMUM (материалы/коллайдеры/двери/свет/HLOD) | ✅ Город оптимизирован: merged-префабы, процедурные материалы (0 URP/Lit), 10 811 коллайдеров, 2 363 двери, 2 087 источников света с пулом/группами, HLOD L1/L2 (5.41M → 267k), Y-up rebase 22 чанка, F8/F9 PASS |
| Фермы | ✅ 0-0…0-4 + MEGA_REFINERY_1: материалы/двери/свет/коллайдеры/мердж (90–121 батчей); мегаферма pending FO-размещение |
| NPC-корабли (визуал + навмеш) | ✅ Грейфт визуала 17 кораблей, rebake навмешей, LOR/ALB, навмеш-контракт; shadow budget HeavyII |
| Паром / камера / загрузка | ✅ Интерполяция `s`, синк физики, SpringArm на платформе, T-LOAD01 (экран загрузки + предзагрузка за меню), вход с первого клика |
| UIv2 (MainMenu + Cloudy + иконки) | ✅ Milestone 1/1.5, порт MainMenu и Cloudy Navigator, 168 иконок (379 файлов), 17 категорий |
| Culling T-CULL01 | ✅ Final acceptance PASS, P1 2229 DC, runbook; свет L1–L6 закрыт |
| T-HOUSE01 | ✅ Нумерация домов + discovery на карте (534 маркера, радиус 15 м) |
| Инфра | ✅ Пакеты (rive-unity), FO save-guard, bake tools, Addressables hash |
| Floating Origin | ⚠️ Ребейз работает (F8/F9 PASS на PRIMUM/фермах/кораблях), но `runtimeRebaseReadiness = NOT_READY` (контракты/partial runtime evidence) — без изменений с 0.1.91 |
| NPC-навигация | ⚠️ LOS 97% (AIR03e) — без изменений с 0.1.91; терминальная боль (доки) — следующий фокус |

**Открытые хвосты (честно):**
- Терминальная боль NPC (доки — следующий фокус после магистралей).
- B2-персистентность hotspot отложена (сейвы).
- Маска `OverlapSphere r=1000` — доминанта CPU зон.
- Стартовый спайк хоста 1370 мс не чинен.
- Медиана PlayerLoop в замере 2 хуже (нужен повтор в том же сценарии 20+ кораблей).
- Мегаферма pending FO-размещение в сцене.
- Ручные Play Mode-проверки по навыку `manual-playtest-only` — за пользователем (агент помечает NOT RUN).
- Версия в сцене на HEAD — 0.1.94; 0.2.0 — целевая (roadmap/changelogs) — синхронизировать при финальном коммите.

---

*Диапазон: `c7e11be9` (v0.1.91) → `0c45f778` (HEAD). 366 коммитов, 1950 файлов. Версия в `BootstrapScene.unity` = 0.1.94; целевая 0.2.0.*