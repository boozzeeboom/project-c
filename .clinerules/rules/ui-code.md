---
paths:
  - "Assets/_Project/Scripts/UI/**"
  - "Assets/_Project/UI/**"
---

# UI Code Rules

- UI Toolkit for runtime UI (recommended for performance)
- uGUI for world-space UI or legacy support
- Separate UI data from UI logic — UI reads from data, never owns game state
- Use events/callbacks for communication
- Pool UI elements for lists and inventories
- Handle multiple resolutions with anchors and scaling
- Support keyboard/gamepad navigation

## UI Toolkit Best Practices

```csharp
// Use UQuery for element retrieval
var button = root.Q<Button>("SubmitButton");
var label = root.Q<Label>("DescriptionLabel");

// Bind data via visual tree
// Avoid direct component access in Update()
```

## Inventory UI

- InventoryWheel: circular radial layout
- 8 item type slots arranged in ring
- Selection via mouse/touch direction
- Tooltip on hover with item details

## Accessibility

- Support color blind modes
- Adjustable subtitle sizing
- Alternative input methods
- High contrast option