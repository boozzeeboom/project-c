# ПРОМТ: Реализация World Streaming — Фаза 1 (Foundation)

**Контекст проекта:**
- Unity 6 URP, MMO проект с мультиплеером (Unity NGO — Netcode for GameObjects)
- Мир радиусом ~350,000 units (XZ координаты до ±260,000)
- 5 горных массивов, 29 пиков, 890+ процедурных облачных мешей
- Текущая архитектура: единая сцена, весь мир загружен постоянно
- Floating Origin существует но buggy (сдвигает только "Mountains")
- Документация по архитектуре: `docs/world/LargeScaleMMO/`

---

## ФАЗА 1: Foundation (Без мультиплеера)

**Цель:** Создать базовую инфраструктуру стриминга мира, валидировать в одиночном режиме.

**Существующие файлы для использования:**
- `Assets/_Project/Scripts/World/Core/WorldData.cs` — ScriptableObject с данными мира
- `Assets/_Project/Scripts/World/Core/MountainMassif.cs` — данные массивов
- `Assets/_Project/Scripts/World/Core/WorldDataTypes.cs` — PeakData, FarmData, и т.д.
- `Assets/_Project/Scripts/World/Generation/MountainMeshGenerator.cs` — генерация гор
- `Assets/_Project/Scripts/World/Generation/MountainMeshBuilderV2.cs` — построение мешей
- `Assets/_Project/Scripts/World/Core/FloatingOrigin.cs` — текущий Floating Origin (нужно заменить)

---

## ЗАДАЧИ ДЛЯ РЕАЛИЗАЦИИ

### ЗАДАЧА 1.1: WorldChunkManager — Реестр чанков

**Путь:** `Assets/_Project/Scripts/World/Streaming/WorldChunkManager.cs`

**Требования:**
- Grid-based система чанков (размер чанка: 2000x2000 units)
- Реестр всех чанков мира (ChunkId: GridX, GridZ)
- Вычисление WorldBounds для каждого чанка
- Определение какие пики/фермы находятся в каждом чанке
- CloudSeed для каждого чанка (детерминированная генерация)
- Grid-based lookup: `GetChunkAtPosition(Vector3 worldPos)` → ChunkId
- `GetChunksInRadius(Vector3 centerPos, int radiusInChunks)` → List<ChunkId>

**Структура данных:**
```csharp
public struct ChunkId {
    public int GridX;
    public int GridZ;
}

public enum ChunkState { Unloaded, Loading, Loaded, Unloading }

public class WorldChunk {
    public ChunkId Id;
    public Bounds WorldBounds;
    public ChunkState State;
    public List<PeakData> Peaks;
    public List<FarmData> Farms;
    public int CloudSeed;
}
```

**Ожидаемый результат:**
- Класс `WorldChunkManager` (MonoBehaviour)
- Инициализируется при старте, строит реестр чанков на основе WorldData
- Можно запросить чанк по позиции игрока
- Можно запросить список чанков в радиусе
- [Editor] ChunkVisualizer — отображение сетки чанков в Scene View

---

### ЗАДАЧА 1.2: ProceduralChunkGenerator — Генерация содержимого чанка

**Путь:** `Assets/_Project/Scripts/World/Streaming/ProceduralChunkGenerator.cs`

**Требования:**
- По ChunkId генерирует все объекты чанка:
  - Вызывает MountainMeshGenerator для каждого пика в чанке
  - Генерирует облака из CloudSeed (детерминированно)
  - Инстанцирует фермы из FarmData
- Детерминизм: одинаковый Seed → одинаковый результат на любом запуске
- Асинхронность: генерация через coroutine (yield return для распределения нагрузки)

**Детерминированный Seed:**
```csharp
public int GenerateChunkSeed(ChunkId chunkId, int globalSeed) {
    unchecked {
        int hash = 17;
        hash = hash * 31 + chunkId.GridX;
        hash = hash * 31 + chunkId.GridZ;
        hash = hash * 31 + globalSeed;
        return hash;
    }
}
```

**Ожидаемый результат:**
- Класс ProceduralChunkGenerator (MonoBehaviour или статический)
- Метод `GenerateChunkAsync(WorldChunk chunk, Transform parentTransform)` → IEnumerator
- Генерация гор, облаков, ферм в пределах Bounds чанка

---

### ЗАДАЧА 1.3: ChunkLoader — Client-side загрузка/выгрузка

**Путь:** `Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs`

**Требования:**
- Загружает чанк по команде: `LoadChunk(ChunkId)`
- Выгружает чанк по команде: `UnloadChunk(ChunkId)`
- Управляет fade-in/fade-out для облаков (alpha fade, 1-2 секунды)
- Отслеживает статус загрузки каждого чанка
- Асинхронная загрузка через coroutine (не блокирует main thread)

**Ожидаемый результат:**
- Класс ChunkLoader (MonoBehaviour)
- Методы: LoadChunk(ChunkId), UnloadChunk(ChunkId)
- Словарь загруженных чанков с их состоянием
- Подписка на события: OnChunkLoaded, OnChunkUnloaded

---

### ЗАДАЧА 1.4: FloatingOriginMP — Мультиплеер-совместимый сдвиг

**Путь:** `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`

**Требования:**
- Замена текущего FloatingOrigin.cs
- Сдвигает ВСЕ объекты в сцене (не только "Mountains"):
  - Mountains
  - Clouds
  - Farms
  - Trade zones
  - Любые другие world objects
- Threshold: 100,000 units
- Округление сдвига до 10,000 units (избегание накопления ошибок)
- Сдвиг в LateUpdate
- Публичный метод `ResetOrigin()` — для ручного сброса при телепортации

**Подготовка к мультиплееру:**
- Метод `ApplyWorldShift(Vector3 offset)` — для будущего RPC от сервера
- Событие `OnWorldShifted(Vector3 offset)` — для подписчиков (NetworkTransform correction)

**Ожидаемый результат:**
- Класс FloatingOriginMP (MonoBehaviour)
- Автоматически находит все world roots (Mountains, Clouds, и т.д.)
- Сдвигает все синхронно
- Debug info: текущий totalOffset, количество сдвигов

---

### ЗАДАЧА 1.5: Editor Tool — SceneView Navigator + Chunk Visualizer

**Путь:** `Assets/_Project/Scripts/Editor/WorldEditorTools.cs`

**Требования:**

**A. Scene Navigator:**
- Editor Window: Tools → Project C → World → Scene Navigator
- Кнопки для телепортации к каждому из 29 пиков
- `SceneView.lastActiveSceneView.LookAtDirect()` для перемещения
- Поле ввода произвольных координат XZ

**B. Chunk Visualizer:**
- Отображение сетки чанков в Scene View (Gizmos)
- Цветовая индикация состояния чанка:
  - Зелёный = Loaded
  - Жёлтый = Loading
  - Серый = Unloaded
  - Красный = Unloading
- Toggle: Show/Hide chunk grid
- Настройка: размер сетки, радиус отображения

**Ожидаемый результат:**
- Editor Window для навигации
- OnDrawGizmos для визуализации чанков
- Меню: Tools → Project C → World → [Scene Navigator | Toggle Chunk Visualizer]

---

## ИНТЕГРАЦИЯ С СУЩЕСТВУЮЩИМ КОДОМ

### Что НЕ менять:
- `MountainMeshGenerator.cs` — используется как есть
- `MountainMeshBuilderV2.cs` — используется как есть
- `WorldData.cs`, `MountainMassif.cs`, `WorldDataTypes.cs` — данные как есть
- `NoiseUtils.cs` — утилиты шума как есть
- `NetworkManagerController.cs` — пока не трогать
- `NetworkPlayer.cs` — пока не трогать
- `ShipController.cs` — не трогать

### Что заменить:
- `FloatingOrigin.cs` → `FloatingOriginMP.cs` (замена, старый можно удалить или заархивировать)
- `WorldGenerator.cs` (legacy) → `WorldChunkManager.cs` (замена)

### Что добавить:
- `WorldChunkManager.cs` — новый
- `ProceduralChunkGenerator.cs` — новый
- `ChunkLoader.cs` — новый
- `FloatingOriginMP.cs` — новый
- `WorldEditorTools.cs` — новый

---

## КРИТЕРИИ ПРИЁМКИ ФАЗЫ 1

1. ✅ WorldChunkManager строит реестр всех чанков мира при старте
2. ✅ Можно запросить ChunkId по мировой позиции
3. ✅ Можно запросить список чанков в радиусе от позиции
4. ✅ ProceduralChunkGenerator генерирует горы + облака для одного чанка
5. ✅ Генерация детерминирована (одинаковый Seed → одинаковый результат)
6. ✅ ChunkLoader загружает/выгружает чанки асинхронно
7. ✅ FloatingOriginMP сдвигает ВСЕ объекты (Mountains + Clouds + другие)
8. ✅ Editor tool позволяет телепортироваться к любому пику
9. ✅ Chunk Visualizer показывает сетку чанков в Scene View
10. ✅ Тест: телепортация между пиками работает, мир генерируется/сдвигается корректно

---

## ТЕХНИЧЕСКИЕ ТРЕБОВАНИЯ

### Производительность:
- Генерация чанка < 500ms на main thread (распределение через coroutine)
- Fade-in/fade-out облаков: 1-2 секунды
- Chunk unload: немедленное уничтожение объектов + GC.Collect (периодически)

### Код:
- Следовать существующим конвенциям проекта (namespace, именование)
- Использовать `UnityEngine` и `UnityEditor` (для Editor tools)
- Комментарии для публичных методов
- Debug.Log для ключевых событий (загрузка/выгрузка/сдвиг)

### Тестирование:
- Протестировать в Play Mode с WorldCamera
- Телепортироваться к разным пикам
- Проверить что FloatingOriginMP сдвигает все объекты
- Проверить что чанки загружаются/выгружаются корректно
- Проверить Gizmos в Scene View

---

## ОЖИДАЕМЫЙ РЕЗУЛЬТАТ СЕССИИ

1. Все 5 задач реализованы
2. Код скомпилирован без ошибок
3. Тест в Play Mode: телепортация между пиками работает
4. Editor tool: навигация к пикам работает
5. Chunk Visualizer: сетка видна в Scene View

---

## ДОПОЛНИТЕЛЬНЫЙ КОНТЕКСТ

**Архитектура проекта:**
- `Assets/_Project/Scripts/` — основные скрипты
- `Assets/_Project/Data/` — ScriptableObject данные
- `Assets/_Project/Trade/` — торговая система
- `Assets/_Project/Prefabs/` — префабы
- `Assets/_Project/Materials/` — материалы
- `Assets/_Project/Textures/` — текстуры

**Горные массивы (5):**
1. Himalayan (Everest, K2, Kangchenjunga, и т.д.)
2. Alpine (Mont Blanc, и т.д.)
3. African (Kilimanjaro, и т.д.)
4. Andean (Aconcagua, и т.д.)
5. Alaskan (Denali, и т.д.)

**Текущие пики:** 29 штук, координаты в WorldData

**Сцена:** `Assets/ProjectC_1.unity` (единственная)

---

## ССЫЛКИ НА ДОКУМЕНТАЦИЮ

- `docs/world/LargeScaleMMO/README.md` — индекс каталога
- `docs/world/LargeScaleMMO/01_Architecture_Plan.md` — полная архитектура
- `docs/world/LargeScaleMMO/02_Technical_Research.md` — техническое исследование
- `docs/world/LargeScaleMMO/ADR-0002_WorldStreaming_Architecture.md` — ADR решения
- `docs/world/prompt_editor_navigation_large_world.md` — исходная проблема
