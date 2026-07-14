# GDD 21: Quest & Mission System

**Game:** Project C: The Clouds
**Version:** 2.0
**Status:** 🟢 Реализовано (M1–M19, 50+ tickets, ~8400 строк кода) + Двойной аудит (июль 2026)
**Last Updated:** 14.07.2026
**Author:** Малков Леонид Андреевич

---

## 1. Обзор системы

### 1.1 Цель

Создать гибкую систему квестов и миссий, обеспечивающую постоянный геймплейный цикл. Система поддерживает сюжетные линии, процедурную генерацию (через CSV pipeline), кооп-режим и долгосрочные цели через ежедневные/еженедельные испытания (deferred).

### 1.2 Объём

- Quest Types — 5 основных типов (design, в SO)
- Quest Generation — через CSV Import/Export pipeline (M19)
- Quest Structure — полный цикл (Discover→Offer→Active→Completed→TurnedIn)
- Reward System — QuestReward SO (credits, items, cargo, reputation, unlocks)
- Quest Chains — multi-stage квесты (M13 T-Q22)
- Multiplayer Quests — per-player, нет shared party progress
- Quest UI — DialogWindow, QuestTracker, QuestToast, CharacterWindow tab
- Daily/Weekly Challenges — deferred

### 1.3 Этапы реализации

| Этап | Система | Статус |
|------|---------|--------|
| M1 | Серверный фундамент (QuestServer, QuestWorld) | ✅ DONE |
| M2 | QuestInstance, DTO, TargetRpc | ✅ DONE |
| M3 | DialogTree, DialogueNode, QuestDefinition SO | ✅ DONE |
| M4 | QuestObjective, QuestStage, QuestPrerequisite | ✅ DONE |
| M5 | Reputation + NpcAttitude MVP (FactionId, NpcAttitude) | ✅ DONE |
| M6 | QuestTriggerService (8+ типов триггеров) | ✅ DONE |
| M7 | MetaRequirement (QuestPrerequisite 7 типов) | ✅ DONE |
| M8 | Persistence (JsonQuestStateRepository) | ✅ DONE |
| M9 | QuestDatabase (central registry SO) | ✅ DONE |
| M10 | CharacterWindow → таб «Квесты» | ✅ DONE |
| M11 | Mira E2E demo (полный playthrough) | ✅ DONE |
| M12 | Input remap (F = pickup, E = NPC) | ✅ DONE |
| M13 | Real-time objectives + Multi-stage | ✅ DONE |
| M14 | ItemRegistry (32 items, single source of truth) | ✅ DONE |
| M15 | QuestToast (queue-based, 2.5s display) | ✅ DONE |
| M16 | QuestDatabaseWindow (Editor) | ✅ DONE |
| M17 | QuestGraphView (readonly viz) | ✅ DONE |
| M18 | Editable QuestGraph (add/delete, drag edges) | ✅ DONE |
| M19 | CSV Import/Export (3 формата, 802 квеста, 106 NPC) | ✅ DONE |
| T-NPC-ENEMY | NPC Enemy System (Goblins, FSM, loot) | ✅ DONE |
| Future | Daily/Weekly | ⏳ DEFERRED |
| Future | Procedural quest generation | ⏳ DEFERRED |
| Future | Multiplayer shared party progress | ⏳ DEFERRED |

### 1.4 Связанные документы

- GDD_20_Progression_RPG.md
- GDD_23_Faction_Reputation.md
- GDD_22_Economy_Trading.md
- `docs/NPC_quests/08_ROADMAP.md` — главный roadmap
- `docs/NPC_quests/02_V2_ARCHITECTURE.md` — namespace layout
- `docs/NPC_quests/DEEP_AUDIT_2026-07-09.md` — аудит №1
- `docs/NPC_quests/DEEP_AUDIT_2026-07-13.md` — аудит №2

---

## 2. Quest Types

### 2.1 Основные типы квестов (design)

Типы квестов описываются в `QuestDefinition` SO. Создаются дизайнером вручную или через CSV импорт.

| Тип | Описание | Сложность | Время выполнения |
|-----|----------|-----------|------------------|
| **Доставка** | Перевозка груза между точками | Easy-Medium | 5-15 мин |
| **Разведка** | Исследование неизвестных зон | Medium-Hard | 10-25 мин |
| **Сопровождение** | Защита NPC или другого игрока | Hard | 15-30 мин |
| **Контрабанда** | Нелегальная перевозка через СОЛ | Hard-Expert | 10-20 мин |
| **Поиск артефактов** | Обнаружение редких предметов | Expert-Master | 20-45 мин |

> **Примечание:** Дизайн-контент типов квестов — зона game-designer'а. Runtime система не знает о «типе» квеста — квест определяется набором stage'ей + objectives.

Формулы наград (design reference):
```
Reward = base_reward × difficulty × reputation_modifier × completion_bonus
XP = base_XP × difficulty × level_modifier × co_op_bonus
Rep = base_rep × difficulty × faction_modifier
```

---

## 3. Quest Structure (реализация)

### 3.1 QuestState enum

```csharp
enum QuestState {
    Discovered = 0,  // найден, но не начат
    Offered = 1,     // предложен NPC
    Active = 2,      // принят, в процессе
    Completed = 3,   // условия выполнены, не сдан
    Failed = 4,      // провален
    TurnedIn = 5     // сдан, награды получены
}
```

### 3.2 Жизненный цикл квеста

```
[Discovered] → [Offered] → [Active] → [Completed] → [TurnedIn]
                  ↓            ↓
              [Declined]   [Failed]
```

### 3.3 QuestDefinition (ScriptableObject)

| Поле | Описание |
|------|----------|
| questId | Уникальный ID (string, stable key) |
| displayName | Имя квеста |
| description | Описание |
| faction | FactionId привязка |
| minReputation | Мин. репутация фракции |
| stages[] | Массив этапов (QuestStage) |
| rewards | QuestReward (credits, items, reputation, unlocks) |
| prerequisites[] | QuestPrerequisite (7 типов) |
| oneShot | Нельзя повторить |
| discoverable | Можно найти через Discover |

### 3.4 QuestStage

| Поле | Описание |
|------|----------|
| stageId | int, уникальный в рамках квеста |
| objectives[] | QuestObjective (8 типов) |
| onEnterActions[] | DialogueAction (17 типов) при входе |
| onCompleteActions[] | DialogueAction при завершении |
| nextStageId | ID следующего этапа (null = последний) |

### 3.5 QuestObjective (8 типов)

| Тип | Описание |
|-----|----------|
| HaveItem | Иметь предмет в инвентаре |
| TalkToNpc | Поговорить с NPC |
| StandOnTrigger | Стоять на триггер-зоне |
| ReachLocation | Достичь локации (poll 5s) |
| ReputationAtLeast | Репутация фракции ≥ N |
| NpcAttitudeAtLeast | Отношение NPC ≥ N |
| WaitForEvent | Ждать WorldEventBus события |
| EventDriven | Кастомное событие |

### 3.6 QuestReward

| Поле | Описание |
|------|----------|
| credits | float |
| items[] | ItemQuantity (ItemRegistry id + count) |
| cargoItems[] | ItemQuantity для грузового отсека |
| reputation[] | ReputationDelta (FactionId + delta) |
| unlocks[] | string[] (world flags, titles) |

### 3.7 QuestPrerequisite (7 типов)

QuestCompleted, QuestNotCompleted, FactionReputation, NpcAttitude, HasItem, Level, WorldFlag.

---

## 4. Reward System

### 4.1 Компоненты наград (design)

| Тип награды | Диапазон |
|-------------|----------|
| XP | Через WorldEventBus → StatsServer (QuestCompleted: 10 XP базовых) |
| Кредиты | Через QuestReward.credits → QuestServer.GiveCredits |
| Ресурсы | Через QuestReward.items[] |
| Репутация | Через QuestReward.reputation[] → QuestWorld.ModifyReputation |
| Предметы | Через ItemRegistry → инвентарь игрока |
| Разблокировки | World flags через QuestReward.unlocks[] |

### 4.2 Таблица лута (design)

| Качество | Шанс | Пример |
|----------|------|--------|
| Обычный | 60% | Стандартные модули |
| Необычный | 25% | Улучшенные модули |
| Редкий | 10% | Уникальные модули |
| Эпический | 4% | Легендарные модули |
| Легендарный | 1% | Уникальные предметы |

---

## 5. Skill Progression (RPG integration)

XP за квесты идёт через WorldEventBus:
- `QuestAcceptedEvent` → StatsServer.ApplyXp (3 XP базовых)
- `QuestCompletedEvent` → StatsServer.ApplyXp (10 XP базовых)

Оба конфигурируются в `ExperienceConfig`. StatType — через `StatSourceMapConfig`.

---

## 6. Dialog System

### 6.1 DialogTree (ScriptableObject)

| Компонент | Описание |
|-----------|----------|
| DialogTree | SO — дерево диалогов |
| DialogueNode | Узел: текст NPC, опции, условия, действия |
| DialogueEdge | Связь: label, targetNodeId, conditions[], action, hideIfUnavailable |
| DialogueCondition | 12 типов условий (HasItem, QuestStateEquals, ReputationAtLeast, ...) |
| DialogueAction | 17 типов действий (GiveCredits, TakeItem, AddReputation, AddNpcAttitude, CompleteObjective, ...) |

### 6.2 DialogWindow (UI Toolkit)

- Typewriter char-by-char (40 chars/sec), F-skip, mouse-click skip
- ESC close, cursor management, pickingMode toggle
- 4 FIX'ы от MarketWindow унаследованы
- TEXT NPC всегда виден сверху, кнопки квестов прокручиваются (T-UI04 fix)

---

## 7. Quest UI

### 7.1 QuestTracker (HUD overlay)

- Singleton MonoBehaviour, scene-placed, DontDestroyOnLoad
- Top-right позиция
- `Track(questId)` / `Untrack()` / `Toggle(questId)`
- Auto-hide когда нет tracked, auto-untrack если quest удалён
- 9 UI Toolkit FIX'ы

### 7.2 QuestToast (M15)

- Runtime VisualElement, bottom-center
- Queue-based (2.5s display)
- События: "📜 Accepted: ...", "💚 mira_01 +5", "💰 +200 CR", "✨ Найден квест: ..."

### 7.3 CharacterWindow → таб «Квесты» (T-Q11)

- 4 под-секции: active / completed / failed / discovered
- State badge + Accept-кнопка для Discovered
- Track-кнопка в строках (T-Q12)

### 7.4 Editor UI

| Инструмент | Описание |
|-----------|----------|
| QuestDatabaseWindow (M16) | UI Toolkit EditorWindow, TreeView + Detail panel. `Tools > ProjectC > Quests > Quest Database Explorer` |
| QuestNodeGraphView + QuestGraphView (M17) | Readonly graph viz: `Tools > ProjectC > Quests > Quest Node Graph` + `Assets/ProjectC/Open Quest Graph`. 4 node types: Quest, Stage, Objective, Reward |
| Editable QuestNodeGraph (M18) | T-Q30..T-Q34: TextField в нодах, save back to SO, add/delete stages/objectives, quest-to-quest prereq edge, drag-create edges |

---

## 8. Quest Chains (multi-stage)

### 8.1 Реализация (M13 T-Q22)

Multi-stage квесты — один `QuestDefinition` с несколькими `QuestStage`, соединёнными `nextStageId`.

**Flow:**
```
TryAdvanceStage:
  → fire onCompleteActions[currentStage]
  → transition (currentStageId = nextStageId)
  → fire onEnterActions[nextStage]
```

### 8.2 Цепочки гильдий (design)

5 основных цепочек (по одной на гильдию):

| Гильдия | Цепочка | Квестов | Мин. уровень | Награда |
|---------|---------|---------|--------------|---------|
| Мысли | "Тайны Разума" | 10 | 10 | Титул "Мыслитель" |
| Созидания | "Мастер Созидания" | 10 | 10 | Уникальный модуль |
| Силы | "Путь Воина" | 10 | 10 | Боевой корабль |
| Тайн | "Хранитель Тайн" | 10 | 15 | Секретная локация |
| Успеха | "Золотой Путь" | 10 | 10 | Торговая монополия |

> **Примечание:** В коде созданы тестовые квесты (5 штук: collect_copper_ore, find_artifact, stage_intro_demo, stage_multi_demo, collect_copper). Production-контент цепочек требует квест-дизайнера.

---

## 9. CSV Import/Export Pipeline (M19)

### 9.1 Форматы

| Файл | Колонок | Описание |
|------|---------|----------|
| `*_quests.csv` | 21 колонка | Definition + stages + objectives + rewards + prerequisites |
| `*_npcs.csv` | 9 колонок | NPC data + attitude links |
| `*_dialogs.csv` | 15 колонок | Диалоговые деревья |

### 9.2 Auto-фичи (T-Q19.1..3)

- Auto-populate `questTurnIns` (последний stage с TalkToNpc)
- Auto-link `defaultDialogTree` по `{npcId}_default` convention
- `NpcCsvImporter` — 395 строк, batch-update NPC

### 9.3 Тестовые данные

- 106 NPC, 802 квеста — 1 кнопка импорт
- Примеры: `Assets/_Project/Quests/Import/{example_quests,example_npcs,example_dialogs}.csv`

### 9.4 Инструмент

`Tools > ProjectC > Quests > CSV Import/Export`

### 9.5 Документация для writer'а

`docs/NPC_quests/M19_CSV_PIPELINE_v2.md` (26 KB, 7 разделов)

---

## 10. Trigger System

### 10.1 QuestTriggerService

- `QuestWorld.Update()` → `TriggerService.EvaluateAll()` каждые 5 секунд
- Связывает WorldEventBus события с прогрессом квестов

### 10.2 Типы триггеров (8+)

| Тип | Описание |
|-----|----------|
| TalkedToNpc | Разговор с NPC |
| HaveItem | Получение предмета |
| CargoHasItem | Получение груза |
| ReputationAtLeast | Репутация ≥ |
| NpcAttitudeAtLeast | Отношение NPC ≥ |
| LocationReached | Достижение локации (poll 5s) |
| DayNightPhase | Время суток |
| EventDriven | Кастомное событие |
| KilledEntity (stub) | Убийство NPC |
| ContractAccepted/Completed | Контракт (через ContractMetaBridge) |

---

## 11. NPC System

### 11.1 NpcDefinition (ScriptableObject)

| Поле | Описание |
|------|----------|
| npcId | Уникальный ID (string) |
| displayName | Имя NPC |
| faction | FactionId |
| questOffers[] | Какие квесты предлагает |
| questTurnIns[] | Какие квесты принимает |
| attitudeLinks[] | Cross-faction influence конфиги |

### 11.2 NpcController (scene-placement)

- MonoBehaviour с trigger collider
- Scene-placed в `WorldScene_0_0.unity`
- E-key chain extension

### 11.3 NPC Enemy System (T-NPC-ENEMY, июнь 2026)

| Компонент | Назначение |
|-----------|------------|
| NpcBrain | FSM (Idle→Chase→Attack→Dead), 4 RPC |
| NpcSpawner | Surface validation, rate-limit, leash 30m, batch spawn |
| GoblinEnemy prefab | NetworkObject + NavMeshAgent + Animator |
| NpcLootTable (SO) | Weighted item list, drop chance |
| LootPicker | Drop items on death, pickup interaction |

---

## 12. Contract → Quest Bridge

### 12.1 ContractMetaBridge

Server-side singleton, scene-placed в BootstrapScene, DontDestroyOnLoad.

**Поток:**
1. `ContractServer` публикует 3 events в `WorldEventBus`:
   - `ContractAcceptedEvent`
   - `ContractCompletedEvent`
   - `ContractFailedEvent`
2. `ContractMetaBridge` подписан → `QuestWorld.MarkContractAccepted/Completed/Failed`
3. `QuestTriggerService.Evaluate($"ContractCompleted:{contractId}")`

---

## 13. Persistence

### 13.1 JsonQuestStateRepository

- `IQuestStateRepository` interface
- Atomic JSON write per-client в `Application.persistentDataPath`
- `QuestSaveData` (POCO): quests + rep + npcAttitude + 5 string sets
- Immediate save на каждый state change
- Load on `OnClientConnectedCallback` в `QuestServer`

---

## 14. Аудиты (июль 2026)

### Двойной глубокий аудит (T-QAUDIT)

| Аудит | Дата | Файл | Ключевые находки |
|-------|------|------|-----------------|
| Первый | 2026-07-09 | docs/NPC_quests/DEEP_AUDIT_2026-07-09.md | Архитектура, стабы, дублирование, интеграции |
| Повторный | 2026-07-13 | docs/NPC_quests/DEEP_AUDIT_2026-07-13.md | Сравнение с предыдущим, регрессы |

### Критические находки

- **Квестовые ассеты утеряны:** FactionDefinition, NpcDefinition, QuestDefinition файлы отсутствуют, GUIDs в QuestDatabase висят в никуда. Требуется восстановление из CSV-бэкапов.
- **DialogWindow fix (T-UI04, `aa2a1ec`):** Текст NPC всегда виден сверху, кнопки квестов прокручиваются. Fix: 85vh → 520px (vh не поддерживается Unity USS).

---

## 15. Что открыто / TODO

| # | Задача | Milestone | Приоритет |
|---|--------|-----------|-----------|
| 1 | **Quest assets утеряны** (FactionDefinition, NpcDefinition, QuestDefinition SO) | Требуется восстановление | 🔴 High |
| 2 | **Quest content — 5-10 production квестов** | post-MVP | 🔴 High |
| 3 | **Multiplayer shared party progress** | post-MVP | 🟡 Med |
| 4 | **Daily/Weekly Challenges** | Future | 🟢 Low |
| 5 | **Procedural quest generation** | Future | 🟢 Low |
| 6 | **Localization (все строки в .po)** | post-MVP | 🟢 Low |
| 7 | **T-X2 — Faction migration** (TradeItemDefinition.Faction → FactionId) | design discussion | 🟡 Med |

---

## 16. Архитектура

### 16.1 Server-side

```
QuestServer (NetworkBehaviour, BootstrapScene)
  ├─ 9 RPC (RequestTalkToNpc, RequestAdvanceDialogue, RequestAcceptQuest, ...)
  ├─ Rate limit 30 ops/min/client
  └─ QuestWorld (POCO singleton)
       ├─ _questById: Dictionary<string, QuestDefinition>
       ├─ _questsByPlayer: Dictionary<ulong, List<QuestInstance>>
       ├─ _reputation: Dictionary<(ulong, FactionId), int>
       ├─ _npcAttitude: Dictionary<(ulong, string), int>
       ├─ _worldFlags: Dictionary<(ulong, string), bool>
       ├─ _dialogByPlayer: Dictionary<ulong, DialogSession>
       ├─ TriggerService: QuestTriggerService
       └─ Repository: IQuestStateRepository
```

### 16.2 Client-side

```
QuestClientState (singleton, AutoSpawn)
  ├─ CurrentSnapshot (QuestSnapshotDto)
  ├─ Reputation / NpcAttitude
  ├─ LastResult / LastRepResult
  ├─ 6 events: OnSnapshotUpdated, OnReputationUpdated, OnQuestResult, ...
  └─ RequestAcceptQuest(questId, fromNpcId)

UI:
  ├─ DialogWindow (UIDocument)
  ├─ QuestTracker (HUD overlay)
  ├─ QuestToast (runtime VisualElement)
  └─ CharacterWindow → tab «Квесты»
```

### 16.3 Data Layer (ScriptableObjects)

QuestDefinition | QuestStage | QuestObjective | QuestReward | QuestPrerequisite | QuestState
DialogTree | DialogueNode | DialogueEdge | DialogueCondition | DialogueAction | SpeakerRef
NpcDefinition | FactionDefinition | FactionId | NpcAttitude
QuestDatabase | ItemRegistry

### 16.4 Editor Tooling

QuestDatabaseWindow | QuestNodeGraphView | QuestGraphView | QuestGraphWindow
QuestCsvImporter | QuestCsvExporter | QuestCsvSchema | QuestCsvWindow
DialogCsvImporter | NpcCsvImporter | DialogueConditionDrawer
QuestDefinitionValidator | QuestDatabaseAutoDiscover

---

## 17. Файлы (C#)

```
Quests/
├── Bridges/ContractMetaBridge.cs
├── Client/QuestClientState.cs
├── Core/QuestInstance.cs, QuestWorld.cs
├── Dialogue/DialogTree.cs, DialogueAction.cs, DialogueCondition.cs, DialogueNode.cs, SpeakerRef.cs
├── Dto/DialogStepDto.cs, QuestProgressDto.cs, QuestResultDto.cs, QuestSnapshotDto.cs, ReputationSnapshotDto.cs
├── Editor/DialogCsvImporter.cs, DialogueConditionDrawer.cs, NpcCsvImporter.cs, QuestCsv*.cs, QuestDatabase*.cs, QuestGraph*.cs, QuestNodeGraphView.cs
├── Factions/FactionDefinition.cs, FactionId.cs, NpcAttitude.cs
├── Network/QuestServer.cs
├── NpcController.cs
├── Npcs/NpcDefinition.cs
├── Persistence/IQuestStateRepository.cs, JsonQuestStateRepository.cs, QuestSaveData.cs
├── QuestDatabase.cs
├── Quests/QuestDefinition.cs, QuestObjective.cs, QuestPrerequisite.cs, QuestReward.cs, QuestStage.cs, QuestState.cs, QuestStateTransition.cs
├── Testing/M13QuestTriggerZone.cs
├── Triggers/ConcreteTriggers.cs, IQuestTrigger.cs, QuestTriggerService.cs
└── UI/DialogWindow.cs, QuestToast.cs, QuestTracker.cs
```

---

## 18. Tuning Knobs

| Параметр | Default | Описание |
|----------|---------|----------|
| MaxActiveQuestsPerPlayer | 20 | Максимум активных квестов |
| QuestTriggerService tick interval | 5s | Частота оценки прогресса |
| Dialog typewriter speed | 40 chars/sec | Скорость печати текста |
| QuestToast display time | 2.5s | Время показа уведомления |
| Rate limit | 30 ops/min/client | Защита от спама RPC |

---

*Документ создан для Project C: The Clouds.*
