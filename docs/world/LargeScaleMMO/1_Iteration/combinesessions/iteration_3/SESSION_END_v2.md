# Iteration 3 — Session End Report v2

**Date:** 2026-04-18  
**Session:** Testing Chunk Oscillation fix  
**Status:** ❌ **КОРНЕВАЯ ОШИБКА НЕ ИСПРАВЛЕНА**

---

## Executive Summary

v2 fix (GetWorldPosition()) **НЕ решил проблему oscillation**. Анализ логов выявил **более глубокую корневую ошибку**: неправильный выбор NetworkObject в `FloatingOriginMP.GetWorldPosition()`.

---

## Анализ Логов

### Наблюдение 1: GetWorldPosition() стабилен

```
[FloatingOriginMP] GetWorldPosition: close to origin (30975), using raw position
[FloatingOriginMP] Debug: mode=ServerAuthority, cameraWorldPos=(30375, 503, -6050), dist=30975
```

- `GetWorldPosition()` стабильно возвращает `(30375, 503, -6050)` — это ПРАВИЛЬНАЯ позиция игрока
- FloatingOriginMP LateUpdate работает корректно

### Наблюдение 2: transform.position OSCILLATES

```
[NetworkPlayer] UpdatePlayerChunkTracker: transform.pos=(30375, 503, -6050), GetWorldPos=(30375, 503, -6050)  ✓ MATCH
[NetworkPlayer] UpdatePlayerChunkTracker: transform.pos=(12925, 60, 28199), GetWorldPos=(30375, 503, -6050)   ✗ OSCILLATION!
```

**КОРНЕВАЯ ОШИБКА:**
- `transform.position` oscills между двумя совершенно разными позициями:
  - Правильная: `(30375, 503, -6050)` 
  - Неправильная: `(12925, 60, 28199)` — это какой-то ДРУГОЙ объект!
- `GetWorldPosition()` стабилен и всегда возвращает правильную позицию

### Наблюдение 3: GetWorldPosition() возвращает позицию из НЕПРАВИЛЬНОГО источника

Несмотря на то что `GetWorldPosition()` возвращает правильную позицию, лог показывает что **позиция берётся НЕ из positionSource**:

```
[FloatingOriginMP] GetWorldPosition: close to origin (30975), using raw position
```

Это означает что `positionSource.position` НЕ используется. Метод находит позицию другим способом (через NetworkPlayer поиск), и **там выбирается НЕПРАВИЛЬНЫЙ NetworkObject**.

---

## Корневая Ошибка

### Код в FloatingOriginMP.GetWorldPosition():

```csharp
// 2. NetworkPlayer — ПРИОРИТЕТ!
var networkPlayers = FindObjectsByType<Unity.Netcode.NetworkObject>();
foreach (var netObj in networkPlayers)
{
    // Ищем NetworkPlayer с IsOwner=true И позицией далеко от origin (>10000)
    if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
    {
        Vector3 pos = netObj.transform.position;
        // ...
        if (pos.magnitude > 10000)
        {
            return pos;  // ВОЗВРАЩАЕТ transform.position, НЕ ИСПОЛЬЗУЕТ GetWorldPosition!
        }
    }
}
```

**ПРОБЛЕМА:** Этот код ищет NetworkObject по `IsOwner`, но:
1. `IsOwner` проверяется в КОНТЕКСТЕ вызывающего — на разных клиентах разные объекты будут `IsOwner=true`
2. На ХОСТЕ: `IsOwner=true` может быть у ДВУХ разных NetworkObject (ghost и настоящий)
3. Выбирается НЕПРАВИЛЬНЫЙ NetworkObject

**ПОСЛЕДСТВИЕ:**
- `GetWorldPosition()` иногда возвращает позицию ghost/клона вместо настоящего игрока
- Эта позиция используется в FloatingOriginMP LateUpdate
- PlayerChunkTracker получает НЕПРАВИЛЬНУЮ позицию через transform.position

---

## Поведение системы

### FloatingOriginMP.GetWorldPosition():
1. Пытается использовать `positionSource.position` если близко к origin
2. НЕ использует positionSource → ищет NetworkPlayer с IsOwner
3. Выбирает НЕПРАВИЛЬНЫЙ NetworkObject (oscillation между ghost и real)
4. Возвращает `pos` = transform.position выбранного объекта
5. **GetWorldPosition() стабилен когда выбирает настоящего игрока**

### NetworkPlayer.UpdatePlayerChunkTracker():
1. Вызывает `GetWorldPosition()` → получает правильную позицию
2. Но использует `transform.position` для обновления PlayerChunkTracker
3. **transform.position oscills потому что это ЛОКАЛЬНЫЙ transform игрока**
4. **GetWorldPosition() стабилен, но NetworkPlayer использует НЕПРАВИЛЬНЫЙ источник!**

### PlayerChunkTracker:
1. Получает oscilling позицию из NetworkPlayer
2. Вычисляет неправильный chunk
3. Загружает/выгружает чанки неправильно

---

## Двойной Источник Осцилляции

**Осцилляция ВОЗНИКАЕТ на ДВУХ уровнях:**

1. **FloatingOriginMP.GetWorldPosition():** Иногда выбирает НЕПРАВИЛЬНЫЙ NetworkObject
   - Но возврат стабилен когда выбран правильный
   
2. **NetworkPlayer.UpdatePlayerChunkTracker():** Использует `transform.position` вместо `GetWorldPosition()`
   - transform.position НЕ equals GetWorldPosition()!
   - Это разные объекты!

**ВЫВОД:** v2 fix неполный. Нужно:
1. Либо заставить NetworkPlayer использовать `GetWorldPosition()` напрямую
2. Либо исправить выбор NetworkObject в FloatingOriginMP

---

## Архитектурная Проблема

```
FloatingOriginMP
    |
    +-- GetWorldPosition()
    |       |
    |       +-- positionSource (НЕ используется!)
    |       +-- NetworkPlayer поиск (ВЫБИРАЕТ НЕПРАВИЛЬНЫЙ!)
    |
    +-- LateUpdate() --> использует GetWorldPosition() --> правильно
    |
    +-- PlayerChunkTracker
            |
            +-- NetworkPlayer.transform.position --> OSCILLATES
            +-- GetWorldPosition() --> стабилен, но не используется!
```

---

## Что Работает Правильно

1. ✅ `FloatingOriginMP.GetWorldPosition()` — стабилен когда выбран правильный объект
2. ✅ `FloatingOriginMP.LateUpdate()` — работает корректно, threshold проверяется правильно
3. ✅ `FloatingOriginMP` не вызывает сдвиг мира потому что позиция < threshold

## Что Не Работает

1. ❌ **FloatingOriginMP.positionSource НЕ используется** — это явный источник позиции но код его игнорирует
2. ❌ **NetworkPlayer.UpdatePlayerChunkTracker() использует `transform.position`** — это ЛОКАЛЬНАЯ позиция которая oscills
3. ❌ **GetWorldPosition() использует IsOwner проверку** — на хосте это нестабильно
4. ❌ **PlayerChunkTracker получает неправильную позицию** — вызывает ненужную загрузку/выгрузку чанков

---

## Recommended Fix (Iteration 3.1)

### Option A: NetworkPlayer должен использовать GetWorldPosition()

```csharp
// NetworkPlayer.UpdatePlayerChunkTracker()
private void UpdatePlayerChunkTracker()
{
    // ...
    Vector3 worldPosition;
    
    var floatingOrigin = FloatingOriginMP.Instance;
    if (floatingOrigin != null)
    {
        worldPosition = floatingOrigin.GetWorldPosition();
    }
    else
    {
        worldPosition = transform.position;  // Fallback
    }
    
    _playerChunkTracker.ForceUpdatePlayerChunk(OwnerClientId, worldPosition);
}
```

**НО!** Это не решит проблему если GetWorldPosition() выбирает неправильный NetworkObject.

### Option B: Исправить выбор NetworkObject в FloatingOriginMP

Нужно добавить ДОПОЛНИТЕЛЬНУЮ проверку:
- Проверять тег "Player"
- Проверять компонент NetworkPlayer
- Не доверять IsOwner на хосте

### Option C: Явно назначить positionSource

Убедиться что в инспекторе назначен правильный Transform как positionSource:
- Назначить ThirdPersonCamera.target (NetworkPlayer.transform)
- ИЛИ назначить сам NetworkPlayer.transform
- Убрать fallback на NetworkPlayer поиск

---

## Замечания по Chunk Loading

Логи показывают много "Unloading chunk by server command" для чанков которые не загружены:

```
[ChunkLoader] Чанк Chunk(-2, 1) не загружен, невозможно выгрузить.
[ChunkLoader] Чанк Chunk(-2, 0) не загружен, невозможно выгрузить.
[ChunkLoader] Чанк Chunk(-1, 1) не загружен, невозможно выгрузить.
```

**Это НЕ КРИТИЧНО**, но показывает что:
1. PlayerChunkTracker отправляет команды выгрузки для чанков которые не нужны
2. Это результат oscillaции позиции — игрок "прыгает" между чанками
3. После исправления oscillaции эти warning'и исчезнут

---

## Итог

| Компонент | Статус | Комментарий |
|-----------|--------|------------|
| FloatingOriginMP.GetWorldPosition() | ⚠️ Работает частично | Возвращает стабильную позицию, но выбирает неправильный источник |
| FloatingOriginMP.LateUpdate() | ✅ Работает | Threshold проверяется правильно |
| NetworkPlayer.UpdatePlayerChunkTracker() | ❌ Не работает | Использует transform.position вместо GetWorldPosition() |
| PlayerChunkTracker | ⚠️ Работает частично | Получает oscillating позицию |
| Chunk Loading/Unloading | ⚠️ Избыточные операции | Результат oscillaции |

---

## Next Steps (Iteration 3.1)

1. **Исправить NetworkPlayer.UpdatePlayerChunkTracker()** — использовать GetWorldPosition()
2. **Протестировать** — oscillation должна прекратиться
3. **Если не помогло** — исправить выбор NetworkObject в FloatingOriginMP.GetWorldPosition()

---

## Files Modified This Session

1. `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` — GetWorldPosition() сделан public
2. `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — добавлен вызов GetWorldPosition() в UpdatePlayerChunkTracker()
3. `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs` — hysteresis для oscillation на границах

## Files Needing Changes

1. `NetworkPlayer.UpdatePlayerChunkTracker()` — возможно нужно переписать логику использования позиции
2. `FloatingOriginMP.GetWorldPosition()` — возможно нужно добавить дополнительные проверки выбора NetworkObject
