# 11 — Visual Markers: DockPadVisualMarker v2

> **Статус (2026-07-12):** 🔬 **Анализ завершён.** План утверждён, ожидает реализацию.
> **Сессия:** 2026-07-12 (Aura, profile `project-c`)
> **Тикет:** `T-DOCK-14` — полная переработка визуальных маркеров падов.
> **Связанные доки:** `00_README.md` (Known issues), `06_ROADMAP.md` (T-DOCK-14), `ARCHITECTURE.md`.

---

## TL;DR

`DockPadVisualMarker` (MVP-заглушка) никогда не работал корректно:
- Плоский цветной Quad, только 2 цвета (free/occupied)
- Детекция через `Physics.OverlapSphere(10f)` — не находит корабли
- Не знает про assignments, NPC, статус диспетчера
- Не `NetworkBehaviour` — нет доступа к серверной правде

**Цель v2:** 5 визуальных состояний с holographic-эффектами, полная интеграция с системой докинга, сетевая синхронизация.

---

## 1. Анализ текущего состояния

### 1.1 Что есть сейчас

| Компонент | Файл | Строк | Роль |
|-----------|------|-------|------|
| `DockPadVisualMarker` | `Stations/DockPadVisualMarker.cs` | 164 | Создаёт Quad + Unlit/Color материал, каждые 0.5с проверяет `OverlapSphere` |
| `DockingPadTriggerBox` | `Stations/DockingPadTriggerBox.cs` | 98 | Триггер-зона, `IsShipInside` (bool), отправляет `NotifyTouchedDownRpc` |
| `PadTriggerReference` | `Stations/PadTriggerReference.cs` | 26 | Мини-маркер для внешних систем |
| `DockingWorld` | `Core/DockingWorld.cs` | 524 | Server-only SOT: `_occupiedPads`, `_pendingByClient`, `_assignmentsByClient` |
| `DockingServer` | `Network/DockingServer.cs` | 346 | Server hub: RPCs для запросов/подтверждений/отстыковки |
| `DockingClientState` | `Client/DockingClientState.cs` | ~200 | Client singleton: статус локального игрока |
| `DockingZoneRegistry` | `Network/DockingZoneRegistry.cs` | ~80 | Static реестр станций |

### 1.2 Корневые причины неработоспособности

1. **Детекция:** `Physics.OverlapSphere(center, 10f)` — радиус 10м недостаточен для поиска крупных кораблей, плюс проблема слоёв (коллайдеры кораблей на других layers).
2. **2 состояния вместо 6:** Система различает `Idle/Assigned/Docked/Cancelled/WrongPad` + NPC vs Player, но маркер видит только «есть корабль / нет корабля».
3. **Нет сетевой синхронизации:** `DockingWorld` — server-only. Клиент не знает реальной занятости падов.
4. **Примитивный визуал:** Плоский Quad без анимации, свечения, depth.

### 1.3 Структура префаба Pad

```
Pad_01 (GameObject)
├── BoxCollider (trigger)
├── DockingPadTriggerBox
├── PadTriggerReference
├── DockPadVisualMarker (тот самый)
├── Pad_01 (TMP label — дочерний)
└── Empty_visual (пустой дочерний GameObject)
```

Pad — **не** `NetworkObject`. Он child станции (`DockStationController`), которая `NetworkObject`.

---

## 2. Архитектура v2

### 2.1 Главное решение: как синхронизировать состояние

**Проблема:** Пады — children станции, не могут быть `NetworkBehaviour` напрямую (NGO требует `NetworkObject` на том же GameObject).

**Решение: `PadStateSync` — новый `NetworkBehaviour` на корне станции**, который держит `NetworkList<PadStateEntry>` со всеми падами. Каждый `DockPadVisualMarker` (остаётся `MonoBehaviour`) читает состояние из `PadStateSync` через parent.

```
DockStationController (NetworkObject)
└── PadStateSync (NetworkBehaviour)          ← НОВЫЙ
    └── NetworkList<PadStateEntry>           ← синхронизируется сервер→клиенты
        ├── [0] padId="PAD-001", IsOccupied, OccupiedBy, IsPending
        ├── [1] padId="PAD-002", ...
        └── [N] padId="PAD-00N", ...

Pad_01 (MonoBehaviour, child)
├── DockingPadTriggerBox (.IsShipInside — локальный флаг)
└── DockPadVisualMarker ← читает PadStateSync + DockingClientState
```

**Альтернатива (отвергнута):** делать каждый пад отдельным `NetworkObject` — overhead сети (лишние `NetworkObject` на каждом паду, усложняет `ScenePlacedObjectSpawner`).

### 2.2 Поток данных

```
СЕРВЕР:
DockingWorld.ConfirmAssignment()
  → PadStateSync.UpdatePadState(padId, occupied:true, clientId)
  → NetworkList обновляется → автосинхронизация на клиенты

КЛИЕНТ:
DockPadVisualMarker.LateUpdate()
  → читает PadStateSync.GetState(padId)
  → сверяет с DockingClientState.Instance (мой ли пад?)
  → определяет PadVisualState
  → обновляет материал/анимацию
```

### 2.3 Состояния визуального маркера

```csharp
public enum PadVisualState
{
    Neutral,        // серый/белый тусклый — пад вне зоны внимания
    Free,           // зелёный/бирюзовый, мягкое пульсирование — можно запросить
    Pending,        // жёлтый/янтарный — ждёт подтверждения (другого игрока)
    AssignedToMe,   // синий/голубой, активное свечение + ring — МОЙ пад
    AssignedOther,  // жёлтый steady — зарезервирован для другого
    OccupiedNpc,    // оранжевый — NPC на паду
    OccupiedPlayer, // красный steady — игрок на паду
    WrongPad        // красный pulsing — сел не на свой (кратковременно)
}
```

---

## 3. Milestones

### M-MARKER-1: Сетевая синхронизация состояния падов
**Тикеты:** T-DOCK-14a, T-DOCK-14b
**Оценка:** ~2.5 часа

### M-MARKER-2: Визуальные ассеты (Shader Graph + материалы + ring mesh)
**Тикеты:** T-DOCK-14c
**Оценка:** ~2 часа

### M-MARKER-3: Новый `DockPadVisualMarker` + интеграция
**Тикеты:** T-DOCK-14d, T-DOCK-14e
**Оценка:** ~2.5 часа

### M-MARKER-4: Тестирование + документация
**Тикеты:** T-DOCK-14f
**Оценка:** ~1 час

**Всего:** 6 тикетов, ~8 часов.

---

## 4. Детальные тикеты

### T-DOCK-14a: `PadStateSync` — NetworkBehaviour синхронизации

**Milestone:** M-MARKER-1
**Оценка:** ~90 мин
**Зависит от:** ничего (новый компонент)

**Что делаем:**
- `Assets/_Project/Scripts/Docking/Stations/PadStateSync.cs` — новый `NetworkBehaviour`:
  ```csharp
  public struct PadStateEntry : INetworkSerializable
  {
      public string padId;
      public bool isOccupied;
      public ulong occupiedByClientId; // 0 = никто
      public bool isPending;
      public bool isAssigned;          // pending + confirmed
      public ulong assignedToClientId;
  }
  ```
- `NetworkList<PadStateEntry>` — автосинхронизация со всех клиентов в зоне
- Методы: `UpdatePadState(padId, ...)`, `GetState(padId)`, `ClearAll()`
- На сервере: инициализируется при `OnNetworkSpawn`, собирает пады через `GetComponentsInChildren<DockingPadTriggerBox>()`
- Регистрируется на корне `DockStationController` (через `RequireComponent` или ручное добавление)

**Acceptance:**
- Compile: 0 errors
- `PadStateSync` появляется на `DockStation_Primium` в сцене
- При `StartHost`: `NetworkList` содержит N записей (по числу падов)

---

### T-DOCK-14b: Интеграция `PadStateSync` с `DockingWorld`

**Milestone:** M-MARKER-1
**Оценка:** ~60 мин
**Зависит от:** T-DOCK-14a

**Что делаем:**
- В `DockingWorld.ConfirmAssignment()` → `PadStateSync.UpdatePadState(padId, isAssigned:true, clientId)`
- В `DockingWorld.ReleaseAssignment()` → `PadStateSync.UpdatePadState(padId, isAssigned:false, isOccupied:false)`
- В `DockingWorld.ScanExistingOccupants()` → `PadStateSync.UpdatePadState(padId, isOccupied:true, clientId)`
- В `DockingWorld.RegisterPendingAssignment()` → `PadStateSync.UpdatePadState(padId, isPending:true)`
- В `DockingWorld.CancelPendingAssignment()` → `PadStateSync.UpdatePadState(padId, isPending:false)`
- `PadStateSync` ищется через `DockingZoneRegistry` → `station.GetComponent<PadStateSync>()`
- Убрать старый `_occupiedPads` Dictionary не надо — это SOT сервера для бизнес-логики. `PadStateSync` — отдельный слой для клиентской визуализации.

**Acceptance:**
- Compile: 0 errors
- После `RequestDocking` → `Confirm`: клиент видит обновлённый `PadStateEntry.isAssigned = true`
- После `ReleaseAssignment`: `isAssigned = false, isOccupied = false`

---

### T-DOCK-14c: Holographic Pad Material + Ring Mesh

**Milestone:** M-MARKER-2
**Оценка:** ~120 мин
**Зависит от:** ничего (ассеты можно делать параллельно)

**Что делаем:**

#### 14c-1: Shader Graph — `HolographicPad`
- URP Shader Graph: `Assets/_Project/Shaders/HolographicPad.shadergraph`
- Properties: `_BaseColor`, `_EmissionColor`, `_EmissionStrength`, `_GridDensity`, `_ScrollSpeed`, `_RingThickness`
- Прозрачный (Surface = Transparent, Blend = Alpha)
- Radial grid (процедурная генерация через UV-polar coordinates)
- Scrolling effect (вращение grid линий)
- Emission + Fresnel edge glow

#### 14c-2: Ring Mesh
- `Assets/_Project/Meshes/PadRing.fbx` — torus/ring (внешний диаметр = размеру пада, тонкий)
- Сгенерировать через `generate_model_from_text` или создать в редакторе ProBuilder

#### 14c-3: Материалы (5 штук)
- `Assets/_Project/Materials/Docking/M_Pad_Free.mat`
- `Assets/_Project/Materials/Docking/M_Pad_AssignedToMe.mat`
- `Assets/_Project/Materials/Docking/M_Pad_Occupied.mat`
- `Assets/_Project/Materials/Docking/M_Pad_Pending.mat`
- `Assets/_Project/Materials/Docking/M_Pad_Neutral.mat`

Каждый — инстанс `HolographicPad` с разными цветами/параметрами.

#### 14c-4: Particle Prefab (AssignedToMe)
- `Assets/_Project/Prefabs/VFX/Pad_AssignedGuide.prefab`
- 4 угловых маркера (мелкие частицы по периметру) + направляющая стрелка вверх

**Acceptance:**
- Shader компилируется без ошибок
- Материалы отображаются в Scene View с holographic-эффектом
- Ring mesh корректного размера для стандартного пада

---

### T-DOCK-14d: `DockPadVisualMarker` v2 — полная переработка

**Milestone:** M-MARKER-3
**Оценка:** ~90 мин
**Зависит от:** T-DOCK-14a, T-DOCK-14b, T-DOCK-14c

**Что делаем:**
- Полный rewrite `DockPadVisualMarker.cs`
- Убрать `OverlapSphere`, `Update()` с таймером, `BuildMarker()` с Quad
- Новая архитектура:

```csharp
public class DockPadVisualMarker : MonoBehaviour
{
    // Инспектор
    [SerializeField] private Material neutralMat;
    [SerializeField] private Material freeMat;
    [SerializeField] private Material pendingMat;
    [SerializeField] private Material assignedToMeMat;
    [SerializeField] private Material assignedOtherMat;
    [SerializeField] private Material occupiedNpcMat;
    [SerializeField] private Material occupiedPlayerMat;
    
    // Визуальные объекты (создаются в Awake)
    private GameObject _padSurface;   // диск с HolographicPad материалом
    private GameObject _padRing;      // ring по периметру
    private GameObject _guideParticles; // particle effect (только AssignedToMe)
    
    // Runtime state
    private PadStateSync _stateSync;
    private DockingPadTriggerBox _padBox;
    private PadVisualState _currentState;
    
    // Свойства анимации
    private float _pulsePhase;
    private MaterialPropertyBlock _props;
    
    void Awake()  → BuildVisuals() (создаёт _padSurface + _padRing)
    void Start()  → кеширует _stateSync из GetComponentInParent<PadStateSync>()
    void LateUpdate() → ReadState() → UpdateVisuals()
}
```

- **ReadState():** логика определения `PadVisualState`:
  1. `_padBox.IsShipInside` → `OccupiedPlayer` / `OccupiedNpc` (в зависимости от `OccupiedByClientId`)
  2. `PadStateSync.GetState(padId)`:
     - `isAssigned && assignedTo == localClientId` → `AssignedToMe`
     - `isAssigned && assignedTo != localClientId` → `AssignedOther`
     - `isPending` → `Pending`
  3. Иначе → `Free`

- **UpdateVisuals():** применение материала через `MaterialPropertyBlock`, анимация через `_pulsePhase`:
  - `Free`: мягкое пульсирование emission (sin wave)
  - `AssignedToMe`: активный scrolling ring + частицы + повышенная emission
  - `Occupied*`: статичный цвет, без анимации
  - `Pending`: слабое жёлтое пульсирование

**Acceptance:**
- Compile: 0 errors
- Маркер корректно показывает все 7 состояний
- При изменении состояния на сервере → клиент видит обновление в течение ~1 кадра

---

### T-DOCK-14e: Обновление префаба `Pad_01` + расстановка в сцене

**Milestone:** M-MARKER-3
**Оценка:** ~60 мин
**Зависит от:** T-DOCK-14d

**Что делаем:**
- Обновить `Assets/_Project/Prefabs/NPC_ZONES/Pad_01.prefab`:
  - Оставить `DockingPadTriggerBox`, `PadTriggerReference`
  - Заменить `DockPadVisualMarker` на новую v2 версию с назначенными материалами
  - Убрать старый `_PadMarker` (Quad) — больше не создаётся
  - TMP label обновить (новый стиль, шрифт)
- Обновить все `Pad_*` в `WorldScene_0_0.unity` (reimport prefab)
- Добавить `PadStateSync` на `DockStation_Primium`
- Обновить `PortStationCreator.cs` (Editor-скрипт) для создания падов с v2 маркером

**Acceptance:**
- Префаб `Pad_01.prefab` открывается без ошибок
- `DockStation_Primium` в `WorldScene_0_0.unity` имеет `PadStateSync` компонент
- При `StartHost`: все пады отображают правильные holographic-маркеры

---

### T-DOCK-14f: Smoke test + документация

**Milestone:** M-MARKER-4
**Оценка:** ~60 мин
**Зависит от:** T-DOCK-14e

**Что делаем:**
- Проверить сценарий: `StartHost` → подлететь к Примуму → `T` → «Запросить посадку» → подтвердить → визуальный маркер меняется на `AssignedToMe` → приземлиться → маркер `OccupiedPlayer` → отстыковка → маркер `Free`
- Проверить NPC: NPC-корабль на паду → маркер `OccupiedNpc`
- Проверить WrongPad: запросить один пад, сесть на другой → маркер `WrongPad`
- Обновить `docs/Docking_stations/00_README.md` (Known issues)
- Обновить `docs/Docking_stations/06_ROADMAP.md` (T-DOCK-14 → ✅)
- Обновить `docs/Docking_stations/CHANGELOG.md`

**Acceptance:**
- Все сценарии работают без ошибок
- Документация обновлена
- Compile: 0 errors

---

## 5. Граф зависимостей

```
T-DOCK-14a (PadStateSync)
  ↓
T-DOCK-14b (DockingWorld + PadStateSync integration)
  ↓
T-DOCK-14c (Shader + Materials + Ring Mesh)  ← параллельно с 14a/14b
  ↓
T-DOCK-14d (DockPadVisualMarker v2)
  ↓
T-DOCK-14e (Prefab + Scene update)
  ↓
T-DOCK-14f (Smoke test + Docs)
```

---

## 6. Файлы, которые будут изменены/созданы

| Файл | Действие | Тикет |
|------|----------|-------|
| `Assets/_Project/Scripts/Docking/Stations/PadStateSync.cs` | **Создать** | T-DOCK-14a |
| `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` | **Изменить** (добавить вызовы PadStateSync) | T-DOCK-14b |
| `Assets/_Project/Shaders/HolographicPad.shadergraph` | **Создать** | T-DOCK-14c |
| `Assets/_Project/Materials/Docking/M_Pad_*.mat` (5 шт.) | **Создать** | T-DOCK-14c |
| `Assets/_Project/Meshes/PadRing.fbx` | **Создать** | T-DOCK-14c |
| `Assets/_Project/Scripts/Docking/Stations/DockPadVisualMarker.cs` | **Переписать** | T-DOCK-14d |
| `Assets/_Project/Prefabs/NPC_ZONES/Pad_01.prefab` | **Обновить** | T-DOCK-14e |
| `Assets/_Project/Scripts/Editor/PortStationCreator.cs` | **Обновить** | T-DOCK-14e |
| `docs/Docking_stations/00_README.md` | **Обновить** | T-DOCK-14f |
| `docs/Docking_stations/06_ROADMAP.md` | **Обновить** | T-DOCK-14f |
| `docs/Docking_stations/CHANGELOG.md` | **Обновить** | T-DOCK-14f |

---

*Документ создан: 12 июля 2026 | Агент: Aura | План утверждён, реализация — следующим шагом.*
