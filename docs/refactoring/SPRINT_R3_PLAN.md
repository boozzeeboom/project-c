# SPRINT R3 — ПЛАН ВЫПОЛНЕНИЯ

**Дата:** 2026-04-15
**Статус:** 🚧 IN PROGRESS
**Фокус:** Architecture Cleanup + R2 Bug Fixes

---

## ЗАДАЧИ R3 (Architecture)

### [R3-001] Убрать Reflection из NetworkPlayer
**SP:** 2 | **Owner:** @gameplay-programmer

**Файлы:** `NetworkPlayer.cs:182`

**Текущий код (плохо):**
```csharp
var invField = typeof(InventoryUI).GetField("inventory", 
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
if (invField != null)
    invField.SetValue(_inventoryUI, _inventory);
```

**Решение:** Добавить публичный метод `SetInventory()` в InventoryUI:
```csharp
public void SetInventory(Inventory inv) => inventory = inv;
```

**Критерии:**
- [ ] Ноль reflection в NetworkPlayer
- [ ] InventoryUI.SetInventory() работает корректно
- [ ] Компиляция без warnings

---

### [R3-002] Async reconnect в NetworkManagerController
**SP:** 3 | **Owner:** @network-programmer

**Файлы:** `NetworkManagerController.cs:198, 223, 257`

**Текущий код (плохо):**
```csharp
System.Threading.Thread.Sleep(250); // Блокирует UI thread!
```

**Решение:** Заменить на async/await с Task.Delay:
```csharp
await Task.Delay(250); // Неблокирующий
```

**Критерии:**
- [ ] Ноль Thread.Sleep в коде
- [ ] Reconnect работает корректно
- [ ] UI не блокируется

---

### [R3-003] Удалить мёртвый код в FloatingOriginMP
**SP:** 1 | **Owner:** @unity-specialist

**Файлы:** `FloatingOriginMP.cs:305-334`

**Текущий код (мёртвый):**
```csharp
private void CollectWorldObjects(Transform worldRoot) { ... }
```

**Решение:** Удалить метод полностью (больше не используется)

**Критерии:**
- [ ] CollectWorldObjects() удалён
- [ ] FloatingOriginMP работает корректно
- [ ] Компиляция без warnings

---

## R2 BUG FIXES (Must Fix Before R3 Complete)

### [R2-BUG-001] Клиент не может брать контракты (C)
**Priority:** 🔴 P1

**Описание:** На хосте клавиша C открывает интерфейс контрактов. На клиенте — нет.

**Анализ:**
- RPC `ContractRequestServerRpc` отправляется с клиента на сервер
- Проверить: есть ли проверка `RequireOwnership = false`
- Проверить: правильно ли вызывается RPC с клиента

**Файлы для проверки:**
- `NetworkPlayer.cs` — обработка клавиши C
- `ContractSystem.cs` — логика

---

### [R2-BUG-002] Клиент не может покупать на рынке (E)
**Priority:** 🔴 P1

**Описание:** UI рынка открывается, кнопки жмутся, но покупка не осуществляется.

**Анализ:**
- RPC `TradeBuyServerRpc` требует серверной авторизации
- Проверить: есть ли `[Rpc(SendTo.Server)]` атрибут
- Проверить: работает ли серверная валидация

**Файлы для проверки:**
- `TradeUI.cs` — UI рынка
- `TradeMarketServer.cs` — серверная логика

---

### [R2-BUG-003] UI инвентаря с наложениями
**Priority:** 🟡 P2

**Описание:** Inventory UI отображается некорректно — наложения элементов.

**Анализ:**
- Проверить: создаётся ли несколько инстансов InventoryUI
- Проверить: синхронизация состояния UI

**Файлы для проверки:**
- `InventoryUI.cs` — логика UI
- `NetworkPlayer.cs:180-185` — спавн UI

---

## CAPACITY

| Developer | Availability | Sprint commitment |
|-----------|--------------|-------------------|
| @gameplay-programmer | 80% | 5 SP |
| @network-programmer | 70% | 3 SP |
| @unity-specialist | 60% | 1 SP |
| **Total** | | **9 SP** |

---

## DEFINITION OF DONE

- [ ] Ноль reflection в production коде (кроме Editor tools)
- [ ] Ноль Thread.Sleep в production коде
- [ ] R2-001 контракты работают на клиенте
- [ ] R2-002 рынок работает на клиенте
- [ ] R2-003 UI инвентаря без наложений
- [ ] Code review пройден
- [ ] Все тесты пройдены
