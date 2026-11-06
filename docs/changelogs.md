v0.2.0

World Content & Visual Fill

The world comes alive. Version 0.2.0 is a content milestone: the city of Primum is rebuilt as a real, walkable, optimized world, farms and ships get their final visuals, the UI is ported to the new v4-2 design, and loading/culling/lighting are tuned for performance. No new core mechanics — pure content and polish, as promised for 0.2.0.

Primum (the city)
- Primum rebuilt from raw import into merged, optimized prefabs: ~20k renderers reduced to ~1.2k, materials reduced 24,378 -> 7,081 slots (105 source names -> 45 targets + 31 variants), zero URP/Lit slots left in city and farms.
- 10,811 colliders generated for the city (houses 10,355 / base 74 / admin 382) so every building and alley is walkable and collidable.
- 2,363 doors placed as prefab instances with controllers — enter any building.
- 2,087 light sources with a nearest-light pool (<=96 active within 140 m) and day/night groups; lamps flicker individually; Forward+ rendering instead of the 4-light-per-object limit.
- HLOD: city triangles reduced 5.41M -> 267k (-95%) with L1 (>3 km) and L2 (>12 km) impostors; visibility director culls sub-pixel objects; city draw calls 46-48 at L1, 2 at L2.
- Y-up mesh rebase applied to 22 chunks (1:1 verified, max error 0.00003 m) so procedural materials read correctly on every face.
- House numbering and discovery: 534 house markers; opening a house's radius (15 m) reveals it on the map; farms always shown. Chart window gains "Houses" (discovered) and "Farms" layers.

Farms
- Farms 0-0, 0-2 (Timber), 0-3 (Rice), 0-4 (Cereal) and the Mega-Refinery 1 finished: M_PC_* material palette, doors and lamps as prefabs, static merges (90-121 batches per farm), colliders (up to 521 on farm 0-4).
- Mega-Refinery: 10,106 source meshes merged into 32 batches, 89 colliders (482k triangles, 0 degenerate), 500 door prefabs, 278 lamps, 9 material variants. Pending in-scene placement via Floating Origin pipeline.

Ships
- 17 NPC ships receive final reference visuals and rebaked deck navmeshes (Heavy, Medium, Light, HeavyII classes); Lorraine (class S) and Albatross get M_PC_* palette, merge and LOD.
- Ship "Gigant" — the largest ship in the game (floating city for 300+ people) — modeled and registered.
- Parom (ferry) routes: visual merge of parom + parom-ferma, client-side interpolation of cabin position, physics sync on cabin move, camera follows platform 1:1, edge-damping in meters so the cabin no longer flies straight over sagging cables. Trolley prefab prioritized over baked-in cabin; cabin preview at route stations (editor).
- HeavyII pilot shadow budget and LOD measured (LOD0 1538 / LOD1 235 / LOD2 104 draw calls).

UI (v4-2 port)
- Main Menu ported to the mod's v4-2 design: 3D atmosphere (sky, veil, ropes with ferry, dust, day cycle), smoke field, letter blow-out, altimeter ribbon, project blocks; fixes for tab readability, blur cascade, gamma blending; Inter font; fast first-click entry; world preload behind the menu; loading screen with progress on Start.
- Cloudy Navigator ("Cloud Map") ported steps 0-9 plus A7-A13: data feed, motion layer (48-pool), preview stage with its own camera, actions (quests, contracts, exchange-zone transfer), equipment slots, character preview with studio light, connection graph with sagging cables and sticker notes, calendar with notes, appearance button, skill learn/forget/slot binding, six combat slots, two graphs, navigation to calendar/relations/ship/cargo/quests.
- 168 new 2D icons imported as sprites and wired into asset Sprite fields across 17 categories (Actions, Categories, Combat, Contracts, Crafting, Equipment, Factions, Gathering, Knowledge, Map, NPC, Quests, Ship, Skills, Stats, Trade, Weapons).

Camera
- SpringArm camera reworked for moving platforms: samples through target history, anti-pop relative to target, 1:1 platform follow, lag applied only to local motion; no jitter on ferries.
- High-speed camera (admin run/noclip) no longer lags or jumps: lag capped at 2.5 m, snap removed, interpolation eased above 25 m/s, smoothing squeezed to 25% above 30 m/s.
- Broken zoom in ThirdPersonCamera prefab fixed (zoomStepPercent 50 -> 0.2, zoomChaseSpeed 50 -> 8).

Loading and culling
- World loading screen activates the scene after 0.9 with frames for rendering; background world preload disabled in MainMenu.
- Culling final acceptance (T-CULL01): Stage 8 PASS at 2,229 draw calls (from 27,131 on the heavy city pose), visibility director, shadow budget, HLOD switching. Light tracks L1-L6 closed (fade-in 0.15 s accepted, fade-out 0.30 s rejected by hold gate).
- Ship and door culling under GRD via VisibilityDirector.

Houses and map
- House numbering and discovery: 534 numbered house markers; discovering a house (15 m radius) reveals it on the chart; farms always shown; chart gains "Houses" (discovered) and "Farms" layers.

Notes
- Floating Origin rebasing works (F8/F9 pass on Primum, farms, ships) but runtimeRebaseReadiness remains NOT_READY (contracts/partial runtime evidence) — unchanged from 0.1.91.
- NPC navigation unchanged (LOS 97% from 0.1.90); terminal pain (docks) is the next focus.
- Play Mode verification is performed by the user; the agent marks runtime checks as NOT RUN.

----------------------------------
v0.1.45 

World, Ships & Named Crews
Since v0.1.31, the project has expanded with improved world and ship data, corrected FBX engine and propeller pivots, and a server-authoritative named crew system for NPC ships. Named pilots, ship-local crew manifests, deck anchors, ship attachment, spawning, and captain identities are now in place. Outside the Git history, the Primum port received a full blocking and low-poly pass, and the first light ship was modeled in low-poly style.
----------------------------------
v0.1.31

Markets & Contracts

Full contract lifecycle: board offers, active contracts, and completed records are now handled separately, so board updates no longer affect contracts you've already accepted.
New Receipt flow: Accept → Claim Cargo → Transport → Submit.
Contract cargo is now locked to its contract — it can't be sold, unloaded, or used in another contract.
Delivery completion is verified server-side (owner, state, location, ship, and exact cargo amount) before rewards are granted.
If a save fails, the whole economic operation rolls back — no partial states, no duplicate rewards or debt.
Distance data added for 12 market zones.
Factions

Faction identities moved to a stable data layer with data-driven combat relations.
Quests

First onboarding quest (Onboarding Alfa) now works end-to-end: pickup, NPC dialogue, objectives, and rewards.
Quests tab redesigned in the character window.
The stakeout/spy HUD now shows all objectives of the quest you're tracking.
Quest dialogue and objectives localized (RU/EN).
UI & Interaction

Contextual interaction hints (Talk/Use) for NPCs and world objects.
Gameplay input is now blocked while menus are open (dialogue, market, inventory, crafting, skill trees, customization, comm panel) — no more accidental attacks.
Trade / NPC Ships

Random NPC cargo trade mode.
Cargo limits now scale from the assigned ship.
World

Ship and cargo setup pass; minor world adjustments.

----------------------------

0.1.20 - # Markets & Contracts — Core Refactoring Stage Completed
As part of the contract system audit, we fixed several issues that could lead to the loss of active contracts, rewards being issued without cargo delivery, and state desynchronization after a save error.

## What Was Wrong
Previously, board offers, active contracts, and completed records shared the same logic. Because of this:
* **Contract list regeneration** could delete an already accepted contract.
* **A failed contract** could continue to occupy an active slot.
* **A delivery contract** could be completed without the cargo being correctly written off.
* **A Receipt contract** lacked a fully functional issue-and-return lifecycle.
* **A persistence error** could save only a partial economic operation.

## What Changed
### Separated the Contract Lifecycle
Available offers, player's active contracts, all contract records, and completed/failed contracts are now stored separately. This allows for safe board updates without affecting already accepted contracts.

### Made Delivery Completion Server-Authoritative
Before completion, the server now verifies the owner, contract state, location, ship, and exact cargo amount. Cargo is written off before rewards are granted. If an error occurs at any stage, the state rolls back.

### Implemented Full Receipt Flow
Receipt contracts now follow this sequence:
```text
Accept Contract → Claim Cargo → Transport → Submit Contract
```
Cargo is issued via a distinct operation and marked as belonging to a specific contract, player, and ship. This cargo cannot be sold separately, unloaded, or used in another contract. 

Upon cancellation or expiration, the cargo returns to the reserve. If the cargo was never issued, no debt is incurred.

### Added Global Rollback for Persistence Errors
When a save fails, not only the contract data is rolled back, but also related assets:
* Cargo
* Credits
* Debt
* Active indexes
* Receipt ownership metadata

This prevents partially applied economic operations.

### Updated Network and UI
Added a distinct RPC operation for claiming Receipt cargo and verifying ship ownership and zone. The UI now features a cargo claim button, a `[CONTRACT]` indicator, and locks preventing manual unloading of contract cargo.

### Updated Configuration
* Contract timers moved to `ContractCatalog`.
* A full distance graph has been calculated for 12 deployed `MarketZones`.
* Two locations without a fully functional `MarketZone` remain disabled.

## Results
The contract system is now significantly better protected against:
* Disappearance of active contracts
* Duplicate reward payouts
* Completion without cargo
* Sale or loss of Receipt cargo
* Partial state saving
* Duplicate debt or reward accrual

------------------------------------------------

✅ 0.1.15 — Quests, Character & Main Menu (Complete)
In short: quests work end-to-end, switching your character's gender is seamless, the main menu is more user-friendly, and the world remembers exactly where you left off.
• Quests: First onboarding quest (Onboarding Alfa) — full loop: pickup, NPC dialogue, objectives, rewards
• Quests: No duplicate rewards, turn-in only available upon completing all objectives, NPC proximity check
• Character: Male/female body model toggle — model, skeleton, and animations swap entirely; skills remain fully functional after switching
• Main Menu: Language selection, external link buttons, changelog, version display, settings panel, solo-dev notice
• Saving: Ship position preserved when returning to menu, ship keys, equipment persistence
• Camera: Stable spring-arm camera
• Visuals: Personalized edge detection
A post-apocalyptic sky MMO sandbox inspired by the book Integral Pyavitsa. Survivors live on floating islands high above the Cloud Sea. You are a trader and a pilot: take on cargo, haul it through storms and wind corridors, fight off pirates, invest in your ship — and set off again.
Trade → Fly → Fight → Upgrade → Battle
The game is currently at version 0.1.0 — the foundation is in place. From here we move through milestones, each one a complete, playable stage.

-------------------------------------------------

✅ 0.1.0 — Foundation (Complete)
~4 months of development, 1,621 commits. Nearly the entire core has been built:
• Economy, combat, ships, quests — 90%+ of the codebase • Architecture designed for multiplayer from day one: "the server decides, the client displays" • A 480×320 km world, 24 zones, day/night cycle, thunderstorms • Cloud Ocean 3.0 — volumetric clouds, ship trails, storm cells, lightning • Content creation toolkit: quests, dialogues, and NPCs can be built with zero coding • Localization into 9 languages
➡ 0.2.0 — World Content. Hand-crafted quests, NPCs, routes, ship types. No new mechanics — pure content and polish.
🚢 0.3.0 — Visuals. Full ship and environment models replacing primitives.
⚔ 0.4.0 — Skills Come Alive. Animations and VFX for all combat abilities.
🎧 0.5.0 — First Start-to-Finish Playable Build. Audio and polish. A complete playthrough from beginning to end with comfortable visuals.
🖥 0.6.0 — Interface. UI/UX: HUD, menus, new-player onboarding.
🤝 0.7.0 — Co-op. Steam: friends, invites, up to 4 players.
🌐 0.8.0 — MMO. Server build. Dedicated servers: seamless world, accounts, anti-cheat.
🏆 1.0 — Release. Final visuals and audio. A fully polished co-op MMO.

--------------------------------------------------