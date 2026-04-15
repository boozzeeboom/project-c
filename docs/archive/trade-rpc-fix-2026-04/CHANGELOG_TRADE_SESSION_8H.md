# Сессия 8H: Trade UI — Финальный Отчёт и Инструкция для Следующей Сессии

**Дата:** 2026-04-15  
**Статус:** Завершена с незакрытой проблемой  
**Следующий шаг:** Требуется глубокий анализ NGO ClientRpc API

---

## 📋 ЧТО ДЕЛАЛИ

### Цель
Исправить отображение купленных товаров в складской вкладке TradeUI при Host+Client конфигурации.

### Достигнуто
1. ✅ Торговля работает — покупка/продажа проходит успешно
2. ✅ Серверная логика корректна — все операции валидируются
3. ✅ RPC отправляется — `SendTradeResultToClient` вызывается
4. ✅ Кредиты обновляются (новые значения приходят в RPC)

### НЕ Решено
❌ **TradeUI не получает обновление склада** — товары покупаются, но не отображаются во вкладке [СКЛАД]

---

## 🔍 НАЙДЕННАЯ ПРОБЛЕМА

### Корневая причина
```
[NetworkPlayer] TradeResultClientRpc: localId=0, OwnerClientId=1, success=True
[NetworkPlayer] Этот клиент (id=0) НЕ владеет NetworkPlayer (owner=1), пропускаю
```

**На хосте:**
- `NetworkManager.Singleton.LocalClientId` = 0
- `NetworkPlayer.OwnerClientId` = 1 (для клиентского игрока)
- `NetworkPlayer.IsOwner` = False (для всех NetworkPlayer кроме своего)

**Почему RPC не доходит:**
1. RPC отправляется на `OwnerClientId = 1`
2. На хосте все NetworkPlayers получают RPC через `[ClientRpc]`
3. Проверка `LocalClientId == OwnerClientId` вызывается на КАЖДОМ NetworkObject
4. На хосте `localId=0` всегда ≠ `OwnerClientId=1` для клиентских NetworkPlayers
5. Поэтому `TradeUI.Instance.OnTradeResult()` никогда не вызывается

---

## 📁 СВЯЗАННЫЕ ФАЙЛЫ

| Файл | Роль |
|------|------|
| `Assets/_Project/Trade/Scripts/TradeUI.cs` | Клиентский UI торговли |
| `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` | Серверная логика торговли |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | Содержит TradeResultClientRpc |
| `docs/CHANGELOG_TRADE_2026-04-15.md` | Предыдущий changelog |
| `docs/bugs/BUG-2026-04-15-PORT.md` | Документация порта |

---

## 🔧 ЧТО ПРОБОВАЛИ

### Попытка 1: IsOwner
```csharp
if (!IsOwner) { return; }
```
**Результат:** ❌ `IsOwner=False` для всех NetworkPlayer на хосте (кроме своего)

### Попытка 2: LocalClientId == OwnerClientId
```csharp
ulong localId = NetworkManager.Singleton.LocalClientId;
if (localId != OwnerClientId) { return; }
```
**Результат:** ❌ На хосте `localId=0`, `OwnerClientId=1` — всегда не равны

### Попытка 3: Debug логи в SyncWarehouseItem
```csharp
Debug.Log($"[TradeUI] SyncWarehouseItem: ВЫЗВАН!");
```
**Результат:** ❌ Метод не вызывается — RPC не доходит до TradeUI

---

## 💡 КЛЮЧЕВОЕ ПОНИМАНИЕ ПРОБЛЕМЫ

### NGO ClientRpc поведение на хосте

**Как работает `[ClientRpc]`:**
```
1. Сервер вызывает TradeResultClientRpc() на своём NetworkPlayer
2. Атрибут [ClientRpc] отправляет RPC ВСЕМ клиентам
3. На хосте ВСЕ NetworkPlayers получают этот RPC
4. Каждый NetworkPlayer выполняет тело RPC
```

**Почему проверки не работают:**
```
Host Context:
├── NetworkManager.LocalClientId = 0 (всегда)
├── NetworkPlayer(Owner=0): IsOwner=True, OwnerClientId=0 ✓
└── NetworkPlayer(Owner=1): IsOwner=False, OwnerClientId=1 ✗
    └── LocalClientId=0 ≠ OwnerClientId=1 → пропуск
```

---

## 📚 ДОКУМЕНТАЦИЯ ДЛЯ СЛЕДУЮЩЕЙ СЕССИИ

### 1. Unity Netcode for GameObjects ClientRpc

**Официальная документация:**
- https://docs.unity.com/ugs/en-us/manual/netcode/current/learn/tutorials/community-docs/rpc-communication
- https://docs.unity.com/ugs/en-us/manual/netcode/current/api/Unity.Netcode.ClientRpcAttribute

**Ключевые методы для изучения:**
- `[ClientRpc]` — отправить всем клиентам
- `[ClientRpc(Host = false)]` — отправить только НЕ хосту
- `SendToClient` параметры для таргетинга конкретного клиента

### 2. Конкретные вопросы для исследования

1. **Как отправить RPC конкретному клиенту на хосте?**
   - Метод `TradeResultClientRpc(..., ClientRpcParams)`?
   - `SendToClient(ulong clientId)`?

2. **Почему IsOwner возвращает False на хосте для других игроков?**
   - Это документированное поведение?
   - Есть ли исключение?

3. **Как правильно проверять "этот клиент владеет этим объектом"?**
   - `NetworkManager.Singleton.LocalClientId == OwnerClientId`
   - Или это не работает на хосте?

### 3. Альтернативные подходы к решению

**Вариант A: Разный код для хоста и клиента**
```csharp
// На сервере
player.TradeResultClientRpc(...); // отправляет всем

// На клиенте (не хосте)
void TradeResultClientRpc(...) {
    if (!IsOwner) return; // работает для чистого клиента
}
```

**Вариант B: TargetClientRpc**
```csharp
// Отправить конкретному клиенту
[ClientRpc(Host = false)]
void TradeResultClientRpc(..., ClientRpcParams prms) {
    // ...
}

// ИЛИ использовать SendToClient
```

**Вариант C: Отказаться от ClientRpc**
```csharp
// Вместо ClientRpc использовать напрямую
if (NetworkManager.Singleton.IsHost) {
    // Локальный вызов для хоста
    TradeUI.Instance.OnTradeResult(...);
} else {
    // RPC для чистого клиента
    TradeResultClientRpc(...);
}
```

### 4. План действий для следующей сессии

```
1. Изучить Unity Netcode for GameObjects документацию по ClientRpc
   → https://docs.unity.com/ugs/en-us/manual/netcode/current/api/Unity.Netcode.ClientRpcAttribute
   
2. Найти примеры TargetClientRpc
   → Как отправить RPC конкретному клиенту
   
3. Проверить: работает ли IsOwner на хосте?
   → Создать тестовый скрипт с debug логами
   
4. Найти: как получить clientId из RpcParams?
   → RpcParams.Receive.LocalClientId
   
5. Протестировать решения из вариантов A, B, C
```

---

## 🎯 ЧЕКЛИСТ ДЛЯ СЛЕДУЮЩЕЙ СЕССИИ

- [ ] Изучить `ClientRpcAttribute` в документации Unity
- [ ] Найти примеры `TargetClientRpc` 
- [ ] Проверить `IsOwner` поведение на хосте
- [ ] Найти `RpcParams.Receive.LocalClientId` использование
- [ ] Реализовать исправление (вариант B или C)
- [ ] Протестировать на Host+Client
- [ ] Протестировать на выделенном сервере

---

## 📊 СТАТУС ПО СИСТЕМАМ

| Система | Статус | Комментарий |
|---------|--------|-------------|
| TradeMarketServer | ✅ Работает | Серверная логика корректна |
| Кредиты | ✅ Синхронизируются | Приходят в RPC |
| Склад | ❌ НЕ работает | RPC не доходит до TradeUI |
| UI Отображение | ❌ НЕ работает | TradeUI не обновляется |
| Покупка/Продажа | ✅ Работает | На сервере всё ОК |

---

## 🔗 ПОЛЕЗНЫЕ РЕСУРСЫ

### Unity Netcode for GameObjects
- https://docs.unity.com/ugs/en-us/manual/netcode/current/getting-started/about-netcode
- https://github.com/Unity-Technologies/netcode-for-gameobjects

### NGO RPC Best Practices
- https://docs.unity.com/ugs/en-us/manual/netcode/current/best-practices-scene-management/rpc-best-practices

### Примеры кода
```csharp
// Отправка конкретному клиенту
[ClientRpc]
private void MyClientRpc(ClientRpcParams clientRpcParams = default)
{
    // Тело RPC
}

// Вызов для конкретного клиента
ulong targetClientId = 1;
ClientRpcParams clientParams = new ClientRpcParams
{
    Send = new ClientRpcSendParams
    {
        TargetClientIds = new ulong[] { targetClientId }
    }
};
MyClientRpc(clientParams);
```

---

## 📝 СЛЕДУЮЩИЙ ШАГ — СОЗДАТЬ ТЕСТОВЫЙ СКРИПТ

В следующей сессии создать `TradeDebugTest.cs`:

```csharp
public class TradeDebugTest : NetworkBehaviour
{
    [ClientRpc]
    public void TestClientRpc(ulong targetClientId = ulong.MaxValue)
    {
        var nm = NetworkManager.Singleton;
        Debug.Log($"[Test] localId={nm.LocalClientId}, target={targetClientId}, IsOwner={IsOwner}");
        
        if (targetClientId != ulong.MaxValue && nm.LocalClientId == targetClientId)
        {
            Debug.Log($"[Test] Этот клиент — целевой!");
        }
    }
    
    // Вызвать с хоста:
    // ulong targetId = 1; // client id
    // TestClientRpc(new ClientRpcParams { ... });
}
```

Этот тест покажет как работает таргетинг на хосте.

---

**Дата создания:** 2026-04-15 22:30  
**Автор:** Claude Code (Cline)  
**Следующая сессия:** Глубокий анализ NGO API
