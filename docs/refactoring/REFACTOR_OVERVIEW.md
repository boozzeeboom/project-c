# Refactoring Overview — Project C: The Clouds

Дата: 2026-04-15
Основание: Code Review (2026-04-15)

---

## Проблемы по приоритетам

### 🔴 P0 — Critical (Must-Fix)

| ID | Файл | Проблема | Решение |
|----|------|----------|---------|
| R-001 | Все Update() loops | Allocations в hot paths | Pre-allocate, ObjectPool |
| R-002 | NetworkPlayer.cs:358 | FindObjectsByType в Update() | Кэширование + Trigger |
| R-003 | NetworkPlayer.cs:403,418 | FindObjectsByType в Update() | Кэширование + Trigger |
| R-004 | WorldCamera.cs:243 | FindObjectsByType в hot path | Кэширование |
| R-005 | ThirdPersonCamera.cs:196 | FindAnyObjectByType в методе | Убрать или кэшировать |
| R-006 | ShipController.cs:209 | FindAnyObjectByType в Awake | Оставить (1 раз), но документировать |

### 🟡 P1 — Should-Fix

| ID | Файл | Проблема | Решение |
|----|------|----------|---------|
| R-101 | Inventory.cs | Нет INetworkSerializable | Реализовать интерфейс |
| R-102 | NetworkPlayer.cs:182 | Reflection для _inventoryUI | Сделать публичный сеттер |
| R-103 | NetworkManagerController.cs | Thread.Sleep(250) | Использовать async/await |
| R-104 | All files | Mixed Input System + KeyCode | Унифицировать Input Actions |
| R-105 | FloatingOriginMP.cs | CollectWorldObjects мёртвый код | Удалить |

### 🔵 P2 — Nice-to-Have

| ID | Файл | Проблема | Решение |
|----|------|----------|---------|
| R-201 | ShipController.cs | Magic numbers | Вынести в ScriptableObject |
| R-202 | All UI files | Runtime UI creation | Использовать префабы |
| R-203 | ShipController.cs | Missing null-checks | Добавить null guards |

---

## Спринты

| Спринт | Фокус | Приоритеты |
|--------|-------|------------|
| Sprint R1 | Performance Hotfix | R-001 → R-006 |
| Sprint R2 | Network Sync Fix | R-101, R-104 |
| Sprint R3 | Architecture Cleanup | R-102, R-103, R-105 |
| Sprint R4 | Polish & Prefabs | R-201 → R-203 |

---

## Owner matrix

| Owner |擅长领域| Спринты |
|-------|--------|---------|
| @gameplay-programmer | Player, Inventory | R1, R2 |
| @network-programmer | Network, Sync | R2 |
| @unity-specialist | Engine, Architecture | R3, R4 |
| @technical-artist | UI, VFX | R4 |

---

## Definition of Done для рефакторинга

- [ ] Профилирование "до" записано
- [ ] Профилирование "после" записано
- [ ] Zero allocations в hot paths (подтверждено профайлером)
- [ ] Code review пройден
- [ ] Тесты в редакторе пройдены
- [ ] Документация обновлена