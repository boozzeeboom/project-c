# Project C — Large Scale MMO: Iteration Plan

**Дата:** 18 апреля 2026  
**Проект:** ProjectC_client  
**Версия:** `v0.0.18-deep-analysis`

---

## Overview

Глубокий анализ (3 subagent) выявил **6 критических проблем интеграции**:

1. 🔴 **FloatingOriginMP конфликтует с ChunkLoader** — обе системы работают на >150k units
2. 🔴 **FloatingOriginMP jitter после телепорта** — двойной расчёт offset
3. 🟡 **WorldStreamingManager не получает события** — OnChunkLoaded/Unloaded не подключены
4. 🟡 **PlayerChunkTracker слабо связан** — NetworkPlayer не обновляет позицию
5. 🟡 **ChunkNetworkSpawner prefabs = null**
6. 🟡 **StreamingTest компоненты не подключены**

---

## Iteration 1: Fix FloatingOriginMP Jitter & Integration

**Цель:** Исправить критические баги FloatingOriginMP и определить зоны ответственности.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> F6 телепорт работает без jitter. Console показывает корректную позицию.
> FloatingOrigin и ChunkLoader НЕ конфликтуют.

### Tasks

#### 1.1 Исправить GetWorldPosition() — Jitter Fix
**Файл:** `FloatingOriginMP.cs` строки ~500-600

**Текущий код (баг):**
```csharp
Vector3 GetWorldPosition() {
    return positionSource.position - _totalOffset;  // Двойной расчёт!
}
```

**Новый код:**
```csharp
Vector3 GetWorldPosition() {
    if (positionSource == null) return transform.position;
    
    float distToOrigin = positionSource.position.magnitude;
    // Если близко к origin — уже локальная позиция
    if (distToOrigin < threshold * 0.5f) {
        return positionSource.position;
    }
    
    return positionSource.position - _totalOffset;
}
```

#### 1.2 Добавить ShouldUseFloatingOrigin() — Зоны ответственности
**Файл:** `FloatingOriginMP.cs`

```csharp
public bool ShouldUseFloatingOrigin() {
    if (positionSource == null) return false;
    return positionSource.position.magnitude > 150000f;
}
```

#### 1.3 Добавить события для синхронизации
**Файл:** `FloatingOriginMP.cs`

```csharp
public System.Action<Vector3> OnFloatingOriginTriggered;
public System.Action OnFloatingOriginCleared;
```

---

## Iteration 2: Fix WorldStreamingManager Integration

**Цель:** Подключить обратную связь от ChunkLoader к WorldStreamingManager.

**Длительность:** 1 сессия

**Критерий приёмки:** 
> WorldStreamingManager получает события OnChunkLoaded/OnChunkUnloaded.
> Console показывает "Chunk loaded: X,Y" при загрузке.

### Tasks

#### 2.1 Подписаться на ChunkLoader events
**Файл:** `WorldStreamingManager.cs`, метод `Awake()`

```csharp
private void Awake() {
    if (_instance == null) {
        _instance = this;
        DontDestroyOnLoad(gameObject);
    } else {
        Destroy(gameObject);
        return;
    }
    
    // Подписка на ChunkLoader events
    if (chunkLoader != null) {
        chunkLoader.OnChunkLoaded += OnChunkLoadedHandler;
        chunkLoader.OnChunkUnloaded += OnChunkUnloadedHandler;
    }
}

private void OnChunkLoadedHandler(ChunkId chunkId) {
    Debug.Log($"[WorldStreamingManager] Chunk loaded: {chunkId.GridX},{chunkId.GridZ}");
}

private void OnChunkUnloadedHandler(ChunkId chunkId) {
    Debug.Log($"[WorldStreamingManager] Chunk unloaded: {chunkId.GridX},{chunkId.GridZ}");
}
```

#### 2.2 Добавить логирование в ChunkLoader
**Файл:** `ChunkLoader.cs`

```csharp
public System.Action<ChunkId> OnChunkLoaded;
public System.Action<ChunkId> OnChunkUnloaded;

// В конце LoadChunkCoroutine():
OnChunkLoaded?.Invoke(chunkId);

// В конце UnloadChunk():
OnChunkUnloaded?.Invoke(chunkId);
```

---

## Iteration 3: Fix PlayerChunkTracker Integration

**Цель:** Создать надёжную связь между NetworkPlayer и PlayerChunkTracker.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> PlayerChunkTracker получает обновления позиции от NetworkPlayer.
> Сервер отправляет LoadChunk RPC при смене чанка.

### Tasks

#### 3.1 Добавить UpdatePlayerPosition() в PlayerChunkTracker
**Файл:** `PlayerChunkTracker.cs`

```csharp
/// <summary>
/// Обновить позицию игрока для трекинга.
/// Вызывается из NetworkPlayer.
/// </summary>
public void UpdatePlayerPosition(ulong clientId, Vector3 worldPosition) {
    if (!IsServer) return;
    
    // Определить текущий чанк
    var chunkId = GetChunkAtPosition(worldPosition);
    
    // Проверить смену чанка
    if (_playerChunks.TryGetValue(clientId, out var currentChunk)) {
        if (currentChunk != chunkId) {
            // Смена чанка — отправить RPC
            UnloadChunkClientRpc(clientId, currentChunk);
            LoadChunkClientRpc(clientId, chunkId);
        }
    } else {
        // Первый чанк
        LoadChunkClientRpc(clientId, chunkId);
    }
    
    _playerChunks[clientId] = chunkId;
}

private ChunkId GetChunkAtPosition(Vector3 position) {
    // Использовать WorldChunkManager
    if (chunkManager != null) {
        return chunkManager.GetChunkAtPosition(position);
    }
    // Fallback: вычислить вручную
    int gridX = Mathf.FloorToInt(position.x / CHUNK_SIZE);
    int gridZ = Mathf.FloorToInt(position.z / CHUNK_SIZE);
    return new ChunkId(gridX, gridZ);
}
```

#### 3.2 Добавить вызов из NetworkPlayer
**Файл:** `NetworkPlayer.cs`

```csharp"
private PlayerChunkTracker _playerChunkTracker;

private void Start() {
    // Найти PlayerChunkTracker
    _playerChunkTracker = FindFirstObjectByType<PlayerChunkTracker>();
}

private void FixedUpdate() {
    if (IsOwner && _playerChunkTracker != null) {
        _playerChunkTracker.UpdatePlayerPosition(OwnerClientId, transform.position);
    }
}
```

---

## Iteration 4: Setup & Test

**Цель:** Настроить компоненты в сцене и протестировать.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> F5 → телепортация работает
> F6 → телепортация на Far Peak работает без jitter
> F7 → загрузка чанков работает
> F8 → сброс origin работает

### Tasks

#### 4.1 Назначить prefabs для ChunkNetworkSpawner
**Файл:** `ChunkNetworkSpawner.cs`

1. Создать prefab для сундука с NetworkObject
2. Создать prefab для NPC с NetworkObject
3. Назначить в инспекторе

#### 4.2 Подключить StreamingTest
**Файл:** `StreamingTest.cs`

1. Назначить `positionSource` = NetworkPlayer
2. Назначить `worldStreamingManager` = WorldStreamingManager

#### 4.3 Тест F-клавиш
```
F5: Телепорт на ближний пик → Чанки загружаются
F6: Телепорт на Far Peak → Без jitter
F7: Загрузка чанков вокруг позиции → Console показывает
F8: Сброс FloatingOrigin → Origin сбрасывается
F9: Toggle grid → Grid визуализируется
F10: Toggle debug HUD → HUD показывает
```

---

## Iteration 5: Multiplayer Test

**Цель:** Протестировать синхронизацию в мультиплеере.

**Длительность:** 1-2 сессии

**Критерий приёмки:** 
> Host + Client: оба видят одинаковые загруженные чанки.
> Сервер отправляет LoadChunkClientRpc при смене чанка.

### Tasks

#### 5.1 Build Settings
1. Добавить сцену в Build
2. Запустить 2 инстанса

#### 5.2 Тест синхронизации
```
1. Host: Start as Host
2. Client: Connect to localhost
3. Host: Переместиться → PlayerChunkTracker отслеживает
4. Host: Сменить чанк → LoadChunkClientRpc отправляется
5. Client: Получает RPC → Чанк загружается
6. Client: Видит тот же контент что и Host
```

---

## 📊 Roadmap Summary

```
Iteration 1: Fix FloatingOriginMP Jitter (1-2 сессии)
  ├─ 1.1 Исправить GetWorldPosition()
  ├─ 1.2 Добавить ShouldUseFloatingOrigin()
  └─ 1.3 Добавить события синхронизации

Iteration 2: Fix WorldStreamingManager Integration (1 сессия)
  ├─ 2.1 Подписаться на ChunkLoader events
  └─ 2.2 Добавить логирование

Iteration 3: Fix PlayerChunkTracker Integration (1-2 сессии)
  ├─ 3.1 Добавить UpdatePlayerPosition()
  └─ 3.2 Добавить вызов из NetworkPlayer

Iteration 4: Setup & Test (1-2 сессии)
  ├─ 4.1 Назначить prefabs
  ├─ 4.2 Подключить StreamingTest
  └─ 4.3 Тест F-клавиш

Iteration 5: Multiplayer Test (1-2 сессии)
  ├─ 5.1 Build Settings
  └─ 5.2 Тест синхронизации
```

---

## 🗂️ Архивные документы

После завершения каждой итерации перемещать в `old_sessions/`:
- Промежуточные результаты тестирования
- Анализы артефактов
- Старые сессионные отчёты

**Сохранить в корне (актуальные):**
- `CURRENT_STATE.md` — глубокий анализ
- `ITERATION_PLAN.md` — этот план
- `01_Architecture_Plan.md` — архитектура

---

## ⚠️ Risks & Mitigations

| Риск | Вероятность | Mitigation |
|------|-------------|------------|
| Jitter не исправляется | Средняя | Subagent указал точный код для исправления |
| Конфликт систем остаётся | Средняя | Итерация 1.2 определяет зоны ответственности |
| Multiplayer test нестабилен | Высокая | Тестировать в одиночном режиме сначала |

---

## 📋 Files to Modify Summary

| Файл | Строк | Итерация | Задача |
|------|-------|----------|--------|
| FloatingOriginMP.cs | 1020 | 1 | Jitter fix + зоны ответственности |
| WorldStreamingManager.cs | 651 | 2 | Подписка на события |
| ChunkLoader.cs | 412 | 2 | Добавить события |
| PlayerChunkTracker.cs | 383 | 3 | UpdatePlayerPosition() |
| NetworkPlayer.cs | 600+ | 3 | Вызов UpdatePlayerPosition() |
| ChunkNetworkSpawner.cs | 347 | 4 | Prefabs |

---

**Автор:** Claude Code + Subagents  
**Обновлено:** 18.04.2026  
**Анализ:** 3 subagents провели глубокий анализ (44.9% context usage)