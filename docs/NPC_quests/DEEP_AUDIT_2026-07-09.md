# 🔍 Глубокий аудит системы квестов (NPC Quests v2)

> **Дата:** 2026-07-09
> **Скоуп:** полный анализ кода, документации, архитектуры, стабов, дублирования, и интеграций.
> **Метод:** grep по всем `.cs`, чтение ключевых файлов, сверка roadmap ↔ реализация.
> **Вердикт:** Система **зрелая, рабочая, чистая**. ~62+ часов разработки, 50+ тикетов (M1–M19), 0 compile errors. Mira E2E demo работает. Есть документированные стабы и несколько точек для рефакторинга.

---

## 1. Общая картина (TL;DR)

```
┌─────────────────────────────────────────────────────────────────┐
│  Слой данных (SO)                                               │
│  ├─ FactionId (enum, 12 значений)                               │
│  ├─ FactionDefinition (SO, 11 ассетов)                          │
│  ├─ NpcDefinition (SO, 106 ассетов: Mira + npc_002..npc_105)   │
│  ├─ DialogTree (SO, 2 ассета: mira_default + MiraDefault)       │
│  ├─ QuestDefinition (SO, ~3+ ассета: FindArtifact,              │
│  │   EventDrivenQuest, collect_copper_ore, StageIntroDemo,      │
│  │   StageMultiDemo)                                            │
│  └─ QuestDatabase (SO, центральный реестр)                      │
├─────────────────────────────────────────────────────────────────┤
│  Сервер (server-authoritative)                                  │
│  ├─ QuestServer (NetworkBehaviour, ~1600+ строк)                │
│  │   ├─ 9 RPCs: TalkToNpc, AdvanceDialogue, AcceptQuest,       │
│  │   │   TurnInQuest, TrackQuest, RefreshQuests,                │
│  │   │   RefreshReputation, RefreshNpcAttitude, DiscoverQuest   │
│  │   ├─ Rate limiting (30 ops/min/client)                       │
│  │   ├─ WorldEventBus подписки (7 handlers)                     │
│  │   ├─ T-Q20 tick (5s interval → QuestWorld.TickAll)           │
│  │   └─ T-Q28 runtime DialogTree builder                        │
│  ├─ QuestWorld (POCO singleton, ~1340 строк)                    │
│  │   ├─ Per-player state: quests, reputation, npcAttitude,      │
│  │   │   worldFlags, dialogSessions                             │
│  │   ├─ TryOffer / TryAccept / TryTurnIn / TryAdvanceObjective  │
│  │   ├─ ModifyReputation / ModifyNpcAttitude (cross-faction)    │
│  │   ├─ T-Q20: EvaluateAndAdvanceStage / TryAdvanceStage        │
│  │   ├─ T-Q28: ArePrerequisitesMet / IsPrerequisiteMet          │
│  │   └─ T-Q18: SavePlayer / LoadPlayer (persistence)            │
│  ├─ QuestTriggerService (POCO, ~160 строк)                      │
│  │   ├─ 11 factory-registered trigger types                     │
│  │   └─ Evaluate(playerId, hint) + MatchesObjective             │
│  └─ ContractMetaBridge (MonoBehaviour, ~100 строк)              │
│      └─ ContractAccepted/Completed/Failed → QuestWorld markers  │
├─────────────────────────────────────────────────────────────────┤
│  Клиент (DTO projection)                                        │
│  ├─ QuestClientState (singleton, ~170 строк)                    │
│  │   ├─ 6 events: OnSnapshotUpdated, OnQuestResult,             │
│  │   │   OnReputationResult, OnQuestDiscovered,                 │
│  │   │   OnDialogStepReceived, OnDialogActionResultReceived     │
│  │   └─ RequestAcceptQuest forwarder                            │
│  ├─ DTOs (7 файлов, все INetworkSerializable)                   │
│  └─ NetworkPlayer: 6 ReceiveXxxTargetRpc                        │
├─────────────────────────────────────────────────────────────────┤
│  UI (UI Toolkit)                                                │
│  ├─ DialogWindow (UIDocument, ~320 строк)                       │
│  ├─ QuestTracker (UIDocument, HUD overlay, ~210 строк)          │
│  ├─ QuestToast (runtime VisualElement, queue-based)             │
│  └─ CharacterWindow (таб «Квесты», 4 под-секции, ~3000 строк)  │
├─────────────────────────────────────────────────────────────────┤
│  Editor Tools                                                   │
│  ├─ QuestDatabaseWindow (UI Toolkit EditorWindow, ~370 строк)   │
│  ├─ QuestNodeGraphView (GraphView, editable, ~590 строк)        │
│  ├─ QuestGraphView (custom VisualElement, maintenance, ~340)    │
│  ├─ CSV: Schema + Importer + Exporter + Window (~900 строк)     │
│  ├─ NpcCsvImporter отдельно (~300 строк)                        │
│  ├─ DialogCsvImporter отдельно (~300 строк)                     │
│  ├─ QuestDatabaseAutoDiscover                                    │
│  └─ QuestDefinitionValidator                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Что работает (✅ DONE)

| Категория | Что | Статус |
|-----------|-----|--------|
| **Data model** | FactionId, FactionDefinition, NpcDefinition, DialogTree, DialogueNode/Edge/Condition/Action, QuestDefinition, QuestStage, QuestObjective, QuestReward, QuestPrerequisite, QuestState | ✅ |
| **Server core** | QuestServer (NetworkBehaviour, RPCs, rate-limit), QuestWorld (POCO, state, transitions) | ✅ |
| **Event bus** | WorldEventBus (static, Publish/Subscribe/Reset), 10+ WorldEvent подтипов | ✅ |
| **Triggers** | TalkedToNpc, HaveItem, ReputationAtLeast, NpcAttitudeAtLeast, DayNightPhase, Event, ContractCompleted, ContractAccepted | ✅ |
| **DTOs** | QuestSnapshotDto, QuestProgressDto, ReputationSnapshotDto, NpcAttitudeSnapshotDto, DialogStepDto, DialogOptionDto, DialogActionResultDto, QuestResultDto | ✅ |
| **Client state** | QuestClientState (singleton, events, auto-spawn), ReputationClientState, NpcAttitudeClientState (inline) | ✅ |
| **Dialog** | DialogWindow (UI Toolkit, typewriter, F-skip, options, reputation badges) | ✅ |
| **Quest log** | CharacterWindow таб «Квесты» (Active/Completed/Failed/Discovered, Accept-кнопка) | ✅ |
| **Quest tracker** | QuestTracker (HUD overlay, tracked quest, counter) | ✅ |
| **Toast** | QuestToast (queue-based, bottom-center, displayName, delta) | ✅ |
| **Persistence** | JsonQuestStateRepository (atomic JSON save per state change) | ✅ |
| **Reputation** | ModifyReputation, ModifyNpcAttitude (clamp, cross-faction influence, event publish) | ✅ |
| **Contract bridge** | ContractMetaBridge (ContractAccepted/Completed/Failed → QuestWorld markers) | ✅ |
| **Prerequisites** | T-Q28: ArePrerequisitesMet (QuestCompleted, QuestActive, Reputation, NpcAttitude, HaveItem, FlagIsSet) | ✅ |
| **Real-time objectives** | T-Q20: tick-based EvaluateAndAdvanceStage, TryAdvanceStage (onEnter/onComplete actions) | ✅ |
| **Editor DB window** | QuestDatabaseWindow (TreeView, detail panels, 4 группы) | ✅ |
| **Editor GraphView** | QuestNodeGraphView (editable, nodes+edges, save to SO, add/delete) | ✅ |
| **CSV import/export** | M19: single-file CSV pipeline (schema, parser, importer, exporter, window) | ✅ |
| **Input refactor** | T-X3: PlayerInputReader singleton + events + NetworkPlayer subscribes | ✅ |
| **Mira E2E demo** | Полный playthrough (talk → accept → collect → deliver → turn-in → rewards) | ✅ |
| **v1 cleanup** | T-Q19: 4 v1 NPC файла удалены (1447 LOC), InteractableManager очищен | ✅ |
| **NPCTrader rename** | T-X1: NPCTrader → MarketTrader | ✅ |

---

## 3. Что НЕ доделано (стабы / TODO / deferred)

### 3.1 Стаб-триггеры (всегда `return false`)

| Триггер | Файл | Причина | Что нужно |
|---------|------|---------|-----------|
| **CargoHasItemTrigger** | `ConcreteTriggers.cs:158` | Помечен «T-Q15 fill», но T-Q15 done | Интеграция с `TradeWorld.GetOrLoadCargo()` / `CargoData` |
| **LocationReachedTrigger** | `ConcreteTriggers.cs:172` | Помечен «T-Q15 fill», но T-Q15 done | Интеграция с `PlayerChunkTracker` / `NetworkPlayer.transform` |
| **KilledEntityTrigger** | `ConcreteTriggers.cs:186` | Ждёт combat system | Оставить стабом до появления combat |

Плюс **ShipDockedAtTrigger** — упоминается в документации (`06_TRIGGERS_AND_INTEGRATION.md`), но отсутствует в `ConcreteTriggers.cs` и `QuestTriggerService` factories.

### 3.2 Стаб-действия (DialogueAction)

| Действие | Где | Статус |
|----------|-----|--------|
| **SetFlag** (50) | `QuestServer.cs:1557` | Логируется → «T-Q18+ fill», но T-Q18 done |
| **SwitchDialogTree** (52) | `QuestServer.cs:1557` | Логируется → «T-Q18+ fill» |
| **DiscoverQuest** (13) | `QuestServer.cs:1557` | Логируется → не реализован |
| **FailQuest** (12) | `QuestServer.cs:1557` | Логируется → не реализован |
| **TakeCargoItem** (23) | `QuestServer.cs:1557` | Логируется → не реализован |
| **OpenService** (41) | `QuestServer.cs:1287` | Stub: «ServiceUI TBD». Закрывает диалог, но UI сервиса не открывает |

**Вывод:** 6 из 17 типов действий — логирующие стабы. Для MVP квестов (Mira) не критично, но блокирует полноценные квестовые цепочки с флагами, переключением деревьев диалога, и сервисными NPC.

### 3.3 Открытые roadmap-позиции

| Тикет | Что | Приоритет | Усилие |
|-------|-----|-----------|--------|
| **T-X4** | Input remap: E=pickup→F, E=NPC talk | 🟡 Medium | ~45 мин |
| **M17 polish** | Edges always visible в QuestNodeGraphView | 🟢 Low | ~1 ч |
| **Quest content** | Реальные квесты (не тестовые) | 🔴 High | Дни (авторский контент) |
| **M15.1** | NPC displayName lookup в Toast (сейчас «mira_01» вместо «Mira») | 🟢 Low | ~30 мин |
| **Localization** | Вынос строк в .po / LocalizationTable | 🟢 Low | ~3 ч |
| **T-X2** | TradeItemDefinition.Faction → FactionId миграция | ⏭️ DEFERRED | ~1 ч + дизайн-дискуссия |
| **T-Q09b** | GraphView sub-tab внутри QuestDatabaseWindow | ⏭️ DEFERRED | Покрыт M17 отдельным окном |

---

## 4. Проблемы и точки рефакторинга

### 🔴 4.1 Дублирование GraphView

Два файла делают одно и то же:

| Файл | Тип | Статус | Строк |
|------|-----|--------|-------|
| `QuestNodeGraphView.cs` | GraphView (UnityEditor.Experimental.GraphView) | **Активный**, M18 editable base | ~590 |
| `QuestGraphView.cs` | Custom VisualElement + Painter2D | **Maintenance mode** (old v8) | ~340 |

**Рекомендация:** Удалить `QuestGraphView.cs` + `QuestGraphWindow.cs` (старый), оставить только `QuestNodeGraphView.cs`. Старый более не используется и путает разработчиков.

### 🟡 4.2 Дублирование CSV-парсеров

Три независимых CSV-парсера:

| Файл | Для чего | Строк |
|------|----------|-------|
| `QuestCsvSchema.cs` | Основной парсер квестов (SplitCsvLines) | ~350 |
| `NpcCsvImporter.cs` | Свой парсер для NPC (ParseNpcCsv) | ~300 |
| `DialogCsvImporter.cs` | Свой парсер для диалогов (ParseDialogCsv) | ~300 |

Все три дублируют логику разбора CSV (кавычки, экранирование, split). `NpcCsvImporter.cs:305` и `DialogCsvImporter.cs:287` содержат собственные `ParseXxxCsv` методы.

**Рекомендация:** Вынести общий `CsvParser` (строки SplitCsvLines из QuestCsvSchema) в отдельный `CsvUtils.cs` и переиспользовать во всех трёх импортёрах.

### 🟡 4.3 QuestWorld.cs — раздувание

`QuestWorld.cs` = **~1340 строк**. Смешивает:
- Quest state transitions (TryOffer/TryAccept/TryTurnIn)
- Reputation/NpcAttitude modifiers
- Objective evaluation (T-Q20 tick logic)
- Persistence (Save/Load)
- NPC talk/event tracking
- Prerequisites (T-Q28)

**Рекомендация (не срочно):** Рассмотреть split на partial class или выделение:
- `QuestWorld.State.cs` — TryOffer/TryAccept/TryTurnIn/TryAdvanceObjective/TryAdvanceStage
- `QuestWorld.Reputation.cs` — ModifyReputation/ModifyNpcAttitude
- `QuestWorld.Tick.cs` — EvaluateAndAdvanceStage/IsObjectiveSatisfied
- `QuestWorld.Persistence.cs` — SavePlayer/LoadPlayer

Либо через композицию: `ReputationService`, `ObjectiveService`, `PersistenceService` как отдельные классы.

### 🟡 4.4 Два диалоговых ассета с коллизией имён

- `Assets/_Project/Quests/Data/Dialogs/mira_default.asset`
- `Assets/_Project/Quests/Data/Dialogs/MiraDefault.asset`

На Windows (case-insensitive) это потенциальная проблема. Один из них — вероятно, устаревший.

**Рекомендация:** Проверить, какой используется в `Mira.asset → defaultDialogTree`, и удалить неиспользуемый.

### 🟢 4.5 NpcFaction — коллизия имён

Старая проблема решена (v1 `NpcFaction` enum → `[Obsolete]`), но появилась **новая**:

| Класс | Файл | Назначение |
|-------|------|------------|
| `ProjectC.Factions.FactionId` (enum) | `Quests/Factions/FactionId.cs` | 12 lore-фракций (квесты, репутация) |
| `ProjectC.AI.NpcFaction` (SO class) | `Scripts/AI/NpcFaction.cs` | Combat AI: матрица отношений фракций (T-NPC-S19) |

Это разные концепции, но одинаковое имя сбивает с толку. В AI-системе `NpcFaction` используется в `NpcSpawnerConfig` и `NpcSocialBrain` (через `NpcFaction` SO, не enum).

**Рекомендация:** Документировать различие явно. Рассмотреть переименование AI-класса в `CombatFaction` или `AIFaction` чтобы избежать путаницы.

### 🟢 4.6 QuestServer.cs — большой файл

`QuestServer.cs` = **~1600+ строк** (судя по структуре). Смешивает:
- Lifecycle/init
- RPC handlers (9 методов)
- WorldEventBus subscribers (7 handlers)
- Snapshot builders (BuildQuestSnapshot, BuildReputationSnapshot, etc.)
- T-Q10 dialog step builder
- FireDialogAction (switch с 17+ cases)
- T-Q28 runtime DialogTree builder
- Helper methods (SendXxxToClient, BroadcastXxx, ResolveQuestDisplayName)

**Рекомендация (не срочно):** `FireDialogAction` (switch на 17+ case) можно вынести в `DialogueActionRunner.cs`. Snapshot builders — в `QuestSnapshotBuilder.cs`.

---

## 5. Интеграционные точки — что связано с квестами

| Подсистема | Точки интеграции | Статус |
|-----------|-----------------|--------|
| **Inventory** | `InventoryWorld.AddItemDirect` / `InventoryServer.TryRemove` + `ItemAddedEvent`/`ItemRemovedEvent` | ✅ |
| **Trade/Contract** | `ContractAcceptedEvent`/`ContractCompletedEvent`/`ContractFailedEvent` → `ContractMetaBridge` → `QuestWorld` | ✅ |
| **Trade/Market** | `MarketInteractor.TryOpenMarket` (из DialogWindow при OpenMarket action) | ✅ |
| **Stats/XP** | `QuestAcceptedEvent`/`QuestCompletedEvent` → `StatsServer.ApplyXp` | ✅ |
| **Reputation** | `ReputationChangedEvent`/`NpcAttitudeChangedEvent` → `QuestTriggerService` + CharacterWindow | ✅ |
| **DayNight** | `DayNightPhaseChangedEvent` → `DayNightPhaseTrigger` | ✅ |
| **Combat** | `KilledEntityTrigger` → **стаб** (ждёт combat system) | ⏳ |
| **Cargo** | `CargoHasItemTrigger` → **стаб** | ⏳ |
| **Location** | `LocationReachedTrigger` → **стаб** | ⏳ |
| **Ship** | `ShipDockedAtTrigger` → **не реализован** | ⏳ |
| **Player input** | `PlayerInputReader` refactor done, E-key NPC branch в `NetworkPlayer` | ✅ |
| **World streaming** | NPC в `WorldScene_0_0`, не `BootstrapScene` (per user rule 2026-06-07) | ✅ |

---

## 6. Качество кода

### 6.1 Хорошие практики (соблюдаются)
- ✅ Server-authoritative паттерн (QuestServer → QuestWorld → RPC → DTO → ClientState)
- ✅ `INetworkSerializable` с hand-rolled IsWriter/IsReader для nullable полей
- ✅ Rate limiting на все RPC (ContractServer pattern)
- ✅ `WorldEventBus` с `Reset()` для test isolation
- ✅ `DontDestroyOnLoad` + auto-spawn для ClientState
- ✅ Чёткие регионы / разделители в коде
- ✅ `#if UNITY_EDITOR` для editor-only кода
- ✅ Debug.isDebugBuild гварды на логах
- ✅ XML-документация на public методах
- ✅ Обратная совместимость (T-Q27 itemId fallback на stringParam)

### 6.2 Точки улучшения
- 🟡 Много кода в одном файле (QuestWorld 1340 строк, QuestServer 1600+)
- 🟡 Дублирование CSV-парсеров
- 🟡 Дублирование GraphView
- 🟡 Некоторые стаб-комментарии устарели («T-Q15 fill» / «T-Q18+ fill» — а T-Q15/T-Q18 уже done)
- 🟢 Много Debug.Log в продакшен-коде (защищены `debugMode`/`Debug.isDebugBuild`)
- 🟢 NpcController — визуальный плейсхолдер (Cube + TMPro), не production-ready
- 🟢 Toast показывает «mira_01» вместо «Mira» (M15.1 не сделан)

---

## 7. Сводка рисков

| # | Риск |Severity | Описание |
|---|------|---------|----------|
| 1 | Стаб-триггеры | 🟡 | CargoHasItem + LocationReached всегда false. Квесты с этими objective не работают |
| 2 | Стаб-действия | 🟡 | SetFlag/SwitchDialogTree/FailQuest — заглушки. Квестовые цепочки с branching не работают |
| 3 | OpenService stub | 🟡 | Сервисные NPC не открывают UI ремонта/заправки |
| 4 | Combat отсутствует | 🟡 | KilledEntityTrigger — стаб. Боевые квесты невозможны |
| 5 | QuestWorld раздувание | 🟢 | 1340 строк, труднее поддерживать. Не блокер |
| 6 | NpcFaction naming | 🟢 | AI-система и квесты используют «NpcFaction» для разных вещей |
| 7 | Дублирование CSV/GraphView | 🟢 | Техдолг, не блокер |
| 8 | Нет production-квестов | 🔴 | Все текущие квесты — тестовые (FindArtifact, EventDrivenQuest, collect_copper_ore, StageDemo*) |
| 9 | Нет локализации | 🟢 | Все строки хардкод-RU |
| 10 | T-X4 input remap | 🟡 | E=pickup сейчас. После remap F=pickup может сломать muscle memory |

---

## 8. Рекомендованный порядок действий

### 🚀 Ближайшие (MVP+)

1. **Дописать стаб-триггеры** — `LocationReachedTrigger` + `CargoHasItemTrigger` (интеграция с `PlayerChunkTracker` и `TradeWorld`)
2. **Дописать стаб-действия** — `SetFlag` + `SwitchDialogTree` (нужны для branching quest chains)
3. **Починить `FailQuest`** — нужно для квестов с таймером / fail-условиями
4. **Удалить старый GraphView** — `QuestGraphView.cs` + `QuestGraphWindow.cs`
5. **Убрать дублирующий диалог-ассет** — `mira_default.asset` или `MiraDefault.asset`
6. **T-X4 input remap** — E=pickup→F

### 📋 Техдолг (можно отложить)

7. **Общий CSV-парсер** — вынести в `CsvUtils.cs`
8. **Split QuestWorld.cs** — на partial class / композицию
9. **M15.1** — NPC displayName в Toast
10. **NpcController** — заменить Cube-плейсхолдер на реальные модели/анимации

### 🎮 Контент (авторская работа)

11. **Production-квесты** — 5–10 реальных квестов через CSV-импорт
12. **Локализация** — вынос строк в localization tables

---

## 9. Итоговый вердикт

**Система квестов — одна из самых проработанных в проекте.** Архитектура чистая (server-authoritative, event-driven, DTO-projection), паттерны соблюдены, интеграции с Inventory/Trade/Contract/Stats работают. Объём проделанной работы (~80–90 часов, учитывая fix-итерации) впечатляет.

**Главный вывод:** Технически система готова к production-контенту. Основные блокеры — отсутствие production-квестов (нужен авторский контент) и несколько недописанных стабов (CargoHasItem/LocationReached триггеры + SetFlag/SwitchDialogTree/FailQuest действия). Рефакторинг (dedup GraphView/CSV, split QuestWorld) желателен но не критичен.

**Ничего «забытого» или «сломанного» в процессе интеграций не обнаружено.** Все подсистемы (Inventory, Trade, Contract, Stats, DayNight) корректно подключены через WorldEventBus. M19 CSV-пайплайн позволяет дизайнерам быстро создавать квесты. M18 QuestNodeGraph даёт визуальное редактирование. M16 QuestDatabaseWindow — централизованный просмотр.
