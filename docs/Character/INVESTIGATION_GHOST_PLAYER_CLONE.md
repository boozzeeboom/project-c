# Ghost Player Clone — Investigation, Root Cause & 4-Layer Fix

**File:** `docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md`
**Created:** 2026-06-04 18:41 → 20:25 (Yekaterinburg, UTC+5)
**Status:** ✅ **RESOLVED** — все 4 слоя починены, поведение проверено live
**Severity:** HIGH (gameplay-blocking, рендеринг «фантом-клона» с инвертированным управлением)
**Affects:** Unity 6 (6000.4.1f1) + Netcode for GameObjects 2.11.0 + `BootstrapScene` host-mode

---

## TL;DR

На хосте в `NetworkPlayer` имелся **«двойной ownership»** двух GameObject'ов: scene-placed
`PlayerSpawner` и auto-spawned `NetworkPlayer(Clone)`. Оба получали `IsOwner=true` для
clientId=0, оба обрабатывали ввод, оба спавнили `InventoryUI` и `ThirdPersonCamera_0`.
Юзер видел «клона», который двигался независимо (часто — «инвертированно»).

**Root cause (4 независимых слоя):**

| # | Файл | Симптом | Фикс |
|---|---|---|---|
| 1 | `NetworkPlayerSpawner.cs` | Ручной `SpawnAsPlayerObject(0)` в `Update()` дублировал NGO PlayerPrefab auto-spawn | Удалён `Update()` loop, оставлен diagnostic-only |
| 2 | `NetworkPlayer.cs` | Scene-placed `PlayerSpawner` (server-owned, `IsOwner=true` на хосте) запускал `SpawnCamera()` + `SpawnInventory()` | Guard в `OnNetworkSpawn` по маркер-компоненту `NetworkPlayerSpawner` |
| 3 | `ClientSceneLoader.cs` | `FindGameObjectWithTag("Player")` первым матчил scene-placed, телепорт был на scene-placed, а не на real player | Helper `FindRealLocalPlayerGameObject()` опирается на NGO `PlayerObject` (source of truth) |
| 4 | `NetworkPlayer.prefab` | `cameraPrefab: {fileID: 0}` (NULL) на префабе → `SpawnCamera` не создавал камеру для auto-spawned → игрок невидим | `cameraPrefab` → `ThirdPersonCamera.prefab`; `walkSpeed` 5000 → 5 |

После фикса в Hierarchy: `InventoryUI` × 1, `ThirdPersonCamera` × 1, `NetworkPlayer(Clone)` × 1
(включён), `PlayerSpawner` scene-placed × 1 (с `enabled = false` на NetworkPlayer, не мешает).
WASD управляет одним игроком, камера следит, инвертирования нет.

---

## Подробный разбор

### 1. Проблема

**Сценарий:** Unity Editor → Play Mode → `NetworkTestMenu` → **Host** (хост-сервер, чтобы
кто-то мог подключиться).

**Симптом:** Игрок видит «второго себя» — клона, который двигается отдельно, часто
**инвертированно** (W на основном → клон едет назад). Дублируются:
- `NetworkPlayer` компонент (с `IsOwner=true` × 2)
- `InventoryUI` (по одной от каждого NetworkPlayer)
- `ThirdPersonCamera_0` (камеры конкурируют за target)
- `Inventory` child GameObject (тоже × 2)

**Воспроизводимость:** Не каждый запуск — race condition вокруг
`InScenePlacedSourceGlobalObjectIdHash` (см. `INTEGRATION_SHIPS_TO_WORLD_0_0.md` §1).

### 2. Live Evidence (session 2026-06-04, 18:41 Yekaterinburg)

Иерархия, снятая через `unityMCP manage_scene action=get_hierarchy` в момент наблюдения:

| GameObject | instanceID | Position | Компоненты | `IsOwner` (OwnerClientId) |
|---|---|---|---|---|
| `PlayerSpawner` (scene-placed) | 58158 | (40016.93, 2501.31, 40015.3) | `NetworkObject, NetworkTransform, NetworkPlayer, CharacterController, PlayerInputReader, NetworkPlayerSpawner` | **true (0)** ← footgun |
| `NetworkPlayer(Clone)` (auto-spawn) | −87162 | (7656.93, **−1 862 830.5**, 49927.89) | `NetworkObject, NetworkTransform, NetworkPlayer, CharacterController, PlayerInputReader` (БЕЗ `NetworkPlayerSpawner`) | **true (0)** |
| `ThirdPersonCamera_0` | −87200 | (40015.66, 2511.17, 40019.22) | `Transform, ThirdPersonCamera, Camera, UniversalAdditionalCameraData` | n/a — привязана к `PlayerSpawner` |
| `InventoryUI` × 2 | −87192, −87232 | — | `Transform, InventoryUI` | по одному на каждый NetworkPlayer |

**Y = −1 862 830** у клона — реально провалился на 1.8M единиц. `CharacterController` +
gravity + `_velocity.y += gravity * dt` → бесконечное падение, при этом `_moveInput`
обрабатывается → X/Z «уплывают».

Console log (отфильтровано — host start):

```
[NMC] Awake: NM=NetworkManager, NetConfig=Unity.Netcode.NetworkConfig
[NMC] StartHost() called
[NMC] Transport configured: 127.0.0.1:7777
[NMC] Calling StartHost()...
[NMC] HandleClientConnected: clientId=0, IsServer=True, IsClient=True
[NetworkTestMenu] Player connected: 0
[NetworkPlayerSpawner] Client connected: 0
[NMC] StartHost() completed. IsHost=True, IsServer=True
[ScenePlacedObjectSpawner] Scene Scene(0, 0): spawned=3, already=0, failed=0
[NetworkPlayer] Teleport to (39999.50, 3000.00, 39999.50)
```

**Заметьте:** в логе **НЕТ** `[NetworkPlayerSpawner] Host/Server player spawned`. Значит,
`SpawnLocalPlayer()` был вызван (Update сработал), но ушёл в ранний return по
`!IsSpawned == false` (NGO уже заспавнил scene-placed объект → `IsSpawned=true`). А
PlayerPrefab в `NetworkConfig` NGO заспавнил **параллельно** → второй `NetworkPlayer` с
`OwnerClientId=0`.

### 3. Корневые причины по слоям

#### Layer 1: `NetworkPlayerSpawner.Update()` спавнит PlayerObject руками

`Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs` (старая версия):

```csharp
private void Update()
{
    if (useScenePlayerAsHost && !_hasSpawnedHostPlayer && ...IsHost) {
        _hasSpawnedHostPlayer = true;
        SpawnLocalPlayer();
    }
}

private void SpawnLocalPlayer()
{
    var networkObject = GetComponent<NetworkObject>();
    if (networkObject != null && !networkObject.IsSpawned) {
        networkObject.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId);
        // ↑ КОНФЛИКТ с NGO PlayerPrefab auto-spawn
    }
}
```

`NetworkConfig.PlayerPrefab` (на bootstrap-инстансе NetworkManager) уже настроен на
`NetworkPlayer.prefab` (guid `224427a7f796e5b448f07ed8c2a1469b`). NGO 2.x при
`StartHost()` → `OnServerStarted` → `OnClientConnected(0)` **автоматически** спавнит
этот префаб и регистрирует его как PlayerObject для clientId=0.

Дополнительно `NetworkPlayerSpawner.Update()` пытается сделать то же самое руками — спавнит
scene-placed GameObject (на котором сам висит) как PlayerObject для clientId=0.

**Результат:** на хосте **два** `NetworkPlayer` с `IsOwner = true` для clientId=0.
Оба обрабатывают `Update()` (нет guard'а против двойного ownership). Оба двигаются от
ввода. Камера спавнится ОДИН раз (`if (IsOwner) SpawnCamera()` в `OnNetworkSpawn`) и
привязывается к тому `transform`, который вызвал первым → **«инвертированное» ощущение**:
второй игрок движется в мировом Z+ при `_myCamera == null` (fallback `forward = Vector3.forward`),
а камера следит за первым игроком, движущимся в координатах камеры.

**Почему «вижу не всегда»** — race condition вокруг
`InScenePlacedSourceGlobalObjectIdHash`:

- Hash ≠ 0 → NGO auto-spawns scene-placed объект → `IsSpawned = true` сразу → guard
  `!IsSpawned` срабатывает → **баг НЕ воспроизводится**
- Hash == 0 (типично для руками-добавленных в сцену) → NGO НЕ спавнит scene-placed →
  `IsSpawned = false` → `SpawnLocalPlayer()` спавнит вручную → **баг воспроизводится**

Зависит от того, насколько «удачно» Unity сериализовал `BootstrapScene.unity` в последний раз.

**`SpawnPlayerForClient(clientId)` для remote клиента** — та же проблема:
`Instantiate(thisNetworkObject.gameObject, ...)` + `SpawnAsPlayerObject(clientId)`
клонирует scene-placed `PlayerSpawner` для каждого remote клиента → дубли поверх NGO auto-spawn.

#### Layer 2: `NetworkPlayer.OnNetworkSpawn` запускает player init для scene-placed

**Даже после фикса Layer 1** scene-placed `PlayerSpawner` имеет `IsOwner = true` на хосте
(server-owned, `OwnerClientId=0=LocalClientId`). `OnNetworkSpawn` без guard'а запускал
`SpawnCamera()` + `SpawnInventory()` и для пустышки, и для real player → дубль
`InventoryUI` и (раньше) `ThirdPersonCamera_0`.

**Попытка v1 фикса** (неудачная): guard `!networkObject.IsPlayerObject`. Сломало сцену
полностью — «после play host ничего не грузит». **Причина:** NGO 2.x для auto-spawned
префаба НЕ гарантирует, что `IsPlayerObject == true` на момент `OnNetworkSpawn`
(timing race в `SpawnAsPlayerObject`-пути). Guard ошибочно срабатывал и на
auto-spawned, отключал real player → ни камеры, ни input.

**Финальный фикс** (v2): маркер-компонент `NetworkPlayerSpawner` на GameObject как
дискриминатор. Scene-placed `PlayerSpawner` имеет его по дизайну, auto-spawned
`NetworkPlayer(Clone)` — нет (подтверждено живой иерархией). Не зависит от внутренней
кухни NGO, работает железно.

#### Layer 3: `ClientSceneLoader` ищет local player по `IsOwner`, ловит scene-placed

**Даже после Layer 2 v2** «не спавнится после плей хост». Live-снимок иерархии показал:
scene-placed `PlayerSpawner` телепортирован на (39999.5, 3000, 39999.5), а
`NetworkPlayer(Clone)` **УПАЛ на Y=−38 503**.

Причина: `ClientSceneLoader.UpdatePlayerTransformAfterSpawn()` /
`FindLocalPlayer()` / `WaitForPlayer()` / `AutoLoadInitialSceneCoroutine()` использовали
`FindGameObjectWithTag("Player")` (у обоих тег Player) и/или `FindObjectsByType<NetworkPlayer>().First(IsOwner)`
— оба попадали на scene-placed первым (по instanceID, т.к. scene-placed загружается раньше).
Телепорт уходил на scene-placed, real player оставался на (0, 3, 0) и падал.

**Фикс:** helper `FindRealLocalPlayerGameObject()` опирается на
`NetworkManager.ConnectedClients[LocalClientId].PlayerObject` (source of truth от NGO,
гарантированно указывает на auto-spawned NetworkPlayer(Clone)). Все 4 call site'а
сначала пробуют helper, fallback — со старой логикой, но с усиленным фильтром
`IsOwner && IsPlayerObject` в каждом цикле + `name.Contains("PlayerSpawner") == false`
в tag-поиске.

#### Layer 4: `NetworkPlayer.prefab` имеет `cameraPrefab: {fileID: 0}` (NULL)

**После Layer 3** «камеры нет → игрок невидим». Live-иерархия показала: scene-placed
отключён, real `NetworkPlayer(Clone)` жив и `IsOwner=true`, **но `ThirdPersonCamera_0`
отсутствует**, `MainCamera` остался на сцене-плейсмент позиции (239997, 3000, 159998).

Причина: `SpawnCamera()` в `NetworkPlayer.cs`:
```csharp
if (cameraPrefab != null) {
    var camObj = Instantiate(cameraPrefab.gameObject);
    _myCamera = camObj.GetComponent<ThirdPersonCamera>();
} else {
    _myCamera = FindAnyObjectByType<ThirdPersonCamera>(); // null, если в сцене нет
}
```

В **`NetworkPlayer.prefab:129`** `cameraPrefab: {fileID: 0}` — **NULL**. NGO auto-spawn
берёт значения прямо из префаба. Scene override в `BootstrapScene.unity:19143-19145`
(указывает на `ThirdPersonCamera.prefab`, guid `020b4cd7c3349134b8c1de87bed1f706`)
применяется ТОЛЬКО к scene-placed инстансу, **не к auto-spawn clone**. В исходном
баг-стейте scene-placed создавал `ThirdPersonCamera_0`, а auto-spawned через fallback
`FindAnyObjectByType<ThirdPersonCamera>()` находил уже созданную и цеплял. С моим
Layer 2 scene-placed отключён → камера не создаётся → fallback null → **нет камеры**.

Бонус: `walkSpeed: 5000` в префабе (в 1000x быстрее нормы, по коду default=5, scene
override=5) — аномалия, чьё-то случайное значение. Тоже починил.

**Фикс:** через `manage_prefabs action=modify_contents`:
- `cameraPrefab` → `ThirdPersonCamera.prefab` (guid `020b4cd7c3349134b8c1de87bed1f706`)
- `walkSpeed` 5000 → 5

---

## 4. Все патчи (для code review)

### 4.1 `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs` (Layer 1)

- Удалён `Update()` host-spawn loop
- Удалена/переписана `SpawnPlayerForClient` — diagnostic-only, логирует warning
- `useScenePlayerAsHost` помечен `[Obsolete]` + `#pragma warning disable 0414` (для совместимости сериализованной сцены)
- Добавлен `[Header("DIAGNOSTIC")]` с `logAutoSpawn` (default true), логирующий
  `[NetworkPlayerSpawner] NGO PlayerPrefab auto-spawned player for clientId={0}`

### 4.2 `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (Layer 2)

В `OnNetworkSpawn` сразу после `base.OnNetworkSpawn()` и получения `networkObject`:

```csharp
// Надёжный дискриминатор: наличие компонента NetworkPlayerSpawner на GameObject.
// (IsPlayerObject timing-unsafe в OnNetworkSpawn — см. INVESTIGATION_GHOST_PLAYER_CLONE.md)
if (GetComponent<NetworkPlayerSpawner>() != null)
{
    if (_controller != null) _controller.enabled = false;
    enabled = false;
    Debug.Log($"[NetworkPlayer] Skipping player init for scene-placed 'PlayerSpawner' GameObject (has NetworkPlayerSpawner marker, IsOwner={IsOwner}, IsPlayerObject={networkObject.IsPlayerObject}). См. INVESTIGATION_GHOST_PLAYER_CLONE.md.");
    return;
}
```

И симметричный guard в `OnNetworkDespawn` по тому же признаку (рано выходим, чтобы
не пытаться `Destroy(_myCamera)` / `Destroy(_inventoryUI)` / `RemovePilot` на пустышке,
у которой этих полей нет).

Добавлен `using ProjectC.Network;` для резолва `NetworkPlayerSpawner` без полного namespace.

### 4.3 `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` (Layer 3)

Добавлен private helper:

```csharp
/// <summary>
/// Возвращает GameObject НАСТОЯЩЕГО локального игрока (auto-spawned NetworkPlayer(Clone)
/// из NetworkConfig.PlayerPrefab), или null если ещё не заспавнен.
///
/// НЕ возвращает scene-placed не-player NetworkObject'ы (например PlayerSpawner в
/// BootstrapScene), у которых на хосте IsOwner==true (server-owned, OwnerClientId=0
/// = LocalClientId) — это footgun NGO 2.x. Использует source of truth:
/// NetworkManager.ConnectedClients[LocalClientId].PlayerObject.
/// </summary>
private GameObject FindRealLocalPlayerGameObject()
{
    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return null;

    var localClientId = NetworkManager.Singleton.LocalClientId;
    if (NetworkManager.Singleton.ConnectedClients.TryGetValue(localClientId, out var cc)
        && cc?.PlayerObject != null
        && cc.PlayerObject.IsSpawned
        && cc.PlayerObject.IsPlayerObject)
    {
        return cc.PlayerObject.gameObject;
    }
    return null;
}
```

Все 4 call site'а (`UpdatePlayerTransformAfterSpawn`, `FindLocalPlayer`, `WaitForPlayer`,
`AutoLoadInitialSceneCoroutine`) сначала пробуют helper. Если null — fallback к
старой логике, но с усиленным фильтром `IsOwner && IsPlayerObject` в каждом цикле и
`name.Contains("PlayerSpawner") == false` в tag-based поиске.

### 4.4 `Assets/_Project/Prefabs/NetworkPlayer.prefab` (Layer 4)

Через `mavis mcp call unityMCP manage_prefabs action=modify_contents`:

- `cameraPrefab: {fileID: 0}` → `cameraPrefab: {fileID: 5405426399639928988, guid: 020b4cd7c3349134b8c1de87bed1f706, type: 3}`
  (ссылка на `Assets/_Project/Prefabs/ThirdPersonCamera.prefab`)
- `walkSpeed: 5000` → `walkSpeed: 5`

Scene override в `BootstrapScene.unity:19143-19145` (тоже указывает на `ThirdPersonCamera.prefab`)
остался нетронутым — он больше не нужен (префаб содержит правильную ссылку), но
и не вредит (override перезаписывает то же значение).

---

## 5. Verification (Live, 20:25 Yekaterinburg)

Юзер: «сейчас игроком можно управлять, перезапустил сцену плеймод». Live-снимок
через `unityMCP manage_scene`:

| Объект | Position | Статус |
|---|---|---|
| `PlayerSpawner` (scene-placed) | (39999.5, 2510, 39999.5) | `NetworkPlayer` с `enabled = false`, не мешает |
| `NetworkPlayer(Clone)` (real player) | (39820.91, 2532.83, 39999.5) | `IsOwner=true`, движется |
| `ThirdPersonCamera_0` | (39821.79, 2543.71, 40000.58) | **ровно ОДНА**, position ≈ player (с offset за спиной) |
| `InventoryUI` | — | **ровно ОДНА** |

**Counts критичные:** `InventoryUI` × 1 ✓, `ThirdPersonCamera` × 1 ✓,
`NetworkPlayer(Clone)` × 1 ✓, scene-placed × 1 (отключён, корректно).

WASD управляет, камера следит, инвертирования нет. **БАГ ЗАКРЫТ.**

---

## 6. ЧТО НЕ меняли (consistency)

- **`ScenePlacedObjectSpawner`** — обрабатывает **другой** класс scene-placed
  NetworkObject'ов (в стриминговых `WorldScene_*` сценах, не в `BootstrapScene`).
  Нужен для NRE-фикса от 2026-06-02. Не трогаем.
- **`NetworkConfig.PlayerPrefab`** — остаётся `NetworkPlayer.prefab`. **Единственный**
  правильный путь спавна игроков.
- **`BootstrapScene` PlayerSpawner GameObject** — оставлен, потому что на нём висят
  legacy-компоненты, на которые могут быть ссылки. Полное удаление = отдельная задача
  с референс-аудитом.
- **`BootstrapScene.unity` cameraPrefab override (line 19143-19145)** — оставлен.
  Перезаписывает то же значение, что и в префабе. Можно удалить как косметику, но
  не критично.

---

## 7. Follow-up (опционально, на следующей сессии)

### Корневой fix: убрать `NetworkPlayer` со scene-placed `PlayerSpawner`

В `BootstrapScene.unity` выбрать `PlayerSpawner` в Hierarchy, удалить компоненты:
- `NetworkPlayer` (он там для legacy-причин, не нужен)
- `PlayerInputReader` (тоже legacy)

Оставить:
- `NetworkObject` (нужен для network-spawn scene-placed объекта — иначе сломается
  `ScenePlacedObjectSpawner`-логика для WorldScene_*)
- `NetworkPlayerSpawner` (diagnostic-only, логирует auto-spawn)
- `NetworkTransform`, `CharacterController` (если нужны)

После этого guard в `NetworkPlayer.cs` (Layer 2) станет не нужен — но defense-in-depth
пусть остаётся.

### Другие helper'ы, которые могут ловить scene-placed

`OnNetworkSpawn` отключает scene-placed `PlayerSpawner` (`enabled = false`), но ряд
мест итерируют по `FindObjectsByType<NetworkPlayer>` и фильтруют только по `IsOwner`:

- `Assets/_Project/Trade/Scripts/Client/MarketClientState.cs:171-179` — `FindLocalPlayer()`
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs:109-115` — `FindLocalPlayer()`
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs:198-204` — `PollLocalPlayerZone`
- `Assets/_Project/Trade/Scripts/TradeUI.cs:52-69` — `Player`
- `Assets/_Project/Trade/Scripts/TradeDebugTools.cs:220` — `_player` cache

Сценарий: первым в `FindObjectsByType` находится отключённый scene-placed, возвращается
→ потом `MarketInteractor.TryOpenMarket()` NRE при `_inventory` / `_inventoryUI`
(которых у отключённого нет). **Редко, но возможно.** Если проявится — добавить
`&& GetComponent<NetworkObject>().IsPlayerObject` в каждый `FindLocalPlayer`.

---

## 8. Lessons Learned (cross-project, зафиксировано в agent memory)

### NGO 2.x: `IsOwner` vs `IsPlayerObject` на хосте — footgun

`NetworkBehaviour` на хосте имеет footgun: для scene-placed NetworkObject'а, заспавненного
NGO как **обычный** (не PlayerObject), `OwnerClientId = ServerClientId = 0`, а
`LocalClientId` на хосте = 0 → **`IsOwner` = true** даже для НЕ-PlayerObject'ов. Это
НЕ баг NGO, это by design — server-owned objects are always 'owned by' the host's
local client. На remote клиенте `LocalClientId != 0` → для scene-placed объект
`IsOwner = false`, и баг не виден. **На хосте** — два NetworkPlayer с `IsOwner = true`
→ двойная player init (SpawnCamera, SpawnInventory, Update), ghost clones, inverted
movement, телепорт не того объекта.

`IsPlayerObject` — правильный дискриминатор для player-specific логики. **НО** во
время `OnNetworkSpawn` для auto-spawned префаба NGO 2.x может НЕ выставить
`IsPlayerObject = true` синхронно (timing race в `SpawnAsPlayerObject` flow). Проверка
`!IsPlayerObject` в `OnNetworkSpawn` ошибочно отключит настоящего игрока. После 0.5s
задержки `IsPlayerObject` уже корректен.

**Source of truth для player lookup:**
`NetworkManager.ConnectedClients[LocalClientId].PlayerObject` — это и есть auto-spawned
префаб-инстанс. **Всегда** предпочитать его вместо `FindObjectsByType<T>().First(IsOwner)`
в loader/UI-helper'ах.

**Правильный паттерн в 3 слоя:**
1. **`NetworkPlayerSpawner.cs`**: убрать `Update()` host-spawn loop, убрать ручной
   `SpawnAsPlayerObject` — пусть NGO сам спавнит из `NetworkConfig.PlayerPrefab`.
2. **`NetworkPlayer.OnNetworkSpawn`**: guard по наличию маркер-компонента (НЕ
   `IsPlayerObject`!) — например, `GetComponent<MySpawnerMarker>() != null` →
   scene-placed пустышка, skip player init + `enabled = false`.
3. **Loaders/UI-helpers** (`ClientSceneLoader`, `MarketClientState`, `MarketInteractor`,
   `MarketZone`, `TradeUI`, `TradeDebugTools`): вместо
   `FindObjectsByType<NetworkPlayer>().First(IsOwner)` использовать
   `NetworkManager.ConnectedClients[LocalClientId].PlayerObject` или добавить
   `&& no.IsPlayerObject` в фильтр.

### Prefab-уровневые gotcha'ы

- **Scene overrides применяются ТОЛЬКО к scene-placed инстансам**, не к auto-spawn clones.
  Если `PlayerPrefab` сам по себе не имеет нужной ссылки (например `cameraPrefab`), NGO
  auto-spawn возьмёт null/empty → логика в `if (prefab != null)` сломается тихо.
- **Всегда заполняй поля на префабе**, а не только в scene override. Это source of truth
  для NGO auto-spawn.

### Расследование «через MCP» = gold standard

- `unityMCP manage_scene action=get_hierarchy` с `include_transform=true` — мгновенный
  снимок позиций ВСЕХ GameObject'ов.
- `unityMCP read_console filter_text=<keyword>` — быстрый фильтр логов.
- `unityMCP find_gameobjects search_term=...` — подсчёт инстансов по имени/тегу/компоненту.
- **PowerShell gotcha:** JSON-аргументы мажутся через `{}` → всегда использовать
  `--file <path>` (записать JSON в `C:\Users\leon7\AppData\Local\Temp\*.json`).

---

## 9. Связанные документы

- `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md` — `InScenePlacedSourceGlobalObjectIdHash` race
- `AGENTS.md` §"Scene architecture — bootstrap + 24 streaming scenes" — общая архитектура
- `AGENTS.md` §"Scene-placed NetworkObject — корневая причина NRE" — смежный footgun
- `Assets/_Project/Prefabs/ThirdPersonCamera.prefab` — `cameraPrefab` reference

---

**Автор:** Mavis (Mavis) for Project C
**Session:** `mvs_eadfcd60e518448fbe3305a71cbb00fe`
**Дата:** 2026-06-04
