# Sprint R2 — Network Sync Fix

**Дата:** 2026-04-21
**Статус:** 📋 Planned
**Фокус:** Сетевая синхронизация + унификация Input System

---

## Goal

Реализовать INetworkSerializable для ключевых классов и унифицировать Input System. Подготовить инфраструктуру для надёжного мультиплеера.

---

## Stories

### [R2-001] INetworkSerializable для Inventory
**Story Points:** 5
**Owner:** @network-programmer
**Приоритет:** P1

**Описание:**
- Реализовать INetworkSerializable для Inventory
- Синхронизировать данные между клиентами
- Сохранять инвентарь на сервере для реконнекта

**Критерии приёмки:**
- [ ] Inventory синхронизируется между клиентами
- [ ] Реконнект восстанавливает инвентарь
- [ ] Trade работает без рассинхронизации

**Задачи:**
- [ ] Реализовать INetworkSerializable
- [ ] Создать NetworkInventory компонент
- [ ] Мигрировать SaveToPrefs на серверное сохранение
- [ ] Тест: 2 клиента, обмен предметами

---

### [R2-002] Унификация Input System в ShipController
**Story Points:** 4
**Owner:** @gameplay-programmer
**Приоритет:** P1

**Описание:**
- Заменить IsKeyDown(KeyCode) на Input Actions
- Использовать InputAction для всех клавиш (A-Z, Shift, etc.)

**Критерии приёмки:**
- [ ] Все клавиши работают через Input Actions
- [ ] Нет mixed Input System + KeyCode
- [ ] Работает в editor и build

**Задачи:**
- [ ] Создать ShipInputActions asset
- [ ] Переписать IsKeyDown на InputAction listeners
- [ ] Удалить KeyCodeToKey маппинг
- [ ] Тест: все клавиши работают

---

### [R2-003] Унификация Input System в NetworkPlayer
**Story Points:** 3
**Owner:** @gameplay-programmer
**Приритет:** P1

**Описание:**
- Заменить Keyboard.current на Input Actions
- Вынести ввода в отдельный компонент InputReader

**Критерии приёмки:**
- [ ] WASD, Space, Shift работают через Input Actions
- [ ] Нет Keyboard.current в Update()

**Задачи:**
- [ ] Создать PlayerInputActions asset
- [ ] Создать PlayerInputReader компонент
- [ ] Подключить к NetworkPlayer
- [ ] Тест: движение, прыжок, бег

---

## Capacity

| Developer | Availability | Sprint commitment |
|-----------|--------------|-------------------|
| @network-programmer | 70% | 5 points |
| @gameplay-programmer | 60% | 4 points |
| **Total** | | **9 points** |

---

## Stretch Goals

- [ ] NetworkVariable для позиции игрока (дополнительно к RPC)
- [ ] Client-side prediction улучшение

---

## Definition of Done

- [ ] INetworkSerializable реализован и протестирован
- [ ] Input Actions работают для всех вводов
- [ ] Code review пройден
- [ ] Multiplayer тест пройден (2+ клиента)