# Qty Row Refactor — дизайн-ноут

## Проблема

После внедрения кнопок ± в qty-row (R3-фича) перестали быть видны цифры в TextField количества:

- **«Белый квадратик»** — TextField рендерится на всю ширину контейнера (1487px вместо 50px)
- **Цифры не видны** — TextElement внутри TextField не наследует `color` от родителя, остаётся светлым на светлом фоне

**Первоисточник:** USS-стили (`MarketWindow.uss`) не применяются к MarketWindow — `root.styleSheets.count = 0`. Причина не установлена (Resources.Load + styleSheets.Add не отрабатывает, `<ui:Style src>` в UXML не подхватывается).

**До кнопок** qty-row содержал только Label + TextField. Без стилей он всё равно был узким (2 элемента), и визуально цифры просматривались. С 6 элементами (4 кнопки + Label + TextField) без width constraints каждый элемент растягивается, ломая layout.

## Текущее состояние (file:line)

- `MarketWindow.cs:233-234` — `_qtyField` / `_warehouseQtyField` находятся из UXML
- `MarketWindow.cs:1751-1772` — `ApplyQtyFieldStyle()` задаёт inline-стили (новый код)
- `MarketWindow.uss:96-134` — `.qty-row` / `.qty-field` / `.qty-btn` / `.unity-text-input` — стили, которые не применяются
- `MarketWindow.uxml:71-82` — разметка qty-row для рынка (market-qty-*)
- `MarketWindow.uxml:84-95` — разметка qty-row для склада (warehouse-qty-*)

## Решение

**Отказ от USS для qty-row.** Все стили задаём через C# inline на уже существующие элементы. USS-файл остаётся для других частей окна. qty-row стили дублировать не нужно — они живут только в C#.

### Новый метод: `SetupQtyRow()`

Вызывается из `EnsureBuilt()` после нахождения всех элементов.

Что делает:
1. **TextField (qty-field / warehouse-qty-field):** width=50, height=24, светло-серый фон, чёрный текст, border-radius
2. **Inner TextElement:** чёрный цвет текста (UI Toolkit не наследует)
3. **Кнопки ±:** width=height=22, border-radius=11 (круглые), цвет фона (красный/зелёный), цвет текста
4. **Label «Кол-во:»:** font-size=11, цвет

### Почему не чиним USS

Попытки загрузить USS заняли 40+ итераций. `Resources.Load` + `styleSheets.Add` не работают на rootVisualElement. Вероятная причина: StyleSheet из Resources загружается без asset path в Editor, и `styleSheets.Add()` игнорирует его. Эта проблема затрагивает только MarketWindow — другие UIDocument (DialogWindow, ShipHudPanel) имеют `styleSheets=1`. Менять механизм загрузки USS для всего проекта рискованно. Inline-стили для одного ряда — безопасное и быстрое решение.

## Скоуп

### Входит
- `MarketWindow.cs` — `SetupQtyRow()` + вызов в `EnsureBuilt()`
- Удаление `ApplyQtyFieldStyle()` (заменяется `SetupQtyRow()`)

### НЕ входит
- `MarketWindow.uss` — не трогаем (другие части окна могут зависеть от него)
- `MarketWindow.uxml` — не трогаем (разметка корректна)
- Другие табы (Contracts, Exchanger) — у них нет qty-field
- Другие UIDocument — не трогаем

## Файлы которые НЕ меняются

- `MarketWindow.uss`
- `MarketWindow.uxml`
- Любые файлы вне `MarketWindow.cs`

## Тест-план

1. **Play Mode** → открыть рынок
2. Цифра `1` в окошке количества — **чёрная на светло-сером фоне**
3. Кнопки ± — круглые, 22×22px, цветные (красные для минус, зелёные для плюс)
4. Кнопки ± работают (меняют значение в поле)
5. Warehouse-qty-row при переключении на склад — то же самое
