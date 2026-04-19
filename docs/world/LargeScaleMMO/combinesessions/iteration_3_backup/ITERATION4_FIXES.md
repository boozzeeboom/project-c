# Iteration 4 — Applied Fixes

**Дата:** 19.04.2026, 01:15 MSK  
**Статус:** ✅ ИСПРАВЛЕНИЯ ПРИМЕНЕНЫ  
**Фокус:** 
1. Мир сдвигает к игроку при 100,000 — игрок не может отдалиться
2. Чанки грузятся вокруг стартовой локации вместо вокруг игрока

---

## 🔴 КОРНЕВЫЕ ПРИЧИНЫ (подтверждены)

### Проблема 1: Мир сдвигает к игроку

**Корневая причина:** В ServerSynced режиме после BroadcastWorldShiftRpc() на клиенте вызывался ApplyWorldShift(), который:
1. Сдвигал мир через ApplyShiftToAllRoots()
2. НЕ телепортировал игрока!
3. Игрок на клиенте оставался на 150000
4. GetWorldPosition() возвращал 150000
5. ShouldUseFloatingOrigin(150000) → TRUE → бесконечный цикл сдвигов

### Проблема 2: Чанки грузятся вокруг стартовой

**Корневая причина:** Вытекает из Проблемы 1:
1. Кэш игрока содержал неправильную позицию (150000)
2. GetWorldPosition() возвращал 150000
3. WorldStreamingManager.LoadChunksAroundPlayer(150000)
4. GetChunkAtPosition(150000) вычислял чанки вокруг origin вместо вокруг игрока

---

## ✅ ПРИМЕНЁННЫЕ ИСПРАВЛЕНИЯ

### Fix 1: TeleportOwnerPlayerToOrigin() в ApplyWorldShift()

**Файл:** `FloatingOriginMP.cs`  
**Метод:** `ApplyWorldShift()` (строки ~1120-1155)  
**Изменение:** Добавлен вызов `TeleportOwnerPlayerToOrigin()` для ServerSynced режима

```csharp
// ITERATION 4 FIX: Телепортируем игрока на клиенте!
if (mode == OriginMode.ServerSynced)
{
    Debug.Log("[FloatingOriginMP] ApplyWorldShift: ServerSynced mode - teleporting player to origin");
    TeleportOwnerPlayerToOrigin();
}
```

**Результат:** После сдвига мира на клиенте игрок телепортируется рядом с TradeZones, как на сервере.

---

### Fix 2: ShouldUseFloatingOrigin() проверка _totalOffset

**Файл:** `FloatingOriginMP.cs`  
**Метод:** `ShouldUseFloatingOrigin()` (строки ~326-370)  
**Изменение:** Добавлена проверка _totalOffset для ServerSynced режима

```csharp
// ITERATION 4 FIX: В ServerSynced режиме проверяем _totalOffset!
if (mode == OriginMode.ServerSynced)
{
    if (_totalOffset.magnitude > 100f)
    {
        if (Time.time - _lastShiftTime < SHIFT_COOLDOWN)
        {
            if (showDebugLogs && Time.frameCount % 120 == 0)
                Debug.Log($"[FloatingOriginMP] ShouldUseFloatingOrigin: ServerSynced - cooldown active, totalOffset={_totalOffset.magnitude:F0}, ignoring");
            return false;
        }
    }
}
```

**Результат:** После сдвига мира cooldown защищает от повторного запроса сдвига.

---

## 📋 ЧТО ДЕЛАЮТ ИСПРАВЛЕНИЯ

### До Fix 1:

```
Клиент получает BroadcastWorldShiftRpc:
    → ApplyWorldShift() сдвигает мир
    → Игрок ОСТАЁТСЯ на 150000 ← ПРОБЛЕМА
    → GetWorldPosition() = 150000
    → ShouldUseFloatingOrigin(150000) = TRUE
    → RequestWorldShiftRpc(150000) → новый сдвиг
    → БЕСКОНЕЧНЫЙ ЦИКЛ
```

### После Fix 1:

```
Клиент получает BroadcastWorldShiftRpc:
    → ApplyWorldShift() сдвигает мир
    → TeleportOwnerPlayerToOrigin() ← ДОБАВЛЕНО
    → Игрок на (0, 5, 0) ← ПРАВИЛЬНО
    → GetWorldPosition() = (0, 5, 0)
    → ShouldUseFloatingOrigin((0,5,0)) = FALSE
    → НЕ запрашиваем сдвиг ← ПРАВИЛЬНО
```

---

## 🔍 ДОПОЛНИТЕЛЬНАЯ ЗАЩИТА

### Teleport Cooldown (уже был)

```csharp
// TeleportOwnerPlayerToOrigin():
_teleportCooldownActive = true;
_teleportCooldownEndTime = Time.time + TELEPORT_COOLDOWN_DURATION; // 1.5 секунды
```

Это защищает кэш от перезаписи в UpdateCachedPlayerPosition() пока NetworkTransform синхронизирует позицию.

---

## 📝 ЛОГИ ДЛЯ ОТЛАДКИ

При включенном `showDebugLogs = true`:

**Ожидаемые логи при сдвиге:**
```
[FloatingOriginMP] RequestWorldShiftRpc: SERVER processing - cameraPos=150000, offset=150000
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: ВЫЗВАН!
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: cooldown ACTIVATED until X.XX
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: НАЙДЕН по тегу Player: NetworkPlayer(Clone), позиция ДО=150000
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: позиция УСТАНОВЛЕНА в (0, 5, 0)
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: кэш обновлён
[FloatingOriginMP] RequestWorldShiftRpc: BroadcastWorldShiftRpc sent to all clients
[FloatingOriginMP] Received world shift from server: offset=150000
[FloatingOriginMP] ApplyWorldShift: ServerSynced mode - teleporting player to origin  ← NEW!
[FloatingOriginMP] TeleportOwnerPlayerToOrigin: ВЫЗВАН!  ← NEW!
```

---

## ✅ ЧЕКЛИСТ ТЕСТИРОВАНИЯ

- [ ] Запустить игру в режиме ServerSynced (multiplayer)
- [ ] Переместить игрока на 100,000+
- [ ] Проверить что сдвиг происходит ОДИН раз
- [ ] Проверить что игрок остаётся рядом с TradeZones после сдвига
- [ ] Проверить что чанки загружаются вокруг игрока, а не вокруг origin
- [ ] Проверить что игрок может двигаться после сдвига
- [ ] Проверить что мир НЕ "убегает" от игрока

---

**Автор:** Claude Code  
**Дата:** 19.04.2026, 01:15 MSK  
**Следующий шаг:** Тестирование исправлений в Unity
