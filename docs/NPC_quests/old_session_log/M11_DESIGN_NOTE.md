# M11 — Mira End-to-End Demo (full implementation)

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.4 M11

## Что сделано

### Quest `find_artifact` (минимизирован)
- Stages: 1 (start) — quest просто трекает, реальная механика через dialog.
- Rewards: 0/0/0/0 (всё через dialog actions).
- Objective: 1 (find_artifact, itemTradeItemId=time_crystal, required=1).

### Dialog tree `MiraDefault.asset` (переписан)
**8 nodes:**
- `greeting` — 3 options:
  - "У тебя есть работа?" — visible if `HasItem(ancient_key)>=1` AND `QuestStateEquals(find_artifact, Discovered)` → `offer_quest`
  - "Ну что, нашёл Кристалл Времён?" — visible if `QuestStateEquals(find_artifact, Active)` → `check_b`
  - "Пока" → end
- `offer_quest` — 2 options:
  - "Я помогу" → action `TakeItem(ancient_key, 1)` → `accept_thanks`
  - "Нет, не хочу" → `decline`
- `accept_thanks` — 1 option:
  - "Хорошо" → action `OfferQuest(find_artifact)` → end
- `decline` — 1 option: "До свидания" → end
- `check_b` — 2 options:
  - "Да" → `give_b`
  - "Нет" → `not_yet`
- `give_b` — 2 options:
  - "(отдать Кристалл)" — visible if `HasItem(time_crystal)>=1` → action `TakeItem(time_crystal, 1)` → `complete_thanks`
  - "Стоп, я передумал" → `no_b`
- `no_b` — "Не вижу у тебя Кристалла. Иди ищи" → 1 option: "Извини" → end
- `not_yet` — "Ок, как найдёшь — приноси" → 1 option: "До свидания" → end
- `complete_thanks` — `onEnterActions`: AddReputation(+25, GuildOfThoughts=1) + AddNpcAttitude(mira_01, +10) + CompleteObjective(find_artifact) → 1 option: "Спасибо, Мира" → action `GiveCredits(1000)` → end

### CompleteObjective action — real impl (server-side)
- `QuestServer.FireDialogAction.CompleteObjective` теперь real, не stub.
- Если quest в player log → `QuestWorld.TryTurnIn(questId)`.
- TryTurnIn auto-completes from Active → Completed → TurnedIn.
- Applies `QuestDefinition.rewards` (в нашем случае 0/0/0/0 — ничего не даёт).
- Dialog actions сами дают credits/rep/attitude.

### Items in scene (рядом с Mira)
- `[Pickup_AncientKey]` at (40004.48, 2502.77, 39984.94) — itemId=1
- `[Pickup_TimeCrystal]` at (40010.48, 2502.77, 39984.94) — itemId=2
- Mira at (40007.48, 2502.77, 39984.94)
- Distance: ~3m to each side.

### Compile
0 errors ✅.

## Verify (твои тесты — 5-7 мин)

### Setup
1. **Снеси старый quest state** (если был active ранее): удали `C:\Users\<user>\AppData\LocalLow\<Company>\<Product>\quest_state_0.json` (или Start host → найди кнопку reset).
2. Start host → connect client (existing flow).
3. P → CharacterWindow → КВЕСТЫ → должно быть пусто (чистый state).

### Step 1: Pick up item A
1. Walk to `[Pickup_AncientKey]` (3m to the left of Mira).
2. Press F (or interact) → pickup.
3. Console: `[InventoryServer] AddItemDirect client=0 itemId=1`.

### Step 2: Talk to Mira — get quest
1. Walk to Mira → press E.
2. Mira: "Приветствую, искатель. Чем могу помочь?"
3. Available edges:
   - "У тебя есть работа для меня?" (visible — has A)
   - "Пока."
4. Click "У тебя есть работа для меня?"
5. Mira: "У меня есть дело..." (offer_quest)
6. Click "Я помогу."
7. Console: `[QuestServer] FireDialogAction: TakeItem id=1 x1 → True`
8. Mira: "Спасибо за ключ. Принеси Кристалл Времён..."
9. Click "Хорошо."
10. Console: `[QuestWorld] TryOffer: client=0 quest=find_artifact → Discovered`
11. Dialog closes.
12. P → CharacterWindow → КВЕСТЫ → "Найти Кристалл Времён" в "Найденных".
13. Click "ПРИНЯТЬ" → quest в "Активных".

### Step 3: Pick up item B
1. Walk to `[Pickup_TimeCrystal]` (3m to the right of Mira).
2. Pickup → item added to inventory.

### Step 4: Return to Mira — turn in
1. Walk to Mira → press E.
2. Mira: "Приветствую, искатель."
3. Available edges:
   - "Ну что, нашёл Кристалл Времён?" (visible — quest active)
   - "Пока."
4. Click "Ну что, нашёл Кристалл Времён?"
5. Mira: "Ну что, нашёл Кристалл Времён?" (check_b)
6. Click "Да."
7. Mira: "Дай-ка посмотрю..." (give_b)
8. Available edges:
   - "(отдать Кристалл)" (visible — has B)
   - "Стоп, я передумал."
9. Click "(отдать Кристалл)".
10. Console: 
    - `[QuestServer] FireDialogAction: TakeItem id=2 x1 → True`
    - `[QuestServer] FireDialogAction: AddReputation faction=GuildOfThoughts delta=25 newValue=25`
    - `[QuestServer] FireDialogAction: AddNpcAttitude npc=mira_01 delta=10 newValue=10`
    - `[QuestServer] FireDialogAction: CompleteObjective quest=find_artifact → TryTurnIn`
    - `[QuestWorld] TryTurnIn: client=0 quest=find_artifact toNpc= → TurnedIn`
11. Mira: "Невероятно! Это действительно он! Спасибо тебе..." (complete_thanks)
12. Click "Спасибо, Мира."
13. Console: `[QuestServer] FireDialogAction: GiveCredits delta=1000 0→1000`
14. Dialog closes.

### Step 5: Verify rewards
1. P → CharacterWindow → таб РЕПУТАЦИЯ → "Гильдия Мысли" = 25.
2. P → CharacterWindow → таб NPC → Mira attitude = 10.
3. P → CharacterWindow → таб КВЕСТЫ → "Найти Кристалл Времён" в "Завершённых".
4. Credits (нижняя строка HUD, "Кошелек") = 1000.

## Edge cases (не баги)

- **Нет Item A**: option "У тебя есть работа?" не появится (HasItem cond). Только "Пока".
- **Нет Item B при turn-in**: option "(отдать Кристалл)" скрыта. Доступно "Стоп, я передумал" → `no_b` → "Не вижу у тебя Кристалла. Иди ищи."
- **Quest в Active (после accept)**: option "У тебя есть работа?" НЕ показывается (questStateEquals Discovered cond → false).
- **Quest в TurnedIn**: option "Ну что, нашёл?" НЕ показывается (questStateEquals Active cond → false).
- **Persistence (T-Q18)**: accept + restart → quest всё ещё в Active.

## Известные нюансы

- **"Принять" в КВЕСТЫ** — нужно после OfferQuest, иначе quest остаётся в Discovered. Это by design (T-Q15).
- **ApplyQuestRewards = 0** для этого квеста — все rewards даются через dialog actions. Если `QuestDefinition.rewards` ненулевые, будут накладываться поверх.
- **Mira остаётся в том же месте** — scene не модифицирована для world objects (Key_Red_Pickup, LockBox_*, MarketZone, корабли — всё на месте).

## Uncommitted

- `Assets/_Project/Quests/Network/QuestServer.cs` — CompleteObjective real impl
- `Assets/_Project/Quests/Data/Quests/FindArtifact.asset` — минимизирован (1 stage, 0 rewards)
- `Assets/_Project/Quests/Data/Dialogs/MiraDefault.asset` — переписан (8 nodes)
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — +2 pickups (AncientKey, TimeCrystal)
- `M11_DESIGN_NOTE.md` — этот файл (rewrite)
