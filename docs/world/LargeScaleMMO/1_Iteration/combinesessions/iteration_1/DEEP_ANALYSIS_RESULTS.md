# Deep Analysis Results: FloatingOriginMP Jitter Problem

**Дата:** 18.04.2026, 16:28  
**Статус:** 🔴 КРИТИЧЕСКИЙ ВЫВОД

---

## 🎯 Ключевые находки

### 1. Архитектурный конфликт (КРИТИЧНО)

```
FloatingOriginMP сдвигает мир
        ↓
WorldRoot.position меняется на (-250000, 0, -250000)
        ↓
NetworkTransform компоненты (на игроке, кораблях, NPC)
получают НОВУЮ позицию объекта относительно WorldRoot
        ↓
Но сервер отправляет ПОЗИЦИЮ В АБСОЛЮТНЫХ КООРДИНАТАХ
        ↓
NGO интерполяция работает с НЕВЕРНЫМИ данными
        ↓
JITTER / DESYNC
```

**Проблема:** FloatingOriginMP и NGO NetworkTransform работают **независимо друг от друга**.

---

### 2. Что было испробовано (14 коммитов)

| Подход | Результат |
|--------|-----------|
| positionSource abstraction | ❌ Не помогло |
| TradeZones exclusion | ⚠️ Частично |
| OnWorldShifted event | ⚠️ Частично |
| Client-side correction disabled | ⚠️ Частично |
| World shift cooldown | ✅ От спама помогает |
| Threshold tuning (100k→150k) | ❌ Не помогло |
| GetWorldPosition() с fallback | ❌ Не помогло |

**Вывод:** Все исправления были вокруг FloatingOriginMP, но проблема в **интеграции с NGO**.

---

### 3. Root Cause Analysis

#### Причина 1: NGO NetworkTransform использует Transform.position
```csharp
// NetworkTransform.cs (internal Unity)
transform.position = Vector3.Lerp(from, to, t);
```
Когда WorldRoot сдвигается, `transform.position` меняется, но сервер отправляет абсолютные координаты.

#### Причина 2: Сервер отправляет АБСОЛЮТНЫЕ координаты
```csharp
// Server side:
position = player.transform.position; // Абсолютная позиция
// Отправляется клиенту
```
Клиент применяет сдвиг мира, получает неправильные данные.

#### Причина 3: Интерполяция накапливает ошибку
```csharp
// Каждый кадр:
lerp(from, to, t); // Ошибка накапливается
```

---

## 💡 Альтернативные решения (НЕ испробованные)

### Option A: Отключить NetworkTransform на больших расстояниях

```csharp
public class FloatingOriginIntegration : MonoBehaviour
{
    public NetworkObject networkObject;
    
    void OnWorldShifted(Vector3 offset)
    {
        // Отключаем синхронизацию позиции на время сдвига
        if (networkObject != null)
        {
            var nt = networkObject.GetComponent<NetworkTransform>();
            if (nt != null)
            {
                nt.enabled = false;
                // Через 0.5с включаем обратно
                Invoke(nameof(ReEnableNetworkTransform), 0.5f);
            }
        }
    }
}
```

### Option B: Отправлять СДВИГ через RPC перед синхронизацией

```csharp
// Server:
[ServerRpc]
void RequestWorldShiftRpc(Vector3 cameraPos)
{
    Vector3 offset = CalculateOffset(cameraPos);
    
    // Сначала отправляем offset ВСЕМ
    BroadcastWorldShiftRpc(offset);
    
    // Затем отправляем позиции с учётом offset
    SyncPositionsWithOffset(offset);
}
```

### Option C: Использовать Chunk-Based Streaming вместо FloatingOrigin

Вместо сдвига мира — **загружать/выгружать чанки вокруг игрока**.

Плюсы:
- Не нужно сдвигать мир
- NGO работает нормально
- ChunkLoader уже реализован

Минусы:
- Нужен серверный контроль загрузки
- Больше сетевого трафика

### Option D: Double precision via DOTS

```csharp
// НЕ СОВМЕСТИМО С NGO!
using Unity.Mathematics;
double3 position; // double precision
```

**ВНИМАНИЕ:** DOTS/ECS несовместим с NGO (Netcode for GameObjects)!

---

## 📊 Unity Official Large World Solutions

| Solution | Совместимость с NGO | Применимость |
|----------|---------------------|--------------|
| Floating Origin | ✅ Да | ⚠️ Имеет проблемы с NGO |
| Chunk Streaming | ✅ Да | ✅ Лучший вариант |
| DOTS/ECS | ❌ Нет | ❌ Несовместимо |
| Addressables | ✅ Да | ✅ Для ассетов |
| LOD Groups | ✅ Да | ✅ Помогает со стабильностью |
| Camera Far Clip < 5000 | ✅ Да | ✅ Рекомендуется |

---

## 🎯 Рекомендации

### На ближайшую перспективу:

1. **Документировать что jitter не может быть полностью исправлен** в текущей архитектуре
2. **Использовать Chunk-Based Streaming** вместо (или параллельно с) FloatingOriginMP
3. **Отложить jitter fix** на отдельную итерацию после стабилизации системы

### План действий:

| Приоритет | Действие | Результат |
|-----------|----------|-----------|
| 1 | Завершить Iteration 1 (базовые фиксы) | ✅ Offset не растёт |
| 2 | Перейти к Iteration 2 (WorldStreamingManager) | ChunkLoader работает |
| 3 | Перейти к Iteration 3 (PlayerChunkTracker) | Сервер управляет чанками |
| 4 | Отложить jitter fix | Отдельная задача |

---

## 📁 Документы для изучения

- `docs/world/LargeScaleMMO/SOLUTION_OPTIONS.md` — Alternative solutions
- `docs/LARGE_WORLD_SOLUTIONS.md` — Unity official
- `docs/world/LargeScaleMMO/01_Architecture_Plan.md` — Architecture

---

**Вывод:** FloatingOriginMP в текущей реализации **имеет фундаментальную проблему** с интеграцией в NGO. Для полного исправления требуется переделка архитектуры (Option B или C).

**Альтернатива:** Использовать Chunk-Based Streaming который уже реализован и не конфликтует с NGO.

---

**Автор:** Claude Code + Subagents Analysis  
**Дата:** 18.04.2026, 16:28