# MainMenu — Документация

> **Дата:** 2026-08-07
> **Статус:** ✅ Реализовано
> **Контекст:** `docs/UI/esc-menu/` — EscMenu (переиспользуем секции настроек)
> **Локализация:** `docs/world/Localization/` — `Loc.cs`

---

## Что сделано

Заменили UGUI `NetworkTestCanvas`/`NetworkTestMenu` на UI Toolkit `MainMenuWindow` в BootstrapScene.

## Структура меню

```
MainMenu (корень)
├── ОДИНОЧНАЯ ИГРА     → NetworkManagerController.StartHost() → скрыть меню
├── ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ → IP-экран
│   ├── TextField (placeholder: 127.0.0.1)
│   ├── ПОДКЛЮЧИТЬСЯ → NetworkManagerController.ConnectToServer(ip, 7777) → скрыть меню
│   └── ← НАЗАД        → возврат в корень меню
├── НАСТРОЙКИ           → открыть панель 480×460 с фоном, border и ScrollView
│   ├── Графика (GraphicsSettingsSection)
│   ├── Звук (AudioSettingsSection)
│   └── Геймплей (GameplaySettingsSection) — включая выбор языка
└── ВЫХОД               → Application.Quit() / EditorApplication.isPlaying = false
```

## Файлы

```
Assets/_Project/Resources/UI/
├── MainMenuWindow.uxml              # UXML: панель меню, IP-экран
├── MainMenuStyles.uss               # стили (full-screen overlay, кнопки)
└── MainMenuPanelSettings.asset      # PanelSettings (ConstantPhysicalSize)

Assets/_Project/Scripts/UI/MainMenu/
└── MainMenuWindow.cs                # UIDocument + Stack<VisualElement> навигация

Assets/_Project/Editor/Localization/
└── AddMainMenuLocKeys.cs            # Editor-скрипт: 9 ключей ui.main_menu.* в UI_Table
```

## Локализация

Ключи в `UI_Table` (`ui.main_menu.*`):

| Ключ | ru | en |
|---|---|---|
| `ui.main_menu.title` | PROJECT C: THE CLOUDS | PROJECT C: THE CLOUDS |
| `ui.main_menu.subtitle` | Версия Alpha 0.1 | Alpha 0.1 |
| `ui.main_menu.button.host` | ОДИНОЧНАЯ ИГРА | SOLO GAME |
| `ui.main_menu.button.connect` | ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ | CONNECT TO SERVER |
| `ui.main_menu.button.settings` | НАСТРОЙКИ | SETTINGS |
| `ui.main_menu.button.quit` | ВЫХОД | QUIT |
| `ui.main_menu.button.ip_connect` | ПОДКЛЮЧИТЬСЯ | CONNECT |
| `ui.main_menu.button.back` | ← НАЗАД | ← BACK |
| `ui.main_menu.ip_label` | Введите IP-адрес сервера: | Enter server IP: |

## Навигация и стек

- `Stack<VisualElement>` — корень (`_rootButtons`) + подменю (`_ipPanel`, `BuildSettingsPanel()`)
- `NavigateTo(panel)` — прячет текущий экран, добавляет панель в `_contentWindow` (если не ребёнок), скрывает title/subtitle
- `NavigateToRoot()` — сбрасывает стек до первого элемента, возвращает title/subtitle
- `SetHeaderVisible(bool)` — переключает видимость `_titleLabel`/`_subtitleLabel` при входе/выходе из подменю

## Панель настроек

- Контейнер 480px × max-height 460px с `backgroundColor: rgba(18, 22, 32, 0.95)`, border-radius 8px, border 2px
- Header: кнопка «← НАЗАД» + заголовок «НАСТРОЙКИ»
- `ScrollView` → `GraphicsSettingsSection.Create()` + `AudioSettingsSection.Create()` + `GameplaySettingsSection.Create()`
- Виджеты используют классы из `EscMenuSettingsStyles.uss` (загружается в `rootVisualElement.styleSheets`)
- Выбор языка (LocaleSelector) — через `GameplaySettingsSection`

## Выбор языка (правый верхний угол)

- Контейнер `main-lang-selector` закреплён в правом верхнем углу (`position: absolute; top: 14px; right: 16px; width: 150px`)
- `CustomDropdown` с нативными названиями языков из `LocaleSelector.Locales`
- Переключение через `LocaleSelector.SetLocale`; двусторонняя синхронизация с настройками (`Loc.OnLocaleChanged`)

## Изменения в BootstrapScene

- **Добавлен:** `MainMenu` GameObject (UIDocument + MainMenuWindow)
- **Отключен:** `NetworkTestCanvas.Canvas.enabled = false`

## Что НЕ трогали

- `NetworkManagerController` — без изменений
- `EscMenuWindow`, `SettingsWidgets`, `SettingsManager` — без изменений, переиспользуются
- `Loc.cs`, `LocaleSelector.cs` — без изменений
- `UIManager.cs` — без изменений

## Баг-фиксы (итерация 2)

| Проблема | Причина | Решение |
|---|---|---|
| Настройки не открывались | `NavigateTo` не добавлял динамическую панель в визуальное дерево | Добавлен `_contentWindow.Add(panel)` + `_contentWindow` ссылка на `main-menu-window` |
| Виджеты без стилей | `EscMenuSettingsStyles.uss` не загружался | `Resources.Load<StyleSheet>("UI/EscMenuSettingsStyles")` → `rootVisualElement.styleSheets` |
| Частичная вёрстка / нет фона | Панель без контейнера, title/subtitle оставались видимы | Контейнер 480×460 с `backgroundColor` + border; `SetHeaderVisible(false)` при `NavigateTo` |
