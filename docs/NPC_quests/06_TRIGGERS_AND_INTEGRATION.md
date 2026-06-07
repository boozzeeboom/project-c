# 06 — Trigger System & Cross-Subsystem Integration

> **Цель:** квест должен "знать" о мире (что у игрока в инвентаре, где корабль,
> какой фракции репутация, какое время суток). Без trigger system квесты —
> декоративные. Тут — design event-bus'а и интеграции.

---

## 6.1 Что должен уметь trigger

**Quest trigger** = условие, которое может стать satisfied в любой момент
runtime и продвинуть квест.

**Примеры (из задания):**
- "Поговори с NPC X" — `TalkedToNpcTrigger`.
- "Принеси предмет Y NPC Z" — `HaveItemTrigger` + `TalkedToNpcTrigger`.
- "Достигни локации L" — `LocationReachedTrigger`.
- "Событие мира E произошло" — `EventTrigger`.

**Дополнительные из GDD:**
- "Убей N мобов типа T" — `KilledEntityTrigger` (combat — будущее).
- "Имей репутацию ≥ R с фракцией F" — `ReputationAtLeastTrigger`.
- "Дождись времени суток P" — `DayNightPhaseTrigger`.
- "Дождись дня X реального времени" — `RealTimeTrigger`.
- "Имей предмет Q в cargo корабля" — `CargoHasItemTrigger`.
- "Корабль пристыкован в порту P" — `ShipDockedAtTrigger`.

---

## 6.2 IQuestTrigger interface

```csharp
namespace ProjectC.Quests.Triggers
{
    public interface IQuestTrigger
    {
        string TriggerId { get; }                       // unique key, used for dedup
        bool IsSatisfied(QuestInstance instance, ulong playerId);
        void OnAttach(QuestInstance instance);          // optional: subscribe to event bus
        void OnDetach(QuestInstance instance);          // optional: unsubscribe
    }
}
```

**Не нужен** `Update` метод — polling в `QuestWorld.Tick()` (server `FixedUpdate`, раз в 5 сек).

**Не нужен** `OnTriggered` event — `QuestWorld.Evaluate(playerId, triggerId)` сам решит, что делать.

---

## 6.3 QuestTriggerService

```csharp
namespace ProjectC.Quests.Triggers
{
    public sealed class QuestTriggerService
    {
        private readonly Dictionary<ulong, List<IQuestTrigger>> _playerTriggers = new();
        private readonly Dictionary<string, Func<IQuestTrigger>> _triggerFactories = new();
        private readonly QuestWorld _world;

        public QuestTriggerService(QuestWorld world) { _world = world; }

        public void RegisterTriggerType(string typeId, Func<IQuestTrigger> factory)
        {
            _triggerFactories[typeId] = factory;
        }

        public void Attach(ulong playerId, QuestInstance instance, string triggerTypeId)
        {
            if (!_triggerFactories.TryGetValue(triggerTypeId, out var factory)) return;
            var trigger = factory();
            trigger.OnAttach(instance);
            if (!_playerTriggers.TryGetValue(playerId, out var list))
            {
                list = new List<IQuestTrigger>();
                _playerTriggers[playerId] = list;
            }
            list.Add(trigger);
        }

        public void Detach(ulong playerId, QuestInstance instance)
        {
            if (!_playerTriggers.TryGetValue(playerId, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i].OnDetach(instance);
                list.RemoveAt(i);
            }
        }

        // Called by QuestWorld.Tick() or by event-bus subscribers
        public void Evaluate(ulong playerId, string triggerIdHint = null)
        {
            if (!_playerTriggers.TryGetValue(playerId, out var list)) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var trigger = list[i];
                if (triggerIdHint != null && trigger.TriggerId != triggerIdHint) continue;

                // Iterate active quests for this player
                foreach (var quest in _world.GetActiveQuests(playerId))
                {
                    if (trigger.IsSatisfied(quest, playerId))
                    {
                        _world.TryAdvanceObjective(playerId, quest.questId, trigger.TriggerId);
                    }
                }
            }
        }

        // World event broadcast (from any server system)
        public void OnWorldEvent(WorldEvent ev)
        {
            // For all players, evaluate relevant triggers
            // (e.g. OnItemAdded event → re-evaluate HaveItem triggers)
            if (ev is ItemAddedEvent iae) Evaluate(iae.PlayerId, $"HaveItem:{iae.ItemId}");
            // ...
        }
    }
}
```

**`WorldEvent`** = простой tagged union:

```csharp
public abstract class WorldEvent
{
    public ulong PlayerId { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class ItemAddedEvent : WorldEvent
{
    public int ItemId;     // ItemData (int)
    public int Count;
    public string TradeItemId;  // TradeItemDefinition (string)
}

public sealed class ItemRemovedEvent : WorldEvent { /* same */ }

public sealed class CargoAddedEvent : WorldEvent { /* shipNetId + itemId + count */ }

public sealed class CargoRemovedEvent : WorldEvent { /* same */ }

public sealed class NpcTalkedEvent : WorldEvent
{
    public string NpcId;
    public string DialogTreeId;
    public string NodeId;
}

public sealed class ReputationChangedEvent : WorldEvent
{
    public FactionId Faction;
    public int NewValue;
    public int Delta;
}

public sealed class LocationReachedEvent : WorldEvent
{
    public string SceneId;
    public Vector3 Position;
    public float Radius;
}

public sealed class DayNightPhaseChangedEvent : WorldEvent
{
    public DayNightPhase NewPhase;
}

public sealed class ShipDockedEvent : WorldEvent
{
    public ulong ShipNetId;
    public string ZoneId;
}

public sealed class CustomEvent : WorldEvent
{
    public string EventId;
}
```

---

## 6.4 Конкретные trigger implementations

### `TalkedToNpcTrigger`
```csharp
public sealed class TalkedToNpcTrigger : IQuestTrigger
{
    public string TargetNpcId { get; set; }
    public string TriggerId => $"TalkedToNpc:{TargetNpcId}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        return _world.HasNpcTalkedTo(playerId, TargetNpcId);
    }
}
```

**Set в** `QuestServer.RequestAdvanceDialogueRpc` после успешного advance.

### `HaveItemTrigger`
```csharp
public sealed class HaveItemTrigger : IQuestTrigger
{
    public int ItemDataId { get; set; }    // ItemData (int)
    public int RequiredQuantity { get; set; }
    public string TriggerId => $"HaveItem:{ItemDataId}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        return InventoryWorld.Instance.CountOf(playerId, ItemDataId) >= RequiredQuantity;
    }
}
```

**Не подписывается** на event bus — poll каждые 5 сек в `QuestWorld.Tick()`.

### `CargoHasItemTrigger`
```csharp
public sealed class CargoHasItemTrigger : IQuestTrigger
{
    public ulong ShipNetId { get; set; }
    public string TradeItemId { get; set; }
    public int RequiredQuantity { get; set; }
    public string TriggerId => $"CargoHasItem:{ShipNetId}:{TradeItemId}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        if (!TradeWorld.Instance.TryGetCargo(ShipNetId, out var cargo)) return false;
        return cargo.GetQuantity(TradeItemId) >= RequiredQuantity;
    }
}
```

### `ReputationAtLeastTrigger`
```csharp
public sealed class ReputationAtLeastTrigger : IQuestTrigger
{
    public FactionId Faction { get; set; }
    public int RequiredValue { get; set; }
    public string TriggerId => $"ReputationAtLeast:{Faction}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        return _world.GetReputation(playerId, Faction) >= RequiredValue;
    }
}
```

**Подписывается** на `ReputationChangedEvent` — instant update.

### `LocationReachedTrigger`
```csharp
public sealed class LocationReachedTrigger : IQuestTrigger
{
    public string SceneId { get; set; }
    public Vector3 Position { get; set; }
    public float Radius { get; set; } = 50f;
    public string TriggerId => $"LocationReached:{SceneId}:{Position}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        // Get player position from NetworkPlayer
        var player = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
        if (player == null) return false;
        if (player.scene.name != SceneId) return false;
        return Vector3.Distance(player.transform.position, Position) <= Radius;
    }
}
```

**Poll** каждые 5 сек.

### `DayNightPhaseTrigger`
```csharp
public sealed class DayNightPhaseTrigger : IQuestTrigger
{
    public DayNightPhase RequiredPhase { get; set; }
    public string TriggerId => $"DayNightPhase:{RequiredPhase}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        return DayNightController.Instance?.CurrentPhase == RequiredPhase;
    }
}
```

**Подписывается** на `DayNightPhaseChangedEvent`.

### `ShipDockedAtTrigger`
```csharp
public sealed class ShipDockedAtTrigger : IQuestTrigger
{
    public ulong ShipNetId { get; set; }
    public string ZoneId { get; set; }
    public string TriggerId => $"ShipDockedAt:{ShipNetId}:{ZoneId}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        // Need future DockZone system. For now: poll CargoData ownership.
        // Return false in v1 if DockZone doesn't exist.
        return false;
    }
}
```

**Stub для v1**, real implementation когда `DockZone` появится.

### `EventTrigger` (custom)
```csharp
public sealed class EventTrigger : IQuestTrigger
{
    public string EventId { get; set; }
    public string TriggerId => $"Event:{EventId}";

    public bool IsSatisfied(QuestInstance instance, ulong playerId)
    {
        return _world.HasEventOccurred(playerId, EventId);
    }
}
```

**Set в** `DialogueAction.EmitEvent` или other server actions.

---

## 6.5 QuestTriggerService — event-driven evaluation

**Решение D2 (см. `09_OPEN_QUESTIONS.md` §J):** **full event bus**, не polling. `QuestTriggerService` подписывается на `WorldEventBus` events и evaluates triggers при получении event.

**В `QuestServer.OnNetworkSpawn` (server-only):**
```csharp
public override void OnNetworkSpawn()
{
    if (!IsServer) return;

    // Subscribe to all relevant WorldEvent types
    WorldEventBus.Subscribe<ItemAddedEvent>(OnItemAdded);
    WorldEventBus.Subscribe<ItemRemovedEvent>(OnItemRemoved);
    WorldEventBus.Subscribe<CargoAddedEvent>(OnCargoAdded);
    WorldEventBus.Subscribe<CargoRemovedEvent>(OnCargoRemoved);
    WorldEventBus.Subscribe<ReputationChangedEvent>(OnReputationChanged);
    WorldEventBus.Subscribe<NpcAttitudeChangedEvent>(OnNpcAttitudeChanged);
    WorldEventBus.Subscribe<CustomEvent>(OnCustomEvent);
    WorldEventBus.Subscribe<DialogVisitedEvent>(OnDialogVisited);
    WorldEventBus.Subscribe<DayNightPhaseChangedEvent>(OnDayNightChanged);
    WorldEventBus.Subscribe<LocationReachedEvent>(OnLocationReached);
    WorldEventBus.Subscribe<ContractCompletedEvent>(OnContractCompleted);  // через ContractMetaBridge
}

public override void OnNetworkDespawn()
{
    if (!IsServer) return;
    WorldEventBus.Unsubscribe<ItemAddedEvent>(OnItemAdded);
    // ... etc, mirror all subscribes
}

private void OnItemAdded(ItemAddedEvent ev)
{
    QuestWorld.Instance?.TriggerService.Evaluate(ev.PlayerId, $"HaveItem:{ev.ItemId}");
    QuestWorld.Instance?.TriggerService.Evaluate(ev.PlayerId, $"HaveTradeItem:{ev.TradeItemId}");
}

private void OnReputationChanged(ReputationChangedEvent ev)
{
    QuestWorld.Instance?.TriggerService.Evaluate(ev.PlayerId, $"ReputationAtLeast:{ev.Faction}");
}

private void OnNpcAttitudeChanged(NpcAttitudeChangedEvent ev)
{
    QuestWorld.Instance?.TriggerService.Evaluate(ev.PlayerId, $"NpcAttitudeAtLeast:{ev.NpcId}");
}

// ... etc для каждого подписанного event
```

**Polling fallback** (для `LocationReachedTrigger` и `DayNightPhaseTrigger` если event missed):
- `QuestServer.FixedUpdate` каждые 5 сек проверяет позицию игроков + current day/night phase.
- Это **defensive backup**, не primary mechanism.

---

## 6.6 Интеграция с существующими подсистемами

### Inventory (Items)

**API:** `InventoryServer.AddItem(clientId, intItemId, ItemType)` + new `TryRemove(clientId, intItemId, count)`.

**Quest → Inventory:** `DialogueAction.GiveItem(intItemId, count)` → `InventoryServer.AddItem`. `DialogueAction.TakeItem(intItemId, count)` → new `InventoryServer.TryRemove`.

**Inventory → Quest (event, через WorldEventBus):**
```csharp
// In InventoryServer.cs after AddItem succeeds:
WorldEventBus.Publish(new ItemAddedEvent
{
    PlayerId = clientId,
    ItemId = itemId,
    Count = 1,
    TradeItemId = null  // optional mapping from ItemData → TradeItemDefinition
});
// Same for TryRemove → ItemRemovedEvent
```

**Важно:** `InventoryWorld` is in `ProjectC.Items` namespace, `QuestTriggerService` is in `ProjectC.Quests`. **Items → Quests** dependency допустима (Quests = new subsystem, Items = stable). **Не наоборот.**

### Trade (Market/Contract)

**API:** `MarketServer` (full, 522 LOC), `ContractServer` (412 LOC), `IPlayerDataRepository`.

**Quest → Trade:** квест может потребовать warehouse item:
- `QuestObjective.HaveItem(TradeItemId, qty)` → check `Warehouse.GetQuantity`.
- `DialogueAction.GiveTradeItem(TradeItemId, qty)` → `Warehouse.TryAdd`.

**Trade → Quest (event):** нет прямой интеграции в v1 (warehouse changes = quest objective already evaluated via polling).

**Note:** contract ≠ quest (см. `01_CURRENT_STATE_AUDIT.md` §1.9). `DialogueAction.OfferContract(contractId)` exists для единичных кейсов, но не часть quest flow.

### CargoData (per-ship)

**API:** `CargoData.TryAdd/TryRemove/GetQuantity` (line 39, 73, 32 of `CargoData.cs`).

**Quest → Cargo:** `DialogueAction.GiveCargoItem(TradeItemId, qty)` → `TradeWorld.GetOrLoadCargo(shipNetId).TryAdd(...)`.

**Cargo → Quest (event):** add hook in `CargoData.TryAdd` to fire `CargoAddedEvent`.

### Ship Controller

**API:** `ShipController` (player ship), `PlayerStateMachine` (board/disembark).

**Quest → Ship:** "dock at port X" objective → check `PlayerStateMachine.IsDocked` (future API).

**Ship → Quest (event):** подписка на `OnDock` event (когда появится).

### DayNight

**API:** `DayNightController` (in `Scripts/Core/DayNight/`), `DayNightProfile : SO`.

**Quest → DayNight:** read `DayNightController.CurrentPhase` для `DayNightPhaseTrigger`.

**DayNight → Quest (event):** подписка на `OnPhaseChanged` event (нужно добавить в `DayNightController`).

### World Streaming

**API:** `PlayerChunkTracker`, `SceneID`, `SceneRegistry`.

**Quest → World:** "reach scene X" objective → `PlayerChunkTracker.CurrentSceneId`.

**World → Quest (event):** подписка на `OnSceneChanged` event (если есть).

### MetaRequirement (existing)

**API:** `MetaRequirement.CanPlayerUse` (line 96), `MetaRequirementRegistry` (line 27).

**Не путать:** MetaRequirement = per-interactable "do you have the key" gate. Quest = per-player "go do X arc" progression. **They are orthogonal but can interact:**

- Quest с objective "Open the locked chest at location X" → `MetaRequirement.CanPlayerUse` проверяется на quest advance.
- Reverse: locked chest shows `MetaRequirementClientState.OnAccessDenied` + Quest log shows "X blocked, need key Y" hint.

**Не интегрируем в v1.** Просто документируем.

### Combat (future)

**API:** нет ещё (combat system не реализован в проекте, см. README roadmap).

**Quest → Combat:** в v2 (когда combat появится): `KilledEntityTrigger`, `DamageDealtTrigger`, `SurvivedWaveTrigger`.

---

## 6.7 Все triggers — event-driven (full bus)

**Решение D2 (см. `09_OPEN_QUESTIONS.md` §J):** **full event bus**, **все triggers** event-driven.

| Trigger type | Primary mechanism | Polling fallback? |
|---|---|---|
| `TalkedToNpcTrigger` | `DialogVisitedEvent` (publisher: `QuestServer`) | Нет |
| `HaveItemTrigger` | `ItemAddedEvent` + `ItemRemovedEvent` (publisher: `InventoryServer`) | Нет |
| `CargoHasItemTrigger` | `CargoAddedEvent` + `CargoRemovedEvent` (publisher: `TradeWorld`) | Нет |
| `ReputationAtLeastTrigger` | `ReputationChangedEvent` (publisher: `QuestServer.ModifyReputation`) | Нет |
| `NpcAttitudeAtLeastTrigger` | `NpcAttitudeChangedEvent` (publisher: `QuestServer.ModifyNpcAttitude`) | Нет |
| `DayNightPhaseTrigger` | `DayNightPhaseChangedEvent` (publisher: `DayNightController`) | Да (defensive, каждые 30 сек) |
| `LocationReachedTrigger` | `LocationReachedEvent` (publisher: `PlayerChunkTracker`) | Да (defensive, каждые 5 сек) |
| `EventDrivenTrigger` (новый) | `CustomEvent` (publisher: `DialogueAction.EmitEvent`) | Нет |
| `ContractCompletedTrigger` (новый) | `ContractCompletedEvent` (publisher: `ContractServer` через `ContractMetaBridge`) | Нет |
| `ShipDockedAtTrigger` | `ShipDockedEvent` (publisher: `ShipController` future) | Нет |
| `KilledEntityTrigger` | (нет publisher — combat не существует) | Нет, stub |

**Polling только как defensive backup** для случаев, когда event может быть пропущен (location — игрок может перемещаться в зону незаметно для подписчика).

**Static singleton + `Reset()`** — для EditMode tests, где можно Publish без подписчиков.

---

## 6.8 Pitfall-лист (triggers/integration)

| # | Pitfall | Источник |
|---|---------|---------|
| 1 | Inventory `AddItem` / `RemoveItem` сейчас не выстреливает event → квест не узнает | `InventoryServer.cs:79-88` |
| 2 | Нет server-side event bus в проекте — нужно создать с нуля | subagent analysis §3.4 |
| 3 | `InventoryWorld` не персистится — квест rewards пропадут при server restart | `InventoryWorld.cs:21` TODO |
| 4 | 2 item-системы (ItemData int vs TradeItemDefinition string) — квест objective имеет оба поля | `02_V2_ARCHITECTURE.md` §2.11 |
| 5 | CargoData не имеет `IsSatisfied` pattern — нужна интеграция | `CargoData.cs:32-100` |
| 6 | DayNightController не имеет public event для phase change | read `DayNightController.cs` to verify |
| 7 | PlayerChunkTracker — какой API для "current scene"? | `PlayerChunkTracker.cs` skim |
| 8 | Combat не существует — `KilledEntityTrigger` stub | README |

---

## 6.9 Open questions (triggers/integration)

**Все вопросы решены 2026-06-07 (см. `09_OPEN_QUESTIONS.md` §D):**

- ✅ **D1: Inventory persistence** — фиксим ДО quest rewards (T-X0).
- ✅ **D2: Event-bus** — **full bus** (все triggers event-driven, polling только defensive backup).
- ✅ **D3: Quest → Combat** — `KilledEntityTrigger` stub, TODO когда combat появится.

**Оставшиеся open questions из смежных разделов:**

- **F1** (точные tier'ы reputation) — tune после v1.
- **A3 §G** (cross-faction influence calc) — MVP stub, полная реализация v2.
- **A5 §H** (save на каждый state change vs debounce) — immediate save выбран.
