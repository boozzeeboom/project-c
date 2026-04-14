# Session Prompt: World Streaming Phase 2 — Multiplayer Integration

**Дата:** 14 апреля 2026 (следующая сессия)  
**Проект:** ProjectC_client  
**Предыдущая сессия:** `SESSION_2026-04-14.md`

---

## 📋 Цель сессии

Реализовать **Фазу 2: Multiplayer Integration** согласно архитектуре из `01_Architecture_Plan.md`.

---

## 📖 Контекст (прочитать первым)

### Обязательно прочитать:
1. ✅ [SESSION_2026-04-14.md](./SESSION_2026-04-14.md) — что было сделано в предыдущей сессии
2. ✅ [01_Architecture_Plan.md](./01_Architecture_Plan.md) — секции 3.3 (NGO Integration) и 4 (Фаза 2)
3. ✅ [ADR-0002_WorldStreaming_Architecture.md](./ADR-0002_WorldStreaming_Architecture.md) — полная архитектура

### Статус Фазы 1:
Все 5 задач Фазы 1 **завершены** ✅:
- WorldChunkManager — реестр чанков
- ProceduralChunkGenerator — детерминированная генерация
- ChunkLoader — асинхронная загрузка/выгрузка
- FloatingOriginMP — сдвиг мира + RPC
- WorldEditorTools — Chunk Visualizer

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

### 1. F-клавиши не работают
- **Статус:** ❌ Требуется исправление
- **Описание:** При старте в консоли есть ошибки
- **Приоритет:** Высокий — нужно исправить для тестирования

**План исправления:**
1. Запустить Unity Editor и получить полный лог ошибок
2. Проверить `StreamingTest.cs` — убедиться что `Update()` вызывается
3. Проверить что `WorldStreamingManager.Instance` инициализирован

### 2. Возможные проблемы:
- `WorldStreamingManager` не наследует `NetworkBehaviour` (пока)
- `StreamingTest` должен работать только в одиночном режиме (без сети)
- `FloatingOriginMP` должен работать и на клиенте для применения сдвига

---

## 📝 Документация для обновления

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

## 📁 Структура результата

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