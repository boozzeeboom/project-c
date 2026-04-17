# Next Session Prompt: FloatingOriginMP — Игрок vs Камера

**Дата:** 17 апреля 2026 г.  
**Проект:** ProjectC_client  
**Status:** ✅ КОРНЕВАЯ ПРИЧИНА НАЙДЕНА

---

## 🔴 КОРНЕВАЯ ПРИЧИНА

Debug лог показал:
```
cameraWorldPos=(0, 0, 0)  ← КАМЕРА НА (0,0,0)!
```

**Камера НЕ двигается!** Она остаётся на (0,0,0) потому что:
- Камера — отдельный объект, не child игрока
- Игрок бежит на 150,000+, но камера стоит на месте
- FloatingOriginMP отслеживал позицию КАМЕРЫ, а не ИГРОКА!

---

## ✅ ИСПРАВЛЕНИЕ

### Было:
```csharp
// GetWorldPosition() искал: камеру
if (_camera != null) return _camera.transform.position;
if (Camera.main != null) return Camera.main.transform.position;
```

### Стало:
```csharp
// GetWorldPosition() ищет: ИГРОКА ПЕРВЫМ!
// 1. positionSource (явный)
// 2. NetworkManager.Singleton.LocalClient.PlayerObject (ПРИОРИТЕТ!)
// 3. FindObjectsByType<NetworkObject>() → IsOwner
// 4. _camera
// 5. Camera.main
// 6. Vector3.zero
```

---

## НОВЫЙ DEBUG ЛОГ

После исправления лог покажет:
```
[FloatingOriginMP] Debug: playerPos=150000, _totalOffset=0, adjustedPos=150000, dist=150000, threshold=150000
```

Это означает:
- `playerPos` — позиция ИГРОКА (не камеры!)
- `dist = 150000` — игрок ушёл на 150k от мира
- `dist > threshold (150000)` — сдвиг должен произойти!

---

## 🧪 ТЕСТИРОВАНИЕ

### Тест 1: Local режим + ручной бег
```
1. FloatingOriginMP.mode = Local
2. Play Mode
3. Ручной бег на 150,000+
4. Ожидаемый лог:
   [FloatingOriginMP] Debug: playerPos=150000, _totalOffset=0, dist=150000, threshold=150000
   [FloatingOriginMP] CRITICAL SHIFT: ...
```

### Тест 2: Проверка сдвига
```
После сдвига:
[FloatingOriginMP] Debug: playerPos=160000, _totalOffset=150000, adjustedPos=10000, dist=10000, threshold=150000
```
- `adjustedPos = 160000 - 150000 = 10000`
- `dist = 10000 < 150000` — сдвиг НЕ нужен!

### Тест 3: ServerSynced
```
1. FloatingOriginMP.mode = ServerSynced
2. LateUpdate пропускает (ждём сервер)
3. F8 — ручной ResetOrigin
4. Ожидаемый лог:
   [FloatingOriginMP] Before ResetOrigin: playerPos=150000, ...
   [FloatingOriginMP] After ResetOrigin: ...
```

---

## АРХИТЕКТУРА

### Проблема Floating Origin
```
Игрок бежит:   0 → 50,000 → 100,000 → 150,000
Камера:        0 → 0      → 0       → 0  (не двигается!)

Floating Origin проверял КАМЕРУ → сдвиг не происходил
```

### Решение
```
Игрок бежит:   0 → 50,000 → 100,000 → 150,000
Floating Origin проверяет ИГРОКА → сдвиг на 150,000
```

---

## ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `SESSION_2026-04-17_FINAL_REPORT.md` | Итоговый отчёт |
| `SESSION_2026-04-17_ANALYSIS.md` | Анализ subagents |

---

## КРИТЕРИИ УСПЕХА

- [ ] Debug лог показывает позицию ИГРОКА (playerPos), не камеры
- [ ] При беге на 150k+ происходит сдвиг
- [ ] Артефакты не появляются
- [ ] После сдвига playerPos - _totalOffset < threshold

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 18:39 MSK
