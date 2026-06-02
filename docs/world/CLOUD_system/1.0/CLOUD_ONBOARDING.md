# CLOUD_system — Onboarding Summary

**Version:** 0.1 | **Date:** 3 мая 2026 | **Status:** 🔴 Draft

---

## Project Context

**Project:** Project C: The Clouds
**System:** Cloud Rendering & Weather System
**Current State:** Mesh-based (SPHERES), needs full rework
**Target State:** Hybrid volumetric + server-authoritative weather

### Key Documents

| Document | Purpose |
|----------|---------|
| `CLOUD_ARCHITECTURE.md` | Master technical architecture |
| `CLOUD_VISUAL_DESIGN.md` | Visual design, colors, shader params |
| `CLOUD_IMPLEMENTATION_PLAN.md` | 6-phase testing-based implementation |
| `ADR-Cloud-001-Rendering-Architecture.md` | Key architectural decisions |

### Related GDD Documents

| GDD | Relevance |
|-----|-----------|
| `GDD_02_World_Environment.md` | Cloud specs in §7 (Weather System) |
| `GDD_12_Network_Multiplayer.md` | Network architecture (NGO) |
| `GDD_12_1_Scene_World_Streaming.md` | 24-scene architecture |
| `GDD_14_Visual_Art_Pipeline.md` | CloudGhibli shader, URP setup |

---

## Current System (What We're Replacing)

### Files
```
Assets/_Project/Scripts/Core/
├── CloudSystem.cs         — Main orchestrator (3 layers + cumulonimbus)
├── CloudLayer.cs          — Generates 50-350 clouds per layer via CreatePrimitive
├── CloudLayerConfig.cs     — ScriptableObject config

Assets/_Project/Scripts/World/Clouds/
└── CumulonimbusCloud.cs   — Storm column (cylinder mesh + particles)

Assets/_Project/Art/Shaders/
└── CloudGhibli.shader     — Unlit shader (rim glow, FBM noise, vertex displacement)
```

### Problems
1. **890+ GameObjects** — each cloud is a sphere/plane, massive draw calls
2. **Not volumetric** — just textured meshes, flat appearance
3. **Unlit shader** — no lighting/shadows from scene
4. **No network sync** — wind/storms local to each client
5. **Not scene-aware** — clouds don't work with 24-scene streaming

---

## Target Architecture

### Server Side (Authority)
- `ServerWeatherController` — broadcasts wind direction/speed
- `ServerStormManager` — spawns/tracks storm positions

### Client Side (Rendering)
- `CloudManager` — receives server state, coordinates rendering
- `BillboardCloudRenderer` — instanced billboards for upper layer
- `VolumetricCloudPass` — URP render pass for raymarch (lower layer)
- `StormController` — client visual for server-authoritative storms

### Network Sync (Low Bandwidth)
| Data | Frequency | Bandwidth |
|------|-----------|-----------|
| Wind | 0.5 Hz | ~18 B/s |
| Storms | 0.2-0.5 Hz | ~40 B/s |
| Time | 0.1 Hz | ~0.4 B/s |
| **Total** | | **~58 B/s** |

---

## Implementation Phases

| Phase | Duration | Goal | Success Criteria |
|-------|----------|------|------------------|
| 1 | Week 1 | Server wind | 2 clients see same wind direction |
| 2 | Week 1-2 | Storm authority | 5 storms at same positions |
| 3 | Week 2-3 | Hybrid rendering | Visual quality improved, perf OK |
| 4 | Week 3 | Scene streaming | Clouds work across scene boundaries |
| 5 | Week 3-4 | Floating origin | No glitches after world shift |
| 6 | Week 4+ | Polish | "Tasty" clouds, player feedback |

---

## Key Technical Decisions

### Rendering
- **NOT VDB** — too heavy
- **Analytical noise** — no 3D texture memory cost
- **Billboard + Volumetric hybrid** — by layer
- **URP ScriptableRenderPass** — for raymarch

### Networking
- **Server-authoritative wind** — all clients sync
- **Storm positions from server** — gameplay-relevant
- **Decorative clouds client-only** — no sync overhead
- **Low frequency broadcasts** — 2-5 second intervals

### Scene Distribution
- **Bootstrap:** CloudManager + ServerWeatherController + ServerStormManager
- **World scenes:** 100 clouds per layer per scene (300 total active)
- **Clouds under WorldRoot** — inherit floating origin shifts

---

## Performance Budget

| Metric | Budget | Current |
|--------|--------|---------|
| Cloud GPU | <3 ms | ~8 ms (890 meshes) |
| Cloud CPU | <3.6 ms | High (890 Update loops) |
| Draw calls | <2000 total | 890+ (clouds alone) |
| Memory | <30 MB | ~10 MB (OK) |
| Network | <100 B/s | ~58 B/s (OK) |

---

## Immediate Next Steps

1. **Read CLOUD_ARCHITECTURE.md** — understand full architecture
2. **Review CLOUD_IMPLEMENTATION_PLAN.md** — Phase 1 tasks
3. **Begin Phase 1: Server Wind System** — create ServerWeatherController.cs
4. **Test with 2 clients** — verify wind sync works

---

## Questions to Ask When Starting

1. Which layer should we start replacing first? (recommend: Upper billboard)
2. Do we keep CloudGhibli.shader as fallback? (recommend: yes)
3. Storm lightning in shader or VFX Graph? (recommend: VFX Graph)
4. 3D texture noise or analytical? (recommend: analytical)

---

## Common Pitfalls

| Pitfall | Prevention |
|---------|------------|
| Overcomplicating raymarch | Start with billboards, add volumetric later |
| Ignoring scene streaming | Test cloud regeneration on scene boundary crossing |
| Forgetting floating origin | Parent clouds under WorldRoot |
| Network desync | Use interpolation for smooth wind transitions |
| Performance regression | Profile each phase, stay within budget |

---

**Status:** 🔴 Draft — for team review before Phase 1