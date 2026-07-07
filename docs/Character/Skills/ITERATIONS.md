# ITERATIONS — Items/Weapons Refactoring

## Итерация от 2026-07-24

**Задача:** T-WPN-01-REF-02 — Унификация оружия, Items ↔ Equipment ↔ Skills ↔ Combat рефакторинг
**Коммит:** `ac4e8a7ee91dad1ab6a4956502ba2156f457c58e` — T-WPN-01-REF-02: Унификация оружия — WeaponItemData + ThrowableItemData → единый класс

**Изменения:**
- `WeaponItemData.cs` — +WeaponHandling enum, +WeaponClass.Throwable, +throw-поля, R3 equipSlot fix
- `ThrowableItemData.cs` — 🗑 Удалён
- `InventoryWorld.cs` — R2 публичные методы (GetItemId, IsItemRegistered, RegisterIfMissing)
- `EquipmentServer.cs` — R2 без reflection, R5 прямые вызовы
- `EquipmentWorld.cs` — R5 прямой вызов SkillsWorld
- `SkillsServer.cs` — R5 прямые вызовы StatsServer/EquipmentWorld
- `SkillsWorld.cs` — R5 прямой вызов StatsServer.ApplyXpDirect
- `SkillsClientState.cs` — R4 кэш навыков
- `SkillInputService.cs` — R4 кэш, +Throwable в DescribeMaskShort
- `ICombatDamageProvider.cs` — комментарий
- `WeaponClassCatalog.cs` — +Throwable entry
- 4 `.asset` конвертированы Throwables/ → Weapons/
- 4 старых `.asset` удалены из Throwables/

## Fix 1 — 2026-07-24

**Коммит:** `0698138` — fix: клиентский _itemCache заполняется из ItemRegistry

**Проблема:** На чистом клиенте `InventoryWorld.Instance = null` → `_itemCache` пуст → `GetCachedDefinition` всегда null → все предметы в UI показывались как "Welding Mask".

**Fix:** `_itemCache` заполняется из `ItemRegistry.asset` (Resources SO) на обеих сторонах:
```csharp
var registry = Resources.Load<ItemRegistry>("ItemRegistry");
registry.EnsureLoaded();
foreach (var entry in registry.GetEntries())
    _itemCache[entry.id] = entry.item;
```

## Fix 2 — 2026-07-24

**Коммит:** `e2ad10c` — fix: ID-коллизия в GetOrRegisterItemId/RegisterIfMissing

**Проблема:** `_itemDatabase.Count + 1` коллидировал с существующими ID из ItemRegistry. Граната показывалась как "Antigrav Cable", арбалет как "Antigrav Brass AGL-8".

**Fix:**
1. Следующий свободный ID: `while (_itemDatabase.ContainsKey(newId)) newId++`
2. Поиск по `itemName` как fallback (разные SO-инстансы на сцене)
3. `(Clone)`-suffix обработка

## Fix 3 — 2026-07-24

**Коммит:** `8391461` — fix: itemName в снапшоте DTO вместо lookup по кэшу

**Проблема:** Снапшот нёс только `itemId` (int). Клиент делал lookup в кэше, который не синхронизирован с динамическими ID сервера.

**Fix:** `itemName` добавлен в `InventoryItemDto`. Сервер заполняет в `BuildSnapshot`. `InventoryTab` использует `first.itemName` напрямую.
