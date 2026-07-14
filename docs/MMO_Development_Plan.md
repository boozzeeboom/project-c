# План разработки ММО "Project C: The Clouds" на Unity

**Последнее обновление:** 31 июля 2026 г. | **Текущая версия:** `v0.0.50 — NPC Unified Behavior + Stats Refactor + VFX`

> **Что нового (7–31 июля 2026):** **134 коммита, 17 эпиков.** Подробная ретроспектива: `docs/dev/retrospective_d1850f6c_to_HEAD.md`.
>
> **⚔️ Боевая система:** Полный цикл ranged/throwables (луки, арбалеты, ружья, гранаты) + projectile/throw visuals. Унификация иерархии оружия (3→2 типа). Skill Tree: persistence, cooldown per skill, slot bindings save/load, throwCount consumption. Targeting: Q/E cycling, outline highlight (URP shader), obstruction check.
>
> **🎯 VFX System (Phase 0-2):** Data model (11 полей в SkillNodeConfig) → Runtime (ISkillVfxProvider + SkillVfxService + ObjectPool) → 4 primitive prefabs + назначение на 27 скиллов. NPC VFX унификация.
>
> **💥 Damage Numbers:** World Space TMP + pool + Billboard + distance scaling для всех типов атак, AOE и критов.
>
> **📊 Stats Architecture Refactoring (T-STAT01..05):** Аудит (10 проблем) → 5 этапов исправлений. Единая формула Player/NPC: `StatsToFlat(tier) = tier * 5 + 10`. StatBucket вместо flat struct. StatsConfig → 3 SO. Equipment multipliers applied.
>
> **👾 NPC Unified Behavior (Phase 1-4, S01-S21):** NpcSocialBrain — центральный контроллер. Patrol, Flee, Grudge, Emotions, Morality, Group Tactics, Cover, Surrender, FactionSystem, VengeanceMemory, Full Idle. Код-ревью: 3 P0 + 2 P1 + 3 P2 исправлено. Статические реестры (AllBrains/AllCoverPoints/AllSitPoints) вместо FindObjectsByType.
>
> **🎓 NPC Skills:** NpcSkillSet + NpcSkillOverride (data layer) + multi-source assignment + NpcSkillSetEditor.
>
> **⚙️ NPC Systems:** Spawn Cycle Control (конечные волны + перезапуск), Loot Config (визуал + лут-таблица в инспекторе), NPC AOE/Ranged/Throwables + Debug visualization.
>
> **🔧 Crafting:** Глубокий аудит → 12/12 fixes (5 критических + 7 техдолгов).
>
> **⛏️ Mining:** Аудит + критические fixes (disconnect handler, WorldEventBus XP path, copy-paste bug).
>
> **📜 Quests:** Двойной аудит → диагностика утерянных ассетов (FactionDefinition, NpcDefinition, QuestDefinition).
>
> **🖥️ UI:** Переработка блока характеристик (цвета per-stat, tier-рамки, font-size 11px). DialogWindow fix. Кнопка «БРОСИТЬ» в инвентаре. Pickup перенесён с E на F.
>
> **📚 Документация:** `docs/dev/retrospective_d1850f6c_to_HEAD.md`, `docs/dev/ITERATIONS.md`, 6 аудитов, 15+ итерационных логов.

> **Предыдущее обновление (30 июня 2026):** **Character Customisation L1+L3+L4 ✅ + v0.0.35.** Полный цикл — 6 документов дизайна → 15 C# файлов (CustomisationSave, DTO, ClientState, Applier, UI Window) → M/F переключение, 6 пресетов тела, 2 стиля волос, цвета кожи/волос/одежды, AnimatorOverrideController. UI по паттерну SkillTreeWindow. Bug #1 (domain reload → heightScale=0 → персонаж невидим) исправлен.
>
> **NPC Enemy System P0-P2 ✅:** NpcBrain FSM (Idle→Chase→Attack→Dead), NpcSpawner с surface validation/rate-limit/leash, goblin prefab (NetworkObject), 5-state AnimatorController, passive/aggressive/neutral поведение, loot pickup.
>
> **Real-Time Combat MVP ✅ + Skills Phase 1-4 ✅:** 24+ файла боевого ядра — DamageCalculator (hit/miss/crit/armor/skills), AOE (5 формул), SkillTreeWindow UI (graph+zoom/pan+badges), 27+ Skill SO, SkillAnimationPlayer (runtime override controller), Weapon/Armor/Technique catalogs, raycast targeting.
>
> **Character Animations 🔄 v0.5.1:** BlendTree directional movement (MoveX/MoveY), combat clips (idle/walk/run/attack/death), female override controller.
>
> **Input System ✅ Phase 1-2.5:** InputBindingsConfig SO (31 binding), EscMenu, key rebinding, save/load/reset через PlayerPrefs.
>
> **Equipment Visual Phase 2 ✅:** CharacterEquipmentVisualApplier (bone mapping, visual sockets), equip bug fix (rate-limit N callback).
>
> **NPC Ships M3.2.15:** ✅ Подробнее — см. ретроспективу `docs/NPC_others_peacfull/pc_ship/99_RETROSPECTIVE.md`.

> **Предыдущее обновление (24 июня 2026):** **NPC Ships M3.2.15 — первый рабочий round-trip ✅.** 27 коммитов, полный ретроспективный анализ: `docs/NPC_others_peacfull/pc_ship/99_RETROSPECTIVE.md`.

> **Предыдущее обновление (17 июня 2026):** **T-CARGO-06: Per-instance лимиты трюма + модульное расширение.** Cargo лимиты перенесены из статического `ShipClassLimits.Get(cls)` в Inspector-editable поля `ShipController`. ~3 ч. См. `docs/Ships/cargo_system/CARGO_REFACTOR_PLAN_2026-06-17.md` §T-CARGO-06.

> **Предыдущее обновление (17 июня 2026):** **Cargo System v2 (Trade-интеграция) — рефакторинг завершён.** Cargo больше не отдельный `MonoBehaviour` (ProjectC.Player.CargoSystem удалён) — теперь подсистема Trade: `ProjectC.Trade.Core.CargoData` (POCO) + `TradeWorld._cargoCache[shipId]` + `OnCargoChanged` event. Штраф скорости `GetSpeedPenalty` — серверная формула, реплицируется через `NetworkVariable<float>` в `ShipController._serverCargoPenalty`. Маппинг `ShipFlightClass → ShipClass` через `ShipClassMappingConfig` SO (inspector-editable, `Resources/ShipClassMapping.asset`). Столкновения: `ShipController.OnCollisionEnter` → `TradeWorld.TryDamageCargo` → dangerous leak (5%×10%), fragile marked. Параметры — `ShipCollisionDamageConfig` SO (`Resources/ShipCollisionDamage.asset`). Удалены: legacy `CargoSystem.cs` MonoBehaviour, 3 broken-refs в `WorldScene_0_0.unity`. ~10.5 ч, 5 этапов. См. `docs/Ships/cargo_system/CARGO_REFACTOR_PLAN_2026-06-17.md`.

> **Предыдущее обновление (17 июня 2026):** **Composite Ship Architecture — Phase 0+1.** Корабль больше не монолитный блок — теперь это составная конструкция (летающая баржа). Реализованы: `ShipRootReference` (маркер на любой части корабля), `ShipComponentLocator` (единый поиск ShipController), `PilotSeatController` (место пилота как отдельный ребёнок), `DoorController` (slide-анимация E-key). Ключевые изменения: игрок НЕ пропадает при посадке (стоит в кресле), парентируется к корню корабля (физика не дергается). Камера переключает target на `ShipRoot`. `InteractableManager.FindNearestShip` использует PilotSeat коллайдер для чёткой зоны посадки. Полный анализ и roadmap: `docs/Ships/analysis-composite-ship.md`, `docs/Ships/roadmap-integration.md`.
> 3 характеристики: Сила (майнинг), Ловкость (ходьба/прыжок), Интеллект (квесты/диалоги) с геометрическим ростом по тирам. 13 слотов экипировки (10 одежда + 3 модуля). Effective stats (base + equip bonuses). [НАДЕТЬ] из инвентаря, [СНЯТЬ] в одежде. 8 навыков (4 боевых + 4 социальных).
> **UI-рефакторинг:** CharacterWindow полностью переработан — single-page ПЕРСОНАЖ layout (характеристики + одежда + модули + навыки). Inventory split layout (ScrollView list + detail panel). Устранено пустое пространство под списком, фильтр "Все типы" убран с инвентаря. USS-стили переписаны с нуля для корректного flex-растяжения.
> **Технические фиксы:** динамическое разрешение ID предметов (FindItemIdByName), auto-registration ClothingItemData в _itemDatabase, fix unequip→inventory (AddItemDirect + ID fallback по slot), save/load (flush on disconnect + auto-save 30s), effective stats inline в SendSnapshotToOwner.
> См. `docs/Character/00_README.md`, `docs/Character/08_ROADMAP.md`.
>
> **Предыдущее обновление (13 июня 2026):** **Resources Exchanger (обменник ресурсов) — MVP завершён.** Мост между двумя системами предметов: pickable (инвентарь, 1 кг) ↔ boxed (склад, 100 кг). 4-я вкладка «Обменник» в MarketWindow. Pack: 100 осколков → 1 ящик на складе. Unpack: 1 ящик → 100 осколков в инвентарь. InventoryWorld.MAX_SLOTS увеличен до 1000 (конфигурируется в инспекторе). Исправлены: спавн scene-placed NetworkObject в BootstrapScene (OnServerStarted), PushSnapshot инвентаря и склада после каждой операции, группировка предметов по itemId в UI. 5 тикетов T-E01–T-E05, ~30 ч работы. См. `docs/Markets/Resources_exchanger/01_ANALYSIS.md`.
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
   - ✅ Ветка `main` на GitHub

3. **Базовая архитектура:** ✅
   - ✅ Unity Netcode for GameObjects (вместо Mirror)
   - ✅ Прототип серверной части: .NET 8 Console App
   - ✅ Документация протокола клиент-сервер (позже)

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
- ✅ **Ветер для персонажа (✅ 2026-07-01)** — `WindManager` + `WindZone` применяются к персонажу через `NetworkPlayer.ProcessMovement`. Правила по состоянию: на палубе/на земле/в прыжке — ветер с разными коэффициентами. Профили: Constant, Gust, Shear.

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
- ✅ ⭐ **Engine ON/OFF + IDLE (2026-07-05)** — `_netEngineRunning` NetworkVariable. Enter — включить/выключить. IDLE-расход 0.05 fuel/s (корабль «завис» без пилота). Выход (F) разрешён всегда на любой скорости. NPC всегда ENGINE ON. HUD индикатор в K3. См. `docs/Ships/ENGINE_POWER_STATE.md`.
- ✅ ⭐ **Ship Damage Subsystem (2026-07-05)** — `ShipHull` (NetworkBehaviour, `IDamageTarget`). HP по классам (100/200/400/600), armorHull=5. Два источника: столкновения (формула `(energy−8)×0.5`, cap 50) + боевое оружие. 0 HP = «сломан» (скорости ×0.1, груз обнулён, `IsAlive()=true` — корабль не деспаунится). Ремонт в доке за 300 кр. Три защиты от ложных ударов при стыковке (minRelativeSpeed 3 м/с + postUndockGrace 3 сек + IsDocked guard). См. `docs/Ships/damage_subsystem/`.
- ✅ ⭐ **Repair Manager (2026-07-04..05)** — доковый менеджер модулей: `ShipModuleServer` (RPC install/remove/sell/repaint/hull-repair), `RepairManagerWindow` (UI Toolkit), Ship Observation Camera (FlyToShip + ▲▼◀▶), Ship Repainting (цвет + кредиты), Module Visual Preview (Editor tool). См. `docs/Ships/Modul_system/`.
- ✅ ⭐ **Ship Key subsystem (R2-SHIP-KEY-001, 2026-06-06)** — ~~физический ключ-предмет для запуска. `ShipKeyBinding` + `ShipKeyServer` + `ShipKeyClientState` + `ShipKeyToast`.~~ **Obsolete — удалено в P1 рефакторинге (2026-07-05).** Заменено на `KeyRodInstanceWorld` (static facade, 0 reflection, 1 источник правды) + `MetaRequirementRegistry`. См. `docs/Ships/Key-subsystem/` + `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md`.
- ✅ Рефакторинг кода — ShipController.cs (2000+ строк), разделение на подсистемы

### 1.5 Переключение режимов (пеший ↔ корабль) ✅
- ✅ F — подойти к кораблю (< 5м) → сесть/выйти
- ✅ PlayerStateMachine — управление состояниями
- ✅ Камера адаптируется к режиму
- ✅ Проверка при выходе: ~~корабль на земле ИЛИ скорость < 2 м/с~~ → **снято (2026-07-05):** выход разрешён всегда, на любой скорости (двигатель остаётся в текущем состоянии — ON=зависнет, OFF=упадёт)
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
  - ✅ `TryDrop / TryMove / TryUse` (InventoryServer) — TODO; UI кнопки есть, RPC не подключены
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
  - ✅ Таб "Персонаж" — T-P01..T-P18 (Character Progression: STR/DEX/INT + 13 экипировки + 8 навыков, single-page)
  - ✅ Таб "Репутация" — реализован в NPC+Quests v2 (см. секцию 1.7)
  - ✅ Таб "Контракты" — реализован в NPC+Quests v2 (T-Q11..T-Q20+, см. `docs/Character-menu/sub_contracts-tab/`)
  - ✅ Таб "Корабль" — MVP-заглушка (хард-стат), план в `docs/Character-menu/00_OVERVIEW.md` §3
  - ✅ Visual fix 2026-06-05: characterWindowUss привязан к правильному USS-ассету (был UXML-bug); все class-стили с `!important` (UnityDefaultRuntimeTheme fix)
- ✅ ⭐ **SkillTreeWindow** (UI Toolkit) — интерактивный граф навыков: zoom/pan, learned/available/locked узлы, badge-счётчики, tooltip при наведении. Паттерн — CharacterWindow (Clear+CloneTree+Resources.Load fallback). 5 FIX'ов (см. `docs/Character/Skills/UI_TOOLKIT_GUIDE.md`).
- ✅ ⭐ **Input System Phase 1-2.5** — `InputBindingsConfig` SO (31 биндинг), EscMenu с UI Toolkit окном, полноценный rebinding (Listen → Assign → Save), сброс на defaults, сериализация в PlayerPrefs.
- ✅ ⭐ UIManager — централизованный менеджер UI (приоритеты, z-ordering, input management)
- ✅ ⭐ UIFactory — фабрика UI компонентов (8 методов, устранено 120 строк дублирования)
- ✅ ⭐ UITheme — ScriptableObject темы (51+ цвет → UITheme.Default, авто-создание)
- ✅ ⭐ TextMeshPro migration — все UI на TMP (убран legacy UnityEngine.UI.Text)
- ✅ ⭐ Cursor management — lock/unlock при открытых UI
- ✅ ⭐ Input priority system — CanReceiveInput, Escape закрывает верхнюю панель
- ✅ ⭐ ConfirmationDialog — создан (отключён для торговли по фидбеку)
- ✅ ⭐ Audio feedback infrastructure — готовы методы PlayClick/PlayError/Open/Close
- ✅ Эмодзи устранены из TMP UI (📋📦⚡📝📢 → [Контракт] [Груз] [Срочный])
- ✅ Оценка UI системы: 4.5/10 → 7/10 (+55%)

### 1.7.1 Docking Stations (MVP) ✅ ЗАВЕРШЕНО (2026-06-20)

**Цель:** Реализовать стыковочные порты — игрок подлетает в зоне связи, открывает CommPanel, запрашивает посадку, получает назначенный pad, летит к нему, при касании — двигатель блокируется (Docked). Двусторонняя связь с диспетчером.

**Серверный hub:**
- ✅ `DockingServer` (NetworkBehaviour, scene-placed в `BootstrapScene`) — `RequestDockingRpc`, `RequestConfirmAssignmentRpc` (Q7 двусторонняя), `RequestTakeoffRpc`, `NotifyTouchedDownRpc`
- ✅ `DockingWorld` (server-only singleton MonoBehaviour, DontDestroyOnLoad) — single source of truth занятости pads (`_occupiedPads: Dictionary<padKey, clientId>`)
- ✅ Rate limiting (copy-paste из `QuestServer`), null-safety на всех string DTO полях
- ✅ `ScanExistingOccupants()` при старте — корабли, уже стоящие на падах, регистрируются как occupants

**Клиентский state:**
- ✅ `DockingClientState` (singleton, `[RuntimeInitializeOnLoadMethod] AutoCreate`) — events `OnAwaitingConfirmation`, `OnAssignmentFailed`, `OnStatusReceived`, `OnTakeoffApproved`, `OnTouchedDown`
- ✅ `DockingZoneRegistry` (static) — `LocalPlayerStation` / `LocalPlayerShipStation` для T-key check

**UI:**
- ✅ `CommPanelWindow` (UI Toolkit) — двусторонний диалог с диспетчером, кнопки `[Запросить посадку]`, `[Хорошо]`, `[Отбой]`, `[Отменить запрос]`, `[Отстыковка]`. Расположен справа (`right:24px; top:50%`), компактный (320×~200px). Не модальный (без затемнения экрана). Theme + `!important`-стили по канону `docs/UI/UI_TOOLKIT_GUIDE.md`.
- ✅ `DockPadVisualMarker` (runtime Quad-метка на каждом паде) — создаёт Quad + Unlit/Color материал, читает `_padBox.IsShipInside`. ⚠️ Цвет не меняется корректно — **требует переработки** (тикет `T-DOCK-14`).

**FSM / физика:**
- ✅ `ShipController._netIsDocked` (NetworkVariable<bool>, server-write) — сервер-авторитативный флаг
- ✅ `EnterDocked()` / `ExitDocked()` — `_rb.isKinematic = true/false`, обнуление velocity
- ✅ `SendShipInput` — guard `if (_netIsDocked.Value) return;` (owner + server defense in depth)

**SO + ассеты:**
- ✅ `DockStationDefinition` (паспорт станции) — stationId, locationId, displayName, padLayout, voiceLines, landingWindowSeconds
- ✅ `DockPadLayout` (список pads) — `Pads[]` с `padId`, `localPosition`, `localEulerAngles`, `compatibleShipClasses[]`, `triggerBoxSize`. `DefaultTriggerBoxSize` для всех pads.
- ✅ `DispatcherVoiceLines` (фразы по контексту: Greeting, Assigning, Assigned, AwaitingConfirmation, Touchdown, WrongPad)
- ✅ `DockStationDefinition_Primium.asset` (5 pads: PAD-001..005, разные классы совместимости), `DockPadLayout_Primium.asset`, `DispatcherVoiceLines_Default.asset`
- ✅ `CommPanelPanelSettings.asset` (themeUss=UnityDefaultRuntimeTheme)

**Сцена:**
- ✅ `[DockStation_Primium]` в `WorldScene_0_0.unity` — root с `DockStationController` + `OuterCommZone` (radius=1000m), 5 child trigger-boxов `Pad_001..005` с `DockingPadTriggerBox` + `DockPadVisualMarker`
- ✅ `[CommPanelWindow]` в `BootstrapScene.unity` — UIDocument + CommPanelWindow + PanelSettings

**HUD интеграция (T-DOCK-HUD):**
- ✅ `ShipHudController.K5 (DISPATCH)` — подключена к `DockingZoneRegistry.LocalPlayerShipStation`. Красная точка ● вне зоны, зелёная в зоне. Показывает `DISPATCHER STN-PRM-001` + `REGION Примум` + подсказка `T — связаться`.

**Подсистемы / Edge cases:**
- ✅ Физическая проверка падов — `Physics.OverlapBox` через `DockingWorld.AssignPad` (нельзя сесть на занятный физически)
- ✅ Совместимость классов — проверка `IsCompatible` с fallback override из trigger-box
- ✅ Initial state detection в UI — если `Ship.IsDocked == true`, CommPanel показывает Docked state
- ✅ Auto-close после одобрения отстыковки — `HandleTakeoffApproved` → `SetOpen(false)`

**Что НЕ сделано (Phase 2 / Phase 1.5):**
- ✅ **Departure subsystem** — отдельная подсистема вылета по запросу через T (`08_DEPARTURE_SUBSYSTEM.md`)
- ⏳ **Автопилот стыковки** (модуль `MODULE_AUTO_DOCK`) — GDD-10 §4.2 P2-T2
- ✅ **NPC-корабли на падах (M3.2)** — Полный round-trip: док → взлёт → полёт → CommZone → пад → стыковка → обратно. 4 NPC, 2 станции. Документация: `docs/NPC_others_peacfull/pc_ship/`.
- ⏳ **`DockPadVisualMarker` v2** — переделка маркера с правильной реакцией на `IsShipInside`

**Документация:**
- ✅ `docs/Docking_stations/AUDIT_AND_REFACTOR.md` — полный аудит + 5 фаз рефакторинга
- ✅ `docs/Docking_stations/CHANGELOG.md` — лог всех фиксов (RPC null-safety, UI реверт, двигатель-блокировка, initial scan, отстыковка)
- ✅ `docs/Docking_stations/REFACTOR_PLAN.md`, `docs/Docking_stations/BUG_AUDIT.md` — предыдущие отчёты

**Stats:**
- **+12** новых C# файлов (`Assets/_Project/Scripts/Docking/**`, ~50 KB)
- **+3** SO файлов
- **+1** PanelSettings
- **+1** обновлён `ShipHudController` (K5 Dispatch column)
- **+1** новый документ `docs/UI/UI_TOOLKIT_GUIDE.md` (~24 KB)
- **+3** документа в `docs/Docking_stations/` (AUDIT_AND_REFACTOR, CHANGELOG entry)

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
- ✅ **Удалены в P1 рефакторинге (2026-07-05)** — `ShipKeyBinding.cs`, `ShipKeyServer.cs`, `ShipKeyClientState.cs`, `ShipKeyToast.cs`, `ShipOwnershipRegistry.cs`, `KeyRodInstanceBinding.cs` (7 файлов, -1139 строк). `NetworkManagerController` больше не создаёт `ShipKeyClientState`. `NetworkPlayer` — убраны `ReceiveShipKey*TargetRpc`.
- ✅ Единый источник правды: `KeyRodInstanceWorld` (static facade, 0 reflection). `ShipController` создаёт `KeyRodInstance` в `OnNetworkSpawn`. См. `docs/Ships/Key-subsystem/31_KEY_ANALYSIS_2026-07-21.md` + `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md`.

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
**Преемник:** R2-SHIP-KEY-003 — уникальные экземпляры ключей (2026-06-19, v18-v20 MVP завершён). Каждый корабль имеет уникальный KeyRodInstance (server-side registry `KeyRodInstanceWorld`). Подбор/дроп с передачей instanceId. Persistence через `JsonKeyRodInstanceRepository`. Drop↔pickup реактивирует Lost instance, не создаёт дубль. UI: вкладка "КОРАБЛЬ" в CharacterWindow показывает только корабли игрока (`MyShipsTab`). TAB-колесо: сектор 1 = "ВЛАДЕНИЕ" (Equipment + Key). См. `docs/Ships/Key-subsystem/99_CHANGELOG.md` (v1–v20) + `28_KEY_ARCHITECTURE_REVIEW.md` (глубокий обзор).

**Migration guide:** `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` (337 строк).
**Предшественник:** `docs/Ships/Key-subsystem/00_OVERVIEW.md` (Ship Key MVP, R2-SHIP-KEY-001).

---

### 1.10 Сбор ресурсов (Resource Gathering / Mining) ✅ MVP + CRITICAL Fixes (T-G01–T-G07, 2026-06-10 + T-MINE01, июль 2026)

**Новая подсистема:** интерактивные 3D-объекты в мире — подойти и нажать F → сбор N секунд → предмет в инвентарь.

**Принцип:** «пусть бегает и рубит» — движение не прерывает сбор. Tool check через MetaRequirement (Кирка → Руда). Возобновляемые узлы с cooldown.

| Компонент | Назначение | Тикет |
|-----------|-----------|-------|
| `ResourceNodeConfig` (SO) | Параметры: время сбора, кол-во harvests, cooldown, результат, анимация | T-G01 ✅ |
| `ResourceNode` (NetworkBehaviour) | State machine (Idle/Occupied/Depleted/Cooldown) + MetaReq tool check + client animation | T-G02 ✅ |
| `ResourceNodeConfig` — расширение (T-G08) | Tool check через ItemId, Anim trigger parameter, 3 доп. конфига (Iron, Copper, Herb) | T-G08 ✅ |
| Player gather animation (T-G09) | Scale-pulse + emission flash, интеграция с CharacterAnimationController (StateHasher < 0.3f) | T-G09 ✅ |
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

**Документация:** `docs/Mining/00_OVERVIEW.md` + `10_DESIGN.md` + `20_IMPLEMENTATION_PLAN.md` + `ROADMAP.md` + `99_CHANGELOG.md` + `AUDIT_2026-07-11.md` + `AUDIT_2026-07-12_DEEP.md`.

**Фиксы (T-MINE01, июль 2026):** disconnect handler, WorldEventBus XP path, copy-paste XP Quantity bug, prefab update, Tree.asset, StatsConfig deprecation. См. `docs/dev/retrospective_d1850f6c_to_HEAD.md` §2.16.

### 1.11 Крафт-система (Crafting) ✅ MVP + 12/12 Fixes (T-C01–T-C07c, 2026-06-11 + T-CRAFT01, июль 2026)

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

**Фиксы (T-CRAFT01, июль 2026):** Глубокий аудит → 5 критических багов (B1-B5) + 7 техдолгов (T1-T7) = 12/12 исправлено. L1: `CraftingCompletedEvent` в `WorldEventBus`. См. `docs/Crafting_system/AUDIT_2026-07-09.md`, `docs/Crafting_system/ITERATIONS.md`.

**Документация:** `docs/Crafting_system/00_OVERVIEW.md` + `10_DESIGN.md` + `ROADMAP.md` + `AUDIT_2026-07-09.md`.

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

### 1.13 NPC + Quests v2 (полная подсистема) ✅ ЗАВЕРШЕНО (M1–M19, 2026-06-09..13) + Аудиты + Dialog Fixes (июль 2026)
**Цель:** Полноценная система квестов и диалогов с NPC, от создания данных до выполнения в игре и редакторского инструментария.

| Компонент | Описание | Статус |
|-----------|----------|--------|
| `QuestServer` (NetworkBehaviour) | 9 RPC, tick 5s, rate-limit 30/min/client | ✅ M1-M4 |
| `QuestWorld` (POCO) | Quest state, reputation, attitude, flags | ✅ M5-M8 |
| `QuestClientState` (singleton) | 6 событий, 8 DTOs | ✅ M6 |
| `DialogWindow` (UIDocument) | Typewriter 40cps, F-skip, 4 FIX'ы | ✅ M9 |
| `QuestTracker` (HUD) | Track/Untrack, top-right | ✅ M10 |
| `NpcController` | Trigger zone, E-key chain | ✅ M11 |
| Persistence | JsonQuestStateRepository, immediate save | ✅ M8 |
| Multi-stage | onEnter/onComplete, TryAdvanceStage | ✅ M13 |
| ItemRegistry | SO, 32 items, id↔ItemData | ✅ M14 |
| Toast | Queue-based, 4 события | ✅ M15 |
| QuestDatabaseWindow | Editor: Tools→Quests→Explorer | ✅ M16 |
| QuestNodeGraph | Readonly + Editable (M18) | ✅ M17-M18 |
| CSV Import/Export | 3 входа (quests + npcs + dialogs), 1 кнопка | ✅ M19 |

**Stats:** ~8400 строк кода, 106 NPC, 802 квеста, 2 DialogTree, 6 CSV файлов.

**Аудиты (июль 2026):** Двойной глубокий аудит → критическое открытие: квестовые ассеты (FactionDefinition, NpcDefinition, QuestDefinition) утеряны — GUIDs в QuestDatabase висят в никуда. См. `docs/NPC_quests/DEEP_AUDIT_2026-07-09.md` + `DEEP_AUDIT_2026-07-13.md`.
**DialogWindow fix (T-UI04):** Текст NPC всегда виден сверху, кнопки квестов прокручиваются. Fix: 85vh → 520px (vh не поддерживается Unity USS).

**Документация:** `docs/NPC_quests/08_ROADMAP.md` + `docs/NPC_quests/M19_CSV_PIPELINE_v2.md` + `docs/NPC_quests/ITERATIONS.md`.

### 1.14 Composite Ship Architecture (Phase 0–1) ✅ MVP ЗАВЕРШЁН (2026-06-17)

**Цель:** Перейти от монолитного корабля (1 куб с ShipController) к составному — летающая баржа из иерархии GameObjects. Фундамент для всех будущих ship-систем.

**Что реализовано:**

| Компонент | Файл | Назначение | Статус |
|-----------|------|------------|--------|
| `ShipRootReference` | `Scripts/Ship/ShipRootReference.cs` | Маркер на любой части корабля. Кеширует ShipController/Rigidbody/NetworkObject с корня | Phase 0 ✅ |
| `ShipComponentLocator` | `Scripts/Ship/ShipComponentLocator.cs` | Static helper: FindShipController(GameObject) от любой части | Phase 0 ✅ |
| `ShipRoot` on ShipController | `Scripts/Player/ShipController.cs` | `public Transform ShipRoot => transform.root` | Phase 0 ✅ |
| `PilotSeatController` | `Scripts/Ship/PilotSeatController.cs` | Триггер места пилота. `_controller.enabled = false` при посадке | Phase 1 ✅ |
| Camera → ShipRoot | `Scripts/Core/ThirdPersonCamera.cs` | `SetTargetMode(target, isShip)` — атомарная смена target+режима | Phase 1 ✅ |
| Player parenting | `Scripts/Player/NetworkPlayer.cs` | `SetParent(ShipRoot)` при посадке, `SetParent(null)` при выходе | Phase 1 ✅ |
| Player stays visible | `Scripts/Player/NetworkPlayer.cs` | _playerRenderers НЕ отключаются — стоит в кресле | Phase 1 ✅ |
| `DoorController` | `Scripts/Ship/DoorController.cs` | Slide-анимация (Lerp), локальная, E-key toggle | Phase 3 ✅ |
| `DoorController` + MetaRequirement | — | Дверь-замок: требуется ключ для открытия | Phase 3 ⬜ |

**Изменения в существующих подсистемах:**

| Подсистема | Изменение | Статус |
|-----------|-----------|--------|
| `InteractableManager.FindNearestShip` | Приоритет PilotSeat коллайдера (чёткая зона посадки) | ✅ |
| `ShipModuleManager` | `GetComponentsInChildren<ModuleSlot>()` — уже ищет в детях | ✅ Готов (не менялся) |
| `WindZone` | `GetComponentInParent<ShipController>()` — уже находит корень | ✅ Готов (не менялся) |
| `MeziyModuleActivator` | Нужен `GetComponentsInChildren<MeziyNozzle>()` вместо serialized ссылки | ⏳ Phase 4 |

**Архитектурные решения:**
- ShipController **остаётся на корне** — не переносим на место пилота
- Один Rigidbody на корне — все дети без Rigidbody
- Дочерние объекты **без NetworkObject** (для MVP)
- Парентинг игрока к ShipRoot — фикс физики («дергалось» без этого)

**Документация:**
- `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` — обзор (3 KB)
- `docs/Ships/analysis-composite-ship.md` — полный анализ (29 KB, 12 разделов)
- `docs/Ships/roadmap-integration.md` — план реализации (10 KB, 224 строки)
- `docs/gdd/GDD_10_Ship_System.md` §14 — GDD-дополнение

---

### 1.15 Real-Time Combat + Skills (T-RTC, T-CB, T-INP) ✅ MVP ЗАВЕРШЁН (2026-06-25..28) + Расширения (июль 2026)

**Цель:** Добавить полноценный real-time combat с DamageCalculator, AOE, скиллами, деревом навыков, прицеливанием и анимациями атак.

#### Боевое ядро (T-RTC):

| Компонент | Файл / Path | Назначение | Статус |
|-----------|-------------|------------|--------|
| `DamageCalculator` | `Scripts/Combat/DamageCalculator.cs` | hit/miss/crit/armor/skills — 5 формул | T-RTC-01 ✅ |
| `AOEHelper` | `Scripts/Combat/AOEHelper.cs` | 5 формул AOE: sphere, box, capsule, cone, radial | T-RTC-02 ✅ |
| `CombatTargeting` | `Scripts/Combat/CombatTargeting.cs` | Raycast-прицеливание (наводить на врага по R) | T-RTC-03 ✅ |
| `WeaponCatalog` (SO) | `Data/Combat/WeaponCatalog.asset` | SO каталог оружия | T-RTC-04 ✅ |
| `ArmorCatalog` (SO) | `Data/Combat/ArmorCatalog.asset` | SO каталог брони | T-RTC-04 ✅ |
| `TechniqueCatalog` (SO) | `Data/Combat/TechniqueCatalog.asset` | SO каталог техник | T-RTC-04 ✅ |

#### Skill System (T-CB / T-INP):

| Компонент | Файл / Path | Назначение | Статус |
|-----------|-------------|------------|--------|
| `SkillNodeConfig` (SO) | `Data/Combat/Skills/` (27+ файлов) | 4 типа: weapon/armor/technique/passive | T-CB-05 ✅ |
| `SkillTreeConfig` (SO) | `Data/Combat/Skills/SkillTreeConfig.asset` | Дерево навыков: связи, позиции, иконки | T-CB-06 ✅ |
| `SkillTreeWindow` (UIDocument) | `UI/Resources/UI/SkillTreeWindow.{uxml,uss}` | Интерактивный граф (zoom/pan/badges/tooltip) | T-CB-07 ✅ |
| `SkillManager` | `Scripts/Combat/SkillManager.cs` | Singleton: tree, node state, points, learn/unlearn | T-CB-08 ✅ |
| `SkillAnimationPlayer` | `Scripts/Combat/SkillAnimationPlayer.cs` | Override controller по SkillType, runtime swap | T-CB-09 ✅ |
| `SkillModifier` | `Scripts/Combat/SkillModifier.cs` | DamageModifier → DamageCalculator интеграция | T-CB-11 ✅ |
| `DamageEventArgs` | `Scripts/Combat/DamageEvents.cs` | events: OnDealDamage/TakeDamage/SkillUsed | T-CB-13 ✅ |
| `WeaponItemData` | `Scripts/Items/WeaponItemData.cs` | Подкласс ItemData (damage/range/skillType) | T-CB-19 ✅ |

**Stats:** 24+ C# файла, ~35 KB кода, 27+ SkillNodeConfig SO.

**Key design decisions:**
- `DamageCalculator` — **server-authoritative**, damage deal via NetworkRPC
- Skill tree — **client-predicted** (immediate UI response), validated server-side on use
- `SkillAnimationPlayer` использует runtime `AnimatorOverrideController` — swap по `SkillType`
- Inventory integration: `EquipmentWindow` → `SkillManager` → `SkillTreeWindow`
- `DamageCalculator` вызывает `SkillModifier` chain (damage + armor penetration + etc.)

**Документация:** `docs/Character/Skills/`, `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md`, `100_TARGET_HIGHLIGHT_AND_SWITCHING.md`, `110_DAMAGE_NUMBERS.md`, `docs/Character/Skills/Battle/85_VFX_DESIGN.md`, `docs/dev/retrospective_d1850f6c_to_HEAD.md`.

---

### 1.16 NPC Enemy System (T-NPC) ✅ P0-P2 + Unified Behavior Architecture + Skills + Spawn/Loot (Июнь–Июль 2026)

**Цель:** Добавить враждебных NPC (goblins) с FSM, спавном, лутом, анимациями и базовым AI.

| Компонент | Файл / Path | Назначение | Статус |
|-----------|-------------|------------|--------|
| `NpcBrain` | `Scripts/NPC/NpcBrain.cs` | FSM: Idle→Chase→Attack→Dead, 3 типа (passive/aggressive/neutral) | P0 ✅ |
| `NpcBrain.Config` | nested struct | aggroRange, attackRange, moveSpeed, damage, hp, lootTable, respawnTime | P0 ✅ |
| `NpcSpawner` | `Scripts/NPC/NpcSpawner.cs` | SurfaceNav validation, rate-limit, server-authoritative leash (30m) | P1 ✅ |
| `Goblin` prefab | `Prefabs/NPC/Goblin.prefab` | NetworkObject + NpcBrain + NavMeshAgent + Animator + CapsuleCollider | P1 ✅ |
| `NpcAnimatorController` | `Animations/NPC/NpcAnimator.controller` | 5 states: Idle/Walk/Run/Attack/Dead | P2 ✅ |
| `NpcAttackAnimationEvent` | `Scripts/NPC/NpcAttackAnimationEvent.cs` | AnimationEvent → NpcBrain.OnAttackHit → DamageCalculator | P2 ✅ |
| Loot — `NpcLootTable` (SO) | `Data/NPC/LootTables/` | Нода лута: массив предметов + вес | P2 ✅ |
| Pickup on death | `NpcBrain.Dead()` | Server-spawn PickupItem при смерти + loot roll | P2 ✅ |

**Stats:** +8 C# файлов, ~2.5 KB Goblin prefab, 12 материалов (URP/Lit).

**Scene placement (WorldScene_0_0):**
- `[NpcSpawner_Test]` @ (40060, 2502, 40060) — спавнит goblin с пассивным/агрессивным режимом
- Goblin spawn на платформе в ПРИМУМ-регионе

**Key decisions:**
- NavMeshAgent на сервере (host-authoritative) — NPC движется на хосте, клиент видит через NetworkTransform
- `surfaceValidation` — проверяет, что NavMeshAgent.SamplePosition успешен перед спавном
- Leash 30m — NPC возвращается при отдалении от точки спавна
- LootTable — SO с weighted random, спавн PickupItem при смерти

**Известные ограничения:**
- ⏳ Multiplayer sync: NPC позиция синхронизируется через NetworkTransform, но анимации — только на хосте
- ⏳ P3: Pathfinding sync (NavMesh только на хосте)

---

### 1.17 Character Customisation (T-CUS) ✅ L1+L3+L4 ЗАВЕРШЁН (2026-06-28..30)

**Цель:** Позволить игроку изменять внешность персонажа: пол, пресет тела, рост/ширину, цвета кожи/волос/одежды, стиль волос.

| Компонент | Файл | Назначение | Статус |
|-----------|------|------------|--------|
| `CharacterBodyType` (enum) | `Customisation/CharacterBodyType.cs` | Male/Female | L1 ✅ |
| `BodyPresetId` (enum) | `Customisation/BodyPresetId.cs` | 6 пресетов: Default/Athletic/Heavy/Slim/Elder/Young | L1 ✅ |
| `HairStyleId` (enum) | `Customisation/HairStyleId.cs` | 2 стиля: Bald/Short | L3 ✅ |
| `CustomisationSave` | `Customisation/CustomisationSave.cs` | JsonUtility DTO: colors, presets, clothing overrides | L1+L3+L4 ✅ |
| `CustomisationClientState` | `Customisation/CustomisationClientState.cs` | Singleton + events + ApplyCustomisationSnapshot | L3 ✅ |
| `ClothingOverrideData` | `Customisation/ClothingOverrideData.cs` | per-item color override (not implemented in UI yet) | L4 ✅ |
| `CustomisationWindow` (UI) | `Customisation/CustomisationWindow.cs` | Full-screen overlay, 3 секции: тело/лицо/одежда | L1+L3+L4 ✅ |
| `UICustomisationManager` | `Customisation/UI/CustomisationManager.cs` | UI → CustomisationSave → Applier | L3+L4 ✅ |
| `CharacterCustomisationApplier` | `Customisation/CharacterCustomisationApplier.cs` | Применяет snapshot на MeshRenderer + AnimatorOverrideController | L3+L4 ✅ |
| `AnimatorOverrideController` импорт | — | Load(CharacterBodyType + BodyPresetId + HairStyleId) → runtime | L3 ✅ |
| `CharacterWindow` таб "Персонаж" | — | T-P01..T-P18: STR/DEX/INT статистика + экипировка + навыки | L1 ✅ |

**Bug #1 (фикс:** Domain reload → `CustomisationClientState.CurrentSnapshot` = struct default (heightScale=0) → `_visualRoot.localScale` = (0,0,0) → персонаж невидим. Исправлен: nullable fallback + `OnBeforeSerialize` guard.

**Stats:** +15 C# файлов, ~6 документов дизайна (`docs/Character/Customisation/`).

---

### 1.18 Equipment Visual (T-EV) ✅ Phase 2 ЗАВЕРШЁН (2026-06-27)

**Цель:** Визуальное отображение экипированного оружия и брони на персонаже (bone mapping, visual sockets).

| Компонент | Файл | Назначение | Статус |
|-----------|------|------------|--------|
| `CharacterEquipmentVisualApplier` | `Scripts/Customisation/CharacterEquipmentVisualApplier.cs` | Bone mapping: Weapon/Shield/Helmet/Chest (+7 bone slots) | T-EV-01 ✅ |
| `EquipmentVisualSocket` | `Scripts/Items/EquipmentVisualSocket.cs` | Определение socket на скелете (HumanBodyBones) | T-EV-02 ✅ |
| `visualPrefab` on ItemData | `Scripts/Items/ItemData.cs` | GameObject reference + attach params | T-EV-03 ✅ |
| `EquipmentChangedHandler` | `Scripts/Customisation/EquipmentChangedHandler.cs` | OnEquipmentChanged → Instantiate/Destroy visual | T-EV-04 ✅ |
| Equip bug fix | `CharacterWindow.ChangeEquipmentSlot` | Rate-limit N callback предотвращает duplicate equip | T-EV-05 ✅ |

**Key decisions:**
- Visual prefab живёт как child анимированного bone — следует за анимацией skeleton
- `CharacterCustomisationApplier` — единая точка входа для customisation + equipment visual
- Socket mapping через `HumanBodyBones` enum (стандарт Unity Avatar)

**Stats:** +5 C# файлов, ~15 KB кода.

---

### 1.19 Input System (Phase 1-2.5) ✅ ЗАВЕРШЁН (2026-06-25..26)

**Цель:** Централизованное управление привязкой клавиш: EscMenu, rebinding UI, save/load/reset.

| Компонент | Файл | Назначение | Статус |
|-----------|------|------------|--------|
| `InputBindingsConfig` (SO) | `Data/Input/InputBindingsConfig.asset` | 31 биндинг: move/action/combat/UI | Phase 1 ✅ |
| `EscMenuWindow` (UIDocument) | `Scripts/UI/EscMenuWindow.cs` | Overlay-пауза, кнопки: Settings/Controls/Quit | Phase 1 ✅ |
| `InputRebindingPanel` (UIDocument) | `Scripts/UI/InputRebindingPanel.cs` | Listen → Assign → Save/Reset workflow | Phase 2 ✅ |
| `PlayerPrefsInputRepository` | `Scripts/Player/PlayerPrefsInputRepository.cs` | Сериализация override → PlayerPrefs | Phase 2.5 ✅ |
| `DefaultInputRestorer` | `Scripts/Player/DefaultInputRestorer.cs` | Сброс на заводские defaults | Phase 2.5 ✅ |
| EscMenu → Rebinding → Save flow | Integrated | Close on Apply, Cancel возвращает предыдущие | Phase 2 ✅ |

**Stats:** +6 C# файлов, ~2 UXML/USS, 1 SO.

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
- ✅ Визуальная задержка загрузки чанков в новых сценах
- ⏳ Коррекция позиции отключена — требует полноценной реализации для мультиплеера
- ✅ WorldSceneManager / ServerSceneManager / WorldStreamingManager / WorldChunkManager / FloatingOriginMP — написаны, но **не развёрнуты в сцене**. Фокус проекта сейчас — `WorldScene_0_0`, остальные 23 сцены — на потом.
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

6. **Персонаж:** ✅ (2026-06-25..28)
   - ✅ **Character Animations v0.5.1** — BlendTree directional movement (MoveX/MoveY), combat clips (idle/walk/run/attack/death), female override controller
   - ✅ NetworkPlayer интеграция — capsule заменён анимированным мешем, синхронизация анимаций
   - ✅ AnimatorOverrideController импорт через ScriptableObject (L3/L4 кастомизация)

7. **UI и предметы:** ⏳
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

### 3.1 Характеристики и навыки (RPG) ✅ + Stats Architecture Refactoring (июнь–июль 2026)
1. **Система прокачки:** ✅
   - ✅ **SkillNodeConfig (ScriptableObject)** — 27+ навыков: weapon, armor, technique, passive типы
   - ✅ **SkillTreeWindow** (UI Toolkit) — интерактивный граф: zoom/pan, learned/available/locked узлы, badge-счётчики, tooltip
   - ✅ **SkillAnimationPlayer** — runtime override controller для анимаций навыков
   - ✅ **DamageCalculator** — hit/miss/crit/armor/skills интеграция с SkillModifier
   - ✅ **Weapon/Armor/Technique catalogs** — 3 SO каталога с боевыми параметрами
   - ✅ Балансировка чисел через ScriptableObject (SkillConfig, SkillTreeConfig)

2. **Stats Architecture Refactoring (T-STAT01..05, июль 2026):** ✅
   - ✅ **Аудит (T-STAT01):** 10 структурных проблем (P0-P10) — дублирование статов в 21 файле, equip bonuses не работали, две системы Player/NPC
   - ✅ **T-STATS02:** P0 fix — Combat использует effective stats (tier + equip + skill bonuses)
   - ✅ **T-STATS03:** StatsConfig разделён на 3 SO: `ExperienceConfig`, `StatSourceMapConfig`, `StatDebugConfig`
   - ✅ **P1:** `PlayerStats` flat struct → `StatBucket` + static ref accessors (PlayerStatsRef удалён)
   - ✅ **P8:** Equipment multipliers applied: `effective = (StatsToFlat(tier) + flatBonus) * (1.0 + sumMultipliers)`
   - ✅ **T-STATS04 (P3/P9):** Единая формула Player/NPC: `StatsToFlat(tier) = tier * 5 + 10`
   - ✅ **T-STATS05:** StatsServer config wiring + full playtest guide (P0-P10)
   - 📋 См. `docs/Character/11_STATS_ARCHITECTURE_AUDIT.md`, `12_STATS_ARCHITECTURE_AUDIT_V2.md`, `13_SESSION_CONTINUATION.md`, `14_PLAYTESTS_STATS_AUDIT.md`, `docs/Character/CHANGELOG.md`

### 3.2 Базовая торговля (Недели 9-11) ✅ ЗАВЕРШЕНО (Сессии 1-5)
1. **TradeItemDefinition (ScriptableObject):** ✅
   - ✅ Определение всех товаров: id, цена, вес, объём, иконка
   - ✅ Флаги: опасный, хрупкий, контрабанда
2. **CargoSystem — груз корабля:** ✅ (Trade v2, 2026-06-17 + рефакторинг 2026-07-03)
   - ✅ `CargoData` POCO + `TradeWorld._cargoCache[shipId]` — единый источник правды
   - ✅ `ShipCargoRegistry.GetEffectiveLimits()` — per-instance лимиты + модульные бонусы (T-CARGO-06)
   - ✅ Скоростной штраф: `GetSpeedPenalty` → `_serverCargoPenalty` NetworkVariable<float>
   - ✅ Столкновения: `ShipController.OnCollisionEnter` → `TradeWorld.TryDamageCargo` (dangerous leak 5%×10%, fragile marked)
   - ✅ `ShipCollisionDamageConfig` SO (`Resources/ShipCollisionDamage.asset`)
   - ✅ Штраф от столкновений **наложен на HP корпуса** (ShipHull.ApplyCollisionDamage, 2026-07-05)
   - ✅ **NPC Cargo (2026-07-03):** `NpcCargoService` + `TryNpcBuy`/`TryNpcSell` + `NpcCargoTradeListConfig`
   - ✅ **Cargo UI (2026-07-02..03):** детальный список в CharacterWindow + Cargo Manager консоль + 3D визуал
   - ✅ **Cargo ownership guard (2026-07-06):** `IsOwnerOfShip` в ShipCargoServer + MarketServer (P5)
   - ❌ Legacy `CargoSystem.cs` MonoBehaviour — **удалён (P2, 2026-07-05)**
   - ❌ 3 broken-refs в `WorldScene_0_0.unity` — **убраны**
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

### 3.4.5 Июль 2026: Crew, Market Refactor, Repair Manager, Engine, Damage, Refactor ✅ ЗАВЕРШЕНО (1–6 июля)

**Цель:** Закрыть критические пробелы: NPC на палубе, товарооборот NPC-кораблей, доковый менеджер модулей, двигатель ON/OFF, повреждения корабля, большой рефакторинг Key/Cargo/документации.

#### 3.4.5.1 NPC Crew на движущемся корабле (T-CREW) — 1–2 июля

| Компонент | Назначение | Статус |
|-----------|-----------|--------|
| `PlatformRideHelper` | Общий хелпер probe + carry-формула | ✅ |
| `NetworkPlayer.ApplyPlatformCarry` | Единый Move: `motion*dt + _platformDelta` (не 2 Move за кадр) | ✅ |
| `groundedForMovement` | `_isGrounded \|\| _onPlatform` — без подскоков | ✅ |
| `NpcBrain.DriveDeckNav` | Прокси-агент: относительное смещение через `DeckLocalToWorld` | ✅ |
| `PickupDeckRide` | L3 carry для pickup'ов: carry-формула + `RefreshWorldBase()` (4 попытки) | ✅ |

**Документация:** `docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md`.

#### 3.4.5.2 MARKET-ID-REFACTOR — 3 июля

**Проблема → Решение:**
- `locationId` в разных регистрах (primium vs PRIMIUM) → `ToUpperInvariant()` везде
- Ручной список MarketConfig в BootstrapScene → `MarketConfigCollector.CollectFromLoadedScenes()` — авто-сбор
- 5 ручных шагов для добавления рынка → «Разместил MarketZone → рынок работает»

**Документация:** `docs/Markets/MARKET_ID_REFACTOR_DESIGN.md`.

#### 3.4.5.3 Repair Manager + Ship Customisation — 4–5 июля

| Компонент | Назначение | Статус |
|-----------|-----------|--------|
| `ModuleShopEntry` / `ModuleShopDatabase` | SO: модуль + цена + ресурсы | ✅ |
| `ShipModuleServer` | NetworkBehaviour: RPC install/remove/sell/repaint/repair-hull | ✅ |
| `RepairManagerWindow` | UI Toolkit окно (CustomDropdown, qty-кнопки, скролбары) | ✅ |
| `ShipObservationCamera` | FlyToShip + орбита (▲▼◀▶), отдельная Camera без AudioListener | ✅ |
| Ship Repainting | Цвет из палитры → credit payment, `ShipTelemetryState.shipColorR/G/B` | ✅ |
| Module Visual Preview | Editor tool: ▶ Preview с `HideFlags.DontSave` | ✅ |

**Документация:** `docs/Ships/Modul_system/01_ARCHITECTURE.md`, `02_REPAIR_MANAGER.md`, `03_REPAINT_PLAN.md`.

#### 3.4.5.4 Engine ON/OFF + IDLE — 5 июля

- `_netEngineRunning` NetworkVariable<bool> — сервер-авторитативный
- Enter — включить/выключить (только когда пилот в кресле)
- IDLE: без пилота — antiGravity работает, idle-расход 0.05 fuel/s
- Выход (F) разрешён всегда, на любой скорости
- При `fuel == 0` — авто-выключение
- NPC всегда ENGINE ON

**Документация:** `docs/Ships/ENGINE_POWER_STATE.md`.

#### 3.4.5.5 Ship Damage Subsystem — 5 июля

- `ShipHull` (NetworkBehaviour, `IDamageTarget`) — `NetworkVariable<int>` hull/maxHull
- `ShipDamageConfig` SO: maxHull по классам (100/200/400/600), armorHull=5
- Два источника урона: столкновения + боевое оружие (через CombatServer)
- 0 HP = «сломан»: скорости ×0.1, груз обнулён, `IsAlive()=true`
- Три защиты от ложных ударов при стыковке/отстыковке
- Ремонт в доке: `ключ + IsDocked + TryModifyCredits(-300)` → `RepairFull()`

**Документация:** `docs/Ships/damage_subsystem/00_DESIGN.md`, `01_ARCHITECTURE.md`, `02_INTEGRATION_AND_REPAIR.md`.

#### 3.4.5.6 SHIP_REFACTOR_PLAN P1–P5 — 5–6 июля

| Фаза | Описание | Статус |
|------|----------|--------|
| **P1** | Рефакторинг Key Subsystem: -1139/+651 строк, 7 файлов удалено, 0 reflection | ✅ |
| **P2** | Удаление legacy CargoSystem.cs + speed penalty fix | ✅ |
| **P3** | Актуализация документации (CargoSystem, Key-subsystem, roadmap) | ✅ |
| **P4** | L1 Customisation — Module Visual (visualPrefab + Editor preview) | ✅ |
| **P5** | Cargo ownership guard (4 метода в ShipCargoServer + MarketServer) | ✅ |

**Документация:** `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md`, `docs/Ships/ITERATIONS.md`.

---

**Известные проблемы (P0-P1):**
- [`docs/TRADE_SYSTEM_RAG.md`](TRADE_SYSTEM_RAG.md) — ⭐⭐ RAG документация (архитектура, потоки, формулы)
- [`docs/TRADE_DEBUG_GUIDE.md`](TRADE_DEBUG_GUIDE.md) — отладка (симптомы → решения)
- [`docs/gdd/GDD_22_Economy_Trading.md`](gdd/GDD_22_Economy_Trading.md) — GDD экономики (v3.0)
- [`docs/gdd/GDD_INDEX.md`](gdd/GDD_INDEX.md) — каталог всех GDD (включая GDD_25 Trade Routes, в работе)
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
- ✅ **R3-INV-DROP-001 (Drop visual):** исправлен — `PickupDeckRide` + `RefreshWorldBase()` (2026-07-02). Pickup'ы больше не прыгают при отлипании от палубы.
- 🟢 **MetaRequirement TODO (R2-META-REQ-001, Этап 2):** частично закрыто P1 рефакторингом (удалены 7 obsolete файлов, 0 reflection). Оставшиеся пункты: `_consumeOnUse` логика, `ProgressInfo` UI, disconnect-reconnect race fix (см. `docs/MetaRequirement/50_KNOWN_ISSUES.md`).
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
   - ✅ **M19 CSV pipeline — финал** (T-Q19.1–T-Q19.3, 2026-06-13):
     - ✅ T-Q19.1 Авто-заполнение `questTurnIns` у NPC (последний stage + TalkToNpc → NPC)
     - ✅ T-Q19.2 Авто-link `defaultDialogTree` (DialogTree `{npcId}_default` → NPC)
     - ✅ T-Q19.3 `npcs.csv` — 9 колонок (services, attitudeLinks, attitudeMin/Max, greeting, voice, radius, showGreeting)
   - ✅ **DialogCsvImporter** — 15 колонок treeId/fromNodeId/fromText/fromSpeaker/edgeLabel/toNodeId/conditions/actions. Создаёт DialogTree + auto-link к NPC.
   - ✅ **NpcCsvImporter** — 9 колонок, batch-update существующих NPC.
   - ✅ **Тестовые данные:** 106 NPC, 802 квеста, 2 DialogTree, 6 CSV файлов.
   - ✅ Writer-документация: `docs/NPC_quests/M19_CSV_PIPELINE_v2.md` (26 KB, 7 разделов).
   - ✅ **Контент квестов (расширение):** 🟢 106 NPC, 802 квеста, 6 CSV файлов — bulk импорт завершён. **Авторский контент** — открыто (создание сюжетных квестов через CSV/Editor).
   - ⏳ **Ежедневные испытания** — не начато (post-MVP).

2. **Визуальные улучшения:**
   - Финальные шейдеры для облаков и воды
   - Анимации персонажей и кораблей (Mixamo/Custom)
   - Пост-обработка: bloom, color grading, хроматическая аберрация
   - **Визуал торговли:** 3D модели грузов, NPC-торговцы, торговые посты
   - ✅ **Ветер для персонажа (реализация):**
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

## Инструменты и зависимости:
- **Клиент:** Unity 6000.5.2f1, URP, Netcode for GameObjects, DOTween (анимации), Cinemachine (камера)
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
- **Система ветров (Сессия 3 + 2026-07-01):** WindZone, WindZoneData — объёмные триггеры с профилями (Constant, Gust, Shear)
  - ✅ Реализовано для кораблей (ShipController v2.2)
  - ✅ Реализовано для персонажа (2026-07-01) — `NetworkPlayer.ProcessMovement`, правила по состоянию
- **Art Bible:** [`docs/ART_BIBLE.md`](docs/ART_BIBLE.md)

### Критические риски и меры:
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
| [GDD_10: Ship System](gdd/GDD_10_Ship_System.md) | 4 класса кораблей, физика, кооп-пилотирование. **✅ Ship Key MVP (R2-SHIP-KEY-001) + MetaRequirement v1 (R2-META-REQ-001) реализованы (2026-06-06), см. `docs/Ships/Key-subsystem/00_OVERVIEW.md` + `docs/MetaRequirement/00_OVERVIEW.md`. ✅ Docking Stations MVP (2026-06-20), см. `docs/Docking_stations/00_README.md`.** |
| [GDD_11: Inventory & Items](gdd/GDD_11_Inventory_Items.md) | 8 типов, круговое колесо, LootTable, сундуки. **✅ sub_inventory-tab (P-таб) + MetaRequirement extensions (HasAllItems/HasAnyItem/CountOf/GetMissingItems) реализованы, см. `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` + `docs/MetaRequirement/30_RUNTIME_FLOW.md`.** |
| [GDD_12: Network & Multiplayer](gdd/GDD_12_Network_Multiplayer.md) | NGO, RPC, реконнект, Dedicated Server |
| [GDD_12.1: Scene-Based World Streaming](gdd/GDD_12_1_Scene_World_Streaming.md) | 24 сцены, 4×6 grid, boundary-based loading |
| [GDD_13: UI/UX System](gdd/GDD_13_UI_UX_System.md) | HUD, Ghibli стиль, адаптивность. **✅ CharacterWindow v2 (5+ табов) реализован (2026-06-05), см. `docs/Character-menu/00_OVERVIEW.md`.** |
| [GDD_14: Visual & Art Pipeline](gdd/GDD_14_Visual_Art_Pipeline.md) | URP, CloudGhibli, шейдеры, постобработка |
| [GDD_15: Audio System](gdd/GDD_15_Audio_System.md) | AudioMixer, SFX, музыка, 3D звук |

### Content — Контентные системы
| Документ | Описание |
|----------|----------|
| [GDD_20: Progression & RPG](gdd/GDD_20_Progression_RPG.md) | XP, уровни 1-50, деревья навыков. ✅ T-P01..T-P18 (2026-06-17) |
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

## 🎨 Визуальные задачи (Visual Tasks)

Полный перечень визуального контента, необходимого для минимальной версии игры. Каждая «штука» требует набора компонентов: 3D-модель, материал(ы), текстура(ы), VFX, анимация.

**Прогресс:** ~11% (36/332)

---

### 🚢 Корабли игрока (4 класса × 5 компонентов = 20)

#### Light Ship
- [ ] 3D-модель (FBX, 5-8k tri)
- [ ] Материалы (корпус, стекло, emissive)
- [ ] Текстуры (Albedo, Normal, Emissive, AO)
- [ ] VFX (антиграв-свечение двигателей)
- [ ] Анимация (ветровые лопасти)

#### Medium Ship
- [ ] 3D-модель (FBX, 8-12k tri)
- [ ] Материалы (корпус, окна, emissive)
- [ ] Текстуры (Albedo, Normal, Emissive, AO)
- [ ] VFX (антиграв-свечение)
- [ ] Анимация (ветровые лопасти)

#### Heavy Ship
- [ ] 3D-модель (FBX, 12-18k tri)
- [ ] Материалы (корпус, окна, emissive, детали)
- [ ] Текстуры (Albedo, Normal, Emissive, AO)
- [ ] VFX (антиграв-свечение, выхлоп)
- [ ] Анимация (ветровые лопасти)

#### Heavy-II Ship
- [x] 3D-модель (FBX, 15-20k tri) — NPC_Ship_HeavyII_03.prefab
- [ ] Материалы (корпус, окна, emissive)
- [ ] Текстуры (Albedo, Normal, Emissive, AO)
- [ ] VFX (антиграв-свечение, выхлоп)
- [ ] Анимация (ветровые лопасти)

---

### 🚢 Корабли NPC (4 фракции × 3 компонента = 12)

#### Trader Ship
- [ ] 3D-модель (FBX)
- [ ] Материалы
- [ ] Текстуры

#### Pirate Ship
- [ ] 3D-модель (FBX)
- [ ] Материалы
- [ ] Текстуры

#### Guard Ship
- [ ] 3D-модель (FBX)
- [ ] Материалы
- [ ] Текстуры

#### Cultist Ship
- [ ] 3D-модель (FBX)
- [ ] Материалы
- [ ] Текстуры

---

### 🏙 Города (5 городов, ~31 задача)

#### Primus
- [ ] 3D-модели зданий (5 типов: жилое, торговое, административное, док, храм)
- [ ] Материалы (кирпич, металл, стекло, дерево)
- [ ] Текстуры (Albedo, Normal)
- [ ] Платформы и мосты (модели + материалы)
- [ ] Визуальные детали (флаги, огни, трубы)

#### Tertius
- [ ] 3D-модели зданий (5 типов)
- [ ] Материалы
- [ ] Текстуры
- [ ] Платформы и мосты
- [ ] Визуальные детали

#### Quartus
- [ ] 3D-модели зданий (4 типа)
- [ ] Материалы
- [ ] Текстуры
- [ ] Платформа
- [ ] Визуальные детали

#### Kilimanjaro
- [ ] 3D-модели зданий (4 типа)
- [ ] Материалы
- [ ] Текстуры
- [ ] Платформа
- [ ] Визуальные детали

#### Secundus
- [ ] 3D-модели зданий (3 типа)
- [ ] Материалы
- [ ] Текстуры
- [ ] Док
- [ ] Визуальные детали

---

### 🌍 Окружение (6 задач)

- [x] Облака (3 слоя: Upper/Middle/Lower) — шейдер CloudGhibli, генерация, движение
- [ ] Skybox (материал + шейдер, закатный градиент)
- [ ] Террасы и фермы (модели + материалы)
- [x] Фоновые пики (ProceduralNoiseGenerator, IslandMaterial)
- [ ] Дальние платформы (модели + материалы)
- [ ] Завеса (VeilRaymarch.shader + VFX)

---

### 👤 Персонаж игрока (84 задачи)

#### Базовая модель
- [x] 3D-модель (Mixamo FBX) — есть character_URP.mat
- [ ] 4 базовые анимации (Idle, Walk, Run, Jump)
- [x] Материал (character_URP.mat)
- [ ] Текстуры персонажа (Albedo, Normal, Emissive)

#### Боевые анимации (21 тип)
- [ ] Melee: LightAttack01, LightAttack02, HeavyAttack, ComboFinisher
- [ ] Ranged: Shoot_Bow, Shoot_Crossbow, Shoot_Rifle, Reload
- [ ] Throw: Throw_Grenade, Throw_Knife
- [ ] Magic/Skills: Cast_01..Cast_04
- [ ] Defensive: Block, Dodge, Parry, Roll
- [ ] Stun, Death, GetUp

#### VFX навыков (21 скилл × cast + impact = 42)
- [x] VFX Impact_Melee — PF_VFX_Impact_Melee.prefab
- [x] VFX Impact_Explosion — PF_VFX_Impact_Explosion.prefab
- [x] VFX MuzzleFlash_Basic — PF_VFX_MuzzleFlash_Basic.prefab
- [x] VFX Projectile_Arrow — PF_VFX_Projectile_Arrow.prefab
- [ ] VFX Cast_Heal, Cast_Buff, Cast_Shield
- [ ] VFX Slash, Pierce, Crush
- [ ] VFX Fire, Ice, Lightning
- [ ] VFX Poison, Bleed, Vampire
- [ ] VFX Silence, Stun, Fear
- [ ] VFX Teleport, Invisibility, Slow
- [ ] VFX AOE: Explosion, Shockwave, HolyGround

#### Кастомизация (8 задач)
- [ ] 6 пресетов тела (Default, Athletic, Heavy, Slim, Elder, Young) — 3D-модели
- [ ] 2 причёски (Bald, Short) — 3D-модели
- [ ] Текстуры кожи (3 тона)
- [ ] Текстуры волос (3 цвета)
- [ ] UI preview для пресетов
- [ ] UI preview для причёсок
- [ ] Color picker UI
- [ ] Синхронизация кастомизации по сети (через NetworkPlayer)

#### Equipment Visual (8 слотов)
- [ ] Weapon — 3D-модели (меч, топор, булава, кинжал, лук, арбалет, ружьё)
- [ ] Shield — модель щита
- [ ] Helmet — модель шлема
- [ ] Chest — модель нагрудника
- [ ] Shoulders — модель наплечников
- [ ] Gloves — модель перчаток
- [ ] Boots — модель сапог
- [ ] Belt — модель пояса

---

### ⚔ Оружие и броня (24 задачи)

#### Melee (4 × 2 = 8)
- [ ] Sword: модель + материал
- [ ] Axe: модель + материал
- [ ] Mace: модель + материал
- [ ] Dagger: модель + материал

#### Ranged (4 × 2 = 8)
- [ ] Bow: модель + материал
- [ ] Crossbow: модель + материал
- [ ] Pneumatic Rifle: модель + материал
- [ ] Mesium Rifle: модель + материал

#### Throwables (2 × 3 = 6)
- [ ] Grenade Basic: модель + материал + VFX взрыва
- [ ] Grenade Antigrav: модель + материал + VFX взрыва

#### Armor Sets (5 сетов)
- [ ] Light armor set
- [ ] Medium armor set
- [ ] Heavy armor set
- [ ] Merchant armor set
- [ ] Cultist armor set

---

### 👾 Враги (4 фракции × 6 = 24)

#### Goblin
- [x] 3D-модель (Npc_Goblin.prefab)
- [x] Материал (Npc_Goblin)
- [x] Анимации placeholder (Idle, Walk, Run, Attack, Death)
- [ ] VFX спавна
- [ ] VFX смерти
- [ ] Оружие гоблина (модель)

#### Bandit
- [ ] 3D-модель
- [ ] Материал
- [ ] Анимации (Idle, Walk, Run, Attack, Death)
- [ ] VFX спавна
- [ ] VFX смерти
- [ ] Оружие бандита

#### Pirate
- [ ] 3D-модель
- [ ] Материал
- [ ] Анимации (Idle, Walk, Run, Attack, Death)
- [ ] VFX спавна
- [ ] VFX смерти
- [ ] Оружие пирата

#### Cultist
- [ ] 3D-модель
- [ ] Материал
- [ ] Анимации (Idle, Walk, Run, Attack, Death)
- [ ] VFX спавна
- [ ] VFX смерти
- [ ] Оружие культиста

---

### ✨ VFX и частицы (12 задач)

- [ ] Ship Light — антиграв-свечение (Particle System)
- [ ] Ship Medium — антиграв-свечение
- [ ] Ship Heavy — антиграв-свечение
- [ ] Ship Heavy-II — антиграв-свечение
- [x] Молнии Завесы (VeilRaymarch.shader + VeilRaymarchMesh.shader)
- [ ] Пыль при беге по платформе
- [ ] Дождь (Particle System)
- [ ] Посадка/взлёт корабля (облако пыли/пара)
- [ ] God rays — лучи сквозь облака (Fake или URP Volume)
- [ ] Ветер (трава, флаги) — Shader Graph
- [ ] Огоньки в городах (ночью)
- [ ] Пар/дым из труб зданий

---

### 🔮 Кастомные шейдеры (6 задач)

- [x] CloudGhibli.shader — облака (noise + rim glow + vertex displacement)
- [ ] URP Character shader — персонаж (Mixamo + skin)
- [ ] URP Ship shader — корабль (металл, свечение)
- [ ] Water/Ocean shader — вода/Завеса
- [ ] Building shader — здания (градиент, детали)
- [ ] Holographic shader — голографические элементы
- [ ] Terrain shader — террасы, фермы

---

### 🎛 Пост-процессинг (URP Volume, 7 задач)

- [ ] Bloom (Threshold: 0.8, Intensity: 0.5)
- [ ] Tonemapping (Mode: ACE)
- [ ] Vignette (Intensity: 0.2)
- [ ] Film Grain (Intensity: 0.05)
- [ ] Chromatic Aberration (Intensity: 0.03)
- [ ] Color Grading (Temperature, Tint)
- [ ] Fog (Exponential, цвет Завесы)

---

### 🖼 UI иконки (~80 задач)

- [ ] Иконки ресурсов (34 товара: древесина, металл, мезий, еда и т.д.)
- [ ] Иконки навыков (27 скиллов)
- [ ] Иконки оружия (9 видов)
- [ ] Иконки брони (5 сетов)
- [ ] Иконки расходников (зелья, гранаты, патроны)
- [ ] Иконки квестов (5 типов)
- [ ] Иконки фракций (5 гильдий)
- [ ] UI спрайты: кнопки, панели, фоны
- [ ] UI спрайты: рамки предметов (по редкости)
- [ ] UI спрайты: портреты NPC (4 фракции)
- [ ] UI спрайты: миникарта, компас
- [ ] UI спрайты: загрузочные экраны (5 городов)

---

## Мини-игра CloudTrader ✅

В рамках прототипирования разработана standalone HTML-игра **CloudTrader** (v1.1.11) — `docs/Fun/index.html`. Полный цикл: торговля, корабли, контракты, контрабанда, пиратский комбат. Реализовано.

---

*Примечание: План ориентировочный. Длительность этапов может меняться в зависимости от размера команды, фидбека и технических сложностей. Рекомендуется использовать спринты по 2 недели с демонстрацией результатов.*
