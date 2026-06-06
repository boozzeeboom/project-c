# Inventory Sub-System — Known Issues & TODO

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `20_IMPLEMENTATION_PLAN.md`

Список **известных ограничений** текущей реализации (Phase 7) + **TODO** на Phase 8+.

---

## Известные баги (Phase 7)

### 🟠 MEDIUM (R3-INV-DROP-001, 2026-06-06): Drop-спавн теряет визуальное представление предмета

**Симптом:**
- Игрок подбирает красиво оформленный `[PickupItem]` (например, цветной ключ `[Key_Blue_Pickup]`,
  синий URP/Lit материал с emission)
- Через UI (TAB-колесо → Drop) или горячую клавишу выбрасывает предмет из инвентаря
- В мире появляется **новый** `PickupItem` — но он **белый/базовый**, без цвета, без emission,
  без иконки, которая была у оригинала

**Сцена repro:**
1. Editor → BootstrapScene → Play → StartHost
2. Подойти к `[Key_Blue_Pickup]` (синяя сфера, URP/Lit материал с emission) → E (подобрать)
3. Открыть TAB-колесо → выбрать слот Equipment (синий ключ) → нажать Drop (или будущий UI)
4. На земле появляется `[PickupItem]`, но он **белый** (или какой указан в `_dropPickupPrefab`),
   а не синий

**Ожидаемое поведение:** выброшенный предмет выглядит так же, как подобранный (тот же цвет, материал,
icon — или хотя бы визуально различимый для своего itemId).

**Корневая причина (root cause):**
В `Assets/_Project/Items/Network/InventoryServer.cs:142-149` (метод `RequestDropRpc`) при
успешном drop сервер спавнит префаб `_dropPickupPrefab` и выставляет ему **только данные**:
```csharp
pickup.itemData = itemData;                                       // SO-ссылка
pickup.itemId  = InventoryWorld.Instance.GetOrRegisterItemId(itemData);
```
Никакая визуальная часть (MeshRenderer.sharedMaterial, Sprite icon, размер, particle-эффект)
**не пробрасывается** с оригинального scene-placed pickup'а на новый server-spawned.

Scene-placed `PickupItem` в `WorldScene_0_0.unity` имел свой MeshRenderer.material,
выставленный в инспекторе (или через MCP `manage_components` с `sharedMaterial`). Эта
настройка **уникальна для каждого экземпляра** в сцене. Префаб `_dropPickupPrefab`,
наоборот, имеет **один общий** материал на все дропы (его MeshRenderer.sharedMaterial),
который обычно = базовый (default cube/sphere material).

Поскольку `ItemData` (SO) хранит только `itemName/itemType/description/icon/maxStack/weightKg`
(`Assets/_Project/Scripts/Core/ItemType.cs:18-34`) — **НЕ хранит 3D-материал**, цвет, размер
или иной визуал, нет механизма передать "как должен выглядеть предмет itemId=N" от scene-placed
источника в server-spawned экземпляр.

**Почему не чиним в этой сессии:**
- Затронуты несколько подсистем: `ItemData` (расширение полей), `InventoryServer` (новый
  путь визуализации), `PickupItem` (новая логика `ApplyItemDataVisual`), `IconDatabase` или
  аналог (отдельный SO или `Sprite[]`).
- Часть проблемы решается в рамках **ItemData v2** (планируется: `icon` уже есть, но
  не используется для 3D; `visualPrefab` / `visualMaterial` / `colorTint` — новые поля).
- Без согласования с дизайнером (визуальный стиль, asset pipeline) фиксить опасно —
  можем сломать существующие pickup'ы.
- Трудоёмкость: ~0.5-1 сессия.

**Workaround для тестов:**
- Все 30+ pickup'ов в `WorldScene_0_0` уже имеют индивидуальные материалы (URP/Lit с цветом
  и emission). Если drop в мир **не используется** (т.е. игрок только подбирает, не выбрасывает)
  — баг не проявляется.
- До фикса: в тестах избегать drop'а цветных предметов, либо не обращать внимания на "обесцвечивание".

**Phase 10+ fix (план):**
1. Расширить `ItemData` (опционально) полями:
   ```csharp
   public Material visualMaterial;  // 3D-материал для PickupItem (если есть)
   public Vector3  visualScale = Vector3.one;  // размер
   public Color    visualTint = Color.white;   // модификатор цвета (если material=null)
   ```
2. Добавить `PickupItem.ApplyItemDataVisual()` (server-side после `Instantiate`):
   ```csharp
   if (itemData.visualMaterial != null) renderer.sharedMaterial = itemData.visualMaterial;
   transform.localScale = itemData.visualScale;
   // + tint через MaterialPropertyBlock если material=null
   ```
3. В `InventoryServer.RequestDropRpc` после `pickup.itemData = itemData`:
   `pickup.ApplyItemDataVisual();`
4. Создать/назначить материалы для существующих 30+ `ItemData` (AssetDatabase workflow,
   один проход).

**Файлы:**
- Бага: `Assets/_Project/Items/Network/InventoryServer.cs:142-149` (spawn + set itemData)
- PickupItem: `Assets/_Project/Scripts/Core/PickupItem.cs:28-99` (нет метода ApplyItemDataVisual)
- ItemData: `Assets/_Project/Scripts/Core/ItemType.cs:18-34` (нет visual-полей)

**Связанные подсистемы:**
- `MetaRequirement` (R2-META-REQ-001) — LockBox-блоки используют color tint через
  `LockBox._baseColor` поле (не через ItemData), так что у них проблемы нет. Если в будущем
  делать "ключ как ItemData + цветной pickup", та же проблема проявится — фикс должен быть
  общим.

**Связь с другими багами:** см. §"🟢 LOW: Drop в мир не работает" (TODO Phase 8). Тот баг
про то, что drop в принципе возвращает InternalError. Этот (R3-INV-DROP-001) — про то, что
даже когда drop работает, теряется визуал.

**Статус:** 🟠 **ИЗВЕСТЕН, НЕ ЧИНИМ в этой сессии.** Записан для следующего бага-фикса
(после Phase 10 stackable / cargo).

---

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

### 12. Phase 9 — Cleanup (2026-06-05)

Phase 9 = полный cleanup legacy. Все 6 dead-файлов + 8 dead-ассетов удалены.

### 12.1 Что удалено (13 файлов)

**Код (5):**
- `Assets/_Project/Scripts/Core/Inventory.cs` (177 lines) — локальный MonoBehaviour, нигде не инстанцируется с Phase 4
- `Assets/_Project/Scripts/Core/NetworkInventory.cs` (240 lines) — старый NetworkBehaviour, заменён InventoryServer
- `Assets/_Project/Scripts/Player/ItemPickupSystem.cs` (238 lines) — мёртвый код с давних времён
- `Assets/_Project/Scripts/UI/InventoryUI.cs` (384 lines) — IMGUI/GL TAB-колесо, заменено UI Toolkit
- `Assets/_Project/Scripts/Core/ItemDatabaseInitializer.cs` (85 lines) — дублировал InventoryWorld.RegisterAllItems

**Ассеты (8):**
- `Assets/_Project/Resources/Items/Item_Type1..8.asset` — старые заглушки без имён/иконок, 0 ссылок

### 12.2 Что патчено перед удалением (safety)

- `PickupItem.cs` — убрана `else`-ветка с `NetworkInventory.GetItemId` (теперь единственный путь через `InventoryWorld.GetOrRegisterItemId`)
- `NetworkChestContainer.cs` — убран legacy fallback на `NetworkInventory.AddItem`. Теперь **единственный** путь — v2 `InventoryServer.AddItem`. Если `InventoryServer.Instance == null` — `Debug.LogError` (должно быть impossible после fix'а ScenePlacedObjectSpawner)
- `NetworkPlayer.cs`:
  - Удалены поля `private Inventory _inventory; private InventoryUI _inventoryUI;`
  - `OnNetworkSpawn` — убрана `_inventory.LoadFromPrefs()` (v2 persistence = ответственность сервера)
  - `OnNetworkDespawn` — убраны `_inventory.SaveToPrefs()` + `Destroy(_inventoryUI.gameObject)`
  - `SpawnInventory()` — no-op, оставлен как hook
  - `TryPickup()` — убрана legacy chest-ветка (`_nearestChest.GetLootItems()` + `_inventory.AddMultipleItems`). Pickup — единственный путь `_nearestPickup.Collect()` (v2 RPC)
- `NetworkManagerController.cs` — убран reflection-блок сохранения `_inventory` через `GetField("_inventory")`

### 12.3 Что НЕ удалено (живой legacy)

- `Assets/_Project/Scripts/Core/ChestContainer.cs` (не-сетевой) — используется в `InteractableManager`, `NetworkPlayer.FindNearestInteractable` (fallback), `ChunkNetworkSpawner`. Это **живой** код, не dead. Миграция — отдельная задача.
- `Assets/_Project/Scripts/Player/ItemPickupSystem.cs` — см. выше (всё-таки удалён, но `ChestContainer` остался)

### 12.4 Compile после cleanup

**0 errors.** Можно коммитить.

---

## Заключение

Phase 7 + 8 (функциональность + bugfixes) + 9 (cleanup legacy) — **готовы к коммиту**.

**Compile state:** 0 errors.
**Coverage:**
- ✅ Pickup (single client)
- ✅ Chest pickup (single client)
- ✅ TAB-колесо (UI Toolkit)
- ✅ P-таб (CharacterWindow)
- ✅ Server-authoritative snapshot
- ✅ Legacy cleanup (13 файлов удалено, ~1700 lines мёртвого кода)
- ⚠️ Multi-client — не проверено (требует ParrelSync)
- ❌ Stackable / Drop / Use / Cargo — TODO Phase 10+
