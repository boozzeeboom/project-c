# MetaRequirement — Known Issues & TODO

**Документ:** баги + TODO для MetaRequirement-подсистемы.
**Дата:** 2026-06-06
**Зависит от:** `00_OVERVIEW.md`, `10_IMPLEMENTATION_GUIDE.md`, `30_RUNTIME_FLOW.md`

---

## Известные баги (Этап 1, R2-META-REQ-001)

### ✅ РЕШЕНО: Compile errors в ShipKeyServer.cs (5 шт)

**Симптом:** После превращения `ShipKeyBinding` в `[Obsolete]` alias `MetaRequirement`,
`ShipKeyServer.cs` потерял доступ к `ServerKeyItemId` и `ShipDisplayName` (теперь это
`ServerItemIds[]` и `InteractableDisplayName` в родительском `MetaRequirement`).

**Файлы и фиксы:**
- `ShipKeyServer.cs:84-85` (RegisterBinding log): `ShipDisplayName → InteractableDisplayName`, `ServerKeyItemId → ServerItemIds[0]`
- `ShipKeyServer.cs:112` (RequestCanBoardRpc reason): `b.ShipDisplayName → b.InteractableDisplayName`
- `ShipKeyServer.cs:172-173` (PushBindingsToClient): `kvp.Value.ServerKeyItemId → kvp.Value.ServerItemIds[0]`, `kvp.Value.ShipDisplayName → kvp.Value.InteractableDisplayName`
- `ShipKeyServer.cs:52-55` (GetKeyItemId): `b.ServerKeyItemId → b.ServerItemIds[0]`
- `ShipKeyServer.cs:65-73` (CanPlayerBoard fallback): `b.ServerKeyItemId → b.ServerItemIds[0]`

**Статус:** ✅ RESOLVED (2026-06-06).

### ✅ РЕШЕНО: `RpcInvokePermission.Owner` deprecation warning

**Симптом:** CS0618 warning на `RequireOwnership = true` (deprecated в NGO 2.x).

**Фикс:** `MetaRequirementRegistry.RequestCanUseRpc` — заменено на `InvokePermission = RpcInvokePermission.Owner`.

**Статус:** ✅ RESOLVED.

---

## TODO (Этап 2+)

### 🟠 MEDIUM: `_consumeOnUse` НЕ реализован

**Симптом:** Поле `MetaRequirement._consumeOnUse` есть, но логика забирания предметов после
успешного использования **отсутствует**. Если дизайнер поставит галочку в инспекторе —
ничего не произойдёт (no-op).

**Phase 10+ fix:**
- В `MetaRequirement.CanPlayerUse` после `allowed=true` проверить `_consumeOnUse`
- Если true — вызвать `InventoryWorld.RemoveItems(clientId, ids)` (новый метод, тоже TODO)
- Race condition: между `CanPlayerUse` (check) и `RemoveItems` (consume) другой RPC может
  забрать тот же предмет. **Нужен reservation pattern** (TODO).

**Связь:** см. `00_OVERVIEW.md` §6.6.

### 🟢 LOW: Drop в мир теряет визуал (R3-INV-DROP-001)

**Симптом:** Если игрок подобрал цветной `PickupItem` (например, `[Key_Blue_Pickup]` с
синим URP/Lit материалом) и потом выбросил его через drop-UI, в мире появится
**белый/базовый** `PickupItem` (из `_dropPickupPrefab`).

**Причина:** `ItemData` не хранит 3D-материал, цвет, размер. `PickupItem.ApplyItemDataVisual`
не существует. `InventoryServer.RequestDropRpc` не пробрасывает визуал с scene-placed
источника.

**Подробнее:** `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md` §"R3-INV-DROP-001".

**Не чиним в MetaRequirement-сессии** — это **Inventory-баг**, не MetaRequirement. Записан
отдельно.

### 🟢 LOW: `MetaRequirement.OnAccessAllowed` event нет в client state (server-side only event отсутствует)

**Симптом:** В MVP UI подписывается на `MetaRequirementClientState.OnAccessAllowed` (client
event). Но **серверной** версии этого event'а нет (т.е. `MetaRequirement.OnServerAllowed`
— нет такого). Если нужен серверный callback (например, для `consumeOnUse` логики) — нет
простого способа.

**Phase 10+ fix:** добавить в `MetaRequirement`:
```csharp
public event Action<ulong, MetaRequirement> OnServerAllowed;  // server-only
```
И дёргать в `MetaRequirementRegistry.RequestCanUseRpc` при `allowed=true`.

### 🟢 LOW: `ProgressInfo` НЕ используется UI

**Симптом:** `MetaRequirement.GetPlayerProgress(clientId)` есть, но `MetaRequirementClientState`
не хранит/не отдаёт его. UI (tooltip "Прогресс: 2/5") не реализован.

**Phase 10+ fix:**
- `MetaRequirementClientState.GetProgress(ulong netId)` — вызывает `MetaRequirementRegistry` через RPC или читает server-side projection
- UI tooltip подписывается на `InventoryClientState.OnSnapshotUpdated` + `MetaRequirementClientState.OnRequirementsUpdated`

### 🟢 LOW: `MetaRequirement.GetMissingItems` (server-side helper) отсутствует в client

**Симптом:** `MetaRequirement` умеет генерировать `BuildAutoReason` (список недостающих),
но клиент **не** получает этот список — только итоговую строку `reason` через RPC.

**Phase 10+ fix:** `MetaRequirementDto` расширить `int[] missingIds`, чтобы UI мог
показать "Не хватает: [иконка A, иконка B]" вместо одной строки.

### 🟢 LOW: `MetaRequirementRegistry.Instance` singleton race при нескольких scene-placed

**Симптом:** Если в сцене **два** GameObject с `MetaRequirementRegistry` (например, scene
copy-paste), `Instance` присвоится первому заспавнившемуся, второй перезапишет
(т.к. `if (Instance == null) Instance = this`). Это безопасно, но **не выводит warning**
о дубликате.

**Phase 10+ fix:** в `OnNetworkSpawn` — `if (Instance != null) Debug.LogWarning("Duplicate MetaRequirementRegistry in scene")`.

### 🟢 LOW: `MetaRequirement._consumeOnUse` нет UI feedback

**Симптом:** Если игрок открыл блок с `_consumeOnUse=true` (после фикса), он не увидит
что предмет пропал из инвентаря, кроме как открыв TAB-колесо. Нет визуального feedback
"предмет использован".

**Phase 10+ fix:** на `OnAccessAllowed` (client) — если `_consumeOnUse=true` — показать
toast "Предмет использован" (отдельный тип toast).

### 🟢 LOW: Нет UI для Drop-UI (выброс предмета)

**Симптом:** UI для drop'а из инвентаря не реализован. `InventoryServer.RequestDropRpc`
существует, но UI кнопки нет.

**Phase 10+ fix:** добавить кнопку "ВЫБРОСИТЬ" в P-таб sublist (по аналогии с "ИСПОЛЬЗОВАТЬ").

### 🟢 LOW: Disconnect → reconnect race (pending RequestCanUse)

**Симптом:** Если игрок нажал E (отправил `RequestCanUse`) и сразу disconnect'нулся —
RPC дойдёт до сервера (или нет), но `NetworkPlayer` уже уничтожен → ответ
`ReceiveMetaRequirementResponseTargetRpc` не дойдёт. На reconnect — `_lastCanUseRequestTime`
НЕ сбрасывается (TODO).

**Phase 10+ fix:** в `NetworkPlayer.OnNetworkSpawn` (IsOwner) — `_lastCanUseRequestTime = -10f; _pendingCanUseInteractableId = ulong.MaxValue;`.

### 🟢 LOW: `MetaRequirement` на динамически создаваемых объектах

**Симптом:** Документация говорит "spawn dynamic", но реально не тестировалось. `PickupItem` —
`MonoBehaviour`, не `NetworkBehaviour`, поэтому не участвует в MetaRequirement flow. Если
нужен "бросаемый MetaRequirement-объект" (например, "ключ-сундук" с замком) — flow не
отработан.

**Phase 10+ fix:** сделать `PickupItem` часть MetaRequirement (с обратной логикой: подбираешь
= "use", а не "store"), или новый `MetaRequirementPickup` компонент.

---

## Что НЕ делаем в MetaRequirement (out of scope)

- **Crafting** (A + B → C) — отдельная подсистема, не lock-key.
- **Conditions** (время суток, репутация, фракция) — отдельная подсистема, не предметы.
- **Persistent requirements** (сохранение "собрал 3 из 5" между сессиями) — после persistence инвентаря.
- **Hot-wire / взлом замка** — отдельная фича, не MVP.
- **TTL на pickup'ы** — предметы пропадают через N часов. Не в скоупе.
- **Multi-progress UI (5/8 с иконками)** — V2, см. `00_OVERVIEW.md` §7.

---

## История

- **2026-06-06 (R2-META-REQ-001):** Этап 1 MetaRequirement. Сделано:
  - 7 новых файлов в `Assets/_Project/Scripts/MetaRequirement/`
  - 4 алиаса `[Obsolete]` в `Assets/_Project/Scripts/Ship/Key/`
  - `InventoryWorld` extensions (HasAllItems/HasAnyItem/CountOf/GetMissingItems)
  - `NetworkManagerController.CreateMetaRequirementClientState`
  - `NetworkPlayer` Target RPC'и + `TryInteractNearestMetaRequirement`
  - 3 SO `Item_Key_Blue/Red/Green.asset`
  - 3 Pickup-ключа + 3 LockBox-блока в `WorldScene_0_0.unity` (под `[MetaRequirement_Test]`)
  - `[MetaRequirementRegistry]` + `[MetaRequirementToast]` в `BootstrapScene.unity`
  - `MetaRequirementPanelSettings.asset` (копия ShipKeyPanelSettings)
  - **Статус: 🟢 COMPILE-OK, готово к Play-mode тесту.**
- **(Планируется) R2-META-REQ-002 (Этап 2):** `_consumeOnUse` логика, ProgressInfo UI, multi-item UI tooltip, reservation pattern.
- **(Планируется) R3-INV-DROP-001 fix:** `ItemData` visual fields (visualMaterial/visualScale/visualTint) + `PickupItem.ApplyItemDataVisual`. См. `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md`.
- **(Планируется) Удаление алиасов:** через 1-2 релиз-цикла удалить `ShipKeyBinding/ShipKeyServer/ShipKeyClientState/ShipKeyToast` (после миграции всех сцен на MetaRequirement*).
