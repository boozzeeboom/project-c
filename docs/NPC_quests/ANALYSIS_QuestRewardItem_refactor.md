# Analysis: QuestRewardItem + QuestObjective — поля drag-and-drop для предметов

> **Дата:** 2026-07-20
> **Контекст:** Сейчас в наградах и целях квестов предметы задаются строковым ID. Нужны поля для прямого перетаскивания `.asset` файлов.

---

## 1. Три места, где нужен drag-and-drop

| # | Где | Текущее поле (string) | Тип перетаскиваемого asset | Пример |
|---|-----|----------------------|---------------------------|--------|
| 1 | `QuestReward.items[]` (награда в инвентарь) | `tradeItemId` | **`ItemData`** | `Assets/_Project/Resources/Items/Item_Tech_Hand_Lamp_Beacon-2.asset` |
| 2 | `QuestReward.cargoItems[]` (награда-груз) | `tradeItemId` | **`TradeItemDefinition`** | `Assets/_Project/Trade/Data/Items/TradeItem_resource_iron_box.asset` |
| 3 | `QuestObjective.itemTradeItemId` (HaveItem/DeliverItem) | `itemTradeItemId` | **`ItemData`** | `Assets/_Project/Resources/Items/Item_...` |

> **Важно:** строковые ID (`tradeItemId`, `itemTradeItemId`) — **сохраняем** для CSV-импорта. Новые поля — **дополнительные**, с приоритетом: если `assetRef != null` → используем его, иначе fallback на строку.

---

## 2. Текущий код (AS-IS)

### 2.1 QuestRewardItem (QuestReward.cs:13–21)
```csharp
[Serializable]
public class QuestRewardItem
{
    public string tradeItemId = "";   // ← один string для обоих типов предметов!
    public int count = 1;
}
```
Проблема: `items[]` (ItemData) и `cargoItems[]` (TradeItemDefinition) делят один и тот же класс, с одним полем `tradeItemId`.

**Рантайм-выдача `items[]`** (QuestWorld.cs:577-590):
```
tradeItemId → int.TryParse → int legacyIntId → InventoryWorld.AddItemDirect(clientId, legacyIntId, ItemType.Resources)
```
→ система `ItemData` (int ID).

**Рантайм-выдача `cargoItems[]`** — skipped (T-Q18), но это `TradeItemDefinition`.

### 2.2 QuestObjective.itemTradeItemId (QuestObjective.cs:41-42)
```csharp
[Tooltip("Trade item id (dlya DeliverItem, HaveItem).")]
public string itemTradeItemId = "";
```

**Рантайм-резолв** (QuestWorld.ResolveItemId, стр. 880-903):
```
itemTradeItemId → 1) int.TryParse → 2) ItemRegistry.TryGetIdByName → 3) Resources.LoadAll<ItemData> по itemName
```
→ система `ItemData` (int ID).

---

## 3. План изменений (TO-BE)

### 3.1 QuestRewardItem — два новых поля

```csharp
[Serializable]
public class QuestRewardItem
{
    // Оставляем для CSV
    [Tooltip("TradeItemDefinition.itemId (string). Оставлено для CSV-импорта.")]
    public string tradeItemId = "";

    // НОВОЕ: pickable item (для items[] — инвентарь)
    [Tooltip("Pickable item (ItemData) — перетащи .asset из Resources/Items/. Приоритетнее tradeItemId.")]
    public ItemData pickupItem;

    // НОВОЕ: cargo/trade item (для cargoItems[] — груз)
    [Tooltip("Cargo item (TradeItemDefinition) — перетащи .asset из Trade/Data/Items/. Приоритетнее tradeItemId.")]
    public TradeItemDefinition cargoItem;

    [Min(1)]
    public int count = 1;
}
```

### 3.2 QuestObjective — новое поле

```csharp
public class QuestObjective
{
    // Оставляем для CSV
    [Tooltip("Trade item id (для DeliverItem, HaveItem). Оставлено для CSV-импорта.")]
    public string itemTradeItemId = "";

    // НОВОЕ: pickable item (ItemData) — drag-and-drop
    [Tooltip("Pickable item (ItemData) для HaveItem/DeliverItem. Приоритетнее itemTradeItemId.")]
    public ItemData pickupItem;
    
    // ... остальные поля без изменений
}
```

### 3.3 Рантайм-резолв

**QuestWorld.ResolveItemId** — добавить приём `ItemData`:
```csharp
public static int ResolveItemId(string itemTradeItemId, ItemData pickupItem = null)
{
    // 0. Прямая ссылка на ItemData (новое!)
    if (pickupItem != null)
    {
        var inv = InventoryWorld.Instance;
        if (inv != null)
        {
            int id = inv.GetOrRegisterItemId(pickupItem);
            if (id > 0) return id;
        }
    }
    // 1. int.TryParse (legacy)
    // 2. ItemRegistry.TryGetIdByName
    // 3. Resources.LoadAll<ItemData> fallback
}
```

**QuestWorld.ApplyQuestRewards** — `items[]`:
```csharp
// Было: int.TryParse(ri.tradeItemId, out int legacyIntId)
// Стало:
int itemId = 0;
if (ri.pickupItem != null)
    itemId = inv.GetOrRegisterItemId(ri.pickupItem);
else if (!string.IsNullOrEmpty(ri.tradeItemId))
    itemId = ResolveItemId(ri.tradeItemId);
```

**QuestWorld.ApplyQuestRewards** — `cargoItems[]` (T-Q18+, когда заработает):
```csharp
// Использует ri.cargoItem (TradeItemDefinition ref)
```

### 3.4 Что НЕ меняется

- CSV-импорт (`QuestCsvImporter`) — продолжает писать `tradeItemId` / `itemTradeItemId` строкой
- CSV-экспорт — формат без изменений
- `QuestRewardItem` остаётся общим для `items[]` и `cargoItems[]` (т.к. оба в одном `QuestReward`)

---

## 4. Файлы к изменению

| # | Файл | Что |
|---|------|-----|
| 1 | `Quests/Quests/QuestReward.cs` | Добавить `pickupItem` (ItemData), `cargoItem` (TradeItemDefinition) |
| 2 | `Quests/Quests/QuestObjective.cs` | Добавить `pickupItem` (ItemData) |
| 3 | `Quests/Core/QuestWorld.cs` | `ResolveItemId` — поддержка `ItemData` ref; `ApplyQuestRewards` — резолв через ref |
| 4 | `Quests/Editor/QuestDefinitionValidator.cs` | Валидация новых полей |
| 5 | `Quests/Editor/QuestNodeGraphView.cs` | ObjectField для `pickupItem`/`cargoItem` + `pickupItem` в objectives |
| 6 | `Quests/Editor/QuestGraphView.cs` | Обновить отображение |
| 7 | `Quests/Editor/QuestDatabaseWindow.cs` | Обновить отображение |

---

## 5. Риски

- **`QuestRewardItem`** — общий для двух типов предметов. Дизайнер должен класть `ItemData` в `pickupItem` (для `items[]`) и `TradeItemDefinition` в `cargoItem` (для `cargoItems[]`). Перепутает — в инспекторе это будет видно (разные типы).
- **String ID** — сохраняем, обратная совместимость полная. CSV-импорт не ломается.
- **`Serializable` класс** — добавление полей не ломает существующие `.asset` файлы.
