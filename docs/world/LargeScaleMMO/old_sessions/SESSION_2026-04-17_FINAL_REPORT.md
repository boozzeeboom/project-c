# Session Report: FloatingOriginMP — 17.04.2026 Final

**Дата:** 17.04.2026, 18:11  
**Проект:** ProjectC_client  
**Статус:** ✅ ОСНОВНЫЕ БАГИ ИСПРАВЛЕНЫ

---

## ИСТОРИЯ ПРОБЛЕМ

### Проблема 1: NullReferenceException (ИСПРАВЛЕНО)
- `_camera == null` вызывал NullReferenceException в ResetOrigin()
- Добавлен `GetWorldPosition()` с 4 уровнями fallback

### Проблема 2: Множественные вызовы ResetOrigin() (ИСПРАВЛЕНО)
- StreamingTest_AutoRun.Update() вызывал ResetOrigin() при каждом кадре
- Убран вызов из Update() и TeleportToTestPosition()

### Проблема 3: Артефакты при ручном беге (ВЫЯВЛЕНА)
- При F5 телепортации — артефактов нет
- При ручном беге — артефакты появляются
- **Причина:** Режим Server Synced

---

## РЕЖИМЫ FloatingOriginMP

### OriginMode.Local (рекомендуется для одиночной игры)
```csharp
if (mode == OriginMode.ServerSynced) return; // НЕ пропускаем LateUpdate
// Сдвигаем мир в LateUpdate если cameraPos > threshold
```

### OriginMode.ServerSynced (для мультиплеера)
```csharp
if (mode == OriginMode.ServerSynced) return; // ПРОПУСКАЕМ LateUpdate
// Ожидаем сдвиг от сервера через BroadcastWorldShiftRpc
```

---

## ЛОГИ ПРИ ТЕЛЕПОРТАЦИИ (F5)

```
[FloatingOriginMP] Before ResetOrigin: 
  worldPos=(250000, 500, 250000)
  totalOffset=(0, 0, 0)
  offset=(200000, 0, 200000)

[FloatingOriginMP] After ResetOrigin: 
  newWorldPos=(250000, 500, 250000)
  totalOffset=(200000, 0, 200000)
  shiftCount=7
```

**Анализ:**
- worldPos = 250,000 — позиция после телепортации
- totalOffset = 200,000 — сдвиг на 200,000
- shiftCount = 7 — 7 сдвигов от предыдущих телепортаций
- Артефактов НЕТ — сдвиг работает корректно

---

## ПОЧЕМУ АРТЕФАКТЫ ПРИ РУЧНОМ БЕГЕ

### Режим Server Synced
```
void LateUpdate() {
    if (mode == OriginMode.ServerSynced) return;  // ⚠️ НИЧЕГО НЕ ДЕЛАЕТ!
    // Сдвиг мира...
}
```

При режиме Server Synced:
1. LateUpdate НЕ сдвигает мир
2. ResetOrigin() вызывается только из TeleportToPeak()
3. При ручном беге ResetOrigin() не вызывается
4. Камера уходит на 100,000+ без сдвига мира
5. **Артефакты появляются!**

### Решение

**Для одиночной игры (без сервера):**
```csharp
FloatingOriginMP.mode = OriginMode.Local;
```

**Для мультиплеера:**
```csharp
// Host
FloatingOriginMP.mode = OriginMode.ServerAuthority;

// Client
FloatingOriginMP.mode = OriginMode.ServerSynced;
```

---

## ИСПРАВЛЕННЫЕ ФАЙЛЫ

| Файл | Изменения |
|------|-----------|
| `FloatingOriginMP.cs` | Добавлен GetWorldPosition(), исправлены null-check |
| `StreamingTest_AutoRun.cs` | Убран дублирующий вызов ResetOrigin() из Update() |

---

## ТЕКУЩИЕ НАСТРОЙКИ

### FloatingOriginMP
| Параметр | Значение |
|----------|----------|
| mode | **ServerSynced** ← ⚠️ |
| threshold | 150,000 |
| shiftRounding | 10,000 |
| showDebugLogs | true |
| showDebugHUD | true |

---

## РЕКОМЕНДАЦИЯ

**Изменить режим для одиночной игры:**
```
FloatingOriginMP.mode = OriginMode.Local;
```

ИЛИ

**Убрать ServerSynced режим вовсе** — для одиночной игры он не нужен.

---

## СЛЕДУЮЩИЕ ШАГИ

1. [ ] Изменить FloatingOriginMP.mode на Local
2. [ ] Протестировать ручной бег на 150,000+
3. [ ] Проверить что артефакты исчезли
4. [ ] Проверить ServerAuthority + ServerSynced в мультиплеере

---

## ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `ARTIFACT_ANALYSIS_2026-04-17.md` | Анализ артефактов |
| `LARGE_WORLD_SOLUTIONS.md` | Large world solutions |
| `SESSION_2026-04-17_FIXED.md` | Ранние исправления |
| `NEXT_SESSION_PROMPT.md` | Текущий prompt |

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 18:11 MSK
