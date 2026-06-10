# Анализ системы NPC + Quests v2 — состояние после M11

> Дата: 2026-06-08
> Сессий: ~10 (T-Q01..T-Q19, T-X1, T-X5)
> Код: 28 файлов (новых), ~5000 LOC (всего изменений)
> Документация: 18 design notes

---

## 1. Что мы построили — общая архитектура

```
ДАННЫЕ (ScriptableObject — ассеты в папках)
  ├── NpcDefinition.asset     — NPC: id, faction, dialogTree, questOffers/TurnIns
  ├── QuestDefinition.asset    — квест: id, stages, objectives, rewards (credits/items/rep)
  ├── DialogTree.asset         — диалог: nodes, edges, conditions, actions, onEnterActions
  ├── FactionDefinition.asset  — фракция: id, color, reputation tiers
  └── QuestDatabase.asset      — реестр (авто-сканирование, не ручной)

СЕРВЕР (Unity.Netcode NetworkBehaviour — BootstrapScene)
  ├── QuestServer — хаб: RPC (RequestTalkToNpc, RequestAdvanceDialogue, Accept, TurnIn)
  │                 EvaluateConditions, FireDialogAction, BroadcastReputation/Attitude
  ├── QuestWorld (POCO singleton) — state: questsByPlayer, reputation, npcAttitude, flags
  ├── InventoryServer — AddItemDirect, TryRemove, PushSnapshot
  ├── ContractServer — контракты + bridge на quests
  ├── MarketServer — market snapshots
  └── TradeWorld — Repository (credits)

КЛИЕНТ (singletons — BootstrapScene, DontDestroyOnLoad)
  ├── QuestClientState — snapshot квестов
  ├── ReputationClientState — репутация (11 factions)
  ├── NpcAttitudeClientState — отношение к NPC
  ├── InventoryClientState — инвентарь + credits
  ├── ContractClientState — контракты
  └── MarketClientState — market + credits

UI (UI Toolkit — UIDocument)
  ├── DialogWindow  — floating диалог (typewriter, F-skip, портрет, attitude badge)
  ├── QuestTracker  — HUD (текущий отслеживаемый квест)
  ├── CharacterWindow — P-меню: табы КВЕСТЫ, РЕПУТАЦИЯ, NPC, ПЕРСОНАЖ
  └── MarketWindow — торговля (существовала до квестов)

EDITOR TOOLS
  ├── QuestDatabaseAutoDiscover — авто-сканирование asset'ов, Tools/ProjectC/Quests/Re-scan
  ├── QuestDefinitionValidator — валидация квестов, меню Tools/ProjectC/Validate All Quests
  └── DialogueConditionDrawer — Property Drawer для условий/действий в Inspector

PERSISTENCE
  ├── IQuestStateRepository + JsonQuestStateRepository — server-side JSON
  ├── JsonInventoryRepository — инвентарь
  └── PlayerPrefsRepository — credits/контракты
```

---

## 2. Что НЕ доделано

### Editor Tools (T-Q09b — GraphView deferred)
- **❌ QuestDatabaseWindow** — IDE-style окно с TreeView, поиском, reverse-index (**не создано**)
- **❌ GraphView** — визуальный редактор DialogTree (кликай-перетаскивай nodes)
- **❌ QuestAssetWatcher** — AssetPostprocessor для авто-инвалидации кеша
- **❌ QuestValidator CLI** — для CI/CD

Существующие editor tools — только вспомогательные утилиты. **Полноценного GUI для нарратив-дизайнера нет.** Редактирование — через стандартный Inspector + SO ассеты.

### Система условий (ConditionTypes)
- `CargoHasItem` (11) — не реализован (stub → `return true`)
- `TimeOfDayIn` (40) — реализован (проверяет DayNightController)
- `PlayerInZone` (41) — не реализован (stub → `return true`)
- `WasNodeVisited` (43) — не реализован
- **Нет NOT-логики** — `QuestNotEquals` / `!HasItem` не существует. Для инверсии нужно дублировать edges.

### Система действий (ActionTypes)
- `GiveCargoItem` (22) / `TakeCargoItem` (23) — stub (нет active ship tracking)
- `FailQuest` (12) — stub
- `DiscoverQuest` (13) — stub
- `SetFlag` (50) — stub
- `SwitchDialogTree` (52) — stub
- `CompleteObjective` (11) — M11 fix: real impl (вызывает TryTurnIn)
- `AcceptQuest` (14) — NEW: TryOffer+TryAccept

### Objective System (QuestDefinition.objectives)
- **❌ Objectives НЕ проверяются** — QuestWorld.TryAccept/TryTurnIn не проверяют объективы (авто-комплит)
- `QuestObjectiveType.TalkToNpc` (0) — не отслеживается
- `QuestObjectiveType.ReachLocation` (2) — не отслеживается
- `QuestObjectiveType.HaveItem` (4) — не отслеживается
- `QuestObjectiveType.EventDriven` (7) — не отслеживается
- **Нет TriggerService.Evaluate в реальном времени** — авто-проверка objectives не реализована

### Другое
- **Нет auto-complete по objectives** — квест завершается только через CompleteObjective dialog action
- **Нет stage transition** — onEnterActions/onCompleteActions на стадиях не вызываются
- **Нет тостов/уведомлений** — игрок не видит "Квест получен", "Награда выдана"
- **T-X2** — TradeItemDefinition.Faction vs FactionId — **DEFERRED** (design issue)
- **T-Q09b** — GraphView — **DEFERRED**
- **30+ deprecated warnings** (ShipKeyClientState, FindObjectsOfType) — T-X3 pending

---

## 3. Хардкод vs конфигурируемость — честный разбор

### Что полностью настраивается через SO (без кода): ✅

| Компонент | Как создать |
|-----------|-------------|
| **NPC** | Создать `NpcDefinition.asset` → заполнить id, faction, dialogTree, questOffers |
| **Квест** | Создать `QuestDefinition.asset` → questId, stages, objectives, rewards |
| **Диалоговое дерево** | Создать `DialogTree.asset` → nodes, edges, conditions, actions, onEnterActions |
| **Фракция** | Создать `FactionDefinition.asset` → id, color, reputation tiers |
| **Условия на edge** | `DialogueCondition` — 12 типов, настраиваются в Inspector |
| **Действия на edge** | `DialogueAction` — 18 типов, настраиваются в Inspector |
| **Награды квеста** | `QuestDefinition.rewards` — credits, items, reputation, unlocks |
| **hideIfUnavailable** | `bool` на каждом edge — скрыть или показать "заблокировано" |

### Что реализовано generic (любой квест, любая NPC): ✅

| Компонент | Доказательство |
|-----------|----------------|
| `AcceptQuest` (14) | TryOffer + TryAccept — любой questId |
| `TakeItem` / `GiveItem` | `int.TryParse(stringParam)` — любой itemId |
| `GiveCredits` | `intParam` credits — любые |
| `AddReputation` | `factionParam` + `intParam` — любая фракция |
| `AddNpcAttitude` | `stringParam=npcId` + `intParam` — любой NPC |
| `CompleteObjective` | `stringParam=questId` — любой quest (TryTurnIn) |
| `HasItem` condition | `stringParam=itemId, intParam=count` — любой предмет |
| `QuestStateEquals` condition | `stringParam=questId, questStateParam` — любой квест |
| `ReputationAtLeast` condition | `factionParam, intParam` — любая фракция |
| `hideIfUnavailable` | Все edges — сервер фильтрует |
| `onEnterActions` | Все nodes — сервер вызывает |

### Что хардкодно (требует C# изменений): 🟡

| Часть | Почему хардкод |
|-------|----------------|
| **ItemType в TakeItem/GiveItem** | Всегда `ItemType.Resources`. Нельзя выдать предмет типа `Equipment` из диалога |
| **Item id mapping (string→int)** | Dialog tree хранит `stringParam`, инвентарь использует `int itemId`. `int.TryParse` — костыль |
| **ItemData и PickupItem** | `itemId` в PickupItem — int, но `ItemData` не регистрируется в едином каталоге с id |
| **onEnterActions execution** | Был баг (не вызывались) — M11 fix добавил generic цикл |
| **snapshot push** | После каждого change — нужно было добавлять вручную (M11 fix: починили) |
| **credits в InventorySnapshot** | Был `0f`, M11 fix: читает Repository. Generic ✅ |
| **QuestStateEquals: return false** | M11 fix: теперь generic |
| **No QuestDatabaseWindow** | Полноценный explorer не создан |

### ИТОГО: Мои M11 фиксы

**НЕ были хардкодом для Mira.** Все изменения — generic:

| Фикс | Generic? |
|------|----------|
| `AcceptQuest = 14` | ✅ Любой questId |
| `HasItem` parsing | ✅ Любой itemId |
| `QuestStateEquals` return false | ✅ Любой quest |
| `hideIfUnavailable` filter | ✅ Все edges |
| `onEnterActions` execution | ✅ Все nodes |
| `BroadcastReputationChange` | ✅ Any faction |
| `BroadcastNpcAttitudeChange` | ✅ Any NPC |
| `SendQuestSnapshotToClient` | ✅ Any quest |
| `InventorySnapshot.credits` | ✅ Any player |
| `InventoryServer.PushSnapshot` | ✅ Any player |

**Единственное, что специфично для Mira** — сам файл `MiraDefault.asset` (dialog tree) и `FindArtifact.asset` (quest definition). Это **данные**, не код.

---

## 4. Как создавать новые квесты (инструкция для непрограммиста)

### Быстрый старт — 10 минут

**Шаг 1. Создать предметы (если нужны)**

Есть 2 пути:
- **(а) Использовать существующие ItemData** — из папки `Assets/_Project/Items/` (30 штук: медь, железо, ключи, еда...)
- **(б) Создать новый** — `RMB → Create → Project C → Item Data`, заполнить имя, тип

**Шаг 2. Создать квест**

`RMB → Assets/_Project/Quests/Data/Quests/ → Create → Project C → Quest Definition`

Заполнить:
- `questId` — уникальный ID (например "rescue_pilot")
- `displayName` — "Спасатель пилота"
- `description` — текст описания
- `faction` — фракция (необязательно)
- `stages[].objectives[]` — не обязательно (пока не проверяются)
- `rewards` — credits, items, reputation (выдадутся при TurnIn)

**Шаг 3. Создать NPC (если новый)**

`RMB → Assets/_Project/Quests/Data/Npcs/ → Create → Project C → Npc Definition`

Заполнить:
- `npcId` (уникальный)
- `faction`
- `defaultDialogTree` — привязать DialogTree
- `questOffers[]` — добавить questId
- `questTurnIns[]` — добавить questId

**Шаг 4. Создать DialogTree**

`RMB → Assets/_Project/Quests/Data/Dialogs/ → Create → Project C → Dialog Tree`

Самый важный шаг. Структура:

```
greeting ── "У тебя есть работа?" ──→ offer_quest (условие: HasItem)
         ── "Я принёс предмет"   ──→ turn_in     (условие: QuestStateEquals Active)
         ── "Пока"               ──→ end

offer_quest ── "Я помогу" ──→ accept_thanks (action: TakeItem(A))
            ── "Нет"      ──→ decline

accept_thanks ── "Хорошо" ──→ end (action: AcceptQuest = type:14)
```

**Каждый элемент node:**
- `speaker` — кто говорит
- `text` — что говорит
- `edges[]` — варианты ответа игрока:
  - `label` — текст кнопки
  - `condition` / `conditions[]` — условия видимости (AND)
  - `action` — действие при клике
  - `targetNodeId` — куда перейти (пусто = закрыть диалог)
  - `hideIfUnavailable` — скрыть если недоступно
- `onEnterActions[]` — действия при входе в node (награды и т.п.)

**Шаг 5. Разместить NPC в сцене**

Если NPC ещё нет в `WorldScene_0_0`:
1. Открыть сцену `Assets/_Project/Scenes/World/WorldScene_0_0.unity`
2. Создать GameObject → добавить `NpcController` (v2)
3. В `NpcController` указать `npcId` (должен совпадать с `NpcDefinition.npcId`)

**Шаг 6. Разместить Pickup предметы (если нужны)**

1. Перетащить `Assets/_Project/Prefabs/PickupItem_Test.prefab` в сцену
2. Установить `itemId` (int) и `itemData` (ItemData SO)
3. Убедиться что `itemData.itemType == Resources` (иначе TakeItem не найдёт)

**Шаг 7. Проверить**

1. `Tools → ProjectC → Validate All Quests` — нет ошибок
2. `Tools → ProjectC → Quests → Re-scan Quest Database` — обновить реестр
3. Start host → пройти квест

### Типовые паттерны

**"Дай предмет → получи квест":** 
- HasItem(itemA, 1) на edge "У тебя есть работа?"
- "Я помогу" → action TakeItem(itemA)
- "Хорошо" → action AcceptQuest(questId)

**"Принеси предмет → получи награду":**
- onEnterActions complete_thanks → AddReputation + AddNpcAttitude + CompleteObjective
- Edge "Спасибо" → action GiveCredits(1000)

**"Диалог без квеста (просто репутация)":**
- Edge → action AddReputation

---

## 5. Editor Tools — что как использовать

### Существующие (работают)

| Инструмент | Меню | Назначение |
|------------|------|------------|
| **QuestDatabaseAutoDiscover** | `Tools → ProjectC → Quests → Re-scan Quest Database` | Авто-сканирование всех .asset файлов → заполняет реестр |
| **QuestDefinitionValidator** | `Tools → ProjectC → Validate All Quests` | Проверяет dangling references, пустые стейджи, ошибки |
| **QuestDefinitionValidator (single)** | `Tools → ProjectC → Validate Selected Quest` | Проверяет только выбранный в Project окне ассет |
| **DialogueConditionDrawer** | (авто) | Умный Inspector — показывает только релевантные поля для выбранного типа условия/действия |

### Не созданы (нужна отдельная сессия)

| Инструмент | Почему важно |
|------------|--------------|
| **QuestDatabaseWindow** | Единое окно: иерархия NPC→квесты→диалоги, reverse-index, поиск |
| **GraphView** | Визуальное редактирование DialogTree (кликай-перетаскивай nodes) |
| **QuestAssetWatcher** | Авто-обновление индекса при изменении asset'ов |
| **NpcSpawner** | Кнопка "Spawn NPC в сцене" из NpcDefinition |

---

## 6. Где мы сейчас и что дальше

### Milestones roadmap

| M | Статус | Что |
|---|--------|-----|
| M1-M3 | ✅ | Data foundation + server + player interaction |
| M4 | ✅ | Quest log + tracker |
| M5 | ✅ | Reputation + NpcAttitude |
| M6 | ✅ | Item integration + bridge |
| M7 | ✅ | Full action set |
| M8 | ✅ | Persistence (JSON) |
| M9 | 🟡 | Cleanup (T-Q19 ✅ T-X1 ✅ T-X2 DEFERRED) |
| M10 | 🟡 | Editor tool (CRUD done, GraphView DEFERRED) |
| **M11** | **🟡 Demo** | **Accept → Play → TurnIn → Rewards — PROVEN** |
| M12 | ⏭️ | Input remap (F = pickup) |

### Ближайшие задачи

1. **M11 — финальный тест** (user runs)
2. **T-X3** — deprecated API warnings cleanup (ShipKeyClientState, FindObjectsOfType)
3. **T-Q09b** — GraphView sub-tab (большой editor tool)
4. **Objective system** — реальная проверка objectives (авто-комплит stage при выполнении условий)
5. **System toasts** — уведомления "Квест получен", "Награда выдана"

### Ограничения, которые нужно знать

- **Item id: string vs int** — Dialog tree использует `stringParam`, Inventory — `int itemId`. Конвертация через `int.TryParse` — хрупко. Нужен единый реестр TradeItemDefinition (string) → itemDataId (int)
- **ItemType всегда Resources** — TakeItem/GiveItem не умеют Equipment/Food/Tech. Если предмет другого типа — не сработает
- **Objectives не проверяются** — авто-прогресс квеста только через dialog actions (CompleteObjective). Нет проверки "игрок подобрал предмет → objective resolved"
- **Нет встроенного тестирования** — ни одного юнит-теста или play-mode теста
- **Нет локализации** — текст зашит в ассетах на русском
