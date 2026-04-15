---
name: brainstorm
description: "Guided game concept ideation — from zero idea to a structured game concept document. Uses professional studio ideation techniques, player psychology frameworks, and structured creative exploration."
argument-hint: "[genre or theme hint, or 'open' for fully open brainstorm]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, WebSearch, AskUserQuestion
---

When this skill is invoked:

1. **Parse the argument** for an optional genre/theme hint (e.g., `roguelike`,
   `space survival`, `cozy farming`). If `open` or no argument, start from
   scratch.

2. **Check for existing concept work**:
   - Read `docs/gdd/game-concept.md` if it exists (resume, don't restart)
   - Read `docs/gdd/game-pillars.md` if it exists (build on established pillars)

3. **Run through ideation phases** interactively, asking the user questions at
   each phase. Do NOT generate everything silently — the goal is **collaborative
   exploration** where the AI acts as a creative facilitator.

---

### Phase 1: Creative Discovery

Start by understanding the person, not the game. Ask questions conversationally:

**Emotional anchors**:
- What's a moment in a game that genuinely moved you, thrilled you, or made you lose track of time?
- Is there a fantasy or power trip you've always wanted in a game?

**Taste profile**:
- What 3 games have you spent the most time with? What kept you coming back?
- Do you prefer games that challenge you, relax you, tell you stories, or let you express yourself?

---

### Phase 2: Concept Generation

Generate **3 distinct concepts** using these techniques:

- **Verb-First Design**: Start with the core player verb (build, fight, explore)
- **Mashup Method**: Combine [Genre A] + [Theme B]
- **Experience-First Design**: Start from desired player emotion

For each concept, present:
- Working Title
- Elevator Pitch (1-2 sentences)
- Core Verb
- Core Fantasy
- Unique Hook
- Primary MDA Aesthetic
- Estimated Scope
- Why It Could Work
- Biggest Risk

---

### Phase 3: Core Loop Design

Define loops:
- **30-Second Loop** (moment-to-moment)
- **5-Minute Loop** (short-term goals)
- **Session Loop** (30-120 minutes)
- **Progression Loop** (days/weeks)

---

### Phase 4: Pillars and Boundaries

Define 3-5 pillars with design tests. Then define 3+ anti-pillars.

---

### Phase 5: Player Type Validation

Using Bartle taxonomy and Quantic Foundry model:
- Primary player type
- Secondary appeal
- Who is this NOT for

---

4. **Generate the game concept document** and save to `docs/gdd/game-concept.md`

5. **Suggest next steps**:
   - `/design-review` to validate completeness
   - `/map-systems` to decompose into systems
   - `/prototype` to test core mechanic