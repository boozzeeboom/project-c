# Floating Origin Position Sync Issue — 17.04.2026

**Дата:** 17.04.2026, 18:58  
**Проект:** ProjectC_client  
**Status:** 🔴 ПРОБЛЕМА ВЫЯВЛЕНА — требуется анализ

---

## 🔴 КЛЮЧЕВАЯ ПРОБЛЕМА

### Наблюдение

**Во время игры (бег):**
```
cameraWorldPos=(-78, 1, 61)
_totalOffset=(0, 0, 0)
adjustedPos=(-78, 1, 61)
dist=99
threshold=100000
```

**На паузе (стоп игры):**
```
cameraPos=(-304970.20, 0.58, 901228.10)
totalOffset=(-300000.00, 0.00, 900000.00)
```

### Вывод

`LocalClient.PlayerObject.transform.position` **НЕ синхронизируется** во время движения!

- Во время игры: показывает ~(-78, 1, 61) — позиция у origin
- На паузе: показывает ~(-304970, 901228) — ПРАВИЛЬНАЯ позиция!

---

## ИЗВЕСТНЫЕ ФАКТЫ

### 1. LateUpdate работает
```
[FloatingOriginMP] CRITICAL SHIFT: offset=(-300000.00, 0.00, 900000.00)
[FloatingOriginMP] After shift: totalOffset=(-300000.00, 0.00, 900000.00)
```

Сдвиг на 900,000 происходит — значит LateUpdate срабатывает когда позиция станет "правильной".

### 2. adjustedPos = cameraWorldPos - _totalOffset
```
dist = |cameraWorldPos - _totalOffset|
```

Если `_totalOffset = 0`, то `dist = cameraWorldPos.magnitude`.

### 3. Threshold срабатывает только на паузе
```
threshold = 100000
```

Когда `dist > 100000` → сдвиг происходит.

---

## ВОЗМОЖНЫЕ ПРИЧИНЫ

### Гипотеза 1: NetworkTransform Interpolation
NGO использует **интерполяцию** для визуализации движения.
- `transform.position` показывает **интерполированную** позицию
- На паузе интерполяция останавливается → показывает **реальную** позицию

### Гипотеза 2: Client Prediction
- Клиент **предсказывает** движение
- `LocalClient.PlayerObject` — это **серверный representation**
- Реальная позиция хранится в другом месте

### Гипотеза 3: Parent/Child Relationship
- `LocalClient.PlayerObject` — это **child TradeZones/WorldRoot**
- Когда мир сдвигается, сетевой объект тоже сдвигается
- `transform.position` показывает позицию **относительно parent**

### Гипотеза 4: NetworkVariable синхронизация
- Позиция синхронизируется через `NetworkVariable<Vector3>`
- Синхронизация происходит **между кадрами**
- `transform.position` может быть **запаздывающей**

---

## ЧТО ТРЕБУЕТСЯ ДЛЯ АНАЛИЗА

1. Структура `NetworkPlayer` иерархия
2. Как работает `NetworkTransform` в NGO
3. Где хранится "реальная" позиция игрока
4. Как работает client-side prediction

---

## АНАЛИЗ С ПОМОЩЬЮ SUBAGENTS

Subagents должны:
1. Изучить `NetworkPlayer.cs` — структуру и компоненты
2. Изучить NGO `NetworkTransform` — как синхронизируется позиция
3. Найти "реальную" позицию игрока во время движения

---

## SUBAGENT ANALYSIS RESULTS

### Agent 1: NGO NetworkTransform Specialist

**КОРНЕВАЯ ПРИЧИНА: Две разные позиции!**

| Состояние | `transform.position` | Что это |
|-----------|---------------------|---------|
| **Во время движения** | Интерполированная (визуальная) | Плавное отображение, НЕ реальная |
| **На паузе** | Реальная серверная | Т.к. интерполяция остановилась |

**Почему так:**
- NGO отправляет позицию ~15 раз/сек
- Клиенты интерполируют к ней: `transform.position = Lerp(current, target, time)`
- На паузе интерполяция останавливается → показывает реальную

**Решение:**
1. Получить доступ к `NetworkTransform.AuthoritativePosition` (если доступен)
2. Использовать серверную позицию через `NetworkVariable<Vector3>`
3. Добавить кастомный компонент для экспорта позиции

---

### Agent 2: ProjectC NetworkPlayer Specialist

**Анализ NetworkPlayer.cs:**
- Движение через `CharacterController.Move()` (НЕ через transform.position)
- Нет отдельной синхронизации позиции через NetworkVariable
- Позиция синхронизируется через `NetworkTransform`

**Проблема:**
```csharp
LocalClient.PlayerObject.transform.position // ← ИНТЕРПОЛИРОВАННАЯ позиция
```

**Решение для singleplayer:**
- FloatingOriginMP работает в **Local mode**
- Использовать `Camera.main.transform.position` вместо `LocalClient.PlayerObject`

**Решение для multiplayer:**
- **Server Authority mode**
- Сервер отслеживает позицию через `NetworkVariable<Vector3>`
- Сервер вызывает сдвиг → рассылает RPC

---

### Agent 3: Floating Origin Architect

**Рекомендуемая архитектура:**

```
┌─────────────────────────────────────────────────────────────┐
│                    SERVER AUTHORITY MODE                      │
├─────────────────────────────────────────────────────────────┤
│  1. FloatingOriginMP на СЕРВЕРЕ отслеживает позицию          │
│  2. Сервер вызывает BroadcastWorldShiftRpc(offset)          │
│  3. Клиенты принимают сдвиг через ApplyWorldShift()         │
│                                                              │
│  Клиент: mode = OriginMode.ServerSynced                      │
│  - GetWorldPosition() НЕ вызывается для сдвига               │
│  - Ждём серверный RPC                                        │
└─────────────────────────────────────────────────────────────┘
```

**Для Singleplayer (Local mode):**
- НЕ использовать `LocalClient.PlayerObject`
- Использовать `positionSource` или `Camera.main`
- ИЛИ добавить компонент `FloatingOriginTracker` на игрока

---

## RECOMMENDED SOLUTION

### Option 1: Server Authority (Multiplayer)

```csharp
public class FloatingOriginMP : MonoBehaviour
{
    void LateUpdate()
    {
        if (mode == OriginMode.ServerAuthority && !IsServer) return;
        if (mode == OriginMode.ServerSynced) return; // Ждём RPC
        
        // Только сервер вычисляет и рассылает сдвиг
        if (IsServer)
        {
            Vector3 playerPos = GetServerPosition();
            if (playerPos.magnitude > threshold)
            {
                BroadcastWorldShiftRpc(CalculateOffset(playerPos));
            }
        }
    }
}
```

### Option 2: Local Tracker Component

Создать компонент `FloatingOriginTracker` на игроке:

```csharp
public class FloatingOriginTracker : MonoBehaviour
{
    public Vector3 WorldPosition => transform.position;
}
```

FloatingOriginMP ищет этот компонент для получения позиции.

---

## NEXT STEPS

1. **Immediate:** Изменить FloatingOriginMP на Server Authority mode
2. **Testing:** Протестировать сдвиг в multiplayer
3. **Documentation:** Обновить FloatingOriginMP.cs с комментариями

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 19:01 MSK
