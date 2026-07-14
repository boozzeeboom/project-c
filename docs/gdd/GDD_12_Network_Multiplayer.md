# GDD-12: Network & Multiplayer — Project C: The Clouds

**Версия:** 2.0 | **Дата:** 14 июля 2026 г. | **Статус:** 🟢 Реализовано (Host + Client)
**Автор:** Малков Леонид Андреевич

---

## 1. Overview

Сетевая система Project C: The Clouds построена на **Netcode for GameObjects (NGO) 2.x** с **авторитарным сервером**. Поддерживаются режимы Host и Client. Выделенный Dedicated Server (.NET 8) **НЕ реализован** — весь код клиент-сервер работает внутри Unity.

### Ключевые особенности
- **Авторитарный сервер** — сервер единственный источник истины
- **Host + Client** — хост является одновременно сервером и игроком
- **NGO 2.x `[Rpc(SendTo....)]` API** — современный RPC-синтаксис
- **Server-authoritative** — физика, движение, инвентарь валидируются сервером
- **Reconnect** — авто-реконнект 5 попыток, сохранение данных
- **Player Count** — счётчик в реальном времени

---

## 2. Network Architecture

### Сетевая модель

```
┌─────────────────────────────────────────────────────┐
│                    HOST                             │
│  ┌─────────────────┐    ┌──────────────────────┐   │
│  │  СЕРВЕР (автор.) │    │  Клиент 0 (хост)     │   │
│  │  - Валидация     │    │  - Ввод              │   │
│  │  - Репликация    │    │  - Рендер            │   │
│  │  - Физика        │    │  - UI                │   │
│  └─────────────────┘    └──────────────────────┘   │
└─────────────────────────────────────────────────────┘
         ▲                          ▲
         │  UnityTransport (UDP)    │
         │  Порт 7777               │
    ┌────┴──────────────────────────┴────┐
    │                                    │
┌───▼───────────┐              ┌────────▼──────────┐
│  Клиент 1     │   ...        │  Клиент N         │
│  - Ввод       │              │  - Ввод           │
│  - Рендер     │              │  - Рендер         │
│  - UI         │              │  - UI             │
└───────────────┘              └───────────────────┘
```

**Dedicated Server:** ❌ Не реализован. Серверный код выполняется в Unity-клиенте хоста.

### Компоненты

| Компонент | Описание | Файл |
|-----------|----------|------|
| NetworkManagerController | Обёртка NGO, Host/Client управление | `Core/NetworkManagerController.cs` |
| NetworkPlayer | Игрок: движение, камера, RPC-взаимодействия | `Player/NetworkPlayer.cs` |
| ShipController | Корабль: физика, кооп-пилотирование, RPC | `Player/ShipController.cs` |
| NetworkPlayerSpawner | Спавн/деспавн игроков | `Network/NetworkPlayerSpawner.cs` |
| ScenePlacedObjectSpawner | Спавн scene-placed NetworkObject (InScenePlacedSourceGlobalObjectIdHash == 0) | `World/Scene/ScenePlacedObjectSpawner.cs` |
| NetworkUI | UI сети: Disconnect, Reconnect, Player Count | `UI/NetworkUI.cs` |

### Технические параметры

| Параметр | Значение |
|----------|----------|
| Транспорт | UnityTransport (UDP) |
| Порт | 7777 |
| Протокол | NGO 2.x (Netcode for GameObjects) |
| Макс. игроков | Зависит от конфигурации (по умолч. 4) |
| Tick rate | Зависит от FixedUpdate (физика) |

---

## 3. Connection Flow

### Подключение Host

```
1. Игрок нажимает "Start Server" (или автоматически)
2. NetworkManagerController.StartHost()
3. NetworkManager.StartHost()
4. Сервер запущен на порту 7777
5. Клиент 0 (хост) подключён локально
6. NetworkPlayerSpawner спавнит NetworkPlayer префаб
7. OnClientConnectedCallback → обновление UI
```

### Подключение Client

```
1. Игрок вводит IP:Port (по умолчанию 127.0.0.1:7777)
2. NetworkManagerController.StartClient()
3. UnityTransport подключается к серверу
4. Сервер спавнит NetworkPlayer для клиента
5. Клиент получает управление своим игроком
6. OnClientConnectedCallback → обновление UI
```

### Disconnect

```
1. Игрок нажимает "Disconnect" (или Escape → Disconnect)
2. NetworkManagerController.Shutdown()
3. OnClientDisconnectCallback → UI обновление
4. Возврат в меню подключения
```

### Reconnect

```
1. OnTransportFailure() → обрыв соединения
2. Авто-реконнект (до 5 попыток)
3. При успехе: восстановление состояния
4. При провале: Reconnect кнопка (ручная)
5. IP:Port сохраняется для быстрого подключения
```

**⚠️ Статус Reconnect:** Механизм авто-реконнекта реализован (до 5 попыток с экспоненциальной задержкой), но полное восстановление состояния игрока (инвентарь, позиция) требует дополнительной серверной синхронизации.

---

## 4. RPC System

Используется синтаксис NGO 2.x: `[Rpc(SendTo.Target)]`.

### ServerRpc (клиент → сервер)

| RPC | Описание | Параметры | Исходный код |
|-----|----------|-----------|-------------|
| `SubmitSwitchModeRpc()` | Переключение режима (Walking/Flying) | `RpcParams rpcParams = default` | `Player/NetworkPlayer.cs` |
| `SubmitJumpRpc()` | Прыжок | `RpcParams rpcParams = default` | `Player/NetworkPlayer.cs` |
| `TeleportServerRpc()` | Телепорт (админ/отладка) | `Vector3 position` | `Player/NetworkPlayer.cs` |
| `CollectNpcLootServerRpc()` | Запрос подбора лута NPC | `ulong lootNetId, RpcParams` | `Player/NetworkPlayer.cs` |
| `ToggleEngineServerRpc()` | Вкл/выкл двигатель корабля | — | `Player/NetworkPlayer.cs` |
| `SubmitShipInputRpc()` | Ввод корабля (скорость, поворот, тангаж, буст) | `float thrust, yaw, pitch, vertical, bool boost` | `Player/ShipController.cs` |

### ClientRpc (сервер → клиент)

| RPC | Описание | Параметры | Исходный код |
|-----|----------|-----------|-------------|
| `HidePickupRpc()` | Скрыть предмет | `Vector3 targetPos, RpcParams` | `Player/NetworkPlayer.cs` |
| `OpenChestRpc()` | Открыть сундук | `Vector3 targetPos, RpcParams` | `Player/NetworkPlayer.cs` |
| `ApplyServerPositionRpc()` | Применить серверную позицию (коррекция) | `Vector3 serverPosition, RpcParams` | `Player/NetworkPlayer.cs` |
| `TeleportAllClientRpc()` | Телепорт всех игроков | `Vector3 position, RpcParams` | `Player/NetworkPlayer.cs` |
| `ReceiveMarketSnapshotTargetRpc()` | Получить рыночные данные | `MarketSnapshotDto snapshot, RpcParams` | `Player/NetworkPlayer.cs` |

### SendTo Target

| Target | Описание | Пример |
|--------|----------|--------|
| `SendTo.Everyone` | Все клиенты + сервер | `HidePickupRpc`, `OpenChestRpc`, `SubmitSwitchModeRpc` |
| `SendTo.Server` | Только сервер | `CollectNpcLootServerRpc`, `TeleportServerRpc` |
| `SendTo.Owner` | Только владелец NetworkObject | `ApplyServerPositionRpc`, `ReceiveMarketSnapshotTargetRpc` |
| `SendTo.SpecifiedClients` | Выбранные клиенты | [🔴 Запланировано] |

### ⚠️ Известные проблемы RPC

| Проблема | Описание | Приоритет |
|----------|----------|-----------|
| **Boost в ShipInputRPC** | Параметр `boost` добавлен, требуется валидация на сервере | 🟡 Средне |
| **RPC-спам при быстром вводе** | NPUT-логирование может засорять консоль | 🟢 Низкий |

---

## 5. Player Spawning

### Спавн игрока

| Этап | Описание |
|------|----------|
| 1 | NetworkManager.StartHost/StartClient |
| 2 | NetworkPlayerSpawner спавнит NetworkPlayer префаб |
| 3 | `OnNetworkSpawn()` → инициализация |
| 4 | Создание/привязка камеры |
| 5 | Режим: Walking (пеший) |

### NetworkPlayer префаб (Registered NetworkPrefab)

| Компонент | Описание |
|-----------|----------|
| NetworkObject | NGO компонент |
| NetworkTransform | Синхронизация позиции (ServerAuthority) |
| NetworkPlayer (~3200+ LOC, 116KB) | Логика игрока: RPC, движение, взаимодействие, состояние |
| CharacterController | Физика (пеший режим) |
| PlayerController | Ввод/управление |
| PlayerStateMachine | Состояния (Walking/Flying/InShip) |

### Регистрация префабов

| Файл | Описание |
|------|----------|
| `DefaultNetworkPrefabs.asset` | Список разрешённых NetworkPrefab |
| NetworkManager | Ссылка на NetworkPlayer префаб |

### ScenePlacedObjectSpawner

Для NetworkObject, размещённых непосредственно в сцене (с `InScenePlacedSourceGlobalObjectIdHash == 0`), используется `ScenePlacedObjectSpawner` (`World/Scene/ScenePlacedObjectSpawner.cs`). Он управляет спавном/деспавном объектов, которые существуют в сцене на момент загрузки, но требуют корректной сетевой привязки.

---

## 6. Ship Sync

### Синхронизация корабля

```
Клиент:                          Сервер:
  │                                │
  ├── SubmitShipInputRpc(input) ──▶│
  │                                ├── Усреднение: Σinput / N
  │                                ├── Применение физики
  │                                ├── Репликация позиции
  │◀── NetworkTransform sync ──────┤
  ├── Интерполяция                 │
  ├── Рендер                       │
```

### Кооп-пилотирование

| Параметр | Описание |
|----------|----------|
| Усреднение | `finalInput = Sum(pilotInput[i]) / pilotCount` |
| Серверная авторитетность | Только сервер считает ввод |
| Частота | Каждый FixedUpdate |
| **SubmitShipInputRpc** | Реализован в `ShipController.cs` (строка 1470) — принимает `thrust, yaw, pitch, vertical, boost` |
| [🔴 Запланировано] Приоритет капитана | Вес 1.5x |

---

## 7. Inventory Sync

### Текущая синхронизация

| Действие | Синхронизация | Метод |
|----------|--------------|-------|
| Подбор предмета | ✅ Все клиенты | `HidePickupRpc` (SendTo.Everyone) |
| Открытие сундука | ✅ Все клиенты | `OpenChestRpc` (SendTo.Everyone) |
| Содержимое инвентаря | ✅ Через `InventoryClientState/InventoryServer` | NetworkVariable + RPC (подсистема инвентаря v2) |
| Подбор лута NPC | ✅ Сервер-авторитарный | `CollectNpcLootServerRpc` |
| Сохранение при дисконнекте | ⚠️ Частично (PlayerPrefs / серверный Snapshot) | — |

**Примечание:** Инвентарь синхронизируется через клиент-серверную подсистему `InventoryClientState/InventoryServer`. Полная серверная валидация реализована.

### [🔴 Запланировано] Улучшения синхронизации

| Метод | Описание |
|-------|----------|
| Торговля между игроками | UI обмена между игроками |
| Античит-валидация | Проверка целостности инвентаря на сервере |

---

## 8. Error Handling

### Обработка обрывов

| Событие | Действие | Callback |
|---------|----------|----------|
| Обрыв транспорта | Авто-реконнект 5 попыток | OnTransportFailure |
| Клиент отключился | Удаление NetworkPlayer | OnClientDisconnectCallback |
| Сервер упал | Возврат в меню | OnServerStopped |

### Логирование

| Событие | Лог |
|---------|-----|
| Подключение | `[Network] Client {id} connected` |
| Отключение | `[Network] Client {id} disconnected` |
| Обрыв | `[Network] Transport failure, reconnecting...` |
| Реконнект | `[Network] Reconnected successfully` |
| Player Count | `[Network] Players: {count}` |

---

## 9. [УДАЛЕНО] Dedicated Server

Отдельный выделенный сервер **НЕ реализован**. См. раздел 11 (Future Architecture) для планов.

---

## 10. Known Issues & Limitations

| Проблема | Описание | Приоритет | Статус |
|----------|----------|-----------|--------|
| **Boost в ShipInputRPC** | Параметр `boost` требует валидации | 🟡 | Не исправлено |
| **Reconnect неполный** | Восстановление состояния после реконнекта не полностью реализовано | 🟡 | Улучшить |
| **Client-side Prediction** | Базовое предсказание, не полноценное | 🟢 | Улучшить |
| **Нет Matchmaking** | Нет системы лобби/подбора | 🔴 | Будущее |
| **Нет шардинга** | Один сервер = один мир | 🔴 | Будущее |
| **Нет выделенного сервера** | .NET 8 сервер не реализован | 🔴 | Будущее |

---

## 11. Future Architecture

### [🔴 Запланировано] Этап 5+

| Компонент | Описание | Статус |
|-----------|----------|--------|
| **Сервер .NET 8** | Отдельный серверный билд | ❌ Не реализовано |
| **Master-сервер** | Matchmaking, лобби, список серверов | ❌ Не реализовано |
| **Шардинг мира** | Несколько зон, автоматическое масштабирование | ❌ Не реализовано |
| **JWT аутентификация** | Аккаунты, сессии | ❌ Не реализовано |
| **PostgreSQL** | База данных: аккаунты, прогресс, инвентарь | ❌ Не реализовано |
| **Redis** | Кэширование, сессии | ❌ Не реализовано |
| **Kubernetes** | Оркестрация серверов | ❌ Не реализовано |
| **Голосовой чат (Vivox)** | Voice communication | ❌ Не реализовано |
| **Docker-контейнеризация** | Контейнеризация сервера | ❌ Не реализовано |

Все перечисленные компоненты являются **архитектурными планами** и не имеют реализации в коде.

---

## 12. Formulas

| Формула | Описание |
|---------|----------|
| `cooperativeInput = Sum(pilotInput[i]) / pilotCount` | Усреднение кооп-ввода |
| `interpolation = Vector3.Lerp(prevPos, targetPos, speed * dt)` | Интерполяция позиции |
| `correction = targetPos - predictedPos` | Коррекция при рассинхроне |
| `reconnectDelay = baseDelay * attempt` | Задержка между попытками |

---

## 13. Tuning Knobs

| Параметр | Мин | Макс | Текущее | Влияние |
|----------|-----|------|---------|---------|
| `port` | 1024 | 65535 | 7777 | Сетевой порт |
| `reconnectAttempts` | 1 | 20 | 5 | Попытки реконнекта |
| `reconnectBaseDelay` | 0.5 | 10 | 2.0 | Базовая задержка (сек) |
| `interpolationSpeed` | 5 | 30 | 15 | Скорость интерполяции |

---

## 14. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | Host запускает сервер | Нажать "Start Server" | ✅ |
| 2 | Client подключается к Host | Ввести IP:Port, Connect | ✅ |
| 3 | Движение синхронизируется | 2 игрока, видеть друг друга | ✅ |
| 4 | Корабли синхронизируются | Кооп-пилотирование | ✅ |
| 5 | Предметы исчезают у всех | Подбор одним игроком | ✅ |
| 6 | Сундуки открываются у всех | Открытие одним игроком | ✅ |
| 7 | Disconnect работает | Кнопка Disconnect | ✅ |
| 8 | Reconnect работает | Обрыв → авто-подключение | ✅ |
| 9 | Player Count обновляется | Подключить/отключить клиента | ✅ |
| 10 | RPC `SubmitShipInputRpc` с boost | Проверить передачу boost-параметра | ✅ |
| 11 | ScenePlacedObjectSpawner | Спавн объектов с InScenePlacedSourceGlobalObjectIdHash == 0 | ✅ |

### [🔴] Не реализовано

| # | Критерий | Причина |
|---|----------|---------|
| — | Dedicated Server запускается | Нет отдельного сервера |
| — | Полная синхронизация инвентаря | Частично реализована (см. Known Issues) |
| — | Лобби/Matchmaking | Не реализовано |
| — | Boost целостность | Параметр добавлен, требуется доп. валидация |

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [GDD_12_1_Scene_World_Streaming.md](GDD_12_1_Scene_World_Streaming.md) | [GDD_13_UI_UX_System.md](GDD_13_UI_UX_System.md)
