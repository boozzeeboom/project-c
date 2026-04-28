# Graph Report - docs/world/LargeScaleMMO/2_iteration_scene-mode  (2026-04-28)

## Corpus Check
- 13 files · ~10,418 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 361 nodes · 618 edges · 12 communities detected
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 45 edges (avg confidence: 0.79)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Altitude Corridor System|Altitude Corridor System]]
- [[_COMMUNITY_Chunk Loading System|Chunk Loading System]]
- [[_COMMUNITY_Ship Controller|Ship Controller]]
- [[_COMMUNITY_Scene Management|Scene Management]]
- [[_COMMUNITY_Network Player|Network Player]]
- [[_COMMUNITY_World Scene Generator|World Scene Generator]]
- [[_COMMUNITY_Player Chunk Tracking|Player Chunk Tracking]]
- [[_COMMUNITY_Client Scene Loader|Client Scene Loader]]
- [[_COMMUNITY_Player State Machine|Player State Machine]]
- [[_COMMUNITY_World Streaming Manager|World Streaming Manager]]
- [[_COMMUNITY_Player Controller|Player Controller]]
- [[_COMMUNITY_Streaming Setup Runtime|Streaming Setup Runtime]]

## God Nodes (most connected - your core abstractions)
1. `ShipController` - 45 edges
2. `NetworkPlayer` - 38 edges
3. `WorldSceneGenerator` - 31 edges
4. `ServerSceneManager` - 30 edges
5. `WorldStreamingManager` - 27 edges
6. `PlayerChunkTracker` - 21 edges
7. `ClientSceneLoader` - 20 edges
8. `ChunkLoader` - 15 edges
9. `PlayerStateMachine` - 13 edges
10. `SceneID Struct` - 12 edges

## Surprising Connections (you probably didn't know these)
- `OnGUI()` --calls--> `ToString()`  [INFERRED]
  Assets\_Project\Scripts\World\Streaming\WorldStreamingManager.cs → Assets\_Project\Scripts\World\Streaming\WorldChunkManager.cs
- `Duplicate AudioListener Warning` --caused_by--> `WorldSceneGenerator`  [INFERRED]
  docs/world/LargeScaleMMO/2_iteration_scene-mode/editor_errors/ERRORS_ANALYSIS_28042026.md → docs/world/LargeScaleMMO/2_iteration_scene-mode/TEST_WORKFLOW.md
- `PlayerChunkTracker` --references--> `SceneID Struct`  [EXTRACTED]
  docs/world/LargeScaleMMO/2_iteration_scene-mode/IMPLEMENTATION_LOG.md → docs/world/LargeScaleMMO/2_iteration_scene-mode/IMPLEMENTATION_PLAN.md
- `Scene Grid 4x6` --conceptually_related_to--> `SceneID Struct`  [INFERRED]
  docs/world/LargeScaleMMO/2_iteration_scene-mode/РїР»Р°РЅ СЃС†РµРЅ.txt → docs/world/LargeScaleMMO/2_iteration_scene-mode/IMPLEMENTATION_PLAN.md
- `ClientSceneLoader` --references--> `NGO CheckObjectVisibility`  [EXTRACTED]
  docs/world/LargeScaleMMO/2_iteration_scene-mode/IMPLEMENTATION_PLAN.md → docs/world/LargeScaleMMO/2_iteration_scene-mode/GRAPH_REPORT.md

## Hyperedges (group relationships)
- **Two-Layer Architecture: Scene Layer and Chunk Layer** — scene_layer_79999x79999, chunk_layer_2000x2000, sceneid_struct, sceneregistry_so, worldscenemanager, serverscenemanager, clientsceneloader [EXTRACTED 0.85]
- **Bootstrap Scene Runtime Components** — bootstrap_scene, serverscenemanager, clientsceneloader, scene_transition_coordinator, altitude_corridor_system, floatingoriginmp [EXTRACTED 0.85]
- **World Scene Generated Content** — world_scene, worldscene_generator_editor, horizontal_wrap, pole_blockers [EXTRACTED 0.85]
- **NGO Scene-Based Visibility Filtering** — sceneboundnetworkobject, checkobjectvisibility, clientsceneloader, serverscenemanager [EXTRACTED 0.85]
- **Scene Grid Topology with Wrap and Poles** — scene_grid_6x4, horizontal_wrap, pole_blockers, sceneid_struct, sceneregistry_so [EXTRACTED 0.85]
- **Scene-Based World Architecture** — sceneid_struct, player_scene_tracker, scene_manager, client_scene_loader, scene_bound_network_object, ngo_check_object_visibility, networkhide_networkshow, scene_preloader [EXTRACTED 0.85]
- **Scene Generation Pipeline** — bootstrap_scene_generator, world_scene_generator, scene_registry_scriptableobject, scene_grid_4x6 [EXTRACTED 0.90]
- **Scene Visibility System** — ngo_check_object_visibility, scene_bound_network_object, networkhide_networkshow, player_scene_tracker [EXTRACTED 0.90]
- **Editor Bugs Discovered During Runtime** — trade_debug_tools, floating_origin_camera_bug, cloud_system_config_bug, audio_listener_duplicate, event_system_duplicate [EXTRACTED 0.85]
- **Scene Transition Flow** — player_scene_tracker, scene_manager, client_scene_loader, networkhide_networkshow, scene_bound_network_object [EXTRACTED 0.90]
- **FloatingOriginMP Not Needed Rationale** — unity_float32_precision, scene_79_999_safe, floating_origin_mp [EXTRACTED 0.95]

## Communities

### Community 0 - "Altitude Corridor System"
Cohesion: 0.05
Nodes (60): AltitudeCorridorData, AltitudeCorridorSystem, Duplicate AudioListener Warning, Bootstrap Scene, BootstrapSceneGenerator, BootstrapSceneGenerator Editor Script, NGO CheckObjectVisibility, Chunk Layer (2,000x2,000) (+52 more)

### Community 1 - "Chunk Loading System"
Cohesion: 0.06
Nodes (7): ChunkLoader, ProjectC.World.Streaming, MonoBehaviour, WorldChunkManager, PoleBlockerComponent, ProjectC.Editor, WorldStreamingManager

### Community 2 - "Ship Controller"
Cohesion: 0.08
Nodes (4): KeyCodeToKey(), OnValidate(), ProjectC.Player, ShipController

### Community 3 - "Scene Management"
Cohesion: 0.08
Nodes (5): NetworkBehaviour, ProjectC.World.Scene, SceneTransitionCoordinator, ProjectC.World.Scene, ServerSceneManager

### Community 4 - "Network Player"
Cohesion: 0.07
Nodes (2): NetworkPlayer, ProjectC.Player

### Community 5 - "World Scene Generator"
Cohesion: 0.13
Nodes (2): EditorWindow, WorldSceneGenerator

### Community 6 - "Player Chunk Tracking"
Cohesion: 0.15
Nodes (2): PlayerChunkTracker, ProjectC.World.Streaming

### Community 7 - "Client Scene Loader"
Cohesion: 0.15
Nodes (3): ClientSceneLoader, ProjectC.World.Scene, Equals()

### Community 8 - "Player State Machine"
Cohesion: 0.25
Nodes (2): PlayerStateMachine, ProjectC.Core

### Community 9 - "World Streaming Manager"
Cohesion: 0.22
Nodes (5): ProjectC.World.Streaming, ToString(), WorldChunk, OnGUI(), ProjectC.World

### Community 10 - "Player Controller"
Cohesion: 0.25
Nodes (2): PlayerController, ProjectC.Player

### Community 11 - "Streaming Setup Runtime"
Cohesion: 0.39
Nodes (2): ProjectC.Core, StreamingSetupRuntime

## Knowledge Gaps
- **32 isolated node(s):** `ProjectC.World`, `ProjectC.World.Streaming`, `ProjectC.World.Streaming`, `WorldChunk`, `ProjectC.World.Streaming` (+27 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Network Player`** (42 nodes): `NetworkPlayer.cs`, `NetworkPlayer`, `.ApplyServerPositionRpc()`, `.ApplyShipState()`, `.ApplyWalkingState()`, `.AutoResetOriginAfterTeleport()`, `.ContractAcceptServerRpc()`, `.ContractCompleteServerRpc()`, `.ContractFailServerRpc()`, `.ContractListClientRpc()`, `.ContractRequestServerRpc()`, `.ContractResultClientRpc()`, `.FindNearestInteractable()`, `.FindNearestShip()`, `.FixedUpdate()`, `.GetNearbyInteractableName()`, `.GetOwnerId()`, `.HasNearbyInteractable()`, `.HidePickupRpc()`, `.IsNearbyChest()`, `.OnDestroy()`, `.OnNetworkDespawn()`, `.OnNetworkSpawn()`, `.OnWorldShifted()`, `.OpenChestRpc()`, `.ProcessMovement()`, `.SpawnCamera()`, `.SpawnInventory()`, `.SubmitSwitchModeRpc()`, `.TeleportAllClientRpc()`, `.TeleportLocal()`, `.TeleportServerRpc()`, `.TeleportToPosition()`, `.TradeBuyServerRpc()`, `.TradeResultClientRpc()`, `.TradeSellServerRpc()`, `.TryPickup()`, `.Update()`, `ProjectC.Player`, `.GetExitPosition()`, `.RemovePilot()`, `.RemovePilotRpc()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `World Scene Generator`** (31 nodes): `EditorWindow`, `WorldSceneGenerator`, `.AddAltitudeCorridorSystem()`, `.AddClientSceneLoader()`, `.AddCloudSystem()`, `.AddFloatingOriginMP()`, `.AddScenesToBuildSettings()`, `.AddSceneTransitionCoordinator()`, `.AddServerSceneManager()`, `.AddWorldSceneManager()`, `.AddWorldStreamingManager()`, `.CreateBoundaryCollider()`, `.CreateBoundaryColliders()`, `.CreateBoundaryVisualization()`, `.CreateDirectionalLight()`, `.CreateGroundMaterial()`, `.CreateGroundPlane()`, `.CreateOutputDirectory()`, `.CreatePlayerPrefab()`, `.CreatePlayerSpawnPoint()`, `.CreatePoleBlocker()`, `.CreateRuntimeSetup()`, `.CreateScene()`, `.CreateSceneLabel()`, `.CreateSceneRegistry()`, `.GenerateAllScenes()`, `.GetRowColor()`, `.OnGUI()`, `.RegenerateAll()`, `.RegenerateMaterial()`, `.ShowWindow()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Player Chunk Tracking`** (25 nodes): `PlayerChunkTracker.cs`, `PlayerChunkTracker`, `.FindPlayerTransformCoroutine()`, `.ForceUpdatePlayerChunk()`, `.GetChunkAtPosition()`, `.GetChunkIdAtPosition()`, `.GetClientLoadedChunkCount()`, `.GetPlayerChunk()`, `.GetPlayerScene()`, `.LoadChunkClientRpc()`, `.LoadChunksForClient()`, `.LogDebug()`, `.OnClientConnected()`, `.OnClientDisconnected()`, `.OnNetworkDespawn()`, `.OnNetworkSpawn()`, `.UnloadChunkClientRpc()`, `.UnloadChunksForClient()`, `.Update()`, `.UpdatePlayerChunk()`, `.UpdatePlayerPosition()`, `ProjectC.World.Streaming`, `.CanLoadChunk()`, `.LoadChunkByServerCommand()`, `.UnloadChunkByServerCommand()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Player State Machine`** (14 nodes): `PlayerStateMachine.cs`, `PlayerStateMachine`, `.ApplyFlying()`, `.ApplyWalking()`, `.Awake()`, `.Disembark()`, `.FindNearestShip()`, `.ForceState()`, `.OnDisable()`, `.OnEnable()`, `.Start()`, `.TryBoardNearestShip()`, `.TrySwitchMode()`, `ProjectC.Core`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Player Controller`** (9 nodes): `PlayerController.cs`, `PlayerController`, `.Awake()`, `.HandleMovement()`, `.OnDisable()`, `.OnEnable()`, `.Start()`, `.Update()`, `ProjectC.Player`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Streaming Setup Runtime`** (8 nodes): `StreamingSetupRuntime.cs`, `ProjectC.Core`, `StreamingSetupRuntime`, `.AddStreamingTest()`, `.Awake()`, `.ForceReinitialize()`, `.OrganizeWorldRoot()`, `.SetupFloatingOrigin()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ShipController` connect `Ship Controller` to `Scene Management`, `Network Player`?**
  _High betweenness centrality (0.196) - this node is a cross-community bridge._
- **Why does `NetworkPlayer` connect `Network Player` to `Scene Management`?**
  _High betweenness centrality (0.142) - this node is a cross-community bridge._
- **What connects `ProjectC.World`, `ProjectC.World.Streaming`, `ProjectC.World.Streaming` to the rest of the system?**
  _32 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Altitude Corridor System` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Chunk Loading System` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Ship Controller` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._
- **Should `Scene Management` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._