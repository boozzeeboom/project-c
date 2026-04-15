# Сетевая архитектура Project C — Полная документация

**Версия:** `v0.0.12-stage2-complete` | **Дата:** 5 апреля 2026 г.
**Ветка:** `qwen-gamestudio-agent-dev` | **Архитектура:** Авторитарный сервер (Host = Server + Client 0)

---

## ⚠️ Известные проблемы и задачи

### 🟡 Среднеприоритетные

| Проблема | Описание |
|----------|----------|
| Boost (Shift) для кораблей | Сервер не знает о бусте клиента — параметр `boost` не передаётся в RPC |
| Инвентарь локальный | Сохраняется в PlayerPrefs, но НЕ синхронизируется между игроками |
| WorldGenerationSettings.asset | Legacy предупреждение при запуске (не критично) |

### 🔴 Отложено (Этап 5+)

| Задача | Описание |
|--------|----------|
| Отдельный серверный билд (.NET 8) | Master-сервер для матчмейкинга и лобби |
| Система лобби/комнат | Группировка игроков до 4 чел, приглашения |
| Полная серверная валидация инвентаря | Anti-cheat, серверный авторитет для предметов |

### ✅ Исправлено

| Проблема | Статус | Дата |
|----------|--------|------|
| ~~Disconnect кнопка в левом углу~~ | ✅ Пофиксено — кнопка по центру экрана | — |
| ~~Предметы не исчезают у других игроков~~ | ✅ HidePickupRpc (SendTo.Everyone) | — |
| ~~Сундуки не работали после изменений~~ | ✅ Возвращён старый рабочий подбор | — |
| ~~Инвентарь терялся при реконнекте~~ | ✅ Сохранение/загрузка через PlayerPrefs | — |
| ~~Host не запускался при нажатии кнопки~~ | ✅ Исправлен вызов корутины StartHostCoroutine() | 15.04.2026 |
| ~~UnityTransport не привязан к NetworkConfig~~ | ✅ Автоматическая привязка в Awake() | 15.04.2026 |

---

## 🏗️ Общая архитектура

```
┌─────────────────────────────────────────────────────┐
│              HOST (Unity Editor / билд)              │
│  ┌──────────────┐  ┌──────────────┐                 │
│  │    СЕРВЕР    │  │   Клиент 0   │                 │
│  │  геймплея    │  │   (игрок)    │                 │
│  │  (авторитет) │  │              │                 │
│  └──────────────┘  └──────────────┘                 │
│        ▲                 ▲                          │
│        │                 │                          │
│        └────────┬────────┘                          │
│                 │ UnityTransport (UDP)               │
│          ┌──────┴──────┐                             │
│          │             │                             │
│    ┌─────┴─────┐ ┌──────────┐                      │
│    │  Клиент 1 │ │  Клиент N │  (билды или Unity)   │
│    │           │ │           │                      │
│    └───────────┘ └───────────┘                      │
└─────────────────────────────────────────────────────┘
```

### Ключевой принцип

> **Сервер — единственный источник истины.**  
> Клиенты **никогда** не меняют игровое состояние напрямую.  
> Клиенты шлют **ввод** → сервер **считает** → **реплицирует** результат.

---

## 📦 Компоненты сети

### 1. NetworkManager

| Файл | Назначение |
|------|-----------|
| `NetworkManagerController.cs` | Обёртка над NGO NetworkManager |
| `NetworkManager.prefab` | Префаб с NetworkManager + UnityTransport |
| `DefaultNetworkPrefabs.asset` | Список зарегистрированных префабов |

**Настройки транспорта:**
- Protocol: UDP
- Port: `7777`
- ServerListenAddress: `0.0.0.0`
- HeartbeatTimeout: `500ms`
- DisconnectTimeout: `30000ms`

**Запуск режимов:**
```csharp
networkManager.StartHost();   // Сервер + клиент 0 (тестирование)
networkManager.StartServer(); // Только сервер (Dedicated, кнопка в UI + -server build arg)
networkManager.StartClient(); // Только клиент
```

**Dedicated Server:**
- Кнопка "Start Server" в NetworkUI
- Автозапуск при аргументе `-server` или `-dedicatedserver`
- Headless режим: `-batchmode -nographics -server`
- См. [`DEDICATED_SERVER.md`](DEDICATED_SERVER.md)

---

### 2. NetworkPlayer (игрок)

**Файл:** `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

**Что делает:**
- Спавнится автоматически для каждого подключившегося клиента (PlayerPrefab в NetworkManager)
- Управляет движением, камерой, инвентарём, кораблём для **локального** игрока
- Удалённые игроки — только видны (NetworkTransform синхронизирует)

**Client-side Prediction:**
- Клиент (Owner) **сразу** применяет движение локально — без задержки
- Сервер периодически проверяет позицию и при рассинхронизации > `positionCorrectionThreshold` шлёт `ApplyServerPositionRpc`
- Клиент плавно корректирует позицию через Lerp (без рывков)
- Порог и скорость коррекции настраиваются в Inspector

**Архитектура ввода:**
```
Клиент: Keyboard.current.wKey.isPressed → ProcessMovement() локально
        → NetworkTransform (OwnerAuthority) реплицирует позицию

Клиент: E → TryPickup() → HidePickupRpc() (SendTo.Everyone)
        → все клиенты скрывают предмет
```

**Компоненты на префабе:**
| Компонент | Назначение |
|-----------|-----------|
| `NetworkObject` | Сетевая идентификация |
| `NetworkTransform` | Авторитет: **Owner** (владелец двигает сам) |
| `CharacterController` | Физика пешего режима |
| `NetworkPlayer.cs` | Основной скрипт |

**Спавн при подключении:**
```csharp
// В OnNetworkSpawn():
if (IsOwner) {
    SpawnCamera();      // Персональная камера
    SpawnInventory();   // Inventory + InventoryUI
    ApplyWalkingState();
} else {
    _controller.enabled = false; // Удалённый — не управляется
}
```

---

### 3. ShipController (корабль)

**Файл:** `Assets/_Project/Scripts/Player/ShipController.cs`

**Архитектура:**
```
Клиент (в корабле):
  WASD + Q/E + мышь → SendShipInput(thrust, yaw, pitch, vertical)
    → SubmitShipInputRpc() → ServerRpc

Сервер (FixedUpdate):
  Суммирует ввод всех пилотов → усредняет → применяет к Rigidbody
  → NetworkTransform (ServerAuthority) реплицирует позицию всем
```

**Кооп-пилотирование:**
- Несколько игроков могут сесть в один корабль
- Ввод от всех пилотов **усредняется** на сервере
- При выходе одного пилота — остальные продолжают управлять

**Компоненты на каждом корабле:**
| Компонент | Назначение |
|-----------|-----------|
| `NetworkObject` | Сетевая идентификация |
| `NetworkTransform` | Авторитет: **Server** (сервер двигает) |
| `Rigidbody` | Физика полёта |
| `ShipController.cs` | Сетевой контроллер |

**Важно:** Все корабли на сцене должны иметь `NetworkTransform` с `AuthorityMode: Server`.

---

### 4. Переключение режимов (F — посадка/выход)

**Архитектура:**
```
Клиент нажимает F:
  SubmitSwitchModeRpc() → SendTo.Everyone

Все клиенты (включая сервер):
  if (_inShip):
    // Выход: телепорт на палубу, показать игрока, снять пилота
  else:
    // Посадка: найти ближайший корабль (<5м), скрыть игрока, добавить пилота
```

**RPC тип:** `SendTo.Everyone` — потому что каждый клиент меняет своё состояние самостоятельно (сервер не двигает игроков, только корабль).

---

### 5. Инвентарь

**Архитектура:**
- Каждый игрок имеет **свой** Inventory (спавнится как дочерний объект NetworkPlayer)
- Инвентари **НЕ синхронизируются** между игроками (каждый видит свои предметы)
- Предметы в мире исчезают синхронно через RPC
- **Сохранение:** при Disconnect инвентарь сохраняется в PlayerPrefs, при Reconnect — восстанавливается

**Подбор предмета (E):**
```
Клиент:
  1. Локально: _inventory.AddItem(itemData)
  2. HidePickupRpc(pos) → SendTo.Everyone → все скрывают предмет

Сундук:
  1. Локально: _inventory.AddMultipleItems(loot) + TriggerSectorFlash()
  2. OpenChestRpc(pos) → SendTo.Everyone → все открывают/скрывают сундук

Реконнект:
  1. Disconnect → _inventory.SaveToPrefs() → PlayerPrefs
  2. Reconnect → OnNetworkSpawn → _inventory.LoadFromPrefs() → предметы восстановлены
```

**Компоненты:**
| Файл | Назначение |
|------|-----------|
| `Inventory.cs` | Хранение предметов по типам + SaveToPrefs/LoadFromPrefs |
| `InventoryUI.cs` | Круговое колесо (GL-рендер), привязан к Inventory |
| `PickupItem.cs` | Подбираемый предмет в мире (триггер, покачивание) |
| `ChestContainer.cs` | Сундук с LootTable, анимация открытия |
| `LootTable.cs` | ScriptableObject: таблица добычи (шансы, min/max, guaranteed) |
| `ItemDatabaseInitializer.cs` | Авто-регистрация всех предметов из Resources и сцены |

---

### 6. Камера

**Архитектура:**
- Каждый игрок спавнит **персональную** ThirdPersonCamera (копия префаба)
- Камера — дочерний объект NetworkPlayer
- При посадке в корабль: камера переключается на корабль (`SetTarget(ship.transform)`)
- При выходе: камера возвращается к игроку

**Файлы:**
| Файл | Назначение |
|------|-----------|
| `ThirdPersonCamera.cs` | Орбитальная камера от третьего лица |
| `ThirdPersonCamera.prefab` | Префаб камеры (спавнится на каждого игрока) |

---

## 🔌 RPC — обзор всех вызовов

| RPC | Тип | От кого → кому | Что делает |
|-----|-----|----------------|------------|
| `SubmitSwitchModeRpc()` | SendTo.Everyone | Клиент → все | Посадка/выход из корабля |
| `HidePickupRpc(pos)` | SendTo.Everyone | Клиент → все | Скрыть подобранный предмет |
| `OpenChestRpc(pos)` | SendTo.Everyone | Клиент → все | Открыть/скрыть сундук |
| `SubmitShipInputRpc(...)` | SendTo.Server | Клиент → сервер | Ввод корабля (60 раз/сек) |
| `AddPilotRpc(clientId)` | SendTo.Everyone | Клиент → все | Добавить пилота в корабль |
| `RemovePilotRpc(clientId)` | SendTo.Everyone | Клиент → все | Снять пилота |

---

## 🔌 События подключения/отключения

### NetworkManagerController

| Событие | Когда срабатывает | Что делает |
|---------|-------------------|------------|
| `OnClientConnectedCallback` | Клиент подключился | Логирует, обновляет UI, обновляет счётчик игроков |
| `OnClientDisconnectCallback` | Клиент отключился | Логирует, обновляет UI, обновляет счётчик игроков |
| `OnServerStarted` | Сервер запущен | Логирует |
| `OnTransportFailure` | Ошибка транспорта | Логирует ошибку, запускает авто-реконнект |

### Reconnect система

**Авто-реконнект:**
- При `OnTransportFailure` → автоматические попытки (до 5, с задержкой 3с)
- Если все попытки провалились → показывается кнопка Reconnect

**Ручной реконнект:**
- Кнопка "Reconnect" появляется после Disconnect или провала авто-реконнекта
- Сохраняет последний IP:Port → Shutdown → ConnectToServer

**Сохранение при отключении:**
- Disconnect → `Inventory.SaveToPrefs()` → PlayerPrefs
- OnNetworkDespawn → `Inventory.SaveToPrefs()`
- OnNetworkSpawn (Owner) → `Inventory.LoadFromPrefs()`

### NetworkUI — Disconnect и Reconnect кнопки

**Архитектура:**
- Disconnect кнопка создаётся **программно** в `CreateDisconnectButton()`
- Reconnect кнопка — назначается через Inspector, скрыта по умолчанию
- Показывается при подключении/сбое, скрывается при отключении
- **Escape** — toggle видимости Disconnect (экстренный выход)
- **Позиционирование:** по центру экрана

**Player Count:**
- `playerCountText` обновляется при connect/disconnect/host start
- Host учитывается как +1 к ConnectedClients

**Связанные файлы:**
- `NetworkManagerController.cs` — события подключения, реконнект, Dedicated Server
- `NetworkUI.cs` — Disconnect/Reconnect кнопки, player count, статус

---

## 📐 Зарегистрированные префабы

**DefaultNetworkPrefabs.asset:**
| Префаб | Тип |
|--------|-----|
| `NetworkPlayer` | Игрок (PlayerPrefab в NetworkManager) |

**Корабли** (стоят на сцене с самого начала):
- Каждый корабль имеет `NetworkObject` + `NetworkTransform(Server)`
- Не нужны в DefaultNetworkPrefabs (не спавнятся динамически)

---

## 🎮 Управление (итоговая карта)

| Клавиша | Пеший режим | Режим корабля |
|---------|-------------|---------------|
| **W/S** | Движение вперёд/назад | Тяга вперёд/назад |
| **A/D** | Поворот | Рыскание (поворот) |
| **Space** | Прыжок | — |
| **Left Shift** | Бег (x2 скорость) | Ускорение (x2 тяга) |
| **Q/E** | — | Лифт вниз/вверх |
| **Мышь** | Вращение камеры | Тангаж (нос вверх/вниз) |
| **F** | Сесть в корабль | Выйти из корабля |
| **E** | Подобрать предмет / сундук | — (резерв) |
| **Tab** | Круговой инвентарь | — |
| **F1** | Подсказки UI | Подсказки UI |

---

## ⚠️ Известные ограничения

| Ограничение | Описание | Когда исправить |
|-------------|----------|-----------------|
| Инвентарь не синхронизируется | Каждый игрок видит только свои предметы (локальный + PlayerPrefs) | Этап 3 (RPG система, серверная валидация) |
| Корабли на сцене (не префабы) | NetworkTransform добавляется вручную в Unity | Префабы кораблей позже |
| Boost (Shift) не передаётся в RPC | Сервер не знает о бусте клиента | Добавить в SubmitShipInputRpc |

### ✅ Реализовано

| Фича | Описание |
|------|----------|
| Client-side Prediction | Клиент сразу двигает себя (OwnerAuthority), сервер корректирует при рассинхронизации |
| Dedicated Server | Кнопка в UI + автозапуск через `-server` аргумент |
| Reconnect система | Авто-реконнект (5 попыток) + ручная кнопка + сохранение инвентаря |
| Синхронизация подбора | HidePickupRpc + OpenChestRpc (SendTo.Everyone) — предметы исчезают у всех |
| Player Count | Счётчик игроков обновляется в реальном времени |
| ItemDatabase | Авто-регистрация предметов из Resources, PickupItem, ChestContainer |

---

## 📦 Дополнительные сетевые фичи (Этап 5+)

| Фича | Описание | Приоритет |
|------|----------|-----------|
| Отдельный серверный билд | Headless Unity или .NET 8 сервер, 24/7 | 🔴 Высокий |
| Система лобби/комнат | Мастер-сервер, создание комнат, матчмейкинг | 🔴 Высокий |
| Серверная валидация инвентаря | Anti-cheat, серверный авторитет для предметов | 🟡 Средний |
| Полная синхронизация инвентаря | NetworkVariable/NetworkList для репликации | 🟡 Средний |
| Улучшенная интерполяция | Кастомный буфер истории позиций, экстраполяция | 🟢 Низкий |

---

## 🔗 Связанные документы

- [`MMO_Development_Plan.md`](MMO_Development_Plan.md) — общий план
- [`STEP_BY_STEP_DEVELOPMENT.md`](STEP_BY_STEP_DEVELOPMENT.md) — журнал шагов
- [`CONTROLS.md`](CONTROLS.md) — карта клавиш
- [`DEDICATED_SERVER.md`](DEDICATED_SERVER.md) — запуск выделенного сервера
- [`SHIP_SYSTEM_DOCUMENTATION.md`](SHIP_SYSTEM_DOCUMENTATION.md) — система кораблей

---

**Последнее обновление:** 5 апреля 2026 г.
**Версия:** `v0.0.12-stage2-complete`
