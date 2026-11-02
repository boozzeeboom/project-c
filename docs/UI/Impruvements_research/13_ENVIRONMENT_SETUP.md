# 13 — Environment Setup & Tooling for Beautiful UI in Unity

> **Scope:** Project C: The Clouds (Unity 6 `6000.5.2f1`, URP 17.5.0, Windows standalone, MMO-sandbox). Companion to `07_DESIGN_TOOLING_AND_FIGMA_FREE.md`, `08_RENDERING_APPROACHES.md`, `10_TOOLING_PIPELINE.md`, `11_AGENT_TOOLING.md`, `12_OVERCOMING_LIMITS.md`. This file is the **environment + tooling setup guide**: what to install, how to wire it, how to run the pipeline, and how to hit AAA visual quality.  
> **Hard constraints inherited:** no Play Mode by agent, no screenshots by agent, no upgrading Unity/packages without explicit user approval, no hand-writing `.meta`/`.asmdef`.

---

## 0. TL;DR (decisions)

| Area | Decision | Why |
|---|---|---|
| Engine baseline | Unity `6000.5.2f1` + URP `17.5.0` (pinned) | Required for NGO 2.x; don't upgrade casually. |
| Primary design tool | **FigCraft** (alpha, ~49% parity) | Free, offline, no account, 400+ commands, MCP+CLI, variables/components/vectors/effects, Dev Mode code gen. |
| Animation/interactive runtime | **Rive** (`rive-unity`, technical preview → Asset Store) | State machines + data binding + scripting; open runtime; designer-authored. |
| Engine-side authoring | **Unity UI Builder** + hand-edited UXML/USS | Only tool that writes the real `*.uxml`/`*.uss` the runtime uses; git-diffable. |
| Fallback design tool | **Figma Free (Starter)** | `.fig` interop, 3-file limit, no Dev Mode/MCP on free; use only for contractor files. |
| Fallback 2 | **Penpot** (self-host) / **Lunacy** (native desktop) | Penpot = real CSS Flexbox/Grid + tokens + MCP; Lunacy = native, opens `.fig`. |
| Tokens | FigCraft `variable.export` (JSON/CSS) → `DesignTokens.uss` + `ThemeData` ScriptableObject | CSS custom props map 1:1 to USS custom props. |
| Hand-off | FigCraft MCP/CLI → thin importer → UXML/USS → UI Builder review → Play Mode test (by user) | No manual pixel-copying; importer is the bridge. |
| "Alive" UI | Rive (interactive) + Lottie (micro) + VFX Graph (juice) + USS transforms/filters + custom URP shaders | Layered: motion + feedback + blur/glow. |
| AAA reverse-eng lessons | Design system as product, data-driven ViewModels, virtualization, dirty tracking, interactive prototypes, perf budgets in CI | Synthesis of Sybo/Bungie/Riot/LoL/Naughty Dog/Halo practice. |

---

## 1. Scope & constraints (Project C)

- **Build target:** StandaloneWindows64 only.
- **UI framework:** UI Toolkit (UXML/USS) for screens/panels/lists/forms; uGUI for world-space/3D UI; TextMeshPro for text; Rive for animated vector UI.
- **Performance budgets (from `06_PERFORMANCE_GUARDRAILS.md`):** ≤4 draw calls for chrome, ≤1 ms UI CPU/frame, 10k+ items via ListView virtualization, pooling, dirty-tracking, off-screen culling.
- **Floating Origin:** new UI content with world coords must follow `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md`.
- **Agent rules:** no Play Mode, no screenshots, no `run_tests` MCP, no build via MCP. User verifies.
- **No breaking the main project:** Milestone 1 (tooling) is done *beside* the game (sandbox). No Milestone 2 without explicit "go".

---

## 2. Environment setup guide

### 2.1 Unity Editor & project baseline

1. **Editor version:** `6000.5.2f1` (Unity 6 stable). Do not upgrade without explicit user approval (NGO 2.x dependency).
2. **Render pipeline:** URP `17.5.0`. Assets under `Assets/_Project/Settings/URP-*`. Keep URP asset assigned in Project Settings → Graphics.
3. **Input:** New Input System `1.19.0`. Pattern: `PlayerInputReader`. Don't refactor old stubs ad hoc.
4. **Netcode:** NGO `2.13.0`. Use `NetworkVariable<T>`, `[Rpc(SendTo.X)]`. No deprecated `[ServerRpc]`/`[ClientRpc]`.
5. **Cinemachine:** `3.1.7` (new 3.x API).
6. **Build target:** StandaloneWindows64. Verify in Build Settings.
7. **Project path conventions:** all gameplay code in `Assets/_Project/Scripts/<Subsystem>/`; UI in `Assets/_Project/UI/` (create if absent). Namespaces `ProjectC.<Subsystem>`.

**Verification:** open Unity → Console → 0 errors; `mcpforunity://editor/state` → `data.compilation.is_compiling == false` and `data.advice.ready_for_tools == true`.

### 2.2 URP & rendering settings for UI

- **URP asset:** enable **Opaque Texture** (required for background blur in uGUI procedural UI; also useful for UITK backdrop effects via render textures). Enable **Depth Texture** if needed for post FX.
- **Quality:** set consistent quality level for UI testing (1080p/1440p/4K).
- **Color space:** Linear (default for URP). Keep sRGB content; convert Display P3 at export if needed.
- **Anti-aliasing:** MSAA off for UI render textures where crispness matters; use SDF text.
- **Shader Graph:** Unity 6.3+ allows custom shaders in UITK via `-unity-material`. If staying on 6.0, use USS + render textures + custom URP shaders.
- **Post-processing:** URP Volume for bloom/vignette on UI render targets (optional, measure cost).

### 2.3 Packages to install (Unity)

Install via Package Manager (git URL or Asset Store). **Ask before changing `Packages/manifest.json`.**

| Package | Source | Purpose | Notes |
|---|---|---|---|
| `com.unity.ui` (UI Toolkit) | Built-in | Core UI | Already present. |
| `com.unity.ugui` | Built-in | World-space/3D UI | Keep for 3D-attached UI. |
| `com.unity.textmeshpro` | Built-in | SDF text | Use for all text. |
| `com.unity.vectorgraphics` (optional) | Package Manager | SVG rendering | Optional; rasterize to PNG by default. |
| Rive Unity runtime | Asset Store / git | Interactive animated UI | Technical preview; Windows needs D3D11/OpenGL or Vulkan. Set Graphics API accordingly (Project Settings → Player). |
| DOTween (optional) | Asset Store | Code-driven tweens | Use when Rive is overkill. |
| Lottie player (optional) | OpenUPM `com.gilzoide.lottie-player` | One-shot micro-anim | Cheaper than Rive for non-interactive. |
| Spine Unity (optional) | Esoteric Software | Skeletal 2D (mascots) | Only for character UI. |

**Rive platform checklist (Windows standalone):** Graphics API = D3D11 or OpenGL or Vulkan. Rive is technical preview; verify on target hardware.

### 2.4 Design tooling install

#### FigCraft (primary)
- **Download:** GitHub releases `storytold/figcraft` (native binaries Win/Mac/Linux; WebAssembly browser build at figcraft.net). Or build: `cargo build --release`.
- **CLI:** `figcraft-cli` (render, export, run scripts, mcp, commands). Build with `cargo build --release -p figcraft-cli` → `target/release/figcraft-cli`.
- **MCP:** `figcraft-cli mcp` (headless) or `figcraft-cli mcp --attach <port>` (attached to running app started with `figcraft --control <port>`).
- **Control channel (attached):** app listens on 127.0.0.1 only. Agent sees changes live; app shows agent avatar + outlines of last edit (display only, not an undo step).
- **Automation confinement (headless only):** `--automation-read-root DIR` / `--automation-write-root DIR` (repeatable). Covers all file-touching commands. Network commands refused when confined.
- **Fonts:** headless uses built-in + system fonts; `--no-system-fonts` for reproducible CI renders.
- **Version pinning:** pin a release; alpha changes frequently. Keep Figma Free fallback.

#### Figma Free (fallback)
- Account on figma.com, Starter plan. Use drafts for scratch; 3-file limit in team folder.
- Use only for `.fig` interop / contractor files. Consider figmingo-mcp (local-first, PAT-based, no monthly caps) if you need agent reads on free plan.

#### Penpot (fallback 2)
- Self-host (Docker) or cloud at penpot.app. Export tokens via `penpot-export` (npm) to CSS/SCSS/JSON. MCP available.

#### Lunacy (fallback 2)
- Download from icons8.com/lunacy (free, native Win/Mac/Linux). Opens `.fig`/`.sketch`. Export PNG/SVG/PDF/CSS/XAML/JSON.

### 2.5 MCP / CLI wiring

**Already configured in opencode:** `unityMCP` (coplay, port 6401, stdio via uvx) and `anklebreaker` (port 7890). Use `unityMCP` as default for standard work; `anklebreaker` only for its unique features (Hub, multi-instance, profiler, capture). See AGENTS.md routing table.

**Add FigCraft MCP (recommended) to your MCP client config** (example, stdio):
```json
{
  "mcpServers": {
    "figcraft": {
      "command": "figcraft-cli",
      "args": ["mcp", "--automation-read-root", "C:\\Projects\\ProjectC_client", "--automation-write-root", "C:\\Projects\\ProjectC_client\\docs\\UI"]
    }
  }
}
```
For live co-editing with the user, run the app with `figcraft --control 7979` and attach: `figcraft-cli mcp --attach 7979`.

**Penpot MCP (optional):** install per `help.penpot.app/mcp` (plugin inside Penpot + MCP server URL + key). Tools: `execute_code`, `export_shape`, `import_image`, `high_level_overview`, `penpot_api_info`.

**Figma MCP on free plan:** official MCP limited to 20 calls/month for read; consider figmingo-mcp (local-first, REST PAT, no caps) for read-side parity.

### 2.6 Folder layout & git hygiene

Create (Milestone 1, sandbox-first):
```
Assets/_Project/UI/
├── Screens/          # *.uxml (after Builder review)
├── Styles/           # *.uss (component styles)
├── Tokens/           # DesignTokens.uss, ThemeData assets
├── Themes/           # ThemeData ScriptableObjects
├── Sprites/          # exported PNG/SVG atlas
├── Connect/          # figcraft.connect.json
├── Scripts/          # ViewModels, UIFrame, bindings
└── Rive/             # .riv files + wrappers
docs/UI/
├── design/           # .figcraft / .fig sources
├── snapshots/        # .fig exports for version history
└── Impruvements_research/
```
**Git hygiene:** commit `.figcraft`/`.fig` snapshots on milestones (Figma Free has 30-day history). Never commit `Library/`, `Temp/`, `Builds/`, `UserSettings/`, `Logs/`, `obj/`, `bin/`. Never hand-write `.meta`/`.asmdef`.

### 2.7 CI & automation (optional, recommended)

- **Headless export:** `figcraft-cli run build.json` or `figcraft-cli mcp` in CI on design-file change → exports tokens + assets + CSS into staging.
- **Importer:** Python/C# script consumes exports → generates `DesignTokens.uss`, `ThemeData`, sprite atlas, UXML/USS, `figcraft.connect.json`. Opens PR; human reviews diff.
- **Drift check:** hash of exported tokens vs committed `DesignTokens.uss`; fail CI on mismatch.
- **Perf guardrails:** draw calls/GC budgets checked in CI (see `06_PERFORMANCE_GUARDRAILS.md`).
- **Visual regression (user-driven):** FigCraft `screenshot`/`export` vs Unity frame (user captures); image diff optional.

### 2.8 Validation checklist (per change)

1. Unity Console → 0 errors, 0 new warnings (relevant).
2. `mcpforunity://editor/state` → `is_compiling == false`, `ready_for_tools == true`.
3. `read_console` after script edits.
4. Perf: ≤4 draw calls chrome, ≤1 ms UI CPU/frame.
5. Manual playtest by user: F8 floating-origin rebase → UI stays; F9 → returns.
6. Visual: compare against FigCraft screenshot at 1080p/1440p/4K.

---

## 3. Tooling comparison & recommendations

### 3.1 Scoring (1–5)

| Criterion | FigCraft | Figma Free | Penpot | Lunacy | UI Builder |
|---|---|---|---|---|---|
| Cost | 5 | 4 | 5 | 5 | 5 |
| Offline / no account | 5 | 2 | 4 (self-host) | 5 | 5 |
| Auto layout fidelity | 5 | 5 | 5 (CSS) | 4 | 3 (manual USS) |
| Variables/tokens | 5 | 4 | 4 | 3 | 3 (consume only) |
| Component/variants | 4 | 5 | 4 | 4 | 2 |
| Vector editing | 5 | 5 | 4 | 4 | 1 |
| Effects (blur/glass) | 5 | 5 | 3 | 3 | 2 |
| Dev Mode / inspect | 4 | 0 (paid) | 3 | 2 | 3 |
| Code gen (CSS/React/…) | 5 | 0 (paid) | 3 | 2 | 0 |
| MCP / agent-driven | 5 | 0 (paid) | 3 | 0 | 0 |
| CLI / headless | 5 | 0 | 2 | 0 | 0 |
| Unity/UXML export | 0 | 0 | 0 | 0 | 5 |
| Maturity/stability | 2 (alpha) | 5 | 4 | 4 | 4 |
| **Total (rough)** | **49** | **32** | **37** | **32** | **32** |

### 3.2 Recommendations (decision matrix)

1. **Adopt FigCraft as primary design tool** for all new UI work (alpha caveat). Keep one Design System file (tokens + components) and screen files.
2. **Keep Figma Free as fallback** for `.fig` interop and contractor hand-offs. Use figmingo-mcp for agent reads if needed.
3. **Keep Penpot/Lunacy as second fallback** (self-hosting / native desktop / CSS parity).
4. **UI Builder remains the engine-side authoring tool.** No design tool replaces it; design tools *feed* it.
5. **Build a thin importer** (Python or C#) over FigCraft exports. Budget 1–2 weeks. Highest-leverage investment.
6. **Rive for all interactive/animated UI** (buttons, HUD, transitions). Lottie for one-shot micro-anim. Spine only for skeletal mascots. VFX Graph for juice.
7. **Do not build a custom design tool.** FigCraft covers 90%; the rest is an importer.
8. **Do not buy Figma Professional** unless Dev Mode/MCP becomes a hard blocker for a contractor pipeline.

---

## 4. Tool deep dives

### 4.1 FigCraft

**Status:** alpha, ~49% parity (own estimate). Core editor works end-to-end. `.fig` import best-effort. No real-world design-doc validation yet. Treat as primary for new work; keep Figma Free fallback. Clean-room (no proprietary code/formats/icons). Dual license MIT/Apache-2.0. No telemetry, no account.

**Capabilities relevant to game UI:**
- **Layout:** auto layout both directions, wrap, grid (fixed/hug/fr tracks, spans), padding, gaps, alignment incl. text baselines, Hug/Fill/Fixed sizing, min/max, absolute positioning, constraints, layout grids.
- **Components:** main components, instances with overrides that survive edits, component sets + variant properties, swap/detach/reset, Assets panel with thumbnails + search.
- **Variables:** color/number/string/bool, modes (Light/Dark), aliases, scopes. Bound to fills, strokes, effects, layout grids, instance properties, corner radius, opacity, stroke weight, visibility, text, font props. `explicitVariableModes` per frame.
- **Styles:** paint, text, effect, grid styles; folders; duplicate/rename/move.
- **Vectors:** real vector networks, booleans (union/subtract/intersect/exclude) staying editable, variable-width strokes, corner radius per point, gradients (linear/radial/angular/diamond), image fills with adjustments, blend modes, drop/inner shadows, layer & background blur, noise, texture, glass (refraction/dispersion/frost/light), masks.
- **Type:** in-place editing, mixed styles, auto width/height, line height, tracking, OpenType, variable font axes, RTL/bidi (HarfRust).
- **Prototypes:** connections, triggers, actions, Smart Animate-style transitions, variables with conditionals/expressions, flows, presentation view, device frames.
- **Dev Mode:** ready-for-dev status, annotations (color-coded categories), measurements/redlines saved in file, inspect (layout, auto layout, typography, fills, strokes, effects with style & variable names), code gen (CSS/HTML/React/Vue/Leptos/SwiftUI/Compose/ArkUI/Flutter — components take properties as params, instances are calls), code connect, compare changes (before/after property diffs), asset export list.
- **Accessibility:** AccessKit on desktop (names/roles/values/states), keyboard nav (F6 between panels), reduce-motion, WCAG AA contrast in both themes.
- **Collab:** live sessions (preview) via `figcraft-server` or local network; cursors, selections, chat, spotlight, per-person undo. No accounts (links = access).

**Export surface (matters for Unity):**

| FigCraft capability | Unity mapping |
|---|---|
| `variable.export` → JSON / CSS / Dart | JSON → `ThemeData` ScriptableObject + `DesignTokens.uss` custom props (`--*`). CSS → 1:1 into USS. Dart → ignore. |
| `file.exportLayers` (PNG/SVG/PDF) | Sprite Atlas (PNG). SVG → rasterize to PNG (Vector Graphics optional). |
| `file.exportSvg` | Same. |
| `inspect.code` (CSS/HTML/React/…) | CSS output → translate flex/padding/gap/color into USS. ~80% mechanical (USS subset of CSS). |
| `inspect.properties` | Layout/typography/fills/strokes/effects with style & variable names → importer source. |
| `inspect.assets` | Layers with export settings, icons, images + `kind` → atlas input list. |
| `codeConnect.export/import` | `figcraft.connect.json` → map FigCraft components to Unity prefabs/UXML templates. |
| `export` (any layer, any scale, png/jpg/svg/webp/pdf/xml) | Batch asset export. |
| `screenshot` | Visual diff input (agent uses; user verifies). |

**MCP + CLI:** newline-delimited JSON-RPC 2.0 over stdio, MCP protocol 2025-06-18 (older accepted). 32 MiB line limit. **Headless:** `figcraft-cli mcp` — in-process engine, one untitled doc, CPU renderer (CI/batch). **Attached:** `figcraft-cli mcp --attach <port>` — drives running app via control channel (`figcraft --control <port>`); user sees changes live. App shows agent avatar + outlines of last edit (display only, no undo step). **CLI:** `render`, `export`, `run <script.json>`, `commands --filter`, `mcp`. Headless render/export for CI. **Commands:** 400+ in one registry; `list_commands {filter?}`. Mutations are one undo step; `batch` groups several into one step with `$ref`/`$step` references. `pointer`, `key`, `type_text` drive canvas. `wait_for_changes` for comment events. **File access confinement:** `--automation-read-root` / `--automation-write-root` (headless only; attached app confined by its own flags). Covers all file-touching commands including `variable.export`/`import`, `codeConnect.export/import`, `export`. Network commands refused when confined.

**Limitations (be honest):** alpha; ~49% parity; rough edges in UI/shortcuts. `.fig` import best-effort (some features lost). **No Unity/UXML/USS exporter** — you must write an importer (Python or C#) over CSS/variables/assets. Budget ~1–2 weeks. No shared libraries across files yet → keep one big Design System file. Multiplayer is preview (no accounts, links = access, not E2E encrypted). Plugins are WASM-sandboxed (no fs/network).

**FigCraft → Unity mapping:**

| FigCraft concept | Unity UI Toolkit equivalent | Notes |
|---|---|---|
| Frame (auto layout vertical/horizontal) | `VisualElement` with USS `flex-direction: column/row` | Direct. |
| Frame (grid layout) | USS has no grid (6.0); flatten to nested flex or absolute positioning. Unity 6.7 adds CSS Grid (opt-in preview). | Use margins or `gap` (Unity 6+ USS `gap` for flex). |
| Component | UXML `<Template>` + C# partial class | Code connect maps 1:1. |
| Component set / variants | USS class variants (`.button--primary`) + state classes | Variant props → C# enum. |
| Variable (color, number, string, bool) | USS custom property `--*` + `ThemeData` ScriptableObject | Modes → theme classes. |
| Style (paint/text/effect) | USS class + shared style asset | Styles don't auto-sync; importer generates classes. |
| Vector (icons) | SVG → PNG sprite (or Vector Graphics package) | Bake at 2x/3x. |
| Effects (blur, shadow, glass) | USS `box-shadow`, opacity, backdrop-filter (limited) + custom shaders | UITK has no backdrop-blur; use render texture or custom shader. |
| Text style | USS `font-size`, `color`, `-unity-font-definition` | SDF font asset required. |
| Prototype transitions | Not imported; reimplement in C# (UIFrame + animation) | Design-time only. |

### 4.2 Rive editor & runtime

**Editor capabilities:** design + animation tool for interactive 2D. State machines, data binding, scripting (AnimaScript early access), components, layouts, text, audio. Exports `.riv` consumed by rive-unity. "What you build looks/behaves exactly the same in runtime."

**State machines:** set of animation states + transitions. Indirectly controlled through transitions conditioned on Data Binding properties. Designer assigns default state machine per artboard. Runtime: play/pause/stop, advance by delta time, settle optimization. Access via `artboard.StateMachine()` (default/index/name). Advance in `Update()`.

**Data binding:** View Models (blueprint with properties: Number/String/Boolean/Color/Enum/List/Nested VM). Instances store values. Bind properties to design elements (text, images, colors, animations, state machines). Auto-bind default/selected instance in RiveWidget inspector or code. Global VMs accessible to all artboards. Properties mutable with getters/setters/observers; changes apply after advance.

**Export:** `.riv` file. Drag into Unity Project → auto-creates Rive Asset. Drag into scene hierarchy → creates screen-space setup inside uGUI Canvas (Rive Panel + Rive Texture Renderer).

**Unity runtime (rive-unity):** technical preview for Mac/Windows. Supports Unity LTS 2021+ (incl. Unity 6). Rendering: WebGL, Metal (Mac/iOS), D3D11/OpenGL/Vulkan (Windows/Android), Vulkan (Linux). Features: animation playback, fit/alignment, listeners, state machine inputs, events, text update, out-of-band assets, procedural rendering, PNG/WEBP/JPEG images. Install via Package Manager git dependency or Asset Store. Now also has responsive layouts, reusable components, virtualization-friendly scrolling, data binding, vector+raster assets, custom Rive Renderer.

**Components in Unity:** RiveWidget (primary). Must be under RivePanel. Fields: Asset, Artboard Name, State Machine Name, Hit Test Behaviour, Fit, Alignment, Layout Scale Factor, Layout Scaling Mode, Fallback/Reference DPI, Speed. Auto-bind modes. Public methods Load(File/Asset, artboard, stateMachine). Artboard properties allow swapping components at runtime.

**Usage pattern:** build UI in Rive Editor → export `.riv` → import into Unity → drive from C# (state machine inputs, data binding, events). Use for buttons (hover/press/disabled), HUD (health/cooldowns/minimap), transitions (screen in/out), loading, empty states. Budget per-instance; avoid 100 simultaneous state machines; use shared artboards.

**Limitations:** technical preview (platform support evolving); D3D12 planned. Screen-space uGUI integration (not UITK native). Text perfectly antialiased. Scripting in editor is early access.

### 4.3 Unity UI Builder

**Capabilities:**
- Visual authoring of `.uxml`/`.uss` (drag-and-drop, live preview).
- UXML instances as templates (`<Template>`/`<Instance>`).
- USS variables management (assign/remove custom props) — Unity 6+.
- Optional Vector Graphics package (`VectorImage` background), 2D Sprite package (sprite as background, open Sprite Editor from Inspector).
- Test UI inside Builder (Preview mode: state-based controls, input validation, ScrollView, `:hover`).
- Style debugging (Matching Selectors, UI Toolkit Debugger).
- Git-diffable output (plain XML/CSS).
- Hot reload of UXML/USS at runtime (with limitations).
- Unity 6.4+: drag-and-drop UXML/USS into Builder; create UI Document from Hierarchy; sub-asset references in USS (`resource(path#sub-asset-name)`); project-wide default theme.
- Unity 6.5+: PanelRenderer (successor to UIDocument), USS Stats profiler.
- Unity 6.6+: UXML upgrade service, runtime VisualElement references from MonoBehaviour fields.
- Unity 6.7 (opt-in, preview): in-scene UI authoring & UI Viewport; keyframed animation for VisualElement properties in Animation window; `[VisualElementComponent]` structs; CSS Grid layout (opt-in); accessibility hierarchies for runtime panels; curved UI (`-unity-curvature`); Font Subsetter; StyleSheet Editing Mode (standalone .uss/.tss in Builder); multiple Builder windows; Shader Graph target for UITK filters.

**Limitations (the ones that bite):**
- **No design-system parity with Figma:** no components with overrides, no variants, no auto layout beyond USS flex (authoring manual), no vector editing, no effects authoring (shadows/blur are CSS-ish).
- **No token import:** USS variables must be typed by hand or generated by importer. Builder can consume but not import from JSON.
- **No animation authoring (6.0–6.4):** Builder is static. Animation in C#/DOTween/Rive. (6.7 adds keyframed animation.)
- **Complex UI = lots of manual USS:** nested flex, responsive breakpoints, state classes. Builder helps but doesn't remove code.
- **World-space UI:** UITK world-space (PanelSettings renderMode = WorldSpace) maturing; PanelRenderer (6.5) improves; uGUI remains safer for 3D-attached UI until 6.x matures.
- **No data binding authoring (6.0):** runtime bindings (Unity 6) configured in code, not Builder. (6.6+ improves references.)
- **Discoverability gaps:** some features text-only (images, TwoPaneSplitView) not in components library; switching Builder ↔ text is rough.
- **USS is not CSS:** no `linear-gradient()` (bake to texture or Painter2D), no arcs/rings/circles (Painter2D), no `clip-path`/masks (bake into alpha), no `@keyframes` (use `transition` or class swapping), no `:last-child`/`:first-child`/`:nth-child` (fails quietly, logged as unknown pseudo class), no grid (6.0). Positional selectors fail silently in player.

**How we use it:**
- **Primary authoring surface** for all `*.uxml`/`*.uss` in `Assets/_Project/UI/`. FigCraft output is *input*; UI Builder is where it becomes engine-real.
- Review imported tokens/assets in Builder; tweak USS; preview across aspect ratios (Preview mode).
- Keep Builder-generated files as single source of truth in git (not design file).
- Use UI Toolkit Debugger + Matching Selectors for style provenance.

### 4.4 Figma Free (Starter)

**Limits (2026):**
- 3 Figma Design/Sites files total in team folder (3 FigJam, 3 Slides); unlimited drafts.
- No team libraries; no Dev Mode; no modes sharing across files (modes per-file); 30-day version history; coarse permissions (only view access on drafts); 1 folder for organization; no AI image tools; no SSO/advanced security.
- Variables only, no modes (paid has 10 modes).
- MCP: 20 tool calls/month (read tools); REST API Tier 1 endpoints (file reads/exports) also 20/month for View/Collab seats; Tier 2 (comments/versions/components/styles) 5–10/min.
- AI credits: 150/day, up to 500/month.

**Workarounds:**
- Split into 3 mega-files: Design System (tokens/components), Screens A, Screens B. Use drafts for scratch.
- Keep components in Design System file; copy/paste into screen files; document in `docs/UI/`.
- Modes live per-file; export variables per file; importer merges by name.
- Git-style: export `.fig` snapshots to repo (`docs/UI/snapshots/`) on milestones.
- Solo dev → permissions not an issue.
- For agent reads on free plan: use figmingo-mcp (local-first, PAT + REST, no monthly caps) or Composio (separate budget). Tell agent to batch node IDs, use shallow depth, reuse cached data.
- Viewers unlimited (view/comment/inspect/export) — good for hand-off.
- Consider Figma Education if eligible (free Professional for 2 years).

**Recommendation:** Fallback only. Use when contractor delivers `.fig` or FigCraft can't open a file.

### 4.5 Penpot

- Open source (Clojure), self-hosted or cloud. Full ownership of design infra; open standards (SVG/CSS/HTML/JSON).
- **Real CSS Flexbox + Grid** (design as code; reduces translation loss to USS). CSS Grid + Flex Layout with fr/auto units, named grid areas, z-index.
- Design tokens (W3C DTCG format) with references/aliases; themes (light/dark, multi-brand).
- Components & variants; libraries shared across team.
- Inspect tab → CSS/HTML/SVG code (copy-paste).
- MCP server (local/remote) for AI workflows: read/write design, generate HTML/CSS, export assets, map components.
- Export: SVG/PNG/PDF + tokens (penpot-export npm → CSS/SCSS/JSON). No Unity path; write UXML/USS from CSS.
- Plugins (open API). Webhooks/access tokens.

**Use when:** need self-hosting (on-prem/compliance), or want designers to author in real CSS Flexbox/Grid (reduces translation loss). MCP exists. Free, open source.

### 4.6 Lunacy

- Free native desktop (Win/Mac/Linux), proprietary (Icons8 EULA). Opens `.fig`/`.sketch`. Real-time collaboration, cloud + offline, 1.5M+ built-in assets, AI tools.
- Auto layout, components, variables, prototyping, shared styles.
- Export: PNG/JPG/ICO/WEBP/TIFF/BMP/SVG/PDF + CSS/XAML/JSON/SVG code preview. Linked Design (live webpage updates from Lunacy).
- 16,000 px max raster dimension. No plugins (third-party integrations not planned). `.fig` import via file (API import deprecated).

**Use when:** contractor sends `.fig`/`.sketch` and you want native desktop editor without Figma account. Free. No Unity path; export PNG/SVG/CSS.

**Neither Penpot nor Lunacy** has variables→USS or component→UXML export. Same importer work as FigCraft, but FigCraft has better MCP/CLI + Dev Mode code gen → prefer FigCraft.

---

## 5. Pipeline design

### 5.1 End-to-end pipeline (FigCraft/Rive → Unity → test)

```
[1 Design]  FigCraft (components, variants, variables/modes, vectors, effects)
            + Rive editor (animated/interactive artboards, state machines, data binding)
       │
       ▼  MCP/CLI (headless or attached)
[2 Export]  variable.export(JSON+CSS)  +  export(PNG/SVG @2x)  +  inspect.code(CSS)
            +  inspect.properties/assets  +  codeConnect.export
            +  Rive: export .riv
       │
       ▼  importer (Python/C#)
[3 Tokens]  DesignTokens.uss  +  ThemeData (ScriptableObject)  +  Sprite Atlas
       │
       ▼  importer
[4 Structure]  *.uxml (from codeConnect + inspect.code template)  +  *.uss (classes)
       │
       ▼  Unity UI Builder (review/tweak/preview across aspect ratios)
[5 Wire]   C# ViewModels + UIFrame + runtime bindings (Unity 6)
            + Rive runtime (state machines + data binding) + Lottie (micro) + VFX Graph (juice)
       │
       ▼  Test by user (Play Mode, visual diff vs FigCraft screenshot, perf guardrails)
[6 Iterate]  change in FigCraft/Rive → re-export → importer → diff → review → commit
```

### 5.2 Artifacts & ownership

| Artifact | Owner | Location |
|---|---|---|
| Design file (`.figcraft`) / Rive (`.riv`) | Designer/agent | `docs/UI/design/` (or external) |
| Exported tokens JSON/CSS | Importer output | `Assets/_Project/UI/Tokens/` |
| `DesignTokens.uss` | Importer + manual | `Assets/_Project/UI/Styles/` |
| `ThemeData.asset` | Importer + manual | `Assets/_Project/UI/Themes/` |
| Sprite atlas (PNG/SVG) | Importer | `Assets/_Project/UI/Sprites/` |
| `*.uxml` / `*.uss` | UI Builder (reviewed) | `Assets/_Project/UI/Screens/` |
| `figcraft.connect.json` | Importer | `Assets/_Project/UI/Connect/` |
| ViewModels / UIFrame | Programmer | `Assets/_Project/UI/Scripts/` |
| `.riv` + wrappers | Designer/programmer | `Assets/_Project/UI/Rive/` |

### 5.3 Automation & CI

- **Headless export:** `figcraft-cli mcp` (or `figcraft-cli run build.json`) in CI on design-file change → exports tokens + assets + CSS into staging folder.
- **Importer:** runs in CI; produces diff; opens PR; human reviews UXML/USS diff (not pixels).
- **Visual regression:** render FigCraft frame (`screenshot`/`export`) and Unity frame (screenshot by user or render texture) → image diff (optional, user-driven since agent can't screenshot Play Mode).
- **Guardrails:** perf budgets checked in CI (draw calls, GC).
- **Drift detection:** importer computes hash of exported tokens; CI fails if Unity's `DesignTokens.uss` doesn't match. Forces re-import.
- **Modes/themes:** FigCraft variable modes → USS classes (`:root` default, `[data-theme="dark"]`) + `ThemeData` switching in C#. Rive state machines read theme via data binding.

### 5.4 Multi-theme (Light/Dark) flow

1. Define variable collection "Theme" with modes Light/Dark in FigCraft.
2. `variable.export {format: "css"}` → `:root { --surface: #fff; … }` and `[data-theme="dark"] { --surface: #111; … }`.
3. Importer splits into `DesignTokens.uss` with both blocks.
4. C# `ThemeManager` toggles `data-theme` attribute on root VisualElement + swaps `ThemeData` ScriptableObject (for non-CSS values like fonts/spacing overrides).
5. Rive state machines read theme via data binding (Rive supports runtime data binding).

### 5.5 Single source of truth

- **Tokens:** FigCraft variables. Unity consumes; never edit tokens in Unity by hand (importer overwrites). Manual USS tweaks go in *component* styles, not token files.
- **Structure:** UXML in git (after Builder review). FigCraft is design input, not runtime.
- **Rive:** `.riv` is the source for animated UI; C# binds data; don't hand-edit exported Rive assets.

---

## 6. AAA visuals: how to make UI "alive"

### 6.1 Visual techniques (FigCraft + UITK + shaders)

- **Glass / frosted panels:** FigCraft glass effect (refraction/dispersion/frost/light) as design reference; in Unity use backdrop-blur via render texture or custom URP shader (glassmorphism). UITK has no native backdrop-filter.
- **Layered shadows:** multiple drop shadows with spread → USS `box-shadow` (Unity 6 supports multiple) or custom.
- **Gradients:** linear/radial/angular/diamond → USS `linear-gradient()` (Unity 6 supports radial too); angular/diamond need custom shader or texture.
- **Blur:** layer blur → USS `filter: blur()` (Unity 6) or post-process; background blur → render texture.
- **Vector marks/icons:** FigCraft vector networks with booleans → SVG → sprite atlas at 2x/3x (crisp, tintable via `-unity-background-image-tint-color`).
- **Noise/texture overlays:** FigCraft noise → PNG texture tiled with opacity.
- **SDF text:** TextMeshPro SDF for crisp text at any scale; UITK text uses font assets (SDF improving in 6.x).
- **Color modes:** Display P3 in FigCraft; Unity uses sRGB by default — convert at export. Keep sRGB for game UI unless targeting HDR.
- **Pixel perfection:** align to device pixel grid; integer positions; test at 1080p/1440p/4K.
- **Procedural UI:** SDF-based shapes (Better Rectangles / custom Shader Graph) for infinitely scalable, pixel-perfect frames without sprite slicing.
- **Curved UI (6.7 preview):** `-unity-curvature` for world-space panels (VR/futuristic HUDs).

### 6.2 Animation stack

- **Rive (primary):** state machines + data binding + scripting. Use for buttons (hover/press/disabled), HUD (health, cooldowns, minimap), transitions (screen in/out), loading, empty states. Runtime: `rive-unity` (open source, no royalties). Designers author in Rive editor; devs bind data via C#.
- **Lottie (secondary):** one-shot micro-animations (success tick, spinner, notification badge). Render to Texture2D via `com.gilzoide.lottie-player` or rlottie. Cheaper than Rive for non-interactive.
- **Spine (tertiary):** skeletal 2D for mascots/animated portraits/character UI. Not for chrome.
- **VFX Graph (juice):** confetti, energy pulses, glows, hit flashes. Render to render texture for UI overlay, or use UITK custom shaders. Particle systems for screen-space feedback (UIParticle System).
- **UITK transforms:** `translate/scale/rotate` with `DynamicTransform`/`GroupTransform` usage hints for cheap animated elements (progress bars, sliders).
- **DOTween:** code-driven tweens for simple fades/slides when Rive is overkill.
- **Keyframed animation (6.7 preview):** author keyframed animations for VisualElement properties directly in Animation window; animate UI and GameObjects together in one clip.
- **Motion blur (UIFX):** smoother animated UI; per-vertex or transform-level.
- **Blur transitions (UIFX Blur Filter):** focus effects, appear/disappear.

### 6.3 Adaptive / responsive

- **Flexbox-first:** UITK layout is flex-based; design in FigCraft with auto layout to match. Use `flex-grow`, `flex-shrink`, min/max, `gap` (Unity 6+).
- **Breakpoints:** USS media queries (`@media`) for aspect ratios; or C#-driven class toggles. Test at 16:9, 21:9, 4:3, mobile.
- **Density-aware scaling:** per-device scaling, player UI-Scale slider (Luna UI pattern). Use `PanelSettings` scale + USS `font-size` in `rem`-like units (UITK uses px; emulate via variables).
- **Safe areas:** handle notches/rounded corners via USS padding driven by `Screen.safeArea`.
- **World-space (6.7 preview):** curved UI, PanelRenderer for simpler world-space workflow.
- **Floating Origin:** UI anchored to screen (not world) is immune; world-space UI must follow rebase pipeline.

### 6.4 Blur / glow / particles (the "alive" layer)

- **Backdrop blur (glassmorphism):** render UI background to RenderTexture at lower res → blur pass (URP renderer feature or post-process) → sample in UITK via `background-image` or custom shader. Cost: measure; use only on focal panels.
- **Glow:** Shader Graph radial mask + HDR color + bloom post-process; or VFX Graph glow sprites. Animated via USS `transition` or Rive.
- **Particles in UI:** UIParticle System (uGUI) or VFX Graph → RenderTexture → UITK `background-image`. Pool instances; avoid per-frame mesh rebuilds.
- **Shadows:** layered `box-shadow` for depth; animated shadow for lift on hover.
- **Noise/texture:** tiled PNG with opacity for tactile feel.
- **Micro-interactions:** button press scale 0.96, hover glow, focus ring, ripple on click. Rive state machines or UITK `transition` + C# class toggles.
- **Text effects:** per-character animations (wave/bounce/shake/rainbow) via rich-text tags (Luna UI pattern) or TextMeshPro animators.
- **Feedback loops:** health bar flash on damage, cooldown pulse when ready, tab punch on switch.

### 6.5 Performance budgets (ties to `06_PERFORMANCE_GUARDRAILS.md`)

- ≤4 draw calls for chrome; ≤1 ms UI CPU/frame; 10k+ items via ListView virtualization.
- Rive: budget per-instance; avoid 100 simultaneous state machines; use shared artboards.
- VFX Graph in UI: render to texture at lower res; limit particle counts; avoid per-frame mesh rebuilds.
- Use `DisplayStyle.None` (not opacity:0) to skip layout+render for hidden UI.
- Pre-allocate vertex buffers; mind Vertex Budget in PanelSettings.
- Dirty-tracking: only repaint changed subtrees (UITK does this; don't force full repaints).
- USS selectors: audit with USS Stats profiler (6.5+); specificity as `id-class-type`.
- Avoid `:last-child` etc. (dead rules + log spam).
- Glass/backdrop-blur: measure; use lower-res render textures.

---

## 7. Reverse engineering: how AAA studios build UI environments

### 7.1 Sybo (Subway Surfers) — GDC 2019 (Natalia Rebrova)
- Problem: multiple Unity projects with inconsistent UI and duplicated work.
- Solution: **unified cross-project UI framework** — shared component library, common theming, reusable screens/widgets, consistent interaction patterns.
- Takeaway: extract a shared UI framework package (UXML/USS + C#) early so future projects/modes reuse it. Don't let each screen reinvent panels/buttons.
- Also: SYBO uses Unity Project Auditor + Memory Profiler + Addressables for performance; UI is part of perf budget, not afterthought.

### 7.2 Bungie (Destiny / Marathon) — Tiger Engine (custom)
- Custom engine, not Unity. UI built in-engine with proprietary tooling; art hand-off via internal pipelines.
- Takeaway: **not directly applicable** (we're on Unity), but principle: UI is first-class system with own framework, data binding, perf budgets. Destiny's UI is heavily data-driven (inventory/stats/social) — validates our MVVM/ViewModel approach. UI density validates virtualization + dirty-tracking.

### 7.3 Riot Games (LoL, Valorant, 2XKO) — ProtoPie + "Play" design system
- **"Play" design system:** repository of predefined components (animations, sounds, transitions) for operational efficiency and consistency across games. Developers access via library.
- **ProtoPie** for interaction prototyping: replaced Figma→AEUX→After Effects (slow renders, no interactivity, bulky MP4 hand-offs) with interactive prototypes shared via links.
- **Lottie** for micro-animations: frame-by-frame in AE → Bodymovin JSON → integrated; precise control over micro-anim.
- **Multi-device integration:** ProtoPie Connect links gamepads/phones/desktops for consistent UX.
- **Hand-off:** component name matching between Figma and ProtoPie keeps prototypes in sync with visual updates.
- Takeaway: **build a "Play"-style component/animation library** (Rive artboards + sound + transitions) shared across screens; use interactive prototypes (Rive/ProtoPie) instead of static MP4 for hand-off; Lottie for micro-anim.

### 7.4 League of Legends client — web-based UI (CEF)
- Reverse engineering shows LoL client hosts a local webserver; UI rendered by Chrome Embedded Framework (CEF). Frontend is web (HTML/CSS/JS), backend API local. Modification/customization familiar to web devs.
- Takeaway: **web-tech UI in games is viable and powerful** (validates Unity UI Toolkit's web-inspired model). Data-driven, hot-reloadable, component-based. But requires perf discipline (CEF overhead); Unity UITK gives similar dev UX with native perf.

### 7.5 Naughty Dog — OpenGOAL / Decima (Zachary Lewis, Days Gone)
- Embedded UI engineer on Decima (Guerrilla) via Naughty Dog collaboration; reverse-engineered proprietary engine; built tools/workflows in Maya.
- OpenGOAL: custom DSL with SID-based symbol binding, compiler/decompiler; data-driven pipelines replacing heavy native code.
- Takeaway: **data-driven UI pipelines** (declarative data → runtime) and **custom tooling** embedded in engine. For us: keep UI data (UXML/USS/tokens) declarative and engine-agnostic where possible; build importer/tooling rather than hardcoding.

### 7.6 Halo (Ian Jones) — unified asset pipeline
- Unified 5 different geometry/animation pipelines into one; 100k+ assets delivered; 3x faster iteration; single source of truth in Alembic files (DCC-agnostic).
- Takeaway: **single source of truth + DCC-agnostic workflows**. For UI: design file (FigCraft/Rive) is source; importer is the single bridge; engine consumes. Don't fork truth between design and engine.

### 7.7 Bungie Oni (Bungie West) — tool mode
- In-game visual editor (Tool Mode) stored in `level0_Tools.dat`; labels helped reverse-engineer binary formats. Tool mode not in release builds.
- Takeaway: **in-editor authoring tools** (like Unity UI Builder) are the modern equivalent; ship design-time tooling, not runtime editors.

### 7.8 Common AAA pattern (synthesis)
1. **Design system as product:** tokens + components + motion + sound, versioned, shared.
2. **Data-driven UI:** ViewModels, bindings, not hardcoded values.
3. **Virtualization** for lists (10k+ items).
4. **Dirty tracking + batching** (repaint only what changed).
5. **Interactive prototypes** for hand-off (not static).
6. **Perf budgets** enforced in CI.
7. **Separation:** design tool = intent; engine = truth. Importer bridges.
8. **Single source of truth** for assets/data; DCC-agnostic where possible.
9. **Custom tooling** (importers, profilers) is a first-class investment.

---

## 8. Risks & mitigations

| Risk | Mitigation |
|---|---|
| FigCraft alpha instability / breaking changes | Pin a release; keep Figma Free fallback; importer versioned against FigCraft's export schema. |
| `.fig` import loss | Test interop on real files early; fall back to Figma Free for contractor files. |
| No Unity exporter → manual importer | Budget 1–2 weeks; start with tokens+assets, defer structure import. |
| Token drift between design & engine | Hash-based CI check; importer is the only writer of token files. |
| Rive runtime perf on low-end | Budget per screen; profile; fall back to static + DOTween. |
| Glass/backdrop-blur cost | Use render textures at lower res; measure. |
| UITK world-space immaturity | Keep uGUI for 3D-attached UI until 6.x matures (PanelRenderer 6.5+). |
| USS != CSS surprises | Keep a "USS limitations" checklist; test in player (not just editor); audit logs for unknown pseudo classes. |
| Free-plan MCP caps (Figma) | Use FigCraft (no caps) as primary; figmingo-mcp/Composio for Figma reads. |
| Agent can't Play Mode/screenshot | User verifies; provide clear manual test steps; use FigCraft screenshots for design-side visual. |

---

## 9. Sources

- FigCraft repo & docs: https://github.com/storytold/figcraft (README, docs/mcp.md, docs/cli.md, docs/control-protocol.md) — MCP 400+ commands, headless/attached, variables export (JSON/CSS/Dart), codeConnect, inspect.code/properties/assets, export, screenshot, confinement.
- FigCraft guide: https://pasqualepillitteri.it/en/news/23599/figcraft-guide-free-figma-alternative-rust
- FigCraft MCP: https://figcraft.run/mcp ; https://figcraft.rerivo.com/
- Rive Unity getting started: https://rive.app/docs/game-runtimes/unity/getting-started
- Rive state machines: https://rive.app/docs/game-runtimes/unity/state-machines
- Rive data binding: https://rive.app/docs/game-runtimes/unity/data-binding
- Rive components: https://rive.app/docs/game-runtimes/unity/components
- Rive View Models: https://rive.app/docs/editor/data-binding/view-models
- rive-unity repo: https://github.com/rive-app/rive-unity
- Rive Unity Asset Store update: https://rive.app/blog/rive-for-unity-updated-and-in-the-unity-asset-store
- Unity UI Builder manual: https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uibuilder/uib-getting-started
- Unity UI Toolkit status Nov 2025: https://discussions.unity.com/t/ui-toolkit-development-status-and-next-milestones-november-2025/1698009
- Unity 6 UI Toolkit updates: https://unity.com/blog/unity-6-ui-toolkit-updates
- State of UI Toolkit in Unity 6.7: https://discussions.unity.com/t/state-of-ui-toolkit-in-unity-6-7/1736756
- USS limitations snapshot: https://pcworkman.dev/guides/unity-ui-toolkit-uss-limitations/
- Figma Starter plan: https://help.figma.com/hc/en-us/articles/13838684089751-Starter-plan-overview
- Figma pricing: https://www.figma.com/pricing ; https://www.figma.com/pricing-faq/
- Figma free plan 2026: https://dupple.com/learn/is-figma-free
- Figma MCP free limits: https://composio.dev/content/figma-mcp-alternative-how-to-avoid-hitting-rate-limits
- figmingo-mcp: https://github.com/wenym8/figmingo-mcp
- Inspect Figma without Dev Mode: https://peezy.cc/guides/inspect-figma-without-dev-mode
- Penpot: https://penpot.app ; MCP: https://help.penpot.app/mcp/ ; code: https://penpot.app/code ; declarative design: https://penpot.app/blog/declarative-design-with-penpot/ ; export: https://github.com/penpot/penpot-export
- Lunacy: https://icons8.com/lunacy ; docs: https://lunacy.docs.icons8.com/ ; export: https://lunacy.docs.icons8.com/export/ ; linked design: https://lunacy.docs.icons8.com/linked_design/
- ProtoPie × Riot: https://www.protopie.io/blog/revolutionizing-game-ux-ui-design-riot-games
- Sybo GDC 2019: https://media.gdcvault.com/gdc2019/presentations/rebrova-natalia-building-unified-cross-project.pdf
- Zach Bohn UI pipeline: https://zachgamedev.com/2023/08/19/ui-design-101-the-ui-pipeline/
- Zachary Lewis (Days Gone/Decima): https://zacharylew.is/resume/
- Bungie/Tiger Engine: https://bungie.fandom.com/wiki/Tiger_Engine
- Destiny 2 UI format (reverse eng): https://projectsunrise.dev/docs/destiny-2/
- LoL client reverse eng (CEF): https://nickcano.com/reversing-league-of-legends-client/
- Naughty Dog OpenGOAL (dconstruct): https://damiangrossklos.com/
- Halo unified pipeline (Ian Jones): https://www.linkedin.com/in/ianjonestd
- Oni level0_Tools (Bungie West): https://wiki.oni2.net/Level0_Tools
- Luna UI (Unity 6 UITK framework): https://www.docs.cupkek.games/
- Animated UI FX: https://assetstore.unity.com/packages/2d/gui/animated-ui-fx-380348
- UIFX Blur/Motion Blur: https://www.chocdino.com/products/legacy/uifx-1.x/blur-filter/about/
- UIParticle System: https://discussions.unity.com/t/release-uiparticle-system-bring-particle-effects-to-your-ui/1693832
- Better Rectangles (procedural UI): https://assetstore.unity.com/packages/tools/gui/better-rectangles-procedural-ui-370610
- All Frame FX In One: https://assetstore.unity.com/packages/2d/gui/all-frame-fx-in-one-294788
- Project C internal: `docs/UI/Impruvements_research/02_*.md`, `03_*.md`, `05_*.md`, `06_*.md`, `07_*.md`, `10_*.md`, `11_*.md`, `12_*.md`, `docs/UI/UI_TOOLKIT_GUIDE.md`

---

*End of report. Next: implement Phase 0 (UI Builder primary + Design System in UI Builder) and build the FigCraft importer (1–2 weeks) per `04_IMPLEMENTATION_PLAN.md`. Agent does not start Play Mode; user verifies.*
