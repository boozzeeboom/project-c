# T-CARGO-UI-02: Cargo Manager на корабле — План реализации

> **Дата:** 2026-07-03
> **Эпик:** #2 из CARGO_REMAINING_WORK
> **Статус:** ✅ Код готов, ждёт компиляции
> **Решения пользователя (2026-07-03):**
> - **Q1:** Push (A) — уже реализован в `ShipTelemetryState.cargoDetail[]` (из T-CARGO-UI-01)
> - **Q2:** Только трюм↔инвентарь. Без Pack/Unpack.
> - **Q3:** Ручное добавление `ShipCargoConsole` на дочерний 3D объект.
> - **Ввод:** F через `PlayerInputReader.OnModeSwitchPressed` → `NetworkPlayer.TryInteractNearestShipCargoConsole()`
>
> **Созданные файлы (7 новых):**
> | Файл | Назначение |
> |------|------------|
> | `Scripts/Ship/Cargo/ShipCargoConsole.cs` | MonoBehaviour + IInteractable компонент |
> | `Trade/Exchange/Network/ShipCargoServer.cs` | Серверный NetworkBehaviour, RPC StoreToCargo/RetrieveFromCargo |
> | `Trade/Scripts/Dto/ShipCargoResultDto.cs` | DTO результата cargo-операции |
> | `Trade/Scripts/Client/ShipCargoClientState.cs` | Клиентская проекция, события для UI |
> | `Trade/Scripts/Client/ShipCargoConsoleWindow.cs` | UI Toolkit окно грузового отсека |
> | `UI/ShipCargoConsoleWindow.uxml` | Разметка UI |
> | `UI/ShipCargoConsoleWindow.uss` | Стили UI |
>
> **Изменённые файлы (3):**
> | Файл | Что изменено |
> |------|-------------|
> | `InteractableManager.cs` | + `_shipCargoConsoles` список, Register/Unregister/FindNearest |
> | `NetworkPlayer.cs` | + `TryInteractNearestShipCargoConsole()`, + `ReceiveShipCargoResultTargetRpc` |
> | `NetworkManagerController.cs` | + `CreateShipCargoClientState()` при OnClientConnected |

---

## TL;DR

Добавить на корабль дочерний GameObject с компонентом `ShipCargoConsole` (MonoBehaviour + IInteractable). По нажатию F рядом с ним — открывается UI Toolkit окно `ShipCargoConsoleWindow` (паттерн ExchangerTab: левая панель = инвентарь, правая = cargo корабля). Кнопки: «В трюм», «Из трюма», «Упаковать», «Распаковать». Серверная логика: новый `ShipCargoServer` (NetworkBehaviour) с RPC для inventory ↔ ship cargo.

---

## Архитектурное решение

**Паттерн:** CraftingStation (IInteractable + InteractableManager + NetworkPlayer F-key) + ExchangerTab (UI Toolkit two-panel + RPC).

**Почему не просто реюз ExchangeServer:**
- ExchangeServer делает inventory ↔ warehouse (склад на станции)
- Для cargo нужно inventory ↔ ship cargo (трюм корабля)
- TradeWorld.TryLoadToShip/TryUnloadFromShip делают warehouse ↔ ship cargo (только в зоне рынка)
- Нужна прямая операция inventory ↔ ship cargo, без привязки к рынку/warehouse

**Новый серверный компонент:** `ShipCargoServer` — NetworkBehaviour в BootstrapScene (рядом с ExchangeServer). Содержит RPC:
- `RequestStoreToCargoRpc` — инвентарь → трюм
- `RequestRetrieveFromCargoRpc` — трюм → инвентарь
- Реюз существующих ExchangeServer.RequestPack/UnpackRpc для Pack/Unpack (инвентарь ↔ склад)

---

## Тикеты

### Тикет 1: ShipCargoConsole (interactable на корабле)

**Новый файл:** `Assets/_Project/Scripts/Ship/Cargo/ShipCargoConsole.cs`

**Что:** MonoBehaviour + IInteractable. Ставится на дочерний GameObject внутри корабля (например `ShipRoot/CargoConsole`). Имеет SphereCollider (IsTrigger), радиус взаимодействия. При входе игрока в триггер — регистрируется в InteractableManager.

**Поля:**
- `float interactionRadius = 3f` — радиус для IInteractable
- Ссылка на родительский `ShipController` (GetComponentInParent)

**IInteractable:**
- `InstanceId` → `ShipController.NetworkObjectId + "_cargo"`
- `DisplayName` → `"Грузовой отсек"`
- `InteractionRadius` → `interactionRadius`
- `Position` → `transform.position`

**Методы:**
- `OnTriggerEnter` → `InteractableManager.RegisterShipCargoConsole(this)`
- `OnTriggerExit` → `InteractableManager.UnregisterShipCargoConsole(this)`

---

### Тикет 2: InteractableManager + NetworkPlayer (F-key wire)

**Изменяемые файлы:**
- `Assets/_Project/Scripts/Core/InteractableManager.cs`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

**InteractableManager:**
- Новый список: `_shipCargoConsoles` (List<ShipCargoConsole>, capacity 8)
- Методы: `RegisterShipCargoConsole`, `UnregisterShipCargoConsole`, `FindNearestShipCargoConsole`
- Добавить в `ClearAll()`

**NetworkPlayer:**
- Новый метод `TryInteractNearestShipCargoConsole()`
- Приоритет в F-key обработке: после `TryInteractNearestCraftingStation()`, до `TryInteractNearestDoor()`
- Важно: взаимодействие доступно ТОЛЬКО когда игрок НЕ в корабле (`!_inShip`) — игрок стоит на палубе/рядом
- Логика:
  1. `InteractableManager.FindNearestShipCargoConsole(position, pickupRange)`
  2. Если найден → получить `ShipController` родителя → открыть `ShipCargoConsoleWindow.Show(shipNetId)`

---

### Тикет 3: ShipCargoServer (серверная логика)

**Новый файл:** `Assets/_Project/Trade/Exchange/Network/ShipCargoServer.cs`

**Что:** NetworkBehaviour для BootstrapScene (как ExchangeServer). Принимает RPC от клиента.

**RPC:**
```
[Rpc(SendTo.Server)]
RequestStoreToCargoRpc(ulong shipNetId, int inventoryItemId, int count)

[Rpc(SendTo.Server)]  
RequestRetrieveFromCargoRpc(ulong shipNetId, string cargoItemId, int count, int inventoryItemId)

[Rpc(SendTo.Server)]
RequestPackRpc(string locationId, int inventoryItemId, int countToRemove)  // делегат → ExchangeServer

[Rpc(SendTo.Server)]
RequestUnpackRpc(string locationId, string warehouseItemId, int countToRemove)  // делегат → ExchangeServer
```

**StoreToCargo (инвентарь → трюм):**
1. Получить ShipController по shipNetId
2. Получить ShipClass через ShipClassMappingConfig
3. `InventoryWorld.Instance.RemoveItems(clientId, itemId, itemType, count)`
4. `TradeWorld.Instance.GetOrLoadCargo(shipNetId, shipClass).TryAdd(...)`
5. `Repository.SetCargo(...)`
6. Push snapshot клиенту

**RetrieveFromCargo (трюм → инвентарь):**
1. `TradeWorld.Instance.GetOrLoadCargo(shipNetId, shipClass).TryRemove(...)`
2. `InventoryWorld.Instance.AddItemDirect(clientId, itemId, itemType)` × count
3. Rollback если инвентарь полон
4. `Repository.SetCargo(...)`
5. Push snapshot клиенту

**Результат:** `ShipCargoResultDto` (success, message, cargoDelta, inventoryDelta) — по паттерну ExchangeResultDto.

---

### Тикет 4: ShipCargoClientState + ShipCargoResultDto

**Новые файлы:**
- `Assets/_Project/Trade/Scripts/Dto/ShipCargoResultDto.cs`
- `Assets/_Project/Trade/Scripts/Client/ShipCargoClientState.cs`

**ShipCargoResultDto:** INetworkSerializable struct { success, message, op, cargoDelta, inventoryDelta }

**ShipCargoClientState:** MonoBehaviour singleton (паттерн ExchangeClientState). Событие `OnResultReceived`. Создаётся в `NetworkManagerController.OnClientConnectedSession`.

**NetworkPlayer:** новый TargetRpc `ReceiveShipCargoResultTargetRpc(ShipCargoResultDto dto)` → пробрасывает в `ShipCargoClientState.Instance.OnShipCargoResultReceived(dto)`.

---

### Тикет 5: ShipCargoConsoleWindow (UI Toolkit)

**Новые файлы:**
- `Assets/_Project/Trade/Scripts/Client/ShipCargoConsoleWindow.cs`
- `Assets/_Project/UI/ShipCargoConsoleWindow.uxml`
- `Assets/_Project/UI/ShipCargoConsoleWindow.uss`

**Что:** UI Toolkit окно (как MarketWindow, но проще — без табов). Паттерн ExchangerTab.

**Структура:**
```
┌──────────────────────────────────────────────┐
│  Грузовой отсек корабля "Шхуна"    [_][X]   │
├────────────────────┬─────────────────────────┤
│  ИНВЕНТАРЬ ИГРОКА  │  ТРЮМ КОРАБЛЯ           │
│  (ListView)        │  (ListView)             │
│  • Железо ×5       │  • Ящик руды ×2         │
│  • Дерево ×3       │  • Бочка воды ×1        │
│                    │                         │
│  [ → В трюм ]      │  [ ← Из трюма ]         │
├────────────────────┴─────────────────────────┤
│  ОБМЕННИК (склад)                            │
│  [ Упаковать (инв→склад) ]                   │
│  [ Распаковать (склад→инв) ]                 │
├──────────────────────────────────────────────┤
│  Статус: OK +3 руды в трюме                  │
└──────────────────────────────────────────────┘
```

**Реализация UI (.cs):**
- `Show(ulong shipNetId)` — строит UI, подписывается на InventoryClientState + ShipCargoClientState
- `RefreshCargoData()` — запрашивает cargo корабля (нужен RPC или использовать существующий snapshot)
- Левая панель: инвентарь из `InventoryClientState.GetItems()` (струппированные, только те что поддерживают упаковку через ResourceExchangeResolver)
- Правая панель: cargo корабля (нужен способ получить содержимое трюма с сервера)
- Кнопки:
  - «В трюм» → `ShipCargoServer.Instance.RequestStoreToCargoRpc(...)`
  - «Из трюма» → `ShipCargoServer.Instance.RequestRetrieveFromCargoRpc(...)`
  - «Упаковать» → `ExchangeServer.Instance.RequestPackRpc(...)`
  - «Распаковать» → `ExchangeServer.Instance.RequestUnpackRpc(...)`

**Открытый вопрос Q1:** откуда клиент получает содержимое cargo корабля? Варианты:
- (A) Расширить `ShipTelemetryState` полем `cargoItems[]` (push)
- (B) Новый RPC `RequestShipCargoDetailRpc(shipNetId)` → ответ TargetRpc (pull)
- **Рекомендация:** вариант (B) pull — проще, меньше bandwidth на постоянную репликацию, консоль открывается редко.

---

### Тикет 6: BootstrapScene + префаб

**Изменяемые файлы:**
- `Assets/_Project/Scenes/BootstrapScene.unity` — добавить `[ShipCargoServer]` GameObject

**Префаб корабля:**
- Добавить дочерний GameObject `CargoConsole` с компонентом `ShipCargoConsole` + SphereCollider (IsTrigger)
- Настроить радиус в инспекторе

---

## Порядок реализации

| # | Тикет | ~Часы | Что делает |
|---|-------|-------|------------|
| 1 | ShipCargoConsole | 0.5 | Компонент на корабль |
| 2 | InteractableManager + NetworkPlayer | 1.0 | F-key wire |
| 3 | ShipCargoServer | 2.0 | Серверная логика inventory↔cargo |
| 4 | ShipCargoClientState + DTO | 0.5 | Клиентская проекция |
| 5 | ShipCargoConsoleWindow | 2.0 | UI Toolkit окно |
| 6 | BootstrapScene + префаб | 0.5 | Сборка сцены |

**Итого:** ~6.5 часов

---

## Открытые вопросы (требуют решения пользователя)

**Q1 (критичный):** Как клиент получает список cargo-предметов корабля?
- **(A) Push:** Расширить `ShipTelemetryState` полем `cargoItems: NetworkList<CargoItemDto>`. Плюс: UI всегда актуален. Минус: bandwidth на каждый cargo-чейндж.
- **(B) Pull:** Новый RPC `RequestShipCargoDetailRpc(shipNetId)` → `TargetRpc` с массивом `CargoItemDto[]`. Плюс: нет постоянной репликации. Минус: задержка при открытии окна.
- Рекомендация: **(B) Pull** — cargo меняется редко, консоль открывается редко, не浪费 bandwidth.

**Q2:** Нужны ли кнопки «Упаковать» / «Распаковать» (инвентарь ↔ склад) в этом окне, или ТОЛЬКО операции с трюмом?
- Документ CARGO_REMAINING_WORK говорит «+ [Упаковать]/[Распаковать] (reюз ExchangeServer)»
- Но для Pack/Unpack нужен locationId (склад привязан к станции). Если игрок не в зоне рынка — склад недоступен.
- Рекомендация: **показать Pack/Unpack только если игрок в MarketZone**, иначе скрыть.

**Q3:** Должен ли `ShipCargoConsole` быть на КАЖДОМ корабле (в префабе) или добавляться вручную?
- Рекомендация: **в префабе корабля**, как дочерний GameObject с коллайдером.

---

## Файлы которые будут созданы/изменены

| Файл | Действие |
|------|----------|
| `Assets/_Project/Scripts/Ship/Cargo/ShipCargoConsole.cs` | **Новый** |
| `Assets/_Project/Scripts/Core/InteractableManager.cs` | Изменить |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | Изменить |
| `Assets/_Project/Trade/Exchange/Network/ShipCargoServer.cs` | **Новый** |
| `Assets/_Project/Trade/Scripts/Dto/ShipCargoResultDto.cs` | **Новый** |
| `Assets/_Project/Trade/Scripts/Client/ShipCargoClientState.cs` | **Новый** |
| `Assets/_Project/Trade/Scripts/Client/ShipCargoConsoleWindow.cs` | **Новый** |
| `Assets/_Project/UI/ShipCargoConsoleWindow.uxml` | **Новый** |
| `Assets/_Project/UI/ShipCargoConsoleWindow.uss` | **Новый** |
| `Assets/_Project/Scenes/BootstrapScene.unity` | Изменить |
