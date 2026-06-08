# T-Q15 — DialogueAction.GiveItem/TakeItem + ContractMetaBridge + QuestWorld TryAccept/TurnIn/SetTracked (medium, 120 мин)

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-Q15 + T-X5 (merged)
**Связь:** 09_OPEN_QUESTIONS.md §A2, §G, §J, §L

## Решение

Roadmap явно указывает что **real impl `QuestWorld.TryAccept` будет в T-Q15** (roadmap line 321). Без него T-Q11 verify (end-to-end Accept) не закроется. T-X5 (ContractServer events) — roadmap ставит ДО T-Q15 (line 397), и T-Q15 ContractMetaBridge требует этих events. **Объединяю** T-X5 + T-Q15 в одну сессию.

## Скоуп (5 частей)

### Part 1: T-X5 — ContractServer publish events
**Файл:** `Assets/_Project/Trade/Scripts/Network/ContractServer.cs`

- `class ContractAcceptedEvent : WorldEvent { contractId, playerId, fromNpcId, timestamp }` — publish при Accept.
- `class ContractCompletedEvent : WorldEvent { contractId, playerId, timestamp, wasReceipt }` — publish при Complete.
- `class ContractFailedEvent : WorldEvent { contractId, playerId, timestamp, debtIncurred }` — publish при Fail.

Новые event classes в `Assets/_Project/Core/WorldEvent.cs` (рядом с ReputationChangedEvent).

Hook points — найти в ContractServer где Accept/Complete/Fail случаются → добавить `WorldEventBus.Publish`. **Grep transitive deps обязательно** чтобы не сломать существующее поведение.

### Part 2: QuestWorld.TryAccept / TryTurnIn / SetTracked
**Файл:** `Assets/_Project/Quests/Core/QuestWorld.cs`

- `public QuestResultDto TryAccept(ulong clientId, string questId, string fromNpcId = "")` — Discovered/Offered → Active. Валидация: state transition allowed (QuestStateTransition.Allowed), quest exists, cap на max active quests. Return Ok/Fail(InvalidState, QuestNotFound, MaxActive, ...).
- `public QuestResultDto TryTurnIn(ulong clientId, string questId, string toNpcId)` — Active → TurnedIn. Валидация: state Active, NPC can accept (через `QuestDefinition.questTurnIns`). **Out of scope:** применение rewards (T-Q15 reward part — пока stub, реальные reward applier в T-Q16+). Return Ok/Fail.
- `public QuestResultDto SetTracked(ulong clientId, string questId, bool track)` — toggle `isTracked` в snapshot.

Каждый метод возвращает `QuestResultDto`. **Bonus:** после успешного Accept/TurnIn → publish `OnQuestStateChanged` event (server-push через `NetworkPlayer.ReceiveQuestSnapshotTargetRpc` → `QuestClientState.OnQuestSnapshotReceived`).

### Part 3: QuestServer.RequestAcceptQuestRpc/RequestTurnInQuestRpc/RequestTrackQuestRpc
**Файл:** `Assets/_Project/Quests/Network/QuestServer.cs`

Заменить stub-логику на вызовы QuestWorld:
- `RequestAcceptQuestRpc` → `QuestWorld.Instance.TryAccept` → если Ok → `SendQuestSnapshotToClient` + `SendQuestResultToClient` (new helper, см. ниже).
- `RequestTurnInQuestRpc` → `QuestWorld.Instance.TryTurnIn` → broadcast.
- `RequestTrackQuestRpc` → `QuestWorld.Instance.SetTracked` → broadcast (snapshot push с обновлённым isTracked).

**New helper:** `SendQuestResultToClient(ulong, QuestResultDto)` — parallel `SendQuestSnapshotToClient` (TargetRpc to `NetworkPlayer.ReceiveQuestResultTargetRpc`).

**Verify NetworkPlayer has ReceiveQuestResultTargetRpc** — если нет, добавить.

### Part 4: ItemActionExecutor (DialogueAction.GiveItem/TakeItem)
**Файл:** `Assets/_Project/Dialogue/ActionExecutors/ItemActionExecutor.cs` (NEW)

- Pattern: `CreditsActionExecutor` (T-Q16 — но не существует yet). T-Q15 — первый action executor, задаёт pattern.
- API: `public static class ItemActionExecutor { public static QuestResultDto ExecuteGiveItem(ulong, DialogueAction); ... }`
- `DialogueAction.GiveItem` — `{ itemId, count, type }` → `InventoryServer.AddItem`.
- `DialogueAction.TakeItem` — `{ itemId, count, type }` → `InventoryServer.TryRemove`.
- Wires в `DialogueActionRunner` (T-Q15: создать если нет) — после edge action evaluation, executes all actions in sequence.

**Note:** T-Q15 — это первый Action executor. Если DialogueActionRunner не существует — это **additional scope**: создать `DialogueActionRunner.cs` в `Assets/_Project/Dialogue/`.

### Part 5: ContractMetaBridge
**Файл:** `Assets/_Project/Quests/Bridges/ContractMetaBridge.cs` (NEW)

- `class ContractMetaBridge : MonoBehaviour` — scene-placed в BootstrapScene.
- `Start()` — subscribe to WorldEventBus (ContractAcceptedEvent, ContractCompletedEvent, ContractFailedEvent).
- На каждый event → `QuestTriggerService.Evaluate(playerId, $"ContractCompleted:{contractId}")` etc.
- Это позволяет quest objective "доставить cargo в порт X" продвигаться при `ContractCompletedEvent`.

**Примечание:** Реальный `ContractCompletedTrigger` (через `QuestTriggerService`) — не часть T-Q15 (T-Q04 уже мог это добавить, проверю). Если trigger не существует → **доп. scope**: `QuestTriggerService.ContractCompletedTrigger` class.

## Файлы для изменения/создания

**Create (3):**
- `Assets/_Project/Quests/Bridges/ContractMetaBridge.cs`
- `Assets/_Project/Dialogue/ActionExecutors/ItemActionExecutor.cs`
- `Assets/_Project/Dialogue/DialogueActionRunner.cs` (если не существует)

**Edit (5):**
- `Assets/_Project/Core/WorldEvent.cs` — +3 event classes (ContractAccepted/Completed/Failed)
- `Assets/_Project/Quests/Core/QuestWorld.cs` — +TryAccept/TryTurnIn/SetTracked
- `Assets/_Project/Quests/Network/QuestServer.cs` — wire RPCs + SendQuestResultToClient
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — publish 3 events
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — ReceiveQuestResultTargetRpc (if missing)

**Scene (1):**
- `Assets/_Project/Scenes/BootstrapScene.unity` — +[ContractMetaBridge] GameObject

## Verify (end-to-end)

1. **Compile:** 0 errors.
2. **Play Mode test (host):**
   - Start host → `Mira_01` offers quest (через dialog) → в Console `OnQuestDiscovered` event.
   - P → CharacterWindow → таб КВЕСТЫ → Discovered под-секция содержит quest.
   - Нажать "ПРИНЯТЬ" → `RequestAcceptQuestRpc` → `QuestWorld.TryAccept` → state Discovered → Active.
   - Console: `[QuestServer] RequestAcceptQuest client=0 quest=...` + `[QuestWorld] TryAccept: client=0 quest=... OK`.
   - Snapshot push → CharacterWindow: "Активные" секция содержит quest. "Найденные" — пустая.
3. **Tracker test:** "Следить" → "Не следить" → `RequestTrackQuestRpc` → snapshot push.
4. **Contract test (если есть active contract):** `ContractServer.Accept` → `ContractAcceptedEvent` → `ContractMetaBridge` → `QuestTriggerService.Evaluate("ContractAccepted:...")`.

## Что НЕ делаем (deferred)

- **Reward application** (ApplyQuestRewards) — T-Q16+.
- **T-Q16 actions** (GiveCredits/AddReputation/AddNpcAttitude) — следующий тикет.
- **T-X5 verify через quest prerequisite** "completable только если contract B completed within last 24h" — это `QuestPrerequisite` extension, не часть T-Q15.
- **NpcDefinition.attitudeLinks** для Mira — T-Q13 follow-up (опционально).

## Открытые вопросы

1. **DialogueActionRunner существует?** Проверю. Если нет — создам (scope creep). Если да — просто wire.
2. **NetworkPlayer.ReceiveQuestResultTargetRpc существует?** Проверю. Если нет — добавлю (с QuestClientState.OnQuestResultReceived — уже есть).
3. **Reward application в TryTurnIn** — пропускаем? Или даём credits+rep stub (от T-Q16)? Решение: skip rewards в TryTurnIn, return state changed only. T-Q16 добавит apply rewards.
