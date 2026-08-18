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