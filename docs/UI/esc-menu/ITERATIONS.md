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

## Итерация от 2026-07-13 (3)

**Задача:** Этап 3a-c — страницы настроек: Графика, Звук, Геймплей
**Коммит:** `6169958` — T-ESC03: Этап 3a-c — страницы настроек
**Изменения:**
- `Assets/_Project/Scripts/UI/EscMenu/GraphicsSettingsSection.cs` — качество, разрешение, экран, VSync, AA
- `Assets/_Project/Scripts/UI/EscMenu/AudioSettingsSection.cs` — громкость + placeholder-каналы
- `Assets/_Project/Scripts/UI/EscMenu/GameplaySettingsSection.cs` — чувств. мыши, инверт Y, субтитры
- `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` — секции подключены

## Итерация от 2026-07-13 (4)

**Задача:** Этапы 4-5 — выход в меню + анимации переходов
**Коммит:** `13ced8b` — T-ESC04: Этапы 4-5 — выход в меню + анимации
**Изменения:**
- `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` — диалог подтверждения, Shutdown, LoadScene, AnimateEntrance
- `Assets/_Project/Resources/UI/EscMenuStyles.uss` — animation classes (stagger/visible)
