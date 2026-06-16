# CharacterWindow — Visual Bug Report (2026-06-05)

## Source
Screenshot from user (composer_2026-06-05_10-36-27-850_8cbe56.png) + my Play-mode recon (screenshot-20260605-153356.png).
Resolution: 2560×1440. Window expected ~720×680 centered top-5%.

## Visible bugs (from screenshot)

### B1. Tabs stacked vertically instead of horizontal row
- 5 tab buttons laid out as VERTICAL stack (one per line, full width ~700px each)
- Eats ~150px vertical space (~22% of 680px window height)
- Tab labels visible: ПЕРСОНАЖ / КОРАБЛЬ / РЕПУТАЦИЯ / КОНТРАКТЫ / ИНВЕНТАРЬ
- Active tab (ПЕРСОНАЖ) is darker blue but no yellow underline visible (border-bottom-color not applied)

### B2. Character stats section: values overlap labels
- "Характеристики" header visible
- Then: Имя / Уровень(Owner) / Опыт / Кредиты / Долг / Контракты активные — all LEFT-aligned, no 2-column layout
- Stat values (—) overlap their labels (e.g. "Долг" overlapped with "0 CR")
- "Одежда, гаджеты и прочие фичи — в будущих итерациях" placeholder visible at bottom of section

### B3. Message label outside window
- "Откройте меню персонажа" rendered BELOW the window border (overflow)
- Window does not expand to contain its content

### B4. Close button overlaps stats
- "ЗАКРЫТЬ" action button renders ON TOP of the bottom stats rows (Долг, Контракты активные)
- Indicates actions row is positioned absolutely or out of flow

### B5. Background "ProjectC — Network Test" UI visible BEHIND window
- Window is not opaque enough or content overflows
- Some other UI (Load World [0,0] button, etc) peeks through

## Files involved
- Assets/_Project/UI/Resources/UI/CharacterWindow.uxml  (tree)
- Assets/_Project/UI/Resources/UI/CharacterWindow.uss   (styles)
- Assets/_Project/Scripts/UI/Client/CharacterWindow.cs   (controller, ApplyInlineFallbackStyles etc.)

## Reference (working design)
- Assets/_Project/Trade/Resources/UI/MarketWindow.uxml (3 tabs: РЫНОК / СКЛАД / КОНТРАКТЫ — works fine, horizontal)
- Assets/_Project/Trade/Resources/UI/MarketWindow.uss  (reference styles)
- Assets/_Project/Trade/Scripts/Client/MarketWindow.cs (FIX 2026-06-04 = 4 bugs they fixed)

## Scope of analysis requested
ONLY diagnosis + concrete fix list. Do NOT modify files.
