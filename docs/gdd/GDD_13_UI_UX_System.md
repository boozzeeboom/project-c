# GDD-13: UI/UX System — Project C: The Clouds

**Версия:** 3.0 | **Дата:** 14 июля 2026 г. | **Статус:** 🟢 Спринты 1-3 завершены + CharacterWindow v2 + DialogWindow + QuestTracker + QuestToast + CustomisationWindow + EscMenuWindow + SkillTreeWindow
**Автор:** Малков Леонид Андреевич

---

## 1. Overview

UI/UX система Project C: The Clouds включает HUD-элементы, CharacterWindow (с 7 табами), диалоговые окна, трекер квестов, Esc-меню, окно кастомизации, дерево навыков, торговлю и сетевую панель. Визуальный стиль соответствует **Sci-Fi + Ghibli** эстетике — мягкие цвета, градиенты, объёмный свет.

**Ключевые изменения v3.0:**
- ✅ Новые UI-окна на UI Toolkit (UXML+USS): CharacterWindow (7 табов), DialogWindow, QuestTracker, QuestToast, CustomisationWindow, SkillTreeWindow, EscMenuWindow
- ✅ Inventory UI — часть CharacterWindow (InventoryTab), не круговое колесо
- ✅ Устаревшие TradeUI/ContractBoardUI через UIFactory заменены на UI Toolkit-подсистемы
- ❌ Круговое колесо инвентаря (GL-рендер) удалено
- ❌ Эмодзи в sci-fi UI устранены

### Ключевые особенности
- **CharacterWindow v2** — P-окно, 7 табов (Персонаж/Корабль/Репутация/Контракты/Инвентарь/Квесты/Внешность/Навыки)
- **DialogWindow** — typewriter-эффект, F-skip, click-skip, ESC close
- **QuestTracker** — HUD overlay (top-right), auto-hide
- **QuestToast** — runtime VisualElement, queue-based, bottom-center
- **CustomisationWindow** — full-screen overlay, color pickers, presets
- **EscMenuWindow** — Settings/Controls/Quit + InputRebindingPanel
- **SkillTreeWindow** — интерактивный граф навыков (zoom/pan)
- **NetworkUI** — панель подключения (Disconnect/Reconnect/Player Count)
- **ControlHintsUI** — подсказки управления (левый верхний угол, F1 toggle)
- **PeakNavigationUI** — навигация по пикам (dev tool, скрыт в production)
- **UIManager** — централизованный менеджер UI (приоритеты, z-ordering, input management)
- **UITheme** — ScriptableObject темы (цвета, шрифты, отступы)

---

## 2. UI Architecture

### 2.1 Текущая архитектура (v3.0)

```
UI System
├── UIManager (singleton, lifecycle management)
│   ├── OpenPanel(name, priority, onClose, panelGo)
│   ├── ClosePanel(name)
│   ├── CanReceiveInput(name) → bool
│   ├── PlayClick() / PlayError() / PlayOpen() / PlayClose()
│   │
│   ├── Canvas-based панели (legacy, TextMeshPro)
│   │   ├── CharacterWindow (UI Toolkit) — открывается через P-key
│   │   ├── NetworkUI (Canvas) — подключение/отключение
│   │   ├── ControlHintsUI (Canvas) — подсказки клавиш
│   │   ├── PeakNavigationUI (Canvas) — навигация по пикам (dev)
│   │   ├── HUDManager (Canvas) — общий HUD
│   │   └── AltitudeUI (Canvas) — высота полёта
│   │
│   ├── UI Toolkit (UIDocument) панели
│   │   ├── CharacterWindow (BootstrapScene) — P-окно, 7 табов
│   │   ├── DialogWindow (BootstrapScene) — NPC диалоги
│   │   ├── QuestTracker (BootstrapScene) — HUD overlay
│   │   ├── QuestToast (BootstrapScene) — уведомления
│   │   ├── EscMenuWindow (BootstrapScene) — пауза/настройки
│   │   ├── CustomisationWindow — кастомизация внешности
│   │   ├── SkillTreeWindow — граф навыков
│   │   ├── CraftingWindow — крафтинг
│   │   └── ShipCargoConsoleWindow — грузовой отсек корабля
│   │
│   └── Toast-система
│       ├── ToastService (static) — шорткаты для тостов
│       ├── QuestToast — уведомления квестов
│       ├── ShipKeyToast — отказ в boarding
│       └── MetaRequirementToast — generic lock-key UI
│
├── UITheme (ScriptableObject, авто-создание)
│   ├── ColorPalette (PanelBackground, RowEven/Odd, Button*, Accent*, Text*)
│   ├── FontSizes (Heading: 22-24px, Body: 14-16px, Caption: 11-13px)
│   └── Spacing (PaddingSmall/Medium/Large, GapSmall/Medium/Large)
│
└── UI Toolkit Runtime (Assets/_Project/UI/Resources/UI/)
    ├── CharacterWindow.uxml/.uss (7 tab sections)
    ├── InventoryWheel.uxml/.uss (sub-inventory tab)
    ├── DialogWindow.uxml/.uss (Quests/)
    ├── QuestTracker.uxml/.uss (Quests/)
    ├── QuestToast.uxml/.uss (Quests/Toast/)
    ├── CustomisationWindow.uxml/.uss
    ├── SkillTreeWindow.uxml/.uss
    ├── CraftingWindow.uxml/.uss
    └── ShipCargoConsoleWindow.uxml/.uss
```

### 2.2 Canvas структура

```
Canvas (Screen Space - Overlay)
├── ControlHintsUI (RectTransform, левый верхний)
│   ├── E — подобрать
│   ├── F — сесть/выйти
│   ├── Tab — инвентарь (CharacterWindow)
│   └── Сундук иконка
│
├── NetworkUI (RectTransform, центр экрана)
│   ├── Connection Panel
│   │   ├── IP Field
│   │   ├── Port Field
│   │   ├── Connect Button
│   │   └── Start Server Button
│   ├── Disconnect Button (по центру, Escape toggle)
│   ├── Reconnect Button
│   └── Player Count
│
├── HUDManager (верх/низ экрана)
│   ├── Crosshair
│   ├── Interaction prompts
│   └── Context-sensitive HUD
│
├── AltitudeUI (левый нижний)
│   └── Высота полёта (метры)
│
└── PeakNavigationUI (правый верхний, dev only)
    ├── Текущий пик / Всего
    ├── Prev Button
    ├── Next Button
    └── Random Button
```

### 2.3 UIDocument (UI Toolkit) — BootstrapScene

Все UI Toolkit окна размещены как scene-placed GameObjects в BootstrapScene (DontDestroyOnLoad):

| Окно | GameObject | PanelSettings | Исходный код |
|------|-----------|---------------|-------------|
| CharacterWindow | `[CharacterWindow]` | CharacterPanelSettings.asset | `UI/Client/CharacterWindow.cs` |
| QuestTracker | `[QuestTracker]` | QuestTrackerPanelSettings.asset | `Quests/UI/QuestTracker.cs` |
| DialogWindow | `[DialogWindow]` | DialogPanelSettings.asset | `Quests/UI/DialogWindow.cs` |
| QuestToast | `[QuestToast]` | (встроенный) | `Quests/UI/QuestToast.cs` |
| EscMenuWindow | `[EscMenu]` | (встроенный) | `UI/EscMenu/EscMenuWindow.cs` |
| CustomisationWindow | `[CustomisationWindow]` | CustomisationPanelSettings.asset | `Customisation/UI/CustomisationWindow.cs` |
| SkillTreeWindow | `[SkillTreeWindow]` | SkillTreePanelSettings.asset | `Skills/UI/SkillTreeWindow.cs` |

---

## 3. HUD Elements

### ControlHintsUI

| Элемент | Описание | Обновление |
|---------|----------|-----------|
| E — подобрать | Иконка + текст | Каждый кадр |
| F — сесть/выйти | Иконка + текст | Каждый кадр |
| Tab — CharacterWindow | Иконка + текст | Статический |
| Сундук | Иконка | Статический |

| Параметр | Значение |
|----------|----------|
| Расположение | Левый верхний угол |
| Отступ | 20px от края |
| Шрифт | LiberationSans SDF |
| Размер | 18px |
| Цвет | Белый с тенью |

### PeakNavigationUI

| Элемент | Описание |
|---------|----------|
| Текущий пик | Название / номер |
| Всего пиков | Общее количество |
| Prev Button | Предыдущий пик |
| Next Button | Следующий пик |
| Random Button | Случайный пик |

| Параметр | Значение |
|----------|----------|
| Расположение | Правый верхний угол |
| Обновление | При переключении пика |
| Зависит от | WorldCamera.cs |
| Статус | 🔴 Dev tool, скрыт в production builds |

---

## 4. Network UI

### Панель подключения

| Элемент | Описание |
|---------|----------|
| IP Field | Поле ввода IP (default: 127.0.0.1) |
| Port Field | Поле ввода порта (default: 7777) |
| Connect Button | Подключение к серверу |
| Start Server Button | Запуск Host |
| Disconnect Button | Отключение (по центру, Escape toggle) |
| Reconnect Button | Повторное подключение |
| Status Text | Статус подключения |
| Player Count | Счётчик игроков |

### Статус-индикаторы

| Статус | Цвет | Текст |
|--------|------|-------|
| Connected | Зелёный | "Подключено" |
| Connecting | Жёлтый | "Подключение..." |
| Disconnected | Красный | "Отключено" |
| Reconnecting | Оранжевый | "Реконнект (N/5)..." |
| Server Started | Синий | "Сервер запущен" |

---

## 5. CharacterWindow v2 (UI Toolkit) ✅

### Концепция

P-окно, единый "личный кабинет" игрока. Открывается по клавише P. Реализован на UI Toolkit (UXML+USS) по паттерну с 4 FIX'ами.

### Табы (всего 7+)

| # | Таб | Серверная сущность | Клиентская проекция | Статус |
|---|-----|--------------------|----------------------|--------|
| 1 | **Персонаж** | (none yet) | `CharacterStatsClientState` | ⏳ MVP-заглушка + хард-стат |
| 2 | **Корабль** | (none yet) | (read from NetworkPlayer local) | ⏳ MVP-заглушка + локальные данные |
| 3 | **Репутация** | QuestServer → ReputationClientState | ReputationClientState + NpcAttitudeClientState | 🟢 Реализовано |
| 4 | **Инвентарь** | InventoryServer | InventoryClientState (single source of truth) | 🟢 Реализовано |
| 5 | **Контракты** | ContractServer | ContractClientState | 🟢 Реализовано (ContractsTab.cs) |
| 6 | **Квесты (sub-tab)** | QuestServer | QuestClientState (4 под-секции) | 🟢 Реализовано |
| 7 | **Внешность** | CustomisationClientState | CustomisationClientState → CustomisationWindow | 🟢 Реализовано |
| 8 | **Навыки** | NetworkSkillTree | SkillTreeWindow | 🟢 Реализовано |

### Реализация

- **Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (178KB, ~3200+ LOC)
- **UXML:** `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` (7 tab sections)
- **USS:** `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` (46KB, с `!important` для всех class-стилей)
- **PanelSettings:** `Assets/_Project/UI/Resources/UI/CharacterPanelSettings.asset`
- **Дочерние компоненты:**
  - `InventoryTab.cs` (48KB) — инвентарь, сортировка, бросок
  - `MyShipsTab.cs` (28KB) — список кораблей игрока
  - `ContractsTab.cs` (21KB) — контракты и квесты
  - `CustomDropdown.cs` (10KB) — кастомный выпадающий список

**4 FIX'а UI Toolkit:** `pickingMode` toggle, `styleSheets.Add(uss)` в `EnsureBuilt`, cursor lock/unlock, `MarkDirtyRepaint + schedule.Execute(50ms)`

---

## 6. [УДАЛЕНО] Inventory Wheel (Circular)

Круговое колесо инвентаря с GL-рендером **удалено**. Функциональность инвентаря перенесена в CharacterWindow → таб "Инвентарь" (InventoryTab). InventoryWheel.uxml/.uss в репозитории сохраняется как визуальный компонент sub-inventory tab, но отдельного InventoryUI (круговое колесо) больше нет.

**Замена:** `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` + `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` — используется внутри CharacterWindow.

---

## 7. DialogWindow ✅

### Концепция

UIDocument окно для диалогов с NPC. Typewriter-эффект (40 chars/sec), F-skip, click-skip, ESC close.

### Реализация

- **Файл:** `Assets/_Project/Quests/UI/DialogWindow.cs` (21KB, 311+ LOC)
- **UXML:** `Assets/_Project/Quests/Resources/UI/DialogWindow.uxml`
- **USS:** `Assets/_Project/Quests/Resources/UI/DialogWindow.uss`
- **PanelSettings:** `Assets/_Project/Quests/Resources/UI/DialogPanelSettings.asset`
- **Scene binding:** BootstrapScene (SerializedObject: m_PanelSettings + sourceAsset + dialogWindowUxml/uss)

**Ключевые особенности:**
- Typewriter coroutine char-by-char, 40 chars/sec
- F skip typewriter — `PlayerInputReader.Instance?.OnModeSwitchPressed`
- Click мышью на body → skip
- `QuestServer.RequestEndConversationRpc`, stale session detection
- `DialogStepDto.cs` — 3 DTO struct fix (writeback-паттерн для string)
- 9 PERSISTENT BUGS lessons — в `old_session_log/T-Q11b_c_session_log_2026-06-08.md`

---

## 8. QuestTracker ✅

### Концепция

HUD overlay (top-right) с отслеживаемым квестом — quest name + текущая цель + кнопка "Скрыть". Auto-hide когда нет tracked.

### Реализация

- **Файл:** `Assets/_Project/Quests/UI/QuestTracker.cs` (10KB, 230+ LOC)
- **UXML:** `Assets/_Project/Quests/Resources/UI/QuestTracker.uxml`
- **USS:** `Assets/_Project/Quests/Resources/UI/QuestTracker.uss` (top-right absolute, dark blue + green border, `!important`)
- **PanelSettings:** `Assets/_Project/Quests/Resources/UI/QuestTrackerPanelSettings.asset`

**API:** `Track(questId)` / `Untrack()` / `Toggle(questId)` / Subscribe `QuestClientState.OnSnapshotUpdated`

---

## 9. QuestToast ✅

### Концепция

Runtime VisualElement, bottom-center, queue-based (все reward'ы по очереди, не drop на cooldown).

### Реализация

- **Файл:** `Assets/_Project/Quests/UI/QuestToast.cs` (12KB, 311+ LOC)
- **ToastService.cs** — static Show + шорткаты
- **ToastUI.cs** — singleton MonoBehaviour, queue max 3, fade in 0.2s, visible 3s, fade out 0.5s
- **BootstrapScene** — `[QuestToast]` GameObject

**Queue fix (T-Q25):** Использован `System.Collections.Generic.Queue<string>` + `ProcessQueue()` coroutine вместо cooldown-подхода.

---

## 10. ShipKeyToast + MetaRequirementToast ✅

| Компонент | Файл | Назначение |
|-----------|------|------------|
| ShipKeyToast | `Scripts/Ship/Key/ShipKeyToast.cs` | UI feedback при отказе в boarding |
| MetaRequirementToast | `Scripts/MetaRequirement/MetaRequirementToast.cs` | Generic lock-key UI: "X/N собрано" |
| ShipKeyPanelSettings | `Assets/_Project/UI/Resources/UI/ShipKeyPanelSettings.asset` | PanelSettings |
| MetaRequirementPanelSettings | `Assets/_Project/UI/Resources/UI/MetaRequirementPanelSettings.asset` | PanelSettings |

---

## 11. EscMenuWindow + InputRebinding ✅

### Концепция

Escape → EscMenu → Settings/Controls → InputRebinding → Listen+Assign+Save/Reset.

### Реализация

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `EscMenuWindow` | `Scripts/UI/EscMenu/EscMenuWindow.cs` (15KB) | Overlay-пауза: 3 кнопки (Settings, Controls, Quit) |
| `AudioSettingsSection` | `Scripts/UI/EscMenu/AudioSettingsSection.cs` | Настройки звука |
| `GraphicsSettingsSection` | `Scripts/UI/EscMenu/GraphicsSettingsSection.cs` | Настройки графики |
| `GameplaySettingsSection` | `Scripts/UI/EscMenu/GameplaySettingsSection.cs` | Настройки геймплея |
| `SettingsWidgets` | `Scripts/UI/EscMenu/SettingsWidgets.cs` | UI-виджеты для настроек |
| `InputBindingsConfig` (SO) | `Scripts/Input/InputBindingsConfig.cs` | 31 binding: move/action/combat/UI |
| `InputBindingsRuntime` | `Scripts/Input/InputBindingsRuntime.cs` | Runtime-привязка ввода |

**UX:**
1. Escape → EscMenu открывается, игра на паузе (Time.timeScale = 0)
2. "Settings" → Audio/Graphics/Gameplay секции
3. "Controls" → переназначение клавиш (Listen → Assign → Save/Reset)
4. "Quit" → Quit to Desktop

---

## 12. CustomisationWindow ✅

### Концепция

Full-screen overlay для изменения внешности персонажа. По паттерну SkillTreeWindow.

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `CustomisationWindow` | `Scripts/Customisation/UI/CustomisationWindow.cs` | Full-screen overlay |
| `CustomisationClientState` | `Scripts/Customisation/CustomisationClientState.cs` | Singleton: CurrentSnapshot, ApplyCustomisationSnapshot |

**Разделы UI:**
- Пол: Male / Female (радио-кнопки)
- Пресет тела: 6 вариантов (Default/Athletic/Heavy/Slim/Elder/Young)
- Цвет кожи: Color picker
- Цвет волос: Color picker
- Причёска: 2 стиля (Bald/Short, с preview)
- Цвет одежды: Color override

**Поток:** P → CharacterWindow → таб "Внешность" → CustomisationWindow → Apply → Broadcast через NetworkPlayer.

---

## 13. SkillTreeWindow ✅

### Концепция

UIDocument overlay-окно для интерактивного графа навыков: zoom/pan, node states, подсветка path.

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `SkillTreeWindow` | `Scripts/Skills/UI/SkillTreeWindow.cs` | Overlay UIDocument, 27+ skill nodes |
| `SkillNodeVisualElement` | `Scripts/Skills/UI/SkillNodeVisualElement.cs` | VisualElement per node: locked/unlocked/available/highlighted |
| `SkillGraphView` | `Scripts/Skills/UI/SkillGraphView.cs` | Zoom/pan, edge connections, node layout |
| `SkillTooltip` | `Scripts/Skills/UI/SkillTooltip.cs` | Hover tooltip: name, description, SP cost, effects |

**Ключевые решения:**
- `SkillTreeWindow` использует CharacterWindow паттерн (Clear+CloneTree+Add, Resources.Load fallback)
- Node states: Locked (серый) → Available (подсвечен) → Unlocked (зелёный)
- Привязан к сети через `NetworkSkillTree` (`NetworkVariable<SkillTreeSnapshot>`)
- 4 FIX UI Toolkit применены: pickingMode, styleSheets.Add, cursor lock, MarkDirtyRepaint

---

## 14. Control Mapping

| Клавиша | UI элемент | Действие | Приоритет |
|---------|-----------|----------|-----------|
| Tab | CharacterWindow (InventoryTab) | Toggle инвентаря | 400 |
| P | CharacterWindow | Toggle персонажа | 400 |
| Escape | EscMenuWindow | Toggle меню паузы | 999 |
| E | ControlHintsUI | Подбор / Взаимодействие | 300 |
| F | ControlHintsUI | Посадка/выход из корабля | 200 |
| F1 | ControlHintsUI | Toggle подсказок | Static |
| N/B | PeakNavigationUI | Prev/Next пик | Dev only |
| R | PeakNavigationUI | Random пик | Dev only |

### Input Priority System (UIManager)

UIManager использует систему приоритетов для предотвращения конфликтов ввода:

```csharp
// Пример: если EscMenu открыт (priority 999), другие панели не получают ввод
if (!UIManager.CanReceiveInput("CharacterWindow")) return;

// Escape автоматически закрывает панель с highest priority
// Cursor lock/unlock управляется через UIManager
```

| Система | Priority | Описание |
|---------|----------|----------|
| EscMenuWindow | 999 | Меню паузы (поверх всего) |
| ConfirmationDialog | 999 | Диалоги подтверждения |
| CustomisationWindow | 800 | Кастомизация |
| SkillTreeWindow | 700 | Дерево навыков |
| CharacterWindow | 400 | P-окно (инвентарь/персонаж) |
| DialogWindow | 350 | Диалоги NPC |
| Trade панели | 200 | Торговля |

---

## 15. Visual Style — Ghibli

### Цветовая палитра UI

| Элемент | Цвет | Описание |
|---------|------|----------|
| Фон HUD | `rgba(0, 0, 0, 0.6)` | Полупрозрачный чёрный |
| Текст HUD | `#FFFFFF` | Белый |
| Активный элемент | `#4CAF50` | Зелёный |
| Неактивный элемент | `#666666` | Серый |
| Кнопки | `#4FC3F7` | Голубой (антигравий) |
| Кнопки hover | `#81D4FA` | Светло-голубой |
| Disconnect | `#F44336` | Красный |
| Status Connected | `#4CAF50` | Зелёный |
| Status Error | `#F44336` | Красный |

### Шрифты

| Параметр | Значение |
|----------|----------|
| Основной | LiberationSans SDF |
| Размер HUD | 18px |
| Размер заголовков | 24px |
| Размер кнопок | 20px |
| Тень | Drop Shadow |
| Обводка | Outline |

---

## 16. Not Implemented ⏳

| # | Задача | Приоритет |
|---|--------|-----------|
| 1 | Таб "Персонаж" (реальный, не заглушка) | 🟢 Low |
| 2 | Таб "Корабль" (реальный, не заглушка) | 🟢 Low |
| 3 | `ProgressInfo` multi-item tooltip в MetaRequirementToast | 🟠 MEDIUM |
| 4 | Display HUD репутации в header | 🟢 Low |
| 5 | Звуковая обратная связь в UI | 🟡 Med |
| 6 | Локализация всех строк UI | 🟢 Low |
| 7 | SettingsWindow (графика/звук — не заглушка) | 🟢 Low |
| 8 | Keybindings → показать current key + conflict detection | 🟡 Med |
| 9 | Главное меню (Start, Settings, Quit) | 🟢 Low |
| 10 | Карта мира (2D/3D с маркерами) | 🔴 High |
| 11 | Мини-карта | 🔴 High |
| 12 | Компас (направление, маркеры) | 🟡 Med |
| 13 | Спидометр | 🟢 Low |

---

## 17. Где смотреть актуальный статус

- **`docs/Character-menu/00_OVERVIEW.md`** + `refactor_log_2026-06-05.md` — CharacterWindow
- **`docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md`** — sub_inventory-tab
- **`docs/NPC_quests/old_session_log/`** — DialogWindow, QuestTracker, QuestToast
- **`docs/Ships/Key-subsystem/00_OVERVIEW.md`** — ShipKeyToast
- **`docs/MetaRequirement/00_OVERVIEW.md`** — MetaRequirementToast
- **`docs/Character/Skills/20_IMPLEMENTATION.md`** — SkillTreeWindow
- **`docs/Character/Customisation/`** — CustomisationWindow
- **`docs/Character/Input/`** — InputRebinding + EscMenu
- **`docs/MMO_Development_Plan.md`** — общий план UI

---

## 18. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | ControlHintsUI отображается | Запустить сцену | ✅ |
| 2 | NetworkUI работает | Connect/Disconnect | ✅ |
| 3 | CharacterWindow открывается | P-key, 7 табов | ✅ |
| 4 | InventoryTab (в CharacterWindow) | Открыть таб Инвентарь | ✅ |
| 5 | DialogWindow работает | Поговорить с NPC | ✅ |
| 6 | Typewriter эффект | Начать диалог | ✅ |
| 7 | F-skip typewriter | Нажать F во время печати | ✅ |
| 8 | QuestTracker отображается | Отследить квест | ✅ |
| 9 | QuestToast показывает уведомления | Получить награду за квест | ✅ |
| 10 | EscMenu открывается | Escape | ✅ |
| 11 | CustomisationWindow | P → Внешность → Кастомизация | ✅ |
| 12 | SkillTreeWindow | P → Навыки | ✅ |
| 13 | Disconnect кнопка по центру | Escape → Disconnect | ✅ |
| 14 | Player Count обновляется | Подключить клиента | ✅ |
| 15 | Reconnect кнопка работает | Обрыв → Reconnect | ✅ |
| 16 | ShipKeyToast при отказе | Попытаться сесть в чужой корабль | ✅ |
| 17 | MetaRequirementToast | Попытаться использовать с blocked метой | ✅ |
| 18 | PeakNavigationUI работает | N/B/R кнопки (dev only) | ✅ |
| 19 | UIManager приоритеты | Открыть EscMenu + CharacterWindow | ✅ |
| 20 | Escape закрывает верхнюю панель | Escape при открытых UI | ✅ |
| 21 | Cursor lock/unlock | Открыть/закрыть любой UI | ✅ |
| 22 | UITheme авто-создание | UITheme_Default.asset в Resources | ✅ |

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [GDD_12_Network_Multiplayer.md](GDD_12_Network_Multiplayer.md) | [CONTROLS.md](../CONTROLS.md) | [docs/Character-menu/00_OVERVIEW.md](../Character-menu/00_OVERVIEW.md)
