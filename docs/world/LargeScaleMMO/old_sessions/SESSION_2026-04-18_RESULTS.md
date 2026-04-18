# Сессия 18.04.2026 — Результаты тестирования FloatingOriginMP

## Дата: 18 апреля 2026, 01:05  
## Статус: ПРОБЛЕМА ОБНАРУЖЕНА

---

## ✅ Что реализовано

1. **RequestWorldShiftRpc** — ServerRpc для запроса сдвига
2. **BroadcastWorldShiftRpc** — ClientRpc для синхронизации
3. **ApplyServerShift / ApplyLocalShift** — разделение логики
4. **Исправлен CS0618** — обновлён атрибут для Unity 6

---

## 📊 Результаты тестирования

### Режим: ServerAuthority  
### Телепортация: F6 → Far Peak (-250000, 500, -250000)

### ✅ Работает правильно:

```
TradeZones restored: 1/1
TradeZones NOW at: (0, 0, 0)
WorldRoot NOW at: (250000, 0, 250000)
Player position: (-250000.00, 500.00, -250000.00)
```

- TradeZones остаётся на месте ✓
- WorldRoot сдвигается ✓
- Player остаётся на месте ✓

### ❌ Проблема: Jitter эффект присутствует

```
positionSource=(-249998, 503, -250000)
totalOffset=(-250000, 0, -250000)
truePos=(2, 503, 0)  ← НЕПРАВИЛЬНО!
```

**Проблема в GetWorldPosition():**
```csharp
Vector3 truePos = positionSource.position - _totalOffset;
// = (-250000) - (-250000) = 0  ← Должно быть -250000!
```

После телепортации `positionSource.position` уже включает сдвиг (относительно WorldRoot), но код вычитает `totalOffset` повторно.

---

## 🔍 Анализ проблемы

### До телепорта (после первого сдвига):
- WorldRoot: (-250000, 0, -250000)
- Player (relative to WorldRoot): (0, 500, 0)
- Player (absolute): (-250000, 500, -250000)
- totalOffset: (-250000, 0, -250000)

### После телепортации:
- WorldRoot: (0, 0, 0) — сброшен
- Player (absolute): (-250000, 500, -250000)
- totalOffset: (-250000, 0, -250000) — всё ещё хранит старый offset!

### Проблема:
`GetWorldPosition()` вызывается через `positionSource` (который УЖЕ сдвинут), и вычитает `totalOffset` повторно.

---

## 📋 Следующие шаги

1. [ ] Исправить расчёт `GetWorldPosition()` для корректной работы после телепорта
2. [ ] Добавить проверку: если `positionSource` уже близко к origin (< threshold), не вычитать totalOffset
3. [ ] Протестировать снова

---

## Файлы изменены

- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`
- `docs/world/LargeScaleMMO/IMPLEMENTATION_PLAN.md`
- `docs/world/LargeScaleMMO/TESTING_FLOATORIGIN.md`
