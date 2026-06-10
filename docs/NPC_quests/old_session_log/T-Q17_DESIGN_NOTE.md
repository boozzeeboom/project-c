# T-Q17 — DialogueAction: OpenMarket/OpenService (small, 15 мин) ✅ DONE 2026-06-08

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-Q17

## Скоуп

### Готово
- `QuestServer.FireDialogAction.OpenMarket` — server log + send DialogActionResult, actionType=OpenMarket. ✅
- `QuestServer.FireDialogAction.OpenService` — server log + send DialogActionResult, actionType=OpenService. ✅
- `DialogWindow.HandleActionResultReceived` — client-side dispatch:
  - `OpenMarket` → `Close()` + `MarketInteractor.TryOpenMarket()` (использует local player zone, no zoneId needed) ✅
  - `OpenService` → `Close()` + log stub (ServiceUI не существует, TBD future milestone). ✅

### Деfer
- **`MarketActionExecutor.cs` / `ServiceActionExecutor.cs`** — не создавал (switch case в `FireDialogAction` достаточно для atomic actions; pattern consistency с T-Q16).
- **`ServiceUI` / `ServiceWindow`** — нет в проекте, только `PriceFormula.cs` (server-side pricing helpers). OpenService stub = close dialog + log. Создание ServiceUI — TBD future (отдельный milestone, не T-Q17).
- **OpenMarket с explicit zoneId** — `MarketInteractor.TryOpenMarket()` не принимает zoneId, использует `MarketZoneRegistry.LocalPlayerZone`. Action stringParam = hint (log only).

## Файлы

- `Assets/_Project/Quests/Network/QuestServer.cs`: split OpenMarket/OpenService into real cases (отдельные case блоки)
- `Assets/_Project/Quests/UI/DialogWindow.cs`: +client-side dispatch в `HandleActionResultReceived`

## Verify (твои тесты)

1. Start host
2. **В Mira dialog tree** добавить edge с action.type=OpenMarket (stringParam=""):
   - **`[QuestServer] FireDialogAction: OpenMarket zone=''`** в Console
   - **`[DialogWindow] Action result: OpenMarket → close dialog + TryOpenMarket`** в Console
   - **`[MarketInteractor] TryOpenMarket: enter — LocalPlayerZone=...`** в Console
   - **MarketWindow UI открывается** (если LocalPlayerZone set, иначе log "no zone in range")
3. Edge с action.type=OpenService:
   - `[QuestServer] FireDialogAction: OpenService serviceId='X' (T-Q17 stub — ServiceUI TBD)` в Console
   - dialog закрывается, **без UI** (stub)

**Примечание:** в Mira's default dialog tree сейчас **нет** OpenMarket/OpenService edges. Это authoring task — добавить в `mira_default.asset` (TBD author).

## Что НЕ делали (deferred)

- **ServiceUI / ServiceWindow** — TBD future milestone (нужна отдельная сессия, и не blocking).
- **MarketActionExecutor / ServiceActionExecutor** — switch case pattern достаточно.
- **Authoring Mira dialog tree** — SO editor work, not programming.

## Risk

**Low.** ✅
