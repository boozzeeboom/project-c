# Cargo System — диагноз (2026-06-17)

**Автор:** Mavis (Mavis)
**Статус:** 🔍 Диагноз / пред-проектная аналитика
**Назначение:** зафиксировать фактическое состояние Cargo подсистемы, прежде чем писать план работ.
**Связано с:** [CARGO_REFACTOR_PLAN_2026-06-17.md](CARGO_REFACTOR_PLAN_2026-06-17.md) (план)

---

## TL;DR

**Cargo System — это НЕ "заглушка, которую мы когда-то зарезервировали".** Это **самодостаточная server-authoritative подсистема на 70% готовности**, встроенная в Trade-стек v2 (`TradeWorld._cargoCache` + `CargoData` POCO + `IPlayerDataRepository.SetCargo/GetCargo` + `ShipSummaryDto` / `ShipCargoDto` в `MarketSnapshotDto`).

Параллельно существует **`Assets/_Project/Trade/Scripts/CargoSystem.cs` (MonoBehaviour, namespace `ProjectC.Player`)** — мёртвый legacy-дубль, который:
- дублирует `ShipClassLimits` из `Trade/Core/CargoData.cs`
- переехал в `ProjectC.Player` namespace и путается с `ShipFlightClass` в `ShipController`
- дёргается в одном-единственном месте — `ShipController.cs:638` (формула `GetSpeedPenalty()`)
- содержит мёртвые `CheckLeak` / `CheckFragile` (методы написаны, нигде не вызваны)
- 3 раза сериализован в `WorldScene_0_0.unity` (legacy-привязки, мертвый код в сцене)
- является источником `ShipClass` для `MarketZone` (через `cargoComp.shipClass`)

**Главная проблема:** документация врёт (3 legacy-дока утверждают, что `CargoSystem` отсутствует), а реальная рабочая Cargo подсистема — внутри `Trade/`, а не как отдельная `Ship/` подсистема.

---

## 1. Что РЕАЛЬНО есть (по факту кода, не документов)

### 1.1 Cargo как часть Trade v2 (РАБОТАЕТ)

| Файл | Что это | Статус |
|---|---|---|
| `Trade/Scripts/Core/CargoData.cs` | POCO класс. Лимиты (`ShipClassLimits`), `TryAdd/TryRemove/ComputeTotalWeight/Volume/Slots/LoadFrom/SaveToList/Clear`. Параметр `shipClass` immutable | ✅ Работает |
| `Trade/Scripts/Core/CargoData.cs:138` | `ShipClassLimits.Get(ShipClass)` — таблица 4 классов (Light=4/100kg/3m³, Medium=10/500/12, HeavyI=20/2000/40, HeavyII=30/5000/80) | ✅ Работает |
| `Trade/Scripts/Core/TradeWorld.cs:381-395` | `GetOrLoadCargo(shipId, ShipClass)` — кэш в `_cargoCache[shipId]`, `InvalidateCargo(shipId)` | ✅ Работает |
| `Trade/Scripts/Core/TradeWorld.cs:272-326` | `TryLoadToShip` / `TryUnloadFromShip` — атомарный двусторонний обмен (cargo ↔ warehouse) с rollback при ошибке | ✅ Работает |
| `Trade/Scripts/Repository/IPlayerDataRepository.cs:29-30` | `TryGetCargo(shipId, out items)` / `SetCargo(shipId, items)` — persistence | ✅ Работает (PlayerPrefs + ServerFile) |
| `Trade/Scripts/Dto/ShipSummaryDto.cs` | DTO для multi-ship selection (вес/объём/слоты/типы) | ✅ Работает |
| `Trade/Scripts/Dto/MarketSnapshotDto.cs:49,57,144` | `cargo` (выбранный) + `shipCargos[]` (все в зоне) + `ShipCargoDto` | ✅ Работает |
| `Trade/Scripts/Network/MarketServer.cs:334,351` | `BuildMarketSnapshot` использует `TradeWorld.GetOrLoadCargo` | ✅ Работает |
| `Trade/Scripts/Network/MarketZone.cs:351-352` | `GetNearbyShips()` — определяет `ShipClass` по `cargoComp.shipClass` | ⚠️ Завязан на старый `CargoSystem` |

**Что уже умеет Trade v2 Cargo:**
- Слоты по `itemId` (стакается)
- Лимиты по 3 осям (slots/weight/volume)
- Persistence в файл/PlayerPrefs
- Репликация через NGO (через `MarketServer` snapshot RPC)
- Rate-limit (на уровне MarketServer)
- Zone check (через `MarketZone`)
- DTO (нет догадок — клиент получает структуру)
- Двусторонний обмен склад ↔ трюм с откатом

### 1.2 `ProjectC.Player.CargoSystem` (МЁРТВЫЙ ДУБЛЬ)

**Файл:** `Assets/_Project/Trade/Scripts/CargoSystem.cs` (287 строк, MonoBehaviour, namespace `ProjectC.Player`)

| Что | Где | Статус |
|---|---|---|
| `enum ShipClass` (Light/Medium/HeavyI/HeavyII) | строка 13 | ⚠️ Дубликат `ProjectC.Player.ShipClass` уже используется в Trade. **Это ДРУГОЙ enum с ТЕМ ЖЕ именем** (путаница) |
| `[Serializable] CargoItem` | строка 25 | Устарело (CargoData использует `WarehouseEntry`) |
| `public List<CargoItem> cargo` | строка 44 | Не используется (CargoData — источник истины) |
| `ShipLimits` Dictionary (копия `ShipClassLimits`) | строка 47 | Дубликат `CargoData.cs:138` |
| `CurrentWeight/CurrentVolume/UsedSlots` | строки 62-108 | Локальный API, не подключён к сети |
| `AddCargo(TradeItemDefinition, int)` | строка 120 | Локальный API, не подключён к сети |
| `RemoveCargo(string, int)` | строка 155 | Локальный API, не подключён к сети |
| `GetSpeedPenalty()` | строка 195 | ⚠️ **ЕДИНСТВЕННОЕ ЖИВОЕ ИСПОЛЬЗОВАНИЕ** — `ShipController.cs:638` |
| `CheckLeakOnCollision()` | строка 219 | ❌ Мёртвый код, нигде не вызван |
| `CheckFragileDamageOnCollision()` | строка 244 | ❌ Мёртвый код, нигде не вызван |

### 1.3 Использование старого `CargoSystem` в проекте

| Файл | Строка | Что | Что делает |
|---|---|---|---|
| `Player/ShipController.cs` | 97 | `[SerializeField] private ProjectC.Player.CargoSystem cargoSystem;` | Поле для сериализации |
| `Player/ShipController.cs` | 638 | `cargoPenalty = cargoSystem.GetSpeedPenalty();` | Штраф скорости в `AddForce` |
| `Trade/Scripts/Network/MarketZone.cs` | 351 | `ShipClass cls = cargoComp != null ? cargoComp.shipClass : ShipClass.Light;` | Определение класса корабля по `cargoComp` |

**Вывод:** старый `CargoSystem` — **это адаптер для `ShipController`**, через который движок узнаёт `ShipClass` (для лимитов скорости) и получает формулу `GetSpeedPenalty()`. Никакой полезной логики хранения груза в нём нет — это `MonoBehaviour`-обёртка с единственной формулой.

---

## 2. Что врёт документация

| Файл | Утверждение | Реальность |
|---|---|---|
| `docs/Ships/roadmap-integration.md:200` | "**Не делаем CargoSystem** \| Класса нет, не нужен для MVP" | Cargo есть, 70% готова, встроена в Trade |
| `docs/Ships/analysis-composite-ship.md:30` | "CargoSystem \| ❌ **Отсутствует** \| Класс не найден, ShipClass enum существует но CargoSystem как скрипта нет" | Cargo есть, но не там искали |
| `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md:67` | "CargoSystem \| ❌ Не существует \| Создаётся с нуля когда понадобится" | Cargo создана, но как часть Trade |
| `docs/Ships/Key-subsystem/00_OVERVIEW.md:84` | "Загрузить товары с рынка на корабль (в зоне) \| ✓ (Cargo-операции не требуют ключа)" | **Cargo-операции требуют ключа?** — нужно сверить с `MarketZone.cs` (см. §6) |
| `docs/Ships/legacy/HOWTO_CREATE_SHIP.md:97` | "**Cargo System** \| (оставить пустым)" | Корректно, но не объясняет зачем |

**Корень проблемы:** документы искали `CargoSystem.cs` в `Assets/_Project/Scripts/Ship/` и не нашли. На самом деле он в `Assets/_Project/Trade/Scripts/`, а основная cargo-логика — `Trade/Core/CargoData.cs`.

---

## 3. Что сломано / не сделано

### 3.1 Cargo UI
- ❌ Нет UI трюма (игрок не видит содержимое груза корабля в HUD)
- ⚠️ `MarketWindow` показывает cargo через вкладку/таб (нужно проверить, какая именно), но вне рынка — игрок груз не видит
- ❌ Нет индикации "перегруз/свободно" в ShipHUD

### 3.2 Скорость от груза
- ⚠️ `ShipController.GetSpeedPenalty()` зависит от **старого** `CargoSystem` (local, не из сети)
- ❌ На клиенте в ShipController нет доступа к серверной `TradeWorld._cargoCache[shipId]`
- ❌ Физика скорости НЕ учитывает реальный груз с сервера, только локальный `cargoSystem.cargo` (который пуст, если не выставлен в инспекторе)

### 3.3 Столкновения
- ❌ `CheckLeak` / `CheckFragile` — мёртвый код
- ❌ `ShipController.OnCollisionEnter` не дёргает эти методы
- ❌ GDD_25 секция 4.3 не реализована

### 3.4 Подключение к ShipController
- ❌ `ShipController.cargoSystem` — ссылка на local MonoBehaviour
- ❌ Нет серверного хука "при спавне корабля → зарегистрировать в TradeWorld"
- ❌ ShipController не знает свой `ShipClass` (определяет только `ShipFlightClass` для физики)

### 3.5 ShipRegistry / ShipDefinition
- ❌ `ShipRegistry.md` упоминает `ShipDefinition` (ScriptableObject) — не существует
- ❌ `ShipController` не имеет ссылки на `ShipDefinition`
- ❌ `ShipClass` (4 класса cargo) не связан с `ShipFlightClass` (4 класса физики) — это **два разных enum'а с разным назначением**, документация путает

### 3.6 Ownership / Security
- ❌ Cargo по `shipNetworkObjectId` — **НЕТ проверки, что клиент действительно управляет этим кораблём**. Любой клиент в зоне рынка может `TryLoadToShip` чужого корабля
- ⚠️ Частично защищено через `MarketZone` (зональный RPC), но не ship-specific

---

## 4. Что НЕЛЬЗЯ трогать (рабочее)

| Компонент | Почему нельзя |
|---|---|
| `Trade/Scripts/Core/CargoData.cs` | Рабочий POCO, источник истины на сервере |
| `Trade/Scripts/Core/TradeWorld.cs:272-395` (cargo-операции) | Сетевой API, используется `MarketServer` и `ContractWorld` |
| `IPlayerDataRepository.SetCargo/GetCargo` | Persistence контракт, обе реализации (PlayerPrefs, ServerFile) |
| `MarketSnapshotDto.cargo` / `shipCargos` / `ShipCargoDto` | Wire-format, обратная совместимость |
| `MarketServer` (cargo-секции) | Снапшоты + RPC |
| `ContractWorld` ссылка на `TradeWorld.GetOrLoadCargo` | Contract-сервер зависит |

---

## 5. Архитектурное решение (что делаем)

### 5.1 Не пишем с нуля
Старая CargoSystem (MonoBehaviour) — **не самостоятельная подсистема**, а **legacy-адаптер**. Её работа:
1. Держать локальный список `cargo` (сейчас пустой)
2. Вычислять `GetSpeedPenalty()` (формула)
3. Содержать `ShipClass` (источник правды для `MarketZone`)

Реальная cargo-подсистема — `Trade/Core/CargoData.cs`. Удалять старый `CargoSystem.cs` можно **только** после того, как:
- Скорость от груза начнёт читаться с сервера
- `MarketZone` получит `ShipClass` не из `cargoComp`, а из надёжного источника
- В сцене `WorldScene_0_0.unity` убраны 3 broken-ссылки на `CargoSystem`

### 5.2 Что реально нужно
**Подсистема `Ship/Cargo/` НЕ нужна** (Trade владеет грузом). Нужны **3 интеграции**:

1. **ShipController** (сервер) — при `OnNetworkSpawn` зарегистрировать корабль в `TradeWorld` (через `GetOrLoadCargo` по `ShipClass`). Скорость от груза читать с сервера.
2. **ShipController** (клиент) — при `FixedUpdate` читать `cargoPenalty` из `ShipCargoClientState` (новый singleton) или из NetworkVariable.
3. **MarketZone** — определение `ShipClass` перенести на компонент корабля (например, `ShipDefinition` SO + компонент-маркер `ShipClassMarker`).

### 5.3 Что делаем с старым `CargoSystem.cs`
**Удаляем** (после интеграции), потому что:
- Код не тестировался, не используется кроме 1 строки физики
- Namespace `ProjectC.Player` создаёт путаницу
- `ShipClassLimits` дубликат
- Логика хранения — мёртвая (CargoData делает то же на сервере)

### 5.4 Scope решения
**Принято Mavis (2026-06-17):** «Серверная бизнес-логика + сетевой хаб» (Cargo уже в составе Trade v2). UI трюма — отдельная задача T-Cargo-UI после завершения интеграции.

---

## 6. Открытые вопросы (нужны ответы)

1. **Ownership корабля:** `MarketZone` принимает `TryLoadToShip` от любого клиента в зоне? Или есть проверка "клиент = пилот корабля"? Сейчас НЕТ проверки (recon не нашёл).
2. **ShipClass источник:** где `ShipController` берёт свой `ShipClass` (Light/Medium/HeavyI/HeavyII)? Сейчас — из `cargoComp.shipClass` (legacy), но cargoComp может быть null. Нужен `ShipDefinition` SO или новый компонент `ShipClassMarker`.
3. **Кооп-пилотирование и груз:** если 2 пилота в одном корабле — оба могут грузить? Сейчас да, без проверок.
4. **`Key-subsystem/00_OVERVIEW.md:84`:** "Cargo-операции не требуют ключа" — это правда? Если у `MarketZone` нет проверки ключа, то ДА (по факту кода). Но это противоречит идее владения кораблём.
5. **Столкновения (leak/fragile):** где должны срабатывать? На `ShipController.OnCollisionEnter`? Какой порог энергии? GDD_25 секция 4.3 — нужна конкретика.

---

## 7. Сводка рисков

| Риск | Вероятность | Влияние | Митигация |
|---|---|---|---|
| Удалить `CargoSystem` до того, как ShipController не научится читать с сервера | Средняя | Высокое | Делаем поэтапно: сначала серверный хук, потом UI, потом удаление. Сцена `WorldScene_0_0.unity` чиним через MCP `manage_scene` |
| `MarketZone` сломается без `cargoComp.shipClass` | Высокая | Среднее | Заменяем на `ShipClassMarker` ДО удаления CargoSystem |
| Регрессия `MarketServer` snapshot | Низкая | Высокое | TradeWorld API не меняем, только добавляем |
| `ShipController` physics рассинхрон | Средняя | Высокое | Серверный `cargoPenalty` через NetworkVariable<float>, клиент только читает |

---

## 7.5. T-CARGO-06: Per-instance лимиты + модули (дополнение 2026-06-17)

После Этапов 1-5 (полный рефакторинг legacy `CargoSystem.cs`) по запросу пользователя добавил **per-instance лимиты + модульное расширение трюма**. Архитектурное решение — лимиты НЕ живут в статическом switch'е `ShipClassLimits.Get(cls)`, а в **Inspector-editable полях `ShipController`** (per-instance).

### Что добавлено

| Файл | Назначение |
|---|---|
| `Assets/_Project/Scripts/Ship/ShipCargoRegistry.cs` | Static `Dictionary<ulong, ShipController>` — мост от server-POCO `TradeWorld` к per-instance лимитам. `OnNetworkSpawn` register, `OnNetworkDespawn` unregister |
| `ShipModule.cs` (modified) | 4 новых поля cargo-бонусов (flat): `cargoSlotsBonus/WeightBonus/VolumeBonus/PenaltyReduction` |
| `ShipModuleManager.cs` (modified) | 4 новых метода `GetCargoXxxBonus()` — суммируют бонусы со всех занятых слотов |
| `ShipController.cs` (modified) | 4 base-поля (`baseMaxCargoSlots/Weight/Volume/PenaltyFactor`) + `GetEffectiveCargoLimits()` (base + bonuses) |
| `TradeWorld.cs` (modified) | `TryLoadToShip` — pre-check через `ShipCargoRegistry`. `GetSpeedPenalty` — читает effective limits |
| `MarketZone.cs` (modified) | `sc.GetEffectiveCargoLimits()` вместо `ShipClassLimits.Get(cls)` |
| `Assets/_Project/Data/Ship/Modules/MODULE_CARGO_BAY_01.asset` | Тестовый модуль: +6 слотов, +50кг, +2м³, -0.02 penalty |

### Архитектурный принцип (D11-D13)

| Решение | Почему |
|---|---|
| **D11**: per-instance лимиты в ShipController | «Лёгкий с большим хранилищем» — конкретный кейс, который не покрывает статический switch по классу. Меняется в инспекторе конкретного префаба |
| **D12**: `ShipCargoRegistry` static | POCO `TradeWorld` не может хранить ссылки на `MonoBehaviour`. Registry — мост (register/unregister в `OnNetworkSpawn`/`OnNetworkDespawn`) |
| **D13**: cargo-бонусы в `ShipModule` flat | Модули stackable (Q-06.2). Penalty reduction отрицательный = уменьшение штрафа. `ShipClassLimits` остаётся **fallback** если корабль не зарегистрирован в registry |

### Тест-результат (cold test через `execute_code`)

```
slotsCount_after_add=1 slot.isOccupied=True
SlotsBonus=6 (expect 6)
WeightBonus=50 (expect 50)
VolBonus=2 (expect 2)
PenaltyRed=-0,02 (expect -0,02)
```

Все 4 бонуса корректно читаются через `ShipModuleManager.GetCargoXxxBonus()`.

### Сценарий проверки в Play Mode

1. Открыть `WorldScene_0_0.unity`, выбрать `Ship_Light_root`
2. Inspector → секция `Cargo (T-CARGO-06, базовые лимиты)` — увеличить `Base Max Cargo Weight: 100 → 500`
3. Save, Play, Start Host
4. Открыть `MarketWindow` → выбрать `Ship_Light_root` → `maxWeight` покажет 500
5. (Опц.) Перетащить `MODULE_CARGO_BAY_01` в Utility-слот корабля → `maxWeight` станет 550

### Что НЕ делал (явно out of scope T-CARGO-06)

- ❌ Не делал UI для управления модулями (уже есть отдельный план)
- ❌ Не делал валидацию «модуль не должен быть уже установлен в 2 слота» (current logic: `ModuleSlot.isOccupied` + manager-уровневые проверки)
- ❌ Не делал визуальное отображение bonus-модулей в HUD

---

## 8. Связанные документы (прочитать перед планом)

- [CARGO_REFACTOR_PLAN_2026-06-17.md](CARGO_REFACTOR_PLAN_2026-06-17.md) — **собственно план** (следующий шаг)
- `docs/dev/INVENTORY_V2_REFACTOR.md` — образец поэтапного v2-рефакторинга
- `docs/dev/CONTRACT_V2_MIGRATION.md` — образец переноса подсистемы в v2
- `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` — что сцена сейчас содержит
- `docs/Ships/roadmap-integration.md` — roadmap (нужно обновить, см. §2)
- `docs/gdd/GDD_25_Trade_Routes.md` — дизайн-источник (секции 4.1, 4.3, 4.4)
- `unity-v2-subsystem-migration` skill — канонический паттерн
- `project-c-netcode-patterns` skill — §24-26 (scene-placed spawn, deferred init, server-authoritative state)
