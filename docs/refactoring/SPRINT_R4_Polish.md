# Sprint R4 — Polish & Prefabs

**Дата:** 2026-05-05
**Статус:** 📋 Planned
**Фокус:** UI prefabs, документация, финальный polish

---

## Goal

Завершить рефакторинг: создать UI prefabs вместо runtime creation, добавить null-checks, обновить документацию.

---

## Stories

### [R4-001] UI Prefabs для ControlHintsUI
**Story Points:** 3
**Owner:** @technical-artist
**Приоритет:** P2

**Описание:**
- Создать префаб ControlHintsUI.prefab
- Заменить runtime creation в ThirdPersonCamera, WorldCamera
- Добавить в сцену по умолчанию

**Критерии приёмки:**
- [ ] Prefab создан и настроен
- [ ] Runtime creation заменён на Instantiate(prefab)
- [ ] Prefab находится в Assets/_Project/Prefabs/UI/

---

### [R4-002] UI Prefabs для Inventory
**Story Points:** 3
**Owner:** @technical-artist
**Приоритет:** P2

**Описание:**
- Создать префаб InventoryPanel.prefab
- Интегрировать с UIManager

**Критерии приёмки:**
- [ ] Prefab создан
- [ ] InventoryUI использует prefab
- [ ] Визуально соответствует текущему GL rendering

---

### [R4-003] Null-checks в ShipController
**Story Points:** 2
**Owner:** @gameplay-programmer
**Приоритет:** P2

**Описание:**
- Добавить null-guards во все публичные методы
- Добавить early returns где нужно

**Критерии приёмки:**
- [ ] Все публичные методы имеют null-checks
- [ ] Нет NullReferenceException в логах
- [ ] Тест: корабль без FuelSystem работает

---

### [R4-004] Обновление документации
**Story Points:** 2
**Owner:** @unity-specialist
**Приоритет:** P2

**Описание:**
- Обновить docs/refactoring/REFACTOR_OVERVIEW.md с результатами
- Добавить профилирование "до/после"
- Обновить docs/STRUCTURE.md

**Критерии приёмки:**
- [ ] REFACTOR_OVERVIEW.md обновлён
- [ ] Профилирование записано
- [ ] STRUCTURE.md актуализирован

---

## Capacity

| Developer | Availability | Sprint commitment |
|-----------|--------------|-------------------|
| @technical-artist | 80% | 6 points |
| @gameplay-programmer | 40% | 2 points |
| @unity-specialist | 50% | 2 points |
| **Total** | | **10 points** |

---

## Definition of Done

- [ ] Все prefabs созданы и протестированы
- [ ] Документация обновлена
- [ ] Code review пройден
- [ ] Финальное профилирование записано