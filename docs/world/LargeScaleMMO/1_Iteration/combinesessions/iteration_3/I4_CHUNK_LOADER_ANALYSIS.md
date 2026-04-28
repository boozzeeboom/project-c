# 🎯 I4: Chunk Loader Offset Problem Analysis

## Контекст

**Проблема:** ChunkLoader грузит чанки вокруг спавн-зоны, а не вокруг игрока.

### Симптомы
1. Чанки загружаются только вокруг спавн-зоны
2. HUD показывает "loaded chunk 20" около origin
3. При отдалении от спавна на 100k - чанки выгружаются, loaded=0
4. Нажатие F7 снова загружает чанки на спавне пока не отойдет еще

---

## Архитектурный Анализ

### Текущая цепочка (СЛОМАНАЯ)

```
WorldStreamingManager.UpdateStreaming()
  └─ Camera.main.transform.position  ← ВСЕГДА близко к origin!
     └─ LoadChunksAroundPlayer(position)
        └─ GetChunksInRadius(position, radius)  ← позиция origin!
           └─ Чанки грузятся вокруг origin!
```

### Почему Camera.main не работает

**FloatingOriginMP сдвигает мир, чтобы камера оставалась близко к origin:**

```
До сдвига:
  Player: (100000, 0, 500000)
  WorldRoot: (0,0,0)
  Camera: (100000, 100, 500500)

После сдвига (сдвигаем мир на -100000, 0, -500000):
  Player: (0, 0, 0) [относительно WorldRoot]
  WorldRoot: (-100000, 0, -500000)
  Camera: (0, 100, 0)  ← ВСЕГДА около origin!
```

### Кто виноват

| Компонент | Проблема |
|----------|----------|
| `WorldStreamingManager.UpdateStreaming()` | Использует `Camera.main` вместо позиции игрока |
| `FloatingOriginMP.GetWorldPosition()` | Правильно вычисляет позицию, но **ничего не делает** с ней в WorldStreamingManager |

### Где должна быть позиция игрока

**Вариант 1:** `GameObject.FindGameObjectWithTag("Player")` - настоящий игрок
**Вариант 2:** `NetworkPlayer` компонент - правильная позиция в сети
**Вариант 3:** Через `FloatingOriginMP.positionSource` если назначен

---

## Референс: Как это работает в FloatingOriginMP

```csharp
// FloatingOriginMP.GetWorldPosition() ПРАВИЛЬНО находит игрока:
1. positionSource (если назначен)
2. NetworkPlayer с IsOwner (приоритет)
3. GameObject с тегом "Player"
4. ThirdPersonCamera (fallback)
5. Camera.main (крайний fallback)

// После сдвига мира позиция корректируется:
// truePos = pos - _totalOffset
```

---

## Референс: Как это работает в StreamingTest

```csharp
// StreamingTest использует правильный подход:
private void UpdateTrackedTransform()
{
    // Приоритет: локальный игрок > камера
    if (useLocalPlayerPosition && _localPlayer != null)
    {
        _trackedTransform = _localPlayer.transform;
    }
    else if (_mainCamera != null)
    {
        _trackedTransform = _mainCamera.transform;
    }
}

private Vector3 GetCurrentPosition()
{
    if (_trackedTransform != null)
    {
        return _trackedTransform.position;
    }
    return Vector3.zero;
}
```

**Но в Update() всё равно используется камера:**
```csharp
// Этот метод вызывается из UpdateStreaming()
streamingManager.LoadChunksAroundPlayer(GetCurrentPosition());
// GetCurrentPosition() возвращает camera.position!
```

---

## Решение

### Шаг 1: Создать общий метод в FloatingOriginMP

```csharp
/// <summary>
/// Получить позицию локального игрока в мировых координатах.
/// Учитывает все сценарии: одиночная игра, хостинг, клиент.
/// </summary>
public Vector3 GetLocalPlayerWorldPosition()
{
    // 1. Явный источник
    if (positionSource != null)
    {
        return positionSource.position;
    }
    
    // 2. NetworkPlayer (IsOwner)
    var networkPlayers = FindObjectsByType<NetworkObject>();
    foreach (var netObj in networkPlayers)
    {
        if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
        {
            return netObj.transform.position;
        }
    }
    
    // 3. Объект с тегом "Player"
    GameObject playerByTag = GameObject.FindGameObjectWithTag("Player");
    if (playerByTag != null)
    {
        return playerByTag.transform.position;
    }
    
    // 4. Fallback
    return Vector3.zero;
}
```

### Шаг 2: Исправить WorldStreamingManager

```csharp
private void UpdateStreaming()
{
    // Ищем позицию игрока, НЕ камеры
    Vector3 playerPosition = GetPlayerPosition();
    
    if (playerPosition != Vector3.zero)
    {
        LoadChunksAroundPlayer(playerPosition);
    }
}

private Vector3 GetPlayerPosition()
{
    // Приоритет 1: FloatingOriginMP знает позицию игрока
    if (floatingOrigin != null)
    {
        return floatingOrigin.GetLocalPlayerWorldPosition();
    }
    
    // Приоритет 2: NetworkPlayer IsOwner
    var networkObjects = FindObjectsByType<NetworkObject>();
    foreach (var netObj in networkObjects)
    {
        if (netObj.IsOwner && netObj.GetComponent<Player.NetworkPlayer>() != null)
        {
            return netObj.transform.position;
        }
    }
    
    // Приоритет 3: Тег "Player"
    GameObject player = GameObject.FindGameObjectWithTag("Player");
    if (player != null)
    {
        return player.transform.position;
    }
    
    // Fallback: камера (для отладки)
    Camera mainCamera = Camera.main;
    if (mainCamera != null)
    {
        return mainCamera.transform.position;
    }
    
    return Vector3.zero;
}
```

### Шаг 3: Убедиться что F7 работает правильно

F7 в StreamingTest уже вызывает `LoadChunksAroundPlayer()`, но:
1. `GetCurrentPosition()` использует `_trackedTransform` который может быть камерой
2. Нужно убедиться что `_trackedTransform` = игрок

---

## Файлы для изменения

1. **FloatingOriginMP.cs** - добавить `GetLocalPlayerWorldPosition()`
2. **WorldStreamingManager.cs** - исправить `UpdateStreaming()` использовать позицию игрока
3. **StreamingTest.cs** - исправить `GetCurrentPosition()` для F7

---

## Контрольный список тестирования

- [ ] После телепортации на 100k - чанки грузятся вокруг игрока
- [ ] HUD показывает loaded chunks рядом с игроком
- [ ] F7 загружает чанки вокруг игрока, не origin
- [ ] Чанки НЕ пропадают когда игрок уходит (они выгружаются около игрока)

---

**Создано:** 19.04.2026
**Версия:** 1.0
