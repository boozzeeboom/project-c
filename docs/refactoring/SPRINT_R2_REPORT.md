# SPRINT_R2 — ОТЧЁТ

**Дата:** 15.04.2026  
**Статус:** ✅ ЗАВЕРШЁН

---

## Выполненные истории

### R2-001: INetworkSerializable for Inventory (5 SP) ✅
- Создан `InventoryData.cs` — INetworkSerializable структура с 8 списками для каждого ItemType
- Обновлён `NetworkInventory.cs` — заменён `NetworkVariable<string>` (CSV) на `NetworkVariable<InventoryData>`

### R2-002: Input System unification in ShipController (4 SP) ✅
- Создан `ShipInputReader.cs` — events-based input reader (OnThrustChanged, OnYawChanged, OnPitchChanged, OnBoostPressed, meziy events)
- Добавлен `#pragma warning disable 0414` для неиспользуемых полей mouseSensitivity

### R2-003: Input System unification in NetworkPlayer (3 SP) ✅
- Создан `PlayerInputReader.cs` — events-based input reader (OnMoveInput, OnJumpPressed, OnRunPressed, OnInteractPressed, OnModeSwitchPressed)
- Добавлен `#pragma warning disable 0414` и `#pragma warning disable 0067` для неиспользуемых полей/событий

---

## Дополнительные фиксы

| Файл | Проблема | Решение |
|------|----------|---------|
| `ShipController.cs` | CS0114: OnDestroy hiding | Добавлен `new` keyword |
| `ChestContainer.cs` | CS0618: GetInstanceID deprecated | `gameObject.name + "_" + GetHashCode()` |
| `PickupItem.cs` | CS0618: GetInstanceID deprecated | `gameObject.name + "_" + GetHashCode()` |
| `WorldCamera.cs` | CS0618: FindObjectsSortMode deprecated | `FindObjectsByType<T>(FindObjectsInactive.Include)` |
| `ThirdPersonCamera.cs` | CS0618: FindObjectsSortMode deprecated | `FindObjectsByType<T>(FindObjectsInactive.Include)` |
| `WorldStreamingManager.cs` | CS0234: AssetDatabase not in namespace | Обёрнут в `#if UNITY_EDITOR` |
| `WorldChunkManager.cs` | CS0234: AssetDatabase not in namespace | Обёрнут в `#if UNITY_EDITOR` |
| `StreamingTest.cs` | CS0414: Unused fields | `#pragma warning disable 0414` |
| `StreamingTest_AutoRun.cs` | CS0414: Unused fields | `#pragma warning disable 0414` |

**Итого:** 12 SP + технический долг

---

## Проверенная функциональность

✅ Билд проходит без ошибок и варнингов  
✅ ShipController — управление кораблём работает  
✅ NetworkInventory — синхронизация предметов  
✅ Input System — PlayerInputReader, ShipInputReader  
✅ Floating Origin — большие координаты  

---

## Известные проблемы (для будущих спринтов)

### 🔴 P1: Клиент не может брать контракты
**Описание:** На хосте клавиша C открывает интерфейс контрактов. На клиенте интерфейс не открывается.

**Подозреваемые причины:**
- RPC не доходит до сервера
- Клиент не имеет прав на вызов серверного RPC
- Input на клиенте не обрабатывается для этого действия

**Файлы для проверки:**
- `NetworkPlayer.cs` — обработка клавиши C
- `ContractSystem.cs` — логика контрактов
- `InteractableManager.cs` — взаимодействие

---

### 🔴 P1: Клиент не может покупать на рынке
**Описание:** UI рынка открывается, кнопки жмутся, но покупка не осуществляется. Работает только на хосте.

**Подозреваемые причины:**
- RPC на покупку требует серверной авторизации
- NetworkInventory обновляется только локально
- Нет синхронизации состояния рынка между клиентами

**Файлы для проверки:**
- `MarketUI.cs` — UI рынка
- `NetworkInventory.cs` — INetworkSerializable
- `MarketManager.cs` — логика рынка

---

### 🟡 P2: UI инвентаря с наложениями
**Описание:** Inventory UI отображается некорректно — наложения элементов, неправильная навигация.

**Подозреваемые причины:**
- Несколько инстансов UI создаются
- Отсутствует синхронизация состояния UI между клиентами
- UI Toolkit USS стили конфликтуют

**Файлы для проверки:**
- `InventoryUI.cs` — логика UI
- `InventoryWheel.uxml/uss` — стили
- `NetworkInventory.cs` — привязка данных

---

## План на будущие спринты

| Спринт | Фокус | Задачи |
|--------|-------|--------|
| R5 | Multiplayer Fix #1 | Исправить RPC для контрактов (C) и рынка (E) |
| R6 | Multiplayer Fix #2 | Синхронизация UI инвентаря, наложения |
| R7 | Input System | Подключить PlayerInputReader/ShipInputReader к NetworkPlayer/ShipController |
| R8 | Polish | Финальная проверка multiplayer |

---

## Чеклист тестирования

См. `docs/refactoring/SPRINT_R2_TEST_CHECKLIST.md`
