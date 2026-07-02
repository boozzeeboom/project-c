# CustomDropdown — замена DropdownField для полной стилизации USS

> **Тикет:** T-CARGO-UI-01-5
> **Автор:** Mavis
> **Дата:** 2026-07-02
**Статус:** ✅ **СДЕЛАНО** (2026-07-02, ~3 ч)
**Связано:** [SHIP_WINDOW.md](SHIP_WINDOW.md) — корабельная секция CharacterWindow

---

## 1. Общее решение

Создать кастомный компонент `CustomDropdown` (VisualElement), который:

- **Кнопка** — `Label` + стрелка `▼`, клик → открывает popup
- **Popup** — `VisualElement` с `ListView`, состоящий из `Label`-items
- **Полностью стилизуемый USS** — все части VisualElement
- **Легковесный** — без лишних зависимостей, ~150 LOC
- **Переиспользуемый** — можно использовать в других окнах (MarketWindow и т.д.)

---

## 2. Структура UXML (визуальная)

```
CustomDropdown (VisualElement)
├── #dropdown-button (Label, clickable)        ← видимая кнопка с именем + ▼
└── #dropdown-popup (VisualElement, hidden)    ← popup-контейнер (overlay)
    └── ListView (#dropdown-list)               ← список choices
        └── Label (item)                        ← каждый choice
```

**Временно** создаётся programmatic (через C#), но может быть вынесен в UXML.

---

## 3. Классы USS

| Класс | Назначение |
|---|---|
| `.custom-dropdown` | Корневой контейнер (flex-row, выравнивание) |
| `.custom-dropdown__button` | Кнопка (текст + стрелка inline) |
| `.custom-dropdown__text` | Текст выбранного значения |
| `.custom-dropdown__arrow` | Стрелка `▼` |
| `.custom-dropdown__popup` | Popup-контейнер (overlay, position absolute) |
| `.custom-dropdown__popup .custom-dropdown__item` | Item в списке (Label) |
| `.custom-dropdown__popup .custom-dropdown__item:hover` | Hover, active |

---

## 4. C# API

```csharp
namespace ProjectC.UI.Client
{
    public class CustomDropdown : VisualElement
    {
        // === События ===
        public event Action<int> OnSelectionChanged; // index выбранного

        // === Публичные методы ===
        public void SetChoices(List<string> choices);   // список имён
        public void SetSelectedIndex(int index);        // выбрать item
        public int SelectedIndex { get; private set; }
        public string SelectedText { get; }

        // === Factory ===
        public static CustomDropdown Create(List<string> choices, int defaultIndex, Action<int> onSelect);
    }
}
```

---

## 5. Логика

**Открытие:** клик по `#dropdown-button` → вычисляем положение на экране (через `parent.LocalToWorld`) → `#dropdown-popup` становится `position: absolute; display: flex` с правильными координатами.

**Закрытие:** клик вне popup'а → `RegisterCallback<PointerDownEvent>` на панели → закрыть; выбор item → `OnSelectionChanged` + закрыть.

**Scroll:** `ListView` внутри popup'а — если choices > 6, появляется скролл (через макс-высоту).

**Highlight:** выбранный item имеет класс `.selected`.

---

## 6. Интеграция в MyShipsTab

**Замена:** `DropdownField _selector` → `CustomDropdown _selector`.

**BuildUI (MyShipsTab.cs):**

```csharp
// было:
_selector = root.Q<DropdownField>("ship-selector");

// стало:
_selector = new CustomDropdown();
_selector.name = "ship-selector";
root.Q<VisualElement>("ship-selector-container").Add(_selector); // или вставить рядом
```

**RefreshShipList:**
```csharp
_selector.SetChoices(_choices);
_selector.SetSelectedIndex(_selectedIndex >= 0 ? _selectedIndex : 0);
_selector.OnSelectionChanged += OnSelectorChanged;
```

**OnSelectorChanged:**
```csharp
private void OnSelectorChanged(int index)
{
    _selectedIndex = index;
    RenderSelectedShip();
}
```

**UXML:** убрать `<ui:DropdownField name="ship-selector"/>`, вместо него `<ui:VisualElement class="custom-dropdown-wrapper" />`.

---

## 7. Стили (USS)

```css
.custom-dropdown {
    flex-direction: row;
    align-items: center;
    min-height: 24px;
    flex-shrink: 0;
}
.custom-dropdown__button {
    flex-direction: row;
    align-items: center;
    flex-grow: 1;
    padding: 2px 8px;
    background-color: rgba(30, 45, 70, 0.5);
    border-radius: 4px;
    cursor: pointer;
}
.custom-dropdown__text {
    flex-grow: 1;
    font-size: 13px;
    color: rgb(200, 220, 255);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
.custom-dropdown__arrow {
    font-size: 10px;
    color: rgb(160, 185, 220);
    margin-left: 6px;
    flex-shrink: 0;
}

/* Popup - overlay */
.custom-dropdown__popup {
    position: absolute;
    left: 0;
    top: 100%;
    min-width: 200px;
    max-height: 200px;
    background-color: rgba(25, 40, 65, 0.95);
    border-width: 1px;
    border-color: rgba(80, 100, 130, 0.4);
    border-radius: 4px;
    padding: 4px;
    overflow-y: auto;
    z-index: 100;
}
.custom-dropdown__item {
    padding: 6px 10px;
    font-size: 12px;
    color: rgb(200, 220, 255);
    border-bottom-width: 1px;
    border-bottom-color: rgba(80, 100, 130, 0.15);
    cursor: pointer;
}
.custom-dropdown__item:hover {
    background-color: rgba(60, 100, 160, 0.6);
}
.custom-dropdown__item.selected {
    background-color: rgba(50, 90, 150, 0.4);
    -unity-font-style: bold;
}
```

---

## 8. Файлы

| Файл | Действие |
|---|---|
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/CustomDropdown.cs` | Новый — кастомный дропдаун |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` | Изменить — убрать `DropdownField`, добавить `VisualElement.custom-dropdown-wrapper` |
| `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` | Изменить — стили `.custom-dropdown*`, удалить `.ship-selector` |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` | Изменить — `DropdownField` → `CustomDropdown` |
| `docs/UI/SHIP_WINDOW.md` | Обновить — custom dropdown секция |

---

## 9. Оценка

~2-3 часа (1 файл + интеграция + тест).

---

## 10. Verification (для пользователя)

**Compile:** `refresh_unity` → 0 errors. Проверить все окна где был `DropdownField` (CharacterWindow — таб Корабль).

**Play Mode:**
- CharacterWindow → таб Корабль → селектор показывает список кораблей
- Клик → popup тёмный, со скруглениями, hover подсветка
- Выбор → кнопка обновляется, UI перерисовывается
- Клик вне popup'а → popup закрывается
