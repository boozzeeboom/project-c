# Resources Import/Export — End-to-End Guide

> **Статус:** ✅ Phase 1 завершена (T-IE01..T-IE07, июнь 2026).
> **Цель:** за 5 минут — от «у меня есть CSV» до «вижу товар на рынке и обмениваю через UI».

---

## TL;DR

1. Открыть `Tools → ProjectC → Resources → CSV Import/Export`.
2. **Browse** → выбрать `Assets/_Project/Resources/_docs/Resources_Import.csv` (или свой).
3. **Preview** → посмотреть preview, исправить errors если есть.
4. **▶ Import** → ресурсы появятся в проекте (создаются `.asset` файлы, обновляется `ItemRegistry.asset`, `TradeItemDatabase.asset`, `MarketConfig_*.asset`, `ExchangeRateConfig.asset`).
5. **Play Mode** → `F` на PickupItem → `F` на MarketZone → вкладка **Обменник** → **Упаковать/Распаковать** ящики.

---

## 1. Что такое этот импорт и зачем

Project C хранит 4 разных типа SO, описывающих «предметы»:

| Что | Файл SO | Где лежит | Сколько |
|-----|---------|-----------|---------|
| Пикаемые предметы (руда, еда, ключи…) | `ItemData` | `Resources/Items/*.asset` | 47 в проекте |
| Рыночные товары (ящики, слитки) | `TradeItemDefinition` | `Trade/Data/Items/*.asset` | 2 в проекте ⚠️ |
| Цены на рынке (по локациям) | `MarketItemConfig` | inline в `MarketConfig_*.asset` | 7 в проекте |
| Курсы обмена (Pack/Unpack) | `ExchangeRateEntry` | inline в `DefaultExchangeRate.asset` | 4 в проекте |

**Проблема:** чтобы добавить 1 новый ресурс, надо создать/обновить ассеты во всех 4 местах + согласовать `itemName` ↔ `tradeItemId`. Ручная работа, легко ошибиться.

**Решение:** один CSV-файл описывает все 4 типа сразу. Editor tool читает CSV и генерирует/обновляет все ассеты автоматически. **GUID существующих ассетов не меняется** (LootTable, Recipe, PickupItem остаются валидными).

---

## 2. Где лежит что

```
Assets/_Project/
├── Items/
│   ├── Data/
│   │   └── ItemRegistry.asset            ← int id → ItemData (1..N)
│   └── Core/, Client/, Network/, Dto/, Editor/
├── Resources/
│   ├── Items/                            ← ItemData .asset (47 файлов)
│   ├── Exchange/
│   │   └── DefaultExchangeRate.asset      ← ExchangeRateEntry[] (4 rates)
│   └── _docs/                            ← ДОКУМЕНТАЦИЯ (наш артефакт)
│       ├── Resources_Import.csv          ← ЭТАЛОННЫЙ CSV
│       └── Resources_Import_Schema.md    ← Справка (формат, defaults, troubleshooting)
├── Trade/
│   ├── Data/
│   │   ├── TradeItemDatabase.asset       ← List<TradeItemDefinition> (2 items)
│   │   └── Markets/
│   │       ├── MarketConfig_Primium.asset   ← items[] (2)
│   │       ├── MarketConfig_Secundus.asset  ← items[] (2)
│   │       ├── MarketConfig_Tertius.asset   ← items[] (1)
│   │       └── MarketConfig_Quartus.asset   ← items[] (2)
│   └── Scripts/, Prefabs/, Resources/
├── Scenes/
│   ├── BootstrapScene.unity             ← [ExchangeServer] GameObject
│   └── World/
│       └── WorldScene_0_0.unity          ← MarketZone, PickupItem'ы (тестовые)
└── ...
```

---

## 3. Пошаговая инструкция (5 минут до результата)

### Шаг 1. Открыть окно импорта

В Unity Editor: **Tools → ProjectC → Resources → CSV Import/Export**.

Окно откроется с 4 кнопками: `[Browse...] [Preview] [▶ Import] [Export]`, ListView (пустой), и статус-бар внизу.

![Window UI](https://placeholder) — реальный UI описан в `02_DESIGN.md` §5.

### Шаг 2. Выбрать CSV

**Вариант A — использовать эталонный:**
1. **Browse** → перейти в `Assets/_Project/Resources/_docs/` → выбрать `Resources_Import.csv` → Open.

**Вариант B — свой CSV:**
1. Открыть в Excel/LibreOffice существующий CSV или скопировать `_docs/Resources_Import.csv` как шаблон.
2. Сохранить как **UTF-8 with BOM** (важно для кириллицы в Excel).
3. В Editor: **Browse** → выбрать.

### Шаг 3. Preview (проверить)

**Preview** парсит CSV, запускает cross-validate, заполняет ListView. Окно показывает:
- **Summary**: "Parsed N rows. inventory: 37, tradeItems: 5, marketItems: 11, exchangeRates: 4".
- **Global errors** (если есть) — блокируют Import.
- **Per-row errors** (если есть) — строка выделена красным, импорт пропустит её.

**Типичные ошибки и фиксы:**

| Ошибка | Фикс |
|--------|------|
| `inventory: duplicate itemName 'X'` | Дубликат в inventory. Переименовать одну из строк. |
| `marketItems: tradeItemId 'X' not in tradeItems` | В marketItems ссылка на несуществующий tradeItem. Добавить в tradeItems или убрать из marketItems. |
| `exchangeRates: inventoryItemName 'X' not in inventory` | В exchangeRates ссылка на несуществующий item. Добавить в inventory. |
| `Line 5: 'itemType' = 'Apparel' is not valid ItemType` | Неправильный enum. Допустимо: Resources/Equipment/Food/Fuel/Antigrav/Meziy/Medical/Tech. |
| `Line 5: 'maxStack' = '-5' is not a non-negative int` | Отрицательное число. Должно быть ≥ 1. |
| `Line 5: 'allowBuy' = 'foo' is not y/n/yes/no/...` | Bool поля: `y` или `n` (case-insensitive). |

### Шаг 4. Apply (импорт)

**▶ Import** (зелёная кнопка) — кнопка **disabled** если есть global errors (см. ListView).

После клика:
- **Dialog**: "Created: N, Updated: M, Skipped: K, Errors: E, Warnings: W".
- **Status bar**: "Import: C=N, U=M, S=K, E=0, W=0".
- **Results section** под ListView: warnings/errors текстом.

Что произошло:
1. **Новые `ItemData`** созданы в `Assets/_Project/Resources/Items/Item_{Type}_{Name}.asset`.
2. **Существующие** `ItemData` обновлены (description/maxStack/weightKg) — **GUID stable**.
3. **`ItemRegistry.asset`**: добавлены Entry `{id: newId, item: newItem}` для каждого нового.
4. **Новые `TradeItemDefinition`** созданы в `Assets/_Project/Trade/Data/Items/TradeItem_{itemId}.asset`.
5. **`TradeItemDatabase.asset`**: добавлены в `allItems`.
6. **`MarketConfig_*.asset`**: добавлены/обновлены в `items[]`.
7. **`DefaultExchangeRate.asset`**: добавлены/обновлены в `rates[]`.
8. **Console**: `Debug.Log` для каждого `CreateAsset`.

### Шаг 5. Проверить в Play Mode

1. **Save Project** (Ctrl+S) — без этого изменения в `.asset` файлах могут не закрепиться на диске до Play Mode.
2. **Play** (Ctrl+P / ▶).
3. **Host** (кнопка в NetworkManager UI или `Tools → StartHost`).
4. **Загрузится `WorldScene_0_0`** (через `ClientSceneLoader`).

#### Проверка A: подобрать предмет

5. Найти `PickupItem` на сцене (например, `[Pickup_ЖелезнаяРуда]` — ищи в Hierarchy).
6. Подойти к нему, нажать **E**.
7. Откроется **Inventory** (HUD).
8. Предмет добавлен — ID подбирается через `ItemRegistry` (теперь все 47 items зарегистрированы).

#### Проверка B: открыть обменник

9. Найти `MarketZone_Primium` на сцене (например, в `WorldScene_0_0`, координаты ~40096, 2510, 40140).
10. Подойти, нажать **F**.
11. Откроется **MarketWindow** с вкладками: **Рынок / Склад / Контракты / Обменник**.
12. Кликнуть вкладку **Обменник**:
    - **Левая панель**: `inventory` (пикаемые предметы, сгруппированы).
    - **Правая панель**: `warehouse` (ящики, доступные для обмена).
    - **Кнопки**: `→ Упаковать` / `← Распаковать`.

#### Проверка C: упаковать/распаковать

13. В Inventory (HUD) должно быть ≥100 «Железная руда» (или сколько вам доступно).
14. В Обменнике: **выбрать** «Железная руда» слева → ввести `100` (или кликнуть на подсказку).
15. Кликнуть **→ Упаковать**.
16. В Console появится: `[ExchangeServer][Pack] ENTER clientId=...` (server-side RPC).
17. Если успех — `MarketWindow` обновится: слева уменьшилось «Железная руда» на 100, справа появился 1 `Ящик железной руды`.
18. Обратный процесс: **выбрать** «Ящик железной руды» справа → кликнуть **← Распаковать** → +100 «Железная руда» в inventory.

#### Проверка D: купить/продать на рынке (опц.)

19. Переключиться на вкладку **Рынок**.
20. Выбрать товар (например, «Мезий (канистра)»), ввести количество.
21. **Купить** (`Buy`) — credits уменьшатся, item в inventory.
22. **Продать** (`Sell`) — credits увеличатся, item из inventory.

---

## 4. Сценарии использования

### 4.1 Добавить новый ресурс

**Задача:** добавить «Медный камень» (новый тип руды, продаётся ящиками на primium).

1. Открыть `Resources_Import.csv` в Excel.
2. В `# block=inventory` добавить:
   ```
   Медный камень,Resources,Кусок медной породы. Добывается в шахтах.,30,3.0
   ```
3. В `# block=tradeItems` добавить:
   ```
   resource_copper_stone_box,Ящик медного камня,150,60,6,4,n,n,n,None
   ```
4. В `# block=marketItems` добавить:
   ```
   resource_copper_stone_box,primium,180,30,0.02,None,y,y
   ```
5. В `# block=exchangeRates` добавить:
   ```
   resource_copper_stone_box,Медный камень,1,100,Ящик медного камня
   ```
6. Сохранить CSV (UTF-8 BOM).
7. В Unity: **Tools → ProjectC → Resources → CSV Import/Export** → **Browse** → выбрать → **▶ Import**.
8. Проверить результат в dialog: "Created: 2, Updated: 0, Skipped: 0".
9. Проверить файлы:
   - `Resources/Items/Item_Resources_Медный_камень.asset` создан.
   - `Items/Data/ItemRegistry.asset` — добавлена запись (следующий id, например 48).
   - `Trade/Data/Items/TradeItem_resource_copper_stone_box.asset` создан.
   - `Trade/Data/TradeItemDatabase.asset` — `allItems` теперь содержит 6 items.
   - `Trade/Data/Markets/MarketConfig_Primium.asset` — `items[]` теперь содержит 6.
   - `Resources/Exchange/DefaultExchangeRate.asset` — `rates[]` теперь содержит 5.
10. **Play Mode** → подобрать PickupItem с `itemData = Медный камень` → Inventory → Обменник → **→ Упаковать 100** → +1 ящик.

### 4.2 Обновить существующий

**Задача:** изменить `weightKg` у «Железная руда» с 2.0 на 2.5.

1. Открыть `Resources_Import.csv`.
2. Найти строку `Железная руда,Resources,...,20,2.0`.
3. Изменить `2.0` → `2.5`.
4. Сохранить.
5. **Tools → ProjectC → Resources → CSV Import/Export** → Browse → Preview → Import.
6. Dialog: "Created: 0, Updated: 1, Skipped: 0".
7. **GUID ассета НЕ изменился** (если бы — сломались LootTable/Recipe, ссылающиеся на него).

### 4.3 Round-trip проверка (идемпотентность)

1. **Export** → сохранить в `dump.csv` (любой путь).
2. **Browse** → выбрать `dump.csv` → **Preview** → проверить, что нет errors.
3. **Import**.
4. Dialog: **"Created: 0, Updated: N, Skipped: 0, Errors: 0"** — если так, importer идемпотентен.

### 4.4 Добавить рецепт (Phase 2 — out of scope MVP)

**TODO:** когда будет `CraftingCsvImporter`. Сейчас рецепты управляются вручную:
- `Assets/_Project/Resources/Crafting/Recipes/Recipe_*.asset` — создаются вручную через меню или копированием существующего.
- `Assets/_Project/Resources/Crafting/Stations/Station_*.asset` — `_allowedRecipes[]` редактируется вручную.

---

## 5. Что лежит в эталонном `Resources_Import.csv`

Сгенерирован из текущего состояния проекта + добавлены шаблонные строки для демонстрации формата:

| Блок | Строк | Описание |
|------|-------|----------|
| `# block=inventory` | 37 | Все ItemData в `Resources/Items/` (47, но 10 — legacy дубликаты/заглушки) |
| `# block=tradeItems` | 5 | Mesium, Antigrav, Iron/Copper/Wood box'ы |
| `# block=marketItems` | 11 | Mesium+Antigrav на 4 локациях + Iron box на primium/secundus + Copper box primium + Wood box primium |
| `# block=exchangeRates` | 4 | Iron/Copper/Wood box'ы + Antigrav ingot |

**Включает legacy артефакты** (видны как legacy, не баги):
- `Говно` (Resources, test stub)
- `Медная руда` × 2 (id 26 и id 29 — два разных ассета)
- `Мезий` (id 34, без описания)
- `TestStageItem`, `ТестовыйКамень` и т.д.

**Cleanup** — отдельный тикет (пользователь решает, что переименовать/удалить).

---

## 6. Troubleshooting

### 6.1 `Tools → ProjectC → Resources → CSV Import/Export` не появляется в меню

- Убедиться что компиляция успешна: **Console → 0 errors**.
- Перезапустить Unity Editor (иногда после добавления `MenuItem` нужно `Window → Layouts → Revert Factory Settings` или полный restart).
- Проверить что `ResourcesCsvWindow.cs` лежит в `Assets/_Project/Items/Editor/` и namespace `ProjectC.Items.Editor`.

### 6.2 После Import — Inventory в Play Mode пуст / предметы не подбираются

- Проверить `Items/Data/ItemRegistry.asset`: добавлена ли запись? Если нет — посмотреть Console на warnings, возможно cross-validate заблокировал импорт.
- Проверить что `ItemData.asset` создан в `Resources/Items/`. Если нет — `Created: 0` в dialog, посмотреть warnings.
- Убедиться что PickupItem на сцене **использует** правильный `ItemData` (поле `itemData` в Inspector → drag&drop).

### 6.3 Обменник не показывает ящики

- Открыть вкладку **Обменник** в MarketWindow.
- Левая панель показывает **только предметы из inventory** (которые можно упаковать). Правая — **только ящики на складе**.
- Если ящики пустые — нужны 100+ пикаблов одного типа (например, 100 «Железная руда» → 1 `resource_iron_box`).
- Если ящиков на складе нет — нужно либо упаковать, либо купить на рынке (вкладка **Рынок**).

### 6.4 Console: `[ExchangeServer][Pack] FAIL: Insufficient items`

- Не хватает предметов в inventory для упаковки. Курс: `inventoryQty: 100` → нужно ≥100 одного типа.

### 6.5 Console: `[ExchangeServer][Pack] FAIL: Warehouse cannot accept: warehouse_max_weight`

- Склад переполнен по весу. Курс: `weight: 50` для ящика → каждый ящик 50 кг. Лимит склада = 10000 кг (см. `Warehouse.cs:38`).

### 6.6 При Import выскакивает dialog: "ItemRegistry not found"

- Файл `Assets/_Project/Items/Data/ItemRegistry.asset` должен существовать. Если удалён — создать через меню `ProjectC/Items/Item Registry` или восстановить из git.

### 6.7 CSV с кириллицей — крякозябры

- Убедиться что файл сохранён в **UTF-8 with BOM**. В Excel: **Save As → Tools → Web Options → Encoding: UTF-8**.
- В LibreOffice: **File → Save As → Character Set: UTF-8**.

### 6.8 После Import — изменения в `.asset` не сохраняются

- **Ctrl+S** в Unity (Project view).
- Без Save — изменения в памяти, но не на диске. После restart Editor — откатятся.

### 6.9 Дубликат `itemName` блокирует импорт

Это **global error** (см. §3 шаг 3). Нужно:
- Открыть CSV, найти дубликат.
- Переименовать одну из строк (например, «Медная руда (2)»).
- Re-import.

---

## 7. Acceptance-чек-лист (user validation)

Перед тем как считать Phase 1 завершённой, проверьте что всё работает:

```bash
# 1. Открыть: Tools → ProjectC → Resources → CSV Import/Export
#    → окно открылось, 0 errors в Console

# 2. Browse → Assets/_Project/Resources/_docs/Resources_Import.csv
#    → Preview → ListView заполнен 57 строками (37+5+11+4)
#    → Status: "Preview ready" (без errors)

# 3. ▶ Import → dialog: "Created: ~9, Updated: ~48, Skipped: 0, Errors: 0"
#    (точные цифры зависят от pre-state)

# 4. Ctrl+S в Unity → сохранить изменения

# 5. Play Mode → Start Host → загрузится WorldScene_0_0

# 6. Найти PickupItem_ЖелезнаяРуда (Hierarchy) → F → подобрать
#    → Inventory HUD показывает "Железная руда x1" (или больше)

# 7. Найти MarketZone_Primium → F → MarketWindow

# 8. Вкладка "Обменник" → слева видны пикаемые предметы
#    (но минимум 100 одного типа для упаковки)
#    Справа видны ящики на складе (если были)

# 9. Через вкладку "Рынок" → купить "Мезий (канистра)" за credits
#    → вернуться на "Обменник" → упаковать что есть (если хватает)

# 10. Закрыть MarketWindow (Esc) → вернуться в игру
```

**Если какой-то шаг не работает** — см. §6 (Troubleshooting) или GitHub issues.

---

## 8. Документация (смежные файлы)

| Файл | Содержание |
|------|------------|
| `Resources_Import_Schema.md` | Справка по формату CSV (эта же секция) |
| `02_DESIGN.md` | Архитектура: алгоритм парсера, валидатора, importer'а, exporter'а, макет Editor Window |
| `03_TICKETS.md` | История тикетов T-IE01..T-IE07 |
| `04_EXAMPLES.md` | Больше примеров CSV (smoke, full, user flow, error cases) + acceptance-сценарии |
| `01_ANALYSIS.md` | Глубокий разбор 5 SO-систем, ID-пространств, edge-cases |
| `README.md` | Индекс, контекст, прецеденты (QuestCsv) |
| `../Resources_exchanger/01_ANALYSIS.md` | Архитектура обменника (Pack/Unpack) |
| `../Resources_exchanger/02_IMPLEMENTATION.md` | Реализация обменника (как работает Pack/Unpack) |

---

**Phase 1 ЗАВЕРШЕНА.** Следующий шаг — Phase 2: `CraftingCsvImporter` (рецепты + CraftingStation). Отдельный roadmap, не скоуп текущей итерации.
