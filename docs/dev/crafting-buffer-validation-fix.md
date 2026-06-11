# Crafting: buffer validation fix (T-C07c)

## TL;DR
`StartCraftRpc` не проверял наличие ингредиентов в buffer. На хосте (clientId=0) check owner проходил автоматически, позволяя запускать крафт с пустым buffer'ом → бесплатный предмет.

## Bugs

### Bug 1: StartCraftRpc без валидации buffer
- Файл: `CraftingServer.cs`, метод `StartCraftRpc`
- Owner check: `job.OwnerClientId != clientId` → на хосте 0==0 → pass
- Buffer не проверяется вообще → пустой committed → крафт завершается → предмет выдаётся
- **Фикс**: проверка `job.Buffer.Count == 0` + матчинг ингредиентов с рецептом

### Bug 2: AddIngredientRpc bypass при null InventoryWorld
- Файл: `CraftingServer.cs`, метод `AddIngredientRpc`
- Если `InventoryWorld.Instance == null`, проверка RemoveItems пропускается
- Предмет «добавляется» в буфер без списания из инвентаря
- **Фикс**: возвращать ошибку вместо silent skip

## Fixes

1. `StartCraftRpc` — после recipe check добавить:
   - `if (job.Buffer.Count == 0) → Denied(InvalidArgs, "Нет ингредиентов")`
   - Optional: валидация что buffer содержит минимум то что нужно для recipe

2. `AddIngredientRpc` — изменить блок `if (invWorld == null)`:
   - Сейчас: Warning + continue
   - Фикс: `return Denied(InternalError, "Inventory not ready")`
