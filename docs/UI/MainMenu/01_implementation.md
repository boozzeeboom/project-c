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
├── НАСТРОЙКИ           → открыть ScrollView с секциями из EscMenu
│   ├── Графика (GraphicsSettingsSection)
│   ├── Звук (AudioSettingsSection)
│   └── Геймплей (GameplaySettingsSection) — включая выбор языка
└── ВЫХОД               → Application.Quit() / EditorApplication.isPlaying = false
```

## Файлы

```
Assets/_Project/Resources/UI/
├── MainMenuWindow.uxml              # UXML: панель меню, IP-экран
├── MainMenuStyles.uss               # стили
└── MainMenuPanelSettings.asset      # PanelSettings

Assets/_Project/Scripts/UI/MainMenu/
└── MainMenuWindow.cs                # MonoBehaviour + UIDocument + stack-навигация

Assets/_Project/Editor/Localization/
└── AddMainMenuLocKeys.cs            # Editor-скрипт: добавил 9 ключей в UI_Table (ru + en)
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

## Изменения в BootstrapScene

- **Добавлен:** `MainMenu` GameObject (UIDocument + MainMenuWindow)
- **Отключен:** `NetworkTestCanvas.Canvas.enabled = false`

## Что НЕ трогали

- `NetworkManagerController` — без изменений
- `EscMenuWindow`, `SettingsWidgets`, `SettingsManager` — без изменений, переиспользуются
- `Loc.cs`, `LocaleSelector.cs` — без изменений
- `UIManager.cs` — без изменений
