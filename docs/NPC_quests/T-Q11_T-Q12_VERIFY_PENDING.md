# T-Q11 + T-Q12 — verify pending

**Дата:** 2026-06-08  
**Статус:** UI реализован и компилится, но **end-to-end не проверен** — квест не может попасть в `Active` state, потому что server impl `QuestWorld.TryAccept` = T-Q15 (ещё не сделан).

## Что проверено ✅
- Dialog открывается по E
- Typewriter char-by-char
- F-skip
- Click-skip

## Что НЕ проверено ⏸️ (нужен T-Q15)
- **QuestTracker toggle "Следить"/"Не следить"** — нет Active квеста, нечего трекать
- Таб "КВЕСТЫ" в CharacterWindow — empty (discovered квесты не принимаются без server impl)
- `OnSnapshotUpdated` flow на клиенте

## Follow-up после T-Q15
1. Play Mode → подойти к Mira → E → "Принять" → квест появится в "Активные"
2. Нажать "Следить" в табе КВЕСТЫ → QuestTracker overlay (top-right) должен показать quest name + objective
3. Нажать "Не следить" → overlay скрывается
4. Verify в Console: `[QuestTracker] Track: quest=...` / `[QuestTracker] Untrack: quest=...`
5. Take/turn-in квеста → auto-untrack
