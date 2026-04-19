# Iteration 3: Session Start — PlayerChunkTracker Integration

**Дата:** 18.04.2026, 17:05  
**Предыдущие итерации:** ✅ I1 (FloatingOriginMP Jitter fix), ✅ I2 (ChunkLoader events)  
**Цель сессии:** Создать надёжную связь NetworkPlayer ↔ PlayerChunkTracker

---

## 📊 Состояние после Iteration 2

### ✅ Что реализовано:

| Компонент | Статус |
|-----------|--------|
| FloatingOriginMP.GetWorldPosition() | ✅ Исправлен jitter |
| ChunkLoader.OnChunkLoaded/Unloaded | ✅ События работают |
| WorldStreamingManager подписка | ✅ Подключено |
| PlayerChunkTracker | ⚠️ Слабая связь с NetworkPlayer |
| NetworkPlayer → PlayerChunkTracker | ❌ Нет обновления |

### 🔴 Критическая проблема Iteration 3:

**NetworkPlayer не обновляет PlayerChunkTracker.**

Это означает:
- Сервер не знает в каком чанке игрок
- RPC LoadChunk/UnloadChunk не вызываются
- Chunk streaming в мультиплеере НЕ работает

---

## 📋 Задачи Iteration 3

### 3.1 PlayerChunkTracker.cs — Добавить UpdatePlayerPosition()

```csharp
public void UpdatePlayerPosition(ulong clientId, Vector3 worldPosition)
{
    if (!IsServer) return;
    
    var chunkId = GetChunkAtPosition(worldPosition);
    
    if (_playerChunks.TryGetValue(clientId, out var currentChunk)) {
        if (currentChunk != chunkId) {
            UnloadChunkClientRpc(clientId, currentChunk);
            LoadChunkClientRpc(clientId, chunkId);
        }
    } else {
        LoadChunkClientRpc(clientId, chunkId);
    }
    
    _playerChunks[clientId] = chunkId;
}
```

### 3.2 NetworkPlayer.cs — Добавить вызов

```csharp
private PlayerChunkTracker _playerChunkTracker;

private void Start() {
    _playerChunkTracker = FindFirstObjectByType<PlayerChunkTracker>();
}

private void FixedUpdate() {
    if (IsOwner && _playerChunkTracker != null) {
        _playerChunkTracker.UpdatePlayerPosition(OwnerClientId, transform.position);
    }
}
```

---

## 🔍 Анализ кода

### PlayerChunkTracker.cs (текущее состояние)

- 383 строки
- Содержит `_playerChunks` Dictionary<ulong, ChunkId>
- Метод `GetPlayerChunk(ulong clientId)` — читает из словаря
- НО: Нет метода `UpdatePlayerPosition()`!
- Обновление происходит через корутину `UpdatePlayerChunksCoroutine()` — ненадёжно

### NetworkPlayer.cs (текущее состояние)

- 600+ строк
- Содержит `NetworkTransform`
- НЕТ ссылки на PlayerChunkTracker
- НЕТ вызова обновления позиции в PlayerChunkTracker

---

## ⚠️ Проблема с корутиной UpdatePlayerChunksCoroutine

```csharp
// Текущий код PlayerChunkTracker.UpdatePlayerChunksCoroutine():
private IEnumerator UpdatePlayerChunksCoroutine()
{
    while (true)
    {
        yield return new WaitForSeconds(0.5f);
        
        // Пытается найти NetworkPlayer через FindObjectsByType
        // НО: порядок FindObjectsByType не гарантирован
        // НО: может найти не того игрока
    }
}
```

**Решение:** Заменить корутину на прямой вызов из NetworkPlayer.

---

## 📁 Файлы для изучения

1. `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs`
2. `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

---

## 📋 План выполнения

- [ ] 1. Прочитать PlayerChunkTracker.cs полностью
- [ ] 2. Прочитать NetworkPlayer.cs полностью
- [ ] 3. Создать резервную копию обоих файлов
- [ ] 4. Добавить UpdatePlayerPosition() в PlayerChunkTracker
- [ ] 5. Добавить вызов из NetworkPlayer
- [ ] 6. Создать TEST_CHECKLIST.md
- [ ] 7. Задокументировать изменения в COMPLETION_REPORT.md

---

**Автор:** Claude Code  
**Статус:** Начало сессии
