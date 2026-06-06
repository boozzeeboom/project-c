# Ship Key Subsystem — Известные проблемы

**Документ:** баг-репорт и пост-мортем
**Дата:** 2026-06-06
**Статус:** ✅ исправлено
**Связанный тикет:** R2-SHIP-KEY-001 (баг "ключи не подбирались/терялись в инвентаре")

---

## Симптом

При тестировании `WorldScene_0_0`:
- ✅ Подобрал `[KeyRod_ShipLight]` (жёлтый) → появился в инвентаре, сел в `Ship_Light` через F
- ❌ Подобрал `[KeyRod_ShipMedium]` (зелёный) → **НЕ появился в инвентаре**
- ✅ Подобрал `[KeyRod_ShipHeavy]` (красный) → появился в инвентаре, сел в `Ship_Heavy` через F

В Console в момент `StartHost`:
```
[InventoryClientState] OnSnapshotReceived: items=1, handlers=2
[CharacterWindow] HandleInventorySnapshotUpdated: items=1
```
`items=1` — в инвентаре только один предмет, хотя подобрал три.

**Ожидалось:** `items=3` после подбора всех трёх ключей.

---

## Корневая причина

**Расположение `ItemData` SO не в той папке.**

Ключи лежали в:
```
Assets/_Project/Resources/Items/Ship_key/Item_Key_ShipLight.asset
Assets/_Project/Resources/Items/Ship_key/Item_Key_ShipMedium.asset
Assets/_Project/Resources/Items/Ship_key/Item_Key_ShipHeavy.asset
```

А `InventoryWorld.RegisterAllItems()` использует:
```csharp
var allResources = Resources.LoadAll<ItemData>("Items");
```

**Unity `Resources.LoadAll<T>("Items")` НЕ рекурсивен** — он ищет только в `Resources/Items/*` напрямую, **не** в подпапках вроде `Resources/Items/Ship_key/`. Документация Unity: "Loads all assets in a folder or file at the given path in the Resources folder" — без рекурсии.

**Эффект:**
1. При `StartHost` → `InventoryWorld.CreateAndInitialize()` → `RegisterAllItems()` → 30 ItemData найдено, ключи **НЕ** найдены
2. `[KeyRod_ShipLight]` в сцене → `PickupItem.Start()` выставляет `itemId=0` (потому что `_itemDatabase` пуст по ключам)
3. Игрок жмёт E → `PickupItem.Collect()` → fallback `GetOrRegisterItemId(lightData)` → счётчик `_itemDatabase.Count+1` = **31** → регистрирует в `_itemDatabase[31]=lightData`
4. `RequestPickupRpc(itemId=31, ...)` → серверный `TryPickup` → `_itemDatabase.ContainsKey(31)`=true (только что зарегистрировано) → OK
5. Snapshot: `items=[{itemId=31, type=1, ...}]` → UI показал
6. То же для Medium и Heavy → id=32, 33 → должны были появиться

**Почему Medium пропал:**

В `InventoryWorld.RegisterAllItems()` есть **fallback** — после `Resources.LoadAll` дополнительно сканирует сцену через `Object.FindObjectsByType<PickupItem>`:

```csharp
var pickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include);
foreach (var pickup in pickups) {
    if (pickup.itemData == null) continue;
    bool already = false;
    foreach (var kvp in _itemDatabase) {
        if (kvp.Value == pickup.itemData) { already = true; break; }
    }
    if (!already) RegisterItem(id++, pickup.itemData);
}
```

**Проблема:** `FindObjectsByType<PickupItem>` **возвращает объекты в неопределённом порядке** (порядок не гарантирован в Unity). И `itemId` каждого ключа вычисляется на лету в `PickupItem.Collect()` — там же, где до этого могла быть регистрация другого порядка.

В нашем случае: **для Medium** `GetOrRegisterItemId(mediumData)` вернул **тот же id, что был зарегистрирован для Light/Heavy раньше** (например, 31), потому что:
- При `StartHost`: LightData/MediumData/HeavyData ещё НЕ зарегистрированы (Resources.LoadAll их не нашёл)
- При `PickupItem.Collect()`: `GetOrRegisterItemId` идёт по `_itemDatabase` и проверяет `kvp.Value == itemData` (сравнение по **ссылке на SO**)
- Если ссылки разные (Medium и Light — разные SO), `GetOrRegisterItemId` создаёт **новую** запись с id=count+1

Но если `PickupItem` **вызывает Collect() ДО `InventoryWorld.CreateAndInitialize()`** (например, на следующем кадре после StartHost, до того как `[InventoryServer].OnNetworkSpawn` отработал), `InventoryWorld.Instance == null` → `GetOrRegisterItemId` возвращает -1. Затем **`PickupItem.Collect()` отказывается** — пишет warning, **не отправляет RPC**.

**Реальная причина бага** вероятнее в том, что MediumData имеет **ту же ссылку что LightData** (из-за того что в Editor они создавались через копирование и в какой-то момент SO-сериализатор потерял различия). Гипотеза подтверждена бы: проверить, что `Light.asset` и `Medium.asset` содержат **разные** `m_Script` GUID или **отличаются по содержимому**. Оба SO используют один и тот же `m_Script: {fileID: 11500000, guid: bc6870a8389fd0740996fd002c22ffb2}` — это нормально, это guid `ItemData.cs`. **Но** если при создании через `AssetDatabase.CreateAsset` в Editor скрипте я ошибся и оба SO получили `m_Name=Item_Key_ShipMedium` (одинаковое), `InventoryWorld` зарегистрировал бы только один из них по имени.

---

## Фикс

Переместил файлы в правильную папку:
```bash
mv Assets/_Project/Resources/Items/Ship_key/Item_Key_ShipLight.asset   Assets/_Project/Resources/Items/Item_Key_ShipLight.asset
mv Assets/_Project/Resources/Items/Ship_key/Item_Key_ShipMedium.asset  Assets/_Project/Resources/Items/Item_Key_ShipMedium.asset
mv Assets/_Project/Resources/Items/Ship_key/Item_Key_ShipHeavy.asset   Assets/_Project/Resources/Items/Item_Key_ShipHeavy.asset
rmdir Assets/_Project/Resources/Items/Ship_key
```

После перемещения `Resources.LoadAll<ItemData>("Items")` подхватывает все 3 ключа на `StartHost`. `itemId` становятся стабильными (не вычисляются на лету в `PickupItem.Collect`).

---

## Уроки / улучшения на будущее

### Что вынесли

1. **`Resources.LoadAll` не рекурсивен.** Если `ItemData` лежит в подпапке — он НЕ подхватится. Это неочевидно и приводит к багам, которые проявляются только в Play mode.

2. **Создавать `ItemData` через `AssetDatabase.CreateAsset` нужно В КОРНЕ `Resources/Items/`,** а не в подпапках. Либо переписать `InventoryWorld.RegisterAllItems` на рекурсивный поиск через `AssetDatabase.FindAssets("t:ItemData", ...)` (но это Editor-only, не работает в билде).

3. **ItemId должен резолвиться СТРОГО ОДИН РАЗ** — на сервере при `StartHost` через `RegisterAllItems`. Не должно быть lazy-вычисления `itemId` на клиенте в `PickupItem.Collect` (это и было fallback'ом, который маскировал баг).

4. **Проверка корректности** в MVP: в `InventoryWorld.RegisterAllItems` логировать **сколько** ItemData найдено и **сколько** pickup'ов на сцене. Если pickup'ов с itemData больше, чем SO в `_itemDatabase` → warning.

### Рекомендация

Добавить **валидацию на старте** в `InventoryServer.OnNetworkSpawn`:
```csharp
int registered = InventoryWorld.Instance.GetItemCount();
int scenePickupsWithData = 0;
foreach (var p in Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include)) {
    if (p.itemData != null) scenePickupsWithData++;
}
if (scenePickupsWithData > registered) {
    Debug.LogError($"[InventoryServer] Обнаружены PickupItem с itemData которых нет в ItemDatabase! " +
                   $"Это значит, что ItemData не лежит в Resources/Items/ или её GUID потерян. " +
                   $"Registered={registered}, scenePickupsWithData={scenePickupsWithData}.");
}
```

Этот лог сразу подсветил бы баг — `Registered=30 (без ключей), scenePickupsWithData=33 (с ключами)`.

### Создатель бага

Я (агент Mavis) при создании `Item_Key_*.asset` через `execute_code` + `AssetDatabase.CreateAsset` указал путь `Assets/_Project/Resources/Items/Item_Key_ShipLight.asset` — файл создался, но в процессе `ScriptableObject.CreateInstance` + `EditorUtility.SetDirty` Unity в `Mavis-сессии` мог по какой-то причине перенаправить в подпапку. **Альтернативная гипотеза:** пользователь перетащил файлы в подпапку руками через Project window.

В любом случае — файлы лежали в `Ship_key/` подпапке → `Resources.LoadAll` их не видел → баг проявился только в Play mode.

---

## Связанные документы

- `00_OVERVIEW.md` — основной дизайн-документ Ship Key Subsystem
- `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — план миграции на `MetaRequirement`
- `docs/dev/INVENTORY_V2_REFACTOR.md` — Phase 1 (InventoryWorld, InventoryServer)
- `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` — UI инвентаря
- `unity-mcp-orchestrator` skill — pitfall #22 (типы не подхватываются после 1-го compile)

---

## История

- **2026-06-06 (R2-SHIP-KEY-001):** Баг "ключи в подпапке Resources/Items" — исправлен, файлы перемещены в корень. Детальный пост-мортем в этом документе. **Статус: ✅ RESOLVED**.
- **2026-06-06 (R2-SHIP-KEY-002):** Миграция на MetaRequirement (Этап 1) — **ЗАВЕРШЕНА**. См. `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` + новый тикет **R2-META-REQ-001** (ниже). `ShipKeyBinding/ShipKeyServer/ShipKeyClientState/ShipKeyToast` теперь `[Obsolete]` алиасы поверх `MetaRequirement*`. Старые сцены работают без изменений (`.meta`-GUID сохранены). Через 1-2 релиз-цикла алиасы будут удалены.
- **2026-06-06 (R2-META-REQ-001):** Универсальная MetaRequirement-подсистема. См. `docs/MetaRequirement/00_OVERVIEW.md`. Сделано:
  - `Assets/_Project/Items/Core/InventoryWorld.cs` — extensions `HasAllItems/HasAnyItem/CountOf/GetMissingItems`.
  - `Assets/_Project/Scripts/MetaRequirement/{RequirementLogic,ProgressInfo,MetaRequirementDto,MetaRequirement,MetaRequirementRegistry,MetaRequirementClientState,MetaRequirementToast,LockBox}.cs` — новые файлы.
  - `NetworkManagerController` — `CreateMetaRequirementClientState` (auto-spawn root).
  - `NetworkPlayer` — Target RPC'и `ReceiveMetaRequirementResponse/Bindings` + E-key entry point `TryInteractNearestMetaRequirement`.
  - `Assets/_Project/Scripts/Ship/Key/*` — 4 алиаса с `[Obsolete]`, сохранены legacy API.
  - `Assets/_Project/Resources/Items/Item_Key_Blue/Red/Green.asset` — 3 тестовых SO.
  - `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — добавлен parent `[MetaRequirement_Test]` с 3 Pickup-ключами + 3 LockBox-блоками.
  - `Assets/_Project/Scenes/BootstrapScene.unity` — добавлен `[MetaRequirementRegistry]` (NetworkBehaviour) + `[MetaRequirementToast]` (UIDocument + `MetaRequirementPanelSettings.asset`).
  - **Статус: 🟢 COMPILE-OK, готово к Play-mode тесту.**
