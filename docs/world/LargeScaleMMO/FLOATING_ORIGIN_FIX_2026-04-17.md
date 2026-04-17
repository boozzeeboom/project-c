# FloatingOriginMP Fixes — 2026-04-17

## ✅ ИСПРАВЛЕНИЯ ВНЕСЁННЫЕ СЕГОДНЯ

### 1. TradeZones Исключён из Сдвига

**Проблема:** TradeZones сдвигался вместе с WorldRoot, что ломало камеру.

**Решение:** 
- TradeZones добавлен в `excludeFromWorldRoots`
- После сдвига TradeZones восстанавливается на `(0,0,0)`

```csharp
// excludeFromWorldRoots
"TradeZones",
"TradeZone",
"Player",
"NetworkPlayer"
```

### 2. positionSource Корректируется на totalOffset

**Проблема:** positionSource сдвигался вместе с миром → offset накапливался бесконечно.

**Решение:**
```csharp
// GetWorldPosition() возвращает "истинную" позицию
if (positionSource != null)
{
    Vector3 truePos = positionSource.position - _totalOffset;
    return truePos;
}
```

### 3. Формула Distance Исправлена

**Проблема:** `adjustedPos = cameraWorldPos - _totalOffset` давала огромные значения.

**Решение:**
```csharp
// GetWorldPosition() уже возвращает скорректированную позицию
float distFromOrigin = cameraWorldPos.magnitude;
```

### 4. Debug Логи Добавлены

- Parent объекта FloatingOriginMP
- TradeZones позиция после сдвига
- camera позиция после сдвига

---

## 🔴 ПРОБЛЕМА: АРТЕФАКТЫ НА ПЕРСОНАЖЕ

### Исследованные Причины (Subagent Results)

#### 1. Нет синхронизации с OnWorldShifted (КРИТИЧНО)
```csharp
// FloatingOriginMP.cs вызывает:
OnWorldShifted?.Invoke(offset);

// НО NetworkPlayer.cs НЕ подписан!
```

**✅ ИСПРАВЛЕНО:** NetworkPlayer.cs теперь подписан на OnWorldShifted!

#### 2. NetworkTransform имеет Server Authority
- `AuthorityMode: 1` — сервер управляет позицией
- `PositionThreshold: 0.001` — очень малый порог
- Интерполяция включена

#### 3. Client-side Prediction ломается
```csharp
// NetworkPlayer.cs: FixedUpdate коррекция
if (_hasServerPosition)
{
    float dist = Vector3.Distance(transform.position, _serverPosition);
    if (dist > positionCorrectionThreshold)
    {
        // Коррекция использует сдвинутую позицию
    }
}
```

**✅ ИСПРАВЛЕНО:** OnWorldShifted сбрасывает _hasServerPosition!

#### 4. ShipController кэширует компоненты
```csharp
// TurbulenceEffect, SystemDegradationEffect кэшируют _transform
// После сдвига используют СТаРЫЕ значения
```

#### 5. Velocity не сбрасывается
```csharp
// ShipController не сбрасывает _rb.linearVelocity после сдвига
```

**✅ ИСПРАВЛЕНО:** OnWorldShifted сбрасывает _velocity!

---

## 📋 ПЛАН ИСПРАВЛЕНИЯ АРТЕФАКТОВ

### ✅ Приоритет 1: Подписка NetworkPlayer на OnWorldShifted (ГОТОВО)
```csharp
// NetworkPlayer.cs
void OnNetworkSpawn()
{
    ProjectC.World.Streaming.FloatingOriginMP.OnWorldShifted += OnWorldShifted;
}

private void OnWorldShifted(Vector3 offset)
{
    _hasServerPosition = false;  // Сброс коррекции
    _velocity = Vector3.zero;      // Сброс velocity
}
```

### Приоритет 2: Сброс Velocity после сдвига (ЧАСТИЧНО)
- ✅ Сбрасывается в NetworkPlayer
- ⏳ Нужно сбрасывать в ShipController тоже

### Приоритет 3: Оповещение ShipController (НЕ ГОТОВО)
- ShipController нуждается в подписке на OnWorldShifted

### Приоритет 4: Сброс NetworkTransform интерполяции (НЕ ГОТОВО)
- Требует дополнительного исследования

---

## 📊 РЕЗУЛЬТАТЫ ТЕСТИРОВАНИЯ

| Тест | До | После |
|------|-----|-------|
| TradeZones на (0,0,0) | ❌ | ✅ |
| Offset не растёт | ❌ | ✅ (5365 < 150000) |
| Camera на месте | ❌ | ✅ |
| Артефакты персонажа | ❌ | ❌ (требуется исправление) |

---

## 🔍 ДАЛЬНЕЙШИЕ ИССЛЕДОВАНИЯ

1. Проверить иерархию сцены — Player должен быть ВНЕ WorldRoot
2. Настроить NetworkTransform для совместимости с FO
3. Рассмотреть отключение интерполяции во время сдвига
