# 02 — V2-архитектура NPC + Quest + Reputation + Dialogue

> Источники: subagent reports по NPC, Input/UI, Trade/Inventory, Editor tooling.
> **Цель:** полная серверно-авторитарная архитектура, повторяющая v2-pattern
> (Market/Contract/Inventory/MetaRequirement) и расширяющая его.

---

## 2.1 Общая схема

```
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  Player (Client) │  │  Server (Host)   │  │  World State     │
└────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘
         │                     │                     │
         │ E-key (interact)   │                     │
         ↓                     │                     │
  InteractableManager         │                     │
  .FindNearestNpc             │                     │
         │                     │                     │
         ↓                     │                     │
  QuestInteractor              │                     │
  .RequestTalkToNpcRpc ───→ QuestServer (NetworkBehaviour, BootstrapScene)
         │                     │                     │
         │                     │ Validates:          │
         │                     │  - dist check       │
         │                     │  - rate limit       │
         │                     │  - NPC allowed      │
         │                     ↓                     │
         │              QuestWorld (POCO, source-of-truth)
         │              ├─ _questsByPlayer: Dict<clientId, List<QuestInstance>>
         │              ├─ _reputation: Dict<(clientId, factionId), int>
         │              └─ _dialogState: Dict<clientId, DialogSession>
         │                     │                     │
         │                     │ On dialogue action: │
         │                     ↓                     │
         │              DialogueActionExecutor      │
         │              ├─ OfferQuest ──→ QuestWorld.TryOffer
         │              ├─ GiveItem ────→ InventoryServer.AddItem
         │              ├─ TakeItem ────→ InventoryServer.TryRemove (NEW)
         │              ├─ GiveCredits ─→ Repository.SetCredits
         │              └─ AddReputation → ReputationWorld.Set
         │                     │                     │
         │ ←─── [Rpc(SendTo.Owner)] ReceiveDialogueStepDto, ReceiveQuestsSnapshotDto
         │                     │
         ↓                     │
  NetworkPlayer.ReceiveDialogueStepTargetRpc
         ↓
  QuestClientState.Instance.OnDialogueStep(snapshot)
         ↓
  DialogWindow.ShowStep(snapshot)
         │
         ↓
  UI Toolkit render + typewriter
```

---

## 2.2 Namespaces (план)

| Namespace | Типы | Зависимости |
|-----------|------|-------------|
| `ProjectC.Factions` | `FactionId` (enum, promoted из `NpcFaction`), `FactionDefinition : SO` | none (root) |
| `ProjectC.Reputation` | `ReputationDefinition : SO`, `ReputationClientState`, `NpcAttitudeClientState`, server-side state in `QuestWorld` | `Factions` |
| `ProjectC.Dialogue` | `DialogTree : SO`, `DialogueNode`, `DialogueEdge`, `DialogueCondition`, `DialogueAction` | none (data only) |
| `ProjectC.Quests` | Server `QuestServer` (NetworkBehaviour), `QuestWorld` (POCO), `QuestDefinition : SO`, `QuestStage`, `QuestObjective`, `QuestInstance` (POCO state), `QuestTriggerService` | `Factions`, `Dialogue`, `Reputation`, `ProjectC.Core` (WorldEventBus) |
| `ProjectC.Quests.Dto` | `QuestDto`, `QuestObjectiveDto`, `QuestSnapshotDto`, `QuestResultDto`, `QuestResultCode` (all `INetworkSerializable`) | `Quests` |
| `ProjectC.Quests.Client` | `QuestClientState` (MonoBehaviour, Instance singleton) | `Quests.Dto` |
| `ProjectC.Quests.UI` | `DialogWindow.uxml/uss`, `QuestTracker.uxml/uss`, `DialogWindow` code-behind | `Quests.Client` |
| `ProjectC.Quests.Triggers` | `IQuestTrigger`, `QuestTriggerService`, конкретные trigger'ы (`TalkedToNpcTrigger`, `HaveItemTrigger`, etc.) | `Quests`, `ProjectC.Core` |
| `ProjectC.Quests.Bridges` | `ContractMetaBridge` (подписывается на Contract events, обновляет quest objectives) | `Quests`, `Trade.Contract` |
| `ProjectC.Quests.Persistence` | `IQuestStateRepository`, `JsonQuestStateRepository`, `QuestSaveData` | `Quests`, `Reputation` |
| `ProjectC.Core` | `WorldEventBus` (singleton), `WorldEvent` base + `ItemAddedEvent`/`ItemRemovedEvent`/`ReputationChangedEvent`/`QuestStateChangedEvent`/`CustomEvent`/etc. | none (root) |
| `ProjectC.Editor.Quests` | `QuestDatabaseWindow` (EditorWindow), `QuestDatabase : SO` (registry), `QuestAssetWatcher` (AssetPostprocessor) | `Quests`, `Dialogue`, `Factions` |

**Корневая папка:** `Assets/_Project/Quests/` (новый — greenfield, см. `08_ROADMAP.md`).

---

## 2.3 ScriptableObject inventory

### 2.3.1 `FactionDefinition : SO`
```
[CreateAssetMenu("Project C/Factions/Faction Definition")]
fields:
  factionId: FactionId            // enum key (None..Neutral)
  displayName: string             // loc key
  color: Color                    // for badges in UI
  iconSprite: Sprite
  loreDescription: string
  defaultAttitude: FactionAttitude  // Friendly, Neutral, Hostile
  reputationThresholds: ReputationTier[]  // Friendly @ 50, Honored @ 100, etc.
```

### 2.3.2 `ReputationDefinition : SO`
```
[CreateAssetMenu("Project C/Quests/Reputation Definition")]
fields:
  factionId: FactionId
  min: int = -100, max: int = 100
  decayPerDay: float = 0           // optional
  tiers: ReputationTier[]          // label, threshold value, USS class for badge
```

### 2.3.3 `NpcDefinition : SO` (replaces `NpcData`)
```
[CreateAssetMenu("Project C/NPC Definition")]
fields:
  npcId: string
  displayName: string
  faction: FactionId
  portrait: Sprite
  prefab: GameObject               // visual mesh + animator
  animatorConfig: AnimatorConfig  // NEW: SO with trigger parameter names

  defaultDialogTree: DialogTree    // top-level, shared across NPCs OK

  // Per-NPC quest assignments
  questOffers: string[]            // questIds this NPC can Offer
  questTurnIns: string[]           // questIds this NPC can complete (objective "return to <npcId>")

  services: ServiceFlags           // [None, Trade, Repair, Refuel, Restock]

  // Visuals
  interactionRadius: float = 3.0
  greetingText: string             // loc key
  showGreeting: bool = true

  // Cross-faction influence (см. 09_OPEN_QUESTIONS.md §G, MVP stub)
  attitudeLinks: AttitudeLink[]    // { targetFaction, deltaOnLike, deltaOnDislike }

  // Personal relationship defaults (NpcAttitude стартовое значение)
  personalAttitudeMin: int = -100
  personalAttitudeMax: int = 200
```

### 2.3.3a `AttitudeLink` (POCO struct in NpcDefinition)
```
[Serializable]
public class AttitudeLink
{
    public FactionId targetFaction;        // какая фракция страдает/выигрывает
    public int deltaOnLike;                // +N когда NpcAttitude улучшается
    public int deltaOnDislike;             // -N когда NpcAttitude ухудшается
}
```

**Пример:** Mira (GuildOfThoughts) имеет `attitudeLink: { targetFaction: GuildOfCreation, deltaOnLike: -5, deltaOnDislike: +3 }`. Улучшил отношения с Мирой → GuildOfCreation rep -5. Ухудшил → +3.

### 2.3.4 `QuestDefinition : SO`
```
[CreateAssetMenu("Project C/Quests/Quest Definition")]
fields:
  questId: string
  displayName: string
  description: string              // loc key
  faction: FactionId?              // optional faction-gated
  minReputation: int = 0           // prerequisite

  stages: QuestStage[]             // ordered list

  rewards: QuestReward
    └─ credits, items[], reputation[], unlocks[]

  prerequisites: QuestPrerequisite[]
    └─ {type: QuestId|Reputation|Item|FactionFlag, ...}
```

### 2.3.5 `QuestStage` (POCO struct in QuestDefinition)
```
fields:
  stageId: string
  description: string              // loc key
  objectives: QuestObjective[]
  onEnterActions: DialogueAction[]   // fired once on entering stage
  onCompleteActions: DialogueAction[] // fired on completing this stage
  nextStage: string?               // null = quest end
```

### 2.3.6 `QuestObjective` (POCO struct)
```
fields:
  objectiveId: string
  type: QuestObjectiveType
    // TalkToNpc, DeliverItem, ReachLocation, KillEntity,
    // HaveItem, ReputationAtLeast, WaitForEvent, Custom
  description: string              // loc key

  // Type-specific params (only one is used per type)
  targetNpcId: string              // TalkToNpc
  itemId: string, quantity: int, toNpcId: string  // DeliverItem
  sceneId: string, position: Vector3  // ReachLocation
  entityType: string, count: int   // KillEntity
  itemId: string, quantity: int    // HaveItem
  factionId: FactionId, value: int // ReputationAtLeast
  eventId: string                  // WaitForEvent
  flagId: string                   // Custom

  optional: bool = false
  required: bool = true            // if false, doesn't block stage completion
```

### 2.3.7 `DialogTree : SO`
```
[CreateAssetMenu("Project C/Dialogue/Dialog Tree")]
fields:
  treeId: string
  displayName: string              // loc key
  rootNodeId: string
  nodes: DialogueNode[]            // flat list, edges reference nodeIds
  localizationTable: TextAsset?    // optional CSV/JSON loc
```

### 2.3.8 `DialogueNode` (POCO struct in DialogTree)
```
fields:
  nodeId: string
  speaker: SpeakerRef              // NpcId | "player" | "narrator"
  text: string                     // loc key
  portraitEmotion: string?         // optional, for portrait variant
  edges: DialogueEdge[]            // outgoing options
  onEnterActions: DialogueAction[] // fired when node is shown
```

### 2.3.9 `DialogueEdge` (POCO struct)
```
fields:
  label: string                    // player-visible response, loc key
  targetNodeId: string
  condition: DialogueCondition?    // if null, always available
  action: DialogueAction?          // fired on selection (before transition)
  hideIfUnavailable: bool = true   // or grey out
```

### 2.3.10 `DialogueCondition` (composite pattern)
```
type: ConditionType
  // Composite: And, Or, Not (with children: DialogueCondition[])
  // Atomic:
  //   HasItem(itemId, qty)
  //   QuestStateEquals(questId, QuestState)
  //   QuestStageEquals(questId, stageId)
  //   ReputationAtLeast(factionId, value)
  //   ReputationAtMost(factionId, value)
  //   TimeOfDayIn(phase)
  //   PlayerInZone(zoneId)
  //   FlagIsSet(flagId)
  //   WasNodeVisited(treeId, nodeId)
```

### 2.3.11 `DialogueAction` (composite pattern, like Condition)
```
type: ActionType
  // Composite: Sequence, Parallel (with children: DialogueAction[])
  // Atomic:
  //   OfferQuest(questId)
  //   CompleteObjective(questId, objectiveId)
  //   FailQuest(questId, reason)
  //   GiveItem(itemId, qty)              // to character inventory
  //   TakeItem(itemId, qty)              // from character inventory (uses InventoryServer.TryRemove)
  //   GiveCargoItem(itemId, qty)         // to ship cargo (uses CargoData.TryAdd)
  //   TakeCargoItem(itemId, qty)
  //   GiveCredits(amount)
  //   AddReputation(factionId, delta)
  //   OpenMarket(zoneId)
  //   OpenService(serviceId)
  //   SetFlag(flagId)
  //   EmitEvent(eventId)
  //   SwitchDialogTree(treeId)            // change active dialog for current NPC
  //   EndConversation
```

### 2.3.12 `WorldEvent` + `WorldEventBus` (full event bus)

**Решение D2 из `09_OPEN_QUESTIONS.md`** — **full bus**, не polling.

**`WorldEvent` base + подтипы** (см. `06_TRIGGERS_AND_INTEGRATION.md` §6.3):
```csharp
namespace ProjectC.Core
{
    public abstract class WorldEvent
    {
        public ulong PlayerId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Item subsystem
    public sealed class ItemAddedEvent : WorldEvent { public int ItemId; public int Count; public string TradeItemId; }
    public sealed class ItemRemovedEvent : WorldEvent { /* same */ }

    // Cargo
    public sealed class CargoAddedEvent : WorldEvent { public ulong ShipNetId; public string TradeItemId; public int Count; }
    public sealed class CargoRemovedEvent : WorldEvent { /* same */ }

    // Trade
    public sealed class ContractAcceptedEvent : WorldEvent { public string ContractId; public string FromNpcId; }
    public sealed class ContractCompletedEvent : WorldEvent { public string ContractId; public bool WasReceipt; }
    public sealed class ContractFailedEvent : WorldEvent { public string ContractId; public float DebtIncurred; }
    public sealed class ItemTradedEvent : WorldEvent { public string ItemId; public int Count; public bool IsBuy; }

    // Reputation
    public sealed class ReputationChangedEvent : WorldEvent { public FactionId Faction; public int NewValue; public int Delta; }
    public sealed class NpcAttitudeChangedEvent : WorldEvent { public string NpcId; public int NewValue; public int Delta; }

    // Quest
    public sealed class QuestStateChangedEvent : WorldEvent { public string QuestId; public byte OldState; public byte NewState; }
    public sealed class DialogVisitedEvent : WorldEvent { public string TreeId; public string NodeId; }

    // World
    public sealed class LocationReachedEvent : WorldEvent { public string SceneId; public Vector3 Position; public float Radius; }
    public sealed class DayNightPhaseChangedEvent : WorldEvent { public byte NewPhase; }
    public sealed class ShipDockedEvent : WorldEvent { public ulong ShipNetId; public string ZoneId; }

    // Custom
    public sealed class CustomEvent : WorldEvent { public string EventId; }
}
```

**`WorldEventBus` singleton** (в `ProjectC.Core`):
```csharp
public static class WorldEventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public static void Publish<T>(T ev) where T : WorldEvent
    {
        if (_subscribers.TryGetValue(typeof(T), out var list))
            for (int i = 0; i < list.Count; i++)
                ((Action<T>)list[i])?.Invoke(ev);
    }

    public static void Subscribe<T>(Action<T> handler) where T : WorldEvent
    {
        if (!_subscribers.TryGetValue(typeof(T), out var list))
        {
            list = new List<Delegate>();
            _subscribers[typeof(T)] = list;
        }
        list.Add(handler);
    }

    public static void Unsubscribe<T>(Action<T> handler) where T : WorldEvent
    {
        if (_subscribers.TryGetValue(typeof(T), out var list))
            list.Remove(handler);
    }

    public static void Reset() => _subscribers.Clear();  // для EditMode tests
}
```

**Publishers (хуки в серверы):**

| Сервер | Publishes |
|--------|-----------|
| `InventoryServer` | `ItemAddedEvent`, `ItemRemovedEvent` |
| `MarketServer` | `ItemTradedEvent` |
| `ContractServer` | `ContractAcceptedEvent`, `ContractCompletedEvent`, `ContractFailedEvent` |
| `QuestServer` | `QuestStateChangedEvent`, `ReputationChangedEvent`, `NpcAttitudeChangedEvent`, `DialogVisitedEvent` |
| `DayNightController` | `DayNightPhaseChangedEvent` |
| `ShipController` (future) | `ShipDockedEvent` |
| `PlayerChunkTracker` | `LocationReachedEvent` |

**Subscribers (Quest triggers):**

| Trigger | Subscribes to |
|---------|---------------|
| `TalkedToNpcTrigger` | `DialogVisitedEvent` |
| `HaveItemTrigger` | `ItemAddedEvent`, `ItemRemovedEvent` |
| `CargoHasItemTrigger` | `CargoAddedEvent`, `CargoRemovedEvent` |
| `ReputationAtLeastTrigger` | `ReputationChangedEvent` |
| `NpcAttitudeAtLeastTrigger` | `NpcAttitudeChangedEvent` |
| `EventDrivenTrigger` | `CustomEvent` |
| `DayNightPhaseTrigger` | `DayNightPhaseChangedEvent` |
| `ContractCompletedTrigger` | `ContractCompletedEvent` (через `ContractMetaBridge`) |
| `KilledEntityTrigger` | (stub, no publisher yet) |
| `LocationReachedTrigger` | `LocationReachedEvent` |

**Static singleton + `Reset()`** — для EditMode test isolation.

**См. `06_TRIGGERS_AND_INTEGRATION.md` §6.7 для правил "event vs polling" (в этой версии — всё event-driven).**

---

## 2.4 Server hub: `QuestServer` (NetworkBehaviour)

**Паттерн:** mirror `ContractServer.cs:26-412` line-for-line.

```csharp
// File: Assets/_Project/Quests/Network/QuestServer.cs
[RequireComponent(typeof(NetworkObject))]
public class QuestServer : NetworkBehaviour
{
    public static QuestServer Instance { get; private set; }

    [SerializeField] private QuestDatabase questDatabase;   // SO registry
    [SerializeField] private int maxActiveQuestsPerPlayer = 20;
    [SerializeField] private int maxOpsPerMinute = 30;
    [SerializeField] private bool debugMode = true;

    // C→S request RPCs (all SendTo.Server, Owner-only)
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestTalkToNpcRpc(string npcId, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestAdvanceDialogueRpc(
        string dialogTreeId, string currentNodeId, int optionIndex,
        string talkingToNpcId, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestAcceptQuestRpc(string questId, string fromNpcId, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestTurnInQuestRpc(string questId, string toNpcId, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestTrackQuestRpc(string questId, bool track, RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestRefreshQuestsRpc(RpcParams rpcParams = default);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestRefreshReputationRpc(RpcParams rpcParams = default);

    // S→C response RPCs (SendTo.Owner — routed through NetworkPlayer.ReceiveXxxTargetRpc)
    // (Note: in the project's v2 pattern, the actual TargetRpc lives on NetworkPlayer,
    //  the server calls it via `target.ReceiveQuestSnapshotTargetRpc(snapshot)`)
}
```

### Lifecycle (mirror ContractServer)

```csharp
public override void OnNetworkSpawn()
{
    if (Instance == null) Instance = this;
    if (!IsServer) { enabled = false; return; }

    // Reuse Trade's repository (already initialized by MarketServer)
    var repository = TradeWorld.Instance?.Repository ?? new PlayerPrefsRepository();
    QuestWorld.CreateAndInitialize(repository, questDatabase);
}

public override void OnNetworkDespawn()
{
    if (Instance == this) Instance = null;
    if (IsServer) QuestWorld.Instance?.SaveAll();
}
```

### Rate limiting (copy-paste ContractServer pattern)

```csharp
private readonly Dictionary<ulong, List<float>> _opTimestamps = new();
private bool CheckRateLimit(ulong clientId)
{
    if (!_opTimestamps.TryGetValue(clientId, out var timestamps))
    {
        timestamps = new List<float>();
        _opTimestamps[clientId] = timestamps;
    }
    timestamps.RemoveAll(t => Time.time - t > 60f);
    if (timestamps.Count >= maxOpsPerMinute) return false;
    timestamps.Add(Time.time);
    return true;
}
```

### Server-side state: `QuestWorld` (POCO)

```csharp
public class QuestWorld
{
    public static QuestWorld Instance { get; private set; }
    private QuestDatabase _database;
    private IPlayerDataRepository _repository;

    // Per-player quest state
    private Dictionary<ulong, List<QuestInstance>> _questsByPlayer = new();
    // Per-player reputation
    private Dictionary<(ulong, FactionId), int> _reputation = new();
    // Per-player active dialog session
    private Dictionary<ulong, DialogSession> _dialogByPlayer = new();
    // World flags (key-value)
    private HashSet<string> _worldFlags = new();

    public QuestResultCode TryOfferQuest(ulong clientId, string questId, string fromNpcId);
    public QuestResultCode TryTurnInQuest(ulong clientId, string questId, string toNpcId);
    public QuestResultCode TryAdvanceObjective(ulong clientId, string questId, string objectiveId);
    public QuestResultCode EvaluateDialogueAction(ulong clientId, DialogueAction action);

    // Reputation
    public int GetReputation(ulong clientId, FactionId faction);
    public void ModifyReputation(ulong clientId, FactionId faction, int delta);

    // Persistence
    public void SavePlayer(ulong clientId);
    public void LoadPlayer(ulong clientId);
    public void SaveAll();
}
```

---

## 2.5 DTOs (INetworkSerializable)

### `QuestDto` (per-quest row)
```csharp
public struct QuestDto : INetworkSerializable
{
    public FixedString64Bytes questId;
    public FixedString128Bytes displayName;
    public byte state;              // QuestState enum byte
    public FixedString64Bytes currentStageId;
    public int stageProgress;       // X of Y objectives in current stage
    public float timeRemaining;     // for timed quests (0 = no limit)
    public FixedString64Bytes trackedNpcId;  // optional
}
```

### `QuestObjectiveDto`
```csharp
public struct QuestObjectiveDto : INetworkSerializable
{
    public FixedString64Bytes objectiveId;
    public FixedString128Bytes description;
    public byte type;               // QuestObjectiveType enum byte
    public int currentCount;        // for "collect 5/10" type
    public int requiredCount;
    public bool completed;
    public bool optional;
}
```

### `QuestSnapshotDto` (all my quests)
```csharp
public struct QuestSnapshotDto : INetworkSerializable
{
    public ulong playerClientId;
    public FixedString64Bytes trackedQuestId;   // empty if none tracked
    public QuestDto[] activeQuests;
    public QuestDto[] completedQuests;
    public QuestDto[] failedQuests;

    // Hand-rolled NetworkSerialize with IsWriter/IsReader branches
    // (see ContractSnapshotDto.cs:62-77 for the array pattern)
    // (see ContractResultDto.cs:60-90 for the Nullable<T> pitfall workaround)
}
```

### `QuestResultDto` + `QuestResultCode`
```csharp
public enum QuestResultCode : byte
{
    Ok = 0,
    NotInZone = 1,
    NpcNotFound = 2,
    NpcNotInteractable = 3,
    QuestNotFound = 4,
    QuestAlreadyActive = 5,
    QuestNotActive = 6,
    QuestPrerequisitesNotMet = 7,
    QuestStageLocked = 8,
    QuestAlreadyCompleted = 9,
    ObjectiveNotFound = 10,
    ItemMissing = 11,
    CargoFull = 12,
    InventoryFull = 13,
    ReputationTooLow = 14,
    ReputationTooHigh = 15,
    RateLimited = 16,
    NotHost = 17,
    InternalError = 18,
    DialogueInvalidOption = 19,
    DialogueConditionFailed = 20,
    DialogueActionFailed = 21,
}

public struct QuestResultDto : INetworkSerializable
{
    public byte code;               // QuestResultCode
    public FixedString128Bytes message;  // pre-localized on server
    public FixedString64Bytes contextQuestId;
    public FixedString64Bytes contextNpcId;
    public bool success;            // convenience: code == Ok

    // Optional: updated quest (nullable — uses hand-rolled pattern from ContractResultDto.cs:60-90)
    public bool hasUpdatedQuest;
    public QuestDto updatedQuest;   // only valid if hasUpdatedQuest

    // NetworkSerialize with manual IsWriter/IsReader branches
}
```

### `DialogueStepDto` (current state of conversation)
```csharp
public struct DialogueStepDto : INetworkSerializable
{
    public FixedString64Bytes dialogTreeId;
    public FixedString64Bytes currentNodeId;
    public FixedString64Bytes speakerNpcId;
    public FixedString128Bytes speakerName;
    public FixedString512Bytes text;        // pre-resolved on server (localized)
    public byte[] portraitRef;              // sprite reference (asset GUID hash)

    public DialogueOptionDto[] options;     // available options (filtered by conditions)

    public bool isConversationEnd;
}

public struct DialogueOptionDto : INetworkSerializable
{
    public FixedString128Bytes label;
    public int optionIndex;                 // matches the array index for RPC
    public bool isAvailable;                // false if condition failed
    public byte reputationTint;             // 0=neutral, 1=positive, 2=negative (for outline color)
    public FixedString64Bytes hintIfUnavailable;  // tooltip text
}
```

### `ReputationSnapshotDto`
```csharp
public struct ReputationSnapshotDto : INetworkSerializable
{
    public ulong playerClientId;
    public ReputationEntryDto[] entries;

    public struct ReputationEntryDto : INetworkSerializable
    {
        public byte factionId;               // FactionId enum byte
        public int value;
        public byte tier;                    // ReputationTier enum byte
    }
}
```

### Pitfall: `Nullable<T>` не сериализуется NGO 2.x

**Известный workaround** (см. `ContractResultDto.cs:60-90`):
```csharp
public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
{
    serializer.SerializeValue(ref code);
    serializer.SerializeValue(ref message);
    // ...
    if (serializer.IsWriter)
    {
        bool has = updatedQuest.HasValue;  // WRITER path: HasValue is valid
        serializer.SerializeValue(ref has);
        if (has) { var q = updatedQuest.Value; q.NetworkSerialize(serializer); }
    }
    else
    {
        bool has = false;
        serializer.SerializeValue(ref has);
        if (has)
        {
            var q = default(QuestDto);
            q.NetworkSerialize(serializer);
            updatedQuest = q;
        }
        else { updatedQuest = null; }
    }
}
```

**Все DTO с `?` (nullable) полями ОБЯЗАНЫ использовать этот паттерн.**

---

## 2.6 Client state projection: `QuestClientState`

**Паттерн:** mirror `ContractClientState.cs:20-49` (plain `MonoBehaviour`, не `NetworkBehaviour`).

```csharp
public class QuestClientState : MonoBehaviour
{
    public static QuestClientState Instance { get; private set; }

    public QuestSnapshotDto? CurrentQuestsSnapshot { get; private set; }
    public DialogueStepDto? CurrentDialogueStep { get; private set; }
    public ReputationSnapshotDto? CurrentReputation { get; private set; }
    public QuestResultDto? LastResult { get; private set; }

    // Events (UI subscribes)
    public event Action<QuestSnapshotDto> OnSnapshotUpdated;
    public event Action<DialogueStepDto> OnDialogueStep;
    public event Action<QuestResultDto> OnQuestResult;
    public event Action<ReputationSnapshotDto> OnReputationUpdated;

    // Auto-spawned in NetworkManagerController.Awake (DontDestroyOnLoad)
    private void Awake()
    {
        if (Instance == null) Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Called by NetworkPlayer.ReceiveXxxTargetRpc (server-routed)
    public void OnSnapshotReceived(QuestSnapshotDto snapshot) { ... }
    public void OnDialogueStepReceived(DialogueStepDto step) { ... }
    public void OnQuestResultReceived(QuestResultDto result) { ... }
    public void OnReputationReceived(ReputationSnapshotDto rep) { ... }

    // Convenience methods (called by UI / Interactor)
    public void RequestTalkToNpc(string npcId) { ... }
    public void RequestAdvanceDialogue(string treeId, string nodeId, int optionIndex, string npcId) { ... }
    public void RequestAcceptQuest(string questId, string fromNpcId) { ... }
    public void RequestTurnInQuest(string questId, string toNpcId) { ... }
    public void RequestTrackQuest(string questId, bool track) { ... }
    public void RequestRefreshQuests() { ... }
    public void RequestRefreshReputation() { ... }
}
```

---

## 2.7 `QuestInteractor` (RPC sender)

`MarketInteractor`-style: тонкая обёртка над `QuestClientState.Request*` методами + автоматическая конвертация `string npcId` → `NetworkObject` reference (через `FindNearestNpc`).

```csharp
public class QuestInteractor : MonoBehaviour
{
    [SerializeField] private float talkRange = 5f;

    // Called from NetworkPlayer E-key pipeline (NEW branch)
    public bool TryTalkToNpc()
    {
        var npc = InteractableManager.FindNearestNpc(transform.position, talkRange);
        if (npc == null) return false;
        QuestClientState.Instance?.RequestTalkToNpc(npc.GetNpcData().npcId);
        return true;
    }
}
```

---

## 2.8 Trigger system: `IQuestTrigger`

**MVP решение:** polling в `QuestWorld.Tick()` (server `FixedUpdate`). Server-сторона имеет полный доступ к `InventoryWorld.CountOf(playerId, itemId)`, `ReputationWorld.Get`, etc.

**v2 решение (optional):** server-side event bus `event Action<WorldEvent> OnWorldEvent`, fires from all `*Server` classes.

```csharp
public interface IQuestTrigger
{
    string TriggerId { get; }
    bool IsSatisfied(QuestInstance instance, ulong playerId);
    void OnAttach(QuestInstance instance);  // optional: subscribe to event bus
    void OnDetach(QuestInstance instance);
}

public sealed class QuestTriggerService
{
    private readonly Dictionary<string, IQuestTrigger> _triggers = new();
    public void Register(IQuestTrigger trigger);
    public void Unregister(string triggerId);
    public void EvaluateAll(ulong playerId, WorldEvent ev);
}
```

**Конкретные triggers:**
- `TalkedToNpcTrigger` — срабатывает после `QuestServer.RequestAdvanceDialogueRpc` (server knows which NPC was talked to).
- `ItemInInventoryTrigger` — poll `InventoryWorld.CountOf` в `Tick()` (каждые 5 сек, не каждый frame).
- `CargoFullTrigger` — check `CargoData` для `DeliverItem` objectives.
- `ReputationAtLeastTrigger` — срабатывает в `ReputationWorld.Set` (immediate).
- `DayNightPhaseTrigger` — poll `DayNightController.CurrentPhase` каждые 30 сек.
- `LocationReachedTrigger` — poll `PlayerChunkTracker` или `NetworkPlayer.transform.position` каждые 5 сек.
- `EventTrigger` — explicit `EmitEvent(eventId)` → server fires `EvaluateAll`.

**See `06_TRIGGERS_AND_INTEGRATION.md` for full design.**

---

## 2.9 Persistence: IPlayerDataRepository

**Reuse** existing `IPlayerDataRepository` (`Trade/Scripts/Repository/IPlayerDataRepository.cs`):
- `PlayerPrefsRepository` (default, host-only)
- `ServerFileRepository` (P1, JSON files per-player)

**New methods to add:**
```csharp
// In IPlayerDataRepository.cs
bool TryGetQuests(ulong clientId, out QuestSaveData data);
void SetQuests(ulong clientId, QuestSaveData data);
bool TryGetReputation(ulong clientId, out ReputationSaveData data);
void SetReputation(ulong clientId, ReputationSaveData data);
bool TryGetWorldFlags(out HashSet<string> flags);
void SetWorldFlags(HashSet<string> flags);
```

**Or alternative: parallel repository** `IQuestStateRepository` (cleaner separation, no impact on Trade):
```csharp
public interface IQuestStateRepository
{
    QuestSaveData LoadQuests(ulong clientId);
    void SaveQuests(ulong clientId, QuestSaveData data);
    ReputationSaveData LoadReputation(ulong clientId);
    void SaveReputation(ulong clientId, ReputationSaveData data);
    // ...
}
```

**Рекомендация:** `IQuestStateRepository` как параллельный, чтобы не модифицировать Trade-контракт (Trade = stable, расширять нельзя без user-approval).

**Save policy:** на каждое state change (как ContractServer, но Trade там не сохраняет — это gap). Серверный `QuestWorld.SavePlayer(clientId)` синхронно пишет JSON. Для перформанса — дебаунс 1 сек на write per player.

---

## 2.10 InventoryService.TryRemove (новый)

**Проблема:** `InventoryServer` (см. `Items/Network/InventoryServer.cs`) имеет только:
- `RequestPickupRpc` / `RequestDropRpc` / `RequestMoveRpc` / `RequestUseRpc` / `RequestRefreshRpc` (player-initiated)
- `AddItem(ulong clientId, int itemId, ItemType itemType)` (server-only, used by chest rewards)

**Нет:** `TryRemove(ulong clientId, int itemId, int count) → InventoryResultDto` — для quest turn-in'а.

**Решение:** добавить в `InventoryServer`:
```csharp
public InventoryResultCode TryRemove(ulong clientId, int itemId, int count)
{
    if (!IsServer) return InventoryResultCode.NotServer;
    if (!ValidateItemExists(itemId)) return InventoryResultCode.ItemNotFound;
    if (InventoryWorld.Instance.CountOf(clientId, itemId) < count) return InventoryResultCode.NotEnough;

    // Remove from InventoryData
    var inventory = InventoryWorld.Instance.GetOrLoadInventory(clientId);
    int removed = inventory.RemoveItems(itemId, count);  // NEW method on InventoryData
    if (removed < count) return InventoryResultCode.InternalError;

    // Send snapshot
    SendSnapshot(clientId, null);
    return InventoryResultCode.Ok;
}
```

**Similar gap exists for CargoData:** `TryRemove(string itemId, int count)` уже есть (`CargoData.cs:73`). OK.

**И в Trade warehouse:** `TryRemove` уже есть (`Warehouse.cs:94`). OK.

**Decision:** **Только InventoryServer.TryRemove — новый метод.** Cargo и Warehouse уже OK.

---

## 2.11 Item-system reconcile (Quest vs Inventory vs Trade)

**Проблема:** 2 parallel item systems:
- `TradeItemDefinition` (string id, для economy/warehouses/cargo)
- `ItemData` (int id, для character inventory, generated by `ItemDatasetGenerator`)

**Где какую использовать для квестов:**

| Квест операция | System | Why |
|---|---|---|
| "Принеси мне `<X>` в личном инвентаре" | `ItemData` + `InventoryServer` | character inventory, `CountOf` server-side |
| "Доставь груз `<X>` на корабле в порт Y" | `TradeItemDefinition` + `CargoData` | ship cargo, persisted |
| "Положи `<X>` в warehouse `Z`" | `TradeItemDefinition` + `Warehouse` | per-location warehouse, persisted |
| "Заплати `<N>` кредитов" | `IPlayerDataRepository.SetCredits` | credits are separate from items |

**Квесты используют ОБЕ системы.** `QuestObjective` имеет два optional поля:
- `string itemId` (для Trade — warehouses, cargo)
- `int itemDataId` (для Items — character inventory, через `InventoryWorld.GetOrRegisterItemId(itemData)`)

**Это раздражает, но reflect'ит существующее состояние проекта.** См. вопрос #4 в `09_OPEN_QUESTIONS.md` — можно ли унифицировать.

---

## 2.12 Faction system reconcile

**Проблема:** 2 faction-концепта:
- `NpcFaction` (12 lore values) в `World/Npc/NpcData.cs:9-23` (GuildOfThoughts, GuildOfCreation, ..., Pirates, Neutral, None)
- `Faction` (для item licensing) в `Trade.Config` (`TradeItemDefinition.requiredFaction`)

**Решение v2:** **promote `NpcFaction` → `FactionId` в `ProjectC.Factions` namespace** как authoritative enum. `TradeItemDefinition` переименовывает свой `Faction` поле → `FactionId requiredFaction`. Trade-файлы: тикет **T-X2**, требует design discussion (см. `08_ROADMAP.md` §8.3 T-X2 + §8.0 «DEFERRED»).

**`FactionDefinition : SO`** садится рядом с `FactionId` enum, индексируется через `FactionDatabase : SO` registry.

---

## 2.13 Animator integration

**Проблема v1:** magic strings "Idle"/"Walk"/"Talk" в `NpcEntity.UpdateAnimation` (line 275-291).

**Решение v2:** `AnimatorConfig : SO`:
```
fields:
  animatorController: RuntimeAnimatorController
  triggerIdle: string = "Idle"
  triggerWalk: string = "Walk"
  triggerTalk: string = "Talk"
  triggerEmotion: string = "Emotion"     // for portraitEmotion integration
  emotionParameter: string = "EmotionIndex"  // int param
```

`NpcDefinition.animatorConfig` ссылается на SO. `NpcEntity.SetState(NpcState)` использует `animatorConfig.triggerXxx` вместо magic string.

---

## 2.14 Pitfall-лист (для следующей сессии)

| # | Pitfall | Источник |
|---|---------|---------|
| 1 | `NpcEntity` (NetworkBehaviour) требует `ScenePlacedObjectSpawner` если в BootstrapScene руками | AGENTS.md §Scene architecture |
| 2 | `Nullable<T>` в `INetworkSerializable` — hand-rolled workaround | `ContractResultDto.cs:60-90` |
| 3 | No `NetworkList` для quest state — use RPC+DTO | `ContractServer.cs`, `InventoryServer.cs` — нигде нет NetworkList |
| 4 | Все singleton'ы (ClientState) — `Awake` auto-spawn в `NetworkManagerController.Awake`, `DontDestroyOnLoad` | `ContractClientState.cs:38-49` |
| 5 | 4 FIX'ы для UI Toolkit windows (pickingMode, Position, fallback, MarkDirtyRepaint) | `CharacterWindow.cs:295-304, 441-445` |
| 6 | `Cursor.lockState = Locked` on close, `None` on open | `CharacterWindow.cs:1228-1241` |
| 7 | Shared labels across tabs (`_creditsLabel`, `_messageLabel`) — always update, gate rebuilds on `_activeTab` | `project-c-ui-as-tab` skill pitfall #32-35 |
| 8 | No `.meta`/`.asmdef` writes | AGENTS.md |
| 9 | Server-authoritative: server saves, client renders | v2 pattern |
| 10 | Cross-cache refresh: `OnXxxSnapshotUpdated` — refresh cache unconditionally, gate rebuilds on active tab | `project-c-ui-as-tab` R3-005 lesson |
