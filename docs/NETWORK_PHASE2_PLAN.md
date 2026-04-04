# Этап 2: Сетевой фундамент — MMO Dedicated Server

**Ветка:** `qwen-dev-mmo` (от `qwen-dev`)
**Дата начала:** 4 апреля 2026 г.
**Цель:** Авторитарный dedicated server для MMO-архитектуры

---

## 🎯 Стратегия

### Что мы делаем

Создаём **авторитарный dedicated server** на базе Unity Netcode for GameObjects (NGO) с headless-билдом. Сервер — единственный источник истины. Клиенты только шлют ввод и получают состояние.

### Архитектура

```
┌─────────────────────────────────────────────────────────┐
│                    Dedicated Server                      │
│              (headless Unity build, Linux)               │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │  World Gen   │  │  Physics     │  │  Game Logic  │  │
│  │  (пики,      │  │  (корабли,   │  │  (инвентарь, │  │
│  │  облака)     │  │  коллизии)   │  │  сундуки)    │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │         NetworkManager (Server Mode)              │   │
│  │  • Player spawning                                │   │
│  │  • NetworkVariable replication                    │   │
│  │  • ServerRpc обработка                            │   │
│  │  • Scene management                               │   │
│  └──────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────┘
                         │ UnityTransport (UDP)
                         │
          ┌──────────────┼──────────────┐
          │              │              │
    ┌─────┴─────┐  ┌─────┴─────┐  ┌─────┴─────┐
    │  Client 1 │  │  Client 2 │  │  Client N │
    │  (Unity)  │  │  (Unity)  │  │  (Unity)  │
    └───────────┘  └───────────┘  └───────────┘
```

### Ограничения NGO для MMO (честно)

| Параметр | NGO (текущий) | Идеал для MMO |
|----------|---------------|---------------|
| **Игроков на сервер** | ~8-16 комфорт, ~32 предел | 100-500+ |
| **Масштабирование** | Один сервер = одна сцена | Шардинг, зоны |
| **Client-side Prediction** | Нет (планируется в будущем) | Обязательно |
| **Entity-система** | GameObjects (тяжёлые) | DOTS/ECS (лёгкие) |

**Наш путь:**
1. **Сейчас:** NGO Dedicated Server, 4-8 игроков, одна сцена. Доказываем что механики работают.
2. **Позже (если нужно):** Переход на Netcode for Entities (DOTS) + шардинг мира.

---

## 📋 Пошаговый план (10 шагов)

Каждый шаг — маленький, проверяемый, коммитится отдельно.

---

### 🔧 ФАЗА 0: Починить текущую инфраструктуру

#### Шаг 1: Зарегистрировать NetworkPlayer как PlayerPrefab

**Что делаем:**
- Открыть `NetworkManager.prefab` в Unity
- Перетащить `NetworkPlayer.prefab` в поле **PlayerPrefab** компонента NetworkManager
- Добавить компонент `NetworkPlayer.cs` на префаб `NetworkPlayer.prefab`

**Как проверить:**
- Запустить сцену → нажать Host → в логе видно "Local player spawned"
- NetworkPlayer появляется в сцене

**Файлы:**
- `Assets/_Project/Prefabs/NetworkManager.prefab`
- `Assets/_Project/Prefabs/NetworkPlayer.prefab`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

---

#### Шаг 2: Зарегистрировать префабы в DefaultNetworkPrefabs

**Что делаем:**
- Открыть `DefaultNetworkPrefabs.asset`
- Добавить `NetworkPlayer.prefab` в список

**Как проверить:**
- В инспекторе DefaultNetworkPrefabs видно NetworkPlayer в списке
- NetworkManager видит префаб при старте

**Файлы:**
- `Assets/DefaultNetworkPrefabs.asset`

---

#### Шаг 3: Тест Host + Client на одной машине

**Что делаем:**
- Собрать билд (`File → Build Settings → Build`)
- Запустить редактор Unity → нажать **Host**
- Запустить билд → ввести IP `127.0.0.1` → нажать **Connect**

**Как проверить:**
- Оба экземпляра показывают друг друга
- Позиции синхронизируются (двигай одного — второй видит)

**Критерий успеха:** Два окна, два игрока, видят друг друга, позиции синхронизированы.

---

### 🏗️ ФАЗА 1: Авторитарный сервер — движение

#### Шаг 4: ServerRpc для ввода игрока + NetworkVariable позиции

**Что делаем:**
- Переписать `NetworkPlayer.cs`:
  - Убрать клиентское управление движением
  - Добавить `NetworkVariable<Vector3> ServerPosition`
  - Добавить `[Rpc(SendTo.Server)] SubmitInputRpc(Vector2 input)` — клиент шлёт ввод
  - Сервер обрабатывает ввод в `Update()`, обновляет `ServerPosition`
  - Клиенты читают `ServerPosition` и интерполируют позицию

**Архитектура:**
```
Клиент: WASD → Vector2 input → SubmitInputRpc(input) → Сервер
Сервер:  Получить input → посчитать новую позицию → ServerPosition.Value = newPos
Клиент:  ServerPosition.Value → transform.position (интерполяция)
```

**Как проверить:**
- Host: двигаемся → Client видит движение с небольшой задержкой (интерполяция)
- Client: двигаемся → Host видит движение
- Только сервер контролирует реальную позицию

**Файлы:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

---

#### Шаг 5: Настройка NetworkTransform для плавной интерполяции

**Что делаем:**
- Настроить `NetworkTransform` на `NetworkPlayer.prefab`:
  - `Interpolate: true`
  - `PositionThreshold: 0.01` (чувствительность синхронизации)
  - `SendMode: Variable` (отправлять только при изменении)

**Как проверить:**
- Движение плавное, без рывков и телепортаций
- При быстром движении — не дёргается

**Файлы:**
- `Assets/_Project/Prefabs/NetworkPlayer.prefab` (настройка компонента)

---

#### Шаг 6: Синхронизация вращения и режима (пеший/корабль)

**Что делаем:**
- Добавить `NetworkVariable<Quaternion> ServerRotation`
- Добавить `NetworkVariable<PlayerMode> CurrentMode` (enum: OnFoot, InShip)
- Сервер обновляет оба значения, клиенты читают

**Как проверить:**
- Игрок поворачивается — второй видит поворот
- Игрок садится в корабль — второй видит смену режима

**Файлы:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
- `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` (адаптация под сеть)

---

### 🎮 ФАЗА 2: Игровые механики по сети

#### Шаг 7: Сетевой инвентарь — подбор предметов

**Что делаем:**
- `ItemPickupSystem.cs` → вместо прямого подбора шлёт `SubmitPickupRpc(itemId)`
- Сервер проверяет:
  - Предмет существует и рядом с игроком
  - Игрок в режиме "пеший"
- Сервер добавляет предмет в `NetworkVariable<List<int>> Inventory`
- Сервер деспаунит предмет в мире (`NetworkObject.Despawn()`)
- Клиент получает обновление через `OnValueChanged`

**Архитектура:**
```
Клиент (E) → SubmitPickupRpc(itemId) → Сервер
Сервер: Проверить расстояние → Добавить в инвентарь → Despawn предмета
Сервер: NetworkVariable<Inventory> обновляется → Все клиенты получают
```

**Как проверить:**
- Один игрок подбирает предмет — у второго предмет исчезает из мира
- Инвентарь синхронизирован у обоих

**Файлы:**
- `Assets/_Project/Scripts/Player/ItemPickupSystem.cs`
- `Assets/_Project/Scripts/Core/Inventory.cs` (добавить NetworkVariable)
- `Assets/_Project/Scripts/Core/PickupItem.cs` (сетевой деспаун)

---

#### Шаг 8: Сетевые сундуки

**Что делаем:**
- `ChestContainer` → открытие через `SubmitChestOpenRpc(chestId)`
- Сервер:
  - Проверяет расстояние
  - Генерирует лут из LootTable
  - Добавляет в инвентарь
  - Деспаунит сундук
- Сервер шлёт `ClientRpc` для анимации открытия (визуал на клиентах)

**Как проверить:**
- Один открывает сундук — оба получают предметы, сундук исчезает

**Файлы:**
- `Assets/_Project/Scripts/Core/ChestContainer.cs`

---

### 🔧 ФАЗА 3: Dedicated Server и стабильность

#### Шаг 9: Обработка подключений, отключений, обрывов

**Что делаем:**
- `NetworkManagerController.cs`:
  - Подписаться на `NetworkManager.OnClientConnectedCallback`
  - Подписаться на `NetworkManager.OnClientDisconnectCallback`
  - Подписаться на `NetworkManager.OnServerStarted`
  - Логировать подключения/отключения
  - Кнопка **Disconnect** в NetworkUI
  - Индикатор статуса: 🟢 Подключён / 🔴 Отключён
  - Список игроков в UI

**Как проверить:**
- Подключить клиента → сервер логирует "Client X connected"
- Отключить клиента → сервер логирует "Client X disconnected"
- Обрыв соединения (Alt+F4 клиент) → сервер обнаруживает через таймаут

**Файлы:**
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs`
- `Assets/_Project/Scripts/UI/NetworkUI.cs`

---

#### Шаг 10: Dedicated Server build (headless)

**Что делаем:**
- Создать `ServerBootstrap.cs` — скрипт запуска сервера:
  - Определяет `Application.isEditor` vs билд
  - В билде автоматически запускает `StartServer()`
  - Отключает рендеринг, камеру, UI (headless)
  - Логирует в консоль: IP, порт, подключения
- Настроить Build Settings:
  - `File → Build Settings → Linux (x86_64)`
  - `Headless Mode: true` (отключает графику)
  - Или через аргумент командной строки: `-batchmode -nographics`
- Создать `server_config.txt` или использовать аргументы:
  - `-port 7777`
  - `-maxPlayers 8`

**Как проверить:**
- Собрать Linux headless билд
- Запустить: `./ProjectC_Server.x86_64 -port 7777`
- Сервер запускается без окна, логирует в консоль
- Два клиента подключаются с `127.0.0.1:7777`
- Игра работает

**Файлы:**
- `Assets/_Project/Scripts/Core/ServerBootstrap.cs` (новый)
- Build Settings

---

## 🔌 Что НЕ делаем в Этапе 2

| Не делаем | Почему | Когда |
|-----------|--------|-------|
| Клиент-side prediction | Сложно, нужно позже | Этап 3-4 |
| Серверная база данных (PostgreSQL) | Пока данные в памяти | Этап 3 (RPG система) |
| Шардинг мира (несколько сцен) | Усложняет инфраструктуру | Когда будет нужно |
| Античит система | Пока нет экономики | Этап 5 |
| Голосовой чат | Внешний сервис (Vivox) | Этап 4 |
| Matchmaking/Лобби | Пока ручное подключение | После базовой сети |

---

## 📊 Сводная таблица шагов

| # | Шаг | Фаза | Файлы | Статус |
|---|-----|------|-------|--------|
| 1 | PlayerPrefab + NetworkPlayer.cs на префабе | 0: Починить | NetworkManager.prefab, NetworkPlayer.prefab | ⏳ |
| 2 | DefaultNetworkPrefabs регистрация | 0: Починить | DefaultNetworkPrefabs.asset | ⏳ |
| 3 | Тест Host + Client (2 окна) | 0: Починить | — | ⏳ |
| 4 | ServerRpc ввода + NetworkVariable позиции | 1: Движение | NetworkPlayer.cs | ⏳ |
| 5 | Настройка NetworkTransform интерполяции | 1: Движение | NetworkPlayer.prefab | ⏳ |
| 6 | Синхронизация вращения + режима | 1: Движение | NetworkPlayer.cs, PlayerStateMachine.cs | ⏳ |
| 7 | Сетевой подбор предметов | 2: Механики | ItemPickupSystem.cs, Inventory.cs, PickupItem.cs | ⏳ |
| 8 | Сетевые сундуки | 2: Механики | ChestContainer.cs | ⏳ |
| 9 | Подключения, отключения, Disconnect UI | 3: Dedicated | NetworkManagerController.cs, NetworkUI.cs | ⏳ |
| 10 | Headless Dedicated Server build | 3: Dedicated | ServerBootstrap.cs | ⏳ |

---

## 🛠️ Технические детали

### UnityTransport настройки (для Dedicated Server)

| Параметр | Значение | Обоснование |
|----------|----------|-------------|
| Protocol | UDP | Быстрее TCP, потеря пакетов не критична |
| Server Listen Address | `0.0.0.0` | Слушать все интерфейсы |
| Port | `7777` | Стандартный, можно менять |
| Max Connect Attempts | `60` | Достаточно для плохих соединений |
| Heartbeat Timeout | `500ms` | Быстрое обнаружение обрыва |
| Disconnect Timeout | `30000ms` | 30 сек до отключения |
| Debug Simulator | Off (в релизе) | Включать для тестов лага |

### NetworkVariable типы

| Данные | Тип | Кто пишет | Кто читает |
|--------|-----|-----------|------------|
| Позиция игрока | `NetworkVariable<Vector3>` | Сервер | Все клиенты |
| Вращение игрока | `NetworkVariable<Quaternion>` | Сервер | Все клиенты |
| Режим (пеший/корабль) | `NetworkVariable<int>` | Сервер | Все клиенты |
| Инвентарь | `NetworkVariableList<int>` | Сервер | Все клиенты |
| Здоровье (будущее) | `NetworkVariable<float>` | Сервер | Все клиенты |

### ServerRpc безопасность

Каждый ServerRpc должен **валидировать** входные данные:

```csharp
[Rpc(SendTo.Server)]
void SubmitPickupRpc(int itemId, RpcParams rpcParams = default)
{
    // 1. Проверить что игрок существует
    if (!TryGetComponent<NetworkObject>(out var netObj)) return;
    
    // 2. Проверить расстояние (анти-чит)
    var distance = Vector3.Distance(transform.position, itemPosition);
    if (distance > maxPickupRadius) 
    {
        Debug.LogWarning($"Client {OwnerClientId} tried to pickup from too far: {distance}m");
        return; // Отклонить
    }
    
    // 3. Проверить что предмет существует
    if (!PickupItemRegistry.TryGet(itemId, out var item)) return;
    
    // 4. Выполнить действие
    AddItemToInventory(itemId);
    item.Despawn();
}
```

---

## 🚀 Git стратегия

```bash
# Создать ветку от qwen-dev
git checkout qwen-dev
git checkout -b qwen-dev-mmo
git push upstream qwen-dev-mmo

# Каждый шаг коммитится отдельно
git add .
git commit -m "Шаг 1: NetworkPlayer зарегистрирован как PlayerPrefab"
git push

# Если что-то сломалось — откат
git reset --hard HEAD
```

---

## 📝 Тест-план для каждого шага

| Шаг | Тест | Ожидаемый результат |
|-----|------|---------------------|
| 1 | Host → лог "Local player spawned" | ✅ Игрок появляется |
| 2 | DefaultNetworkPrefabs содержит NetworkPlayer | ✅ Виден в списке |
| 3 | 2 окна, Host + Client | ✅ Оба видят друг друга |
| 4 | Двигать игрока на клиенте A | ✅ Игрок B видит движение |
| 5 | Быстрое движение | ✅ Плавно, без рывков |
| 6 | Поворот + смена режима | ✅ Синхронизировано |
| 7 | Подбор предмета | ✅ Предмет исчезает у всех, инвентарь обновлён |
| 8 | Открытие сундука | ✅ Лут у всех, сундук исчез |
| 9 | Disconnect + обрыв | ✅ Сервер логирует, UI обновлён |
| 10 | Headless билд + 2 клиента | ✅ Сервер без графики, клиенты играют |

---

## 🔗 Связанные документы

- [`MMO_Development_Plan.md`](MMO_Development_Plan.md) — общий план разработки
- [`STEP_BY_STEP_DEVELOPMENT.md`](STEP_BY_STEP_DEVELOPMENT.md) — принцип пошаговой разработки
- [`QWEN_CONTEXT.md`](QWEN_CONTEXT.md) — контекст проекта

---

**Следующий шаг:** После утверждения плана — начать с Шага 1 (починить PlayerPrefab).

**Ветка:** `qwen-dev-mmo` | **Принцип:** "Медленнее = Быстрее"
