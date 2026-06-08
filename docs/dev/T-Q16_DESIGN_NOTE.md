# T-Q16 — DialogueAction: GiveCredits/AddReputation/AddNpcAttitude (small, 30 мин) ✅ DONE 2026-06-08

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-Q16
**Связь:** 09_OPEN_QUESTIONS.md §G, T-Q13 (cross-faction), T-Q15 (stubs)

## Скоуп (как в roadmap + deferred ApplyQuestRewards)

### Готово
- `QuestServer.FireDialogAction.GiveCredits` — server-side: TradeWorld.Repository.GetCredits + delta → SetCredits, push ContractSnapshot через `ContractServer.Instance.PushPlayerSnapshot`. ✅
- `QuestServer.FireDialogAction.AddReputation` — server-side: QuestWorld.ModifyReputation (T-Q13 already had, broadcast+event built-in). ✅
- `QuestServer.FireDialogAction.AddNpcAttitude` — server-side: QuestWorld.ModifyNpcAttitude (T-Q13 already had, broadcast+event+cross-faction built-in). ✅
- `ContractServer.PushPlayerSnapshot(ulong)` — public helper для QuestServer чтобы пушить обновлённый snapshot к клиенту (когда credits change вне Contract flow). ✅

### Деfer
- **`ApplyQuestRewards` при TurnIn** (was T-Q15 deferred) — бонусный scope, не в roadmap для T-Q16. Перенесён в **T-Q18 persistence milestone** (M8, рядом с другими reward-related items).
- **Reputation/Attitude cross-faction для `GiveCredits` rewards** — out of scope.

## Файлы

- `Assets/_Project/Quests/Network/QuestServer.cs`: real impl 3 actions, +Mathf для credit clamp
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs`: +`PushPlayerSnapshot(ulong)` public helper

## Verify (твои тесты)

1. P → подойти к Mira → E
2. **Если у Mira в dialog tree есть `GiveCredits(50)` action** (например в intro node после quest accept) → в Console:
   - `[QuestServer] FireDialogAction: GiveCredits delta=50 1000→1050`
   - `ContractClientState.credits` в UI обновится (пока не показывается в CharacterWindow — нет вкладки credits yet, but repo state изменён)
3. **Если `AddReputation(+25, FactionId.GuildOfThoughts)`** → 
   - `[QuestServer] FireDialogAction: AddReputation faction=GuildOfThoughts delta=25 newValue=25`
   - CharacterWindow таб РЕПУТАЦИЯ → 25 для GuildOfThoughts
4. **Если `AddNpcAttitude(+5, "mira_01")`** →
   - `[QuestServer] FireDialogAction: AddNpcAttitude npc=mira_01 delta=5 newValue=5`
   - Dialog header показывает "❤ +5" (если открыт dialog с Mira)

**Note:** ни одно из этих actions не настроено в Mira's dialog tree по умолчанию — нужно вручную добавить в `Assets/_Project/Quests/Data/Dialogs/` (mira_default.asset) edge action. Это **authoring task** (out of scope T-Q16).

## Что НЕ делали (deferred)

- **ApplyQuestRewards в TryTurnIn** — перенесён в T-Q18.
- **`MarketClientState` показывает credits в UI** — out of scope T-Q16. UI fix для credits display.
- **DialogActionRunner class** — не создавал. Switch case в `FireDialogAction` достаточно.
