# 10 — UI Tooling & Pipeline (Design → Engine)

> **Scope:** Tooling research for Project C (Unity 6.000.5.2f1, URP, Windows standalone, MMO-sandbox). Builds on `07_DESIGN_TOOLING_AND_FIGMA_FREE.md` (Figma Free vs alternatives) and `03_AAA_UI_ARCHITECTURE_BLUEPRINT.md`. Focus: how to go from a design file to a living, AAA-grade UI in Unity without a manual hand-off hell.
>
> **Verdict up front:** FigCraft (open source, Rust, alpha ~49%) is the best *free* Figma-class design tool for an agent-driven pipeline, but it has no Unity exporter — you build a thin importer over its CSS/variables/assets. Unity UI Builder stays the primary *authoring* surface for UXML/USS. Rive is the animation runtime of choice for interactive UI. Figma Free remains a fallback for `.fig` interop. Penpot/Lunacy are fallbacks. Do not build a custom design tool.

---

## 0. TL;DR

| Decision | Choice | Why |
|---|---|---|
| Design tool (primary) | **FigCraft** (alpha) | Free, offline, MCP+CLI+Dev Mode, variables, components, vectors, effects; agent-drivable; no account. |
| Design tool (fallback) | **Figma Free + drafts** | `.fig` interop, Team Library absent; 3-file limit. |
| Design tool (fallback 2) | **Penpot** (self-hosted) / **Lunacy** | Penpot = open-source + real CSS Flexbox/Grid; Lunacy = native desktop, opens `.fig`. |
| UI authoring in engine | **Unity UI Builder** + hand-edited UXML/USS | Only tool that writes the actual `*.uxml`/`*.uss` the runtime uses; git-diffable. |
| Animation runtime | **Rive** (rive-unity) for interactive UI; **Lottie** for linear micro-anim; **Spine** for skeletal 2D; **VFX Graph** for particle/glow. | Rive = state machines + data binding + open runtime; others are narrower. |
| Tokens | FigCraft `variable.export` → `DesignTokens.uss` + `ThemeData` ScriptableObject | CSS custom props map 1:1 to USS custom props. |
| Hand-off | FigCraft MCP/CLI → importer → UXML/USS → UI Builder review → Play Mode test (by user) | No manual pixel-copying. |

---

## 1. Tooling landscape (one paragraph each)

- **FigCraft** — open-source (MIT/Apache-2.0), clean-room Rust reimplementation of the Figma-class editor. Native (Win/Mac/Linux/FreeBSD/Web, no Electron), offline, no account/telemetry. ~49% parity, early alpha. Frames, auto layout (flex + grid, hug/fill/fixed, wrap, gaps, padding, baseline), components & variants, vector networks (booleans, variable-width strokes), styles, variables (color/number/string/bool, modes, aliases, scopes), prototypes, Dev Mode (inspect, code gen, measurements, ready-for-dev, annotations, code connect), accessibility (screen reader, WCAG AA), i18n (14+ langs), WebAssembly plugins. **Agent-ready: 400+ commands, one registry, CLI, JSON control channel, MCP server (headless or attached to a running app).** Exports PNG/JPG/WebP/SVG/PDF, `.fig` (best effort), `.figcraft` (own format), `.json` (REST API file JSON with variables), CSS/HTML/React/Vue/Leptos/SwiftUI/Compose/ArkUI/Flutter code.
- **Figma Free (Starter)** — 1 file, 3 design files total, unlimited drafts, no Team Library, no Dev Mode/MCP, 30-day version history, coarse permissions. Enough for a solo dev if you split into 3 files; not enough for a shared design system with modes across files.
- **Penpot** — open source, self-hosted, Clojure. Real CSS Flexbox + Grid (designers and devs share the layout model). SVG/CSS/HTML/JSON export, design tokens, components/variants, MCP. No native Unity path; you export SVG/PNG + write UXML/USS. Best when you need self-hosting or want CSS-parity layout authoring.
- **Lunacy** (Icons8) — free native desktop (Win/Mac/Linux), opens `.fig`/`.sketch`, exports PNG/SVG/PDF/CSS, built-in assets. Good as a `.fig` viewer/editor when you don't want Figma; no Unity path.
- **ProtoPie** — high-fidelity interaction prototyping (Riot Games uses it). Not a design authoring tool; use for interaction specs, not pixels.
- **Rive editor** — design + animation tool for interactive 2D. State machines, data binding, scripting. Exports `.riv` consumed by rive-unity. This is where "living" UI lives.
- **Lottie / After Effects + Bodymovin** — linear micro-animations as JSON. Good for one-shot icons, spinners, success ticks.
- **Spine** — 2D skeletal animation. Good for character/creature UI (mascots, animated portraits), less for chrome.
- **VFX Graph** — GPU particle systems. Use for screen-space juice (confetti, energy, glows), not layout.

---

## 2. FigCraft deep dive

### 2.1 Status & honesty
- **Alpha, ~49% parity** (their own estimate). Core editor works end-to-end. `.fig` import is best-effort. No real-world design-doc validation yet. Expect rough edges. Treat it as *primary for new work*, keep Figma Free as fallback for `.fig` interop.
- Clean-room (no proprietary code/formats/icons). Dual license MIT/Apache-2.0. No telemetry, no account.

### 2.2 Capabilities relevant to game UI
- **Layout:** auto layout both directions, wrap, grid (fixed/hug/fr tracks, spans), padding, gaps, alignment incl. text baselines, Hug/Fill/Fixed sizing, min/max, absolute positioning, constraints, layout grids.
- **Components:** main components, instances with overrides that survive edits, component sets + variant properties, swap/detach/reset, Assets panel with thumbnails + search.
- **Variables:** color/number/string/bool, modes (Light/Dark), aliases, scopes. Bound to fills, strokes, effects, layout grids, instance properties, corner radius, opacity, stroke weight, visibility, text, font props. `explicitVariableModes` per frame.
- **Styles:** paint, text, effect, grid styles; folders; duplicate/rename/move.
- **Vectors:** real vector networks, booleans (union/subtract/intersect/exclude) that stay editable, variable-width strokes, corner radius per point, gradients (linear/radial/angular/diamond), image fills with adjustments, blend modes, drop/inner shadows, layer & background blur, noise, texture, glass (refraction/dispersion/frost/light), masks.
- **Type:** in-place editing, mixed styles, auto width/height, line height, tracking, OpenType, variable font axes, RTL/bidi (HarfRust), text styles linked to chars.
- **Prototypes:** connections, triggers, actions, Smart Animate-style transitions, variables with conditionals/expressions, flows, presentation view, device frames.
- **Dev Mode:** ready-for-dev status, annotations (color-coded categories), measurements/redlines saved in file, inspect (layout, auto layout, typography, fills, strokes, effects with style & variable names), code gen (CSS/HTML/React/Vue/Leptos/SwiftUI/Compose/ArkUI/Flutter — components take properties as params, instances are calls), code connect, compare changes (before/after property diffs), asset export list.
- **Accessibility:** AccessKit on desktop (names/roles/values/states), keyboard nav (F6 between panels), reduce-motion, WCAG AA contrast in both themes.
- **Collab:** live sessions (preview) via `figcraft-server` or local network; cursors, selections, chat, spotlight, per-person undo. No accounts (links = access).

### 2.3 Export surface (the parts that matter for Unity)

| FigCraft capability | Unity mapping |
|---|---|
| `variable.export` → JSON / CSS / Dart | JSON → `ThemeData` ScriptableObject + `DesignTokens.uss` custom props (`--*`). CSS → 1:1 into USS. Dart → Flutter only (ignore). |
| `file.exportLayers` (PNG/SVG/PDF) | Sprite Atlas (PNG). SVG → rasterize to PNG (Unity Vector Graphics optional). |
| `file.exportSvg` | Same. |
| `inspect.code` (CSS/HTML/React/…) | CSS output → translate flex/padding/gap/color into USS. Not 1:1 (USS subset of CSS), but ~80% mechanical. |
| `inspect.properties` | Layout/typography/fills/strokes/effects with style & variable names → source data for importer. |
| `inspect.assets` | Layers with export settings, icons, images + `kind` → atlas input list. |
| `codeConnect.export/import` | `figcraft.connect.json` → map FigCraft components to Unity prefabs/UXML templates. |
| `export` (any layer, any scale, png/jpg/svg/webp/pdf/xml) | Batch asset export. |
| `screenshot` | Visual diff input (agent uses; user verifies). |

### 2.4 MCP + CLI (agent integration)
- **Transport:** newline-delimited JSON-RPC 2.0 over stdio, MCP protocol 2025-06-18 (older accepted). 32 MiB line limit.
- **Headless:** `figcraft-cli mcp` — in-process engine, one untitled doc, CPU renderer. Good for CI/batch.
- **Attached:** `figcraft-cli mcp --attach <port>` — drives a running app via its control channel (`figcraft --control <port>`); user sees changes live. App shows an agent avatar + outlines of last edit (display only, no undo step).
- **CLI:** `render`, `export`, `run <script.json>`, `commands --filter`, `mcp`. Headless render/export for CI.
- **File access confinement:** `--automation-read-root` / `--automation-write-root` (headless only; attached app is confined by its own flags). Covers all file-touching commands including `variable.export`/`import`, `codeConnect.export/import`, `export`. Network commands refused when confined.
- **Commands:** 400+ in one registry; `list_commands {filter?}`. Mutations are one undo step; `batch` groups several into one step with `$ref`/`$step` references. `pointer`, `key`, `type_text` drive the canvas. `wait_for_changes` for comment events.
- **Fonts:** headless uses built-in + system fonts; `--no-system-fonts` for reproducible CI renders.

### 2.5 How to use FigCraft for Unity UI (the mapping)

| FigCraft concept | Unity UI Toolkit equivalent | Notes |
|---|---|---|
| Frame (auto layout vertical/horizontal) | `VisualElement` with USS `flex-direction: column/row` | Direct. |
| Frame (grid layout) | USS has no grid; flatten to nested flex or use absolute positioning | Gap: use margins or `gap` (Unity 6+ USS `gap` supported for flex). |
| Component | UXML `<Template>` / prefab-equivalent + C# partial class | Code connect maps 1:1. |
| Component set / variants | USS class variants (`.button--primary`) + state classes | Variant props → C# enum. |
| Variable (color, number, string, bool) | USS custom property `--*` + `ThemeData` ScriptableObject | Modes → theme classes (`:root` / `[data-theme="dark"]`). |
| Style (paint/text/effect) | USS class + shared style asset | Styles don't auto-sync; importer generates classes. |
| Vector (icons) | SVG → PNG sprite (or Vector Graphics package) | Bake at 2x/3x. |
| Effects (blur, shadow, glass) | USS `box-shadow`, opacity, backdrop-filter (limited) + custom shaders | UITK has no backdrop-blur; use render texture or custom shader for glass. |
| Text style | USS `font-size`, `color`, `-unity-font-definition` | SDF font asset required. |
| Prototype transitions | Not imported; reimplement in C# (UIFrame + animation) | Design-time only. |

### 2.6 Limitations (be honest)
- Alpha; ~49% parity; rough edges in UI/shortcuts.
- `.fig` import best-effort (some features lost).
- **No Unity/UXML/USS exporter.** You must write an importer (Python or C#) over CSS/variables/assets. Budget ~1–2 weeks.
- No shared libraries across files (like Figma Team Library) yet → copy/paste components between files, or keep one big Design System file.
- Multiplayer is preview (no accounts, links = access, not E2E encrypted).
- Plugins are WASM-sandboxed (no fs/network) — fine for design, not for engine.

---

## 3. Figma Free — limitations & workarounds

(See `07_*.md` §1 for full table.)

| Limitation | Workaround |
|---|---|
| 3 design files total | Split into 3 mega-files: **Design System** (tokens/components), **Screens A**, **Screens B**. Use drafts for scratch. |
| No Team Library | Keep components in the Design System file; copy/paste into screen files; document in `docs/UI/`. |
| No Dev Mode / MCP | Use FigCraft instead as primary; Figma Free only for `.fig` interop. Or pay Professional ($16/seat/mo) for one seat if Dev Mode becomes critical. |
| No modes sharing across files | Modes live per-file; export variables per file; importer merges by name. |
| 30-day version history | Git-style: export `.fig` snapshots to repo (`docs/UI/snapshots/`) on milestones. |
| Coarse permissions | Solo dev → not an issue. |

**Recommendation:** Figma Free is a *fallback*, not the primary. Use it only when a contractor delivers `.fig` or when FigCraft can't open a file.

---

## 4. Unity UI Builder — capabilities & limitations

### Capabilities
- Visual authoring of `.uxml` and `.uss` (drag-and-drop, live preview).
- UXML instances as templates (`<Template>`/`<Instance>`).
- USS variables management (assign/remove custom props) — Unity 6+.
- Optional: Vector Graphics package (`VectorImage` as background), 2D Sprite package (sprite as background, open Sprite Editor from Inspector).
- Test UI inside Builder; style debugging.
- Git-diffable output (plain XML/CSS).
- Hot reload of UXML/USS at runtime (with limitations).

### Limitations (the ones that bite)
- **No design-system parity with Figma:** no components with overrides, no variants, no auto layout beyond USS flex (which is fine, but authoring is manual), no vector editing, no effects authoring (shadows/blur are CSS-ish).
- **No token import:** USS variables must be typed by hand or generated by an importer. UI Builder can *consume* them but not *import from JSON*.
- **No animation authoring:** Builder is static. Animation is done in C#/DOTween/Rive.
- **Complex UI = lots of manual USS:** nested flex, responsive breakpoints, state classes. Builder helps but doesn't remove the code.
- **World-space UI:** UITK world-space (PanelSettings renderMode = WorldSpace) is still maturing in 6.x; uGUI remains the safer choice for 3D-attached UI.
- **No data binding authoring:** runtime bindings (Unity 6) are configured in code, not in Builder.

### How we use it
- **Primary authoring surface** for all `*.uxml`/`*.uss` in `Assets/_Project/UI/` (or wherever). FigCraft output is *input*; UI Builder is where it becomes engine-real.
- Review imported tokens/assets in Builder; tweak USS; preview across aspect ratios.
- Keep Builder-generated files as the single source of truth in git (not the design file).

---

## 5. Alternatives: Penpot & Lunacy (short)

- **Penpot** — use when: you need self-hosting (on-prem, no cloud), or you want designers to author in real CSS Flexbox/Grid (reduces translation loss to USS). Export SVG/PNG + JSON tokens; no Unity path. MCP exists. Free, open source.
- **Lunacy** — use when: a contractor sends `.fig`/`.sketch` and you want a native desktop editor without Figma account. Free. No Unity path; export PNG/SVG/CSS.
- **Neither** has variables→USS or component→UXML export. Same importer work as FigCraft, but FigCraft has better MCP/CLI and Dev Mode code gen, so prefer FigCraft.

---

## 6. Tooling comparison (scoring 1–5)

| Criterion | FigCraft | Figma Free | Penpot | Lunacy | UI Builder |
|---|---|---|---|---|---|
| Cost | 5 (free) | 4 (free, limited) | 5 | 5 | 5 (bundled) |
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

**Read:** FigCraft wins on agent/automation and cost, loses on maturity. UI Builder is the only one that touches the engine. Use them as a *pair*.

---

## 7. Recommendations (decision matrix)

1. **Adopt FigCraft as primary design tool** for all new UI work, with the understanding it's alpha. Keep one Design System file (tokens + components) and screen files.
2. **Keep Figma Free as fallback** for `.fig` interop and contractor hand-offs.
3. **Keep Penpot/Lunacy as second fallback** (self-hosting / native desktop).
4. **UI Builder remains the engine-side authoring tool.** No design tool replaces it; design tools *feed* it.
5. **Build a thin importer** (Python or C#) over FigCraft's `variable.export` (JSON/CSS) + `export` (PNG/SVG) + `inspect.code` (CSS) + `codeConnect`. Budget 1–2 weeks. This is the single highest-leverage investment.
6. **Rive for all interactive/animated UI** (buttons, HUD, transitions). Lottie for one-shot micro-anim. Spine only for skeletal mascots. VFX Graph for juice.
7. **Do not build a custom design tool.** FigCraft already does 90% of what you'd build; the remaining 10% (Unity export) is an importer, not an editor.
8. **Do not buy Figma Professional** unless Dev Mode/MCP becomes a hard blocker for a contractor pipeline; FigCraft covers the agent path for free.

---

## 8. "Living UI" pipeline: FigCraft → Unity

### 8.1 Stages

```
[1 Design]  FigCraft (components, variants, variables/modes, vectors, effects)
      │
      ▼  MCP/CLI (headless or attached)
[2 Export]  variable.export(JSON+CSS)  +  export(PNG/SVG @2x)  +  inspect.code(CSS)  +  codeConnect.export
      │
      ▼  importer (Python/C#)
[3 Tokens]  DesignTokens.uss  +  ThemeData (ScriptableObject)  +  Sprite Atlas
      │
      ▼  importer
[4 Structure]  *.uxml (from codeConnect + inspect.code template)  +  *.uss (classes)
      │
      ▼  Unity UI Builder (review/tweak/preview)
[5 Wire]   C# ViewModels + UIFrame + bindings (Unity 6 runtime bindings)
      │
      ▼  Rive runtime (interactive anim) + Lottie (micro) + VFX Graph (juice)
[6 Test]   Play Mode by user; visual diff vs FigCraft screenshot; perf guardrails
      │
      ▼
[7 Iterate]  change in FigCraft → re-export → importer → diff → review → commit
```

### 8.2 Artifacts & ownership

| Artifact | Owner | Location |
|---|---|---|
| Design file (`.figcraft`) | Designer/agent | `docs/UI/design/` (or external) |
| Exported tokens JSON/CSS | Importer output | `Assets/_Project/UI/Tokens/` |
| `DesignTokens.uss` | Importer + manual | `Assets/_Project/UI/Styles/` |
| `ThemeData.asset` | Importer + manual | `Assets/_Project/UI/Themes/` |
| Sprite atlas (PNG/SVG) | Importer | `Assets/_Project/UI/Sprites/` |
| `*.uxml` / `*.uss` | UI Builder (reviewed) | `Assets/_Project/UI/Screens/` |
| `figcraft.connect.json` | Importer | `Assets/_Project/UI/Connect/` |
| ViewModels / UIFrame | Programmer | `Assets/_Project/UI/Scripts/` |

### 8.3 Automation (CI-friendly)
- **Headless export:** `figcraft-cli mcp` (or `figcraft-cli run build.json`) in CI on design-file change → exports tokens + assets + CSS into a staging folder.
- **Importer:** runs in CI; produces diff; opens PR; human reviews UXML/USS diff (not pixels).
- **Visual regression:** render FigCraft frame (`screenshot`/`export`) and Unity frame (screenshot by user or render texture) → image diff (optional, user-driven since agent can't screenshot Play Mode).
- **Guardrails:** perf budgets (`06_PERFORMANCE_GUARDRAILS.md`) checked in CI (draw calls, GC).

### 8.4 Round-trip & single source of truth
- **Single source of truth for tokens:** FigCraft variables. Unity consumes; never edit tokens in Unity by hand (importer overwrites). Manual USS tweaks go in *component* styles, not token files.
- **Single source of truth for structure:** UXML in git (after Builder review). FigCraft is design input, not runtime.
- **Drift detection:** importer computes a hash of exported tokens; CI fails if Unity's `DesignTokens.uss` doesn't match. Forces re-import.
- **Modes/themes:** FigCraft variable modes → USS classes (`:root` default, `[data-theme="dark"]`) + `ThemeData` switching in C#.

### 8.5 Multi-theme (Light/Dark) flow
1. Define variable collection "Theme" with modes Light/Dark in FigCraft.
2. `variable.export {format: "css"}` → `:root { --surface: #fff; … }` and `[data-theme="dark"] { --surface: #111; … }`.
3. Importer splits into `DesignTokens.uss` with both blocks.
4. C# `ThemeManager` toggles `data-theme` attribute on root VisualElement + swaps `ThemeData` ScriptableObject (for non-CSS values like fonts/spacing overrides).
5. Rive state machines read theme via data binding (Rive supports runtime data binding).

---

## 9. AAA visuals with these tools

### 9.1 Visual techniques (all achievable with FigCraft + UITK + shaders)
- **Glass / frosted panels:** FigCraft glass effect (refraction/dispersion/frost/light) as design reference; in Unity use backdrop-blur via render texture or a custom URP shader (glassmorphism). UITK has no native backdrop-filter.
- **Layered shadows:** multiple drop shadows with spread → USS `box-shadow` (Unity 6 supports multiple) or custom.
- **Gradients:** linear/radial/angular/diamond → USS `linear-gradient()` (Unity 6 supports radial too); angular/diamond need custom shader or texture.
- **Blur:** layer blur → USS `filter: blur()` (Unity 6) or post-process; background blur → render texture.
- **Vector marks/icons:** FigCraft vector networks with booleans → SVG → sprite atlas at 2x/3x (crisp, tintable via `-unity-background-image-tint-color`).
- **Noise/texture overlays:** FigCraft noise → PNG texture tiled with opacity.
- **SDF text:** TextMeshPro SDF for crisp text at any scale; UITK text uses font assets (SDF in 6.x improving).
- **Color modes:** Display P3 support in FigCraft; Unity uses sRGB by default — convert at export (FigCraft can convert). Keep sRGB for game UI unless targeting HDR.
- **Pixel perfection:** align to device pixel grid; use integer positions; test at 1080p/1440p/4K.

### 9.2 Animation stack
- **Rive (primary):** state machines + data binding + scripting. Use for buttons (hover/press/disabled), HUD (health, cooldowns, minimap), transitions (screen in/out), loading, empty states. Runtime: `rive-unity` (open source, no royalties). Designers author in Rive editor; devs bind data via C#.
- **Lottie (secondary):** one-shot micro-animations (success tick, spinner, notification badge). Render to Texture2D via `com.gilzoide.lottie-player` or rlottie. Cheaper than Rive for non-interactive.
- **Spine (tertiary):** skeletal 2D for mascots/animated portraits/character UI. Not for chrome.
- **VFX Graph (juice):** confetti, energy pulses, glows, hit flashes. Render to render texture for UI overlay, or use UI Toolkit custom shaders. Particle systems for screen-space feedback.
- **UITK transforms:** `translate/scale/rotate` with `DynamicTransform`/`GroupTransform` usage hints for cheap animated elements (progress bars, sliders).
- **DOTween:** code-driven tweens for simple fades/slides when Rive is overkill.

### 9.3 Performance (ties to 06_PERFORMANCE_GUARDRAILS.md)
- Target: ≤4 draw calls for chrome, ≤1 ms UI CPU/frame, 10k+ items via ListView virtualization.
- Rive: budget per-instance; avoid 100 simultaneous state machines. Use shared artboards where possible.
- VFX Graph in UI: render to texture at lower res; limit particle counts; avoid per-frame mesh rebuilds.
- Use `DisplayStyle.None` (not opacity:0) to skip layout+render for hidden UI.
- Pre-allocate vertex buffers; mind Vertex Budget in PanelSettings.
- Dirty-tracking: only repaint changed subtrees (UITK does this; don't force full repaints).

---

## 10. Reverse engineering: how AAA studios build UI pipelines

### 10.1 Sybo (Subway Surfers) — "Building a Unified Cross-Project UI Framework" (GDC 2019, Natalia Rebrova)
- Problem: multiple Unity projects (Subway Surfers + spin-offs) with inconsistent UI and duplicated work.
- Solution: a **unified cross-project UI framework** — shared component library, common theming, reusable screens/widgets, consistent interaction patterns.
- Takeaway for Project C: extract a shared UI framework package (UXML/USS + C#) early, so future projects (or game modes) reuse it. Don't let each screen reinvent panels/buttons.
- Also: SYBO uses Unity Project Auditor + Memory Profiler + Addressables for performance (Subway Surfers City). UI is part of the perf budget, not an afterthought.

### 10.2 Bungie (Destiny / Marathon) — Tiger Engine (custom)
- Custom engine (Tiger), not Unity. UI is built in-engine with proprietary tooling; art hand-off through internal pipelines.
- Takeaway: **not directly applicable** (we're on Unity), but the principle stands: UI is a first-class system with its own framework, data binding, and perf budgets. Destiny's UI is heavily data-driven (inventory, stats, social) — supports our MVVM/ViewModel approach.
- Bungie's UI density (lots of layered panels, real-time updates) validates virtualization + dirty-tracking.

### 10.3 Riot Games (LoL, Valorant, 2XKO) — ProtoPie + "Play" design system
- **"Play" design system:** repository of predefined components (animations, sounds, transitions) for operational efficiency and consistency across games. Developers access via library.
- **ProtoPie** for interaction prototyping: replaced Figma→AEUX→After Effects (slow renders, no interactivity, bulky MP4 hand-offs) with interactive prototypes shared via links.
- **Lottie** for micro-animations: frame-by-frame in AE → Bodymovin JSON → integrated; precise control over micro-anim.
- **Multi-device integration:** ProtoPie Connect links gamepads/phones/desktops for consistent UX (relevant for multi-platform).
- **Hand-off:** component name matching between Figma and ProtoPie keeps prototypes in sync with visual updates; rules apply broadly.
- Takeaway: **build a "Play"-style component/animation library** (Rive artboards + sound + transitions) that is shared across screens; use interactive prototypes (Rive/ProtoPie) instead of static MP4 for hand-off; Lottie for micro-anim.

### 10.4 Common AAA pattern (synthesis)
1. **Design system as product:** tokens + components + motion + sound, versioned, shared.
2. **Data-driven UI:** ViewModels, bindings, not hardcoded values.
3. **Virtualization** for lists (10k+ items).
4. **Dirty tracking + batching** (repaint only what changed).
5. **Interactive prototypes** for hand-off (not static).
6. **Perf budgets** enforced in CI.
7. **Separation:** design tool = intent; engine = truth. Importer bridges.

---

## 11. Risks & mitigations

| Risk | Mitigation |
|---|---|
| FigCraft alpha instability / breaking changes | Pin a release; keep Figma Free fallback; importer versioned against FigCraft's export schema. |
| `.fig` import loss | Test interop on real files early; fall back to Figma Free for contractor files. |
| No Unity exporter → manual importer | Budget 1–2 weeks; start with tokens+assets, defer structure import. |
| Token drift between design & engine | Hash-based CI check; importer is the only writer of token files. |
| Rive runtime perf on low-end | Budget per screen; profile; fall back to static + DOTween. |
| Glass/backdrop-blur cost | Use render textures at lower res; measure. |
| UITK world-space immaturity | Keep uGUI for 3D-attached UI until 6.x matures. |

---

## 12. Sources

- FigCraft repo & docs: https://github.com/storytold/figcraft (README, docs/mcp.md, docs/cli.md, docs/control-protocol.md) — MCP 400+ commands, headless/attached, variables export (JSON/CSS/Dart), codeConnect, inspect.code/properties/assets, export, screenshot, confinement.
- FigCraft guide: https://pasqualepillitteri.it/en/news/23599/figcraft-guide-free-figma-alternative-rust
- Figma Free/Starter plan: https://help.figma.com (Starter plan overview), https://www.figma.com/pricing
- Unity UI Builder: https://docs.unity3d.com/6000.6/Documentation/Manual/UIBuilder.html (+ sub-pages)
- Unity UI Toolkit dev status: https://discussions.unity.com/t/ui-toolkit-development-status-and-next-milestones-november-2025/1698009
- Penpot vs Figma: https://penpot.app/penpot-vs-figma ; https://ux.tools/compare/penpot/vs/lunacy
- Lunacy: https://icons8.com/lunacy
- ProtoPie × Riot Games: https://www.protopie.io/blog/revolutionizing-game-ux-ui-design-riot-games
- Rive: https://rive.app/game-ui ; https://github.com/rive-app/rive-unity ; https://rive.app/blog/rive-as-a-lottie-alternative
- Lottie Unity: https://github.com/gindemit/unity-rlottie ; https://openupm.com/packages/com.gilzoide.lottie-player
- Spine: https://en.esotericsoftware.com/spine-unity
- VFX Graph & UI: https://discussions.unity.com/t/vfx-graph-ui-support/1556550 ; https://realtimevfx.com/t/how-do-you-do-ui-vfx/7642
- Sybo GDC 2019: https://media.gdcvault.com/gdc2019/presentations/rebrova-natalia-building-unified-cross-project.pdf ; https://www.youtube.com/watch?v=VSYExV7Uz-k
- Bungie/Tiger Engine: https://bungie.fandom.com/wiki/Tiger_Engine
- Riot "Play" design system: https://www.protopie.io/blog/revolutionizing-game-ux-ui-design-riot-games (component library section)
- Project C internal: `docs/UI/Impruvements_research/02_*.md`, `03_*.md`, `05_*.md`, `06_*.md`, `07_*.md`, `docs/UI/UI_TOOLKIT_GUIDE.md`

---

*End of report. Next: implement Phase 0 (UI Builder primary + Design System in UI Builder) and build the FigCraft importer (1–2 weeks) per `04_IMPLEMENTATION_PLAN.md`.*
