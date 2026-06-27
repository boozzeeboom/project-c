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

---

## TL;DR — что менять

**НЕ ТРОГАТЬ (уже корректные, но теперь читаются из InputBindingsConfig):**
- Движение (WASD → MoveForward/Backward/Left/Right), прыжок (Space → Jump), бег (Shift → Run)
- Теперь читаются через `IsActionHeld()`/`IsActionJustPressed()` из `InputBindingsConfig`
- Можно переназначать через UI (EscMenu → [НАСТРОЙКИ] → клик на строку → нажать клавишу)

**СДЕЛАНО (Phase 1 — 2.2):**
- InputBindingsConfig ScriptableObject + InputBindingsRuntime singleton
- SkillInputService.Update() polling для combat skills (10 биндов)
- EscMenuWindow (главное меню по Esc с кнопкой [НАСТРОЙКИ])
- KeybindingsWindow (список биндов + click-to-rebind)
- UIManager (стек панелей, Esc → CloseTopPanel)
- Движение через InputBindingsConfig (Phase 2.5 начало)
- Bug-001 задокументирован (Esc после CharacterWindow)

**НЕ ТРОГАТЬ (остаётся хардкод):**
- Посадка/выход корабля (F), управление кораблём (ShipInputReader)
- Подбор/инвентарь/рынок (E), диалог NPC (E)
- Сбор ресурса (F), crafting (F), дверь (F)
- Docking/CommPanel (T), F3/F4 debug HUD
- Esc закрытие в CharacterWindow (сам себе)

---

## История

| Дата | Сессия | Изменения |
|------|--------|-----------|
|| 2026-06-28 | #6 | Первая версия. Карта ввода собрана, план миграции утверждён, ждёт реализации в сессии #7+. |
|| 2026-06-28 | #7 (Phase 1) | InputBindingsConfig + InputBindingsRuntime + SkillInputService.Update() polling (10 combat skills). |
|| 2026-06-28 | #7 (Phase 2.1) | EscMenuWindow + KeybindingsWindow (read-only) + UIManager стек/логика. |
|| 2026-06-28 | #7 (Phase 2.2) | KeybindingsWindow: click-to-rebind (runtime). InputBindingsRuntime: RebindAction/RebindSkill API. |
|| 2026-06-28 | #7 (Phase 2.5 start) | NetworkPlayer: движение (WASD/Space/Shift) читается из InputBindingsConfig через IsActionHeld(). Rebind работает сразу. |
