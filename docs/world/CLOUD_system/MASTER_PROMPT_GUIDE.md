# CLOUD_system — Master Session Continuation Guide

**Version:** 1.0 | **Date:** 3 мая 2026 | **Status:** 🔴 Living Document

---

## 1. Project Overview

### 1.1 What Is This Project

**Project C: The Clouds** — Open world MMO set above clouds in 2090. Players fly ships between mountain peaks across a world of 24 streaming scenes (79,999 × 79,999 units each).

**Cloud System Goal:** Create an immersive, "tasty" cloud rendering system with server-authoritative wind/storms and client-side performance optimization.

### 1.2 Current System State

| Component | Status | Location |
|-----------|--------|----------|
| Current cloud implementation | ⚠️ PROBLEMATIC — 890+ mesh primitives | `Assets/_Project/Scripts/Core/CloudSystem.cs` |
| Target architecture | 🔴 PLANNED v0.3 | `docs/world/CLOUD_system/` |
| Scene streaming | ✅ WORKING | `ClientSceneLoader.cs` |
| Network (NGO) | ✅ WORKING | `NetworkManagerController.cs` |

### 1.3 Key Constraints

| Constraint | Value |
|------------|-------|
| Scene size | 79,999 × 79,999 units |
| Preload distance | 10,000 units |
| Unload distance | 10,000 units |
| Max loaded scenes | 4 |
| FloatingOriginMP | ❌ NOT USED (not needed) |
| Cloud budget GPU | <3ms |
| Cloud budget CPU | <3.6ms |

---

## 2. Critical Architecture Decisions (v0.3)

### 2.1 NEVER DO These (Confirmed Wrong)

| Decision | Why Wrong |
|----------|-----------|
| ❌ Sky Dome as cloud layer | Sky dome is SKY RENDERER, not volumetric clouds |
| ❌ Camera-following distant impostors | Breaks multiplayer — each player sees different positions |
| ❌ Per-scene cloud regeneration | Causes visual popping at boundaries |
| ❌ Distance-based layers | Contradicts altitude corridor gameplay |
| ❌ Full raymarch (64-128 steps) | 4-20ms GPU — too expensive for 60Hz |
| ❌ 150 clouds total | Too sparse — 0.008 clouds/km² |

### 2.2 CORRECT Architecture (v0.3)

| Element | Correct Approach |
|---------|-----------------|
| Layer structure | **Altitude-based**: Upper (6000-8000m), Middle (3000-5000m), Lower (1500-3000m) |
| Near clouds (0-5km) | 280 total (80+120+80), GPU instanced, player-centered recycling at 15km |
| Distant clouds (5-15km) | 140 billboards at **FIXED WORLD POSITIONS**, not camera-following |
| Storms | 5 server-authoritative, world-space, visible if <50km |
| Wind | Server-broadcast at 0.5 Hz, central WindManager |
| Rendering | Instanced mesh with improved CloudGhibli shader, NOT full raymarch |
| Sky Dome | **ONLY as sky renderer** (blue gradient, sun) — NOT a cloud layer |

### 2.3 Cloud Distribution

```
Layer UPPER (6000-8000m): 80 near + 40 distant = 120 clouds
Layer MIDDLE (3000-5000m): 120 near + 60 distant = 180 clouds  ← DENSEST (player flies THROUGH)
Layer LOWER (1500-3000m): 80 near + 40 distant = 120 clouds
Storms: 5 server-authoritative

TOTAL: ~425 clouds (vs current 890)
```

---

## 3. Document Reading Order

When continuing a session, read documents in this EXACT order:

### Phase A: Context (Start Here)

```
1. docs/world/CLOUD_system/CLOUD_TECHNICAL_SUMMARY.md
   ├── Why v0.3 is correct
   ├── What was rejected and why
   └── Key constraints

2. docs/world/CLOUD_system/CLOUD_ARCHITECTURE.md
   ├── Complete v0.3 architecture
   ├── WindManager, CloudManager, DistantCloudManager
   ├── Storm system (world-space)
   └── Performance budget
```

### Phase B: Implementation Details

```
3. docs/world/CLOUD_system/CLOUD_IMPLEMENTATION_PLAN.md
   ├── 6 phases with test criteria
   ├── Phase 1: Wind + CloudManager
   ├── Phase 2: Instanced Near Clouds
   ├── Phase 3: Distant Impostors
   ├── Phase 4: Storm Authority
   ├── Phase 5: Shader Improvements
   └── Phase 6: Polish
```

### Phase C: Related Systems (Context Only)

```
4. docs/world/LargeScaleMMO/2_iteration_scene-mode/SYSTEM_OVERVIEW.md
   └── Scene streaming (10k preload, 79,999 size, 4×6 grid)

5. docs/gdd/GDD_02_World_Environment.md (cloud sections only)
   └── Original cloud spec: 3 layers, 890+ clouds, day/night

6. docs/gdd/GDD_14_Visual_Art_Pipeline.md (cloud shader section)
   └── CloudGhibli shader, visual style (Sci-Fi + Ghibli)
```

### Phase D: Code Reference

```
7. Assets/_Project/Scripts/Core/CloudSystem.cs
   └── Current problematic implementation (for reference)

8. Assets/_Project/Scripts/Core/CloudLayer.cs
   └── Current layer generation (for understanding current issues)

9. Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs
   └── Scene streaming integration (how it works)
```

---

## 4. Graphify Usage

### 4.1 When to Use Graphify

**USE Graphify when:**
- Starting a NEW session (first 10 minutes)
- Making architectural decisions
- Finding code connections
- Understanding complex dependencies

**DO NOT use Graphify when:**
- You already have context from docs
- Doing straightforward implementation
- Debugging specific issues

### 4.2 How to Query Graphify

```bash
# Search for cloud-related code
# Open: docs/graph.html
# Search for: "CloudSystem", "CloudLayer", "CloudRendering"

# Key communities in graph:
# - Community 17: "Weather & Clouds" (38 nodes)
# - Community 4: "World Streaming" (105 nodes)
# - Community 0: "Scene Management" (125 nodes)
```

### 4.3 Graph Navigation Steps

1. Open `docs/graph.html` in browser
2. Search for component name (e.g., "CloudSystem")
3. Click node to see connections
4. Check "Neighbors" list for related components
5. Use Legend to identify communities

### 4.4 Key Graph Communities

| Community | Color | Contains |
|-----------|-------|----------|
| 17 | #FF9DA7 | Weather & Clouds |
| 4 | #59A14F | World Streaming |
| 0 | #4E79A7 | Scene Management |
| 2 | #E15759 | Ship Flight Physics |

---

## 5. Session Continuation Protocol

### 5.1 Starting a New Session

**MANDATORY first steps:**

```
1. Read CLOUD_TECHNICAL_SUMMARY.md (10 minutes)
2. Read CLOUD_ARCHITECTURE.md (20 minutes)
3. Read CLOUD_IMPLEMENTATION_PLAN.md (15 minutes)
4. Check current phase in CLOUD_IMPLEMENTATION_PLAN.md
5. Identify what was completed last session
6. Identify immediate next task
7. Check for any new documents in docs/world/CLOUD_system/
```

### 5.2 Resuming After Break

**If break < 1 week:**
```
1. Read CLOUD_TECHNICAL_SUMMARY.md (5 minutes)
2. Review current phase in CLOUD_IMPLEMENTATION_PLAN.md
3. Check what was completed
4. Read any updated documents
5. Proceed with next task
```

**If break > 1 week:**
```
1. Full re-read of Phase A + B documents (1 hour)
2. Check git log for recent changes
3. Review any session notes in docs/world/CLOUD_system/
4. Then proceed as new session
```

### 5.3 Session Structure

```
SESSION TEMPLATE:

Hour 1:
├── Read/update context (30 min)
├── Task planning (15 min)
└── Implementation (15 min)

Hour 2-3:
└── Implementation + testing

Hour 4:
├── Integration test
├── Documentation update
└── Next session prep
```

### 5.4 Before Ending Session

**MUST DO before ending:**
```
1. Update CLOUD_IMPLEMENTATION_PLAN.md with:
   ├── What was completed
   ├── What was found/decided
   └── What needs to happen next session

2. Write session notes in docs/world/CLOUD_system/SESSIONS/
   ├── SESSION_YYYY-MM-DD.md
   └── Summary of work + decisions + blockers

3. Commit if significant changes:
   git add docs/world/CLOUD_system/
   git commit -m "Cloud system: [what was done]"
```

---

## 6. Implementation Testing Protocol

### 6.1 Phase Validation Checklist

Before declaring a phase COMPLETE, ALL tests must pass:

**Phase 1 (Wind System) — Tests:**
- [ ] WindManager receives server RPC
- [ ] All cloud systems (upper/middle/lower/distant/storms) receive wind
- [ ] Two clients see same wind direction
- [ ] Wind change propagates within 5 seconds

**Phase 2 (Instanced Near Clouds) — Tests:**
- [ ] 280 near clouds generated around player
- [ ] GPU instancing working (single draw call per layer)
- [ ] Clouds move with wind direction
- [ ] Clouds recycle at 15km, reposition to 8-12km
- [ ] Performance <1.5ms GPU per layer

**Phase 3 (Distant Impostors) — Tests:**
- [ ] 140 distant impostors at FIXED world positions
- [ ] Same positions visible to all clients
- [ ] Impostors move with wind
- [ ] No popping at scene boundaries

**Phase 4 (Storm Authority) — Tests:**
- [ ] 5 storms spawned at server positions
- [ ] All clients see storms at same world positions
- [ ] Storms move with wind
- [ ] Lightning triggers with VFX + material flash
- [ ] Storms hidden if >50km from player

**Phase 5 (Shader Improvements) — Tests:**
- [ ] 3 noise octaves (not 2)
- [ ] Light influence parameter works
- [ ] Day/night tint blending works
- [ ] Lightning flash parameter works

**Phase 6 (Polish) — Tests:**
- [ ] URP Volume with Bloom (0.7 intensity)
- [ ] Full integration — 60 FPS
- [ ] Multi-client: 2 clients, all synchronized
- [ ] Visual quality: "tasty" clouds, not flat primitives

### 6.2 Performance Profiling

**GPU Profiler (Unity):**
```
Window > Analysis > Profiler
1. Record frame
2. Check "Rendering" tab
3. Look for: "CloudSystem", "DrawMeshInstanced"
4. Target: <3ms GPU for all clouds
```

**CPU Profiler:**
```
Window > Analysis > Profiler
1. Check "CPU" tab
2. Look for: Update loops, cloud recycling
3. Target: <3.6ms CPU
```

### 6.3 Multi-Client Testing

**Setup:**
```
1. Build server (or run as Host)
2. Build client x2
3. Host + 2 clients
4. Record both screens (video)
5. Compare cloud directions, positions
```

**Verification Points:**
- Wind direction: Same on both clients
- Distant impostors: Same world positions
- Storms: Same positions
- Near clouds: Same behavior (may differ slightly due to recycling timing)

---

## 7. Integration Checklist

### 7.1 CloudManager Integration

```csharp
// In BootstrapScene (or where CloudManager lives):
public class CloudManager : MonoBehaviour
{
    public static CloudManager Instance { get; private set; }

    [Header("References")]
    public WindManager windManager;
    public NearCloudRenderer upperLayer;
    public NearCloudRenderer middleLayer;
    public NearCloudRenderer lowerLayer;
    public DistantCloudManager distantManager;
    public StormController[] stormControllers; // 5

    private void Start()
    {
        // Subscribe to wind updates
        windManager.OnWindUpdated += HandleWindUpdate;
    }

    void HandleWindUpdate(Vector3 direction, float speed)
    {
        upperLayer?.SetWind(direction, speed);
        middleLayer?.SetWind(direction, speed);
        lowerLayer?.SetWind(direction, speed);
        distantManager?.SetWind(direction, speed);
        foreach (var storm in stormControllers)
            storm?.SetWind(direction, speed);
    }
}
```

### 7.2 Scene Loader Integration

**CloudManager does NOT need to integrate with ClientSceneLoader.**

Reason: Clouds are player-centered, NOT scene-bound. No OnSceneLoaded handler needed.

**When player moves between scenes:**
1. ClientSceneLoader handles scene loading/unloading
2. CloudManager continues running (DontDestroyOnLoad)
3. Near clouds stay around player, recycle naturally
4. Distant clouds remain at world positions
5. No cloud pop or regeneration needed

### 7.3 WindManager Integration

```csharp
// ServerWeatherController.cs
public class ServerWeatherController : NetworkBehaviour
{
    [ClientRpc] void BroadcastWindClientRpc(Vector3 direction, float speed)
    {
        WindManager.Instance?.ApplyWindUpdate(direction, speed);
    }
}

// CloudManager receives from WindManager
windManager.OnWindUpdated += HandleWindUpdate;
```

---

## 8. File Structure

### 8.1 Current Files (Will Be Modified)

```
Assets/_Project/Scripts/Core/
├── CloudSystem.cs           # WILL BE DEPRECATED → replaced by CloudManager
├── CloudLayer.cs            # WILL BE REFACTORED → becomes NearCloudRenderer
├── CloudLayerConfig.cs     # WILL BE MODIFIED → new parameters
└── CloudSystem.prefab      # WILL BE REPLACED

Assets/_Project/Scripts/Core/Weather/    # NEW DIRECTORY
├── WindManager.cs           # NEW
├── ServerWeatherController.cs # NEW
└── ServerStormManager.cs   # NEW

Assets/_Project/Scripts/Core/Clouds/    # NEW DIRECTORY
├── CloudManager.cs         # NEW (replaces CloudSystem)
├── NearCloudRenderer.cs    # NEW (replaces CloudLayer)
└── DistantCloudManager.cs  # NEW

Assets/_Project/Scripts/World/Clouds/
├── CumulonimbusCloud.cs    # DEPRECATE → replaced by StormController
├── CumulonimbusManager.cs  # DEPRECATE
├── CloudClimateTinter.cs   # KEEP (works with new system)
└── StormController.cs      # NEW
```

### 8.2 Documents

```
docs/world/CLOUD_system/
├── CLOUD_ARCHITECTURE.md          # Master architecture (READ THIS FIRST)
├── CLOUD_IMPLEMENTATION_PLAN.md  # Implementation phases with tests
├── CLOUD_TECHNICAL_SUMMARY.md    # Summary of decisions
├── CLOUD_VISUAL_DESIGN.md        # Visual design (shader, colors)
├── CLOUD_ONBOARDING.md           # For new team members
├── SESSIONS/                     # Session notes
│   └── SESSION_YYYY-MM-DD.md
└── ADR-Cloud-001-Rendering-Architecture.md # Key decisions
```

### 8.3 Scene Hierarchy

```
BootstrapScene.unity
├── NetworkManagerController
├── PlayerSpawner
├── WindManager                    ← NEW
├── ServerWeatherController        ← NEW, NetworkBehaviour
├── ServerStormManager             ← NEW, NetworkBehaviour
├── CloudManager                   ← NEW, DontDestroyOnLoad
│   ├── NearCloudRenderer Upper
│   ├── NearCloudRenderer Middle
│   ├── NearCloudRenderer Lower
│   └── DistantCloudManager
└── StormController (5 instances) ← spawned dynamically, DontDestroyOnLoad
```

---

## 9. Critical Decision Points

### 9.1 Must-Decide Before Implementation

| Question | Decision | Rationale |
|----------|----------|-----------|
| Target platform | PC + Mobile? | Affects step count, resolution |
| Cloud count per layer | 80/120/80 | Can adjust but stay near ~400 total |
| Impostor texture resolution | 512×512? | Higher = better quality, more memory |
| Storm visibility distance | 50km? | Affects gameplay balance |

### 9.2 Can Be Changed During Implementation

| Decision | Flexibility | Notes |
|----------|-------------|-------|
| Wind broadcast interval | 2-5 seconds | Affects responsiveness vs bandwidth |
| Recycling distance | 12-18km | Affects cloud density |
| Cloud size range | 50-200m | Visual variety |
| Instancing batch size | Per layer | Performance tuning |

### 9.3 Cannot Change (Architectural)

| Decision | Why Fixed |
|----------|----------|
| Altitude-based layers | Matches flight corridor gameplay |
| World-space distant impostors | Multiplayer requirement |
| Player-centered near clouds | Prevents boundary popping |
| WindManager as central | Single source of truth |
| Storms world-space | Multiplayer consistency |

---

## 10. Common Pitfalls

### 10.1 What NOT To Do

| ❌ DON'T | Why | ✅ DO Instead |
|----------|-----|---------------|
| Don't use Sky Dome as cloud layer | It's sky renderer, not volumetric | Use DistantCloudManager for horizon |
| Don't make impostors follow camera | Breaks multiplayer sync | Fixed world positions |
| Don't regenerate clouds per-scene | Causes boundary pop | Player-centered recycling |
| Don't use distance-based layers | Contradicts flight corridors | Altitude-based (Upper/Middle/Lower) |
| Don't implement full raymarch | Too expensive (4-20ms GPU) | Improved shader on instanced mesh |
| Don't create per-cloud GameObjects | 890+ draw calls | GPU instancing |

### 10.2 Warning Signs

| Warning | Likely Problem |
|---------|----------------|
| Cloud count > 500 | Too many, optimize |
| Draw calls > 20 for clouds | Instancing not working |
| GPU > 4ms for clouds | Shader too complex, reduce steps |
| Clouds popping at boundaries | Per-scene regeneration, switch to recycling |
| Players see different clouds | Distant impostors following camera, fix to world-space |

---

## 11. Quick Reference

### 11.1 Key Numbers

| Parameter | Value |
|----------|-------|
| Near cloud count | 280 (80+120+80) |
| Distant impostor count | 140 |
| Storm count | 5 |
| Total clouds | ~425 |
| Near cloud distance | 0-5km |
| Distant cloud distance | 5-15km |
| Recycle distance | 15km |
| Storm visibility | 50km |
| Wind broadcast | 0.5 Hz (2 sec) |
| Draw call budget | ~5-7 |

### 11.2 Key Scripts

| Script | Purpose |
|--------|---------|
| `WindManager.cs` | Central wind state, receives server RPC |
| `CloudManager.cs` | Orchestrates all cloud systems |
| `NearCloudRenderer.cs` | GPU instanced near clouds per altitude layer |
| `DistantCloudManager.cs` | Fixed world-position distant impostors |
| `ServerWeatherController.cs` | Server broadcasts wind at 0.5 Hz |
| `ServerStormManager.cs` | Server controls storm positions |
| `StormController.cs` | Client visual for server-authoritative storms |

### 11.3 Key Documents

| Document | When to Read |
|----------|--------------|
| `CLOUD_TECHNICAL_SUMMARY.md` | Every session start |
| `CLOUD_ARCHITECTURE.md` | Implementation decisions |
| `CLOUD_IMPLEMENTATION_PLAN.md` | Current phase, next task |
| `SYSTEM_OVERVIEW.md` | Scene streaming context |
| `GDD_02` (cloud sections) | Original requirements |

---

## 12. Session Notes Template

```markdown
# CLOUD_system Session — YYYY-MM-DD

## Phase
Phase X: [Name]

## Started With
- Context from: [documents read]
- Current state: [what was in place]

## Work Completed
- [ ] Task 1
- [ ] Task 2

## Findings
- [What was discovered]
- [What works, what doesn't]

## Tests Run
| Test | Result |
|------|--------|
| Wind sync | ✅/❌ |
| Cloud count | ✅/❌ |
| Performance | ✅/❌ |

## Blockers
- [Issues preventing progress]

## Next Session
1. [Immediate next task]
2. [Follow-up task]
3. [Future consideration]

## Decisions Made
- [Architectural decisions]
- [Parameter changes]
```

---

**Status:** 🔴 Ready for use — Read before every session