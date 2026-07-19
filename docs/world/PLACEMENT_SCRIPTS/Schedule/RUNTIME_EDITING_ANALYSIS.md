# Runtime Schedule Editing — анализ

**Дата:** 2026-07-18  
**Контекст:** NpcShipScheduleOverviewWindow v2 завершён. Заказчик спрашивает: «что если нужно будет видеть все маршруты и править их рантайм когда сервер работает?»

---

## 1. Текущая архитектура (as-is)

### 1.1. Data flow

```
[NpcShipSchedule.asset]  ──editor-only──>  [NpcShipController.schedule]  ──OnNetworkSpawn──>  [NpcShipWorld.RegisterNpc]
                                                     │                                              │
                                                     │                                              ├─ _npcByInstanceId[id] = state
                                                     │                                              └─ _scheduleByNpcInstanceId[id] = schedule (SO ref)
                                                     │
                                              [Runtime: read-only]
```

**Ключевые факты:**

| Слой | Что хранится | Мутабельность |
|------|-------------|---------------|
| `NpcShipSchedule.asset` | SO на диске | Только Editor |
| `NpcShipController.schedule` | Ссылка на SO (SerializeField) | Только Editor |
| `NpcShipWorld._scheduleByNpcInstanceId` | `Dictionary<ulong, NpcShipSchedule>` — те же SO-ссылки | Read-only в рантайме |
| `NpcShipState.CurrentRoute` | **Копия** `schedule.routes[idx]` (struct by-value) | Мутабельна (меняется через `AdvanceScheduleIndex`) |

**Вывод:** NpcShipSchedule SO в рантайме используется **только для чтения** — данные копируются в `NpcShipState.CurrentRoute` при регистрации и при `AdvanceScheduleIndex`. Сам SO никогда не мутирует в рантайме.

### 1.2. Где schedule потребляется в рантайме

| Потребитель | Что читает | Когда |
|------------|-----------|-------|
| `NpcShipWorld.RegisterNpc` | `schedule.routes[0]` → `state.CurrentRoute` | При спавне NPC |
| `NpcShipWorld.AdvanceScheduleIndex` | `schedule.routes[idx]`, `schedule.scheduleType` | Каждый цикл маршрута |
| `NpcShipWorld.TickNpc` (Docked) | `schedule.minDwellTimeSec`, `schedule.maxDwellTimeSec` | Каждый кадр в Docked |
| `NpcShipController.NavTick` (Docked → ResolveDwellTime) | `schedule.minDwellTimeSec`, `schedule.maxDwellTimeSec` | При входе в Docked |
| `NpcShipController.RunDwellCargoTrade` | `schedule.GetOrInitCargoTrade()` | Один раз за docking |
| `NpcShipWorld.TickNpc` (Loading) | `schedule.maxDwellTimeSec` | При расчёте loading time |

### 1.3. Что такое `NpcShipRoute` (struct)

```csharp
[Serializable]
public struct NpcShipRoute
{
    public string fromLocationId;
    public string toLocationId;
    public float dwellTimeSec;
    public float dwellRandomAddMinSec;
    public float dwellRandomAddMaxSec;
    public float flightDurationSec;
    public ShipFlightClass preferredShipClass;
    public NpcShipDemandCategory demandCategory;
}
```

**Копируется by-value** в `NpcShipState.CurrentRoute`. Изменение `State.CurrentRoute` влияет только на конкретный NPC, не на других NPC с тем же schedule.

### 1.4. Ограничения

1. **SO = editor-only.** В билде `NpcShipSchedule` не может быть изменён. `AssetDatabase` и `SerializedObject` недоступны.
2. **Нет network sync.** NpcShipWorld — обычный MonoBehaviour, не NetworkBehaviour. Нет NetworkVariable/NetworkList для schedule-данных.
3. **Копирование by-value.** `NpcShipState.CurrentRoute` — независимая копия. Изменение SO не аффектит уже летящих NPC до следующего `AdvanceScheduleIndex`.
4. **Нет runtime API для мутации.** Ни `NpcShipWorld`, ни `NpcShipController` не предоставляют методов «изменить schedule на лету».
5. **Нет persistence в билде.** В Editor можно сохранить SO через `EditorUtility.SetDirty`. В билде некуда сохранять.

---

## 2. Что нужно для runtime-редактирования

### 2.1. Три уровня сложности

| Уровень | Что даёт | Что требует |
|---------|---------|------------|
| **A — Editor Play-Mode** | Править SO в Editor во время Play Mode; изменения применяются при следующем спавне/цикле NPC | Ничего нового — уже работает через Inspector |
| **B — Runtime mirror + Server API** | Править расписания на лету через серверные команды; NPC перечитывают при следующем cycle | Новый `RuntimeSchedule` (сериализуемый struct/class), ServerRpc, NetworkVariable |
| **C — Full runtime system** | Расписания — полностью runtime-first, редактируются через админский UI, сохраняются в сейвы | Переработка всей системы: миграция с SO на данные, сетевая синхронизация, UI |

### 2.2. Уровень A — Editor Play-Mode (уже частично работает)

**Что уже есть:**
- `NpcShipScheduleOverviewWindow` может редактировать SO через `SerializedObject` → изменения сохраняются на диск
- В Play Mode: `RefreshSchedules()` перечитает изменённые SO из `AssetDatabase`
- `NpcShipController.OnNetworkSpawn` → `RegisterNpc` читает `schedule.routes[0]` свежим

**Что нужно доделать:**
- **Hot-reload для уже летящих NPC.** Сейчас `AdvanceScheduleIndex` читает `schedule.routes[idx]` при КАЖДОМ переходе. Если изменить `routes[]` в SO во время полёта — следующий `AdvanceScheduleIndex` подхватит изменения. Это УЖЕ работает для новых cycle.
- **Принудительный перезапуск цикла.** Кнопка «Apply to all active NPCs» → сброс `ScheduleIndex=0` + перечитывание `routes[0]`.
- **Проверка валидности.** Если новый `routes[]` короче текущего `ScheduleIndex` — сбросить индекс.

**Объём работ: ~2-4 часа.** Добавить в OverviewWindow кнопку «Apply to Runtime» (активна только в Play Mode), которая через `NpcShipWorld.Instance` итерирует всех NPC и сбрасывает `ScheduleIndex → 0`.

### 2.3. Уровень B — Runtime mirror + Server API

**Концепт:** создать рантайм-зеркало `NpcShipSchedule` внутри `NpcShipWorld`, которое можно мутировать через server-authoritative команды.

#### 2.3.1. RuntimeSchedule (сериализуемая struct)

```csharp
[Serializable]
public struct RuntimeScheduleData
{
    public string scheduleId;
    public string displayName;
    public NpcShipSchedule.ScheduleType scheduleType;
    public RuntimeRouteData[] routes;
    public float meanArrivalIntervalSec;
    public float arrivalIntervalStdDev;
    public float minArrivalSpacingSec;
    public float minDwellTimeSec;
    public float maxDwellTimeSec;
    public RuntimeCargoData cargoTrade;
}
```

Дублирует все поля `NpcShipSchedule` + `NpcShipRoute` как сериализуемые struct'ы (не SO).

#### 2.3.2. Где хранить

```csharp
// NpcShipWorld (server-only)
private Dictionary<string, RuntimeScheduleData> _runtimeSchedules; // scheduleId → data
private Dictionary<ulong, string> _npcScheduleId;                   // npcInstanceId → scheduleId
```

При `RegisterNpc`: копировать SO → `RuntimeScheduleData` в `_runtimeSchedules` (если ещё нет).  
При `AdvanceScheduleIndex`: читать из `_runtimeSchedules[scheduleId].routes[idx]`.

#### 2.3.3. Network sync

**Вариант 1 — Polling (проще).** Клиенты не хранят schedule-данные. Сервер шлёт `ClientRpc` только при изменении.

**Вариант 2 — NetworkVariable (структурированнее).** Новая `NetworkBehaviour`-компонента `NpcScheduleSync` на каждом NPC хранит:
- `NetworkVariable<RuntimeRouteData>` currentRoute — для UI на клиенте
- `NetworkList<RuntimeRouteData>` allRoutes — для отображения всего расписания

```csharp
public class NpcScheduleSync : NetworkBehaviour
{
    public NetworkVariable<RuntimeRouteData> CurrentRoute = new();
    public NetworkVariable<int> ScheduleIndex = new();
    public NetworkVariable<int> TotalRoutes = new();
    
    [ServerRpc]
    public void SetRouteServerRpc(int index, RuntimeRouteData newRoute) { ... }
}
```

#### 2.3.4. Mutation API

```csharp
// NpcShipWorld — server-authoritative
public void SetRoute(string scheduleId, int routeIndex, RuntimeRouteData newRoute);
public void AddRoute(string scheduleId, RuntimeRouteData newRoute);
public void RemoveRoute(string scheduleId, int routeIndex);
public void SetScheduleType(string scheduleId, NpcShipSchedule.ScheduleType newType);
public void ApplyToAllNpcs(string scheduleId); // force re-read для всех NPC с этим scheduleId
```

#### 2.3.5. UI layer

**Для Editor:** NpcShipScheduleOverviewWindow подключается к `NpcShipWorld.Instance` в Play Mode и шлёт ServerRpc через Editor-скрипт (возможно только если Editor = host/client).

**Для билда:** Нужен отдельный in-game UI (UGUI/UI Toolkit) — админская панель, доступная только серверу/хосту.

#### 2.3.6. Объём работ: ~3-5 дней
- Новые struct'ы: `RuntimeScheduleData`, `RuntimeRouteData` — 2-3 часа
- `NpcShipWorld` — переключение на `RuntimeScheduleData` вместо SO — 4-6 часов
- `NpcScheduleSync` (NetworkBehaviour) — 3-4 часа
- Mutation API + валидация — 2-3 часа
- UI (Editor companion / in-game panel) — 5-8 часов

### 2.4. Уровень C — Full runtime system

**Концепт:** Полный отказ от SO для runtime-логики. Расписания — это данные, загружаемые из JSON/SO при старте сервера, мутируемые в рантайме, сохраняемые в save-файлы.

#### 2.4.1. Что меняется

| Аспект | As-is | To-be |
|--------|-------|-------|
| Источник данных | SO на диске (Editor-only) | SO при старте → `RuntimeScheduleDatabase` (in-memory) |
| Мутация | Только Editor | Server-authoritative API |
| Persistence | `AssetDatabase.SaveAssets` (Editor) | Save-файл (JSON/Binary) |
| Синхронизация | Нет | NetworkVariable/NetworkList |
| UI | Editor window | In-game UGUI + Editor companion |

#### 2.4.2. RuntimeScheduleDatabase

```csharp
// Server-only singleton
public class RuntimeScheduleDatabase : MonoBehaviour
{
    // Все расписания (ключ = scheduleId)
    private Dictionary<string, RuntimeScheduleData> _all = new();
    
    // Привязка NPC → scheduleId
    private Dictionary<ulong, string> _npcBinding = new();
    
    // Событие: schedule изменился → все NPC должны перечитать
    public event Action<string> OnScheduleChanged;
    
    // API
    public RuntimeScheduleData Get(string scheduleId);
    public void Set(string scheduleId, RuntimeScheduleData data);
    public IEnumerable<string> AllScheduleIds { get; }
    
    // Persistence
    public void LoadFromSave(SaveData data);
    public void SaveToSave(SaveData data);
}
```

#### 2.4.3. Объём работ: ~2-3 недели
- `RuntimeScheduleDatabase` + миграция NpcShipWorld — 3-4 дня
- Network sync (NetworkVariable/NetworkList) — 2-3 дня
- Save/load — 1-2 дня
- Mutation API + валидация — 1-2 дня
- Admin UI (in-game) — 3-5 дней
- Тестирование + отладка — 2-3 дня

---

## 3. Рекомендация

**Для текущей стадии проекта (M1-M3.2): Уровень A достаточен.**

Уровень A уже частично работает: изменения SO через Inspector/OverviewWindow в Play Mode подхватываются при следующем `AdvanceScheduleIndex`. Единственное что нужно — кнопка «Force Apply to All Active NPCs» для мгновенного сброса индекса.

**Уровень B** имеет смысл если:
- Появляется need для билдового администрирования (не Editor)
- Нужно чтобы клиенты видели schedule-данные (UI на клиенте)
- Нужна сетевая валидация (сервер authoritative над маршрутами)

**Уровень C** — только при полном переходе на runtime-data-driven архитектуру (v2+).

---

## 4. Практический следующий шаг (Уровень A)

Добавить в `NpcShipScheduleOverviewWindow` (в Play Mode):

1. Кнопку **«♻ Apply to Runtime»** в Tab 1 (All Schedules):
   - Итерирует `NpcShipWorld.Instance.AllNpcs`
   - Для NPC с matching `scheduleId` → сбрасывает `state.ScheduleIndex = 0`
   - Логирует сколько NPC затронуто

2. Индикатор **«Play Mode Active»** в тулбаре окна при `Application.isPlaying`.

3. Вкладку **«🟢 Runtime»** (активна только в Play Mode):
   - Список всех NPC из `NpcShipWorld.Instance.AllNpcs`
   - Текущий статус FSM, CurrentRoute, ScheduleIndex
   - Возможность принудительно переключить route

---

*Создано: 2026-07-18*
