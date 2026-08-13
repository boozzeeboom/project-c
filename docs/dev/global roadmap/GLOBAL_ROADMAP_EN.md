# Project C: The Clouds — Global Roadmap

**Document version:** 1.0 · **Product version:** v0.1.0 · **Date:** 07.08.2026
**Purpose:** public development roadmap — clear to players, informative to publishers/investors. Timeframes are given only for the completed stretch 0.0.1 → 0.1.0; the future is deliberately undated — quality matters more than the calendar.

---

## 1. What this game is

**Project C: The Clouds** — an MMO sandbox set in a post-apocalyptic sky world. The surviving civilization lives on floating islands and peaks above an endless cloud sea, divided into guilds, trading houses, and the mysterious Veil at the lower edge of the world.

**The core of the game is one continuous loop:**

> **Trade → Fly → Fight → Upgrade → Fight again**

You are a free trader and pilot: pick up cargo on one island, carry it through storms and wind corridors, fight off pirates, earn money, invest it into your ship and gear — and hit the skies again. The world is alive: NPCs have jobs and daily schedules, prices react to player actions, trade convoys cross the sky, and storm cells change weather and routes.

**What makes it unique:** the "sky civilization" setting + the full "trade-fly-fight" cycle in a single product, architected from day one for co-op and MMO (not "build a single-player game, then redo it").

---

## 2. How versions work

| Format | Meaning | Example |
|---|---|---|
| `0.X.0` | **Milestone** — a key, complex product change | 0.2.0 — world content |
| `0.X.YY` | **Task** within a milestone — a concrete step | 0.2.01 — first quest pack |
| `1.0` | **Release** — a fully debugged co-op MMO product | 1.0 |

The product is currently at milestone **v0.1.0** — the foundation. Each next milestone (0.2.0, 0.3.0 … 1.0) is reached through a series of tasks (0.X.01, 0.X.02 …); only when every task of a milestone is done and verified does the version move on.

---

## 3. What happened: the path 0.0.1 → 0.1.0

**⏱ Timeframe (the only milestone with dates):** active development — **~4 months** (April — August 2026), one developer, 1,621 commits.

In that time, from an empty project, we built:

| Block | What was done |
|---|---|
| **Engine & networking** | Unity 6 + URP + Netcode for GameObjects. Server-authoritative architecture: economy, combat, inventory and quests are computed on the server; the client only displays and sends commands. |
| **World** | 24 streaming scenes (480×320 km world), procedural peaks, day/night with 5 phases, moon and constellations, wind zones, cloud sea. |
| **Ships** | 4 classes, flight physics, fuel, upgrade modules, station docking, damage and repair, cargo hold, wind corridors. |
| **Economy** | Dynamic prices (supply/demand react to player actions), trading, delivery contracts, warehouses, resource exchange, NPC traders, debt system. |
| **Combat & progression** | Ranged/thrown/melee combat, AOE attacks, crits, armor; stats, 13 equipment slots, skill tree, knowledge and recipes (unlocked in the world, lost on death). |
| **Living world** | Quests and dialogues with NPCs, factions and reputation, hostile NPCs with AI, civilian NPCs with daily schedules, peaceful NPC ships with routes and timetables. |
| **Tools** | In-house editors for content: node-based quest/dialogue/NPC editor, CSV import, custom inspectors for every system (see §4.2). |
| **Visuals** | Volumetric clouds (Cloud Ocean 3.0), obstacle-avoiding camera, post-processing, edge-detection stylization. |
| **Infrastructure** | Persistence of progress (inventory, quests, contracts, ship positions), main menu, localization. |

**The key principle of this phase:** build not "a game prototype" but a **tool for making a game** — so the next stage (filling in content) is fast and doesn't require programming every detail.

---

## 4. What we have now: v0.1.0

### 4.1 Codebase — 90%+ ready

- **Debugged for solo play**, yet **architected for multiplayer/co-op**: server-authoritative systems, anti-cheat at the architecture level (a client cannot "gift" itself an item), rate-limiting, resilience to disconnects.
- **23 subsystems, 558 code files** (~27,400 lines of runtime code + 71 editor tool files), 26 shaders, 1,199+ configuration assets.
- **Persistence**: player progress (inventory, quests, contracts, ships, reputation) survives restarts — a storage interface is ready and can be connected to any server database.
- **Performance**: profiling of 14 subsystems, runtime monitoring, elimination of hitches.

### 4.2 Content creation tools — a "world builder"

This is not just game code, it's also the **code of the tools** used to fill the world:

- **Unified Quest Graph** — a single node-based editor: one graph shows NPC → dialogues → quests → rewards. Built for non-programmers (a game designer/scriptwriter works with a mouse, no code).
- **DialogTree Editor** — visual dialogue editor with conditions and actions.
- **CSV pipeline** — quests, NPCs and dialogues can be managed in spreadsheets and imported in bulk (handy for leads/content teams).
- **Custom editors** for every system: markets (edit right on the scene), ships, docks, NPC brains, NPC ship schedules, skill trees, factions, quest rewards.
- **Ship preset creators**, summary windows across all world objects.

> Result: content (quests, NPCs, routes, skills, modules, ship types) is created **without writing code** — this is the foundation of milestone 0.2.0.

### 4.3 Technical visuals — "prepared groundwork"

- **Cloud Ocean 3.0** — volumetric clouds (raymarch): 4 layers 800–7000 m, Ghibli style, day/sunset, interactivity — ships leave trails in the clouds, wake cone, contrails, storm cells with procedural shape and lightning.
- **Camera** — full cycle: obstacle avoidance, adaptive distance, smooth wall fade, zoom, auto-snap on teleport.
- **Day/night** — 5 phases, moon with phases, 215 stars and 24 constellations.
- **Post-processing** — bloom, color grading, vignette, edge detection.
- **Character animation** — BlendTree movement, combat clips, gender/body/color customization.
- **VFX infrastructure** — 3-phase effect system (cast → flight → impact) with pooling, assigned to 27+ skills.
- **The Veil** (VeilRaymarch) — the lower edge of the world, a key setting element.

### 4.4 UI/UX and localization

- **UI Toolkit** (Unity's modern UI system): character window (5+ tabs), skill tree, market, dock module manager, dialogues, crafting, ESC menu with settings, main menu.
- **Rebindable keys** — players remap controls to their liking (31 actions).
- **Localization into 9 languages** (EN/RU/DE/FR/ES/PT/IT/PL/UK) with runtime switching — translators work via CSV, without touching code.
- Unified theme, UI component factory, centralized window management.

### 4.5 The end-to-end gameplay loop (core loop)

Verified from A to Z: **take cargo → load → take off → fly through winds and storms → dock → sell → spend on modules/repair/gear → accept a contract → hit the skies again.** Combat is built into this loop: pirates on the routes, convoy escorts, reputation penalties for attacks.

### 4.6 Project numbers (for reference)

| Metric | Value |
|---|---|
| Commits | 1,621 |
| Code files (C#) | 558 (runtime) + 71 (editor) |
| Lines of runtime code | ~27,400 |
| Subsystems | 23 |
| Shaders / VFX | 26 / 2 |
| Config assets (ScriptableObject) | 1,199+ |
| World scenes | 24 (streaming, 6×4 grid) |
| Localization languages | 9 |
| Documents | 817 |

---

## 5. What's next: milestones 0.2.0 → 1.0

### 🗺 0.2.0 — "Filling the world" *(no new mechanics — content and debugging only)*
1. **Original content**: story quests and chains, skills, dialogues — via the ready editors (§4.2), no programming.
2. **World**: NPC placement with activities, NPC convoy trade routes, locations and their economies.
3. **Ships**: ship types, upgrade modules, class balance.
4. **Debugging everything**: a full core-loop pass, economy and combat balance, fixing found bugs.

### 🚢 0.3.0 — "Core visuals: ships and environment"
1. Ship models for all classes (per the Art Bible, "comfortable low-poly").
2. Environment: cities, peaks, landing pads, bridges, replacing primitives.
3. Materials and textures, integration into existing ship and world systems.
4. Visual details: cargo, resources, interactive objects.

### ⚔️ 0.4.0 — "Skills come alive: animations and VFX"
1. Combat skill animations (melee/ranged/thrown, casts).
2. VFX for every skill: cast → flight → impact (infrastructure is ready — §4.3).
3. NPC animations: combat, activities, reactions.
4. Animation sync in multiplayer (visuals on all clients).

### 🎧 0.5.0 — "Audio and polish" — *first playable version*
1. Sound: music, ship engines, wind, combat, ambience (3D audio).
2. Gameplay polish: balance, tutorial, comfortable controls.
3. Full-frame performance audit and optimization for mid-range hardware.
4. **Milestone criterion: a fully playable version "from start to finish" with comfortable visuals (between blocking and low-poly).**

### 🖥 0.6.0 — "UI/UX"
1. Interface redesign per the Art Bible: HUD, menus, windows.
2. New player onboarding (first 30 minutes).
3. Accessibility: settings, scaling, gamepad.
4. Usability tests and fixes based on results.

### 🤝 0.7.0 — "Co-op"
1. Steam integration: friends, invitations, sessions.
2. Co-op up to 4 players: shared world, joint quests, trading and combat.
3. Hosting: P2P and/or dedicated server for co-op sessions.
4. Multiplayer gameplay debugging in real sessions.

### 🌐 0.8.0 — "Server-side MMO"
1. Dedicated servers, running the world server-side (already designed into the architecture — §4.1).
2. Deploying 24-scene streaming + a single seamless world.
3. Accounts, server-side persistence, anti-cheat.
4. Scaling: sharding, monitoring, infrastructure.

### 🏆 1.0 — "Release"
1. Final visuals and audio (full art pipeline per the Art Bible).
2. Final polish and balancing based on closed-test feedback.
3. Release of a fully debugged co-op MMO product.

---

## 6. Honest limitations (what's not ready yet)

To keep the roadmap honest — what is deliberately deferred:

- **Original content**: data structures are ready, but quests/NPCs/dialogues are currently filled with test data to validate systems. Real content is written in milestone 0.2.0.
- **Visuals**: ships and environment are primitives/blocking (functional, but not final). Models — milestone 0.3.0.
- **Skill animations and VFX**: infrastructure and some effects are ready; the full set — milestone 0.4.0.
- **Sound**: the game has no audio (except UI clicks) — milestone 0.5.0.
- **24-scene streaming**: code is written, but the active build runs one scene (gameplay focus); full streaming — milestone 0.8.0.
- **Dynamic network spawning** of objects requires setting up the network prefab registry (known task, does not affect current gameplay).

**Why this is fine:** the sequence is chosen so that at every milestone the system is stable and playable, not "everything at once and broken." Visuals and audio deliberately come after gameplay — balance iterations are cheaper, and final art doesn't get redone.

---

## 7. One-picture summary

| Milestone | Focus | Outcome |
|---|---|---|
| **0.1.0** ✅ (~4 mo.) | Foundation: networking, economy, combat, ships, tools, visual groundwork | Ready-made world builder, 90%+ of codebase |
| **0.2.0** | Content and debugging (no new mechanics) | World filled, everything debugged |
| **0.3.0** | Core visuals: ships + environment | Primitives replaced with models |
| **0.4.0** | Skill animations + VFX | Combat comes alive |
| **0.5.0** | Audio + polish | **First fully playable version, start to finish** |
| **0.6.0** | UI/UX | Comfortable interface & onboarding |
| **0.7.0** | Co-op (Steam) | Play together, up to 4 players |
| **0.8.0** | Server-side MMO | Seamless world, dedicated server, anti-cheat |
| **1.0** | Final visuals + audio | **Co-op MMO release** |

---

*Document is maintained in `docs/dev/global roadmap/`. On every milestone reached, update: milestone status, "what changed", next step.*
