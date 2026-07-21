# Player-Ship Position Persistence — План

> **Статус:** План ✅ | Реализация: ⏳
> **Цель:** Связать сохранение позиции игрока с кораблём при выходе/дисконнекте. Игрок, вышедший в полёте на корабле, при повторном входе появляется у своего корабля (зависшего в воздухе), а не на дефолтной точке респавна.
> **Дата:** 2026-07-21
> **Основание:** Анализ `ShipController` (FixedUpdate fuel/idle logic), `ShipPositionServer` (существующий ship persistence), `NetworkPlayer` (board/disembark/despawn), `PlayerRespawnTracker` (respawn).

---

## 1. Проблема

### Текущее поведение (as-is)

1. Игрок садится в корабль → `AddPilot()` → `_pilots.Add(clientId)` → движок включён, корабль летит.
2. Игрок выходит из корабля (F) → `RemovePilot(OwnerClientId)` → `_pilots.Remove(clientId)`.
3. Если пилотов больше нет и NPC-пилота нет:
   - **Двигатель остаётся включённым** (комментарий в коде: «ENGINE-STATE: выход разрешён всегда, независимо от скорости. Двигатель остаётся в текущем состоянии»).
   - Корабль переходит в **IDLE-режим** (`_engineRunning && _pilots.Count == 0 && !_hasNpcPilot`):
     - `antiGravity` работает → корабль **зависает** в воздухе ✅
     - **Топливо тратится** (idle consumption 0.05/s) ❌
     - **Ветер продолжает сносить** корабль ❌
   - Позиция корабля продолжает сохраняться через `ShipPositionServer` (каждые 5s) ✅
4. Игрок дисконнектится → `OnNetworkDespawn()` → `RemovePilot()` → то же самое.
5. Игрок перезаходит → спавнится на **дефолтной точке респавна** (BootstrapScene), а не у своего корабля ❌

### Корневые причины

| # | Проблема | Где |
|---|----------|-----|
| P1 | Нет проверки «есть ли пилот» при потреблении топлива на idle | `ShipController.FixedUpdate:1261` |
| P2 | Нет заморозки позиции корабля при уходе последнего пилота | `ShipController.RemovePilotRpc:1852` |
| P3 | Нет сохранения позиции игроков (только кораблей) | `ShipPositionServer` |
| P4 | Нет восстановления позиции игрока из save при connect | `NetworkPlayer.OnNetworkSpawn` |

---

## 2. Дизайн решения

### 2.1 Общая схема

```
Player connects
  → PlayerPositionServer.RestorePlayer(clientId)
    → был на корабле (inShip=true, shipId известен)?
      → ShipPositionServer уже восстановил корабль (этап 3.5s)
      → телепорт игрока на GetExitPosition() корабля
      → корабль завис в воздухе, ждёт пилота
    → не был на корабле?
      → телепорт на последнюю сохранённую позицию

Player in ship → exits/disconnects
  → RemovePilotRpc: если _pilots.Count == 0 после удаления
    → FREEZE: _rb.linearVelocity = 0, _rb.angularVelocity = 0
    → _frozenByNoPilot = true
    → Engine stays ON, но fuel consumption BLOCKED флагом

ShipController.FixedUpdate
  → если _frozenByNoPilot && _pilots.Count == 0:
    → antiGravity работает (чтобы не упал)
    → топливо НЕ тратится
    → ветер НЕ применяется

PlayerPositionServer (каждые 5s, вместе с ShipPositionServer)
  → собирает всех NetworkPlayer
  → сохраняет clientId, позицию, inShip, shipPersistentId

Player connects
  → OnNetworkSpawn → через 4s PlayerPositionServer.RestorePlayer()
  → если сохранён inShip → телепорт к кораблю
```

### 2.2 Новые компоненты

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `PlayerPositionSaveData` | DTO в `ShipPositionSaveData.cs` | Данные позиции одного игрока |
| `PlayerPositionServer` | Новый `Core/ShipPosition/PlayerPositionServer.cs` | Save/restore позиций игроков |
| Изменения в `ShipController` | `ShipController.cs` | Freeze + fuel block при отсутствии пилотов |
| Изменения в `NetworkPlayer` | `NetworkPlayer.cs` | Restore из save при connect |

---

## 3. Детальная реализация

### 3.1 DTO: `PlayerPositionSaveData`

Добавить в `ShipPositionSaveData.cs` (тот же файл, тот же namespace):

```csharp
[Serializable]
public class PlayerPositionSaveData
{
    public ulong clientId;           // NGO clientId
    public float px, py, pz;         // world position
    public bool inShip;              // был на корабле?
    public string shipPersistentId;  // _shipPersistentId корабля (если inShip)
    public long savedAtUnix;
}
```

Добавить wrapper в `ShipPositionListWrapper`:
```csharp
public List<PlayerPositionSaveData> players = new List<PlayerPositionSaveData>();
```

### 3.2 ShipController: freeze + fuel block

**Новое поле:**
```csharp
// T-PLAYER-PERSIST: корабль заморожен (нет пилотов, двигатель включён)
private bool _frozenByNoPilot = false;
```

**Изменение в `RemovePilotRpc`:**
```csharp
[Rpc(SendTo.Everyone)]
private void RemovePilotRpc(ulong clientId, RpcParams rpcParams = default)
{
    _pilots.Remove(clientId);

    // T-PLAYER-PERSIST: если пилотов не осталось, заморозить корабль
    if (_pilots.Count == 0 && _engineRunning && !_hasNpcPilot)
    {
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        _frozenByNoPilot = true;
        // Сбросить накопленный ввод
        _sumThrust = 0; _sumYaw = 0; _sumPitch = 0; _sumVertical = 0;
        _boostCount = 0; _inputCount = 0;
    }
}
```

**Изменение в `FixedUpdate` — idle fuel consumption (строка 1261):**
```csharp
// ENGINE-STATE: idle расход топлива (двигатель включён, ввода нет)
// T-PLAYER-PERSIST: если корабль заморожен (нет пилотов) — топливо не тратится
if (_engineRunning && !_hasNpcPilot && fuelSystem != null && !engineStalled && isIdle && !_frozenByNoPilot)
{
    fuelSystem.ConsumeFuel(fuelSystem.IdleConsumptionRate * dt);
}
```

**Изменение в `FixedUpdate` — ветер (строки 1469-1473):**
```csharp
// T-PLAYER-PERSIST: если корабль заморожен — ветер не применяется
if (!_frozenByNoPilot)
{
    ApplyWind(dt);
    ApplyGlobalWind(dt);
}
```

**Снятие флага `_frozenByNoPilot`:**
В `AddPilotRpc`, при добавлении первого пилота:
```csharp
[Rpc(SendTo.Everyone)]
private void AddPilotRpc(ulong clientId, RpcParams rpcParams = default)
{
    _pilots.Add(clientId);
    _frozenByNoPilot = false; // T-PLAYER-PERSIST: пилот вернулся — разморозить
    enabled = true;
}
```

### 3.3 PlayerPositionServer

**Новый файл:** `Assets/_Project/Scripts/Core/ShipPosition/PlayerPositionServer.cs`

```csharp
namespace ProjectC.Core.ShipPosition
{
    /// <summary>
    /// Server-only persistence позиций игроков.
    /// Save: каждые 5s вместе с ShipPositionServer.
    /// Restore: при OnNetworkSpawn игрока (с задержкой 4s).
    /// </summary>
    public class PlayerPositionServer : MonoBehaviour
    {
        public static PlayerPositionServer Instance { get; private set; }

        [SerializeField] private float saveIntervalSec = 5f;
        [SerializeField] private bool debugMode = true;

        private IShipPositionRepository _repo;
        private float _nextSaveTime;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _repo = new JsonShipPositionRepository();
        }

        private void Start()
        {
            _nextSaveTime = Time.time + saveIntervalSec;
        }

        private void Update()
        {
            if (!IsServerSafe()) return;
            if (Time.time < _nextSaveTime) return;
            _nextSaveTime = Time.time + saveIntervalSec;

            SaveAllPlayers();
        }

        // ── Save ──

        private void SaveAllPlayers()
        {
            // Загружаем существующий файл (чтобы не перетереть ships)
            var allData = _repo.LoadAll();
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            
            var playerList = new List<PlayerPositionSaveData>();
            foreach (var np in allPlayers)
            {
                if (!np.IsSpawned) continue;
                if (np.OwnerClientId == 0 && !np.IsOwner) continue; // skip host's scene-placed ghost

                bool inShip = np.IsInShip;
                string shipId = "";
                if (inShip && np.CurrentShip != null)
                    shipId = np.CurrentShip.ShipPersistentId;

                Vector3 pos = np.GetEffectivePosition();

                playerList.Add(new PlayerPositionSaveData
                {
                    clientId = np.OwnerClientId,
                    px = pos.x, py = pos.y, pz = pos.z,
                    inShip = inShip,
                    shipPersistentId = shipId,
                    savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }

            allData.players = playerList;
            _repo.SaveAll(allData.ships, playerList); // нужен новый метод SaveAll с players

            if (debugMode)
                Debug.Log($"[PlayerPositionServer] Saved {playerList.Count} players");
        }

        // ── Restore ──

        public void RestorePlayer(NetworkPlayer np)
        {
            if (!IsServerSafe()) return;

            var allData = _repo.LoadAll();
            var match = allData.players?.Find(p => p.clientId == np.OwnerClientId);
            if (match == null)
            {
                if (debugMode) Debug.Log($"[PlayerPositionServer] No save for client={np.OwnerClientId}");
                return;
            }

            if (match.inShip && !string.IsNullOrEmpty(match.shipPersistentId))
            {
                // Игрок был на корабле — найти корабль и телепортировать
                var allShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
                var ship = System.Array.Find(allShips, s => s.ShipPersistentId == match.shipPersistentId);
                
                if (ship != null && ship.IsSpawned)
                {
                    Vector3 exitPos = ship.GetExitPosition();
                    np.TeleportTo(exitPos);
                    if (debugMode)
                        Debug.Log($"[PlayerPositionServer] Restored player {np.OwnerClientId} to ship '{match.shipPersistentId}' at {exitPos}");
                    return;
                }
                // Корабль не найден — fallback на позицию
            }

            // Не на корабле (или корабль не найден) — телепорт на сохранённую позицию
            Vector3 pos = new Vector3(match.px, match.py, match.pz);
            np.TeleportTo(pos);
            if (debugMode)
                Debug.Log($"[PlayerPositionServer] Restored player {np.OwnerClientId} to position {pos}");
        }

        private static bool IsServerSafe()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }
    }
}
```

### 3.4 NetworkPlayer: restore on connect

**Добавить в `OnNetworkSpawn` (после `RegisterWithCombatServer()`):**
```csharp
// T-PLAYER-PERSIST: восстановить позицию из save (с задержкой, после ShipPositionServer.Restore)
if (IsServer || IsOwner) // IsServer для dedicated, IsOwner для host
{
    StartCoroutine(RestorePlayerPositionCoroutine());
}
```

**Новая корутина:**
```csharp
private IEnumerator RestorePlayerPositionCoroutine()
{
    // Ждём ShipPositionServer.RestoreCoroutine (3.5s) + PlayerPositionServer init
    yield return new WaitForSeconds(4f);

    if (PlayerPositionServer.Instance != null)
        PlayerPositionServer.Instance.RestorePlayer(this);
    else
        Debug.LogWarning($"[NetworkPlayer] PlayerPositionServer.Instance == null — skip restore for client={OwnerClientId}");
}
```

**Добавить публичный метод `TeleportTo`:**
```csharp
/// <summary>
/// T-PLAYER-PERSIST: телепортировать игрока (сервер-авторитативно).
/// Используется PlayerPositionServer.RestorePlayer.
/// </summary>
public void TeleportTo(Vector3 position)
{
    if (_controller != null) _controller.enabled = false;
    transform.position = position;
    if (_controller != null) _controller.enabled = true;
    Physics.SyncTransforms();

    // Сброс fall-таймера респавна
    var tracker = GetComponent<PlayerRespawnTracker>();
    if (tracker != null) tracker.ResetFallTimer();
}
```

### 3.5 ShipPositionRepository — расширение

Добавить перегрузку `SaveAll` с players:

```csharp
public void SaveAll(List<ShipPositionSaveData> ships, List<PlayerPositionSaveData> players)
{
    var path = FilePath;
    var wrapper = new ShipPositionListWrapper { ships = ships, players = players };
    lock (_ioLock)
    {
        try
        {
            var json = JsonUtility.ToJson(wrapper, prettyPrint: false);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[JsonShipPositionRepository] SaveAll failed: {ex.Message}");
        }
    }
}
```

**Важно:** `SaveAll` из `ShipPositionServer` нужно тоже обновить — передавать сохранённых players (читать из `_repo.LoadAll()` перед записью, сохранять `players`). Альтернативно: совместить `PlayerPositionServer.Update` и `ShipPositionServer.Update` в один цикл. Но проще сделать чтобы `PlayerPositionServer` писал в тот же файл атомарно.

**Решение для атомарности:** объединить save в `PlayerPositionServer.Update` (он вызывает общий save ships + players). `ShipPositionServer.Update` перестаёт писать напрямую — вместо этого вызывает `PlayerPositionServer.Instance.RequestSave()`. Или наоборот.

**Рекомендуемый подход:** оставить `ShipPositionServer.Update` как есть, но добавить ему поле `_pendingPlayers`, которое заполняет `PlayerPositionServer`. На каждом save-цикле `ShipPositionServer` сначала читает players из `PlayerPositionServer.Instance`, пишет всё вместе.

**Самый простой подход (MVP):** `PlayerPositionServer` имеет свой `SaveAllPlayers()`, который:
1. Читает существующий файл (`_repo.LoadAll()`)
2. Обновляет только `players` поле
3. Пишет обратно (`_repo.SaveAll(ships, players)`)

Это неатомарно с `ShipPositionServer` (два write на один файл с минимальным интервалом), но для 5-секундного интервала это приемлемо. Альтернативно — объединить оба в один `Update`.

---

## 4. Что НЕ делаем (out of scope)

- ❌ Сохранение HP/инвентаря/статов игрока (отдельная система)
- ❌ Сохранение позиции камеры/поворота игрока
- ❌ Авто-посадка в корабль при connect (игрок появляется на палубе, не в кресле)
- ❌ Сохранение состояния двигателя после перезапуска сервера (engine после restore всегда OFF — требует ручного запуска)
- ❌ Атомарность save ships + players в одном write (MVP — два отдельных write, следующий итерацией)

---

## 5. Порядок выполнения

| Этап | Тикет | Файлы | LOC | Часы |
|------|-------|-------|-----|------|
| **1** | T-PP-DTO | `ShipPositionSaveData.cs` (+PlayerPositionSaveData, +Wrapper) | +25 | 0.15 |
| **2** | T-PP-REPO | `ShipPositionRepository.cs` (+SaveAll overload) | +15 | 0.15 |
| **3** | T-PP-FREEZE | `ShipController.cs` (_frozenByNoPilot, RemovePilotRpc freeze, AddPilotRpc unfreeze) | +25 | 0.3 |
| **4** | T-PP-FUEL | `ShipController.cs` (FixedUpdate: idle fuel guard, wind guard) | +8 | 0.15 |
| **5** | T-PP-SERVER | `PlayerPositionServer.cs` (новый) | ~120 | 0.75 |
| **6** | T-PP-NETPLAYER | `NetworkPlayer.cs` (RestorePlayerPositionCoroutine, TeleportTo) | +35 | 0.3 |
| **7** | T-PP-INTEGRATE | Создание PlayerPositionServer в BootstrapScene | 5 | 0.1 |
| | **Итого** | **1 новый, 4 изменённых** | **~233** | **~1.9** |

### Зависимости

```
T-PP-DTO (1)
  ↓
T-PP-REPO (2)
  ↓
T-PP-FREEZE (3) ───┐
T-PP-FUEL (4) ─────┤
  ↓                 │
T-PP-SERVER (5) ←───┘
  ↓
T-PP-NETPLAYER (6)
  ↓
T-PP-INTEGRATE (7)
```

---

## 6. Валидация (smoke test)

| # | Проверка | Ожидание |
|---|----------|----------|
| 1 | Игрок садится в корабль, взлетает, выходит (F) | Корабль зависает в воздухе, топливо НЕ тратится, ветер НЕ сносит |
| 2 | Через 6+ секунд | Файл `ShipPositions.json` содержит `players` с `inShip=true` |
| 3 | Игрок выходит в главное меню → заходит снова | Игрок появляется у своего корабля (на палубе) |
| 4 | Двигатель корабля включён (до выхода был on) | Корабль висит, двигатель on, топливо не тратится |
| 5 | Игрок заходит на корабль (F) | `_frozenByNoPilot=false`, топливо снова тратится, управление работает |
| 6 | Игрок НЕ на корабле → disconnect → reconnect | Игрок появляется на последней сохранённой позиции |
| 7 | Первый старт (нет save) | Игрок спавнится как обычно, без ошибок |

---

## 7. Риски

| Риск | Вероятность | Митигация |
|------|-------------|-----------|
| `PlayerPositionServer` и `ShipPositionServer` пишут в один файл неатомарно | Средняя | Интервал 5s даёт запас; при коллизии — потеря 1 цикла players (допустимо) |
| `RestorePlayerPositionCoroutine` срабатывает до `ShipPositionServer.RestoreCoroutine` | Низкая | Задержка 4s vs 3.5s для кораблей |
| `_frozenByNoPilot` не снимается при NPC-pilot takeover | Низкая | NPC используют `_hasNpcPilot`, guard в `AddPilotRpc` |
| `TeleportTo` конфликтует с CharacterController | Низкая | Паттерн `_controller.enabled=false → position → enabled=true` проверен в `PlayerRespawnTracker` |
| Игрок на корабле без `_shipPersistentId` | Низкая | Lazy init в getter `ShipPersistentId` — всегда заполняется |

---

**Создано:** 2026-07-21 на основе анализа `ShipController.FixedUpdate` (idle fuel + wind), `ShipPositionServer` (существующий save), `NetworkPlayer.SubmitSwitchModeRpc` (board/disembark/despawn), `PlayerRespawnTracker` (respawn), `ShipPositionSaveData` (DTO).
**Следующий шаг:** Запрос на реализацию T-PP-DTO.
