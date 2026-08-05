# Аудит всех хардкоженных строк

> Автоматически собрано grep-поиском. Всё что нужно локализовать.

---

## 1. System-сообщения (switch/case)

### InventoryClientState.LocalizeResultCode
Файл: `Assets/_Project/Items/Client/InventoryClientState.cs:264-280`

| Код | Текущая строка (RU) | Ключ |
|---|---|---|
| Ok | "OK" | `sys.inventory.ok` |
| NotInZone | "Слишком далеко от предмета" | `sys.inventory.not_in_zone` |
| InventoryFull | "Инвентарь полон" | `sys.inventory.full` |
| ItemNotFound | "Предмет не найден" | `sys.inventory.item_not_found` |
| NotEnoughQuantity | "Недостаточно предметов" | `sys.inventory.not_enough_quantity` |
| InvalidSlot | "Неверный слот" | `sys.inventory.invalid_slot` |
| RateLimited | "Слишком много запросов" | `sys.inventory.rate_limited` |
| InternalError | "Внутренняя ошибка" | `sys.internal_error` |
| NoPermission | "Нет прав на операцию" | `sys.inventory.no_permission` |
| ItemNotOwned | "Этого предмета нет в инвентаре" | `sys.inventory.item_not_owned` |
| StackOverflow | "Стек переполнен" | `sys.inventory.stack_overflow` |

### MarketClientState.LocalizeResultCode
Файл: `Assets/_Project/Trade/Scripts/Client/MarketClientState.cs:180`

(нужно вычитать все коды — grep показал наличие метода, содержимое не читали)

### ContractClientState.LocalizeResultCode
Файл: `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs:111-132`

| Код | Текущая строка (RU) | Ключ |
|---|---|---|
| Ok | "OK" | `sys.contract.ok` |
| NotInZone | "Вы должны быть в зоне NPC-агента" | `sys.contract.not_in_zone` |
| ContractNotFound | "Контракт не найден" | `sys.contract.not_found` |
| ContractNotPending | "Контракт уже принят или истёк" | `sys.contract.not_pending` |
| ContractNotActive | "Контракт не активен" | `sys.contract.not_active` |
| ContractNotAssigned | "Это не ваш контракт" | `sys.contract.not_assigned` |
| MaxActiveReached | "Слишком много активных контрактов" | `sys.contract.max_active` |
| TooMuchDebt | "Слишком большой долг" | `sys.contract.too_much_debt` |
| TimerExpired | "Время контракта истекло" | `sys.contract.timer_expired` |
| WrongDestination | "Вы не в целевой локации" | `sys.contract.wrong_destination` |
| CargoMissing | "Нет нужного груза" | `sys.contract.cargo_missing` |
| WarehouseFull | "Нет места на складе" | `sys.contract.warehouse_full` |
| ItemNotFound | "Товар не найден" | `sys.contract.item_not_found` |
| RateLimited | "Слишком много запросов" | `sys.rate_limited` |
| InternalError | "Внутренняя ошибка" | `sys.internal_error` |

### ContractServer.ContractClientState_LocalizeResultCode
Файл: `Assets/_Project/Trade/Scripts/Network/ContractServer.cs:408-411`

**Должен быть ПОЛНОСТЬЮ УДАЛЁН.** Сервер не должен локализовать. Только коды.

---

## 2. Инвентарь: error-сообщения (InventoryWorld.cs)

Файл: `Assets/_Project/Items/Core/InventoryWorld.cs`

| Строка | Контекст |
|---|---|
| `$"Предмет ID={itemId} не найден"` | PickupItem: item not in DB |
| `$"Слишком далеко ({dist:F1}м, порог {PICKUP_RANGE_M:F1}м)"` | PickupItem: distance check |
| `$"Инвентарь полон ({data.TotalCount}/{_maxSlots})"` | PickupItem: full |
| `$"Ключ (ID={itemId}) уже есть в инвентаре"` | Key duplicate |
| `$"Ключ (instanceId={instanceId}) уже есть в инвентаре"` | Key lost reactivation |
| `$"Подобран предмет"` | Success message |
| `$"Слот {slotIndex} вне диапазона"` | DropItem |
| `$"Слот {slotIndex} пуст"` | DropItem |
| `"Quantity должен быть > 0"` | DropItem validation |

**Решение:** Все эти строки должны уйти. Методы `Fail(...)` должны принимать ТОЛЬКО InventoryResultCode, а клиент сам локализует.

---

## 3. UI: EscMenu

### GameplaySettingsSection.cs
Файл: `Assets/_Project/Scripts/UI/EscMenu/GameplaySettingsSection.cs`

| Строка | Ключ |
|---|---|
| `"Управление"` | `ui.settings.gameplay.title` |
| `"Чувств. мыши"` | `ui.settings.gameplay.mouse_sensitivity` |
| `"Инвертировать Y"` | `ui.settings.gameplay.invert_y` |
| `"Чувств. зума"` | `ui.settings.gameplay.zoom_sensitivity` |
| `"Доступность"` | `ui.settings.accessibility.title` |
| `"Субтитры"` | `ui.settings.accessibility.subtitles` |
| `"Выбор языка будет доступен после внедрения локализации."` | **Заменить на DropdownField** |

### AudioSettingsSection.cs
Файл: `Assets/_Project/Scripts/UI/EscMenu/AudioSettingsSection.cs`

(нужно вычитать файл)

### GraphicsSettingsSection.cs
Файл: `Assets/_Project/Scripts/UI/EscMenu/GraphicsSettingsSection.cs`

(нужно вычитать файл)

### EscMenuWindow.cs (навигация)
Файл: `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs`

(нужно вычитать названия разделов)

---

## 4. UI: CharacterWindow

Файл: `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`

| Строка | Контекст |
|---|---|
| `"Все"` | filter dropdown |
| `"Контракты"` | filter dropdown |
| `"Квесты"` | filter dropdown |
| `"Активные"` | filter dropdown |
| `"Доступные"` | filter dropdown |
| `"Все типы"` | inventory filter |
| `"Игрок (Owner)"` | stat name |
| `"Игрок"` | stat name |
| `"—"` | no data |
| `$"Фракций: {snapshot.entries.Length}"` | rep status |
| `"Нет данных о репутации"` | rep status |

---

## 5. UI: MarketWindow

Файл: `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs`

| Строка | Контекст |
|---|---|
| `"Ошибка: "` | prefix for error |
| `$"OK ({result.itemId} x{result.quantity})"` | trade success |
| `LocalizeOp(result.op)` | operation name |

---

## 6. ScriptableObject: ItemData

Файл: `Assets/_Project/Scripts/Core/ItemType.cs`

| Поле | Содержит |
|---|---|
| `itemName` | "Железная руда", "Медная руда"... |
| `description` | "Обычная железная руда. Добывается в шахтах."... |

### ItemTypeNames
Файл: `Assets/_Project/Scripts/Core/ItemTypeNames.cs`

| Индекс | Текущая строка |
|---|---|
| 0 | "Ресурсы" |
| 1 | "Оборудование" |
| 2 | "Еда" |
| 3 | "Топливо" |
| 4 | "Антигравий" |
| 5 | "Мезий" |
| 6 | "Медикаменты" |
| 7 | "Техника" |

---

## 7. ScriptableObject: NpcDefinition

Файл: `Assets/_Project/Quests/Npcs/NpcDefinition.cs`

| Поле | Пример |
|---|---|
| `displayName` | "Мира", "Зорик" |
| `greetingText` | "Greetings, traveler." |

---

## 8. ScriptableObject: QuestDefinition

Файл: `Assets/_Project/Quests/Quests/QuestDefinition.cs`

| Поле | Пример |
|---|---|
| `displayName` | "Найти артефакт" |
| `description` | "Артефакт Древних был утерян..." |

---

## 9. ScriptableObject: QuestStage + QuestObjective

Файлы: `QuestStage.cs`, `QuestObjective.cs`

| Поле | Пример |
|---|---|
| `QuestStage.description` | "Найдите вход в пещеру" |
| `QuestObjective.description` | "Поговорите с Мирой" |

---

## 10. ScriptableObject: FactionDefinition

Файл: `Assets/_Project/Quests/Factions/FactionDefinition.cs`

| Поле | Пример |
|---|---|
| `displayName` | "Гильдия Мыслителей" |
| `loreDescription` | "Древняя гильдия..." |
| `ReputationTier.tier` | "Недруг", "Друг", "Уважаемый" |

---

## 11. Диалоги

### DialogueNode.text
Файл: `Assets/_Project/Quests/Dialogue/DialogueNode.cs`

Сотни реплик во всех DialogTree. Это **самый большой объём** текста.

### DialogueEdge.label
Файл: `Assets/_Project/Quests/Dialogue/DialogueNode.cs`

Реплики выбора игрока: "Я помогу.", "Расскажи подробнее."

---

## 12. Созвездия

Файл: `Assets/_Project/Scripts/Core/DayNight/ConstellationData.cs`

| Поле | Пример |
|---|---|
| `Constellation.localizedName` | уже названо правильно |

---

## 13. Прочее

Нужно проверить (grep не покрыл):
- `ControlHintsUI.cs` — подсказки "Нажмите E..."
- `HUDManager.cs` — HUD элементы
- `NetworkUI.cs` — сетевые статусы
- `ConfirmationDialog.cs` — "Да"/"Нет"
- `UIFactory.cs` — общие UI-компоненты
- `CraftingWindow` — крафт UI
- `QuestTracker` — трекер квестов в HUD
- `SkillTree` — дерево навыков
