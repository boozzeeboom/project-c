# Agent Prompts — Phase 2: World Streaming

**Дата:** 16 апреля 2026 г.  
**Проект:** ProjectC_client  
**Статус:** ✅ Готов для исполнения

---

## Промпт для network-programmer

### Зона ответственности
Server-side chunk management + RPC sync

### Задачи по приоритету

#### 1. Создать PlayerChunkTracker.cs

**Расположение:** `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs`

**Описание:** Серверный компонент, который отслеживает позицию каждого игрока и определяет какой чанк активен.

```csharp
using Unity.Netcode;
using System.Collections.Generic;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// Серверный компонент — отслеживает какой игрок в каком чанке.
    /// Управляет загрузкой/выгрузкой чанков для каждого клиента.
    /// </summary>
    public class PlayerChunkTracker : NetworkBehaviour
    {
        // Dictionary: ClientId → Current ChunkId
        private readonly Dictionary<ulong, ChunkId> _playerChunks = new();
        
        // Радиус загрузки чанков (в чанках)
        [SerializeField] private int loadRadius = 2;
        
        // Ссылка на WorldChunkManager
        private WorldChunkManager _chunkManager;
        
        public override void OnNetworkSpawn()
        {
            // Подписка на события игроков
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            _chunkManager = FindAnyObjectByType<WorldChunkManager>();
        }
        
        private void OnClientConnected(ulong clientId)
        {
            // Добавить в словарь
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            // Убрать из словаря, выгрузить чанки клиента
        }
        
        public void UpdatePlayerChunk(ulong clientId, Vector3 position)
        {
            ChunkId newChunk = _chunkManager.GetChunkAtPosition(position);
            
            if (!_playerChunks.TryGetValue(clientId, out var oldChunk) || !oldChunk.Equals(newChunk))
            {
                // Чанк изменился — обновить
                _playerChunks[clientId] = newChunk;
                OnPlayerEnteredChunk(clientId, newChunk);
            }
        }
        
        private void OnPlayerEnteredChunk(ulong clientId, ChunkId chunkId)
        {
            // Отправить клиенту команду загрузить чанк
            // LoadChunkClientRpc(chunkId, clientId)
        }
    }
}
```

**Ключевые RPC:**
```csharp
[ClientRpc]
void LoadChunkClientRpc(ChunkId chunkId, ClientRpcParams rpcParams = default);

[ClientRpc]
void UnloadChunkClientRpc(ChunkId chunkId, ClientRpcParams rpcParams = default);
```

---

#### 2. Добавить LoadChunkRpc/UnloadChunkRpc в WorldStreamingManager

**Файл:** `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs`

**Изменения:**
```csharp
// Добавить RPC для сервера → клиент
[ClientRpc]
private void LoadChunkClientRpc(ChunkId chunkId, ClientRpcParams rpcParams = default)
{
    if (IsServer) return; // Не выполнять на сервере
    LoadChunksAroundPlayer(GetPlayerPosition(), 1);
}

[ClientRpc]
private void UnloadChunkClientRpc(ChunkId chunkId, ClientRpcParams rpcParams = default)
{
    if (IsServer) return;
    // Выгрузить чанк
}
```

---

#### 3. Добавить WorldShiftRpc в FloatingOriginMP

**Файл:** `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`

**Изменения:**
```csharp
// Применить сдвиг от сервера (НЕ вычислять самостоятельно!)
public void ApplyServerShift(Vector3 offset)
{
    ApplyShiftToAllRoots(offset);
    _totalOffset += offset;
    _shiftCount++;
    OnWorldShifted?.Invoke(offset);
}

[ClientRpc]
private void WorldShiftClientRpc(Vector3 offset, ClientRpcParams rpcParams = default)
{
    ApplyServerShift(offset);
}
```

---

### Критерии приёмки

| Тест | Критерий |
|------|----------|
| T1 | PlayerChunkTracker логирует: `Player {id} entered chunk ({x}, {z})` |
| T2 | LoadChunkClientRpc вызывается при смене чанка |
| T3 | FloatingOriginMP не вычисляет сдвиг самостоятельно (только от сервера) |

---

## Промпт для gameplay-programmer

### Зона ответственности
NPC/Chest spawn per chunk + game logic

### Задачи по приоритету

#### 1. Создать ChunkNetworkSpawner.cs

**Расположение:** `Assets/_Project/Scripts/World/Streaming/ChunkNetworkSpawner.cs`

**Описание:** Server-side спавн/деспавн NetworkObjects при загрузке/выгрузке чанков.

```csharp
using Unity.Netcode;
using System.Collections.Generic;

namespace ProjectC.World.Streaming
{
    /// <summary>
    /// Серверный компонент — спавнит/деспавнит NetworkObjects
    /// (сундуки, NPC, квесты) при загрузке/выгрузке чанков.
    /// </summary>
    public class ChunkNetworkSpawner : NetworkBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject chestPrefab;
        [SerializeField] private GameObject npcPrefab;
        
        // Реестр: ChunkId → список NetworkObjectId
        private readonly Dictionary<ChunkId, List<ulong>> _spawnedObjects = new();
        
        public void SpawnForChunk(ChunkId chunkId, WorldChunk chunk)
        {
            // Спавн сундуков из chunk.Farms
            foreach (var farm in chunk.Farms)
            {
                var obj = Instantiate(chestPrefab, farm.worldPosition, Quaternion.identity);
                var networkObj = obj.GetComponent<NetworkObject>();
                networkObj.Spawn();
                
                if (!_spawnedObjects.ContainsKey(chunkId))
                    _spawnedObjects[chunkId] = new List<ulong>();
                _spawnedObjects[chunkId].Add(networkObj.NetworkObjectId);
            }
        }
        
        public void DespawnForChunk(ChunkId chunkId)
        {
            if (!_spawnedObjects.TryGetValue(chunkId, out var objectIds))
                return;
            
            foreach (var objId in objectIds)
            {
                if (NetworkManager.Singleton.SpawnManager.IsSpawned(objId))
                {
                    var obj = NetworkManager.Singleton.SpawnManager.GetNetworkObject(objId);
                    obj.Despawn();
                }
            }
            
            _spawnedObjects.Remove(chunkId);
        }
    }
}
```

---

#### 2. Интеграция с ChestContainer

**Файл:** `Assets/_Project/Scripts/Core/ChestContainer.cs`

**Изменения:**
```csharp
// Добавить свойство ChunkId
public ChunkId OwningChunkId { get; set; }

// Привязка к чанку при создании
public void SetChunk(ChunkId chunkId)
{
    OwningChunkId = chunkId;
}
```

---

### Критерии приёмки

| Тест | Критерий |
|------|----------|
| T1 | При загрузке чанка спавнятся сундуки |
| T2 | При выгрузке чанка сундуки деспавнятся |
| T3 | Сундуки видны всем клиентам |

---

## Промпт для unity-specialist

### Зона ответственности
FloatingOriginMP sync + scene management

### Задачи по приоритету

#### 1. FloatingOriginMP — режим "серверный синхронизированный"

**Файл:** `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`

**Режимы работы:**
```csharp
public enum OriginMode
{
    Local,           // Локальный сдвиг (singleplayer)
    ServerSynced,     // Сдвиг от сервера (multiplayer client)
    ServerAuthority   // Сервер инициирует сдвиг (multiplayer host/server)
}

[Header("Mode")]
public OriginMode mode = OriginMode.ServerSynced;
```

**Изменения для ServerSynced:**
```csharp
void LateUpdate()
{
    if (mode == OriginMode.Local)
    {
        // Старый код — локальный сдвиг
    }
    else if (mode == OriginMode.ServerSynced)
    {
        // НЕ вычислять сдвиг — только принимать от сервера
        // Действия не требуются
    }
}

// Вызывается сервером через WorldShiftClientRpc
public void ApplyServerShift(Vector3 offset) { ... }
```

---

#### 2. Preload System

**Файл:** `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs`

```csharp
[Header("Preload")]
[Tooltip("预加载相邻 чанков数量")]
[SerializeField] private int preloadRadius = 1;

[Tooltip("预加载延迟 (秒) — после входа в новый чанк")]
[SerializeField] private float preloadDelay = 2f;

private float _lastPreloadTime = 0f;

private void UpdatePreload()
{
    if (Time.time - _lastPreloadTime < preloadDelay) return;
    
    // Загрузить соседние чанки заранее
    List<ChunkId> neighborChunks = _chunkManager.GetChunksInRadius(
        GetPlayerPosition(), 
        preloadRadius
    );
    
    foreach (var chunkId in neighborChunks)
    {
        if (!_loadedChunks.Contains(chunkId))
        {
            _chunkLoader.LoadChunk(chunkId);
            _loadedChunks.Add(chunkId);
        }
    }
    
    _lastPreloadTime = Time.time;
}
```

---

### Критерии приёмки

| Тест | Критерий |
|------|----------|
| T1 | FloatingOriginMP работает в режиме ServerSynced |
| T2 | Preload загружает соседние чанки заранее |
| T3 | Нет hitching при переходе между чанками |

---

## Промпт для qa-tester

### Зона ответственности
Multiplayer test scenarios

### Тестовые сценарии

#### T1: 2 игрока, разные чанки

**Setup:**
1. Запустить Host (Окно 1)
2. Запустить Client (Окно 2), подключиться
3. Host в позиции (0, 0, 0) — чанк (0, 0)
4. Client в позиции (3000, 0, 0) — чанк (1, 0)

**Ожидаемые логи:**
```
[PlayerChunkTracker] Player 0 entered chunk (0, 0)
[PlayerChunkTracker] Player 1 entered chunk (1, 0)
[LoadChunkClientRpc] Loading chunk (0, 0) for client 0
[LoadChunkClientRpc] Loading chunk (1, 0) for client 1
```

**Критерий успеха:** Оба видят свой контент (горы, облака, сундуки)

---

#### T2: Игрок переходит в другой чанок

**Setup:**
1. Host в позиции (0, 0, 0) — чанк (0, 0)
2. Host двигается в сторону (3000, 0, 0)

**Ожидаемые логи:**
```
[PlayerChunkTracker] Player 0 entered chunk (1, 0)
[LoadChunkClientRpc] Loading chunk (1, 0) for client 0
[UnloadChunkClientRpc] Unloading chunk (0, 0) for client 0
```

**Критерий успеха:** Соседний чанк загружается, старый выгружается

---

#### T3: Host сдвигает мир

**Setup:**
1. Host в позиции (120000, 0, 0)
2. FloatingOriginMP threshold = 100000

**Ожидаемые логи:**
```
[FloatingOriginMP] Server shift triggered: offset=(100000, 0, 0)
[WorldShiftClientRpc] Syncing shift to all clients
[FloatingOriginMP] Client received shift: offset=(100000, 0, 0)
```

**Критерий успеха:** Оба клиента в одной позиции мира (относительно друг друга)

---

#### T4: Server Authority

**Setup:**
1. Client пытается загрузить чанк без команды сервера

**Ожидаемое поведение:** Чанк НЕ загружается, только по команде сервера

**Критерий успеха:** Сервер полностью контролирует загрузку

---

## График орхестрации

```
Sprint 1 (Day 1-5):
├── Day 1-2: network-programmer
│   ├── [ ] Создать PlayerChunkTracker.cs
│   └── [ ] Добавить LoadChunkRpc в WorldStreamingManager
├── Day 3-4: unity-specialist
│   ├── [ ] FloatingOriginMP ServerSynced mode
│   └── [ ] Добавить WorldShiftClientRpc
└── Day 5: qa-tester
    └── [ ] Тест T1: 2 игрока в разных чанках

Sprint 2 (Day 6-10):
├── Day 6-7: gameplay-programmer
│   ├── [ ] Создать ChunkNetworkSpawner.cs
│   └── [ ] Chest спавнится с чанком
├── Day 8-9: network-programmer
│   ├── [ ] RequestChunkNetworkObjectsRpc
│   └── [ ] UnloadChunkRpc
└── Day 10: qa-tester
    └── [ ] Тест T2: Переход между чанками

Sprint 3 (Day 11-15):
├── Day 11-13: unity-specialist
│   ├── [ ] Preload System
│   └── [ ] Memory Budget
├── Day 14: qa-tester
│   ├── [ ] Тест T3: Сдвиг мира
│   └── [ ] Тест T4: Server Authority
└── Day 15: Оркестратор
    └── [ ] Финальная проверка Phase 2
```

---

## Финальный Session Prompt

```markdown
# Session Prompt: Phase 2 — World Streaming Implementation

**Дата:** 16 апреля 2026 г.
**Проект:** ProjectC_client
**Цель:** Завершить Phase 2 — Multiplayer World Streaming

## Контекст

### Что реализовано (Phase 1 ✅)
- FloatingOriginMP.cs — сдвиг мира
- WorldChunkManager.cs — реестр чанков
- ChunkLoader.cs — загрузка/выгрузка
- ProceduralChunkGenerator.cs — генерация

### Что НЕ реализовано (Critical Gaps ❌)
1. PlayerChunkTracker — сервер не знает какой игрок в каком чанке
2. LoadChunkRpc — команды загрузки чанков
3. FloatingOriginMP Server Sync — синхронизация сдвига
4. ChunkNetworkSpawner — спавн NetworkObjects

## Орхестрация

### Sprint 1: Server-Side Foundation
**network-programmer:**
1. Создать PlayerChunkTracker.cs
2. Добавить LoadChunkRpc/UnloadChunkRpc в WorldStreamingManager

**unity-specialist:**
1. Добавить WorldShiftClientRpc в FloatingOriginMP
2. FloatingOriginMP в режим ServerSynced

**qa-tester:**
1. Тест T1: 2 игрока в разных чанках

### Sprint 2: Network Object Spawn
**gameplay-programmer:**
1. Создать ChunkNetworkSpawner.cs
2. Chest/NPC спавнятся с чанком

### Sprint 3: Preload + Polish
**unity-specialist:**
1. Preload System — загрузка соседних чанков
2. Memory Budget мониторинг

## Тесты для валидации

| # | Тест | Критерий |
|---|------|----------|
| T1 | 2 игрока, разные чанки | Оба видят свой контент |
| T2 | Переход между чанками | Соседний загружается |
| T3 | Сдвиг мира | Оба в одной позиции |
| T4 | Server Authority | Сервер контролирует |

## Файлы

**Новые:**
- `PlayerChunkTracker.cs`
- `ChunkNetworkSpawner.cs`

**Модификация:**
- `WorldStreamingManager.cs` (+LoadChunkRpc, +UnloadChunkRpc)
- `FloatingOriginMP.cs` (+WorldShiftClientRpc, +ServerSynced mode)
- `ChestContainer.cs` (+ChunkId)

## Успешные критерии Phase 2

1. ✅ Сервер знает какой игрок в каком чанке
2. ✅ Клиент получает LoadChunkRpc от сервера
3. ✅ FloatingOriginMP синхронизирован
4. ✅ Сундуки/NPC спавнятся с чанком
5. ✅ Preload работает без hitching
```

---

**Статус:** ✅ Готов для начала Phase 2