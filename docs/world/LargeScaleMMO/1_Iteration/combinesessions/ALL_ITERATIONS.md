# All Iterations — Seamless World 650K Radius

**Project:** ProjectC_client — MMO с бесшовным миром  
**Цель:** Радиус мира 650,000 единиц, игрок летает свободно, чанки вокруг персонажа  
**Дата:** 19.04.2026

---

## ИТЕРАЦИИ

### Iteration 1: Начало (14-15.04.2026)

**Задача:** Архитектурный план системы стриминга мира

**Результаты:**
- Создан `docs/world/LargeScaleMMO/01_Architecture_Plan.md`
- Выбрана кастомная система стриминга (Вариант B)
- Определены компоненты: WorldChunkManager, ChunkLoader, FloatingOriginMP
- Chunk size = 2000 units, радиус стриминга = 6000 units

**Проблемы:**
- Не реализовано

---

### Iteration 2: Foundation (15-16.04.2026)

**Задача:** Базовая инфраструктура стриминга

**Созданные файлы:**
- `WorldChunkManager.cs` — реестр чанков, grid-based lookup
- `ProceduralChunkGenerator.cs` — генерация гор + облаков для чанков
- `ChunkLoader.cs` — загрузка/выгрузка чанков
- `ChunkNetworkSpawner.cs` — спавн NetworkObjects в чанках
- `PlayerChunkTracker.cs` — server-side трекинг чанков игроков

**Исправления:**
- I2-001: Подписка на ChunkLoader events в WorldStreamingManager

**Проблемы:**
- FloatingOriginMP работает некорректно — мир сдвигается к игроку бесконечно

---

### Iteration 3: Multiplayer Integration (16-18.04.2026)

**Задача:** Синхронизация Floating Origin в мультиплеере

**Файлы:**
- `FloatingOriginMP.cs` — мультиплеер-синхронизированный сдвиг мира

**Исправления (из LOG.md):**

| ID | Описание | Статус |
|----|----------|--------|
| I3.1 | BroadcastWorldShiftRpc только ServerAuthority | ✅ |
| I3.2 | ApplyWorldShiftRpc для клиентов | ✅ |
| I3.3 | FindOwnerPlayer() для телепортации | ✅ |
| I3.4 | RPC Reorder protection | ✅ |
| I3.5 | Teleport cooldown | ✅ |
| I3.11 | Защита от спама сдвигов | ✅ |
| I3.18 | FloatingOriginMP.GetWorldPosition() | ✅ |
| I3.20 | Container exclude от TradeZones | ✅ |
| I3.22 | TeleportToPeak проверка расстояния | ✅ |
| I3.24 | UpdateStreaming использует FloatingOrigin | ✅ |
| I3.25 | ApplyWorldShift для ServerSynced | ✅ |

**Проблемы выявленные:**
1. Мир сдвигает к игроку при 100,000 — игрок не может отдалиться
2. Чанки грузятся вокруг стартовой локации вместо вокруг игрока

**Корневые причины (ITERATION3_DEBUG.md):**
- `ApplyWorldShift()` сдвигал мир, но НЕ телепортировал игрока
- Игрок оставался на 150000 → бесконечный цикл
- `GetWorldPosition()` возвращал неправильную позицию

---

### Iteration 4: CRITICAL FIXES (19.04.2026) ← ТЕКУЩАЯ

**Задача:** Исправить бесконечный цикл сдвигов и неправильную загрузку чанков

**Корневые причины:**
1. После сдвига мира игрок оставался на позиции 150000
2. `GetPlayerPosition()` возвращал 150000
3. `ShouldUseFloatingOrigin(150000)` → TRUE → бесконечный цикл
4. `WorldStreamingManager.LoadChunksAroundPlayer(150000)` → чанки вокруг origin

**ИСПРАВЛЕНИЯ:**

#### Fix 1: TeleportOwnerPlayerToOrigin() вызывается всегда

```csharp
// FloatingOriginMP.cs, ApplyWorldShift():
// CRITICAL: Teleport player back to origin after shift
// This prevents infinite shift loop!
if (mode == OriginMode.ServerSynced || mode == OriginMode.Local)
{
    Debug.Log("[FloatingOriginMP] Calling TeleportOwnerPlayerToOrigin() to prevent infinite shifts");
    TeleportOwnerPlayerToOrigin();
}
```

**Что делает:**
1. После сдвига мира — телепортирует игрока на TradeZones
2. Игрок теперь на (0, 5, 0) вместо 150000
3. `GetPlayerPosition()` возвращает (0, 5, 0)
4. Расстояние от origin = 5 < threshold (150000)
5. БЕСКОНЕЧНЫЙ ЦИКЛ ПРЕДОТВРАЩЁН!

#### Fix 2: Teleport Cooldown защита кэша

```csharp
private const float TELEPORT_COOLDOWN_DURATION = 1.5f;
private bool _teleportCooldownActive = false;

// После телепортации:
_teleportCooldownActive = true;
_teleportCooldownEndTime = Time.time + TELEPORT_COOLDOWN_DURATION;

// GetPlayerPosition() использует кэш вместо реальной позиции
// Это защищает от NetworkTransform lag (позиция ещё не синхронизирована)
```

#### Fix 3: UpdateCachedPlayerPosition() с защитой

```csharp
private void UpdateCachedPlayerPosition()
{
    // Don't update cache during teleport cooldown
    if (_teleportCooldownActive)
    {
        return;
    }
    // ... обновляем кэш
}
```

---

## АРХИТЕКТУРА ITERATION 4

### Принцип работы FloatingOriginMP

```
ИГРОК ЛЕТАЕТ → distance > 150,000 → ApplyWorldShift()
                                        ↓
                           ShiftAllContainers(160000)
                                        ↓
                           TeleportOwnerPlayerToOrigin()
                                        ↓
                           Игрок на TradeZones (0, 5, 0)
                                        ↓
                           Кэш обновлён
                                        ↓
                           GetWorldPosition() = (0, 5, 0)
                                        ↓
                           distance = 5 < 150,000
                                        ↓
                           НЕТ ПОВТОРНОГО СДВИГА ✓
```

### Схема взаимодействия

```
FloatingOriginMP                    WorldStreamingManager
      ↓                                      ↓
GetWorldPosition() ──────────────►  LoadChunksAroundPlayer()
(кэшированная позиция)            (позиция игрока после телепорта)
      ↑                                      ↓
      │                              ChunkLoader.LoadChunk()
TeleportOwnerPlayerToOrigin()      (загрузка чанков вокруг TradeZones)
(после сдвига мира)                       ↓
                                     ProceduralChunkGenerator
                                           ↓
                                     Mountains + Clouds
```

---

## ПЛАН ТЕСТИРОВАНИЯ

### Тест 1: Одиночный режим (Local)

```
1. Запустить игру в одиночном режиме
2. Включить showDebugLogs = true
3. Переместить игрока на 160,000+
4. Ожидаемый результат:
   - ОДИН сдвиг мира
   - Игрок телепортируется на TradeZones
   - GetWorldPosition() = (0, 5, 0)
   - Чанки загружаются вокруг TradeZones
   - HUD показывает "TELEPORT COOLDOWN"
   - После cooldown игрок может двигаться
```

### Тест 2: Мультиплеер (ServerSynced)

```
1. Запустить Host игру
2. Присоединиться клиентом
3. Переместить host на 160,000+
4. Ожидаемый результат:
   - Host: RequestWorldShiftRpc → ApplyWorldShift → BroadcastWorldShiftRpc
   - Client: Receive BroadcastWorldShiftRpc → ApplyWorldShiftRpc → TeleportOwnerPlayerToOrigin()
   - Оба игрока на TradeZones
   - Чанки загружаются вокруг обоих
```

### Тест 3: Радиус 650,000

```
1. Запустить игру
2. Перемещаться по миру в радиусе 650,000
3. Ожидаемый результат:
   - Каждые 150,000 единиц — сдвиг мира
   - После сдвига — игрок на TradeZones
   - Мир "возвращается" к игроку
   - Максимум ~65 сдвигов для полного радиуса
```

---

## ФАЙЛЫ ИЗМЕНЁННЫЕ В ITERATION 4

| Файл | Изменение |
|------|-----------|
| `FloatingOriginMP.cs` | Полностью переписан с правильной логикой |
| `WorldStreamingManager.cs` | UpdateStreaming использует FloatingOrigin |

---

## СЛЕДУЮЩИЕ ШАГИ

1. **Тестирование** — проверить все 3 теста
2. **Оптимизация** — Job System для генерации мешей
3. **Preloading** — загрузка соседних чанков заранее
4. **Memory budget** — контроль памяти

---

**Автор:** Claude Code  
**Дата:** 19.04.2026, 01:30 MSK