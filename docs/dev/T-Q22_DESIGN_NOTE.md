# T-Q22 — Stage transitions + onEnterActions/onCompleteActions

> **Дата:** 2026-06-09
> **Сессия:** M13 финальный тикет
> **Roadmap:** расширяет `08_ROADMAP.md` §8.3.1
> **Status:** 📋 DESIGN — реализация после подтверждения
> **Зависимости:** T-Q20 (tick + EvaluateAndAdvanceStage) ✅, T-Q21 (UI) ✅

---

## 1. Проблема (audit 2026-06-09)

Текущая реализация T-Q20/T-Q21 покрывает:
- ✅ Server tick каждые 5 сек → EvaluateObjective → ...
- ✅ HaveItem с counter `currentValue/requiredQuantity` → UI обновляется
- ✅ Single-stage quest: objectives complete → onCompleteActions fire → rewards → state=Completed

**Но НЕ покрыто (audit показал):**

1. **Multi-stage transitions** — ни один quest в `QuestDatabase` не имеет 2+ stages. `nextStageId` в `QuestStage` определён, но **никогда не тестировался**.
2. **`onEnterActions` не тестировались** — поле есть в SO, fire логика в `TryAdvanceStage` есть, но ни в одном production quest нет ни одного onEnter action.
3. **`TryTurnIn` баг** — на line 371-377 `TryTurnIn` для `state=Active` сразу ставит `state=Completed` **минуя** `TryAdvanceStage`. Это значит `onCompleteActions` финального stage **не вызываются** через turn-in flow. Они вызываются только через tick (`EvaluateAndAdvanceStage`).
4. **Двухступенчатый transition test** — quest `collect_copper_ore` (single-stage) не позволяет увидеть A→B→Completed flow.

## 2. Что в скоупе T-Q22

### 2.1 Backend (QuestWorld)
- **Проверить** что `TryAdvanceStage` правильно отрабатывает `nextStageId` между non-final stages (A→B).
- **Исправить `TryTurnIn`**: для финального stage вызывать `TryAdvanceStage` вместо прямого `state=Completed`. Это гарантирует fire `onCompleteActions` через turn-in.
- **Сохранять `currentStageId` в save/load** (T-Q18 уже это делает, но верифицировать).

### 2.2 Новый quest: `stage_multi_demo`
2 stages для теста:
- **Stage A "collect"**: HaveItem(TestStageItem, 1)
  - `onEnter: AddReputation(GuildOfThoughts, +3)` — fire при entering
  - `onComplete: GiveCredits(20)` — fire при completing
  - `nextStageId: "deliver"`
- **Stage B "deliver"**: TalkToNpc("mira_01")
  - `onEnter: AddNpcAttitude("mira_01", +10)` — fire при entering
  - `onComplete: GiveCredits(50)` — fire при completing
  - `nextStageId: ""` → final → state=Completed

### 2.3 Сцена
- **НЕ ТРОГАЕМ** существующие pickup'ы от M13 (`[Pickup_CopperOre_1/2/3]`) — они для T-Q20
- **ДОБАВЛЯЕМ** один `[Pickup_TestStageItem]` рядом с Mira
- QuestDatabase.asset — добавляем quest (не удаляем `collect_copper_ore`, `find_artifact`, `event_driven`)

## 3. Сценарий верификации

### Test 1: `stage_intro_demo` (single-stage с onEnter)
- Принять quest → tick → Console: `[QuestServer] FireDialogAction: AddNpcAttitude mira_01 delta=5` (onEnter at Accept)
- E → Mira → TalkToNpc objective complete → tick → Console: `FireDialogAction: GiveCredits delta=10 0→10` (onComplete)
- Quest в "Завершённых"

### Test 2: `stage_multi_demo` (multi-stage)
- Принять quest → Console: `[QuestWorld] AddReputation ... 0→3` (stage A onEnter) + `+3 rep` в P-таб РЕПУТАЦИЯ
- Подобрать `[Pickup_TestStageItem]` → ждать 5 сек → Console: `FireDialogAction: GiveCredits delta=20 0→20` (stage A onComplete) + `Stage advanced: collect → deliver` + `FireDialogAction: AddNpcAttitude mira_01 delta=10` (stage B onEnter)
- В P-таб КВЕСТЫ → quest в "Активных" → currentStage="deliver" → objective "Поговори с Мирой"
- E → Mira → tick → Console: `FireDialogAction: GiveCredits delta=50 20→70` (stage B onComplete) + `State transitioned: → Completed`
- Quest в "Завершённых", P-таб ПЕРСОНАЖ: +70 CR

### Test 3: верификация `TryTurnIn` fix
- (после фикса) Создать test сценарий где quest с `onComplete: GiveCredits(100)` в финальном stage
- Через dialog `CompleteObjective` action → TryTurnIn → Console: `FireDialogAction: GiveCredits delta=100` (а не silent state change)

## 4. Файлы

- **Новые assets:**
  - `Assets/_Project/Quests/Data/Quests/StageIntroDemo.asset` (single-stage с onEnter)
  - `Assets/_Project/Quests/Data/Quests/StageMultiDemo.asset` (multi-stage)
  - `Assets/_Project/Resources/Items/Item_Resource_TestStageItem.asset` (новый ItemData)
- **Изменяемые scenes:**
  - `WorldScene_0_0.unity` — добавить `[Pickup_TestStageItem]` GameObject (НЕ удалять существующие!)
- **Изменяемые scripts:**
  - `Assets/_Project/Quests/Core/QuestWorld.cs` — fix `TryTurnIn` для финального stage (вызывать TryAdvanceStage)
  - `Assets/_Project/Quests/Data/QuestDatabase.asset` — append новых quests (НЕ удалять существующие)

## 5. НЕ в скоупе T-Q22

- Редактор tools (M16/M17)
- Тосты (M15) — игрок увидит изменения в P-табе, без всплывающих уведомлений
- Item ID registry (M14) — используем `itemName` lookup как в `resolveItemId` для TestStageItem
- Branching stages (A→B OR A→C) — linear chain достаточно для верификации
- Save/load для stage transitions (T-Q18 уже это делает)

## 6. Файлы документации

- Этот файл: `docs/dev/T-Q22_DESIGN_NOTE.md`
- Roadmap update: `docs/NPC_quests/08_ROADMAP.md` §8.3.1 (отметить T-Q22)
- Verify checklist: `docs/dev/T-Q22_VERIFY_CHECKLIST.md` (после реализации)

## 7. Критерии готовности

- [ ] `StageIntroDemo.asset` создан с onEnter action
- [ ] `StageMultiDemo.asset` создан с 2 stages (collect → deliver)
- [ ] `[Pickup_TestStageItem]` расставлен в `WorldScene_0_0` рядом с Mira
- [ ] `QuestDatabase.asset` содержит 5 quests (без удаления старых)
- [ ] `TryTurnIn` исправлен — fire `onCompleteActions` для финального stage
- [ ] 0 compile errors
- [ ] User verify: 2 quest теста проходят (см. §3)
