---
paths:
  - "Assets/_Project/Shaders/**"
  - "Assets/_Project/Materials/**"
---

# Shader Code Rules

- Use Shader Graph for artist-editable shaders where possible
- Document shader properties and usage in header comment
- Create fallback materials for mobile/low-end platforms
- Profile GPU performance with RenderDoc/Unity Profiler
- Follow naming: PascalCase, descriptive (CloudGhibli.shader)

## URP Requirements

- ❌ **NEVER** create URP assets via C# code
- ✅ **ONLY** via Unity Editor UI: `Edit → Project Settings → Graphics`
- ✅ Use `UniversalRendererData` (NOT `ForwardRendererData`)
- ✅ Shader Graphs → `Universal Render Pipeline/Lit`

## Project C Shaders

### CloudGhibli.shader
- Procedural cloud generation
- Cel-shaded rendering (soft shadows, outline)
- Ghibli-inspired softness (low contrast, pastel colors)
- Animated via time uniform

### Usage
```hlsl
// In shader properties
_CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
_CloudSpeed ("Cloud Speed", Float) = 0.5