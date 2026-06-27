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

---

## TL;DR — что менять

**НЕ ТРОГАТЬ (уже корректные):**
- Движение (WASD), прыжок (Space), бег (Shift) в PlayerInputReader + NetworkPlayer
- Посадка/выход корабля (F), управление кораблём (W/S/A/D/Q/E + MouseY + Shift)
- Подбор/инвентарь/рынок (E), диалог NPC (E)
- Сбор ресурса (F), crafting (F), дверь (F)
- Docking/CommPanel (T), F3/F4 debug HUD
- Esc закрытие в окнах (CharacterWindow, SkillTreeWindow, CraftingWindow, DialogWindow)

**СОЗДАТЬ НОВОЕ:**
- `InputBindingsConfig.cs` ScriptableObject (дефолты через инспектор)
- 1 `.asset` файл в `Assets/_Project/Resources/InputBindingsConfig.asset`

**ДОБАВИТЬ (минимально-инвазивно):**
- `SkillInputBindings` модуль в SkillInputService (читает из InputBindingsConfig)
- 1 extension method `TryActivateFromInput()` в SkillInputService для горячих комбинаций (ЛКМ / ПКМ / Ctrl+ЛКМ / ...)

**НЕ ТРОГАТЬ (явно):**
- NetworkPlayer Update (только добавить вызов в нужную точку)
- PlayerInputReader (оставить как есть — он не для боя)
- Все UI-окна (Esc-handlers оставить как есть)

---

## История

| Дата | Сессия | Изменения |
|------|--------|-----------|
| 2026-06-28 | #6 | Первая версия. Карта ввода собрана, план миграции утверждён, ждёт реализации в сессии #7+. |
