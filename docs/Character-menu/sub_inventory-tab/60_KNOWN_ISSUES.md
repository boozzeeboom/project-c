# Inventory Sub-System — Known Issues & TODO

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `20_IMPLEMENTATION_PLAN.md`

Список **известных ограничений** текущей реализации (Phase 7) + **TODO** на Phase 8+.

---

## Известные баги (Phase 7)

### ✅ РЕШЕНО (R3-005, 2026-06-05): NetworkChestContainer мигрирован на v2

**Было:**
- `NetworkChestContainer.RequestOpenChestServerRpc` использовал `playerObject.GetComponent<NetworkInventory>()` (старый компонент)
- NetworkInventory **НЕ отправлял** snapshot в `InventoryClientState` → UI не обновлялся
- Также: `clientId = NetworkManager.Singleton.LocalClientId` (на dedicated server это ID хоста, не того кто открыл)
- И: `InvokePermission` не задан (deprecated API warning)

**Стало:**
- `RequestOpenChestServerRpc(RpcParams rpcParams = default)` с `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]`
- `clientId = rpcParams.Receive.SenderClientId` (правильный, кто реально отправил)
- Сначала пытается `InventoryServer.Instance?.AddItem(clientId, itemId, itemType)` (v2)
- Fallback на `NetworkInventory` (legacy safety net)
- `Console: [NetworkChestContainer] v2 AddItem: itemId=N, type=..., ok=True` — для дебага

**Файлы:**
- `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs` (строки 170-265)
- `docs/Character-menu/sub_inventory-tab/70_CHEST_PICKUP_TESTS.md` — manual tests

**Verification:** тестовые сундуки + pickup в WorldScene_0_0 @ (40000, 2512, 40000), 3 chest + 6 pickup.

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

## 11. Phase 8 — Bugfixes (2026-06-05)

Три бага найдены и исправлены в этой сессии (после первой попытки тестирования).

### 11.1 БАГ #1: `[InventoryServer]` не заспавнен

**Симптом:** Chest pickup → нет snapshot → TAB-колесо пустое (в прошлой итерации).

**Root cause:**
- `[InventoryServer]` (scene-placed в BootstrapScene) имеет `InScenePlacedSourceGlobalObjectIdHash = 0`
- NGO scene manager **не спавнит** объекты с `hash=0` автоматически
- `ScenePlacedObjectSpawner.SpawnInAllLoadedScenes()` фильтровал только `WorldScene_*` — **BootstrapScene пропускалась**

**Fix:** `ScenePlacedObjectSpawner.cs` — убрал фильтр `scene.name.StartsWith("WorldScene_")`. Теперь для не-WorldScene сцен спавним все scene-placed NetworkObject вручную.

**Verify:** `Scene(0, 0): spawned=12, already=0, failed=0` (12 = InventoryServer + MarketServer + ContractServer + CloudManager + ServerWeatherController + остальные сцен-placed).

### 11.2 БАГ #2: Pickup не добавлял в инвентарь

**Симптом:** "предметы визуально подбираются, но в инвентаре не числятся".

**Root cause:** `NetworkPlayer.TryPickup()` (строки 578-588) вызывал **`_inventory.AddItem(_nearestPickup.itemData)`**, но `_inventory == null` после Phase 4 (SpawnInventory заноплен). Предмет деактивировался через RPC, но **никогда не попадал в InventoryClientState**.

**Fix:** `NetworkPlayer.cs` — fallback на v2: `if (_inventory != null) _inventory.AddItem(...) else _nearestPickup.Collect()`. `PickupItem.Collect()` шлёт `RequestPickup` через `InventoryClientState → InventoryServer.RequestPickupRpc`.

**Verify:** `PickupItem <Name> успешно подобран` + `InventoryWorld Player 0 picked up ID=7 (Food). Total: 5`.

### 11.3 БАГ #3: P-таб пуст при первом клике (нужно toggle через Рынок)

**Симптом:** P → Инвентарь (1-й клик) — пусто. P → Рынок → Инвентарь (3-й клик) — работает.

**Root cause (двухслойный):**

1. **Lazy-subscribe race condition:** В `EnsureBuilt` (OnEnable) `InventoryClientState.Instance` мог быть `null` (NetworkManagerController.Awake ещё не создал root GO — Unity script execution order race). Подписка `OnSnapshotUpdated += HandleInventorySnapshotUpdated` пропускалась. После StartHost Instance появлялся, но EnsureBuilt уже отработал.

   Доказательство: в логе `[InventoryClientState] OnSnapshotReceived: items=N, handlers=1` — подписан **только InventoryUI (TAB-колесо)**, не CharacterWindow.

2. **ListView layout pitfall:** даже если подписка отрабатывала, первый `display: none → flex` на `inventory-section` (UXML дефолт `style="display: none;"`) не вызывал повторный layout — VE не создавались, `bindItem` не вызывался. После toggle через другой таб — VE создавались.

**Fix (3 части):**

1. **Lazy-subscribe в `Update()`** (`CharacterWindow.cs`):
   ```csharp
   private void Update() {
       if (_built && !_isInventorySubscribed) {
           var invState = InventoryClientState.Instance;
           if (invState != null) {
               SubscribeInventory();
               invState.RequestRefresh();
               Debug.Log("[CharacterWindow] Lazy-subscribed to InventoryClientState.OnSnapshotUpdated");
           }
       }
       // ... старый код ...
   }
   ```
   Флаг `_isInventorySubscribed` — идемпотентность. `SubscribeInventory()` / `UnsubscribeInventory()` — через флаг, без bare `+=`/`-=`.

2. **`RefreshInventoryCache` + `ApplyInventoryFilters`**: `Rebuild()` → `RefreshItems()` + null-trick:
   ```csharp
   if (!ReferenceEquals(_inventoryList.itemsSource, _inventoryCache))
       _inventoryList.itemsSource = _inventoryCache;
   _inventoryList.RefreshItems();
   ```
   `RefreshItems()` вызывает `bindItem` для всех видимых элементов (а не пересоздаёт их).

3. **`SwitchTab("inventory")`**: добавил `_inventoryList.MarkDirtyRepaint()` после `display: flex` — принудительный пересчёт layout для ListView после first-show.

**Verify (после фикса):**
- `[CharacterWindow] Lazy-subscribed to InventoryClientState.OnSnapshotUpdated`
- `OnSnapshotReceived: items=N, handlers=2` (вместо 1)
- `[CharacterWindow] HandleInventorySnapshotUpdated: items=N, ...`
- P → Инвентарь сразу показывает содержимое (без toggle)

### 11.4 Compile error при добавлении Update

**Симптом:** `error CS0111: Type 'CharacterWindow' already defines a member called 'Update'`

**Root cause:** я добавил свой `private void Update()`, не зная что в `CharacterWindow` уже есть свой `Update()` (для Esc-handler'а).

**Fix:** встроил lazy-subscribe в существующий Update, не дублируя.

### 11.5 Что осталось открытым

- **Multi-client verify** (ParrelSync) — не тестировалось в этой сессии
- **NetworkChestContainer legacy fallback** — остаётся (как safety net), но `InventoryServer.AddItem` теперь основной путь
- **Cleanup** (Phase 9, отдельная сессия): удаление `Inventory.cs`, `InventoryUI.cs` IMGUI, `ItemPickupSystem.cs`, `NetworkInventory.cs` (когда все потребители мигрируют), `Item_Type1..8.asset`

---

## Заключение

Phase 7 (базовая функциональность) — **готово к тестированию**. Phase 8 (bugfixes) — **готово**.

**Compile state:** 0 errors.
**Coverage:**
- ✅ Pickup (single client) — bugfix #11.2
- ✅ Chest pickup (single client) — bugfix #11.1
- ✅ TAB-колесо (UI Toolkit) — Phase 4
- ✅ P-таб (CharacterWindow) — bugfix #11.3
- ✅ Server-authoritative snapshot
- ⚠️ Multi-client — не проверено (требует ParrelSync)
- ❌ Stackable / Drop / Use / Cargo — TODO Phase 9+
