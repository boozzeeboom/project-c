# Sprint R3 — Architecture Cleanup

**Дата:** 2026-04-28
**Статус:** 📋 Planned
**Фокус:** Чистка архитектуры, устранение технического долга

---

## Goal

Устранить архитектурные проблемы: Reflection для приватных полей, синхронный Thread.Sleep, мёртвый код. Подготовить код к долгосрочной поддержке.

---

## Stories

### [R3-001] Убрать Reflection из NetworkPlayer
**Story Points:** 2
**Owner:** @gameplay-programmer
**Приоритет:** P1

**Описание:**
- Заменить reflection-код для доступа к _inventoryUI на публичный сеттер
- Добавить proper property с validation

**Критерии приёмки:**
- [ ] Ноль reflection в NetworkPlayer
- [ ] InventoryUI.SetInventory() работает корректно
- [ ] Компиляция без warnings

---

### [R3-002] Async reconnect в NetworkManagerController
**Story Points:** 3
**Owner:** @network-programmer
**Приоритет:** P1

**Описание:**
- Заменить Thread.Sleep(250) на async/await
- Использовать CancellationToken для graceful shutdown

**Критерии приёмки:**
- [ ] Ноль Thread.Sleep в коде
- [ ] Reconnect работает корректно
- [ ] UI не блокируется

---

### [R3-003] Удалить мёртвый код в FloatingOriginMP
**Story Points:** 1
**Owner:** @unity-specialist
**Приоритет:** P1

**Описание:**
- Удалить метод CollectWorldObjects() и связанный код
- Очистить закомментированные секции

**Критерии приёмки:**
- [ ] CollectWorldObjects() удалён
- [ ] FloatingOriginMP работает корректно
- [ ] Тест: Far clip plane > 100,000 units

---

### [R3-004] Рефакторинг ShipController class presets
**Story Points:** 3
**Owner:** @gameplay-programmer
**Приоритет:** P2

**Описание:**
- Вынести magic numbers из ApplyShipClass() в ScriptableObject настройки
- Создать ShipFlightClassConfig asset

**Критерии приёмки:**
- [ ] ShipFlightClassConfig создаётся в Inspector
- [ ] Presets работают для всех 4 классов
- [ ] Hot reload работает

---

## Capacity

| Developer | Availability | Sprint commitment |
|-----------|--------------|-------------------|
| @network-programmer | 70% | 3 points |
| @gameplay-programmer | 80% | 5 points |
| @unity-specialist | 60% | 1 point |
| **Total** | | **9 points** |

---

## Definition of Done

- [ ] Ноль reflection в production коде
- [ ] Ноль Thread.Sleep в production коде
- [ ] Code review пройден
- [ ] Все тесты пройдены