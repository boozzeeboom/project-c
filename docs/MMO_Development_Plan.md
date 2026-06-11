# План разработки ММО "Project C: The Clouds" на Unity

**Последнее обновление:** 11 июня 2026 г. | **Текущая версия:** `v0.0.22-exchange-system-complete`

> **Что нового с прошлого обновления (10 июня 2026):** **Resources Exchanger (обменник ресурсов) — MVP завершён.** Мост между двумя системами предметов: pickable (инвентарь, 1 кг) ↔ boxed (склад, 100 кг). 4-я вкладка «Обменник» в MarketWindow. Pack: 100 осколков → 1 ящик на складе. Unpack: 1 ящик → 100 осколков в инвентарь. InventoryWorld.MAX_SLOTS увеличен до 1000 (конфигурируется в инспекторе). Исправлены: спавн scene-placed NetworkObject в BootstrapScene (OnServerStarted), PushSnapshot инвентаря и склада после каждой операции, группировка предметов по itemId в UI. 5 тикетов T-E01–T-E05, ~30 ч работы. См. `docs/Markets/Resources_exchanger/01_ANALYSIS.md`.
>
> **Предыдущее обновление (10 июня 2026):** **Crafting (крафт-система) — MVP завершён.** Подойти к станции → F → окно → выбрать рецепт → добавить ингредиенты (+1/+Все) → Начать крафт → таймер (10с) с ProgressBar + тост + анимация станции → Готово → Забрать → предмет в инвентарь. 2 станции в WorldScene_0_0: [CraftingStation_Table] (3 рецепта: медный/железный слиток, ключ корабля) и [CraftingStation_Shipyard] (1 рецепт). Подписки на несколько станций, независимая работа. Инвентарь: списание/выдача через InventoryWorld. 9 тикетов T-C01–T-C07c, ~12-15 ч работы. См. `docs/Crafting_system/ROADMAP.md`.

---

## Этап 0: Подготовка окружения ✅ ЗАВЕРШЁН
**Цель:** Настроить рабочую среду и базовую архитектуру проекта.

### Задачи:
1. **Установка ПО:** ✅
   - ✅ Unity 6 с URP
   - ✅ Netcode for GameObjects (NGO)
   - ✅ Visual Studio Code / IDE с C#
   - ✅ Git LFS для работы с ассетами

2. **Настройка репозитория:** ✅
   - ✅ Структура папок `Assets/_Project/`
   - ✅ `.gitignore` + `.gitattributes` (LFS)
   - ✅ Ветка `qwen-dev` на GitHub
   - ✅ `.qwenignore` для оптимизации контекста

3. **Базовая архитектура:** ✅
   - ✅ Unity Netcode for GameObjects (вместо Mirror)
   - ✅ Прототип серверной части: .NET 8 Console App
   - ⏳ Документация протокола клиент-сервер (позже)

---

## Этап 1: Прототип ядра геймплея 🔄 В ПРОЦЕССЕ
**Цель:** Реализовать базовые механики в оффлайн-режиме.

### 1.1 Мир и генерация ✅
- ✅ Процедурная генерация горных пиков (шум Перлина)
- ✅ Мелкие острова между пиками
- ✅ Система облаков: 3 слоя, 890+ облаков, движение, анимация формы
- ✅ Интеграция с WorldGenerator
- ✅ **Система штормов (Storm Cloud System):**
  - ✅ StormCloudGenerator — пул штормов (max 5), спавн по паттерну
  - ✅ CloudSpherePhysics — parting physics (сферы разлетаются при пролёте)
  - ✅ EventCloud — event-driven шторма через ServerStormManager
  - ✅ CloudLayerConfig — ScriptableObject с generator7.0 параметрами
  - ✅ RuntimeMeshSampler — runtime sampling меша для ParentMeshPath
  - ⏳ **Parent mesh pattern** — генерация сфер по поверхности меша (отложено)
  - ⏳ **Advanced physics** — collision между сферами (отложено)
  - ⏳ **Lightning VFX** — ParticleSystem для молний (отложено)
  - ⏳ **Runtime pattern loading** — Addressables для CloudLayerConfig (отложено)

### 1.2 Камера ✅
- ✅ WorldCamera — свободный полёт, телепортация к пикам (N/B/R/H)
- ✅ ThirdPersonCamera — орбитальная камера от третьего лица (персонаж/корабль)

### 1.3 Контроллер персонажа (пеший режим) ✅
- ✅ WASD — движение вперёд/назад + стрейф
- ✅ Мышь — вращение камеры
- ✅ Space — прыжок
- ✅ Left Shift — бег
- ✅ CharacterController + коллизии
- ⏳ **Ветер для персонажа** — взять за основу систему ветров кораблей (Сессия 3: WindZone, WindZoneData)
  - WindZone триггеры работают для персонажа (CharacterController входит в зону)
  - Влияние ветра на движение: снос при сильном ветре, сопротивление
  - Классы персонажей (если будут) → разная windExposure
  - Профили ветра: Constant, Gust, Shear — переиспользовать WindZoneData
  - Связь с GDD_02 (Погода) и GDD_01 (Физика персонажа)

### 1.4 Контроллер корабля ✅ ЗАВЕРШЕНО (Сессии 1-5_4: 12 апреля 2026)
- ✅ Smooth movement — Mathf.SmoothDamp для frame-rate независимого сглаживания
- ✅ W/S — тяга вперёд/назад (плавный ramp-up 0.3s)
- ✅ A/D — рыскание (поворот, smooth 0.3s, decay 1.0s)
- ✅ Q/E — лифт вверх/вниз (smooth 1.0s, max 2.5 м/с)
- ✅ Мышь Y — тангаж (нос вверх/вниз, smooth 0.7s, ±20°)
- ✅ Left Shift — ускорение (x2 тяга)
- ✅ Rigidbody + антигравитация (зависание)
- ✅ Стабилизация к горизонту (pitch + roll, auto при отсутствии ввода 0.5s+)
- ✅ ⭐ 4 класса кораблей: Light/Medium/Heavy/HeavyII (масса, скорость, маневренность)
- ✅ ⭐ Altitude Corridor System — коридоры высот, турбулентность, деградация
- ✅ ⭐ Wind & Environmental Forces — зоны ветра (Constant, Gust, Shear), снос корабля
- ✅ ⭐ Module System — ShipModule/ModuleSlot/ShipModuleManager, совместимость, энергия
- ✅ ⭐ Fuel System — расход/регенерация, дозаправка L, stall при fuel < 10
- ✅ ⭐ Meziy Passive/Active/Overheat — MODULE_MEZIY_PITCH/ROLL/YAW/THRUST
- ✅ ⭐ Meziy Status HUD (F4) — индикаторы 🟢🔵🔴, прогресс-бары перегрева/кулдауна
- ✅ ⭐ MODULE_MEZIY_THRUST — Shift+W (рывок вперёд) / Shift+S (торможение)
- ✅ ⭐ MODULE_ROLL — разблокировка крена (Z/X), force = mass * 0.2f
- ✅ ⭐ MeziyThrusterVisual — URP-совместимые частицы, авто-создание
- ✅ ⭐ ShipDebugHUD (F3) — debug overlay: fuel, speed, meziy state, roll
- ✅ ⭐ Co-op пилотирование — несколько игроков, усреднение ввода (NetworkBehaviour)
- ✅ ⭐ **Ship Key subsystem (R2-SHIP-KEY-001, 2026-06-06)** — физический ключ-предмет для запуска. `ShipKeyBinding` (MonoBehaviour) + `ShipKeyServer` (NetworkBehaviour hub) + `ShipKeyClientState` (singleton) + `ShipKeyToast` (UIDocument). F-key разделён на выход/посадку, pre-F RPC `RequestCanBoard` (1.5 сек timeout), server-side defense-in-depth guard в `SubmitSwitchModeRpc`. 3 ключа: `Item_Key_ShipLight/Medium/Heavy.asset`. `WorldScene_0_0.unity` — 3 KeyRod PickupItem + ShipKeyBinding на 3 ShipController. **MVP, deprecated** — superseded by MetaRequirement (см. §1.9). См. `docs/Ships/Key-subsystem/00_OVERVIEW.md`.
- ⏳ Рефакторинг кода — ShipController.cs v2.7 (1200+ строк), разделение на подсистемы

### 1.5 Переключение режимов (пеший ↔ корабль) ✅
- ✅ F — подойти к кораблю (< 5м) → сесть/выйти
- ✅ PlayerStateMachine — управление состояниями
- ✅ Камера адаптируется к режиму
- ✅ Проверка при выходе: корабль на земле ИЛИ скорость < 2 м/с
- ✅ ⭐ **Server-side key validation (R2-SHIP-KEY-001, 2026-06-06)** — F-посадка блокируется, если в инвентаре пилота нет нужного ключа. Pre-F RPC `RequestCanBoard` (1.5 сек timeout) → `ShipKeyServer.InventoryWorld.HasItem` → вернуть CanPlayerBoard result + reason. Defense-in-depth: повторная проверка внутри `SubmitSwitchModeRpc` (на случай bypass через прямой RPC). `ShipKeyToast` UI: "Нужен ключ X для корабля Y" + fade-out 3 сек. **Сейчас superseded by MetaRequirement** (см. §1.9) — единый generic механизм для всех Interactable-объектов.

### 1.6 Подбор предметов и инвентарь ✅
- ✅ **Подбор предметов** (E — ближайший в радиусе 3м)
- ✅ **Круговой инвентарь (TAB-колесо)** — UI Toolkit singleton, 8 секторов, группировка по типам
  - ✅ Hover-подсветка секторов (жёлтый при наведении)
  - ✅ Подсписок при >1 предмета в секторе
  - ✅ **Auto-spawn через RuntimeInitializeOnLoadMethod (Phase 4)**
- ✅ **CharacterWindow → таб "Инвентарь" (P-таб)** — детальный список с фильтрами по типу, поиском по имени
  - ✅ Single source of truth: оба UI читают **тот же** `InventoryClientState` (server-authoritative singleton)
  - ✅ Подбор → оба UI обновляются атомарно через `OnSnapshotUpdated`
  - ✅ Phases 0–7 ✅ done (2026-06-05), Phase 8 (cleanup `InventoryUI.cs` IMGUI-файл) — TODO
  - ⏳ `TryDrop / TryMove / TryUse` (InventoryServer) — TODO; UI кнопки есть, RPC не подключены
- ✅ **Открытие сундуков** — контейнеры с несколькими предметами через LootTable
- ✅ **Анимация сундука** — плавное вращение + масштаб при открытии
- ✅ **Вспышка секторов** — визуальная обратная связь при получении лута
- ✅ **Приоритет взаимодействия** — сундук > обычный предмет
- ✅ **Drop в мир (Phase 10, 2026-06-05)** — server-spawn PickupItem (R3-INV-DROP-001: visual representation теряется, см. `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md`)

**Документация:** `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` + `60_KNOWN_ISSUES.md`.

### 1.7 UI ✅ ЗАВЕРШЕНО (Спринты 1-3: 10-12 апреля 2026) + Character Window v2 (2026-06-05)
- ✅ ControlHintsUI — подсказки управления (F1 — скрыть/показать)
- ✅ PeakNavigationUI — навигация между пиками (скрыт в production builds)
- ✅ InventoryUI — круговое колесо (8 секторов, semantic labels: Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech)
- ✅ NetworkUI — подключение/отключение (Disconnect по центру, Reconnect, Player Count)
- ✅ TradeUI — торговля (TextMeshPro, UITheme, UIFactory)
- ✅ ContractBoardUI — контракты (TextMeshPro, UITheme, UIFactory)
- ✅ ⭐ **CharacterWindow** (P-окно, 5+ табов, UI Toolkit) — `Assets/_Project/UI/Resources/UI/CharacterWindow.{uxml,uss,asset}` + `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (1345+ LOC)
  - ✅ Pattern скопирован с MarketWindow: 4 FIX'ы сразу бесплатно (pickingMode/cursor/inline-fallback/MarkDirtyRepaint)
  - ✅ Таб "Инвентарь" — sub_inventory-tab, см. секцию 1.6 + `docs/Character-menu/sub_inventory-tab/`
  - ✅ Таб "Квесты" — 6-й таб, добавлен в T-Q11 (см. `08_ROADMAP.md` §8.3.1 T-Q11)
  - ✅ Track-кнопка в строках квестов (T-Q12) — QuestTracker toggle
  - ⏳ Табы "Персонаж" / "Корабль" / "Репутация" / "Контракты" — MVP-заглушки (хард-стат), план в `docs/Character-menu/00_OVERVIEW.md` §3
  - ✅ Visual fix 2026-06-05: characterWindowUss привязан к правильному USS-ассету (был UXML-bug); все class-стили с `!important` (UnityDefaultRuntimeTheme fix)
- ✅ ⭐ UIManager — централизованный менеджер UI (приоритеты, z-ordering, input management)
- ✅ ⭐ UIFactory — фабрика UI компонентов (8 методов, устранено 120 строк дублирования)
- ✅ ⭐ UITheme — ScriptableObject темы (51+ цвет → UITheme.Default, авто-создание)
- ✅ ⭐ TextMeshPro migration — все UI на TMP (убран legacy UnityEngine.UI.Text)
- ✅ ⭐ Cursor management — lock/unlock при открытых UI
- ✅ ⭐ Input priority system — CanReceiveInput, Escape закрывает верхнюю панель
- ✅ ⭐ ConfirmationDialog — создан (отключён для торговли по фидбеку)
- ✅ ⭐ Audio feedback infrastructure — готовы методы PlayClick/PlayError/Open/Close
- 🟡 Эмодзи устранены из TMP UI (📋📦⚡📝📢 → [Контракт] [Груз] [Срочный])
- 🟡 Оценка UI системы: 4.5/10 → 7/10 (+55%)
- 📋 Подробные отчёты: `docs/QWEN-UI-AGENTIC-SUMMARY.md` (UI Спринты 1-3) + `docs/Character-menu/00_OVERVIEW.md` (CharacterWindow) + `docs/Character-menu/refactor_log_2026-06-05.md` (visual fix)

### 1.8 Сетевая инфраструктура (базовая) ✅ ЗАВЕРШЕНО

---

### 1.9 Meta-требования (ключ-замок, lock-key) ✅ Этап 1 ЗАВЕРШЁН (R2-META-REQ-001, 2026-06-06)
**Цель:** обобщить Ship Key Subsystem (1 предмет на 1 корабль) в **универсальную систему требований** для любых Interactable-объектов: корабли, двери, контейнеры, терминалы, квестовые зоны. Массив требуемых предметов (от 1 до N) с логикой ALL / ANY / AT_LEAST_N.

**Серверный hub:**
- ✅ `MetaRequirementRegistry` (NetworkBehaviour, scene-placed в `BootstrapScene`) — `RegisterMetaRequirement(netId, MetaRequirement)`, `CanPlayerUse(clientId, netId) → bool + reason`, `RequestCanUseRpc(netId) → TargetRpc`
- ✅ `MetaRequirement` (NetworkBehaviour MonoBehaviour, generic) — `_requiredItems: ItemData[]`, `_logic: RequirementLogic { All, Any, AtLeastN }`, `_requiredCount: int` (для AtLeastN), `_interactableDisplayName: string`, `OnInventoryChanged` event, `CanPlayerUse(clientId, out reason)`, `ProgressInfo` struct для UI
- ✅ `MetaRequirementClientState` (client singleton) — `OnCanUseResponse`, `OnBindingsPushed`, `OnInteractableFound`
- ✅ `MetaRequirementToast` (UIDocument) — generic UI: показывает "X/N собрано" + список недостающих предметов + reason

**Extensions в `InventoryWorld`:**
- ✅ `HasAllItems(ulong clientId, int[] itemIds)` — AND-логика
- ✅ `HasAnyItem(ulong clientId, int[] itemIds)` — OR-логика
- ✅ `CountOf(ulong clientId, int itemId)` — сколько штук (для AT_LEAST_N)
- ✅ `GetMissingItems(ulong clientId, int[] itemIds)` — массив недостающих

**Wiring:**
- ✅ `NetworkManagerController.CreateMetaRequirementClientState()` (auto-spawn root GO в `Awake`)
- ✅ `NetworkPlayer.TryInteractNearestMetaRequirement()` (E-key entry point для НЕ-кораблей)
- ✅ `NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc` + `ReceiveMetaRequirementBindingsTargetRpc`

**Алиасы (backward compat):**
- ⏳ `ShipKeyBinding.cs` — `[Obsolete]` empty subclass → `MetaRequirement`
- ⏳ `ShipKeyServer.cs` / `ShipKeyClientState.cs` — legacy API сохранён, `[Obsolete]`
- ⏳ `ShipKeyToast.cs` — НЕ `[Obsolete]`, legacy functional
- ⏳ **TODO (через 1-2 релиз-цикла):** удалить алиасы после миграции всех сцен

**Тестовые ассеты (R2-META-REQ-001 verification, 2026-06-06):**
- ✅ 3 SO `ItemData`: `Item_Key_Blue.asset` / `Item_Key_Red.asset` / `Item_Key_Green.asset`
- ✅ 6 URP/Lit материалов: `Key_{Blue,Red,Green}.mat` + `LockBox_{Blue,Red,Green}.mat`
- ✅ `MetaRequirementPanelSettings.asset` (dedicated UI Toolkit PanelSettings)
- ✅ `WorldScene_0_0.unity`: parent `[MetaRequirement_Test]` (X=40050/40044/40038, Y=2502.7, Z=39990) с 3 Pickup-сферами + 3 LockBox-кубами
- ✅ `BootstrapScene.unity`: `[MetaRequirementRegistry]` (server hub) + `[MetaRequirementToast]` (UI)

**Stats:**
- **+7** новых C# файлов (`Scripts/MetaRequirement/*`, ~50 KB)
- **+4** extensions в `InventoryWorld` (backward compatible)
- **+3** SO ItemData
- **+6** материалов
- **+9** GameObject'ов в сценах
- **+9** документов в `docs/MetaRequirement/`
- **Compile:** 0 errors, warnings только pre-existing + by-design obsolete-usage в алиасах

**TODO (Этап 2+):**
- ⏳ `_consumeOnUse` логика + reservation pattern (сейчас поле есть, логика — TODO)
- ⏳ `ProgressInfo` UI в `MetaRequirementToast` (multi-item tooltip "3/5 ключей собрано")
- ⏳ Disconnect → reconnect race fix (`OnClientConnectedCallback` пушит bindings, race с уже-spawned)
- ⏳ Multi-MetaRequirement в одной зоне (сейчас 1→1)
- ⏳ Использование `MetaRequirement` для квестов (см. `docs/NPC_quests/08_ROADMAP.md` — T-Q?? когда потребуется)

**Документация:** `docs/MetaRequirement/00_OVERVIEW.md` (517 строк) + `10_IMPLEMENTATION_GUIDE.md` (22 KB) + `20_INSPECTOR_REFERENCE.md` + `30_RUNTIME_FLOW.md` + `40_TESTING_GUIDE.md` + `50_KNOWN_ISSUES.md` + `99_CHANGELOG.md` + `RECIPES.md` (10 рецептов).
**Migration guide:** `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` (337 строк).
**Предшественник:** `docs/Ships/Key-subsystem/00_OVERVIEW.md` (Ship Key MVP, R2-SHIP-KEY-001).

---

### 1.10 Сбор ресурсов (Resource Gathering / Mining) ✅ MVP ЗАВЕРШЁН (T-G01–T-G07, 2026-06-10)

**Новая подсистема:** интерактивные 3D-объекты в мире — подойти и нажать F → сбор N секунд → предмет в инвентарь.

**Принцип:** «пусть бегает и рубит» — движение не прерывает сбор. Tool check через MetaRequirement (Кирка → Руда). Возобновляемые узлы с cooldown.

| Компонент | Назначение | Тикет |
|-----------|-----------|-------|
| `ResourceNodeConfig` (SO) | Параметры: время сбора, кол-во harvests, cooldown, результат, анимация | T-G01 ✅ |
| `ResourceNode` (NetworkBehaviour) | State machine (Idle/Occupied/Depleted/Cooldown) + MetaReq tool check + client animation | T-G02 ✅ |
| `GatheringServer` (NetworkBehaviour) | RPC hub + server tick (0.5s) + cooldown tick + GatherResult DTO (INetworkSerializable) | T-G03 ✅ |
| `GatheringClientState` (client singleton) | Events: OnGatherProgress/Completed/Interrupted/Denied/Cancelled + timer timeout | T-G04 ✅ |
| `GatheringToastController` (UIDocument) | ProgressBar + "Добыто: X × N" + flash-fill на прерывании | T-G04 ✅ |
| `NetworkPlayer.TryGatherNearestNode()` | F-key entry (gather > boarding), MetaReq → OnAccessAllowed → gather | T-G05 ✅ |
| ResourceNode animation (client) | Scale-pulse ±15% + emissive flash (LockBox pattern, `_EMISSION` material) | T-G06 ✅ |
| Player gather animation (MVP) | Scale-pulse ±8% на персонаже во время сбора (вход для будущего StateHasher) | T-G07 ✅ |

**В сцене WorldScene_0_0:**
- 3 ResourceNode (IronVein, CopperVein, PlantHerb) — каждый с BoxCollider (trigger) + NetworkObject + ResourceNode + MetaRequirement
- 2 Pickup_Pickaxe (E → подобрать) — Кирка как tool

**В BootstrapScene:**
- `[GatheringServer]` (NetworkObject + GatheringServer)
- `[GatheringToast]` (UIDocument + GatheringToastController)
- `GatheringClientState` — auto-spawn через `NetworkManagerController.CreateGatheringClientState()`

**Ключевые решения:**
- **F-key** (выше boarding по приоритету)
- **MetaRequirement** для tool check (All/Any/AtLeastN) — бесплатный toast отказа
- **Без distance check** во время сбора (пусть бегает и рубит)
- **ProgressBar** через UI Toolkit runtime-constructed VisualElement

**Документация:** `docs/Mining/00_OVERVIEW.md` + `10_DESIGN.md` + `20_IMPLEMENTATION_PLAN.md` + `ROADMAP.md` + `99_CHANGELOG.md`.

### 1.11 Крафт-система (Crafting) ✅ MVP ЗАВЕРШЁН (T-C01–T-C07c, 2026-06-11)

**Цель:** Позволить игроку превращать ресурсы в полезные предметы через крафт-станции.

**Что реализовано:**

| Компонент | Описание | Статус |
|-----------|----------|--------|
| `RecipeData` (SO) | Ингредиенты, выходы, время крафта (сек) | T-C01 ✅ |
| `CraftingStationConfig` (SO) | Список рецептов, displayName | T-C01 ✅ |
| `CraftingWorld` (POCO singleton) | Реестр рецептов/станций/jobs, state machine (Empty/Buffered/InProgress/Completed) | T-C02 ✅ |
| `CraftingTimeService` (MonoBehaviour) | Tick 1Гц, событие OnTick, подписка CraftingServer | T-C02 ✅ |
| DTOs (6 structs) | CraftingSnapshotDto, CraftingResultDto, CraftingJobState, CraftingResultCode, BufferedIngredientDto, CommittedIngredientDto | T-C02 ✅ |
| `CraftingServer` (NetworkBehaviour) | 5 RPC (Subscribe/AddIngredient/Start/Cancel/Collect), rate-limit, subscriber push (1Гц), CheckDistance | T-C03 ✅ |
| `CraftingStation` (NetworkBehaviour) | 3 NetworkVariable (replicatedState, jobOwnerClientId, activeRecipeId), trigger zone, MetaReq tool check, client emission animation | T-C04 + T-C07c ✅ |
| `CraftingClientState` (client singleton) | Events (Progress/Completed/Interrupted/Denied/Cancelled), timeout watcher, RequestSubscribe/AddIngredient/Start/Cancel/Collect | T-C05 ✅ |
| `CraftingProgressController` (UIDocument toast) | ProgressBar + "✅ Готово" — живёт при закрытом окне | T-C05 ✅ |
| `CraftingWindow` (UIDocument) | Recipe ListView, BufferGrid, +1/+All, Start/Cancel/Collect кнопки, ProgressBar, Station switch | T-C06 ✅ |
| InventoryWorld интеграция | RemoveItems при AddIngredient, AddItemDirect при Collect, возврат при Cancel | T-C07b ✅ |
| Станция анимация | Emission pulse (orange, HDR) + scale wiggle при InProgress, зелёная вспышка при Completed | T-C07c ✅ |

**Assets в Resources:**
- `Resources/Crafting/Recipes/Recipe_CopperIngot.asset` — 3×CopperOre → 1×CopperIngot, 10с
- `Resources/Crafting/Recipes/Recipe_IronIngot.asset` — 3×IronOre → 1×IronIngot, 10с
- `Resources/Crafting/Recipes/Recipe_ShipKeyLight.asset` — 1×Ingot + 1×CrystalDust → 1×ShipLight, 30с
- `Resources/Crafting/Stations/` — Station_CraftingTable.asset (3 рецепта), Station_Shipyard.asset (1 рецепт)

**Scene placement:**
- WorldScene_0_0: `[CraftingStation_Table]` @ (39969, 2502.8, 40045) — универсальная (3 рецепта)
- WorldScene_0_0: `[CraftingStation_Shipyard]` @ (39940, 2502.8, 39982) — только ShipKeyLight
- BootstrapScene: `[CraftingServer]` (NetworkObject + CraftingServer)
- BootstrapScene: `[CraftingClientState]` (auto-spawn NMC)
- BootstrapScene: `[CraftingProgressController]` (UIDocument + panelSettings)

**Паттерн:** копия Gathering/Mining (ResourceNode → CraftingStation, GatheringServer → CraftingServer, GatheringToast → CraftingProgress). 9 тикетов, ~12-15 ч работы.

**Документация:** `docs/Crafting_system/00_OVERVIEW.md` + `10_DESIGN.md` + `ROADMAP.md`.

---

### 1.12 Обменник ресурсов (Resources Exchanger) ✅ MVP ЗАВЕРШЁН (T-E01–T-E05, 2026-06-11)

**Цель:** Создать мост между двумя системами предметов — pickable (инвентарь, добыча/крафт) и boxed (склад/рынок/торговля), не ломая существующие системы.

**Что реализовано:**

| Компонент | Описание | Статус |
|-----------|----------|--------|
| `ExchangeRateConfig` (SO) | Список пар: warehouseItemId ↔ inventoryItemName + курс (inventoryQty/warehouseQty) | T-E01 ✅ |
| `ExchangeRateEntry` (struct) | warehouseItemId, inventoryItemName, inventoryQty, warehouseQty, displayName | T-E01 ✅ |
| `ResourceExchangeResolver` | Lookup-слой: FindRateForItemName / FindRateForWarehouseItem, ResolveInventoryItemId (int ID ↔ itemName) | T-E01 ✅ |
| `ExchangeWorld` (POCO singleton) | Pack (инвентарь→склад) + Unpack (склад→инвентарь) с rollback на каждой стороне | T-E02 ✅ |
| `ExchangeServer` (NetworkBehaviour) | 2 RPC (RequestPackRpc / RequestUnpackRpc), zone validation, rate-limit, try-catch | T-E03 ✅ |
| `ExchangeClientState` (client singleton) | Events OnResultReceived для UI | T-E03 ✅ |
| `ExchangeResultDto` | success/message/warehouseDelta/inventoryDelta — INetworkSerializable | T-E03 ✅ |
| 4-я вкладка «Обменник» в MarketWindow | Левая панель (pickable, grouped), правая (warehouse), кнопки Упаковать/Распаковать | T-E04 ✅ |
| `[ExchangeServer]` в BootstrapScene | Root NetworkObject + NetworkObject. Спавнится через ScenePlacedObjectSpawner.OnServerStarted | T-E05 ✅ |
| Antigrav pickable item + курс | Item_Antigrav_осколок.asset + antigrav_ingot_v01 курс в DefaultExchangeRate | T-E05 ✅ |

**Архитектурные изменения:**

| Изменение | Мотивация |
|-----------|-----------|
| `InventoryWorld.MAX_SLOTS` 32→1000, конфигурируется через `InventoryServer.maxSlots` | Unpack 100 предметов за раз не влезал в 32 слота |
| `ScenePlacedObjectSpawner` подписка на `NetworkManager.OnServerStarted` | Scene-placed NetworkObject в BootstrapScene не спавнились (InScenePlacedSourceGlobalObjectIdHash==0) |
| `MarketServer.PushPlayerSnapshot(clientId)` public helper | После Pack склад в UI не обновлялся (InventoryServer не шлёт market snapshot) |
| `InventoryServer.Instance.PushSnapshot` + `MarketServer.Instance.PushPlayerSnapshot` | Клиент не видел изменения ни инвентаря, ни склада после Pack/Unpack |

**DefaultExchangeRate.asset (Resources/Exchange/):**

| warehouseItemId | inventoryItemName | Курс |
|----------------|-------------------|:----:|
| resource_iron_box | Железная руда | 100:1 |
| resource_copper_box | Медная руда | 100:1 |
| resource_wood_box | Древесина | 100:1 |
| antigrav_ingot_v01 | Антигравий (осколок) | 100:1 |

**Scene placement:**
- BootstrapScene: `[ExchangeServer]` (NetworkObject + ExchangeServer)
- BootstrapScene: `[ExchangeClientState]` (auto-spawn NMC)
- WorldScene_0_0: PickupItem (Item_Antigrav_осколок) на земле возле рынка

**Паттерн:** MarketServer (RPC hub) + ExchangeWorld (POCO) + tab в MarketWindow + config-driven (новые пары = запись в SO).

**Документация:** `docs/Markets/Resources_exchanger/01_ANALYSIS.md` + `02_IMPLEMENTATION.md` + `03_FIXES_HISTORY.md`.

---

## 🚢 Система кораблей — Детализация (Сессии 1-5_4)

> **Ветка:** `qwen-gamestudio-agent-dev` | **ShipController:** v2.7 | **Коммиты:** `fdc76b4`, `845ec5e`

### Реализованные системы

| # | Система | Сессия | Статус | Файлы |
|---|---------|--------|--------|-------|
| 1 | Core Smooth Movement | 1 | ✅ | ShipController.cs v2.0 → v2.1 |
| 2 | Altitude Corridor System | 2 | ✅ | AltitudeCorridorData, AltitudeCorridorSystem, TurbulenceEffect, SystemDegradationEffect |
| 3 | Wind & Environmental Forces | 3 | ✅ | WindZone, WindZoneData, интеграция в ShipController v2.2 |
| 4 | Module System Foundation | 4 | ✅ | ShipModule, ModuleSlot, ShipModuleManager, 7 модулей (YAW_ENH, PITCH_ENH, LIFT_ENH, ROLL, MEZIY_*) |
| 5 | Fuel System + Meziy Modules | 5 | ✅ | ShipFuelSystem, MeziyModuleActivator, MeziyThrusterVisual, MODULE_ROLL |
| 5.2 | Continuous Mode Rewrite | 5_2 | ✅ | Переработка между, Debug HUD, фиксы частиц |
| 5.3 | Passive/Active/Overheat | 5_3 | ✅ | Новая архитектура: C/V (pitch), Z/X (roll), Shift+A/D (yaw) |
| 5.4 | UI, Thrust Module, Polish | 5_4 | ✅ | MeziyStatusHUD, MODULE_MEZIY_THRUST, cooldown 15s, валидация слотов |

### Архитектура ShipController v2.7 (1249 строк)

```
FixedUpdate (сервер):
  1. AverageInputs() — усреднение от всех пилотов
  2. ApplyModuleModifiers() — модульные множители + passive meziy
  3. MeziyModuleActivator.Tick() — перегрев/кулдаун
  4. Fuel check — engine stall при fuel < 10
  5. Atmospheric refuel (L) — 2.0 fuel/s, stationary only
  6. Meziy activation — C/V, Z/X, Shift+A/D, Shift+W/S
  7. Smooth thrust/yaw/pitch/lift/roll — Mathf.SmoothDamp
  8. Validate altitude — corridor effects
  9. Apply forces — thrust, anti-gravity, lift, rotation
  10. Stabilization — auto при отсутствии ввода 0.5s+
  11. ApplyMeziyEffects — torque + thrust boost
  12. ApplyWind — зарегистрированные зоны
  13. ClampVelocity / ClampPitchAngle
```

### Параметры кораблей (4 класса)

| Параметр | Light | Medium | Heavy | HeavyII |
|----------|-------|--------|-------|---------|
| Mass | 800kg | 1000kg | 1500kg | 2000kg |
| Max Speed | 50 м/с | 40 м/с | 25 м/с | 18 м/с |
| Thrust Force | 500 | 650 | 800 | 900 |
| Yaw Force | 3500 | 3000 | 2000 | 1500 |
| Yaw Smooth | 0.25s | 0.3s | 0.5s | 0.7s |
| Yaw Decay | 0.8s | 1.0s | 1.5s | 2.0s |
| Wind Exposure | 1.2 | 1.0 | 0.7 | 0.5 |

### Мезиевые модули (4 модуля)

| Модуль | Сила | Кулдаун | Fuel Cost | Управление |
|--------|------|---------|-----------|------------|
| MODULE_MEZIY_PITCH | 500 | 15s | 5 | C (вверх) / V (вниз) |
| MODULE_MEZIY_ROLL | 800 | 15s | 5 | Z (влево) / X (вправо) |
| MODULE_MEZIY_YAW | 1000 | 15s | 5 | Shift+A / Shift+D |
| MODULE_MEZIY_THRUST | 800 | 15s | 4 | Shift+W (ускор.) / Shift+S (тормоз) |

**Принцип:** Модуль установлен = пассивный эффект (+10% к управлению, 0 топлива). Зажата клавиша = активный выхлоп (torque/thrust + частицы + расход). 10 сек активности → перегрев → 15 сек кулдаун.

### Управление кораблём (полная карта)

| Клавиша | Действие |
|---------|----------|
| W/S | Тяга вперёд/назад |
| A/D | Рыскание (поворот) |
| Q/E | Подъём/спуск |
| Мышь Y | Тангаж |
| Left Shift | Ускорение |
| Z/X | Крен (требует MODULE_ROLL) |
| C/V | Мезиевый тангаж |
| Shift+A/D | Мезиевое рыскание |
| Shift+W/S | Мезиевый рывок вперёд/торможение |
| L | Дозаправка (stationary) |
| F3 | Debug HUD |
| F4 | Meziy Status HUD |

### Известные ограничения

| # | Ограничение | Приоритет | План |
|---|-------------|-----------|------|
| 1 | ShipController.cs v2.7 — 1200+ строк, монолитный файл | P2 | Рефакторинг: разделение на подсистемы (Movement, Environment, Modules, Fuel) |
| 2 | Cinemachine Impulse для турбулентности не работает | P3 | Отложено |
| 3 | Wind lanes между пиками не реализованы | P3 | Отложено |
| 4 | MODULE_MEZIY_THRUST расход топлива — 8 fuel/sec при continuous | P2 | Балансировка после тестов |
| 5 | Shift+W потенциальный конфликт с обычным thrust | P2 | Требуется геймплейное тестирование |

### Статистика разработки

| Метрика | Значение |
|---------|----------|
| Сессий проведено | 8 (1, 2, 3, 4, 5, 5.2, 5.3, 5.4) |
| Багов зафиксировано | 14 (все исправлены) |
| Файлов создано | 20+ |
| Файлов изменено | 10+ |
| Коммитов | 25+ |
| Откатов | 1 (asmdef инцидент) |
| Тегов бэкапа | 5 |

---

## Этап 2: Сетевой фундамент (Недели 7-10) ✅ ЗАВЕРШЁН
**Цель:** Реализовать клиент-серверное взаимодействие.

### Задачи:
1. **Серверная часть:** ✅
   - ✅ Архитектура: Авторитарный сервер (клиент только отправляет ввод)
   - ✅ Dedicated Server режим (кнопка в UI + `-server` build arg)
   - ✅ Headless режим: `-batchmode -nographics -server`
   - ⏳ WebSocket сервер на .NET 8 (Этап 5+)
   - ⏳ Система комнат/лобби для групп до 4 игроков (Этап 5+)

2. **Клиентская интеграция:** ✅
   - ✅ Сетевой контроллер (NetworkBehaviour)
   - ✅ Client-side Prediction (базовое — коррекция позиции при рассинхронизации)
   - ✅ Интерполяция других игроков (NetworkTransform Lerp 0.1s)
   - ✅ Сохранение/восстановление инвентаря при реконнекте (PlayerPrefs)
   - ✅ Синхронизация подбора (HidePickupRpc, OpenChestRpc — SendTo.Everyone)

3. **Тестирование сети:** ✅
   - ✅ Локальный запуск: Host + Client (1 сервер + 1 клиент)
   - ✅ Синхронизация физики кораблей (кооп-пилотирование)
   - ✅ Обработка разрывов соединения (авто-реконнект + ручная кнопка)
   - ✅ Player Count (обновляется в реальном времени)
   - ⏳ Тестирование 1 сервер + 2-3 клиента (нужен 2й компьютер)

**Результат:** Работающий мультиплеер с Host+Client, реконнект, сохранение инвентаря, Dedicated Server.

**⏳ Отложено на Этап 5+:**
- Отдельный серверный билд (.NET 8 / Master-сервер)
- Система лобби/комнат
- Полная серверная валидация инвентаря (anti-cheat)

---

## Этап 2.1: Масштабный мир (24 сцены) ✅ ЗАВЕРШЁН (1 мая 2026)
**Цель:** Реализовать распределённый мир на основе 24 сцен для поддержки MMO-масштаба.

### Задачи:

1. **Архитектура мира:** ✅
   - ✅ **24 сцены в сетке 4×6** (GridColumns=6, GridRows=4)
   - ✅ Размер каждой сцены: **79,999 × 79,999 units**
   - ✅ Общий размер мира: ~480,000 × ~320,000 units
   - ✅ Именование: `WorldScene_{GridX}_{GridZ}` (e.g., `WorldScene_0_0`)

2. **Система загрузки сцен:** ✅
   - ✅ **ClientSceneLoader** — загрузка/выгрузка сцен на клиенте
   - ✅ **ServerSceneManager** — отслеживание позиций клиентов на сервере
   - ✅ **SceneID** — структура координат (GridX, GridZ)
   - ✅ **SceneRegistry** — ScriptableObject с метаданными сцен
   - ✅ Стратегия **"1+1"**: текущая сцена + 1 предзагруженная соседняя
   - ✅ Предзагрузка при приближении к границе (10,000 units)
   - ✅ Выгрузка при удалении >10,000 units
   - ✅ Максимум 4 загруженные сцены одновременно

3. **Интеграция с сетевой подсистемой:** ✅
   - ✅ Синхронизация позиции через **FloatingOriginMP**
   - ✅ **PlayerSpawner** отслеживает мировую позицию
   - ✅ **NetworkPlayer** отслеживает локальную позицию
   - ✅ RPC для перехода между сценами (`LoadSceneTransitionClientRpc`)
   - ✅ Корректная работы с кораблём и персонажем

4. **Фиксы и стабилизация:** ✅
   - ✅ Singleton для ClientSceneLoader (предотвращение дубликатов)
   - ✅ Sentinel значение `-1,-1` вместо `0,0` для определения текущей сцены
   - ✅ Поиск Player через тег "Player" (PlayerSpawner)
   - ✅ Отключена коррекция позиции (порог 99,999 units)
   - ✅ Исправлена X/Z оси в именах сцен

### Ключевые компоненты:

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `ClientSceneLoader` | `Scripts/World/Scene/ClientSceneLoader.cs` | Основной загрузчик сцен |
| `ServerSceneManager` | `Scripts/World/Scene/ServerSceneManager.cs` | Серверное отслеживание |
| `SceneID` | `Scripts/World/Scene/SceneID.cs` | Координаты сцены |
| `SceneRegistry` | `Scripts/World/Scene/SceneRegistry.cs` | Метаданные сцен |
| `WorldSceneManager` | `Scripts/World/WorldSceneManager.cs` | Центральный координатор |

### Документация:
- [`docs/world/LargeScaleMMO/2_iteration_scene-mode/SYSTEM_OVERVIEW.md`](world/LargeScaleMMO/2_iteration_scene-mode/SYSTEM_OVERVIEW.md) — Overview системы
- [`docs/world/LargeScaleMMO/2_iteration_scene-mode/INDEX.md`](world/LargeScaleMMO/2_iteration_scene-mode/INDEX.md) — Навигация по документации
- [`docs/world/LargeScaleMMO/2_iteration_scene-mode/COMPLETION_REPORT.md`](world/LargeScaleMMO/2_iteration_scene-mode/COMPLETION_REPORT.md) — Полный отчёт
- [`docs/world/LargeScaleMMO/2_iteration_scene-mode/SCENE_ARCHITECTURE_DECISION.md`](world/LargeScaleMMO/2_iteration_scene-mode/SCENE_ARCHITECTURE_DECISION.md) — ADR

### Известные ограничения (pending):
- ⏳ Визуальная задержка загрузки чанков в новых сценах
- ⏳ Коррекция позиции отключена — требует полноценной реализации для мультиплеера
- ⏳ Y спавна = 3000 (для тестирования) — вернуть к нормальному значению
- ⏳ WorldSceneManager / ServerSceneManager / WorldStreamingManager / WorldChunkManager / FloatingOriginMP — написаны, но **не развёрнуты в сцене**. Фокус проекта сейчас — `WorldScene_0_0`, остальные 23 сцены — на потом.
- ✅ **NPC+Quest v2 scene-placement rule (2026-06-07):** BootstrapScene = server infra ONLY (NetworkManager, [QuestServer], [QuestClientState], [ContractMetaBridge], ScenePlacedObjectSpawner, [QuestTracker], [QuestToast], [MarketWindow], [DialogWindow]). Game-world objects → `WorldScene_X_Z` (NPC `[Mira]`, chest, pickup, market zone, ships). Иначе scene-placed NetworkObject с `InScenePlacedSourceGlobalObjectIdHash == 0` → не спавнится NGO → NRE в RPC. Подробнее: `docs/Ships/INTEGRATION_SHIPS_TO_WORLD_0_0.md`.

---

## Этап 2.5: Визуальный прототип (Недели 11-14) 🔄 В ПРОЦЕССЕ
**Цель:** Заменить примитивы на модели, создать визуальную идентичность Sci-Fi + Ghibli.

### Задачи:
1. **URP-совместимость:** ✅
   - ✅ Исправлены материалы: Standard → URP/Lit (WorldGenerator.cs)
   - ✅ Создан URP fallback для пиков и облаков
   - ✅ MaterialURPConverter — авто-конвертация при запуске
   - ✅ Созданы URP-материалы (CloudMaterial_URP, character_URP)

2. **Облака (Ghibli-стиль):** ✅
   - ✅ CloudGhibli.shader — кастомный URP Unlit шейдер
   - ✅ Noise + rim glow + vertex displacement (морфинг форм)
   - ✅ ProceduralNoiseGenerator — FBM noise текстуры (512×512)
   - ✅ Авто-интеграция в CloudLayer.cs

3. **Арт-библия:** ✅
   - ✅ docs/ART_BIBLE.md — полная визуальная спецификация
   - ✅ Цветовая палитра, освещение, post-processing настройки
   - ✅ Спецификации кораблей, персонажей, окружения, UI
   - ✅ Пайплайн ассетов, конвенция имён, референсы

4. **Модели кораблей (приоритет): ⏳
   - ⏳ Лёгкий корабль (торообразный, 5-8k tri, Blender → FBX → Unity)
   - ⏳ Ветровые лопасти (вращающиеся, отдельный меш)
   - ⏳ Антиграв-двигатели (emissive + bloom)
   - ⏳ Интеграция в ShipController (замена примитива)

5. **Окружение:** ⏳
   - ⏳ Горные пики — текстуры из Poly Haven (CC0 скалы)
   - ⏳ 3-5 префабов построек (платформа, склад, хостел)
   - ⏳ Посадочные площадки
   - ⏳ Мосты между пиками (процедурные)

6. **Персонаж:** ⏳
   - ⏳ Mixamo-персонаж (idle, walk, run, jump)
   - ⏳ Интеграция в NetworkPlayer (замена capsule)

7. **UI и предметы:** ⏳
   - ⏳ 8 иконок инвентаря (128×128, game-icons.net или нарисовать)
   - ⏳ 3D модель сундука (low-poly, анимация открытия)
   - ⏳ Модели ресурсов (мезий, антигравий, МНП)

8. **Post-Processing + Day-Night Cycle:** ✅ ЗАВЕРШЕНО
   - ✅ URP Volume: Bloom, ColorAdjustments, Vignette
   - ✅ 3 VolumeProfiles: DayVolumeProfile, NightVolumeProfile, TwilightVolumeProfile
   - ✅ ServerWeatherController — timeOfDay + temperature sync
   - ✅ DayNightController — 5 phases (Morning/Midday/Evening/Twilight/Night), smooth transitions
   - ✅ Temperature filter via dedicated Volume (priority 200, aggressive color grading)
   - ✅ Fog + Ambient lighting per phase
   - ✅ Skybox materials: Day/Night/Twilight (material swap)
   - ✅ Sun directional light animation (position follows server time)
   - ✅ Moon mesh + phase material (MoonController)
   - ✅ ConstellationController (215 stars, 24 constellations, sky dome radius 900000)
   - ✅ Runtime profile instantiation (prevents asset reset on play/stop)
   - ⏳ Moon orbit angle fine-tuning (low priority — mesh visible, phases work)

**Результат:** Визуально различимый прототип — корабли, облака, пики, персонаж с анимациями.

**Документы:**
- [`docs/ART_BIBLE.md`](ART_BIBLE.md) — визуальная спецификация
- `Assets/_Project/Art/Shaders/CloudGhibli.shader` — шейдер облаков
- `Assets/_Project/Scripts/Core/ProceduralNoiseGenerator.cs` — noise текстуры
- `Assets/_Project/Scripts/Core/MaterialURPConverter.cs` — конвертация материалов

---

## Этап 3: Ролевая система и Торговля (Недели 9-14) ✅ ЗАВЕРШЁН
**Цель:** Добавить RPG-элементы, сохранение данных и **полную систему торговли «Дальнобойщики над облаками»**.

### 3.1 Характеристики и навыки (RPG)
1. **Система прокачки:**
   - Система прокачки (опыт, уровни, очки навыков)
   - Деревья навыков для пилотирования и выживания
   - Балансировка чисел (таблицы в ScriptableObject)

### 3.2 Базовая торговля (Недели 9-11) ✅ ЗАВЕРШЕНО (Сессии 1-5)
1. **TradeItemDefinition (ScriptableObject):** ✅
   - ✅ Определение всех товаров: id, цена, вес, объём, иконка
   - ✅ Флаги: опасный, хрупкий, контрабанда
2. **CargoSystem — груз корабля:** ✅
   - ✅ Отдельный от личного инвентаря
   - ✅ Слоты, вес, объём, влияние на скорость
   - ✅ Проверка опасного груза (протечка мезия)
3. **LocationMarket — рынок локации:** ✅
   - ✅ demand_factor, supply_factor, текущие цены
   - ✅ ScriptableObject с начальными данными
4. **TradeUI — базовый интерфейс:** ✅ ЗАВЕРШЕНО + UI Спринты 1-3
   - ✅ Отображение цен, покупка, продажа
   - ✅ ServerRpc: BuyItem, SellItem
   - ✅ Защита от двойных RPC (_tradeLocked)
   - ✅ Склад игрока + трюм корабля
   - ✅ ⭐ Миграция на TextMeshPro (UnityEngine.UI.Text → TextMeshProUGUI)
   - ✅ ⭐ UITheme интеграция (40+ цветов → UITheme.Default)
   - ✅ ⭐ UIFactory интеграция (убран boilerplate код)
   - ✅ ⭐ Semantic labels для типов товаров
   - ✅ ⭐ Cursor management при открытии/закрытии
5. **Серверная валидация:** ✅
   - ✅ Все транзакции только на сервере
   - ✅ Валидация: quantity > 0, locationId, currentPrice > 0
   - ✅ Rate limiting (отключён для отладки)
   - ✅ Clamp факторов (0.0 … 1.5)

### 3.3 Контракты НП (Недели 11-13) ✅ ЗАВЕРШЕНО (Сессия 7 + UI Спринты 1-3)
1. **ContractSystem:** ✅
   - ✅ НП-доставка: взять товар → доставить → получить
   - ✅ Система «под расписку» (туториал-крючок, первые 2 часа)
   - ✅ Долговая система: не доставил = долг × 1.5 + штраф репутации
2. **NPC-агент НП:** ✅ + UI Спринты 1-3
   - ✅ Доска контрактов в городах
   - ✅ Туториал: первый контракт «под расписку»
   - ✅ ⭐ ContractBoardUI миграция на TextMeshPro
   - ✅ ⭐ UITheme интеграция (15+ цветов → UITheme.Default)
   - ✅ ⭐ UIFactory интеграция (убран boilerplate код)
   - ✅ ⭐ Эмодзи устранены (📋📦⚡📝 → [Контракт] [Груз] [Срочный])
   - ✅ ⭐ Cursor management при открытии/закрытии
   - ✅ ⭐ Input priority через UIManager
3. **✅ Мост в квестовую систему (T-X5 + T-Q15, 2026-06-08):**
   - ✅ `ContractServer` публикует 3 events: `ContractAcceptedEvent` / `ContractCompletedEvent` / `ContractFailedEvent`
   - ✅ `ContractMetaBridge` (server-side singleton, scene-placed в BootstrapScene, DontDestroyOnLoad) подписан на events → `QuestWorld.MarkContractAccepted/MarkContractCompleted` + `QuestTriggerService.Evaluate()`
   - ✅ Квесты могут следить за состоянием контрактов через `HasContractAccepted`/`HasContractCompleted` objectives
4. **⏳ Серверная база данных:** (Этап 5+)
   - PostgreSQL для хранения аккаунтов, прогресса, инвентаря, долгов
   - Redis для кэширования сессионных данных
   - API для безопасного обмена данными (авторизация через JWT)

### 3.4 Динамическая экономика (Недели 13-14) ✅ ЗАВЕРШЕНО (Сессия 6 + 8D-8F)
1. **Tick-система:** ✅
   - ✅ Каждые N минут (зависит от режима): обновление рынка
   - ✅ NPC-трейдеры перемещают товары (4 трейдера: ГосКонвой, Ветер, Караванщик, Челнок)
   - ✅ Затухание спроса/предложения — ELASTIC "КАЧЕЛИ" (0.92x/тик, возврат из пика за ~80 мин)
   - ✅ Пассивная регенерация стока (+8% от базового за тик)
   - ✅ Глобальные события: "Мезиевая лихорадка" (триггер от спроса)
   - ✅ Delta-отправка обновлений (только изменённые предметы)
2. **Влияние игроков на рынок:** ✅
   - ✅ Каждая покупка/продажа меняет demand/supply
   - ✅ Массовая скупка → дефицит → цены растут
3. **PlayerDataStore — единый источник данных:** ✅ (Сессия 8F)
   - ✅ Кредиты ОБЩИЕ для всех локаций
   - ✅ Склады привязаны к локациям
   - ✅ Кэш в памяти + PlayerPrefs (P0: заменить на БД)
   - ✅ Готов к замене на PostgreSQL (интерфейс абстракции)
4. **Сохранение состояния рынка:** ⏳ (отложено — добавить при реализации persistence)
   - Сохранение: demand/supply факторы, active events, tick-таймер
   - Восстановление при перезагрузке сервера
   - Формат: JSON или БД
   - **Референс:** в NPC+Quests v2 сделан `JsonQuestStateRepository` (T-Q18) — atomic JSON в `Application.persistentDataPath`, immediate save на каждый state change, единый JSON на игрока. Можно скопировать паттерн для market state.

**Результат:** ✅ Сохранение прогресса между сессиями, **ПОЛНАЯ система торговли с динамической экономикой, контрактами НП и долговой системой**.

**Документация:**
- [`docs/TRADE_SYSTEM_RAG.md`](TRADE_SYSTEM_RAG.md) — ⭐⭐ RAG документация (архитектура, потоки, формулы)
- [`docs/TRADE_DEBUG_GUIDE.md`](TRADE_DEBUG_GUIDE.md) — отладка (симптомы → решения)
- [`docs/gdd/GDD_22_Economy_Trading.md`](gdd/GDD_22_Economy_Trading.md) — GDD экономики (v3.0)
- [`docs/gdd/GDD_25_Trade_Routes.md`](gdd/GDD_25_Trade_Routes.md) — GDD маршрутов
- [`docs/QWEN-UI-AGENTIC-SUMMARY.md`](QWEN-UI-AGENTIC-SUMMARY.md) — ⭐ UI система: полный отчёт спринтов 1-3

**Известные проблемы (P0-P1):**
- 🔴 P0: PlayerPrefs → заменить на IPlayerDataRepository + БД (Сессия 10). **Частично сделано:**
  - **Inventory:** `JsonInventoryRepository` (T-X0, 2026-06-05) — atomic JSON save/load per-client в `Application.persistentDataPath`.
  - **Quests:** `JsonQuestStateRepository` (T-Q18, 2026-06-08) — atomic JSON, immediate save.
  - **Не покрывает:** Trade/Cargo/Contract state.
- 🔴 P0: FindAnyObjectByType → PlayerRegistry (Сессия 10) — частично решено кэшированием в PeakNavigationUI. **Полностью решён в NPC+Quests v2** (`QuestServer.Instance` singleton, `QuestClientState` RuntimeInitializeOnLoadMethod) **и в Inventory v2** (`InventoryClientState` singleton + `RuntimeInitializeOnLoadMethod`).
- 🔴 P0: ScriptableObject state → MarketConfig + MarketState (Сессия 10). **В NPC+Quests v2 — решён частично:** `QuestDatabase` SO (registry, не state) + `ItemRegistry` SO (id↔item mapping, не state).
- 🟡 P1: Валидация позиции в RPC (Сессия 10)
- 🟡 P1: Clamp quantity + rate limit (Сессия 10) — **В NPC+Quests v2 — решён:** QuestServer rate limit 30 ops/min/client.
- 🟠 MEDIUM: **R3-INV-DROP-001 (2026-06-06) — Drop теряет визуальное представление предмета** (см. `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md`). Игрок подбирает цветной ключ с emission → drop → появляется базовая белая сфера. **Не блокер** для текущего контента, но визуально сбивает.
- 🟠 MEDIUM: **MetaRequirement TODO (R2-META-REQ-001, Этап 2)** — `_consumeOnUse` логика, `ProgressInfo` UI, disconnect-reconnect race fix (см. `docs/MetaRequirement/50_KNOWN_ISSUES.md` §"TODO").
- 🟡 UI: Контракты не сдаются с грузом на корабле (Спринт 3.3 — MVC рефакторинг)

---

## Этап 3.5: Торговые фракции и Контент 🔜 ЗАПЛАНИРОВАН (Недели 14-16)
**Цель:** Расширить торговлю мануфактурами, подпольем и военными анклавами.

### Задачи:
1. **Мануфактуры (4 фракции):**
   - Аврора (двигатели), Титан (военные), Гермес (латекс), Прометей (МНП)
   - Репутация мануфактур → скидки, эксклюзивные контракты
   - Перекрёстное влияние с НП
2. **Свободные торговцы (чёрный рынок):**
   - Вступление через контрабанду (репутация +30)
   - Чёрные рынки на заброшенных платформах
   - Без налога, но риск обнаружения СОЛ
3. **Военные анклавы:**
   - Опасные маршруты, ×2-3 награда
   - Военные контракты: доставка оружия, сопровождение
4. **Торговые маршруты (карта):**
   - 6 основных маршрутов НП-конвоев
   - 4 независимых маршрута (чёрные рынки)
   - Статус маршрутов: открыт/заблокирован
5. **Глобальные события рынка:**
   - Дефицит мезия, бум антигравия, блокада, эпидемия, война гильдий
   - Визуальное оповещение игроков

### ✅ Фундамент фракций (реализован в NPC+Quests v2, 2026-06-08)
> Технический фундамент для Этапа 3.5 уже есть (M5, T-Q01, T-Q13). Контент и lore-связи — на этапе 3.5.
> - ✅ `ProjectC.Factions.FactionId` enum (12 lore значений: GuildOfThoughts, GuildOfCreation, GuildOfSecrets, ..., Pirates, Neutral)
> - ✅ `ReputationClientState` (singleton) — per-faction репутация игрока
> - ✅ `NpcAttitudeClientState` (singleton) — per-NPC отношение
> - ✅ `FactionDefinition` SO с cross-faction influence (MVP stub)
> - ✅ Dialog actions `AddReputation` / `AddNpcAttitude` (T-Q16) — NPC диалоги меняют репутацию
> - ⏳ **M5 в roadmap NPC+Quests:** сама Rep-таблица (12 guilds, tier thresholds, display messages) — на этапе 3.5
> - ⏳ Кросс-фракционные influence — MVP stub, полная реализация → v2
> - ⏳ Display HUD репутации в header (деферред с T-Q10)
> - ⏳ T-X2 (DEFERRED): `TradeItemDefinition.Faction` (8 manufacturer factions) vs `FactionId` (12 lore guilds) — разные концепции, нужен design discussion (см. `docs/NPC_quests/08_ROADMAP.md` §8.0 «DEFERRED»).

**Результат:** Живой мир торговли — НП vs мануфактуры, чёрный рынок, военные маршруты, динамические события.

---

## Этап 4: Контент и полировка (Недели 16-22) 🔄 В ПРОЦЕССЕ
**Цель:** Наполнить мир контентом, улучшить визуал и **интегрировать торговлю в Core Loop**.

### Задачи:
1. **Миссии и квесты:** ✅ ЗАВЕРШЕНО (сессии 2026-06-07..09, см. `docs/NPC_quests/08_ROADMAP.md`)
   - ✅ **NPC + Quests v2 подсистема** (50+ тикетов, 19 milestones, ~8400 строк кода)
   - ✅ Серверная логика выполнения: `QuestServer` (NetworkBehaviour) + `QuestWorld` (POCO) + 9 RPC
   - ✅ Диалоги с NPC (ветвящиеся сюжеты): `DialogTree` SO + `DialogueNode`/`Edge`/`Condition`/`Action` + UI Toolkit `DialogWindow` (typewriter, F-skip, mouse-click skip)
   - ✅ **Mira end-to-end quest** (M11) — полный playthrough с pickup, dialog tree, reputation, attitude, credits
   - ✅ Multi-stage квесты (M13) — `onEnter`/`onComplete` actions, stage transitions
   - ✅ Real-time objective evaluation (tick 5 sec) — `QuestTriggerService` + 8 trigger types
   - ✅ Persistence (M8) — `JsonQuestStateRepository`, immediate save на каждом state change
   - ✅ Editor tooling: `QuestDatabaseWindow` (M16), `QuestNodeGraph` (M17), **Editable** (M18), **CSV Import/Export** (M19)
   - ✅ Quest toast notifications (M15): "📜 Accepted", "💚 +5", "💰 +200 CR", "✨ Найден квест"
   - ✅ Item ID single source of truth (M14): `ItemRegistry` SO + 32 items
   - ✅ **Торговые квесты:** ✅ **Quest ↔ Contract мост** — `ContractMetaBridge` (M7): NPC dialog actions `GiveItem`/`TakeItem` + ContractServer events (`ContractAcceptedEvent`/`ContractCompletedEvent`/`ContractFailedEvent`) → quest trigger evaluation
   - ⏳ **Контент квестов (не доделано):** создать 5–10 production квестов (сейчас есть 5 тестовых: `collect_copper_ore`, `find_artifact`, `stage_intro_demo`, `stage_multi_demo`, `collect_copper`). **Авторский контент** — открыто.
   - ⏳ **Ежедневные испытания** — не начато (post-MVP).

2. **Визуальные улучшения:**
   - Финальные шейдеры для облаков и воды
   - Анимации персонажей и кораблей (Mixamo/Custom)
   - Пост-обработка: bloom, color grading, хроматическая аберрация
   - **Визуал торговли:** 3D модели грузов, NPC-торговцы, торговые посты
   - ⏳ **Ветер для персонажа (реализация):**
     - Адаптация WindZone из кораблей для CharacterController
     - Визуальный эффект: развевание одежды, волос, плащей
     - Звук ветра в ушах при нахождении в зоне
     - Геймплейное влияние: снос на узких мостах, усиление прыжка по ветру
     - Тестовая сцена: персонаж проходит через 3 зоны ветра (Constant, Gust, Shear)

3. **Звук и музыка:**
   - Динамический саундтрек (меняется от ситуации)
   - 3D-звуки двигателей, ветра, шагов
   - Голосовой чат (интеграция Vivox/Discord SDK)

4. **⏳ P2P торговля между игроками:** (Этап 3.5 или 4)
   - UI обмена, серверная валидация, налог 5%

5. **⏳ Рефакторинг P0 проблем торговой системы:** (Сессия 10)
   - IPlayerDataRepository вместо PlayerPrefs
   - PlayerRegistry вместо FindAnyObjectByType
   - MarketConfig + MarketState разделение

**Результат:** Вертикальный срез игры с основным циклом геймплея, **работающая торговля «Дальнобойщики над облаками»**, подготовка к MMO.

---

## Этап 5: Масштабирование и оптимизация (Недели 22-28)
**Цель:** Подготовить инфраструктуру для большого онлайна.

### Задачи:
1. **Серверная масштабируемость:**
   - Шардинг мира (разделение на зоны/сервера)
   - Оркестрация через Kubernetes (автомасштабирование под нагрузкой)
   - Мониторинг: Prometheus + Grafana (метрики нагрузки, лаги)

2. **Оптимизация клиента:**
   - LOD-системы для кораблей и островов
   - Occlusion Culling для сложных сцен
   - Пулинг объектов (пули, эффекты, NPC)

3. **Античит:**
   - Серверная валидация всех критичных действий
   - Обнаружение аномалий (скорость, урон, телепорты)
   - Репорты игроков + автоматические баны

**Результат:** Стабильная работа при 100+ одновременных игроках на шард.

---

## Этап 6: Закрытое бета-тестирование (Недели 25-28)
**Цель:** Выявить проблемы перед публичным релизом.

### Задачи:
1. **Сбор фидбека:**
   - Закрытая группа тестеров (50-100 человек)
   - Сбор метрик: время сессии, точки выхода, баги
   - Опросы по балансу и удобству управления

2. **Итерации по фидбеку:**
   - Хотфиксы критичных багов
   - Балансировка экономики и прокачки
   - Улучшение туториала и первого впечатления

3. **Подготовка к релизу:**
   - Локализация (минимум: EN, RU)
   - Страницы в магазинах (Steam, Epic)
   - Маркетинг: трейлер, скриншоты, пресс-кит

**Результат:** Готовый к раннему доступу продукт.

---

## Этап 7: Ранний доступ и поддержка (Постоянно)
**Цель:** Постепенный рост аудитории и контента.

### Задачи:
1. **Релиз в Early Access:**
   - Публикация на платформах
   - Поддержка сообщества (Discord, форумы)
   - Регулярные патчи (раз в 2 недели)

2. **План контента:**
   - Новые острова, корабли, сюжетные линии
   - Сезонные события и ивенты
   - Расширение кооператива (рейды, гильдии)

3. **Монетизация (если нужна):**
   - Косметические предметы (скины, эффекты)
   - Боевой пропуск (без pay-to-win)
   - Донаты за поддержку развития

---

## Критические риски и меры:
1. **Сложность сетевой синхронизации физики**
   → Начать с упрощенной модели, тестировать с первых недель.

2. **Читеры в экономике**
   → Вся критичная логика только на сервере, логирование действий.

3. **Высокие требования к инфраструктуре**
   → Использовать облачные решения (AWS GameLift, Azure PlayFab) для старта.

4. **Перегрузка контентом**
   → Фокус на минимально жизнеспособный продукт (MVP) для раннего доступа.

5. **Ограниченность арт-ресурсов (1 человек)**
   → Приоритет: корабли → облака → пики → остальное. Mixamo + CC0 текстуры.

6. **URP-совместимость шейдеров**
   → Все новые материалы — только URP Lit/Unlit. Standard → авто-конвертация.

---

## Инструменты и зависимости:
- **Клиент:** Unity 6, URP, Netcode for GameObjects, DOTween (анимации), Cinemachine (камера)
- **Сервер:** .NET 8, WebSocket/Netty, PostgreSQL, Redis, Docker/K8s
- **DevOps:** GitHub Actions (CI/CD), Sentry (ошибки), Prometheus (мониторинг)
- **Ассеты:**
  - Blender (3D-модели) → FBX → Unity
  - Poly Haven / AmbientCG (CC0 текстуры)
  - Mixamo (персонажи + анимации)
  - game-icons.net (UI-иконки, CC BY 3.0)
  - Krita / Materialize (текстуры)
  - FMOD/Wwise (звук)
- **Кастомные шейдеры:** CloudGhibli (URP Unlit + noise + rim glow)
- **Система ветров (Сессия 3):** WindZone, WindZoneData — объёмные триггеры с профилями (Constant, Gust, Shear)
  - ✅ Реализовано для кораблей (ShipController v2.2)
  - ⏳ Запланировано для персонажа (адаптация CharacterController)
- **Art Bible:** [`docs/ART_BIBLE.md`](docs/ART_BIBLE.md)

---

## 📋 Game Design Documents (GDD)

Полная спецификация всех игровых систем разработана в формате GDD:

### Core — Фундаментальные документы
| Документ | Описание |
|----------|----------|
| [GDD_00: Game Overview](gdd/GDD_00_Overview.md) | Концепция, пиллары, USP, целевая аудитория |
| [GDD_01: Core Gameplay](gdd/GDD_01_Core_Gameplay.md) | Core Loop, управление, физика, режимы |
| [GDD_02: World & Environment](gdd/GDD_02_World_Environment.md) | Мир, 15 пиков, 4 города, Завеса, погода |

### Systems — Технические системы
| Документ | Описание |
|----------|---------|
| [GDD_10: Ship System](gdd/GDD_10_Ship_System.md) | 4 класса кораблей, физика, кооп-пилотирование. **✅ Ship Key MVP (R2-SHIP-KEY-001) + MetaRequirement v1 (R2-META-REQ-001) реализованы (2026-06-06), см. `docs/Ships/Key-subsystem/00_OVERVIEW.md` + `docs/MetaRequirement/00_OVERVIEW.md`.** |
| [GDD_11: Inventory & Items](gdd/GDD_11_Inventory_Items.md) | 8 типов, круговое колесо, LootTable, сундуки. **✅ sub_inventory-tab (P-таб) + MetaRequirement extensions (HasAllItems/HasAnyItem/CountOf/GetMissingItems) реализованы, см. `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` + `docs/MetaRequirement/30_RUNTIME_FLOW.md`.** |
| [GDD_12: Network & Multiplayer](gdd/GDD_12_Network_Multiplayer.md) | NGO, RPC, реконнект, Dedicated Server |
| [GDD_12.1: Scene-Based World Streaming](gdd/GDD_12_1_Scene_World_Streaming.md) | 24 сцены, 4×6 grid, boundary-based loading |
| [GDD_13: UI/UX System](gdd/GDD_13_UI_UX_System.md) | HUD, Ghibli стиль, адаптивность. **✅ CharacterWindow v2 (5+ табов) реализован (2026-06-05), см. `docs/Character-menu/00_OVERVIEW.md`.** |
| [GDD_14: Visual & Art Pipeline](gdd/GDD_14_Visual_Art_Pipeline.md) | URP, CloudGhibli, шейдеры, постобработка |
| [GDD_15: Audio System](gdd/GDD_15_Audio_System.md) | AudioMixer, SFX, музыка, 3D звук |

### Content — Контентные системы
| Документ | Описание |
|----------|----------|
| [GDD_20: Progression & RPG](gdd/GDD_20_Progression_RPG.md) | XP, уровни 1-50, деревья навыков |
| [GDD_21: Quest & Mission System](gdd/GDD_21_Quest_Mission_System.md) | 5 типов квестов, цепочки гильдий. **✅ Реализовано в NPC+Quests v2** (2026-06-07..09). |
| [GDD_22: Economy & Trading](gdd/GDD_22_Economy_Trading.md) | Кредиты, ресурсы, спрос/предложение |
| [GDD_23: Faction & Reputation](gdd/GDD_23_Faction_Reputation.md) | 5 Гильдий, подполье, СОЛ, репутация. **✅ Reputation+NpcAttitude подсистема реализована в NPC+Quests v2.** |
| [GDD_24: Narrative & World Lore](gdd/GDD_24_Narrative_World_Lore.md) | Хронология, глоссарий, сюжетные арки |

**Полный каталог:** [`docs/gdd/GDD_INDEX.md`](gdd/GDD_INDEX.md)

### Связь GDD с этапами разработки

| Этап | Связанные GDD |
|------|--------------|
| Этап 1 (Прототип ядра) | GDD_01, GDD_10, GDD_11 |
| **Этап 1.4 + 1.9 (корабли + meta-требования)** | GDD_10, GDD_11, `docs/Ships/Key-subsystem/`, `docs/MetaRequirement/` |
| Этап 2 (Сетевой фундамент) | GDD_12, GDD_12.1, GDD_13 |
| Этап 2.1 (Масштабный мир) | GDD_12.1, GDD_02 |
| Этап 2.5 (Визуальный прототип) | GDD_02, GDD_14, GDD_15 |
| Этап 3 (RPG + Базовая торговля) | GDD_20, GDD_22, GDD_25 |
| Этап 3.5 (Торговые фракции) | GDD_22, GDD_23, GDD_25 |
| Этап 4 (Контент и полировка) | GDD_21, GDD_23, GDD_24, GDD_25 |
| Этап 5+ (Масштабирование) | GDD_12 (Future Architecture) |

---

## Мини-игры CloudTrader (docs/Fun/index.html)

**Версия:** v1.1.11 | **Файл:** `docs/Fun/index.html` (~4,034 строк) | **Стек:** Чистый HTML/JS/CSS, Canvas API, Google Fonts

> В рамках прототипирования MMO "Project C: The Clouds" параллельно разработана полноценная standalone HTML-игра **CloudTrader** — trading/adventure в стиле Ghibli над облаками. Цель: накопить **50,000 CR** через торговлю, контракты и управление кораблём.

### Системы мини-игр

| # | Система | Описание |
|---|---------|----------|
| 1 | **Торговля** | 34 товара, динамические цены (спрос/предложение), 6 городов, 5% налог на продажу |
| 2 | **Корабли** | 4 класса (Шлюпка→Карго-лайнер), модули C/B/A/S-tier (груз/скорость) |
| 3 | **Топливо и прочность** | Ремонт, заправка, деградация при полёте |
| 4 | **Контракты** | Доставка груза, награда × расстояние, долг при провале |
| 5 | **Контрабанда** | 5 запрещённых товаров, 13% шанс ареста |
| 6 | **Бар** | 15 ветвящихся сценариев (кости, драки, чёрный рынок, истории) |
| 7 | **Тюрьма** | 10 сценариев, 3 исхода (освобождение/побег/смерть) |
| 8 | **Пираты** | Top-down shooter на Canvas (800×600), волны врагов, boss fights |
| 9 | **Случайные события** | 10 ивентов (штормы, дефицит, рейды, караваны) |
| 10 | **День/ночь** | 4 фазы, CSS-переходы, молнии ночью |
| 11 | **Владение портами** | Апгрейд городов (1-10 ур.), пассивный доход, скидка на топливо |
| 12 | **Журнал** | Последние 10 событий, полный log |

### Пиратский Комбат (подробно)

**Механика:**
- Trigger: 13% базовый + 2% за каждую единицу расстояния
- Boss chance: 5% при distance ≥ 4
- Countdown: 5-4-3-2-1 перед боем
- Враги спавнятся сверху (y: -50 to -100) и летят к игроку

**Управление:**
| Платформа | Движение | Прицеливание | Стрельба |
|-----------|----------|--------------|----------|
| PC | WASD / Стрелки | Мышь | Клик / Пробел |
| Mobile | D-pad кнопки | Drag по aim-зоне | Автовыстрел при aim |

**Враги:**
- Обычные: 2 HP, 0.8-2.0 speed, 1.8s shoot interval
- Boss: 10 HP, 1.2 speed, 0.8s shoot interval, ×5 награда

**Награда:** `50 + score` CR при победе

### Архитектура

```
docs/Fun/index.html (4,034 строк)
├── HTML/CSS (~300 строк)
│   ├── CSS Variables (Ghibli palette)
│   ├── Mobile responsive
│   └── All screens/menus
├── Game State (~100 строк init)
│   └── state object (credits, cargo, ship, cities, etc.)
├── Core Loop (~200 строк)
│   ├── update() — tick system
│   ├── renderMap() — Canvas sky/clouds/cities
│   └── renderAll() — UI sync
├── Trading System (~300 строк)
│   ├── buyItem() / sellItem()
│   ├── calculatePrice()
│   └── MarketState per city
├── Ship System (~200 строк)
│   ├── refuel() / repair()
│   ├── upgradeShip() / equipModule()
│   └── travel() with damage calculation
├── Events (~150 строк)
│   ├── triggerRandomEvent()
│   ├── 10 event handlers
│   └── applyEventEffect()
├── Bar System (~400 строк)
│   └── 15 scenarios with choices
├── Prison System (~350 строк)
│   └── 10 scenarios, 3 outcomes
├── Pirate Combat (~600 строк)
│   ├── pirateGameLoop() — RAF
│   ├── updatePirateCombat() — movement/shooting
│   ├── renderPirateCombat() — Canvas draw
│   ├── bindPirateControls() — PC + Mobile handlers
│   └── enemy AI (chase + shoot)
└── Utils (~200 строк)
    ├── localStorage save/load
    ├── notify() — toast messages
    └── logEvent()
```

### Что реализовано

- ✅ Полный торговый цикл (6 городов, 34 товара, динамика)
- ✅ 4 корабли + 8 модулей
- ✅ Система топлива/урон/ремонт
- ✅ Контракты с долгами
- ✅ Контрабанда + тюрьма
- ✅ Бар (15 сценариев)
- ✅ Пиратский комбат (PC + Mobile)
- ✅ Случайные события (10)
- ✅ День/ночь с молниями
- ✅ 6 городов с уникальным товарами
- ✅ Порты с апгрейдом (1-10)
- ✅ Журнал событий
- ✅ localStorage save/load
- ✅ Mobile D-pad controls
- ✅ v1.1.11 version tag

### Ключевые решения

- **Single HTML file** — zero dependencies, portable
- **Canvas API** — map + combat (no WebGL)
- **CSS Variables** — theming + day/night cycle
- **Mobile-first responsive** — D-pad + aim zone for touch
- **Ghibli palette** — cream/amber/soft-cyan/purple

---

*Примечание: План ориентировочный. Длительность этапов может меняться в зависимости от размера команды, фидбека и технических сложностей. Рекомендуется использовать спринты по 2 недели с демонстрацией результатов.*
