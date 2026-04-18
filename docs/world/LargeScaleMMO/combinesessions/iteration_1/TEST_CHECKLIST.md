# Iteration 1: Тестовый чеклист

**Статус:** ⏳ Ожидает проверки  
**Дата:** 18.04.2026, 15:53 (UTC+5)

---

## 🧪 Обязательные проверки ПЕРЕД продолжением

### Step 1: Unity Play Mode

```
1. Открой Unity Editor
2. Открой сцену: Assets/ProjectC_1.unity
3. Нажми Play (▶)
4. Открой Console: Ctrl+Shift+C
```

---

### Step 2: Проверь HUD

**Должно отображаться:**
- [ ] `Offset: (0, 0, 0)` — без сдвига
- [ ] `Shifts: 0` — без сдвигов
- [ ] `Pos: (0, ?, 0)` — позиция игрока
- [ ] `Init: True` — инициализирован

---

### Step 3: Нажми F6

**Ожидаемые результаты:**

| Проверка | Критерий | Статус |
|----------|----------|--------|
| Console logs | `positionSource=(-249998, 503, -250000)` | ⏳ |
| Console logs | `truePos=(-249998, 503, -250000)` ← ПРАВИЛЬНО | ⏳ |
| HUD Offset | `Offset: (-250000, 0, -250000)` | ⏳ |
| HUD Shifts | `Shifts: 1` | ⏳ |
| Jitter | Нет дрожания камеры | ⏳ |

---

### Step 4: Нажми F8 (Reset Origin)

**Ожидаемые результаты:**

| Проверка | Критерий | Статус |
|----------|----------|--------|
| Console logs | `ResetOrigin: was totalOffset=...` | ⏳ |
| Console logs | `ResetOrigin: now totalOffset=(0, 0, 0)` | ⏳ |
| HUD Offset | `Offset: (0, 0, 0)` | ⏳ |

---

### Step 5: Проверь новые методы

**В Console выполни:**
```csharp
var fom = FindObjectOfType<FloatingOriginMP>();
Debug.Log($"ShouldUseFloatingOrigin: {fom.ShouldUseFloatingOrigin()}");
Debug.Log($"IsNearOrigin: {fom.IsNearOrigin()}");
Debug.Log($"IsFloatingOriginActive: {fom.IsFloatingOriginActive}");
```

**Ожидаемые результаты (после F8):**
- `ShouldUseFloatingOrigin: False` (близко к origin)
- `IsNearOrigin: True` (pos magnitude < 75000)
- `IsFloatingOriginActive: False` (totalOffset ≈ 0)

---

### Step 6: Снова на Far Peak (F6)

**Ожидаемые результаты:**
- `ShouldUseFloatingOrigin: True` (далеко от origin)
- `IsFloatingOriginActive: True` (totalOffset > 100)

---

## 📋 Заполнение результатов

После каждой проверки отметь:
- ✅ Работает
- ❌ Не работает
- ⚠️ Частично работает

---

## ❌ Если тесты не проходят

| Проблема | Причина | Решение |
|----------|---------|---------|
| Jitter после F6 | positionSource не обновлён | Назначить positionSource в инспекторе |
| truePos = (2, 503, 0) | Фикс не применился | Переоткрыть Unity, перекомпилировать |
| Console пустой | showDebugLogs = false | Установить true в инспекторе |
| HUD не отображается | showDebugHUD = false | Установить true в инспекторе |

---

## ✅ Подтверждение

Все тесты пройдены успешно:
- [ ] F6 teleport работает без jitter
- [ ] Console показывает правильные координаты
- [ ] Новые методы работают корректно

**Подпись:** _________________  
**Дата:** _________________