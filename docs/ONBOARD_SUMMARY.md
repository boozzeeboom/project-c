# Project C: The Clouds — Onboarding Summary

**Date:** 2026-04-27
**Last Updated:** After /graphify indexing + subagent exploration

---

## Project Overview

**Name:** Project C: The Clouds ("Живые Баржи")
**Type:** MMO adventure game set in open sky above poisonous clouds (2090)
**Author:** Indeed (solo dev, 332 commits)
**Tech Stack:** Unity 6000.4.1f1 · URP 17.0.3 · NGO 2.11.0 · .NET 8

---

## Current Branch & State

- **Branch:** main (clean, up to date with origin)
- **Last activity:** FOMP fixes, NPC iteration, Trade system work (April 2026)
- **Active sprints:** R1-R4 refactoring series

---

## Architecture

- **Server-authoritative** model with NGO (Netcode for GameObjects)
- **Floating Origin MP** — handles large world coordinates (>100k units) to prevent float precision issues
- **Custom world streaming** — not using World Streamer 2 asset (per ADR-0002 decision)
- **Singletons + RPC** (ClientRpc/ServerRpc) pattern throughout
- **No Assembly Definitions** — all code compiles to single Assembly-CSharp.dll

---

## Key Systems

### Player System
- `NetworkPlayer` — main player controller with pedestrian + ship modes
- `PlayerStateMachine` — handles mode switching
- `ShipController` — complex ship physics (4 flight classes, altitude corridors, co-op piloting)

### World System
- `FloatingOriginMP` — multiplayer floating origin implementation
- `WorldStreamingManager` → `WorldChunkManager` → `ChunkLoader` → `ProceduralChunkGenerator`
- `MountainMeshBuilderV2` — procedural mountain generation (ADR-0001)

### Trade System
- `TradeMarketServer` — server-side trade logic
- `ContractSystem` + `ContractBoardUI` — contract board
- `LocationMarket` — per-location market with supply/demand
- `CargoSystem` — cargo weight affecting ship speed

### UI System
- `UIManager` — centralized panel management
- `InventoryUI` — circular wheel inventory
- `AltitudeUI` — altitude corridor HUD

### Cloud/Veil System
- `CloudSystem` → `CloudLayer` — 3 cloud layers
- `VeilSystem` — poisonous fog hiding poisoned surface
- `CumulonimbusCloud` + `CumulonimbusManager` — storm clouds

---

## Documentation

- **16 GDD documents** (complete coverage: gameplay, world, ships, trade, UI, audio, progression, factions, lore)
- **ADR-0001** — Mountain mesh v2 generation
- **ADR-0002** — World streaming architecture
- **World Lore Book** — based on "Интеграл Пьявица" by Bruno Arendt
- Extensive session reports in `docs/world/LargeScaleMMO/combinesessions/`

---

## Technical Debt (Refactoring Plan)

### P0 (Critical)
- Allocations in hot paths (Update loops)
- `FindObjectsByType` / `FindAnyObjectByType` in Update loops

### P1 (Should-Fix)
- Inventory doesn't implement `INetworkSerializable`
- Mixed Input System + KeyCode (should unify)
- `Thread.Sleep(250)` in `NetworkManagerController`

### P2 (Nice-to-Have)
- Magic numbers in ShipController
- Runtime UI creation instead of prefabs

---

## Graphify Index

- **103 nodes, 97 edges, 38 communities** (from partial run with 2 chunks)
- `graphify-out/graph.html` — interactive visualization
- `graphify-out/graph.json` — raw graph for queries
- `graphify-out/GRAPH_REPORT.md` — audit trail

**Note:** Full run was truncated due to corpus size (432 files, ~403k words). The 2 chunks that ran cover core code systems well.

---

## File Organization

```
Assets/_Project/
├── Scripts/
│   ├── Core/          # Inventory, CloudSystem, InteractableManager, etc.
│   ├── Editor/        # World building tools
│   ├── Network/       # NetworkPlayerSpawner
│   ├── Player/        # NetworkPlayer, ShipController, PlayerStateMachine
│   ├── Ship/          # AltitudeCorridor, Module system, Fuel, Wind
│   ├── UI/            # UIManager, InventoryUI, AltitudeUI
│   ├── World/
│   │   ├── Chest/     # NetworkChestContainer
│   │   ├── Clouds/    # Cumulonimbus, Veil
│   │   ├── Core/      # FloatingOrigin, MountainMassif, WorldData
│   │   ├── Generation/# MountainMeshBuilder, NoiseUtils
│   │   ├── Npc/       # NpcEntity, NpcDialogueManager
│   │   └── Streaming/ # FOMP, ChunkLoader, WorldStreamingManager
│   └── Trade/         # TradeMarketServer, ContractSystem, TradeUI
├── Editor/
├── Prefabs/
├── Scenes/
└── Documentation/     # (in root docs/ folder)
```

---

## Next Steps (Inferred)

1. Continue FOMP (Floating Origin MP) fixes — current active work
2. Complete R3/R4 refactoring sprints
3. Ship system Phase 2 implementation (per GDD_10)
4. NPC dialogue / quest system expansion

---

## Useful Commands

```bash
# Check status
git status && git log --oneline -10

# Find specific system files
find Assets/_Project/Scripts -name "*.cs" | xargs grep -l "class ShipController"

# Check current work
git log --oneline -20 --all | head -20
```

---

**Last graphify update:** 2026-04-27
**Onboarding status:** Complete — ready to assist with any task