# Analysis: QuestRewardItem — переход от tradeItemId (string) к прямой ссылке на TradeItemDefinition

> **Дата:** 2026-07-20
> **Контекст:** Сейчас в наградах квестов предметы задаются строковым ID (`tradeItemId`). Нужно добавить возможность drag-and-drop префаба/итема (`TradeItemDefinition`) прямо в поле инспектора.

---

## 1. Текущая архитектура (AS-IS)

### 1.1 QuestRewardItem (QuestReward.cs:13–21)

```csharp
[Serializable]
public class QuestRewardItem
{
    [Tooltip("TradeItemDefinition.itemId (string).")]
    public string tradeItemId = "";

    [Min(1)]
    public int count = 1;
}
```

Это `[Serializable]` POCO, встроенный в `QuestReward`, который в свою очередь встроен в `QuestDefinition` (ScriptableObject).

**Два контекста использования** (одно и то же поле `tradeItemId`):
- `QuestReward.items[]` — предметы в **character inventory** (инвентарь персонажа)
- `QuestReward.cargoItems[]` — **cargo items** (груз на корабль)

**Текущий flow награды (`QuestWorld.ApplyQuestRewards`, стр. 572–593):**
```
tradeItemId (string) → int.TryParse → int legacyIntId → InventoryWorld.AddItemDirect(clientId, legacyIntId, ItemType.Resources)
```

Проблема: ID парсится как `int`, хотя `TradeItemDefinition.itemId` — это string (например `"copper_ore"`). Это legacy-поведение (T-Q19 cleanup).

### 1.2 TradeItemDefinition (TradeItemDefinition.cs)

```csharp
[CreateAssetMenu(fileName = "TradeItem_", menuName = "ProjectC/Trade Item")]
public class TradeItemDefinition : ScriptableObject
{
    public string itemId;        // ← это то, что сейчас пишут в tradeItemId
    public string displayName;
    public Sprite icon;
    public float basePrice;
    public float weight;
    public float volume;
    public int slots;
    public bool isDangerous;
    public bool isFragile;
    public bool isContraband;
    public Faction requiredFaction;
}
```

`TradeItemDefinition` — это ScriptableObject. Именно его пользователь хочет перетаскивать в поле награды.

### 1.3 Где QuestRewardItem затронут

| Файл | Строки | Роль |
|------|--------|------|
| `Quests/Quests/QuestReward.cs` | 13–21 | Определение `QuestRewardItem` |
| `Quests/Quests/QuestReward.cs` | 62, 65 | `items[]` и `cargoItems[]` → оба типа `QuestRewardItem[]` |
| `Quests/Core/QuestWorld.cs` | 572–593 | ApplyQuestRewards — рантайм-выдача `items` |
| `Quests/Core/QuestWorld.cs` | 596–599 | ApplyQuestRewards — `cargoItems` (пока skipped) |
| `Quests/Editor/QuestCsvImporter.cs` | 678–691 | `ParseRewardItems()` — CSV → `QuestRewardItem` |
| `Quests/Editor/QuestCsvExporter.cs` | 111–113 | Экспорт `items` в CSV |
| `Quests/Editor/QuestDefinitionValidator.cs` | 236–247 | Валидация `rewards.items[]` |
| `Quests/Editor/QuestNodeGraphView.cs` | 282, 301–308 | Визуализация + edit полей наград |
| `Quests/Editor/QuestGraphView.cs` | 266, 381 | Визуализация наград в старом графе |
| `Quests/Editor/QuestDatabaseWindow.cs` | 284–287 | Отображение наград в DatabaseWindow |

---

## 2. Целевая архитектура (TO-BE)

### 2.1 Новый QuestRewardItem

```csharp
[Serializable]
public class QuestRewardItem
{
    // Старое поле — оставить для обратной совместимости и CSV-импорта
    [Tooltip("TradeItemDefinition.itemId (string). Устарело — используй tradeItem.")]
    public string tradeItemId = "";

    // НОВОЕ: прямая ссылка на TradeItemDefinition.
    // Приоритет: если tradeItem != null → используем его itemId, иначе fallback на tradeItemId.
    [Tooltip("Прямая ссылка на TradeItemDefinition (перетащи .asset сюда). Приоритетнее tradeItemId.")]
    public TradeItemDefinition tradeItem;

    [Min(1)]
    public int count = 1;

    /// <summary>Resolved itemId: предпочитает tradeItem.itemId, fallback на tradeItemId.</summary>
    public string ResolvedItemId => tradeItem != null ? tradeItem.itemId : tradeItemId;
}
```

### 2.2 Обновлённый ApplyQuestRewards

```csharp
// Было (QuestWorld.cs:578-583):
if (!int.TryParse(ri.tradeItemId, out int legacyIntId))
{
    Debug.LogWarning($"... tradeItemId='{ri.tradeItemId}' не конвертируется в int ...");
    continue;
}
var result = inv.AddItemDirect(clientId, legacyIntId, ProjectC.Items.ItemType.Resources);

// Станет:
string resolvedId = ri.ResolvedItemId;
if (string.IsNullOrEmpty(resolvedId))
{
    Debug.LogWarning($"[QuestWorld] ApplyQuestRewards: items[{i}] имеет пустой tradeItemId и tradeItem=null");
    continue;
}
// T-Q19: переходим на string-based itemId (TradeItemDefinition.itemId) вместо legacy int
var result = inv.AddItemDirectByStringId(clientId, resolvedId, ProjectC.Items.ItemType.Resources);
```

> **Важно:** `AddItemDirect` сейчас принимает `int`. Нужно либо добавить перегрузку/новый метод с `string itemId`, либо делать lookup `TradeItemDefinition` → `int` mapping в `ItemRegistry`. Это требует дополнительного investigation `InventoryWorld` API.

### 2.3 cargoItems — отдельный вопрос

`cargoItems` сейчас skipped в `ApplyQuestRewards` (T-Q18 out of scope). Когда cargo-выдача будет имплементирована, ей тоже понадобится `ResolvedItemId`. Рефакторинг `QuestRewardItem` автоматически покроет оба случая.

---

## 3. План изменений (checklist)

### Phase A — Data Model (QuestReward.cs)
- [ ] A1. Добавить `using ProjectC.Trade;` (или ссылку на namespace где лежит `TradeItemDefinition`)
- [ ] A2. Добавить поле `public TradeItemDefinition tradeItem;`
- [ ] A3. Добавить `public string ResolvedItemId => tradeItem != null ? tradeItem.itemId : tradeItemId;`
- [ ] A4. Обновить комментарии/tooltip'ы

### Phase B — Runtime (QuestWorld.cs)
- [ ] B1. Заменить `ri.tradeItemId` → `ri.ResolvedItemId` в `ApplyQuestRewards`
- [ ] B2. Решить вопрос string-based `AddItemDirect` (см. §4)

### Phase C — Editor / CSV
- [ ] C1. `QuestCsvImporter.ParseRewardItems` — оставить без изменений (CSV задаёт `tradeItemId` строкой)
- [ ] C2. `QuestCsvExporter` — экспортировать `ResolvedItemId`
- [ ] C3. `QuestDefinitionValidator` — добавить проверку что `tradeItem != null || !string.IsNullOrEmpty(tradeItemId)`
- [ ] C4. `QuestNodeGraphView` — добавить поле для `tradeItem` в инспекторе (ObjectField)
- [ ] C5. `QuestGraphView` (старый) — обновить отображение
- [ ] C6. `QuestDatabaseWindow` — обновить отображение

### Phase D — Тестирование
- [ ] D1. Проверить `collect_copper_ore.asset` — задать `tradeItem` через инспектор
- [ ] D2. Проверить CSV import — старые CSV должны работать (только `tradeItemId`)
- [ ] D3. Проверить ApplyQuestRewards в рантайме (Play Mode)

---

## 4. ДВЕ РАЗДЕЛЬНЫЕ СИСТЕМЫ ПРЕДМЕТОВ (КЛЮЧЕВОЙ АРХИТЕКТУРНЫЙ ФАКТ)

В проекте существует **два независимых типа предметов**, и между ними **нет прямой связи**:

### 4.1 TradeItemDefinition (торговая система)
- **Файл:** `Assets/_Project/Trade/Scripts/TradeItemDefinition.cs`
- **Тип:** `ScriptableObject` с `itemId: string` (например `"copper_ore"`)
- **Поля:** `itemId`, `displayName`, `icon`, `basePrice`, `weight`, `volume`, `slots`, `isDangerous`, `isFragile`, `isContraband`, `requiredFaction`
- **Используется:** в торговле, контрактах. Это то, что пользователь хочет **drag-and-drop** в награду.

### 4.2 ItemData (инвентарная система)
- **Файл:** `Assets/_Project/Scripts/Core/ItemType.cs`
- **Тип:** `ScriptableObject` с `itemName: string`
- **Поля:** `itemName`, `itemType` (enum Resources/Equipment/...), `description`, `icon`, `equipSlot`, `maxStack`, `weightKg`, `visualPrefab`
- **Регистрируется:** в `InventoryWorld._itemDatabase` (Dictionary<int, ItemData>)
- **Используется:** `InventoryWorld.AddItemDirect(clientId, int itemId, ItemType)` — принимает `int`, а не `string`!

### 4.3 Связь между ними
```
TradeItemDefinition.itemId (string)  ←???→  ItemData.itemName (string)
                                              ↓
                                   InventoryWorld._itemDatabase[int] → ItemData
```
**Прямой связи нет.** `ItemData` не содержит ссылки на `TradeItemDefinition`, и наоборот.

### 4.4 Текущий «костыль» в QuestWorld.ApplyQuestRewards (стр. 577-583)
```csharp
var ri = reward.items[i];
if (!int.TryParse(ri.tradeItemId, out int legacyIntId))  // ← парсит строку как int!
{
    Debug.LogWarning($"... tradeItemId='{ri.tradeItemId}' не конвертируется в int ...");
    continue;
}
var result = inv.AddItemDirect(clientId, legacyIntId, ProjectC.Items.ItemType.Resources);
```
Это работает только если `tradeItemId` — ЧИСЛО-строка (например `"5"`), а не строковый ID типа `"copper_ore"`.

---

## 5. РЕШЕНИЕ: маппинг TradeItemDefinition → ItemData

Нужно добавить мост между системами. **Рекомендуемый подход:**

### 5.1 Добавить поле в ItemData: ссылку на TradeItemDefinition
```csharp
// ItemData.cs — новое поле
[Tooltip("Связанный TradeItemDefinition (для торговли/квестов). Опционально.")]
public TradeItemDefinition tradeItemRef;
```

### 5.2 Либо построить runtime-маппинг по имени
В `InventoryWorld` (или `QuestWorld`) добавить кэш:
```csharp
private Dictionary<string, int> _tradeItemIdToInventoryId; // "copper_ore" → 5
```
Построить через сопоставление `TradeItemDefinition.itemId` ↔ `ItemData.itemName` (или через `tradeItemRef`).

### 5.3 Итоговый flow
```
QuestRewardItem.tradeItem (TradeItemDefinition ref, drag-and-drop)
    ↓ ResolvedItemId = tradeItem.itemId (или tradeItemId для CSV)
    ↓
маппинг: string itemId → int inventoryId
    ↓
InventoryWorld.AddItemDirect(clientId, inventoryId, ItemType.Resources)
```

---

## 6. Что именно изменить (конкретный план)

### Phase A — QuestRewardItem (QuestReward.cs)
- [ ] A1. Добавить `using ProjectC.Trade;`
- [ ] A2. Добавить `public TradeItemDefinition tradeItem;`
- [ ] A3. Добавить `public string ResolvedItemId => tradeItem != null ? tradeItem.itemId : tradeItemId;`

### Phase B — Маппинг (QuestWorld.cs)
- [ ] B1. Добавить `Dictionary<string, int> _tradeToInventoryMap`
- [ ] B2. При инициализации пройти по `InventoryWorld.Instance.GetAllItems()`, для каждого `ItemData` найти соответствующий `TradeItemDefinition` (по имени `itemName` ↔ `itemId`, либо через новое поле `tradeItemRef`)
- [ ] B3. В `ApplyQuestRewards` делать резолв через маппинг, убрать `int.TryParse`

### Phase C — ItemData связь (ItemType.cs)
- [ ] C1. Добавить `public TradeItemDefinition tradeItemRef;` (опционально — позволяет дизайнеру явно связать)

### Phase D — Editor / CSV
- [ ] D1. `QuestCsvImporter.ParseRewardItems` — без изменений (работает через `tradeItemId` строкой)
- [ ] D2. `QuestCsvExporter` — использовать `ResolvedItemId`
- [ ] D3. `QuestDefinitionValidator` — валидировать `tradeItem != null || !string.IsNullOrEmpty(tradeItemId)`
- [ ] D4. `QuestNodeGraphView` — ObjectField для `tradeItem`
- [ ] D5. `QuestGraphView` / `QuestDatabaseWindow` — обновить отображение

---

## 7. Файлы, которые нужно изменить

| # | Файл | Что |
|---|------|-----|
| 1 | `Assets/_Project/Quests/Quests/QuestReward.cs` | Добавить `tradeItem` (TradeItemDefinition ref) + `ResolvedItemId` |
| 2 | `Assets/_Project/Scripts/Core/ItemType.cs` | Добавить `tradeItemRef` (связь ItemData → TradeItemDefinition) |
| 3 | `Assets/_Project/Quests/Core/QuestWorld.cs` | Добавить маппинг `_tradeToInventoryMap`, переписать `ApplyQuestRewards` |
| 4 | `Assets/_Project/Quests/Editor/QuestDefinitionValidator.cs` | Валидация `tradeItem != null \|\| !string.IsNullOrEmpty(tradeItemId)` |
| 5 | `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` | ObjectField для `tradeItem` |
| 6 | `Assets/_Project/Quests/Editor/QuestGraphView.cs` | Обновить отображение наград |
| 7 | `Assets/_Project/Quests/Editor/QuestDatabaseWindow.cs` | Обновить отображение наград |
| 8 | `Assets/_Project/Quests/Editor/QuestCsvExporter.cs` | `ResolvedItemId` в экспорте |

---

## 8. Риски

- **Двойная система предметов (§4):** `TradeItemDefinition` и `ItemData` — независимые SO. Пока между ними нет программной связи, маппинг строится по имени. Если имена не совпадают — предмет не найдётся.
- **Рекомендация:** добавить поле `tradeItemRef` в `ItemData` для явной связи. Без этого маппинг хрупкий.
- **Ссылочная целостность:** если `TradeItemDefinition.asset` удалён или перемещён — ссылка станет `null`. `ResolvedItemId` fallback на `tradeItemId` решает это.
- **Merge conflict:** `QuestRewardItem` — `[Serializable]` класс, изменение полей не ломает существующие `.asset` (Unity добавит новый field с default=null).
- **CSV pipeline:** CSV-импорт продолжает писать `tradeItemId` строкой, `tradeItem` остаётся `null`.

---

## 9. cargoItems

`cargoItems` использует тот же тип `QuestRewardItem[]` (стр. 65). Сейчас cargo-выдача skipped (T-Q18). Когда cargo-система заработает, `ResolvedItemId` + маппинг автоматически покроют оба случая — и `items`, и `cargoItems`.
