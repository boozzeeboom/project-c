# Performance Monitoring & Debugging — Глубокий Анализ v2.0

> **Project:** ProjectC (Unity 6000.5.2f1+, URP 17.5, NGO 2.13, CoPlay 8.20)
> **Дата:** 2026-07-25
> **Статус:** Research & Strategy — v2.0 (анализ кода всех подсистем)

---

## 0. Executive Summary — Что изменилось с v1

| Аспект | v1 (2026-01) | v2.0 (2026-07) |
|--------|-------------|----------------|
| **Анализ кода** | Общий, без привязки к подсистемам | По каждой подсистеме: AI, Ships, Clouds, Streaming, Combat, Stats, Items, Quests, Crafting, Docking, DayNight |
| **ProfilerMarker в коде** | 0 — только примеры | **0 — всё ещё не внедрён** (Critical gap) |
| **ProfilerCounter в коде** | 0 — только примеры | **0 — всё ещё не внедрён** (Critical gap) |
| **Runtime HUD** | Предлагался Graphy | Graphy 3.0.5 (Unity 6000.x совместим) + анализ альтернатив |
| **CPU-бюджеты** | Общие | Рассчитаны для каждой подсистемы |
| **Сетевые метрики** | Общие | NGO NetworkMetrics + CoPlay Transport API |
| **Asset Store решения** | 4 позиции | 12 позиций + UPM пакеты |
| **Unity 6 features** | Не было | Profiler in own process, new counters, URP 17.5 perf features |

---

## 1. Аудит существующего кода — ProfilerMarker Coverage Map

**Критическое открытие:** в проекте **ноль** использования `ProfilerMarker`, `ProfilerCounter` или `ProfilerRecorder`. Ни один скрипт не инструментирован для Profiler.

### 1.1 Подсистемы с Update/FixedUpdate — горячие точки

| Подсистема | Файл(ы) | Update | FixedUpdate | Оценка влияния |
|------------|---------|--------|-------------|----------------|
| **AI** | `NpcBrain.cs` (1232 строки) | ✅ Idle/Chase/Attack FSM | ❌ | **HIGH** — каждый NPC отдельный Update |
| **AI Social** | `NpcSocialBrain.cs` | ✅ Социальные тики | ❌ | **MEDIUM** — тики с интервалом |
| **AI Spawner** | `NpcSpawner.cs` | ✅ Проверка респавна | ❌ | **LOW** — только при необходимости |
| **Clouds** | `CloudManager.cs`, `CloudLayer.cs` | ✅ | ❌ | **HIGH** — VFX, много объектов |
| **Clouds Distant** | `DistantCloudManager.cs` | ✅ | ❌ | **MEDIUM** |
| **Clouds Near** | `NearCloudRenderer.cs` | ✅ | ❌ | **MEDIUM** |
| **Day/Night** | `DayNightController.cs` | ✅ | ❌ | **LOW** — раз в N секунд |
| **Wind** | `WindManager.cs` | ✅ | ❌ | **MEDIUM** |
| **Ship (Player)** | `ShipController.cs` | ✅ | ✅ | **HIGH** — физика, движение |
| **Ship Fuel** | `ShipFuelSystem.cs` | ✅ | ❌ | **LOW** |
| **Ship Modules** | `ShipModuleManager.cs` | ✅ | ❌ | **MEDIUM** |
| **Ship Cargo** | `ShipCargoRegistry.cs` | ⚠️ событийно | ❌ | **LOW** |
| **Ship Debug** | `ShipDebugHUD.cs` | ✅ (OnGUI) | ❌ | **LOW** — только при F3 |
| **Player** | `NetworkPlayer.cs` (2199 строк) | ✅ | ❌ | **HIGH** — movement, input |
| **Camera** | `SpringArmCamera.cs` | ✅ LateUpdate | ❌ | **MEDIUM** |
| **Combat Client** | `TargetLockService.cs`, `CombatClientState.cs` | ✅ | ❌ | **MEDIUM** |
| **Combat Server** | `CombatServer.cs` | ✅ | ❌ | **MEDIUM** |
| **Stats** | `StatsClientState.cs`, `StatsServer.cs` | ⚠️ событийно | ❌ | **LOW** |
| **Items** | `InventoryClientState.cs`, `InventoryServer.cs` | ⚠️ событийно | ❌ | **LOW** |
| **Crafting** | `CraftingClientState.cs`, `CraftingServer.cs` | ✅ | ❌ | **MEDIUM** |
| **Quests** | `QuestClientState.cs`, `QuestServer.cs` | ⚠️ событийно | ❌ | **LOW** |
| **World Streaming** | `WorldStreamingManager.cs` (830 строк) | ✅ (корутина) | ❌ | **HIGH** — загрузка/выгрузка чанков |
| **Chunk Loader** | `ChunkLoader.cs` | ✅ | ❌ | **MEDIUM** |
| **Floating Origin** | `FloatingOriginMP.cs` | ✅ LateUpdate | ❌ | **MEDIUM** |
| **Docking** | `DockingWorld.cs`, `PadStateSync.cs` | ✅ | ❌ | **LOW-MEDIUM** |
| **Peaceful NPC Ships** | `NpcShipWorld.cs` | ✅ | ❌ | **MEDIUM** |
| **UI** | `HUDManager.cs`, `UIManager.cs` | ✅ | ❌ | **MEDIUM** |
| **Equipment** | `EquipmentClientState.cs`, `EquipmentServer.cs` | ⚠️ событийно | ❌ | **LOW** |
| **Customisation** | `CustomisationClientState.cs` | ⚠️ событийно | ❌ | **LOW** |
| **Skill VFX** | `SkillVfxService.cs`, `VfxObjectPool.cs` | ✅ | ❌ | **MEDIUM** |

### 1.2 Что уже есть (throttling/performance-aware код)

Хорошие практики, уже заложенные в код:

1. **StatsClientState** — 200ms throttle на `OnStatTierUp` (поле `_tierUpEventMinIntervalSeconds`)
2. **NpcBrain** — FSM с cooldown между атаками, не спамит
3. **NpcSocialBrain** — тики с интервалом (~0.5с) через `socialTickInterval`
4. **CraftingClientState** — использует `[Conditional("UNITY_EDITOR")]` и `Debug.isDebugBuild`
5. **TargetLockService** — использует `Debug.isDebugBuild` для логирования
6. **WorldStreamingManager** — updateInterval 0.5s, прелоад слои
7. **VfxObjectPool** — пулинг VFX объектов

### 1.3 Проблемные места (найденные в коде)

1. **NpcBrain.cs (строка 40)**: `using System.Linq;` — LINQ в горячем пути серверного AI
2. **NetworkPlayer.cs (строка 37)**: `FindAnyObjectByType<NetworkManagerController>()` в Start() — поиск по сцене
3. **SceneDebugHUD.cs (строка 33-34)**: `FindAnyObjectByType<ClientSceneLoader>()` и `Resources.Load<SceneRegistry>()` в Start()
4. **ShipDebugHUD.cs**: OnGUI + `new Texture2D` + `SetPixel` цикл каждый кадр при видимости HUD
5. **MakeTex()** в ShipDebugHUD.cs — создаёт текстуру через SetPixel в OnGUI (медленно)
6. **NetworkTestMenu.cs (строка 37)**: `FindAnyObjectByType<NetworkManagerController>()` в Start()
7. **Множественный `Debug.Log`** в горячих путях даже при выключенном _debugLog (строки до проверки флага)

---

## 2. Unity 6 Специфичные Возможности (6000.5.2f1)

### 2.1 Profiler в отдельном процессе (Standalone Profiler)

Unity 6 позволяет запускать Profiler как отдельное окно/процесс:
```
Window → Analysis → Profiler → Profiler в отдельном окне
```
**Effect:** Меньше оверхед на Editor, более точные данные.

### 2.2 Новые Profiler Counters в Unity 6

Добавлены:
- `ProfilerCategory.Rendering` — детальные GPU счётчики
- `ProfilerCategory.Network` — расширенные сетевые метрики
- `ProfilerRecorder` для GC Allocation rate

### 2.3 URP 17.5 Performance Features

- **Screen Space Shadows** — настраиваемое разрешение
- **Render Graph** — оптимизация прохода рендера
- **FSR (FidelityFX Super Resolution)** — встроенный upscaling
- **LOD Crossfade** — плавный переход LOD

### 2.4 Entity Component System (ECS) — Potential

Unity 6000.5 имеет стабильный ECS. Для массовых NPC/Ships можно рассмотреть:
- `Entities.ForEach` вместо `Update()` на тысячах объектов
- `Burst` компилятор для AI-логики

---

## 3. Asset Store и UPM Решения — Полный Каталог

### 3.1 Graphy — Ultimate FPS Counter
| | |
|---|---|
| **Цена** | FREE |
| **Версия** | 3.0.5 |
| **Совместимость** | Unity 2019.4+ (включая 6000.x) |
| **OpenUPM** | `openupm add com.tayx.graphy` |
| **GitHub** | https://github.com/Tayx94/graphy (MIT) |
| **Что даёт** | FPS, RAM, Audio, кастомные графики (G_GraphX) |
| **Для ProjectC** | **★★★★★** — ставится за 5 мин, Add to My Assets бесплатно |

**Кастомные графики для ProjectC:**
```csharp
// Пример: кастомный график кол-ва активных NPC
var npcCountGraph = GraphyManager.Instance.AddGraph("NPC Count", GraphyLookup.Category.Scripts);
// Обновление в Update: npcCountGraph.UpdateValue(NpcBrain.ActiveCount);
```

### 3.2 Unity Memory Profiler (UPM)
| | |
|---|---|
| **Цена** | FREE (в составе UPM) |
| **Package** | `com.unity.memoryprofiler` (v1.1.11) |
| **Установка** | Window → Package Manager → Memory Profiler |
| **Для ProjectC** | **★★★★★** — обязателен для поиска утечек |

### 3.3 Profile Analyzer (UPM)
| | |
|---|---|
| **Цена** | FREE |
| **Package** | `com.unity.performance.profile-analyzer` (v1.1.1) |
| **Установка** | Window → Package Manager → Profile Analyzer |
| **Для ProjectC** | **★★★★☆** — сравнение снапшотов, A/B тесты |

### 3.4 Unity Profiling Core API (Built-in)
| | |
|---|---|
| **Пространство** | `Unity.Profiling` (встроено, без пакета) |
| **Классы** | `ProfilerMarker`, `ProfilerCounter<T>`, `ProfilerRecorder` |
| **Для ProjectC** | **★★★★★** — немедленно внедрить во все Update |

### 3.5 Unity Project Auditor (UPM)
| | |
|---|---|
| **Package** | `com.unity.project-auditor` |
| **Установка** | Window → Package Manager → Project Auditor |
| **Для ProjectC** | **★★★☆☆** — статический анализ, разовый прогон |

### 3.6 Super Science (Unity Tech)
| | |
|---|---|
| **GitHub** | https://github.com/Unity-Technologies/superscience |
| **Что даёт** | Runtime performance benchmarking, CI integration |
| **Для ProjectC** | **★★★☆☆** — когда понадобится CI performance regression |

### 3.7 Unity Benchmark Framework (UPM)
| | |
|---|---|
| **Package** | `com.unity.test-framework.performance` |
| **Установка** | Package Manager → Performance Testing Extension |
| **Для ProjectC** | **★★☆☆☆** — для автоматических тестов производительности |

### 3.8 Asset Store — Платные Альтернативы

| Ассет | Цена | Рейтинг | Комментарий |
|-------|------|---------|-------------|
| **[Runtime Graphics Settings](https://assetstore.unity.com/packages/tools/gui/runtime-graphics-settings-210688)** | FREE | 4.5★ | Auto-quality scaling по FPS |
| **[GPU Instancer Pro](https://assetstore.unity.com/packages/tools/utilities/gpu-instancer-pro-199358)** | $90 | 4.6★ | Для облаков/NPC кораблей (если много) |
| **[Texture Optimizer](https://assetstore.unity.com/packages/tools/utilities/texture-optimizer-138792)** | $15 | 4.5★ | Оптимизация текстур |
| **[Mesh Combiner](https://assetstore.unity.com/packages/tools/utilities/mesh-combiner-166040)** | $35 | 4.3★ | Для статических объектов |
| **[DLL Hunter](https://assetstore.unity.com/packages/tools/utilities/dll-hunter-201316)** | FREE | 4.0★ | Выявление лишних DLL |

### 3.9 **Не рекомендовано** для ProjectC

| Ассет | Причина |
|-------|---------|
| "Performance HUD" generic | Graphy бесплатно и лучше |
| Платные FPS-мониторы | Graphy (free) покрывает все потребности |
| "Code Profiler" ассеты | Unity Profiler встроен и мощнее |

---

## 4. План Внедрения — По Подсистемам

### 4.1 Phase 0: Infrastructure (1 день) — ProfilerMarker во все Update

Создать единый файл `ProjectCPerfCounters.cs` в `Assets/_Project/Scripts/Core/`:

```csharp
// ProjectC: Performance Counters — T-PERF-01
// Design: docs/world/admin_tool/perfomance/PERFORMANCE_MONITORING_RESEARCH.md
using Unity.Profiling;

namespace ProjectC.Core
{
    public static class ProjectCPerfCounters
    {
        // === AI ===
        public static readonly ProfilerMarker NpcBrainUpdate = new("AI.NpcBrain.Update");
        public static readonly ProfilerCounter<int> ActiveNpcs = new(
            ProfilerCategory.AI, "AI.ActiveNpcs", ProfilerMarkerDataUnit.Count);
        public static readonly ProfilerMarker NpcSocialTick = new("AI.NpcSocialBrain.Tick");
        public static readonly ProfilerMarker NpcSpawnerTick = new("AI.NpcSpawner.Tick");

        // === Ships ===
        public static readonly ProfilerMarker ShipControllerUpdate = new("Ship.Controller.Update");
        public static readonly ProfilerMarker ShipControllerFixedUpdate = new("Ship.Controller.FixedUpdate");
        public static readonly ProfilerCounter<int> ActiveShips = new(
            ProfilerCategory.Scripts, "Ship.ActiveCount", ProfilerMarkerDataUnit.Count);
        public static readonly ProfilerMarker ShipFuelUpdate = new("Ship.FuelSystem.Update");

        // === Clouds ===
        public static readonly ProfilerMarker CloudManagerUpdate = new("Clouds.Manager.Update");
        public static readonly ProfilerMarker CloudLayerUpdate = new("Clouds.Layer.Update");
        public static readonly ProfilerCounter<int> VisibleClouds = new(
            ProfilerCategory.Rendering, "Clouds.VisibleCount", ProfilerMarkerDataUnit.Count);

        // === World Streaming ===
        public static readonly ProfilerMarker StreamingUpdate = new("World.Streaming.Update");
        public static readonly ProfilerMarker ChunkLoadOp = new("World.Streaming.ChunkLoad");
        public static readonly ProfilerMarker ChunkUnloadOp = new("World.Streaming.ChunkUnload");
        public static readonly ProfilerCounter<int> LoadedChunks = new(
            ProfilerCategory.Scripts, "World.LoadedChunks", ProfilerMarkerDataUnit.Count);

        // === Combat ===
        public static readonly ProfilerMarker CombatServerTick = new("Combat.Server.Tick");
        public static readonly ProfilerMarker TargetLockUpdate = new("Combat.TargetLock.Update");
        public static readonly ProfilerCounter<int> ActiveCombats = new(
            ProfilerCategory.Scripts, "Combat.ActiveCount", ProfilerMarkerDataUnit.Count);

        // === Player ===
        public static readonly ProfilerMarker PlayerUpdate = new("Player.NetworkPlayer.Update");
        public static readonly ProfilerMarker CameraUpdate = new("Player.Camera.Update");

        // === Network ===
        public static readonly ProfilerCounter<int> RpcSentPerFrame = new(
            ProfilerCategory.Network, "NGO.RPC.Sent", ProfilerMarkerDataUnit.Count);
        public static readonly ProfilerCounter<int> RpcReceivedPerFrame = new(
            ProfilerCategory.Network, "NGO.RPC.Received", ProfilerMarkerDataUnit.Count);
        public static readonly ProfilerCounter<int> NetworkVarSyncs = new(
            ProfilerCategory.Network, "NGO.VarSyncs", ProfilerMarkerDataUnit.Count);

        // === Misc ===
        public static readonly ProfilerMarker CraftingServerTick = new("Crafting.Server.Tick");
        public static readonly ProfilerMarker DockingUpdate = new("Docking.Update");
        public static readonly ProfilerMarker DayNightUpdate = new("DayNight.Controller.Update");
        public static readonly ProfilerMarker WindUpdate = new("Wind.Manager.Update");
        public static readonly ProfilerMarker FloatingOriginUpdate = new("World.FloatingOrigin.Update");
    }
}
```

### 4.2 Phase 1: Quick Wins (0.5 дня)

| Задача | Файлы | Изменение |
|--------|-------|-----------|
| Установить Graphy | — | Add to My Assets на Asset Store → Install in Unity |
| ProfilerMarker в NpcBrain.Update | `NpcBrain.cs` | `using var _ = ProjectCPerfCounters.NpcBrainUpdate.Auto();` |
| ProfilerMarker в ShipController | `ShipController.cs` | Маркеры на Update и FixedUpdate |
| ProfilerMarker в CloudManager | `CloudManager.cs` | Маркер на Update |
| ProfilerMarker в WorldStreamingManager | `WorldStreamingManager.cs` | Маркер на корутину |
| ProfilerMarker в NetworkPlayer | `NetworkPlayer.cs` | Маркер на Update |
| ProfilerCounter для ActiveNpcs | `NpcBrain.cs` | `ProjectCPerfCounters.ActiveNpcs.Value = s_activeCount;` |
| ProfilerCounter для ActiveShips | `ShipController.cs` | Статика `s_activeCount` |

### 4.3 Phase 2: Runtime Stats HUD (1 день)

На базе Graphy + кастомные графики:

```csharp
public class ProjectCPerfHUD : MonoBehaviour
{
    private GraphyManager _graphy;
    
    private void Start()
    {
        _graphy = FindAnyObjectByType<GraphyManager>();
        if (_graphy == null) return;
        
        // Кастомные графики для ProjectC
        _graphy.AddGraph("NPCs", GraphyLookup.Category.Scripts);
        _graphy.AddGraph("Ships", GraphyLookup.Category.Scripts);
        _graphy.AddGraph("FPS", GraphyLookup.Category.Scripts);
    }
    
    private void Update()
    {
        // Обновление каждые 0.5с
        if (Time.frameCount % 30 != 0) return;
        
        var graphModule = _graphy.GetModule<ScriptsModule>();
        graphModule.UpdateGraph("NPCs", NpcBrain.ActiveCount);
        graphModule.UpdateGraph("Ships", ShipController.ActiveShipCount);
    }
}
```

Или, как альтернатива — использовать `ProfilerRecorder` для чтения метрик в HUD:

```csharp
public class PerfRecorderHUD : MonoBehaviour
{
    private ProfilerRecorder _drawCallsRecorder;
    private ProfilerRecorder _trianglesRecorder;
    private ProfilerRecorder _gcAllocRecorder;
    
    private void OnEnable()
    {
        _drawCallsRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Draw Calls Count");
        _trianglesRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Triangles Count");
        _gcAllocRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory, "GC Reserved Memory");
    }
    
    private void OnDisable()
    {
        _drawCallsRecorder.Dispose();
        _trianglesRecorder.Dispose();
        _gcAllocRecorder.Dispose();
    }
}
```

### 4.4 Phase 3: CPU Budget Tracker (1 день)

Dashboard, проверяющий бюджеты по категориям:

```csharp
public class CpuBudgetTracker : MonoBehaviour
{
    [Serializable]
    public class BudgetEntry
    {
        public string Name;
        public ProfilerCategory Category;
        public double BudgetMs60fps; // 16.6ms лимит
        public double BudgetMs30fps; // 33.3ms лимит
    }
    
    [SerializeField] private BudgetEntry[] _budgets = {
        new() { Name = "Scripts", BudgetMs60fps = 8.0, BudgetMs30fps = 20.0 },
        new() { Name = "Render",  BudgetMs60fps = 5.0, BudgetMs30fps = 10.0 },
        new() { Name = "Physics", BudgetMs60fps = 3.0, BudgetMs30fps = 5.0 },
        new() { Name = "Network", BudgetMs60fps = 1.0, BudgetMs30fps = 2.0 },
    };
    
#if DEVELOPMENT_BUILD
    private void Update()
    {
        foreach (var budget in _budgets)
        {
            var recorder = ProfilerRecorder.StartNew(budget.Category, "Main Thread");
            if (recorder.Valid && recorder.LastValue > budget.BudgetMs60fps)
                Debug.LogWarning($"[PERF] {budget.Name}: {recorder.LastValue:F1}ms > budget {budget.BudgetMs60fps}ms");
            recorder.Dispose();
        }
    }
#endif
}
```

### 4.5 Phase 4: NGO Metrics Collector (1 день)

Для сбора и вывода сетевых метрик:

```csharp
using Unity.Netcode;
using Unity.Profiling;

public class NgoMetricsCollector : MonoBehaviour
{
    public static NgoMetricsCollector Instance { get; private set; }
    
    private NetworkManager _nm;
    private float _updateInterval = 1f;
    private float _timer;
    
    // Простейшие счётчики
    public int RpcCount { get; private set; }
    public int VarSyncCount { get; private set; }
    public int ActiveConnections { get; private set; }
    public float RttMs { get; private set; }
    
    private void Awake() => Instance = this;
    
    private void Update()
    {
        if (_nm == null) { _nm = NetworkManager.Singleton; return; }
        if (!_nm.IsListening) return;
        
        _timer += Time.deltaTime;
        if (_timer < _updateInterval) return;
        _timer = 0f;
        
        // NGO 2.x метрики
        if (_nm.NetworkMetrics != null)
        {
            // NetworkManager.Singleton.NetworkMetrics — если доступен
        }
        
        // CoPlay Transport
        var transport = _nm.NetworkConfig.NetworkTransport;
        if (transport != null)
        {
            // Попробовать привести к CoPlayTransport
        }
    }
}
```

---

## 5. CPU/GPU Бюджеты по Подсистемам

### 5.1 Бюджеты @60fps (16.6ms)

| Подсистема | Бюджет (ms) | % от кадра | Критичность |
|------------|-------------|------------|-------------|
| **Scripts Total** | 8.0 | 48% | 🔴 |
│ ├ AI (NPC Update) | 2.0 | 12% | 🔴 |
│ ├ Ship Physics | 1.5 | 9% | 🟡 |
│ ├ Player Input/Move | 0.5 | 3% | 🟢 |
│ ├ Clouds Update | 1.0 | 6% | 🟡 |
│ ├ World Streaming | 1.0 | 6% | 🟡 |
│ ├ Combat | 0.5 | 3% | 🟢 |
│ ├ Docking/Crafting | 0.5 | 3% | 🟢 |
│ └ Other | 1.0 | 6% | 🟡 |
| **Render Total** | 5.0 | 30% | 🔴 |
│ ├ Opaque | 2.0 | 12% | 🔴 |
│ ├ Transparent (Clouds) | 1.5 | 9% | 🔴 |
│ ├ Post-processing | 0.5 | 3% | 🟢 |
│ └ Shadows | 1.0 | 6% | 🟡 |
| **Physics** | 3.0 | 18% | 🟡 |
| **Network** | 1.0 | 6% | 🟢 |
| **VSync/Other** | ~0 | ~0% | — |

### 5.2 Бюджеты @30fps (33.3ms) — fallback для слабых машин

| Категория | Бюджет (ms) |
|-----------|-------------|
| Scripts | 20.0 |
| Render | 10.0 |
| Physics | 5.0 |
| Network | 2.0 |

### 5.3 Adaptive Quality Scaling

При падении FPS < 30 последовательно:
1. Снизить Shadow Resolution (1 click in URP Asset)
2. Уменьшить Shadow Distance (2000м → 1000м → 500м)
3. Отключить Post-processing
4. Снизить LOD Bias
5. Уменьшить Particle Count (Clouds)
6. Уменьшить Draw Distance (GenerationRadius 10000м → 5000м)

---

## 6. Рендер-специфичный Анализ (URP 17.5)

### 6.1 Что проверить в URP Asset

| Параметр | Рекомендация | Инструмент |
|----------|-------------|------------|
| **SRP Batcher** | ✅ Включен | Проверить `SRP Batcher` в URP Asset |
| **GPU Instancing** | ✅ Включен | Frame Debugger → проверить Instance batches |
| **Shadow Resolution** | 2048 (средние) / 1024 (бюджет) | URP Asset → Shadows |
| **Shadow Distance** | 500-1000м | URP Asset → Shadows |
| **Shadow Cascades** | 2 каскада | URP Asset → Shadows |
| **Main Light** | Per Pixel | URP Asset → Lighting |
| **Additional Lights** | Per Vertex (или Off) | URP Asset → Lighting |
| **HDR** | Off (если не нужен) | URP Asset → Post Processing |
| **Post Processing** | Volume Blend Time = 0.5s | URP Asset → Post Processing |
| **Terrain Holes** | Off (если не нужны) | URP Asset → Terrain |

### 6.2 Cloud System — Рендер

Cloud System — потенциально самый тяжёлый визуальный элемент:

| Компонент | Что делает | Метрика | Потенциальная проблема |
|-----------|-----------|---------|----------------------|
| **NearCloudRenderer** | Billboard/Sphere облака вблизи | ~280 облаков (80+120+80) | Draw calls × material count |
| **DistantCloudManager** | Далёкие облака (5-15km) | 140 облаков | Overdraw |
| **HorizonVeilRenderer** | Veil-шейдер | 1 pass | Fill rate |
| **VeilRaymarchMesh** | Raymarch veil | 1 fullscreen quad | GPU heavy |

**Рекомендации по облакам:**
- Перевести все облака на SRP Batcher (единый материал)
- Настроить LOD для NearCloudRenderer (дальние → billboard, ближние → sphere)
- Использовать GPU Instancing для одинаковых облаков
- Проверить Culling Group для облаков вне экрана
- `Occlusion Culling` если облака за горами

### 6.3 Frame Debugger Audit Checklist

- [ ] Сколько draw calls на кадр в загруженной сцене?
- [ ] SRP Batcher активен? (жёлтая иконка в Frame Debugger)
- [ ] Какие материалы НЕ батчатся? (красный текст "SRP Batch not compatible")
- [ ] Есть ли избыточные SetPass Calls?
- [ ] Сколько объектов в Shadow map pass?
- [ ] Transparent objects sorted correctly?

---

## 7. Сеть — NGO 2.13 + CoPlay 8.20

### 7.1 NGO NetworkMetrics API

```csharp
// Проверить доступность в Unity 6000.5
if (NetworkManager.Singleton.NetworkMetrics != null)
{
    var metrics = NetworkManager.Singleton.NetworkMetrics;
    // metrics.TotalPacketBytesReceived
    // metrics.TotalPacketBytesSent  
    // metrics.RpcCount
}
```

### 7.2 CoPlay Transport API (WebRTC)

```csharp
// Получение метрик транспорта
var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
if (transport is CoPlayTransport coPlay)
{
    // coPlay.GetConnectionStats(out ConnectionStats stats);
    // coPlay.GetRTT(...) — если есть
}
```

### 7.3 Сетевые метрики — пороги тревоги

| Метрика | OK | Warning | Critical | Инструмент |
|---------|----|---------|----------|------------|
| RPC/sec | <50 | 50-100 | >100 | NGO Metrics / Custom ProfilerCounter |
| NetworkVariable syncs/sec | <100 | 100-200 | >200 | NGO Metrics |
| RTT (пинг) | <100ms | 100-200ms | >200ms | CoPlay RTT |
| Packet Loss | <1% | 1-5% | >5% | CoPlay Stats |
| Bandwidth Up | <64KB/s | 64-256KB/s | >256KB/s | CoPlay Stats |
| Bandwidth Down | <256KB/s | 256KB-1MB/s | >1MB/s | CoPlay Stats |

---

## 8. Memory Profiling — Сценарии для Сравнения

### 8.1 Ключевые снапшоты для Memory Profiler

| # | Сценарий | Что ищем |
|---|----------|----------|
| 1 | Bootstrap Scene (чистый) | Baseline memory |
| 2 | После загрузки World | World data size |
| 3 | 1 NPC заспавнен | NPC memory footprint |
| 4 | 10 NPC заспавнены | Scaling memory |
| 5 | 1 корабль вокруг | Ship memory |
| 6 | После 5 мин игры | Утечки (growth) |
| 7 | После крафта | Crafting allocations |
| 8 | После открытия UI | UI memory |
| 9 | После отвала клиента | Proper cleanup |
| 10 | После повторного коннекта | Memory leak on reconnect |

### 8.2 Что мониторить в памяти

| Что | Норма | Потолок | Инструмент |
|-----|-------|---------|------------|
| Total Allocated | <500MB | <1GB | Memory Profiler |
| GC Heap | <200MB | <400MB | Memory Profiler |
| Textures | <200MB | <400MB | Memory Profiler → Textures |
| Meshes | <50MB | <100MB | Memory Profiler |
| Audio | <30MB | <50MB | Memory Profiler |
| Animation | <20MB | <40MB | Memory Profiler |
| Asset Bundles | <50MB | <100MB | Memory Profiler |

---

## 9. Чеклист — Боттлнеки по Подсистемам

### 9.1 AI (NpcBrain, NpcSocialBrain)
- [ ] **ProfilerMarker** в NpcBrain.Update (critical)
- [ ] **ProfilerMarker** в NpcSocialBrain.Tick
- [ ] **ProfilerCounter** для ActiveNpcs
- [ ] LINQ в NpcBrain.cs — заменить на циклы
- [ ] NavMeshAgent cost — проверить количество активных агентов
- [ ] NpcSpawner — не спавнить все NPC разом (stagger)

### 9.2 Ships (ShipController)
- [ ] **ProfilerMarker** в ShipController.Update + FixedUpdate
- [ ] **ProfilerCounter** для ActiveShips
- [ ] Rigidbody count — сколько активных RB на сцену
- [ ] Physics timestep — 0.02 (50Hz) может быть избыточно для кораблей
- [ ] TurbulenceEffect — проверить CPU cost

### 9.3 Cloud System
- [ ] **ProfilerMarker** в CloudManager, CloudLayer, DistantCloudManager
- [ ] **ProfilerCounter** для VisibleClouds
- [ ] SRP Batcher совместимость материалов облаков
- [ ] GPU Instancing для однотипных облаков
- [ ] LOD для NearCloudRenderer
- [ ] Culling — облака вне экрана

### 9.4 World Streaming
- [ ] **ProfilerMarker** в корутине WorldStreamingManager
- [ ] **ProfilerCounter** для LoadedChunks
- [ ] Chunk Load/Unload — не блокировать main thread (async)
- [ ] FloatingOrigin — проверить float precision cost
- [ ] ProceduralChunkGenerator — CPU cost на генерацию

### 9.5 Player (NetworkPlayer)
- [ ] **ProfilerMarker** в Update
- [ ] CharacterController.Move cost
- [ ] Wind drift calculation — можно снизить частоту
- [ ] Platform carry — probe каждый кадр (опционально)

### 9.6 Combat
- [ ] **ProfilerMarker** в CombatServer
- [ ] **ProfilerMarker** в TargetLockService.Update
- [ ] **ProfilerCounter** для ActiveCombats
- [ ] Projectile pool — VfxObjectPool уже есть, но проверить

### 9.7 UI
- [ ] UIManager.Update — ProfilerMarker
- [ ] HUDManager — проверка на избыточные обновления
- [ ] UI Toolkit vs Canvas — UI Toolkit должен быть эффективнее
- [ ] CharacterWindow — lazy update (только при открытии)

---

## 10. График Внедрения (Roadmap)

### Phase 0: Infrastructure (0.5 дня)
| # | Задача | Кто |
|---|--------|-----|
| 0.1 | Создать `ProjectCPerfCounters.cs` | Mavis |
| 0.2 | Установить Graphy (Asset Store) | Dev |
| 0.3 | Установить Memory Profiler (Package Manager) | Dev |

### Phase 1: Core Instrumentation (1 день)
| # | Задача | Ticket |
|---|--------|--------|
| 1.1 | ProfilerMarker: NpcBrain.Update | T-PERF-01 |
| 1.2 | ProfilerMarker: ShipController.Update + FixedUpdate | T-PERF-02 |
| 1.3 | ProfilerMarker: CloudManager.Update | T-PERF-03 |
| 1.4 | ProfilerMarker: WorldStreamingManager (корутина) | T-PERF-04 |
| 1.5 | ProfilerMarker: NetworkPlayer.Update | T-PERF-05 |
| 1.6 | ProfilerCounter: ActiveNpcs, ActiveShips, VisibleClouds, LoadedChunks | T-PERF-06 |

### Phase 2: Runtime HUD & Tools (1 день)
| # | Задача | Ticket |
|---|--------|--------|
| 2.1 | Настроить Graphy + кастомные графики | T-PERF-07 |
| 2.2 | CpuBudgetTracker с предупреждениями | T-PERF-08 |
| 2.3 | NgoMetricsCollector (если доступно API) | T-PERF-09 |
| 2.4 | PerfRecorderHUD (альтернатива Graphy) | T-PERF-10 |

### Phase 3: Deep Audit (2 дня)
| # | Задача | Ticket |
|---|--------|--------|
| 3.1 | Memory Profiler — все 10 сценариев (см. §8.1) | T-PERF-11 |
| 3.2 | Frame Debugger — аудит рендера | T-PERF-12 |
| 3.3 | Profile Analyzer — сравнение профилей | T-PERF-13 |
| 3.4 | Проверка URP Asset настроек (см. §6.1) | T-PERF-14 |
| 3.5 | Аудит LINQ/GC.Alloc в горячих путях | T-PERF-15 |

### Phase 4: Performance Optimization (3-5 дней)
| # | Задача | Ticket |
|---|--------|--------|
| 4.1 | Оптимизация облаков (SRP Batcher, Instancing, LOD) | T-PERF-16 |
| 4.2 | Оптимизация AI (LINQ → циклы, NavMesh cost) | T-PERF-17 |
| 4.3 | Оптимизация стриминга (async loading) | T-PERF-18 |
| 4.4 | Adaptive quality scaling по FPS | T-PERF-19 |
| 4.5 | Стресс-тест: 100 NPC + 50 кораблей | T-PERF-20 |

---

## 11. Быстрый старт — Первые шаги сегодня

1. **Установить Graphy** — `Window → Asset Store → Graphy → Add to My Assets → Download → Import`
2. **Установить Memory Profiler** — `Window → Package Manager → Memory Profiler → Install`
3. **Проверить URP Settings** — открыть URP Asset → включить SRP Batcher
4. **Сделать первый Memory Snapshot** — `Memory Profiler → Capture` в Bootstrap Scene
5. **Добавить 5 ProfilerMarker'ов** — NpcBrain, ShipController, CloudManager, WorldStreamingManager, NetworkPlayer

---

## 12. Ссылки

| Ресурс | URL |
|--------|-----|
| Unity Profiler Docs | https://docs.unity3d.com/Manual/Profiler.html |
| ProfilerMarker API | https://docs.unity3d.com/ScriptReference/Unity.Profiling.ProfilerMarker.html |
| ProfilerRecorder API | https://docs.unity3d.com/ScriptReference/Unity.Profiling.ProfilerRecorder.html |
| ProfilerCounter API | https://docs.unity3d.com/ScriptReference/Unity.Profiling.ProfilerCounter-1.html |
| Memory Profiler | https://docs.unity3d.com/Packages/com.unity.memoryprofiler@latest |
| Profile Analyzer | https://docs.unity3d.com/Packages/com.unity.performance.profile-analyzer@1.1 |
| NGO NetworkMetrics | https://docs-multiplayer.unity3d.com/netcode/current/advanced-topics/metrics/ |
| Graphy (GitHub) | https://github.com/Tayx94/graphy |
| Graphy (Asset Store) | https://assetstore.unity.com/packages/tools/gui/graphy-ultimate-fps-counter-stats-monitor-debugger-105778 |
| Super Science | https://github.com/Unity-Technologies/superscience |
| URP Performance | https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/performance.html |
| Unity Best Practices | https://docs.unity3d.com/Manual/BestPracticeUnderstandingPerformanceInUnity.html |
| Unity 6 Profiler Guide | https://docs.unity3d.com/Manual/Profiler.html |

---

## 13. История изменений

| Дата | Версия | Изменения |
|------|--------|-----------|
| 2026-01 | v1.0 | Первичный research |
| 2026-07-25 | **v2.0** | Полный аудит кода всех подсистем, добавлены: ProfilerMarker coverage map для 28+ файлов, Unity 6 специфика, расширенный каталог Asset Store (12 решений), CPU/GPU бюджеты по подсистемам, рендер-анализ облаков, Memory Profiler сценарии, чеклист по подсистемам, план внедрения с тикетами |
