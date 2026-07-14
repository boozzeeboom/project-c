# Bugfix: NRE при сериализации InventoryItemDto (itemName = null)

**Дата:** 2026-07-05
**Связанные файлы:** `InventoryItemDto.cs`, `InventoryWorld.cs`

---

## Симптом

`NullReferenceException` при подборе предмета (PickupItem → Collect → RequestPickup → SendSnapshot):
```
NullReferenceException: Object reference not set to an instance of an object
FastBufferWriter.WriteValueSafe(String s, ...)
InventoryItemDto.NetworkSerialize(...)  ← line 43: serializer.SerializeValue(ref itemName)
```

Инвентарь не отображал предметы после подбора.

## Причина

В `InventoryWorld.BuildSnapshot()` для **Key-предметов** (`ItemType.Key`) поле `itemName` структуры `InventoryItemDto` не заполнялось — оставалось `null` (значение по умолчанию для string в struct). Для обычных предметов `itemName` заполнялся корректно.

`FastBufferWriter.WriteValueSafe` (Netcode for GameObjects) не поддерживает null-строки — бросает NRE.

## Исправление

### 1. `InventoryItemDto.NetworkSerialize` — null-safe serialization

Добавлен `hasName` bool-флаг перед строкой (паттерн как у `InventorySnapshotDto.locationId`):

```csharp
bool hasName = !string.IsNullOrEmpty(itemName);
serializer.SerializeValue(ref hasName);
if (hasName) { /* write string */ }
else { /* set null on reader */ }
```

### 2. `InventoryWorld.BuildSnapshot` — itemName для Key-предметов

Для Key-слотов теперь извлекается имя из `_itemDatabase`:

```csharp
string keyName = null;
if (_itemDatabase.TryGetValue(slot.itemId, out var keyDef))
    keyName = keyDef.itemName;
// ...
itemName = keyName ?? $"Key#{slot.itemId}",
```

## Статус

✅ Компиляция чистая. Требуется playtest.
