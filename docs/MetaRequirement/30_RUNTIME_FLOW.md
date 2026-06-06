# MetaRequirement — Runtime Flow

**Документ:** что происходит в рантайме — sequence-диаграммы, RPC-вызовы, edge-cases.
**Дата:** 2026-06-06

---

## 1. Bootstrap: от StartHost до стабильного состояния

```
[Server]
  StartHost() в NetworkManagerController
    ↓
  NetworkManager.OnServerStarted
    ↓
  InventoryServer.OnNetworkSpawn
    → InventoryWorld.CreateAndInitialize()  ← singleton, register all ItemData из Resources/Items/
    ↓
  MetaRequirementRegistry.OnNetworkSpawn (scene-placed в BootstrapScene)
    → Instance = this
    → подписка на OnClientConnectedCallback
    → HandleClientConnected(0) ← host = client 0
      → Invoke PushRequirementsToClient (через 0.5 сек)
    ↓
  MetaRequirementRegistry.PushRequirementsToClient
    → собрать все _requirements (для пустой сцены Bootstrap — 0 шт)
    → если 0 → return без warning
    ↓
  NetworkClientSceneLoader загружает WorldScene_0_0 additively
    ↓
  В WorldScene_0_0:
    ScenePlacedObjectSpawner.SpawnInAllLoadedScenes()
    → находит NetworkObject с !IsSpawned
    → spawn'ит руками (для тех, у кого InScenePlacedSourceGlobalObjectIdHash==0)
    ↓
  На каждом MetaRequirement в сцене: OnNetworkSpawn (server)
    → ResolveItemIds (lazy через InventoryWorld)
    → MetaRequirementRegistry.RegisterRequirement(netId, this)
      → Debug.Log "Registered requirement: netId=N, displayName='...', logic=All, itemIds=[31]"
    ↓
  На кораблях (с ShipKeyBinding-алиасом):
    → OnNetworkSpawn (server) → ResolveKeyItemId (старый путь)
    → ShipKeyServer-ALIAS.RegisterBinding (но это _bindings не используется — fallback есть в CanPlayerBoard)
    → MetaRequirement (родитель) тоже регистрирует через MetaRequirementRegistry
    ↓
  Через ~0.5s после OnClientConnected:
    → MetaRequirementRegistry.PushRequirementsToClient
      → если есть requirements — push на host (server-client)
      → Debug.Log "Pushed N requirement(s)"
    → ShipKeyServer-ALIAS.PushBindingsToClient
      → push старых bindings (корабли) на host
      → Debug.Log "Pushed N binding(s)"

[Client] (host = client)
  NetworkManagerController.Awake:
    → CreateMarketClientState
    → CreateContractClientState
    → CreateInventoryClientState
    → CreateShipKeyClientState
    → CreateMetaRequirementClientState
  ↓
  StartHost
  ↓
  NetworkManagerController.HandleServerStarted
  ↓
  Все scene-placed NetworkObject спавнятся (включая MetaRequirement на LockBox, корабли)
  ↓
  NetworkPlayer.OnNetworkSpawn (IsOwner=true для host):
    → SpawnCamera, SpawnInventory
    → (no explicit meta-req code — auto работает)
  ↓
  Получает Target RPC от сервера:
    ReceiveMetaRequirementBindingsTargetRpc
      → MetaRequirementClientState.OnRequirementsPushed(...)
        → _requirements заполняется DTO
        → OnRequirementsUpdated event (UI подписывается если хочет)
    ReceiveShipKeyBindingsTargetRpc
      → ShipKeyClientState-ALIAS.OnBindingsPushed(...)
  ↓
  (опционально) UI на OnEnable → TrySubscribe на OnAccessAllowed / OnAccessDenied
```

**Итог после bootstrap:**
- Сервер: реестр всех MetaRequirement в `_requirements` (server-side)
- Клиент: проекция реестра в `_requirements` (client-side DTO)
- ShipKeyServer-ALIAS: legacy `_bindings` для обратной совместимости
- UI: подписан на OnAccessAllowed/OnAccessDenied

---

## 2. Успешный доступ (игрок подобрал ключ → E на LockBox)

```
[Client - owner NetworkPlayer]
  Update() tick:
    Keyboard.current.eKey.wasPressedThisFrame
    ↓
    TryInteractNearestMetaRequirement()
      → FindObjectsByType<MetaRequirement>(Exclude)
      → для каждого:
        - skip ShipController
        - dist = collider.bounds.ClosestPoint vs transform.position
        - if dist < max(pickupRange, boardDistance) → nearest
      → if nearest == null → return false (fallback на chest/pickup)
      → race protection check (если недавно слали — return true)
      → _lastCanUseRequestTime = Time.unscaledTime
      → _pendingCanUseInteractableId = nearest.NetworkObjectId
      → MetaRequirementClientState.RequestCanUse(nearest.NetworkObjectId)
        → MetaRequirementRegistry.RequestCanUseRpc(nearest.NetworkObjectId, RpcParams)
          [RPC → Server]
    ↓
    return true (claim event handled, не идём в chest/pickup fallback)

[Server]
  MetaRequirementRegistry.RequestCanUseRpc
    → clientId = rpcParams.Receive.SenderClientId
    → req = GetRequirement(interactableNetworkObjectId)
    → if req == null:
        // unknown interactable = "нечего проверять"
        allowed = true; reason = ""
      else:
        allowed = req.CanPlayerUse(clientId, out reason)
          → GetServerItemIds (lazy resolve)
          → switch logic:
              All:    InventoryWorld.HasAllItems
              Any:    InventoryWorld.HasAnyItem
              AtLeastN: count unique have >= _requiredCount
          → if !allowed → reason = _customFailureMessage ?? BuildAutoReason
    → NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc(netId, allowed, reason)
    → Debug.Log "CanUse: client=0, obj=N, allowed=True"

[Client - owner]
  ReceiveMetaRequirementResponseTargetRpc
    → _lastCanUseRequestTime = -10 (сброс race protection)
    → _pendingCanUseInteractableId = ulong.MaxValue
    → MetaRequirementClientState.OnCanUseResponse(netId, allowed, reason)
      → if allowed:
          OnAccessAllowed event → UI handlers (LockBox-анимация, etc.)
          Debug.Log "Use allowed"
        else:
          OnAccessDenied event → MetaRequirementToast
          Debug.LogWarning "Access denied: netId=N, reason='...'"

[Client - UI]
  MetaRequirementToast (Update tick):
    if OnAccessDenied event triggered:
      → if !_built → TryBuild
      → if cooldown passed:
          _label.text = reason
          _container.display = Flex
          _hideCoroutine = StartCoroutine(HideAfter(_duration))
        else:
          skip (cooldown)
  LockBox (Update tick, if OnAccessAllowed event triggered):
    → no = GetComponent<NetworkObject>()
    → if no.NetworkObjectId != netId → return (другой объект)
    → if reopenCooldown passed:
        StartCoroutine(AnimateOpen)
          Phase 1 (50%): scale + emission ramp-up
          Phase 2 (50%): scale + emission ramp-down
```

---

## 3. Отказ (игрок без ключа → E на LockBox)

То же самое, но:
- `allowed = false`
- `reason` = `_customFailureMessage` (если задано) или `"Нужен предмет: <itemName>"`
- `OnAccessDenied` event
- `MetaRequirementToast` показывает toast с reason
- `OnAccessAllowed` **не** дёргается — LockBox не анимируется
- `_pendingCanUseInteractableId` сбрасывается (можно снова нажать E)

---

## 4. Race conditions и edge cases

### 4.1 Двойной E (race F-key)
Защита в `NetworkPlayer`:
```csharp
if (Time.unscaledTime - _lastCanUseRequestTime < CAN_USE_REQUEST_TIMEOUT
    && _pendingCanUseInteractableId == nearest.NetworkObjectId) {
    return; // ещё ждём ответ
}
```
**`CAN_USE_REQUEST_TIMEOUT = 1.5f` секунд.** Если игрок жмёт E дважды в течение 1.5 сек
на тот же объект — второй игнорируется. Ответ на первый сбросит флаг.

### 4.2 Stale-id (объект unregister'нулся пока летел RPC)
В `MetaRequirementClientState.OnCanUseResponse`:
```csharp
bool stillTracked = _requirements.ContainsKey(netId);
if (!allowed) {
    // ... deny event ...
} else {
    OnAccessAllowed?.Invoke(netId);
    Debug.Log($"... {(stillTracked ? "" : " [stale, no UI notify]")}");
}
```
**Важно:** для `allowed=true` OnAccessAllowed **всегда** дёргается (даже если stale) —
LockBox может начать анимацию, но если объект уже деспавнился, анимация упадёт
(но это безопасно — StopCoroutine в OnDisable).

### 4.3 Drop в инвентарь на сервере НЕ сразу (race pickup → drop)
**Не реализовано в MVP** (см. Phase 8+ TODO). Сейчас:
- Pickup: `InventoryWorld.TryPickup` — sync add
- Drop: `InventoryWorld.TryDrop` — sync remove + spawn PickupItem в мире
- Между ними — атомарно в пределах RPC, но в multiplayer — не транзакционно

### 4.4 Inventory snapshot между Pickup и Use
`InventoryWorld.HasAllItems` использует **текущее** состояние `_playerInventories`.
Если игрок **только что** подобрал ключ (RPC обработан, snapshot доставлен) — HasAllItems
вернёт true. Если snapshot **ещё в полёте** (но в Unity RPC обрабатываются sync в основном
потоке) — race нет, всё последовательно.

### 4.5 Scene transition (стриминг 24 сцен)
- При выгрузке стриминговой сцены `ScenePlacedObjectSpawner` спавнил scene-placed `NetworkObject` →
  они деспавнятся (NGO auto)
- `MetaRequirement.OnNetworkDespawn` → `MetaRequirementRegistry.UnregisterRequirement` → удаление из `_requirements`
- Клиент получает `OnNetworkDespawn` → `NetworkObject` становится "невалидным"
- Если в этот момент был pending `RequestCanUse` — RPC вернётся со stale netId → попадёт в `OnCanUseResponse` stale-check
- `_requirements` на клиенте НЕ очищается автоматически (только при следующем `PushRequirementsToClient` на реконнекте или scene-load)

### 4.6 Два клиента подходят к одному LockBox одновременно
- Оба клиента отправляют `RequestCanUse` (parallel)
- Сервер обрабатывает **последовательно** (RPC ordered) — каждому свой `allowed/denied`
- Первый: `allowed=true` (если есть ключ), анимация проигрывается у первого
- Второй: `allowed=true` (если тоже есть ключ), анимация проигрывается у второго
- LockBox `_reopenCooldown` — локальная защита от спама анимации на конкретном клиенте
- `_consumeOnUse=true` (Phase 10+) — нужен reservation pattern (TODO)

### 4.7 Disconnect / Reconnect
- `MetaRequirementClientState` — singleton на DontDestroyOnLoad, переживает disconnect
- На reconnect — `OnClientConnected` на сервере → `HandleClientConnected` → `PushRequirementsToClient` (свежий push)
- Client получает новый `ReceiveMetaRequirementBindingsTargetRpc` → `_requirements` обновляется
- Если у клиента в этот момент был pending `RequestCanUse` — `NetworkPlayer` уничтожается → RPC не дойдёт (без crash)
- На reconnect: `_lastCanUseRequestTime = -10f` сбрасывается через новый `ReceiveMetaRequirementResponseTargetRpc`? **Нет** — нужен явный сброс. TODO.

### 4.8 Unknown interactable (нет MetaRequirement)
В `MetaRequirementRegistry.RequestCanUseRpc`:
```csharp
if (req == null) {
    // Без MetaRequirement = "нечего проверять". Разрешаем.
    allowed = true;
    reason = "";
}
```
Это даёт **default allow** для объектов, у которых нет MetaRequirement. Тот же паттерн
что в `ShipKeyServer.CanPlayerBoard` (для совместимости с уже-рабочими сценами).

**⚠️ Caveat:** если хочется жёсткую блокировку "всё что не в реестре = deny" — поменять
на `allowed = false; reason = "Interactable не зарегистрирован"`.

### 4.9 InventoryWorld.Instance == null (race при StartHost)
В `MetaRequirement.CanPlayerUse`:
```csharp
if (InventoryWorld.Instance == null) {
    reason = $"Нужен предмет для: {InteractableDisplayName}";
    return false;
}
```
**Всегда deny** если InventoryWorld не инициализирован. Это защита: лучше отказать
(игрок увидит toast), чем пустить без проверки.

То же в `MetaRequirementRegistry.RequestCanUseRpc`:
```csharp
if (req == null) { allowed = true; reason = ""; }
else { allowed = req.CanPlayerUse(clientId, out reason); }
```
**Если `req` есть, но `InventoryWorld` нет** — `req.CanPlayerUse` сам обработает и deny'ит.

### 4.10 Null / corrupted payload
Все RPC принимают простые типы (`ulong`, `bool`, `string`, массивы). NGO сериализует
через `BufferSerializer<T>`. Если payload corrupted — NGO выбросит exception в `RpcMessage`
handler → логируется в Console. Обработка на нашей стороне: мы используем `?.` и `??`
везде, NRE не должно быть.

### 4.11 Несколько MetaRequirement на одном GameObject
Технически возможно (разные `ItemData[]`, разные `displayName`). `MetaRequirementRegistry._requirements` —
`Dictionary<ulong /*netId*/, MetaRequirement>`, поэтому **только последний** зарегистрируется.
Это **anti-pattern** — один GameObject = один MetaRequirement. Если нужно несколько
требований — комбинируйте в один (например, `_requiredItems=[A, B, C]`).

### 4.12 `MetaRequirement` на `ShipController` (legacy)
Текущий код **разрешает** — `[Ship_Light]` имеет `ShipKeyBinding` (алиас `MetaRequirement`).
Он попадёт в `MetaRequirementRegistry._requirements` (как обычный MetaRequirement),
но в `NetworkPlayer.TryInteractNearestMetaRequirement()` есть фильтр:
```csharp
if (mr.GetComponent<ShipController>() != null) continue;  // skip ships
```
Так что **корабли не обрабатываются через E** — они идут через F (как и раньше). ✓

---

## 5. Сетевой трафик (estimate)

| Событие | Server → Client | Client → Server | Notes |
|---|---|---|---|
| `RequestCanUse` | — | 1 RPC (~30B) | per E-нажатие |
| `ReceiveMetaRequirementResponse` | 1 RPC (~50B + reason) | — | per response |
| `ReceiveMetaRequirementBindings` (push) | 1 RPC (~100B + names) | — | per OnClientConnected |
| `OnAccessDenied` (internal) | — | — | 0 bytes (Unity event) |
| `OnAccessAllowed` (internal) | — | — | 0 bytes (Unity event) |

**Baseline:** ~150B на одно взаимодействие. На 100 интеракций/мин = 15KB/min = 900KB/hour.
Не критично.

**Push** (per OnClientConnected): зависит от количества MetaRequirement в сцене.
WorldScene_0_0: 3 LockBox + 3 ShipKeyBinding (legacy) ≈ 6 шт × ~50B = 300B. Не критично.

---

## 6. Сравнение с Ship Key (старой подсистемой)

| Аспект | ShipKey (legacy) | MetaRequirement |
|---|---|---|
| Items per requirement | 1 (только 1 ключ) | N (массив) |
| Логика | implicit "ALL of 1" | explicit `All` / `Any` / `AtLeastN` |
| UI feedback | toast "Нет ключа корабля (...)" | toast "Нужен предмет: <itemName>" (список) |
| Display name | hardcoded "Корабль" | configurable per-requirement |
| Custom failure message | нет | да |
| Animation hook | только toast | `OnAccessAllowed` event (для визуала) |
| NetworkBehaviour hub | `[ShipKeyServer]` (deprecated) | `[MetaRequirementRegistry]` (generic) |
| DTO | `ShipKeyBindingDto` (1 item per ship) | `MetaRequirementDto` (N items per req, с logic+count+consume) |
| Aliases | — | `ShipKeyBinding` = `MetaRequirement`, и т.д. (4 алиаса) |

**Backward compatibility:** оба flow работают параллельно. Корабли (старые) и LockBox
(новые) сосуществуют без конфликтов.

---

## 7. Что увидит игрок (Player Experience)

| Действие | С ключом | Без ключа |
|---|---|---|
| Подойти к LockBox (E) | scale-up + emissive flash, cooldown 0.5s между повторами | toast «Нужен предмет: Ключ: Синий Замок», 2.5 сек |
| Подойти к Ship (F) | сесть за штурвал | toast «Нет ключа корабля (Корабль Light)» (старый формат) |
| Подойти к Chest (E) | открыть сундук, лут в инвентарь | — (нет MetaRequirement) |
| Подойти к Pickup (E) | подобрать в инвентарь | — |

**Все toast'ы идут вниз экрана, разные подсистемы (ShipKeyToast, MetaRequirementToast) пока
не конфликтуют** (разные VisualElement'ы, разные PanelSettings — ShipKeyPanelSettings и
MetaRequirementPanelSettings).

---

## 8. Что увидит разработчик (Console output)

Типичный успешный сценарий:

```
[InventoryWorld] Created. Items registered: 33
[MetaRequirementRegistry] OnNetworkSpawn. IsServer=True, existing requirements=0
[InventoryServer] RequestPickupRpc received from client 0
[InventoryClientState] OnSnapshotReceived: items=1, handlers=2
[MetaRequirementRegistry] Registered requirement: netId=42, displayName='Синий Сундук', logic=All, itemIds=[31]
[MetaRequirementRegistry] Registered requirement: netId=43, displayName='Красный Сундук', logic=All, itemIds=[32]
[MetaRequirementRegistry] Registered requirement: netId=44, displayName='Зелёный Сундук', logic=All, itemIds=[33]
[MetaRequirementRegistry] PushRequirementsToClient: pushed 3 requirement(s).
[NetworkPlayer:0] ReceiveMetaRequirementResponseTargetRpc (allowed=True)
[MetaRequirementClientState] Use allowed: netId=42, reason=''
```

Без ключа:

```
[MetaRequirementRegistry] CanUse: client=0, obj=42, allowed=False, reason='Нужен предмет: Ключ: Синий Замок'
[MetaRequirementClientState] Access denied: netId=42, reason='Нужен предмет: Ключ: Синий Замок'
```
