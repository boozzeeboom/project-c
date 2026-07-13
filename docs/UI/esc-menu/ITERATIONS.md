# Итерации разработки EscMenu

## Итерация от 2026-07-13

**Задача:** Этап 1 — рефакторинг EscMenuWindow: stack-навигация, ScrollView, back-btn, делегация Esc из UIManager
**Коммит:** `0edf836` — T-ESC01: Этап 1 — рефакторинг EscMenuWindow (stack-навигация, ScrollView, back-btn)
**Изменения:**
- `Assets/_Project/Resources/UI/EscMenuStyles.uss` — размер 480×440, новые стили
- `Assets/_Project/Resources/UI/EscMenuWindow.uxml` — header, ScrollView, 3 кнопки
- `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` — stack-навигация, IsInSubmenu()
- `Assets/_Project/Scripts/UI/UIManager.cs` — делегация Esc в NavigateBack для подменю
- `docs/UI/esc-menu/01_implementation_plan.md` — план (rev.3)
- `docs/UI/esc-menu/02_audit_notes.md` — аудит кода и gaps
- `docs/UI/esc-menu/03_stage1_log.md` — лог ошибок и решений этапа 1

## Итерация от 2026-07-13 (2)

**Задача:** Этап 2 — SettingsManager + SettingsWidgets: PlayerPrefs-хранилище, фабрика виджетов, стили
**Коммит:** `6d429d1` — T-ESC02: Этап 2 — SettingsManager + SettingsWidgets + USS
**Изменения:**
- `Assets/_Project/Scripts/Core/SettingsManager.cs` — статический Singleton (PlayerPrefs, события)
- `Assets/_Project/Scripts/UI/EscMenu/SettingsWidgets.cs` — фабрика CreateSlider/Toggle/Dropdown/SectionHeader
- `Assets/_Project/Resources/UI/EscMenuSettingsStyles.uss` — стили виджетов
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — ApplyAll() при старте
- `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` — загрузка EscMenuSettingsStyles.uss
