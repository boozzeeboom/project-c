# Scene System Test Instructions

**Дата:** 28.04.2026
**Система:** Scene-Based World Loading (Phase 1-2)

---

## Prerequisites

1. Unity project builds successfully
2. Existing chunk streaming system works (WorldStreamingManager, ChunkLoader)
3. NetworkPlayer can move and sync position

---

## Test 1: Базовая компиляция

### Шаг 1: Проверка файлов
```
Assets/_Project/Scripts/World/Scene/
├── SceneID.cs
├── SceneRegistry.cs
├── ServerSceneManager.cs
├── ClientSceneLoader.cs
├── SceneTransitionCoordinator.cs
└── SceneBoundNetworkObject.cs

Assets/_Project/Scripts/World/
├── WorldSceneManager.cs
```

### Шаг 2: Unity Build
1. Открыть Unity Editor
2. File → Build Settings
3. Проверить что нет ошибок компиляции
4. Построить билд

**Ожидаемый результат:** Build succeeds without errors

---

## Test 2: Prefab Setup (Editor)

### Шаг 1: SceneRegistry ScriptableObject
1. Создать `Assets/_Project/Data/World/SceneRegistry.asset`
2. Menu: ProjectC → World → Scene Registry
3. Параметры: GridColumns = 8, GridRows = 8

### Шаг 2: NetworkManager.prefab
1. Открыть `Assets/_Project/Prefabs/NetworkManager.prefab`
2. Добавить компоненты:
   - `ServerSceneManager` (Drag SceneRegistry в поле)
   - `SceneTransitionCoordinator` (ClientSceneLoader = null, auto-find)

### Шаг 3: Player prefab
1. Открыть Player prefab
2. Добавить компонент `ClientSceneLoader`
3. Установить SceneRegistry

### Шаг 4: World object
1. Найти или создать World root object в сцене
2. Добавить `WorldSceneManager`
3. Установить ссылки:
   - Client Scene Loader → Player's ClientSceneLoader
   - Scene Registry → SceneRegistry asset

**Ожидаемый результат:** Все ссылки установлены, нет missing references

---

## Test 3: Scene Registry Setup

### Шаг 1: Создание тестовых сцен
1. Создать 4 сцены в Unity:
   - `WorldScene_0_0`
   - `WorldScene_1_0`
   - `WorldScene_0_1`
   - `WorldScene_1_1`

2. В каждой сцене:
   - Создать пустой GameObject с позицией (0, 0, 0)
   - Добавить текст "Scene X_Y" для идентификации
   - Добавить Plane с разным цветом для визуального различия

### Шаг 2: Build Settings
1. File → Build Settings
2. Добавить все тестовые сцены в список
3. НЕ включать их в Build - они будут загружаться аддитивно

**Ожидаемый результат:** 4 сцены созданы и добавлены в Build Settings

---

## Test 4: Одиночный клиент (Host)

### Шаг 1: Запуск
1. Запустить игру в Editor (Play Mode)
2. Убедиться что NetworkManager инициализирован
3. ServerSceneManager должен появиться в Hierarchy

### Шаг 2: Проверка логов
```
[ServerSceneManager] ServerSceneManager initialized on server.
[WorldSceneManager] WorldSceneManager initialized.
[ClientSceneLoader] ClientSceneLoader initialized.
```

### Шаг 3: Движение
1. Подвигать персонажа
2. Проверить логи:
```
[PlayerChunkTracker] Player X scene changed from Scene(0, 0) to Scene(0, 0)
```

**Ожидаемый результат:** Система инициализируется без ошибок

---

## Test 5: Multiplayer (2 клиента)

### Шаг 1: Подготовка
1. Построить билд игры
2. Запустить 2 инстанса (один как Host, один как Client)

### Шаг 2: Host
1. Запустить как Host
2. ServerSceneManager должен инициализироваться

### Шаг 3: Client
1. Подключиться к Host
2. Client должен получить InitializeClientSceneServerRpc
3. ClientSceneLoader должен загрузить начальную сцену

### Шаг 4: Ожидаемые логи (Host)
```
[ServerSceneManager] Client connected: 1
[ServerSceneManager] Client 1 assigned to scene Scene(0, 0)
[ServerSceneManager] Client 1 transitioning from Scene(0, 0) to Scene(1, 0)
```

### Шаг 5: Ожидаемые логи (Client)
```
[SceneTransitionCoordinator] HandleInitialScene received for scene Scene(0, 0)
[ClientSceneLoader] Loading scene: WorldScene_0_0
[ClientSceneLoader] Scene loaded: Scene(0, 0)
[Coordinator] Client 1 confirmed scene loaded: Scene(0, 0)
```

**Проверить:** ClientRpc приходит только целевому клиенту

---

## Test 6: Scene Transition

### Шаг 1: Настройка
1. Два клиента в разных сценах
2. Клиент 1 в Scene(0, 0)
3. Клиент 2 в Scene(1, 0)

### Шаг 2: Движение к границе
1. Подвинуть Client 1 к позиции x = 79,000
2. Должен триггер preload соседней сцены

### Шаг 3: Ожидаемые логи
```
[WorldSceneManager] Preloading scene: Scene(1, 0)
[ClientSceneLoader] Loading scene: WorldScene_1_0
[ServerSceneManager] Client 1 transitioning from Scene(0, 0) to Scene(1, 0)
[ServerSceneManager] Hidden object X from client 1
[ServerSceneManager] Scene loaded: Scene(1, 0)
[ServerSceneManager] Shown object Y to client 1
```

### Шаг 4: Visibility проверка
1. Объект в Scene(0, 0) должен быть скрыт от Client 1
2. Объект в Scene(1, 0) должен быть виден Client 1

**Проверить:** SceneBoundNetworkObject.ShouldClientSeeObject() вызывается

---

## Test 7: Scene-Aware Chunk Loading

### Шаг 1: Настройка
1. Запустить Host
2. Запустить Client
3. Клиент в Scene(0, 0)

### Шаг 2: Чанк за границей сцены
1. Подвинуть клиента к границе Scene(0, 0)
2. WorldSceneManager должен загрузить Scene(1, 0)

### Шаг 3: Чанк в незагруженной сцене
1. Проверить что чанки Scene(1, 0) НЕ загружаются пока сцена не загружена
2. После загрузки сцены - чанки должны начать загружаться

### Ожидаемые логи
```
[WorldSceneManager] Scene loaded: Scene(1, 0)
[WorldStreamingManager] Scene filter updated: 9 scenes
[WorldStreamingManager] Chunk (41, 0) now allowed (scene loaded)
```

---

## Test 8: NGO CheckObjectVisibility

### Шаг 1: Spawn объект
1. Создать тестовый объект с `SceneBoundNetworkObject`
2. Установить OwnedScene = Scene(0, 0)
3. Заспавнить на сервере

### Шаг 2: Visibility для разных клиентов
1. Client 1 в Scene(0, 0) → должен видеть объект
2. Client 2 в Scene(1, 0) → должен НЕ видеть объект

### Ожидаемые логи (Server)
```
[SceneBoundNetworkObject:TestObject] CheckObjectVisibility: Client 1 in scene Scene(0, 0), object in scene Scene(0, 0), visible=True
[SceneBoundNetworkObject:TestObject] CheckObjectVisibility: Client 2 in scene Scene(1, 0), object in scene Scene(0, 0), visible=False
```

---

## Test 9: FloatingOriginMP Control

### Шаг 1: Within scene range (0-80k)
1. Player на позиции (50,000, 0, 50,000)
2. FloatingOriginMP должен быть DISABLED

### Шаг 2: Beyond scene range (>80k)
1. Player на позиции (100,000, 0, 0)
2. FloatingOriginMP должен быть ENABLED

### Ожидаемые логи
```
[WorldSceneManager] FloatingOriginMP DISABLED (dist=50000, threshold=90000)
[WorldSceneManager] FloatingOriginMP ENABLED (dist=100000, threshold=90000)
```

---

## Test 10: Cleanup и Edge Cases

### Тест: Client disconnect
1. Host + Client
2. Client отключается
3. Ожидаемые логи:
```
[ServerSceneManager] Client disconnected: 1
[ServerSceneManager] Client 1 disconnected, scene Scene(0, 0) unloaded
```

### Тест: Scene already loaded
1. При попытке загрузить уже загруженную сцену
2. Ожидаемое поведение: пропуск загрузки
```
[ClientSceneLoader] Scene Scene(0, 0) already loaded or loading
```

---

## Known Limitations

1. **Preload direction** - Currently only triggers for X+ and Z+ directions. Need to expand to all 4 directions.
2. **Arbitrary delay** - ShowSceneObjectsAfterLoad uses 1 second delay. Should use actual scene load confirmation.
3. **Grid bounds** - Hardcoded as 7 (0-7 for 8x8 grid). Should use SceneRegistry values.

---

## Debug Flags

Для включения debug логов:

| Component | Field |
|-----------|-------|
| ServerSceneManager | showDebugLogs = true |
| ClientSceneLoader | showDebugLogs = true |
| SceneBoundNetworkObject | _showDebugLogs = true |
| WorldSceneManager | showDebugLogs = true |
| WorldStreamingManager | showDebugHUD = true |

---

## Success Criteria

- [ ] Build succeeds without errors
- [ ] SceneRegistry ScriptableObject created
- [ ] Prefabs updated with new components
- [ ] 4 test scenes created
- [ ] Single client initializes correctly
- [ ] Two clients can connect
- [ ] Scene transition triggers at boundary
- [ ] Objects visible only in matching scene
- [ ] Chunks load only in loaded scenes
- [ ] FloatingOriginMP toggles based on distance
