# CharacterWindow — Inventory tab + full split-out refactor

**Дата:** 2026-06-16
**Автор:** Mavis (Mavis)
**Триггер:** P-инвентарь пустой при открытии CharacterWindow → ИНВЕНТАРЬ; блок описания предмета не виден; сортировка по типу потеряна
**Скоуп:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (3174 строки, монолит), `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml`, `Assets/_Project/UI/Resources/UI/CharacterWindow.uss`

---

## 1. Диагноз (что нашёл)

### 1.1 Критично: CharacterWindow.cs имеет сломанную парность скобок

После коммита `f136fd5` ("Character UI full refactor_1-2 — UXML rewrite") в методе `ApplyInventoryFilters` (строки 1686-1708) прибавились **лишние закрывающие скобки** — скорее всего артефакт предыдущего "SESSION 2 ROLLBACK":

```
1704:             if (!ReferenceEquals(_inventoryList.itemsSource, filteredList)) {
1705:             _inventoryList.itemsSource = filteredList;
1706:             }                   ← закрывает if
1707:             _inventoryList.RefreshItems();
1708:             }                   ← ЛИШНЯЯ: закрывает ApplyInventoryFilters
```

После лишней `}` всё содержимое со строки 1709 по 2895 (SubscribeQuestState, SubscribeSkills, SubscribeReputation, HandleNpcAttitudeSnapshot, OnAcceptContractClicked, OnCloseClicked, Show/Hide/SetVisible, OnAcceptQuestClicked и т.д., ~1200 строк) оказывается **внутри тела ApplyInventoryFilters**. Доказательства:

- `indent` всех строк 1709-2895 = 12 (т.е. на уровень глубже method body)
- `private bool _isQuestStateSubscribed` (1714), `private bool _isSkillsSubscribed` (1721) — поля с `private` модификатором внутри метода → **CS1525: Unexpected symbol** (или подобное)
- Баланс `{}` сходится только из-за того, что выше 1708 был открыт лишний `{` (в одной из строк 1692-1704)

**Это объясняет, почему инвентарь пустой:** CharacterWindow скорее всего не компилируется → `Instance == null` → `cw.Toggle()` в NetworkPlayer ничего не делает → игрок видит CharacterWindow (он scene-placed) с пустым tab-content (потому что EnsureBuilt не отработал).

Сейчас CharacterWindow показывается (значит UIDocument-source-asset загружается), но кнопки кликаются "в молоко" — скорее всего потому, что **ссылки** `_inventoryList`, `_invDetailName` и т.д. остаются `null` (EnsureBuilt бросил на стадии `_contractsList` или ещё раньше).

### 1.2 Sort по ItemType потеряна

`RefreshInventoryCache` (строки 1116-1185) группирует по `itemId` через `Dictionary<int, ...>`, потом итерирует `foreach (var kvp in groups)`. **Порядок Dictionary — порядок вставки, не по типу.** Раньше (до f136fd5) был sort по `(type, displayName)` — он исчез.

### 1.3 Блок описания предмета есть, но не виден

UXML строки 109-117 содержат `<ui:VisualElement name="inventory-detail" class="inventory-detail-pane">` с 5 лейблами (name / type / weight / stat / desc) — **структурно всё на месте**. Но:

- USS `.inventory-layout` (строки 446-456) задаёт `flex-direction: row; flex-grow: 1; height: 100%` — это правильно
- USS `.inventory-list-pane` (470-482): `flex-basis: 60%` — ок
- USS `.inventory-detail-pane` (490-501): `flex-basis: 40%` — ок
- Проблема: **в `.character-window` нет `flex-shrink: 0` на `.actions` и `.message-label`** → они "съедают" всю высоту, а inventory-layout получает min-height: 0 (благодаря `min-height: 0 !important` на `.list-section`), что в сумме даёт высоту 0 для list-section
- **На скриншоте видно:** вкладка ИНВЕНТАРЬ видна, видна красная ЗАКРЫТЬ, но пустая область. Это значит `.list-section` свёрнут в высоту 0, а `.actions` + `.message-label` заняли всё свободное место

### 1.4 ЗАКРЫТЬ-кнопка перекрывает content

`.actions` (USS 353-358): `margin-top: 4px; min-height: 30px; flex-shrink: 0` — ок
`.message-label` (371-380): `margin-top: 4px; padding: 4px 6px; flex-shrink: 0` — ок

Но на скриншоте видно, что **ЗАКРЫТЬ прямо под табами, а inventory section свёрнут**. Это значит высота `.character-window` рассчитывается неверно: `max-height: 92%` от body, но внутри `flex-direction: column` без явного `flex-shrink: 1` на `.list-section` (есть `flex-shrink: 1` и `min-height: 0` — но если все дочерние элементы `.tabs`, `.actions`, `.message-label` имеют явный `height` / `min-height`, то `.list-section` сжимается до 0).

---

## 2. Цели рефакторинга

1. **Исправить** сломанные скобки в CharacterWindow.cs (минимально — починить лишние `}`, чтобы файл снова компилировался)
2. **Восстановить** sort по `(ItemType, displayName)` в `RefreshInventoryCache`
3. **Починить** вёрстку: `.list-section` для inventory должна получать реальную высоту (добавить flex в parent chain или отрегулировать `min-height`)
4. **Разделить** монолит CharacterWindow.cs (3174 строк) на логические табы:
   - `CharacterWindow.cs` — только хром (header, info-bar, tabs, actions, visibility, lifecycle, 4 FIX'ы)
   - `Tabs/CharacterTab.cs` — ПЕРСОНАЖ (одежда, модули, характеристики, навыки)
   - `Tabs/InventoryTab.cs` — ИНВЕНТАРЬ
   - `Tabs/ContractsTab.cs` — КОНТРАКТЫ
   - `Tabs/QuestsTab.cs` — КВЕСТЫ
   - `Tabs/ReputationTab.cs` — РЕПУТАЦИЯ
   - `Tabs/ShipTab.cs` — КОРАБЛЬ
5. Сохранить все 4 FIX'ы из MarketWindow (pickingMode, cursor, MarkDirtyRepaint+50ms, inline fallback)
6. Сохранить cross-tab cache rule (HandleInventorySnapshotUpdated: refresh cache unconditionally, gate UI rebuild на _activeTab)
7. Сохранить switchTab data refresh (см. pitfall из skill — T-E04 fix)

---

## 3. План (по шагам)

### Шаг A. Минимальный fix — убрать лишние скобки

**Скоуп:** `CharacterWindow.cs:1706-1708` — убрать 1 лишнюю `}`, оставить правильную парность. До того, чтобы файл точно скомпилировался.

**Риск:** высокий — после удаления лишней `}` остальной код (1714+) может выявить новые ошибки компиляции. Это нормально — по одной за раз.

**Verify:** `refresh_unity` + `read_console` — должно быть 0 errors.

### Шаг B. Добавить sort в RefreshInventoryCache

**Скоуп:** добавить `OrderBy(i => i.type).ThenBy(i => i.displayName)` перед циклом `foreach (var kvp in groups)`.

**Риск:** низкий.

**Verify:** запустить P-инвентарь, проверить порядок: сначала все "Антигравий", потом все "Еда", и т.д.

### Шаг C. Починить вёрстку (чтобы inventory-section получал высоту)

**Скоуп:** USS — убедиться, что `.list-section` имеет реальную высоту. Вариант: добавить `flex-grow: 1; flex-shrink: 1; min-height: 0` более явно. Также: дать `.actions` и `.message-label` `flex-shrink: 0` (уже есть), но `.tabs` тоже `flex-shrink: 0` (есть).

Возможно проблема в том, что **header + info-bar + tabs + actions + message-label** суммарно > max-height, и `.list-section` схлопывается до 0. **Решение:** `min-height: 200px !important` на `.list-section` (или другой разумный минимум).

**Риск:** низкий.

**Verify:** скриншот P-инвентаря — должен быть виден list.

### Шаг D. Полный рефакторинг — split tabs

**Скоуп:** создать папку `Assets/_Project/Scripts/UI/Client/CharacterWindow/`, в ней:
- `CharacterWindow.cs` (хром, ~400 строк)
- `Tabs/CharacterTab.cs`
- `Tabs/InventoryTab.cs` — самый приоритетный, т.к. сломан
- `Tabs/ContractsTab.cs`
- `Tabs/QuestsTab.cs`
- `Tabs/ReputationTab.cs`
- `Tabs/ShipTab.cs`
- `ITabController.cs` — interface с `BuildUI(VisualElement root)`, `RefreshData()`, `OnTabShown()`, `OnTabHidden()`, `OnDisable()`

**Паттерн:** CharacterWindow.OnEnable → `tab.BuildUI(_root)`, CharacterWindow.SwitchTab → `tab.OnTabShown()`.

**Риск:** высокий — много кода, много классов, легко потерять subscriptions.

**Verify:** каждый таб работает отдельно, P-инвентарь показывает предметы сортированно по типу, при выборе предмета detail-panel обновляется.

### Шаг E. Документация + cleanup

- Обновить `docs/dev/CHARACTER_WINDOW_TABS.md` (новый) — архитектура табов
- Сохранить ticket-ссылки `R2-003`, `R2-007`, `T-P15..T-P18` (T-P19, T-P20, T-P21 добавятся для табов)
- Очистить `Debug.Log` диагностики (оставить только essential)

---

## 4. Verify protocol (per skill `unity-mcp-orchestrator`)

После каждого шага:
1. `refresh_unity` (scope=all, compile=request, wait_for_ready=true)
2. `read_console` (types=[error, warning])
3. **0 errors expected**
4. **0 NEW warnings** (existing warnings отмечены в baseline)

User runs Play Mode manually — проверяет:
- `P` → CharacterWindow открывается
- Click `ИНВЕНТАРЬ` → виден список предметов, отсортированный по типу
- Click на предмет → правая панель обновляется с описанием, типом, весом, бонусами
- Кнопка `ЗАКРЫТЬ` не перекрывает контент
- Click `КВЕСТЫ` → видны 4 подсекции
- `P` повторно → CharacterWindow закрывается

---

## 5. Открытые вопросы / risk register

- **Q1:** Шаг A vs Шаг D — лучше делать Шаг A отдельно (минимальный fix), затем Шаг D. Если A провалится — урон ограничен.
- **Q2:** В UXML есть `<ui:VisualElement name="filters-row" class="filters-row" style="display: none;">` — показ только для contracts. Оставить как есть.
- **Q3:** `[НАДЕТЬ]` кнопка в инвентаре — она сейчас использует reflection (`Type.GetType` + `Invoke` для `EquipmentServer.RequestEquipRpc`). Это anti-pattern из skill (`unity-mcp-orchestrator` pitfall #11 + reflection-stub). Но это **не блокер для инвентаря** — fix оставим на следующий тикет.
- **Q4:** Шаг D потребует много времени — возможно разбить на 2-3 тикета: D1 = InventoryTab, D2 = CharacterTab+Skills, D3 = остальные.
- **Q5:** Сортировка по типу: оставить порядок по `ItemType` enum (Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech) или сделать configurable через UI? → Пока hardcoded по enum.
- **Q6:** При выборе предмета в инвентаре `OnInventorySelectionChanged` — `_selectedInventoryItem` объявлено в CharacterWindow.cs:157, но нигде не читается. Удалить или оставить для будущего?

---

## 6. Что НЕ делаем (out of scope)

- ❌ Перенос MarketWindow в эту же систему — отдельный файл
- ❌ Замена reflection-RPC stub для [НАДЕТЬ] — отдельный тикет
- ❌ Добавление фильтра по типу в filter-source DropdownField — он уже есть, см. `ConfigureInventoryFilters`
- ❌ Виртуализация списка инвентаря — предметов мало (≤30 в MVP), manual VisualElement rows хватит
- ❌ Изменение `EquipSlot`, `ClothingItemData` и других Equipment-классов
- ❌ Migration P-инвентаря в отдельный UIDocument — `project-c-ui-as-tab` skill говорит держать всё в одном CharacterWindow
