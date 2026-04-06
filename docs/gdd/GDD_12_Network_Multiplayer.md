# GDD-12: Network & Multiplayer — Project C: The Clouds

**Версия:** 1.0 | **Дата:** 6 апреля 2026 г. | **Статус:** ✅ Документировано
**Автор:** Qwen Code (Game Studio: @network-programmer + @lead-programmer)

---

## 1. Overview

Сетевая система Project C: The Clouds построена на **Netcode for GameObjects (NGO)** с **авторитарным сервером**. Поддерживаются режимы Host, Client и Dedicated Server.

### Ключевые особенности
- **Авторитарный сервер** — сервер единственный источник истины
- **Host + Client** — синхронизация движения, камеры, инвентаря, кораблей
- **Dedicated Server** — headless режим, `-server` build arg
- **Reconnect** — авто-реконнект 5 попыток, сохранение инвентаря
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

DEDICATED SERVER (отдельный процесс):
  -server build arg → headless режим (без рендера)
  -batchmode -nographics → без GUI
```

### Компоненты

| Компонент | Описание | Файл |
|-----------|----------|------|
| NetworkManagerController | Обёртка NGO, Host/Client/Server | NetworkManagerController.cs |
| NetworkPlayer | Игрок: движение, камера, инвентарь | NetworkPlayer.cs |
| ShipController | Корабль: физика, кооп-пилотирование | ShipController.cs |
| NetworkUI | UI сети: Disconnect, Reconnect | NetworkUI.cs |
| NetworkInventory | Сетевая синхронизация инвентаря | NetworkInventory.cs |

### Технические параметры

| Параметр | Значение |
|----------|----------|
| Транспорт | UnityTransport (UDP) |
| Порт | 7777 |
| Протокол | NGO (Netcode for GameObjects) |
| Макс. игроков | [🔴 Запланировано] 64 |
| Tick rate | [🔴 Запланировано] 30 Hz |

---

## 3. Connection Flow

### Подключение Host

```
1. Игрок нажимает "Start Server" (или автоматически)
2. NetworkManager.StartHost()
3. Сервер запущен на порту 7777
4. Клиент 0 (хост) подключён локально
5. Спавн NetworkPlayer префаба
6. Спавн ThirdPersonCamera как дочернего
7. OnClientConnectedCallback → обновление UI
```

### Подключение Client

```
1. Игрок вводит IP:Port (по умолчанию 127.0.0.1:7777)
2. NetworkManager.StartClient()
3. UnityTransport подключается к серверу
4. Сервер спавнит NetworkPlayer для клиента
5. Клиент получает управление своим игроком
6. OnClientConnectedCallback → обновление UI
```

### Disconnect

```
1. Игрок нажимает "Disconnect" (или Escape)
2. Inventory.SaveToPrefs() → сохранение
3. NetworkManager.Shutdown()
4. OnClientDisconnectCallback → UI обновление
5. Возврат в меню подключения
```

### Reconnect

```
1. OnTransportFailure() → обрыв соединения
2. Inventory.SaveToPrefs() → сохранение
3. Авто-реконнект (до 5 попыток)
4. При успехе: Inventory.LoadFromPrefs() → восстановление
5. При провале: Reconnect кнопка (ручная)
6. IP:Port сохраняется для быстрого подключения
```

---

## 4. RPC System

### ServerRpc (клиент → сервер)

| RPC | Описание | Параметры |
|-----|----------|-----------|
| `SubmitShipInputRpc()` | Ввод корабля | inputX, inputZ, mouseY, liftInput |
| `SubmitSwitchModeRpc()` | Переключение режима | mode (Walking/Flying) |
| `RequestPickupRpc()` | Запрос подбора | itemId |
| `RequestChestOpenRpc()` | Запрос сундука | chestId |

### ClientRpc (сервер → клиент)

| RPC | Описание | Параметры |
|-----|----------|-----------|
| `HidePickupRpc()` | Скрыть предмет | itemId |
| `OpenChestRpc()` | Открыть сундук | chestId |
| `AddPilotRpc()` | Добавить пилота | playerId |
| `RemovePilotRpc()` | Удалить пилота | playerId |

### SendTo Target

| Target | Описание | Пример |
|--------|----------|--------|
| `SendTo.Everyone` | Все клиенты + сервер | HidePickupRpc, OpenChestRpc |
| `SendTo.SpecifiedClients` | Выбранные клиенты | [🔴 Запланировано] |
| `ServerRpc` | Только сервер | SubmitShipInputRpc |

### [⚠️ Известная проблема] Boost не передаётся в RPC

| Проблема | Параметр `boost` не включён в SubmitShipInputRpc |
|----------|------------------------------------------------|
| Решение | Добавить параметр `bool boost` в RPC |
| Приоритет | 🟡 Средне |

---

## 5. Player Spawning

### Спавн игрока

| Этап | Описание |
|------|----------|
| 1 | NetworkManager спавнит NetworkPlayer префаб |
| 2 | OnNetworkSpawn() → инициализация |
| 3 | SpawnCamera() → ThirdPersonCamera как дочерний |
| 4 | Инвентарь инициализируется |
| 5 | Режим: Walking (пеший) |

### NetworkPlayer префаб

| Компонент | Описание |
|-----------|----------|
| NetworkObject | NGO компонент |
| NetworkTransform | Синхронизация позиции (ServerAuthority) |
| NetworkPlayer | Логика игрока |
| CharacterController | Физика (пеший режим) |
| PlayerController | Ввод |
| PlayerStateMachine | Состояния |
| Inventory | Инвентарь |

### Регистрация префабов

| Файл | Описание |
|------|----------|
| DefaultNetworkPrefabs.asset | Список разрешённых NetworkPrefab |
| NetworkManager | Ссылка на NetworkPlayer префаб |

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
| [🔴 Запланировано] Приоритет капитана | Вес 1.5x |

---

## 7. Inventory Sync

### Текущая синхронизация

| Действие | Синхронизация | Метод |
|----------|--------------|-------|
| Подбор предмета | ✅ Все клиенты | HidePickupRpc (SendTo.Everyone) |
| Открытие сундука | ✅ Все клиенты | OpenChestRpc (SendTo.Everyone) |
| Содержимое инвентаря | ❌ Не синхронизируется | — |
| Сохранение при дисконнекте | ✅ PlayerPrefs | SaveToPrefs |
| Загрузка при реконнекте | ✅ PlayerPrefs | LoadFromPrefs |

### [🔴 Запланировано] Полная синхронизация (Этап 3)

| Метод | Описание |
|-------|----------|
| NetworkList<ItemData> | Синхронизация содержимого |
| Серверная валидация | Anti-cheat проверка |
| Торговля | UI обмена между игроками |

---

## 8. Error Handling

### Обработка обрывов

| Событие | Действие | Callback |
|---------|----------|----------|
| Обрыв транспорта | Авто-реконнект 5 попыток | OnTransportFailure |
| Клиент отключился | Удаление игрока | OnClientDisconnectCallback |
| Сервер упал | Возврат в меню | OnServerStopped |
| Таймаут | [🔴 Запланировано]Disconnect | — |

### Логирование

| Событие | Лог |
|---------|-----|
| Подключение | `[Network] Client {id} connected` |
| Отключение | `[Network] Client {id} disconnected` |
| Обрыв | `[Network] Transport failure, reconnecting...` |
| Реконнект | `[Network] Reconnected successfully` |
| Player Count | `[Network] Players: {count}` |

---

## 9. Dedicated Server

### Запуск

| Параметр | Значение |
|----------|----------|
| Build arg | `-server` |
| Режим | Headless (без рендера) |
| Дополнительно | `-batchmode -nographics` |
| Порт | 7777 (настраивается) |

### Реализация

| Компонент | Описание |
|-----------|----------|
| NetworkManagerController | Проверка `-server` arg |
| Headless mode | Без камеры, без UI (кроме логов) |
| Авто-старт | Server starts automatically |
| Логирование | Консольные логи |

### [🔴 Запланировано] Улучшения

| Фича | Описание | Этап |
|------|----------|------|
| Конфиг-файл | server.json с настройками | Этап 5 |
| RCON | Удалённое управление | Этап 5 |
| Мониторинг | Prometheus + Grafana | Этап 5 |
| CI/CD | GitHub Actions билд | Этап 5 |

---

## 10. Known Issues & Limitations

| Проблема | Описание | Приоритет | Статус |
|----------|----------|-----------|--------|
| **Инвентарь не синхронизируется** | Каждый игрок видит свои предметы | 🟡 | Этап 3 |
| **Boost не в RPC** | Параметр boost не передаётся | 🟡 | Не исправлено |
| **NetworkVariable<string> не работает** | NGO ограничение | 🟡 | Откат |
| **Client-side Prediction базовое** | Не полноценное предсказание | 🟢 | Улучшить |
| **Нет системы лобби** | Нет matchmaking | 🔴 | Этап 5+ |
| **Нет шардинга** | Один сервер = один мир | 🔴 | Этап 5+ |

---

## 11. Future Architecture

### [🔴 Запланировано] Этап 5+

| Компонент | Описание |
|-----------|----------|
| **Сервер .NET 8** | Отдельный серверный билд |
| **Master-сервер** | Matchmaking, лобби, список серверов |
| **Шардинг мира** | Несколько зон, автоматическое масштабирование |
| **JWT аутентификация** | Аккаунты, сессии |
| **PostgreSQL** | База данных: аккаунты, прогресс, инвентарь |
| **Redis** | Кэширование, сессии |
| **Kubernetes** | Оркестрация серверов |
| **Голосовой чат (Vivox)** | Voice communication |

---

## 12. Formulas

| Формула | Описание |
|---------|----------|
| `cooperativeInput = Sum(pilotInput[i]) / pilotCount` | Усреднение кооп-ввода |
| `interpolation = Vector3.Lerp(prevPos, targetPos, speed * dt)` | Интерполяция позиции |
| `correction = targetPos - predictedPos` | Коррекция при рассинхроне |
| `reconnectDelay = baseDelay * attempt` | Задержка между попытками |
| `tickRate = 30 Hz` [🔴] | Частота обновления сервера |

---

## 13. Tuning Knobs

| Параметр | Мин | Макс | Текущее | Влияние |
|----------|-----|------|---------|---------|
| `port` | 1024 | 65535 | 7777 | Сетевой порт |
| `reconnectAttempts` | 1 | 20 | 5 | Попытки реконнекта |
| `reconnectBaseDelay` | 0.5 | 10 | 2.0 | Базовая задержка (сек) |
| `interpolationSpeed` | 5 | 30 | 15 | Скорость интерполяции |
| `maxPlayers` | 2 | 128 | 4 [🔴] | Макс. игроков |
| `tickRate` | 10 | 120 | 30 [🔴] | Частота сервера (Hz) |

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
| 8 | Reconneкт работает | Обрыв → авто-подключение | ✅ |
| 9 | Инвентарь сохраняется | Disconnect → Reconnect | ✅ |
| 10 | Player Count обновляется | Подключить/отключить | ✅ |
| 11 | Dedicated Server запускается | `-server` build arg | ✅ |
| 12 | Boost синхронизируется | [⚠️ Не синхронизируется] | 🔴 |
| 13 | Полная синхронизация инвентаря | [🔴 Запланировано] | 🔴 |
| 14 | Лобби/Matchmaking | [🔴 Запланировано] | 🔴 |

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [NETWORK_ARCHITECTURE.md](../NETWORK_ARCHITECTURE.md) | [DEDICATED_SERVER.md](../DEDICATED_SERVER.md)
