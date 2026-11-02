# 16 — Automated Pipeline: HTML/CSS/JS Prototype (v4-2) → Unity UI

> **Scope:** Research + blueprint for automating the conversion of the project's web prototype (`.Aura/Slices/v4-2/` — HTML + CSS + JS, ~23 modules, ~1000 lines CSS) into Unity 6 UI Toolkit (UXML/USS/C#) with **zero manual steps** after the prototype changes. Builds on `10_TOOLING_PIPELINE.md`, `11_AGENT_TOOLING.md`, `12_OVERCOMING_LIMITS.md`, `05_REVERSE_ENGINEERING_NOTES.md`. Targets Project C (Unity 6000.5.2f1, URP, Windows standalone).
>
> **Verdict up front:** There is no single off-the-shelf "HTML→Unity" converter that is good enough for a AAA game UI, but the building blocks exist (html2uuitk, RottenUITK, HtmlToUIToolKit, style-extraction libs, DOTween, Rive). The realistic automation is a **multi-stage agent-driven pipeline**: (1) render the prototype in a headless browser and dump computed styles + DOM, (2) parse/normalize tokens and structure, (3) generate UXML/USS/C# via an LLM with a strict conversion skill, (4) bake unsupported CSS into sprites/shaders, (5) wire animations to DOTween/Rive, (6) validate with visual diff + USS capability probe. The agent (OpenCode + unityMCP) drives every stage; the user only reviews diffs and runs Play Mode.

---

## 0. TL;DR

| Stage | Input | Output | Tooling | Automation |
|---|---|---|---|---|
| 0 Capture | v4-2 HTML/CSS/JS | Rendered DOM + computed styles JSON | Playwright/Puppeteer (headless Chromium) | Fully automated |
| 1 Tokens | CSS + computed styles | `DesignTokens.uss` + `ThemeData` SO + sprite list | PostCSS / css-tree / projectwallace | Fully automated |
| 2 Structure | DOM JSON | UXML tree (flex) | html2uuitk / custom converter | Automated + LLM review |
| 3 Styles | CSS rules + tokens | USS (mapped subset) | html2uuitk / mapping table | Automated + LLM review |
| 4 Assets | CSS `url()`, inline SVG, canvas | PNG/SVG sprite atlas | Playwright screenshot + sharp/svgo | Fully automated |
| 5 Animations | JS/CSS keyframes/transitions | C# coroutines / DOTween sequences / Rive `.riv` | Parser + LLM + DOTween | Semi (Rive needs designer) |
| 6 Workarounds | Unsupported CSS (gap, gradients, shadows, blur) | Margins, 9-slice sprites, shaders, C# | Capability probe + baker | Automated |
| 7 Wire | UXML/USS | C# ViewModels + bindings + events | LLM codegen (skill) | Semi (logic) |
| 8 Validate | Unity scene | Visual diff + USS warnings + perf | unityMCP + image diff | User runs Play Mode |

**Key insight (from `unityworkflowlibrary.dev`):** port from the *rendered* reference (`getComputedStyle`), never from markup text. Authored CSS is inherited/cascaded/unit-converted; only the rendered tree gives exact px rects, resolved colours, real type scale, and runtime-generated content (v4-2 computes card faces/seats/buttons in JS).

---

## 1. The source: v4-2 prototype

- Location: `.Aura/Slices/v4-2/` — `index.html`, `src/*.css` (style.css, alt.css, rel.css, skills.css), `src/*.js` (~23 modules: main, ui, home, bag, calendar, quests, ships, relations, skills, …), `plan/04_halos.md`, `tests/*`.
- It is a **full interactive prototype**: card faces, seats, buttons are rendered by `renderVals`-style functions; animations live in JS (`motion.js`, `halos.js`, `flex.js`); layout uses CSS flex + custom `.mgN` gap-margin helpers and `.grp-*` grouping because USS has no grid/gap in Unity 6.0 (gap arrives in 6.7).
- Already ported partially to `Assets/_Project/UIv2/` (Home, Bag, Calendar, Quests, Ships, … screens styled "after v4-2") and to `Assets/_Project/Debug/CloudyNavigator` (uGUI). The goal of this pipeline: make that port **automated and repeatable** when v4-2 changes.
- v4-2 CSS uses: `:root` variables, flex, absolute positioning, `linear-gradient`/`radial-gradient`, `box-shadow` (0 in v4-2 per audit? actually gradients/shadows), `backdrop-filter`? (check), `transform`, `transition`, `@keyframes`, `::before`/`::after`, `filter`, `clip-path`, `gap`? (custom). All need mapping.

---

## 2. Unity UI Toolkit capabilities (measured on 6.000.5)

Source: Unity USS property reference + `unityworkflowlibrary.dev` USS capability probe (6000.5 measured set).

### Supported (direct pass-through)
`flex`, `flex-direction`, `flex-wrap`, `flex-grow/shrink/basis`, `justify-content`, `align-items`, `align-self`, `align-content`, `padding`, `margin`, `width/height`, `min/max-*`, `position: absolute/relative`, `top/right/bottom/left`, `overflow`, `opacity`, `background-color`, `border-*`, `border-radius`, `color`, `font-size`, `letter-spacing`, `word-spacing`, `white-space`, `visibility`, `cursor`, `text-align` (via `-unity-text-align`), `font-family` (via `-unity-font`), `font-weight/style` (via `-unity-font-style`), `text-shadow`, `transition` (limited), `rotate/scale/translate`, `transform-origin`, `z-index` (6.7; not 6.0), `box-shadow` (6.0+), `backdrop-filter` (6.6+, URP only, screen-space only), `filter`, `aspect-ratio`, `gap` (6.7), `grid-*` (6.0+, `display: grid`), `linear-gradient`/`radial-gradient` on `background-image` (6.7).

### Unsupported / degraded (must workaround)
`gap` (pre-6.7), `display: grid` template (pre-6.7), `z-index` (pre-6.7), `backdrop-filter` (only URP screen-space), `linear-gradient`/`radial-gradient` (pre-6.7), `box-shadow` (pre-6.0? supported 6.0+), `clip-path`/masks, `@keyframes`/`animation` (USS `animation` exists but uses Unity AnimationClips, not CSS keyframes), `::before`/`::after` (no generated content), `:nth-child`/`:first-child`/`:last-child`, `@media`, `float`, `text-transform`, `text-overflow: ellipsis` (partial), `linear-gradient` angular/diamond, `filter: drop-shadow` (6.6+), `position: fixed/sticky`.

### Hard limits (no workaround in USS — must bake)
- `backdrop-filter` on world-space panels (unsupported) → render texture + custom shader or skip.
- Arbitrary CSS shapes (clip-path, masks) → bake into sprite alpha.
- Complex gradients (angular/diamond) → texture or custom shader (Painter2D / Shader Graph).
- CSS `animation`/`@keyframes` with complex easing → DOTween or Unity AnimationClip (`.anim`), not USS `animation`.

**Implication for automation:** the converter must run a **capability probe** (write one test USS, read import warnings) per Unity version, and route unsupported properties to a "bake" stage instead of emitting broken USS.

---

## 3. Tools & libraries for parsing

### 3.1 HTML/CSS → UXML/USS (existing converters)

| Tool | Type | Strengths | Gaps | Use in pipeline |
|---|---|---|---|---|
| **html2uuitk** (npm, `fake-skate`) | CLI + Node | 73+ USS props, HTML→UXML element mapping, shorthand expansion, CSS var chain resolution, circular-ref detection, rule dedup, `rem/em/vw/vh` conversion, modern color syntax normalization | No JS, no animations, no assets, no gap/grid workaround, no computed styles | First-pass mechanical conversion for static layout/styles |
| **RottenUITK** (`@rotten-games/rotten-uitk`) | CLI + Unity package + browser preview | HTML + Tailwind → UXML/USS/C# builder class, `watch` mode, preview in browser | Tailwind-centric; limited to its template set | Reference for "C# builder class" output pattern |
| **HtmlToUIToolKit** (Unity package, `jixinhaoqi`) | Unity editor + browser converter | 30+ HTML tags, 50+ CSS props, grid→flex approx, gradient→avg color, rich text, pseudo-classes, sprite path post-processing, AI SKILL prompt | No JS, no animations, browser-DOM dependent | In-editor conversion + path post-processing for sprites |
| **UX Importer for UI Toolkit** (Asset Store) | Figma/JSON → UXML/USS | JSON design export, localization sync | Figma-centric, not HTML | Not for HTML prototype |
| **UIToolkitMCP** (MarzLars) | MCP server | LLM-friendly conversion guidance + Unity docs | Guidance only, not a converter | Agent reference for property mappings |

**Decision:** Use **html2uuitk** as the *mechanical* first pass (deterministic, scriptable, CI-friendly), then an **LLM agent pass** (with a project skill encoding the conversion table + workarounds) to fix what html2uuitk can't (computed styles, JS-driven content, animations, unsupported CSS).

### 3.2 CSS parsers / token extractors

| Library | Language | Purpose | Use |
|---|---|---|---|
| **PostCSS** + `postcss-custom-properties` | Node | Parse CSS, resolve `var()` chains, extract `:root` tokens | Token extraction stage |
| **css-tree** (npm) | Node | Lossless CSS AST, fast, spec-compliant | Structural CSS → USS rule rewriting |
| **@projectwallace/css-analyzer** + **css-design-tokens** | Node | Extract colors/fonts/sizes/spacing/gradients into W3C Design Tokens format, with authored-vs-computed + property usage | Design token pipeline (core + semantic tokens) |
| **style-dictionary** | Node | Tokens JSON → multi-platform (CSS, USS, JSON, C#) | Generate `DesignTokens.uss` + `ThemeData` SO from one token source |
| **csso** / **clean-css** | Node | Minify/dedup | Optional post-processing of USS |
| **Beautiful Soup / lxml** | Python | HTML DOM parsing | Structure extraction if doing Python pipeline |
| **Playwright / Puppeteer** | Node | Headless browser: render prototype, dump `getComputedStyle` per node, screenshot, capture runtime-generated DOM | **Critical:** computed-style capture (the "rendered ref" approach) |
| **sharp** | Node | Image processing: resize, atlas packing, 9-slice extraction | Asset stage |
| **svgo** | Node | SVG optimization | Icon assets |
| **colord** / **chroma.js** | Node | Color parsing/normalization (hex/rgb/hsl/oklch → Unity color) | Token normalization |
| **css-what** / **nth-check** | Node | Selector parsing | Selector → USS selector rewriting |

### 3.3 HTML parsers (C# side, if converting in-Unity)

| Library | Use |
|---|---|
| **AngleSharp** (C#) | Full HTML/CSS DOM + computed styles in .NET; can run in Unity editor scripts |
| **HtmlAgilityPack** | Lighter HTML parsing |
| **Markdig** | Markdown (not needed here) |

**Decision:** Keep parsing in **Node/Python** (Playwright + PostCSS + css-tree) outside Unity, emit artifacts (JSON + UXML/USS/PNG) into `Assets/_Project/UIv2/`, then let Unity import. This avoids runtime dependencies and matches the project's "design tool = intent, engine = truth" principle. Use **AngleSharp** only if an in-editor importer is needed.

### 3.4 Animation tooling

| Tool | Use |
|---|---|
| **DOTween** (Demigiant) | Code-driven tweens for simple fades/slides/scale/position/color; `DOTween.To`, sequences, easing curves, `WaitForCompletion` for coroutines |
| **Unity Animation (`.anim` + Animator)** | Complex multi-property keyframe animations; author in Animation window or generate via script (`AnimationClip.SetCurve`) |
| **Rive** (`rive-unity`) | Interactive state-machine animations (hover/press/disabled, HUD, transitions); designer-authored in Rive editor; runtime data binding |
| **Lottie** (`com.gilzoide.lottie-player`) | One-shot micro-animations (success tick, spinner) |
| **VFX Graph** | Particle/glow juice (confetti, energy) rendered to texture |
| **Coroutine lerp util** (custom) | Lightweight programmatic animation without DOTween dependency |

### 3.5 Agent/MCP tooling

- **OpenCode + unityMCP** (port 6401): `create_script`, `script_apply_edits`, `manage_gameobject`, `manage_asset`, `read_console`, `batch_execute` (≤25), resources `mcpforunity://editor/state`, `mcpforunity://project/info`.
- **anklebreaker** (port 7890): unique features only (profiler, undo history, multi-instance). Not for standard scene/script work.
- **Playwright MCP** (if available) or `execute` shell calls to a Node script for headless browser capture.
- **Git CLI** for commits (per project rules).

---

## 4. The automated pipeline (step-by-step)

### Stage 0 — Capture (rendered reference)

**Goal:** produce a JSON snapshot of the *rendered* prototype: DOM tree + computed style per node + bounding rects + runtime-generated content + screenshots of key states.

1. Serve v4-2 locally: `python -m http.server` in `.Aura/Slices/v4-2/` (file:// renders scripts inert → unbound `{{...}}`).
2. Playwright script (`capture.js`) opens Chromium at a fixed viewport (e.g. 1920×1080, matching artboard), waits for `main.js` init, then:
   - Walks DOM recursively, emitting `{ path, tag, classes, text, rect: {x,y,w,h}, computedStyle: {...} }`.
   - Captures `getComputedStyle` for a curated property set (layout, color, typography, effects, transforms).
   - Triggers interactive states (hover/press, open panels, dealt hands) by calling prototype JS hooks or simulating clicks; captures each state as a separate snapshot (`state-id`).
   - Screenshots full page + per-component (Playwright `screenshot({ clip })`).
   - Exports inline SVGs and canvas snapshots (toDataURL) to files.
   - Dumps `:root` CSS variables (`getComputedStyle(document.documentElement)` filtered to `--*`).
3. Output: `out/v4-2/snapshot.json` + `out/v4-2/screens/*.png` + `out/v4-2/assets/*`.

**Automation:** fully scripted, runs in CI on prototype change. No user steps.

### Stage 1 — Token extraction

**Goal:** `DesignTokens.uss` + `ThemeData` ScriptableObject + asset manifest, derived from CSS `:root` + computed styles.

1. Parse all CSS files with `css-tree`/PostCSS; extract custom properties from `:root` and component scopes.
2. Resolve `var()` chains (html2uuitk-style, with circular-ref detection and fallbacks).
3. Normalize values: `rem/em` → px (base 16), `vw/vh` → %, colors → hex/rgba, letter-spacing doubled (Unity convention).
4. Classify tokens: color, dimension (spacing/size), font-family, font-size, font-weight, radius, shadow, duration, easing.
5. Use `@projectwallace/css-design-tokens` to cross-check: extract actual used colors/fonts/sizes/spacing from all rules (catches tokens not declared in `:root` but used inline).
6. Dedup: merge near-duplicate values (e.g. 20px/21px type scale) — **but** keep exact computed values (per `unityworkflowlibrary`: the design used exactly 20px & 21px carrying hierarchy via weight/alpha/letter-spacing; do not invent a 12/14/16/18 ramp).
7. Emit:
   - `DesignTokens.uss` with `:root { --color-*: ...; --spacing-*: ...; }` + `[data-theme="dark"] { ... }` for modes.
   - `ThemeData.cs`/`.asset` (ScriptableObject) for non-CSS values (font assets, sprite refs).
   - `tokens.json` (W3C DTCG format) for style-dictionary round-trip.
8. Hash tokens; CI fails if Unity's `DesignTokens.uss` ≠ generated (drift detection).

**Automation:** fully scripted.

### Stage 2 — Structure conversion (HTML → UXML)

**Goal:** UXML tree reflecting the rendered DOM, with flex layout, using UITK element types.

1. Run html2uuitk on `index.html` + CSS as mechanical first pass → initial `.uxml`/`.uss`.
2. **LLM agent pass** (skill `html-to-uxml`, loaded from project) reviews the snapshot JSON and:
   - Maps HTML tags → UITK controls (div→VisualElement, p/span/h1-6→Label, button→Button, input→TextField/Toggle/Slider, img→VisualElement with background-image, ul/ol/li→VisualElement, select→DropdownField, progress→ProgressBar).
   - Skips `script/style/head/form/svg/canvas/audio/video/iframe`; warns on table elements.
   - Flattens runtime-generated content: for each JS-rendered list (quests, skills, ships), emit a `ListView`/`VisualElement` container with a C# item template instead of static items.
   - Applies the project's "gap workaround": since USS gap is unavailable pre-6.7, convert CSS `gap` → margin on children (`.grp-r/c/w` pattern) OR, if targeting Unity 6.7+, emit `gap` directly. Decision: support both via a config flag `unityVersion` (6.0 vs 6.7).
   - Converts CSS Grid → nested flex (html2uuitk grid approx) OR, on 6.7, native `display: grid`.
   - Fixes z-index → hierarchy order (pre-6.7) or `z-index` (6.7).
   - Removes `position: fixed/sticky` → absolute within root.
3. Emit `*.uxml` (one per screen) + `*.uss` (classes).

**Automation:** mechanical + agent; agent pass is scripted via a prompt template with the snapshot + conversion table. Human reviews the diff (not pixels).

### Stage 3 — Style conversion (CSS → USS)

**Goal:** USS classes with mapped properties, token references, workarounds.

Use the mapping table (from `12_OVERCOMING_LIMITS.md` + Unity docs):

| CSS | USS | Notes |
|---|---|---|
| `display: flex` | `display: flex` | Only flex supported (pre-6.7) |
| `display: grid` | `display: flex` (nested) OR `display: grid` (6.7) | |
| `gap` | margin on children OR `gap` (6.7) | |
| `padding/margin` | same | px/% |
| `width/height` | same | px/%; vw/vh → % |
| `position: absolute` | same | relative to parent |
| `top/right/bottom/left` | same | |
| `overflow: auto/scroll` | `overflow: hidden` | |
| `position: fixed` | `position: absolute` | |
| `background-color` | same | |
| `border-*` | same | |
| `border-radius` | same | per-corner supported |
| `color` | same | |
| `font-size` | same | px only |
| `font-weight: 600+` | `-unity-font-style: bold` | |
| `font-family` | `-unity-font-definition` | requires SDF font asset |
| `text-align` | `-unity-text-align` | middle-left/center/right |
| `letter-spacing` | same (doubled) | |
| `opacity` | same | |
| `transform` | `translate/rotate/scale` | separate props, not shorthand |
| `box-shadow` | same (6.0+) | multi-shadow supported |
| `linear-gradient` | `linear-gradient()` (6.7) OR baked sprite | |
| `radial-gradient` | `radial-gradient()` (6.7) OR baked | |
| `backdrop-filter` | same (6.6+, URP, screen-space) OR render texture | |
| `filter` | same (6.0+) | |
| `text-shadow` | same | |
| `transition` | same (limited) | prefer DOTween for complex |
| `animation/@keyframes` | — | DOTween / `.anim` / Rive |
| `::before/::after` | real child VisualElements | |
| `:hover/:active/:focus` | same pseudo-classes | |
| `:nth-child/:first/last` | — | C# or margin tricks |
| `@media` | — | C# screen-size checks |
| `text-transform` | — | C# `ToUpper()` |
| `text-overflow: ellipsis` | `-unity-text-overflow-position` | may need C# truncation |
| `z-index` | hierarchy order (6.0) OR `z-index` (6.7) | |
| `clip-path/masks` | — | bake into sprite alpha |
| `aspect-ratio` | same (6.0+) | |

**Agent pass rules:**
- Every property not in the "supported" list → route to Stage 6 (workarounds) or emit a `/* UNSUPPORTED: ... */` comment + create a bake task.
- Use USS custom properties (`var(--*)`) exclusively; no hardcoded values (per project convention).
- Expand shorthands (margin, padding, border, flex).
- Dedup rules when multiple selectors map to the same output (html2uuitk does this).
- Pseudo-classes pass through.
- `!important` → drop (USS ignores).

### Stage 4 — Asset extraction

**Goal:** sprite atlas (PNG/SVG) + icon list + 9-slice metadata.

1. From CSS: regex/AST extract all `url(...)` references (images, fonts).
2. From HTML: extract `<img src>`, inline `<svg>`, `<canvas>`.
3. From Playwright snapshot: capture any runtime-generated images (canvas toDataURL, SVG serialized).
4. For inline SVG icons: run svgo → save as `.svg` (or rasterize to PNG at 2x/3x for Unity sprite, since Unity Vector Graphics is optional).
5. For CSS gradients/shadows that can't be USS: render the element in Playwright (screenshot of just that node with transparent bg) → PNG → Unity sprite (9-slice if scalable). This is the "bake" approach for glass/gradient panels.
6. Pack into a Sprite Atlas via Unity `manage_asset` or a C# editor script (Sprite Atlas v2).
7. Emit `asset-manifest.json` mapping CSS class/element → sprite path + 9-slice borders (computed from Playwright rects or authored in CSS `border-image-slice`).

**Automation:** scripted; Unity sprite atlas creation can be driven via `execute_menu_item` or a small editor script invoked through unityMCP `execute_code` (allowed? project says don't write .meta/.asmdef by hand; editor scripts OK in `Assets/_Project/Editor/`).

### Stage 5 — Animation conversion

**Goal:** C# animation code (coroutines/DOTween) + Rive `.riv` references, matching prototype motion.

1. **Extract animation definitions:**
   - Parse CSS `@keyframes` → keyframe tables (property, time, value, easing).
   - Parse `transition` declarations → property/duration/easing/delay.
   - Parse JS: identify `requestAnimationFrame` loops, `setTimeout`/`setInterval`, GSAP/anime.js calls (if any), CSS class toggles that drive state. v4-2 uses custom `motion.js` and `halos.js` — inspect for animation patterns.
2. **Classify:**
   - Simple property tweens (opacity, transform, color, size) with standard easing → **DOTween** (`DOMove/DOScale/DOFade/DOColor` or generic `DOTween.To`).
   - Complex multi-property choreography → **Unity AnimationClip** (generate via `AnimationClip.SetCurve` + `AnimationUtility` or author in Animation window).
   - Interactive state machines (hover/press/disabled, panel open/close, HUD states) → **Rive** (designer re-authors in Rive editor; agent only wires the `.riv` + data binding; cannot auto-generate `.riv` from CSS).
   - One-shot micro-animations (spinners, success ticks) → **Lottie**.
   - Particle/glow juice → **VFX Graph**.
3. **Codegen:** LLM agent generates C# animation controllers:
   - `FooAnimator.cs` with methods `PlayOpen()`, `PlayClose()`, `PlayHover()`.
   - Uses DOTween sequences with the extracted durations/easings; falls back to coroutine lerp util if DOTween unavailable.
   - Hooks into UITK events (`PointerEnterEvent`, `PointerLeaveEvent`, `ClickEvent`) and `TransitionEndEvent`.
4. **Rive hand-off:** for interactive animations, agent emits a spec (`rive-spec.md`) listing artboards, state machines, inputs, and data bindings needed; designer (or agent via Rive editor automation if available) builds the `.riv`. Agent then wires `RiveWidget` + data binding in C#.

**Honesty:** Rive `.riv` cannot be generated from CSS/JS automatically today (no converter). This is the one stage requiring designer/agent authoring in Rive editor. Everything else (DOTween tweens, coroutines) is automatable.

### Stage 6 — Workarounds / baking (Unity limitations)

**Goal:** eliminate unsupported CSS by transforming it into supported equivalents or baked assets.

Capability probe (per Unity version): write a test USS file with all candidate properties, import, read Console warnings → build the "supported set" for this Unity version. Store in `uss-capability.json`.

For each unsupported property in the converted USS:
- `gap` → margin on children (`.mgN` pattern) OR native `gap` if 6.7.
- `display: grid` (complex templates) → nested flex (auto) OR native grid if 6.7.
- `z-index` → reorder elements in UXML (paint order) OR native if 6.7.
- `linear-gradient`/`radial-gradient` (pre-6.7) → render element in Playwright → PNG → `background-image: resource(...)` (9-slice if scalable). OR, on 6.7, emit native.
- `box-shadow` (pre-6.0? supported 6.0+) → if supported, pass; else nested element with background + blur sprite.
- `backdrop-filter` → if URP screen-space, pass; else render-texture blur shader OR drop (design fallback: solid semi-transparent bg).
- `clip-path`/masks → bake shape into sprite alpha.
- `::before/::after` → real child `VisualElement` with descriptive class (e.g. `.window__glow-line`).
- `@media` → C# screen-size checks in `UIFrame`/`ResponsiveLayout`.
- `text-transform` → C# in ViewModel/binding.
- `:nth-child` → C# or negative-margin trick.
- `position: fixed/sticky` → absolute in root + C# pinning.
- `text-overflow: ellipsis` → `-unity-text-overflow-position` + C# truncation fallback.
- `filter: drop-shadow` → `filter` (6.6+) or baked sprite.

**Bake pipeline:** Playwright renders the element (or a component) with the unsupported effect at 2x/3x into a PNG with transparency; `sharp` trims/optimizes; Unity imports as sprite; USS references it via `background-image: resource("...")` with 9-slice borders computed from the element's padding.

### Stage 7 — Wiring (C# ViewModels + bindings)

**Goal:** connect UXML to data and events.

1. LLM agent (skill `uxml-to-csharp`) generates:
   - `FooScreen.cs` (MonoBehaviour/VisualElement controller) that loads UXML, queries elements by name/class, sets up bindings.
   - ViewModels implementing `INotifyPropertyChanged` (or Unity 6 runtime data binding `[CreateProperty]`).
   - Event handlers (`clicked`, `valueChanged`) wired to commands.
   - Navigation integration with `UIFrame` (modal stack, focus).
2. Uses Unity 6 runtime binding (`[UxmlElement]`, `[UxmlAttribute]`, data binding) where applicable.
3. Registers screens in the project's screen router.

**Automation:** semi — logic (what a button does) must be specified by the developer; the agent scaffolds structure and binding boilerplate from the UXML + a "behavior spec" (YAML) listing events→commands.

### Stage 8 — Validation

**Goal:** catch regressions without manual pixel-hunting.

1. **Compile check:** unityMCP `read_console` (errors+warnings) after `create_script`/`script_apply_edits`; wait `is_compiling == false`. Zero errors required.
2. **USS capability probe:** re-run; zero "unknown property" warnings.
3. **Visual diff:** agent cannot screenshot Play Mode (project rule), so the user runs Play Mode and provides a screenshot; agent compares against the Playwright reference screenshot via image diff (SSIM/pixelmatch) and reports deltas. Or use a render-texture capture script run by the user.
4. **Perf guardrails:** draw calls, UI CPU/frame, GC (per `06_PERFORMANCE_GUARDRAILS.md`); anklebreaker profiler if needed.
5. **Drift detection:** token hash check in CI.

---

## 5. Agent-driven automation (no manual steps)

### 5.1 Orchestration

A single entry point: `scripts/ui-pipeline/run.js` (Node) invoked by the agent (or CI) with `--slice v4-2 --unity 6.0 --out Assets/_Project/UIv2`. It runs Stages 0–6 and writes artifacts. Stages 7–8 require agent LLM passes + user verification.

Pipeline steps the agent executes (via `execute`/shell):
1. `node scripts/ui-pipeline/capture.js` → snapshot + screenshots.
2. `node scripts/ui-pipeline/tokens.js` → `DesignTokens.uss`, `tokens.json`, `ThemeData` stub.
3. `node scripts/ui-pipeline/convert.js` (html2uuitk + post-processing) → `*.uxml`, `*.uss`.
4. Agent LLM pass: load skill `html-to-uxml` (to be created) with snapshot + conversion table → rewrite UXML/USS for computed styles, workarounds, runtime lists.
5. `node scripts/ui-pipeline/assets.js` → sprites + atlas + manifest.
6. `node scripts/ui-pipeline/animations.js` → extract keyframes/transitions → `animations.json`.
7. Agent LLM pass: load skill `uxml-animations` → generate C# DOTween controllers + Rive spec.
8. Agent LLM pass: load skill `uxml-to-csharp` → generate controllers + ViewModels.
9. `unityMCP` batch: import assets, create USS/UXML files via `create_script`? (files are assets, not scripts — write via file system into `Assets/...`, then `refresh`? Project rule: don't call refresh after create_script; for asset files, Unity auto-imports on focus; agent can poll `is_compiling`/asset import). Actually writing `.uxml`/`.uss`/`.png` to disk triggers Unity import; no `create_script` needed. Use `manage_asset` for sprite import settings.
10. `unityMCP read_console` → verify zero errors.
11. User runs Play Mode + provides screenshot → agent visual diff.

### 5.2 Skills to create (project-local, `.opencode/skills/`)

- `html-to-uxml`: conversion table + workaround rules + project conventions (namespaces, `_camelCase`, Russian comments, ticket IDs).
- `uxml-animations`: CSS keyframes/transitions → DOTween/Rive mapping + C# codegen template.
- `uxml-to-csharp`: UXML → controller/ViewModel scaffolding with UIFrame integration.
- `uss-capability-probe`: run probe, emit `uss-capability.json`.

### 5.3 Agent constraints (from project rules)

- Never write `.meta`/`.asmdef` by hand.
- Never call `refresh_unity` after `create_script`/`script_apply_edits`; poll `is_compiling`.
- Never launch Play Mode or take screenshots; user does that.
- Commit only via Git CLI, with ticket ID; never push.
- Standard work goes through `unityMCP` (port 6401); anklebreaker only for unique features.

### 5.4 CI integration

- Trigger: change in `.Aura/Slices/v4-2/**` or `scripts/ui-pipeline/**`.
- Run pipeline stages 0–6 headlessly (Playwright + Node).
- Commit generated artifacts to a branch; open PR with diff of UXML/USS/C#.
- CI checks: token hash drift, USS capability (no unknown props), compile (Unity batch mode headless build? project says don't run build via MCP; CI can run `unity -batchmode -runTests` or just compile check via `unity -batchmode -quit` with `-logFile`), perf budgets.
- Human review: UXML/USS diff (not pixels) + Play Mode verification by user.

---

## 6. Handling Unity-specific limitations (summary)

| CSS feature | Unity status (6.0) | Strategy |
|---|---|---|
| `gap` | Unsupported (6.7+) | Margin on children (`.mgN`) |
| CSS Grid templates | Unsupported (6.7+) | Nested flex |
| `z-index` | Unsupported (6.7+) | Hierarchy order |
| `backdrop-filter` | 6.6+, URP, screen-space only | Pass if eligible else solid bg / render-texture blur |
| Gradients | 6.7+ (linear/radial) | Bake to sprite pre-6.7 |
| `box-shadow` | Supported 6.0+ | Pass |
| `clip-path`/masks | Unsupported | Bake into sprite alpha |
| `@keyframes`/`animation` | USS animation uses AnimationClips, not CSS | DOTween / `.anim` / Rive |
| `::before/::after` | No generated content | Real child elements |
| `:nth-child` | Unsupported | C# or margin tricks |
| `@media` | Unsupported | C# screen checks |
| `text-transform` | Unsupported | C# |
| `position: fixed/sticky` | Unsupported | Absolute + C# pinning |
| `text-overflow: ellipsis` | Partial | `-unity-text-overflow-position` + C# |
| `filter: drop-shadow` | 6.6+ | Pass or bake |
| `aspect-ratio` | Supported | Pass |
| `transform` shorthand | Separate props | Rewrite to translate/rotate/scale |
| `font-family` | `-unity-font-definition` | SDF font asset required |
| `text-align` | `-unity-text-align` | middle-* mapping |
| `letter-spacing` | doubled | ×2 |
| `rem/em/vw/vh` | px/% | Convert (base 16px) |
| Modern color syntax `rgb(0 0 0 / .5)` | Normalize to `rgba(0,0,0,0.5)` | html2uuitk does this |

---

## 7. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Playwright capture misses runtime-generated content | Trigger all states via prototype JS hooks; capture per-state snapshots |
| html2uuitk output needs heavy fixing | Agent LLM pass with strict conversion skill; human reviews diff |
| Token drift | Hash-based CI check; importer is sole writer |
| Rive `.riv` can't be auto-generated | Designer/agent authors in Rive editor; agent wires only |
| USS version differences | Capability probe per Unity version; config flag |
| Prototype changes frequently | Pipeline is idempotent; CI re-runs on change |
| Agent hallucinates Unity APIs | Use `unity_reflect`/`unity_docs` (docs tool group) to verify before codegen |
| `.meta`/`.asmdef` corruption | Never hand-write; let Unity generate |
| Perf regressions | Guardrails in CI; anklebreaker profiler |

---

## 8. Sources

- html2uuitk: https://github.com/fake-skate/html2uuitk (npm) — HTML+CSS → UXML/USS, 73+ props, var resolution, dedup.
- RottenUITK: https://github.com/enesunal-m/flexui (npm `@rotten-games/rotten-uitk`) — HTML+Tailwind → UXML/USS/C# builder.
- HtmlToUIToolKit: https://github.com/jixinhaoqi/HtmlToUIToolKit — Unity package, browser converter, 30+ tags, 50+ props, grid→flex.
- UX Importer for UI Toolkit: https://assetstore.unity.com/packages/tools/gui/ux-importer-for-ui-toolkit-337438
- UIToolkitMCP: https://github.com/MarzLars/UIToolkitMCP — conversion guidance + Unity docs.
- USS property reference: https://docs.unity.com/en-us/engine/7000.0/manual/uitoolkits/uielements/uie-uss/properties/reference
- backdrop-filter: https://docs.unity.com/en-us/engine/6000.6/manual/uitoolkits/uielements/uie-uss/uss-filter/backdrop-filter
- Unity 6.7 UI Toolkit status: https://discussions.unity.com/t/state-of-ui-toolkit-in-unity-6-7/1736756
- USS capability probe / rendered-ref doctrine: https://unityworkflowlibrary.dev/workflows/html-css-to-ui-toolkit/ and https://unityworkflowlibrary.dev/workflows/uss-uxml/
- CSS→USS mapping: https://github.com/raddue/crucible/blob/main/skills/mock-to-unity/references/css-to-uss-mapping.md
- mock-to-unity skill: https://github.com/raddue/crucible/blob/main/skills/mock-to-unity/SKILL.md
- @projectwallace/css-design-tokens: https://github.com/projectwallace/css-design-tokens
- style-dictionary: https://amzn.github.io/style-dictionary/
- PostCSS: https://postcss.org/ ; css-tree: https://github.com/csstree/csstree
- Playwright: https://playwright.dev/
- DOTween: https://dotween.demigiant.com/ (docs: https://dotween.demigiant.com/documentation.php)
- Rive Unity: https://github.com/rive-app/rive-unity ; https://rive.app/docs/runtimes/getting-started
- Lottie Unity: https://openupm.com/packages/com.gilzoide.lottie-player
- Coroutine animation util: https://michaelbitzos.com/devblog/programmatic-animation-using-coroutines
- Balancy animation porting guide (Unity `.anim` → web): https://en.docsv2.balancy.dev/ui-builder/unity_animation_porting_guide/
- Project C internal: `docs/UI/Impruvements_research/05_*.md`, `07_*.md`, `10_*.md`, `11_*.md`, `12_*.md`, `docs/UI/full rebuild plans/Cloudy_redesign/*.md`, `.Aura/Slices/v4-2/`.

---

*End of report. Next: implement `scripts/ui-pipeline/` (Stages 0–6) and create project skills `html-to-uxml`, `uxml-animations`, `uxml-to-csharp`, `uss-capability-probe`; then run first conversion on v4-2 and review diff.*