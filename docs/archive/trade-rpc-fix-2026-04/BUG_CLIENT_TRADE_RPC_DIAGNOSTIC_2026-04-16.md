# Баг: ClientRpc не доходит до клиента — Диагностика

**Дата:** 16.04.2026 | **Статус:** Требуется дополнительная диагностика

---

## Проблема

Клиент (clientId=1) покупает товар:
- Сервер обрабатывает покупку ✅
- Кредиты списываются на сервере ✅
- **ClientRpc НЕ доходит до клиента** ❌
- UI не обновляется ❌

---

## Все попытки исправления

### Попытка 1: ClientRpcParams с TargetClientIds
**Файл:** `NetworkPlayer.cs`, `TradeMarketServer.cs`
**Изменение:** Добавлен `ClientRpcParams` с `TargetClientIds = [clientId]`
**Результат:** ❌ RPC всё ещё не доходит

### Попытка 2: Поиск через ConnectedClients
**Файл:** `TradeMarketServer.cs`
**Изменение:** Использование `NetworkManager.Singleton.ConnectedClients` вместо `FindObjectsByType`
**Результат:** ❌ RPC всё ещё не доходит

### Попытка 3: Вызов через NetworkObject клиента
**Файл:** `TradeMarketServer.cs`
**Изменение:** Получение `playerNetObj` из `ConnectedClients`
**Результат:** ❌ RPC всё ещё не доходит

### Попытка 4: Fallback для локального клиента
**Файл:** `TradeMarketServer.cs`
**Изменение:** Если `clientId == nm.LocalClientId` — вызов `OnTradeResult` напрямую
**Результат:** ❌ Fallback не срабатывает (лог "Локальный клиент" не появляется)

---

## Анализ логов

### Логи ХОСТА (clientId=0):
```
[TMS] SendTradeResult: clientId=1, success=True, newCredits=614
[TMS] Отправляю TradeResultClientRpc клиенту 1 с TargetClientIds
[TMS] TradeResultClientRpc отправлен клиенту 1 через NetworkObject клиента
```

### Вывод:
- Хост отправляет RPC правильно
- Но на клиенте (clientId=1) **НЕТ лога `[NetworkPlayer] TradeResultClientRpc`**
- Это означает: RPC либо не отправляется, либо не доходит

---

## Возможные причины

### 1. Проблема маршрутизации на хосте
- `TradeMarketServer` находится на отдельном NetworkObject
- RPC вызывается через `NetworkPlayer` компонент
- Возможно, `ClientRpc` неправильно маршрутизируется на хосте

### 2. NetworkObject не синхронизирован
- `TradeMarketServer` зарегистрирован в сети?
- Префаб добавлен в `DefaultNetworkPrefabs.asset`?

### 3. TradeUI.Instance == null на клиенте
- `TradeUI` может не существовать на клиенте
- Или `OnTradeResult` не обновляет UI правильно

### 4. PlayerTradeStorage не синхронизирован
- Данные хранятся локально
- Нет синхронизации между сервером и клиентом

---

## Следующие шаги диагностики

### Шаг 1: Проверить NetworkObject TradeMarketServer
- Запустить в Network Manager Debug
- Проверить что `TradeMarketServer` имеет `NetworkObject`
- Проверить что `TradeMarketServer` спавнится на сервере

### Шаг 2: Добавить логи на клиенте ✅ ВЫПОЛНЕНО
- Проверить получает ли клиент **любые** RPC
- Добавлен лог в начало `TradeResultClientRpc`

### Шаг 3: Проверить TradeUI ✅ ВЫПОЛНЕНО
- Существует ли `TradeUI.Instance` на клиенте?
- Вызывается ли `OnTradeResult`?

### Шаг 4: Создать принудительный UI склада ✅ ВЫПОЛНЕНО
- Создан `TradeDebugTools.cs`
- UI всегда отображается справа на экране
- Не зависит от TradeUI
- Обновляется каждые 0.5 сек
- F3 = toggle visibility

### Шаг 5: Добавить ForceRefresh в RPC ✅ ВЫПОЛНЕНО
- При получении `TradeResultClientRpc` вызывается `TradeDebugTools.Instance.ForceRefresh()`
- Это обновит UI напрямую из PlayerDataStore

---

## Код для проверки

```csharp
// В TradeResultClientRpc - добавить в начало
[ClientRpc]
public void TradeResultClientRpc(...)
{
    Debug.Log($"[NetworkPlayer] TradeResultClientRpc ВЫЗВАН! targetClientId={targetClientId}");
    // ...
}

// В TradeMarketServer - проверить существует ли TradeUI
if (TradeUI.Instance == null)
{
    Debug.LogError($"[TMS] TradeUI.Instance == null! UI не существует!");
}
```

---

## Связанные файлы

- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — RPC методы
- `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` — серверная логика
- `Assets/_Project/Trade/Scripts/TradeUI.cs` — клиентский UI
- `Assets/_Project/Scripts/Core/PlayerTradeStorage.cs` — хранение данных
