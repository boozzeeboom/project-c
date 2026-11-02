# Project C — AAA UI Architecture & Pipeline Blueprint

**Target:** MMO sandbox, Unity 6, URP, Windows standalone (primary), many concurrent windows.
**Authoring date:** 2026-11-02
**Status:** Architecture blueprint (pre-implementation). Complements `UI_SOLUTIONS_RESEARCH_2024-2026.md`.

---

## 0. Executive summary

Project C's UI must support a **desktop-class MMO shell** (chat, inventory, map, crafting, social, market, settings, character sheet, quest journal, world map, minimap, HUD) that is **data-dense, multi-window, heavily localized, and themeable**, running at 60+ FPS on Windows while remaining **authorable by designers in Figma** and **iterable by engineers without re-rigging**.

Recommendation (consistent with the prior research, now elevated to an architecture):

- **Primary UI framework: Unity UI Toolkit (UITK)** for all screen-space chrome, windows, panels, lists, forms, chat, settings. Retained mode, Flexbox layout, USS theming, runtime data binding, virtualized `ListView`/`TreeView`, and Figma round-trip all map directly to MMO needs.
- **uGUI (Canvas)** retained **only** for world-space/3D-attached UI: nameplates, in-world prompts, floating combat text, minimap blips, and heavily animated HUD elements that need GameObject transforms / DOTween / TextMeshPro SDF. Do not migrate these to UITK until world-space UITK (6.2+) matures for your use case.
- **Rive** as an embedded motion layer for state-machine-driven vector UI (loading screens, animated icons, transition wipes, data-bound micro-interactions). It complements, never replaces, the layout system.
- **TextMeshPro** for all text (both stacks), with SDF atlas discipline.
- **NoesisGUI / Coherent Gameface / WebView** are explicitly out of scope for core UI; revisit only if profiling or licensing review changes.

The architecture below is organized around a **Screen/Window/Panel/Widget taxonomy**, a **single façade (UIFrame)**, **data-bound ViewModels**, a **modal stack with focus/navigation**, an **input router**, and a **token-driven theming system**. It is designed so that adding a new MMO window is a **UXML + one ViewModel + one registration line**, with zero changes to core.

---

## 1. Goals & constraints

| # | Goal | Rationale |
|---|------|-----------|
| G1 | Many concurrent, resizable, dockable windows | MMO desktop UX |
| G2 | Virtualized lists of 10k+ items (inventory, chat, market) | MMO data scale |
| G3 | Sub-1 ms UI CPU per frame on mid-range Windows PC | 60 FPS budget with 3D rendering |
| G4 | ≤ 4 draw calls for the full chrome layer | URP batching target |
| G5 | Designer-driven theming (dark/light + brand skins) without code | Live ops / seasonal events |
| G6 | Figma → playable UI in < 1 day per screen | Production velocity |
| G7 | Full keyboard + gamepad + mouse navigation | Accessibility + PC/Steam Deck |
| G8 | Localization into 12+ languages incl. CJK/RTL | Global MMO |
| G9 | Screen-reader-friendly on Windows (Narrator) | Accessibility compliance |
| G10 | Hot reload of UXML/USS in dev; safe reload in prod | Iteration |

**Hard constraints:** Unity 6 (6000.x), URP, Windows primary. No console/mobile in v1 (but keep touch-friendly targets as a forward-compat courtesy). No WebView for core UI.

---

## 2. AAA UI architecture patterns (public observations)

### 2.1 Screen / Window / Panel / Widget taxonomy

Adopt the battle-tested taxonomy from the Blood Runs Cold / Sybo-style frameworks, adapted for MMO:

- **Screen** — a self-contained, full-context UI surface. Two kinds:
  - **Panel** — persistent chrome that coexists with everything (HUD, minimap, chat dock, currency bar, action bar). Always mounted or cheaply toggled.
  - **Dialog (Window)** — a modal or semi-modal surface that is the focus of attention (inventory, map, settings, crafting). Has history.
- **Widget** — a reusable, data-bound fragment (item slot, chat line, quest entry, tooltip, damage number). Instanced many times.
- **Layer** — a container that owns one class of Screen and controls z-order, input blocking, and transitions (DialogLayer, PanelLayer, OverlayLayer, TooltipLayer, WorldSpaceLayer).
- **Para-layer** — a hierarchy bucket a Layer reparents into (e.g. `UnderModals`, `Modals`, `Tooltips`, `Debug`), so z-order is structural, not numeric.

**Rule:** Game code never touches Screens or Layers directly. It talks to one façade (`UIFrame`).

### 2.2 MVVM vs MVP vs event-driven (the honest critique)

Public GDC writing (e.g. "A critique of MVC/MVVM as a pattern for game development") warns that strict MVVM in games creates parallel model hierarchies and boilerplate. The pragmatic AAA answer, seen in Bungie/Sybo-style systems:

- **One controller per Screen** (View + thin presenter in one class), **not** a separate ViewModel class per field.
- **Shared, game-owned data models** (inventory, character stats) are the "Model"; the Screen reads them via data binding or events. Do not duplicate game state into UI models.
- **Events/signals** for cross-module decoupling: game systems emit (`OnInventoryChanged`, `OnQuestUpdated`); UI subscribes if mounted. If no UI is mounted, nothing happens — no work, no allocations.
- **MVVM-style binding is used at the leaf**: UITK runtime bindings bind a Screen's bound properties to UXML elements. This is mechanical, not architectural.

So: **MVP-ish screens + event-driven data flow + UITK leaf binding.** Avoid full MVVM frameworks (StrangeIOC-style) — they are boilerplate-heavy and fight Unity's serialization.

### 2.3 Data binding & reactive UI

- UITK runtime data binding (Unity 6) binds plain C# objects to visual elements via `INotifyBindablePropertyChanged` / `IDataSourceViewHashProvider`.
- Optimize: `[GeneratePropertyBag]`, `[CreateProperty]`, change-tracking interfaces. This removes reflection and prevents no-op updates.
- For high-frequency data (HP bar, cooldown, chat typing), bypass binding and write directly to the element in a throttled update (≤ 10 Hz for text, per-frame for transforms).
- Reactive pattern for lists: bind a `ListView` to an `ObservableList<T>`-style backing store; mutate the store, let the view diff.

### 2.4 State machines & screen flow

- A **FlowController** (state machine) owns the high-level game flow: `Boot → MainMenu → CharacterSelect → Loading → InWorld → Paused → …`. Each state activates a set of Panels/Dialogs.
- Screens themselves are **not** state machines for flow; they are views. Flow lives in one place.
- **Transition grammar**: a small set of reusable transitions (fade, slide-from-edge, scale-pop, crossfade). Parametric tweens (DOTween or UITK USS transitions) for the default 90%; Animator only for exceptional, artist-authored sequences. Animator is avoided for routine UI because of Canvas/vertex-buffer churn and asset explosion.
- **Input is blocked during transitions** via a transparent full-screen blocker, preventing soft-locks and double-taps.

### 2.5 Modal stack & focus/navigation

- A **ModalStack** (LIFO) of Dialogs. Opening a Dialog pushes; closing pops. The top Dialog is the only interactable one; lower ones are dimmed + input-blocked (not destroyed — preserves state).
- **Focus ring**: each Dialog declares a default focus target and a tab order. On push, focus moves to the new Dialog's default; on pop, focus returns to the previous top's last-focused element.
- **Navigation graph**: for gamepad/keyboard, define explicit next/prev (up/down/left/right) per control; fall back to geometric nearest-neighbor. UITK's built-in `Focusable` + `FocusController` is the base; augment with a custom `FocusNavigator` for wrap-around and modal scoping.
- **Escape/Back** pops the top modal; if stack empty, opens the pause menu.

### 2.6 Input routing

Layers, in priority order (highest first):

1. `SystemOverlay` (loading, fatal error, platform overlays)
2. `Modal` (top dialog input)
3. `Tooltip` (hover, non-blocking)
4. `HUD` (action bar, minimap)
5. `World` (gameplay input — only when no modal consumes)

A central **InputRouter** (built on Unity's Input System) dispatches each input action to the highest active layer that claims it. Layers register handlers; the router resolves conflicts. World input is suspended (not just ignored) when a modal is open, to avoid accidental movement.

### 2.7 Localization

- Unity Localization package + UITK bindings (`LocalizedString` binding in UXML/C#).
- Budget **+40% text expansion** in layouts (German/Finnish); use Flexbox so text reflows.
- RTL: Arabic/Hebrew need shaped text + right-to-left layout. UITK's text shaping support is limited — validate early; fallback to TextMeshPro for RTL-heavy surfaces if needed.
- Font fallback chains for CJK; never rely on OS fonts in builds (test in WebGL/standalone).
- Locale switch must not require scene reload: bind locale to a `Locale` asset and re-evaluate bindings.

### 2.8 Accessibility

- Windows: integrate Unity's Accessibility module (screen reader nodes) for Narrator. Map each interactable UITK element to an accessibility node with role, label, value, and action.
- Keyboard: full tab navigation, visible focus ring (high-contrast), skip links.
- Visual: respect Windows high-contrast themes, scalable UI (DPI), color-blind-safe palettes (don't encode meaning in color alone), motion-reduction setting.
- Audio: captions/subtitles for voiced content; UI sounds with volume control.

### 2.9 Theming / design tokens

- Tokens are the single source of truth: colors, spacing, radii, typography, motion timings, elevation. Stored as USS custom properties (`--color-primary`, `--space-md`, `--radius-pill-9`, …) on `:root`.
- Themes are **assets, not code**: a `ThemeData` ScriptableObject bakes tokens into a real USS `StyleSheet` sub-asset; a `ThemeApplier` adds that one sheet to the panel root. The `var()` cascade repaints the whole tree, including `:hover`/`:disabled`/`:checked`.
- Ship Dark + Light; author brand/seasonal themes in a Theme Configurator with live preview. Scope a theme to `:root` (whole panel) or a class (`.theme-night`) so two themes can coexist.
- One `.mobile` (or `.compact`) class on the root flips spacing/tap targets — forward-compat for Steam Deck / future platforms.

### 2.10 Public-talk observations (reverse engineering, public info only)

| Title / Source | Observation relevant to Project C |
|---|---|
| **Destiny 2** — Bungie, "Building the user interface for Destiny" (GDC 2016); "UI of Destiny 2: Pursuits page" (Medium) | Dense, information-heavy MMO-like UI; progressive disclosure (show more only on prompt); item-centric focus; "eyes stay in one place" philosophy; avoid forcing gaze shifts. |
| **Cyberpunk 2077** — CDPR, "User Interface in Cyberpunk 2077: Challenges and Optimizations" (GDC Showcase 2023) | Built UI tech from scratch to support **thousands of independent UI instances** across Night City; UI eluded standard usage patterns and was used for gameplay; heavy emphasis on fitting the system into performance budgets and platform targets. Lesson: budget for many instances early; pool aggressively. |
| **Fortnite** — Epic, "Optimizing UE4 for Fortnite" (GDC 2018) | UI as part of a broader performance budget story; HUD configurability; cross-platform consistency. |
| **Valorant** — Riot, "Art Directing a Consistent and Scalable Interactive Brand Identity" (GDC 2017); "Lessons from Building UI/UX in 2XKO" (GDC) | Unified cross-project UI framework; scalable brand identity; transition from patchwork to unified flexible system for a "forever game". |
| **Genshin Impact** — miHoYo, GDC 2021 | Anime-style open world; UI keeps both beginner and veteran in mind; constant UX iteration; many systems surfaced without overwhelming. |
| **Star Citizen** — CIG | Extremely dense, simulation-grade UI; heavy world-space/diegetic UI; performance challenges with many simultaneous elements — cautionary tale for scope. |
| **SimCity 2013** — EA (GDC talk, "AAA Game UI in HTML and JavaScript") | Web-tech UI inside a native engine via embedded browser; validates the web-inspired retained-mode approach UITK takes. |
| **Sybo Games** — "Building a Unified Cross-Project UI Framework" (GDC 2019) | Unified framework across projects to avoid visual/interaction inconsistency; shared component library. |
| **DICE LA** — "Art Direction for AAA UI" (GDC 2018) | Consistent UI art via theory + best practices; art direction discipline. |
| **Nordeus** — custom UI hierarchy system (Unite talk) | When framework limits bite, studios write custom hierarchies; weigh cost/benefit before doing so. |

**Common AAA threads:** (1) UI is treated as a first-class system with its own budget, not an afterthought; (2) reusable component libraries + design tokens; (3) event-driven, data-bound, virtualized; (4) heavy investment in tooling (Figma/Blend/Noesis Studio) and artist empowerment; (5) performance guardrails (draw calls, overdraw, pooling) enforced in CI.

---

## 3. Asset pipeline

### 3.1 Figma → engine

Three-tier strategy, in priority order:

1. **Primary: official "UI Toolkit Connector for Figma"** (experimental). Imports frames as `.uxml` + `.uss`, maps design tokens → theme stylesheets, one-click sync. Treat as beta; budget manual polish per screen.
2. **Fallback: FigmaToUnity** (open source, github.com/TrackMan/Unity.Package.FigmaToUnity) — parses Figma page documents into UXML/USS.
3. **Fallback: cdmvision/unity-figma-importer-plugin** — outputs both uGUI prefabs and UITK UXML/USS; useful for hybrid screens.
4. **Paid safety net: Figma Converter for Unity** (D.A. Assets) — multi-framework export; keep a license for crunch.

**Workflow:** Figma (design + tokens) → sync → UXML/USS in `Assets/UI/Screens/...` → engineer wires ViewModel + registration → playtest → feedback loop back to Figma. Never hand-edit generated UXML structure in a way that breaks re-sync; keep hand edits in companion `.cs` or partial UXML includes.

### 3.2 Design tokens

- Tokens live in Figma as variables (color, spacing, radius, typography, motion).
- Exported to a `DesignTokens.uss` + a `ThemeData` ScriptableObject via a small exporter (JSON → USS + C#). Keep the exporter in `Tools/FigmaExport/`.
- Naming: `--color-{role}-{variant}`, `--space-{size}`, `--radius-{name}`, `--font-{role}`, `--motion-{name}`. Roles, not values (e.g. `--color-danger`, not `--color-red`).

### 3.3 Component library

- A shared `DesignSystem` folder (see sinanata/unity-ui-toolkit-design-system as a reference implementation) containing: tokens, 40+ components (buttons, inputs, tabs, toggles, sliders, meters, modals, toasts, badges, avatars, nav, pagination, empty states, skeletons, spinners), 120+ SVG icons tinted via `-unity-background-image-tint-color`, and a mobile/compact override stylesheet.
- Components are **UXML templates + USS classes**, never prefabs of GameObjects. Composition over inheritance.
- Version the design system as a git submodule or UPM package (`com.projectc.designsystem`) so multiple projects/branches stay in sync.

### 3.4 Automated export

- CI step: Figma token export → regenerate `DesignTokens.uss` → run a smoke test that all UXML screens parse and all USS variables resolve.
- Sprite/texture export: Figma images → PNG/SVG into `Assets/UI/Textures/`, auto-atlased.

### 3.5 Versioning

- UI assets versioned in git alongside code. Design system as submodule with tagged releases.
- Screen UXML/USS changes reviewed in PRs; visual diffs via screenshot tests (Unity Test Framework + image comparison) for critical screens.

### 3.6 Hot reload

- Dev: Unity's domain reload + UITK live preview in UI Builder; plus a custom file watcher that re-imports UXML/USS on change without domain reload where possible.
- Prod: theme switching at runtime via `ThemeApplier`; no UXML hot reload in shipping builds (stability).

---

## 4. Performance architecture

### 4.1 Batching & draw calls

- **Target: ≤ 4 draw calls for the full chrome layer** (one per layer: HUD, Modal, Tooltip, Overlay).
- UITK batches by GPU state (shader, textures, mesh). Breaks occur on: texture switches beyond 8, masks (stencil), material changes, z/layer changes.
- Mitigations:
  - Use **2D Sprite Atlas** for static icons/sprites; UITK dynamic atlas for runtime textures. Keep total distinct textures ≤ 8 per batch.
  - **Rectangular masks** (shader-based, no stencil) preferred; complex/rounded masks only where needed, ≤ 7 nested levels, flat hierarchy.
  - Set **Vertex Budget** in PanelSettings to fit the whole UI in one buffer (e.g. 20k–50k vertices) to avoid buffer splits.
  - Usage hints: `DynamicTransform` for animated elements, `GroupTransform` for containers with many animated children.
  - Animate **transforms** (`translate`/`scale`/`rotate`), never layout props (`width`/`height`/`top`/`left`).
  - Avoid class-switching during animations on large hierarchies; use inline style changes.

### 4.2 Text atlas

- TextMeshPro SDF atlases: one atlas per font weight/style, sized to cover the glyph set; dynamic atlas with a clear-on-build step; disable AutoSize on dynamic fields; use SDF16 for large glyphs.
- UITK text: prefer static font assets; fallback chain for CJK/Arabic; test shaping in builds.
- Limit distinct fonts to ≤ 4 families to keep atlas count low.

### 4.3 Caching & pooling

- **Pool Widgets**, not destroy. Reparent + disable (disable first, then reparent, to avoid dirtying). Use a `GameObjectPool`/`VisualElementPool` keyed by template.
- Pool chat lines, inventory slots, quest entries, damage numbers, tooltips.
- Cache parsed UXML (`VisualTreeAsset`) and USS (`StyleSheet`) references; never `Resources.Load` per instance.
- Cache localization strings per locale; invalidate on locale change.

### 4.4 Invalidation & dirty flags

- Data binding change-tracking (`INotifyBindablePropertyChanged`, `IDataSourceViewHashProvider`) prevents no-op updates.
- For manual updates: dirty flags per element; only recompute layout/style when dirty.
- Avoid per-frame `Update` on UI; drive from events. High-frequency (HP, cooldown) throttled to ≤ 10 Hz text / per-frame transform only.

### 4.5 Off-screen culling

- `ListView`/`TreeView` virtualization renders only visible items (handles 10k+).
- For custom scroll views, set `style.overflow = Overflow.Hidden` and use `ListView` rather than a giant `ScrollView` of elements.
- Hide off-screen Panels with `DisplayStyle.None` (stops layout + render) rather than `opacity: 0` (still rendered).

### 4.6 Render targets & resolution scaling

- Chrome runs in Screen Space (Overlay or Camera) at native resolution; **exclude UI from render-scale changes** (don't downscale UI with 3D render scale).
- For world-space UI: dedicated camera with higher resolution / supersampling; or render to a `RenderTexture` at a fixed DPI and display via a quad.
- DPI awareness: scale UI by `Screen.dpi` / reference resolution (1920×1080 base); test at 100/125/150/200% Windows scaling.

### 4.7 World-space UI

- World-space UI (nameplates, prompts, floating text) stays **uGUI** in v1 (mature, TextMeshPro SDF, DOTween). Mount on a dedicated "WorldUI" canvas with its own camera.
- Evaluate UITK world-space (6.2+) for v2; it supports `PanelSettings` render mode = World Space.
- Pool and billboard world-space elements; cap simultaneous count (e.g. ≤ 50 nameplates) with distance-based LOD (hide beyond N meters, simplify beyond M).

---

## 5. Unity 6 specifics

### 5.1 UI Toolkit data binding

- Runtime bindings API (Unity 6). Bind plain C# objects to visual elements.
- Use `[GeneratePropertyBag]` + `[CreateProperty]` for compile-time property bags (no reflection).
- Implement `INotifyBindablePropertyChanged` + `IDataSourceViewHashProvider` for change tracking.
- Binding modes: `TwoWay` for inputs, `ToOneWay` for display, `ToSource` for rare cases. Update triggers: `OnValueChanged` (default) or `OnFocusOut` for expensive validation.

### 5.2 USS variables

- Custom properties (`--*`) on `:root` or scoped selectors. Use `var(--name, fallback)`.
- Unity 6 USS additions: `@import`, `background-size`, `-unity-background-image-tint-color`, parent-state cascades. Import order is load-bearing (general → specific → mobile last).
- Limitation: USS variables cannot be mutated from C# at runtime in all cases; prefer stylesheet swapping for theming.

### 5.3 Custom controls

- Derive from `VisualElement`, register with `[UxmlElement]` / `[UxmlAttribute]` (Unity 6). Expose bindable properties.
- Keep custom controls **stateless-ish**: they render bound data; logic lives in the ViewModel/Screen.
- One control = one responsibility (e.g. `ItemSlot`, `ChatLine`, `HealthBar`).

### 5.4 UXML templates

- `VisualTreeAsset` (`.uxml`) for structure; `UxmlTraits`/attributes for parameters.
- Use `Template`/`Instance` for reusable fragments (e.g. `ItemSlot.uxml` instanced N times in a `ListView`).
- Keep UXML shallow; deep hierarchies cost layout.

### 5.5 PanelSettings

- One `PanelSettings` asset per layer (HUD, Modal, Tooltip, Overlay) — controls sort order, scale mode, vertex budget, dynamic atlas, theme.
- Scale mode: `ScaleWithScreenSize`, reference 1920×1080, match 0.5.
- Assign a `ThemeStyleSheet` (or runtime `ThemeApplier`) per panel.

### 5.6 Render texture & world-space UI

- For world-space UITK: `PanelSettings.renderMode = WorldSpace`, attach `UIDocument` to a GameObject. Supports input via `PanelRaycaster`.
- For URP post-processing on UI: render to a `RenderTexture` via a dedicated camera and display on a quad (costs a pass; budget carefully).

---

## 6. Figma-to-Unity workflows & plugins (summary)

| Tool | Type | Output | Notes |
|---|---|---|---|
| Official UI Toolkit Connector for Figma | Official, experimental | UXML + USS + tokens | Primary path; beta |
| FigmaToUnity (TrackMan) | Open source | UXML + USS | Fallback importer |
| cdmvision unity-figma-importer | Open source | uGUI prefabs + UITK | Hybrid fallback |
| Figma Converter for Unity (D.A. Assets) | Paid | Multi-framework | Safety net |
| Unity Assistant 2.7 (pre) | Official, pre-release | Figma import | Watchlist |
| VectoUI | Paid | uGUI prefabs | Legacy fallback |

**Recommendation:** standardize on the official connector; keep FigmaToUnity vendored as a fallback; document the manual polish checklist (fonts, icons, bindings, RTL).

---

## 7. Proposed architecture for Project C

### 7.1 Layered module map

```
ProjectC/
├── Core/                    # game-agnostic framework
│   ├── UIFramework/          # UIFrame, layers, modal stack, focus, input router, transitions
│   ├── DataBinding/         # base VM helpers, observable collections, binding adapters
│   ├── Theming/             # ThemeData, ThemeApplier, token USS generation
│   ├── Localization/        # locale service, string tables, RTL helpers
│   ├── Accessibility/       # screen-reader node bridge, focus ring, contrast
│   └── Performance/         # pools, caches, dirty flags, profiling hooks
├── Game/                    # game-specific
│   ├── UI/
│   │   ├── Screens/         # one folder per Screen (UXML + USS + .cs)
│   │   ├── Widgets/         # reusable widgets
│   │   ├── ViewModels/      # per-screen VMs (thin, game-data adapters)
│   │   └── Flow/            # FlowController state machine, transitions
│   └── Data/                # game data models (inventory, character, quest, ...)
├── DesignSystem/            # tokens, components, icons (submodule/UPM)
├── Tools/
│   ├── FigmaExport/         # token + asset export
│   └── UITests/             # screenshot + parse tests
└── ThirdParty/              # Rive runtime, etc.
```

### 7.2 Module boundaries & ownership

| Module | Owns | Depends on | Must not |
|---|---|---|---|
| `UIFramework` | UIFrame façade, layers, modal stack, focus, input routing, transitions, pooling primitives | Unity core, Input System | Game data, specific screens |
| `DataBinding` | Observable collections, binding adapters, change-tracking base classes | UIFramework | Game logic |
| `Theming` | Tokens, ThemeData, ThemeApplier, USS gen | UIFramework | Game logic |
| `Localization` | Locale service, string tables, RTL | Unity Localization | UI specifics |
| `Accessibility` | Screen-reader bridge, focus ring styles | UIFramework | Game logic |
| `Performance` | Pools, caches, dirty flags, profiling | UIFramework | Game logic |
| `Game/UI/Screens` | Screen controllers, VMs, UXML/USS | All Core | Other screens directly (go via UIFrame) |
| `Game/UI/Flow` | FlowController, transition defs | UIFramework, Screens | Game data (reads via events) |
| `DesignSystem` | Tokens, components, icons | none (pure assets) | Code |

**Boundary rules:**
- Screens communicate with game systems **only** via events/signals and read-only data queries. Never mutate game state from a Screen except through a documented command.
- No Screen references another Screen. Navigation goes through `UIFrame` + `FlowController`.
- `DesignSystem` has zero code dependencies; it is pure assets + optional thin helpers.

### 7.3 Core interfaces (C#)

```csharp
// Façade — the only entry point game code uses.
public interface IUIFrame
{
    void Initialize(UIFrameConfig config);
    void ShowScreen(string screenId, IScreenProperties props = null);
    void HideScreen(string screenId);
    void PushModal(string modalId, IScreenProperties props = null);
    void PopModal();
    void ClearModals();
    bool IsModalOpen { get; }
    IUIScreen CurrentModal { get; }
    T GetScreen<T>(string screenId) where T : IUIScreen;
    event Action<string> OnScreenShown;
    event Action<string> OnScreenHidden;
}

// Layer contract.
public interface IUILayer
{
    string LayerId { get; }
    int Priority { get; }
    void Mount(IUIScreen screen);
    void Unmount(IUIScreen screen);
    void SetInputEnabled(bool enabled);
    bool ClaimsInput(InputAction action);
}

// Screen contract.
public interface IUIScreen
{
    string ScreenId { get; }
    bool IsVisible { get; }
    IScreenProperties Properties { get; }
    void SetProperties(IScreenProperties props);
    UniTask ShowAsync(CancellationToken ct = default);
    UniTask HideAsync(CancellationToken ct = default);
    void OnFocusGained();
    void OnFocusLost();
}

public interface IScreenProperties { } // marker; concrete payloads derive

// Modal stack.
public interface IModalStack
{
    void Push(IUIScreen modal);
    IUIScreen Pop();
    IUIScreen Peek();
    IReadOnlyList<IUIScreen> Stack { get; }
    event Action<IUIScreen> Pushed;
    event Action<IUIScreen> Popped;
}

// Focus.
public interface IFocusNavigator
{
    void SetScope(VisualElement scope);
    void MoveNext();
    void MovePrev();
    void MoveDirection(FocusDirection dir);
    VisualElement Current { get; }
    void SetDefault(VisualElement target);
    event Action<VisualElement> FocusChanged;
}

// Input routing.
public interface IInputRouter
{
    void RegisterLayer(IUILayer layer);
    void UnregisterLayer(IUILayer layer);
    bool Dispatch(InputAction action, InputActionPhase phase);
    IUILayer TopClaimant(InputAction action);
}

// Theming.
public interface IThemeService
{
    ThemeData Current { get; }
    void Apply(ThemeData theme, VisualElement root);
    void Apply(string themeId, VisualElement root);
    event Action<ThemeData> Changed;
}

// Localization.
public interface ILocalizationService
{
    Locale CurrentLocale { get; }
    void SetLocale(Locale locale);
    string Get(string table, string key);
    LocalizedString Bind(string table, string key);
    event Action<Locale> LocaleChanged;
}

// Pooling.
public interface IUIPool<T> where T : VisualElement
{
    T Get();
    void Release(T instance);
    void Prewarm(int count);
    int ActiveCount { get; }
    int PooledCount { get; }
}

// Transitions.
public interface ITransition
{
    UniTask PlayInAsync(VisualElement target, CancellationToken ct);
    UniTask PlayOutAsync(VisualElement target, CancellationToken ct);
}
```

### 7.4 Screen flow & modal stack (concrete behavior)

- `FlowController` is a state machine (e.g. using a lightweight FSM or `ScriptableObject`-based states). States: `Boot, MainMenu, CharacterSelect, Loading, InWorld, Paused, Settings, ...`. Each state declares `PanelsToMount` and `ModalsToPush`.
- On state enter: mount panels, push modals, block/unblock input per layer priority.
- On state exit: pop modals (with transition), unmount panels.
- `ModalStack.Push` dims the previous top via a `--modal-dim` overlay and blocks its input; runs the new modal's `ShowAsync` (transition in).
- `ModalStack.Pop` runs `HideAsync` on the top, then restores focus to the new top's last-focused element and removes dim.

### 7.5 Data binding & VM layer

- Each Screen has a `*ViewModel` (plain C# class) implementing `INotifyBindablePropertyChanged`. VMs are **adapters** over game data: they expose bindable properties and commands; they do not own game state.
- VMs are created by a `ViewModelFactory` (injected game services) when the Screen mounts; disposed on unmount.
- High-frequency data uses a `ThrottledBinding` helper (min interval) or direct element writes.
- Lists: `ListView` bound to an `ObservableList<T>` in the VM; mutations raise collection-changed events; UITK diffs.

### 7.6 Input routing (concrete)

- Built on Unity Input System. One `InputActionAsset` with action maps per layer: `UI`, `HUD`, `World`, `System`.
- `InputRouter` activates/deactivates action maps based on active layers and modal stack.
- When a modal is open: `World` map disabled; `UI` map enabled and scoped to the modal's focus scope.
- Gamepad: D-pad/analog → focus navigation; face buttons → confirm/cancel/context; bumpers → tab switching within a window.

### 7.7 Theming/tokens (concrete)

- `DesignTokens.uss` defines all `--*` variables on `:root`.
- `ThemeData` ScriptableObject holds token values; editor bakes them into a `StyleSheet` sub-asset.
- `ThemeApplier` (per panel) adds the theme sheet to the root and toggles a scope class (`.theme-dark`/`.theme-light`).
- Seasonal themes = new `ThemeData` asset + one line in a theme registry; selectable at runtime in settings and (optionally) pushed by live-ops config.

### 7.8 Localization & accessibility (concrete)

- Unity Localization package. String tables per feature (`UI.Common`, `UI.Inventory`, …). Asset tables for icons/images that vary by locale.
- UITK binding: `<ui:Label text="{localized:...}" />` or C# `LocalizedString`.
- RTL: detect locale direction; apply a `.rtl` class on root that flips `flex-direction`/text-align; validate text shaping in a standalone build before shipping a locale.
- Accessibility: `AccessibilityNode` per interactable; `IFocusNavigator` drives screen-reader focus; high-contrast theme variant; motion-reduction flag disables transitions.

### 7.9 Performance budget & guardrails

| Metric | Budget | Enforcement |
|---|---|---|
| Chrome draw calls | ≤ 4 | Frame Debugger in CI smoke test |
| UI CPU per frame | ≤ 1.0 ms (mid-range PC) | Profiler marker `UIFrame.Update` |
| Modal open transition | ≤ 16 ms (no hitch) | Profiler |
| Inventory list scroll (10k items) | ≥ 60 FPS | PlayMode perf test |
| Chat append (1k lines) | ≤ 0.5 ms | PlayMode test |
| Memory: UI assets | ≤ 120 MB | Memory Profiler snapshot in CI |
| World-space nameplates | ≤ 50 simultaneous | Runtime cap + LOD |

Guardrails: a `UIPerformanceMonitor` MonoBehaviour in dev builds logs violations to console and (optionally) to a telemetry endpoint. CI runs a PlayMode perf suite on a reference machine.

---

## 8. Migration strategy

Assuming the project may start from uGUI or from scratch. Phased, low-risk:

**Phase 0 — Foundations (2–3 weeks)**
- Stand up `UIFramework`, `DesignSystem`, token pipeline, one PanelSettings per layer, Figma connector trial.
- Deliverable: a themed main menu + settings window in UITK, end-to-end (design → Figma → UXML → VM → play).

**Phase 1 — HUD & Panels (3–4 weeks)**
- Migrate persistent HUD (currency, action bar, minimap container, chat dock) to UITK Panels.
- Keep world-space minimap blips in uGUI; bridge via a shared data model.
- Deliverable: in-world HUD running on UITK; perf baseline recorded.

**Phase 2 — Core Windows (4–6 weeks)**
- Inventory, character sheet, quest journal, map, crafting, market — Dialogs with virtualized lists.
- Implement ModalStack, focus navigation, input routing.
- Deliverable: all primary windows in UITK; uGUI windows removed.

**Phase 3 — Social & Chat (2–3 weeks)**
- Chat window with virtualized, pooled lines; emotes; channels.
- Social window (friends, guild, party).

**Phase 4 — Polish & Accessibility (2–3 weeks)**
- Theming (dark/light + one seasonal), localization (first 4 languages incl. one CJK), accessibility (Narrator, keyboard, high-contrast, motion-reduction).
- Performance pass: pooling, batching, vertex budgets, culling.

**Phase 5 — World-space UI decision (1–2 weeks eval)**
- Evaluate UITK world-space for nameplates/prompts vs staying on uGUI. Decision gate: only migrate if perf + authoring improve.

**Rollback plan:** Each phase keeps the previous system behind a feature flag (`UI_USE_UITK`) so a regression can fall back to uGUI for a single screen without reverting the whole phase. Screens are behind `IScreen` interfaces, so swapping implementations is local.

**Migration rules:**
- Never migrate a screen and redesign it in the same PR.
- Migrate data models first (make them UI-agnostic), then views.
- Keep a `Legacy/` folder for uGUI screens until the phase is signed off, then delete (don't leave dead code).

---

## 9. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| UITK runtime roughness / API churn | Medium | Medium | Pin Unity minor version; abstraction layer (`IUIScreen`) isolates; phase-gated migration |
| Figma connector breaks on re-sync | Medium | Low | Fallback importer vendored; manual polish checklist; don't over-customize generated UXML |
| USS variable / theming gaps | Medium | Medium | Theme via stylesheet swap, not runtime var mutation; test each theme in CI |
| RTL/CJK text shaping gaps | Medium | High | Validate in standalone build in Phase 4; fallback to TMP for RTL-heavy surfaces |
| Performance regression in chat/market lists | Medium | High | Virtualization + pooling mandatory; perf tests in CI; budget enforcement |
| Accessibility gaps on Windows Narrator | High | Medium | Start accessibility in Phase 0, not Phase 4; weekly Narrator smoke tests |
| World-space UI scope creep | Medium | Medium | Explicit decision gate in Phase 5; default to uGUI |
| Designer/artist adoption friction | Medium | Medium | Artist training in Phase 0; UI Builder + Figma as primary authoring; keep Animator escape hatch |

---

## 10. Appendix

### A. Folder layout (UI assets)

```
Assets/
├── UI/
│   ├── Screens/{ScreenName}/{ScreenName}.uxml, .uss, {ScreenName}Screen.cs, {ScreenName}ViewModel.cs
│   ├── Widgets/{WidgetName}/...
│   ├── Panels/{PanelName}/...
│   └── Themes/{ThemeName}.asset
├── DesignSystem/          # submodule
└── Resources/             # only for truly global assets
```

### B. Naming conventions

- Screen IDs: `PascalCase` matching folder name (`Inventory`, `CharacterSheet`).
- USS classes: `ds-*` for design system, `pc-*` for project-specific, BEM-ish modifiers (`pc-item-slot--selected`).
- ViewModels: `{Screen}ViewModel`.
- Layers: `{Name}Layer`.
- Pools: `{Widget}Pool`.

### C. Pre-ship checklist (per screen)

- [ ] UXML parses; no missing assets.
- [ ] All text localized; +40% expansion tested.
- [ ] RTL tested if applicable.
- [ ] Keyboard + gamepad navigation works; focus ring visible.
- [ ] Screen reader node labels set.
- [ ] High-contrast theme tested.
- [ ] Motion-reduction respected.
- [ ] Draw calls ≤ budget (Frame Debugger).
- [ ] No per-frame allocations (Profiler).
- [ ] Pooling in place for repeated widgets.
- [ ] Transitions don't soft-lock input.
- [ ] Escape/Back pops correctly.

### D. References (public)

- Unity Docs: "Optimizing performance (UI Toolkit)" — docs.unity3d.com (6000.5/6000.6).
- Unity Docs: "Runtime data binding" — docs.unity3d.com (7000.0).
- Unite 2024: "Getting the best performance with UI Toolkit" (Nicolas Borromeo).
- Unite 2025: "Reach new audiences with Accessibility and Localization in Unity 6".
- GDC: Bungie "Building the user interface for Destiny" (2016); CDPR "User Interface in Cyberpunk 2077" (2023 Showcase); Riot "Art Directing a Consistent and Scalable Interactive Brand Identity" (2017); Sybo "Building a Unified Cross-Project UI Framework" (2019); DICE LA "Art Direction for AAA UI" (2018); miHoYo "Genshin Impact" (2021).
- Game Developer: "A UI System Architecture and Workflow for Unity" (Yanko Oliveira).
- Design tokens: Martin Fowler, "Design Token-Based UI Architecture".
- Figma: official UI Toolkit Connector; FigmaToUnity; cdmvision unity-figma-importer.
- Open source reference: sinanata/unity-ui-toolkit-design-system (MIT).

---

*End of blueprint.*
