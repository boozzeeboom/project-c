# Next Session Prompt: StreamingTest_AutoRun Bug Fixed

**Дата:** 17 апреля 2026 г.  
**Проект:** ProjectC_client  
**Status:** ⚠️ НАЙДЕН БАГ — множественные вызовы ResetOrigin

---

## ⚠️ НАЙДЕННАЯ ПРОБЛЕМА

### Корневая причина артефактов

**Проблема:** `ResetOrigin()` вызывается **МНОГОКРАТНО** при плавной телепортации.

В `StreamingTest_AutoRun.Update()`:
```csharp
if (direction.magnitude > 50000f)
{
    fo.ResetOrigin();  // ⚠️ ВЫЗЫВАЕТСЯ ПРИ КАЖДОМ КАДРЕ!
}
```

При плавном перемещении камеры к 150,000:
1. Камера движется: 0 → 50,000 → 50,100 → 50,200 → ...
2. При direction.magnitude > 50,000 → вызывается ResetOrigin()
3. Мир сдвигается, камера "телепортируется" относительно мира
4. На следующем кадре — снова проверка → снова сдвиг
5. **27+ сдвигов за одну телепортацию!**

### Расчёт
```
-4,050,000 / 150,000 = 27 сдвигов
```

---

## ✅ ЧТО ИСПРАВЛЕНО

### 1. StreamingTest_AutoRun.cs

**Убран дублирующий вызов ResetOrigin() в TeleportToTestPosition():**
- Было: `fo.ResetOrigin()` вызывался перед телепортацией (когда камера на 0) — бесполезно
- Стало: ResetOrigin вызывается только в OnTeleportComplete() -> TeleportToPeak()

**Убран вызов ResetOrigin() в Update():**
- Было: ResetOrigin() вызывался при каждом кадре если direction.magnitude > 50,000
- Стало: ResetOrigin вызывается только в OnTeleportComplete()

### 2. FloatingOriginMP.cs (ранее)

- Добавлен `GetWorldPosition()` с 4 уровнями fallback
- NullReferenceException исправлен

---

## ⚠️ ОСТАВШАЯСЯ ПРОБЛЕМА

Даже после исправления, `ResetOrigin()` вызывается в `OnTeleportComplete()` -> `TeleportToPeak()`.

Это правильно, но нужно проверить что:
1. `smoothTeleport = false` (для тестирования) ИЛИ
2. В `smoothTeleport = true` LateUpdate не вызывает ResetOrigin

**Текущий код:**
- Update(): НЕ вызывает ResetOrigin (исправлено)
- TeleportToTestPosition(): НЕ вызывает fo.ResetOrigin() (исправлено)
- OnTeleportComplete(): вызывает TeleportToPeak() -> floatingOrigin.ResetOrigin()

---

## 🧪 ТЕСТИРОВАНИЕ

### Тест 1: Без плавной телепортации
```
1. В Inspector: smoothTeleport = false
2. Play Mode
3. F5 — телепортация на 150,000
4. Проверь: должен быть ТОЛЬКО ОДИН сдвиг
```

### Тест 2: С плавной телепортацией
```
1. В Inspector: smoothTeleport = true
2. Play Mode
3. F5 — плавная телепортация на 150,000
4. Проверь: LateUpdate больше не вызывает ResetOrigin
5. OnTeleportComplete вызывает сдвиг один раз
```

### Тест 3: F8
```
1. Play Mode
2. F8 — ручной вызов ResetOrigin
3. Проверь: сдвиг происходит один раз
```

---

## ОЖИДАЕМЫЕ ЛОГИ ПОСЛЕ ИСПРАВЛЕНИЯ

```
[FloatingOriginMP] CRITICAL SHIFT: offset=(150000.00, 0.00, 150000.00), roots=2
[FloatingOriginMP] Roots BEFORE shift: 'WorldRoot'=(0.00, 0.00, 0.00)  ← БЫЛО 0,0,0!
[FloatingOriginMP] After shift: totalOffset=(150000.00, 0.00, 150000.00)
```

WorldRoot ДОЛЖЕН начинаться с (0, 0, 0), не с (-4,050,000)!

---

## Документы

| Документ | Описание |
|----------|----------|
| `ARTIFACT_ANALYSIS_2026-04-17.md` | Анализ артефактов |
| `LARGE_WORLD_SOLUTIONS.md` | Large world solutions |
| `SESSION_2026-04-17_FIXED.md` | Ранние исправления |

---

## Следующие шаги

1. [ ] Протестировать с smoothTeleport = false
2. [ ] Протестировать с smoothTeleport = true
3. [ ] Проверить что WorldRoot начинается с (0,0,0)
4. [ ] Проверить что артефакты исчезли

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 18:01 MSK
