# Next Session Prompt: FloatingOriginMP — Final Fix

**Дата:** 17 апреля 2026 г.  
**Проект:** ProjectC_client  
**Status:** ✅ ИСПРАВЛЕНО — спам сдвигов остановлен

---

## ✅ ЧТО ИСПРАВЛЕНО

### 1. NullReferenceException (FloatingOriginMP.cs)
- Добавлен `GetWorldPosition()` с 4 уровнями fallback

### 2. Множественные вызовы ResetOrigin() (StreamingTest_AutoRun.cs)
- Убран вызов из Update()
- Убран дублирующий вызов из TeleportToTestPosition()

### 3. СПАМ СДВИГОВ В LateUpdate — **КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ**

**БЫЛО:**
```csharp
Vector3 cameraWorldPos = GetWorldPosition();
if (Mathf.Abs(cameraWorldPos.x) > threshold) // 250000 > 150000 = TRUE!
```

После сдвига камера остаётся на 250,000. Threshold=150,000. Значит `250,000 > 150,000` — **следующий LateUpdate СНОВА вызывает сдвиг!**

**СТАЛО:**
```csharp
Vector3 adjustedPos = cameraWorldPos - _totalOffset;
if (Mathf.Abs(adjustedPos.x) > threshold)
```

Теперь проверяем позицию **относительно мира**:
- `_totalOffset` = 250,000 (мир сдвинут на -250k)
- `cameraWorldPos` = 250,000
- `adjustedPos` = 250,000 - 250,000 = 0
- `0 > 150,000` = **FALSE** — сдвиг НЕ нужен!

---

## ТЕКУЩИЙ КОД LateUpdate()

```csharp
void LateUpdate() {
    // ...
    Vector3 cameraWorldPos = GetWorldPosition();
    
    // ВАЖНО: Проверяем позицию ОТНОСИТЕЛЬНО мира
    Vector3 adjustedPos = cameraWorldPos - _totalOffset;
    
    if (Mathf.Abs(adjustedPos.x) > threshold ||
        Mathf.Abs(adjustedPos.y) > threshold ||
        Mathf.Abs(adjustedPos.z) > threshold)
    {
        // Сдвигаем мир
        Vector3 offset = RoundShift(cameraWorldPos);
        ApplyShiftToAllRoots(offset);
        _totalOffset += offset;
        _shiftCount++;
        _lastShiftTime = Time.time;
    }
}
```

---

## 🧪 ТЕСТИРОВАНИЕ

### Тест 1: Local режим + ручной бег
```
1. FloatingOriginMP.mode = Local
2. Play Mode
3. Ручной бег на 150,000+
4. Проверить:
   - LateUpdate НЕ спамит
   - Один сдвиг на каждые ~150k пройденного расстояния
```

### Тест 2: F5 телепортация
```
1. Play Mode
2. F5 — телепортация на 250,000
3. Проверить:
   - Артефактов нет
   - Сдвиг один раз
   - LateUpdate не спамит
```

### Тест 3: ServerSynced
```
1. FloatingOriginMP.mode = ServerSynced
2. Play Mode
3. LateUpdate пропускает (режим ожидает от сервера)
4. F8 — ручной ResetOrigin
5. Проверить что работает
```

---

## ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `SESSION_2026-04-17_FINAL_REPORT.md` | Итоговый отчёт |
| `ARTIFACT_ANALYSIS_2026-04-17.md` | Анализ артефактов |
| `SESSION_2026-04-17_ANALYSIS.md` | Анализ subagents |

---

## КРИТЕРИИ УСПЕХА

- [ ] LateUpdate НЕ спамит после телепортации
- [ ] Ручной бег вызывает ровно 1 сдвиг на каждые ~150k
- [ ] Артефакты не появляются
- [ ] F5 телепортация работает корректно

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 18:27 MSK
