# Bug Reports — Project C

**Последнее обновление:** 12 апреля 2026

---

## 🟡 Актуальные баги

| # | Баг | Приоритет | Статус | Документ |
|---|-----|-----------|--------|----------|
| 1 | Shift+W конфликт с обычным thrust | P1 | Ожидает тестирования | [SESSION5_4_BUGS_AND_RISKS.md](SESSION5_4_BUGS_AND_RISKS.md) |
| 2 | MODULE_MEZIY_THRUST расход топлива (8 fuel/sec) | P2 | Балансировка после тестов | [SESSION5_4_BUGS_AND_RISKS.md](SESSION5_4_BUGS_AND_RISKS.md) |
| 3 | MeziyStatusHUD может не найти FuelSystem | P3 | Обработан (null check) | [SESSION5_4_BUGS_AND_RISKS.md](SESSION5_4_BUGS_AND_RISKS.md) |

---

## ✅ Закрытые баги

| # | Баг | Сессия | Решение |
|---|-----|--------|---------|
| 1 | 57 ошибок asmdef | 1 | Откачено к `d403073`, asmdef удалены |
| 2 | Корабль не летит вперёд | 5→5_2 | Добавлен `_currentThrust = Mathf.SmoothDamp()` |
| 3 | Fuel=0 не блокирует управление | 5→5_2 | `controlThreshold` увеличен до 10 |
| 4 | Частицы всегда видны | 5→5_2 | `Awake()` + `EnsureDeactivated()` |
| 5 | Roll force = 15 слишком мала | 5_2 | `rollForce = mass * 0.2f` |
| 6 | Мезиевые модули не активируются | 5→5_2→5_3 | Passive/active архитектура |
| 7 | Collection modified exception | 5_2 | Словарь не изменялся внутри foreach |
| 8 | MODULE_MEZIY_ROLL/YAW "not found" | 5_2→5_3 | Детальные логи при старте |
| 9 | Визуал сопел не виден | 5→5_2 | `AutoCreateParticles()`, CustomEditor |
| 10 | Z/C крен не работал | 5_2 | Roll force увеличена, клавиши переназначены |
| 11 | HUD не виден | 5_2 | Зелёный текст на чёрном фоне с рамкой |
| 12 | Debug спам | 5_2 | Убраны логи каждый кадр |
| 13 | ModuleSlot Inspector валидация | 4 | `OnValidate()` — работает |
| 14 | Кулдаун 0 сек | 5_4 | Forced 15s в Initialize() |

Закрытые баг-репорты перемещены → [`docs/Old_sessions/`](../Old_sessions/)

---

## 📊 Статистика

| Метрика | Значение |
|---------|----------|
| Всего багов | 14 |
| Исправлено | 14 (100%) |
| Критических | 2 (оба исправлены) |
| Ожидает тестирования | 2 |
