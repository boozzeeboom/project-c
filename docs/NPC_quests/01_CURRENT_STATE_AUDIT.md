# 01 — Текущее состояние NPC-подсистемы (v1 audit)

> Источник: subagent report `C:\Users\leon7\ANALYSIS_NPC_SUBSYSTEM.md` (полная версия, ~3000 слов).
> Подтверждено путём чтения всех 4 файлов, 10 интеграционных файлов, поиска по `Assets/_Project/`.

---

## 1.1 Что есть (inventory)

4 файла в `Assets/_Project/Scripts/World/Npc/`, namespace `ProjectC.World.Npc`:

| Файл | LOC | Тип | Назначение |
|---|---|---|---|
| `NpcData.cs` | 241 | `ScriptableObject` + 2 enum + 2 `[Serializable]` класса | Данные NPC: id, name, faction (12 значений), portrait, prefab, `DialogueNode[]` (массив), services flags, greeting text. |
| `NpcEntity.cs` | 352 | `NetworkBehaviour` | "Мозг" NPC: `NpcState` enum, `NetworkVariable<NpcState>`, wander FSM (runs on all peers!), animator triggers (magic strings "Idle"/"Walk"/"Talk"), `StartDialogue()` → `NpcDialogueManager.Instance?.StartDialogue(...)`. **0 RPC методов.** |
| `NpcInteraction.cs` | 213 | `MonoBehaviour : IInteractable` | "Trigger collider"-сторона: регистрируется в `InteractableManager.RegisterNpc` (line 61, 67, 73, 79), владеет `Interact()` (line 85) → `NpcEntity.StartDialogue()` или fallback `NpcDialogueManager.Instance?.StartDialogue(...)` (line 112). |
| `NpcDialogueManager.cs` | 634 | `MonoBehaviour` singleton (uGUI) | Singleton `Instance` через `FindAnyObjectByType` (line 22-36) — **fragile**, `Awake` self-destruct on duplicate, опшен-пул из 6 кнопок, typewriter в `Update()`, **9 TODO-стабов** (give item, give rep, trigger event, open trade, open service, play sound, check inventory, check rep). `Input.GetKeyDown(KeyCode.Space)` — **AGENTS.md violation** (line 163). |

**Суммарно: 1440 LOC, 9 TODO-комментариев, 0 production-вызовов.**

---

## 1.2 Что реализовано vs что стаб

### Реализовано (работает):
- ✅ `NpcData` как SO с `[CreateAssetMenu]` ("Project C/NPC Data", order 100).
- ✅ `NpcEntity.StartDialogue()` — корректно вызывает `NpcDialogueManager.Instance?.StartDialogue(...)`.
- ✅ `NpcInteraction.Interact()` — корректно вызывает `NpcEntity.StartDialogue()` или fallback.
- ✅ `NpcDialogueManager.StartDialogue()` — открывает panel, typewriter, options.
- ✅ `DialogueNode.text` + `DialogueOption[]` — линейная навигация по `nextNodeId`.
- ✅ Typewriter effect (`Update()` tick).
- ✅ NetworkVariable для `NpcState` (server-authoritative).
- ✅ `IInteractable` интерфейс с 4 property.
- ✅ `InteractableManager.RegisterNpc/UnregisterNpc/FindNearestNpc`.

### Стаб (TODO-комментарии, цитаты):

**`NpcData.cs:68-86` — `IsAvailable()`:**
```csharp
// TODO: Check inventory for requiredItemId
// TODO: Check reputation
if (!string.IsNullOrEmpty(requiredItemId))
{
    // Placeholder: In real implementation, check player inventory
    return true;
}
if (requiredReputation > 0)
{
    // Placeholder: In real implementation, check faction reputation
    return true;
}
return true;
```
**Нет ни одного path, возвращающего `false`**. Каждый опшен всегда "available".

**`NpcDialogueManager.cs:532-580` — `ProcessNodeEffects()` (5 TODO):**
```csharp
// TODO: Add to player inventory        (giveItemId branch, line 538)
// TODO: Add to faction reputation system (reputationGain, line 545)
// TODO: Trigger UnityEvent or send notification (triggerEvent, line 552)
// TODO: Open trade interface             (DialogueNodeType.Trade, line 568)
// TODO: Open service UI                  (DialogueNodeType.Service, line 573)
```

**`NpcDialogueManager.cs:582-597` — `ProcessOptionEffects()` (2 TODO):**
```csharp
// TODO: Add to player inventory (rewardItemId, line 588)
// TODO: Play audio cue          (soundCue, line 595)
```

**`NpcInteraction.cs:128` — `ShowGreeting()`:**
```csharp
// TODO: Show floating text or notification
Debug.Log($"[NpcInteraction] {DisplayName} says: \"{npcData.greetingText}\"");
```

**`NpcInteraction.cs:167` — `CanInteract()`:**
```csharp
// TODO: Add checks for quest state, reputation, etc.
return npcData != null;
```

**Итого: 9 TODO, все — `Debug.Log` стабы, ни одна сайд-эффект не работает.**

---

## 1.3 Touchpoints (кто использует NPC-систему)

**Production-вызовы v1 NPC-типов (поиск по `Assets/_Project/`):**

| Ссылка | Где | Назначение |
|---|---|---|
| `NpcData` | (никто, кроме самих NPC-файлов) | 0 callers |
| `NpcEntity` | (никто, кроме самих NPC-файлов) | 0 callers |
| `NpcInteraction` | `Core/InteractableManager.cs:90, 101, 127, 127, 232, 239` | registry API |
| `NpcDialogueManager` | (никто, кроме `NpcEntity.cs:258` и `NpcInteraction.cs:112`) | 0 external callers |
| `RegisterNpc` | только `NpcInteraction.cs:61, 67, 73, 79` | self-registration |
| `FindNearestNpc` | **никто** | dead code, нет callers |

**Сцены:** 0 NPC GameObjects в `BootstrapScene.unity` или в любом из 24 `WorldScene_*.unity`.

**Префабы:** 0 NPC-префабов (всего 13 префабов, none — NPC).

**Ассеты:** 0 `NpcData` ассетов ни в `Assets/_Project/Data/`, ни в `Assets/_Project/ScriptableObjects/`, ни в `Assets/_Project/Resources/`.

**Вывод:** вся v1 NPC-подсистема — это **изолированный код-прототип**, не подключённый ни к чему. Чистый лист для v2.

---

## 1.4 Архитектурные проблемы v1

| # | Категория | Проблема | Файл:строка |
|---|-----------|----------|-------------|
| 1 | Network/sync | `NpcEntity` — `NetworkBehaviour` без RPC, без `ScenePlacedObjectSpawner` awareness. Если добавить в сцену руками — NRE. | `NpcEntity.cs:14` |
| 2 | Network/sync | Wander FSM в `Update()` (line 122-151) **выполняется на всех peers** (нет `if (!IsServer) return`), `Random.Range` десинхронизирует сервер/клиенты. | `NpcEntity.cs:122-151` |
| 3 | Singleton | `FindAnyObjectByType<NpcDialogueManager>()` в getter — null в stream-сценах, где singleton выгружен. | `NpcDialogueManager.cs:22-36` |
| 4 | Singleton | Нет `DontDestroyOnLoad` — не переживает загрузку 24 WorldScenes. | `NpcDialogueManager.cs:119-152` |
| 5 | Data model | `DialogueNode[]` массив — линейный, **нет графа, нет переменных, нет условий на quest state**. | `NpcData.cs:166` |
| 6 | Data model | `nextNodeId` — stringly-typed; typo → silent end-of-dialogue. | `NpcData.cs:48`, `NpcDialogueManager.cs:286-291` |
| 7 | Data model | `reputationGain` — one-shot int, не persistent state. | `NpcData.cs:128` |
| 8 | Data model | Нет локализации (тексты хардкод RU/EN). | `NpcData.cs:104` |
| 9 | Faction | `NpcFaction` enum (12 значений) **нигде не хранится как состояние** — только как enum-тег. Reputation values отсутствуют. | `NpcData.cs:9-23` |
| 10 | Contract overlap | `DialogueNode.contractId` ссылается на `Trade.ContractData` (delivery board) — но **нет glue**: `NpcDialogueManager.cs:559` просто `Debug.Log`, не вызывает `ContractServer.RequestAcceptRpc`. | `NpcDialogueManager.cs:559` |
| 11 | Quest state | **0 quest-related типов в проекте**. Нет `Quest`, `QuestStage`, `QuestInstance`, `QuestObjective`, `ObjectiveProgress`. | (search verified) |
| 12 | Trigger system | Нет триггеров (item, ship, location, time, event). | — |
| 13 | Persistence | **0 server-side world state** для квестов/репутации. | — |
| 14 | UI framework | uGUI/TextMeshPro, конфликтует с UI Toolkit migration (CharacterWindow, MarketWindow). | `NpcDialogueManager.cs:3,4,44-66` |
| 15 | Input | `Input.GetKeyDown(KeyCode.Space)` нарушает AGENTS.md "use PlayerInputReader". | `NpcDialogueManager.cs:163` |
| 16 | Animator | Magic strings "Idle"/"Walk"/"Talk" — non-configurable per NPC. | `NpcEntity.cs:275-291` |
| 17 | Code quality | Misleading comment: "uses ServerRpc/ClientRpc for multi-player sync" — но RPC нет вообще. | `NpcEntity.cs:12` |

---

## 1.5 Что переиспользовать в v2

| Сохранить | Почему |
|---|---|
| `NpcFaction` enum (12 lore values) | Lore-канонично; уже используется `TradeDatabase.GetItemsByFaction`, `ContractData.Create` formula. Промоутировать в `ProjectC.Factions.FactionId` (top-level). |
| `DialogueNode` concept (text + options[]) | Базовый паттерн правильный, но v2 = graph, не flat array. |
| Typewriter effect (charsToShow = floor(timer * speed)) | UX-логика правильная, переписать как coroutine на `IVisualElementScheduledItem` (UI Toolkit). |
| Skip-on-Space/Click UX | Хороший паттерн, переписать на `PlayerInputReader` events. |
| `NetworkVariable<NpcState>` pattern | Server-authoritative, replicated — расширить на `displayName` + `FactionId` для client-side rendering без asset round-trip. |
| `IInteractable` interface | Минимальный, правильный. Добавить `void Interact()` method? (сейчас нет, каждый реализует своё). |
| `InteractableManager.RegisterNpc/FindNearestNpc` | Registry pattern, zero allocations в hot path. Просто `FindNearestNpc` не вызывается из `NetworkPlayer` E-pipeline. |
| Split `NpcEntity` (server) + `NpcInteraction` (client) | Правильное разделение ответственности. v2 сохранить. |
| `Auto-add SphereCollider` pattern | Дублируется в `NpcInteraction.EnsureCollider` (line 179-198) и `NpcEntity.EnsureCollider` (line 294-308). Consolidate. |
| `InstanceId` pattern (string с GetHashCode) | Уникальный ID для interactable registry. OK. |

---

## 1.6 Что удалить / переписать

| Удалить/переписать | Почему |
|---|---|
| `NpcDialogueManager.cs` целиком (634 строки) | uGUI, fragile singleton, all TODO-стабы, Input.GetKeyDown. |
| `DialogueNode[]` flat-array model | Заменить на directed graph (`DialogTree.nodes[]` + `DialogueEdge[]`). |
| `DialogueOption.rewardItemId/contractId/soundCue/triggerEvent/reputationGain` | Side-effect fields, должны быть на `DialogueAction`, не на Option. |
| `reputationGain` на `DialogueNode` | Delete — reputation = server-side replicated state, не `Debug.Log`. |
| 9 TODO-комментариев | Заменяются реальной v2-логикой. |
| `showGreeting` / `greetingText` / `ShowGreeting()` | Debug.Log stub → UI Toolkit floating tooltip на HUD. |
| `animator.ResetTrigger("Idle"/"Walk"/"Talk")` magic strings | AnimatorConfig SO per NPC (or shared). |
| `NpcData.prefab` (line 159) | Дубль — prefab ref должен быть на `NpcEntity` component или registry asset, не на data. |

---

## 1.7 Naming conflict с Trade

`ProjectC.Trade.Core.NPCTrader` (file: `Assets/_Project/Trade/Scripts/Core/NPCTrader.cs`) — **полностью unrelated** класс:
- `[Serializable]`, plain POCO, не `MonoBehaviour`.
- Server-side economic actor: moves cargo между markets на tick (line 62-93).
- Никак не связан с dialogue.

**Конфликт:** слово "NPC" используется в двух несвязанных подсистемах. Решение v2:
- **Рекомендация:** rename `Trade.Core.NPCTrader` → `MarketTrader` (entity = market actor, не "real NPC").
- Если не rename: документировать overlap явно в v2 namespace = `ProjectC.World.Npc` для диалогов, `ProjectC.Trade.Core.NPCTrader` — отдельный.

**См. вопрос #6 в `09_OPEN_QUESTIONS.md`.**

---

## 1.8 Связь с другими v2-системами

| Система | Файл | Состояние | v2 NPC-Quest integration |
|---|---|---|---|
| **Market** | `Trade/Scripts/Network/MarketServer.cs` | ✅ working (522 LOC) | Reference: server hub pattern. |
| **Contract** | `Trade/Scripts/Network/ContractServer.cs` | ✅ working (412 LOC) | Reference: DTO + ClientState + persistence. **Not unified with Quest** (см. §1.9). |
| **Inventory** | `Items/Network/InventoryServer.cs` + `Items/InventoryWorld.cs` | ✅ working (305 + 445 LOC) | Integration: quest rewards give items via `InventoryServer.AddItem(clientId, intItemId, type)`. **Gap:** no `RemoveItem` (only `TryDrop`). |
| **MetaRequirement** | `Scripts/MetaRequirement/MetaRequirementRegistry.cs` (231 LOC) | ✅ working (quest-LIKE pattern) | Reference: per-entity state hub. Gap: no per-quest state, no progress, no stages. |
| **CharacterWindow** | `UI/Client/CharacterWindow.cs` (1345 LOC) | ✅ working (5 tabs) | Add "Квесты" таб. Reputation таб уже есть (line 80, 89, 393-398), но `_reputationCache` (line 507) — пустой, ждёт `ReputationClientState`. |

---

## 1.9 Quest vs Contract — design decision

**Текущее состояние:** "Quest" в коде = `contractId` строка на `DialogueNode`. `NpcDialogueManager.cs:559` Debug.Log вместо реального вызова.

**Аргументы за объединение** (одна система):
- Оба: "go do X for Y, get reward Z".
- Переиспользование timer/accept/complete flow → -50% работы.
- UI один (Market board + NPC dialog → один список).

**Аргументы за разделение** (две системы):
- **Contract** — 24/7 generated, market board, no narrative, no reputation gate, no stages, simple Pending/Active/Completed/Failed state, timer-based, debt system.
- **Quest** — one-shot, gated by reputation/quest progression, branching stages, side-effects (give item, give rep, complete objective, emit event), narrative arc.

**Рекомендация v2:** **разделить, с тонкой bridge'ей.**
- `QuestDefinition : SO` — independent system.
- `DialogueAction.OfferQuest(questId)` — server-side вызывает `QuestServer.TryOffer(playerId, questId)`.
- `DialogueAction.CompleteObjective(questId, objectiveId)` — same.
- `DialogueAction.OfferContract(contractId)` — для единичных кейсов, когда NPC даёт именно contract (например, торговец просит "доставь X в Y" — это по форме contract, не quest). Route в `ContractServer.TryAccept(playerId, contractId)`.

**См. вопрос #2 в `09_OPEN_QUESTIONS.md`.**

---

## 1.10 Граф зависимостей v1

```
NpcData.cs       ← (nothing reads it)
NpcEntity.cs     → calls NpcDialogueManager.Instance?.StartDialogue (NpcEntity.cs:258)
NpcInteraction.cs → calls InteractableManager.RegisterNpc/UnregisterNpc (line 61, 67, 73, 79)
                  → calls NpcEntity.StartDialogue (line 107)
                  → calls NpcDialogueManager.Instance?.StartDialogue (line 112)
NpcDialogueManager.cs ← (nothing calls StartDialogue except NpcEntity and NpcInteraction)
Core/InteractableManager.cs ← (nothing calls FindNearestNpc in production)
```

**Net:** NPC subsystem = **leaf node в проекте**. Zero production callers. Clean slate для v2.
