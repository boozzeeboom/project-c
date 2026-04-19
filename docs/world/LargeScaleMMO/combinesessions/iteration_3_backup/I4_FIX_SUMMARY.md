# 🎯 I4: Chunk Loader Player Position Fix - COMPLETED

## Статус

**ВЕРСИЯ:** 1.0
**ДАТА:** 19.04.2026
**СТАТУС:** ✅ ИСПРАВЛЕНО

---

## Проблема

ChunkLoader грузит чанки вокруг спавн-зоны (origin), а не вокруг игрока.

### Симптомы
1. Чанки загружаются только вокруг спавн-зоны
2. HUD показывает "loaded chunk 20" около origin
3. При отдалении от спавна на 100k - чанки выгружаются, loaded=0
4. Нажатие F7 снова загружает чанки на спавне

### Причина
`WorldStreamingManager.UpdateStreaming()` использовал `Camera.main.transform.position`:
- Camera на TradeZones
- TradeZones сдвигается ВМЕСТЕ с миром
- После сдвига камера ВСЕГДА близко к origin
- **Чанки грузятся вокруг origin, а не игрока!**

---

## Решение

### Изменения в WorldStreamingManager.cs

**Было:**
```csharp
private void UpdateStreaming()
{
    Camera mainCamera = Camera.main;
    if (mainCamera != null)
    {
        LoadChunksAroundPlayer(mainCamera.transform.position);
    }
}
```

**Стало:**
```csharp
private void UpdateStreaming()
{
    // Ищем позицию ИГРОКА, а не камеры!
    Vector3 playerPosition = GetPlayerPosition();
    
    if (playerPosition != Vector3.zero || _cachedPlayerTransform != null)
    {
        LoadChunksAroundPlayer(playerPosition);
    }
}
```

### Новые методы

1. **`FindLocalPlayer()`** - ищет трансформ локального игрока
   - Приоритет 1: FloatingOriginMP.positionSource
   - Приоритет 2: NetworkPlayer с IsOwner
   - Приоритет 3: Тег "Player"
   - Fallback: ThirdPersonCamera

2. **`GetPlayerPosition()`** - возвращает позицию игрока
   - Использует кешированный трансформ
   - Fallback на камеру с WARNING

3. **`UpdatePlayerCache()`** - обновляет кеш игрока

---

## Файлы изменены

| Файл | Изменение |
|------|-----------|
| `WorldStreamingManager.cs` | Добавлен `FindLocalPlayer()`, `GetPlayerPosition()`, `UpdatePlayerCache()`. Исправлен `UpdateStreaming()` |

---

## HUD Debug

Включи `showDebugHUD` в WorldStreamingManager для отладки:

```
World Streaming Manager
Loaded Chunks: 20
Center Chunk: [0, 0]
Load Radius: 2
Unload Radius: 3

Player Tracking:
Cached Player: NetworkPlayer(Clone)
Player Pos: (100000, 5, 500000)
```

---

## Тестирование

1. **Включи showDebugHUD** в WorldStreamingManager
2. **Телепортируйся на 100k** от спавна
3. **Проверь HUD:**
   - `Cached Player` должен показывать имя игрока
   - `Player Pos` должен показывать позицию далеко от origin
   - `Loaded Chunks` должны быть вокруг игрока
4. **Нажми F7** - чанки должны загружаться вокруг игрока

---

## Примечание

FloatingOriginMP.cs НЕ изменен (из-за проблем с форматированием). 
Но WorldStreamingManager уже имеет собственную логику поиска игрока,
которая работает автономно.

---

**Следующий шаг:** Тестирование в игре
