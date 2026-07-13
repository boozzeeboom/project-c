# EscMenu — Аудит кода и gaps (2026-07-13)

> **Дата:** 2026-07-13
> **Контекст:** Полный разбор существующего кода перед реализацией полноценного EscMenu.
> **Связан:** `01_implementation_plan.md` (rev.2)

---

## Scope аудита

Проверено 14 файлов в 7 директориях. Цель: верифицировать план `01_implementation_plan.md`, найти gaps между планом и реальностью.

---

## 1. Что есть (структура проекта)

### Создание окон (runtime)

Все UI-окна создаются в `NetworkManagerController.Awake()` (после NetworkManager):

```csharp
CreateEscMenuWindow();      // строка 668: [EscMenuWindow] DontDestroyOnLoad
CreateKeybindingsWindow();  // строка 711: [KeybindingsWindow] DontDestroyOnLoad
CreateUIManager();          // строка 727: [UIManager] DontDestroyOnLoad
```

`RebindPromptWindow` — создаётся динамически через `EnsureExists()` (не через NMC).

Паттерн создания:
```csharp
var go = new GameObject("[EscMenuWindow]");
DontDestroyOnLoad(go);
var doc = go.AddComponent<UIDocument>();
doc.panelSettings = Resources.Load<PanelSettings>("UI/EscMenuPanelSettings");
go.AddComponent<EscMenuWindow>();
```

### EscMenuWindow.cs (104 LOC)

- Использует **антипаттерн**: `Clear() + CloneTree() + Add()` (UI_TOOLKIT_GUIDE.md §2 Ошибка 4)
- `Resources.Load<StyleSheet>` fallback (BGU-003, переименован в EscMenuStyles, но паттерн остался)
- Единственная кнопка: "НАСТРОЙКИ" → закрывает EscMenu, открывает KeybindingsWindow как отдельное окно
- `_root.pickingMode = PickingMode.Ignore` когда закрыто, Position когда открыто

### EscMenuWindow.uxml (12 LOC)

```xml
<ui:UXML>
    <ui:VisualElement name="esc-menu-root" class="esc-root">
        <ui:VisualElement name="esc-window" class="esc-window">
            <ui:Label name="esc-title" text="МЕНЮ" class="esc-title" />
            <ui:VisualElement name="esc-buttons" class="esc-buttons">
                <ui:Label name="esc-settings-btn" text="НАСТРОЙКИ" class="esc-btn" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

### EscMenuStyles.uss (115 LOC)

- Окно: 360×280px, centered via `translate: -50% -50%`
- `!important` на всех свойствах ✅
- Стили скроллбаров (T-CARGO-UI-SCROLL)

### EscMenuPanelSettings.asset

- `themeUss` задан ✅ (не Ошибка 2)
- ScaleMode: 1 (ScaleWithScreenSize)
- ReferenceResolution: 1200×800

### UIManager.cs (393 LOC)

- `DefaultExecutionOrder(-200)` (заметка: в NMC написано -100, реально -200)
- `HandleGlobalInput()` — единственный Esc-handler в проекте
- Стек панелей: `List<UIPanelInfo>` с сортировкой по Priority
- Кursor: разблокирует при OpenPanel, блокирует при закрытии последней
- AudioSource для UI-звуков (поля AudioClip есть, но вызовов PlaySound нет)

### KeybindingsWindow.cs (301 LOC)

- Полный rebind UI: ListeningState, ApplyRebind, RebuildLists
- Esc внутри: если режим прослушивания → CancelListening, иначе SetOpen(false)
- **НЕ использует UIManager.ClosePanel** — сам закрывается в Update()
- Тоже использует антипаттерн Clear+CloneTree

---

## 2. Найденные GAPs

### GAP-01: Esc-handler vs submenu навигация (критический)

**Симптом:** UIManager.HandleGlobalInput() — единственный Esc-handler. При Esc:
1. Если `_openPanels.Count > 0` → `CloseTopPanel()` (закроет EscMenu целиком)
2. Submenu-навигация требует NavigateBack() при Esc

**Решение:** Добавить делегацию:
```csharp
// В HandleGlobalInput(), перед CloseTopPanel():
if (_openPanels.Count > 0 && _openPanels[0].PanelName == "EscMenu")
{
    ProjectC.UI.EscMenu.EscMenuWindow.Instance?.HandleGlobalEscape();
    return;
}
```

`EscMenuWindow.HandleGlobalEscape()`:
- depth > 1 → `NavigateBack()`
- depth == 1 → `Hide()` (текущее поведение)

### GAP-02: Размер окна

- Текущий: 360×280
- Нужен: ~480×440 с ScrollView внутри

### GAP-03: mouseSensitivity хардкод

| Файл | Строка | Код |
|---|---|---|
| WorldCamera.cs:444 | `currentX += _lookInput.x * mouseSensitivityX;` | `[SerializeField] private float mouseSensitivityX = 3f;` |
| WorldCamera.cs:445 | `currentY -= _lookInput.y * mouseSensitivityY;` | `[SerializeField] private float mouseSensitivityY = 3f;` |
| ThirdPersonCamera.cs:263 | `_yaw += _lookInput.x * mouseSensitivityX;` | `[SerializeField] private float mouseSensitivityX = 3f;` |
| ThirdPersonCamera.cs:264 | `_pitch -= _lookInput.y * mouseSensitivityY;` | `[SerializeField] private float mouseSensitivityY = 3f;` |
| PlayerInputReader.cs:20 | `[SerializeField] private float mouseSensitivityX = 2f;` | UNUSED (#pragma 0414) |

**Решение:** Создать SettingsManager, модифицировать WorldCamera и ThirdPersonCamera на чтение оттуда.

### GAP-04: AudioMixer не существует

- `.mixer` файлов в проекте нет
- `AudioListener.volume` нигде не используется
- Единственный AudioSource — в UIManager

**Решение:** `AudioListener.volume` для Master. AudioMixer — когда дойдём до звука.

### GAP-05: Нет GameEventBus

**Решение:** SettingsManager — статический класс со встроенными C# events. Не нужен отдельный event bus.

### GAP-06: Нет локализации

- Language dropdown требует: все строки UI, описания, диалоги, инвентарь
- В проекте — 0 строк кода локализации

**Решение:** DEFERRED.

### GAP-07: MainMenu не существует

- `MainMenu.unity` — нет в Scenes/
- `MainMenuSceneGenerator.cs` — Editor-скрипт, создаёт сцену по кнопке в меню
- Сейчас нет главного меню (игра стартует сразу в мир)

**Решение:** `SceneManager.LoadScene("BootstrapScene")` как reload/restart. Позже — отдельная MainMenu сцена.

### GAP-08: RebindPromptWindow — тот же антипаттерн

```csharp
// RebindPromptWindow.EnsureBuilt() строка 52-58
_doc.rootVisualElement.Clear();
_doc.rootVisualElement.styleSheets.Add(promptUss);
_root = promptUxml.CloneTree();
_root.style.position = Position.Absolute;
// ...
_doc.rootVisualElement.Add(_root);
```

**Решение:** Зафиксировано, вне scope этого плана. Рефакторинг при следующем проходе по UI.

### GAP-09: KeybindingsWindow — отдельное окно, не sub-page

Текущий flow (InitSettingsButton):
```csharp
SetOpen(false);
UIManager.Instance.ClosePanel("EscMenu");
KeybindingsWindow.Instance.Show(); // открывается как отд. окно (Priority 200)
```

Нужно: `EscMenuWindow.NavigateTo(keybindingsPage)` внутри того же EscMenu.

### GAP-10: Нет USS для виджетов настроек

В плане были CS-файлы, но не .uss. Нужен отдельный `EscMenuSettingsStyles.uss`.

---

## 3. Контекст: смежные системы

| Система | Файл | Релевантность |
|---|---|---|
| InputBindingsRuntime | `Scripts/Input/InputBindingsRuntime.cs` | Save/Load/Reset через PlayerPrefs — паттерн для SettingsManager |
| CustomDropdown | `Scripts/UI/Client/CharacterWindow/CustomDropdown.cs` | Готовый VisualElement-based dropdown, можно реиспользовать |
| UITheme | (не найден отдельный файл) | Immersive strategy упоминает — проверить при L3 |

---

## 4. История

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-07-13 | Аудит | Создан документ. 14 файлов, 10 gaps. |
