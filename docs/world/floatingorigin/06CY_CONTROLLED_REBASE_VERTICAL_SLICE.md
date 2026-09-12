# T-FO06CY — Controlled rebase vertical slice

Date: 2026-09-12

## Decision

The project has reached the point where additional pure floating-origin contracts are no longer justified before runtime integration. The previous plan continued splitting the problem into contracts after the safety boundaries were already sufficient to attempt one concrete transaction. That was a planning error: it delayed the first real rebase and expanded the documentation surface without producing runtime evidence.

This iteration starts the first concrete, user-controlled vertical slice.

## Implemented scope

Added `GlobalMotionControlledRebaseSlice` and bound it to `NetworkManager` in `BootstrapScene`.

The slice is deliberately closed-world and explicit:

- source scene: `Assets/_Project/Scenes/World/WorldScene_0_0.unity`;
- participants: all root GameObjects of that loaded scene, sorted and recorded as `WORLD_SCENE_ROOT/<name>`;
- local player: the NGO local `PlayerObject`, added only when it is not already contained by a registered world root;
- optional additional root: only when explicitly assigned in the component;
- no global `FindObjectsByType` discovery and no ShipDeck/NPC/NavMesh/NetworkBaseline admission;
- no automatic threshold trigger.

The initial frame policy is explicit in the component: origin `(0,0,0)` by default, `maxLocalCoordinate=100000`, threshold `256`, quantum `256`. The plan is created from the live local player position using `OriginRebasePlan.TryCreate`; the normal Bootstrap spawn placement is not treated as a rebase.

## Transaction

The user starts the transaction with `F8` or the component context menu. `F9` runs the same path but forces validation failure after the transform application so the rollback path is exercised.

The transaction uses the existing coordinator and participant contracts:

1. resolve the local NGO player;
2. build the explicit scene-root participant set;
3. create `OriginRebasePlan` from the current local player position;
4. freeze the player's `CharacterController`;
5. preflight and capture participant snapshots;
6. apply `Plan.LocalTranslation` to every admitted root;
7. call `Physics.SyncTransforms()`;
8. rebuild the admitted participant state;
9. validate finite state;
10. publish the new local frame and generation;
11. commit and release the controller;
12. on failure, restore snapshots in reverse order and release the controller.

Evidence is emitted through the existing `[T-FO06Y]` probe with `runtimeRebase` phases: `FrameInitialized`, `Requested`, `Frozen`, `FramePrepared`, `Applied`, `PhysicsSynchronized`, `Rebuilt`, `Validated`, `Published`, `Completed`, `RollbackRequested`, `RollbackCompleted`, `RollbackFaulted`, `Rejected` and `Released`.

## Important interpretation boundaries

The observed `y≈0 → y≈2502` transition in the earlier capture is the ordinary Bootstrap-to-world spawn teleport. It is not a floating-origin rebase and is not evidence of a rebase writer bug.

The legacy warning `Failed to create agent because it is not close enough to the NavMesh` remains an ignored legacy issue. It is excluded from this slice's floating-origin acceptance and must not trigger more contract work.

## Verification status

- Script compilation: PASS (`check_compile_errors` returned no errors).
- BootstrapScene binding: PASS; component is attached to `NetworkManager` and the scene was saved.
- Play Mode success capture: NOT RUN — user-controlled only.
- Play Mode rollback capture: NOT RUN — user-controlled only.
- Screenshots: NOT RUN — user-controlled only.
- NGO baseline publication after rebase: NOT integrated in this first slice and must be evaluated before promoting the slice to multiplayer acceptance.
- ShipDeck/NPC/NavMesh lifecycle: intentionally out of scope for this player/world slice.

## User capture procedure

1. Start the existing pilot from `BootstrapScene` as usual.
2. Wait until the local player has spawned and the existing baseline evidence is visible.
3. Press `F8` once. Confirm the log contains `runtimeRebase.Requested`, `Applied`, `PhysicsSynchronized`, `Validated`, `Published`, `Completed` and `Released`.
4. Confirm the player's local position and the loaded WorldScene roots moved by the same translation while relative gameplay state remains intact.
5. Repeat from a fresh run and press `F9`. Confirm `RollbackRequested`, `RollbackCompleted` and `Released`, and confirm the participant positions return to their pre-request values.
6. Save the relevant Unity console capture and report the result before any further floating-origin implementation.
