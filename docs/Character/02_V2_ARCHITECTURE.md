# V2 Architecture — Character Progression

> **Дата:** 2026-06-14
> **Базируется на:** `docs/NPC_quests/02_V2_ARCHITECTURE.md` (v2 hub-pattern), `docs/Character-menu/10_DESIGN.md` (UI расширение)
> **v2-pattern:** server-side hub (NetworkBehaviour) + DTO (INetworkSerializable) + per-feature ClientState singleton + UI Toolkit UI fed by ClientState.

---

## 1. Общая архитектура подсистемы

```
┌──────────────────────────────────────────────────────────────────────────┐
│                      SERVER (authoritative)                              │
│                                                                          │
│  ┌────────────────┐    ┌────────────────┐    ┌────────────────┐          │
│  │ GatheringServer│    │ CraftingServer │    │ ExchangeServer │  ...     │
│  │  (existing)    │    │  (existing)    │    │  (existing)    │          │
│  └───────┬────────┘    └────────┬───────┘    └────────┬───────┘          │
│          │ Publish               │ Publish             │ Publish         │
│          │ MiningCompletedEvent  │ CraftingCompleted   │ Exchange...     │
│          ▼                       ▼                    ▼                 │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │                       WorldEventBus                              │    │
│  │  (Assets/_Project/Core/WorldEventBus.cs:23 — статический)         │    │
│  │  Publish/Subscribe/Unsubscribe — synchronous, type-routed          │    │
│  └──────────────────────────────────────────────────────────────────┘    │
│          ▲                       ▲                    ▲                 │
│          │ Subscribe             │ Subscribe          │ Subscribe        │
│          │                       │                    │                 │
│  ┌───────┴───────────────────────┴────────────────────┴─────────────┐    │
│  │  StatsServer (NEW — BootstrapScene, NetworkBehaviour)             │    │
│  │  OnNetworkSpawn: Subscribe 8 events + start FixedUpdate tracker   │    │
│  │  OnPlayerConnected: Send StatsSnapshotDto via TargetRPC           │    │
│  │  Per-tick: compute walk distance, pilot distance → add XP         │    │
│  │  Persists via JsonCharacterDataRepository on save events          │    │
│  └───────┬──────────────────────────────────────────────────────────┘    │
│          │ Persists (file per clientId)                                  │
│          ▼                                                               │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │  JsonCharacterDataRepository (NEW)                                │    │
│  │  File: character_<clientId>.json                                   │    │
│  │  Sections: Stats (3 floats + level), Equipment (slots), Skills    │    │
│  └──────────────────────────────────────────────────────────────────┘    │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
                                  │
                                  │ TargetRPC (NGO 2.x, SendTo.Owner)
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      CLIENT (per-player, replicated)                     │
│                                                                          │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐           │
│  │ StatsClientState│  │EquipmentClient  │  │SkillsClientState│           │
│  │  (singleton)    │  │State (singleton)│  │  (singleton)    │           │
│  │ OnStatsUpdated  │  │OnEquipChanged   │  │OnSkillsUpdated  │           │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘           │
│           │ subscribe          │ subscribe          │ subscribe          │
│           ▼                    ▼                    ▼                    │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │  CharacterWindow.cs (EXISTING — расширяем)                        │    │
│  │  + таб "ПРОГРЕССИЯ" → sub-tabs: [Статы] [Одежда] [Модули] [Навыки]│    │
│  │  SubscribeStats/UnsubscribeStats/HandleStatsSnapshotUpdated        │    │
│  │  RefreshStatsCache + RefreshEquipmentCache + RefreshSkillsCache    │    │
│  └──────────────────────────────────────────────────────────────────┘    │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 2. StatsServer — детальная архитектура

### 2.1 Расположение и lifecycle

**Файл (новый):** `Assets/_Project/Scripts/Stats/StatsServer.cs`
**Namespace:** `ProjectC.Stats`
**Паттерн:** NetworkBehaviour, scene-placed в BootstrapScene рядом с `[GatheringServer]`, `[CraftingServer]` и т.д.

```csharp
public class StatsServer : NetworkBehaviour {
    public static StatsServer Instance { get; private set; }

    // === Subscribe handles (для удобного Unsubscribe) ===
    private Action<MiningCompletedEvent> _handleMining;
    private Action<CraftingCompletedEvent> _handleCrafting;
    private Action<ExchangeCompletedEvent> _handleExchange;
    private Action<MarketTradedEvent> _handleMarket;
    private Action<QuestAcceptedEvent> _handleQuestAccepted;
    private Action<QuestCompletedEvent> _handleQuestCompleted;
    private Action<DialogVisitedEvent> _handleDialog;
    private Action<ShipPilotTickEvent> _handlePilotTick;
    private Action<PlayerJumpedEvent> _handleJumped;
    private Action<PlayerRunStateChangedEvent> _handleRunState;

    // === Distance tracker ===
    private Dictionary<ulong, Vector3> _lastPosPerPlayer = new();
    private Dictionary<ulong, float> _walkedDistanceXpBuffer = new();
    private const float DISTANCE_XP_THRESHOLD = 10f;  // +1 XP per 10m walked

    // === NPC-spam protection ===
    private Dictionary<ulong, Dictionary<string, float>> _lastDialogPerPlayerNpc = new();
    [SerializeField] private float _dialogXpCooldownSeconds = 60f;

    // === Lifecycle ===
    public override void OnNetworkSpawn() {
        if (!IsServer) return;
        Instance = this;

        _handleMining = OnMiningCompleted;        WorldEventBus.Subscribe(_handleMining);
        _handleCrafting = OnCraftingCompleted;    WorldEventBus.Subscribe(_handleCrafting);
        _handleExchange = OnExchangeCompleted;    WorldEventBus.Subscribe(_handleExchange);
        _handleMarket = OnMarketTraded;           WorldEventBus.Subscribe(_handleMarket);
        _handleQuestAccepted = OnQuestAccepted;   WorldEventBus.Subscribe(_handleQuestAccepted);
        _handleQuestCompleted = OnQuestCompleted; WorldEventBus.Subscribe(_handleQuestCompleted);
        _handleDialog = OnDialogVisited;          WorldEventBus.Subscribe(_handleDialog);
        _handlePilotTick = OnPilotTick;           WorldEventBus.Subscribe(_handlePilotTick);
        _handleJumped = OnJumped;                 WorldEventBus.Subscribe(_handleJumped);
        _handleRunState = OnRunState;             WorldEventBus.Subscribe(_handleRunState);
    }

    public override void OnNetworkDespawn() {
        WorldEventBus.Unsubscribe(_handleMining);     // ... mirror all 10
        if (Instance == this) Instance = null;
    }

    // === Server tick — distance tracking ===
    private void FixedUpdate() {
        if (!IsServer) return;
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds) {
            var player = NetworkManager.Singleton.ConnectedClients[clientId]?.PlayerObject;
            if (player == null) continue;
            var netPlayer = player.GetComponent<NetworkPlayer>();
            if (netPlayer == null || netPlayer.IsInShip) continue;
            // walked distance
            Vector3 currentPos = netPlayer.transform.position;
            if (_lastPosPerPlayer.TryGetValue(clientId, out var lastPos)) {
                float dist = Vector3.Distance(currentPos, lastPos);
                if (dist > 0f) AccumulateWalkedXp(clientId, dist);
            }
            _lastPosPerPlayer[clientId] = currentPos;
        }
    }
}
```

### 2.2 XP → Stats формула (геометрический рост)

**Файл:** `Assets/_Project/Scripts/Stats/StatGrowthConfig.cs` (SO)

```csharp
[CreateAssetMenu(menuName = "Project C/Stats/Stat Growth Config")]
public class StatGrowthConfig : ScriptableObject {
    [Header("Base XP per tier (XP_for_next_tier)")]
    [SerializeField, Min(1f)] private float _baseXp = 100f;

    [Header("Growth rate (geometric)")]
    [Tooltip("XP_for_next_tier = baseXp * (growthRate ^ currentTier)")]
    [SerializeField, Range(1.01f, 3.0f)] private float _growthRate = 1.5f;

    [Header("Global multiplier (for testing)")]
    [Tooltip("Applied to ALL XP gains. 1.0 = no change. Tunable for season/event buffs.")]
    [SerializeField, Range(0.01f, 10f)] private float _globalMultiplier = 1.0f;

    [Header("Per-stat multipliers (default 1.0)")]
    [SerializeField, Min(0f)] private float _strengthMultiplier = 1.0f;
    [SerializeField, Min(0f)] private float _dexterityMultiplier = 1.0f;
    [SerializeField, Min(0f)] private float _intelligenceMultiplier = 1.0f;

    // === Public API ===
    public float XpForNextTier(int currentTier) {
        if (currentTier < 0) return _baseXp;
        return _baseXp * Mathf.Pow(_growthRate, currentTier);
    }

    public float ApplyGlobalMultiplier(float xp) => xp * _globalMultiplier;
    public float ApplyStatMultiplier(StatType stat, float xp) {
        return xp * (stat switch {
            StatType.Strength => _strengthMultiplier,
            StatType.Dexterity => _dexterityMultiplier,
            StatType.Intelligence => _intelligenceMultiplier,
            _ => 1f,
        });
    }
}
```

### 2.3 PlayerStats struct + tier computation

**Файл:** `Assets/_Project/Scripts/Stats/PlayerStats.cs`

```csharp
public enum StatType : byte { Strength = 0, Dexterity = 1, Intelligence = 2 }

[Serializable]
public struct PlayerStats {
    public float strength;       // current XP within tier
    public float dexterity;
    public float intelligence;
    public int strengthTier;
    public int dexterityTier;
    public int intelligenceTier;
    public float strengthTotalXp;    // cumulative (for UI)
    public float dexterityTotalXp;
    public float intelligenceTotalXp;

    public static PlayerStats Default => new() {
        strength = 0, dexterity = 0, intelligence = 0,
        strengthTier = 0, dexterityTier = 0, intelligenceTier = 0,
        strengthTotalXp = 0, dexterityTotalXp = 0, intelligenceTotalXp = 0,
    };
}
```

**Tier promotion logic (server, в StatsServer.ApplyXp):**
```csharp
private void ApplyXp(ulong clientId, StatType stat, float xpAmount) {
    xpAmount = _config.ApplyGlobalMultiplier(xpAmount);
    xpAmount = _config.ApplyStatMultiplier(stat, xpAmount);
    var stats = _world.GetOrCreateStats(clientId);

    float currentXp = stat switch {
        StatType.Strength => stats.strength,
        StatType.Dexterity => stats.dexterity,
        StatType.Intelligence => stats.intelligence,
        _ => 0
    };
    int currentTier = stat switch {
        StatType.Strength => stats.strengthTier,
        StatType.Dexterity => stats.dexterityTier,
        StatType.Intelligence => stats.intelligenceTier,
        _ => 0
    };

    currentXp += xpAmount;

    // Tier promotion loop (no cap by design)
    while (currentXp >= _config.XpForNextTier(currentTier)) {
        currentXp -= _config.XpForNextTier(currentTier);
        currentTier++;
        // Optional: emit "level up" event for UI pulse / toast
        WorldEventBus.Publish(new StatTierUpEvent {
            PlayerId = clientId, StatType = stat, NewTier = currentTier
        });
    }

    // Update struct
    switch (stat) {
        case StatType.Strength:
            stats.strength = currentXp;
            stats.strengthTier = currentTier;
            stats.strengthTotalXp += xpAmount;
            break;
        // ... DEX, INT аналогично
    }
    _world.SetStats(clientId, stats);
    SendSnapshotToOwner(clientId);  // TargetRPC
}
```

### 2.4 Snapshot DTO (server → client)

**Файл:** `Assets/_Project/Scripts/Stats/Dto/StatsSnapshotDto.cs`

```csharp
[Serializable]
public struct StatsSnapshotDto : INetworkSerializable {
    public float strength;
    public float dexterity;
    public float intelligence;
    public int strengthTier;
    public int dexterityTier;
    public int intelligenceTier;
    public float strengthXpForNextTier;     // computed для UI прогресс-бар
    public float dexterityXpForNextTier;
    public float intelligenceXpForNextTier;
    public float strengthTotalXp;            // cumulative
    public float dexterityTotalXp;
    public float intelligenceTotalXp;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref strength);
        serializer.SerializeValue(ref dexterity);
        serializer.SerializeValue(ref intelligence);
        serializer.SerializeValue(ref strengthTier);
        serializer.SerializeValue(ref dexterityTier);
        serializer.SerializeValue(ref intelligenceTier);
        serializer.SerializeValue(ref strengthXpForNextTier);
        serializer.SerializeValue(ref dexterityXpForNextTier);
        serializer.SerializeValue(ref intelligenceXpForNextTier);
        serializer.SerializeValue(ref strengthTotalXp);
        serializer.SerializeValue(ref dexterityTotalXp);
        serializer.SerializeValue(ref intelligenceTotalXp);
    }
}
```

---

## 3. EquipmentServer — одежда и модули

### 3.1 Расположение и lifecycle

**Файл (новый):** `Assets/_Project/Scripts/Equipment/EquipmentServer.cs`
**Namespace:** `ProjectC.Equipment`

```csharp
public class EquipmentServer : NetworkBehaviour {
    public static EquipmentServer Instance { get; private set; }

    public override void OnNetworkSpawn() {
        if (!IsServer) return;
        Instance = this;
    }

    // === RPCs ===
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestEquipRpc(int itemId, EquipSlot slot, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!_world.TryEquip(clientId, itemId, slot, out var reason)) {
            SendEquipResult(clientId, EquipResult.Denied(reason));
            return;
        }
        SendEquipResult(clientId, EquipResult.Equipped(itemId, slot));
        SendSnapshotToOwner(clientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestUnequipRpc(EquipSlot slot, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!_world.TryUnequip(clientId, slot, out var reason)) {
            SendEquipResult(clientId, EquipResult.Denied(reason));
            return;
        }
        SendEquipResult(clientId, EquipResult.Unequipped(slot));
        SendSnapshotToOwner(clientId);
    }
}
```

### 3.2 EquipmentData struct (Dictionary → parallel arrays для NGO 2.x)

**Файл:** `Assets/_Project/Scripts/Equipment/EquipmentData.cs`

```csharp
public enum EquipSlot : byte {
    None = 0,
    Head = 1, Chest = 2, Legs = 3, Feet = 4, Back = 5, Hands = 6,
    Accessory1 = 7, Accessory2 = 8,
    WeaponMain = 9, WeaponOff = 10,
    Module1 = 20, Module2 = 21, Module3 = 22
}

[Serializable]
public struct EquipmentData : INetworkSerializable {
    // Fixed-size arrays для всех возможных слотов (1 byte per slot presence, int per slot itemId)
    public const int SLOT_COUNT = 13;  // 0..10 + 20..22, normalize to 0..12
    public byte[] slotOccupied;    // [0, 1] per slot
    public int[] slotItemIds;      // [0, itemId] per slot

    public static EquipmentData Empty => new() {
        slotOccupied = new byte[SLOT_COUNT],
        slotItemIds = new int[SLOT_COUNT],
    };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        // Serialize byte[] and int[] manually
        if (slotOccupied == null) slotOccupied = new byte[SLOT_COUNT];
        if (slotItemIds == null) slotItemIds = new int[SLOT_COUNT];
        for (int i = 0; i < SLOT_COUNT; i++) {
            serializer.SerializeValue(ref slotOccupied[i]);
            serializer.SerializeValue(ref slotItemIds[i]);
        }
    }

    public bool TryGetItemId(EquipSlot slot, out int itemId) {
        int idx = SlotToIndex(slot);
        if (idx < 0 || slotOccupied[idx] == 0) { itemId = 0; return false; }
        itemId = slotItemIds[idx];
        return true;
    }

    public void SetItem(EquipSlot slot, int itemId) {
        int idx = SlotToIndex(slot);
        slotOccupied[idx] = 1;
        slotItemIds[idx] = itemId;
    }

    public void ClearSlot(EquipSlot slot) {
        int idx = SlotToIndex(slot);
        slotOccupied[idx] = 0;
        slotItemIds[idx] = 0;
    }

    private static int SlotToIndex(EquipSlot slot) {
        int v = (int)slot;
        if (v == 0) return -1;  // None
        if (v <= 10) return v;  // Head..WeaponOff → 1..10
        if (v >= 20 && v <= 22) return 11 + (v - 20);  // Module1..3 → 11..13... wait, SLOT_COUNT=13
        return -1;
    }
}
```

### 3.3 Equip validation (skills check, slot match)

```csharp
public bool TryEquip(ulong clientId, int itemId, EquipSlot slot, out string reason) {
    reason = "";
    var playerStats = _statsWorld.GetStats(clientId);  // для skill check
    var playerSkills = _skillsWorld.GetLearnedSkills(clientId);  // для skill check
    var inventory = _inventoryWorld.GetInventory(clientId);  // для item ownership check

    // 1. Item exists?
    if (!InventoryWorld.Instance.GetItemDataById(itemId, out var itemData)) {
        reason = "Предмет не найден"; return false;
    }

    // 2. Item is clothing/module?
    if (itemData is not ClothingItemData clothing && itemData is not ModuleItemData module) {
        reason = "Этот предмет не надевается"; return false;
    }

    // 3. Slot match?
    EquipSlot requiredSlot = (itemData as ClothingItemData)?.slot ?? (itemData as ModuleItemData)?.slot ?? EquipSlot.None;
    if (requiredSlot != slot) {
        reason = $"Слот не подходит: нужен {requiredSlot}"; return false;
    }

    // 4. Skill requirements?
    var requiredSkills = clothing != null ? clothing.requiredSkills : module.requiredSkills;
    foreach (var skill in requiredSkills) {
        if (!playerSkills.Contains(skill.skillId)) {
            reason = $"Требуется навык: {skill.displayName}"; return false;
        }
    }

    // 5. Item ownership? (в инвентаре есть?)
    if (!inventory.ContainsItem(itemId)) {
        reason = "Предмета нет в инвентаре"; return false;
    }

    // 6. Slot empty?
    var equip = GetEquipment(clientId);
    if (equip.TryGetItemId(slot, out _)) {
        // Slot occupied → либо unequip старый, либо fail
        // MVP: fail (нужно сначала unequip)
        reason = "Слот занят, сначала снимите предмет"; return false;
    }

    // All checks passed
    equip.SetItem(slot, itemId);
    SetEquipment(clientId, equip);
    return true;
}
```

---

## 4. SkillsServer — навыки

### 4.1 Расположение и lifecycle

**Файл (новый):** `Assets/_Project/Skills/SkillsServer.cs`
**Namespace:** `ProjectC.Skills`

```csharp
public class SkillsServer : NetworkBehaviour {
    public static SkillsServer Instance { get; private set; }

    public override void OnNetworkSpawn() {
        if (!IsServer) return;
        Instance = this;
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestLearnSkillRpc(string skillId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!_world.TryLearnSkill(clientId, skillId, out var reason, out var snapshot)) {
            SendSkillResult(clientId, SkillResult.Denied(reason));
            return;
        }
        SendSkillResult(clientId, SkillResult.Learned(skillId));
        SendSnapshotToOwner(clientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestForgetSkillRpc(string skillId, RpcParams rpcParams = default) {
        // MVP: skills are permanent (no forget). Stub for future.
    }
}
```

### 4.2 SkillNodeConfig SO (per skill)

```csharp
public enum SkillCategory : byte { Social = 0, Combat = 1 }

[CreateAssetMenu(fileName = "Skill_", menuName = "Project C/Skill Node")]
public class SkillNodeConfig : ScriptableObject {
    [Header("Identity")]
    public string skillId;             // "social_diplomacy_1"
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Category")]
    public SkillCategory category;

    [Header("Prerequisites")]
    [Tooltip("All listed skills must be unlocked to learn this one.")]
    public SkillNodeConfig[] prerequisites = Array.Empty<SkillNodeConfig>();

    [Header("Effects (applied when learned)")]
    public SkillEffect[] effects = Array.Empty<SkillEffect>();

    [Header("XP cost to learn")]
    [Tooltip("XP spent from Intelligence stat pool to unlock this skill.")]
    [SerializeField, Min(0f)] private float _learnXpCost = 50f;

    [Header("Tier requirement (optional)")]
    [Tooltip("Minimum Intelligence tier required. 0 = no requirement.")]
    [SerializeField, Min(0)] private int _requiredIntelligenceTier = 0;

    // Public accessors
    public float LearnXpCost => _learnXpCost;
    public int RequiredIntelligenceTier => _requiredIntelligenceTier;
}

[Serializable]
public struct SkillEffect {
    public enum Type : byte {
        StatMod = 0,            // +X to STR/DEX/INT (multiplier or additive)
        AbilityUnlock = 1,      // unlocks a specific ability ID (for future weapons)
        PassiveEffect = 2,      // generic passive (future use)
    }
    public Type type;
    public StatType statType;    // только для StatMod
    public float floatValue;
    public string stringParam;   // ability id / passive id
}
```

### 4.3 SkillsWorld.TryLearnSkill — валидация prerequisites + spend XP

```csharp
public bool TryLearnSkill(ulong clientId, string skillId, out string reason, out SkillsSnapshotDto newSnapshot) {
    reason = "";
    newSnapshot = default;

    // 1. Skill exists?
    if (!_skillConfigsById.TryGetValue(skillId, out var skill)) {
        reason = "Навык не найден"; return false;
    }

    // 2. Already learned?
    if (_learnedPerPlayer[clientId].Contains(skillId)) {
        reason = "Навык уже изучен"; return false;
    }

    // 3. Prerequisites?
    foreach (var prereq in skill.prerequisites) {
        if (!_learnedPerPlayer[clientId].Contains(prereq.skillId)) {
            reason = $"Требуется: {prereq.displayName}"; return false;
        }
    }

    // 4. Intelligence tier requirement?
    var stats = _statsWorld.GetStats(clientId);
    if (stats.intelligenceTier < skill.RequiredIntelligenceTier) {
        reason = $"Требуется Интеллект тир {skill.RequiredIntelligenceTier}+"; return false;
    }

    // 5. XP cost (spend from Intelligence pool)?
    if (skill.LearnXpCost > 0) {
        if (stats.intelligence < skill.LearnXpCost) {
            reason = $"Не хватает XP (нужно {skill.LearnXpCost:F0})"; return false;
        }
        _statsWorld.ApplyXp(clientId, StatType.Intelligence, -skill.LearnXpCost);
    }

    // All checks passed
    _learnedPerPlayer[clientId].Add(skillId);
    ApplySkillEffects(clientId, skill);
    newSnapshot = BuildSnapshot(clientId);
    return true;
}
```

---

## 5. Client-side projection

### 5.1 StatsClientState

**Файл:** `Assets/_Project/Scripts/Stats/StatsClientState.cs`

```csharp
public class StatsClientState : MonoBehaviour {
    public static StatsClientState Instance { get; private set; }

    public StatsSnapshotDto? CurrentStats { get; private set; }
    public event Action<StatsSnapshotDto> OnStatsUpdated;
    public event Action<StatType, int> OnStatTierUp;  // для UI pulse

    private void Awake() { Instance = this; }

    public void OnStatsSnapshotReceived(StatsSnapshotDto snap) {
        var prevTier = CurrentStats.HasValue ? (CurrentStats.Value.strengthTier, ...) : (-1, ...);
        CurrentStats = snap;
        OnStatsUpdated?.Invoke(snap);
        // Detect tier-up для pulse
        if (prevTier.strength >= 0 && snap.strengthTier > prevTier.strength)
            OnStatTierUp?.Invoke(StatType.Strength, snap.strengthTier);
        // ... DEX, INT аналогично
    }
}
```

### 5.2 EquipmentClientState + SkillsClientState

Зеркалят StatsClientState по тому же pattern.

### 5.3 NetworkPlayer — Receive* TargetRPCs

```csharp
// В NetworkPlayer.cs (existing), добавить:

[Rpc(SendTo.Owner)]
public void ReceiveStatsSnapshotTargetRpc(StatsSnapshotDto snap, RpcParams rpcParams = default)
    => StatsClientState.Instance?.OnStatsSnapshotReceived(snap);

[Rpc(SendTo.Owner)]
public void ReceiveEquipmentSnapshotTargetRpc(EquipmentSnapshotDto snap, RpcParams rpcParams = default)
    => EquipmentClientState.Instance?.OnEquipmentSnapshotReceived(snap);

[Rpc(SendTo.Owner)]
public void ReceiveSkillsSnapshotTargetRpc(SkillsSnapshotDto snap, RpcParams rpcParams = default)
    => SkillsClientState.Instance?.OnSkillsSnapshotReceived(snap);
```

### 5.4 NetworkManagerController — auto-spawn client-states

```csharp
// В NetworkManagerController.Awake, добавить:

private void CreateStatsClientState() { /* по pattern CreateMetaRequirementClientState */ }
private void CreateEquipmentClientState() { /* same */ }
private void CreateSkillsClientState() { /* same */ }
```

---

## 6. WorldEventBus — новые события

### 6.1 Файл `WorldEvent.cs` — добавить 9 классов

```csharp
// === Stats events ===
public class MiningCompletedEvent : WorldEvent {
    public ulong PlayerId;
    public int ItemId;
    public string ItemName;
    public int Quantity;
    public bool IsDepleted;
}

public class CraftingCompletedEvent : WorldEvent {
    public ulong PlayerId;
    public ulong StationNetId;
    public string RecipeId;
    public string ResultItemName;
}

public class ExchangeCompletedEvent : WorldEvent {
    public ulong PlayerId;
    public byte Op;          // 0=Pack, 1=Unpack
    public int ItemId;
    public int Quantity;
}

public class MarketTradedEvent : WorldEvent {
    public ulong PlayerId;
    public byte Op;          // 0=Buy, 1=Sell
    public int ItemId;
    public int Quantity;
    public long NewCredits;
}

public class QuestAcceptedEvent : WorldEvent {
    public ulong PlayerId;
    public string QuestId;
}

public class QuestCompletedEvent : WorldEvent {
    public ulong PlayerId;
    public string QuestId;
}

public class ShipPilotTickEvent : WorldEvent {
    public ulong PlayerId;
    public float DeltaDistance;  // meters
}

public class PlayerJumpedEvent : WorldEvent {
    public ulong PlayerId;
}

public class PlayerRunStateChangedEvent : WorldEvent {
    public ulong PlayerId;
    public bool IsRunning;
}

// === Internal: tier up (для UI pulse / toast) ===
public class StatTierUpEvent : WorldEvent {
    public ulong PlayerId;
    public StatType StatType;
    public int NewTier;
}
```

### 6.2 Минимальные изменения в существующих серверах

| Файл | Изменение |
|------|-----------|
| `GatheringServer.cs:159` | После `SendGatherResultToClient(Completed)` добавить `WorldEventBus.Publish(new MiningCompletedEvent {...})` |
| `CraftingServer.cs:86-103` | В `OnCraftingTick`, когда `job.State` transition to `Completed` → publish |
| `ExchangeServer.cs:154` (Pack success) | После `_world.Pack(...) == success` → publish |
| `ExchangeServer.cs:214` (Unpack success) | То же |
| `MarketServer.cs:134` (Buy success) | После `r.IsSuccess` → publish |
| `MarketServer.cs:150` (Sell success) | То же |
| `QuestServer.cs:455` | В `OnWorldStageTransition`, публиковать `QuestAcceptedEvent` на `Discovered→Active`, `QuestCompletedEvent` на `Active→Completed` |
| `ShipController.cs FixedUpdate` | Добавить distance accumulator, publish `ShipPilotTickEvent` per pilot |
| `NetworkPlayer.cs` | + `SubmitJumpRpc`, + `NetworkVariable<bool> IsRunning`, + publish events |

---

## 7. UI integration — CharacterWindow расширение

(Подробно в `07_UI_TABS_IN_CHARACTER_WINDOW.md`.)

**Краткая суть:**
1. Добавляем 1 top-level таб "ПРОГРЕССИЯ" (вместе с 6 существующих = 7 top-level)
2. Внутри progression-section — sub-tabs: `[Статы] [Одежда] [Модули] [Навыки]` (через buttons в sub-row)
3. UXML: ~80 строк новых VisualElement'ов
4. USS: ~60 строк новых классов
5. CharacterWindow.cs: ~400 строк (4 row-factory + 4 SubscribeX/UnsubscribeX + 4 handler + расширение SwitchTab)

---

## 8. Pitfalls

### 8.1 Race condition WorldEventBus subscribers

**Проблема:** Если `StatsServer.OnNetworkSpawn` срабатывает ПОСЛЕ того как `InventoryWorld` опубликовал `ItemAddedEvent` для этого игрока → пропускаем событие.

**Решение:** В `OnNetworkSpawn` после Subscribe — отправляем текущий snapshot через TargetRPC, не только ждём новых событий. Это даёт double-safety (snapshot + deltas).

### 8.2 NPC dialog spam protection — clock skew

**Проблема:** `Time.realtimeSinceStartup` на server может дрейфовать между domain reloads.

**Решение:** Используем `NetworkManager.Singleton.ServerTime.Time` (double) для cooldown timestamps. Или храним timestamps как `long serverTicks` (1 tick = 1/60s).

### 8.3 EquipmentData slot count vs network bandwidth

**Проблема:** 13 slots × (byte + int) = 13 × 5 = 65 байт на snapshot. Для sync каждые 5 sec это 13 байт/сек/игрок — терпимо, но если 100 игроков на dedicated server = 1300 байт/сек.

**Решение:** Sync только on-change, не periodic. Deltas через `byte[13] dirtyMask` или просто "send full snapshot" если что-то equip/unequip.

### 8.4 JsonUtility не сериализует Dictionary

**Проблема:** `JsonCharacterDataRepository` хочет хранить EquipmentData, SkillsLearnedSet, Stats — но `JsonUtility` не работает с `Dictionary<>`.

**Решение:** Создаём parallel DTO `CharacterSaveData` с `List<int>` и `List<string>` полями (как `InventorySaveData` в `JsonInventoryRepository.cs:38-39`).

### 8.5 Skill tree prerequisites циклы

**Проблема:** A requires B, B requires A → бесконечный цикл при `TryLearnSkill`.

**Решение:**
1. **Static check в `OnValidate` SO** — warning при создании цикла
2. **Runtime check в `TryLearnSkill`** — visited set при обходе prerequisites
3. **Поле `maxDepth` в `SkillNodeConfig`** — `prerequisitesDepth > 5 → warning`

### 8.6 Atomic save vs crash

**Проблема:** `JsonCharacterDataRepository.Save` через `File.WriteAllText` — если crash посередине, файл corrupted.

**Решение:** Используем `tmp` + `Move` pattern из `JsonQuestStateRepository.cs:74-85`:
```csharp
var tmpPath = path + ".tmp";
File.WriteAllText(tmpPath, json);
if (File.Exists(path)) File.Delete(path);
File.Move(tmpPath, path);
```

### 8.7 Stats/XP не дублируются в разных местах

**Проблема:** Если XP хранится как `NetworkVariable` на NetworkPlayer + в `StatsWorld` + в `JsonCharacterDataRepository` — три источника истины, легко рассинхронизировать.

**Решение:** Один источник — `StatsWorld` (server). `JsonCharacterDataRepository` — только для persistence (загружается при connect, сохраняется при disconnect). `NetworkVariable` НЕ используем — periodic snapshot RPC достаточно.

### 8.8 CharacterWindow 9 tabs wrap

**Проблема:** 9 top-level tabs оборачиваются на 2 ряда (съедают ~44px chrome).

**Решение:** Вложенные sub-tabs под "ПРОГРЕССИЯ" — 7 top-level + 4 sub внутри progression-section.

---

## 9. Что НЕ делаем

- ❌ Не трогаем `InventoryData` — Equipment отдельно, не смешиваем с 8 `List<int>` полями
- ❌ Не используем `UnityEditor.Experimental.GraphView` в runtime
- ❌ Не пишем `.meta` / `.asmdef`
- ❌ Не добавляем 9 top-level tabs (вложенные sub-tabs)
- ❌ Не делаем `PlayerStats` как `NetworkVariable` на NetworkPlayer (snapshot RPC проще)
- ❌ Не забываем `_inShip` check в distance tracker (пилот не считается за DEX-walked-distance)
- ❌ Не используем `PlayerInputReader` (dead code) — пишем напрямую в `NetworkPlayer.Update`
- ❌ Не забываем NPC-spam cooldown для `DialogVisitedEvent`
