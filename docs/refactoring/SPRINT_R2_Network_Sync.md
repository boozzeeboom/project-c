# Sprint R2 — Network Sync Fix

**Дата:** 2026-04-21
**Статус:** 📋 Planned
**Фокус:** Сетевая синхронизация + унификация Input System

---

## Goal

Реализовать INetworkSerializable для ключевых классов и унифицировать Input System. Подготовить инфраструктуру для надёжного мультиплеера.

---

## Stories

### [R2-001] INetworkSerializable для Inventory ✅ IMPLEMENTED
**Story Points:** 5
**Owner:** @network-programmer
**Приоритет:** P1

**Описание:**
- Реализовать INetworkSerializable для Inventory
- Синхронизировать данные между клиентами
- Сохранять инвентарь на сервере для реконнекта

**Критерии приёмки:**
- [x] Inventory синхронизируется между клиентами
- [x] Реконнект восстанавливает инвентарь
- [x] Trade работает без рассинхронизации

**Задачи:**
- [x] Реализовать INetworkSerializable
- [x] Создать NetworkInventory компонент
- [x] Мигрировать SaveToPrefs на серверное сохранение

**Реализовано:**
- `InventoryData.cs` — структура с INetworkSerializable
- `NetworkInventory.cs` — обновлён для использования InventoryData
- Методы: PickupItemServerRpc, AddItem, Clear

---

### [R2-002] Унификация Input System в ShipController 🚧 IN PROGRESS
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
- [x] Создать ShipInputReader компонент
- [ ] Подключить ShipInputReader к ShipController
- [ ] Переписать IsKeyDown на события ShipInputReader
- [ ] Тест: все клавиши работают

**Созданные файлы:**
- `ShipInputReader.cs` — events для thrust, yaw, pitch, vertical, boost, meziy

---

### [R2-003] Унификация Input System в NetworkPlayer 🚧 IN PROGRESS
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
- [x] Создать PlayerInputReader компонент
- [ ] Подключить PlayerInputReader к NetworkPlayer
- [ ] Переписать Keyboard.current на события
- [ ] Тест: движение, прыжок, бег

**Созданные файлы:**
- `PlayerInputReader.cs` — events для move, jump, run, interact, mode switch

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