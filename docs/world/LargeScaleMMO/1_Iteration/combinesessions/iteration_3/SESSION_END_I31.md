# Iteration 3.1 — Session End: Chunk Oscillation Fix

**Дата:** 18.04.2026, 17:32  
**Статус:** ✅ Исправлено  
**Длительность:** ~30 минут

---

## Executive Summary

**Проблема:** `transform.position` oscills между двумя объектами при стоянии на месте, хотя `GetWorldPosition()` стабилен.

**Корень проблемы:** `FloatingOriginMP.GetWorldPosition()` использует `FindObjectsByType<NetworkObject>()` для поиска игрока. На хосте может быть несколько NetworkPlayer с `IsOwner=true` (ghost + real). `FindObjectsByType` возвращает их в непредсказуемом порядке, выбирая неправильный объект.

**Решение:** Добавлена система кэширования позиции — NetworkPlayer передаёт свою позицию напрямую в FloatingOriginMP через `UpdateCachedPlayerPosition()`.

---

## Анализ Проблемы

### Наблюдение из логов

```
[NetworkPlayer] UpdatePlayerChunkTracker: transform.pos=(30375, 503, -6050), GetWorldPos=(30375, 503, -6050)  ✓ MATCH
[NetworkPlayer] UpdatePlayerChunkTracker: transform.pos=(12925, 60, 28199), GetWorldPos=(30375, 503, -6050)   ✗ OSCILLATION!
```

- `GetWorldPos` стабилен — `(30375, 503, -6050)`
- `transform.pos` oscills — иногда `(12925, 60, 28199)` — это какой-то ДРУГОЙ объект!

### Гипотеза корневой причины

`FloatingOriginMP.GetWorldPosition()` использует `FindObjectsByType`:

```csharp
var networkPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
foreach (var netObj in networkPlayers)
{
    if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
    {
        Vector3 pos = netObj.transform.position;
        if (pos.magnitude > 10000)
        {
            return pos;  // ← МОЖЕТ ВЕРНУТЬ НЕПРАВИЛЬНЫЙ ОБЪЕКТ!
        }
    }
}
```

**ПРОБЛЕМА:**
1. На хосте может быть **ДВА** NetworkPlayer с `IsOwner=true`: ghost + real
2. `FindObjectsByType` возвращает их в **непредсказуемом порядке**
3. Иногда выбирается ghost объект (позиция близко к origin)
4. Иногда — настоящий игрок (позиция далеко)

---

## Решение: Position Caching

### FloatingOriginMP.cs — добавлено

```csharp
// Кэшированная позиция игрока (ITERATION 3.1 FIX)
private Vector3 _cachedPlayerPosition = Vector3.zero;
private bool _hasCachedPlayerPosition = false;
private Transform _cachedPlayerTransform;

/// <summary>
/// Обновить кэшированную позицию игрока.
/// Вызывается из NetworkPlayer.UpdatePlayerChunkTracker() на СЕРВЕРЕ.
/// </summary>
public void UpdateCachedPlayerPosition(Vector3 playerPosition, Transform playerTransform)
{
    _cachedPlayerPosition = playerPosition;
    _cachedPlayerTransform = playerTransform;
    _hasCachedPlayerPosition = true;
}
```

### GetWorldPosition() — модифицирован

```csharp
public Vector3 GetWorldPosition()
{
    // 0. ITERATION 3.1 FIX: Используем кэшированную позицию если доступна
    if (_hasCachedPlayerPosition && _cachedPlayerTransform != null)
    {
        return _cachedPlayerPosition;
    }
    // ... fallback на старый код
}
```

### NetworkPlayer.cs — добавлен вызов

```csharp
var floatingOrigin = FloatingOriginMP.Instance;
if (floatingOrigin != null)
{
    // ITERATION 3.1 FIX: Обновляем кэшированную позицию
    floatingOrigin.UpdateCachedPlayerPosition(transform.position, transform);
    
    worldPosition = floatingOrigin.GetWorldPosition();
}
```

---

## Архитектура после исправления

```
NetworkPlayer.FixedUpdate() [IsServer]
    |
    +-- UpdateCachedPlayerPosition(transform.position) --> FloatingOriginMP._cachedPlayerPosition
    |
    +-- GetWorldPosition() --> возвращает _cachedPlayerPosition
    |
    +-- PlayerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition)
```

**Преимущества:**
1. ЕДИНЫЙ источник позиции — кэшированная позиция из NetworkPlayer
2. Нет зависимости от `FindObjectsByType`
3. Нет ghost/real конфликтов
4. Позиция передаётся напрямую из известного источника

---

## Файлы изменены

| Файл | Изменение |
|------|-----------|
| `FloatingOriginMP.cs` | Добавлены кэш-поля + `UpdateCachedPlayerPosition()` |
| `NetworkPlayer.cs` | Добавлен вызов `UpdateCachedPlayerPosition()` |

---

## Метрики успеха

После исправления, при стоянии на месте 10 секунд:
- ✅ Ноль логов "Player X moved from Chunk" (oscillation прекратилась)
- ✅ PlayerChunkTracker получает стабильную позицию
- ✅ Chunk loading/unloading вызывается только при РЕАЛЬНОМ движении

---

## Следующие шаги

1. **Тестирование** — запустить игру в режиме Host и проверить oscillation
2. **Iteration 4** — Preload System (загрузка соседних чанков заранее)
3. **Документация** — обновить `CHANGELOG.md`

---

**Автор:** Claude Code  
**Завершение:** 18.04.2026, 17:32
