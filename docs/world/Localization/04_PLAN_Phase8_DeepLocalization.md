# План: Глубокая локализация — Phase 8 (LOC-11)

> **Статус:** ПЛАН
> **Дата:** 2026-08-06
> **Контекст:** Фазы 0–7 закрыли инфраструктуру, но ~80% UI-строк остались хардкожены. При переключении языка не переведены: EscMenu (главные кнопки), CharacterWindow (целиком), InventoryTab, ContractsTab, MyShipsTab, MarketWindow, RepairManager.

---

## Шаг 0. Пререквизит: убедиться что SharedData修复лен

**Проверить:** запущен ли `ProjectC → Localization → Rebuild Tables (SharedData fix)` после написания `LocalizationTableRepair.cs`. Если нет — запустить.

**Проверка:** `Window → Asset Management → Localization Tables` → все 4 коллекции показывают ключи > 0. Нативный CSV Export даёт непустой файл.

---

## Шаг 1. Расширить UI_Table новыми ключами

Добавить в `LocalizationTableRepair.UIKeyMap` (и в саму `UI_Table_ru`) **~80 новых ключей**:

### 1a. EscMenu — главные кнопки + хардкод
```
ui.esc_menu.button.continue     → "ПРОДОЛЖИТЬ"
ui.esc_menu.button.settings     → "НАСТРОЙКИ"
ui.esc_menu.button.rescue       → "СПАСЕНИЕ"
ui.esc_menu.button.exit         → "ВЫХОД В МЕНЮ"
ui.esc_menu.root_title          → "МЕНЮ"
```

### 1b. CharacterWindow — фильтры и статусы
```
ui.character.filter.all         → "Все"
ui.character.filter.contracts   → "Контракты"
ui.character.filter.quests      → "Квесты"
ui.character.filter.active      → "Активные"
ui.character.filter.available   → "Доступные"
ui.character.filter.completed   → "Завершённые"
ui.character.filter.all_types   → "Все типы"
ui.character.player_owner       → "Игрок (Owner)"
ui.character.player             → "Игрок"
ui.character.no_data            → "—"
ui.character.bonuses            → "Бонусы: "
ui.character.equip              → "НАДЕТЬ"
ui.character.unequip            → "СНЯТЬ"
ui.character.drop               → "БРОСИТЬ"
ui.character.no_reputation      → "Нет данных о репутации"
ui.character.no_attitude        → "Нет данных об отношениях"
ui.character.factions_count     → "Фракций: {0}"
ui.character.attitudes_count    → "Отношений: {0}"
ui.character.no_contracts       → "Нет активных или доступных контрактов"
ui.character.active_available   → "Активных: {0} | Доступно: {1}"
ui.character.select_item_left   → "Выберите предмет слева"
ui.character.select_contract    → "Выберите контракт из списка"
ui.character.contract_unavailable → "Этот контракт уже не доступен для принятия"
ui.character.contract_not_active  → "Этот контракт не активен"
ui.character.request_sent       → "Запрос отправлен..."
ui.character.no_active_contracts → "Нет активных контрактов"
ui.character.active_count       → "Активных: {0}"
```

### 1c. Quest states
```
ui.quest.state.discovered       → "ОБНАРУЖЕН"
ui.quest.state.offered          → "ПРЕДЛОЖЕН"
ui.quest.state.active           → "АКТИВЕН"
ui.quest.state.completed        → "ВЫПОЛНЕН"
ui.quest.state.turned_in        → "СДАН"
ui.quest.state.failed           → "ПРОВАЛЕН"
ui.quest.track                  → "Следить"
ui.quest.untrack                → "Не следить"
ui.quest.discovered_unavailable → "Список найденных квестов недоступен"
ui.quest.reject_unavailable     → "Отказ от квеста пока не реализован (ждёт серверную часть)"
```

### 1d. Skills
```
ui.skill.learn                  → "Изучить"
ui.skill.forget                 → "Забыть"
```

### 1e. Contract types + ranks
```
ui.contract.type.standard       → "Обычный"
ui.contract.type.urgent         → "Срочный"
ui.contract.type.receipt        → "Квитанция"
ui.contract.rank.primium        → "Примум"
ui.contract.rank.secundus       → "Секундус"
ui.contract.rank.tertius        → "Терциус"
ui.contract.rank.quartus        → "Квартус"
```

### 1f. MyShipsTab
```
ui.ship.no_ships                → "Нет доступных кораблей. Найдите ключ в мире."
ui.ship.hull_broken             → "Прочность: СЛОМАН"
ui.ship.hull_empty              → "Прочность: —"
ui.ship.fuel_empty              → "Топливо: —"
ui.ship.cargo_empty             → "Груз: — (нет данных)"
ui.ship.modules_zero            → "Модулей: 0"
ui.ship.hold_empty              → "Трюм пуст"
```

### 1g. MarketWindow
```
ui.market.loading               → "Загрузка рынка..."
ui.market.no_data               → "Нет данных о рынке"
ui.market.server_unavailable    → "Сервер обменника не доступен"
ui.market.server_not_ready      → "Сервер обменника не инициализирован. Подождите пару секунд."
ui.market.select_left           → "Выберите предмет в левом списке"
ui.market.select_right          → "Выберите товар в правом списке"
ui.market.show_all              → "Показать все товары"
ui.market.show_mine             → "Показать мои товары"
ui.market.select_ship_first     → "Сначала выберите корабль"
ui.market.no_contracts_here     → "Нет контрактов на этой локации"
ui.market.op.buy                → "Куплено"
ui.market.op.sell               → "Продано"
ui.market.op.load               → "Погрузка"
ui.market.op.unload             → "Разгрузка"
```

### 1h. ShipCargoConsoleWindow
```
ui.cargo.select_inventory       → "Выберите предмет в инвентаре"
ui.cargo.select_hold            → "Выберите ящик в трюме"
ui.cargo.server_unavailable     → "Сервер грузового отсека не доступен"
ui.cargo.unpack_unavailable     → "Распаковка недоступна: нет курса обмена для этого товара"
```

**Метод:** расширить `UIKeyMap` в `LocalizationTableRepair.cs` → запустить `Rebuild Tables` → проверить `SharedData.Entries`.

---

## Шаг 2. Привязать Loc в коде

### 2a. EscMenuWindow.cs
- `WireRootButtons()`: после `clicked +=` добавить `Loc.Bind(button, key)` для 4 главных кнопок
- `NavigateBack()` стр.233: `"МЕНЮ"` → `Loc.Get("ui.esc_menu.root_title")`
- `NavigateToRoot()` стр.253: `"МЕНЮ"` → `Loc.Get("ui.esc_menu.root_title")`

### 2b. CharacterWindow.cs
- Все хардкоженные русские строки заменить на `Loc.Get(...)`:
  - `_contractFilterSourceOptions`, `_contractFilterStateOptions`, `_inventoryFilterStateOptions` — хранить ключи, показывать через `Loc.Get`
  - `_statName.text` стр.1031-1032: `"Игрок (Owner)"` / `"Игрок"` / `"—"`
  - `_messageLabel.text` стр.1157-1158, 1169-1170, 1857-1859
  - `ClearInventoryDetail` стр.1507
  - `"Бонусы: "` стр.1545, 1556
  - `"НАДЕТЬ"` / `"СНЯТЬ"` стр.1586, 2853
  - `"Следить"` / `"Не следить"` стр.3088, 3132
  - `QuestStateToBadge` стр.3307-3312
  - `"Изучить"` / `"Забыть"` стр.2539
  - Все `SetMessage(...)` вызовы: стр.3439, 3463, 3476, 3482, 3499, 3508, 3514, 3523, 3532, 3538, 3547, 3593

### 2c. InventoryTab.cs
- `_inventoryFilterStateOptions` → ключи с `Loc.Get`
- `"НАДЕТЬ"` / `"СНЯТЬ"` / `"БРОСИТЬ"` → `Loc.Get`
- `ClearInventoryDetail` → `Loc.Get`
- `"Бонусы: "` → `Loc.Get`

### 2d. ContractsTab.cs
- `_contractFilterStateOptions` → ключи с `Loc.Get`
- `GetLocationRank` стр.329-332 → `Loc.Get($"ui.contract.rank.{locationId}")`
- `GetContractTypeName` стр.464-466 → `Loc.Get($"ui.contract.type.{type}")`
- Все `_messageLabel.text` → `Loc.Get`
- `"Выберите контракт из списка"` → `Loc.Get`

### 2e. MyShipsTab.cs
- Все хардкоженные строки → `Loc.Get`

### 2f. MarketWindow.cs
- `LocalizeOp` стр.1586-1589 → `Loc.Get($"ui.market.op.{op}")`
- Все `SetMessage(...)` и `_messageLabel.text` → `Loc.Get`
- `_myItemsToggle.text` стр.1258 → `Loc.Get`

### 2g. ShipCargoConsoleWindow.cs
- Все `SetStatus(...)` → `Loc.Get`

---

## Шаг 3. Проверить RepairManager

Открыть `RepairManagerWindow.uxml` и `RepairManagerWindow.cs` — найти все хардкоженные русские строки, добавить ключи в UI_Table, обернуть в `Loc.Get`.

---

## Шаг 4. Верификация

- `check_compile_errors` → 0 ошибок
- Play Mode → EscMenu → переключить язык → главные кнопки меняются
- Пройти все табы CharacterWindow → фильтры/статусы меняются
- Открыть MarketWindow → сообщения меняются
- Открыть RepairManager → всё меняется

---

## Оценка

| Шаг | Часы |
|---|---|
| 0. Проверка SharedData | 0.2h |
| 1. Расширение UI_Table (~80 ключей) | 1h |
| 2. Привязка Loc в коде (7 файлов) | 3h |
| 3. RepairManager | 0.5h |
| 4. Верификация | 0.5h |
| **Итого** | **~5h** |
