# KODA UI-AGENTIC SUMMARY

> Аудит UI-элементов проекта ProjectC_client  
> Дата: 2025-07  
> Режим: анализ без правок кода  
> Команда: ux-designer, ui-programmer, art-director

---

## 1. Реестр UI-компонентов

| # | Компонент | Файл | Тип | Фреймворк | Ввод |
|---|-----------|------|-----|-----------|------|
| 1 | ControlHintsUI | `Assets/_Project/Scripts/UI/ControlHintsUI.cs` | Overlay-подсказки | TMP + IMGUI | Клавиатура (F1) |
| 2 | InventoryUI | `Assets/_Project/Scripts/UI/InventoryUI.cs` | Круговое колесо (8 секторов) | GL + OnGUI | Клавиатура (Tab) + Мышь |
| 3 | PeakNavigationUI | `Assets/_Project/Scripts/UI/PeakNavigationUI.cs` | Список пиков + навигация | UGUI (Button, TMP) | Мышь |
| 4 | NetworkUI | `Assets/_Project/Scripts/UI/NetworkUI.cs` | Панель подключения | UGUI (Button, TMP, InputField) | Мышь + Клавиатура (Esc) |
| 5 | TradeUI | `Assets/_Project/Trade/Scripts/TradeUI.cs` | Панель торговли | UGUI (программное создание) | Клавиатура + Мышь |
| 6 | ContractBoardUI | `Assets/_Project/Trade/Scripts/ContractBoardUI.cs` | Доска контрактов | UGUI (программное создание) | Клавиатура + Мышь |

---

## 2. Оценка по ролям

### 2.1 UX-дизайнер

#### ControlHintsUI
- ✅ Понятная структура подсказок (персонаж / корабль / управление)
- ✅ F1 для toggle — стандартный паттерн
- ⚠️ **Нет gamepad-поддержки** — подсказки только для клавиатуры/мыши
- ⚠️ **Хардкод строк** — все подсказки захардкожены в `UpdateHints()`, нет локализации
- ⚠️ **FindAnyObjectByType<TextMeshProUGUI>** как fallback — может найти чужой TMP-элемент

#### InventoryUI
- ✅ Круговое колесо — понятный паттерн (GTA-стиль)
- ✅ 8 секторов = 8 типов предметов, логичная структура
- ✅ Вспышка секторов при получении предметов — хорошая обратная связь
- 🔴 **OnGUI/GL рендер** — устаревший подход, не масштабируется, не поддерживает стили
- 🔴 **Нет gamepad-навигации** — только мышь для выбора сектора
- ⚠️ **Нет drag-and-drop** или использования предмета из колеса
- ⚠️ **Хардкод "Инвентарь"** в центре — не локализовано
- ⚠️ **Подсписок при >1 предмете** — но нет выбора конкретного предмета из подсписка

#### PeakNavigationUI
- ✅ Простая и понятная навигация по пикам
- ✅ Авто-заполнение списка
- ⚠️ **Нет gamepad-навигации** — только мышь
- ⚠️ **FindAnyObjectByType<WorldGenerator>** каждый раз при `UpdateCurrentPeakText()` — дорого и ненадёжно
- ⚠️ **Хардкод строк** — `"{i + 1}. {peak.name} ({peak.height:F0}м)"` не локализовано

#### NetworkUI
- ✅ Чёткий поток: Host/Server/Client → Disconnect
- ✅ Автоматический показ/скрытие кнопок по состоянию
- ✅ Кнопка Reconnect при потере связи
- 🔴 **Программное создание Disconnect-кнопки** — модифицирует Canvas в рантайме, хрупко
- 🔴 **Модификация Canvas RectTransform в CreateDisconnectButton()** — может сломать другие элементы на Canvas
- ⚠️ **FindAnyObjectByType<Canvas>** — может найти не тот Canvas
- ⚠️ **Keyboard.current.escapeKey** в Update — нет абстракции ввода
- ⚠️ **Нет валидации порта** — `ushort.TryParse` без проверки диапазона
- ⚠️ **Хардкод "Disconnect"** на кнопке — не локализовано, смесь языков (RU/EN)

#### TradeUI
- ✅ Два режима: Рынок / Склад+Трюм — покрывает основные сценарии
- ✅ Дебаунс + _tradeLocked — защита от двойных RPC
- ✅ Клавиатурные шорткаты (1/2/L/U/T/Esc/R)
- 🔴 **Полностью программное создание UI** — 300+ строк BuildUI, невозможно редактировать визуально
- 🔴 **UnityEngine.UI.Text вместо TMP** — LegacyRuntime.ttf, нет поддержки кириллицы корректно, нет SDF
- 🔴 **Нет gamepad-навигации**
- ⚠️ **Синдром God Object** — TradeUI управляет: торговлей, складом, погрузкой, событиями рынка, контрактами
- ⚠️ **Debug-клавиша R** в проде — сбрасывает кредиты без защиты (#if !UNITY_EDITOR)
- ⚠️ **Хардкод всех строк** — нет локализации
- ⚠️ **SelectItem/SelectCargoItem** — индексация запутана (divider не увеличивает index)

#### ContractBoardUI
- ✅ Отдельный префаб от TradeUI — правильное разделение
- ✅ Активные + доступные контракты в одном списке
- ✅ Клавиатурная навигация (Up/Down/Enter/Shift+Enter/C)
- 🔴 **Полностью программное создание UI** — аналогично TradeUI
- 🔴 **UnityEngine.UI.Text вместо TMP** — LegacyRuntime.ttf
- 🔴 **Нет gamepad-навигации**
- ⚠️ **HighlightRow()** — перебирает ВСЕ дети _contentPanel, включая DividerRow без Image (пропускает, но тратит циклы)
- ⚠️ **_showActiveTab** — объявлен, но никогда не используется (`#pragma warning disable 0414`)
- ⚠️ **Хардкод строк** — нет локализации

---

### 2.2 UI-программист

#### Архитектурные проблемы

| Приоритет | Проблема | Файлы | Влияние |
|-----------|----------|-------|---------|
| 🔴 P0 | `FindAnyObjectByType` — 17+ вызовов по проекту | Все UI-скрипты | В мультиплеере может найти чужой объект; медленно |
| 🔴 P0 | `PlayerPrefs` для авторитетных данных | PlayerDataStore, Inventory, PlayerCreditsManager | Dedicated Server не работает; данные теряются при рестарте |
| 🔴 P0 | Программное создание UI без префабов | TradeUI, ContractBoardUI | Невозможно редактировать визуально; дублирование кода (MakeLabel/MakeBtn/CreatePanel — копипаст между TradeUI и ContractBoardUI) |
| 🔴 P0 | `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` | TradeUI, ContractBoardUI | Устаревший шрифт, проблемы с кириллицей, нет SDF-рендеринга |
| 🟡 P1 | Singleton-паттерн без DontDestroyOnLoad | TradeUI, ContractBoardUI | Утечки при смене сцен; Instance может указывать на уничтоженный объект |
| 🟡 P1 | Нет абстракции ввода | Все UI-скрипты | Прямое обращение к `Keyboard.current` — невозможно заменить на gamepad |
| 🟡 P1 | OnGUI/GL рендер | InventoryUI | Устаревший API, не работает с Canvas, проблемы при масштабировании |
| 🟡 P1 | Хрупкая индексация в списках | TradeUI, ContractBoardUI | DividerRow не увеличивает index → баги при навигации |
| 🟡 P1 | `_tradeLocked` не сбрасывается при дисконнекте | TradeUI | Если сервер не ответил — UI заблокирован навсегда |
| 🟡 P1 | Debug-клавиша R без `#if UNITY_EDITOR` | TradeUI | Игрок может случайно сбросить кредиты |
| 🟡 P1 | `PlayerTradeStorage` добавляется через `AddComponent` если не найден | TradeUI | Создаёт пустой компонент без данных |
| 🔵 P2 | Нет pooling для строк списка | TradeUI, ContractBoardUI | Destroy/Instantiate при каждом RenderItems() |
| 🔵 P2 | `GUIStyle` создаётся каждый кадр в OnGUI | InventoryUI | GC allocation каждый кадр |
| 🔵 P2 | Static `_glMaterial` в InventoryUI | InventoryUI | Не очищается при выгрузке сцены |

#### Код-дублирование

TradeUI и ContractBoardUI содержат идентичные методы:
- `CreatePanel()` — полностью идентичен
- `MakeLabel()` — полностью идентичен
- `MakeBtn()` — полностью идентичен
- `MakeDividerRow()` — полностью идентичен
- `MakeEmptyRow()` — полностью идентичен
- `HighlightRow()` — похожая логика
- `HandleInput()` — похожая структура

**Рекомендация:** Вынести в общий базовый класс `ProgrammaticUIPanel` или helper-утилиту.

---

### 2.3 Арт-директор

#### Визуальная согласованность

| Аспект | Статус | Примечание |
|--------|--------|------------|
| Цветовая палитра | ⚠️ Частично | TradeUI и ContractBoardUI используют схожие тёмные цвета, но ControlHintsUI и InventoryUI — совершенно другие |
| Шрифт | 🔴 Нет | ControlHintsUI/NetworkUI/PeakNavigationUI = TMP; TradeUI/ContractBoardUI = LegacyRuntime.ttf — два разных шрифта |
| Размеры шрифтов | ⚠️ | Нет единой типографики: 11-24px в разных местах без системы |
| Стиль кнопок | ⚠️ | TradeUI/ContractBoardUI: программные кнопки с одинаковым стилем; NetworkUI: Inspector-кнопки (нет гарантии соответствия) |
| Фоны панелей | ⚠️ | TradeUI: `0.04, 0.04, 0.07, 0.97`; ContractBoardUI: `0.03, 0.05, 0.08, 0.97` — похожи, но не идентичны |
| Анимации | 🔴 Нет | Нет переходов, анимаций открытия/закрытия панелей |
| Иконки | 🔴 Нет | Контракты используют emoji (📋⚡📝📦) вместо sprite-иконок |

#### Проблемы разрешения

- InventoryUI: радиусы колеса захардкожены (210px/70px) — не масштабируется с разрешением
- TradeUI/ContractBoardUI: CanvasScaler ScaleWithScreenSize 1920×1080 — ок, но программные элементы с абсолютными позициями могут ломаться
- ControlHintsUI: зависит от TMP-объекта в сцене — размер не контролируется

---

## 3. Сводная таблица проблем

| # | Приоритет | Категория | Проблема | Затронутые файлы |
|---|-----------|-----------|----------|------------------|
| 1 | 🔴 P0 | Архитектура | FindAnyObjectByType вместо реестра | Все UI |
| 2 | 🔴 P0 | Архитектура | PlayerPrefs для серверных данных | PlayerDataStore, Inventory, PlayerCreditsManager |
| 3 | 🔴 P0 | Качество | Программное создание UI без префабов | TradeUI, ContractBoardUI |
| 4 | 🔴 P0 | Качество | LegacyRuntime.ttf вместо TMP | TradeUI, ContractBoardUI |
| 5 | 🟡 P1 | Ввод | Нет gamepad-поддержки ни в одном UI | Все 6 компонентов |
| 6 | 🟡 P1 | Ввод | Прямой Keyboard.current без абстракции | Все UI |
| 7 | 🟡 P1 | Локализация | Все строки захардкожены на русском | Все 6 компонентов |
| 8 | 🟡 P1 | Архитектура | Код-дублирование (MakeLabel/MakeBtn/CreatePanel) | TradeUI, ContractBoardUI |
| 9 | 🟡 P1 | UX | Хрупкая индексация с DividerRow | TradeUI, ContractBoardUI |
| 10 | 🟡 P1 | UX | _tradeLocked не сбрасывается при дисконнекте | TradeUI |
| 11 | 🟡 P1 | UX | Debug R без #if UNITY_EDITOR | TradeUI |
| 12 | 🟡 P1 | UX | AddComponent<PlayerTradeStorage> как fallback | TradeUI |
| 13 | 🟡 P1 | Визуал | Нет анимаций открытия/закрытия | Все панели |
| 14 | 🟡 P1 | Визуал | Два разных шрифта (TMP vs LegacyRuntime) | Все UI |
| 15 | 🟡 P1 | Визуал | Emoji вместо sprite-иконок | ContractBoardUI |
| 16 | 🟡 P1 | Архитектура | Singleton без DontDestroyOnLoad | TradeUI, ContractBoardUI |
| 17 | 🟡 P1 | Качество | OnGUI/GL рендер устарел | InventoryUI |
| 18 | 🟡 P1 | UX | Модификация Canvas в рантайме | NetworkUI |
| 19 | 🔵 P2 | Производительность | Нет pooling для строк | TradeUI, ContractBoardUI |
| 20 | 🔵 P2 | Производительность | GUIStyle allocation каждый кадр | InventoryUI |
| 21 | 🔵 P2 | Производительность | Static _glMaterial не очищается | InventoryUI |
| 22 | 🔵 P2 | UX | _showActiveTab объявлен, но не используется | ContractBoardUI |
| 23 | 🔵 P2 | UX | InventoryUI: подсписок без выбора предмета | InventoryUI |

---

## 4. Соответствие pipeline team-ui

### Phase 1: UX Design — Статус
- ❌ Нет формализованных wireframes
- ❌ Нет user-flow документов
- ❌ Нет gamepad-маппинга
- ⚠️ Accessibility: нет text scaling, нет colorblind-режима, нет контраст-проверки

### Phase 2: Visual Design — Статус
- ❌ Нет art bible для UI
- ⚠️ Цвета определены инлайн, не централизованно
- ❌ Нет иконок (emoji вместо спрайтов)
- ❌ Нет анимаций

### Phase 3: Implementation — Статус
- ⚠️ UI не владеет game state — ✅ (правильно: только отображение + события)
- ❌ Нет локализации — все строки хардкод
- ❌ Нет поддержки gamepad
- ❌ Нет accessibility (text scaling, colorblind)
- ⚠️ Data binding: частичный — TradeUI/ContractBoardUI привязаны к данным через FindAnyObjectByType

### Phase 4: Review — Не проводился

### Phase 5: Polish — Не проводился

---

## 5. Рекомендации по приоритету

### Немедленные (P0)
1. **Заменить FindAnyObjectByType** на PlayerRegistry/ServiceLocator — критично для мультиплеера
2. **Заменить PlayerPrefs** на IPlayerDataRepository + SQLite — критично для Dedicated Server
3. **Мигрировать TradeUI/ContractBoardUI на TMP** — LegacyRuntime.ttf не поддерживает кириллицу корректно
4. **Вынести общий код** TradeUI/ContractBoardUI в базовый класс — устранить 200+ строк дублирования

### Следующий спринт (P1)
5. **Добавить абстракцию ввода** (InputAction или интерфейс IUIInput) — подготовка к gamepad
6. **Реализовать gamepad-навигацию** для всех панелей
7. **Создать систему локализации** — таблицы строк, ключи вместо хардкода
8. **Удалить debug-клавишу R** или обернуть в `#if UNITY_EDITOR`
9. **Добавить сброс _tradeLocked** при дисконнекте/таймауте
10. **Заменить OnGUI/GL** в InventoryUI на UGUI/Canvas
11. **Создать UI Art Bible** — типографика, палитра, стили кнопок

### Когда будет время (P2)
12. Object pooling для списков TradeUI/ContractBoardUI
13. Анимации открытия/закрытия панелей
14. Sprite-иконки вместо emoji
15. Accessibility: text scaling, colorblind режим

---

## 6. Метрики

| Метрика | Значение |
|---------|----------|
| UI-компонентов | 6 |
| FindAnyObjectByType вызовов | 17+ |
| PlayerPrefs-ключей | 6+ форматов |
| Строк хардкода (русский) | ~60+ |
| Строк дублирования кода | ~200+ |
| Gamepad-совместимых панелей | 0 / 6 |
| Панелей с локализацией | 0 / 6 |
| Панелей с TMP | 3 / 6 |
| Панелей с анимациями | 0 / 6 |
| Панелей с accessibility | 0 / 6 |

---

*Документ сгенерирован Koda team-ui audit. Без правок кода.*
