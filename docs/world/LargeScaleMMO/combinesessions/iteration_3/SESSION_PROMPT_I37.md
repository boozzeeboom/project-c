# Iteration 3.7 — Session Prompt: Тестирование Infinite Shift Fix

**Дата:** 18.04.2026  
**Статус:** 📋 ТРЕБУЕТСЯ ТЕСТИРОВАНИЕ  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅ I3.4 ✅ I3.5 ✅ I3.6 ⚠️

---

## 🎯 Цель Сессии

**Протестировать исправление бесконечного роста offset в ServerAuthority режиме**

После исправления I3.7 (LateUpdate пропускается в ServerAuthority):
- FloatingOriginMP больше НЕ вызывает ApplyServerShift() в LateUpdate
- Сервер управляет сдвигом через PlayerChunkTracker → RequestWorldShiftRpc()
- Offset больше НЕ растёт бесконечно

---

## 🔧 Внесённые Изменения (I3.7)

### `FloatingOriginMP.cs` — LateUpdate()

**Было:**
```csharp
void LateUpdate()
{
    // В Local режиме — работаем. В ServerSynced — пропускаем (ждём сервер).
    if (mode == OriginMode.ServerSynced) return;
    
    // ... проверка threshold и вызов ApplyServerShift() ...
}
```

**Стало:**
```csharp
void LateUpdate()
{
    // ITERATION 3.7 FIX: Полностью пропускаем LateUpdate в ServerAuthority режиме!
    // В ServerAuthority управление сдвигом осуществляется из:
    // 1. PlayerChunkTracker.FixedUpdate() — вызывает OnWorldShifted event
    // 2. FloatingOriginMP.RequestWorldShiftRpc() — серверный RPC
    if (mode == OriginMode.ServerAuthority)
    {
        return;
    }
    
    // ... остальной код ...
}
```

---

## 📋 Проверочный Список

### 1. Тест Бесконечного Роста Offset

**Ожидаемое поведение (ПОСЛЕ фикса):**
- totalOffset останавливается на одном значении
- Нет бесконечных SERVER SHIFT логов
- Offset не превышает 2-3 сдвига за сессию

**Лог ДО фикса (БАГ):**
```
totalOffset=300000 → 450000 → 600000 → 750000 → ... (бесконечно)
```

**Лог ПОСЛЕ фикса (ИСПРАВЛЕНО):**
```
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(300000.00, 0.00, 300000.00)
// Останавливается на 300k, больше нет логов
```

### 2. Тест Телепорта на 1M+

- [ ] Телепортировать игрока на 1,000,000+ units (клавиша F5)
- [ ] Проверить: Нет бесконечного роста offset
- [ ] Проверить: FloatingOrigin сдвигает мир корректно
- [ ] Проверить: Нет прыжков на 5+ чанков за один кадр

### 3. Тест Chunk Tracking

**Ожидаемое поведение:**
- ChunkId игрока меняется плавно (1-2 чанка за раз)
- Нет oscillation паттернов
- После остановки — ChunkId стабилен

---

## 🔍 Root Cause (Для Справки)

### Почему был бесконечный рост?

```
TradeZones (GameObject)
├── FloatingOriginMP (Camera)
│   └── position = (150000, 500, 150000) ← НЕ сдвигается!
```

1. LateUpdate вызывает GetWorldPosition()
2. GetWorldPosition() возвращает (150000, 500, 150000) — позиция камеры
3. distFromOrigin = 212,132 > threshold (150,000)
4. ApplyServerShift() вызывается СНОВА
5. TradeZones восстанавливается на (0,0,0), но камера на ней — остаётся на (150000, 500, 150000)
6. Цикл повторяется бесконечно

### Решение

Отключить LateUpdate в ServerAuthority режиме. Сервер управляет сдвигом через:
1. PlayerChunkTracker.FixedUpdate() → вызывает RequestWorldShiftRpc()
2. FloatingOriginMP.RequestWorldShiftRpc() → применяет сдвиг

---

## 📁 Документы Сессии

| Документ | Описание |
|----------|---------|
| `SESSION_END_I36_BUG.md` | Детали бага бесконечного роста |
| `FloatingOriginMP.cs` | Исправленный файл |

---

## ⚠️ Ожидаемые Результаты

### До I3.7 (БАГ):
```
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(300000.00, 0.00, 300000.00)
[FloatingOriginMP] SERVER SHIFT: offset=(150000.00, 0.00, 150000.00), cameraPos=(150000.00, 503.28, 150000.00)
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(450000.00, 0.00, 450000.00)
... бесконечно
```

### После I3.7 (ИСПРАВЛЕНО):
```
[FloatingOriginMP] SERVER SHIFT complete: totalOffset=(300000.00, 0.00, 300000.00)
// Больше нет логов SERVER SHIFT — LateUpdate пропускается
```

---

## 🎯 Следующие Шаги После Тестирования

1. **Если тесты успешны:** Отметить I3.7 как ЗАВЕРШЁННЫЙ
2. **Если баги остались:** Создать SESSION_PROMPT_I38 с деталями проблемы
3. **Следующая итерация:** I3.8 → Начать подготовку к тестированию с другим игроком

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 18:58  
**Следующий шаг:** Запустить Unity, провести тесты, записать результаты
