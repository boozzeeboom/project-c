# 08 — Roadmap: тикеты, порядок, риски

> **Цель:** распланировать имплементацию по сессиям, чтобы каждая сессия
> давала compile-clean, верифицируемый incremental progress. Mavis
> (помощник) делает code, юзер тестит и коммитит.
>
> **Обновлено 2026-06-07:** учтены ответы пользователя на `09_OPEN_QUESTIONS.md` (A1, A2, A3, A5, B1, B2, C1, C3, D1, D2, E2, E3). Детальные спецификации решений — в `09_OPEN_QUESTIONS.md` §G-§M.

---

## 8.1 Принципы разбивки

- **Один тикет = один PR = одна сессия = ~30-120 мин кодинга** (по объёму).
- **Каждый тикет компилируется и не ломает существующее.**
- **Тестирование — пользователь** (юзер запускает Unity, проверяет в Editor/PlayMode).
- **Без `.meta`/`.asmdef` writes** (см. AGENTS.md HARD RULES).
- **Cleanup-тикеты отдельно от feature-тикетов** (не смешивать).
- **Pitfall-листы:**
  - `02_V2_ARCHITECTURE.md` §7 — server/DTO/persistence pitfalls.
  - `04_DIALOG_AND_QUEST_UI.md` §4.8 — UI Toolkit pitfalls.
  - `05_INPUT_AND_INTERACTION.md` §5.9 — input/interaction pitfalls.
  - `06_TRIGGERS_AND_INTEGRATION.md` §6.8 — trigger/integration pitfalls.

---

## 8.2 Финальный порядок тикетов (с зависимостями)

```
T-Q01 (namespaces + FactionId + NpcAttitude)
   ↓
T-Q02 (NpcDefinition : SO + FactionDefinition : SO)
   ↓
T-Q03 (DialogTree : SO + DialogueNode/Edge/Cond/Action)
   ↓
T-Q04 (QuestDefinition : SO + EventDriven objective + Discovered state)
   ↓
T-Q05 (QuestServer skeleton + QuestWorld POCO)
   ↓
T-X0 (InventoryWorld persistence + WorldEventBus hooks)  ← НОВЫЙ, обязательный
   ↓
T-Q06 (WorldEventBus + QuestTriggerService + базовые trigger'ы)  ← расширен
   ↓
T-Q07 (QuestClientState + ReputationClientState + NpcAttitudeClientState + DTOs)
   ↓
T-X3 (PlayerInputReader full refactor: events + NetworkPlayer subscribe)  ← перемещён
   ↓
T-Q08 (QuestInteractor + E-key NPC branch)
   ↓
T-Q09 (QuestDatabaseWindow — UI Toolkit EditorWindow, FULL CRUD)  ← расширен
   ↓
T-Q09b (GraphView sub-tab для DialogTree)  ← НОВЫЙ
   ↓
T-Q10 (DialogWindow.uxml/uss + code-behind, F skip typewriter)
   ↓
T-Q11 (Quest log таб в CharacterWindow + Discovered section)
   ↓
T-Q12 (QuestTracker.uxml/uss)
   ↓
T-Q13 (ReputationClientState + NpcAttitudeClientState + CharacterWindow tab fix)  ← расширен
   ↓
T-Q14 (InventoryServer.TryRemove + event hooks)
   ↓
T-X5 (ContractServer publish events для quest bridge)  ← НОВЫЙ
   ↓
T-Q15 (DialogueAction: GiveItem/TakeItem + ContractMetaBridge)
   ↓
T-Q16 (DialogueAction: GiveCredits/AddReputation/AddNpcAttitude)
   ↓
T-Q17 (DialogueAction: OpenMarket/OpenService)
   ↓
T-Q18 (Persistence: IQuestStateRepository + JSON, immediate save)
   ↓
T-Q19 (C1 cleanup: delete v1 NPC)
   ↓
T-X1 (Trade.Core.NPCTrader rename → MarketTrader) ← optional
   ↓
T-X2 (TradeItemDefinition Faction → FactionId migration) ← optional
   ↓
T-X4 (input remap: pickup E → F) ← future TODO, после end-to-end demo
```

**Сводка по новым тикетам:**
| Тикет | Где в порядке | Обязательность |
|-------|---------------|----------------|
| **T-X0** | между T-Q05 и T-Q06 | **Обязательный** (D1) |
| **T-Q09b** | после T-Q09 | **Обязательный** (B2) |
| **T-X5** | после T-Q14 | **Обязательный** (A2) |
| **T-X3** | между T-Q07 и T-Q08 (перемещён) | **Обязательный** (C3) |
| **T-X4** | в конец, после T-Q19 | Future TODO (A1) |

**Сводка по расширениям:**
| Тикет | Расширение | Источник |
|-------|-----------|----------|
| T-Q01 | + NpcAttitude struct (per-NPC rep слот) | A3 |
| T-Q04 | + EventDriven objective + Discovered state | E3 |
| T-Q06 | + WorldEventBus singleton + хуки во все серверы | D2 |
| T-Q07 | + ReputationClientState + NpcAttitudeClientState | A3 |
| T-Q08 | + NPC branch в E-pipeline (после T-X3) | A1 |
| T-Q09 | + full CRUD (create/edit/delete/duplicate) | B1 |
| T-Q10 | + F skip typewriter (вместо Space) + click | C1 |
| T-Q11 | + Discovered раздел + "Accept" кнопка | E3 |
| T-Q13 | + NpcAttitudeClientState + rep tab fix | A3 |
| T-Q14 | + event hooks (publish ItemAdded/Removed) | D2 |
| T-Q15 | + ContractMetaBridge | A2 |
| T-Q16 | + AddNpcAttitude action | A3 |
| T-Q18 | + immediate save + NpcAttitude persistence | A5, A3 |

---

## 8.3 Тикеты (детально)

### T-Q01 — Namespaces + FactionId promotion + NpcAttitude (small, 30 мин)

**Скоуп:**
- Создать папку `Assets/_Project/Quests/`.
- Создать `ProjectC.Factions` namespace.
- Скопировать `NpcFaction` enum из `World/Npc/NpcData.cs:9-23` в новый файл `ProjectC/Factions/FactionId.cs`.
- Переименовать в `FactionId`.
- **НЕ** удалять `NpcFaction` пока (для backward compat) — `[Obsolete]` attribute + comment.
- **Добавить `NpcAttitude` struct** рядом с `FactionId` (см. `09_OPEN_QUESTIONS.md` §G):
  ```csharp
  public readonly struct NpcAttitude : IEquatable<NpcAttitude>
  {
      public readonly string NpcId;
      public readonly int Value;  // -100..+200
      public NpcAttitude(string npcId, int value) { ... }
      public bool Equals(NpcAttitude other) => NpcId == other.NpcId && Value == other.Value;
      public override int GetHashCode() => HashCode.Combine(NpcId, Value);
  }
  ```

**Verify:** Unity Editor → console 0 errors.

**Risk:** low. Чисто rename + namespace promotion + struct.

---

### T-Q02 — NpcDefinition + FactionDefinition SO (medium, 60 мин)

**Скоуп:**
- `ProjectC/Factions/FactionDefinition.cs` (ScriptableObject).
- `ProjectC/Quests/NpcDefinition.cs` (ScriptableObject).
- Поля согласно `02_V2_ARCHITECTURE.md` §2.3.1, §2.3.3.
- **+ поле `attitudeLinks[]`** в `NpcDefinition` для cross-faction influence (см. `09_OPEN_QUESTIONS.md` §G, MVP stub):
  ```csharp
  [Serializable]
  public class AttitudeLink {
      public FactionId targetFaction;
      public int deltaOnLike;      // +N when NPC relationship improves
      public int deltaOnDislike;   // -N when NPC relationship worsens
  }
  public AttitudeLink[] attitudeLinks = Array.Empty<AttitudeLink>();
  ```
- 1 test asset `GuildOfThoughts.asset` + `Mira.asset` (см. `07_DATA_MODEL_EXAMPLES.md` §7.1, §7.4).

**Verify:** Mira.asset редактируется в Inspector, attitudeLinks виден.

**Risk:** low. Data layer.

---

### T-Q03 — DialogTree + DialogueNode/Edge/Condition/Action (large, 120 мин)

**Скоуп:**
- `ProjectC/Dialogue/DialogTree.cs` (SO).
- `ProjectC/Dialogue/DialogueNode.cs` (POCO).
- `ProjectC/Dialogue/DialogueEdge.cs`.
- `ProjectC/Dialogue/DialogueCondition.cs` (composite + atomic).
- `ProjectC/Dialogue/DialogueAction.cs`.
- 1 test asset `MiraDefault.asset` (по `07_DATA_MODEL_EXAMPLES.md` §7.3).

**Verify:** Open MiraDefault.asset → Inspector показывает nodes tree + edges.

**Risk:** medium. Composite pattern (And/Or/Not) легко ошибиться.

---

### T-Q04 — QuestDefinition + QuestStage/Objective + EventDriven (large, 120 мин)

**Скоуп:**
- `ProjectC/Quests/QuestDefinition.cs` (SO).
- `ProjectC/Quests/QuestStage.cs` (POCO).
- `ProjectC/Quests/QuestObjective.cs` (POCO).
- `ProjectC/Quests/QuestReward.cs`.
- `ProjectC/Quests/QuestPrerequisite.cs`.
- `ProjectC/Quests/QuestState.cs` enum — **+ `Discovered = 0`** (см. `09_OPEN_QUESTIONS.md` §K).
- `ProjectC/Quests/QuestObjectiveType.cs` enum — **+ `EventDriven = 7`** (см. `09_OPEN_QUESTIONS.md` §K).
- `ProjectC/Quests/QuestStateTransition.cs` (allowed transitions: Discovered→Active, Offered→Active, Active→Completed/Failed, Completed→TurnedIn).
- 1 test asset `FindArtifact.asset` + 1 test `EventDrivenQuest.asset` с objective EventDriven.

**Verify:** FindArtifact.asset → 5 stages. EventDrivenQuest.asset → 1 stage, EventDriven objective.

**Risk:** medium. EventDriven trigger взаимодействует с WorldEventBus (ещё не существует, но поле можно создать заранее).

---

### T-Q05 — QuestServer + QuestWorld (large, 150 мин)

**Скоуп:**
- `ProjectC/Quests/Network/QuestServer.cs` (NetworkBehaviour).
- `ProjectC/Quests/Core/QuestWorld.cs` (POCO singleton).
- `ProjectC/Quests/Core/QuestInstance.cs` (POCO runtime state с полем `state`).
- Place `QuestServer` GameObject в `BootstrapScene.unity`.
- Wire `ScenePlacedObjectSpawner` (per AGENTS.md).
- Rate limiting.
- All RPCs declared (RequestTalkToNpc, RequestAdvanceDialogue, RequestAcceptQuest, RequestTurnInQuest, RequestTrackQuest, RequestRefreshQuests, RequestRefreshReputation, RequestRefreshNpcAttitude, RequestDiscoverQuest).
- `OnNetworkSpawn` initializes `QuestWorld.Instance`.
- `OnNetworkDespawn` flushes all player state to disk.

**Verify:**
- QuestServer GameObject в BootstrapScene, NetworkObject component, IsSpawned.
- Play Mode → no NRE.
- Console: `[QuestServer] OnNetworkSpawn - IsServer=...`.

**Risk:** high. NetworkBehaviour + scene placement = NRE risk. MUST use ScenePlacedObjectSpawner.

**Pitfalls:** AGENTS.md §Scene architecture + `02_V2_ARCHITECTURE.md` §7.

---

### T-X0 — InventoryWorld persistence + WorldEventBus hooks (medium, 90 мин) — НОВЫЙ, ОБЯЗАТЕЛЬНЫЙ

**Скоуп (см. `09_OPEN_QUESTIONS.md` §D1 + §J):**
- Создать `ProjectC.Core.WorldEventBus` (static singleton с `Publish<T>` / `Subscribe<T>` / `Reset()`).
- Создать `ProjectC.Core.WorldEvent` base + `ItemAddedEvent`, `ItemRemovedEvent`, `ReputationChangedEvent`, `QuestStateChangedEvent`, `CustomEvent`.
- `Items/InventoryWorld.cs`: добавить `IInventoryRepository` interface + `JsonInventoryRepository` (default).
- Save на каждый `AddItemDirect` и `TryRemove`.
- Load on player connect (если есть persisted state — restore).
- После Add/Remove → publish `ItemAddedEvent` / `ItemRemovedEvent` через WorldEventBus.
- `Application.persistentDataPath/inventory_<clientId>.json`.

**Verify:**
- Add item, kill server, restart, reconnect → items still there.
- Add item → console log shows `[WorldEventBus] Published ItemAddedEvent`.
- Subscribe test (debug button в editor) — receives event.

**Risk:** medium-high. Модифицирует stable Items subsystem. **Обязательно grep transitive deps** (см. `09_OPEN_QUESTIONS.md` §D1 — "внимательно").

**Pitfalls:**
- НЕ сломать существующий `TryDrop` / `TryPickup` flow.
- JSON serialization вложенных ItemData — version migration.
- File I/O race conditions при concurrent saves (use lock per clientId).

---

### T-Q06 — WorldEventBus + QuestTriggerService + 5+ trigger'ов (large, 150 мин) — РАСШИРЕН

**Скоуп (см. `09_OPEN_QUESTIONS.md` §J):**
- `ProjectC.Quests.Triggers.IQuestTrigger` (interface).
- `ProjectC.Quests.Triggers.QuestTriggerService` (server-side singleton, подписывается на WorldEventBus).
- Concrete triggers:
  - `TalkedToNpcTrigger` (event-bus: `NpcTalkedEvent`).
  - `HaveItemTrigger` (event-bus: `ItemAddedEvent`, `ItemRemovedEvent`).
  - `CargoHasItemTrigger` (event-bus: `CargoAddedEvent`, `CargoRemovedEvent`).
  - `ReputationAtLeastTrigger` (event-bus: `ReputationChangedEvent`).
  - `NpcAttitudeAtLeastTrigger` (event-bus: `NpcAttitudeChangedEvent`).
  - `LocationReachedTrigger` (poll каждые 5 сек).
  - `DayNightPhaseTrigger` (event-bus: `DayNightPhaseChangedEvent`).
  - `EventTrigger` (event-bus: `CustomEvent`).
  - `KilledEntityTrigger` (stub, TODO когда combat).
- All triggers **event-driven** (full bus, не polling).
- **Hooks в существующих серверах** (см. таблицу в `09_OPEN_QUESTIONS.md` §J):
  - `DayNightController` → publish `DayNightPhaseChangedEvent`.
  - (Market, Contract — позже в T-X5).

**Verify:**
- Subscribe в `QuestWorld.OnNetworkSpawn` → `QuestTriggerService.Instance.SubscribeToAll(...)`.
- Trigger test (debug editor button) — publishes event, trigger fires, quest advances.
- Console log: `[QuestTriggerService] Evaluated TalkedToNpcTrigger for questId=find_artifact`.

**Risk:** medium. Event-bus = cross-cutting, много подписок. **Test isolation** — static singleton with `Reset()`.

---

### T-Q07 — Client states + DTOs (large, 180 мин) — РАСШИРЕН

**Скоуп:**
- DTOs: `QuestDto`, `QuestObjectiveDto`, `QuestSnapshotDto`, `QuestResultDto`, `QuestResultCode`, `DialogueStepDto`, `DialogueOptionDto`.
- **+ `ReputationDto`, `ReputationEntryDto`, `ReputationSnapshotDto`** (см. `09_OPEN_QUESTIONS.md` §G).
- **+ `NpcAttitudeDto`, `NpcAttitudeSnapshotDto`**.
- **+ `DiscoveredQuestDto`** (для событийных квестов).
- Client state projections:
  - `QuestClientState` (singleton, OnSnapshotUpdated, OnDialogueStep, OnQuestResult, OnDiscoveredQuest).
  - `ReputationClientState` (singleton, OnReputationUpdated).
  - `NpcAttitudeClientState` (singleton, OnNpcAttitudeUpdated).
- `NetworkPlayer.ReceiveXxxTargetRpc` методы для всех snapshot типов.
- Server → call `target.ReceiveXxxTargetRpc(snapshot)` после операций.
- Auto-spawn всех ClientState в `NetworkManagerController.Awake`.
- **Hand-rolled IsWriter/IsReader branches** для nullable DTOs (per `ContractResultDto.cs:60-90`).

**Verify:**
- All ClientState Instance != null в Play Mode.
- QuestServer → call SendSnapshot → ClientState.OnSnapshotUpdated fires.
- All DTOs round-trip serialize/deserialize (EditMode test).

**Risk:** high. DTOs + INetworkSerializable + nullable workaround = error-prone.

**Pitfalls:** `02_V2_ARCHITECTURE.md` §2.5 + §7.

---

### T-X3 — PlayerInputReader full refactor (medium, 90 мин) — ПЕРЕМЕЩЁН, РАСШИРЕН

**Скоуп (см. `09_OPEN_QUESTIONS.md` §M):**
1. `PlayerInputReader.cs`:
   - Добавить `public static PlayerInputReader Instance { get; private set; }` + `Awake` setter.
   - Все events reliable: `OnMoveInput`, `OnJumpPressed`, `OnRunPressed/Released`, `OnInteractPressed` (E), `OnModeSwitchPressed` (F), `OnPausePressed` (Esc), `OnMouseDelta`.
2. `NetworkPlayer.Awake`: подписаться на все events, internal handlers `_OnEKeyPressed`, `_OnFKeyPressed`, etc.
3. Удалить direct `Keyboard.current.*Key.wasPressedThisFrame` polling из `NetworkPlayer.Update`.
4. `PlayerStateMachine.Awake`: подписаться на `OnModeSwitchPressed` (F).
5. Grep transitive deps — все подписки на input должны идти через `PlayerInputReader.Instance`.

**Verify:**
- Play Mode → все input events work как раньше (WASD move, Space jump, F board, E pickup, Esc close).
- Console: `[PlayerInputReader] OnInteractPressed fired for clientId=...`.

**Risk:** medium. Рефактор input pipeline — много подписок. **Тщательно grep** всех `Keyboard.current` usages.

---

### T-Q08 — QuestInteractor + E-key NPC branch (small, 45 мин) — РАСШИРЕН

**Скоуп (см. `09_OPEN_QUESTIONS.md` §L):**
- `ProjectC/Quests/Interactions/QuestInteractor.cs` (MonoBehaviour).
- **Добавить NPC branch в начало E-pipeline** в `NetworkPlayer.cs:375`:
  ```csharp
  // 0. NPC (highest priority)
  if (QuestInteractor.Instance != null && QuestInteractor.Instance.TryTalkToNpc()) return;
  // 1. (existing) MetaRequirement / chest / pickup / market
  ```
- Auto-spawn `QuestInteractor` в `NetworkManagerController.Awake`.
- Использует `PlayerInputReader.Instance?.OnInteractPressed` (после T-X3).

**Verify:**
- Place NPC prefab в WorldScene_0_0 (тестовый).
- Play Mode → press E near NPC → console log `[QuestInteractor] TryTalkToNpc - npcId=...`.
- E-handler падает through к pickup/chest если NPC нет.

**Risk:** low. Wire-up only (server response stubbed до T-Q10).

---

### T-Q09 — QuestDatabaseWindow (Editor tool, FULL CRUD) (large, 180 мин) — РАСШИРЕН

**Скоуп (см. `09_OPEN_QUESTIONS.md` §I):**
- `Assets/_Project/Editor/Quests/QuestDatabaseWindow.cs` (UI Toolkit EditorWindow).
- `QuestDatabaseWindow.uxml`, `.uss`.
- `QuestIndexBuilder.cs` (reverse-index cache).
- `QuestAssetWatcher.cs` (AssetPostprocessor).
- **Full CRUD** (см. детали в `09_OPEN_QUESTIONS.md` §I):
  - ✅ Просмотр (TreeView, MultiColumnListView, search, filters).
  - ✅ **Создание** новых SO-ассетов через toolbar buttons.
  - ✅ **Редактирование** через inline `PropertyField`.
  - ✅ **Удаление** с modal confirmation.
  - ✅ **Drag-and-drop** linking.
  - ✅ **Дублирование** через context menu.
  - ✅ **Real-time validation** с inline badges.
- 3-pane layout per `03_EDITOR_TOOLING.md` §3.3.

**Verify:**
- `Window → Project C → Quests → Database Explorer` → window opens.
- Toolbar buttons: "+ NPC", "+ Quest", "+ Dialog" → создают ассеты.
- Click row → inline edit поля.
- Delete → modal confirm.
- AssetPostprocessor: rename .asset в Project window → cache invalidates.

**Risk:** medium. UI Toolkit в editor — new territory. Full CRUD = много кода, но tractable.

---

### T-Q09b — GraphView sub-tab для DialogTree (large, 150 мин) — НОВЫЙ, ОБЯЗАТЕЛЬНЫЙ

**Скоуп (см. `09_OPEN_QUESTIONS.md` §B2):**
- Sub-tab в `QuestDatabaseWindow` для visual graph editing DialogTree.
- Использует `UnityEditor.Experimental.GraphView`.
- `DialogGraphView : GraphView` с custom `DialogueNodeView` + `DialogueEdgeView`.
- Drag-drop nodes, click edges для редактирования.
- **Sync с SO**: edits в GraphView → updates `DialogTree.nodes[]` + `DialogTree.edges[]` → save `.asset`.
- Validate button → проверяет reachability, dangling edges.

**Verify:**
- Open dialog tree в QuestDatabaseWindow → switch to "Graph" tab → видим визуальный граф.
- Drag node → edges обновляются.
- Save → SO ассет обновлён, Inspector показывает те же данные.

**Risk:** high. GraphView API experimental, complex. Может потребовать 2 итерации.

**Pitfalls:** GraphView API может измениться между Unity versions. Проверить Unity 6.0.4 API.

---

### T-Q10 — DialogWindow UI Toolkit (large, 150 мин) — РАСШИРЕН

**Скоуп:**
- `Assets/_Project/UI/Resources/UI/DialogWindow.uxml`, `.uss`.
- `Assets/_Project/Quests/UI/DialogWindow.cs` (MonoBehaviour, 4 FIX'ы).
- Place `DialogWindow` GameObject в `BootstrapScene.unity`.
- Auto-spawn в `NetworkManagerController.Awake`.
- Typewriter coroutine.
- USS classes per `04_DIALOG_AND_QUEST_UI.md` §4.3.
- **+ F skip typewriter** (вместо Space) — `PlayerInputReader.Instance?.OnModeSwitchPressed` skip when visible (см. `09_OPEN_QUESTIONS.md` §C1 + §L).
- **+ Click мышью** на body → skip.
- **+ Two reputation badges** (factionRep + npcAttitude) в header.
- Subscribe to `QuestClientState`, `ReputationClientState`, `NpcAttitudeClientState`.

**Verify:**
- Open DialogWindow → window appears, 4 FIX'ы apply, no flicker.
- Show test step → text typewriter works, options appear.
- Press F → typewriter skips.
- Click body → typewriter skips.
- Esc → window hides, cursor locks.

**Risk:** high. UI Toolkit + 4 FIX'ы + multi-state input.

**Pitfalls:** `04_DIALOG_AND_QUEST_UI.md` §4.8.

---

### T-Q11 — Quest log таб в CharacterWindow (medium, 90 мин) — РАСШИРЕН

**Скоуп:**
- Modify `CharacterWindow.uxml` — add `tab-quests` button + `quests-section`.
- Modify `CharacterWindow.uss` — quest row styles.
- Modify `CharacterWindow.cs` — fields, EnsureBuilt, SwitchTab, subscribe.
- **+ Discovered раздел** (см. `09_OPEN_QUESTIONS.md` §K):
  - Под-таб или section "Discovered" с "Accept" кнопкой.
  - При клике "Accept" → `QuestClientState.RequestAcceptQuest(questId, npcId)`.
- Subscribe to `QuestClientState.OnDiscoveredQuest`.

**Verify:**
- Open CharacterWindow → 6th tab "КВЕСТЫ".
- Active/Completed/Failed/Discovered секции.
- Discovered → Accept → квест переходит в Active.

**Risk:** medium. Existing CharacterWindow — 1345 LOC, не сломать 5 существующих табов.

**Pitfalls:** Cross-tab cache (R3-005 lesson).

---

### T-Q12 — QuestTracker overlay (small, 30 мин)

**Скоуп:**
- `Assets/_Project/UI/Resources/UI/QuestTracker.uxml`, `.uss`.
- `Assets/_Project/Quests/UI/QuestTracker.cs`.
- Place QuestTracker GameObject в BootstrapScene.

**Verify:** Active tracked quest shows in top-right corner. Hide when no tracked quest.

**Risk:** low. Standalone overlay.

---

### T-Q13 — ReputationClientState + NpcAttitudeClientState + tab fix (medium, 60 мин) — РАСШИРЕН

**Скоуп (см. `09_OPEN_QUESTIONS.md` §G):**
- `ProjectC.Reputation.ReputationClientState` (singleton, OnReputationUpdated).
- **+ `ProjectC.Reputation.NpcAttitudeClientState`** (singleton, OnNpcAttitudeUpdated).
- Modify `CharacterWindow.cs` — replace empty `RefreshReputationCache` (line 507).
- **+ NpcAttitude под-список** в Reputation табе.
- **+ Cross-faction influence calc** (server-side, в QuestWorld.ModifyNpcAttitude) — MVP stub (полная реализация → v2).

**Verify:**
- Modify reputation in editor test → CharacterWindow tab updates.
- NpcAttitude badge в DialogWindow header.
- Cross-link: улучшить Mira → factionRep[GuildOfCreation] уменьшается (с конфигом).

**Risk:** low. Wire-up + UI.

---

### T-Q14 — InventoryServer.TryRemove + event bus hooks (medium, 60 мин)

**Скоуп (см. `09_OPEN_QUESTIONS.md` §J):**
- `Items/Network/InventoryServer.cs` — add `TryRemove(ulong, int, int)` public method.
- `Items/InventoryWorld.cs` — `RemoveItems(int, int)` private method.
- **+ event hooks**: publish `ItemRemovedEvent` через WorldEventBus.
- Verify `ItemAddedEvent` уже published (от T-X0).

**Verify:** Call `InventoryServer.TryRemove` → snapshot updates, `ItemRemovedEvent` fires.

**Risk:** medium. Modifying stable Items subsystem. **Grep transitive deps** обязательно.

---

### T-X5 — ContractServer publish events для quest bridge (medium, 60 мин) — НОВЫЙ, ОБЯЗАТЕЛЬНЫЙ

**Скоуп (см. `09_OPEN_QUESTIONS.md` §A2 + §J):**
- `ContractServer.cs` — добавить publishes:
  - `ContractAcceptedEvent { contractId, playerId, fromNpcId, timestamp }`.
  - `ContractCompletedEvent { contractId, playerId, timestamp, wasReceipt }`.
  - `ContractFailedEvent { contractId, playerId, timestamp, debtIncurred }`.
- `ContractClientState.cs` — подписка на эти events (passive, не обрабатывает).
- Через эти events `QuestServer` может react (например, quest objective "доставить cargo в порт X" completed когда `ContractCompletedEvent` fires).

**Verify:**
- Accept contract → console log `[WorldEventBus] Published ContractAcceptedEvent`.
- Quest с objective tracking contract completion → advances when ContractCompletedEvent fires.

**Risk:** low. Добавление publishes (existing accept/complete/fail hooks).

---

### T-Q15 — DialogueAction: GiveItem/TakeItem + ContractMetaBridge (medium, 90 мин) — РАСШИРЕН

**Скоуп:**
- `ProjectC/Dialogue/ActionExecutors/ItemActionExecutor.cs`.
- Wires `DialogueAction.GiveItem/TakeItem` → `InventoryServer.AddItem/TryRemove`.
- **+ ContractMetaBridge** (см. `09_OPEN_QUESTIONS.md` §A2):
  - `ProjectC/Quests/Bridges/ContractMetaBridge.cs`.
  - Подписывается на `ContractCompletedEvent`, `ContractAcceptedEvent`.
  - `QuestTriggerService.ContractCompletedTrigger` — quest objective checks "игрок выполнил contract X".
  - Allow quest prerequisites: "quest A completable только если contract B completed within last 24h".

**Verify:**
- Walk through Mira quest, complete "intro" → `GiveCredits(50)`, `AddReputation(+25)` works.
- Complete contract → quest objective "доставить cargo" advances.

**Risk:** medium. Touches inventory server + new bridge.

---

### T-Q16 — DialogueAction: GiveCredits/AddReputation/AddNpcAttitude (small, 45 мин) — РАСШИРЕН

**Скоуп (см. `09_OPEN_QUESTIONS.md` §G):**
- `ProjectC/Dialogue/ActionExecutors/CreditsActionExecutor.cs`.
- `ProjectC/Dialogue/ActionExecutors/ReputationActionExecutor.cs`.
- **+ `ProjectC/Dialogue/ActionExecutors/NpcAttitudeActionExecutor.cs`** (новый).
- `QuestWorld.ModifyReputation(playerId, faction, delta)`.
- `QuestWorld.ModifyNpcAttitude(playerId, npcId, delta)` — triggers cross-faction influence (MVP stub).

**Verify:** After "intro" stage complete → credits +50, reputation +25, npcAttitude[mira] +5.

**Risk:** low.

---

### T-Q17 — DialogueAction: OpenMarket/OpenService (small, 30 мин)

**Скоуп:**
- `ProjectC/Dialogue/ActionExecutors/MarketActionExecutor.cs`.
- `ProjectC/Dialogue/ActionExecutors/ServiceActionExecutor.cs`.
- Calls `MarketWindow.Instance.Open(zoneId)` / `ServiceUI.Open(serviceId)`.

**Verify:** Click "Покажи свои товары" → MarketWindow opens, close → dialog resumes.

**Risk:** low.

---

### T-Q18 — Persistence: IQuestStateRepository + JSON, immediate save (large, 120 мин) — РАСШИРЕН

**Скоуп (см. `09_OPEN_QUESTIONS.md` §A5 + §H):**
- `ProjectC/Quests/Persistence/IQuestStateRepository.cs` (interface).
- `ProjectC/Quests/Persistence/JsonQuestStateRepository.cs` (default impl).
- `ProjectC/Quests/Persistence/QuestSaveData.cs` (POCO, includes quests + rep + npcAttitude + flags).
- `ProjectC/Quests/Persistence/ReputationSaveData.cs`.
- `ProjectC/Quests/Persistence/NpcAttitudeSaveData.cs`.
- **Immediate save on every state change** (no debounce, per `09_OPEN_QUESTIONS.md` §H).
- `QuestWorld.SavePlayer(clientId)` пишет ВСЕ данные atomic в ОДИН JSON.
- Hooks: `QuestStateTransition`, `StageTransition`, `ObjectiveProgressed`, `ReputationChanged`, `NpcAttitudeChanged`, `DialogVisitedNode`, `FlagSet`.
- Load on player connect (в `QuestServer.HandleClientConnected`).
- `Application.persistentDataPath/quest_state_<clientId>.json`.

**Verify:**
- Accept quest, restart server, reconnect → quest still in active state.
- Modify reputation, kill server, restart → rep сохранилось.
- Console: `[QuestWorld] Saved player 12345 state (1.2 KB) in 1.1ms`.

**Risk:** medium-high. JSON serialization of nested DTOs. **Version migration** — out of scope v1 (см. F6 в `09_OPEN_QUESTIONS.md`).

**Pitfalls:** Save на каждом state change — perf acceptable (1-5 KB JSON, 1 ms).

---

### T-Q19 — C1 cleanup: delete v1 NPC (medium, 60 мин)

**Скоуп:**
- Grep transitive deps по всем .cs файлам на `NpcData`/`NpcEntity`/`NpcInteraction`/`NpcDialogueManager`.
- Delete (move to `Assets/_Project/_archive/v1_npc/` если git-friendly, иначе rm + commit).
- Update AGENTS.md если явно упомянуто.

**Verify:**
- Console: 0 errors, 0 warnings.
- Project compiles.
- Play Mode: quest system works end-to-end.

**Risk:** medium. Grep transitive deps обязателен.

---

### T-X1 — Trade.Core.NPCTrader → MarketTrader (small, 30 мин) — OPTIONAL

**Скоуп:**
- Grep `NPCTrader` usages.
- Rename `NPCTrader` → `MarketTrader` (class, file, references).

**Verify:** Console 0 errors.

**Risk:** low.

---

### T-X2 — TradeItemDefinition Faction → FactionId (medium, 60 мин) — OPTIONAL

**Скоуп:**
- Verify `TradeItemDefinition` has `Faction` field.
- Rename → `FactionId requiredFaction` (after T-Q01).
- Update references.

**Risk:** medium.

---

### T-X4 — input remap: pickup E → F (small, 45 мин) — FUTURE TODO

**Скоуп (см. `09_OPEN_QUESTIONS.md` §L):**
- Remap `Keyboard.current.eKey.wasPressedThisFrame` (в `NetworkPlayer.Update:375` pickup/chest branch) → `Keyboard.current.fKey.wasPressedThisFrame` ИЛИ `PlayerInputReader.Instance?.OnModeSwitchPressed`.
- NPC talk остаётся на E.
- Документировать в AGENTS.md "F = boarding + pickup + future NPC action".

**Verify:** Press F near pickup → pickup. Press E near NPC → dialog.

**Risk:** low (после T-X3 + T-Q08 уже готовы).

**Примечание:** это **future TODO** — делать после end-to-end demo (Mira quest) полностью работает.

---

## 8.4 Milestones (обновлено)

| Milestone | Тикеты | Что работает |
|-----------|--------|--------------|
| **M1 — Data foundation** | T-Q01, T-Q02, T-Q03, T-Q04 | Все SO + NpcAttitude struct + EventDriven objective. Inspector редактируется. |
| **M1.5 — Inventory persistence + Event bus foundation** | T-X0 | WorldEventBus + inventory save/load. Hooks в InventoryServer. |
| **M2 — Server core** | T-Q05, T-Q06, T-Q07 | QuestServer спавнится, RPCs работают, DTOs передаются, full event bus + triggers. |
| **M2.5 — Input refactor** | T-X3 | PlayerInputReader events, NetworkPlayer subscribes. |
| **M3 — Player interaction** | T-Q08, T-Q10 | E-key → talk to NPC → DialogWindow opens (F skip). |
| **M4 — Quest log + tracker** | T-Q11, T-Q12 | Player can accept quest, see in log (Active/Completed/Discovered), see tracker. |
| **M5 — Reputation + NpcAttitude** | T-Q13 | Reputation updates, NpcAttitude, CharacterWindow tab fix. |
| **M6 — Item integration** | T-Q14, T-Q15 | Quest rewards give items, quest objectives check items, ContractMetaBridge. |
| **M7 — Full action set** | T-Q16, T-Q17, T-X5 | Credits/rep/attitude/market actions + ContractServer events. |
| **M8 — Persistence** | T-Q18 | Quests + rep + attitude survive server restart. |
| **M9 — Cleanup** | T-Q19, T-X1, T-X2 | v1 NPC deleted, optional renames. |
| **M10 — Editor tool** | T-Q09, T-Q09b | Quest Database Explorer с full CRUD + GraphView. |
| **M11 — End-to-end demo** | After M9 | Mira quest full playthrough. |
| **M12 — Input remap** | T-X4 | F = pickup (future, post-demo). |

**Рекомендуемый темп:** 1-2 тикета за сессию, 1 PR за тикет.

---

## 8.5 Оценка общей трудоёмкости (обновлено)

| Категория | Тикеты | ~Часов |
|-----------|--------|--------|
| Foundation (SO/data + structs) | T-Q01-T-Q04 | ~6 ч |
| World event bus + inventory persistence | T-X0 | ~1.5 ч |
| Server + client core (с full event bus) | T-Q05-T-Q07 | ~9 ч |
| Input refactor | T-X3 | ~1.5 ч |
| Player interaction (NPC branch) | T-Q08 | ~1 ч |
| Editor tool (CRUD + GraphView) | T-Q09, T-Q09b | ~6 ч |
| UI (dialog + quest log + tracker) | T-Q10-T-Q12 | ~5 ч |
| Reputation + NpcAttitude | T-Q13 | ~1.5 ч |
| Integration (inventory, contract bridge) | T-Q14, T-X5, T-Q15 | ~4 ч |
| Action set (credits, rep, market) | T-Q16, T-Q17 | ~1.5 ч |
| Persistence (atomic JSON, immediate save) | T-Q18 | ~2 ч |
| Cleanup + optional | T-Q19, T-X1, T-X2, T-X4 | ~3 ч |
| **TOTAL** | **22 тикета** | **~42 ч чистого кодинга** |

**С реальным PlayMode-тестированием, отладкой, fix-циклами, GraphView итерациями: ~60-90 ч (5-8 сессий в неделю).**

---

## 8.6 Риски (обновлено)

| # | Риск | Митигация |
|---|------|-----------|
| 1 | Scene-placed NRE (NetworkObject) | T-Q05: обязательно через `ScenePlacedObjectSpawner`. |
| 2 | INetworkSerializable pitfall (Nullable<T>) | T-Q07: hand-rolled pattern из `ContractResultDto.cs:60-90`. |
| 3 | UI Toolkit в editor (T-Q09) | Использовать UI Toolkit examples Unity 6 docs. |
| 4 | GraphView API experimental (T-Q09b) | Tщательно проверить Unity 6.0.4 API, fallback на indented tree. |
| 5 | 4 FIX'ы для DialogWindow | T-Q10: copy-paste CharacterWindow 4 FIX'ы. |
| 6 | InventoryWorld persistence (T-X0) | Фиксим ДО quest rewards. Grep transitive deps. |
| 7 | WorldEventBus static singleton — testability | `Reset()` method для test isolation. |
| 8 | Cross-tab cache (R3-005 lesson) | T-Q11: lazy subscribe + unconditional refresh + gated rebuild. |
| 9 | Grep transitive deps при cleanup (T-Q19) | Тщательно grep ВСЕ .cs файлов. |
| 10 | Inventory server modify (T-Q14) | Тестировать существующий TryDrop/TryPickup. |
| 11 | Combat не существует | `KilledEntityTrigger` stub. |
| 12 | Full PlayerInputReader refactor (T-X3) | Делать ДО T-Q08. Тщательно grep `Keyboard.current`. |
| 13 | Full event bus (T-Q06) | Cross-cutting — тестировать каждую подписку отдельно. |
| 14 | NpcAttitude + cross-faction influence (T-Q13) | MVP stub для cross-calc, полная реализация v2. |
| 15 | EventDriven quests (T-Q04 + T-Q11) | Discovered state UI может быть confusing — disambiguate в UI. |
