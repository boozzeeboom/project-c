# CustomDropdown — замена DropdownField для полной стилизации USS

> **Тикет:** T-CARGO-UI-01-5
> **Автор:** Mavis
> **Дата:** 2026-07-02
> **Статус:** ✅ **СДЕЛАНО** (2026-07-02, ~3 ч)
> **Связано:** [SHIP_WINDOW.md](SHIP_WINDOW.md) — корабельная секция CharacterWindow

---

## TL;DR

`DropdownField` в Unity 6 runtime использует `GenericDropdownMenu` (не VisualElement) — выпадающий список **не стилизуется USS**. Написан `CustomDropdown` — полностью VisualElement-based, все части стилизуются через USS.

Заменены селекторы:
- CharacterWindow → таб «Корабль» (выбор своих кораблей) ✅
- MarketWindow → селектор корабля (выбор nearby ships) ✅
- CharacterWindow фильтры (`filter-source`, `filter-state`) — пропущены (сложная интеграция с ContractsTab/InventoryTab)

---

## 1. Архитектура

```
CustomDropdown (VisualElement)
├── .custom-dropdown__button (VisualElement, clickable)
│   ├── .custom-dropdown__text (Label — выбранное значение)
│   └── .custom-dropdown__arrow (Label — ▼)
└── .custom-dropdown__popup (VisualElement, overlay на main-container, position absolute)
    └── .custom-dropdown__item (Label × N — items списка)
```

### 1.1 Popup позиционирование

Popup добавляется на **`main-container`** (корень UIDocument), а не на `panel.visualTree`. Координаты:

```csharp
var mainContainer = FindMainContainer(); // parent-chain walk до main-container
var worldPos = _button.LocalToWorld(Vector2.zero);
var localPos = mainContainer.WorldToLocal(worldPos);
_popupContainer.style.left = localPos.x;
_popupContainer.style.top = localPos.y + btnHeight;
```

`FindMainContainer()` — ручной обход `parent`-цепочки, т.к. `GetFirstAncestorWhere()` не существует в Unity 6 (API появился позже).

### 1.2 Закрытие popup'а

При открытии регистрируется `PointerDownEvent` на `mainContainer` с `TrickleDown`. Проверка закрытия — через `_popupContainer.Contains(target)` (иерархия VisualElement, не координаты):

```csharp
if (target != null && _popupContainer.Contains(target)) return; // внутри popup — не закрываем
if (target != null && _button.Contains(target)) return;          // внутри кнопки — не закрываем
ClosePopup();
```

`Contains(target)` — проверка является ли target дочерним элементом popup'а (работает независимо от системы координат).

---

## 2. Проблемы и их решения

### 2.1 GenericDropdownMenu — не VisualElement

**Проблема:** `DropdownField` в Unity 6 runtime использует `GenericDropdownMenu` (наследует `AbstractGenericMenu`, не `VisualElement`). Popup — системный уровень, USS не применим.

**Решение:** `CustomDropdown` — полностью свой VisualElement.

### 2.2 overflow: hidden родителя обрезает popup

**Проблема:** `list-section` (родитель дропдауна) имеет `overflow: hidden`. Popup как дочерний элемент обрезается.

**Решение:** Popup добавляется на `main-container` (корневой контейнер без `overflow: hidden`), позиционируется абсолютно.

### 2.3 Двойной класс custom-dropdown

**Проблема:** UXML контейнер имел `class="custom-dropdown"` И `CustomDropdown` в конструкторе тоже добавляет этот класс. Двойное наслоение → min-height 48px вместо 24px, кривая верстка.

**Решение:** В UXML контейнер без класса. Только сам `CustomDropdown` добавляет `custom-dropdown`.

### 2.4 Координаты popup при закрытии

**Проблема (v1):** `OnRootPointerDown` использовал `evt.localPosition` (корневая система координат) для `ContainsPoint()`. Не совпадало с локальной системой popup'а → popup закрывался сразу после открытия.

**Решение:** Заменил координатную математику на `_popupContainer.Contains(target)` — проверка по иерархии VisualElement.

### 2.5 SetChoices сбрасывал выбранный индекс

**Проблема:** `SetChoices(list)` без `defaultIndex` всегда взывал `_selectedIndex = 0`. При каждом снепшоте рынка выбор юзера сбрасывался на первый корабль.

**Решение (v2):** Передавать текущий `_selectedShipIndex` как defaultIndex:
```csharp
_shipSelector.SetChoices(choices, _selectedShipIndex);
```

### 2.6 Initial selection не дёргал событие

**Проблема:** Старый `DropdownField.value = choices[0]` дёргал `RegisterValueChangedCallback`. Новый `SetSelectedIndex(0)` не дёргал `OnSelectionChanged` → cargo не подгружался при первом открытии.

**Решение:** `SetSelectedIndex(0, fireEvent: true)` при начальной загрузке.

---

## 3. C# API

```csharp
namespace ProjectC.UI.Client
{
    public class CustomDropdown : VisualElement
    {
        public event Action<int> OnSelectionChanged; // index выбранного

        public void SetChoices(List<string> choices, int defaultIndex = -1);
        //   defaultIndex = -1: если _choices пуст → _selectedIndex = 0,
        //   иначе сохраняет переданный defaultIndex.
        //   Для синхронизации с внешним selectedIndex передавать его явно.

        public void SetSelectedIndex(int index, bool fireEvent = false);
        public int SelectedIndex { get; }
        public string SelectedText { get; }

        public void Cleanup(); // закрыть popup при уничтожении окна
    }
}
```

---

## 4. Где используется

| Файл | Контекст | Состояние |
|---|---|---|
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/CustomDropdown.cs` | Реализация | ✅ |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` | Таб «Корабль» | ✅ |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | Стили для CharacterWindow | ✅ |
| `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` | Селектор корабля в рынке | ✅ |
| `Assets/_Project/Trade/Resources/UI/MarketWindow.uss` | Стили для MarketWindow | ✅ |

**Не заменены:** `filter-source` / `filter-state` в CharacterWindow (передаются в ContractsTab и InventoryTab) — сложная цепочка зависимостей, отложено.

---

## 5. Стили USS (актуальные)

Стили живут в двух USS файлах (одинаковые правила, отличаются размеры под контекст):

| Свойство | CharacterWindow (таб Корабль) | MarketWindow (селектор) |
|---|---|---|
| `.custom-dropdown__button` | `min-height: 24px; font-size: 13px` | `height: 26px; font-size: 12px` |
| `.custom-dropdown__text` | `font-size: 13px` | `font-size: 12px; line-height: 14px` |
| `.custom-dropdown__arrow` | `font-size: 10px` | `font-size: 9px` |

Оба используют `!important` для переопределения Unity runtime theme.

---

## 6. Трудности при реализации (pitfalls)

1. **Unity 6 API gaps** — `GetFirstAncestorWhere()` не существует. Ручной parent-chain walk.
2. **USS specificity** — Unity runtime theme имеет высокий приоритет, требуется `!important`.
3. **CustomDropdown не должен дублировать класс контейнера** — иначе двойные размеры.
4. **`containsPoint()` vs `Contains(target)`** — первый оперирует в локальной системе координат и легко ошибиться; второй надёжнее.
5. **`SetChoices` без defaultIndex = сброс** — всегда передавать текущий selectedIndex.
6. **FireEvent при SetSelectedIndex** — по умолчанию false, для initial selection нужно true.
7. **Popup на main-container, а не на panel.visualTree** — panel.visualTree может иметь другую систему координат.
8. **`evt.StopPropagation()` на item'ах** — обязательно, иначе `OnRootPointerDown` (TrickleDown) закроет popup до обработки клика по item'у.

---

## 7. История изменений

| Дата | Что |
|---|---|
| 2026-07-02 | T-CARGO-UI-01-5: CustomDropdown создан, заменён селектор в MyShipsTab |
| 2026-07-02 | Замена в MarketWindow ship-selector |
| 2026-07-02 | Документация + сводка трудностей |
