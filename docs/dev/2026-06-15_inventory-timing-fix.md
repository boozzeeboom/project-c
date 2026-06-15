# 2026-06-15: Inventory — BootstrapScene spawn timing fix

## TL;DR
Два бага, оба из-за порядка инициализации в BootstrapScene:

### Баг 1: Mining — "Инвентарь полон" на пустом инвентаре
**Корень**: `ResourceNode.Config.ResolveItemIds()` вызывается в `OnNetworkSpawn`.
Если ResourceNode спавнится раньше `InventoryServer`, то `InventoryWorld.Instance == null`
→ `_resultItemId` остаётся -1 → `AddItemDirect(-1)` → `ItemNotFound`.

ResourceNode маскирует реальную ошибку в хардкод `"Инвентарь полон"` (ResourceNode.cs:329).

**Фикс**: Re-resolve itemId в `CompleteGather()` перед `AddItemDirect`:
```csharp
if (_config.ResultItemId <= 0) _config.ResolveItemIds();
```
Плюс `Debug.LogWarning` с реальным кодом ошибки.

### Баг 2: Pickup — предметы исчезают со сцены, но не попадают в инвентарь
**Корень**: `PickupItem.Collect()` подписывался на **глобальное** событие
`InventoryClientState.OnInventoryResult`. Если результат от другого вызова
(ExchangeUnpack, другой PickupItem) приходил раньше — `HandlePickupResult`
деактивировал GameObject по чужому успешному результату.

**Фикс**: 
- Добавлен per-operation callback `InventoryClientState._pendingPickupCallback`
- `RequestPickup` overload с `Action<InventoryResultDto> onResult`
- `OnResultReceived` дёргает callback ДО глобального события
- PickupItem больше не подписывается на `OnInventoryResult`
- `HandlePickupResult` стал прямым колбэком, без unsubscribe

## Changed files
- `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs`
- `Assets/_Project/Items/Client/InventoryClientState.cs`
- `Assets/_Project/Scripts/Core/PickupItem.cs`

## Verification
1. Open BootstrapScene in Unity Editor
2. Enter Play Mode (хост)
3. Подойти к ResourceNode → нажать F → дождаться сбора
   - Должен появиться toast о получении предмета
   - Инвентарь (P) должен показать +1 предмет
4. Найти PickupItem на сцене → подойти → нажать E
   - Предмет должен исчезнуть И появиться в инвентаре
5. Проверить консоль (через `read_console`):
   - `[ResourceNode] Gather INTERRUPTED` — не должно быть (если есть — смотрим `code=` в Warning)
   - `[InventoryWorld] Player X picked up ID=Y` — должен быть при успешном pickup
   - `[InventoryServer] OnNetworkSpawn. IsServer=true, _itemCache=N` — проверка что инвентарь жив

## Risks
- Per-operation callback thread-safety: нет (Unity single-thread)
- Multiple pickups: только один активный pickup на клиента (`_pendingPickupCallback` перезаписывается)
- Если `_pendingPickupCallback` не null при старте pickup — Warning в консоли
