# EscMenu — План реализации (Phase 1: структура и настройки)

> **Дата:** 2026-07-13 (rev.2 — аудит + правки)
> **Статус:** Этап 1 ✅, Этап 2 ✅, Этап 3a-c ✅, 3d ⏳, Этап 4 ✅, Этап 5 ✅
> **Лог этапа 1:** `docs/UI/esc-menu/03_stage1_log.md`
> **Лог этапа 2:** `docs/UI/esc-menu/04_stage2_log.md`
> **Лог этапа 3:** `docs/UI/esc-menu/05_stage3_log.md`
> **Лог этапов 4-5:** `docs/UI/esc-menu/06_stage4_5_log.md`
> **Контекст:** `docs/UI/esc-menu/` — документация по разработке EscMenu
> **Аудит:** `docs/UI/esc-menu/02_audit_notes.md` — полный разбор кода и gaps

---

## 0. Что НЕ трогаем (красные линии)

| Компонент | Причина |
|---|---|
| `UIManager.HandleGlobalInput()` — алгоритм | Esc-логика работает правильно: стек → CharacterWindow → внешние окна → Toggle EscMenu. Любое изменение сломает BUG-001 (исправлен) |
| `UIManager.OpenPanel()` / `ClosePanel()` API | Сигнатуры менять нельзя — KeybindingsWindow и будущие подокна используют этот API |
| `EscMenuWindow.Toggle()` / `Show()` / `Hide()` / `IsOpen()` | Публичный API, вызывается из UIManager |

### Что **добавляется** в UIManager (минимальное, без изменения API)

```csharp
// Единственное добавление в HandleGlobalInput() — после проверки _openPanels.Count > 0:
//
// if (_openPanels[0].PanelName == "EscMenu")
// {
//     EscMenuWindow.Instance?.HandleGlobalEscape();
//     return;
// }
//
// EscMenuWindow.HandleGlobalEscape() решает: NavigateBack() если глубина > 1,
// или Hide() если на корневом экране (текущее поведение).
```

---

## 1. Структура меню (дерево)

```
EscMenu (корень)
├── ПРОДОЛЖИТЬ              → Hide() (закрыть меню)
├── НАСТРОЙКИ               → открыть подменю "settings"
│   ├── Управление           → KeybindingsWindow (как sub-page ВНУТРИ EscMenu)
│   ├── Графика              → подменю "graphics"
│   │   ├── Качество (Low/Medium/High/Ultra)
│   │   ├── Разрешение
│   │   ├── Полный экран
│   │   ├── VSync
│   │   └── Сглаживание
│   ├── Звук                 → подменю "audio"
│   │   └── Общая громкость (AudioListener.volume; AudioMixer — в будущем)
│   └── Геймплей             → подменю "gameplay"
│       ├── Чувствительность мыши
│       ├── Инвертировать Y
│       └── Субтитры
└── ВЫХОД В МЕНЮ             → подтверждение → перезагрузка BootstrapScene

⚠ Язык (Language) — DEFERRED. В проекте нет инфраструктуры локализации.
```

### Принцип навигации

- **Главное меню** — первый экран при открытии EscMenu
- **Подменю** открываются заменой контента внутри `esc-content` (не новыми окнами, не Clear+CloneTree)
- **Кнопка «НАЗАД»** в каждом подменю — возврат на уровень выше
- **Esc на главном меню** — закрывает меню (через UIManager → `HandleGlobalEscape()`)
- **Esc на подменю** — возврат на уровень выше (НЕ закрывает меню целиком)
- **KeybindingsWindow** — встраивается как sub-page, а не отдельное окно (текущее поведение с ClosePanel+Show меняется)

---

## 2. Результаты аудита кода (14 файлов изучено)

### Найденные проблемы и их решения

| # | Проблема | Решение |
|---|---|---|
| GAP-01 | Esc-handler UIManager конфликтует с submenu-навигацией — UIManager всегда закроет EscMenu | Добавить `HandleGlobalEscape()` в EscMenuWindow + 3 строки в UIManager (см. §0) |
| GAP-02 | Текущий размер `.esc-root` 360×280 не вместит настройки | Увеличить до 480×440, добавить ScrollView для страниц с большим числом опций |
| GAP-03 | mouseSensitivity хардкодом в WorldCamera и ThirdPersonCamera, не через PlayerPrefs | Создать `SettingsManager`, модифицировать обе камеры на чтение оттуда |
| GAP-04 | AudioMixer не существует, каналы разделить нельзя | Использовать AudioListener.volume для Master, остальные слайдеры — placeholder |
| GAP-05 | GameEventBus не существует, нет механизма оповещения подписчиков | SettingsManager — статический класс со встроенными событиями (OnMouseSensitivityChanged и т.д.) |
| GAP-06 | Language dropdown — нет локализации нигде в проекте | **DEFERRED.** Вынести из этапа 3c |
| GAP-07 | MainMenu.unity не существует | Выход в меню = перезагрузка BootstrapScene (ставшая MainMenu-заглушка) |
| GAP-08 | RebindPromptWindow тоже использует антипаттерн Clear+CloneTree | Зафиксировано, НЕ в scope этого плана |
| GAP-09 | KeybindingsWindow сейчас открывается как ОТДЕЛЬНОЕ окно (ClosePanel + Show) | Переделать на sub-page внутри EscMenu через NavigateTo |
| GAP-10 | В плане не было .uss для настроечных виджетов | Добавить `EscMenuSettingsStyles.uss` |

---

## 3. Этапы реализации

### Этап 1: Рефакторинг EscMenuWindow (фундамент)

**Задача:** Исправить антипаттерн `Clear() + CloneTree()`, внедрить систему подменю, настроить размер окна.

**Что делаем:**
1. Перевести `EnsureBuilt()` на канонический шаблон из `UI_TOOLKIT_GUIDE.md` §3:
   - UIDocument сам грузит UXML → используем `_doc.rootVisualElement`
   - USS добавляем через `styleSheets.Add()` без дублирования
   - **Убрать `Clear() + CloneTree() + Add()`**
2. Внедрить `Stack<VisualElement>` для навигации по подменю:
   - `_menuStack` — стек открытых экранов (главное меню + подменю)
   - `NavigateTo(VisualElement panel)` — показать новый экран, спрятать текущий
   - `NavigateBack()` — вернуться на предыдущий экран
3. Добавить `HandleGlobalEscape()` — вызывается из UIManager:
   - если глубина стека > 1 → NavigateBack()
   - если 1 → Hide()
4. Увеличить `.esc-root` до 480×440, контент — ScrollView
5. Инкапсулировать кнопку «НАЗАД» как переиспользуемый элемент
6. Добавить `DefaultExecutionOrder(-150)` чтобы Update() бежал между UIManager(-200) и всеми остальными — запасной Esc-handler для submenu

**Файлы:**
- `EscMenuWindow.cs` — рефакторинг
- `EscMenuWindow.uxml` — контейнеры: `esc-header`, `esc-content` (ScrollView), `esc-back-btn`
- `EscMenuStyles.uss` — расширение стилей под новый размер + ScrollView внутри

**Критерий готовности:** меню открывается/закрывается как раньше, но с кнопкой «НАЗАД» и структурой подменю (пока пустых). Esc на подменю → возврат. Esc на корне → закрытие.

---

### Этап 2: UI-компоненты настроек (переиспользуемые)

**Задача:** Создать набор переиспользуемых UI-компонентов для страниц настроек + SettingsManager для данных.

**Что делаем:**
1. `SettingsWidgets` — фабрика:
   - `CreateSlider(string label, float min, float max, float initial, Action<float> onChange)` → VisualElement
   - `CreateToggle(string label, bool initial, Action<bool> onChange)` → VisualElement
   - `CreateDropdown(string label, List<string> choices, int selected, Action<int> onChange)` → VisualElement (использует готовый CustomDropdown)
   - `CreateSectionHeader(string title)` → VisualElement с разделителем
2. `SettingsManager` — статический класс (Singleton), хранит:
   - `MouseSensitivity` (float, PlayerPrefs)
   - `InvertY` (bool, PlayerPrefs)
   - `MasterVolume` (float, PlayerPrefs, применяется к AudioListener.volume)
   - `Subtitles` (bool, PlayerPrefs)
   - `QualityLevel`, `Fullscreen`, `VSync`, `AntiAliasing` (применяются к QualitySettings/Screen)
   - События: `OnMouseSensitivityChanged`, `OnInvertYChanged`, `OnMasterVolumeChanged` и т.д.
   - `Save()` / `Load()` — PlayerPrefs
   - `ApplyAll()` — применить все настройки при старте

**Файлы:**
- `Assets/_Project/Scripts/UI/EscMenu/SettingsWidgets.cs`
- `Assets/_Project/Scripts/Core/SettingsManager.cs`
- `Assets/_Project/Resources/UI/EscMenuSettingsStyles.uss`

**Критерий готовности:** виджеты создаются из кода, выглядят консистентно, SettingsManager читает/пишет PlayerPrefs.

---

### Этап 3: Страницы настроек (наполнение)

#### 3a. Графика (`GraphicsSettingsSection`)

| Настройка | Тип | Источник/API |
|---|---|---|
| Качество | Dropdown (Low/Med/High/Ultra) | `QualitySettings.SetQualityLevel()` |
| Разрешение | Dropdown (список из `Screen.resolutions`) | `Screen.SetResolution()` |
| Полный экран | Toggle | `Screen.fullScreenMode` |
| VSync | Toggle | `QualitySettings.vSyncCount` |
| Сглаживание | Dropdown (Off/2x/4x/8x) | `QualitySettings.antiAliasing` |

**Сохранение:** Применить сразу + PlayerPrefs через SettingsManager.

#### 3b. Звук (`AudioSettingsSection`)

| Настройка | Тип | Диапазон |
|---|---|---|
| Общая громкость | Slider | 0–100% |

**Остальные каналы (Музыка/Эффекты/Голос/UI) — placeholder, скрыты до внедрения AudioMixer.**
**Сохранение:** `AudioListener.volume = value / 100f` сразу + PlayerPrefs через SettingsManager.

#### 3c. Геймплей (`GameplaySettingsSection`)

| Настройка | Тип | Значения |
|---|---|---|
| Чувствительность мыши | Slider | 0.1–10.0 |
| Инвертировать Y | Toggle | On/Off |
| Субтитры | Toggle | On/Off |

**Language — DEFERRED** (нет инфраструктуры локализации).

**Сохранение:** PlayerPrefs через SettingsManager.
**Бриджинг с камерами:** `WorldCamera` и `ThirdPersonCamera` читают `SettingsManager.MouseSensitivity` и `InvertY` в Update (кэшированные поля, обновляются по событиям SettingsManager).

#### 3d. Управление (KeybindingsWindow как sub-page)

Перенос существующего KeybindingsWindow внутрь EscMenu. **Сейчас** он открывается как отдельное окно (через `ClosePanel("EscMenu") + Show()`). **Становится** sub-page через `EscMenuWindow.NavigateTo()`.

**Изменения в KeybindingsWindow:**
- Метод `GetPageRoot()` — возвращает VisualElement для встраивания
- Отключается `SetOpen` через UIManager.OpenPanel (теперь живёт внутри EscMenu)
- Кнопка Save/Reload/Reset остаются
- Esc внутри — возврат в настройки (NavigateBack)

**Критерий готовности этапа 3:** каждая настройка меняет реальное поведение игры, сохраняется между сессиями, камеры реагируют на чувствительность.

---

### Этап 4: Выход в главное меню

**Задача:** Кнопка «ВЫХОД В МЕНЮ» с подтверждением.

**Что делаем:**
1. При клике — показать диалог подтверждения (встроенный в EscMenu, не отдельное окно)
2. При подтверждении:
   - В сетевой игре: `NetworkManager.Singleton.Shutdown()`
   - `SceneManager.LoadScene("BootstrapScene")` (замена MainMenu до появления отдельной сцены)

**Критерий готовности:** кнопка работает, диалог подтверждения показан, при отказе возврат в меню.

---

### Этап 5: Анимации переходов (L2 Motion — базовая)

**Задача:** Плавные переходы между экранами меню.

**Что делаем:**
1. Fade-in/Fade-out для подменю через USS transitions `opacity 0.15s`
2. Slide-анимация для кнопок при появлении (staggered, ~50ms между кнопками через schedule.Execute)
3. Hover/focus через USS transitions (уже частично есть)

**Критерий готовности:** переходы плавные, ≤0.25s, без рывков.

---

## 4. Структура файлов (целевая)

```
Assets/_Project/
├── Resources/UI/
│   ├── EscMenuWindow.uxml              # расширен: все контейнеры подменю + ScrollView
│   ├── EscMenuStyles.uss               # расширен: новый размер + nav-стили
│   ├── EscMenuSettingsStyles.uss       # НОВЫЙ: стили для слайдеров/тогглов/дропдаунов в EscMenu
│   └── EscMenuPanelSettings.asset      # без изменений (themeUss задан)
├── Scripts/UI/EscMenu/
│   ├── EscMenuWindow.cs                # рефакторинг + навигация + HandleGlobalEscape
│   ├── SettingsWidgets.cs              # НОВЫЙ: фабрика виджетов
│   ├── SettingsManager.cs              # НОВЫЙ: статический класс хранения настроек
│   ├── GraphicsSettingsSection.cs      # НОВЫЙ: страница графики
│   ├── AudioSettingsSection.cs         # НОВЫЙ: страница звука (Master только)
│   └── GameplaySettingsSection.cs      # НОВЫЙ: страница геймплея
├── Scripts/Core/
│   └── WorldCamera.cs                  # МОДИФИЦИРОВАН: читает MouseSensitivity из SettingsManager
│   └── ThirdPersonCamera.cs            # МОДИФИЦИРОВАН: читает MouseSensitivity из SettingsManager
docs/UI/esc-menu/
│   ├── 01_implementation_plan.md       # этот документ (rev.2)
│   └── 02_audit_notes.md               # НОВЫЙ: полный разбор кода и gaps
```

---

## 5. План выполнения по этапам

| # | Этап | Порядок | Зависит от | ~Оценка |
|---|---|---|---|---|
| 1 | Рефакторинг EscMenuWindow | **Первый** | Ничего | фундамент |
| 2 | UI-компоненты настроек + SettingsManager | **Второй** | Этап 1 | виджеты + данные |
| 3a | Графика | После 2 | Этап 2 | страница |
| 3b | Звук | После 2 | Этап 2 | страница |
| 3c | Геймплей | После 2 | Этап 2 | страница |
| 3d | Управление (Keybindings sub-page) | После 2 | Этап 2 | интеграция |
| 4 | Выход в меню | После 1 | Этап 1 | кнопка |
| 5 | Анимации | **Последний** | Этап 1 | polish |

**Параллельно можно:** 3a, 3b, 3c — независимы друг от друга. 3d — после 2.

**Зависимости по файлам:**
- `SettingsManager` — НИ ОТ ЧЕГО не зависит (чистый static). Можно делать параллельно с Этапом 1.
- `WorldCamera` / `ThirdPersonCamera` — модификация только после готовности SettingsManager.
- KeybindingsWindow-in-subpage — после рефакторинга EscMenuWindow.

---

## 6. Известные ограничения (open issues)

| # | Ограничение | Статус | Когда решаем |
|---|---|---|---|
| OI-01 | AudioMixer не создан, разделение каналов невозможно | Принято | Внедрить AudioMixer при звуковом пасс |
| OI-02 | Language (локализация) — нет инфраструктуры | Отложено | Отдельный тикет post-MVP |
| OI-03 | MainMenu.unity не существует, используется BootstrapScene reload | Временно | Когда появится MainMenu — заменить |
| OI-04 | RebindPromptWindow использует антипаттерн Clear+CloneTree | Зафиксировано, вне scope | Рефакторинг при следующем проходе по UI |
| OI-05 | PlayerInputReader имеет dead mouseSensitivity поля (#pragma 0414) | Косметика | Удалить при рефакторинге PlayerInputReader |

---

## 7. Чек-лист приёмки

- [ ] Нажатие Esc открывает меню (если ничего не открыто)
- [ ] Esc на главном экране → закрывает меню
- [ ] Esc на подменю → возврат на уровень выше
- [ ] Кнопка «ПРОДОЛЖИТЬ» закрывает меню
- [ ] Все настройки сохраняются в PlayerPrefs
- [ ] Графика: смена качества/разрешения/VSync применяется сразу
- [ ] Звук: громкость меняется через AudioListener.volume
- [ ] Чувствительность мыши: меняется в реальном времени (WorldCamera + ThirdPersonCamera)
- [ ] Инвертировать Y: работает в обеих камерах
- [ ] Субтитры: on/off (сохранение, сам функционал — отдельный тикет)
- [ ] KeybindingsWindow открывается как sub-page внутри EscMenu, а не как отдельное окно
- [ ] Кнопка «ВЫХОД В МЕНЮ» показывает диалог подтверждения
- [ ] При подтверждении: NetworkManager.Shutdown() + LoadScene("BootstrapScene")
- [ ] UIManager.HandleGlobalInput() — изменён минимально (+3 строки, делегация в EscMenuWindow)
- [ ] Clear() + CloneTree() антипаттерн устранён в EscMenuWindow
- [ ] EscMenuPanelSettings — не изменён (themeUss задан ✅)
- [ ] Language — НЕ реализован (DEFERRED, нет инфраструктуры)
