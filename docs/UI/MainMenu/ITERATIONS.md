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

## Итерация от 2026-08-07 (2)

**Задача:** T-UI04 — fix: настройки не открывались (NavigateTo не добавлял панель в дерево)
**Коммит:** `61c926a6` — T-UI04: fix — Настройки не открывались (панель не добавлялась в визуальное дерево)
**Изменения:**
- `MainMenuWindow.cs` — `NavigateTo` добавляет динамические панели в `_contentWindow`, `EscMenuSettingsStyles.uss` загружается

## Итерация от 2026-08-07 (3)

**Задача:** T-UI04 — fix: вёрстка панели настроек (фон, border, скрытие title/subtitle)
**Изменения:**
- `MainMenuWindow.cs` — `BuildSettingsPanel`: контейнер 480×460 с backgroundColor/border-radius/border, header с back-btn + title; `SetHeaderVisible(false)` при NavigateTo
- `docs/UI/MainMenu/01_implementation.md` — полная документация с баг-фиксами

## Итерация от 2026-08-13

**Задача:** T-UI05 — MainMenu: выбор языка в правом верхнем углу
**Коммит:** `e0de174c` — T-UI05: MainMenu — выбор языка в правом верхнем углу
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — контейнер `main-lang-selector` (правый верхний угол)
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — стили компактного выпадающего списка языка
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — `CustomDropdown` с языками из `LocaleSelector` + двусторонняя синхронизация с настройками

## Итерация от 2026-08-13

**Задача:** T-UI06 — MainMenu: кнопки-ссылки в левом нижнем углу
**Коммит:** `3b0743ef` — T-UI06: MainMenu — кнопки-ссылки в левом нижнем углу
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — контейнер `main-links` (левый нижний угол)
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — стили компактных кнопок-ссылок
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — `BuildLinkButtons()`: 5 кнопок с `Application.OpenURL`

## Итерация от 2026-08-16

**Задача:** T-UI07 — MainMenu: удалённый changelog из GitHub справа по центру
**Коммит:** `36d4e40d7afbc255586ff4a1d15f6f29d914ed39` — T-UI07: добавить удалённый changelog в главное меню
**Изменения:**
- `docs/changelogs.md` — файл с записями об обновлениях перенесён в корень `docs`, добавлена первая запись с дорожной картой версии 0.1.0.
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — добавлена правая центральная панель changelog со ScrollView и кнопкой обновления.
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — добавлены стили панели и типов Markdown-строк.
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — загрузка raw-файла GitHub через `UnityWebRequest`, построчный парсинг и обработка ошибки сети.
