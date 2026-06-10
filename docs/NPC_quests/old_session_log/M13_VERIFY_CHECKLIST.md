# M13 — Verify Checklist (для user-теста)

> **Когда:** после T-Q20 + T-Q21 + T-Q22 будут реализованы
> **Дата design:** 2026-06-08
> **Кто тестирует:** user (Play Mode)
> **Дизайн-документ:** `M13_DESIGN_NOTE.md`

---

## Подготовка

1. Создать тестовый quest (если нет):
   ```
   Quest ID: collect_copper_ore
   Stage "collect":
     - Objective: HaveItem(itemDataId=1 [copper_ore], requiredQuantity=3, required=true)
     - onCompleteActions: [GiveCredits(50)]
     - nextStageId: "deliver"
   Stage "deliver":
     - Objective: TalkToNpc(targetNpcId="mira_01", required=true)
     - onCompleteActions: [AddReputation(GuildOfThoughts, 10)]
     - nextStageId: "" (end)
   Rewards: credits=200, reputation=[GuildOfThoughts=25]
   ```

2. Повесить quest на Mira (добавить в `Mira.asset.questOffers[]` и создать dialog edge "Возьми руду" → `OfferQuest`)

3. Удалить `quest_state_0.json` (чистый проход)

4. `Tools → ProjectC → Validate All Quests` → должен пройти без ошибок

---

## Тест-кейсы

### Тест 1 — Real-time HaveItem objective

**Что проверяем:** T-Q20. Pickup → objective auto-progress → stage auto-complete.

**Шаги:**
1. Start host
2. E → Mira → "Возьми руду" (если quest привязан) → AcceptQuest
3. Console должен показать:
   - `[QuestWorld] AcceptQuest: collect_copper_ore → Active`
   - `[QuestServer] SendQuestSnapshotToClient: client=0 quests=1`
4. P → КВЕСТЫ → quest "collect_copper_ore" в "Активных" → objective "Собрать 3 руды" с counter (0/3)
5. Подбери Pickup_ItemA (itemId=1, copper_ore) — если есть в сцене
   - Если нет — нужно создать PickupItem в сцене рядом с Mira

**Ожидаемое поведение:**
- Counter 0/3 → 1/3 в UI
- В Console: `[QuestTriggerService] HaveItem trigger satisfied: clientId=0, count=1`
- При 3rd pickup → 3/3 → `[QuestWorld] Stage advanced: collect_copper_ore collect→deliver`
- Console: `[QuestServer] FireDialogAction: GiveCredits delta=50 0→50`
- P → КВЕСТЫ → currentStageId = "deliver", objective "Поговори с Мирой" появился

**Падение/баг:**
- ❌ Counter не обновляется — snapshot не шлётся
- ❌ Stage не переключается — TryAdvanceStage не вызывается
- ❌ GiveCredits не отрабатывает — onCompleteActions не fire

### Тест 2 — ReachLocation objective

**Что проверяем:** T-Q20. Walk to position → tick → objective satisfied.

**Шаги:**
1. Создать quest с objective `ReachLocation(targetPosition=(50000, 2500, 50000), targetRadius=50)`
2. Accept quest
3. Walk к координатам (или через teleport)
4. Within 50m → подождать 5 сек (tick interval)

**Ожидаемое поведение:**
- Console: `[QuestWorld] ReachLocation satisfied: dist=X < 50m`
- Objective переходит в completed=true
- Если это был последний required objective → stage transition

**Падение/баг:**
- ❌ Distance always infinity — `playerPos` source broken
- ❌ Object never satisfied — radius too small / position wrong

### Тест 3 — Stage transition + onEnterActions

**Что проверяем:** T-Q22. Stage "intro" complete → onCompleteActions fire → transition → onEnterActions fire.

**Шаги:**
1. Quest stage "intro" with `onCompleteActions: [GiveCredits(50)]`, nextStage "main" with `onEnterActions: [EmitEvent("test_event")]`
2. Satisfy "intro" objective
3. Watch Console

**Ожидаемое поведение:**
- Console последовательно:
  - `[QuestServer] FireDialogAction: GiveCredits delta=50 0→50`
  - `[QuestWorld] FireDialogAction: EmitEvent test_event`
  - `[QuestWorld] Stage advanced: stageId=intro → main`
  - `[QuestServer] SendQuestSnapshotToClient: client=0 quests=1`
- P → КВЕСТЫ → currentStageId = "main"

**Падение/баг:**
- ❌ onEnterActions не вызываются — fire loop order bug
- ❌ Snapshot не шлётся — no event hook
- ❌ Credits не начислились — TryAdvanceStage не вызывается

### Тест 4 — UI objective progress

**Что проверяем:** T-Q21. Клиент рендерит objective checkmarks.

**Шаги:**
1. Accept multi-objective quest (3 HaveItem, 1 TalkToNpc)
2. Open P → КВЕСТЫ → выбрать quest
3. Look at objective list

**Ожидаемое поведение:**
- Каждый objective отображается с ☐ или ☑
- Счётчик: "Собрать 3 руды (1/3)"
- При pickup → snapshot update → UI refreshes (5s tick)
- QuestTracker HUD показывает current objective: "Собрать 3 руды (1/3)"

**Падение/баг:**
- ❌ Objective list пуст — DTO не парсится в UI
- ❌ Counter не обновляется — `currentValue` не заполняется
- ❌ Tracker не показывает objective — text binding broken

### Тест 5 — Final quest completion + rewards

**Что проверяем:** T-Q22. Последняя stage → onCompleteActions → state=Completed → def.rewards fire.

**Шаги:**
1. Quest с final stage "return" + onCompleteActions [TakeItem] + nextStageId пуст + rewards [credits=200, rep=25]
2. Complete final stage objective
3. Watch Console + P → КВЕСТЫ + ПЕРСОНАЖ + РЕПУТАЦИЯ

**Ожидаемое поведение:**
- Console:
  - `[QuestServer] FireDialogAction: TakeItem`
  - `[QuestWorld] State transitioned: collect_copper_ore Active→Completed`
  - `[QuestWorld] ApplyQuestRewards: credits +200, rep[GuildOfThoughts] +25`
  - `[QuestServer] SendQuestSnapshotToClient: client=0 quests=1`
- P → КВЕСТЫ → quest в **"Завершённых"**
- P → ПЕРСОНАЖ → кредиты увеличились на 200
- P → РЕПУТАЦИЯ → GuildOfThoughts увеличилась на 25

**Падение/баг:**
- ❌ State не переходит в Completed — TryAdvanceStage last-stage case broken
- ❌ Rewards не выдаются — ApplyQuestRewards не вызывается
- ❌ Credits/rep UI не обновляются — snapshot push missing

---

## Команды для проверки (user runs)

```powershell
# 1. Clean state
rm $env:USERPROFILE\AppData\LocalLow\DefaultCompany\ProjectC_client\quest_state_0.json
rm $env:USERPROFILE\AppData\LocalLow\DefaultCompany\ProjectC_client\inventory_state_0.json

# 2. Unity Editor
# Open Unity → Console → 0 errors expected
# Tools → ProjectC → Validate All Quests → should pass

# 3. Play Mode
# P → Host → connect
# Follow тест-кейсы 1-5 above

# 4. Console reading (for diagnostics)
# Window → General → Console → filter by [QuestWorld], [QuestServer], [QuestTriggerService]
```

---

## Что сообщить assistant после теста

- ✅/❌ каждый тест-кейс
- Скриншот/Console paste если что-то не работает
- Сколько objectives реально проверяются (TalkToNpc, HaveItem, ReachLocation, RepAtLeast, EventDriven, DeliverItem)
- Видны ли onEnterActions / onCompleteActions в Console
- Обновляется ли UI (☐/☑, counter)
