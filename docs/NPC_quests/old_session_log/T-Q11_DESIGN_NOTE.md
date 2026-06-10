# T-Q11 — Quest log таб в CharacterWindow

**Дата:**2026-06-08
**Ветка:** `feature/npc-quest-v2`
**Зависимости:** T-Q07 (QuestClientState + DTOs), T-Q10 (DialogWindow pattern reference).
**Блокирует:** T-Q12 (QuestTracker overlay), T-Q15 (action executors — привязка quest progress к dialogue).
**Трудоёмкость:** ~60-90 мин (UI изменения в существующем окне).

---

## Цель

Добавить в `CharacterWindow` шестой таб **«КВЕСТЫ»** с4 секциями (Active / Completed / Failed / Discovered), подпиской на `QuestClientState.OnSnapshotUpdated`/`OnQuestDiscovered`/`OnQuestResult`, и кнопкой **«Принять»** для discovered-квестов. Это первый шаг M4 (Quest log + tracker).

---

## Scope

### Что добавляется

| # | Файл | Изменение | LOC |
|---|------|-----------|-----|
|1 | `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | +1 tab-button (`tab-quests`), +1 section (`quests-section`) с4 под-секциями (active/completed/failed/discovered), каждая с title-Label + ListView; +1 action-button (`accept-quest-btn`) | +50 |
|2 | `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | +`.quest-section`, `.quest-section-title`, `.quest-row`, `.quest-row-state-*`, `.quest-row-actions`, `.btn-accept-quest` (все с `!important` — theme type > class) | +40 |
|3 | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | +9 полей (sections/lists/btn/tab/cache), +4 row factory, +SwitchTab ветка "quests", +EnsureBuilt подписки + OnDisable unsubscribe, +RefreshQuestsCache, +OnAcceptQuestClicked, +3 handler'а (snapshot/result/discovered), + filter integration | +200 |
|4 | `Assets/_Project/Quests/Client/QuestClientState.cs` | +`RequestAcceptQuest(questId, fromNpcId)` forward в `QuestServer.Instance?.RequestAcceptQuestRpc` | +10 |
|5 | `T-Q11_DESIGN_NOTE.md` | Этот файл | (new) |

### Что НЕ делается (отложено в следующие тикеты)

- **Turn-in / Track / Fail action-кнопки** для Active квестов → ждёт T-Q15+ (`QuestWorld.TryTurnIn`/`SetTracked` server impl).
- **Серверный импл Accept** → `QuestServer.RequestAcceptQuestRpc` пока stub (T-Q05, line309); реальный `QuestWorld.TryAccept` будет в T-Q15. UI вызывает RPC, сервер примет (rate-limit OK), но state не изменится до T-Q15 — **это нормально, не блокер**, UI можно делать и тестировать с Discovered секцией.
- **NPC id для Accept** — приходит откуда? У discovered квеста **нет "offering NPC"**, потому что он EventDriven (сработал по триггеру без NPC). Решение: `fromNpcId = ""` (пустая строка), сервер сам определит по context.
- **QuestTracker overlay** (top-right HUD) → T-Q12.
- **Typewriter / F-skip в DialogWindow** → T-Q12.

---

## Архитектура

### Паттерн

По образцу CharacterWindow табов Контракты / Инвентарь (5 FIX'ы из MarketWindow уже применены):

1. **Каждая секция — отдельная `_xList` + row factory** (как у `_contractsList`/`_inventoryList`).
2. **`_xCache` projection** server state → UI state (как `_contractsCache`/`_inventoryCache`).
3. **`RefreshXCache()` filter** — projection server snapshot → filtered list per state (одна функция для всех4 секций).
4. **`HandleXSnapshotUpdated(snap)`** — UNCONDITIONAL refresh cache + gated UI rebuild (cross-tab lesson R3-005).
5. **`SwitchTab("quests")`** — show `_questsSection`, hide filters-row (как у Reputation таба), hide accept/complete/fail contract buttons, show `accept-quest-btn`.
6. **Subscribe в EnsureBuilt, unsubscribe в OnDisable** — стандартный паттерн.

### State mapping

`QuestSnapshotDto.quests[]` (массив `QuestProgressDto`) фильтруется по `state` (byte):

| UI section | byte filter | QuestState |
|------------|-------------|------------|
| `quests-active` | `state ==2` | `Active` |
| `quests-completed` | `state ==3` | `Completed` |
| `quests-failed` | `state ==4` | `Failed` |
| `quests-discovered` | `state ==0` | `Discovered` |

`Offered` (1) и `TurnedIn` (5) НЕ показываем в журнале — это transient/terminal states.
- `Offered` (1): клиент ещё не нажал Accept. Сейчас нет UI для этой секции (можно отображать как "Доступные" в future). По плану — после T-Q15 будет auto-transition Offered→Active на первом advance.
- `TurnedIn` (5): финальное состояние, уходит в Completed (с галочкой "сдано").

### Accept-кнопка

Только для `Discovered` секции. Per-row button. Click handler:
1. `fromNpcId = ""` (EventDriven — нет конкретного NPC).
2. `QuestClientState.Instance.RequestAcceptQuest(questId, "")` — forward в `QuestServer.Instance?.RequestAcceptQuestRpc(questId, "")`.
3. **Server side пока stub** (T-Q15) — RPC дойдёт, rate-limit пройдёт, но state не сменится. **Это by design, не баг**: UI проверяем, сервер ждёт T-Q15.
4. Optimistic update НЕ делаем (нет `state` mutation в кэше до server confirmation — сервер сейчас не пришлёт snapshot обратно).

### Cross-tab pattern (R3-005 lesson)

```csharp
private void HandleQuestSnapshotUpdated(QuestSnapshotDto snap) {
 // cache — ALWAYS refresh
 RefreshQuestsCache();
 // visible UI — gated
 if (_activeTab == "quests") ApplyQuestFilters();
}
```

`OnQuestResult` (action result) — **только message-label update**, всегда (не gated, как у contracts).

`OnQuestDiscovered` (EventDriven push) — вызывает `RefreshQuestsCache()` (появится новая discovered запись, сервер пришлёт snapshot после push).

---

## Pitfalls (унаследованные из T-Q11b_c session log)

1. **USS class-стили с `!important`** — обязательно (UnityDefaultRuntimeTheme type-selector > class-selector). НЕ `!important` на `display` (чтобы inline toggle работал).
2. **`styleSheets.Add(uss)`** — уже в CharacterWindow.EnsureBuilt (line292). Не дублировать.
3. **Cursor lock** — `Show()`/`Hide()` уже в CharacterWindow (line1230/1238). Не дублировать.
4. **`pickingMode` toggle** — уже в CharacterWindow (line1180/1203). Не дублировать.
5. **`MarkDirtyRepaint() + schedule +50ms`** — уже в CharacterWindow.EnsureBuilt (line441-444). Не дублировать.
6. **Lazy-subscribe в Update()** — у Inventory есть. Для QuestClientState можно НЕ делать lazy — singleton создаётся в `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` (line131 QuestClientState.cs), всегда есть к моменту EnsureBuilt. Но defensive guard — `if (QuestClientState.Instance == null) return;`.

---

## Verify (юзер делает руками)

1. Открыть Unity Editor → дождаться компиляции → Console → **0 errors expected**.
2. Открыть BootstrapScene → Play Mode → открыть CharacterWindow (P).
3. Видны **6 табов**: ПЕРСОНАЖ / КОРАБЛЬ / РЕПУТАЦИЯ / КОНТРАКТЫ / ИНВЕНТАРЬ / **КВЕСТЫ**.
4. Кликнуть КВЕСТЫ → секция видна,4 под-секции (Active/Completed/Failed/Discovered), каждая с заголовком.
5. **Empty state**: "Нет квестов" в каждой секции (сервер пока не прислал snapshot — это нормально до `RequestRefreshQuestsRpc`).
6. В Console: `[CharacterWindow] QuestClientState.Instance OK, подписка на OnSnapshotUpdated/OnQuestResult/OnQuestDiscovered`.
7. (Опционально, если есть EventDriven quest триггер): discovered-квест появляется в Discovered под-секции после trigger fire (OnQuestDiscovered → RefreshQuestsCache).
8. (Accept пока stub) — кнопка "Принять" есть, click → server RPC log `[QuestServer] RequestAcceptQuest client=... quest=... fromNpc=` — это нормально, state не сменится до T-Q15.

---

## Open / следующий тикет

- **T-Q12** — QuestTracker overlay + DialogWindow typewriter/F-skip (следующая сессия по плану).
- **T-Q15** — `QuestWorld.TryAccept` server impl (нужно для полного цикла Accept).
