# GDD-13: UI/UX System — Project C: The Clouds

**Версия:** 2.0 | **Дата:** 12 апреля 2026 г. | **Статус:** ✅ Спринты 1-3 завершены, Спринт 4 (Polish) в ожидании
**Автор:** Qwen Code (Game Studio: @ui-programmer + @ux-designer + @art-director)

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

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [CONTROLS.md](../CONTROLS.md)
