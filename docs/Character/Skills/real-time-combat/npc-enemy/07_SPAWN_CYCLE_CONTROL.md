# 07 — Spawn Cycle Control: конечные волны и перезапуск спавна

**Статус:** 📝 Design doc — реализация следом.
**Дата:** 2026-07-29
**Связано:** `NpcSpawner.cs`, `NpcSpawnerConfig.cs`, `70_NPC_ENEMIES.md`

---

## 1. Проблема

Сейчас `NpcSpawner` работает в **бесконечном** режиме: как только NPC умирает, спавнер через
`_spawnInterval` секунд рефиллит нового — и так до бесконечности, пока `_maxAlive > 0`.

Нужен контролируемый цикл спавна:

- **Конечная волна:** заспавнить N мобов. Когда все убиты → спавн останавливается.
- **Перезапуск по условиям:** волна перезапускается только когда срабатывают настраиваемые
  условия (триггеры), например:
  - прошло N секунд после зачистки
  - игрок покинул корабль (триггер-зона)
  - дверь открылась (любой объект со скриптом)
- **Дизайнерская гибкость:** без хардкода. Дизайнер в Editor собирает условия из готовых
  компонентов и drag'n'drop подключает их к спавнеру.

### Сценарий: корабль с экипажем

1. Игрок заходит на корабль → спавнер активируется, спавнит 5 членов экипажа.
2. Игрок убивает всех 5 → спавн останавливается (цикл исчерпан).
3. Игрок покидает корабль (выходит из триггер-зоны) И проходит 120 секунд.
4. Спавнер перезапускается → новая волна из 5 NPC.

---

## 2. Текущий код (baseline)

### 2.1 `NpcSpawner` — ключевые точки

```
TickSpawn():
  1. Cleanup _spawned (remove dead/despawned)
  2. if _spawned.Count >= _maxAlive → return   ← БЕСКОНЕЧНЫЙ рефилл
  3. FindNearestPlayer()
  4. CheckRateLimit(), Random < _spawnChance
  5. TryFindSpawnPoint()
  6. TrySpawnAtPoint() → _spawned.Add()
```

**Что нужно изменить:** шаг 2 и добавить понятие «цикл исчерпан».

### 2.2 `NpcSpawnerConfig` — текущие поля

Уже есть: `maxAliveCount`, `spawnCheckInterval`, `spawnChance`, `activationRadius`,
`maxSpawnsPerPlayerPerMinute`, плюс behaviour/visual/social секции.

**Что добавить:** режим спавна и лимит волны.

### 2.3 Паттерны проекта

- **Анти-рестриктивное:** если поле = null/0 → fallback к дефолту, не падает.
- **Server-only:** `if (!IsServer) { enabled = false; return; }`
- **ScriptableObject для конфигов:** `NpcSpawnerConfig`, `NpcVisualConfig`, `NpcSkillSet` etc.
- **Inspector-friendly:** `[Header(...)]`, `[Tooltip(...)]`, `[Range(...)]`

---

## 3. Архитектурное решение

### 3.1 Общий подход

Три слоя абстракции:

| Слой | Что делает | Где живёт |
|------|-----------|-----------|
| **Spawn Cycle FSM** | Конечный автомат: `Active → Exhausted → (restart) → Active` | `NpcSpawner` (добавление к существующему) |
| **Restart Trigger** | Интерфейс `ISpawnRestartTrigger` — компонент, который знает «когда перезапускать» | Отдельные MonoBehaviour на любых GameObject |
| **Trigger Composition** | AND/OR-комбинатор для нескольких триггеров | `SpawnRestartGate` (опциональный компонент) |

Дизайнер:
1. Выбирает режим `FiniteCycle` на спавнере.
2. Ставит `totalSpawnLimit = 5` (5 мобов за цикл).
3. Создаёт GameObject `RestartConditions`, вешает на него:
   - `SpawnRestartTimer` (wait 120s after exhaust)
   - `SpawnRestartTriggerZone` (player exits zone)
   - `SpawnRestartGate` (mode = AND)
4. Перетаскивает `RestartConditions` в поле `Restart Triggers` спавнера.

### 3.2 `SpawnMode` enum

```csharp
public enum SpawnMode : byte
{
    Infinite       = 0,  // ← текущее поведение (backward compat, default)
    Finite         = 1,  // спавнить totalSpawnLimit, когда все убиты — остановиться НАВСЕГДА
    FiniteCycle    = 2   // спавнить totalSpawnLimit за цикл, ждать restart trigger(s)
}
```

| Режим | После убийства всех | Поведение |
|-------|---------------------|-----------|
| `Infinite` | Рефиллит новых | Текущее (default) |
| `Finite` | Останавливается | Одноразовая зачистка (квестовый лагерь) |
| `FiniteCycle` | Ждёт restart trigger(s) | Волны (экипаж корабля) |

### 3.3 Интерфейс `ISpawnRestartTrigger`

```csharp
namespace ProjectC.AI
{
    /// <summary>
    /// Компонент, который сообщает NpcSpawner'у «пора перезапустить цикл».
    /// Вешается на любой GameObject. NpcSpawner опрашивает все зарегистрированные
    /// триггеры и перезапускает цикл когда ВСЕ (или ЛЮБОЙ — зависит от gate) сработали.
    /// </summary>
    public interface ISpawnRestartTrigger
    {
        /// <summary>Состояние триггера прямо сейчас. Вызывается каждый кадр спавнером.</summary>
        bool IsTriggered { get; }

        /// <summary>Вызывается спавнером когда цикл исчерпан (все NPC мертвы).</summary>
        void OnCycleExhausted();

        /// <summary>Вызывается спавнером когда цикл перезапущен.</summary>
        void OnCycleStarted();

        /// <summary>Вызывается при регистрации в спавнере.</summary>
        void OnRegistered(NpcSpawner spawner);
    }
}
```

**Почему интерфейс, а не UnityEvent:**
- Триггеру нужно знать *когда* цикл исчерпан (чтобы начать отсчёт таймера).
- Триггер пассивен — NpcSpawner *опрашивает* его (poll), а не ждёт callback.
  Это упрощает композицию: не нужно управлять подписками/отписками на каждый триггер.
- Любой MonoBehaviour может реализовать интерфейс → designer-extensible без нового кода.

### 3.4 Встроенные триггеры (4 штуки)

#### A. `SpawnRestartTimer` — перезапуск по времени

```csharp
public class SpawnRestartTimer : MonoBehaviour, ISpawnRestartTrigger
{
    [Tooltip("Секунд ожидания после exhaust перед сигналом перезапуска.")]
    [Range(1f, 3600f)] public float delaySeconds = 120f;

    // Внутри: OnCycleExhausted() запускает корутину, через delaySeconds → IsTriggered=true
}
```

#### B. `SpawnRestartTriggerZone` — перезапуск по входу/выходу из зоны

```csharp
[RequireComponent(typeof(Collider))]
public class SpawnRestartTriggerZone : MonoBehaviour, ISpawnRestartTrigger
{
    public enum TriggerEvent { OnEnter, OnExit }
    [Tooltip("OnEnter = игрок вошёл → перезапуск. OnExit = игрок вышел → перезапуск.")]
    public TriggerEvent triggerOn = TriggerEvent.OnExit;

    [Tooltip("Какие теги считать 'игроком'.")]
    public string[] playerTags = { "Player" };

    // Внутри: OnTriggerEnter/Exit → если матчит тег → IsTriggered=true
}
```

#### C. `SpawnRestartUnityEvent` — ручной перезапуск (кнопка/скрипт)

```csharp
public class SpawnRestartUnityEvent : MonoBehaviour, ISpawnRestartTrigger
{
    [Tooltip("Вызови Restart() из любого скрипта или через Inspector.")]
    public UnityEvent onRestartRequested;

    public void Restart() { /* IsTriggered = true */ }
}
```

Дизайнер может:
- Повесить этот компонент на дверь.
- В дверном скрипте при открытии вызвать `GetComponent<SpawnRestartUnityEvent>().Restart()`.
- Или через Inspector повесить onClick кнопки на `Restart()`.

#### D. `SpawnRestartGate` — AND/OR композиция

```csharp
public class SpawnRestartGate : MonoBehaviour, ISpawnRestartTrigger
{
    public enum GateMode { All, Any }
    [Tooltip("All = AND (все должны сработать). Any = OR (любой сработал → перезапуск).")]
    public GateMode mode = GateMode.All;

    [Tooltip("Список триггеров (можно перетащить сюда же GameObject'ы с ISpawnRestartTrigger).")]
    public List<MonoBehaviour> triggers;

    // IsTriggered: если All → triggers.All(t => t.IsTriggered)
    //             если Any → triggers.Any(t => t.IsTriggered)
}
```

### 3.5 Изменения в `NpcSpawner`

#### Новые serialized fields:

```csharp
[Header("Spawn Cycle (T-NPC-11)")]
[Tooltip("Infinite = текущее поведение. Finite = спавнить N и остановиться. FiniteCycle = волны с перезапуском.")]
[SerializeField] private SpawnMode _spawnMode = SpawnMode.Infinite;

[Tooltip("Сколько всего NPC спавнить за цикл (для Finite/FiniteCycle). " +
         "При 0 = использовать _maxAlive (backward compat).")]
[Range(0, 50)] [SerializeField] private int _totalSpawnLimit = 0;

[Tooltip("GameObject'ы с компонентами ISpawnRestartTrigger. " +
         "Перезапускают цикл когда ВСЕ сработали. " +
         "Для AND/OR композиции — используй SpawnRestartGate.")]
[SerializeField] private List<MonoBehaviour> _restartTriggers = new List<MonoBehaviour>();
```

#### Новые runtime-поля:

```csharp
private int _totalSpawnedThisCycle;      // сколько заспавнено в текущем цикле
private bool _cycleExhausted;             // цикл исчерпан (все мертвы)
private List<ISpawnRestartTrigger> _resolvedTriggers; // кэш резолвнутых триггеров
private float _nextRestartCheckTime;      // throttle для опроса триггеров
```

#### Модификация `TickSpawn()`:

```
TickSpawn():
  Cleanup dead (_spawned)
  
  // --- Новое: проверка исчерпания цикла ---
  if _cycleExhausted:
      CheckRestartTriggers()   ← опрос триггеров
      return                   ← не спавним пока цикл не перезапущен
  
  // --- Существующая логика спавна ---
  if _spawnMode != Infinite:
      if _totalSpawnedThisCycle >= effectiveLimit → return  // не превысили лимит волны
  
  if _spawned.Count >= _maxAlive → return
  
  // ... существующий спавн ...
  TrySpawnAtPoint() → _spawned.Add(), _totalSpawnedThisCycle++
  
  // --- Новое: детект исчерпания ---
  if _spawnMode != Infinite && _spawned.Count == 0 && _totalSpawnedThisCycle >= effectiveLimit:
      ExhaustCycle()
```

#### `ExhaustCycle()`:

```csharp
private void ExhaustCycle()
{
    _cycleExhausted = true;
    _totalSpawnedThisCycle = 0; // сброс для следующего цикла
    
    foreach (var t in _resolvedTriggers)
        t.OnCycleExhausted();
    
    if (_showDebugLogs)
        Debug.Log($"[NpcSpawner] Cycle exhausted. Waiting for restart triggers...");
}
```

#### `CheckRestartTriggers()`:

```csharp
private void CheckRestartTriggers()
{
    if (_resolvedTriggers.Count == 0) return; // нет триггеров → никогда не перезапустится
    
    if (Time.unscaledTime < _nextRestartCheckTime) return;
    _nextRestartCheckTime = Time.unscaledTime + 1f; // опрос раз в секунду
    
    bool allTriggered = true;
    foreach (var t in _resolvedTriggers)
    {
        if (t == null || !((MonoBehaviour)t).isActiveAndEnabled) continue;
        if (!t.IsTriggered) { allTriggered = false; break; }
    }
    
    if (allTriggered)
    {
        _cycleExhausted = false;
        _totalSpawnedThisCycle = 0;
        _spawned.Clear(); // на всякий случай
        
        foreach (var t in _resolvedTriggers)
            t.OnCycleStarted();
        
        _nextCheckTime = Time.unscaledTime + _spawnInterval;
        
        if (_showDebugLogs)
            Debug.Log("[NpcSpawner] All restart triggers fired — new cycle started.");
    }
}
```

#### `ResolveTriggers()` (в `OnNetworkSpawn`):

```csharp
private void ResolveTriggers()
{
    _resolvedTriggers = new List<ISpawnRestartTrigger>();
    foreach (var mb in _restartTriggers)
    {
        if (mb == null) continue;
        if (mb is ISpawnRestartTrigger trigger)
        {
            trigger.OnRegistered(this);
            _resolvedTriggers.Add(trigger);
        }
        else
        {
            Debug.LogWarning($"[NpcSpawner] {mb.name} doesn't implement ISpawnRestartTrigger, skipping.");
        }
    }
}
```

### 3.6 Изменения в `NpcSpawnerConfig`

```csharp
[Header("Spawn Cycle (T-NPC-11)")]
[Tooltip("Infinite = текущее поведение (backward compat). Finite = одноразовая зачистка. FiniteCycle = волны.")]
public SpawnMode spawnMode = SpawnMode.Infinite;

[Tooltip("Лимит NPC за цикл (для Finite/FiniteCycle). 0 = без лимита (используется только maxAliveCount).")]
[Range(0, 100)] public int totalSpawnLimit = 0;
```

`ApplyConfig()` в `NpcSpawner` читает эти поля (anti-restrictive: если config = null → дефолты из serialized fields на самом спавнере).

---

## 4. Файлы

### Новые (~5):

| Файл | Что |
|------|-----|
| `Assets/_Project/Scripts/AI/SpawnRestart/ISpawnRestartTrigger.cs` | Интерфейс |
| `Assets/_Project/Scripts/AI/SpawnRestart/SpawnRestartTimer.cs` | Таймер |
| `Assets/_Project/Scripts/AI/SpawnRestart/SpawnRestartTriggerZone.cs` | Триггер-зона |
| `Assets/_Project/Scripts/AI/SpawnRestart/SpawnRestartUnityEvent.cs` | Ручной (UnityEvent) |
| `Assets/_Project/Scripts/AI/SpawnRestart/SpawnRestartGate.cs` | AND/OR композитор |

### Изменяемые (~2):

| Файл | Что |
|------|-----|
| `Assets/_Project/Scripts/AI/NpcSpawner.cs` | SpawnMode, cycle FSM, restart triggers |
| `Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs` | +spawnMode, +totalSpawnLimit |

---

## 5. Пример использования дизайнером

**Задача:** корабль, 5 членов экипажа. Игрок убивает всех → спавн стоп. Когда игрок
покидает корабль И прошло 120 сек → новая волна.

**Шаги в Editor:**

1. Выделить `[NpcSpawner]` в сцене → Inspector:
   - `Spawn Mode = FiniteCycle`
   - `Total Spawn Limit = 5`
   - `Max Alive Count = 5`

2. Создать Empty `RestartConditions` как child спавнера:
   - Add Component → `Spawn Restart Timer` → `Delay Seconds = 120`
   - Add Component → `Spawn Restart Trigger Zone` → `Trigger On = OnExit`
   - Add Component → `Spawn Restart Gate` → `Mode = All`, перетащить Timer и Zone в список

3. В спавнере: `Restart Triggers` → перетащить `RestartConditions`.

4. Готово. Ни одной строчки кода.

---

## 6. Edge cases

| Ситуация | Поведение |
|----------|-----------|
| `_restartTriggers` пуст в `FiniteCycle` | Спавн после exhaust **никогда** не перезапустится (логично: нет условий) |
| `_spawnMode = Infinite` | `_restartTriggers` игнорируются (backward compat) |
| `totalSpawnLimit = 0` в Finite | Используется `_maxAlive` как лимит |
| `totalSpawnLimit < _maxAlive` | Спавним меньшую из двух величин (totalSpawnLimit — жёсткий потолок) |
| Триггер уничтожен/отключён | Пропускается в опросе (как несработавший) |
| Спавнер деспавнится | `OnNetworkDespawn` сбрасывает `_resolvedTriggers` |
| Несколько спавнеров делят триггер | Каждый спавнер вызывает `OnRegistered/OnCycleExhausted` независимо — ок |

---

## 7. Roadmap

| Тикет | Что | Оценка |
|-------|-----|--------|
| **T-NPC-11a** | `SpawnMode` + cycle FSM в `NpcSpawner` | ~1.5 ч |
| **T-NPC-11b** | `ISpawnRestartTrigger` + 4 встроенных триггера | ~1.5 ч |
| **T-NPC-11c** | `NpcSpawnerConfig` поля + `ApplyConfig` | ~0.5 ч |
| **T-NPC-11d** | Play Mode verify: конечная волна + таймер + зона | ~0.5 ч |

**Итого:** ~4 часа (1 сессия).
