# Iteration 3.14: Анализ логов — КРИТИЧЕСКАЯ ПРОБЛЕМА

**Дата:** 18.04.2026, 21:55 MSK  
**Статус:** 🚨 КРИТИЧЕСКАЯ ПРОБЛЕМА ОБНАРУЖЕНА

---

## 🚨 КРИТИЧЕСКОЕ НАБЛЮДЕНИЕ: Y координата OSCILLS!

### Данные из логов:
```
[PlayerChunkTracker] position=(503.32, 352.55, 300.00)
[NetworkPlayer] worldPosition=(502.49, 503.00, 300.00)

[PlayerChunkTracker] position=(502.49, 503.00, 300.00)
[NetworkPlayer] worldPosition=(503.32, 349.70, 300.00)

[PlayerChunkTracker] position=(503.32, 349.70, 300.00)
[NetworkPlayer] worldPosition=(502.49, 503.00, 300.00)

[PlayerChunkTracker] position=(502.49, 503.00, 300.00)
[NetworkPlayer] worldPosition=(503.32, 346.24, 300.00)

[PlayerChunkTracker] position=(503.32, 346.24, 300.00)
[NetworkPlayer] worldPosition=(502.49, 503.00, 300.00)
```

### Анализ:
```
Паттерн Y координаты:
  352.55 → 503.00 → 349.70 → 503.00 → 346.24 → 503.00
  
Это НЕ нормальное движение!
Y oscills между ~350 и ~503 — разница ~150 единиц!
```

---

## ❌ ПРОБЛЕМА: Телепортация НЕ работает!

### Ожидание:
```
Игрок телепортирован на (0, 5, 0)
Должен быть в позиции ~(0, 5, 0)
```

### Реальность:
```
Игрок в позиции ~(500, 350-503, 300) — это ДАЛЕКО от (0, 5, 0)!
```

### Возможные причины:
1. **TeleportOwnerPlayerToOrigin() НЕ вызывается?**
2. **NetworkTransform перезаписывает позицию?**
3. **CharacterController.enabled = false не работает?**
4. **Client-side prediction восстанавливает старую позицию?**

---

## 📊 АНАЛИЗ OSCILLATION

### Фиксированная позиция:
```
X: ~502-503 (стабильно)
Z: 300 (стабильно)
Y: 346-503 (oscills!)
```

### Вычисление чанка:
```
При Y = 352: gridY = floor(352 / 2000) = 0
При Y = 503: gridY = floor(503 / 2000) = 0

X = 503: gridX = floor(503 / 2000) = 0
Z = 300: gridZ = floor(300 / 2000) = 0

Chunk = (0, 0) — НО ОЖИДАЛИСЬ ДАЛЬНИЕ ЧАНКИ!
```

### Вопрос: Почему игрок на ~500, а не на ~0?
```
TeleportOwnerPlayerToOrigin() вызывается, но игрок остаётся на ~500!
Это означает что телепортация НЕ применяется корректно.
```

---

## 🔍 ВОЗМОЖНЫЕ ПРИЧИНЫ OSCILLATION

### 1. Client-side prediction восстанавливает позицию?
```
В NetworkPlayer.FixedUpdate():
  if (_hasServerPosition)
  {
      transform.position = Vector3.Lerp(...);
  }
  
Если _hasServerPosition = true и _serverPosition = (500, ...),
то позиция восстанавливается после телепорта!
```

### 2. NetworkTransform синхронизирует старую позицию?
```
После телепорта серверная позиция = (0, 5, 0)
НО клиент получает синхронизацию и восстанавливает старую позицию?
```

### 3. CharacterController не позволяет телепортацию?
```
_characterController.Move() вызывается в ProcessMovement()
Если телепортация не отключает movement, игрок возвращается?
```

---

## 📋 ЧТО НУЖНО ПРОВЕРИТЬ

### 1. Вызывается ли TeleportOwnerPlayerToOrigin()?
```
Добавить лог В НАЧАЛО метода:
  Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: ВЫЗВАН!");
```

### 2. Применяется ли телепортация?
```
Добавить лог ПОСЛЕ set position:
  Debug.Log($"[FloatingOriginMP] TeleportOwnerPlayerToOrigin: позиция УСТАНОВЛЕНА в {localPos}");
```

### 3. Восстанавливается ли позиция?
```
Проверить _hasServerPosition после телепорта.
Если true → позиция восстанавливается клиентской коррекцией!
```

### 4. Почему Y oscills между 350 и 503?
```
350 = ~500 - 150 = возможно гравитация?
503 = ~500 = возможно серверная позиция?

Это выглядит как КОЛЕБАНИЕ между клиентской и серверной позицией!
```

---

## 🔧 ПЛАН ИСПРАВЛЕНИЯ

### Шаг 1: Зафиксировать TELEПОРТАЦИЮ
```
В TeleportOwnerPlayerToOrigin():
  1. Добавить лог "ВЫЗВАН"
  2. Добавить лог "позиция УСТАНОВЛЕНА"
  3. Проверить CharacterController.enabled
  4. Проверить что это именно тот объект (IsOwner)
```

### Шаг 2: Проверить Client-side prediction
```
В NetworkPlayer.FixedUpdate():
  После телепорта _hasServerPosition должна быть false!
  Добавить проверку:
    if (_hasServerPosition) Debug.Log($"[NetworkPlayer] STILL have server position: {_serverPosition}");
```

### Шаг 3: Проверить NetworkTransform
```
Возможно NetworkTransform перезаписывает позицию после телепорта.
Проверить: отключается ли NetworkTransform на время телепорта?
```

### Шаг 4: Проверить интервал обновления
```
chunkTrackerUpdateInterval = 0.25s
НО логи показывают вызовы СЛИШКОМ ЧАСТО!

Возможно updateInterval не работает правильно?
Проверить: _lastChunkTrackerUpdate обновляется?
```

---

**Обновлено:** 18.04.2026, 21:55 MSK  
**Автор:** Claude Code  
**Версия:** iteration_3_debug_v2