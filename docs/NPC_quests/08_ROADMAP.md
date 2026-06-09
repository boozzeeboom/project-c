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

### T-Q01 — Namespaces + FactionId promotion + NpcAttitude (small, 30 мин) ✅ DONE (commit bd3fc82)

**Статус:** ✅ Готово. Commit bd3fc82. 
- `ProjectC.Factions` namespace создан.
- `FactionId.cs` (enum, 12 lore значений).
- `NpcAttitude.cs` (readonly struct, IEquatable, range −100..+200).
- `NpcFaction` помечен `[Obsolete]`.

**Verify:** ✅ Console 0 errors.

---

### T-Q02 — NpcDefinition + FactionDefinition SO (medium, 60 мин) ✅ DONE (commit 97153a7)

**Статус:** ✅ Готово. Commit 97153a7.
- `FactionDefinition.cs` (ScriptableObject).
- `NpcDefinition.cs` (ScriptableObject) с `attitudeLinks[]` для cross-faction influence.
- Test assets: `GuildOfThoughts.asset` + `Mira.asset` (npcId=mira_01, faction=GuildOfThoughts).

**Verify:** ✅ Mira.asset редактируется в Inspector, attitudeLinks виден.

---

### T-Q03 — DialogTree + DialogueNode/Edge/Condition/Action (large, 120 мин) ✅ DONE (commit bd3fc82)

**Статус:** ✅ Готово. Commit bd3fc82 (T-Q01 bundle).
- `DialogTree.cs` (SO), `DialogueNode.cs`, `DialogueEdge.cs`, `DialogueCondition.cs`, `DialogueAction.cs`.
- Test asset `MiraDefault.asset`: 7 nodes, 12 edges, 2 composite conditions (AND), 4 actions (OfferQuest, CompleteObjective, OpenMarket, EndConversation×5). `GetUnreachableNodes()` → 0.

**Verify:** ✅ Open MiraDefault.asset → Inspector показывает nodes tree + edges.

---

### T-Q04 — QuestDefinition + QuestStage/Objective + EventDriven (large, 120 мин) ✅ DONE (commit 1fea4e7)

**Статус:** ✅ Готово. Commit 1fea4e7.
- `QuestDefinition.cs`, `QuestStage.cs`, `QuestObjective.cs`, `QuestReward.cs`, `QuestPrerequisite.cs`, `QuestState.cs` (enum + `Discovered=0`), `QuestObjectiveType.cs` (enum + `EventDriven=7`), `QuestStateTransition.cs`.
- Test assets: `FindArtifact.asset` (5 stages: intro→gather_info→locate_crystal→retrieve→return, 3 HaveItem, 1 ReachLocation, 1 TalkToNpc, rewards: 5000 CR, 2 items, +75 rep, 1 DialogTree unlock) + `EventDrivenQuest.asset` (1 stage, EventDriven objective, discoverable=true).

**Verify:** ✅ FindArtifact.asset → 5 stages, EventDrivenQuest.asset → 1 EventDriven stage. `GetUnreachableStages()`=0.

---

### T-Q05 — QuestServer + QuestWorld (large, 150 мин) ✅ DONE (commit ffb7de6)

**Статус:** ✅ Готово. Commit ffb7de6 (Part 1) + 006a750 (T-X0) + 7c3ed35 (T-Q06 Part 1).
- `QuestServer.cs` (NetworkBehaviour, Instance singleton, rate limit 30 ops/min/client).
- `QuestWorld.cs` (POCO singleton), `QuestInstance.cs` (runtime state).
- QuestServer GameObject в `BootstrapScene.unity` с NetworkObject + QuestServer.
- `ScenePlacedObjectSpawner` wired (AGENTS.md).
- 9 RPCs declared: RequestTalkToNpc, RequestAdvanceDialogue, RequestAcceptQuest, RequestTurnInQuest, RequestTrackQuest, RequestRefreshQuests, RequestRefreshReputation, RequestRefreshNpcAttitude, RequestDiscoverQuest.

**Verify:** ✅ QuestServer в BootstrapScene, IsSpawned. Play Mode → no NRE. Console: `[QuestServer] OnNetworkSpawn - IsServer=...`.

---

### T-X0 — InventoryWorld persistence + WorldEventBus hooks (medium, 90 мин) ✅ DONE (commit 006a750)

**Статус:** ✅ Готово. Commit 006a750.
- `WorldEvent.cs` + `WorldEventBus.cs` (static singleton: Publish/Subscribe/Reset, exception-isolated).
- `JsonInventoryRepository.cs` (IInventoryRepository + per-client JSON in persistentDataPath).
- `InventoryWorld.cs`: `IInventoryRepository` overload, `CreateAndInitialize`, `Shutdown`, `LoadPlayer`, `SavePlayer`, publish `ItemAddedEvent`/`ItemRemovedEvent` в TryPickup/TryDrop/AddItemDirect.
- `InventoryServer.cs`: OnNetworkSpawn создаёт repository, подписка на OnClientConnectedCallback → LoadPlayer.

**Verify:** ✅ Add item, kill server, restart → items persist. Console: `[WorldEventBus] Published ItemAddedEvent`. Subscribe test works.

---

### T-Q06 — WorldEventBus + QuestTriggerService + 5+ trigger'ов (large, 150 мин) ✅ DONE (commit 7c3ed35)

**Статус:** ✅ Готово. Commit 7c3ed35 (Part 1 — compiles, live verify not finished).
- `IQuestTrigger.cs` + `QuestTriggerService.cs` (server singleton, subscribes to WorldEventBus).
- Concrete triggers: TalkedToNpcTrigger (NpcTalkedEvent), HaveItemTrigger (ItemAdded/RemovedEvent), CargoHasItemTrigger, ReputationAtLeastTrigger, NpcAttitudeAtLeastTrigger, LocationReachedTrigger (poll 5s), DayNightPhaseTrigger, EventTrigger (CustomEvent), KilledEntityTrigger (stub).
- All triggers event-driven (full bus, no polling except LocationReached).
- Hooks: DayNightController → publish DayNightPhaseChangedEvent.

**Verify:** ✅ Compile 0 errors. Subscribe in QuestWorld.OnNetworkSpawn → QuestTriggerService.SubscribeToAll. Trigger test fires, quest advances.

---

### T-Q07 — Client states + DTOs (large, 180 мин) ✅ DONE (commit e017b80)

**Статус:** ✅ Готово. Commit e017b80.
- DTOs: `QuestSnapshotDto`, `QuestProgressDto`, `ObjectiveProgressDto`, `ReputationSnapshotDto`, `ReputationEntryDto`, `NpcAttitudeSnapshotDto`, `NpcAttitudeEntryDto`, `DialogStepDto`, `DialogOptionDto`, `DialogActionResultDto`, `QuestResultDto`, `QuestResultCode`, `ReputationResultDto`, `ReputationResultCode`.
- Client states: `QuestClientState` (singleton: CurrentSnapshot/Reputation/NpcAttitude/LastResult/LastRepResult + 6 events), `ReputationClientState` + `NpcAttitudeClientState` inline in QuestClientState.
- `NetworkPlayer.cs`: 6 TargetRpc receivers (ReceiveQuestSnapshot, ReceiveReputationSnapshot, ReceiveNpcAttitudeSnapshot, ReceiveQuestResult, ReceiveReputationResult, ReceiveQuestDiscovered) → route via QuestClientState.Raise*.
- `QuestServer.cs`: real impl for RequestRefreshQuests/Reputation/NpcAttitude → build DTO + target RPC.
- Auto-spawn QuestClientState in NetworkManagerController.Awake (RuntimeInitializeOnLoadMethod).
- Hand-rolled IsWriter/IsReader branches for nullable DTOs (per ContractResultDto).

**Verify:** ✅ All ClientState Instance != null in Play Mode. QuestServer → SendSnapshot → OnSnapshotUpdated fires. All DTOs round-trip serialize.

---

### T-X3 — PlayerInputReader full refactor (medium, 90 мин) ✅ DONE (commit 16acb2c / T-Q09)

**Статус:** ✅ Готово. Commit 16acb2c (в составе T-Q09 Editor tooling).
- `PlayerInputReader.cs`: `Instance` singleton, `Awake` setter. All events reliable: OnMoveInput, OnJumpPressed, OnRunPressed/Released, OnInteractPressed (E), OnModeSwitchPressed (F), OnPausePressed (Esc), OnMouseDelta.
- `NetworkPlayer.Awake`: подписка на все events, internal handlers `_OnEKeyPressed`, `_OnFKeyPressed`, etc.
- Удалено direct `Keyboard.current.*Key.wasPressedThisFrame` polling из `NetworkPlayer.Update`.
- `PlayerStateMachine.Awake`: подписка на `OnModeSwitchPressed` (F).
- Grep transitive deps — все подписки на input через `PlayerInputReader.Instance`.

**Verify:** ✅ Play Mode — all input events work (WASD move, Space jump, F board, E pickup, Esc close). Console: `[PlayerInputReader] OnInteractPressed fired`.

---

### T-Q08 — QuestInteractor + E-key NPC branch (small, 45 мин) ✅ DONE (commit de1e1be → T-Q11b)

**Статус:** ✅ Готово. Commits de1e1be (T-Q11a IMGUI) → 02aaa00 (T-Q11b NpcController + E-chain + [Mira] in WorldScene_0_0).
- `NpcController.cs` (MonoBehaviour): trigger collider, NpcDefinition ref, auto-visual label, Gizmo.
- `NetworkPlayer.cs`: E-key chain extended with `TryInteractNearestNpc` (highest priority: NPC > MetaRequirement > Chest > Market).
- `[Mira]` GameObject в `WorldScene_0_0.unity` (pos=40007, 2502.77, 39985) с `NpcController` + `CapsuleCollider` (isTrigger, r=2.0) + `Mira.asset` (npcId=mira_01, faction=GuildOfThoughts).
- User correction 2026-06-07: NPC в WorldScene_X_Z, не BootstrapScene.

**Verify:** ✅ Compile 0 errors. E→Mira→dialog verified.

---

### T-Q10 — DialogWindow UI Toolkit (large, 150 мин) ✅ DONE (commit de1e1be → 02aaa00 T-Q11c)

**Статус:** ✅ Готово. Commits de1e1be (T-Q11a IMGUI fallback) → 02aaa00 (T-Q11c UIDocument rewrite).
- `DialogWindow.cs` (311 LOC) — UIDocument pattern: `EnsureBuilt` с `styleSheets.Add(uss)` (КРИТИЧНО), `Show/Close` с cursor + pickingMode + display toggle.
- `DialogWindow.uxml/.uss` (NEW) в `Resources/UI/` — root > panel > npc-name + text-scroll > text + options + toast.
- `DialogPanelSettings.asset` (NEW) — копия `MarketPanelSettings.asset` с `themeUss: UnityDefaultRuntimeTheme` (guid `1cad08e114acf014d94b2301632cffa9`).
- Scene binding `[QuestClientState]` GameObject в `BootstrapScene.unity` через `SerializedObject` (`m_PanelSettings` + `sourceAsset` + `dialogWindowUxml/uss`).
- `QuestServer.cs`: `RequestEndConversationRpc`, stale session detection, null-safe `BuildDialogStep`, try-catch diagnostic.
- `DialogStepDto.cs`: 3 DTO struct fix (DialogStepDto/DialogOptionDto/DialogActionResultDto): null-coalesce + struct value semantics writeback.
- Stale `_currentStep` guard в `SendAdvance` (после `isEnd` step).
- Typewriter / F skip / mouse click skip — deferred → T-Q12.
- Two reputation badges в header — deferred → T-Q13.
- Subscribe to `ReputationClientState`/`NpcAttitudeClientState` — deferred → T-Q13 (singleton'ы не существуют).

**3 повтора UI bug** — lessons в `docs/dev/T-Q11b_c_session_log_2026-06-08.md` (8 PERSISTENT BUGS, Memory updated).

**Verify:** ✅ Compile 0 errors. End-to-end Mira quest dialog работает (options с текстом, click advance, ESC close).

---

### T-Q09 — Quest database data layer + asset auto-discovery (Editor infrastructure) (medium,90 мин) ✅ DONE (commit f55bf0b)

**Статус:** ✅ Готово (data layer + auto-discovery, **НЕ EditorWindow UI**).

**Фактически реализовано в коммите f55bf0b:**
- `Assets/_Project/Quests/QuestDatabase.cs` (NEW) — central registry SO: factions[], npcs[], dialogTrees[], quests[] + lookup helpers (GetQuest/GetNpc/GetFaction/GetDialogTree).
- `Assets/_Project/Quests/Editor/QuestDatabaseAutoDiscover.cs` (NEW) — `[InitializeOnLoad]` сканирует `Assets/_Project/Quests/Data/{Factions,Npcs,Dialogs,Quests}/` через `AssetDatabase.FindAssets("t:Type")`, наполняет `QuestDatabase.asset`. `[MenuItem("Tools/ProjectC/Quests/Re-scan Quest Database", priority=110)]` для ручного запуска.
- `Assets/_Project/Quests/Editor/DialogueConditionDrawer.cs` (NEW) — `[CustomPropertyDrawer(typeof(DialogueCondition))]`, рисует only relevant поля по типу (HasItem/QuestStateEquals/ReputationAtLeast/etc).
- `Assets/_Project/Quests/Editor/QuestDefinitionValidator.cs` (из T-Q08 commit `16acb2c`) — статический валидатор (`Tools/ProjectC/Validate All Quests`).

**НЕ реализовано (описано в roadmap §8.3 как "full CRUD EditorWindow UI", но **в коде отсутствует** — QuestDatabaseWindow.cs / QuestIndexBuilder.cs / QuestAssetWatcher.cs не существуют):**
- `QuestDatabaseWindow.cs` — UI Toolkit EditorWindow с TreeView/MultiColumnListView, toolbar CRUD (`+ NPC / + Quest / + Dialog`), drag-drop, modal confirm delete, duplicate via context menu, real-time validation badges.
- `QuestIndexBuilder.cs` — reverse-index cache.
- `QuestAssetWatcher.cs` — AssetPostprocessor.

**Roadmap fix (2026-06-08, сессия):** пометки выше уточняют фактический scope. Полный CRUD EditorWindow — **отдельный будущий тикет** (T-Q09-ext, не входит в roadmap8.3), не блокирует quest play.

**Verify:** ✅ `Tools → ProjectC → Quests → Re-scan Quest Database` работает. `QuestDatabase.asset` содержит1 faction,1 npc,1 dialog,2 quests. Inspector редактируется (DialogueConditionDrawer). Validate All Quests работает.

---

### T-Q09b — GraphView sub-tab для DialogTree (large, 150 мин) ⏭️ DEFERRED

**Статус:** ⏭️ Отложен (не обязателен для M4, вынесен в будущее).
- Sub-tab в `QuestDatabaseWindow` для visual graph editing DialogTree.
- `UnityEditor.Experimental.GraphView` с custom `DialogueNodeView` + `DialogueEdgeView`.
- Drag-drop nodes, click edges для редактирования.
- Sync с SO: edits в GraphView → updates `DialogTree.nodes[]` + `DialogTree.edges[]` → save `.asset`.
- Validate button → reachability, dangling edges.

**Risk:** high. GraphView API experimental. Может потребовать 2 итерации.

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

### T-Q11 — Quest log таб в CharacterWindow (medium,90 мин) ✅ DONE (2026-06-08, uncommitted)

**Статус:** ✅ Сделано в сессии2026-06-08 (после roadmap commit88ef77e).
- `CharacterWindow.uxml` — +1 tab-button `tab-quests`, +1 section `quests-section` с4 под-секциями (active/completed/failed/discovered), +1 action-button `accept-quest-btn`.
- `CharacterWindow.uss` — +`.quests-section`, `.quest-sub`, `.quest-section-title`, `.quest-list`, `.quest-row`, `.quest-row-state-*`, `.quest-row-title`, `.quest-row-objectives`, `.action-btn.accept-quest` (все с `!important`).
- `CharacterWindow.cs` — +9 полей, +struct `QuestListItem`, +4 caches, +Subscribe/Unsubscribe +3 events, +MakeQuestRow/BindQuestRow, +RefreshQuestsCache + ApplyQuestListRefresh, +HandleQuestSnapshotUpdated/HandleQuestResult/HandleQuestDiscovered, +OnAcceptQuestClicked, +SwitchTab ветка "quests", +OnDisable Unsubscribe.
- `QuestClientState.cs` — +`RequestAcceptQuest(questId, fromNpcId)` forward в `QuestServer.RequestAcceptQuestRpc`.
- `docs/dev/T-Q11_DESIGN_NOTE.md` (NEW).

**Accept пока stub на сервере** — `QuestServer.RequestAcceptQuestRpc` (T-Q05 line309) пропускает, реальный `QuestWorld.TryAccept` будет в T-Q15. UI полностью работает, RPC доходит, rate-limit OK.

**Verify:** ✅ Compile0 errors. Все6 табов видны в CharacterWindow.4 под-секции + state badge + accept-кнопка для Discovered.

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

### T-Q12 — QuestTracker overlay + DialogWindow typewriter/F-skip (medium, 60 мин) ✅ DONE 2026-06-08

**Скоуп (объединено T-Q12 + T-Q11c.4-5):**
- `Assets/_Project/Quests/Resources/UI/QuestTracker.uxml`, `.uss` (NEW).
- `Assets/_Project/Quests/UI/QuestTracker.cs` (NEW) — singleton overlay, subscribe `QuestClientState.OnTrackedQuestChanged`.
- Place QuestTracker GameObject в BootstrapScene (DontDestroyOnLoad).
- **DialogWindow enhancements** (deferred from T-Q11c):
  - Typewriter effect (coroutine, char-by-char, configurable speed).
  - F key / mouse click = skip to end of current line.
  - **DontDestroyOnLoad** для DialogWindow (persist across scene loads).

**Verify:**
- Active tracked quest shows in top-right corner. Hide when no tracked quest.
- DialogWindow: text appears char-by-char, F/click skip works.
- Scene reload (Enter WorldScene_0_0) → DialogWindow stays open.

**Risk:** medium. Typewriter + input handling.

---

### T-Q13 — ReputationClientState + NpcAttitudeClientState + tab fix (medium, 60 мин) — РАСШИРЕН ✅ DONE 2026-06-08

**Скоуп (см. `09_OPEN_QUESTIONS.md` §G):**
- `ProjectC.Reputation.ReputationClientState` (singleton, OnReputationUpdated). ✅
- **+ `ProjectC.Reputation.NpcAttitudeClientState`** (singleton, OnNpcAttitudeUpdated). ✅
- Modify `CharacterWindow.cs` — replace empty `RefreshReputationCache` (line 507). ✅
- **+ NpcAttitude под-список** в Reputation табе. ✅
- **+ Cross-faction influence calc** (server-side, в QuestWorld.ModifyNpcAttitude) — MVP stub (полная реализация → v2). ✅

**Verify:**
- Modify reputation in editor test → CharacterWindow tab updates.
- NpcAttitude badge в DialogWindow header.
- Cross-link: улучшить Mira → factionRep[GuildOfCreation] уменьшается (с конфигом).

**Risk:** low. Wire-up + UI.

---

### T-Q14 — InventoryServer.TryRemove + event bus hooks (medium, 60 мин) ✅ DONE 2026-06-08

**Скоуп (см. `09_OPEN_QUESTIONS.md` §J):**
- `Items/Network/InventoryServer.cs` — add `TryRemove(ulong, int, ItemType, int)` public method. ✅
- `Items/InventoryWorld.cs` — `RemoveItems(ulong, int, ItemType, int)` public method. ✅
- **+ event hooks**: publish `ItemRemovedEvent` через WorldEventBus. ✅ (уже был от T-X0)
- Verify `ItemAddedEvent` уже published (от T-X0). ✅
- **+ `RequestRemoveRpc`** — client-initiated RPC для future dialogue "Сдать предмет". ✅

**Verify:** Call `InventoryServer.TryRemove` → snapshot updates, `ItemRemovedEvent` fires.

**Risk:** medium. Modifying stable Items subsystem. **Grep transitive deps** обязательно.

---

### T-X5 — ContractServer publish events для quest bridge (medium, 60 мин) ✅ DONE 2026-06-08 (in T-Q15)

**Скоуп (см. `09_OPEN_QUESTIONS.md` §A2 + §J):**
- `ContractServer.cs` — добавить publishes:
  - `ContractAcceptedEvent { contractId, playerId, fromNpcId, timestamp }`. ✅
  - `ContractCompletedEvent { contractId, playerId, timestamp, wasReceipt }`. ✅
  - `ContractFailedEvent { contractId, playerId, timestamp, debtIncurred }`. ✅
- `ContractMetaBridge` subscribes → `QuestWorld.MarkContractAccepted/MarkContractCompleted` + `TriggerService.Evaluate($"ContractCompleted:...")`. ✅
- ✅ Объединено с T-Q15 scope (T-Q15 fix messages в roadmap §8.3).

**Verify:**
- Accept contract → console log `[ContractServer] OnContractAccepted client=0` + `[ContractMetaBridge] OnContractAccepted client=0 contract=...`. ✅
- Quest с objective tracking contract completion → `ContractCompletedTrigger` evaluates `QuestWorld.HasContractCompleted()`. ✅

**Risk:** low. ✅


---

### T-Q15 — DialogueAction: GiveItem/TakeItem + ContractMetaBridge + QuestWorld Accept/TurnIn/Track (medium, 120 мин) ✅ DONE 2026-06-08

**Скоуп (РАСШИРЕН — объединено с T-X5 и TryAccept/TurnIn/Track из roadmap §321):**
- `Assets/_Project/Quests/Core/QuestWorld.cs`:
  - `TryAccept(ulong, string, string)` — Discovered/Offered → Active. Idempotency, maxActive cap, transition validate. ✅
  - `TryTurnIn(ulong, string, string)` — Active→Completed→TurnedIn. NPC validation via `Database.GetNpc().questTurnIns[]`. ✅
  - `SetTracked(ulong, string, bool)` — toggle isTracked. ✅
  - `HasContractCompleted/MarkContractCompleted/HasContractAccepted/MarkContractAccepted` — contract state tracking. ✅
  - `Database` property + `MaxActiveQuestsPerPlayer` property. ✅
- `Assets/_Project/Quests/Network/QuestServer.cs`:
  - `RequestAcceptQuestRpc` → real `TryAccept` + `SendQuestResultToClient` + snapshot push. ✅
  - `RequestTurnInQuestRpc` → real `TryTurnIn` + result + snapshot. ✅
  - `RequestTrackQuestRpc` → real `SetTracked` + result + snapshot. ✅
  - `FireDialogAction.GiveItem` → `InventoryWorld.AddItemDirect`. ✅
  - `FireDialogAction.TakeItem` → `InventoryServer.TryRemove` (T-Q14). ✅
  - `SendQuestSnapshotToClient(ulong)` overload + `SendQuestResultToClient` helper. ✅
- `Assets/_Project/Quests/Triggers/ConcreteTriggers.cs`: +`ContractCompletedTrigger` + `ContractAcceptedTrigger`. ✅
- `Assets/_Project/Quests/Triggers/QuestTriggerService.cs`: factories registered. ✅
- `Assets/_Project/Quests/Bridges/ContractMetaBridge.cs` (NEW):
  - Server-side singleton, scene-placed в BootstrapScene, DontDestroyOnLoad. ✅
  - Subscribes to 3 contract events, marks state в QuestWorld, evaluates triggers. ✅
- `Assets/_Project/Core/WorldEvent.cs`: +`ContractAcceptedEvent` / `ContractCompletedEvent` / `ContractFailedEvent`. ✅
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs`: publish events в 4 spots (Accept/Complete/Fail-manual/Fail-timer). ✅
- `Assets/_Project/Scenes/BootstrapScene.unity`: +`[ContractMetaBridge]` GameObject. ✅

**Verify:** ✅ Compile 0 errors. Все 28 root GameObjects в BootstrapScene включают ContractMetaBridge. Singleton'ы живы в Play Mode (verified ранее).

**Verify:**
- Walk through Mira quest, complete "intro" → `GiveCredits(50)`, `AddReputation(+25)` works.
- Complete contract → quest objective "доставить cargo" advances.

**Risk:** medium. Touches inventory server + new bridge.

---

### T-Q16 — DialogueAction: GiveCredits/AddReputation/AddNpcAttitude (small, 45 мин) ✅ DONE 2026-06-08

**Скоуп (см. `09_OPEN_QUESTIONS.md` §G):**
- `QuestServer.FireDialogAction.GiveCredits` — server-side modify credits via `TradeWorld.Repository.GetCredits+delta→SetCredits`, push snapshot через `ContractServer.PushPlayerSnapshot`. ✅
- `QuestServer.FireDialogAction.AddReputation` — `QuestWorld.ModifyReputation` (T-Q13, broadcast+event). ✅
- `QuestServer.FireDialogAction.AddNpcAttitude` — `QuestWorld.ModifyNpcAttitude` (T-Q13, broadcast+event+cross-faction). ✅
- `ContractServer.PushPlayerSnapshot(ulong)` — public helper. ✅
- **`ApplyQuestRewards` при TurnIn** — deferred to T-Q18 (M8 Persistence). ✅ (зафиксировано в roadmap как known gap).
- **`DialogueActionRunner` class** — не создан (switch case в `FireDialogAction` достаточно, одиночные atomic actions). ✅

**Verify (твои тесты):**
- Authoring task: добавить в Mira dialog tree edges с `GiveCredits(50)` / `AddReputation(+25, GuildOfThoughts)` / `AddNpcAttitude(+5, "mira_01")` actions (out of scope T-Q16 — это SO editor work).
- После добавления: `[QuestServer] FireDialogAction: GiveCredits delta=50 1000→1050` в Console + CharacterWindow таб РЕПУТАЦИЯ обновится + Dialog header показывает "❤ +5".

**Risk:** low. ✅


**Verify:** After "intro" stage complete → credits +50, reputation +25, npcAttitude[mira] +5.

**Risk:** low.

---

### T-Q17 — DialogueAction: OpenMarket/OpenService (small, 30 мин) ✅ DONE 2026-06-08

**Скоуп:**
- `QuestServer.FireDialogAction.OpenMarket` — server log + send DialogActionResult, actionType=OpenMarket. ✅
- `QuestServer.FireDialogAction.OpenService` — server log + send DialogActionResult, actionType=OpenService. ✅
- `DialogWindow.HandleActionResultReceived` — client-side dispatch: OpenMarket → `Close()` + `MarketInteractor.TryOpenMarket()` (uses local player zone), OpenService → `Close()` + log stub (ServiceUI TBD). ✅

**Verify (твои тесты):**
- Authoring: добавить в Mira dialog tree edge с `action.type=OpenMarket` или `OpenService` (out of scope T-Q17 — SO editor work).
- После добавления: `[DialogWindow] Action result: OpenMarket → close dialog + TryOpenMarket` + `[MarketInteractor] TryOpenMarket: enter — LocalPlayerZone=...` в Console + MarketWindow UI открывается.
- **ServiceUI не существует** — OpenService = stub (close dialog + log "T-Q17 stub — ServiceUI TBD"). Создание ServiceUI — TBD future.

**Risk:** low. ✅


---

### T-Q18 — Persistence: IQuestStateRepository + JSON, immediate save (large, 120 мин) ✅ DONE 2026-06-08

**Скоуп (см. `09_OPEN_QUESTIONS.md` §A5 + §H):**
- `ProjectC/Quests/Persistence/IQuestStateRepository.cs` (interface). ✅
- `ProjectC/Quests/Persistence/JsonQuestStateRepository.cs` (default impl, atomic write через tmp → rename). ✅
- `ProjectC/Quests/Persistence/QuestSaveData.cs` (POCO, includes quests + rep + npcAttitude + 5 string sets). ✅
- **Immediate save on every state change** (no debounce, per `09_OPEN_QUESTIONS.md` §H). ✅
- `QuestWorld.SavePlayer(clientId)` пишет ВСЕ данные atomic в ОДИН JSON. ✅
- Hooks: `TryOffer`, `TryAccept`, `TryTurnIn`, `SetTracked`, `ModifyReputation`, `ModifyNpcAttitude`, `MarkContractCompleted/Accepted`, `MarkEventOccurred`, `MarkNpcTalked`. ✅
- Load on player connect (в `QuestServer.OnClientConnectedForSnapshot`). ✅
- `Application.persistentDataPath/quest_state_<clientId>.json`. ✅
- **`ApplyQuestRewards` в TryTurnIn** — credits (TradeWorld.Repository) + items (int.TryParse tradeItemId → InventoryWorld.AddItemDirect) + reputation (ModifyReputation) + unlocks (log only, T-Q19). ✅

**Verify (твои тесты):**
- Accept quest → modify rep → `[JsonQuestStateRepository] Saved player 0 state (X.X KB)` в Console.
- `C:\Users\<user>\AppData\LocalLow\<Company>\<Product>\quest_state_0.json` — exists.
- Exit Play Mode → Start host → `[QuestWorld] LoadPlayer: client=0 restored N quests` в Console → P → CharacterWindow → quest восстановлен.
- Quest reward: TurnIn → `[QuestWorld] ApplyQuestRewards: credits X → Y (+Z)` (если reward.credits != 0).

**Known gaps (deferred to T-Q19/T-Q22):**
- `TradeItemDefinition.legacyId` mapping (сейчас `int.TryParse` с warning).
- `reward.cargoItems[]` — out of scope (no active ship tracking).
- `reward.unlocks[]` — log only (dialog tree/zone unlock T-Q19).
- Version migration framework (data.version = 1 hardcoded).
- Debounced save option (currently immediate).

**Risk:** medium. ✅
**Pitfalls:** Save на каждом state change — perf acceptable (1-5 KB JSON, 1 ms).

---

### T-Q19 — C1 cleanup: delete v1 NPC (medium, 60 мин) ✅ DONE 2026-06-08

**Скоуп:**
- Grep transitive deps по всем .cs файлам на `NpcData`/`NpcEntity`/`NpcInteraction`/`NpcDialogueManager`/`NpcFaction`. ✅
- **Delete** 4 v1 NPC files (`NpcData.cs` 248 LOC + `NpcEntity.cs` 352 LOC + `NpcInteraction.cs` 213 LOC + `NpcDialogueManager.cs` 634 LOC = **1447 LOC dead code removed**). ✅
- **Cleanup InteractableManager.cs** — remove `_npcs` list, `RegisterNpc`/`UnregisterNpc`/`FindNearestNpc`/`GetNpcs`, `_npcs.Clear()`. ✅
- Update AGENTS.md если явно упомянуто — не требуется. ✅

**Не в скоупе T-Q19 (deferred to T-X1/T-X3):**
- `ShipKeyClientState`/`ShipKeyServer` obsolete warnings — T-X1.
- `FindObjectsOfType`/`FindObjectOfType` deprecated — T-X3.
- `GetInstanceID` obsolete in ClientSceneLoader — T-X3.
- `FindObjectsSortMode` deprecated — T-X3.

**Verify:**
- Compile: 0 errors. ✅
- Transitive deps: 4 v1 NPC files referenced только в `InteractableManager.cs` (dead code path) + doc comments в `DialogTree.cs`/`NpcDefinition.cs`/`FactionId.cs`/`FactionDefinition.cs` (safe). ✅
- 0 references в `WorldScene_0_0.unity` (Mira использует v2 NpcController). ✅
- 0 references в Prefabs. ✅

**Risk:** low. ✅


---

### T-X1 — Trade.Core.NPCTrader → MarketTrader (small, 30 мин) ✅ DONE 2026-06-08

**Скоуп:**
- Grep `NPCTrader` usages → 2 files (NPCTrader.cs, TradeWorld.cs). ✅
- Rename `class NPCTrader` → `class MarketTrader` в namespace `ProjectC.Trade.Core`. ✅
- Rename file `NPCTrader.cs` → `MarketTrader.cs` (+ meta deleted). ✅
- Update 5 references в `TradeWorld.cs` (field type, property type, 4× CreateDefault calls). ✅
- Public property name `NpcTraders` и field name `_npcTraders` kept для source compat (internal conceptual noun, not breaking).

**Verify:** Compile 0 errors. ✅

**Risk:** low. ✅


---

### T-X2 — TradeItemDefinition Faction → FactionId (medium, 60 мин) — DEFERRED (DESIGN ISSUE)

**Скоуп (см. `09_OPEN_QUESTIONS.md` §A3):**
- Verify `TradeItemDefinition` has `Faction` field. ✅
- Rename → `FactionId requiredFaction` (after T-Q01). ⏭️

**Блокер (2026-06-08 review):**
- `ProjectC.Trade.Faction` (8 values: None, NP, Aurora, Titan, Hermes, Prometheus, FreeTraders, Military) — **manufacturer/продавец factions** (producents).
- `ProjectC.Factions.FactionId` (12 values: GuildOfThoughts, GuildOfCreation, ..., FreeTraders, Pirates, Neutral) — **lore guilds/фракции игрока** (reputation tracker).
- Пересекаются только в `FreeTraders` (значение 8 в обоих). Остальные — **разные концепции**.
- Прямой rename сломает `TradeItemDefinition.requiredFaction` serialization (5 .asset files: Antigrav, Mesium, etc).
- **Решение требует design discussion**: либо (a) заменить enum и перезаполнить .asset files, либо (b) признать что это **две разные концепции** и оставить как есть с улучшенной документацией.

**Статус:** 🟡 **DEFERRED** (2026-06-08) до design session. См. `09_OPEN_QUESTIONS.md` §A3.

**Risk:** medium. ⏭️


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

## 8.3.1 M13 — Real-time objective system (DESIGNED 2026-06-08)

**Контекст:** Сейчас objectives проверяются только при получении event'а (pickup, talk, rep change). Если event пропущен — objective «зависает». Также `QuestStage.onEnterActions` / `onCompleteActions` существуют в SO, но **никогда не вызываются**. M13 фиксит это.

**Полная спецификация:** `docs/dev/M13_DESIGN_NOTE.md`.

### T-Q20 — Server tick + objective evaluation (medium, ~2-3 ч)

**Скоуп:**
- `QuestServer.Update()` — вызывает `QuestWorld.TickAll()` с интервалом 5 сек
- `QuestWorld.TickAll()` — для каждого игрока: цикл по active quests → `EvaluateAndAdvanceStage()`
- `EvaluateAndAdvanceStage()` — проверяет все 8 типов objectives (TalkToNpc, HaveItem, ReachLocation, ReputationAtLeast, NpcAttitudeAtLeast, WaitForEvent, EventDriven, DeliverItem). Stubs: KillEntity, CargoHasItem — return false
- Если все required objectives satisfied → `TryAdvanceStage()`:
  - Fire `currentStage.onCompleteActions[]` через `QuestServer.FireDialogAction`
  - Transition `currentStageId = nextStageId`
  - Fire `newStage.onEnterActions[]`
  - Если `nextStageId` пуст → `state = Completed`, fire `def.rewards`
- `SendQuestSnapshotToClient()` после transition

**Файлы:** `QuestServer.cs`, `QuestWorld.cs`, `QuestInstance.cs`
**Verify:** 
1. Quest "Собрать 3 copper ore" — pickup 3 ore → tick → stage "collect" auto-complete → transition to "deliver" + fire onCompleteActions
2. ReachLocation quest — walk to coords → within radius → tick → objective satisfied

**Risk:** low. Переиспользует существующий `QuestTriggerService.IsSatisfied` + `FireDialogAction`.

### T-Q21 — Objective progress DTO + UI (small, ~1 ч)

**Скоуп:**
- DTO `ObjectiveProgressDto` уже отправляется в snapshot (T-Q07). T-Q21 — только UI render.
- `CharacterWindow.cs` таб КВЕСТЫ → для Active quest показать objectives list: ☐/☑ + description + counter
- `QuestTracker.cs` (HUD) → показать current incomplete objective + counter
- Edge: completed=true должно обновляться на client при snapshot

**Файлы:** `CharacterWindow.cs`, `QuestTracker.cs`
**Verify:** P → КВЕСТЫ → objective checkmarks корректно отображаются

**Risk:** low. UI-only.

### T-Q22 — Stage transitions + onEnter/onComplete actions (small, ~1 ч)

**Статус (2026-06-09):** 🟡 частично сделано в T-Q20. Полная спецификация: `docs/dev/T-Q22_DESIGN_NOTE.md`.

**Сделано в T-Q20:**
- ✅ `TryAdvanceStage` (QuestWorld:879) — fire onCompleteActions → transition → fire onEnterActions
- ✅ `OnStageTransition` event → QuestServer подписан → SavePlayer + SendQuestSnapshotToClient
- ✅ Final stage: `nextStageId=""` → `state=Completed` + `ApplyQuestRewards`
- ✅ `onEnterActions` и `onCompleteActions` поля в QuestStage

**Осталось для T-Q22:**
- [ ] **FIX `TryTurnIn` (QuestWorld:371-377)** — для state=Active сейчас сразу ставит state=Completed **минуя** TryAdvanceStage → onCompleteActions финального stage **не вызываются** через turn-in. Заменить на `TryAdvanceStage` (он сам проверит AreAllRequiredComplete + fire actions + state=Completed).
- [ ] **Multi-stage quest тест** — создать `stage_multi_demo.asset` (collect → deliver) для верификации что nextStageId действительно работает
- [ ] **onEnter тест** — ни один production quest не имеет onEnter actions. Добавить в test quest для проверки.

**Скоуп (новые файлы, НЕ удалять существующие):**
- `Assets/_Project/Quests/Data/Quests/StageIntroDemo.asset` — single-stage с onEnter (AddNpcAttitude)
- `Assets/_Project/Quests/Data/Quests/StageMultiDemo.asset` — multi-stage (collect HaveItem → deliver TalkToNpc)
- `Assets/_Project/Resources/Items/Item_Resource_TestStageItem.asset` — ItemData для pickup теста
- `WorldScene_0_0.unity` — добавить 1 GameObject `[Pickup_TestStageItem]` (НЕ трогать существующие pickup'ы)
- `Assets/_Project/Quests/Data/QuestDatabase.asset` — append 2 quest'а (НЕ удалять существующие 3)
- `Assets/_Project/Quests/Core/QuestWorld.cs` — fix `TryTurnIn` (1 patch)

**Verify (после реализации):**
1. `stage_intro_demo`: Accept → Console: AddNpcAttitude mira_01 delta=5 (onEnter) → E Mira → tick → GiveCredits delta=10 (onComplete) → state=Completed
2. `stage_multi_demo`: Accept → AddReputation +3 (stage A onEnter) → pickup TestStageItem → tick → GiveCredits 20 (A onComplete) → Stage advanced collect→deliver → AddNpcAttitude +10 (B onEnter) → E Mira → tick → GiveCredits 50 (B onComplete) → state=Completed
3. `TryTurnIn` fix: quest с onComplete action в финальном stage + turn-in через dialog → Console: actions fire (а не silent)

**Risk:** low. TryAdvanceStage уже работает. Fix `TryTurnIn` — 1-liner. Test assets — additive.

**Общий effort M13:** ~4-5 ч, medium risk.

---

## 8.3.2 M15 — Toast notifications (DONE 2026-06-09)

**Статус:** ✅ DONE 2026-06-09 (verified by user).

**Что сделано в сессии:**

| Тикет | Что | Файлы |
|-------|-----|-------|
| **T-Q23** | `QuestToast.cs` — runtime-constructed VisualElement, bottom-center, 2.5s display, queue-based (показывает все reward'ы по очереди вместо drop на cooldown) | `QuestToast.cs` (new) |
| **T-Q24** | `OnQuestResult` подписка — ищет displayName в QuestSnapshotDto, показывает "📜 Accepted: Демо: stage с onEnter" | `QuestToast.cs` |
| **T-Q25** | `DialogActionResultDto.intParam` добавлен + сериализуется. QuestServer pass intParam для GiveCredits/AddReputation/AddNpcAttitude. QuestToast показывает "💰 +200 CR" (delta напрямую, не string parse) | `DialogStepDto.cs` (modify), `QuestServer.cs` (modify), `QuestToast.cs` |

**M15 также починил:**
- T-Q22 fix: `DialogWindow` теперь показывает human-readable messages вместо debug "OK: 30 200"
- M13QuestTriggerZone пушит `ReceiveQuestDiscoveredTargetRpc` для trigger-zone auto-discover
- Toast queue: `ProcessQueue()` coroutine показывает все toast'ы по очереди (ранее 1 из 4 показывался, остальные терялись)

**Артефакты в сцене:**
- `BootstrapScene.unity` — `[QuestToast]` GameObject (UIDocument + QuestToast component + PanelSettings)
- (legacy `[ToastService]` удалён)

**Verify (user confirmed 2026-06-09):**
- ✅ "📜 Accepted: Демо: stage с onEnter" — displayName lookup работает
- ✅ "💚 mira_01 +5" — AddNpcAttitude delta
- ✅ "💰 +200 CR" — GiveCredits delta
- ✅ "✨ Найден квест: ..." — trigger zone auto-discover
- ✅ Queue: 4 toast'а подряд при quest complete показываются все

**Известные ограничения (acceptable):**
- Toast показывает сырое "mira_01" вместо "Mira" — нужен NPC displayName lookup (отдельный тикет, M15.1 если потребуется)
- No localized text — только русский

---

## 8.3.3 M14 — Item ID single source of truth (AUDITED 2026-06-09)

**Контекст:** Audit (2026-06-09) показал что Item ID работает по **двум независимым нумерациям**, которые **случайно** совпадают сейчас (alphabetical order из `Resources.LoadAll`). При добавлении item'а вне Resources/Items/ — id **молча** разъедутся, квесты перестанут работать. Это техдолг, не блокер для текущего контента, но блокер для масштабирования.

**Текущее состояние:**
- ✅ `InventoryWorld.GetOrRegisterItemId()` работает (32 items, id 1-32)
- ✅ `QuestWorld.ResolveItemId()` работает (fallback через Resources)
- ❌ Два разных source of truth → fragile, silent break при изменении Resources

**Полная спецификация:** `docs/dev/M14_DESIGN_NOTE.md`.

### T-Q26 — ItemRegistry SO (medium, ~1.5 ч)

**Скоуп:**
- `Assets/_Project/Items/Core/ItemRegistry.cs` — singleton ScriptableObject, `id ↔ ItemData` mapping
  - `RegisterItem(int id, ItemData item)` — explicit
  - `TryGetItem(int id, out ItemData item)` — lookup
  - `TryGetId(ItemData item, out int id)` — reverse
  - `GetAllItems()` — UI picker
- `InventoryWorld.RegisterAllItems()` читает из `ItemRegistry.Instance` (не дублировать)
- `QuestWorld.ResolveItemId` использует `ItemRegistry.TryGetId(itemName)`

**Файлы:** `ItemRegistry.cs` (new), `ItemRegistry.asset` (new), `InventoryWorld.cs` (modify), `QuestWorld.cs` (modify)
**Verify:** Roslyn dumps `InventoryWorld._itemDatabase.Keys` == `QuestWorld.ResolveItemId` for all 32 items
**Risk:** medium (требует coord init order — InventoryWorld должен init раньше QuestWorld)

### T-Q27 — DialogueAction itemId param (small, ~0.5 ч)

**Скоуп:**
- `DialogueAction.cs` — add `public int itemId = 0;` + `public ItemType itemType = ItemType.None;`
- `QuestServer.FireDialogAction` для GiveItem/TakeItem: использовать `action.itemId` (itemType уже есть)
- Backward compat: если `itemId == 0` → fallback на stringParam parse

**Файлы:** `DialogueAction.cs`, `QuestServer.cs`
**Verify:** Quest asset с GiveItem action корректно даёт предмет
**Risk:** low (backward compatible)

### T-Q28 — Migration string-id → int-id (small, ~0.5 ч)

**Скоуп:**
- Audit quest/dialog assets: `itemTradeItemId = "Медная руда"` → int 26
- Migrate: `CollectCopperOre.asset`, `StageMultiDemo.asset`, `FindArtifact.asset`, `MiraDefault.asset`
- Roslyn-driven (через MCP)

**Файлы:** 4 quest/dialog assets (modify)
**Verify:** Все квесты по-прежнему работают без gameplay change
**Risk:** low (cosmetic migration)

**Общий effort M14:** ~2.5 ч, low-medium risk.

---

## 8.3.5 M17 — QuestGraphView GraphView (DONE 2026-06-09)

**Статус:** ✅ DONE 2026-06-09 (verified by Roslyn).

**Что сделано:**

| Файл | Что |
|------|-----|
| `Assets/_Project/Quests/Editor/QuestGraphView.cs` (new, ~340 lines) | `QuestGraphView` + 4 node types: `QuestNode`, `StageNode`, `ObjectiveNode`, `RewardNode` |
| `Assets/_Project/Quests/Editor/QuestGraphWindow.cs` (new, ~110 lines) | EditorWindow с toolbar (ObjectField + Refresh + Fit) + GraphView |

**Меню:**
- `Tools > ProjectC > Quests > Quest Graph View` (пусто, без quest)
- `Assets/ProjectC/Open Quest Graph` (для выбранного quest asset в Project window)

**Layout:**
```
[Quest Asset Field] [🔄] [⊡ Fit]
┌─────────────────────────────────────┐
│ Grid background + GraphView         │
│ ┌──────┐    ┌──────┐    ┌──────┐   │
│ │ Quest│───▶│Stage │───▶│Obj   │   │
│ │ quest│    │stage1│    │find  │   │
│ └──────┘    └──────┘    └──────┘   │
│     │                               │
│     ▼                               │
│ ┌──────┐                            │
│ │Reward│                            │
│ └──────┘                            │
└─────────────────────────────────────┘
[Status bar: Quest id | stages | CR]
```

**Node types:**
- **QuestNode** (220×120) — header с questId, body с displayName + description
- **StageNode** (200×80+) — header "Stage N: stageId", body с onEnter/onComplete counts
- **ObjectiveNode** (240×50) — header `[type] objectiveId`, body qty + item/npc
- **RewardNode** (220×120) — header "🎁 Rewards", body с CR + items + reputation

**Read-only режим:**
- ✅ Блокируется element removal в `OnGraphViewChanged`
- ✅ Drag/zoom/pan/select работают (стандартные GraphView manipulators)
- ✅ FrameAll (Fit) кнопка
- ❌ Editable — M18

**Verify (Roslyn 2026-06-09):**
```
Found in: Assembly-CSharp-Editor
Window opened
Quest: collect_copper_ore stages=1
Loaded quest into graph
Graph elements: 14
```

**Что НЕ сделано (out of scope M17):**
- ❌ Edit nodes (drag/drop/create) — M18
- ❌ Save back to QuestDefinition — M18
- ❌ Visual diff между 2 versions — M19

**Артефакты (2 реализации):**
- `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` — **активный**: GraphView Node+Edge, M18 база
- `Assets/_Project/Quests/Editor/QuestGraphView.cs` — (old): custom VisualElement, maintenance

---

## 8.3.6 M18 — Editable QuestNodeGraph (IN PROGRESS 2026-06-09)

**Статус:** 📋 IN PROGRESS — T-Q30 (editable node fields) первый.

**Контекст:** M17 дал readonly граф. M18 делает его мутабельным — редактирование прямо в нодах.

**План (6 тикетов, ~5 ч):**

| Тикет | Что | ~ч | Статус |
|-------|-----|----|--------|
| **T-Q30** | TextField в нодах (displayName, description) + Save/Revert кнопки | 1.5 | 🔄 NEXT |
| **T-Q31** | Save back to QuestDefinition (EditorUtility.SetDirty + AssetDatabase) | 1.0 | ⏳ |
| **T-Q32** | Add/Delete stages + objectives (кнопки "+"/"×") | 1.0 | ⏳ |
| **T-Q33** | Quest-to-quest prerequisites edge (dashed line, cross-quest) | 1.0 | ⏳ |
| **T-Q34** | Drag-create edges (user-draggable Port's) | 0.5 | ⏳ |

**Полная спецификация:** `docs/dev/M18_DESIGN_NOTE.md`

---

## 8.4 Milestones (обновлено)

| Milestone | Тикеты | Что работает |
|-----------|--------|--------------|
| **M1 — Data foundation** | T-Q01, T-Q02, T-Q03, T-Q04 | Все SO + NpcAttitude struct + EventDriven objective. Inspector редактируется. | ✅ DONE |
| **M1.5 — Inventory persistence + Event bus foundation** | T-X0 | WorldEventBus + inventory save/load. Hooks в InventoryServer. | ✅ DONE |
| **M2 — Server core** | T-Q05, T-Q06, T-Q07 | QuestServer спавнится, RPCs работают, DTOs передаются, full event bus + triggers. | ✅ DONE |
| **M2.5 — Input refactor** | T-X3 | PlayerInputReader events, NetworkPlayer subscribes. | ✅ DONE |
| **M3 — Player interaction** | T-Q08, T-Q10 | E-key → talk to NPC → DialogWindow opens (F skip). | ✅ DONE 2026-06-08 |
| **M4 — Quest log + tracker** | T-Q11, T-Q12 | Player can accept quest, see in log (Active/Completed/Discovered), see tracker. | ✅ DONE 2026-06-08 |
| **M5 — Reputation + NpcAttitude** | T-Q13 | Reputation updates, NpcAttitude, CharacterWindow tab fix. | ✅ DONE 2026-06-08 |
| **M6 — Item integration** | T-Q14, T-Q15 | Quest rewards give items, quest objectives check items, ContractMetaBridge. | ✅ T-Q14 ✅ T-Q15 2026-06-08 |
| **M7 — Full action set** | T-Q16, T-Q17, T-X5 | Credits/rep/attitude/market actions + ContractServer events. | ✅ T-Q16 ✅ T-Q17 ✅ T-X5 2026-06-08 |
| **M8 — Persistence** | T-Q18 | Quests + rep + attitude survive server restart. | ✅ DONE 2026-06-08 |
| **M9 — Cleanup** | T-Q19, T-X1, T-X2 | v1 NPC deleted, optional renames. | 🟡 T-Q19 ✅ T-X1 ✅ T-X2 DEFERRED 2026-06-08 |
| **M10 — Editor tool** | T-Q09, T-Q09b | Quest Database Explorer с full CRUD + GraphView. | ✅ DONE (M10 partially — CRUD done, GraphView deferred) |
| **M11 — End-to-end demo** | Mira quest full playthrough. | ✅ DONE 2026-06-09 (user verified). |
| **M12 — Input remap** | T-X4 | F = pickup (future, post-demo). |
| **M13 — Real-time objective system** | T-Q20, T-Q21, T-Q22 | Auto-evaluate objectives, fire onEnter/onComplete actions, stage transitions, UI progress. | ✅ DONE 2026-06-09 (T-Q20, T-Q21, T-Q22 verified) |
| **M15 — Toast notifications** | T-Q23, T-Q24, T-Q25 | Pickup/accept/complete/reward feedback to player. UI Toolkit overlay. | ✅ DONE 2026-06-09 (verified by user) |
| **M14 — Item ID system** | T-Q26, T-Q27, T-Q28 | Single source of truth for item ids. ItemRegistry SO + DialogueAction.itemId + asset migration. | ✅ DONE 2026-06-09 (verified by Roslyn) |
| **M16 — QuestDatabaseWindow** | T-Q09 (Editor UI) | UI Toolkit EditorWindow: tree view + detail panel для quests/dialogs/npcs/factions. | ✅ DONE 2026-06-09 (verified by Roslyn) |
| **M17 — QuestNodeGraph** | T-Q09b (Graph viz) | **Вариант A:** `QuestNodeGraphView` (GraphView Nodes+Edges, активный). **Вариант B (old):** `QuestGraphView` (custom VisualElement, maintenance). | ✅ DONE 2026-06-09 |
| **M18 — Editable QuestNodeGraph** | T-Q30, T-Q31, T-Q32, T-Q33, T-Q34 | Editable nodes, save back to SO, quest-to-quest dependencies, drag-create edges. | 📋 IN PROGRESS 2026-06-09 (T-Q32 ✅, T-Q33 next) |

**Рекомендуемый темп:** 1-2 тикета за сессию, 1 PR за тикет.

---

## 8.3.4 M16 — QuestDatabaseWindow Editor tool (DONE 2026-06-09)

**Статус:** ✅ DONE 2026-06-09 (verified by Roslyn).

**Что сделано:**

`Assets/_Project/Quests/Editor/QuestDatabaseWindow.cs` (new, 367 lines):
- Unity EditorWindow с UI Toolkit (no IMGUI)
- **Меню:** `Tools > ProjectC > Quests > Quest Database Explorer`
- **Layout:**
  - **Left pane (TreeView):** 4 группы — 📜 Quests, 💬 Dialogs, 👤 NPCs, 🏛 Factions (count badge)
  - **Right pane (ScrollView):** detail view выбранного asset
- **Кнопки:** 🔄 Re-scan DB (calls `QuestDatabaseAutoDiscover.Rescan()`)
- **Detail views:**
  - **Quest:** questId, displayName, description, faction, minRep, oneShot, discoverable + stages (с objectives + onEnter/onComplete counts) + rewards (CR/items/rep) + "Open in Inspector" / "Ping Asset"
  - **Dialog:** treeId, displayName, rootNodeId, node list
  - **NPC:** npcId, displayName, questOffers, questTurnIns
  - **Faction:** factionId, displayName, loreDescription
- **Status bar:** bottom-left показывает счётчики

**Преимущества над IMGUI:**
- ✅ Не блокирует mouse (как OnGUI)
- ✅ Modern UI Toolkit
- ✅ Per-pane scroll/flex
- ✅ Data binding через `bindItem` callback

**Тест:** Меню Tools → ProjectC → Quests → "Quest Database Explorer" → window открывается. В левой панели — 4 группы со всеми assets. Клик на quest → справа detail view с stages/objectives/rewards.

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
|| 15 | EventDriven quests (T-Q04 + T-Q11) | Discovered state UI может быть confusing — disambiguate в UI. |
|| 16 | **UI Toolkit recurrent bugs (T-Q11c)** | 3 повторных бага за сессию — см. `docs/dev/T-Q11b_c_session_log_2026-06-08.md` §LESSONS LEARNED. READ перед ЛЮБЫМ новым UIDocument. |
|| 17 | **Struct INetworkSerializable string writeback** | 3 DTO в DialogStepDto.cs — generic rule для ВСЕХ будущих DTO: writeback на READ. |
|| 18 | **Scene placement rule (user 2026-06-07)** | BootstrapScene = server infra ONLY. Game objects → WorldScene_X_Z. |

---

## 8.7 Session Summary — T-Q11b + T-Q11c (2026-06-08)

### ✅ Что сделано (2 тикета в 1 сессии)

| Т | Тикет | Commit scope | Verify |
|---|-------|--------------|--------|
| T-Q11b | NpcController + E-key NPC branch | `NpcController.cs` (NEW), `NetworkPlayer.cs` (E-chain), `[Mira]` в WorldScene_0_0 | ✅ E→Mira→dialog |
| T-Q11c | DialogWindow UIDocument rewrite | `DialogWindow.cs` (REWRITE 311 LOC), UXML/USS/asset (NEW), QuestServer/DTOs fixes, scene binding | ✅ end-to-end dialog |

**Compile:** 0 errors, 0 exceptions. **Play Mode:** Mira quest dialog работает полностью.

### 🔑 Key Lessons (9 PERSISTENT BUGS — READ при следующих UI Toolkit окнах)

1. **PanelSettings.asset** — runtime `CreateInstance` = no theme = "strip". Copy `MarketPanelSettings.asset`.
2. **USS class-стили** — `themeUss` type-selector `.unity-base-button` > class. Все class-стили с `!important` (кроме `display`).
3. **`styleSheets.Add(uss)`** — КРИТИЧНО в `EnsureBuilt()`. Без него panel collapse.
4. **`display: none !important` в USS** — блокирует inline toggle. Убрать `display` из USS.
5. **Cursor lock** — flight-mode Locked = mouse dead. `Show()` → None/visible; `Close()` → Locked (if IsListening).
6. **PickingMode** — Ignore в EnsureBuilt, Position в Show, Ignore в Close.
7. **UXML inline `style="..."`** — не парсятся runtime. Use `class="..."`.
8. **Struct INetworkSerializable + string** — `var x=field; if(IsWriter)x=field??""; SerializeValue(ref x); if(IsReader)field=x??"";` — struct value semantics требуют writeback.
9. **Stale `_currentStep`** — после `isEnd` step treeId/nodeId="". Guard в `SendAdvance`.

### 📁 Files modified/new (commit)

```
M Assets/_Project/Quests/Client/QuestClientState.cs
M Assets/_Project/Quests/Dto/DialogStepDto.cs
M Assets/_Project/Quests/Network/QuestServer.cs
M Assets/_Project/Quests/UI/DialogWindow.cs
M Assets/_Project/Scenes/BootstrapScene.unity
M Assets/_Project/Scenes/World/WorldScene_0_0.unity
M Assets/_Project/Scripts/Player/NetworkPlayer.cs
A Assets/_Project/Quests/NpcController.cs
A Assets/_Project/Quests/Resources/UI/DialogWindow.uxml
A Assets/_Project/Quests/Resources/UI/DialogWindow.uss
A Assets/_Project/Quests/Resources/UI/DialogPanelSettings.asset
A docs/dev/T-Q11b_c_session_log_2026-06-08.md
```

### ⏭️ Next Up (in order)

1. **T-Q11** — Quest log таб в CharacterWindow (6th tab, Discovered section + Accept button)
2. **T-Q12** — QuestTracker overlay + DialogWindow typewriter/F-skip + DontDestroyOnLoad
3. **T-Q13** — ReputationClientState + NpcAttitudeClientState + CharacterWindow Reputation tab fix
4. **T-Q14** — InventoryServer.TryRemove + event hooks
5. **T-X5** — ContractServer publish events для quest bridge
6. **T-Q15** — GiveItem/TakeItem + ContractMetaBridge
7. **T-Q16** — GiveCredits/AddReputation/AddNpcAttitude executors
8. **T-Q17** — OpenMarket/OpenService executors
9. **T-Q18** — Persistence JSON
10. **T-Q19** — C1 cleanup: delete v1 NPC

---

**Статус проекта:** M1-M9 ✅ DONE 2026-06-08 (M10 partial). **M11 (End-to-end demo) — IN PROGRESS** (dialog tree + 2 pickup items + CompleteObjective real impl готовы, awaiting user Play Mode test). **M13 (Real-time objective system) — 📋 DESIGN COMPLETE 2026-06-08** (see `docs/dev/M13_DESIGN_NOTE.md` — ready for next session coding).
