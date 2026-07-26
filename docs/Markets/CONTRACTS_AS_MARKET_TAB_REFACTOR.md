# Contracts-as-Market-Tab — План рефакторинга

**Дата:** 2026-06-05
**Автор:** Mavis (продолжение C2-этапа)
**Связь:** `docs/dev/CONTRACT_V2_MIGRATION.md`, `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md`

---

## 0. Проблема

После C2-этапа появился `ContractBoardWindow` (UI Toolkit) — **отдельное** окно, как независимая UI-сущность. Это создаёт проблемы:

1. **Перекрывает стартовый UI Host/Server buttons.** `ContractBoardWindow.Awake` создаётся на root GO (через `NetworkManagerController.CreateContractClientState`), но **сам** `ContractBoardWindow` тоже root GO. Когда `ContractInteractor.TryOpenContractBoard()` вызывает `window.Show()`, его `_mainContainer` становится `DisplayStyle.Flex` — и `pickingMode` НЕ установлен в `Ignore` (в отличие от `MarketWindow` где 4-й FIX 2026-06-04 это исправил). Невидимый root растянут на весь экран → перехватывает клики.

2. **Дублирует UI-стек.** Контракты имеют свой `ContractBoardWindow.uxml/.uss/cs` (~25КБ кода), свой `Show/Hide`, свои `ListView` с собственными row factories. Это параллельная реальность к `MarketWindow` (840 строк, 4 FIX'а).

3. **Дублирует зоны.** `ContractZone` сейчас — отдельный scene-placed компонент с собственным `SphereCollider`. По выбору пользователя «контракты по fromLocationId» — `MarketZone` уже знает `locationId`, и можно выдавать контракты из той же зоны (без нового trigger'а).

4. **Legacy `ContractBoardUI` + новый `ContractBoardWindow`** живут параллельно (для регресса), но даже после C1-cleanup останется **один** `ContractBoardWindow` поверх `MarketWindow` — а это лишний UI-layer.

5. **Логика `ContractInteractor.TryOpenContractBoard` дублирует `MarketInteractor.TryOpenMarket`** — оба ищут «nearest zone» и дёргают сервер.

## 1. Решение

**Контракты становятся третьей вкладкой в `MarketWindow`:**
- `tab-market` / `tab-warehouse` / `tab-contracts`
- `MarketZone` остаётся единственной зоной (никакого `ContractZone`)
- Один `MarketWindow` на сцене, все 4 FIX'а работают на обе вкладки
- `ContractBoardWindow.cs/.uxml/.uss` **удаляются**
- `[ContractBoardWindow]` GO в `BootstrapScene` **удаляется**
- `ContractZone.cs` / `ContractZoneRegistry.cs` **удаляются** (заменены `MarketZone` / `MarketZoneRegistry`)
- `ContractInteractor` **удаляется** (объединён с `MarketInteractor`)
- `ContractClientState` остаётся (нужен для `OnContractResult` в HUD)
- `ContractServer` / `ContractWorld` / `ContractDto` / `ContractResultDto` / `ContractResultCode` / `ContractWorldItemResolver` / `ContractDebt` остаются (серверная логика не меняется)
- `ContractTrigger.cs` остаётся как **scene-marker** (наследие v1, не вызывается) — удалится в C1
- `ContractData.cs` / `ContractSystem.cs` / `ContractBoardUI.cs` остаются (legacy для регресса) — удалятся в C1

## 2. Зачем это правильно

- **Один UI-стек = одни фиксы.** Когда юзер поправит layout/race condition в `MarketWindow`, контракты автоматически получат это.
- **Нет z-fighting окон.** `MarketWindow` уже умеет корректно переключать visibility через `DisplayStyle` (см. `_itemSection.style.display = DisplayStyle.Flex/None`).
- **Логика «зона = локация» работает на 2 фичи сразу.** Игрок подходит к `MarketZone_Primium` → открывает рынок → вкладка «Контракты» показывает pending-контракты **этой** локации + свои активные.
- **NPC-агент НП = scene-decoration.** Если в локации есть NPC, он просто декорация. Если нет — игрок всё равно работает с контрактами через рынок.
- **Legacy удаляется быстрее.** По C1-cleanup из аудита удалятся 14 v1 файлов + 4 .asset. Дополнительно: `ContractBoardWindow` + `ContractBoardWindow.uxml/uss` + `ContractZone` + `ContractZoneRegistry` + `ContractInteractor`.

## 3. Что НЕ делаем

- ❌ Не выносим контракты в отдельный GO-окно (этот план).
- ❌ Не «склеиваем» вкладки (т.е. «всё-в-одном-списке»). Три явных вкладки.
- ❌ Не убираем `ContractClientState` — он нужен для HUD-уведомлений (декремент таймера в HUD) и для будущей фичи «уведомление о новом контракте».
- ❌ Не переименовываем `MarketWindow` в `TradeWindow` или `NPCWindow` (отдельный refactor).
- ❌ Не создаём `.asmdef` (HARD RULE AGENTS.md).

## 4. Архитектура (target)

### 4.1. UI: `MarketWindow.uxml` — добавляем 3-й таб

```xml
<!-- Было -->
<ui:VisualElement class="tabs">
    <ui:Button name="tab-market" text="РЫНОК" class="tab-btn" />
    <ui:Button name="tab-warehouse" text="СКЛАД / ТРЮМ" class="tab-btn" />
</ui:VisualElement>

<!-- Стало -->
<ui:VisualElement class="tabs">
    <ui:Button name="tab-market" text="РЫНОК" class="tab-btn" />
    <ui:Button name="tab-warehouse" text="СКЛАД / ТРЮМ" class="tab-btn" />
    <ui:Button name="tab-contracts" text="КОНТРАКТЫ" class="tab-btn" />
</ui:VisualElement>
```

**Новая секция** (рядом с `item-section` / `warehouse-section` / `cargo-section`):

```xml
<ui:VisualElement name="contracts-section" class="list-section" style="display: none;">
    <ui:Label text="Контракты НП" class="section-title" />
    <ui:ListView name="contracts-list" class="item-list" />
</ui:VisualElement>
```

**Стили** (`MarketWindow.uss`): реюз `.list-section`, `.item-list`, `.section-title` — ничего нового не нужно.

### 4.2. UI: `MarketWindow.cs` — добавляем 3-й таб

**Новые поля** (рядом с `_itemSection`, `_warehouseSection`, `_cargoSection`):
```csharp
private VisualElement _contractsSection;
private ListView _contractsList;
private int _selectedContractIndex = -1;
```

**Новая подписка** в `EnsureBuilt`:
```csharp
var tabContracts = _root.Q<Button>("tab-contracts");
if (tabContracts != null) tabContracts.clicked += () => SwitchTab("contracts");

_contractsSection = _root.Q<VisualElement>("contracts-section");
_contractsList = _root.Q<ListView>("contracts-list");
if (_contractsList != null)
{
    _contractsList.makeItem = MakeContractRow;
    _contractsList.bindItem = BindContractRow;
    _contractsList.fixedItemHeight = 32;
    _contractsList.selectionType = SelectionType.Single;
    _contractsList.selectedIndex = -1;
    _contractsList.selectionChanged += selectedItems =>
    {
        _selectedContractIndex = FindSelectedItemIndex<ContractDto>(_contractsList, selectedItems);
        _contractsList.Rebuild();
    };
}
```

**Новый `MakeContractRow` + `BindContractRow`** — портирование из `ContractBoardWindow.cs:255-300` (визуально: type label + item + reward + timer).

**`SwitchTab` (расширить)**:
```csharp
private void SwitchTab(string tab)
{
    _activeTab = tab;
    _itemSection.style.display = (tab == "market")     ? DisplayStyle.Flex : DisplayStyle.None;
    _warehouseSection.style.display = (tab == "warehouse") ? DisplayStyle.Flex : DisplayStyle.None;
    _contractsSection.style.display = (tab == "contracts") ? DisplayStyle.Flex : DisplayStyle.None;
    // ship selector виден только на warehouse (multi-ship)
    _shipSelectorContainer.style.display = (tab == "warehouse") ? DisplayStyle.Flex : DisplayStyle.None;
    // qty row — только на market
    _qtyField.parent.style.display = (tab == "market") ? DisplayStyle.Flex : DisplayStyle.None;
}
```

**Новые кнопки** (рядом с buy/sell/load/unload/close):
```xml
<ui:Button name="accept-btn" text="ВЗЯТЬ" class="action-btn accept" />
<ui:Button name="complete-btn" text="СДАТЬ" class="action-btn complete" />
<ui:Button name="fail-btn" text="ПРОВАЛИТЬ" class="action-btn fail" />
```

И в `EnsureBuilt` подписки:
```csharp
var acceptBtn = _root.Q<Button>("accept-btn");
var completeBtn = _root.Q<Button>("complete-btn");
var failBtn = _root.Q<Button>("fail-btn");
if (acceptBtn != null) acceptBtn.clicked += OnAcceptContractClicked;
if (completeBtn != null) completeBtn.clicked += OnCompleteContractClicked;
if (failBtn != null) failBtn.clicked += OnFailContractClicked;
```

**Кнопки видимы только в табе `contracts`** — аналогично qty/ship-selector:
```csharp
if (_activeTab == "contracts")
{
    acceptBtn.style.display = DisplayStyle.Flex;
    completeBtn.style.display = DisplayStyle.Flex;
    failBtn.style.display = DisplayStyle.Flex;
    // Скрыть buy/sell/load/unload
    _buyBtn.style.display = DisplayStyle.None;
    _sellBtn.style.display = DisplayStyle.None;
    _loadBtn.style.display = DisplayStyle.None;
    _unloadBtn.style.display = DisplayStyle.None;
}
else
{
    // Обратно
    acceptBtn.style.display = DisplayStyle.None;
    completeBtn.style.display = DisplayStyle.None;
    failBtn.style.display = DisplayStyle.None;
    _buyBtn.style.display = DisplayStyle.Flex;
    _sellBtn.style.display = DisplayStyle.Flex;
    _loadBtn.style.display = DisplayStyle.Flex;
    _unloadBtn.style.display = DisplayStyle.Flex;
}
```

**Новые handlers**:
```csharp
private void OnAcceptContractClicked()
{
    if (_selectedContractIndex < 0 || _selectedContractIndex >= _contractsCache.Length) return;
    var c = _contractsCache[_selectedContractIndex];
    ProjectC.Trade.Client.ContractClientState.Instance?.RequestAccept(c.contractId);
}

private void OnCompleteContractClicked()
{
    if (_selectedContractIndex < 0 || _selectedContractIndex >= _contractsCache.Length) return;
    var c = _contractsCache[_selectedContractIndex];
    ProjectC.Trade.Client.ContractClientState.Instance?.RequestComplete(c.contractId);
}

private void OnFailContractClicked()
{
    if (_selectedContractIndex < 0 || _selectedContractIndex >= _contractsCache.Length) return;
    var c = _contractsCache[_selectedContractIndex];
    ProjectC.Trade.Client.ContractClientState.Instance?.RequestFail(c.contractId);
}
```

### 4.3. UI: `MarketWindow.cs` — подписка на `ContractClientState`

**В `OnEnable`** (или в `Start`):
```csharp
var contractState = ProjectC.Trade.Client.ContractClientState.Instance;
if (contractState != null)
{
    contractState.OnSnapshotUpdated += HandleContractSnapshot;
    contractState.OnContractResult += HandleContractResult;
}
```

**Новые handlers**:
```csharp
private ContractDto[] _contractsCache = System.Array.Empty<ContractDto>();

private void HandleContractSnapshot(ContractSnapshotDto snapshot)
{
    // Контракты — в `available` (state==Pending) для текущей локации
    // и `active` (state==Active) — ВСЕГДА показываем активные (по выбору пользователя)
    // (по fromLocationId — пользователь выбрал «по зоне»)
    // значит: available фильтруем по snapshot.locationId, active — все
    var available = (snapshot.available ?? System.Array.Empty<ContractDto>())
        .Where(c => c.fromLocationId == snapshot.locationId).ToArray();
    // active — приходят уже для игрока, фильтруем по assignedPlayerId
    var active = (snapshot.active ?? System.Array.Empty<ContractDto>()).ToArray();

    // Объединяем: сначала active (свои), потом available (новые)
    _contractsCache = active.Concat(available).ToArray();
    if (_contractsList != null)
    {
        _contractsList.itemsSource = _contractsCache;
        _contractsList.Rebuild();
    }
}

private void HandleContractResult(ContractResultDto result)
{
    if (_messageLabel != null && result != null && !string.IsNullOrEmpty(result.message))
    {
        ShowMessage(result.message);
    }
    // Сервер сам шлёт новый snapshot после accept/complete/fail — HandleContractSnapshot обновит UI
}
```

### 4.4. Auto-subscribe: `MarketWindow` шлёт `RequestList` при подписке на маркет

В `Subscribe` (вызывается из `MarketInteractor.TryOpenMarket`):
```csharp
public void Subscribe()
{
    // Сейчас вызывает MarketClientState.RequestSubscribeMarket(zone.LocationId)
    // Нужно дополнительно: ContractClientState.RequestList(zone.LocationId)
    if (MarketClientState.Instance != null) MarketClientState.Instance.RequestSubscribeMarket(_currentZoneId);
    if (ContractClientState.Instance != null) ContractClientState.Instance.RequestList(_currentZoneId);
}
```

### 4.5. `MarketZone` — оставить как есть, удалить `ContractZone`

`ContractZone` и `ContractZoneRegistry` **удаляются**. Все вызовы `ContractZoneRegistry.Get(locationId)` в `ContractServer` заменяются на `MarketZoneRegistry.Get(locationId)`.

**Зачем это правильно:** `MarketZone` уже валидирует позицию (`IsPlayerInZone`), и контракты по `fromLocationId` привязаны к той же локации.

### 4.6. `MarketInteractor` — оставить как есть, удалить `ContractInteractor`

`ContractInteractor.TryOpenContractBoard` **удаляется**. Контракты открываются через `MarketInteractor.TryOpenMarket` → `MarketWindow.Subscribe` → авто-подписка на `ContractClientState.RequestList`.

### 4.7. `[ContractBoardWindow]` GO в `BootstrapScene` — удалить

Через MCP:
```bash
# manage_gameobject delete
manage_gameobject action=delete target="[ContractBoardWindow]"
```

### 4.8. `ContractTrigger.cs` — оставить, но `OpenContractBoard` → no-op (с warning)

Сейчас legacy fallback в `ContractTrigger.OpenContractBoard`:
```csharp
if (!ContractInteractor.TryOpenContractBoard())
{
    // legacy fallback на ContractBoardUI
    ContractBoardUI.Instance?.OpenBoard(null, player);
}
```

После рефактора:
```csharp
public void OpenContractBoard(NetworkPlayer player)
{
    // C2-этап: контракты теперь во вкладке "КОНТРАКТЫ" внутри MarketWindow.
    // Открываем рынок (если игрок в зоне MarketZone).
    ProjectC.Trade.Client.MarketInteractor.TryOpenMarket();
    // Никаких legacy fallback — ContractBoardUI/ContractBoardWindow удалены.
    // Сам ContractTrigger scene-marker оставлен для декорации (NPC-агент) и удалится в C1.
}
```

### 4.9. `ContractClientState` — остаётся как singleton-проекция

- `CurrentSnapshot` — последний снапшот
- `OnSnapshotUpdated` / `OnContractResult` — события
- `RequestList/Accept/Complete/Fail` — API для UI

`MarketWindow` подписывается на эти события в `OnEnable` / `OnDisable` (как сейчас на `MarketClientState`).

## 5. Файлы — что меняется

### Удалить (после успешного smoke-теста):
- `Assets/_Project/Trade/Scripts/Client/ContractBoardWindow.cs` (~22КБ)
- `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uxml`
- `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uss`
- `Assets/_Project/Trade/Scripts/Client/ContractInteractor.cs` (~4.5КБ)
- `Assets/_Project/Trade/Scripts/Network/ContractZone.cs` (~6.6КБ)
- `Assets/_Project/Trade/Scripts/Network/ContractZoneRegistry.cs` (~3.1КБ)

**Удалить GO** в `BootstrapScene.unity`: `[ContractBoardWindow]`.

### Изменить:
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml` — добавить 3-й таб + `contracts-section` + 3 action-кнопки (accept/complete/fail)
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uss` — реюз стилей, **0 новых классов** (если только `.action-btn.accept/.complete/.fail` не нужны — аналогия с `.buy/.sell/.load/.unload`)
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` — добавить 3-й таб + ListView + handlers + подписка на `ContractClientState`
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — заменить `ContractZoneRegistry.Get(locationId)` → `MarketZoneRegistry.Get(locationId)` (8 мест)
- `Assets/_Project/Trade/Scripts/ContractTrigger.cs` — упростить `OpenContractBoard` (только `MarketInteractor.TryOpenMarket`)

### Оставить без изменений:
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs`
- `Assets/_Project/Trade/Scripts/Core/ContractDebt.cs`
- `Assets/_Project/Trade/Scripts/Core/ContractWorldItemResolver.cs`
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` (только Registry.Get замены)
- `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractDto.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractSnapshotDto.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractResultDto.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractResultCode.cs`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (уже добавлены `ReceiveContractSnapshotTargetRpc/ReceiveContractResultTargetRpc`)
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` (auto-spawn `[ContractClientState]`)
- `Assets/_Project/Scenes/BootstrapScene.unity` — оставить `[ContractServer]` GO (нужен)
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — **удалить** `[NPCAgent_Primium]` (заменяется на `MarketZone_Primium`)

## 6. Пошаговый план

1. **Read** `MarketWindow.cs` (840 строк) полностью — особенно `EnsureBuilt`, `SwitchTab`, `Show/Hide`, `OnEnable/OnDisable`, `Subscribe` (если есть).
2. **Patch** `MarketWindow.uxml` — добавить 3-й таб + `contracts-section` + 3 action-кнопки.
3. **Patch** `MarketWindow.uss` — добавить `.action-btn.accept/.complete/.fail` (если нужны) — иначе 0 правок.
4. **Patch** `MarketWindow.cs` — добавить новые поля + ListView + handlers + подписку на `ContractClientState`.
5. **Patch** `ContractServer.cs` — заменить `ContractZoneRegistry.Get` → `MarketZoneRegistry.Get` (8 мест).
6. **Patch** `ContractTrigger.cs` — упростить `OpenContractBoard`.
7. **Удалить** (MCP): `[NPCAgent_Primium]` GO из `WorldScene_0_0.unity`.
8. **Удалить** (MCP): `[ContractBoardWindow]` GO из `BootstrapScene.unity`.
9. **Delete** 6 .cs/.uxml/.uss файлов (после успешного compile check).
10. **Compile** через `refresh_unity` + `read_console` → 0 errors.
11. **Save** сцены.
12. **Smoke test**: host → подойти к MarketZone_Primium → E → в MarketWindow вкладка КОНТРАКТЫ показывает 3 контракта → ВЗЯТЬ → …

## 7. Verification

```powershell
# 1. Compile
# Unity Editor → Console → 0 errors (4 FIX'а MarketWindow работают на 3-й таб)

# 2. Стартовый UI не перекрыт
# • Запусти Build & Play
# • До подхода к MarketZone — Host/Server buttons кликабельны
# • (раньше ContractBoardWindow.Show() съедал клики)

# 3. Smoke test
# • host → подойти к MarketZone_Primium
# • E → открылся MarketWindow
# • По умолчанию таб "РЫНОК" — видны товары (как раньше)
# • Клик "КОНТРАКТЫ" — появился 3-й таб
#   ↑ Доступно: Standard / Urgent / Receipt (3 строки)
# • Выбрать Standard → ВЗЯТЬ → "Контракт принят: [Стандарт] mesium x5"
# • В табе "МОИ КОНТРАКТЫ" (если вложенный) или в верхней части списка
#   ↑ 1 контракт с таймером 5:00
# • Долететь до другой MarketZone (или той же) → E → таб "КОНТРАКТЫ"
#   ↑ Активный контракт с таймером
# • СДАТЬ → "Контракт завершён! Награда: X CR"
# • Кредиты в HUD увеличились

# 4. Layout stability
# • Tab switching между РЫНОК / СКЛАД / КОНТРАКТЫ — плавно, без задержек
# • Окно не пересобирается на каждом E (FIX 4 MarketWindow защищает от этого)

# 5. Race conditions
# • Стресс-тест: быстрое нажатие tab-кнопок — нет дублирования ListView
# • ESC во время ввода — окно закрывается штатно (MarketWindow.Hide)
```

## 8. Документация

После успешного теста:
- `docs/Markets/README.md` — обновить раздел «Архитектура»
- `docs/Markets/ARCHITECTURE.md` — добавить секцию «Contracts as market tab»
- `docs/Markets/FIXES_HISTORY.md` — запись «2026-06-05: контракты объединены с MarketWindow»
- `docs/dev/CONTRACT_V2_MIGRATION.md` — пометить «v2.x» как завершённый, добавить ссылку на `MARKETS_V2_AUDIT_2026-06-05.md` §2.1 C2
- (опционально) `docs/gdd/GDD_22_Economy_Trading.md` — секция §6 «NPC-агенты НП» — пометить что доступ через рынок (нужен user approval по AGENTS.md)

## 9. Открытые вопросы / риски

1. **Сложность `MarketWindow.cs`.** Сейчас 840 строк. Добавление 3-го таба + ListView + handlers + подписки на ещё один singleton = +200-300 строк. Файл станет 1100+ строк. **Решение:** оставить как есть (один файл — одно окно), а в будущем возможно разделить на `MarketWindow` + `MarketTab`/`WarehouseTab`/`ContractsTab` (паттерн partial class или sub-controller). Не в этом этапе.

2. **`ContractDto` импорт в `MarketWindow.cs`.** Сейчас `MarketWindow.cs` не знает про `ContractDto`. Добавляем `using ProjectC.Trade.Dto;` (уже есть) + `using ProjectC.Trade;` (для `ContractType` enum — возможно, понадобится). Минимально инвазивно.

3. **`ContractZoneRegistry` vs `MarketZoneRegistry`.** Сейчас два реестра. В `ContractServer.cs` есть 4 места с `ContractZoneRegistry.Get(locationId)` — заменяем на `MarketZoneRegistry.Get(locationId)`. **Перед удалением `ContractZone` файлов** — проверить grep, что никто кроме `ContractServer` и `ContractInteractor` не использует `ContractZoneRegistry`.

4. **3 NPC-агента в других городах** (secundus/tertius/quartus) — я не добавлял в `WorldScene_0_0` (только primium). Сейчас `[NPCAgent_Primium]` в `WorldScene_0_0.unity` тоже удалится (он не нужен — `MarketZone_Primium` уже там). Когда streaming раскроет остальные 23 сцены — `ContractZone` в них тоже не нужны.

5. **Таймер контрактов в HUD** — `ContractClientState` остаётся, но в HUD сейчас нет countdown-индикатора. Это отдельный тикет (если нужен). Не в этом этапе.

6. **Нотификация о новом контракте** (pop-up) — `ContractClientState.OnContractResult` остаётся, но `MarketWindow.HandleContractResult` показывает только message. Если хотим pop-up — отдельный тикет.

7. **`[ContractServer]` GO в `BootstrapScene` остаётся.** Нужен для RPC. `NetworkObject` + `ContractServer` компоненты — без изменений.

## 10. Файлы для создания/изменения (summary)

**Удалить (6 файлов):**
- `Assets/_Project/Trade/Scripts/Client/ContractBoardWindow.cs`
- `Assets/_Project/Trade/Scripts/Client/ContractInteractor.cs`
- `Assets/_Project/Trade/Scripts/Network/ContractZone.cs`
- `Assets/_Project/Trade/Scripts/Network/ContractZoneRegistry.cs`
- `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uxml`
- `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uss`

**Удалить GO (через MCP):**
- `[ContractBoardWindow]` в `BootstrapScene.unity`
- `[NPCAgent_Primium]` в `WorldScene_0_0.unity`

**Изменить (4 файла):**
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml` — +3-й таб + contracts-section + 3 action-кнопки
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uss` — возможно +3 класса кнопок (`.accept/.complete/.fail`)
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` — +3-й таб + ListView + handlers + подписки (+200-300 строк)
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — заменить `ContractZoneRegistry.Get` → `MarketZoneRegistry.Get` (8 мест)
- `Assets/_Project/Trade/Scripts/ContractTrigger.cs` — упростить `OpenContractBoard` (~10 строк)

**Оставить без изменений (11 файлов):**
- `Core/ContractWorld.cs`, `Core/ContractDebt.cs`, `Core/ContractWorldItemResolver.cs`
- `Network/ContractServer.cs` (только Registry)
- `Client/ContractClientState.cs`
- `Dto/ContractDto.cs`, `Dto/ContractSnapshotDto.cs`, `Dto/ContractResultDto.cs`, `Dto/ContractResultCode.cs`
- `Scripts/Player/NetworkPlayer.cs` (RPC уже добавлены)
- `Scripts/Core/NetworkManagerController.cs` (auto-spawn уже добавлен)
- `Scenes/BootstrapScene.unity` (`[ContractServer]` GO остаётся)

**Оставить legacy (3 файла, удалятся в C1-cleanup):**
- `ContractSystem.cs` (838 строк)
- `ContractBoardUI.cs` (549 строк)
- `ContractData.cs` (259 строк — POCO для `ContractWorld`)

**Документация (после теста):**
- `docs/Markets/README.md` — обновить
- `docs/Markets/ARCHITECTURE.md` — добавить секцию
- `docs/Markets/FIXES_HISTORY.md` — запись
- `docs/dev/CONTRACT_V2_MIGRATION.md` — пометить завершённым
- (опционально) `docs/gdd/GDD_22_Economy_Trading.md` — по user approval

---

**Связанные документы:**
- `docs/dev/CONTRACT_V2_MIGRATION.md` — текущий этап C2
- `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md` §2.1 C2, §4 этап 1
- `docs/Markets/FIXES_HISTORY.md` 2026-06-04 (4 FIX'а MarketWindow)
- `docs/Markets/INTEGRATION.md` — связи
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml` — текущий UI
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` — текущий UI-контроллер (840 строк)
- `AGENTS.md` — hard rules
