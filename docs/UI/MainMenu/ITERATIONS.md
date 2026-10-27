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

## Итерация от 2026-08-16 (2)

**Задача:** T-UI07 — упростить расположение changelog для обновления
**Коммит:** `41c7685280d65677e66f875e9c98aaa7596bb896` — T-UI07: перенести changelog в корень docs
**Изменения:**
- `docs/changelogs.md` — changelog перенесён из `docs/UI/MainMenu/`, чтобы файл было проще находить и обновлять.
- `MainMenuWindow.cs` и документация — обновлён путь к raw-файлу GitHub.

## Итерация от 2026-08-16 (3)

**Задача:** T-UI07 — исправить 404 после переноса changelog
**Коммит:** `6d1ea83e9e7a0a35b9fc933bdf39c37473401302` — T-UI07: исправить загрузку changelog после переноса
**Изменения:**
- `MainMenuWindow.cs` — добавлено принудительное исправление старого сериализованного URL `docs/UI/MainMenu/changelogs.md` на новый `docs/changelogs.md`.
- В предупреждение добавлен фактический URL запроса для диагностики.

## Итерация от 2026-10-27

**Задача:** PCMM — фон меню перекрывался градиентом проекта; табы настроек схлопывались.

**Причина (измерено, не гипотеза):**
- `PCMM_Sky.shader:14` — `Queue = "Background"`, единственный квад стека в этой очереди; остальные слои — `Transparent`.
  `ProjectC_URP` содержит один рендерер `ProjectC_URP_Renderer` с фичами `EdgeDetectionRenderFeature`,
  `VolumetricCloudsRenderFeature` (`BeforeRenderingTransparents`) и `DistantFocusRenderFeature` (`AfterRenderingOpaques`).
  Обе рисующие фичи отсекают только `CameraType.Preview`, поэтому работали и на камере меню и закрашивали
  скай; звёзды, вуали, газ и тросы рисовались уже после них — отсюда «фон частично сработал».
- Таб в `BuildSettings()` был `TextElement` с ребёнком `pc-tab__glow`. Как только у `TextElement` появляется
  ребёнок, собственный текст перестаёт учитываться в интринсик-размере: замер дал 5.1 px на таб, а тот же
  текст без ребёнка — 65.6 px. Три таба по 5.1 px накладывались в баре 623.7 px.

**Изменения:**
- `Assets/Aura/Mods/ProjectCMainMenu/PCMM_Renderer.asset` — копия `ProjectC_URP_Renderer` с очищенным списком
  фич; добавлена в `ProjectC_URP` (renderer list, индекс 1, `m_DefaultRendererIndex` остался 0).
- `ProjectCMainMenuAtmosphere.cs` — камера меню переведена на `PCMM_Renderer`, `renderPostProcessing = false`,
  чтобы палитра меню не зависела от Volume проекта.
- `ProjectCMainMenuWindow.cs` — таб стал контейнером, текст вынесен в отдельный `pc-tab__label`.
- `PCMM_Menu.uss` — правило `.pc-tab__label`; существующие правила `.pc-tab`, `.pc-tab:hover`,
  `.pc-tab.is-active`, `.pc-tab.is-active .pc-tab__glow`, `.pc-tabs > .pc-tab` не менялись.

**Проверка (замер на живом дереве в редакторе, без Play Mode):** табы 65.6 / 38.6 / 79.7 × 32.2 с шагом 18 px,
карточка 639.1 × 550.4 по центру, cloud bank 754.9 × 676.4 (11 блобов, `aspect-ratio` работает),
`pc-panels` 623.7 × 439.8 без переполнения. `check_compile_errors` — 0.

**Известное ограничение:** на панели с масштабом < 1 волосяные линии `height: 1px` округляются в 0
(измерено: USS 1px → 0, инлайн 2px → 1.286). Проявляется только в окне уже 1920 по ширине.

**Что не тронуто:** `WorldScene_0_0.unity`, `ProjectC_URP_Renderer.asset`, USS-фильтры
(`ProjectCMainMenuTextBlower.BlurFiltersEnabled` остаётся `false`).

**Решение по сцене:** вынос меню в отдельную пустую сцену в этот проход не входит — рендер-фичи живут на
`ProjectC_URP`, а не в сцене, поэтому переезд не менял бы ни градиент, ни вёрстку; кнопка «ОДИНОЧНАЯ ИГРА»
требует `GlobalMotionPilotRuntime` и `NetworkManager` из `BootstrapScene`, то есть переезд был бы выносом
сетевого бутстрапа. См. отдельную задачу по разбиению `MenuScene` / `BootScene`.

## Итерация от 2026-10-27 (2)

**Задача:** PCMM — у кнопок и карточки не было фона, «мягкое свечение таба» читалось как жёсткая
полоса, текст без размытия, время суток не отзывалось на смену таба; плюс убрать шаблонные тексты.

**Причина (измерено, не гипотеза):**
- В `PCMM_Menu.uss` на каждом правиле, где в `ui.css` был `linear-gradient()` / `radial-gradient()`,
  стоял только комментарий `[C#] ramp`: генератор ramp-текстур был отложен на «шаг 7»
  (`02_smoke_menu_port.md:246-248`) и не написан. Поэтому `.pc-btn-cloud`, `.pc-btn-cloud__rim`,
  `.pc-tab__glow`, `.pc-card__mist`, `.pc-card-cloud__rim`, `.pc-cld`, `.pc-main__scrim`, `.pc-rule`,
  `.pc-close`, `.pc-row`, `.pc-range-track`, `.pc-switch`, `.pc-select`, `.pc-kbd` не рисовали ничего.
- `--pc-cloud-base` = rgba(7,10,9,0.34) и `--pc-track-2` = rgba(7,10,9,0.55) — угольные тона: они
  писались под светлое небо веб-меню. На затемнённой атмосфере Unity такой фон невидим — отсюда
  «у кнопок нет светлой подложки».
- `ProjectCMainMenuAtmosphere`: `daySpeed` читалось только в строке цикла суток — ни сеттера, ни
  вызова из `RequestTab` / `OpenSettings`.
- Шаблонные тексты `Eyebrow` («THE CLOUDS · ТЕРМИНАЛ ДОСТУПА»), `MenuNote` («В глубине качаются
  тросы…»), хинт с легендой клавиш и строка сборки — это копия `ui.js`, а не интерфейс проекта.

**Изменения:**
- `ProjectCMainMenuRamps.cs` (новый) — генератор ramp-текстур: `Elliptical()` читает CSS-геометрию
  буквально (`radial-gradient(rx ry at cx cy, tint, transparent stop)`), `Horizontal()` — таблица
  стопов; кэш и `HideFlags.DontSave` как у `ProjectCMainMenuSmokeTextures`.
- `ProjectCMainMenuWindow.Ramps.cs` (новый, partial) — `ApplyRamps()` сразу после сборки дерева.
- Тона: подложка кнопки, ореолы и дорожки контролов — светлые (`#e9f2ec` / `#eef3ef`), потому что
  угольные на вуали не видны; угольные листы дыма остались поверх подложки кнопки, так что кнопка
  читается и в светлую фазу суток.
- `ProjectCMainMenuWindow.cs` — убраны `Eyebrow`, `MenuNote`, хинт, легенда клавиш и строка сборки;
  `pc-foot` остался пустым хостом.
- `ProjectCMainMenuWindow.Behaviour.cs` — сняты `notePart` и `BlowTextOf(Eyebrow, …)`.
- `ProjectCMainMenuAtmosphere.cs` — `DaySpeed`, `CurrentDaySpeed`, `PulseDaySpeed(boost, seconds)`;
  буст затухает линейно через `MoveTowards`.
- `ProjectCMainMenuMenuBridge.cs` — `tabDayBoost = 4`, `tabDayBoostSeconds = 2`: скачок `ActiveTab`
  даёт `PulseDaySpeed`, базовая скорость возвращается сама.
- `PCMM_Menu.uss` — `background-size: 100% 100%` на всех ramp-элементах, высота волосяных линий
  1 → 2 px, тёмный ореол `text-shadow` на `.pc-tab`, `.pc-tab__label`, `.pc-card-title`,
  `.pc-row-label`, `.pc-row-note`, `.pc-note-line`.

**Проверка (замер на живом дереве в редакторе, без Play Mode):** root 1920.0 × 1089.2; у
`pc-menu-main` 3 ребёнка (scrim, title, nav-row), у `pc-foot` — 0; карточка 639.1 × 550.4; табы
65.6 / 38.6 / 79.7 (не изменились); `.pc-rule` 2.57 px вместо 0. Текстуры назначены: `pc-btn-cloud`,
`pc-blob`, `pc-tab__glow`, `pc-card__mist`, `pc-main__scrim`, `pc-switch`, `pc-select`, `pc-kbd`.
`check_compile_errors` — 0, ошибок импорта USS в консоли нет.

**Не сделано — шрифт:** в моде своего шрифта нет вообще (`grep` по `.Aura/Mods/projectc-main-menu`
не находит ни `font-family`, ни `--pc-font`), поэтому «шрифт мода» нечего портировать: `ui.css`
полагался на шрифт браузера. В проекте нет ни одного `PanelTextSettings` (все 23 `PanelSettings`
с `textSettings = null`), кириллические начертания есть только у `Graphy` и `LiberationSans`.
Нужен файл шрифта от заказчика — до этого меню рисуется системным шрифтом UITK.

**Что не тронуто:** `WorldScene_0_0.unity`, `BootstrapScene.unity` (не сохранялась), статические
`filter: blur()` в USS, `BlurFiltersEnabled` остаётся `false`, угольные листы
`ProjectCMainMenuSmokeTextures.Cloud` на блобах кнопок, `ProjectC_URP.asset` и `PCMM_Renderer.asset`.
