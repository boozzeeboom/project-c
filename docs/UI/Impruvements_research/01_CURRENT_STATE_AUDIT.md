# UI Implementation Inventory & Pain Points — ProjectC_client

**Generated:** 2026-11-02 (static scan, no files modified)  
**Project:** `C:\Projects\ProjectC_client` (Unity 6000.5.2f1, URP 17.5, Windows standalone)

---

## 1. UI systems in use

| System | Evidence | Scope |
|---|---|---|
| Unity UI Toolkit (UIDocument/UXML/USS) | 32 C# files require `UIDocument`; 19 `.uxml`, 20 `.uss`, 22 PanelSettings | Primary for all game windows/menus |
| uGUI (Canvas/Image/Text) | 2 Canvas components in BootstrapScene + damage-number prefab; `UIFactory.cs` | Damage numbers + a few legacy HUD helpers |
| TextMesh Pro | 17 files reference TMPro (mostly world labels/damage) | World-space labels, not main UI |
| IMGUI (`OnGUI`) | Runtime: perf HUD, debug spawners; rest are Editor windows | Debug/editor only |
| Webview | None found | Not used |
| Custom immediate mode | `ProjectCPerfHUD`, `SceneDebugHUD` | Debug overlays |

**Summary:** UI Toolkit is the dominant system; uGUI/TMP are confined to world-space/debug.

## 2. UI windows/panels inventory

### UIDocument-based windows/controllers (32)
`AdminRuntimeWindow`, `CharacterWindow`, `ChartWindow`, `CloudyNavigatorHost`, `CloudyNavigatorWindow`, `CnPopup`, `CommPanelWindow`, `CraftingProgressController`, `CraftingWindow`, `CustomisationWindow`, `DialogWindow`, `EscMenuWindow`, `InventoryUI`, `KeybindingsWindow`, `MainMenuWindow`, `MarketWindowHost`, `NetworkManagerController`, `QuestToast`, `QuestTracker`, `RebindPromptWindow`, `RepairManagerWindow`, `ShipCargoConsoleWindow`, `ShipHudController`, `SkillBindingWindow`, `SkillTreeWindow`, `SocialSkillTreeWindow`, `ProjectCMainMenuMenuBridge`, `ProjectCMainMenuUiHost`, plus CharacterWindow sub-tabs (`ContractsTab`, `InventoryTab`, `MyShipsTab`) and Market tabs.

### uGUI/HUD overlays (not UIDocument)
`AltitudeUI`, `Billboard`, `ControlHintsUI`, `HUDManager`, `NetworkUI`, `PeakNavigationUI`, `SceneDebugHUD`, `DamageNumberService` (+ `PF_DamageNumber.prefab` Canvas).

### Mod UIs
- `Assets/Aura/Mods/ProjectCMainMenu/` — main menu port (smoke field, fonts, ramps).
- `Assets/Aura/Mods/ProjectCCloudyNavigator/` — Cloudy character window (key `[`, v4.3 skills addendum).

### Asset counts
| Asset type | Count |
|---|---|
| `.uxml` | 19 |
| `.uss` | 20 |
| `*PanelSettings.asset` | 22 |
| UIDocument scripts | 32 |

## 3. Known issues / perf investigations (docs/UI)

| Doc | Key finding |
|---|---|
| `BUGS_PHASE_2.md` | BUG-001 Esc-after-close fixed; **BUG-002 cursor not restored after KeybindingsWindow (open)**; BUG-003 USS `Resources.Load` name conflict fixed |
| `PERF_UIElementsRepaintPanels_INVESTIGATION.md` | **3.1 MB/frame GC** from UI Toolkit repaint; CharacterPanel 390 KB, MarketPanel 283 KB; hidden panels still repaint |
| `UI_TOOLKIT_GUIDE.md` | 5 fatal errors documented; canonical template; layout gotchas |
| `full rebuild plans/Cloudy_redesign/*` | Cloudy port steps 0–9 complete; Play Mode acceptance pending; legacy P still works |
| `esc-menu/*`, `MainMenu/*` | Iteration logs, parity notes |

## 4. Anti-pattern scan

| Anti-pattern | Count | Notes |
|---|---|---|
| `Clear() + CloneTree()` | 14 files | Conflicts with UIDocument auto-load (guide §2 Err 4) |
| `Resources.Load<StyleSheet>` | 15 files (14 active + 1 comment) | Known to return `inlineStyle` garbage (guide §2 Err 1) |
| `!important` overuse | 3,186 occurrences / 20 USS | Required by guide but indicates theme-specificity battles |
| Inline `.style.` assignments | 1,901 occurrences | Heavy runtime style mutation (mods dominate) |
| `MarkDirtyRepaint` | 91 calls / 28 files | Repaint pressure; perf investigation target |
| `Update`/`LateUpdate` in UI scripts | 28 files | Per-frame work; input/polling |
| String concat in `.text` | 15 occurrences | Minor GC |
| Multiple Canvases | 2 in BootstrapScene + damage prefab | Low impact (uGUI only) |
| Raycast targets | 1 explicit | Mostly N/A (UI Toolkit) |

## 5. PanelSettings / theme / sorting / visualTreeAsset

### Theme
All 22 PanelSettings assets have `themeUss` set (UnityDefaultRuntimeTheme) — ✅ no null themes.

### sortingOrder
| PanelSettings | sortingOrder |
|---|---|
| CN (Cloudy) | 200 |
| Customisation, SkillTree | 300 |
| MetaRequirement, ShipKey | 900 |
| Chart | 100 |
| ShipHud | 50 |
| PCMM (Main Menu) | 10 |
| CommPanel, Dialog, QuestTracker, EscMenu, Keybindings, MainMenu, RebindPrompt, SkillBinding, Market, ShipCargo, Character, Crafting, Gathering, Inventory | 0 |

### visualTreeAsset / sourceAsset assignment
- BootstrapScene: 19 UIDocuments; **9 have `sourceAsset` assigned**, **10 are null** (rely on runtime code).
- Mod prefabs (`CloudyNavigatorUI`, `ProjectCMainMenuUI`): `sourceAsset = null` (runtime assignment).
- Risk: null sourceAsset + `Clear()+CloneTree()` pattern = double-mount / domain-reload fragility (guide §2 Err 3/4).

## 6. Recommendations (prioritized)

1. **Perf:** Disable `UIDocument` (or destroy) for hidden panels; investigate Character/Market panel repaint (3.1 MB/frame). Add `UIDocument.enabled = false` on hide.
2. **Anti-pattern cleanup:** Remove `Clear()+CloneTree()` in 14 windows; use `_root = _doc.rootVisualElement` (guide canonical template).
3. **USS loading:** Remove `Resources.Load<StyleSheet>` fallbacks in 14 active files; rely on inspector refs (renamed styles like `EscMenuStyles.uss` already).
4. **Cursor bug:** Fix BUG-002 in `KeybindingsWindow.SetOpen(false)` (restore `Cursor.lockState/visible`).
5. **Scene hygiene:** Assign `sourceAsset` (visualTreeAsset) for the 10 null UIDocuments in BootstrapScene and mod prefabs.
6. **Sorting:** Normalize z-order (many panels at 0); document intended stacking.
7. **Update loops:** Audit 28 UI `Update` methods for per-frame allocations; prefer event-driven updates.

---
*Generated from static scan of `C:\Projects\ProjectC_client` (no files modified).*
