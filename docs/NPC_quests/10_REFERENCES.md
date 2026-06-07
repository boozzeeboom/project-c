# 10 — References & Bibliography

> **Цель:** список всех файлов, документов, subagent-отчётов, GDD, lore,
> pitfall-листов, на которых базируется эта документация.

---

## 10.1 Project C: The Clouds — собственные документы

### Project meta
- `AGENTS.md` — Mavis agent context (v2 patterns, hard rules, project layout, scene architecture).
- `README.md` — top-level project overview.
- `docs/STRUCTURE.md` — структура документации.
- `docs/INDEX.md` — каталог всей документации.

### Lore
- `docs/WORLD_LORE_BOOK.md` — полный лор «Интеграл Пьявица» (canonical reference, read-only).
- `docs/WORLD_LORE_GRAVITY.md` — краткий справочник лора (антигравий, мезий, корабли).

### GDD (game-designer owned, do not modify)
- `docs/gdd/GDD_INDEX.md` — список всех 18 GDD.
- GDD-22 (Economy/Trading) — `docs/gdd/GDD_22_Economy_Trading.md`.
- GDD-23 (Faction/Reputation, referenced from `CharacterWindow.cs:160`) — **TODO: read**.

### Architecture patterns
- `docs/Markets/ARCHITECTURE.md` — v2 pattern (Market/Contract/Inventory).
- `docs/Markets/FLOW_TRADE.md` — flow diagram.
- `docs/Markets/FIXES_HISTORY.md` — 4 FIX'ы for UI Toolkit windows.
- `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md` — full v2 audit.
- `docs/Markets/INTEGRATION.md` — Trade integration.
- `docs/Markets/TRADE_V2_DESIGN.md` — Trade v2 design.
- `docs/Markets/TRADE_V2_INTEGRATION.md` — Trade v2 integration.
- `docs/Markets/KNOWN_ISSUES.md` — known issues.
- `docs/Markets/READMD.md` — overview.

### Migration guides
- `docs/dev/CONTRACT_V2_MIGRATION.md` — Contract v2 migration.
- `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` — UI-as-tab refactor.
- `docs/dev/INVENTORY_V2_REFACTOR.md` — Inventory v2 refactor.
- `docs/dev/INVENTORY_V2_DROP_DESIGN.md` — Inventory drop design.
- `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md` — scene-placed NetworkObject diagnosis.
- `docs/dev/C1_CLEANUP_PLAN_2026-06-05.md` — legacy cleanup pattern.

### Investigation logs
- `docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md` — investigation template.
- `docs/dev/COMMIT_2026-06-04_GHOST_CLONE_FIX.md` — fix log template.

### Sub-system docs
- `docs/Crafting_system/` — 7 files (00_OVERVIEW, 10_DESIGN, 20_IMPL_PLAN, 30_VERIFY, 40_INSPECTOR, 50_KNOWN_ISSUES, 99_CHANGELOG) — template.
- `docs/MetaRequirement/` — 6 files — template.
- `docs/Ships/Key-subsystem/` — 3 files.
- `docs/Character-menu/` — 11 files — multi-tab window reference.
- `docs/Character-menu/sub_inventory-tab/` — 9 files.

---

## 10.2 Project C — файлы кода (v2 reference implementations)

### Trade v2 hub
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` (522 LOC) — server hub.
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` (412 LOC) — server hub.
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` — zone component.
- `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs` — zone registry.
- `Assets/_Project/Trade/Scripts/Network/MarketTimeService.cs` — server time tick.

### Trade DTOs
- `Assets/_Project/Trade/Scripts/Dto/MarketSnapshotDto.cs` — snapshot with arrays.
- `Assets/_Project/Trade/Scripts/Dto/ContractDto.cs` (line 18) — per-contract DTO.
- `Assets/_Project/Trade/Scripts/Dto/ContractSnapshotDto.cs` (line 18) — snapshot with arrays.
- `Assets/_Project/Trade/Scripts/Dto/ContractResultDto.cs` (line 20) — **Nullable<T> workaround at line 60-90**.
- `Assets/_Project/Trade/Scripts/Dto/ContractResultCode.cs` (line 14) — 15-value enum.
- `Assets/_Project/Trade/Scripts/Dto/TradeResultDto.cs`.
- `Assets/_Project/Trade/Scripts/Dto/TradeResultCode.cs`.
- `Assets/_Project/Trade/Scripts/Dto/ShipSummaryDto.cs`.

### Trade client state
- `Assets/_Project/Trade/Scripts/Client/MarketClientState.cs` — singleton projection.
- `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` — singleton projection.
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs` — RPC sender.
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` — UI Toolkit tab window.

### Trade core
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — POCO world state.
- `Assets/_Project/Trade/Scripts/Core/MarketState.cs` (77 LOC).
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — contract persistence.
- `Assets/_Project/Trade/Scripts/Core/ContractDebt.cs` — debt levels.
- `Assets/_Project/Trade/Scripts/Core/MarketItemState.cs`.
- `Assets/_Project/Trade/Scripts/Core/Warehouse.cs` (149 LOC).
- `Assets/_Project/Trade/Scripts/Core/CargoData.cs` (159 LOC).
- `Assets/_Project/Trade/Scripts/Core/NPCTrader.cs` — naming conflict (T-X1).
- `Assets/_Project/Trade/Scripts/Core/TradeItemDefinitionResolver.cs`.
- `Assets/_Project/Trade/Scripts/Core/DatabaseResolver.cs`.
- `Assets/_Project/Trade/Scripts/Core/ContractWorldItemResolver.cs`.
- `Assets/_Project/Trade/Scripts/Core/TradeResult.cs`.
- `Assets/_Project/Trade/Scripts/Core/MarketEvent.cs`.

### Trade persistence
- `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` (line 17).
- `Assets/_Project/Trade/Scripts/Repository/PlayerPrefsRepository.cs`.
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs`.

### Trade item/contract
- `Assets/_Project/Trade/Scripts/TradeItemDefinition.cs` (line 5) — item SO.
- `Assets/_Project/Trade/Scripts/TradeDatabase.cs` (line 7) — registry SO with `OnValidate` index.
- `Assets/_Project/Trade/Scripts/ContractData.cs` (259 LOC).
- `Assets/_Project/Trade/Scripts/CargoSystem.cs` (287 LOC) — legacy client-side.
- `Assets/_Project/Trade/Scripts/Config/MarketConfig.cs` (line 14).
- `Assets/_Project/Trade/Scripts/Config/MarketItemConfig.cs`.

### Items subsystem (newer, more current)
- `Assets/_Project/Items/Network/InventoryServer.cs` (305 LOC) — server hub, **lacks TryRemove**.
- `Assets/_Project/Items/InventoryWorld.cs` (445 LOC) — POCO, non-persistent.
- `Assets/_Project/Items/Client/InventoryClientState.cs` (234 LOC).
- `Assets/_Project/Items/Dto/InventorySnapshotDto.cs`.
- `Assets/_Project/Items/Core/ItemData.cs` (referenced from `Core/ItemType.cs:17`).
- `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs` (156 LOC) — generator pattern.

### MetaRequirement (quest-LIKE precedent)
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` (line 33) — NetworkBehaviour on Interactable.
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs` (line 27) — server hub.
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementClientState.cs` (line 29).
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementDto.cs` (line 24) — uses `FixedString64Bytes`.
- `Assets/_Project/Scripts/MetaRequirement/RequirementLogic.cs` (line 16) — All/Any/AtLeastN.
- `Assets/_Project/Scripts/MetaRequirement/ProgressInfo.cs` (line 145).
- `Assets/_Project/Scripts/MetaRequirement/LockBox.cs`.
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementToast.cs`.

### UI Toolkit reference
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (1345 LOC) — 5-tab window, **canonical pattern**.
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` + `.uss`.
- `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset`.
- `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` + `.uss`.
- `Assets/_Project/UI/Resources/UI/InventoryPanelSettings.asset`.
- `Assets/_Project/UI/Resources/UI/MetaRequirementPanelSettings.asset`.
- `Assets/_Project/UI/Resources/UI/ShipKeyPanelSettings.asset`.
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml`.

### UI legacy (uGUI, do not use for new code)
- `Assets/_Project/Scripts/UI/UIFactory.cs` (573 LOC).
- `Assets/_Project/Scripts/UI/UIManager.cs` (278 LOC).
- `Assets/_Project/Scripts/UI/UITheme.cs` (330 LOC) — color palette reference.
- `Assets/_Project/Scripts/UI/HUDManager.cs`.
- `Assets/_Project/Scripts/UI/SceneDebugHUD.cs`.
- `Assets/_Project/Scripts/UI/ControlHintsUI.cs` (referenced as planned).
- `Assets/_Project/Scripts/UI/ConfirmationDialog.cs`.
- `Assets/_Project/Scripts/UI/AltitudeUI.cs`.
- `Assets/_Project/Scripts/UI/PeakNavigationUI.cs`.
- `Assets/_Project/Scripts/UI/NetworkUI.cs`.
- `Assets/_Project/Scripts/UI/NetworkTestMenu.cs`.
- `Assets/_Project/Scripts/UI/ShipDebugHUD.cs`.
- `Assets/_Project/Scripts/UI/MeziyStatusHUD.cs`.

### Core / Player
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — auto-spawns ClientState singletons.
- `Assets/_Project/Scripts/Core/InteractableManager.cs` (253 LOC) — `RegisterNpc/FindNearestNpc`.
- `Assets/_Project/Scripts/Core/ItemType.cs` (line 17).
- `Assets/_Project/Scripts/Core/PickupItem.cs` (191 LOC).
- `Assets/_Project/Scripts/Core/CloudManager.cs`, `DistantCloudManager.cs`, `NearCloudRenderer.cs`.
- `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs`.
- `Assets/_Project/Scripts/Core/DayNight/DayNightProfile.cs`.

### Player
- `Assets/_Project/Scripts/Player/PlayerInputReader.cs` (128 LOC) — events declared, no subscribers.
- `Assets/_Project/Scripts/Player/PlayerController.cs` (145 LOC) — walking only.
- `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` (210 LOC) — F = board ship.
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — E-key pipeline at line 375.
- `Assets/_Project/Scripts/Player/ShipController.cs` (referenced).
- `Assets/_Project/Scripts/Player/ShipInputReader.cs`.

### Ship
- `Assets/_Project/Scripts/Ship/ShipModule.cs` (line 22) — ShipModule SO.
- `Assets/_Project/Scripts/Ship/ShipModuleManager.cs`.
- `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs`.
- `Assets/_Project/Scripts/Ship/ModuleSlot.cs`.
- `Assets/_Project/Scripts/Ship/MeziyModuleActivator.cs`, `MeziyStatusHUD.cs`, `MeziyThrusterVisual.cs`.
- `Assets/_Project/Scripts/Ship/WindZone.cs`, `WindZoneData.cs`, `TurbulenceEffect.cs`, `SystemDegradationEffect.cs`.
- `Assets/_Project/Scripts/Ship/AltitudeCorridorSystem.cs`, `AltitudeCorridorData.cs`.
- `Assets/_Project/Scripts/Ship/Key/ShipKeyServer.cs` (server hub).
- `Assets/_Project/Scripts/Ship/Key/ShipKeyClientState.cs` (singleton projection).
- `Assets/_Project/Scripts/Ship/Key/ShipKeyToast.cs`.
- `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs`.

### World
- `Assets/_Project/Scripts/World/Npc/` (4 files, v1) — `NpcData.cs`, `NpcEntity.cs`, `NpcInteraction.cs`, `NpcDialogueManager.cs`.
- `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` (99-127) — **MUST use for scene-placed NpcEntity**.
- `Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs`.
- `Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs`.
- `Assets/_Project/Scripts/World/Scene/SceneID.cs`, `SceneRegistry.cs`, `SceneBoundNetworkObject.cs`.
- `Assets/_Project/Scripts/World/Streaming/` (8 files) — chunk management, floating origin.
- `Assets/_Project/Scripts/World/Clouds/` (7 files).
- `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs`.
- `Assets/_Project/Scripts/World/Generation/` (4 files).
- `Assets/_Project/Scripts/World/Core/` (5 files) — WorldData SO registry, MountainMassif, BiomeProfile.

### Network
- `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs`.
- `Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs` (above).

### Editor tools
- `Assets/_Project/Editor/WorldSceneGenerator.cs` (443 LOC) — `EditorWindow` with GUI.
- `Assets/_Project/Editor/BootstrapSceneGenerator.cs` (632 LOC) — `EditorWindow`.
- `Assets/_Project/Editor/MainMenuSceneGenerator.cs`, `TestSceneGenerator.cs`.
- `Assets/_Project/Editor/WorldSceneSetup.cs` (307 LOC).
- `Assets/_Project/Editor/CreateTestShip.cs`, `CreateWindZoneTestScene.cs`, `CreateAltitudeCorridorAssets.cs` (105 LOC, idempotent).
- `Assets/_Project/Editor/CombineMeshesToCollider.cs`.
- `Assets/_Project/Scripts/Editor/ProjectCSceneSetup.cs` (201 LOC).
- `Assets/_Project/Scripts/Editor/WorldAssetCreator.cs` (388 LOC) — **canonical "build database" pattern**.
- `Assets/_Project/Scripts/Editor/CloudLayerConfigAssetsEditor.cs` (254 LOC) — **idempotent + delete menu**.
- `Assets/_Project/Scripts/Editor/WorldEditorTools.cs` (656 LOC) — `SceneNavigatorWindow` (only browser-like).
- `Assets/_Project/Scripts/Editor/StreamingTestAutoRunner.cs` (136 LOC) — **only `[InitializeOnLoad]`**.
- `Assets/_Project/Scripts/Editor/MaterialURPUpgrader.cs`, `RockMaterialCreator.cs`.
- `Assets/_Project/Scripts/Editor/PrepareMainScene.cs`, `PrepareTestScene.cs`.
- `Assets/_Project/Scripts/Editor/MountainMassifBuilder.cs` (v1) + `MountainMassifBuilderV2.cs`.
- `Assets/_Project/Scripts/Editor/PeakDataFiller.cs`, `PeakDataScaler.cs`.
- `Assets/_Project/Scripts/Editor/CreateModuleTestAssets.cs`, `CreateMeziyModuleAssets.cs`.
- `Assets/_Project/Scripts/Editor/FloatingOriginSetup.cs`.
- `Assets/_Project/Trade/Scripts/Editor/TradeAssetGenerator.cs` (102 LOC) — **canonical generator**.

### Debug
- `Assets/_Project/Scripts/Debug/DebugQuadSetup.cs`.

---

## 10.3 Subagent отчёты (эта сессия)

- `C:\Users\leon7\ANALYSIS_NPC_SUBSYSTEM.md` — 472 строки, ~3000 слов, NPC v1 audit.
- `C:\Users\leon7\quest_inventory_report.md` — 621 строк, ~5100 слов, Trade/Inventory analysis.
- `C:\Users\leon7\projectc_quest_editor_tooling_report.md` — 427 строк, ~2500 слов, Editor tooling.
- Subagent #2 (Input/UI) — summary в delegate_task response, ~5500 слов.

---

## 10.4 Skills (загружены в этой сессии)

- `project-c-bootstrap` — сессионный чеклист, stack pins, scene architecture.
- `project-c-ui-as-tab` — UI-as-tab pattern, 4 FIX'ы, cross-tab cache.
- `unity-v2-subsystem-migration` — server hub + DTO + ClientState pattern.
- `map-systems` — game concept → systems decomposition (для documentation mapping).

---

## 10.5 Внешние ресурсы (Unity 6)

### Unity Manual
- Unity 6 Netcode for GameObjects 2.x RPCs: `https://docs.unity3d.com/Packages/com.unity.netcode@2.0/manual/rpc.html`
- Unity 6 UI Toolkit Editor: `https://docs.unity3d.com/Manual/UIE-support-for-editor.html`
- Unity 6 `INetworkSerializable`: `https://docs.unity3d.com/Packages/com.unity.netcode@2.0/api/Unity.Netcode.INetworkSerializable.html`
- Unity 6 `GraphView` (experimental): `https://docs.unity3d.com/Packages/com.unity.modules.imgui@1.0/manual/GraphView.html`
- Unity 6 `AssetPostprocessor`: `https://docs.unity3d.com/ScriptReference/AssetPostprocessor.html`

### Tooling references
- `UnityEditor.TreeView` (UI Toolkit): `https://docs.unity3d.com/ScriptReference/UIElements.TreeView.html`
- `MultiColumnListView`: `https://docs.unity3d.com/ScriptReference/UIElements.MultiColumnListView.html`
- `ToolbarSearchField`: `https://docs.unity3d.com/ScriptReference/UIElements.ToolbarSearchField.html`
- `EditorPrefs`: `https://docs.unity3d.com/ScriptReference/EditorPrefs.html`
- `SessionState`: `https://docs.unity3d.com/ScriptReference/SessionState.html`
- `FixedString64Bytes`: `https://docs.unity3d.com/Packages/com.unity.collections@1.0/api/Unity.Collections.FixedString64Bytes.html`

### External (cited in this doc)
- Yarn Spinner (inspiration, not adopted): `https://yarnspinner.dev/`
- Articy Draft (inspiration, not adopted): `https://www.articy.com/en/articy-draft/`

---

## 10.6 Pitfall-листы (recap)

### From subagent analysis
- `unity-v2-subsystem-migration` SKILL.md §pitfalls (1-40+).
- `project-c-ui-as-tab` SKILL.md §4 FIX'ы + R3-005 cross-tab cache lesson.

### From this session's analysis
- `02_V2_ARCHITECTURE.md` §7 — 10 server/DTO/persistence pitfalls.
- `04_DIALOG_AND_QUEST_UI.md` §4.8 — 10 UI Toolkit pitfalls.
- `05_INPUT_AND_INTERACTION.md` §5.9 — 8 input/interaction pitfalls.
- `06_TRIGGERS_AND_INTEGRATION.md` §6.8 — 8 trigger/integration pitfalls.

### AGENTS.md hard rules
- No `.meta`/`.asmdef` writes.
- No `git commit` / `git push`.
- No `run_tests` MCP.
- No `docs/gdd/*.md` modification.
- No `docs/WORLD_LORE_BOOK.md` modification.
- No Unity-generated dirs (Library/, Temp/, Builds/, etc.).
- New Input System only (no `Input.GetKeyDown`).
- `[Rpc(SendTo.X)]` over `[ServerRpc]`/`[ClientRpc]`.
- `[SerializeField] private` convention.

### Scene architecture
- 1 BootstrapScene + 24 WorldScenes.
- NetworkManager in BootstrapScene, DontDestroyOnLoad.
- Scene-placed NetworkObject → use `ScenePlacedObjectSpawner`.
- Streaming uses vanilla `SceneManager.LoadSceneAsync`, **NOT** NetworkSceneManager.

### MCP work sequence (mandatory)
1. `refresh_unity` (scope=all, compile=request, wait_for_ready=true)
2. `read_console` (errors + warnings)
3. Only then proceed

---

## 10.7 Мнемоника для следующих сессий

**Перед стартом любого T-Q тикета:**
1. Прочитать соответствующий раздел этой документации (`02` / `03` / `04` / etc).
2. Прочитать pitfall-лист в этом разделе.
3. Прочитать соответствующий reference файл (e.g. `ContractServer.cs` для T-Q05).
4. Проверить `09_OPEN_QUESTIONS.md` — все ли вопросы решены для этого тикета.
5. После кода: `refresh_unity` + `read_console`.
6. Передать юзеру verification commands.
7. Юзер коммитит — не делать `git commit` самому.

**Удачи в имплементации. 🚀**
