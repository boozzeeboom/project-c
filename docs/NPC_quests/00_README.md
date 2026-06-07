# NPC + Quest Subsystem — Architecture & Roadmap

> **Статус:** Дизайн-документ (аналитическая фаза). Код не написан.
> **Сессия:** 2026-06-07 (Mavis, profile `project-c`)
> **Каталог:** `docs/NPC_quests/`
> **Конвенция:** См. `AGENTS.md` и `project-c-bootstrap` skill (Unity 6, NGO 2.11, UI Toolkit, .NET 8).

---

## Назначение

Единая система **NPC + диалоги + квесты + репутация**, заменяющая существующий
v1-заглушенный код в `Assets/_Project/Scripts/World/Npc/` (4 файла, 1440 строк,
9 TODO-стабов, 0 production-вызовов). Цели:

1. **Игроку** — классический point-and-talk с NPC: подошёл → нажал `E` →
   открылся диалог с портретом, typewriter-эффектом, ответами-«выходящими
   строками с outline», репутационно-окрашенными, квестовыми целями,
   проверяющими инвентарь/корабль/события мира.
2. **Геймдизайнеру / нарратив-дизайнеру** — удобное создание многоуровневых
   квестов, NPC, диалоговых графов. Один редактор-инструмент «Quest Database
   Explorer», где видно все связи: NPC → его квесты → предметы наград → другие
   NPC.
3. **Архитектору** — соблюсти v2-конвенцию проекта: server-hub
   (`NetworkBehaviour`) + `[Rpc(SendTo.X)]` + DTO (`INetworkSerializable`) +
   `ClientState` projection + UI Toolkit + `IPlayerDataRepository` persistence.

---

## Карта документации

| # | Файл | О чём | Слов |
|---|------|-------|------|
| **00** | `00_README.md` | Этот файл — навигация, финальные решения, статусы. | ~700 |
| **01** | `01_CURRENT_STATE_AUDIT.md` | Что есть сейчас: 4 v1-файла, 9 TODO, отсутствие v2-паттерна. | ~1500 |
| **02** | `02_V2_ARCHITECTURE.md` | Namespaces, скриптбл-обжекты, сервер-хаб, DTO, ClientState, persistence. | ~2500 |
| **03** | `03_EDITOR_TOOLING.md` | Quest Database Explorer: UI Toolkit EditorWindow, AssetPostprocessor, UX-скетч, **full CRUD**. | ~1500 |
| **04** | `04_DIALOG_AND_QUEST_UI.md` | UI Toolkit диалог (F skip) + квестовый лог (с Discovered) + tracker + USS-стили + outline. | ~2000 |
| **05** | `05_INPUT_AND_INTERACTION.md` | E-key pipeline, E = dialog, F = boarding+action, **PlayerInputReader full refactor**. | ~1000 |
| **06** | `06_TRIGGERS_AND_INTEGRATION.md` | **Full event bus**, WorldEventBus, IQuestTrigger, интеграция с Inventory/Trade/Ship/DayNight. | ~1500 |
| **07** | `07_DATA_MODEL_EXAMPLES.md` | Примеры SO-ассетов: NpcDefinition, QuestDefinition, DialogTree, FactionDefinition, **EventDriven**. | ~1200 |
| **08** | `08_ROADMAP.md` | **22 тикета** (включая T-X0, T-X3, T-X4, T-X5, T-Q09b), порядок, milestones, риски. | ~3500 |
| **09** | `09_OPEN_QUESTIONS.md` | **17 финальных решений** (приняты 2026-06-07) + детальные спецификации §G-§M. | ~4500 |
| **10** | `10_REFERENCES.md` | Ссылки на существующие v2-референсы, GDD, lore, и pitfall-листы. | ~1300 |

**Суммарно:** ~22 000 слов. Секции самодостаточны — можно читать в любом
порядке, но рекомендую начать с этого README → 09 (финальные решения) → 08 (roadmap) → 02 (архитектура) → остальные по необходимости.

---

## Резюме анализа (TL;DR) + финальные решения

### Текущее состояние (v1) — `01_CURRENT_STATE_AUDIT.md`

- **4 файла, 1440 строк, в `Assets/_Project/Scripts/World/Npc/`:**
  - `NpcData.cs` (241) — `ScriptableObject` с `NpcFaction` (12 значений),
    `DialogueNode[]` массив, `DialogueOption` с TODO-стабами в `IsAvailable()`.
  - `NpcEntity.cs` (352) — `NetworkBehaviour`, wander-AI, НЕТ ни одного RPC,
    НЕТ v2-паттерна. `NpcState` реплицируется через `NetworkVariable`.
  - `NpcInteraction.cs` (213) — `MonoBehaviour : IInteractable`,
    регистрируется в `InteractableManager`, вызывает `NpcEntity.StartDialogue()`.
  - `NpcDialogueManager.cs` (634) — uGUI singleton, TextMeshPro, typewriter
    в `Update()`, опшен-пул из 6 кнопок, **9 TODO-стабов** (give item, give
    rep, trigger event, open trade, open service, play sound, check inventory,
    check rep). `Input.GetKeyDown(KeyCode.Space)` нарушает конвенцию
    `PlayerInputReader`.
- **0 production-вызовов** за пределами папки. 0 NPC в сценах, 0 NPC-префабов,
  0 `NpcData` ассетов. **Система — изолированный код-прототип.**
- **NPC отсутствует в E-key pipeline** `NetworkPlayer.cs:375` — там только
  MetaRequirement → Chest → Pickup → Market. `FindNearestNpc` в
  `InteractableManager.cs:232` написан, но **никем не вызывается**.

### Целевое состояние (v2) — `02_V2_ARCHITECTURE.md`

**Слои архитектуры** (сверху вниз, от UX к данным):

```
┌─────────────────────────────────────────────────────────────────┐
│  Игрок  →  нажал E  →  InteractableManager.FindNearestNpc       │
│           →  QuestInteractor.RequestAdvanceDialogueRpc          │
└─────────────────────────────────────────────────────────────────┘
                  ↓ [Rpc(SendTo.Server, Owner)]
┌─────────────────────────────────────────────────────────────────┐
│  QuestServer (NetworkBehaviour, BootstrapScene, DontDestroy)    │
│  ├─ QuestWorld (POCO) — все state на сервере                   │
│  ├─ ReputationWorld (POCO) — faction reputation                 │
│  ├─ NpcAttitudeWorld (POCO) — per-NPC отношения                │
│  ├─ WorldEventBus (singleton) — все события мира               │
│  ├─ QuestTriggerService — слушает event bus                     │
│  └─ InventoryService.TryRemove + AddItem (расширенный)          │
│           ↓ [Rpc(SendTo.Owner)]                                 │
└─────────────────────────────────────────────────────────────────┘
                  ↓ DTO snapshots (INetworkSerializable)
┌─────────────────────────────────────────────────────────────────┐
│  NetworkPlayer.ReceiveQuestsSnapshotTargetRpc(snapshot)         │
│           ↓                                                     │
│  QuestClientState (singleton)                                  │
│  ├─ OnSnapshotUpdated event                                     │
│  ├─ OnDialogueStep event                                        │
│  └─ OnDiscoveredQuest event (NEW: событийные квесты)            │
│  ReputationClientState, NpcAttitudeClientState                  │
│           ↓                                                     │
│  QuestLogTab (CharacterWindow)  +  DialogWindow (floating)     │
│  + QuestTracker (HUD)  +  QuestDatabaseWindow (editor)         │
└─────────────────────────────────────────────────────────────────┘
```

**Новые namespace'ы:**
- `ProjectC.Factions` — `FactionDefinition : SO`, `FactionId` enum (promoted).
- `ProjectC.Reputation` — `ReputationClientState`, `NpcAttitudeClientState`.
- `ProjectC.Dialogue` — `DialogTree : SO` (graph), `DialogueNode`, `DialogueEdge`,
  `DialogueCondition`, `DialogueAction`.
- `ProjectC.Quests` — server hub, `QuestDefinition : SO`, `QuestStage`,
  `QuestObjective`, `QuestInstance` (POCO).
- `ProjectC.Quests.Dto` — все `INetworkSerializable` DTOs.
- `ProjectC.Quests.Client` — `QuestClientState`.
- `ProjectC.Quests.UI` — `DialogWindow.uxml/uss`, `QuestTracker.uxml/uss`.
- `ProjectC.Quests.Triggers` — `IQuestTrigger`, `QuestTriggerService`, конкретные triggers.
- `ProjectC.Quests.Bridges` — `ContractMetaBridge`.
- `ProjectC.Quests.Persistence` — `IQuestStateRepository`, `JsonQuestStateRepository`.
- `ProjectC.Core` — `WorldEventBus` (singleton), `WorldEvent` base + подтипы.
- `ProjectC.Editor.Quests` — `QuestDatabaseWindow` (UI Toolkit + full CRUD + GraphView).

**Ключевые дизайн-решения (приняты 2026-06-07, см. `09_OPEN_QUESTIONS.md` §G-§M):**

| # | Решение | Источник |
|---|---------|----------|
| 1 | **E = dialog с NPC, F = boarding + future "action" (осмотреть/обокрасть)**, pickup = F (remap в T-X4 после demo) | A1 |
| 2 | **Quest ≠ Contract**, но **bridge** через meta-инфу (квесты читают ContractAccepted/Completed events) | A2 |
| 3 | **Both: per-faction + per-NPC reputation** с cross-faction influence (MVP stub — полная реализация v2) | A3 |
| 4 | **DialogTree top-level SO** + runtime swap через `SwitchDialogTree` action | A4 |
| 5 | **Server JSON, immediate save** на каждый state change, не debounce. Один JSON на игрока = атомарно | A5 |
| 6 | **Full event bus** — все triggers event-driven, хуки во все серверы (Inventory, Trade, Contract, Ship, DayNight) | D2 |
| 7 | **No NetworkList** для квестов (как везде в проекте — RPC+DTO only) | research |
| 8 | **UI Toolkit dialog** как floating window (не таб в CharacterWindow) | research |
| 9 | **Quest Database Explorer: full CRUD** + **GraphView sub-tab** для DialogTree | B1, B2 |
| 10 | **InventoryService.TryRemove (новый)** — turn-in квестов | research |
| 11 | **PlayerInputReader full refactor** — все events, NetworkPlayer подписывается, internal handlers | C3 |
| 12 | **EventDriven квесты** — Discovered state + запись в журнал, player сам решает следовать | E3 |
| 13 | **Gamepad navigation**: UI Toolkit default, позже отдельно | C2 |
| 14 | **NPC gender**: пусто (тот кто пишет квесты знает М/Ж), полная локализация → later TODO | E1 |

---

## Что НЕ делаем (границы)

Согласно AGENTS.md, в этой сессии:
- ❌ Не пишем код (только документация).
- ❌ Не делаем git commit/push.
- ❌ Не модифицируем `docs/gdd/` (game-designer owned).
- ❌ Не создаём `.meta`/`.asmdef` файлы.
- ❌ Не выкатываем новые editor scripts (только проектируем).
- ❌ Не трогаем legacy v1-файлы NPC (помечены как "v1, to be deleted" в `02_V2_ARCHITECTURE.md` §6).

Следующие сессии (по результатам текущей):
- ✅ По каждому тикету в `08_ROADMAP.md` — отдельная сессия с code + MCP-verify.
- ✅ Cleanup v1 NPC — отдельный тикет T-XX, требует grep transitive deps.
- ✅ Renaming `Trade.Core.NPCTrader` → `MarketTrader` — отдельный тикет (опционально).

---

## Ключевые pitfall'ы, найденные в ходе анализа

(Полный список в `02_V2_ARCHITECTURE.md` §7 и `05_INPUT_AND_INTERACTION.md` §3.)

1. **F ≠ interact** в коде (занимает boarding). E = interact (pickup/chest).
2. **NpcDialogueManager НЕ существует** в `Scene` (хотя вызывается из `NpcInteraction.Interact()`) — `FindAnyObjectByType<NpcDialogueManager>()` всегда возвращает null в production.
3. **`PlayerInputReader.OnInteractPressed` event объявлен, но НИКОГДА не подписан** (dead code).
4. **`NetworkPlayer.FindNearestInteractable` НЕ вызывает `FindNearestNpc`** — нужно добавить branch.
5. **2 разные faction-системы**: `NpcFaction` (12 lore values) в `World.Npc` vs. `Faction` (для items) в `Trade.Config`. Конфликт namespace — нужно мигрировать в `ProjectC.Factions`.
6. **2 разные item-системы**: `TradeItemDefinition` (string id, для economy) vs. `ItemData` (int id, для character inventory) — НЕ связаны. Квест должен решить, какой использовать для rewards.
7. **3 разных store'а для предметов игрока**: `InventoryWorld` (32-slot character, не персистится), `Warehouse` (per-location, персистится), `CargoData` (per-ship, персистится).
8. **No `RemoveItem` server-side** — только `TryDrop` (в мир). Нужен новый `TryRemove`.
9. **No event-bus** для inventory changes — клиент подписан на `InventoryClientState.OnSnapshotUpdated` (OK), но на сервере нет server-side event. Trigger-сервис должен poll'ить.
10. **`NpcEntity` (NetworkBehaviour) НЕ имеет scene-placed spawner awareness** — если добавить в BootstrapScene руками, нужно подключить `ScenePlacedObjectSpawner` (как для Inventory/Market/Contract).
11. **Риск NRE**: `NpcInteraction.Interact()` вызывает `NpcDialogueManager.Instance?.StartDialogue(...)` — но `NpcDialogueManager` не auto-spawned, `FindAnyObjectByType` null в 1-в-1 сценах.
12. **AGENTS.md violation**: `Input.GetKeyDown(KeyCode.Space)` в `NpcDialogueManager.cs:163` — legacy input.

---

## Что нужно от тебя перед стартом T-Q01

**Все 17 вопросов решены 2026-06-07.** Детали в `09_OPEN_QUESTIONS.md` §G-§M.

**Если что-то не так в финальных решениях** — поправь, пересмотрю.
**Если OK** — стартую T-Q01 в следующей сессии:
- Создать `Assets/_Project/Quests/` папку.
- Скопировать `NpcFaction` enum в `ProjectC/Factions/FactionId.cs` (с `[Obsolete]` alias).
- Создать `NpcAttitude` struct рядом (per-NPC reputation slot).
- 1 PR, ~30 мин, compile-clean verify.
