# Session Prompt: World Streaming Phase 2 — Multiplayer Integration

**Дата:** 16 апреля 2026 (обновлено 17.04.2026 16:02)  
**Проект:** ProjectC_client  
**Предыдущая сессия:** `SESSION_2026-04-14.md`  
**Статус:** ✅ ОСНОВНЫЕ КОМПОНЕНТЫ РЕАЛИЗОВАНЫ

---

## ⚠️ КРИТИЧЕСКИЕ ПРОБЛЕМЫ (17.04.2026)

### 1. HUD FloatingOriginMP НЕ показывает offset/shift в основной сцене

**Симптомы:**
- В тестовой сцене: HUD показывает Offset, Shifts — работает ✅
- В основной сцене: HUD НЕ показывает ничего ❌
- Shift counts растёт даже когда игрок стоит на месте

**Причина:**
- FloatingOriginMP не находит world roots в основной сцене
- `_worldRoots.Count == 0` → LateUpdate() сразу return
- HUD не показывается т.к. `_initialized = false` или `_worldRoots.Count == 0`

**Исправлено:**
- Добавлен `NetworkManagerController` в `excludeFromShift`
- FloatingOriginMP запускается в диагностическом режиме даже если roots не найдены
- Улучшены debug логи

### 2. Игрок ВНУТРИ WorldRoot — мир прыгает с игроком

**Симптомы:**
- При перемещении персонажа все world objects прыгают за ним
- HUD показывает Offset и Shifts но они продолжают расти даже когда стоишь

**Причина:**
- NetworkPlayer находится ВНУТРИ WorldRoot
- FloatingOriginMP сдвигает ВСЕ объекты внутри WorldRoot
- Игрок тоже сдвигается → получается что игрок и горы движутся вместе

**Решение:**
1. Player должен быть на верхнем уровне сцены (НЕ дочерним WorldRoot)
2. FloatingOriginMP исключает объекты по именам из `excludeFromShift`

### 3. FloatingOriginMP не инициализируется в основной сцене

**Проверьте:**
1. `Main Camera` имеет компонент `FloatingOriginMP`
2. `worldRootNames` включает имена объектов сцены (WorldRoot, Mountains, Clouds, etc.)
3. `showDebugLogs: true` для диагностики

---

## 📋 Цель сессии

Реализовать **Фазу 2: Multiplayer Integration** согласно архитектуре из `01_Architecture_Plan.md`.

**Фаза 1 завершена** ✅ — базовая инфраструктура стриминга работает в одиночном режиме.  
**Цель Фазы 2** — добавить поддержку мультиплеера (Host + 2+ клиентов).

### Что уже сделано (16.04.2026):

#### Базовые компоненты (из предыдущих сессий):
- ✅ `NetworkTestMenu.cs` — UI меню для тестирования мультиплеера
- ✅ `PrepareTestScene.cs` — Editor скрипт для создания тестовой сцены
- ✅ `NetworkPlayerSpawner.cs` — спавн игроков при подключении
- ✅ `NetworkManagerController.cs` — управление подключениями
- ✅ `NetworkPlayer.cs` — компонент игрока с NetworkBehaviour

#### World Streaming компоненты (Фаза 2):
- ✅ `PlayerChunkTracker.cs` — server-side трекинг позиций игроков в чанках
- ✅ `ChunkNetworkSpawner.cs` — спавн/деспавн NetworkObjects с чанками
- ✅ `FloatingOriginMP.cs` — RPC синхронизация сдвига мира
- ✅ `WorldStreamingManager.cs` — методы `LoadChunkByServerCommand/UnloadChunkByServerCommand`
- ✅ `StreamingTest.cs` — улучшен для мультиплеера (поддержка локального игрока)

### Что требуется сделать:
1. ⬜ Протестировать подключение Host + Client
2. ⬜ Синхронизировать World Streaming между клиентами
3. ⬜ Протестировать с 2+ клиентами (F2.5)

---

## 🖥️ NetworkTestMenu система

### Созданные файлы

| Файл | Строк | Назначение |
|------|-------|------------|
| `NetworkTestMenu.cs` | 136 | UI меню с Host/Client/Server кнопками |
| `PrepareTestScene.cs` | 400+ | Editor скрипт для создания тестовой сцены |
| `NetworkPlayerSpawner.cs` | 72 | Автоматический спавн игроков |

### Архитектура

```
NetworkTestMenu → NetworkManagerController → Unity.Netcode.NetworkManager
                                              ↓
                                        UnityTransport (UTP)
```

### Ключевые решения

1. **Использование NetworkManagerController** — существующий контроллер проекта
2. **SetConnectionData** — правильная настройка транспорта для подключения
3. **События NMC** — OnConnectionStatusChanged, OnPlayerConnected

### Проблемы выявленные

1. **Тестовая сцена не имеет NetworkPlayer префаба** — игроки не синхронизируются
2. **NullReferenceException в NMC** — транспорт не инициализирован
3. **Рекомендация** — использовать основную сцену `ProjectC_1.unity`

---

---

## 📖 Контекст (прочитать первым)

### Обязательно прочитать:
1. ✅ [SESSION_2026-04-14.md](./SESSION_2026-04-14.md) — что было сделано в предыдущей сессии
2. ✅ [SESSION_PROMPT_Phase1_Foundation_STATUS.md](./SESSION_PROMPT_Phase1_Foundation_STATUS.md) — детальный статус Фазы 1
3. ✅ [01_Architecture_Plan.md](./01_Architecture_Plan.md) — секции 3.3 (NGO Integration) и 4 (Фаза 2)
4. ✅ [ADR-0002_WorldStreaming_Architecture.md](./ADR-0002_WorldStreaming_Architecture.md) — полная архитектура

### Существующие компоненты (Фаза 1):
| Компонент | Строк | Статус | Назначение |
|----------|-------|--------|------------|
| `WorldChunkManager.cs` | 294 | ✅ Работает | Реестр чанков с grid-based lookup |
| `ProceduralChunkGenerator.cs` | 392 | ✅ Работает | Детерминированная генерация гор + облаков |
| `ChunkLoader.cs` | 398 | ✅ Работает | Асинхронная загрузка с fade-in/out |
| `FloatingOriginMP.cs` | 398 | ✅ Работает | Сдвиг мира + RPC support |
| `WorldStreamingManager.cs` | 423 | ✅ Работает | Координатор системы (Singleton) |
| `StreamingTest.cs` | 322 | ⚠️ Частично | Тестовый компонент (F-клавиши — проблема) |
| `WorldEditorTools.cs` | 556 | ✅ Работает | Scene Navigator + Chunk Visualizer |

### Архитектура системы:
```
WorldStreamingManager (Singleton)
├── WorldChunkManager → GetChunkAtPosition(), GetChunksInRadius()
├── ProceduralChunkGenerator → GenerateChunkAsync()
├── ChunkLoader → LoadChunk/UnloadChunk
└── FloatingOriginMP → ResetOrigin()
```

---

## 🎯 Задачи Фазы 2

### F2.1: PlayerChunkTracker — Server-side трекинг чанков

**Цель:** Сервер должен знать в каком чанке каждый игрок.

**Реализация:**
1. Создать `PlayerChunkTracker.cs` — компонент для отслеживания позиций игроков
2. На сервере: слушать `NetworkBehaviour.OnNetworkSpawn()` для подключения игроков
3. Каждый тик (или каждые N тиков): обновлять позицию игрока → вычислять чанк → сравнивать с предыдущим
4. Если игрок перешёл в новый чанк → инициировать загрузку/выгрузку

**Код:**
```csharp
public class PlayerChunkTracker : NetworkBehaviour
{
    private ChunkId _currentChunk;
    
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        
        // Подписка на обновление позиции
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            "PlayerPositionUpdate", 
            OnPlayerPositionUpdate
        );
    }
    
    public void ReportPosition(Vector3 position)
    {
        if (!IsOwner) return;
        
        // Отправляем серверу
        ReportPositionServerRpc(position);
    }
    
    [ServerRpc]
    private void ReportPositionServerRpc(Vector3 position, ServerRpcParams rpcParams = default)
    {
        var playerId = rpcParams.Receive.SenderClientId;
        var chunkId = WorldChunkManager.Instance.GetChunkAtPosition(position);
        
        if (!chunkId.Equals(_currentChunk))
        {
            // Игрок перешёл в новый чанк!
            OnChunkChanged(playerId, _currentChunk, chunkId);
            _currentChunk = chunkId;
        }
    }
}
```

### F2.2: RPC LoadChunk/UnloadChunk — Сервер → Клиент команды

**Цель:** Клиенты получают команды загрузки/выгрузки от сервера.

**Реализация:**
1. В `WorldStreamingManager` добавить RPC методы для клиентов
2. Сервер вызывает `LoadChunkClientRpc(ChunkId)` когда игрок входит в чанк
3. Сервер вызывает `UnloadChunkClientRpc(ChunkId)` когда чанк больше не нужен

**Код:**
```csharp
public class WorldStreamingManager : NetworkBehaviour
{
    // На клиенте:
    [ClientRpc]
    public void LoadChunkClientRpc(ChunkId chunkId, Vector3 worldCenter, 
        int[] peakSeeds, int cloudSeed, ClientRpcParams rpcParams = default)
    {
        // Загружаем чанк только если он нужен локальному игроку
        if (ShouldLoadChunk(chunkId))
        {
            ChunkLoader.Instance.LoadChunk(chunkId, worldCenter, peakSeeds, cloudSeed);
        }
    }
    
    // Дополнительно: batch update (F2.5 оптимизация)
    [ClientRpc]
    public void UpdateChunksClientRpc(ChunkId[] loadChunks, ChunkId[] unloadChunks)
    {
        foreach (var chunkId in unloadChunks)
            ChunkLoader.Instance.UnloadChunk(chunkId);
            
        foreach (var chunkId in loadChunks)
            ChunkLoader.Instance.LoadChunk(chunkId);
    }
}
```

### F2.3: NetworkObject Spawn/Despawn — Per-chunk спавн

**Цель:** Фермы, сундуки, NPC спавнятся/деспавнятся с чанками.

**Реализация:**
1. В `ProceduralChunkGenerator` добавить спавн NetworkObjects
2. При генерации чанка: инстанциировать префабы → `Spawn()` → трекать ID
3. При выгрузке: `Despawn()` всех NetworkObjects в чанке

**Код:**
```csharp
public class ProceduralChunkGenerator
{
    private Dictionary<ChunkId, List<ulong>> _chunkNetworkObjectIds = new();
    
    public void SpawnNetworkObjectsForChunk(ChunkId chunkId, List<FarmData> farms)
    {
        var spawnedIds = new List<ulong>();
        
        foreach (var farm in farms)
        {
            var prefab = GetFarmPrefab(farm.farmType);
            var instance = Instantiate(prefab, farm.position, Quaternion.identity);
            
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
                spawnedIds.Add(networkObject.NetworkObjectId);
            }
        }
        
        _chunkNetworkObjectIds[chunkId] = spawnedIds;
    }
    
    public void DespawnNetworkObjectsForChunk(ChunkId chunkId)
    {
        if (!_chunkNetworkObjectIds.TryGetValue(chunkId, out var ids)) return;
        
        foreach (var objId in ids)
        {
            if (NetworkManager.Singleton.SpawnManager.IsSpawned(objId))
            {
                var obj = NetworkManager.Singleton.SpawnManager.GetNetworkObject(objId);
                obj.Despawn();
            }
        }
        
        _chunkNetworkObjectIds.Remove(chunkId);
    }
}
```

### F2.4: FloatingOriginMP синхронизация

**Цель:** Сдвиг мира инициируется сервером и синхронизируется на всех клиентах.

**Реализация:**
1. `FloatingOriginMP` должен работать только на сервере для принятия решений
2. Клиенты получают `WorldShiftClientRpc(Vector3 offset)` и применяют сдвиг
3. Все объекты сдвигаются, включая NetworkObjects

**Код:**
```csharp
public class FloatingOriginMP : NetworkBehaviour
{
    [ServerRpc]
    public void RequestWorldShiftServerRpc(ServerRpcParams rpcParams = default)
    {
        // Проверяем позицию всех игроков (или host-камеры)
        var maxDistance = GetMaxPlayerDistanceFromOrigin();
        
        if (maxDistance > threshold)
        {
            Vector3 offset = CalculateShiftOffset(maxDistance);
            ApplyWorldShift(offset);
            WorldShiftClientRpc(offset); // Синхронизация
        }
    }
    
    [ClientRpc]
    public void WorldShiftClientRpc(Vector3 offset)
    {
        // На клиенте: сдвигаем всё
        ShiftAllWorldObjects(offset);
        _totalOffset += offset;
    }
}
```

### F2.5: Тест — 2 клиента

**Цель:** Проверить работу стриминга в мультиплеере.

**Сценарий тестирования:**
1. Host запускает игру
2. Клиент подключается
3. Host перемещается в новый чанк → клиент должен получить команду загрузки
4. Оба видят одинаковые горы и облака (детерминизм)
5. Фермы/NPC спавнятся синхронно
6. Floating Origin работает корректно для обоих

---

## 🔧 Ключевые файлы для работы

### Новые файлы:
- `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs` — F2.1

### Модифицируемые файлы:
- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs` — F2.2 (добавить RPC)
- `Assets/_Project/Scripts/World/Streaming/ProceduralChunkGenerator.cs` — F2.3 (добавить спавн)
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` — F2.4 (добавить синхронизацию)

### Тестовый компонент:
- `Assets/_Project/Scripts/World/Streaming/StreamingTest.cs` — обновить для мультиплеера

---

## ⚠️ Известные проблемы из Фазы 1

### 1. F-клавиши НЕ работают (КРИТИЧНО — высокий приоритет)
- **Статус:** ❌ Требуется исправление
- **Описание:** 
  - При нажатии F5-F10 ничего не происходит
  - Console не показывает логи `[StreamingTest] Camera found`
  - HUD не отображается
- **Симптомы:**
  - `[StreamingTest]` логи НЕ появляются
  - Телепортация НЕ работает
  - Chunk Grid не переключается

**План отладки:**
1. Проверить что `StreamingTest.cs` КОМПОНЕНТ ДОБАВЛЕН на объект в сцене
2. Проверить что `StreamingTest` компонент имеет галочку Enabled
3. Добавить Debug.Log в Start() для проверки инициализации
4. Проверить что `Camera.main` возвращает камеру
5. Проверить что `Update()` вызывается (добавить Debug.Log)

**Возможные причины:**
1. `StreamingTest` компонент не добавлен на объект
2. `Camera.main` возвращает null (камера создаётся после)
3. `Update()` не вызывается (скрипт отключен)
4. Объект с `StreamingTest` уничтожается при старте
5. Конфликт с другими скриптами управления камерой

**Быстрый тест:**
```csharp
// В Start() добавить:
Debug.Log($"[StreamingTest] Start called! Camera={_mainCamera?.name ?? "NULL"}");

// В Update() добавить:
if (Input.GetKeyDown(KeyCode.F5)) {
    Debug.Log("[StreamingTest] F5 pressed!");
    // ...
}
```

### 2. FloatingOriginMP не найден (INFO)
- **Статус:** ⚠️ Warning, не критично
- **Описание:** `FloatingOriginMP not found. Large world coordinate support disabled.`
- **Причина:** FloatingOriginMP не добавлен на сцену или не найден
- **Решение:** Добавить FloatingOriginMP компонент или игнорировать (опционально)

### 3. ShipModuleManager NullReferenceException (НЕ связано со стримингом)
- **Статус:** ⚠️ Существующий баг
- **Описание:** `NullReferenceException: Object reference not set to an instance of an object` в ShipModuleManager.cs:286
- **Причина:** Слоты модулей не назначены в ShipFlightClass
- **Решение:** Это отдельный баг, не связан с World Streaming

---

## � Документация для обновления

После завершения Фазы 2 обновить:
- `docs/world/LargeScaleMMO/SESSION_PROMPT_Phase2_MultiplayerIntegration_STATUS.md` — статус задач
- `docs/world/LargeScaleMMO/SESSION_YYYY-MM-DD.md` — summary сессии
- `docs/CHANGELOG.md` — что было добавлено

---

## ✅ Критерии приёмки

| ID | Критерий | Проверка |
|----|----------|----------|
| F2.1 | Сервер знает в каком чанке каждый игрок | Лог в Console показывает смену чанков |
| F2.2 | Клиенты получают команды загрузки/выгрузки | ClientRpc вызывается при смене чанка |
| F2.3 | NetworkObjects спавнятся/деспавнятся с чанками | Фермы/NPC появляются при загрузке, исчезают при выгрузке |
| F2.4 | FloatingOrigin синхронизирован на всех клиентах | Сдвиг происходит одновременно на server + client |
| F2.5 | 2 клиента видят одинаковый мир | Host + Client: горы, облака, объекты идентичны |

---

## 🎮 Управление тестированием (F-клавиши)

| Клавиша | Действие |
|---------|----------|
| F5 | Телепортация к следующей точке |
| F6 | Телепортация к предыдущей точке |
| F7 | Загрузить чанки вокруг позиции |
| F8 | Сбросить FloatingOrigin |
| F9 | Toggle Chunk Grid визуализация |
| F10 | Toggle Debug HUD |

---

## �📁 Структура результата

```
Assets/_Project/Scripts/World/Streaming/
├── PlayerChunkTracker.cs         ← НОВЫЙ (F2.1)
├── WorldStreamingManager.cs      ← МОДИФИЦИРОВАН (F2.2)
├── ProceduralChunkGenerator.cs   ← МОДИФИЦИРОВАН (F2.3)
├── FloatingOriginMP.cs           ← МОДИФИЦИРОВАН (F2.4)
├── StreamingTest.cs              ← ОБНОВЛЁН (F2.5)
├── WorldChunkManager.cs          ← БЕЗ ИЗМЕНЕНИЙ
└── ChunkLoader.cs                ← БЕЗ ИЗМЕНЕНИЙ

docs/world/LargeScaleMMO/
├── SESSION_PROMPT_Phase2_MultiplayerIntegration.md ← Этот документ
├── SESSION_PROMPT_Phase2_MultiplayerIntegration_STATUS.md ← Создать после
└── SESSION_YYYY-MM-DD.md        ← Summary после
```

---

## 🔗 Связанные документы

- [01_Architecture_Plan.md](./01_Architecture_Plan.md) — секции 3.3, 4 (Фаза 2)
- [ADR-0002_WorldStreaming_Architecture.md](./ADR-0002_WorldStreaming_Architecture.md)
- [SESSION_2026-04-14.md](./SESSION_2026-04-14.md) — предыдущая сессия
- [TESTING_INSTRUCTIONS.md](./TESTING_INSTRUCTIONS.md) — обновить после