# CS0136 Error Prevention Checklist

## Problem
Local variable `transport` declared in nested block conflicts with same name at method scope level.

## Anti-Pattern (DO NOT USE)
```csharp
public void StartHost()
{
    var netConfig = networkManager.NetworkConfig;

    if (netConfig == null)
    {
        var transport = GetComponent<UnityTransport>(); // BLOCK 1 transport
        // ...
        return;
    }

    var transport = GetComponent<UnityTransport>(); // METHOD SCOPE transport - CONFLICT!
}
```

## Correct Pattern
```csharp
public void StartHost()
{
    // 1. Declare ALL method-scope variables at method level FIRST
    var transport = GetComponent<UnityTransport>();
    if (transport == null)
        transport = gameObject.AddComponent<UnityTransport>();

    // 2. Then handle conditional logic
    var netConfig = networkManager.NetworkConfig;

    if (netConfig == null)
    {
        // Use existing 'transport' variable, don't declare new one
        // ...
        return;
    }

    // Continue using 'transport'...
}
```

## When Fixing This Error

1. Count how many `var transport` declarations exist in the method
2. There should be EXACTLY ONE at method scope level
3. All others inside `if` blocks should be renamed (e.g., `newTransport`, `existingTransport`)
4. OR better: restructure to declare once at top

## Common Locations

- `NetworkManagerController.StartHost()` - line with `if (netConfig == null)` block
- Any method with multiple conditional branches declaring same variable name

## Remember

The C# compiler sees this as TWO different `transport` variables in the SAME scope (the method), not as nested scopes.