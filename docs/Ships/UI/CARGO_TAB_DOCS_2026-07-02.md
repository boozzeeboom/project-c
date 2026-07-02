# Ship Cargo UI — CharacterWindow таб «Корабль»

> **Дата:** 2026-07-02
> **Тикеты:** T-CARGO-UI-01 (1..5)
> **Статус:** ✅ Завершено
> **Связанные доки:** `docs/UI/SHIP_WINDOW.md` (детальная архитектура), `docs/UI/CUSTOM_DROPDOWN_DESIGN.md` (CustomDropdown), `docs/Ships/cargo_system/CARGO_UI_01_DESIGN_2026-07-02.md` (диздок + реализация)

---

## 1. Что сделано

### 1.1. Сервер-push деталей груза (`ShipTelemetryState.cargoDetail[]`)

Добавлен массив `CargoDetailDto[32]` в `ShipTelemetryState`. Сервер (ShipController.UpdateTelemetryState, 5 Hz) резолвит displayName/weight/dangerous/fragile через `TradeItemDefinitionResolver.TryGet` и пушит в NetworkVariable. Клиент получает готовые данные — без Resources.Load на каждое открытие таба.

**Исправленные баги:**
- `cargoMax = 0` (был всегда 0) — теперь через `ShipCargoRegistry.GetEffectiveLimits()` (per-instance + модули)
- `cargoUsed = Items.Count` (число уникальных типов) → `ComputeTotalSlots()` (sum qty * slots)

**Файлы:**
- `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` — `CargoDetailDto`, cargoDetail[]
- `Assets/_Project/Scripts/Player/ShipController.cs` — populate cargoDetail в UpdateTelemetryState

### 1.2. Детальный список груза в MyShipsTab

`RenderCargoDetail(CargoDetailDto[])` показывает for each item: `displayName × qty (weight кг)`. Опасный/хрупкий груз — с warning-цветом фона и префиксом ⚠/❄.

Throttle `ShipTelemetryStateEqualsApprox` расширен для учёта cargoDetail[] (qty-only изменения не пропускались).

**Файлы:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs`
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — ScrollView `ship-cargo-scroll`
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — `.ship-cargo-row/.ship-cargo-name/.ship-cargo-qty`

### 1.3. Рефакторинг вёрстки

Было: одноколоночный layout с ProgressBar'ами и всем последовательно.

Стало:
- **Header** (10-15%): name + class inline, key-id, тонкая полоса топлива (8px)
- **2 колонки** (80-90%): левая = Груз (bar + ScrollView), правая = Модули (заглушка)
- **Footer** (5%): позиция + состояние inline, border-top
- **Кастомные бары** bg+fill вместо ProgressBar (точные, скруглённые, цветные)

### 1.4. CustomDropdown (замена DropdownField)

Проблема: `DropdownField` в Unity 6 runtime использует `GenericDropdownMenu` (AbstractGenericMenu, не VisualElement) — USS не стилизовал popup.

Решение: `CustomDropdown` — полностью VisualElement-based:
- Кнопка (`.custom-dropdown__button`) — Label + ▼, темная полупрозрачная, компактная
- Popup (`.custom-dropdown__popup`) — overlay на main-container, position absolute
- Items (`.custom-dropdown__item`) — тёмные, hover с подсветкой, scroll при >6
- Закрытие — клик вне popup (через `Contains(target)` на main-container)

**Файлы:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/CustomDropdown.cs` — NEW

---

## 2. Структура файлов

```
Assets/_Project/Scripts/UI/Client/CharacterWindow/
├── CharacterWindow.cs              (контроллер окна, 3474 LOC)
├── MyShipsTab.cs                   (таб «Корабль», 581 LOC)
├── CustomDropdown.cs               (NEW) кастомный дропдаун
├── ContractsTab.cs                 (таб «Контракты»)
└── InventoryTab.cs                 (таб «Инвентарь»)

Assets/_Project/UI/Resources/UI/
├── CharacterWindow.uxml            (шаблон окна)
├── CharacterWindow.uss             (стили)
└── CharacterPanelSettings.asset    (настройки панели)
```

---

## 3. Сводка изменений (git diff --stat)

| Файл | +/- |
|---|---|
| `MyShipsTab.cs` | +93/-73 |
| `CharacterWindow.uss` | +151/-73 |
| `CharacterWindow.uxml` | +22/-14 |
| `CustomDropdown.cs` | NEW |
| `ShipTelemetryState.cs` | +96/-29 |
| `ShipController.cs` | +57/-29 |

---

## 4. Документы

| Документ | Содержание |
|---|---|
| `docs/UI/SHIP_WINDOW.md` | Полная документация вкладки — структура, поток данных, USS, история |
| `docs/UI/CUSTOM_DROPDOWN_DESIGN.md` | Дизайн CustomDropdown |
| `docs/Ships/cargo_system/CARGO_UI_01_DESIGN_2026-07-02.md` | Диздок + реализация T-CARGO-UI-01 |
| `docs/Ships/cargo_system/CARGO_REMAINING_WORK_2026-07-02.md` | Сводный план оставшейся работы (ещё 3 эпика) |
| `docs/MMO_Development_Plan.md` | v0.0.36 — упоминание T-CARGO-UI-01 |

---

## 5. История изменений

| Дата | Что |
|---|---|
| 2026-07-02 | T-CARGO-UI-01-1: CargoDetailDto, server-push cargo list, фикс cargoMax |
| 2026-07-02 | T-CARGO-UI-01-2: вёрстка 2 колонки (груз/модули), компактный header |
| 2026-07-02 | T-CARGO-UI-01-3: кастомные бары bg+fill, стилизация DropdownField |
| 2026-07-02 | T-CARGO-UI-01-4: удалён дубликат имени, попытка стилизовать popup USS |
| 2026-07-02 | T-CARGO-UI-01-5: CustomDropdown VisualElement-based — полная стилизация USS вместо DropdownField |

---

## 6. Что осталось (будущие эпики)

| Эпик | Часы | Описание |
|---|---|---|
| T-CARGO-UI-02 | 4-6 | Cargo manager на корабле — UI консоль (Exchanger-стиль) для просмотра и операций с грузом вне рынка |
| T-CARGO-VIS-01 | 4-6 | 3D визуал наполнения трюма ящиками/контейнерами |
| T-CARGO-NPC-01 | 6-10 | Универсальная cargo для NPC-кораблей (те же TradeWorld API) |
