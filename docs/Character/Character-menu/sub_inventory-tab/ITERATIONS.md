# Итерации — sub_inventory-tab

## Итерация от 2026-07-02

**Задача:** Добавить кнопку «БРОСИТЬ» в CharacterWindow → вкладка «Инвентарь» (аналог drop-btn из TAB-колеса).

**Изменения:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/InventoryTab.cs` — 4 правки:
  - `InventoryListItem` struct: добавлено поле `slotIndex`
  - `RefreshInventoryCache`: сохранение `first.slotIndex` в кэш
  - `MakeInventoryRow`: добавлена кнопка `row-drop-btn` с текстом «БРОСИТЬ»
  - `BindInventoryRow`: wiring кнопки (паттерн T-EV-002 через userData)
  - Новый метод `OnDropFromInventoryClicked` — вызывает `InventoryClientState.RequestDrop`
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — стили `.inventory-drop-btn` / `.inventory-drop-label` (красный аналог equip-btn)
- `docs/Character/Character-menu/sub_inventory-tab/00_OVERVIEW.md` — обновлён статус CharacterWindow.tab-inventory
- `docs/Character/Character-menu/sub_inventory-tab/ITERATIONS.md` — этот файл
