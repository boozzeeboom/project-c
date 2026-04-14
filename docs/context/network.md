# Network Context — Project C

**Теги:** `network`, `NGO`, `RPC`, `synchronization`, `FloatingOrigin`, `multiplayer`

---

## 📡 Сетевая Архитектура

### NGO (Netcode for GameObjects)

```
Transport: Unity Transport (UDP/Steam)
Protocol: Netcode for GameObjects (NGO)
Architecture: Host + Client + Dedicated Server
```

### Ключевые Файлы

| Файл | Назначение |
|------|------------|
| `Core/NetworkManagerController.cs` | Обёртка NGO, подключения, реконнект |
| `Player/NetworkPlayer.cs` | Игрок: движение, камера, инвентарь |
| `World/Streaming/FloatingOriginMP.cs` | Синхронизация при origin shift |
| `Core/NetworkInventory.cs` | Синхронизация предметов |

### Network Variables

```csharp
// ✅ Правильно: NetworkVariable для состояния
private NetworkVariable<Vector3> NetworkPosition = new NetworkVariable<Vector3>();

// ✅ Правильно: RPC для one-shot actions
[ServerRpc(RequireOwnership = false)]
public void CastAbilityServerRpc(int abilityId) { }

// ❌ Неправильно: Синхронизация в Update()
void Update() {
    NetworkPosition.Value = transform.position; // VIOLATION
}
```

---

## 🔧 Floating Origin MP

### Проблема
При больших координатах (>100,000 units) возникают погрешности float.

### Решение
```csharp
FloatingOriginMP.SnapToOrigin();
// 1. Сохраняет смещение
// 2. Сдвигает все объекты
// 3. Синхронизирует с другими клиентами
```

### Координаты мира
- Радиус мира: ~350,000 units (XZ ×50)
- Порог origin shift: 100,000 units
- 5 городов, 15+ пиков

---

## 🕹️ RPC Patterns

### ServerRpc (клиент → сервер)
```csharp
[ServerRpc(RequireOwnership = false)]
public void TradeRequestServerRpc(int itemId, int quantity, ulong targetClientId) { }
```

### ClientRpc (сервер → клиенты)
```csharp
[ClientRpc]
public void SyncInventoryClientRpc(string inventoryJson) { }
```

### SendTo (конкретный клиент)
```csharp
networkManager.Singleton.CustomMessagingManager.SendNamedMessage(
    "TradeResult", 
    clientId, 
    stream
);
```

---

## 🔄 Reconnect Flow

```
1. OnClientDisconnectCallback
2. Сохранить инвентарь в PlayerPrefs
3. Показать Disconnect UI
4. Авто-реконнект (5 попыток)
5. Или ручной реконнект через кнопку
6. Восстановить инвентарь из PlayerPrefs
```

---

## ⚠️ Известные Проблемы

| Приоритет | Проблема | Решение |
|-----------|----------|---------|
| P0 | PlayerPrefs для данных | Заменить на БД |
| P1 | Нет проверки позиции в RPC | Добавить locationId |

---

## 📖 Подробнее

- `docs/NETWORK_ARCHITECTURE.md` — полная архитектура
- `docs/NETWORK_PHASE2_PLAN.md` — план этапа
- `docs/DEDICATED_SERVER.md` — запуск Dedicated Server

---

**Обновлено:** 2026-04-15
