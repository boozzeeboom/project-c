# T-QC7: Удаление мёртвой триггерной системы (QuestTriggerService)

**Дата:** 2026-08-13
**Источник:** `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` → пункт **C7**
**Тип:** чистка мёртвого кода (~600 строк), не баг-фикс, не изменение протокола

---

## TL;DR

Удалена event-driven триггерная подсистема, которая не работала **ни одного тика**:

- `QuestTriggerService` (Attach/Detach/Evaluate/MatchesObjective/фабрики)
- `ConcreteTriggers` (15 триггер-классов + их `IsSatisfied()`)
- Интерфейс `IQuestTrigger`
- Все вызовы `TriggerService.Evaluate(...)` в `QuestServer` и `ContractMetaBridge`

Причина: `Attach()` имел **0 вызовов** → `_playerTriggers` всегда пуст → `Evaluate()` всегда `return 0`.

Каноническая модель продвижения квестов — **polling** (`QuestWorld.TickAll`, тик ~5 сек).
Она была и остаётся единственным рабочим механизмом.

---

## Что удалено

### Файлы (удалены целиком)
| Файл | Содержимое |
|---|---|
| `Assets/_Project/Quests/Triggers/QuestTriggerService.cs` | сервис Attach/Detach/Evaluate/MatchesObjective/RegisterDefaultFactories |
| `Assets/_Project/Quests/Triggers/ConcreteTriggers.cs` | 15 реализаций `IQuestTrigger` |
| `Assets/_Project/Quests/Triggers/IQuestTrigger.cs` | интерфейс |

### `QuestWorld.cs`
- удалён `using ProjectC.Quests.Triggers;`
- удалено свойство `public Triggers.QuestTriggerService TriggerService`
- удалена строка `Instance.TriggerService = new Triggers.QuestTriggerService(Instance);`
- из лога `Initialized` убрано упоминание `TriggerService online.`

### `QuestServer.cs` (WorldEventBus-подписчики)
Удалены **7 полностью мёртвых** handler-ов (только `Evaluate`, без side-эффектов):
`OnItemAdded`, `OnItemRemoved`, `OnDayNightChanged`, `OnGameDayChanged`,
`OnGameWeekChanged`, `OnGameMonthChanged`, `OnGameYearChanged`
+ их delegate-поля и Subscribe/Unsubscribe.

**4 handler-а оставлены, но из них вырезан `Evaluate`** (сохранён только полезный side-эффект):

| Handler | Что сохранено | Зачем |
|---|---|---|
| `OnReputationChanged` | `BroadcastReputationChange` | push снапшота репутации в UI |
| `OnNpcAttitudeChanged` | `BroadcastNpcAttitudeChange` | push снапшота attitude в UI |
| `OnCustomEvent` | `MarkEventOccurred` | метка события для polling (`WaitForEvent`/`EventDriven`) |
| `OnDialogVisited` | `MarkNpcTalked` | метка «поговорил с NPC» для polling (`TalkToNpc`) |

### `ContractMetaBridge.cs`
- удалены 3 вызова `TriggerService.Evaluate(...)` (в accept/completed).
- `MarkContractAccepted` / `MarkContractCompleted` **оставлены** (будущие contract-objectives).
- обновлены комментарии.

### Комментарии (косметика)
- `Core/WorldEventBus.cs` и `Core/WorldEvent.cs` — убраны устаревшие упоминания `QuestTriggerService`.

---

## Почему это безопасно (доказательства)

1. **`Attach()` / `Detach()` — 0 вызовов** (grep `\.Attach\(` / `\.Detach\(` → только объявления).
   Значит `_playerTriggers` всегда пуст, а `Evaluate()` на `QuestTriggerService.cs:79-80`
   сразу возвращал `0`. Ни один квест-объектив **никогда** не продвигался через триггеры.

2. **`IsSatisfied()` — 0 вызовов** (grep `\.IsSatisfied\(` → пусто). Все 15 триггеров — мёртвые фабрики.

3. **`MatchesObjective` не мог совпасть**: требовал `trigger.TriggerId == obj.objectiveId`,
   но `TriggerId` = `"HaveItem:42"`, а реальные `objectiveId` = `"obj_q_002_0_s1"`.

4. **4 недозарегистрированные фабрики**: `GameDay/GameWeekday/GameMonth/GameYear` были объявлены
   (`ConcreteTriggers.cs:157-220`), но отсутствовали в `RegisterDefaultFactories` — т.е. hints
   `"GameDay:*"` и т.п. уходили в пустоту даже в теории.

5. **Реальное продвижение — только polling**: `QuestServer.Update` → `QuestWorld.TickAll(Time.deltaTime)`
   (`QuestServer.cs:446`), внутри которого objective-оценка по `objectiveType`
   (`QuestWorld.cs:1152-1206`: TalkToNpc/HasNpcTalkedTo, HaveItem, DeliverItem, ReachLocation,
   ReputationAtLeast, WaitForEvent/EventDriven/HasEventOccurred, KillEntity). Этот путь не тронут.

---

## Что осталось (живой путь)

```
QuestServer.Update (каждый кадр)
   └─ QuestWorld.TickAll(dt)   // внутренний тик ~5 сек
        └─ objective-оценка по objectiveType (polling)

WorldEventBus (живые подписки, только side-эффекты)
   ├─ ReputationChangedEvent → BroadcastReputationChange (push в UI)
   ├─ NpcAttitudeChangedEvent → BroadcastNpcAttitudeChange (push в UI)
   ├─ CustomEvent → MarkEventOccurred (для WaitForEvent/EventDriven polling)
   └─ DialogVisitedEvent → MarkNpcTalked (для TalkToNpc polling)
```

---

## Диагностика багов (если после удаления что-то «сломается»)

> Ключевое: удалённый код **не мог выполнять работу** (Evaluate всегда = 0), поэтому любой
> регресс — это проблема **polling-пути** или **живых Mark*/Broadcast***, а не удаления.

| Симптом | Вероятная причина | Куда смотреть |
|---|---|---|
| Объектив «поговори с NPC» не засчитывается | `MarkNpcTalked` не вызван ИЛИ polling-оценка `TalkToNpc` не видит маркер | `QuestServer.OnDialogVisited` (`:799`); `QuestServer.RequestTalkToNpcRpc` (`MarkNpcTalked` на `:531`); `QuestWorld.HasNpcTalkedTo` (`:874`) + `TickAll` case `TalkToNpc` (`:1154`) |
| Объектив WaitForEvent/EventDriven не засчитывается | `MarkEventOccurred` не вызван (EmitEvent) ИЛИ polling case не сработал | `QuestServer.OnCustomEvent` (`:792`); `QuestServer` case `EmitEvent` (`:1453`); `QuestWorld.HasEventOccurred` (`:949`) + `TickAll` (`:1200`) |
| Репутация/attitude не обновились в UI сразу | потерян push после изменения | `QuestServer.OnReputationChanged`/`OnNpcAttitudeChanged` (`:780`,`:786`); `ModifyReputation`/`ModifyNpcAttitude` в `QuestWorld` (публикуют events) |
| Квест «не продвигается» вообще | `TickAll` не вызывается / `QuestWorld.Instance == null` / `tickInterval` | `QuestServer.Update` (`:446`); `QuestWorld.TickAll` (`:1045`) |

### Быстрая проверка
- `debugMode` у `QuestServer` → логи `[QuestWorld] TickAll`, `MarkNpcTalked`, `MarkEventOccurred`.
- Если маркер пишется, но объектив не засчитывается — ищи в `TickAll` objective-switch.

---

## Rollback (как вернуть)

Вариант 1 — git:
```
git revert <commit>
```

Вариант 2 — восстановить 3 файла из git:
```
git checkout HEAD~1 -- Assets/_Project/Quests/Triggers/
```
и вернуть в `QuestWorld` свойство `TriggerService` + инициализацию, а в `QuestServer`/`ContractMetaBridge`
— вызовы `Evaluate(...)` (тексты удалённых методов см. в `DEEP_AUDIT_2026-08-13.md`, секция C7).

---

## Статус

- [x] Удалены 3 файла триггерной системы.
- [x] Удалены 7 мёртвых bus-handler-ов + 3 Evaluate в ContractMetaBridge.
- [x] Сохранены Mark*/Broadcast* side-эффекты (polling + UI push).
- [x] Компиляция чистая (`check_compile_errors` → no errors).
- [ ] Play-тест квестов (accept → TalkToNpc/HaveItem → turn-in) — **выполняет пользователь**.
