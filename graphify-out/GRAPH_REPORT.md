# Graph Report - .  (2026-05-01)

## Corpus Check
- Large corpus: 454 files · ~459,216 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder, or use --no-semantic to run AST-only.

## Summary
- 1959 nodes · 3521 edges · 45 communities detected
- Extraction: 85% EXTRACTED · 15% INFERRED · 0% AMBIGUOUS · INFERRED: 525 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 42|Community 42]]
- [[_COMMUNITY_Community 43|Community 43]]
- [[_COMMUNITY_Community 44|Community 44]]

## God Nodes (most connected - your core abstractions)
1. `TradeUI` - 48 edges
2. `ShipController` - 45 edges
3. `TradeMarketServer` - 39 edges
4. `NetworkPlayer` - 35 edges
5. `ClientSceneLoader` - 33 edges
6. `ContractSystem` - 31 edges
7. `ServerSceneManager` - 30 edges
8. `ContractBoardUI` - 29 edges
9. `MountainMassifBuilder` - 28 edges
10. `NpcDialogueManager` - 27 edges

## Surprising Connections (you probably didn't know these)
- `OnGUI()` --calls--> `ToString()`  [INFERRED]
  Assets\_Project\Scripts\World\Streaming\WorldStreamingManager.cs → Assets\_Project\Scripts\World\Streaming\WorldChunkManager.cs
- `BootstrapSceneGenerator` --creates--> `SceneRegistry`  [EXTRACTED]
  Assets/_Project/Editor/BootstrapSceneGenerator.cs → Assets/_Project/Editor/WorldSceneGenerator.cs
- `TestSceneGenerator` --creates--> `FloatingOriginMP`  [EXTRACTED]
  Assets/_Project/Editor/TestSceneGenerator.cs → Assets/_Project/Editor/WorldSceneSetup.cs
- `WorldSceneSetup` --creates--> `WorldSceneManager`  [EXTRACTED]
  Assets/_Project/Editor/WorldSceneSetup.cs → Assets/_Project/Editor/TestSceneGenerator.cs
- `MaterialURPConverter` --conceptually_related_to--> `CloudLayerConfig`  [INFERRED]
  Assets/_Project/Scripts/Core/MaterialURPConverter.cs → Assets/_Project/Scripts/Core/CloudLayerConfig.cs

## Hyperedges (group relationships)
- **** —  [EXTRACTED 1.00]
- **** —  [EXTRACTED 1.00]
- **** —  [EXTRACTED 1.00]

## Communities

### Community 0 - "Community 0"
Cohesion: 0.03
Nodes (19): ClientSceneLoader, ProjectC.World.Scene, NetworkBehaviour, PlayerCreditsManager, ProjectC.World.Scene, SceneBoundNetworkObject, FromWorldPosition(), GetNeighbor() (+11 more)

### Community 1 - "Community 1"
Cohesion: 0.03
Nodes (10): ContractBoardUI, ContractData, ProjectC.Trade, ContractSystem, ProjectC.Trade, ContractTrigger, PlayerDebt, ProjectC.Trade (+2 more)

### Community 2 - "Community 2"
Cohesion: 0.03
Nodes (20): AltitudeCorridorSystem, CreateCorridorAsset(), ProjectC.Ship, SetupDefaultCorridors(), AltitudeUI, ProjectC.UI, CumulonimbusCloud, ProjectC.World.Clouds (+12 more)

### Community 3 - "Community 3"
Cohesion: 0.03
Nodes (17): LocationMarket, ProjectC.Trade, MarketEvent, ProjectC.Trade, MarketItem, ProjectC.Trade, NPCTrader, ProjectC.Trade (+9 more)

### Community 4 - "Community 4"
Cohesion: 0.02
Nodes (29): CloudClimateTinter, ProjectC.World.Clouds, ControlHintsUI, ProjectC.UI, FloatingOrigin, ProjectC.World.Core, MaterialURPConverter, ProjectC.Core (+21 more)

### Community 5 - "Community 5"
Cohesion: 0.03
Nodes (12): ChunkLoader, ProjectC.World.Streaming, ChunkNetworkSpawner, ProjectC.World.Streaming, PlayerChunkTracker, ProjectC.World.Streaming, ProjectC.World, StreamingTest_AutoRun (+4 more)

### Community 6 - "Community 6"
Cohesion: 0.04
Nodes (13): AutoTradeZone, ProjectC.Trade, CargoItem, CargoSystem, ProjectC.Player, PlayerTradeStorage, ProjectC.Trade, WarehouseItem (+5 more)

### Community 7 - "Community 7"
Cohesion: 0.03
Nodes (12): Inventory, ProjectC.Items, InventoryUI, ProjectC.Items, ItemTypeNames, ProjectC.Items, NetworkPlayer, ProjectC.Player (+4 more)

### Community 8 - "Community 8"
Cohesion: 0.03
Nodes (13): ChestContainer, ProjectC.Items, IInteractable, InteractableManager, ProjectC.Core, ItemPickupSystem, ProjectC.Player, NetworkChestContainer (+5 more)

### Community 9 - "Community 9"
Cohesion: 0.05
Nodes (8): NetworkManagerController, ProjectC.Core, NetworkTestMenu, ProjectC.UI, NetworkUI, ProjectC.UI, PrepareTestScene, ProjectC.Editor

### Community 10 - "Community 10"
Cohesion: 0.05
Nodes (17): MountainMassifBuilderV2, ProjectC.Editor, MountainMeshBuilderV2, ProjectC.World.Generation, MountainMeshGenerator, ProjectC.World.Generation, MountainProfile, ProjectC.World.Generation (+9 more)

### Community 11 - "Community 11"
Cohesion: 0.04
Nodes (16): CreateModuleTestAssets, ProjectC.Editor, CreateTestShip, ProjectC.Editor, EditorWindow, FloatingOriginSetup, ProjectC.Editor, MarketAssetGenerator (+8 more)

### Community 12 - "Community 12"
Cohesion: 0.05
Nodes (7): BootstrapSceneGenerator, ProjectC.Editor, ProjectC.Editor, TestSceneGenerator, PoleBlockerComponent, ProjectC.Editor, WorldSceneGenerator

### Community 13 - "Community 13"
Cohesion: 0.07
Nodes (14): Editor, GetIdsForType(), NetworkSerialize(), ProjectC.Items, SerializeList(), ItemDatabaseInitializer, ProjectC.Core, MeziyThrusterVisual (+6 more)

### Community 14 - "Community 14"
Cohesion: 0.07
Nodes (7): CloudLayerConfigAssetsEditor, ProjectC.Editor, ProceduralNoiseGenerator, ProjectC.Core, PeakInfo, ProjectC.Core, WorldGenerator

### Community 15 - "Community 15"
Cohesion: 0.05
Nodes (24): AltitudeCorridorData, ProjectC.Ship, BiomeProfile, ProjectC.World.Core, CloudLayerConfig, ProjectC.Core, ItemData, ProjectC.Items (+16 more)

### Community 16 - "Community 16"
Cohesion: 0.07
Nodes (7): MeziyContinuousState, MeziyModuleActivator, ProjectC.Ship, MeziyStatusHUD, ProjectC.Ship, ProjectC.Ship, ShipDebugHUD

### Community 17 - "Community 17"
Cohesion: 0.09
Nodes (6): CloudLayer, ProjectC.Core, CloudSystem, ProjectC.Core, CumulonimbusManager, ProjectC.World.Clouds

### Community 18 - "Community 18"
Cohesion: 0.1
Nodes (6): DialogueNode, DialogueOption, NpcData, ProjectC.World.Npc, NpcDialogueManager, ProjectC.World.Npc

### Community 19 - "Community 19"
Cohesion: 0.09
Nodes (6): ChunkGizmoRenderer, ChunkVisualizer, ChunkVisualizerSettings, ProjectC.Editor, SceneNavigatorWindow, StreamingSetup

### Community 20 - "Community 20"
Cohesion: 0.11
Nodes (4): FloatingOriginMP, ProjectC.World.Streaming, ProjectC.World, StreamingTest

### Community 21 - "Community 21"
Cohesion: 0.1
Nodes (5): ConfirmationDialog, ProjectC.UI, ProjectC.UI, UIManager, UIPanelInfo

### Community 22 - "Community 22"
Cohesion: 0.12
Nodes (4): PeakNavigationUI, ProjectC.Core, ProjectC.Core, WorldCamera

### Community 23 - "Community 23"
Cohesion: 0.13
Nodes (2): MountainMassifBuilder, ProjectC.Editor

### Community 24 - "Community 24"
Cohesion: 0.13
Nodes (4): ProjectC.Ship, ShipModule, ProjectC.Ship, ShipModuleManager

### Community 25 - "Community 25"
Cohesion: 0.12
Nodes (25): AltitudeCorridorSystem, BootstrapSceneGenerator, ClientSceneLoader, CloudClimateTinter, CloudLayer, CloudLayerConfig, CloudSystem, CumulonimbusManager (+17 more)

### Community 26 - "Community 26"
Cohesion: 0.16
Nodes (2): MountainMeshBuilder, ProjectC.World.Generation

### Community 27 - "Community 27"
Cohesion: 0.15
Nodes (2): NpcEntity, ProjectC.World.Npc

### Community 28 - "Community 28"
Cohesion: 0.23
Nodes (2): ProjectC.Editor, WorldAssetCreator

### Community 29 - "Community 29"
Cohesion: 0.21
Nodes (2): MainMenuSceneGenerator, ProjectC.Editor

### Community 30 - "Community 30"
Cohesion: 0.25
Nodes (2): PrepareMainScene, ProjectC.Editor

### Community 31 - "Community 31"
Cohesion: 0.18
Nodes (3): ProjectC.World.Clouds, VeilSystem, VeilWarningZone

### Community 32 - "Community 32"
Cohesion: 0.19
Nodes (3): NetworkPlayerDebugExtensions, ProjectC.Trade, TradeDebugTest

### Community 33 - "Community 33"
Cohesion: 0.24
Nodes (10): ChestContainer, CreateTestShip, IInteractable, InteractableManager, ItemDatabaseInitializer, LootTable, NetworkInventory, PickupItem (+2 more)

### Community 34 - "Community 34"
Cohesion: 0.31
Nodes (6): CreateWindZoneTestScene, WindProfile, WindZone, WindZoneData, CreateWindZoneTestScene, ProjectC.Editor

### Community 35 - "Community 35"
Cohesion: 0.36
Nodes (2): ProjectC.Editor, StreamingTestAutoRunner

### Community 36 - "Community 36"
Cohesion: 0.47
Nodes (2): CreateMeziyModuleAssets, ProjectC.Editor

### Community 37 - "Community 37"
Cohesion: 0.4
Nodes (2): MaterialURPUpgrader, ProjectC.Editor

### Community 38 - "Community 38"
Cohesion: 0.33
Nodes (5): FarmData, HeightmapKeypoint, PeakData, ProjectC.World.Core, RidgeData

### Community 39 - "Community 39"
Cohesion: 0.5
Nodes (2): ProjectC.Trade.Editor, TradeSceneSetupEditor

### Community 40 - "Community 40"
Cohesion: 0.5
Nodes (2): CreateAltitudeCorridorAssets, ProjectC.Editor

### Community 41 - "Community 41"
Cohesion: 0.5
Nodes (4): Inventory, InventoryData, ItemType, ItemTypeNames

### Community 42 - "Community 42"
Cohesion: 0.67
Nodes (2): IInteractable, ProjectC.Core

### Community 43 - "Community 43"
Cohesion: 1.0
Nodes (2): AltitudeCorridorData, CreateAltitudeCorridorAssets

### Community 44 - "Community 44"
Cohesion: 1.0
Nodes (1): StreamingSetupRuntime

## Knowledge Gaps
- **165 isolated node(s):** `ProjectC.Editor`, `ProjectC.Editor`, `ProjectC.Editor`, `ProjectC.Editor`, `ProjectC.Editor` (+160 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 23`** (28 nodes): `MountainMassifBuilder`, `.ApplyCrater()`, `.ApplyHeightProfile()`, `.ApplyKeypointDeformation()`, `.BuildAllMountains()`, `.BuildMassif()`, `.BuildPeakGameObject()`, `.BuildPeakMeshInEditor()`, `.CalculateMeshHeight()`, `.ClearExistingMountains()`, `.FindMassif()`, `.GenerateConeMesh()`, `.GenerateCylinderMesh()`, `.GenerateDomeCapMesh()`, `.GenerateDomeMesh()`, `.GenerateEllipsoidMesh()`, `.GenerateIsolatedMesh()`, `.GenerateMeshForPeak()`, `.GenerateTectonicMesh()`, `.GenerateVolcanicMesh()`, `.GetRockMaterialForMassif()`, `.GetRockMaterialForPeak()`, `.GetTargetHRatio()`, `.OnEnable()`, `.OnGUI()`, `.ShowWindow()`, `ProjectC.Editor`, `MountainMassifBuilder.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 26`** (24 nodes): `MountainMeshBuilder`, `.ApplyCrater()`, `.ApplyHeightProfile()`, `.ApplyKeypointDeformation()`, `.ApplyNoiseDisplacement()`, `.AssignMaterials()`, `.Awake()`, `.BuildPeakMesh()`, `.CalculateMeshHeight()`, `.GenerateBaseMesh()`, `.GenerateConeMesh()`, `.GenerateCylinderMesh()`, `.GenerateDomeCapMesh()`, `.GenerateDomeMesh()`, `.GenerateEllipsoidMesh()`, `.GenerateIsolatedMesh()`, `.GenerateLOD1Mesh()`, `.GenerateTectonicMesh()`, `.GenerateVolcanicMesh()`, `.GetTargetHRatio()`, `.SetupCollider()`, `.Start()`, `ProjectC.World.Generation`, `MountainMeshBuilder.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 27`** (21 nodes): `NpcEntity`, `.Awake()`, `.EndDialogue()`, `.EnsureCollider()`, `.GenerateWanderTarget()`, `.GetNpcData()`, `.HandleIdleState()`, `.HandleWalkingState()`, `.OnDrawGizmosSelected()`, `.OnNetworkDespawn()`, `.OnNetworkSpawn()`, `.OnNetworkStateChanged()`, `.SetNpcData()`, `.SetState()`, `.StartDialogue()`, `.Update()`, `.UpdateAnimation()`, `.UpdateWandering()`, `ProjectC.World.Npc`, `.Interact()`, `NpcEntity.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 28`** (20 nodes): `WorldAssetCreator.cs`, `ProjectC.Editor`, `WorldAssetCreator`, `.CreateAllWorldAssets()`, `.CreateBiomeProfile()`, `.CreateBiomeProfiles()`, `.CreateBiomeProfilesOnly()`, `.CreateFolders()`, `.CreateFoldersOnly()`, `.CreateMountainMassif()`, `.CreateMountainMassifs()`, `.CreateMountainMassifsOnly()`, `.CreateVeilMaterial()`, `.CreateVeilMaterialOnly()`, `.CreateWorldData()`, `.CreateWorldDataOnly()`, `.HexColor()`, `.UpdateAltitudeCorridors()`, `.UpdateCorridor()`, `.UpdateCorridorsOnly()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 29`** (17 nodes): `MainMenuSceneGenerator.cs`, `MainMenuSceneGenerator`, `.CreateBootstrapObjects()`, `.CreateButton()`, `.CreateCanvas()`, `.CreateEventSystem()`, `.CreateMainMenuUI()`, `.CreateNetworkManager()`, `.CreatePlayerObject()`, `.CreatePlayerSpawner()`, `.CreateUIManager()`, `.GenerateMainMenuScene()`, `.GetTypeByName()`, `.OnGUI()`, `.Regenerate()`, `.ShowWindow()`, `ProjectC.Editor`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 30`** (17 nodes): `PrepareMainScene`, `.AddComponentByName()`, `.AddComponentIfMissing()`, `.AddStreamingTest()`, `.ApplyDebugSettings()`, `.ApplyToCurrentScene()`, `.ConfigureFloatingOrigin()`, `.ConfigureWorldStreaming()`, `.EnsureWorldRootExists()`, `.FindOrCreateFloatingOrigin()`, `.FixWorldRootHierarchy()`, `.GetTypeByName()`, `.OnGUI()`, `.ShowWindow()`, `.ShowWindowValidation()`, `ProjectC.Editor`, `PrepareMainScene.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 35`** (8 nodes): `StreamingTestAutoRunner.cs`, `ProjectC.Editor`, `StreamingTestAutoRunner`, `.AddComponentToMainCamera()`, `.OnPlayModeChanged()`, `.OnSceneOpened()`, `.RemoveComponentFromCamera()`, `.RemoveOldTestComponents()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 36`** (6 nodes): `CreateMeziyModuleAssets`, `.CreateMeziyModule()`, `.CreateMeziyModules()`, `.CreateRollModule()`, `ProjectC.Editor`, `CreateMeziyModuleAssets.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 37`** (6 nodes): `MaterialURPUpgrader`, `.CheckMaterialsStatus()`, `.TryConvertMaterial()`, `.UpgradeAllMaterials()`, `ProjectC.Editor`, `MaterialURPUpgrader.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 39`** (5 nodes): `TradeSceneSetupTool.cs`, `ProjectC.Trade.Editor`, `TradeSceneSetupEditor`, `.FindMarket()`, `.SetupTradeScene()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 40`** (4 nodes): `CreateAltitudeCorridorAssets`, `.CreateCorridorAssets()`, `ProjectC.Editor`, `CreateAltitudeCorridorAssets.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 42`** (3 nodes): `IInteractable`, `ProjectC.Core`, `IInteractable.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 43`** (2 nodes): `AltitudeCorridorData`, `CreateAltitudeCorridorAssets`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 44`** (1 nodes): `StreamingSetupRuntime`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ShipController` connect `Community 2` to `Community 0`, `Community 24`, `Community 3`, `Community 7`?**
  _High betweenness centrality (0.066) - this node is a cross-community bridge._
- **Why does `WorldSceneGenerator` connect `Community 12` to `Community 11`?**
  _High betweenness centrality (0.063) - this node is a cross-community bridge._
- **Why does `ClientSceneLoader` connect `Community 0` to `Community 4`?**
  _High betweenness centrality (0.060) - this node is a cross-community bridge._
- **What connects `ProjectC.Editor`, `ProjectC.Editor`, `ProjectC.Editor` to the rest of the system?**
  _165 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.03 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.03 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.03 - nodes in this community are weakly interconnected._