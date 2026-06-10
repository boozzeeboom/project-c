# M11 — Mira End-to-End Demo (2026-06-08)

> commit message: `M11 — Mira quest full playthrough: FindArtifact + dialog tree + pickup items + rep/credits/attitude`

## Обзор

Полный playthrough квеста Миры. Диалоговое дерево переписано, добавлены 2 pickup предмета,
починена вся цепочка: AcceptItem → AcceptQuest → AddReputation → AddNpcAttitude →
CompleteObjective (TryTurnIn) → GiveCredits + snapshot push. Исправлены 10 багов в условиях,
видимости опций, onEnterActions и DTO credits.

---

## Новые ассеты (3)

### `Assets/_Project/Items/Data/Item_Key_AncientKey.asset`
- ItemData: name="Древний Ключ", type=Resources, description="Старинный ключ..."
- itemId=1 в Pickup

### `Assets/_Project/Items/Data/Item_Crystal_TimeCrystal.asset`
- ItemData: name="Кристалл Времён", type=Resources, description="Пульсирующий..."
- itemId=2 в Pickup

### `Assets/_Project/Items/Data/` (папка)
- Создана для хранения ItemData ассетов

---

## Изменённые файлы (6)

### `Assets/_Project/Scripts/World/WorldScene_0_0.unity`
- Добавлены 2 PickupItem prefab рядом с Mira:
  - `[Pickup_AncientKey]` (itemId=1, itemData=Древний Ключ) — 3m слева
  - `[Pickup_TimeCrystal]` (itemId=2, itemData=Кристалл Времён) — 3m справа

### `Assets/_Project/Quests/Data/Dialogs/MiraDefault.asset` (dialog tree)
- Полностью переписан. 8 dialog nodes:
  - `greeting` — "Приветствую..." 
    - "У тебя есть работа?" → conditional `HasItem(1)>=1`, `hideIfUnavailable:1`
    - "Ну что, нашёл Кристалл?" → conditional `QuestStateEquals(find_artifact, Active)`, `hideIfUnavailable:1`
    - "Пока." → unconditionally visible
  - `offer_quest` — "У меня есть дело..." + "Я помогу" (TakeItem id=1) / "Нет"
  - `accept_thanks` — "Спасибо за ключ..." + "Хорошо" (AcceptQuest type=14)
  - `decline` — "Пока"
  - `check_b` — "Ну что, нашёл?" + "Да" / "Нет"
  - `give_b` — "Дай-ка посмотрю..." + "Отдать" (TakeItem id=2, hasItem cond) / "Стоп"
  - `complete_thanks` — "Невероятно!" — **onEnterActions**: AddRep 25 + AddAtt 10 + CompleteObjective
    + "Спасибо, Мира" (GiveCredits 1000)
  - `no_b` — "Не вижу, иди ищи."
  - `not_yet` — "Ок, как найдёшь — приноси."

### `Assets/_Project/Quests/Data/Quests/FindArtifact.asset`
- Минимизирован: 1 stage, 1 objective (type 4, itemTradeItemId=2)
- Rewards: 0/0/0/0 (всё через dialog actions)
- Prerequisites: 0, discoverable: 1, oneShot: 1

### `Assets/_Project/Quests/Dialogue/DialogueAction.cs`
- Добавлен `AcceptQuest = 14` — auto-accept quest (TryOffer + TryAccept)

### `Assets/_Project/Items/Core/InventoryWorld.cs`
- **BUGFIX**: `BuildSnapshot().credits` — был хардкод 0f, теперь читает из
  `TradeWorld.Instance.Repository.GetCredits(clientId)`. CharacterWindow → P → Персонаж
  показывает реальные кредиты.

### `Assets/_Project/Items/Network/InventoryServer.cs`
- Добавлен `public void PushSnapshot(ulong clientId)` — для вызова из QuestServer.GiveCredits

### `Assets/_Project/Quests/Core/QuestWorld.cs`
- `DialogSession.visibleEdges` — список отфильтрованных edges для index mapping

### `Assets/_Project/Quests/Network/QuestServer.cs` (171 строк изменений)

**10 фиксов в одном файле:**

| # | Фикс | Детали |
|---|------|--------|
| 1 | `QuestStateEquals` — real impl | `return false` когда quest не в логе (был `return true`) |
| 2 | `HasItem` — real impl | `int.TryParse(c.stringParam)` вместо хардкода `CountOf(0)` |
| 3 | `AcceptQuest = 14` handler | TryOffer (idempotent) + TryAccept → quest сразу Active |
| 4 | `hideIfUnavailable` | Фильтрация в `BuildDialogStep`: `!available && hideIfUnavailable` → edge не отправляется клиенту |
| 5 | `visibleEdges` index mapping | `session.visibleEdges` для `RequestAdvanceDialogue` (фильтрованные edge → правильный index) |
| 6 | `CompleteObjective` snapshot push | `SendQuestSnapshotToClient` после успешного `TryTurnIn` |
| 7 | `GiveCredits` inventory push | `InventoryServer.Instance.PushSnapshot(clientId)` после `GiveCredits` |
| 8 | `AddReputation` snapshot | Добавлен `BroadcastReputationChange(clientId)` в handler |
| 9 | `AddNpcAttitude` snapshot | Добавлен `BroadcastNpcAttitudeChange(clientId)` в handler |
| 10 | `onEnterActions` execution | Добавлен цикл `FireDialogAction` для `nextNode.onEnterActions[]` при навигации по edge |

---

## Дизайн-ноты

- `docs/dev/M11_DESIGN_NOTE.md` — полная спецификация flow
- `docs/dev/M11_FIXES_2026-06-08.md` — реестр исправлений
- `docs/dev/T-Q*_DESIGN_NOTE.md` — предшествующие T-тикеты

---

## Тест-план

```yaml
# clean state
rm $env:USERPROFILE\AppData\LocalLow\DefaultCompany\ProjectC_client\quest_state_0.json
rm $env:USERPROFILE\AppData\LocalLow\DefaultCompany\ProjectC_client\inventory_state_0.json

# flow
1. Start host (P → хост)
2. Подобрать [Pickup_AncientKey] (слева от Mira)
3. E → Mira → "У тебя есть работа?" → "Я помогу" → "Хорошо"
4. Console: TakeItem id=1 → AcceptQuest → quest Active
5. P → КВЕСТЫ → quest в "Активных"
6. Подобрать [Pickup_TimeCrystal] (справа от Mira)
7. E → Mira → "Ну что, нашёл?" → "Да" → "Отдать"
8. Console: TakeItem id=2 → AddRep 25 → AddAtt 10 → CompleteObjective → TryTurnIn
9. P → КВЕСТЫ → "Завершённых"
10. P → РЕПУТАЦИЯ → Гильдия Мысли = 25
11. P → ПЕРСОНАЖ → кредиты = 4180 CR
```

## Uncommitted

```
M  Assets/_Project/Items/Core/InventoryWorld.cs
M  Assets/_Project/Items/Network/InventoryServer.cs
M  Assets/_Project/Quests/Core/QuestWorld.cs
M  Assets/_Project/Quests/Data/Dialogs/MiraDefault.asset
M  Assets/_Project/Quests/Data/Quests/FindArtifact.asset
M  Assets/_Project/Quests/Dialogue/DialogueAction.cs
M  Assets/_Project/Quests/Network/QuestServer.cs
M  Assets/_Project/Scenes/World/WorldScene_0_0.unity
M  docs/NPC_quests/08_ROADMAP.md

?? Assets/_Project/Items/Data.meta
?? Assets/_Project/Items/Data/Item_Crystal_TimeCrystal.asset
?? Assets/_Project/Items/Data/Item_Crystal_TimeCrystal.asset.meta
?? Assets/_Project/Items/Data/Item_Key_AncientKey.asset
?? Assets/_Project/Items/Data/Item_Key_AncientKey.asset.meta
?? docs/dev/M11_DESIGN_NOTE.md
?? docs/dev/M11_FIXES_2026-06-08.md
```
