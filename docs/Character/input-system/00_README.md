# Input System — Deep Research & Migration Plan

> **Дата:** 2026-06-28 (сессия #6 — глубокий input-ресёрч)
> **Контекст:** подсистемы `Skills/Battle` (SkillInputService) и `Player` (PlayerInputReader, NetworkPlayer) эволюционировали параллельно. Возникла дивергенция: некоторые бинды в PlayerInputReader, некоторые дублированы в NetworkPlayer.Update, SkillInputService использует слоты без явного mapping на клавиши. Цель — единый слой ввода с настройкой через ScriptableObject.

---

## Решения пользователя (фиксировано)

| # | Решение | Источник |
|---|---------|----------|
| **Q-INP-01** | **НЕ хардкод клавиш.** На MVP настройка и бинд через **инспектор** (InputBindingsConfig SO + дефолты). Потом — UI-меню настроек (Phase 2). | 2026-06-28 |
| **Q-INP-02** | **Боевые активные навыки** = малый набор комбинаций: ЛКМ, ПКМ, Ctrl+ЛКМ, Ctrl+ПКМ, Shift+ЛКМ, Shift+ПКМ (6 биндов на основной набор). | 2026-06-28 |
| **Q-INP-03** | **Все старые клавиши (бег, посадка, крафт, инвентарь, диалоги, etc.)** остаются по дефолту как и раньше. Не ломать. | 2026-06-28 |

---

## Структура каталога

| Файл | Назначение |
|------|------------|
| `00_README.md` | этот файл |
| `10_CURRENT_STATE.md` | карта всего существующего ввода (файлы, клавиши, ownership) |
| `20_KEYBIND_INVENTORY.md` | сводная таблица: действие → клавиша → файл:строка |
| `30_INPUT_BINDINGS_CONFIG_DESIGN.md` | дизайн InputBindingsConfig ScriptableObject |
| `40_MIGRATION_PLAN.md` | безопасный план миграции (3 фазы) |
| `50_OPEN_QUESTIONS.md` | что НЕ решено (будущие фазы) |
| `60_PHASE_1_5_SUMMARY.md` | сводка Phase 1 + Phase 1.5 — что сделано, что работает, roadmap |
| `70_PHASE_2_SUMMARY.md` | сводка Phase 2 — EscMenu, KeybindingsWindow, rebind, движение через конфиг |
| `80_PHASE_3_SUMMARY.md` | сводка Phase 3 — BindSlot UI в SkillTreeWindow |

---

## TL;DR — что менять

**НЕ ТРОГАТЬ (уже корректные, но теперь читаются из InputBindingsConfig):**
- Движение (WASD → MoveForward/Backward/Left/Right), прыжок (Space → Jump), бег (Shift → Run)
- Теперь читаются через `IsActionHeld()`/`IsActionJustPressed()` из `InputBindingsConfig`
- Можно переназначать через UI (EscMenu → [НАСТРОЙКИ] → клик на строку → нажать клавишу)

**СДЕЛАНО (Phase 1 — 2.5 + 3):**
- InputBindingsConfig ScriptableObject + InputBindingsRuntime singleton
- SkillInputService.Update() polling для combat skills (10 биндов)
- EscMenuWindow (главное меню по Esc с кнопкой [НАСТРОЙКИ])
- KeybindingsWindow (список биндов + click-to-rebind + RebindPromptWindow)
- UIManager (стек панелей, Esc → CloseTopPanel)
- ВЕСЬ ввод из InputBindingsConfig: WASD, Space, Shift, E, F, P, T, ship controls (W/S/A/D/E/Q/Shift)
- PlayerPrefs persistence + кнопки [СОХР]/[ЗАГР]/[СБРОС]
- Phase 3: BindSlot UI в SkillTreeWindow — назначение навыков на Primary/Secondary/Slot1-4
- Bug-001 ✅ Esc после CharacterWindow не открывает меню

**НЕ ТРОГАТЬ (остаётся хардкод):**
- Посадка/выход корабля (F) — теперь через InputBindingsConfig (ModeSwitch)
- Корабль — через InputBindingsConfig (ShipThrustForward и т.д.)
- Esc закрытие в CharacterWindow (сам себе) — ✅ BUG-001 фикс

---

## История

| Дата | Сессия | Изменения |
|------|--------|-----------|
|| 2026-06-28 | #6 | Первая версия. Карта ввода собрана, план миграции утверждён, ждёт реализации в сессии #7+. |
|| 2026-06-28 | #7 (Phase 1) | InputBindingsConfig + InputBindingsRuntime + SkillInputService.Update() polling (10 combat skills). |
|| 2026-06-28 | #7 (Phase 2.1) | EscMenuWindow + KeybindingsWindow (read-only) + UIManager стек/логика. |
|| 2026-06-28 | #7 (Phase 2.2) | KeybindingsWindow: click-to-rebind (runtime). InputBindingsRuntime: RebindAction/RebindSkill API. |
|| 2026-06-28 | #7 (Phase 2.5 start) | NetworkPlayer: движение (WASD/Space/Shift) читается из InputBindingsConfig через IsActionHeld(). Rebind работает сразу. |
|| 2026-06-28 | #7 (Phase 2.5 complete) | Весь ввод через Config (E/F/P/T/Ship/Interact/ModeSwitch). BUG-001 фикс. |
|| 2026-06-28 | #7 (Phase 2.3) | PlayerPrefs persistence + кнопки СОХР/ЗАГР/СБРОС в KeybindingsWindow. |
|| 2026-06-28 | #7 (Phase 3) | BindSlot UI в SkillTreeWindow — привязка навыков на Primary/Secondary/Slot1-4. |
