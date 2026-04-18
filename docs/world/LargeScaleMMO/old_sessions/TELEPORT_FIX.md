# Исправление телепорта и FloatingOrigin — ВАЖНО

**Дата:** 17 апреля 2026  
**Проект:** ProjectC_client  

---

## Критическая проблема: Игрок ВНУТРИ WorldRoot

**СИМПТОМЫ:**
- При перемещении персонажа все world objects (горы, облака) прыгают за ним
- Персонаж не может отойти от объектов мира
- HUD показывает Offset и Shifts но они продолжают расти даже когда стоишь

**ПРИЧИНА:**  
Игрок (NetworkPlayer) находится ВНУТРИ WorldRoot.

FloatingOriginMP сдвигает ВСЕ объекты внутри WorldRoot. Если игрок тоже там — он тоже сдвигается. Получается что игрок и горы движутся вместе, и персонаж "приклеен" к ним.

---

## Правильная иерархия сцены

### ✅ ПРАВИЛЬНО:

```
Scene
├── Main Camera (has FloatingOriginMP component)
├── NetworkManagerController
├── Player (has NetworkPlayer)          ← ВНЕ WorldRoot!
│   └── ThirdPersonCamera_{OwnerId}     ← ВНЕ WorldRoot!
├── WorldStreamingManager
├── StreamingTest_AutoRun (on Main Camera)
├── EventSystem
└── WorldRoot (position = 0,0,0)        ← ТОЛЬКО world objects здесь
    ├── Mountains
    │   ├── Massif_0
    │   │   └── Peak_0
    │   └── Massif_1
    ├── Clouds
    │   └── CloudLayer
    ├── Farms
    │   └── Farm_0
    └── TradeZones
        └── TradeZone_0
```

### ❌ НЕПРАВИЛЬНО (текущая проблема):

```
Scene
├── Main Camera
├── Player (has NetworkPlayer)          ← ❌ ВНУТРИ WorldRoot!
│   └── ThirdPersonCamera
└── WorldRoot
    ├── Mountains                        ← Сдвигается
    ├── Clouds                           ← Сдвигается
    └── Player                           ← ТОЖЕ сдвигается! ❌
```

---

## Как исправить

### Шаг 1: Проверить иерархию

1. Откройте сцену в Unity
2. Посмотрите Hierarchy
3. Player должен быть **на верхнем уровне** (не дочерним элементом WorldRoot)

### Шаг 2: Переместить Player в正确的 место

1. В Hierarchy найдите Player (или NetworkPlayer)
2. Если он внутри WorldRoot — **перетащите его наружу** (drag outside WorldRoot)
3. WorldRoot должен содержать ТОЛЬКО объекты мира:
   - Mountains
   - Clouds  
   - Farms
   - TradeZones
   - ChunksContainer

### Шаг 3: Проверить FloatingOriginMP

1. FloatingOriginMP должен быть на **Main Camera**
2. В Inspector проверьте `worldRootNames`:
   - Должны быть: "Mountains", "Clouds", "WorldRoot", и т.д.
3. `showDebugLogs: true` для отладки

---

## Почему HUD показывает изменения но не сдвигает мир

Если HUD показывает изменения Offset/ShiftCount, но мир не сдвигается — возможные причины:

1. **FloatingOriginMP не инициализирован** — проверьте что `_worldRoots.Count > 0`
2. **Нет объектов с нужными именами** — добавьте пустой WorldRoot в сцену
3. **Threshold слишком большой** — по умолчанию 100,000 units
4. **Player ВНУТРИ WorldRoot** — игрок сдвигается вместе с миром

**Проверка:** Включите `showDebugLogs = true` в FloatingOriginMP и посмотрите логи:
```
[FloatingOriginMP] Initialized. threshold=100,000, shiftRounding=10,000, roots found: 5
[FloatingOriginMP]   - World root: 'Mountains'
[FloatingOriginMP]   - World root: 'Clouds'
...
```

---

## КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ: excludeFromShift

FloatingOriginMP теперь исключает из сдвига объекты по именам из массива `excludeFromShift`.
Это предотвращает сдвиг игрока вместе с миром.

**Текущий список excludeFromShift:**
- Player
- NetworkPlayer
- ThirdPersonCamera
- Main Camera
- Camera
- EventSystem
- NetworkManager
- StreamingTest
- NetworkManagerController

**Если игрок всё ещё прыгает с миром:**
1. Проверьте что `NetworkPlayer` (или `Player`) находится НЕ под WorldRoot
2. Проверьте что имя объекта игрока совпадает с одним из `excludeFromShift`
3. Откройте Hierarchy и убедитесь: Player должен быть на верхнем уровне сцены

**Отладка:** Добавьте в `ApplyShiftToAllRoots()` временный лог:
```csharp
Debug.Log($"[FloatingOriginMP] Shifting {_worldRoots.Count} roots by {offset}");
```

---

## Телепорт теперь работает так:

1. **F5/F6 нажата** → вызывается `TeleportToTestPosition()`
2. **ResetOrigin()** → сначала сбрасываем FloatingOrigin (сдвигаем мир чтобы камера была близко к 0,0,0)
3. **Телепортируем Player** → перемещаем игрока на новую позицию
4. **Телепортируем Camera** →同步 позицию камеры
5. **OnTeleportComplete()** → вызываем streamingManager для загрузки чанков

---

## Ключевые изменения в StreamingTest_AutoRun

```csharp
private void TeleportToTestPosition(int index)
{
    // 1. Сначала сбрасываем FloatingOrigin
    var fo = FindAnyObjectByType<FloatingOriginMP>();
    if (fo != null)
    {
        fo.ResetOrigin();  // Сдвигаем мир чтобы камера была у origin
    }
    
    // 2. Телепортируем игрока
    var networkObjects = FindObjectsByType<NetworkObject>();
    foreach (var netObj in networkObjects)
    {
        if (netObj.IsOwner)
        {
            netObj.transform.position = _targetPosition;
        }
    }
    
    // 3. Телепортируем камеру
    _mainCamera.transform.position = _targetPosition;
}
```

---

## ГДЕ РАЗМЕСТИТЬ FloatingOriginMP

**НИКОГДА не размещай FloatingOriginMP на префабе камеры!** Это создаёт дубликаты при спавне.

### ПРАВИЛЬНОЕ РАЗМЕЩЕНИЕ:

**Вариант 1: Отдельный контроллер в сцене (РЕКОМЕНДУЕТСЯ)**

```
Hierarchy:
├── NetworkManagerController      ← Добавь FloatingOriginMP СЮДА
├── Player (NetworkPlayer)
├── Main Camera                   ← НЕ добавляй сюда
├── WorldRoot
│   ├── Mountains
│   ├── Clouds
│   └── TradeZones
└── EventSystem
```

1. Выбери NetworkManagerController в Hierarchy
2. Add Component → FloatingOriginMP
3. В Inspector:
   - `showDebugLogs: true`
   - `showDebugHUD: true`
   - `threshold: 1000000` (для больших миров)
   - `shiftRounding: 100000`

**Вариант 2: Отдельный пустой объект**

```
Hierarchy:
├── FloatingOriginController       ← Добавь FloatingOriginMP СЮДА
├── NetworkManagerController
├── Player (NetworkPlayer)
└── WorldRoot
    └── ...
```

1. GameObject → Create Empty → назови "FloatingOriginController"
2. Add Component → FloatingOriginMP
3. Настрой как выше

### ПОЧЕМУ НЕ НА ПРЕФАБЕ КАМЕРЫ:

- Каждая камера спавнится при подключении игрока
- FloatingOriginMP дублируется на каждой камере
- Получается 2+ HUD, конфликты, спам логов
- Один компонент не знает про другие

---

## Следующие шаги

1. [x] Player ВНЕ WorldRoot — проверено
2. [x] Shift counts НЕ растёт когда стоишь
3. [ ] Протестировать телепорт к большим координатам (F5 → 150000, 250000)
4. [ ] Проверить HUD в основной сцене
5. [ ] Проверить что артефакты исчезают после сдвига

---

## Тестирование больших координат (17.04.2026)

**Ожидаемое поведение:**
1. F5 нажимается → игрок телепортируется к точке 150,000 / 150,000
2. Camera position = 150000, 500, 150000
3. FloatingOriginMP检测到 |camera.x| > 100000
4. Сдвигает мир на -150000 по X и Z
5. Camera position теперь = 0, 500, 0
6. TotalOffset = (150000, 0, 150000)
7. Артефакты пропадают

**Если HUD не показывает:**
- Проверьте `showDebugHUD = true` в FloatingOriginMP Inspector
- Проверьте что FloatingOriginMP компонент есть на Main Camera
- Посмотрите лог `[FloatingOriginMP] Initialized. roots found: X`

---

**Автор:** Claude Code  
**Дата:** 17.04.2026
