# T-Q22 — корректная модель TalkToNpc и инициализация целей

> **Дата:** 2026-08-16
> **Статус:** код исправлен, Play Mode ожидает ручной проверки
> **Контекст:** onboarding-квест `onboarding_alfa`

## 1. Что должно быть в runtime

`TalkToNpc` — это одноразовое событие текущего взаимодействия с NPC.

Он не означает:

- игрок когда-либо разговаривал с этим NPC;
- NPC известен игроку;
- игрок находится рядом с последней известной координатой NPC.

Исторический факт знакомства и текущее событие разговора — разные состояния.

| Назначение | Источник данных |
|---|---|
| Игрок уже знаком с NPC / knowledge | постоянный `_npcTalkedTo` |
| Текущая цель «поговорить с NPC» | временный `_npcTalkEvents` |
| Статичная точка или зона | `ReachLocation` + world-space координаты |

## 2. Найденные причины

### 2.1. Цель не отображалась сразу после принятия

`QuestWorld.TryAccept` создавал активный `QuestInstance` и задавал `currentStageId`, но список `objectiveProgress` оставался пустым до первого server tick.

`QuestServer.BuildQuestSnapshot` строил DTO из этого пустого списка. Поэтому сразу после `AcceptQuest` клиент получал активный квест без objectives и показывал «нет цели».

### 2.2. Возврат к Mira засчитывался автоматически

Ранее `TalkToNpc` проверял:

```csharp
HasNpcTalkedTo(clientId, npcId)
```

Этот метод читает постоянный набор NPC, с которыми игрок разговаривал хотя бы раз. После первого разговора с Mira `mira_01` навсегда оставался в наборе.

Запрос открытия диалога и событие `DialogVisitedEvent` оба вызывали `MarkNpcTalked`, а последующие `TalkToNpc` objectives видели тот же вечный флаг и завершались без нового разговора.

## 3. Что исправлено

### 3.1. Инициализация progress текущего stage

В `QuestInstance` добавлены:

- `ResetObjectiveProgress(QuestStage stage)`;
- `EnsureObjectiveProgress(QuestStage stage)`.

Теперь:

- при принятии нового квеста progress текущего stage создаётся сразу;
- при восстановлении старого save недостающие записи добавляются на server tick;
- после перехода на следующий stage старые objectives очищаются, а objectives нового stage создаются заново.

### 3.2. TalkToNpc переведён на transient event

В `QuestWorld` добавлен отдельный неперсистентный набор:

```text
_npcTalkEvents: player → NPC ids talked-to during the current tick
```

Изменения поведения:

1. Успешно открытый диалог создаёт transient `TalkToNpc` event.
2. `DialogVisitedEvent` также отмечает тот же event; `HashSet` не создаёт дубль.
3. `EvaluateObjective(TalkToNpc)` читает `_npcTalkEvents`, а не исторический `_npcTalkedTo`.
4. После обработки всех активных квестов игрока transient events очищаются.
5. `_npcTalkedTo` продолжает хранить knowledge/историю и сохраняться в player state.
6. Разговор, произошедший до принятия квеста, очищается при `TryAccept` и не засчитывает первый stage автоматически.

Одно взаимодействие доступно всем активным квестам текущего tick, после чего событие исчезает. Это не создаёт повторного автозачёта на следующих стадиях.

### 3.3. Некорректный запрос больше не создаёт talk event

`QuestServer.RequestTalkToNpcRpc` теперь вызывает `MarkNpcTalked` только после успешного открытия dialog session.

Неуспешный запрос, неизвестный NPC или отсутствующее дерево диалога не должны удовлетворять `TalkToNpc` objective.

`AcceptQuest` также передаёт фактический `npcId` в `TryAccept`.

## 4. Ожидаемый flow onboarding-квеста

```text
AcceptQuest у onboarding NPC
  → сразу видна цель TalkToNpc: Mira

разговор с Mira
  → meet_mira выполнен
  → цель ReachLocation: RepairManager

достижение RepairManager
  → цель TalkToNpc: Mira
  → координата Mira не проверяется

новый разговор с Mira
  → return_from_repair выполнен
  → цель ReachLocation: MarketZone_Primium

достижение MarketZone_Primium
  → цель TalkToNpc: Mira

новый разговор с Mira
  → return_from_market выполнен
  → Completed / turn-in
  → Key_light_ship
```

## 5. Инварианты для дальнейшего контента

1. `ReachLocation` использовать только для статичных точек/зон.
2. Разговор с NPC всегда оформлять как `TalkToNpc` с `targetNpcId` и `targetNpc`.
3. Нельзя использовать исторический `HasNpcTalkedTo` для завершения текущего `TalkToNpc` objective.
4. При смене stage progress должен соответствовать только текущему stage.
5. После принятия квеста snapshot обязан содержать objective текущего stage без ожидания первого tick.
6. NPC может менять позицию: TalkToNpc подтверждается interaction event, а не координатой.

## 6. Изменённые файлы

- `Assets/_Project/Quests/Core/QuestWorld.cs`
  - transient talk events;
  - TalkToNpc evaluation через текущий event;
  - очистка событий после tick;
  - инициализация/reset progress на accept и stage transition.
- `Assets/_Project/Quests/Core/QuestInstance.cs`
  - `ResetObjectiveProgress`;
  - `EnsureObjectiveProgress`.
- `Assets/_Project/Quests/Network/QuestServer.cs`
  - talk event создаётся после успешного открытия диалога;
  - `AcceptQuest` передаёт `npcId`.

## 7. Проверки

- `check_compile_errors`: `No compile errors`.
- Ассет `onboarding_alfa` по-прежнему содержит:
  - `meet_mira` → `TalkToNpc`;
  - `go_repair` → `ReachLocation`;
  - `return_from_repair` → `TalkToNpc`;
  - `go_market` → `ReachLocation`;
  - `return_from_market` → `TalkToNpc`.
- Play Mode после исправления ещё не запускался.

## 8. Ручной сценарий проверки

1. Принять `onboarding_alfa` у onboarding NPC.
2. Убедиться, что цель отображается сразу, без сообщения «нет цели».
3. Поговорить с Mira — перейти к RepairManager.
4. Дойти до RepairManager — убедиться, что цель сменились на разговор с Mira.
5. Не разговаривая с Mira, дойти до MarketZone_Primium — стадия не должна завершиться.
6. Поговорить с Mira — перейти к MarketZone_Primium.
7. Дойти до MarketZone_Primium — цель должна смениться на разговор с Mira.
8. Не разговаривая с Mira, убедиться, что ключ не выдан.
9. Поговорить с Mira — получить Completed/turn-in и ровно один `Key_light_ship`.
