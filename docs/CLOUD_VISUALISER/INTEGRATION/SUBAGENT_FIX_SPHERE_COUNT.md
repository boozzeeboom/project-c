# Subagent Prompt: Fix sphereCountScale Bug in CloudGenerator.cs

## Task
Fix the sphereCountScale bug in the CloudGenerator.cs template inside `visualizer3d.html`. The slider (1%-400%) works in JS but the generated C# code ignores the `SphereCountScale` field.

## Files to Modify

### 1. CloudTypes.cs template (in visualizer3d.html)

**Location:** Search for `cloudTypesHeader` in visualizer3d.html — this is the TEMPLATE for CloudTypes.cs

**Find the CloudLayerConfig class** and add:
```csharp
public float SphereCountScale = 1f;
```

Also change `Mesh ParentMesh` to `string ParentMeshPath` for JSON serializability, or add separate transform fields as strings.

### 2. CloudGenerator.cs template (in visualizer3d.html)

**Location:** Search for `cloudGeneratorHeader` in visualizer3d.html

**Find the line that calculates maxSphereCount** (should be around line 1260 in the template, inside GenerateSphereLayer):
```csharp
int maxSphereCount = layer.MaxSphereCount;
```

**Replace with:**
```csharp
float sphereCountScale = layer.SphereCountScale > 0 ? layer.SphereCountScale : 1.0f;
int maxSphereCount = (int)(layer.MaxSphereCount * sphereCountScale);
```

## Verification
After modifying the template, the generated CloudGenerator.cs should use SphereCountScale when calculating maxSphereCount.

## Context
- Visualizer file: `C:\UNITY_PROJECTS\ProjectC_client\docs\CLOUD_VISUALISER\web\visualizer3d.html`
- Templates are in TEMPLATES.csharp object (search for `cloudGeneratorHeader`)
- EXPORT_VERSION is '7.0'
- SphereCountScale is already in EXPORT_SCHEMA.sphere array

## Important
- Only modify the TEMPLATE strings, not the JS runtime code
- Use the same pattern as JS: `layer.SphereCountScale > 0 ? layer.SphereCountScale : 1.0f`
- Default to 1.0f if not set or <= 0