# M14 — Item ID system (single source of truth)

> **Дата:** 2026-06-09
> **Сессия:** M14 (T-Q26, T-Q27, T-Q28)
> **Roadmap:** расширяет `08_ROADMAP.md` §8.3.3
> **Статус:** 📋 AUDITED + DESIGN — реализация после подтверждения
> **Зависимости:** M13 ✅, M15 ✅

---

## 1. Audit: текущее состояние (2026-06-09)

**Сейчас работает, но fragile:**

`InventoryWorld` (`Assets/_Project/Items/Core/InventoryWorld.cs`):
```csharp
private void RegisterAllItems() {
    var allResources = Resources.LoadAll<ItemData>("Items");
    int id = 1;
    foreach (var item in allResources) RegisterItem(id++, item);
}
public int GetOrRegisterItemId(ItemData item) {
    foreach (var kvp in _itemDatabase) if (kvp.Value == item) return kvp.Key;
    int newId = _itemDatabase.Count + 1;
    RegisterItem(newId, item);
    return newId;
}
```

`QuestWorld` (`Assets/_Project/Quests/Core/QuestWorld.cs`):
```csharp
public static int ResolveItemId(string itemTradeItemId) {
    if (int.TryParse(itemTradeItemId, out int direct) && direct > 0) return direct;
    var allItems = Resources.LoadAll<ProjectC.Items.ItemData>("Items");
    for (int i = 0; i < allItems.Length; i++) {
        if (allItems[i] == null) continue;
        if (allItems[i].itemName == itemTradeItemId) return i + 1;  // load order = id
    }
    return 0;
}
```

**Проблемы:**

1. **Две независимые нумерации** (по случайности совпадают сейчас):
   - `InventoryWorld.GetOrRegisterItemId`: id = первый зарегистрированный в _itemDatabase (alphabetical order из Resources)
   - `QuestWorld.ResolveItemId`: id = `i + 1` где i = индекс в `Resources.LoadAll` (тоже alphabetical)
   - ⚠️ При добавлении ItemData в **произвольный** Resources, или при изменении Resources folder, id **разъедутся** silently → quest objective HaveItem("Медная руда") не сматчится с pickup.

2. **Скрытая зависимость от Resources folder structure:**
   - ItemData вне `Resources/Items/` → `ResolveItemId` → 0 → objective fail silent
   - Зарегистрируется только через `GetOrRegisterItemId` (id = Count+1) — но quest lookup **не найдёт**

3. **Fragile string-param в dialogue actions:**
   - `DialogueAction.GiveItem / TakeItem` используют `stringParam` (itemName) — ищется по itemName (Resources.LoadAll scan)
   - `DialogueAction` не имеет explicit `itemId` / `itemType` параметров
   - При добавлении item'а с не-уникальным name — silent conflict

## 2. Audit numbers (real dump 2026-06-09)

```
InventoryWorld._itemDatabase (registration order):
  id=1 -> Антиграв-камень большой
  id=2 -> Антиграв-камень малый
  ...
  id=25 -> TestStageItem
  id=26 -> Медная руда
  ...

Resources.LoadAll<ItemData>('Items') (load order):
  idx=0 (id would be 1) -> Антиграв-камень большой
  idx=1 (id would be 2) -> Антиграв-камень малый
  ...
  idx=25 (id would be 26) -> Медная руда
  ...

→ СЕЙЧАС совпадают, но при добавлении item может разойтись.
```

## 3. Что в скоупе M14

### T-Q26 — ItemRegistry single source of truth (medium, ~1.5 ч)

**Скоуп:**
- `Assets/_Project/Items/Core/ItemRegistry.cs` — singleton SO, явный `id ↔ ItemData` mapping
  - `RegisterItem(int id, ItemData item)` — explicit
  - `TryGetItem(int id, out ItemData item)` — lookup
  - `TryGetId(ItemData item, out int id)` — reverse lookup
  - `GetAllItems()` — для UI picker
- InventoryWorld и QuestWorld ссылаются на `ItemRegistry.Instance`
- `RegisterAllItems()` → read from `ItemRegistry.Instance` (не дублировать registration)
- `ResolveItemId(string name)` → `ItemRegistry.TryGetId(itemName)` (no Resources.LoadAll)

**Файлы:** `ItemRegistry.cs` (new) + InventoryWorld.cs, QuestWorld.cs (modify)
**Verify:** Roslyn dumps ids → ID одинаковы в обоих системах после init

**Risk:** medium (требует coord Inventory + Quest init order)

### T-Q27 — DialogueAction itemId param (small, ~0.5 ч)

**Скоуп:**
- `DialogueAction.cs` — add `public int itemId = 0;` + `public ItemType itemType = ItemType.None;`
- `QuestServer.FireDialogAction` — для GiveItem/TakeItem использовать `action.itemId` вместо парсинга `action.stringParam`
- Backward compat: если `itemId == 0` → fallback на stringParam parse (для legacy quest assets)

**Файлы:** DialogueAction.cs, QuestServer.cs
**Verify:** Quest asset с GiveItem action показывает item pickup в inventory

**Risk:** low (backward compatible)

### T-Q28 — Migration string-id → int-id (small, ~0.5 ч)

**Скоуп:**
- Audit всех quest assets, dialog assets: где `itemTradeItemId` / `stringParam` использует item name → migrate to int id
- Pre-existing quest assets: `collect_copper_ore.itemTradeItemId = "Медная руда"` → `26` (lookup через ItemRegistry)
- `MiraDefault.asset` dialog actions: `stringParam = "ancient_key"` → int id lookup
- `FindArtifact.asset` objectives: same

**Файлы:** CollectCopperOre.asset, StageMultiDemo.asset, FindArtifact.asset, MiraDefault.asset (modify via Roslyn)
**Verify:** Все квесты по-прежнему работают без change в gameplay

**Risk:** low (cosmetic data migration)

**Общий effort M14:** ~2.5 ч, low-medium risk.

## 4. Альтернативный подход (НЕ рекомендую)

Можно оставить как есть + добавить **assert at init**: `InventoryWorld._itemDatabase.Keys` == `QuestWorld.ResolveItemId(allResources)` ids. **Не лечит** root cause (две нумерации), только catching.

## 5. Не в скоупе M14

- New ItemData creation tool (EditorWindow) — это M16
- Item trading UI (market) — это T-X1 уже ✅
- Item data validation — DTO schema уже

## 6. Файлы

**New:**
- `Assets/_Project/Items/Core/ItemRegistry.cs`
- `Assets/_Project/Items/Data/ItemRegistry.asset` (instance, populated by editor tool M16)

**Modified:**
- `InventoryWorld.cs` — register from ItemRegistry instead of Resources.LoadAll
- `QuestWorld.cs` — `ResolveItemId` uses ItemRegistry.TryGetId
- `DialogueAction.cs` — +itemId, +itemType
- `QuestServer.cs` — FireDialogAction uses itemId
- 4 quest/dialog assets — string→int migration

## 7. Критерии готовности

- [ ] ItemRegistry SO создан, populated (32 items, ids 1-32)
- [ ] InventoryWorld регистрирует items через ItemRegistry (один источник)
- [ ] QuestWorld.ResolveItemId использует ItemRegistry
- [ ] DialogueAction.itemId/itemType работают
- [ ] All quest assets migrated
- [ ] 0 compile errors
- [ ] Все существующие квесты по-прежнему проходятся
