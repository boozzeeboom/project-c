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

### Архитектура сцен (как сейчас, 2026-06)

- `BootstrapScene` (buildIndex 0) — стартовая. Содержит `NetworkManager` (singleton, `DontDestroyOnLoad`) + `NetworkPlayerSpawner` + `ClientSceneLoader`
- `WorldScene_X_Z` (buildIndex 1–24) — стриминговые, сетка 6×4. Грузятся через `ClientSceneLoader` (обычный `SceneManager.LoadSceneAsync(Additive)`, НЕ `NetworkSceneManager.LoadScene`)
- На хосте при `StartHost()` с `EnableSceneManagement: 1` NGO через `NetworkSceneManager` загружает ВСЕ сцены → scene-placed `NetworkObject` спавнятся автоматически
- Стриминговая инфраструктура (`WorldSceneManager`, `ServerSceneManager`, `WorldStreamingManager`) **написана в коде, но НЕ развёрнута в bootstrap-сцене** — TODO на следующую фазу

### Ключевые Файлы

| Файл | Назначение |
|------|-----------|
| `Core/NetworkManagerController.cs` | Обёртка NGO, подключения, реконнект, StartHost/Server/Client |
| `Player/NetworkPlayer.cs` | Игрок: движение, камера, инвентарь, посадка в корабль (RPC) |
| `World/Scene/ClientSceneLoader.cs` | **Загружает 24 стриминговые сцены additive** (НЕ через NetworkSceneManager) |
| `World/Scene/SceneBoundNetworkObject.cs` | ⏳ Написан, не развёрнут. Per-scene фильтрация видимости через `CheckObjectVisibility` |
| `World/Scene/ServerSceneManager.cs` | ⏳ Написан, не развёрнут. Server-side tracking клиентов по сценам, `NetworkHide/NetworkShow` |
| `World/Streaming/WorldStreamingManager.cs` | ⏳ Написан, не развёрнут. Chunk-based стриминг |
| `World/Streaming/ChunkNetworkSpawner.cs` | Спавнит chest/NPC при загрузке чанков (server-side) |
| `World/Streaming/FloatingOriginMP.cs` | Большие координаты (но не доведён, см. `INTEGRATION_SHIPS_TO_WORLD_0_0.md`) |

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

### Scene-placed NetworkObject и RPC

**`NetworkObject` в стриминговой сцене, загруженной через `SceneManager.LoadSceneAsync(Additive)`**, спавнятся сервером автоматически **только если** сцена загружена через `NetworkSceneManager` с `EnableSceneManagement: 1`. На текущей фазе (фокус на `WorldScene_0_0`) это работает через `BootstrapScene → StartHost()` — NGO грузит все 24 сцены через `NetworkSceneManager`, scene-placed спавнятся.

**Если `IsSpawned == false`** — RPC упадёт NRE в `__endSendRpc` (NGO 2.x source line 354). Защита — guards в коде (см. `INTEGRATION_SHIPS_TO_WORLD_0_0.md` §4.3):

```csharp
// Перед любым [Rpc] вызовом на NetworkBehaviour:
if (NetworkManager.Singleton == null || !IsSpawned) return;
```

И в `NetworkPlayer.SendShipInput` дополнительно проверить `_currentShip.IsSpawned`.

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
