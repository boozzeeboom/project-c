---
paths:
  - "Assets/_Project/Scripts/Player/**"
  - "Assets/_Project/Scripts/Abilities/**"
  - "Assets/_Project/Scripts/Inventory/**"
---

# Gameplay Code Rules

- Use `[Serializable]` for all data classes
- Implement `INetworkSerializable` for network types
- Use events for system communication (not direct references)
- Profile any Update() loops — avoid allocations
- Cache component references in `Awake()` — never call `GetComponent<>()` in `Update()`
- Use `[SerializeField] private` for inspector-exposed fields
- Document design intent in class headers

## Project C Specifics

### Player Controller
- Support grounded and flying states
- Handle gravity zones properly
- Sync state via NGO NetworkVariables

### Inventory
- 8 item types: Resource, Equipment, Consumable, Quest, Treasure, Key, Currency, Misc
- Use InventoryWheel for UI selection
- Chest interactions via trigger volumes

### Abilities
- Implement cooldown tracking with float timer
- Use RPCs for ability activation (unreliable for performance)
- Target acquisition before cast