# M13 — Real-Time Objective System

> **Дата:** 2026-06-08
> **Сессия:** M13 (after M11 demo verified)
> **Roadmap:** расширяет `08_ROADMAP.md` §8.4
> **Status:** 📋 DESIGN — ready for next session coding

---

## Проблема

Сейчас квест продвигается только через dialog action `CompleteObjective` или ручное обновление objective в `QuestWorld.TryAdvanceObjective`. Что это значит для игрока:

1. Квест "Собрать 3 дневника" — не продвигается пока игрок не вернётся к NPC и не кликнет "я сделал"
2. Квест "Дойти до лаборатории" — нет проверки позиции, надо сдать через диалог
3. Quest stages не переключаются автоматически — все required objectives должны быть вызваны через event
4. onEnterActions и onCompleteActions в QuestStage **никогда не вызываются** (существуют в SO, но исполнение не реализовано)

**Что работает event-driven:** pickup (HaveItem), talk to NPC, faction rep change, NPC attitude change, custom event, contract completed/accepted, DayNight phase.

**Что НЕ работает event-driven:** reach location, time elapsed, manual objective "I did this" (кроме CompleteObjective в диалоге).

---

## Решение: real-time tick + stage transition

Добавить серверный тик каждые N секунд, который проверяет objectives и продвигает стадии. Существующая event-driven инфраструктура остаётся — tick её дополняет для «медленных» типов (ReachLocation, WaitForEvent timeout, fallback HaveItem).

---

## Скоуп (3 тикета)

### T-Q20 — Server Tick + Objective evaluation

**Сложность:** medium (~2-3 ч)
**Файлы:** `QuestServer.cs`, `QuestWorld.cs`, `QuestInstance.cs`
**Зависимости:** нет (использует существующий QuestTriggerService)

**План:**

1. **В `QuestServer.cs` добавить `Update()`** — вызывает `QuestWorld.Instance?.TickAll()` с интервалом.

2. **В `QuestWorld.cs` добавить:**
   ```csharp
   public float tickInterval = 5f; // seconds
   private float _tickAccumulator = 0f;
   
   public void TickAll()
   {
       _tickAccumulator += Time.deltaTime;
       if (_tickAccumulator < tickInterval) return;
       _tickAccumulator = 0f;
       
       foreach (var clientId in _questsByPlayer.Keys)
           TickPlayer(clientId);
   }
   
   public void TickPlayer(ulong clientId)
   {
       if (!_questsByPlayer.TryGetValue(clientId, out var quests)) return;
       for (int i = 0; i < quests.Count; i++)
       {
           var inst = quests[i];
           if (inst.state != QuestState.Active) continue;
           var def = GetQuest(inst.questId);
           if (def == null) continue;
           
           // Evaluate ALL objectives в current stage
           if (EvaluateAndAdvanceStage(clientId, inst, def))
           {
               // Stage transitioned; snapshot to client
               QuestServer.NotifyStageTransition?.Invoke(clientId, inst);
           }
       }
   }
   ```

3. **`EvaluateAndAdvanceStage`** — для каждого required objective:
   - TalkToNpc → `HasNpcTalkedTo(clientId, obj.targetNpcId)`
   - HaveItem → `InventoryWorld.CountOf(clientId, intId) >= obj.requiredQuantity`
   - ReachLocation → `Vector3.Distance(playerPos, obj.targetPosition) < obj.targetRadius`
   - ReputationAtLeast → `GetReputation(clientId, obj.targetFaction) >= obj.reputationValue`
   - WaitForEvent → `HasEventOccurred(clientId, obj.eventId)`
   - EventDriven → same as WaitForEvent
   - DeliverItem → `CountOf(...) >= requiredQuantity`
   - KillEntity → stub (returns false until combat system)
   - CargoHasItem → stub (returns false)
   - LocationReached → already covered by ReachLocation

4. **Если все required objectives satisfied** → `TryAdvanceStage(clientId, questId)`:
   - Fire `onCompleteActions[]` через существующий `FireDialogAction` путь (если есть в QuestServer)
   - Если `nextStageId` есть → переход, fire `onEnterActions[]` новой stage
   - Если `nextStageId` пуст → квест в состояние Completed, fire `QuestDefinition.rewards`

5. **Snapshot push** после каждого stage transition через `SendQuestSnapshotToClient`.

**Edge case:** tick во время server shutdown — graceful handle (skip if !IsServer).

**Edge case:** tick во время loading scene — skip если NetworkPlayer не в зоне.

**Performance:** O(active_quests × objectives_per_stage). 50 игроков × 5 активных квестов × 5 objectives = 1250 checks per 5s = 250/s. OK.

---

### T-Q21 — Objective progress DTO + UI

**Сложность:** small (~1 ч)
**Файлы:** `QuestClientState.cs`, `CharacterWindow.cs`, `QuestTracker.cs`

**План:**

1. **`ObjectiveProgressDto`** уже существует (`QuestProgressDto.cs`). Используется в `BuildQuestSnapshot` (line 535+). Проверить что:
   - `completed` правильно сериализуется (true когда objective satisfied)
   - `currentValue` показывает `CountOf(itemDataId)` для HaveItem

2. **`CharacterWindow.cs` — таб КВЕСТЫ** — для каждого Active quest показать objectives с checkmark:
   ```
   ☐ Собрать 3 дневника (1/3)
   ☑ Поговорить с Мирой
   ```

3. **`QuestTracker.cs` (HUD)** — показать текущий objective + counter.

4. **`QuestClientState.cs`** — event `OnObjectiveProgressUpdated(clientId, questId, objectiveId, completed)`. Server RPC не нужен — клиент сам парсит snapshot.

**Примечание:** `ObjectiveProgressDto` уже отправляется в snapshot, но UI его не рендерит. T-Q21 — это чисто UI-обновление.

---

### T-Q22 — Stage transitions + onEnterActions/onCompleteActions

**Сложность:** small (~1 ч)
**Файлы:** `QuestWorld.cs`, `QuestServer.cs`

**План:**

1. **В `QuestWorld.TryAdvanceStage(clientId, questId)`:**
   - Найти instance, def, current stage
   - **Сначала** fire `currentStage.onCompleteActions[]` (atomic, server-side, через реюз QuestServer.FireDialogAction)
   - Затем transition: `instance.currentStageId = def.GetStage(currentStage.nextStageId).stageId`
   - **Потом** fire `newStage.onEnterActions[]`
   - **Если nextStageId пуст** → state = Completed → fire `def.rewards` через ApplyQuestRewards

2. **Существующий `TryTurnIn` уже делает final stage → TurnedIn** — добавить ему вызов onCompleteActions.

3. **Hook для snapshot push** — `QuestServer` подписывается на transition events и вызывает `SendQuestSnapshotToClient`.

**Reuse:** `QuestServer.FireDialogAction` уже generic для всех action types (OfferQuest, TakeItem, GiveCredits, AddReputation, AddNpcAttitude, CompleteObjective). Просто вызвать его в цикле для массива actions.

---

## Дорожная карта M13

```
T-Q20 (Server Tick + Objective eval)        [средний, 2-3 ч]
    ↓
T-Q21 (Objective progress DTO + UI)         [маленький, 1 ч]
    ↓
T-Q22 (Stage transitions + actions)         [маленький, 1 ч]
    ↓
M13 verify (user Play Mode)
```

**Total: ~4-5 ч**, medium risk.

---

## Тест-план (M13)

### 1. Real-time objective eval (T-Q20)

**Кейс:** Quest "Собрать 3 copper ore" с objective `HaveItem(itemId=1, qty=3)`.

1. Start host
2. Accept quest → Active stage "collect"
3. Console: `TryAdvanceObjective` срабатывает при pickup (event-driven)
4. Pickup 1st ore → tick → 1/3
5. Pickup 2nd → 2/3
6. Pickup 3rd → 3/3 → stage "collect" complete → transition to "deliver" → fire `onCompleteActions` (AddReputation) + `onEnterActions` ("deliver" stage)
7. Console: `[QuestWorld] Stage advanced: quest=collect_copper stage=collect→deliver`
8. P → КВЕСТЫ → check: 3 objectives в "collect" все с completed=true, "deliver" objectives (не visited)
9. P → РЕПУТАЦИЯ → +5 GuildOfThoughts (от onCompleteActions)
10. P → КВЕСТЫ → quest moves to "Завершённые" после TurnIn dialog

**Edge case:** Cargo objectives, Kill objectives — должны вернуть false, не крашить.

### 2. ReachLocation objective (T-Q20)

**Кейс:** Quest "Дойти до координат (50000, 2500, 50000)" с objective `ReachLocation(targetPosition, radius=100)`.

1. Accept quest → Active stage "travel"
2. Walk to coordinates (или используй teleport)
3. Within 100m radius → tick (5s) → objective satisfied → stage complete
4. Console: `[QuestWorld] ReachLocation satisfied: dist=87m < 100m`

**Edge case:** If player far — stays unsatisfied, ticks don't crash.

### 3. Stage transitions + actions (T-Q22)

**Кейс:** Quest stage "intro" with `onCompleteActions: [GiveCredits(50)]`, next stage "main" with `onEnterActions: [EmitEvent("quest_started_main")]`.

1. Accept quest → stage "intro"
2. Complete intro objective (например, TalkToNpc)
3. Console should show:
   - `[QuestWorld] FireDialogAction: GiveCredits delta=50 0→50`
   - `[QuestWorld] FireDialogAction: EmitEvent quest_started_main`
   - `[QuestWorld] Stage advanced: intro → main`
4. P → КВЕСТЫ → currentStageId = "main"
5. WorldEvent listener (test) видит "quest_started_main" event

### 4. UI rendering (T-Q21)

1. CharacterWindow → КВЕСТЫ таб → Active quest → list objectives
2. Each objective: ☐ or ☑ icon + description + counter (e.g. "1/3")
3. QuestTracker HUD: shows current quest's first incomplete objective
4. After pickup → snapshot update → UI refreshes within 1 tick (5s) or on snapshot push

---

## Не делаем (out of scope M13)

- **Тосты/уведомления** (M15) — "Квест получен" notifications
- **QuestDatabaseWindow** (M16) — IDE editor
- **GraphView** (M17) — visual dialog editor
- **ItemRegistry** (M14) — string↔int conversion
- **T-Q09b (GraphView)** — DEFERRED
- **Cargo objectives (CargoHasItem)** — STUB, нет cargo system
- **Combat objectives (KillEntity)** — STUB, нет combat system

---

## Связь с существующими тикетами

| Тикет | Связь |
|-------|-------|
| T-Q15 (Accept/TurnIn + FireDialogAction) | Переиспользует `FireDialogAction` для onCompleteActions |
| T-Q07 (QuestProgressDto) | Уже содержит `objectives[]` — T-Q21 только UI render |
| T-Q06 (QuestTriggerService) | Переиспользует `IsSatisfied` triggers для eval |
| T-Q18 (Persistence) | Snapshot save включает objective progress — без изменений |

---

## Файлы, которые НЕ меняются

- `QuestObjective.cs` — структура уже поддерживает все 8 типов
- `QuestStage.cs` — поля `onEnterActions`, `onCompleteActions`, `nextStageId` уже есть
- `QuestTriggerService.cs` — API готов, переиспользуем
- `ConcreteTriggers.cs` — все triggers generic, работают

---

## Закрытие M13

После успешного M13 verify:
- Любой квест может быть многостадийным с авто-прогрессом
- Квест продвигается без dialog actions
- onEnterActions/onCompleteActions работают
- Игрок видит прогресс objectives в UI

Следующие milestones (M14, M15, M16, M17) — по приоритетам.
