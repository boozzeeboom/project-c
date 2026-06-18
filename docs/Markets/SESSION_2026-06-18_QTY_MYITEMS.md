# Session Log: Qty Row Refactor + My Items Filter (2026-06-18)

## Проблемы

1. **«Белый квадратик»** — цифры количества не видны в TextField
   - USS-стили не применяются к MarketWindow (`styleSheets.count=0` на root)
   - TextField упирается во внутреннюю структуру TextInput/TextElement, не наследует цвет
   - Долгая диагностика (40+ итераций) — причина не установлена

2. **BindMarketRow читала из snapshot, а не из itemsSource**
   - При смене `itemsSource` (фильтр) `bindItem` продолжал читать из `snap.Value.items[index]`
   - Отфильтрованный список показывал НЕ те товары

3. **ListView Unity 6 не обновляет строки после смены itemsSource**
   - `Rebuild()` одного раза недостаточно
   - Фикс: `itemsSource = null; Rebuild(); itemsSource = filtered; Rebuild();`

## Решения

### 1. Qty Row: TextField → Label

**Причина:** UI Toolkit TextField имеет внутреннюю структуру TextInput → TextElement, которая не наследует `color` от родителя. Label работает напрямую — стили применяются сразу.

### 2. Qty Row: полный inline-стиль (C#, без USS)

Все стили задаются через `field.style.*` API в методе `SetupQtyRow()`:
- `StyleQtyLabel()` — 50×24px, светло-серый фон, чёрный жирный текст
- `StyleQtyBtn()` — круглые 22×22px, красные/зелёные
- `StyleQtyExtremeBtn()` — прямоугольные 32×22px, синеватые

Вызов из `EnsureBuilt()` после нахождения всех элементов.

### 3. MIN/MAX кнопки

Левее `-10` — **MIN** (устанавливает 1). Правее `+10` — **MAX** (устанавливает 999).
Для обоих qty-row (рынок + склад).

### 4. Зажим qty при действиях

В обработчиках `OnBuyClicked`, `OnSellClicked`, `OnLoadClicked`, `OnUnloadClicked`:
- qty зажимается `Mathf.Min(qty, maxAvailable)`
- Нет ошибки «не хватает» — выполняется с максимально возможным

| Действие | Максимум |
|----------|----------|
| Купить | `availableStock` |
| Продать | `FindWarehouseQty(warehouse, itemId)` |
| Погрузить | `wh.quantity` |
| Разгрузить | `it.quantity` |

### 5. My Items Filter (тоггл на вкладке Рынок)

Кнопка **«Показать мои товары»** / **«Показать все товары»** над списком товаров.

- Фильтр: `FindWarehouseQty(_marketWhCache, item.itemId) > 0`
- Кеш `_marketWhCache` из того же снепшота, что и `_marketItemsCache`
- При обновлении снепшота фильтр применяется повторно

## Изменённые файлы

| Файл | Изменения |
|------|-----------|
| `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml` | TextField → Label; добавлены MIN/MAX кнопки; my-items-toggle |
| `Assets/_Project/Trade/Resources/UI/MarketWindow.uss` | `.my-items-toggle`, `.qty-btn-min`, `.qty-btn-max` стили |
| `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` | SetupQtyRow/StyleQtyLabel/StyleQtyBtn/StyleQtyExtremeBtn; BindMarketRow fix; ApplyMarketFilter; qty clamp; MIN/MAX handlers |
| `docs/Markets/QTY_ROW_REFACTOR.md` | Дизайн-ноут рефакторинга |

## Что НЕ менялось

- Другие UIDocument (DialogWindow, CharacterWindow, CraftingWindow, etc.)
- Другие табы MarketWindow (Контракты, Обменник)
- `src/` (core math)
- `docs/gdd/` (GDD)
