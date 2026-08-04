# Phase 2.4 — VFX: Молнии в грозовых ячейках (ПЛАН РЕАЛИЗАЦИИ)

**Дата:** 2026-08-04  
**Статус:** 🟡 План утверждён, реализация начинается  
**Исходный план:** `CLOUD_OCEAN_MEDIUM_DETAILED_STEPS_PHASE2_3.md` §2.4  
**Причина пересмотра:** Старая шторм-система (StormController / ServerStormManager / StormCloudGenerator) нерабочая и не отлажена. Решение пользователя: не трогать, проектировать с нуля под архитектуру 3.0.

---

## 0. Контекст: что есть и что мёртвое

### Мёртвое (НЕ ТРОГАТЬ)

| Файл | Почему мёртвый |
|---|---|
| `Assets/_Project/Scripts/Core/StormController.cs` | GameObject-based шторм со сферами-мешами; не отлажен |
| `Assets/_Project/Scripts/Core/ServerStormManager.cs` | NetworkBehaviour спавнит штормы; не отлажен |
| `Assets/_Project/Scripts/Core/StormCloudGenerator.cs` | Генерит сферы из CloudLayerConfig; не отлажен |
| `Assets/_Project/Prefabs/StormController.prefab` | Префаб со старыми компонентами |
| `Assets/_Project/Scripts/World/Clouds/StormLightningVfx.cs` | Подписан на `StormController.OnLightningTriggered` — ЗАМЕНИТЬ |

Все эти файлы остаются на диске (возможно, пригодятся для объектных облаков), но **не используются и не редактируются** в рамках 2.4.

### Живое (используем)

| Актив | Роль |
|---|---|
| `Clouds` (GO в BootstrapScene) | Корневой объект облачной системы 3.0 |
| `Clouds/LocalDensityBuffer` | 3D-буфер плотности (работает) |
| `WindManager.Instance` | Направление/скорость ветра |
| `GlobalStormEvents` | Статический класс для broadcast-а интенсивности шторма (есть, можно переиспользовать) |
| `Assets/_Project/VFX/` | Папка для VFX Graph (содержит `Contrail.vfx`) |

---

## 1. Архитектура: три слоя

```
Слой 1 — ИСТОЧНИК СОБЫТИЙ (новый)
┌──────────────────────────────────────┐
│ StormCellDirector : MonoBehaviour    │  ← новый класс, namespace ProjectC.World.Clouds
│                                      │     размещается на GO: Clouds/StormDirector
│ • List<StormCell> _cells             │
│ • Таймеры молний на каждую ячейку    │
│ • Движение ячеек по ветру (WindMgr)  │
│ • Событие: OnLightningTriggered      │
│   (Vector3 worldPos, float intensity)│
│                                      │
│ Для теста: при Start() создаёт       │
│ 1–3 ячейки вокруг камеры.            │
│                                      │
│ → В Phase 3.3 будет заменён на       │
│   WeatherCellManager (данные от       │
│   сервера), но событие остаётся       │
│   тем же.                            │
└──────────────┬───────────────────────┘
               │ event
               ▼
Слой 2 — VFX-КОНТРОЛЛЕР (переписать)
┌──────────────────────────────────────┐
│ StormLightningVfx : MonoBehaviour    │  ← ПЕРЕПИСАТЬ (существующий файл)
│                                      │     namespace ProjectC.World.Clouds
│ • VisualEffect _vfx                  │
│ • Подписка на StormCellDirector      │
│   .OnLightningTriggered              │
│ • Play(worldPos, intensity) →        │
│   задаёт StartPos/EndPos/Seed/Inten  │
│   и вызывает _vfx.Play()             │
│ • Частота вспышек = f(intensity)     │
└──────────────┬───────────────────────┘
               │ SetVector3 / Play
               ▼
Слой 3 — VFX GRAPH (создать)
┌──────────────────────────────────────┐
│ LightningBolt.vfx                    │  ← новый ассет VFX Graph 17.5.0
│                                      │
│ • Параметры графа:                   │
│   - StartPos (Vector3) — верх болта  │
│   - EndPos (Vector3) — низ болта     │
│   - Seed (float) — вариация формы   │
│   - Intensity (float) — яркость      │
│                                      │
│ • Одиночный bolt: ветвистая кривая   │
│   между StartPos/EndPos              │
│ • Вспышка: Point Light или           │
│   сферический bloom в точке удара    │
│ • Lifetime: 0.2–0.5 сек              │
└──────────────────────────────────────┘
```

### Почему три слоя

- **Слой 1 (StormCellDirector)** — временный источник данных на Phase 2.4. В Phase 3.3 `WeatherCellManager` заменит его, но контракт события (`Action<Vector3, float>`) останется неизменным. Слой 2 не придётся переписывать.
- **Слой 2 (StormLightningVfx)** — translation layer: преобразует событие «молния в точке» в параметры VFX Graph. Не знает, откуда пришло событие.
- **Слой 3 (LightningBolt.vfx)** — чистый VFX Graph, не зависит от C#.

---

## 2. Структура данных: StormCell

```csharp
// Внутри StormCellDirector
[System.Serializable]
public struct StormCell
{
    public Vector3 WorldPosition;   // центр ячейки (двигается с ветром)
    public float Radius;            // радиус влияния (500–5000 м)
    public float Intensity;         // 0..1
    public float TimeSinceLightning;// таймер с последней молнии
    public float NextLightningTime; // когда следующая (вычисляется из Intensity)
}
```

- **Размер:** для 2.4 — до 5 ячеек (как в старом ServerStormManager)
- **Позиция:** двигается каждый кадр: `pos += WindManager.Instance.CurrentWindDirection * speed * dt`
- **Частота молний:** `nextLightningTime = Random.Range(10f, 30f) / intensity` → минимум 10 сек между ударами

---

## 3. Пошаговая реализация

### Шаг 1: Создать `LightningBolt.vfx`

**Инструмент:** VFX Graph в Unity Editor (Window → Visual Effects → Graph).  
**Путь:** `Assets/_Project/VFX/LightningBolt.vfx`

**Структура графа:**
```
Spawn: Single burst (1 particle)
  ↓
Initialize:
  • Position = StartPos
  • Direction = normalize(EndPos - StartPos)
  • Length = distance(StartPos, EndPos)
  • Seed = Seed parameter
  ↓
Update:
  • Bolt-кривая: смещение midpoint по перпендикуляру на основе noise(Seed)
  • Color over life: яркая вспышка → затухание
  • Size over life: рост → затухание
  ↓
Output: Particle Strip / Line (или Point с Trail)
```

**Exposed параметры:**
| Имя | Тип | Назначение |
|---|---|---|
| `StartPos` | Vector3 | Верхняя точка (верх ячейки + 300) |
| `EndPos` | Vector3 | Нижняя точка (низ ячейки − 50) |
| `Seed` | float | Уникальность формы болта |
| `Intensity` | float | Множитель яркости/размера |

**Вспышка:** отдельный контекст или subgraph — сфера в точке `EndPos` с additive blending, lifetime 0.15–0.25 сек.

---

### Шаг 2: Создать `StormCellDirector.cs`

**Путь:** `Assets/_Project/Scripts/World/Clouds/StormCellDirector.cs`  
**Namespace:** `ProjectC.World.Clouds` (все новые облачные компоненты здесь)

```csharp
// Сигнатура (скелет)
public class StormCellDirector : MonoBehaviour
{
    public static StormCellDirector Instance { get; private set; }

    [Header("Cells")]
    [Range(1, 10)] public int MaxCells = 5;
    [Range(500f, 5000f)] public float CellRadius = 2000f;
    [Range(500f, 3000f)] public float CellAltitude = 1500f;

    [Header("Lightning")]
    [Range(5f, 60f)] public float LightningIntervalMin = 10f;
    [Range(10f, 120f)] public float LightningIntervalMax = 30f;

    [Header("Test")]
    [Tooltip("При старте создать тестовые ячейки вокруг камеры")]
    public bool SpawnTestCells = true;
    [Range(0, 5)] public int TestCellCount = 2;

    // Внутренние
    private List<StormCell> _cells = new();
    
    // Событие
    public event System.Action<Vector3, float> OnLightningTriggered;
}
```

**Логика:**
- `Awake`: singleton. Читает `WindManager.Instance`.
- `Start`: если `SpawnTestCells` — создаёт `TestCellCount` ячеек на `CellAltitude` вокруг камеры.
- `Update`:
  1. Двигает каждую ячейку по ветру: `cell.WorldPosition += windDir * windSpeed * dt`
  2. Для каждой ячейки: `timeSinceLightning += dt`. Если `timeSince >= nextLightning` → `OnLightningTriggered?.Invoke(pos, intensity)` и сброс таймера.
- `AddCell(Vector3 pos, float radius, float intensity)` — публичный API (для будущего WeatherCellManager).
- `RemoveCell(int index)` — публичный API.
- `GetCells()` — read-only доступ (для дебага/будущей 3.3).

**Интеграция с GlobalStormEvents:** при изменении средней интенсивности ячеек → `GlobalStormEvents.BroadcastStormIntensity(avgIntensity)`.

---

### Шаг 3: Переписать `StormLightningVfx.cs`

**Путь:** `Assets/_Project/Scripts/World/Clouds/StormLightningVfx.cs` (перезапись существующего)

**Текущее состояние:** подписан на `StormController.OnLightningTriggered`.  
**Новое состояние:** подписан на `StormCellDirector.OnLightningTriggered`.

```csharp
// Новая сигнатура
public class StormLightningVfx : MonoBehaviour
{
    [Header("VFX")]
    public VisualEffect Vfx;                    // LightningBolt.vfx
    
    [Header("References")]
    public StormCellDirector Director;          // если null — ищет Instance
    
    [Header("Lightning Shape")]
    [Range(100f, 500f)] public float BoltTopOffset = 300f;
    [Range(0f, 200f)] public float BoltBottomOffset = 50f;
    [Range(0.05f, 1f)] public float BoltDuration = 0.3f;

    // VFX property IDs (static readonly, Shader.PropertyToID)
    
    void OnEnable()  → подписка на Director.OnLightningTriggered
    void OnDisable() → отписка
    
    void HandleLightning(Vector3 worldPos, float intensity)
    {
        if (Vfx == null) return;
        Vfx.SetVector3(StartPosId, worldPos + Vector3.up * BoltTopOffset);
        Vfx.SetVector3(EndPosId, worldPos - Vector3.up * BoltBottomOffset);
        Vfx.SetFloat(SeedId, Random.value);
        Vfx.SetFloat(IntensityId, intensity);
        Vfx.Play();
        StartCoroutine(StopAfterDelay(BoltDuration));
    }
}
```

**Ключевые отличия от старой версии:**
- ❌ Убрать `using ProjectC.Core;` (не нужен)
- ❌ Убрать ссылку на `StormController`
- ❌ Убрать параметр `StormController storm`
- ✅ Добавить ссылку на `StormCellDirector` (с null-guard)
- ✅ Добавить `Intensity` параметр в VFX

---

### Шаг 4: Собрать в BootstrapScene

**Создать в сцене:**
```
Clouds
├── LocalDensityBuffer     (существующий)
└── StormDirector           (НОВЫЙ — создать через create_game_object)
    └── Добавить компонент StormCellDirector
    └── Добавить компонент StormLightningVfx (с ссылкой на VFX)
```

**Настройки StormCellDirector по умолчанию:**
- `MaxCells = 5`
- `CellRadius = 2000`
- `CellAltitude = 1500`
- `SpawnTestCells = true`
- `TestCellCount = 2`

**Для StormLightningVfx:**
- `Director` = ссылка на `StormCellDirector` (тот же GO)
- `Vfx` = ссылка на `Assets/_Project/VFX/LightningBolt.vfx` (через инспектор)

---

## 4. Совместимость с Phase 3.3 (WeatherCellManager)

```
Phase 2.4 (сейчас)                  Phase 3.3 (будущее)
                                    
StormCellDirector                   WeatherCellManager
├── тестовые ячейки (local)         ├── ячейки от сервера (NGO)
├── OnLightningTriggered event       ├── OnLightningTriggered event (тот же сигнатура!)
└── GlobalStormEvents                └── GlobalStormEvents
    
StormLightningVfx                   StormLightningVfx (БЕЗ ИЗМЕНЕНИЙ!)
├── слушает OnLightningTriggered     ├── слушает OnLightningTriggered
└── играет LightningBolt.vfx         └── играет LightningBolt.vfx
```

**Миграция:** в 3.3 `StormCellDirector` заменяется на `WeatherCellManager`, но:
- Сигнатура события `Action<Vector3, float>` сохраняется
- `StormLightningVfx` не меняется
- `LightningBolt.vfx` не меняется
- `GlobalStormEvents` уже существует и переиспользуется обоими

---

## 5. Приёмка

| Критерий | Как проверить |
|---|---|
| В Play Mode создаются тестовые ячейки | Лог `[StormCellDirector] Spawned N test cells` |
| Ячейки двигаются по ветру | Визуально (Debug.DrawLine) или лог позиции |
| Молнии появляются каждые 10–30 сек | Видна вспышка в Game View |
| VFX bolt проигрывается корректно | Виден bolt между верхом и низом ячейки |
| Интенсивность влияет на частоту | При intensity=1.0 чаще, чем при 0.3 |
| GlobalStormEvents broadcast | Другой скрипт (CloudClimateTinter) реагирует на изменение |
| Остановка — нет молний | После выключения SpawnTestCells молнии прекращаются |

---

## 6. Файлы: сводка

| Файл | Действие | Назначение |
|---|---|---|
| `Assets/_Project/VFX/LightningBolt.vfx` | **СОЗДАТЬ** | VFX Graph молнии |
| `Assets/_Project/Scripts/World/Clouds/StormCellDirector.cs` | **СОЗДАТЬ** | Источник штормовых ячеек (временный, до 3.3) |
| `Assets/_Project/Scripts/World/Clouds/StormLightningVfx.cs` | **ПЕРЕПИСАТЬ** | VFX-контроллер (отвязать от StormController) |
| `BootstrapScene.unity` — GO `Clouds/StormDirector` | **СОЗДАТЬ** | Размещение новых компонентов в сцене |

---

## 7. Журнал отклонений от исходного плана

| Пункт плана | Исходное решение | Фактическое решение | Причина |
|---|---|---|---|
| 2.4 — источник событий молний | `StormController.TriggerLightning()` (старый) | `StormCellDirector.OnLightningTriggered` (новый) | Старая шторм-система нерабочая |
| 2.4 — размещение в сцене | `StormController.prefab` | `Clouds/StormDirector` в BootstrapScene | Консолидация облачных объектов под `Clouds` |
| 2.4 — namespace | `ProjectC.Core` (частично) | `ProjectC.World.Clouds` (единообразно) | Все новые облачные компоненты в одном namespace |

---

## 8. Порядок выполнения

1. **Шаг 1:** Создать `LightningBolt.vfx` (VFX Graph) — вручную через Unity Editor
2. **Шаг 2:** Создать `StormCellDirector.cs` — через `create_script`
3. **Шаг 3:** Переписать `StormLightningVfx.cs` — через `replace_in_file`
4. **Шаг 4:** Собрать `Clouds/StormDirector` в BootstrapScene
5. **Верификация:** `check_compile_errors` → Play Mode → `read_console` → скриншоты
6. **Коммит:** `git-commit` skill

---

*План создан 2026-08-04. Отклонения от этого плана в ходе реализации документируются в §7.*
