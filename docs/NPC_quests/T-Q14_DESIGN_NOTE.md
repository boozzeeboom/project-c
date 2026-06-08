# T-Q14 — InventoryServer.TryRemove + event bus hooks (medium, 45 мин)

**Дата:** 2026-06-08
**Roadmap:** `docs/NPC_quests/08_ROADMAP.md` §8.3 T-Q14
**Связь:** 09_OPEN_QUESTIONS.md §J

## Текущее состояние (что уже есть)

✅ **`ItemAddedEvent` published** (от T-X0, через `InventoryWorld.PublishItemAdded`).
✅ **`ItemRemovedEvent` published** (от T-X0, через `InventoryWorld.PublishItemRemoved` в `TryDrop`).
✅ **`QuestServer.OnItemRemoved` handler** — subscribes к `ItemRemovedEvent` и вызывает `TriggerService.Evaluate(playerId, "HaveItem:<itemId>")`.
✅ **`JsonInventoryRepository`** — уже упоминает T-Q14 в комментариях (line 5: "Save on every AddItemDirect / TryRemove (T-Q14)").

## Что нужно сделать (что missing)

❌ **`InventoryWorld.RemoveItems(ulong clientId, int itemId, int count)`** — private method. Симметричный с `AddItemDirect`. Удаляет N штук предмета из конкретного itemType списка, publish `ItemRemovedEvent`, save.
❌ **`InventoryServer.TryRemove(ulong clientId, int itemId, ItemType itemType, int count)`** — public method (server-only). Wrapper для `InventoryWorld.RemoveItems`. Возвращает `InventoryResultDto` (consistent с `AddItem`).
❌ **`InventoryServer.RequestRemoveRpc(int itemId, byte typeByte, int count, RpcParams)`** — client-initiated RPC. Pattern: `RequestDropRpc`. Для T-Q15/T-Q16 не нужен сразу (turn-in будет server-side), но для future use.

## Скоуп

### 1. `InventoryWorld.RemoveItems(ulong, int, int)` — private
- Удаляет N штук itemId из `data.GetIdsForType(itemType)` (по списку, MVP без stack size).
- Если itemId не найден в списке → Fail(InvalidItem, ...).
- Если count > available count → Fail(NotEnough, ...).
- Иначе: `list.Remove(itemId)` × count, save, publish `ItemRemovedEvent`.

### 2. `InventoryServer.TryRemove(ulong, int, ItemType, int)` — public
- Wrapper: validate IsServer, call `RemoveItems`, return result.
- Если IsSuccess → `SendSnapshot(clientId, null)`.

### 3. `InventoryServer.RequestRemoveRpc` — RPC
- Client-initiated: T-Q15 turn-in не использует (он server-side из QuestServer), но для будущего dialogue option "Сдать предмет" — useful.
- Rate limit + publish event.

### 4. Дизайн-решения

- **Не трогаем `AddItem`/`AddItemDirect`** — они работают, T-Q14 только про removal.
- **API consistency:** `TryRemove(ulong, int, ItemType, int count)` — последний параметр это count, не quantity (избегаем naming mismatch с drop API).
- **`InventoryResultCode`:** добавить ли `NotEnough`? Если уже есть — reuse, иначе fallback на `InvalidItem`.

## Файлы для изменения

1. `Assets/_Project/Items/Core/InventoryWorld.cs` — +`RemoveItems(ulong, int, int)` private method.
2. `Assets/_Project/Items/Network/InventoryServer.cs` — +`TryRemove(ulong, int, ItemType, int)` public + `RequestRemoveRpc` RPC.

## Verify

1. **Compile:** 0 errors.
2. **Play Mode test:**
   - `InventoryWorld.Instance.AddItemDirect(0, 1, ItemType.Resources)` → 1 item.
   - `InventoryWorld.Instance.RemoveItems(0, 1, 1)` (через `InventoryServer.TryRemove`) → 0 items, `ItemRemovedEvent` published.
   - Console: `[InventoryServer] TryRemove: client=0 itemId=1 count=1 OK`.

3. **QuestServer.OnItemRemoved check:**
   - При remove → `ItemRemovedEvent` → `QuestServer.OnItemRemoved` → `TriggerService.Evaluate` → если есть quest objective `HaveItem:1` → advance. (Это уже работает — мы только добавляем сам TryRemove.)

## Что НЕ делаем

- **`QuestServer.RequestTurnInQuestRpc` real impl** — это T-Q15.
- **Quest rewards которые дают items** — T-Q15 (`QuestReward` item rewards через `InventoryServer.AddItem`).
- **`InventoryResultCode.NotEnough`** — если нет, добавлю в enum как safety. (Но `RemoveItems` только для server-side use — fail = log, не отправляем client result.)

## Открытые вопросы

1. **`RequestRemoveRpc` нужен сейчас?** T-Q14 plan говорит "Add `TryRemove(ulong, int, int)` public method" + "Verify `ItemAddedEvent` already published". RPC не упомянут. Но без RPC — `TryRemove` можно вызвать только из server-side кода. Это и нужно для T-Q15 (turn-in). **Решение:** добавлю `TryRemove` (must) + `RequestRemoveRpc` (optional, для future).
2. **Naming:** `count` vs `quantity`? В drop API — `quantity`. В TurnIn quest reward API — будет `count`. Делаю `count` (в `RemoveItems`) и `quantity` (в `RequestRemoveRpc` RPC param, consistent с drop).
