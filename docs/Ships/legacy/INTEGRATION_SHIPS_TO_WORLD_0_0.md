# Интеграция ShipController в WorldScene_0_0

**Статус:** в работе (диагноз подтверждён, фикс не начат)
**Автор разбора:** Mavis (агент-напарник)
**Дата:** 2026-06-02

---

## 1. Контекст

**Цель этапа:** сделать сцену `WorldScene_0_0` рабочей на ~90% как изолированный sandbox. Корабли должны спавниться, игрок должен садиться/выходить, RPC должны работать.

**Что сделано до этого разбора:**
- В `WorldScene_0_0` вручную добавлены 3 тестовых ShipController (instanceID 94204, 94146, 94074)
- Игрок нажимает **F** → садится в корабль (визуально работает)
- Дальше что-то ломается → в консоли спам `NullReferenceException` на `__endSendRpc` для `AddPilotRpc` и `SubmitShipInputRpc`

**Что не сделано (явно, по согласованию с пользователем):**
- Полноценная стриминговая система (`WorldSceneManager`, `ServerSceneManager`, `WorldStreamingManager` — код написан, но в bootstrap-сцене **НЕ развёрнут**)
- World в целом: FloatingOrigin проблема не решена, переход игрока между 24 сценами не доведён
- Поэтому рефакторить стриминг сейчас — **НЕЛЬЗЯ**. Фокус — заставить работать 0_0.

---

## 2. Архитектура сцен (что нашёл)

### Build Settings

| buildIndex | Сцена | Роль |
|---|---|---|
| 0 | `Assets/_Project/Scenes/BootstrapScene.unity` | Стартовая. Загружается первой при запуске. Содержит меню, NetworkManager, базовые сервисы |
| 1–6 | `WorldScene_0_0..0_5` | Колонка X=0, Z=0..5. Каждая = 80 000 × 80 000 units |
| 7–12 | `WorldScene_1_0..1_5` | Колонка X=1, Z=0..5 |
| 13–18 | `WorldScene_2_0..2_5` | Колонка X=2, Z=0..5 |
| 19–24 | `WorldScene_3_0..3_5` | Колонка X=3, Z=0..5 |

Итого **25 сцен** (1 bootstrap + 24 стриминговых, сетка 6×4). Сетка определена в `Assets/_Project/Data/Scene/SceneRegistry.asset`.

### Что есть в BootstrapScene

Из `grep` по `BootstrapScene.unity`:

| GameObject / компонент | Файл / GUID | Роль |
|---|---|---|
| `NetworkManager` (line 12964) | префаб `Assets/_Project/Prefabs/NetworkManager.prefab` (guid `593a2fe42fa9d37498c96f9a383b6521`) | Singleton NGO. `DontDestroyOnLoad` через `NetworkManagerController.Awake()` → живёт во всех сценах |
| `NetworkManagerController` | `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | Обёртка: StartHost / StartServer / ConnectToServer / Reconnect. Добавляет `UnityTransport` в Awake |
| `UnityTransport` | NGO package | Транспорт (порт 7777) |
| `NetworkPlayerSpawner` (line 19072) | `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs` | Спавнит host player'а в NetworkPlayer.prefab |
| `ClientSceneLoader` (line 1710) | `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs` | Грузит 24 стриминговые сцены additive через **обычный** `UnityEngine.SceneManagement.SceneManager.LoadSceneAsync` (НЕ `NetworkSceneManager`!) |

**Чего в bootstrap НЕТ** (хотя код написан):
- `WorldSceneManager` (координатор Scene ↔ Chunk ↔ FloatingOrigin)
- `ServerSceneManager` (server-side tracking клиентов по сценам, `NetworkHide/NetworkShow`)
- `WorldStreamingManager` (chunk-based стриминг внутри сцены)
- `WorldChunkManager`, `ChunkLoader`, `FloatingOriginMP`
- `ChunkNetworkSpawner` (спавн сундуков/NPC по чанкам)

→ **Эти подсистемы на данный момент не развёрнуты в сцене.** Код существует, но в bootstrap их нет.

### Что есть в WorldScene_0_0

Из `grep` по `WorldScene_0_0.unity`:

| Объект | Компоненты |
|---|---|
| Root `WorldRoot_0_0` | 5 дочерних (геометрия мира: пики, фермы, прочее) |
| Root `ships` | 3 дочерних — три тестовых ShipController |
| Каждый `ShipController` | `Rigidbody` + `NetworkObject` (RequireComponent добавил автоматически) + `ShipController.cs` (NetworkBehaviour) |

**Чего НЕТ на кораблях** (для правильной работы):
- `NetworkTransform` (Authority: Server) — без него позиция не синхронизируется между клиентом и сервером
- `SceneBoundNetworkObject` — для per-scene видимости (когда стриминг будет доведён)

### NetworkConfig в NetworkManager.prefab

```yaml
NetworkConfig:
  PlayerPrefab: {guid: 224427a7f796e5b448f07ed8c2a1469b}  # NetworkPlayer.prefab
  Prefabs:
    NetworkPrefabsLists: []  # ПУСТО! DefaultNetworkPrefabs.asset не подключён сюда
  EnableSceneManagement: 1  # NGO сам управляет сценами
  PlayerPrefab -> NetworkPlayer
  NetworkPrefabsList -> ПУСТОЙ (DefaultNetworkPrefabs.asset существует, но не присвоен)
```

`DefaultNetworkPrefabs.asset` (Assets/DefaultNetworkPrefabs.asset) существует, но **не присвоен** в `NetworkConfig.Prefabs.NetworkPrefabsLists`. Это отдельный issue (TODO на потом), для текущей задачи не критично.

---

## 3. Корневая причина NRE

### Трассировка из консоли

```
NetworkBehaviour.__endSendRpc (...)  ← NRE
  ← ShipController.AddPilotRpc (line 917)
  ← ShipController.AddPilot (line 911)
  ← NetworkPlayer.SubmitSwitchModeRpc (line 395)
  ← (auto-generated receive handler)
```

И вторая (на каждый кадр после посадки):
```
NetworkBehaviour.__endSendRpc (...)  ← NRE
  ← ShipController.SubmitShipInputRpc (line 608)
  ← ShipController.SendShipInput (line 620)
  ← NetworkPlayer.Update (line 262)
```

### Что значит этот NRE

NGO 2.x `__endSendRpc` (line 354 package `com.unity.netcode.gameobjects`) вызывает `m_NetworkObject.NetworkManager`, потом — `m_NetworkManager.MessagingSystem.SendMessage(...)`. Если на любом из этих шагов что-то `null` — NRE.

`m_NetworkObject` присваивается в `NetworkBehaviour.__initializeVariables()` через `GetComponent<NetworkObject>()`. Это работает всегда, если GameObject жив.

Реальный источник NRE — `NetworkManager.Singleton == null` (NGO не инициализирован). Когда `NetworkManager.Singleton == null`:
- `NetworkBehaviour.SendToEveryone` / `SendToServer` маппит target в `NetworkManager.Singleton` → NRE
- `__endSendRpc` сразу падает, потому что первый же `MessagingSystem` lookup вылетает

### Почему NetworkManager == null

**Гипотеза (подтверждается косвенно):** пользователь открыл `WorldScene_0_0` напрямую в редакторе, **без bootstrap**. В этом случае:

- В сцене **нет** NetworkManager (мы проверили: `find_gameobjects by_component Unity.Netcode.NetworkManager` → 0 results)
- `NetworkManager.Singleton` остаётся `null` навсегда
- Любой `[Rpc]` на любом `NetworkBehaviour` → NRE в `__endSendRpc`

**Косвенные подтверждения:**
- В bootstrap `NetworkManager` есть (line 12964)
- Но в самой `WorldScene_0_0` его нет
- Если бы пользователь запустил через bootstrap (File → Build → Run / или Editor → Play в BootstrapScene), NetworkManager бы работал
- `__endSendRpc` падает на **обоих** RPC подряд (AddPilot и SubmitShipInput) — это не проблема "одного спавна", это проблема **полного отсутствия NGO runtime**

### Что нужно понять про scene-placed NetworkObject

Дополнительно: даже если NetworkManager есть, в проекте есть нюанс, который **может** вылезти позже:

- Сцены грузятся через `SceneManager.LoadSceneAsync(LoadSceneMode.Additive)` (`ClientSceneLoader.cs:744`), **не** через `NetworkSceneManager.LoadScene`
- С `EnableSceneManagement: 1` NGO **должен** спавнить scene-placed NetworkObjects автоматически при загрузке сцены (на сервере)
- Но если сцена загружена **до** `StartHost()` (т.е. была на сцене когда нажали Play), NGO может не зарегистрировать её как network scene
- Сейчас это не проблема, потому что проблема №1 (NetworkManager отсутствует) забивает всё

**Когда** пользователь перейдёт к запуску через bootstrap, нужно будет проверить, что scene-placed `ShipController` корректно спавнятся. Это будет видно по тому, что `IsSpawned == true` на ShipController после `StartHost()`. Если `false` — добавим `ScenePlacedObjectSpawner` (см. план ниже).

---

## 4. План фикса (минимально-инвазивный, для 0_0)

### 4.1. Документация

✅ Сделано в этой сессии:
- Этот файл — `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md`
- `docs/context/ship.md` — обновить (после фикса): добавить `NetworkTransform` в список компонентов, уточнить про scene-placed
- `docs/context/network.md` — обновить (после фикса): добавить раздел про scene-placed NetworkObject

### 4.2. Корабли — добавить `NetworkTransform` на каждый из 3

Через Unity MCP, для каждого из 3 кораблей (instanceID 94204, 94146, 94074):
1. Добавить компонент `Unity.Netcode.Components.NetworkTransform`
2. Выставить `Authority: Server` (свойство `AuthorityMode: 1`)
3. Прочие дефолты (SyncPosition/Rotation/Scale = true, PositionThreshold = 0.001) — ОК
4. **Зачем:** без NetworkTransform NetworkObject сам по себе не реплицирует позицию. Корабль будет стоять на стартовой точке у клиентов, даже если сервер его двигает

### 4.3. Guards в коде (защита от NRE)

`Assets/_Project/Scripts/Player/NetworkPlayer.cs`:
- В `Update()` перед `SubmitSwitchModeRpc()` (line 237) — добавить guard
- В `Update()` перед `SendShipInput(...)` (line 262) — расширить guard (уже есть частичный)

`Assets/_Project/Scripts/Player/ShipController.cs`:
- В `AddPilot(...)` (line 909) — добавить guard
- В `RemovePilot(...)` (line 924) — добавить guard
- В `SendShipInput(...)` (line 618) — добавить guard (для полноты)

**Зачем:** защита от NRE в edge cases (scene transition, domain reload, Network shutdown). Не лечит первопричину, но убирает спам в консоли и оставляет информативный Debug.Log вместо NRE.

### 4.4. ScenePlacedObjectSpawner — **ТОЛЬКО ЕСЛИ НУЖНО**

Создать `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` (~40 строк):
- `MonoBehaviour`, singleton, `DontDestroyOnLoad`
- В `Start()` подписаться на `ClientSceneLoader.OnSceneLoaded`
- В обработчике: `if (!NetworkManager.Singleton.IsServer) return;` → найти все `NetworkObject` в загруженной сцене через `GetRootGameObjects()` → для каждого с `!IsSpawned` вызвать `Spawn(destroyWithScene: true)`
- Логировать

**Создаём ТОЛЬКО ЕСЛИ** пользователь в Play Mode увидит, что `ShipController.IsSpawned == false`. Это **диагностический шаг**, не превентивный.

### 4.5. НЕ делаем

- ❌ `NetworkManager` в 0_0 — не нужен при workflow через bootstrap
- ❌ `SceneBoundNetworkObject` — для будущего, когда стриминг будет доведён
- ❌ `DefaultNetworkPrefabs.asset` — known issue, отдельный тикет
- ❌ Рефактор `ClientSceneLoader` на `NetworkSceneManager` — сломает многое, не в фокусе

### 4.6. Проверка (verification)

После фикса пользователь должен:
1. **Открыть `WorldScene_0_0` в редакторе** (как сейчас)
2. **Нажать Play** → должен появиться host (NetworkManagerController.StartHost вызывается автоматически? или вручную через UI/клавишу?)
3. **Проверить консоль** → 0 errors, 0 NRE про `__endSendRpc`
4. **Подойти к кораблю, нажать F** → сесть, видно как корабль "управляется" (W/S thrust)
5. **Нажать F ещё раз** → выйти
6. **Проверить NetworkManager в Hierarchy** → `NetworkManager` GameObject есть с компонентами
7. **Проверить Inspector на одном из кораблей** → `NetworkObject` + `NetworkTransform` (Authority: Server)

---

## 5. Что НЕ делаем (и почему)

| Не делаем | Почему |
|---|---|
| `SceneBoundNetworkObject` на кораблях | Код написан, но `ServerSceneManager` не развёрнут. Без него `RegisterSceneObject` не вызывается и фильтрация не работает. На текущей фазе все 3 корабля и так в 0_0, и она грузится для теста — per-scene видимость не нужна. TODO: добавить когда стриминг будет доведён |
| Серверный спавнер scene-placed NetworkObject (см. 4.4) | Превентивно — не нужно. Создадим только если `IsSpawned == false` после `StartHost()`. Диагностический, не превентивный |
| Рефактор `ClientSceneLoader` на `NetworkSceneManager` | Огромный рефактор стриминговой системы. Сломает `WorldSceneManager`, `ServerSceneManager`, `FloatingOrigin` логику. Не в фокусе |
| Перенос `NetworkManager` из bootstrap в 0_0 (или наоборот) | NetworkManager **уже** в bootstrap, через `DontDestroyOnLoad` живёт во всех сценах. Перенос ничего не даст, только сломает singleton |
| Подключение `DefaultNetworkPrefabs.asset` в NetworkConfig | Отдельный issue. Не критично для текущего NRE (RPC работают по имени + GlobalObjectIdHash). TODO: разобраться когда будет следующий issue с динамическим спавном |
| Замена `Keyboard.current.fKey.wasPressedThisFrame` в `NetworkPlayer.Update` на `PlayerInputReader` | AGENTS.md требует `PlayerInputReader` для input, но это **отдельный рефактор**, не связан с NRE. TODO: следующая сессия |
| `DontDestroyOnLoad` на спавненных кораблях | Костыль. Правильный путь — `SceneBoundNetworkObject` + `ServerSceneManager.HideSceneObjectsFromClient` (уже написан). Когда стриминг будет доведён — `destroyWithScene: true` + `NetworkHide/Show` |
| Изменение `ShipController.AddPilotRpc` (SendTo.Everyone → SendTo.Server) | Архитектурно может быть правильнее, но `SendTo.Everyone` нужен потому что скрытие игрока (`_controller.enabled = false`, `_playerRenderers`) делается **на каждом клиенте** независимо. Смена семантики — отдельный рефактор |

---

## 6. Уточнённый диагноз (по ответам пользователя, 2026-06-02)

### Workflow пользователя

> *«Для редактирования перехожу в сцену 0_0, редактирую. Для тестов перехожу в bootstrap-сцену, запускаю игру. Появляется меню — жму Host — спавнится персонаж уже в сцене 0_0 на координатах 39000, 3000, 39000 (захардкожено где-то). Если нажать Play со сцены 0_0 — ничего не будет работать.»*

То есть:
- **Правит** в `WorldScene_0_0` напрямую (NetworkManager там не нужен)
- **Тестирует** через bootstrap: открывает `BootstrapScene` в редакторе → Play → UI → "Host" → host player спавнится в 0_0 (координаты `(SCENE_SIZE/2, 3000, SCENE_SIZE/2)` = `(39999.5, 3000, 39999.5)`)
- С `EnableSceneManagement: 1` + `StartHost()` NGO через `NetworkSceneManager` загружает ВСЕ сцены из Build Settings на хосте → scene-placed NetworkObjects спавнятся автоматически

### Что это значит для диагноза

**Первая гипотеза ("NetworkManager отсутствует в 0_0") — отвергнута.** NetworkManager приходит из bootstrap через `DontDestroyOnLoad`, host запускается корректно, player спавнится.

**Реальные гипотезы:**

#### A. `ShipController.IsSpawned == false` в момент RPC

Возможные причины:
- Сцена 0_0 загружается **дважды** на хосте: один раз через `NetworkSceneManager` (при `StartHost`), второй раз через `ClientSceneLoader.AutoLoadInitialSceneCoroutine` (line 351 в `ClientSceneLoader.cs`). Дубликат может сломать spawn tracking.
- Scene-placed `NetworkObject` имеет `InScenePlacedSourceGlobalObjectIdHash` (line 53 в `NetworkManager.prefab`-стиле), но это поле в YAML **не выставлено** (line 53 у NetworkPlayer показывает `InScenePlacedSourceGlobalObjectIdHash: 0`). **Если это поле = 0 у кораблей, NGO не считает их "scene-placed"** и не спавнит автоматически.
- **КРИТИЧНО: требует диагностики в Play Mode.**

#### B. Корабль `NetworkObject` ссылается на null-что-то

- `RequireComponent(typeof(NetworkObject))` гарантирует компонент, но не его корректную инициализацию
- Если у `NetworkObject` не выставлен `GlobalObjectIdHash` (в редакторе должен быть автоматически при первом импорте) — RPC может не находить receiver

#### C. Race condition на host

- При `StartHost` → NGO грузит все сцены → спавнит scene-placed → player спавнится. Если в этот момент пользователь уже нажал F (маловероятно) — race condition. Не наш случай (player спавнится, потом F).

### Что нужно для уверенного диагноза

В Play Mode проверить на одном из ShipController:
- `NetworkObject.IsSpawned` → должно быть `true`
- `NetworkObject.NetworkManager` → не null
- `NetworkObject.GlobalObjectIdHash` → не 0
- `NetworkObject.IsSceneObject` → должно быть `true` (scene-placed)

**Это делает пользователь** (по AGENTS.md пользователь запускает Play Mode вручную).

### Смягчённый план фикса

1. **Добавить `NetworkTransform` (Authority: Server)** на каждый из 3 кораблей. Это **точно** нужно — без него позиция не реплицируется, и даже если RPC работают, клиенты увидят корабль неподвижно.
2. **Добавить guards** в `NetworkPlayer` и `ShipController` — защита от NRE в edge cases (между scene transition, domain reload, и т.п.). Не лечит причину, но убирает спам.
3. **`ScenePlacedObjectSpawner`** (новый файл, ~40 строк) — **только если** после NetworkTransform пользователь увидит, что `IsSpawned == false` на ShipController. Создаём диагностически, не превентивно.
4. **НЕ добавляем `NetworkManager` в сцену 0_0** — он там не нужен при правильном workflow через bootstrap. Если пользователь захочет тестировать напрямую — добавим отдельным шагом.
5. **НЕ трогаем `DefaultNetworkPrefabs.asset`** — для текущей задачи не нужно. Объяснение ниже.

### DefaultNetworkPrefabs — что это и зачем

**Простое объяснение:** `DefaultNetworkPrefabs.asset` — это **реестр префабов**, которые NGO может динамически заспавнить по сети (через `Instantiate(prefab) + NetworkObject.Spawn()`). Это нужно, когда клиент видит, что сервер заспавнил объект из такого префаба, и хочет тоже его отрисовать — NGO ищет префаб в реестре по `GlobalObjectIdHash`.

**Сейчас реестр не присвоен** в `NetworkConfig.Prefabs.NetworkPrefabsLists` (line 53-55 в `NetworkManager.prefab`):
```yaml
Prefabs:
  NetworkPrefabsLists: []  # ПУСТО
```

**Это не критично для текущего NRE**, потому что:
- Наши корабли — scene-placed (стоят в сцене), не динамически спавнятся
- Scene-placed NetworkObjects находят друг друга по `InScenePlacedSourceGlobalObjectIdHash` (не по реестру)
- Player (NetworkPlayer.prefab) — `PlayerPrefab` в NetworkConfig, NGO знает про него автоматически

**Станет критично**, если в будущем:
- Будем динамически спавнить корабли через `Instantiate(shipPrefab).GetComponent<NetworkObject>().Spawn()`
- NGO не найдёт префаб в реестре → warning "prefab not registered" → объект не реплицируется на клиентов

**Решение для будущего** (отдельный тикет):
1. Создать префаб корабля `ShipController.prefab` (вынести из scene-placed)
2. Добавить его в `DefaultNetworkPrefabs.asset`
3. Присвоить ассет в `NetworkConfig.Prefabs.NetworkPrefabsLists` в bootstrap-инстансе NetworkManager

**Сейчас — known issue, в этом фиксе не трогаем.**

---

## 7. Verification (что попросить пользователя запустить после фикса)

### Шаг 1: открыть 0_0 в редакторе (как сейчас), нажать Play
- **Ожидаемо:** host запускается автоматически (или кнопкой/клавишей, см. UI)
- **В Hierarchy** должен появиться `NetworkManager` GameObject
- **В консоли:** 0 errors

### Шаг 2: в Play Mode подойти к одному из 3 кораблей
- **Ожидаемо:** видно 3 корабля в сцене, на своих позициях

### Шаг 3: нажать F
- **Ожидаемо:** игрок "садится" в корабль, камера переключается на корабль (`_myCamera.SetTarget(ship.transform)`)
- **В консоли:** 0 новых ошибок после посадки
- **В Inspector на ShipController:** `NetworkObject.IsSpawned == true`, `NetworkTransform` присутствует

### Шаг 4: нажать W/S/A/D/Q/E
- **Ожидаемо:** корабль реагирует на ввод (хотя бы локально, без других клиентов — NetworkTransform реплицирует)
- **В консоли:** 0 errors

### Шаг 5: нажать F ещё раз
- **Ожидаемо:** игрок выходит из корабля

### Шаг 6: проверить Save (Ctrl+S)
- **Ожидаемо:** сцена сохраняется с добавленным `NetworkManager` и `NetworkTransform` на кораблях

### Шаг 7 (опционально): билд StandaloneWindows64
- File → Build Profiles → Windows → Build
- **Ожидаемо:** билд собирается, в билде 0_0 работает так же

---

## 8. История изменений

| Дата | Что | Кто |
|---|---|---|
| 2026-06-02 | Создан документ. Первичный диагноз: NetworkManager отсутствует при открытии 0_0 напрямую | Mavis |
| 2026-06-02 | Уточнён диагноз по ответам пользователя: NetworkManager приходит из bootstrap, workflow корректный. Реальная причина NRE требует диагностики в Play Mode (`IsSpawned` check). Смягчён план фикса: только NetworkTransform + guards + (опционально) ScenePlacedObjectSpawner | Mavis |
| 2026-06-02 | Применён фикс: добавлен `NetworkTransform` (Authority: Server) на 3 корабля (Ship_Light, Ship_Medium, Ship_Heavy). Сцена `WorldScene_0_0` сохранена | Mavis |
| 2026-06-02 | Добавлены guards в `NetworkPlayer.cs` (SubmitSwitchModeRpc, SendShipInput) и `ShipController.cs` (AddPilot, RemovePilot, SendShipInput) — защита от NRE при scene transition / shutdown. **0 compile errors** | Mavis |
| 2026-06-02 | Обновлены `docs/context/ship.md` (добавлен раздел про NetworkTransform, scene-placed) и `docs/context/network.md` (обновлена карта сцен, добавлена секция про scene-placed + guards) | Mavis |
| 2026-06-02 | **Вторая итерация.** Создан `ScenePlacedObjectSpawner` (серверный спавнер scene-placed NetworkObject через `ClientSceneLoader.OnSceneLoaded`). `InteractableManager.FindNearestShip` переписан на `Collider.bounds.ClosestPoint`. GameObject `ScenePlacedObjectSpawner` добавлен в `BootstrapScene`. **Корневая причина:** `NetworkObject.InScenePlacedSourceGlobalObjectIdHash == 0` у вручную добавленных кораблей → NGO не спавнит автоматически | Mavis |
| 2026-06-02 | **Пользователь подтвердил:** все 3 корабля садятся (F), W/S реагируют. **Работает.** | User |

---

## 11. Финальный статус — ✅ РАБОТАЕТ

**Подтверждено пользователем 2026-06-02 19:16:**
- 0 NRE в консоли
- Light/Medium/Heavy садятся (F)
- W/S/A/D/Q/E реагируют на корабле
- `ScenePlacedObjectSpawner` спавнит все 3 корабля при загрузке 0_0

---

## 12. Что вошло в коммит (для reference)

```
+ Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs  (новый, ~120 строк)
+ docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md                        (новый, ~300 строк)
M Assets/_Project/Scripts/Core/InteractableManager.cs               (FindNearestShip: bounds.ClosestPoint)
M Assets/_Project/Scripts/Player/NetworkPlayer.cs                   (2 guard добавлено)
M Assets/_Project/Scripts/Player/ShipController.cs                  (3 guard добавлено)
M Assets/_Project/Scenes/BootstrapScene.unity                       (GameObject + компонент ScenePlacedObjectSpawner)
M Assets/_Project/Scenes/World/WorldScene_0_0.unity                 (NetworkTransform на 3 корабли)
M docs/context/ship.md                                              (обновлён)
M docs/context/network.md                                           (обновлён)
```

---

## 9. Что было сделано (резюме фикса)

### Изменения в сцене `WorldScene_0_0`

- ✅ `Ship_Light` (instanceID 95858): добавлен `NetworkTransform`, Authority = Server
- ✅ `Ship_Medium` (instanceID 95800): добавлен `NetworkTransform`, Authority = Server
- ✅ `Ship_Heavy` (instanceID 95728): добавлен `NetworkTransform`, Authority = Server
- ✅ Сцена сохранена

### Изменения в сцене `BootstrapScene`

- ✅ GameObject `ScenePlacedObjectSpawner` с компонентом `ProjectC.World.Scene.ScenePlacedObjectSpawner` (instanceID -497056)
- ✅ Сцена сохранена

### Изменения в коде

**Создан новый файл:**

- `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` (~120 строк) — серверный спавнер scene-placed NetworkObject:
  - Singleton-style `MonoBehaviour`, ставится в `BootstrapScene`
  - Подписывается на `ClientSceneLoader.OnSceneLoaded`
  - На сервере (`NetworkManager.Singleton.IsServer`) при загрузке каждой `WorldScene_*` сцены находит все `NetworkObject` с `!IsSpawned` и вызывает `Spawn(destroyWithScene: true)`
  - Также пробегает по уже загруженным сценам в `Start()` (на случай если сцена загружена до нашего `Start`)
  - Делает retry подписки через 1 кадр (если `ClientSceneLoader.Instance == null` в `Start`)

**Изменённые файлы:**

`Assets/_Project/Scripts/Player/NetworkPlayer.cs`:
- Line 234-243: guard перед `SubmitSwitchModeRpc()` — `NetworkManager.Singleton != null && IsSpawned`
- Line 261-269: расширен guard перед `_currentShip.SendShipInput(...)` — добавлена проверка `_currentShip.IsSpawned`

`Assets/_Project/Scripts/Player/ShipController.cs`:
- `SendShipInput(...)`: guard в начале
- `AddPilot(NetworkPlayer pilot)`: guard
- `RemovePilot(ulong clientId)`: guard

`Assets/_Project/Scripts/Core/InteractableManager.cs`:
- `FindNearestShip(...)` — теперь использует `Collider.bounds.ClosestPoint(position)` вместо `Vector3.Distance(position, ship.transform.position)`. Это решает проблему "не садится в Medium/Heavy" для кораблей с увеличенным `localScale`: раньше игрок должен был подойти к **центру transform.position**, теперь — к **ближайшей точке на collider'е** (учитывает визуальный размер)

### Изменения в документации

- ✅ `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md` (этот файл) — создан и обновлён
- ✅ `docs/context/ship.md` — добавлен раздел про NetworkTransform, scene-placed
- ✅ `docs/context/network.md` — обновлена карта сцен, добавлена секция про scene-placed + guards

### Что НЕ сделано и почему

- ❌ `SceneBoundNetworkObject` — для per-scene видимости, нужен когда стриминг мира будет доведён
- ❌ `DefaultNetworkPrefabs.asset` — known issue, отдельный тикет
- ❌ `NetworkManager` в 0_0 — не нужен при workflow через bootstrap
- ❌ Рефактор `ClientSceneLoader` на `NetworkSceneManager` — не в фокусе

---

## 10. Вторая итерация фикса (2026-06-02, вечер)

**Симптомы после первой итерации:**
- NRE ушли ✅
- Light садится визуально, **но не реагирует на W/S**
- Medium/Heavy **не садятся** вовсе

**Корневая причина (найдена через `grep YAML → NetworkObject → InScenePlacedSourceGlobalObjectIdHash: 0`):**

У всех 3 тестовых ShipController (вручную добавленных пользователем) `NetworkObject.InScenePlacedSourceGlobalObjectIdHash == 0`. Это специальное поле NGO 2.x — когда оно `0`, NGO через `NetworkSceneManager` **не считает объект scene-placed** и **не спавнит его автоматически** при `StartHost()`. Используется только `GlobalObjectIdHash` для динамического спавна.

**Цепочка событий до фикса:**

1. `StartHost()` → NGO через `NetworkSceneManager` грузит 24 сцены, но `NetworkObject` с `InScenePlacedSourceGlobalObjectIdHash == 0` **остаются с `IsSpawned == false`**
2. Игрок нажимает F → `SubmitSwitchModeRpc` body на хосте
3. Body находит Light (через `FindNearestShip` с `boardDistance=5`), `_currentShip = lightShip`, `_inShip=true`, контроллер отключается → **визуально "сел"**
4. `_currentShip.AddPilot(this)` → мой guard `if (!IsSpawned) return;` → **early return**, `_pilots` остаётся пустым
5. NetworkPlayer.Update → `_inShip=true` → читает W/S → `SendShipInput` → guard `if (!_currentShip.IsSpawned) return;` → **early return**
6. Server `FixedUpdate`: `if (_pilots.Count == 0) return;` (line 355) → ничего не делает
7. Корабль **не движется**

**Для Medium/Heavy** — дополнительно: `FindNearestShip` использовал `Vector3.Distance(position, ship.transform.position)` с `boardDistance=5f`. Игрок подходит к **визуальному краю** увеличенного корабля, distance до центра > 5 → не находит.

**Что сделано во второй итерации:**

- ✅ Создан `ScenePlacedObjectSpawner` (см. §9) — на сервере при загрузке сцены вызывает `Spawn(destroyWithScene: true)` для всех `NetworkObject` с `!IsSpawned`
- ✅ `FindNearestShip` переписан на `Collider.bounds.ClosestPoint` — теперь работает для кораблей любого размера
- ✅ BootstrapScene сохранена с новым GameObject `ScenePlacedObjectSpawner`

**Ожидаемое поведение после второй итерации:**

1. На хосте при `StartHost()` → `ClientSceneLoader` грузит 0_0 (через `AutoLoadInitialSceneCoroutine`) → `OnSceneLoaded(0_0)` → `ScenePlacedObjectSpawner.HandleSceneLoaded` → находит 3 `NetworkObject` → вызывает `Spawn(destroyWithScene: true)` для каждого → `IsSpawned == true`
2. Игрок нажимает F → `_nearestShip = FindNearestShip` находит ближайший корабль (Light/Medium/Heavy) по `bounds.ClosestPoint` → `_currentShip = nearestShip` → `_currentShip.AddPilot(this)` → guard пропускает (`IsSpawned == true`) → `AddPilotRpc` отправляется → `_pilots.Add(clientId)` на сервере
3. NetworkPlayer.Update → `SendShipInput` → guard пропускает → `SubmitShipInputRpc` отправляется → server `FixedUpdate` видит `_pilots.Count > 0` → применяет силы
4. NetworkTransform реплицирует позицию → клиент видит движение

**Если что-то ещё не работает после второй итерации:**

- Открой Inspector на одном из ShipController в Play Mode → проверь `NetworkObject.IsSpawned` (должно быть `true` после ScenePlacedObjectSpawner)
- Если `false` — `ScenePlacedObjectSpawner` не подхватил. Смотри Console на наличие логов `[ScenePlacedObjectSpawner] Scene (0,0): spawned=N` (должно быть `N=3`)
- Если `true`, но управление не работает — проверь что `_pilots` не пуст (см. `ShipController._pilots` через Inspector в Debug mode)

---

**Следующий шаг:** пользователь тестирует заново через bootstrap → Host → проверяет:
1. В Console: `[ScenePlacedObjectSpawner] Scene (0,0): spawned=3`
2. На ShipController.IsSpawned: `true`
3. Садится в любой корабль (Light/Medium/Heavy) — W/S реагирует
