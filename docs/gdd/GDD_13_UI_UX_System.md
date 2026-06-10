# GDD-13: UI/UX System — Project C: The Clouds

**Версия:** 2.1 | **Дата:** 10 июня 2026 г. (дизайн-контент без изменений с 12 апреля 2026 г.; добавлена §X «Реализация в коде») | **Статус:** 🟢 Спринты 1-3 завершены + CharacterWindow v2 + DialogWindow + QuestTracker + QuestToast + MetaRequirementToast (2026-06-05..09)
**Автор:** Qwen Code (Game Studio: @ui-programmer + @ux-designer + @art-director) — дизайн, Mavis 2026-06-10 — раздел реализации

---

## 1. Overview

UI/UX система Project C: The Clouds включает HUD-элементы, сетевые панели, круговой инвентарь, торговлю и навигацию по пикам. Визуальный стиль соответствует **Sci-Fi + Ghibli** эстетике — мягкие цвета, градиенты, объёмный свет.

### Ключевые особенности
- **ControlHintsUI** — подсказки управления (левый верхний угол, F1 toggle)
- **NetworkUI** — панель подключения (Disconnect/Reconnect/Player Count)
- **InventoryUI** — круговое колесо (8 секторов, semantic labels, GL-рендер)
- **TradeUI** — торговля (TextMeshPro, UITheme, UIFactory)
- **ContractBoardUI** — контракты НП (TextMeshPro, UITheme, UIFactory)
- **PeakNavigationUI** — навигация по пикам (dev tool, скрыт в production)
- **UIManager** — централизованный менеджер UI (приоритеты, z-ordering, input management)
- **UIFactory** — фабрика UI компонентов (8 методов, 0 дублирования)
- **UITheme** — ScriptableObject темы (51+ цвет централизован)

### Метрики качества (после Спринтов 1-3)
| Метрика | До | После | Улучшение |
|---------|-----|-------|-----------|
| Дублирование кода | ~120 строк | 0 | -100% |
| Хардкодные цвета | 51+ | 0 | -100% |
| UI frameworks | 2 (Text + TMP) | 1 (TMP) | -50% |
| Эмодзи в sci-fi UI | 6+ | 0 | -100% |
| Memory leaks | 3 | 1 | -66% |
| Общая оценка | 4.5/10 | 7/10 | +55% |

**Полный отчёт:** [`docs/QWEN-UI-AGENTIC-SUMMARY.md`](../QWEN-UI-AGENTIC-SUMMARY.md)

---

## 2. UI Architecture

### 2.1 Текущая архитектура (после Спринта 3)

```
UI System
├── UIManager (singleton, lifecycle management)
│   ├── OpenPanel(name, priority, onClose, panelGo)
│   ├── ClosePanel(name)
│   ├── CanReceiveInput(name) → bool
│   ├── PlayClick() / PlayError() / PlayOpen() / PlayClose()
│   │
│   └── Панели (стек по priority)
│       ├── TradeUI (200) — Торговля
│       ├── ContractBoardUI (300) — Контракты
│       ├── InventoryUI (400) — Инвентарь
│       └── ConfirmationDialog (999) — Подтверждения
│
├── UIFactory (shared components)
│   ├── CreatePanel(name, parent, x, y, w, h)
│   ├── CreateLabel(name, parent, text, fontSize, color)
│   ├── CreateButton(name, parent, label, onClick)
│   ├── CreateScrollArea(parent, out content)
│   ├── CreateDivider(parent, color)
│   ├── CreateEmptyRow(parent)
│   ├── CreateListRow(parent, text, index, onClick)
│   └── CreateRootCanvas(name)
│
├── UITheme (ScriptableObject, авто-создание)
│   ├── ColorPalette (PanelBackground, RowEven/Odd, Button*, Accent*, Text*)
│   ├── FontSizes (Heading: 22-24px, Body: 14-16px, Caption: 11-13px)
│   └── Spacing (PaddingSmall/Medium/Large, GapSmall/Medium/Large)
│
└── UI Panels
    ├── TradeUI (~1200 строк) — рынок/склад/трюм (TextMeshPro)
    ├── ContractBoardUI (~470 строк) — активные/доступные контракты (TextMeshPro)
    ├── InventoryUI (~280 строк) — круговое колесо (OnGUI + GL, semantic labels)
    ├── NetworkUI (~210 строк) — подключение/отключение (TextMeshPro)
    ├── ControlHintsUI (~130 строк) — подсказки клавиш (TextMeshPro)
    └── PeakNavigationUI (~130 строк) — навигация по пикам (dev, скрыт в builds)
```

### 2.2 Canvas структура

```
Canvas (Screen Space - Overlay)
├── ControlHintsUI (RectTransform, левый верхний)
│   ├── E — подобрать
│   ├── F — сесть/выйти
│   ├── Tab — инвентарь
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
├── InventoryUI (круговое колесо, центр)
│   ├── 8 секторов
│   ├── Hover подсписки
│   └── Flash эффект
│
└── PeakNavigationUI (правый верхний)
    ├── Текущий пик / Всего
    ├── Prev Button
    ├── Next Button
    └── Random Button
```

### RectTransform и якоря

| Элемент | Anchor | Pivot | Описание |
|---------|--------|-------|----------|
| ControlHintsUI | Top-Left | (0, 1) | Левый верхний угол |
| NetworkUI | Center | (0.5, 0.5) | Центр экрана |
| DisconnectBtn | Center | (0.5, 0.5) | Центр, Escape toggle |
| InventoryUI | Center | (0.5, 0.5) | Центр, Tab toggle |
| PeakNavigationUI | Top-Right | (1, 0) | Правый верхний угол |

### [🔴 Запланировано] Масштабирование

| Параметр | Описание |
|----------|----------|
| Canvas Scaler | Scale with screen size |
| Reference resolution | 1920x1080 |
| Match mode | 0.5 (width/height) |
| DPI scaling | [🔴 Запланировано] |

---

## 3. HUD Elements

### ControlHintsUI

| Элемент | Описание | Обновление |
|---------|----------|-----------|
| E — подобрать | Иконка + текст | Каждый кадр |
| F — сесть/выйти | Иконка + текст | Каждый кадр |
| Tab — инвентарь | Иконка + текст | Статический |
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

### [🔴 Запланировано] Дополнительные HUD элементы

| Элемент | Описание | Этап |
|---------|----------|------|
| Компас | Направление, маркеры | Этап 3 |
| Мини-карта | 2D карта мира | Этап 4 |
| Спидометр | Скорость корабля | Этап 2.5 |
| Топливо | Уровень мезия | Этап 2.5 |
| Здоровье | HP игрока | Этап 3 |

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

### Disconnect кнопка

| Параметр | Значение |
|----------|----------|
| Расположение | Центр экрана |
| Toggle | Escape |
| Создание | Программное (NetworkUI.cs:CreateDisconnectButton()) |
| ✅ Исправлено | Позиционирование по центру через якоря |

### Статус-индикаторы

| Статус | Цвет | Текст |
|--------|------|-------|
| Connected | Зелёный | "Подключено" |
| Connecting | Жёлтый | "Подключение..." |
| Disconnected | Красный | "Отключено" |
| Reconnecting | Оранжевый | "Реконнект (N/5)..." |
| Server Started | Синий | "Сервер запущен" |

---

## 5. Inventory UI

### Круговое колесо

| Параметр | Значение |
|----------|----------|
| Активация | Tab — toggle |
| Расположение | Центр экрана |
| Радиус | 120px |
| Секторы | 8 (по типам предметов) |
| Рендер | GL-линии |
| Цвет активного | `#4CAF50` (зелёный) |
| Цвет пустого | `#666666` (серый) |

### Поведение

| Действие | Эффект |
|----------|--------|
| Нажатие Tab | Открыть/закрыть |
| Hover на сектор | Подсветка |
| Сектор с >1 предметом | Подсписок |
| Получение предмета | Вспышка (0.5s) |
| Закрытие | Возврат в игру |

### [🔴 Запланировано] Улучшения

| Фича | Описание | Этап |
|------|----------|------|
| Иконки предметов | 128x128 PNG в секторах | Этап 2.5 |
| «Облачный» дизайн | Ghibli-эстетика, градиенты | Этап 3 |
| Слот 9 (центр) | Ключевой предмет | Этап 3 |
| Анимация открытия | Плавное появление | Этап 2.5 |
| Звук открытия | Звук при Tab | Этап 2.5 |

---

## 6. Control Mapping

| Клавиша | UI элемент | Действие | Приоритет |
|---------|-----------|----------|-----------|
| Tab | InventoryUI | Toggle кругового колеса | 400 |
| Escape | UIManager | Toggle верхней панели | Авто |
| E | ControlHintsUI / ContractBoardUI | Подбор / Открыть контракты | 300 |
| F | ControlHintsUI / TradeUI | Посадка/выход / Открыть торговлю | 200 |
| F1 | ControlHintsUI | Toggle подсказок | Static |
| N/B | PeakNavigationUI | Prev/Next пик | Dev only |
| R | PeakNavigationUI | Random пик | Dev only |

### Input Priority System (UIManager)

UIManager использует систему приоритетов для предотвращения конфликтов ввода:

```csharp
// Пример: если InventoryUI открыт (priority 400), TradeUI (200) не получает ввод
if (!UIManager.CanReceiveInput("TradeUI")) return;

// Escape автоматически закрывает панель с highest priority
// Cursor lock/unlock управляется через UIManager
```

| Панель | Priority | Описание |
|--------|----------|----------|
| TradeUI | 200 | Торговля |
| ContractBoardUI | 300 | Контракты (поверх TradeUI) |
| InventoryUI | 400 | Инвентарь (поверх контрактов) |
| ConfirmationDialog | 999 | Диалоги (поверх всего) |

---

## 7. Visual Style — Ghibli

### Цветовая палитра UI

| Элемент | Цвет | Описание |
|---------|------|----------|
| Фон HUD | `rgba(0, 0, 0, 0.6)` | Полупрозрачный чёрный |
| Текст HUD | `#FFFFFF` | Белый |
| Активный сектор | `#4CAF50` | Зелёный |
| Пустой сектор | `#666666` | Серый |
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

### Стиль элементов

| Параметр | Значение |
|----------|----------|
| Скругление кнопок | 8px |
| Прозрачность фона | 60% |
| Градиенты | [🔴 Запланировано] |
| Иконки | [🔴 Запланировано] |

---

## 8. Responsive Design

### Адаптация к разрешениям

| Разрешение | Масштаб UI | Описание |
|------------|-----------|----------|
| 1920x1080 | 1.0x | Базовое |
| 1280x720 | 0.8x | Уменьшение |
| 2560x1440 | 1.3x | Увеличение |
| 3840x2160 | 2.0x | [🔴 Запланировано] |

### [🔴 Запланировано] Поддержка

| Фича | Описание |
|------|----------|
| Canvas Scaler | Scale with screen size |
| Safe area | Учёт notch/rounded corners |
| Aspect ratio | Поддержка 16:9, 21:9, 4:3 |

---

## 9. Feedback Systems

### Визуальная обратная связь

| Действие | Эффект | Статус |
|----------|--------|--------|
| Подбор предмета | Вспышка сектора (0.5s) | ✅ |
| Открытие сундука | Анимация (поворот + масштаб) | ✅ |
| Hover кнопки | Изменение цвета | [🔴 Запланировано] |
| Подключение | Зелёный статус-текст | ✅ |
| Ошибка | Красный статус-текст | ✅ |
| Реконнект | Оранжевый «Реконнект (N/5)» | ✅ |

### [🔴 Запланировано] Звуковая обратная связь

| Действие | Звук | Этап |
|----------|------|------|
| Клик кнопки | Мягкий клик | Этап 2.5 |
| Подбор предмета | Звук подбора | Этап 2.5 |
| Открытие сундука | Звук открытия | Этап 2.5 |
| Ошибка | Низкий тон | Этап 2.5 |

---

## 10. Accessibility

| Параметр | Описание | Статус |
|----------|----------|--------|
| Размер текста | 18px — читаемо на 1080p | ✅ |
| Контраст | Белый на полупрозрачном чёрном | ✅ |
| Цветовая слепота | [🔴 Запланировано] Альтернативные индикаторы | 🔴 |
| Масштабирование | [🔴 Запланировано] Настройка размера UI | 🔴 |
| Переназначение клавиш | [🔴 Запланировано] | 🔴 |

---

## 11. Future UI

### [🔴 Запланировано] Этап 3-4

| Элемент | Описание | Этап |
|---------|----------|------|
| Меню паузы | Escape → Pause menu | Этап 3 |
| Карта мира | 2D/3D карта с маркерами | Этап 4 |
| Диалоги NPC | Окно диалога, варианты ответов | Этап 4 |
| Журнал квестов | Список квестов, прогресс | Этап 4 |
| Полный инвентарь | I — расширенный вид | Этап 3 |
| Настройки | Графика, звук, управление | Этап 3 |
| Главное меню | Start, Settings, Quit | Этап 2.5 |
| Экран загрузки | Логотип, загрузка мира | Этап 2.5 |

---

## 12. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | ControlHintsUI отображается | Запустить сцену | ✅ |
| 2 | NetworkUI работает | Connect/Disconnect | ✅ |
| 3 | Disconnect кнопка по центру | Escape toggle | ✅ |
| 4 | InventoryUI открывается | Tab, 8 секторов, semantic labels | ✅ |
| 5 | Hover подсвечивает сектор | Мышь на сектор | ✅ |
| 6 | Подсписки при >1 предмете | Hover на заполненный | ✅ |
| 7 | Вспышка при подборе | Подобрать предмет | ✅ |
| 8 | PeakNavigationUI работает | N/B/R кнопки (dev only) | ✅ |
| 9 | Player Count обновляется | Подключить клиента | ✅ |
| 10 | Reconnect кнопка работает | Обрыв → Reconnect | ✅ |
| 11 | TradeUI открывается | F на торговой локации | ✅ |
| 12 | TradeUI Buy/Sell работает | Клик по кнопкам, RPC проходит | ✅ |
| 13 | TradeUI TextMeshPro | Шрифты TMP, не legacy Text | ✅ |
| 14 | ContractBoardUI открывается | E на NPC агенте | ✅ |
| 15 | ContractBoardUI Tabs | Active/Available переключение | ✅ |
| 16 | ContractBoardUI TextMeshPro | Шрифты TMP, не legacy Text | ✅ |
| 17 | UIManager приоритеты | Открыть TradeUI + InventoryUI | ✅ |
| 18 | Escape закрывает верхнюю панель | Escape при открытых UI | ✅ |
| 19 | Cursor lock/unlock | Открыть/закрыть любой UI | ✅ |
| 20 | UITheme авто-создание | UITheme_Default.asset в Resources | ✅ |
| 21 | Эмодзи устранены | [Контракт] [Груз] [Срочный] | ✅ |
| 22 | Звуковая обратная связь | [🔴 Запланировано] | 🔴 Спринт 4 |
| 23 | Главное меню | [🔴 Запланировано] | 🔴 Этап 2.5 |
| 24 | Карта мира | [🔴 Запланировано] | 🔴 Этап 4 |
| 25 | Настройки | [🔴 Запланировано] | 🔴 Этап 3 |
| 26 | **CharacterWindow v2** (5+ табов, P-окно) | см. §X ниже | 🟢 DONE (2026-06-05) |
| 27 | **DialogWindow** (typewriter, F-skip) | см. §X ниже | 🟢 DONE (T-Q11c, 2026-06-08) |
| 28 | **QuestTracker** (HUD overlay) | см. §X ниже | 🟢 DONE (T-Q12, 2026-06-08) |
| 29 | **QuestToast** (уведомления) | см. §X ниже | 🟢 DONE (M15, 2026-06-09) |
| 30 | **ShipKeyToast** (физический ключ) | см. §X ниже | 🟢 DONE (R2-SHIP-KEY-001, 2026-06-06) |
| 31 | **MetaRequirementToast** (generic lock-key UI) | см. §X ниже | 🟢 DONE (R2-META-REQ-001, 2026-06-06) |

---

## X. Реализация в коде (v2, 2026-06-05..09)

> **Секция добавлена Mavis 2026-06-10.** Дизайн-контент (архитектура, Ghibli стиль, control mapping) остаётся в зоне game-designer'а. Здесь — **только статус реализации** новых UI окон и панелей.

### X.1 CharacterWindow v2 (2026-06-05) ✅

**Концепция:** P-окно, единый "личный кабинет" игрока, 5+ табов, по образцу MarketWindow (UI Toolkit, singleton, 4 FIX'ы сразу бесплатно).

**Табы (всего 5+):**

| # | Таб | Серверная сущность | Клиентская проекция | Статус |
|---|-----|--------------------|----------------------|--------|
| 1 | **Персонаж** | (none yet) | `CharacterStatsClientState` (новый) | ⏳ MVP-заглушка + хард-стат |
| 2 | **Корабль** | (none yet) | (read from `NetworkPlayer` local) | ⏳ MVP-заглушка + локальные данные |
| 3 | **Репутация** | `QuestServer` → `ReputationClientState` | `ReputationClientState` + `NpcAttitudeClientState` | 🟢 Реализовано (T-Q13) |
| 4 | **Инвентарь** | `InventoryServer` | `InventoryClientState` (single source of truth с TAB-колесо) | 🟢 Реализовано (sub_inventory-tab) |
| 5 | **Контракты / Квесты** | `ContractServer` + `QuestServer` | `ContractClientState` + `QuestClientState` | 🟢 Реализовано (T-Q11) |
| 6 | **Квесты (sub-tab)** | `QuestServer` | `QuestClientState` (4 под-секции: active/completed/failed/discovered) | 🟢 Реализовано (T-Q11) |

**Реализация:**
- ✅ `Assets/_Project/UI/Client/CharacterWindow.cs` (1345+ LOC) — singleton MonoBehaviour, scene-placed в `BootstrapScene`, DontDestroyOnLoad
- ✅ `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` (5+ tab sections)
- ✅ `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` (с `!important` для всех class-стилей — fix `UnityDefaultRuntimeTheme`)
- ✅ `Assets/_Project/UI/Resources/UI/CharacterWindowPanelSettings.asset` (dedicated PanelSettings)
- ✅ 4 FIX'ы применены: `pickingMode` toggle, `styleSheets.Add(uss)` в `EnsureBuilt`, cursor lock/unlock, `MarkDirtyRepaint + schedule.Execute(50ms)`
- ✅ Visual fix 2026-06-05: `characterWindowUss` привязан к правильному USS-ассету (был UXML-bug → все class-стили игнорировались)
- ✅ P-key для открытия (по решению пользователя 2026-06-05)

**Документация:** `docs/Character-menu/00_OVERVIEW.md` + `sub_inventory-tab/00_OVERVIEW.md` + `refactor_log_2026-06-05.md`.

### X.2 DialogWindow (T-Q11c, 2026-06-08) ✅

**Концепция:** UIDocument окно для диалогов с NPC, typewriter-эффект, F-skip, click-skip, ESC close.

**Реализация:**
- ✅ `Assets/_Project/Quests/UI/DialogWindow.cs` (311 LOC) — UIDocument pattern с 4 FIX'ами от MarketWindow
- ✅ `Assets/_Project/Quests/Resources/UI/DialogWindow.uxml` (root > panel > npc-name + text-scroll > text + options + toast)
- ✅ `Assets/_Project/Quests/Resources/UI/DialogWindow.uss`
- ✅ `Assets/_Project/Quests/Resources/UI/DialogPanelSettings.asset` (копия `MarketPanelSettings.asset` с `themeUss: UnityDefaultRuntimeTheme`)
- ✅ Scene binding в `BootstrapScene.unity` (SerializedObject: `m_PanelSettings` + `sourceAsset` + `dialogWindowUxml/uss`)
- ✅ `QuestServer.RequestEndConversationRpc`, stale session detection, null-safe `BuildDialogStep`, try-catch diagnostic
- ✅ `DialogStepDto.cs` — 3 DTO struct fix (writeback-паттерн для string)
- ✅ Stale `_currentStep` guard в `SendAdvance`
- ✅ **Typewriter** (T-Q12) — coroutine char-by-char, 40 chars/sec
- ✅ **F skip typewriter** — `PlayerInputReader.Instance?.OnModeSwitchPressed`
- ✅ **Click мышью** на body → skip
- ✅ **9 PERSISTENT BUGS lessons** — в `old_session_log/T-Q11b_c_session_log_2026-06-08.md`

**Документация:** `old_session_log/T-Q11b_c_session_log_2026-06-08.md`.

### X.3 QuestTracker (T-Q12, 2026-06-08) ✅

**Концепция:** HUD overlay (top-right) с отслеживаемым квестом — quest name + текущая цель + кнопка "Скрыть". Auto-hide когда нет tracked.

**Реализация:**
- ✅ `Assets/_Project/Quests/UI/QuestTracker.cs` (230 LOC) — singleton MonoBehaviour, scene-placed, DontDestroyOnLoad
- ✅ `Assets/_Project/Quests/Resources/UI/QuestTracker.uxml` (root > panel > name + objective + hide button)
- ✅ `Assets/_Project/Quests/Resources/UI/QuestTracker.uss` (top-right absolute, dark blue + green border, все `!important`)
- ✅ `Assets/_Project/Quests/Resources/UI/QuestTrackerPanelSettings.asset` (копия `DialogPanelSettings.asset`)
- ✅ Public API: `Track(questId)` / `Untrack()` / `Toggle(questId)`
- ✅ Subscribe `QuestClientState.OnSnapshotUpdated` → RefreshDisplay
- ✅ Lazy-subscribe в Update (auto-find QuestClientState если null)
- ✅ Auto-hide когда нет tracked
- ✅ Auto-untrack если quest удалён из snapshot
- ✅ Первая не-completed objective как текущая цель
- ✅ Track-кнопка в строках квестов в CharacterWindow (T-Q12 → T-Q11)

**Документация:** `old_session_log/T-Q12_DESIGN_NOTE.md`.

### X.4 QuestToast (M15, 2026-06-09) ✅

**Концепция:** runtime VisualElement, bottom-center, 2.5s display, queue-based (все reward'ы по очереди, не drop на cooldown).

**Реализация:**
- ✅ `Assets/_Project/UI/Toast/ToastKind.cs` (enum Info/Success/Warning/Error)
- ✅ `Assets/_Project/UI/Toast/ToastService.cs` (static Show + шорткаты)
- ✅ `Assets/_Project/UI/Toast/ToastUI.cs` (singleton MonoBehaviour, queue max 3, fade in 0.2s, visible 3s, fade out 0.5s)
- ✅ `Assets/_Project/UI/Resources/UI/ToastUI.uxml` + `.uss`
- ✅ `BootstrapScene.unity` — `[QuestToast]` GameObject (legacy `[ToastService]` удалён)
- ✅ Quest-specific events: "📜 Accepted: Демо: stage с onEnter", "💚 mira_01 +5", "💰 +200 CR", "✨ Найден квест: ..."
- ✅ **Queue fix (T-Q25)** — `_cooldown=0.3s` дропал reward'ы. Заменён на `System.Collections.Generic.Queue<string>` + `ProcessQueue()` coroutine

**Документация:** `old_session_log/M15_DESIGN_NOTE.md`.

### X.5 ShipKeyToast + MetaRequirementToast (R2-SHIP-KEY-001 + R2-META-REQ-001, 2026-06-06) ✅

**Концепция:** UI feedback при отказе в boarding/использовании — "Нужен ключ X для корабля Y" + список недостающих.

**Реализация:**
- ✅ `Assets/_Project/Scripts/Ship/Key/ShipKeyToast.cs` (UIDocument) — fade-out 3 сек, текст
- ✅ `Assets/_Project/Scripts/MetaRequirement/MetaRequirementToast.cs` (UIDocument) — generic: "X/N собрано" + список недостающих
- ✅ `Assets/_Project/UI/Resources/UI/ShipKeyPanelSettings.asset`
- ✅ `Assets/_Project/UI/Resources/UI/MetaRequirementPanelSettings.asset`
- ✅ `BootstrapScene.unity` — `[MetaRequirementToast]` GameObject (UIDocument + MetaRequirementToast)
- ✅ **TODO:** `ProgressInfo` UI (multi-item tooltip "3/5 ключей собрано") — TODO (см. `docs/MetaRequirement/50_KNOWN_ISSUES.md`)

### X.6 Что НЕ реализовано ⏳

| # | Задача | Milestone | Приоритет |
|---|---|---|---|
| 1 | Таб "Персонаж" (реальный, не заглушка) | post-MVP | 🟢 Low |
| 2 | Таб "Корабль" (реальный, не заглушка) | post-MVP | 🟢 Low |
| 3 | ServiceUI для `OpenService` dialog action | Future | 🟢 Low |
| 4 | `ProgressInfo` multi-item tooltip в MetaRequirementToast | M7+ | 🟠 MEDIUM |
| 5 | Display HUD репутации в header (deferred с T-Q10) | M5 | 🟢 Low |
| 6 | Звуковая обратная связь в UI | Sprint 4 | 🟡 Med |
| 7 | Локализация всех строк | post-MVP | 🟢 Low |

### X.7 Где смотреть актуальный статус

- **`docs/Character-menu/00_OVERVIEW.md`** + `refactor_log_2026-06-05.md` — CharacterWindow
- **`docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md`** — sub_inventory-tab
- **`docs/NPC_quests/old_session_log/T-Q11b_c_session_log_2026-06-08.md`** — DialogWindow 9 bugs
- **`docs/NPC_quests/old_session_log/T-Q12_DESIGN_NOTE.md`** — QuestTracker
- **`docs/NPC_quests/old_session_log/M15_DESIGN_NOTE.md`** — QuestToast
- **`docs/Ships/Key-subsystem/00_OVERVIEW.md`** — ShipKeyToast
- **`docs/MetaRequirement/00_OVERVIEW.md`** — MetaRequirementToast
- **`docs/MMO_Development_Plan.md`** §1.7 — общий план UI

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [CONTROLS.md](../CONTROLS.md) | [`docs/Character-menu/00_OVERVIEW.md`](../Character-menu/00_OVERVIEW.md) | [`docs/NPC_quests/old_session_log/`](old_session_log/) | [`docs/MetaRequirement/`](../MetaRequirement/)
