# Large Scale MMO — Combined Session Prompts

**Проект:** ProjectC_client  
**Дата:** 18.04.2026  
**Версия:** `v0.0.18-combined-sessions`

---

## 📁 Описание

Это каталог с промптами для запуска каждой итерации. Каждый файл — полный промпт для запуска сессии с:
- Целью итерации
- Exact кодом для исправления
- Шагами выполнения
- Критериями тестирования
- Ожидаемыми результатами

---

## 📋 Итерации

| # | Файл | Цель | Длительность | Статус |
|---|------|------|--------------|--------|
| 1 | `ITERATION_1_SESSION.md` | Fix FloatingOriginMP Jitter & Integration | 1-2 сессии | ✅ Завершено (jitter отложен) |
| 2 | `ITERATION_2_SESSION.md` | Fix WorldStreamingManager Integration | 1 сессия | ✅ Завершено и протестировано |
| 3 | `ITERATION_3_SESSION.md` | Fix PlayerChunkTracker Integration | 1-2 сессии | ⏳ После Iter 2 |
| 4 | `ITERATION_4_SESSION.md` | Setup & Test | 1-2 сессии | ⏳ После Iter 3 |
| 5 | `ITERATION_5_SESSION.md` | Multiplayer Test | 1-2 сессии | ⏳ После Iter 4 |

---

## 🎯 Быстрый старт

### Хочешь начать новую итерацию?
1. Выбрать нужную итерацию из таблицы выше
2. Открыть соответствующий файл
3. Следовать шагам выполнения
4. Проверить критерии приёмки

### После завершения итерации:
1. Обновить `docs/world/LargeScaleMMO/CURRENT_STATE.md`
2. Переместить результаты в `docs/world/LargeScaleMMO/old_sessions/`
3. Перейти к следующей итерации

---

## 🔄 Flow диаграмма

```
┌─────────────────┐
│ Start Session   │
└────────┬────────┘
         ▼
┌─────────────────┐
│ Read Iteration   │
│ Session Prompt   │
└────────┬────────┘
         ▼
┌─────────────────┐
│ Follow Steps    │
│ Apply Fixes     │
└────────┬────────┘
         ▼
┌─────────────────┐
│ Test Criteria   │
│ (Acceptance)    │
└────────┬────────┘
         │
    ┌────┴────┐
    │ Pass?   │
    └────┬────┘
    Yes │    No
    ┌───┘    └──┐
    ▼           ▼
┌─────────┐  ┌─────────────────┐
│ Next    │  │ Fix Issues      │
│ Iter?   │  │ Repeat Tests    │
└────┬────┘  └─────────────────┘
     │
  ┌──┴──┐
  │ End │
  └─────┘
```

---

## 📊 Связь с другими документами

| Документ | Назначение |
|----------|------------|
| `docs/world/LargeScaleMMO/CURRENT_STATE.md` | Глубокий анализ проблем |
| `docs/world/LargeScaleMMO/ITERATION_PLAN.md` | Общий план итераций |
| `docs/world/LargeScaleMMO/01_Architecture_Plan.md` | Архитектура системы |
| `combinesessions/ITERATION_*_SESSION.md` | Промпты для сессий |

---

## ⚠️ Важно

**Порядок выполнения:**
1. Iteration 1 (FloatingOriginMP) → ДОЛЖЕН быть первым
2. Iteration 2 (WorldStreamingManager) → После Iter 1
3. Iteration 3 (PlayerChunkTracker) → После Iter 2
4. Iteration 4 (Setup & Test) → После Iter 3
5. Iteration 5 (Multiplayer Test) → После Iter 4

**Не пропускайте итерации!** Каждая зависит от предыдущей.

---

## 🗂️ Структура каталога

```
combinesessions/
├── INDEX.md                    ← Этот файл
├── ITERATION_1_SESSION.md     ← FloatingOriginMP Jitter Fix
├── ITERATION_2_SESSION.md     ← WorldStreamingManager Integration
├── ITERATION_3_SESSION.md     ← PlayerChunkTracker Integration
├── ITERATION_4_SESSION.md     ← Setup & Test
└── ITERATION_5_SESSION.md     ← Multiplayer Test
```

---

**Автор:** Claude Code  
**Дата:** 18.04.2026