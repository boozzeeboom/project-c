# Итерации разработки MainMenu

## Итерация от 2026-08-07

**Задача:** T-UI04 — MainMenu: замена NetworkTestCanvas на UI Toolkit главное меню
**Коммит:** `1a2c791b` — T-UI04: MainMenu — замена NetworkTestCanvas на UI Toolkit главное меню
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — UXML: панель меню (4 кнопки), IP-экран
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — стили главного меню (full-screen overlay)
- `Assets/_Project/Resources/UI/MainMenuPanelSettings.asset` — PanelSettings
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — MonoBehaviour + UIDocument + stack-навигация
- `Assets/_Project/Editor/Localization/AddMainMenuLocKeys.cs` — 9 ключей `ui.main_menu.*` в UI_Table (ru + en)
- `Assets/_Project/Scenes/BootstrapScene.unity` — MainMenu GameObject добавлен, NetworkTestCanvas отключён
- `docs/UI/MainMenu/01_implementation.md` — документация реализации
