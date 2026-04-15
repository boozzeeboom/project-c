# Сессия: Исправление Trade RPC — ClientRpcParams

**Дата:** 15.04.2026 | **Продолжительность:** ~2 часа | **Статус:** Готово к имплементации

---

## Цель сессии

Исправить баг `BUG_CLIENT_TRADE_NOT_WORKING`: клиент (`clientId=1`) не получает результаты торговли от хоста.

---

## Предыстория (из docs/bugs/BUG_CLIENT_TRADE_NOT_WORKING_2026-04-15.md)

### Проблема
- Клиент может покупать и продавать ✓
- Сервер корректно обрабатывает операции ✓
- **Клиент НЕ получает результаты торговли** ✗ — склад остаётся пустым, кредиты не обновляются

### Все попытки (8I-8L) — НЕ РЕШИЛИ ПРОБЛЕМУ
- 8I: Упрощение ClientRpc с IsOwner — работает на хосте, НЕ работает для клиента
- 8J: FindObjectsByType с IsOwner — клиент вызывает RPC от своего имени, но сервер отправляет НЕ тому
- 8K: ConnectedClients для поиска — НЕ РЕШЕНО

### Корневая причина
```
NGO ClientRpc с SendTo.Owner на хосте ДУБЛИРУЕТСЯ на все NetworkPlayer с IsOwner=True.
На хосте только NetworkPlayer(0) имеет IsOwner=True, поэтому:
- RPC для clientId=1 отправляется на NetworkPlayer(0) (локальный)
- IsOwner на NetworkPlayer(0) = True (для clientId=0)
- Но сервер хочет отправить результат клиенту с clientId=1
```

---

## Подготовка к сессии

### 1. Изучить документацию
Прочитать в порядке приоритета:
1. `docs/TRADE_RPC_FIX.md` — готовое решение для копирования
2. `docs/TRADE_MARKET_ARCHITECTURE.md` — полная архитектура
3. `docs/bugs/BUG_CLIENT_TRADE_NOT_WORKING_2026-04-15.md` — история бага

### 2. Проверить окружение
```bash
cd c:\UNITY_PROJECTS\ProjectC_client
git status
```
Убедиться что нет несохранённых изменений.

---

## План работы (по шагам)

### Шаг 1: Бэкап
```bash
git checkout -b fix/trade-rpc-client-params
```

### Шаг 2: Изменить NetworkPlayer.cs (строка ~526-548)

**БЫЛО:**
```csharp
[ClientRpc]
public void TradeResultClientRpc(ulong targetClientId, bool success, string message, 
    float newCredits, string itemId = "", int itemQuantity = 0, bool isPurchase = true)
```

**СТАТЬ:**
```csharp
[ClientRpc]
public void TradeResultClientRpc(ulong targetClientId, bool success, string message, 
    float newCredits, string itemId = "", int itemQuantity = 0, bool isPurchase = true,
    ClientRpcParams rpcParams = default)
```

### Шаг 3: Изменить TradeMarketServer.cs (строка ~558-617)

**БЫЛО:**
```csharp
player.TradeResultClientRpc(clientId, success, message, newCredits, itemId, itemQuantity, isPurchase);
```

**СТАТЬ:**
```csharp
var rpcParams = new ClientRpcParams
{
    Send = new ClientRpcSendParams
    {
        TargetClientIds = new ulong[] { clientId }
    }
};
player.TradeResultClientRpc(clientId, success, message, newCredits, 
    itemId, itemQuantity, isPurchase, rpcParams);
```

### Шаг 4: Тестирование

1. **Запустить Host** в Unity Editor
2. **Запустить Client** в отдельном Unity Editor или билде
3. Host открывает TradeUI (рынок)
4. Client открывает TradeUI
5. Client покупает товар
6. **Проверка:** 
   - Склад клиента обновился?
   - Кредиты клиента уменьшились?
   - В логах: `[TradeUI] OnTradeResult: success=True`

---

## Ожидаемые логи после исправления

```
[TMS] SendTradeResult: clientId=1, success=True, newCredits=990
[TMS] FindPlayerNetworkPlayer(1): найден через ConnectedClients, OwnerClientId=1
[TMS] Отправляю TradeResultClientRpc клиенту 1 с TargetClientIds
[NetworkPlayer] TradeResultClientRpc: targetClientId=1, localClientId=1, success=True
[NetworkPlayer] Вызываю OnTradeResult для клиента 1
[TradeUI] OnTradeResult: success=True, credits=990, itemId=mesium_canister_v01, qty=1
```

---

## Файлы для изменения

| Файл | Строки | Изменение |
|------|--------|-----------|
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | ~526-548 | Добавить `ClientRpcParams rpcParams = default` |
| `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` | ~558-617 | Добавить `ClientRpcParams` с `TargetClientIds` |

---

## Критерии успеха

- [ ] Host может покупать/продавать (работало и до фикса)
- [ ] Client получает результаты покупки (склад обновляется)
- [ ] Client получает результаты продажи (кредиты увеличиваются)
- [ ] Логи соответствуют ожидаемым
- [ ] Нет регрессий в других RPC

---

## Fallback (если ClientRpcParams не работает)

Альтернативное решение — вызывать `OnTradeResult` напрямую если `clientId == LocalClientId`:

```csharp
// В SendTradeResultToClient
if (nm.IsHost && clientId == nm.LocalClientId)
{
    Debug.Log($"[TMS] Локальный клиент — вызываю напрямую");
    TradeUI.Instance?.OnTradeResult(success, message, newCredits, itemId, itemQuantity, isPurchase);
    return;
}
```

---

## После успешного фикса

1. Зафиксировать изменения:
   ```bash
   git add .
   git commit -m "fix: ClientRpcParams for TradeResultClientRpc
   - Используем TargetClientIds для надёжной доставки на хосте
   - Фикс BUG_CLIENT_TRADE_NOT_WORKING"
   ```

2. Обновить документацию:
   - Добавить запись в `CHANGELOG_TRADE_2026-04-15.md`
   - Закрыть баг в `BUG_CLIENT_TRADE_NOT_WORKING_2026-04-15.md`

3. Провести регрессионное тестирование:
   - Покупка/продажа на хосте
   - Покупка/продажа на клиенте
   - Контракты (ContractSystem)
   - Погрузка/разгрузка корабля

---

## Связанные файлы

- `docs/TRADE_RPC_FIX.md` — Готовый код для копирования
- `docs/TRADE_MARKET_ARCHITECTURE.md` — Полная архитектура
- `docs/bugs/BUG_CLIENT_TRADE_NOT_WORKING_2026-04-15.md` — История бага
- `docs/NETWORK_ARCHITECTURE.md` — Сетевая архитектура
