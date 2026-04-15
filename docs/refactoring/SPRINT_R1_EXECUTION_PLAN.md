# Sprint R1 — Execution Plan

**Дата:** 2026-04-15  
**Статус:** 🔄 In Progress  
**Фокус:** Performance Hotfix — устранение allocations в hot paths

---

## Analysis Summary

After code review, identified critical performance issues:

### NetworkPlayer.cs — FindObjectsByType в Update loop

| Line | Method | Issue | Severity |
|------|--------|-------|----------|
| 358 | `FindNearestShip()` | `FindObjectsByType<ShipController>()` — вызывается при каждом нажатии F | P0 |
| 403 | `FindNearestInteractable()` | `FindObjectsByType<ChestContainer>()` — В HOT PATH Update() | P0 |
| 418 | `FindNearestInteractable()` | `FindObjectsByType<PickupItem>()` — В HOT PATH Update() | P0 |
| 465 | `HidePickupRpc()` | `FindObjectsByType<PickupItem>()` — в RPC | P1 |
| 481 | `OpenChestRpc()` | `FindObjectsByType<ChestContainer>()` — в RPC | P1 |

### ThirdPersonCamera.cs — FindAnyObjectByType

| Line | Method | Issue |
|------|--------|-------|
| 196 | `CreateControlHintsUI()` | `FindAnyObjectByType<ControlHintsUI>()` |
| 203 | `CreateControlHintsUI()` | `FindAnyObjectByType<Canvas>()` |

### WorldCamera.cs — FindObjectsByType в Start

| Line | Method | Issue |
|------|--------|-------|
| 243 | `FindWorldRoot()` | `FindObjectsByType<GameObject>()` — В HOT PATH Start() |

### InventoryUI.cs — Material allocation

| Line | Method | Issue |
|------|--------|-------|
| 330-333 | `DrawFilledFan()` | `new Material(...)` создаётся при каждом рендере |
| 355-358 | `DrawOutline()` | `new Material(...)` дублируется |

---

## Implementation Tasks

### Task 1: Create IInteractable Interface
**File:** `Assets/_Project/Scripts/Core/IInteractable.cs` (NEW)

```csharp
namespace ProjectC.Core
{
    /// <summary>
    /// Interface for objects that can be interacted with by the player.
    /// Used for trigger-based caching instead of FindObjectsByType.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Unique identifier for this interactable instance.
        /// </summary>
        string InstanceId { get; }
        
        /// <summary>
        /// Display name shown to player when nearby.
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Interaction radius for this object.
        /// </summary>
        float InteractionRadius { get; }
        
        /// <summary>
        /// World position of this interactable.
        /// </summary>
        Vector3 Position { get; }
    }
}
```

### Task 2: Update PickupItem
**File:** `Assets/_Project/Scripts/Core/PickupItem.cs`

Changes:
- Implement `IInteractable`
- Add `OnTriggerEnter/Exit` registration (static event or direct reference)

### Task 3: Update ChestContainer
**File:** `Assets/_Project/Scripts/Core/ChestContainer.cs`

Changes:
- Implement `IInteractable`
- Add `OnTriggerEnter/Exit` registration

### Task 4: Update NetworkPlayer
**File:** `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

Changes:
- Add cached lists: `_nearestPickup`, `_nearestChest`, `_nearestShip`
- Replace `FindObjectsByType` calls with trigger-based updates
- Add event handlers for interactable registration

### Task 5: Cache ThirdPersonCamera
**File:** `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs`

Changes:
- Cache `ControlHintsUI` and `Canvas` references in Start/Awake

### Task 6: Cache WorldCamera
**File:** `Assets/_Project/Scripts/Core/WorldCamera.cs`

Changes:
- Cache `worldGenerator` in Start (NOT in Update/LateUpdate)

### Task 7: Fix InventoryUI Material
**File:** `Assets/_Project/Scripts/UI/InventoryUI.cs`

Changes:
- Pre-allocate Material in Awake (already static but not used correctly)
- Fix duplicate material creation in `DrawOutline()`

---

## Files to Modify

1. `Assets/_Project/Scripts/Core/IInteractable.cs` — CREATE
2. `Assets/_Project/Scripts/Core/PickupItem.cs` — MODIFY
3. `Assets/_Project/Scripts/Core/ChestContainer.cs` — MODIFY
4. `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — MODIFY
5. `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs` — MODIFY
6. `Assets/_Project/Scripts/Core/WorldCamera.cs` — MODIFY
7. `Assets/_Project/Scripts/UI/InventoryUI.cs` — MODIFY

---

## Definition of Done

- [ ] Zero `FindObjectsByType` / `FindAnyObjectByType` in Update() loops
- [ ] Zero `new Material()` in OnGUI() / Draw* methods
- [ ] Profiling confirms < 0.5ms per Update() frame
- [ ] Editor tests pass (pickup, chest open, ship board)
