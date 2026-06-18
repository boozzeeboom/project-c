# Ship Ownership Model — Server-Side реестр владельцев кораблей

**Подсистема:** Корабли — кто каким кораблём ВЛАДЕЕТ (по уникальному ключу)
**Тег:** `ship-ownership`, `ownership-world`, `ship-requirement`
**Статус:** 📋 Дизайн готов, код НЕ написан
**Дата:** 2026-06-18
**Связанные документы:**
- `20_UNIQUE_KEY_INSTANCE.md` — концепция KeyRodInstance
- `22_SHIP_TELEMETRY_PLAN.md` — UI-проекция состояния корабля
- `23_ROADMAP.md` — тикеты

---

## 1. Проблема

### 1.1 Что есть сейчас

`MetaRequirementRegistry` (`Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs`) — это реестр **требований** (что нужно иметь в инвентаре чтобы interactable работал). У него НЕТ реестра **владения** (кто чем владеет).

`ShipKeyServer` (legacy, теперь `[Obsolete]` alias) хранит `Dictionary<ulong, ShipKeyBinding>` — реестр **связей** `shipId ↔ keyItemId`, но не владения.

### 1.2 Что нужно

Когда игрок A имеет ключ с `instanceId=42, registeredShipId=5`, сервер должен мочь ответить на 3 вопроса:

1. **"Может ли A управлять кораблём 5?"** → да, если instanceId=42 в инвентаре A.
2. **"Какими кораблями владеет A?"** → список (instanceId, shipId) пар.
3. **"Кто сейчас владеет кораблём 5?"** → ClientId A (или B, если передал).

---

## 2. Архитектура

### 2.1 KeyRodInstanceWorld (server POCO singleton)

```csharp
namespace ProjectC.Ship.Key
{
    /// <summary>Server-only static facade. Single source of truth для всех
    /// KeyRodInstance в текущей сессии. Создаётся в KeyRodInstanceBinding.OnNetworkSpawn
    /// (Q11: explicit binding компонент на каждом [KeyRod_*] PickupItem).
    /// Паттерн скопирован с CraftingWorld (Assets/_Project/Scripts/Crafting/CraftingWorld.cs).</summary>
    public static class KeyRodInstanceWorld
    {
        // ----- Instance registry (instanceId → KeyRodInstance) -----
        private static Dictionary<int, KeyRodInstance> _instancesById = new();
        private static int _nextInstanceId = 1;

        // ----- Reverse lookup (shipNetId → instanceId) — 1:1 в MVP -----
        // (расширяется до 1:N, если у одного корабля несколько ключей — например, "main" + "spare")
        private static Dictionary<ulong, int> _primaryInstanceByShipId = new();

        // ----- Per-player index (clientId → List<instanceId>) — для быстрого GetMyShips -----
        private static Dictionary<ulong, List<int>> _instancesByPlayer = new();

        public static bool IsInitialized { get; private set; }

        public static int CreateInstance(int itemId, ulong registeredShipId, ulong ownerPlayerId) { ... }
        public static void DestroyInstance(int instanceId) { ... }
        public static void TransferInstance(int instanceId, ulong fromClientId, ulong toClientId) { ... }

        public static KeyRodInstance GetInstance(int instanceId) { ... }
        public static int GetInstanceIdForShip(ulong shipNetId) { ... }
        public static List<int> GetInstancesForPlayer(ulong clientId) { ... }
    }
}
```

**MVP-граница**: 1 корабль ↔ 1 экземпляр ключа (1:1). Расширение до 1:N ("main + spare") — фаза 2.

### 2.2 ShipOwnershipRequirement (NetworkBehaviour на корабле)

```csharp
namespace ProjectC.Ship.Key
{
    /// <summary>Аналог MetaRequirement, но для кораблей. Проверяет, что у игрока
    /// есть KeyRodInstance с instanceId, привязанным к registeredShipId == этот корабль.
    /// Display name берётся из ShipController._customDisplayName (Q6, 2026-06-18:
    /// "минимальный фикс в шипконтроллер", подтягивается к ключу).</summary>
    [DisallowMultipleComponent]
    public class ShipOwnershipRequirement : NetworkBehaviour
    {
        // Display name резолвится через ShipController на том же GameObject
        private ShipController _shipController;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            _shipController = GetComponent<ShipController>();

            if (MetaRequirementRegistry.Instance != null)
            {
                MetaRequirementRegistry.Instance.RegisterShipOwnership(NetworkObjectId, this);
            }
        }

        public bool IsOwner(ulong clientId)
        {
            if (!IsServer) return false;

            int instanceId = KeyRodInstanceWorld.GetInstanceIdForShip(NetworkObjectId);
            if (instanceId <= 0) return false;

            var inst = KeyRodInstanceWorld.GetInstance(instanceId);
            if (inst == null) return false;
            if (inst.state != KeyRodInstanceState.Active) return false;

            // Владеет ли клиент этим instanceId? Проверяется через InventoryWorld.
            return InventoryWorld.Instance != null
                && InventoryWorld.Instance.HasKeyInstance(clientId, instanceId);
        }

        public bool CanPlayerUse(ulong clientId, out string reason)
        {
            reason = "";
            if (IsOwner(clientId)) return true;

            var inst = KeyRodInstanceWorld.GetInstance(
                KeyRodInstanceWorld.GetInstanceIdForShip(NetworkObjectId));
            string ownerName = inst != null ? $"client#{inst.ownerPlayerId}" : "никого";
            reason = $"Нет ключа корабля (владелец: {ownerName})";
            return false;
        }
    }
}
```

### 2.3 MetaRequirementRegistry — расширение

```csharp
// Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs (дополнение)

public class MetaRequirementRegistry : NetworkBehaviour
{
    // Существующее:
    private readonly Dictionary<ulong, MetaRequirement> _requirements = new();
    public void RegisterRequirement(ulong netId, MetaRequirement req) { ... }
    public bool CanPlayerUse(ulong clientId, ulong netId) { ... }  // ← дополняется

    // Новое:
    private readonly Dictionary<ulong, ShipOwnershipRequirement> _ownershipRequirements = new();

    public void RegisterShipOwnership(ulong netId, ShipOwnershipRequirement req)
    {
        if (!IsServer) return;
        _ownershipRequirements[netId] = req;
    }

    public bool CanPlayerUse(ulong clientId, ulong netId)
    {
        if (!IsServer) return false;

        // 1) Ship ownership (приоритет для кораблей)
        if (_ownershipRequirements.TryGetValue(netId, out var ownership))
        {
            return ownership.CanPlayerUse(clientId, out _);
        }

        // 2) Fallback: MetaRequirement (для блоков/дверей)
        if (_requirements.TryGetValue(netId, out var req))
        {
            return req.CanPlayerUse(clientId, out _);
        }

        // 3) No requirement = allow (default)
        return true;
    }
}
```

### 2.4 Interaction с NetworkPlayer.F-key

`NetworkPlayer.SubmitSwitchModeRpc` сейчас вызывает `ShipKeyServer.CanPlayerBoard` (legacy alias). После T-KEY-06 это изменится на:

```csharp
// В NetworkPlayer.cs SubmitSwitchModeRpc (server side)
if (!_inShip)
{
    var ship = FindNearestShip();
    if (ship == null) return;
    if (!MetaRequirementRegistry.Instance.CanPlayerBoardShip(OwnerClientId, ship.NetworkObjectId))
    {
        return; // deny silently
    }
    // ... boarding logic
}
```

(`CanPlayerBoardShip` — новый wrapper, проверяет ShipOwnership перед MetaRequirement.)

### 2.5 F-key pre-check (client side)

```csharp
// В NetworkPlayer.cs Update (client side, owner)
if (Keyboard.current.fKey.wasPressedThisFrame && !_inShip)
{
    var nearestShip = FindNearestShip();
    if (nearestShip != null)
    {
        // Быстрая client-side проверка через ShipTelemetryClientState
        if (!ShipTelemetryClientState.Instance.IsMyShip(nearestShip.NetworkObjectId))
        {
            ShowKeyMissingToast(nearestShip.NetworkObjectId);
            return;
        }
        ShipKeyClientState.Instance.RequestCanBoard(nearestShip.NetworkObjectId);
    }
}
```

**Важно**: client-side проверка — только UX (toast без RPC). Server всё равно валидирует в `SubmitSwitchModeRpc`.

---

## 3. Сценарии

### 3.1 Игрок A подбирает ключ

```
1. KeyRodInstanceBinding.OnNetworkSpawn (на каждом [KeyRod_*] PickupItem, Q11)
   → KeyRodInstanceWorld.CreateInstance(itemId, shipNetId, ownerClientId=NONE)
   → instanceId=42 создан
2. Player A подбирает Pickup → InventoryServer.TryPickup
3. InventoryWorld.TryPickup добавляет slot (itemId, instanceId=42)
4. KeyRodInstanceWorld.TransferInstance(42, fromClientId=NONE, toClientId=0)
   (NONE = "в мире", не pickup игрока)
5. inst.ownerPlayerId = 0
6. inst.state = Active
```

### 3.2 Игрок A сел в корабль

```
1. A нажимает F → ShipKeyClientState.RequestCanBoard(shipNetId)
2. ShipKeyServer (legacy) → RequestCanBoardRpc → CanPlayerBoard
3. CanPlayerBoard → MetaRequirementRegistry.CanPlayerBoardShip(0, shipNetId)
4. → ShipOwnershipRequirement.IsOwner(0)
5. → KeyRodInstanceWorld.GetInstanceIdForShip(shipNetId) = 42
6. → InventoryWorld.HasKeyInstance(0, 42) = true
7. → allowed = true
8. Client → SubmitSwitchModeRpc (без RPC-проверки, т.к. уже прошли)
9. Server: defense-in-depth check → повторная CanPlayerUse → ok → boarding
```

### 3.3 Игрок B (без ключа) пытается сесть

```
1. B нажимает F → RequestCanBoardRpc(shipNetId)
2. → CanPlayerBoard → ShipOwnershipRequirement.IsOwner(B)
3. → InventoryWorld.HasKeyInstance(B, 42) = false
4. → allowed = false, reason = "Нет ключа корабля (владелец: client#0)"
5. Toast "Нет ключа корабля (владелец: client#0)"
6. SubmitSwitchModeRpc НЕ отправляется
```

### 3.4 Игрок A передаёт ключ игроку B

```
1. A: drop key → InventoryServer.TryDrop(slot with instanceId=42)
2. → InventoryWorld.TryDrop → удаляет slot, спавнит Pickup в мире
3. KeyRodInstanceWorld: state stays Active, instance stays в мире
   (но НЕ в чьём-то инвентаре — separate state "OnGround"?)
4. B: pickup → InventoryServer.TryPickup
5. → InventoryWorld добавляет slot (itemId, instanceId=42) в инвентарь B
6. KeyRodInstanceWorld.TransferInstance(42, from=NONE, toClientId=B)
7. inst.ownerPlayerId = B
8. ShipOwnershipRequirement.IsOwner(A) = false (больше нет ключа)
9. ShipOwnershipRequirement.IsOwner(B) = true
10. A выходит из корабля (если был пилотом) → SubmitSwitchModeRpc exit
```

**Тонкость**: пока A сидит в корабле, и ключ в инвентаре A, IsOwner(A)=true. Если A дропает ключ пока сидит — IsOwner(A)=false, корабль автоматически выгоняет A (`AutoDisembark` ticket — фаза 2).

---

## 4. Edge-cases

| Кейс | Решение |
|---|---|
| **Корабль уничтожен** | `KeyRodInstanceWorld.DestroyInstance(instanceId)` → state=Destroyed, ключ в инвентаре становится "мусором" (TODO: salvage) |
| **NetworkObjectId корабля изменился (server restart)** | T-KEY-04: `[KeyRodInstanceBinding]` хранит **ссылку на GameObject** корабля (resolved в `OnNetworkSpawn` через `GetComponent<NetworkObject>().NetworkObjectId`). T-KEY-PERSIST: при restore lookup по `(ownerPlayerId, itemId)` если NetID mismatch |
| **KeyRodInstance с itemId, который не в ItemDatabase** | `KeyRodInstanceWorld.CreateInstance` валидирует через `InventoryWorld.Instance.GetItemDefinition(itemId) != null` |
| **Instance создан, но Pickup не подобран** | `KeyRodInstance.state = Lost` (TODO в T-KEY-04) |
| **A передал ключ B, потом обратно** | TransferInstance(A → B → A) — каждый раз обновляется ownerPlayerId |
| **A и B одновременно F на корабль** | Server-side `RequestCanBoardRpc` — race handled существующим `MetaRequirementClientState.RequestCanUse` (timeout 1.5s, см. `00_OVERVIEW.md` §6.1) |
| **A в корабле, ключ у B (передан)** | Серверная проверка каждые N секунд (TODO в T-KEY-07): если пилот IsOwner=false → auto-disembark |

---

## 5. Что НЕ входит в MVP

- ❌ **Multiple keys per ship** (main + spare) — фаза 2
- ❌ **Auto-disembark при потере ключа** — фаза 2
- ❌ **Salvage/destroy instance flow** — фаза 2
- ❌ **Owner-history** (`originalOwnerId` записывается, но не отображается) — фаза 2
- ❌ **Pirate/steal flows** — фаза 2

---

## 6. Ссылки

- `20_UNIQUE_KEY_INSTANCE.md` §2.2 — KeyRodInstance struct
- `22_SHIP_TELEMETRY_PLAN.md` §2.4 — `ShipOwnershipRegistry` (NetworkList для синхронизации ownership клиентам)
- `23_ROADMAP.md` T-KEY-01..T-KEY-08 — тикеты
- `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — legacy compat
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs` — расширяемый registry
- `Assets/_Project/Scripts/Crafting/CraftingWorld.cs` — паттерн server-only static facade

---

**Обновлено:** 2026-06-18 — первичный дизайн.
**Обновлено:** 2026-06-18 — Q6: displayName берётся из ShipController._customDisplayName (см. `22_SHIP_TELEMETRY_PLAN.md` §2.3). Q4 cross-ref: ShipOwnershipRegistry синхронизируется через NetworkList.