# Setup & Binding: KeyRod ↔ Ship (R2-SHIP-KEY-003)

> Документация по настройке ключей-экземпляров в Project C.  
> Дата: 2026-06-19 | Версия: v11 | Статус: ✅ MVP завершён

---

## 1. Участвующие компоненты

### На PickupItem (scene-placed, мир)

| Компонент | Файл | Роль |
|---|---|---|
| `PickupItem` | `PickupItem.cs` | Реагирует на **E**. Определяет `itemId` (из `ItemData`). Читает `instanceId` из `KeyRodInstanceBinding`. Отправляет `RequestPickupRpc`. |
| `KeyRodInstanceBinding` | `KeyRodInstanceBinding.cs` | **NEW**. MonoBehavior (не NetworkBehaviour). Привязывает PickupItem → ShipController. При старте сервера создаёт `KeyRodInstance` в `KeyRodInstanceWorld`. Публикует `TryGetInstanceId(out int)` для PickupItem. |
| (опционально) `NetworkObject` | — | Нужен если PickupItem участвует в NetworkVariable/NetworkList. Для PickupItem не требуется (использует RPC от NetworkPlayer). |

### На корабле (scene-placed, мир)

| Компонент | Файл | Роль |
|---|---|---|
| `ShipController` | `ShipController.cs` | Контроллер корабля. Поле `_customDisplayName` — имя корабля для UI. На client-side: `SubscribeToShip` (telemetry). |
| `ShipOwnershipRequirement` | `ShipOwnershipRequirement.cs` | **NEW**. NetworkBehaviour. Регистрируется в `MetaRequirementRegistry` при `OnNetworkSpawn`. `CanPlayerUse(clientId)` — проверяет `KeyRodInstanceWorld.IsOwnerOfShip()`. Server-only. |

### Глобальные сервисы (BootstrapScene)

| Компонент | Файл | Роль |
|---|---|---|
| `ShipOwnershipRegistry` | `ShipOwnershipRegistry.cs` | **NEW**. NetworkBehaviour. `NetworkList<OwnershipEntry>`. Синхронизирует ownerShipList клиентам. Подписан на `KeyRodInstanceWorld.OnOwnershipChanged`. |
| `ShipTelemetryClientState` | `ShipTelemetryClientState.cs` | **NEW**. Singleton. Агрегирует `ShipTelemetryState` со всех ShipController. `MyShips` — owned ships. |

### Persistence

| Файл | Роль |
|---|---|
| `JsonKeyRodInstanceRepository` | Сохраняет `KeyRodInstance` в `Application.persistentDataPath/KeyRodInstances.json`. Сохраняются: `itemId`, `registeredShipId`, `ownerPlayerId`, `originalOwnerId`, `state`, `createdAtUnix`. |
| `JsonInventoryRepository` | Сохраняет инвентарь в `Application.persistentDataPath/inventory_{clientId}.json`. Сохраняет Key-слоты с `keyIds` + `keyInstanceIds`. |

### UI

| Файл | Роль |
|---|---|
| `InventoryTab.cs` | Отображает ключи в инвентаре. Для Key-предметов с instanceId: резолвит имя корабля через `ResolveKeyItemDisplayName()` (3 уровня fallback). |

---

## 2. Как настроить ключ и корабль (пошагово)

### Шаг 1: создание ItemData

1. `Resources/Items/Key_light_ship.asset` (ScriptableObject: ItemData)
2. Поля: `itemName = "Key for Light Ship"` (fallback), `itemType = Key (8)`, `icon = ...`

### Шаг 2: настройка `[KeyRod_ShipName]` PickupItem в мире

1. Create GameObject (например `[KeyRod_ShipLight]`) в `WorldScene`
2. Добавить:
   - `PickupItem` → перетащить `ItemData` в `_itemData`
   - `KeyRodInstanceBinding` → перетащить `_ship` (ShipController) и `_keyItemData` (такой же ItemData)
   - (опционально) Mesh/FBX визуал + коллайдер

### Шаг 3: настройка корабля

1. Корабль уже имеет `ShipController` — задать `_customDisplayName` ("Pushka")
2. Добавить `ShipOwnershipRequirement` (NetworkBehaviour)
3. Убедиться что корабль — NetworkObject с `NetworkObjectId` (NGO выдаёт автоматически)

### Шаг 4: BootstrapScene (делается один раз)

1. `[ShipOwnershipRegistry]` — GameObject с `ShipOwnershipRegistry.cs` под `Toasts_and_meta`
2. `ShipTelemetryClientState` — создаётся автоматически в `NetworkManagerController`

### Шаг 5: тестирование

Запустить Play Mode как Host. Проверить:
- **E** на [KeyRod_ShipLight] → ключ в инвентарь
- **P** → "Key" фильтр → показывает имя корабля
- **F** у корабля → садится
- Выход → Play снова → ключ сохранился

---

## 3. Lifecycle KeyRodInstance

```
                     CreateInstance(itemId, shipNetId, OWNER_NONE)
                                   │
                          ┌────────▼────────┐
                          │  state = Active │
                          │  owner = NONE   │
                          └────────┬────────┘
                                   │
                     ┌─────────────┴─────────────┐
                     │   Pickup (E)               │
                     │   TransferInstance(NONE→me)│
                     │   state = Active           │
                     │   owner = playerClientId   │
                     └─────────────┬─────────────┘
                                   │
                     ┌─────────────┴─────────────┐
                     │   Board ship (F)           │
                     │   ShipOwnershipRequirement │
                     │   → IsOwnerOfShip = True   │
                     └─────────────┬─────────────┘
                                   │
                     ┌─────────────┴─────────────┐
                     │   Drop (Q)                │
                     │   TransferInstance(me→NONE│
                     │   state = Lost            │
                     └─────────────┬─────────────┘
                                   │
                                   ▼
                              DestroyInstance
                              state = Destroyed
```

Persistence auto-save: `CreateInstance`, `TransferInstance`, `UpdateState`, `DestroyInstance` — все триггерят `AutoSave()`.

---

## 4. Display Name Resolution (для инвентаря)

При отображении Key-предмета в `InventoryTab`:

```
ResolveKeyItemDisplayName(dto)
  │
  ├─ Priority 1: ShipTelemetryClientState.MyShips[keyInstanceId]
  │  → displayName из NetworkVariable (telemetry)
  │
  ├─ Priority 2: KeyRodInstanceWorld.GetInstance(instanceId) + FindShipByNetId
  │  → KeyRodInstance.registeredShipId → ShipController.CustomDisplayName
  │
  └─ Priority 3: KeyRodInstanceBinding._ship по itemId (reflection)
     → scene-placed ссылка, стабильная между рестартами
```

Priority 3 — единственный стабильный после перезагрузки persistence, т.к. `instanceId` и `NetworkObjectId` эфемерные.

---

## 5. Защита от дубликатов

- **Pickup guard**: `InventoryWorld.TryPickup` для Key-типа проверяет `data.GetIdsForType(Key).Contains(itemId)` → если ключ уже есть, отклоняет.
- **Instance guard**: `KeyRodInstanceWorld.CreateInstance` проверяет `_primaryInstanceByShipId` → если корабль уже привязан, возвращает существующий instanceId.

---

## 6. Важные замечания

- `KeyRodInstanceBinding` — **MonoBehaviour**, не NetworkBehaviour. Работает только на сервере (проверка `IsServer` в `TryRegister`).
- `instanceId` эфемерен — не сохраняется между сессиями. При загрузке из persistence `KeyRodInstanceWorld` переназначает ID (счётчик `_nextInstanceId++`).
- `_customDisplayName` на `ShipController` — scene-placed инспекторное поле, стабильное.
- `ShipTelemetryClientState.MyShips` фильтрует по `_ownershipCache[ship] == myClientId`. Если ownership не синхронизирован — возвращает пусто.

---

## 7. Файлы, изменённые в v11

| Файл | Изменение |
|---|---|
| `NetworkPlayer.cs` | F-key: `ReceiveMetaRequirementResponseTargetRpc` → вызов `SubmitSwitchModeRpc()` при `allowed` |
| `InventoryTab.cs` | + 4 helper метода для resolution имени; + `using ProjectC.Ship.*` |
| `InventoryData.cs` | + `GetKeyInstanceIds()`, + `SetLastKeySlotInstanceId()` |
| `InventoryWorld.cs` | + `UpdateKeySlotInstanceId()`; guard дубликата в `TryPickup` |
| `InventoryServer.cs` | `OnNetworkSpawn`: инициализация `KeyRodInstanceWorld` с репозиторием; + `UpdateKeySlotInstanceId()` в `RequestPickupRpc` |
| `JsonInventoryRepository.cs` | + `keyIds`/`keyInstanceIds` в `InventorySaveData`; + `case ItemType.Key` в конвертерах |
| `ShipOwnershipRegistry.cs` | `OnNetworkSpawn`: `ShipTelemetryClientState.Instance?.SubscribeToRegistry(this)` |
| `ShipController.cs` | `OnNetworkSpawn` (client): `ShipTelemetryClientState.Instance?.SubscribeToShip(this)` |
| `KeyRodInstanceBinding.cs` | Не переинициализирует `KeyRodInstanceWorld` если уже инициализирован. |
