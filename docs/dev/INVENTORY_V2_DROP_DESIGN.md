# Inventory v2 — Drop в мир (Phase 10 Design, 2026-06-05)

**Автор:** Mavis (Mavis)
**Связанные:** `INVENTORY_V2_REFACTOR.md`, `60_KNOWN_ISSUES.md` §12
**Статус:** Design → Implementation (Phase 10)

---

## 1. Scope

Реализовать `Drop в мир`: игрок выбирает предмет в инвентаре, нажимает кнопку "БРОСИТЬ" → сервер убирает предмет из инвентаря + спавнит `PickupItem` в указанной `worldPos`. **Другие клиенты** автоматически видят новый pickup (через NGO network spawn).

**Out of scope:**
- Stackable (MVP: 1 item = 1 quantity, как в Phase 1-9)
- Drop на других игроков (только в мир)
- Drop в контейнер (отдельная фича)

---

## 2. Slot index семантика

`InventoryItemDto.slotIndex` строится в `InventoryWorld.BuildSnapshot` (строки 220-249):

```csharp
int slotIndex = 0;
foreach (ItemType type in Enum.GetValues(typeof(ItemType))) {
    var ids = data.GetIdsForType(type);
    foreach (int id in ids) {
        items.Add(new InventoryItemDto { ..., slotIndex = slotIndex++ });
    }
}
```

То есть **slotIndex — это индекс в плоском массиве `items[]` snapshot'а**, в порядке ItemType enum (0=Resources, 1=Equipment, ...) и затем по списку itemId'ов внутри каждого типа. **Это ТОЛЬКО клиентская проекция**.

Серверная `InventoryData` (файл `InventoryData.cs`, удалён в Phase 9, но теперь снова нужен для drop) хранит данные в **8 списках по типам**: `_resourceIds`, `_equipmentIds` и т.д.

**Проблема:** после Phase 9 файл `InventoryData.cs` удалён. `InventoryWorld._playerInventories` теперь хранит... что? Проверить.

**Решение для TryDrop:**
1. Клиент шлёт `RequestDropRpc(slotIndex, quantity, worldPos)`.
2. Сервер в `RequestDropRpc` **вычисляет** (itemId, itemType) из `BuildSnapshot` — slotIndex → пройти по `BuildSnapshot` order, найти N-й item.
3. Сервер убирает itemId из соответствующего `_xxxIds` списка.
4. Сервер спавнит PickupItem prefab.

**Альтернатива (лучше):** Клиент шлёт **(itemId, itemType, quantity, worldPos)** — НЕ slotIndex. SlotIndex — клиентская абстракция. Сервер оперирует itemId/itemType напрямую. UI вычисляет slotIndex → itemId перед отправкой.

**Решение: slotIndex остаётся, потому что UI оперирует им** (selection в ListView). Но **сервер конвертирует** slotIndex → (itemId, type) для валидации.

---

## 3. Server flow

```
Client (InventoryUI.OnDropClicked)
  ↓
InventoryClientState.RequestDrop(slotIndex, quantity, worldPos)
  ↓
InventoryServer.RequestDropRpc(slotIndex, quantity, worldPos)
  - rate-limit check
  - InventoryWorld.TryDrop(clientId, slotIndex, quantity, worldPos):
    1. BuildSnapshot → найти (itemId, type) для slotIndex
    2. Validate: itemId в инвентаре клиента? (anti-cheat)
    3. Validate: worldPos в радиусе от player? (анти-телепорт, например 3м)
    4. Убрать itemId из соответствующего _xxxIds списка
    5. Return Ok
  - if Ok:
    - Instantiate PickupItem prefab
    - SetItemData(itemData, worldPos) [server-side only]
    - NetworkObject.Spawn() — реплицирует на ВСЕ клиенты
  - SendSnapshot + SendResult
```

---

## 4. Anti-cheat

| Проверка | Описание |
|---|---|
| **Ownership** | server validates itemId в инвентаре clientId |
| **Distance** | worldPos в радиусе 3м от player position (иначе телепорт-чит) |
| **Rate limit** | уже есть через `InventoryServer.CheckRateLimit` (60 ops/min) |

---

## 5. NetworkObject для runtime-spawn

`PickupItem` уже имеет `NetworkObject` (проверено в `Assets/_Project/Prefabs/PickupItem_Test.prefab`).

NGO 2.x требует, чтобы prefab был зарегистрирован в `NetworkManager.NetworkConfig.Prefabs` (NetworkPrefabsList) **ДО** runtime spawn. Без этого клиент не сможет реплицировать объект — будет NetworkError.

**Решение:** Добавить `PickupItem_Test` prefab в NetworkPrefabsList. Проще всего — через MCP `manage_prefabs` если есть, или через `NetworkManagerController.Awake` программно:

```csharp
// В NetworkManagerController.Awake (после создания netConfig)
var pickupPrefab = Resources.Load<GameObject>("PickupItem_Test");
if (pickupPrefab != null && pickupPrefab.GetComponent<NetworkObject>() != null) {
    netConfig.Prefabs.Add(new NetworkPrefab { Prefab = pickupPrefab });
}
```

**Но Resources/PickupItem_Test.prefab не существует** — он в `Assets/_Project/Prefabs/`. Нужно либо Resources.Load (требует копию в Resources/), либо `AssetDatabase.LoadAssetAtPath` (только Editor, не runtime).

**Альтернатива (лучше):** `[SerializeField] GameObject pickupItemPrefab` на `InventoryServer`. Инжектится в инспекторе или через MCP SerializedObject. Это паттерн, как `ChunkNetworkSpawner.chestPrefab`.

---

## 6. PickupItem: server-side data

`PickupItem` сейчас хранит `itemData` (ItemData) + `itemId` (через `InventoryWorld.GetOrRegisterItemId`). При runtime spawn сервер должен:

1. Instantiate prefab → GO с PickupItem + NetworkObject
2. Set position
3. **SetItemData** — выставить itemData (ItemData) и itemId (int)
4. NetworkObject.Spawn() → NGO реплицирует на клиентов

Нужно добавить public метод `PickupItem.SetItemData(ItemData, int)` + чтобы PickupItem **получал `itemId` на старте** (для `Collect()` → RequestPickupRpc с itemId).

Текущий `Collect()`:
```csharp
int itemId = InventoryWorld.Instance?.GetOrRegisterItemId(itemData) ?? -1;
```

Это **тоже работает на клиенте** (Resources/Items лежит на всех машинах). Но лучше — `itemId` хранится на самом `PickupItem` (сериализуется через NetworkVariable или присваивается при spawn). Для MVP: **itemData → itemId на клиенте через GetOrRegisterItemId** уже работает, оставляем.

---

## 7. UI changes

### 7.1 `InventoryWheel.uxml`

Добавить `drop-btn` рядом с `use-btn`:

```xml
<ui:Button name="drop-btn" text="БРОСИТЬ" class="action-btn drop" />
```

### 7.2 `InventoryUI.cs`

- `_dropBtn = _root.Q<Button>("drop-btn")` в EnsureBuilt
- `if (_dropBtn != null) _dropBtn.clicked += OnDropClicked;`
- `OnDropClicked()`:
  - Validate selection
  - Calculate worldPos = player.position + forward * 1.5m
  - `InventoryClientState.Instance?.RequestDrop(_selectedItemIndex, 1, worldPos)`
  - SetMessage("Бросаю...")

### 7.3 Стили

`InventoryWheel.uss` — добавить `.action-btn.drop { ... !important }` (цвет красный, отличается от use).

---

## 8. Failure cases

| Случай | Server response | UI feedback |
|---|---|---|
| Slot пустой | `InvalidSlot` | "Неверный слот" |
| Distance > 3м | `NotInZone` | "Слишком далеко" |
| ItemId не в инвентаре | `ItemNotOwned` | "Нет такого предмета" |
| Rate limit | `RateLimited` | "Подождите" |
| Network not started | (никогда не RPC) | "Сеть не запущена" |
| Server spawn fail (prefab не зарегистрирован) | `InternalError` | "Ошибка сервера" |

---

## 9. Testing (T14-T17 в 70_CHEST_PICKUP_TESTS.md)

T14: Open TAB, select item in sublist, click "БРОСИТЬ" → item disappears from TAB+UI, PickupItem appears in world.
T15: Other client (multi-client) sees new PickupItem at correct position.
T16: Try drop from too far → rejected.
T17: Drop same item twice → succeeds twice (2 pickups in world, 0 in inventory).

---

## 10. Risks

| Risk | Mitigation |
|---|---|
| NetworkPrefabsList не зарегистрирован → spawn fail | `InventoryServer.dropPickupPrefab` [SerializeField] + проверка в Awake |
| Runtime Instantiate на клиенте | только server spawn (IsServer check) |
| ItemId коллизии при runtime itemId присваивании | GetOrRegisterItemId идемпотентен, OK |
| SlotIndex нестабилен между snapshot'ами | snapshot пересоздаётся после каждой мутации, OK |

---

## 11. Implementation order

1. **Phase C** — `InventoryWorld.TryDrop` (убрать stub, реализовать slot→itemId, remove from list, distance check)
2. **Phase D** — `InventoryServer.RequestDropRpc` (после TryDrop success → spawn prefab)
3. **Phase E** — `InventoryServer.dropPickupPrefab` [SerializeField] + prefab reference через MCP
4. **Phase F** — UI: drop-btn в UXML + OnDropClicked в InventoryUI.cs + USS стили
5. **Phase G** — doc update + tests T14-T17 + 0 errors verify
