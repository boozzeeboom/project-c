# T-CARGO-VIS-01: 3D визуал наполнения трюма (ящики/блоки)

> **Статус:** ✅ Реализовано 2026-07-02
> **Дата:** 2026-07-02
> **Факт:** ~3 ч, 1 файл (additive-only)
> **Эпик:** #3 по roadmap [CARGO_REMAINING_WORK_2026-07-02.md](CARGO_REMAINING_WORK_2026-07-02.md)
> **Оценка:** ~4-6 ч (2-3 тикета)

---

## 1. Задача (Problem Statement)

Cargo — «голые данные» в `TradeWorld._cargoCache`. На палубе/в трюме корабля визуально ничего нет. Игрок видит пустой корабль, хотя в cargo может лежать 5 т руды. Нужен 3D-визуал: ящики/блоки внутри трюма, количество которых соответствует `cargoUsed`.

---

## 2. Архитектурное решение

### 2.1 Data Flow

```
TradeWorld (server)
  ├─ TryLoadToShip / TryUnloadFromShip / TryDamageCargo
  ├─ OnCargoChanged?.Invoke(shipNetId)
  └─ ShipController.RecalculateCargoPenalty(shipNetId)
       └─ UpdateTelemetryState() → NetworkVariable<ShipTelemetryState>.Value = newState
            └─ NGO sync → ShipTelemetryClientState._allShips[shipNetId] updated
                 └─ ShipTelemetryClientState.OnShipStateChanged?.Invoke(shipNetId)
                      └─ ShipCargoVisual.OnShipStateChanged(shipNetId) ← НАШ КОД
                           └─ RefreshVisual(cargoUsed, cargoMax)
```

**Источник данных для визуала:** `ShipTelemetryClientState.GetShipState(shipNetId)?.cargoUsed` — client-side, работает для **всех** кораблей (не только своего), синхронизируется через существующий `NetworkVariable<ShipTelemetryState>` (5 Hz).

**Почему не `TradeWorld.OnCargoChanged` напрямую?** `TradeWorld` — server-side singleton; клиент не имеет доступа. Клиент получает `cargoUsed`/`cargoMax` через telemetry — это уже работает и не требует новых RPC.

### 2.2 Компонент: `ShipCargoVisual`

```
ShipCargoVisual : MonoBehaviour
├─ [SerializeField] BoxCollider _spawnZone        ← invisible trigger, defines spawn volume
├─ [SerializeField] GameObject[] _boxPrefabs       ← random visual per box (≥1 required)
├─ [SerializeField] float _boxBaseSize = 0.5f      ← size of one box (auto-calc if 0)
├─ [SerializeField] int _maxVisibleBoxes = 50      ← cap for performance
├─ [SerializeField] bool _showOverflowIndicator = true
├─ [SerializeField] GameObject _overflowPrefab     ← red blinking box (optional)
│
├─ ulong _shipNetId                                ← resolved from ShipRootReference
├─ int _currentBoxCount = 0
├─ List<GameObject> _spawnedBoxes                   ← object pool (active + inactive)
├─ Stack<GameObject> _pool                          ← recycled inactive boxes
│
├─ Awake()   → ResolveShipNetId() via GetComponentInParent<ShipRootReference>()
├─ OnEnable()  → Subscribe to ShipTelemetryClientState.OnShipStateChanged
├─ OnDisable() → Unsubscribe
│
├─ OnShipStateChanged(ulong shipNetId)
│    └─ if (shipNetId == _shipNetId) → RefreshVisual(cargoUsed)
│
└─ RefreshVisual(int targetBoxCount)
     ├─ targetBoxCount = Mathf.Min(targetBoxCount, _maxVisibleBoxes)
     ├─ Incremental update: spawn extra or return to pool (no full rebuild)
     ├─ PlaceBox(boxIndex) → position within _spawnZone.bounds (grid, bottom→top)
     ├─ Random prefab from _boxPrefabs[]
     └─ If targetBoxCount > _maxVisibleBoxes → show overflow indicator
```

### 2.3 Алгоритм размещения (Grid Layout)

```
Вход: _spawnZone (BoxCollider bounds), _boxBaseSize, boxIndex, totalBoxes

1. Вычисляем grid:
   - cols = floor(bounds.size.x / _boxBaseSize)
   - rows = floor(bounds.size.z / _boxBaseSize)
   - maxLayers = floor(bounds.size.y / _boxBaseSize)
   - Если _boxBaseSize = 0 → auto-calc: boxSize = min(bounds.size) / sqrt(maxSlots)

2. Позиция ящика #N:
   - layer = N / (cols * rows)            ← снизу вверх
   - remainder = N % (cols * rows)
   - col = remainder % cols
   - row = remainder / cols
   - localPos = bounds.min + (col+0.5)*boxSize*right + (layer+0.5)*boxSize*up + (row+0.5)*boxSize*forward

3. Каждый ящик:
   - localScale = Vector3.one * (_boxBaseSize * 0.9f)  ← 10% gap for visual separation
   - parent = transform (this GameObject)
```

### 2.4 Object Pool

Вместо `Instantiate`/`Destroy` на каждое изменение — пул:

```
Получить ящик:
  if (_pool.Count > 0) → pop, SetActive(true)
  else → Instantiate(randomPrefab, parent: transform)

Вернуть в пул:
  SetActive(false), push в _pool
```

Полная перестройка только при загрузке сцены/спавне корабля. Инкрементальное обновление — при каждом `OnShipStateChanged`.

### 2.5 Overflow Indicator

Когда `cargoUsed > cargoMax` — показываем красный мигающий ящик поверх стопки:
- `_overflowPrefab` — отдельный префаб (или тот же ящик с красным материалом)
- Мигание: `Mathf.PingPong(Time.time * 3f, 1f)` → alpha вкл/выкл через CanvasGroup или material color
- Позиция: над последним слоем, по центру

---

## 3. Инспектор (Inspector Fields)

| Поле | Тип | Описание |
|------|-----|----------|
| `_spawnZone` | `BoxCollider` | Техническая зона спавна (IsTrigger=true, невидимый). Определяет границы генерации ящиков. |
| `_boxPrefabs` | `GameObject[]` | Массив префабов ящиков. Минимум 1. Выбираются случайно. Для теста — обычный 3D Cube. |
| `_boxBaseSize` | `float` | Размер одного ящика в метрах. 0 = авто-расчёт по объёму колайдера / cargoMax. Default: 0.5 |
| `_maxVisibleBoxes` | `int` | Лимит отображаемых ящиков (perf). Default: 50 (HeavyII = 30 slots + margin). |
| `_showOverflowIndicator` | `bool` | Показывать красный мигающий индикатор при перегрузе. |
| `_overflowPrefab` | `GameObject` | Префаб для overflow-индикатора (null = использовать тот же бокс с красным tint). |

---

## 4. Placement в иерархии корабля

```
ShipRoot (ShipController + ShipRootReference + NetworkObject + Rigidbody)
├── ShipModel (визуальный меш корабля — корпус, палуба, трюм)
├── PilotSeat
├── ShipCargoConsole (уже есть — T-CARGO-UI-02)
├── ShipCargoVisual (НОВЫЙ)               ← этот GO
│   ├── BoxCollider (_spawnZone)           ← невидимый, определяет зону
│   └── [runtime] CargoBox_00, CargoBox_01, ... (spawned children)
└── ...
```

- `ShipCargoVisual` — отдельный дочерний GO, позиционируется дизайнером внутри модели трюма
- `BoxCollider` на этом же GO (или дочернем `SpawnZone`)
- Все спавнящиеся ящики — children `ShipCargoVisual.transform` → автоматически двигаются с кораблём

---

## 5. Файлы

### Новые (1 файл)

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Scripts/Ship/Cargo/ShipCargoVisual.cs` | MonoBehaviour: подписка на telemetry, grid-размещение, object pool |

### Изменяемые (0 файлов)

**Additive-only.** Новый компонент, не трогает существующий код. Подключается через инспектор.

### Ресурсы (префабы)

| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Prefabs/Cargo/Box_Default.prefab` | Тестовый префаб: Cube (scale 0.45×0.45×0.45) +棕色 Material + BoxCollider |

---

## 6. План реализации (тикеты)

### Тикет 1: Компонент ShipCargoVisual (ядро) — 2-3 ч

- Создать `ShipCargoVisual.cs` в `Assets/_Project/Scripts/Ship/Cargo/`
- `ResolveShipNetId()`: поиск `ShipRootReference` в родителях (GetComponentInParent)
- Подписка на `ShipTelemetryClientState.OnShipStateChanged` (OnEnable/OnDisable)
- `RefreshVisual(int targetCount)`:
  - Сравнить с `_currentBoxCount`
  - Инкрементально добавить/убрать ящики через pool
- `CalculateBoxPosition(int index, int total)`: grid-алгоритм (см. §2.3)
- Object pool: `GetPooledBox()` / `ReturnToPool(GameObject)`
- Overflow-логика (если target > _maxVisibleBoxes)
- Авто-расчёт `_boxBaseSize` если = 0

### Тикет 2: Тестовый префаб + ручная настройка на корабле — 1-2 ч

- Создать `Box_Default.prefab`: Cube primitive → сохранить как prefab в `Assets/_Project/Prefabs/Cargo/`
- Настроить `ShipCargoVisual` на корабле в сцене:
  - Добавить дочерний GO «ShipCargoVisual»
  - Добавить `BoxCollider` (размер = внутренний объём трюма, IsTrigger=true)
  - Заполнить `_boxPrefabs[0]` = Box_Default
  - Проверить в Play Mode: загрузить cargo → ящики появляются

### Тикет 3: Overflow indicator + полировка — 1 ч

- Реализовать мигание overflow-ящика
- Edge cases: cargo=0 → все ящики в пуле, cargoUsed меняется быстро (throttle), disable/enable компонента

---

## 7. Edge Cases

| Случай | Поведение |
|--------|-----------|
| `cargoUsed = 0` | Все ящики возвращаются в пул, ни одного активно. |
| `cargoUsed > _maxVisibleBoxes` | Показываем `_maxVisibleBoxes` ящиков + overflow indicator (мигающий красный). |
| `_boxPrefabs` пуст | `Debug.LogError`, визуал не работает. |
| `_spawnZone` не назначен | `Debug.LogWarning`, визуал не работает. |
| `ShipRootReference` не найден | `Debug.LogWarning`, компонент сам себя выключает (`enabled = false`). |
| `ShipTelemetryClientState.Instance == null` | Клиент ещё не подключился — ждём, ре-подписка при следующем OnEnable. |
| Корабль не в кэше telemetry | `GetShipState()` возвращает null → визуал показывает 0 ящиков. |
| Быстрое изменение cargo (спам) | Инкрементальный pool — не спавним/уничтожаем каждый раз, только Δ. |
| Компонент на префабе (не в сцене) | Awake не резолвит _shipNetId до instantiate в сцену → `Start()` или lazy init. |

---

## 8. Что НЕ входит (Out of Scope)

- ❌ Per-itemId визуалы (разные ящики для руды vs дерева) — **фаза 2**, когда появятся artist-префабы
- ❌ Анимация появления/исчезновения ящиков — мгновенный спавн/деактивация
- ❌ Синхронизация поворота/физики ящиков через сеть — чисто client-side visual
- ❌ LOD/оптимизация для дальних кораблей — все корабли показывают ящики одинаково
- ❌ Интеграция с NPC cargo (T-CARGO-NPC-01) — будет работать автоматически, когда NPC-корабли получат реальный cargo в `TradeWorld._cargoCache`

---

## 9. Verification Checklist

```bash
# 1. Compile
# → Unity Editor → Console → 0 errors

# 2. Play Mode Test
# → Open BootstrapScene
# → Start Host
# → Spawn player ship с ShipCargoVisual
# → Открыть ShipCargoConsole (F), загрузить 5 ящиков
# → Наблюдать: 5 boxes появляются внутри _spawnZone
# → Загрузить ещё 5 → 10 boxes, grid заполняется снизу вверх
# → Выгрузить 3 → 7 boxes (лишние в пуле)
# → Загрузить до перегруза (cargoUsed > cargoMax) → overflow indicator мигает

# 3. Multi-ship Test
# → Spawn 2 разных корабля с разным cargo
# → Каждый показывает своё количество ящиков независимо
```

---

## 10. Связанные документы

- [CARGO_REMAINING_WORK_2026-07-02.md](CARGO_REMAINING_WORK_2026-07-02.md) — родительский план
- [CARGO_UI_01_DESIGN_2026-07-02.md](CARGO_UI_01_DESIGN_2026-07-02.md) — T-CARGO-UI-01 (telemetry source)
- [CARGO_UI_02_PLAN.md](CARGO_UI_02_PLAN.md) — T-CARGO-UI-02 (cargo manager)
- `Assets/_Project/Scripts/Ship/Client/ShipTelemetryClientState.cs` — источник данных
- `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` — payload (cargoUsed/cargoMax)
- `Assets/_Project/Scripts/Ship/ShipRootReference.cs` — привязка к корню корабля
- `Assets/_Project/Scripts/Player/ShipController.cs` — ShipController (владелец telemetry)
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — источник истины cargo (OnCargoChanged)
