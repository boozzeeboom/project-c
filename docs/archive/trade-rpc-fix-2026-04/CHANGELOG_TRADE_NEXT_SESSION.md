# Инструкция для Следующей Сессии: Анализ NGO ClientRpc

**Дата:** 2026-04-15  
**Цель:** Решить проблему с отображением товаров в TradeUI на хосте

---

## 📋 КРАТКОЕ ИЗЛОЖЕНИЕ ПРОБЛЕМЫ

### Что работает
- Покупка/продажа на сервере ✅
- Серверная валидация ✅
- Кредиты приходят в RPC ✅

### Что НЕ работает
- TradeUI не обновляется после покупки на хосте
- Товары покупаются, но не отображаются во вкладке [СКЛАД]

### Корневая причина
```
RPC отправляется → Все NetworkPlayers получают → Проверка IsOwner/LocalClientId 
→ На хосте всегда false → TradeUI.Instance.OnTradeResult() не вызывается
```

---

## 🎯 ЧТО НУЖНО СДЕЛАТЬ

### Шаг 1: Изучить документацию Unity NGO

**Ссылка 1:** https://docs.unity.com/ugs/en-us/manual/netcode/current/api/Unity.Netcode.ClientRpcAttribute

Прочитать:
- Что такое ClientRpc
- Параметр `Host` у ClientRpcAttribute
- Как работает отправка конкретному клиенту

**Ссылка 2:** https://docs.unity.com/ugs/en-us/manual/netcode/current/learn/tutorials/community-docs/rpc-communication

Прочитать:
- Раздел "Sending RPCs to specific clients"
- Примеры с ClientRpcParams

---

### Шаг 2: Найти ответы на ключевые вопросы

1. **Как отправить RPC только конкретному клиенту?**
   - Нужен ли `TargetClientRpc`?
   - Или можно использовать `ClientRpcParams`?

2. **Что делает `Host = false` в ClientRpc?**
   - Пропускает ли хоста?
   - Отправляет ли только клиентам?

3. **Как получить ClientRpcParams?**
   ```csharp
   // Это правильный способ?
   [ClientRpc]
   void MyRpc(ClientRpcParams prms = default) { }
   
   // Вызов:
   MyRpc(new ClientRpcParams { 
       Send = new ClientRpcSendParams { 
           TargetClientIds = new ulong[] { 1 } 
       } 
   });
   ```

---

### Шаг 3: Протестировать решения

#### Вариант A: ClientRpcParams с TargetClientIds
```csharp
// В TradeMarketServer.SendTradeResultToClient
var clientParams = new ClientRpcParams
{
    Send = new ClientRpcSendParams
    {
        TargetClientIds = new ulong[] { clientId }
    }
};
player.TradeResultClientRpc(..., clientParams);
```

#### Вариант B: Host = false
```csharp
[ClientRpc(Host = SendToHost.No)]
void TradeResultClientRpc(...) { }
```

#### Вариант C: Прямой вызов на хосте
```csharp
// В TradeMarketServer
if (NetworkManager.Singleton.IsHost)
{
    // Вызвать напрямую
    TradeUI.Instance.OnTradeResult(...);
}
else
{
    // Отправить RPC
    player.TradeResultClientRpc(...);
}
```

---

### Шаг 4: Создать тестовый скрипт

Создать `Assets/_Project/Trade/Scripts/TradeDebugTest.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Trade
{
    /// <summary>
    /// Тестовый скрипт для диагностики ClientRpc на хосте.
    /// Сессия 8H-Next: понять как работает таргетинг клиентов.
    /// </summary>
    public class TradeDebugTest : NetworkBehaviour
    {
        [ClientRpc]
        public void TestBroadcastClientRpc()
        {
            Debug.Log($"[DebugTest] Broadcast: localId={NetworkManager.Singleton.LocalClientId}, IsOwner={IsOwner}, OwnerClientId={OwnerClientId}");
        }
        
        [ClientRpc]
        public void TestTargetedClientRpc(ulong targetClientId)
        {
            var nm = NetworkManager.Singleton;
            Debug.Log($"[DebugTest] Targeted: localId={nm.LocalClientId}, target={targetClientId}, IsOwner={IsOwner}, OwnerClientId={OwnerClientId}");
            
            if (nm.LocalClientId == targetClientId)
            {
                Debug.Log($"[DebugTest] ✅ Этот клиент — целевой!");
            }
        }
        
        public void TestSendToSpecificClient(ulong targetClientId)
        {
            var clientParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };
            TestTargetedClientRpc(targetClientId, clientParams);
        }
    }
}
```

---

## 📁 ФАЙЛЫ ДЛЯ ИЗМЕНЕНИЯ

| Файл | Изменение |
|------|-----------|
| `TradeMarketServer.cs` | Добавить вызов тестов или исправить отправку RPC |
| `NetworkPlayer.cs` | Возможно изменить TradeResultClientRpc |
| `TradeDebugTest.cs` | Создать для тестирования |

---

## 🔧 БЫСТРЫЙ СТАРТ ДЛЯ СЛЕДУЮЩЕЙ СЕССИИ

```
1. Прочитать docs/CHANGELOG_TRADE_SESSION_8H.md (этот документ)
2. Найти NetworkPlayer с TradeResultClientRpc
3. Понять текущую реализацию
4. Изучить документацию Unity NGO
5. Протестировать Вариант C (прямой вызов на хосте)
6. Если не работает — Вариант A (ClientRpcParams)
```

---

## 📞 БЫСТРЫЕ КОМАНДЫ ДЛЯ ТЕСТИРОВАНИЯ

### На хосте (Host):
1. Запустить игру как Host
2. Подключиться клиентом (Client ID = 1)
3. Открыть Trade UI
4. Купить товар
5. Проверить вкладку [СКЛАД]

### Ожидаемый результат:
```
Вкладка [СКЛАД] показывает:
- antigrav_ingot_v01 x1
- mesium_canister_v01 x1
```

### Фактический результат (проблема):
```
Вкладка [СКЛАД] пуста
```

---

**Готов к следующей сессии!**
