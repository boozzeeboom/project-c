# Sprint R1 — Performance Hotfix

**Дата:** 2026-04-15
**Статус:** ✅ Completed (2026-04-15)
**Фокус:** Устранение allocations в hot paths + кэширование Find* операций

---

## 📊 Отчёт о выполнении

| Story | Название | Статус | Notes |
|-------|----------|--------|-------|
| R1-001 | Pre-allocate caches для NetworkPlayer | ✅ DONE | IInteractable + InteractableManager |
| R1-002 | Кэширование компонентов в ThirdPersonCamera | ✅ DONE | Кэширование UI при старте |
| R1-003 | Кэширование worldGenerator в WorldCamera | ✅ DONE | Кэширование WorldRoot/UI |
| R1-004 | Pre-allocate GL material в InventoryUI | ✅ DONE | Material в Awake |
| Stretch | ShipController.FixedUpdate() profiling | ✅ DONE | Добавлен trigger registration |
| Stretch | Кэшировать _rb проверки в ShipController | ✅ DONE | Awake инициализация |

**Результат:** 9/9 points completed + 2 stretch goals

---

## Тестирование

| Функция | Результат | Комментарий |
|---------|-----------|-------------|
| Подбор предметов (E) | ✅ PASS | PickupItem trigger работает |
| Открытие сундука (E) | ✅ PASS | ChestContainer trigger работает |
| Посадка в корабль (E) | ✅ PASS | ShipController trigger + InteractableManager |

---

---

## Goal

Устранить критические проблемы производительности, выявленные Code Review. Hot paths (Update, FixedUpdate, LateUpdate) должны работать без allocations.

---

## Stories

### [R1-001] Pre-allocate caches для NetworkPlayer
**Story Points:** 3
**Owner:** @gameplay-programmer
**Приоритет:** P0

**Описание:**
- Создать кэшированные списки для `_nearestPickup`, `_nearestChest`, `_nearestShip`
- Реализовать trigger-based обновление вместо FindObjectsByType каждый кадр
- Добавить IInteractable interface

**Критерии приёмки:**
- [x] Ноль allocations в NetworkPlayer.Update()
- [x] Профилирование: Update() < 0.5ms
- [x] Тесты в редакторе: подбор предметов работает

**Задачи:**
- [x] Создать IInteractable interface (`Assets/_Project/Scripts/Core/IInteractable.cs`)
- [x] Добавить OnTriggerEnter/Exit на PickupItem, ChestContainer, ShipController
- [x] Кэшировать список interactables в NetworkPlayer
- [x] Профилировать до/после

**Файлы изменены:**
- `Assets/_Project/Scripts/Core/IInteractable.cs` (new)
- `Assets/_Project/Scripts/Core/InteractableManager.cs` (new)
- `Assets/_Project/Scripts/Core/PickupItem.cs` (modified)
- `Assets/_Project/Scripts/Core/ChestContainer.cs` (modified)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (modified)
- `Assets/_Project/Scripts/Player/ShipController.cs` (modified)

---

### [R1-002] Кэширование компонентов в ThirdPersonCamera
**Story Points:** 2
**Owner:** @unity-specialist
**Приоритет:** P0

**Описание:**
- Убрать FindAnyObjectByType из CreateControlHintsUI()
- Кэшировать ссылки при старте

**Критерии приёмки:**
- [x] Ноль Find* вызовов в CreateControlHintsUI()
- [x] UI подсказок создаётся корректно

**Задачи:**
- [x] Кэшировать ControlHintsUI в Awake/Start
- [x] Проверить singleton паттерн

**Файлы изменены:**
- `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs` (modified)

---

### [R1-003] Кэширование worldGenerator в WorldCamera
**Story Points:** 2
**Owner:** @unity-specialist
**Приоритет:** P0

**Описание:**
- Убрать FindObjectsByType из FindWorldRoot()
- Кэшировать ссылку на worldGenerator

**Критерии приёмки:**
- [x] Ноль allocations в WorldCamera.Start()
- [x] Телепортация к пикам работает

**Задачи:**
- [x] Кэшировать worldGenerator в Start()
- [x] Проверить LateUpdate на allocations

**Файлы изменены:**
- `Assets/_Project/Scripts/Core/WorldCamera.cs` (modified)

---

### [R1-004] Pre-allocate GL material в InventoryUI
**Story Points:** 1
**Owner:** @gameplay-programmer
**Приоритет:** P0

**Описание:**
- Создать Material один раз в Awake, не в OnGUI()
- Добавить proper cleanup

**Критерии приёмки:**
- [x] Material создаётся один раз
- [x] Ноль allocations в OnGUI()
- [x] Cleanup работает в OnDestroy()

**Файлы изменены:**
- `Assets/_Project/Scripts/UI/InventoryUI.cs` (modified)

---

## Capacity

| Developer | Availability | Sprint commitment |
|-----------|--------------|-------------------|
| @gameplay-programmer | 80% | 5 points |
| @unity-specialist | 60% | 4 points |
| **Total** | | **9 points** |

---

## Stretch Goals (если останется время)

- [x] Профилировать ShipController.FixedUpdate() на allocations
- [x] Кэшировать _rb проверки в ShipController

---

## Definition of Done

- [x] Все hot paths проверены профайлером
- [x] Zero allocations подтверждены
- [x] Code review пройден (@network-programmer)
- [x] Тесты в редакторе пройдены (Play mode, 5+ минут)
- [x] Документация обновлена

---

## Изменённые файлы (сводка)

| Файл | Тип | Описание |
|------|-----|----------|
| `Core/IInteractable.cs` | NEW | Интерфейс для интерактивных объектов |
| `Core/InteractableManager.cs` | NEW | Статический менеджер с кэшем |
| `Core/PickupItem.cs` | MODIFIED | Trigger registration |
| `Core/ChestContainer.cs` | MODIFIED | Trigger registration |
| `Core/ThirdPersonCamera.cs` | MODIFIED | Кэширование UI |
| `Core/WorldCamera.cs` | MODIFIED | Кэширование WorldRoot |
| `Player/NetworkPlayer.cs` | MODIFIED | InteractableManager вместо FindObjectsByType |
| `Player/ShipController.cs` | MODIFIED | Trigger registration + Awake инициализация |
| `UI/InventoryUI.cs` | MODIFIED | Material в Awake |
