# Inventory Sub-System — Known Issues & TODO

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `20_IMPLEMENTATION_PLAN.md`

Список **известных ограничений** текущей реализации (Phase 7) + **TODO** на Phase 8+.

---

## Известные баги (Phase 7)

### 🟡 MEDIUM: NetworkChestContainer использует СТАРЫЙ NetworkInventory

**Симптом:** Открытие сундука может не работать или работать нестабильно.

**Причина:** `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs:224`:
```csharp
var networkInventory = playerObject.GetComponent<NetworkInventory>();
if (networkInventory != null) {
    foreach (var item in lootItems) {
        int itemId = NetworkInventory.GetItemId(item);
        networkInventory.AddItem(itemId, item.itemType);
    }
}
```

Это вызывает **старый** `NetworkInventory` (который НЕ отправляет snapshot в `InventoryClientState`). UI не обновится.

**Workaround:** Phase 8 — мигрировать `NetworkChestContainer` на новый `InventoryServer.AddItem(clientId, itemId, type)`.

**Файлы для Phase 8:**
- `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs` (line 224-234)
- Возможно: добавить helper в `InventoryServer`: `AddItemFromChest(clientId, itemId, type)` — server-only

**Проверка сейчас:**
- Открой сундук в Play mode
- Если в TAB-колесе / P-табе появились предметы — OK
- Если нет — Phase 8 fix

---

### 🟡 MEDIUM: Scene-placed [InventoryServer] может не спавниться

**Симптом:** Если `[InventoryServer]` имеет `InScenePlacedSourceGlobalObjectIdHash == 0`, NGO 2.x НЕ спавнит его автоматически.

**Решение (уже есть):** `ScenePlacedObjectSpawner` в BootstrapScene находит все `NetworkObject` с `!IsSpawned` и спавнит руками.

**Проверка:**
1. Play → StartHost
2. Console: ищи `ScenePlacedObjectSpawner] Scene (0,0): spawned=N`
3. N должен включать InventoryServer (как включает ContractServer, MarketServer)
4. Если НЕ включает → Phase 8 (либо добавить InventoryServer в список, либо проверить что ScenePlacedObjectSpawner итерирует все NetworkObject)

**Документация:** `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md`

---

### 🟢 LOW: Item_Type1..8 не обновлены (maxStack/weightKg)

**Симптом:** Старые 8 .asset'ов (Item_Type1..8) имеют `maxStack=1, weightKg=0.1` (дефолтные значения ItemData).

**Причина:** Я добавил поля `maxStack` и `weightKg` в `ItemData` (Phase 6), но **не обновлял** существующие .asset'ы.

**Workaround:** Использовать новые `Item_*.asset` (24 шт) для тестов. Старые `Item_Type1..8` оставлены как legacy.

**Phase 8 fix:**
- Либо обновить .asset'ы (AssetDatabase API)
- Либо удалить старые (cleanup) и заменить на новые
- ⚠️ Проверить, что `LootTable` не ссылается на Item_Type1..8 (если ссылается — обновить)

---

### 🟢 LOW: TAB-колесо не закрывается по Esc

**Симптом:** Нажатие Esc не закрывает TAB-колесо (только Tab повторно или кнопка "ЗАКРЫТЬ").

**Причина:** В `InventoryUI.Update` не реализован Esc handler (TODO).

**Workaround:** Использовать Tab повторно или клик "ЗАКРЫТЬ".

**Phase 8 fix:**
```csharp
private void Update() {
    // ...
    if (Keyboard.current.escapeKey.wasPressedThisFrame && IsVisible()) {
        SetVisible(false);
    }
}
```

---

### 🟢 LOW: Use-кнопка не делает ничего

**Симптом:** Клик "ИСПОЛЬЗОВАТЬ" в sublist выводит "Использование предметов — TODO".

**Причина:** `RequestUse` в `InventoryServer` возвращает `InternalError` (stub).

**Phase 8 fix:**
- Реализовать `RequestUseRpc` (нужны use-эффекты: food → heal, medical → apply buff, etc.)
- Подключить к игровой логике

---

### 🟢 LOW: Drop в мир не работает

**Симптом:** `RequestDropRpc` возвращает `InternalError`.

**Причина:** `InventoryWorld.TryDrop` — stub, нет логики спавна `PickupItem` в мире.

**Phase 8 fix:**
- Сервер спавнит `NetworkObject` с `PickupItem` (нужен server-side prefab)
- Удаляет предмет из инвентаря
- Шлёт snapshot обновлённого инвентаря

---

### 🟢 LOW: Stackable inventory не работает (qty=1 hardcoded)

**Симптом:** Каждый `itemId` = 1 quantity. Если подобрать 2 одинаковых предмета — будет 2 слота с qty=1, а не 1 слот с qty=2.

**Причина:** `InventoryData` хранит `List<int> ids` (без quantity). `BuildSnapshot` всегда ставит `quantity=1`.

**Phase 8 fix:**
- Расширить `InventoryData`: добавить `List<int> quantities` параллельно `List<int> ids`
- В `TryPickup` — если такой itemId уже есть и `quantity < maxStack` → merge, иначе новый слот
- В `BuildSnapshot` — `quantity = data.GetQuantityForItem(itemId)`

**Пример миграции:**
```csharp
public struct InventoryData : INetworkSerializable
{
    private List<int> _resourceIds;
    private List<int> _resourceQtys;  // NEW
    // ...
    public void AddOrStack(ItemType type, int itemId, int delta = 1)
    {
        var ids = GetIdsForType(type);
        var qtys = GetQtysForType(type);
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == itemId && qtys[i] < GetMaxStack(itemId))
            {
                qtys[i] += delta;
                return;
            }
        }
        ids.Add(itemId);
        qtys.Add(delta);
    }
}
```

---

### 🟢 LOW: Cargo system (weightKg) не используется

**Симптом:** `ItemData.weightKg` есть, но нигде не суммируется / не проверяется.

**Phase 8+ fix:**
- Суммировать `weightKg * quantity` всех items
- Сравнивать с `cargoCapacity` (пока нет SO для этого)
- Отображать в P-табе "Корабль" / CharacterWindow

---

## TODO на Phase 8+ (следующая сессия)

### 1. Cleanup legacy (отдельная сессия, после verify)

**Условие:** Phase 7 тестирование прошло успешно (Tests #1-13 в `50_TESTING_GUIDE.md`).

**Что удалить:**
- `Assets/_Project/Scripts/Core/Inventory.cs` (локальный, не инстанцируется)
- `Assets/_Project/Scripts/UI/InventoryUI.cs` (старый IMGUI, не инстанцируется)
- `Assets/_Project/Scripts/Player/ItemPickupSystem.cs` (нигде не используется)
- `Assets/_Project/Scripts/Core/NetworkInventory.cs` (legacy, заменён на `InventoryServer`)
- `Assets/_Project/Scripts/Core/ItemDatabaseInitializer.cs` (заменён `InventoryWorld.RegisterAllItems`)

**⚠️ Пре-фикс:** перед удалением — `grep -rn "Inventory\|NetworkInventory\|ItemDatabaseInitializer"` по всему проекту. Убедиться, что никто не ссылается.

**Документация:** `docs/dev/CLEANUP_PLAN_2026-06-05.md` (по аналогии с `C1_CLEANUP_PLAN_2026-06-05.md`)

---

### 2. Multi-client verify (ParrelSync)

**Что:** Реально проверить, что 2 клиента синхронизированы.

**Шаги:**
1. Установить ParrelSync
2. Создать клон проекта
3. Server на A, Client на B
4. Подбор предмета на A → видно на B
5. Открытие сундука на A → видно на B

**Если не работает:** см. §"Multi-client debug" ниже.

---

### 3. Stackable inventory

См. §"🟢 LOW: Stackable inventory не работает" выше.

**Трудоёмкость:** ~1 сессия (расширить `InventoryData`, обновить `BuildSnapshot` + `TryPickup` + `ApplyInventoryFilters`).

---

### 4. Drop в мир (SpawnPickupItem)

См. §"🟢 LOW: Drop в мир не работает" выше.

**Нужно:**
- Server-side `PickupItem` prefab (NetworkObject)
- `RequestDropRpc` спавнит prefab, удаляет из инвентаря
- Snapshot обновляется

**Трудоёмкость:** ~1 сессия.

---

### 5. Drag-and-drop в P-таб sublist

**Что:** Перетаскивание предметов между слотами внутри инвентаря.

**API:** `RequestMove(fromSlot, toSlot)` — уже есть stub.

**Нужно:** Реализовать drag-and-drop в UI Toolkit (есть встроенный, но нужна интеграция с `ListView`).

---

### 6. Иконки для ItemData

**Сейчас:** все 24 .asset'а имеют `icon = null`.

**Нужно:** Создать/найти 24 спрайта, привязать к ItemData.

**Опционально:** Можно использовать placeholder (белый квадрат с буквой типа).

---

### 7. Анимация вспышки при pickup

**Что:** При успешном pickup — сектор TAB-колеса мигает зелёным (как в старом IMGUI `TriggerSectorFlash`).

**Реализация:** USS transition на `.sector-N.sector-just-picked` class.

---

### 8. Cargo / weight system

См. §"🟢 LOW: Cargo system".

---

## Multi-client debug (если не работает)

### Проверка 1: [InventoryServer] в сцене обоих клиентов

```
Project A: Window → Hierarchy → [InventoryServer] exists?
Project B: Window → Hierarchy → [InventoryServer] exists?
```

Должен быть в **обоих**, т.к. BootstrapScene общий.

### Проверка 2: NetworkObject GlobalObjectIdHash

```
Window → Inspector → [InventoryServer] → NetworkObject → Debug
```

`GlobalObjectIdHash` должен быть **ненулевой** после `ScenePlacedObjectSpawner` спавна. Если 0 — не заспавнен.

### Проверка 3: Server RPC processing

```
Console → [InventoryServer] RequestPickupRpc received from client
```

Если НЕТ — сервер не получает RPC. Возможные причины:
- `IsServer == false` на хосте
- NetworkObject не заспавнен
- Клиент подключился к wrong server (port mismatch)

### Проверка 4: Snapshot delivery

```
Console → [NetworkPlayer:1] ReceiveInventorySnapshotTargetRpc
```

Если НЕТ — сервер не отправляет snapshot. Возможные причины:
- `SendSnapshot` не находит NetworkPlayer
- `NetworkPlayer.GetComponent<NetworkPlayer>()` возвращает null

---

## Что делать если найден новый баг

1. **Скриншот** проблемы (ты делаешь, не Mavis)
2. **Console output** (полный, не фильтрованный)
3. **Шаги для repro** (что нажал, что ожидал, что получил)
4. **Пришли мне** в чат

Я починю в следующей сессии.

---

## Заключение

Phase 7 — **готово к тестированию** для базовых сценариев (Tests #1-13). Phase 8+ — cleanup + advanced features.

**Compile state:** 0 errors.
**Coverage:**
- ✅ Pickup (single client)
- ✅ TAB-колесо (UI Toolkit)
- ✅ P-таб (CharacterWindow)
- ✅ Server-authoritative snapshot
- ⚠️ NetworkChestContainer — частично (см. выше)
- ⚠️ Multi-client — не проверено
- ❌ Stackable / Drop / Cargo — TODO
