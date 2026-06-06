# MetaRequirement Subsystem — Дизайн-документ (Этап 1)

**Подсистема:** Унифицированный замок-ключ (lock-key) — обобщение Ship Key Subsystem
**Тег:** `meta-requirement`, `lock-key`, `quest-items`, `requirement-registry`
**Статус:** 📋 Планируется (Этап 1, оценка 3-4 часа)
**Дата:** 2026-06-06
**Предшественник:** `docs/Ships/Key-subsystem/00_OVERVIEW.md` (Ship Key Subsystem MVP)

---

## 1. Цель

Обобщить текущую **Ship Key Subsystem** (где корабль требует 1 ключ) в **универсальную систему требований** для любых `Interactable`-объектов: корабли, двери, контейнеры, терминалы, квестовые зоны, NPC и т.д. Система должна поддерживать **массив требуемых предметов** (от 1 до N) с различной логикой (ALL / ANY / AT_LEAST_N).

### 1.1 Что остаётся неизменным

Эти компоненты **уже работают** для текущего use case и должны **остаться без изменений** в API:

| Компонент | Файл | Почему не трогаем |
|---|---|---|
| `InventoryWorld` core | `Assets/_Project/Items/Core/InventoryWorld.cs` | Generic, работает для всех предметов. Только **добавляем** extension-методы, не ломаем существующее API |
| `PickupItem` | `Assets/_Project/Scripts/Core/PickupItem.cs` | Standard pipeline pickup'а. Не зависит от типа требования |
| `ItemData` | `Assets/_Project/Scripts/Core/ItemType.cs` | SO. Generic контейнер метаданных |
| `InventoryClientState` | `Assets/_Project/Items/Client/InventoryClientState.cs` | Клиентская проекция инвентаря. Generic |
| `InventoryServer` | `Assets/_Project/Items/Network/InventoryServer.cs` | RPC-hub инвентаря. Generic |
| `NetworkPlayer.F-key` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | **Один entry point** для взаимодействия. Проверка доступа делегируется через `InteractableManager` |
| `InteractableManager` | `Assets/_Project/Scripts/Core/InteractableManager.cs` | Registry взаимодействуемых объектов. Generic — работает с любым `IInteractable` |

### 1.2 Что переименовываем / обобщаем

| Текущее имя (Ship Key) | Новое имя (MetaRequirement) | Причина |
|---|---|---|
| `ShipKeyBinding` (MonoBehaviour) | `MetaRequirement` (MonoBehaviour) | Generic — не корабль-специфичный |
| `ShipKeyServer` (NetworkBehaviour hub) | `MetaRequirementRegistry` (NetworkBehaviour hub) | Generic — реестр любых требований |
| `ShipKeyClientState` (singleton projection) | `MetaRequirementClientState` (singleton projection) | Generic — клиентская проекция |
| `ShipKeyToast` (UIDocument UI) | `MetaRequirementToast` (UIDocument UI) | Generic — UI с списком недостающих |
| `ShipKeyBinding._keyItemData` (1 ItemData) | `MetaRequirement._requiredItems` (ItemData[]) | Массив |
| `ShipKeyBinding._shipDisplayName` (string) | `MetaRequirement._interactableDisplayName` (string) | Generic |

### 1.3 Что добавляем (новое)

| Новый компонент | Файл | Назначение |
|---|---|---|
| `RequirementLogic` enum | `Assets/_Project/Scripts/MetaRequirement/RequirementLogic.cs` | `All` / `Any` / `AtLeastN` |
| `MetaRequirement.CanPlayerUse(ulong clientId, out string reason)` | В `MetaRequirement.cs` | Локальный helper (используется UI для tooltip'а "у вас X/N") |
| `MetaRequirement.OnInventoryChanged` event | В `MetaRequirement.cs` | UI может обновить прогресс-индикатор при изменении инвентаря |
| `InventoryWorld.HasAllItems(ulong clientId, int[] itemIds)` | В `InventoryWorld.cs` (extension) | Серверная проверка AND-логики |
| `InventoryWorld.HasAnyItem(ulong clientId, int[] itemIds)` | В `InventoryWorld.cs` (extension) | Серверная проверка OR-логики |
| `InventoryWorld.CountOf(ulong clientId, int itemId)` | В `InventoryWorld.cs` (extension) | Подсчёт сколько есть у игрока (для AT_LEAST_N) |
| `InventoryClientState.CountOf(int itemId)` | В `InventoryClientState.cs` (extension) | Клиентский mirror (для UI) |
| `MetaRequirement.ProgressInfo` struct | В `MetaRequirement.cs` | Данные для UI: "3/5 ключей собрано" |

---

## 2. Архитектура

### 2.1 Концептуальная диаграмма

```
┌─────────────────────── SERVER (host) ──────────────────────┐
│                                                            │
│  [MetaRequirementRegistry] : NetworkBehaviour             │
│    • RegisterMetaRequirement(netId, MetaRequirement)     │
│    • CanPlayerUse(clientId, netId) → bool + reason      │
│      → InventoryWorld.HasAllItems/HasAnyItem/CountOf    │
│    • RequestCanUseRpc(netId)                              │
│      → TargetRpc обратно клиенту                         │
│                                                            │
│  MetaRequirement на любом GameObject:                     │
│    • _requiredItems : ItemData[]                          │
│    • _logic : RequirementLogic { All, Any, AtLeastN }     │
│    • _requiredCount : int (для AtLeastN)                  │
│    • _interactableDisplayName : string                    │
│    • OnNetworkSpawn → RegisterMetaRequirement            │
│    • ServerKeyItemIds[] (lazy resolve через              │
│      GetOrRegisterItemId на сервере)                      │
└────────────────────────────────────────────────────────────┘
                ▲     │  TargetRpc
                │     ▼
┌─────────────────────── CLIENT ─────────────────────────────┐
│                                                            │
│  [MetaRequirementClientState] : MonoBehaviour (singleton)  │
│    • Projections : dict<netId, MetaRequirementDto>        │
│    • OnBindingsUpdated event                              │
│    • OnAccessDenied event (netId, reason) → UI toast      │
│    • RequestCanUse(netId) → RPC                           │
│                                                            │
│  [MetaRequirementToast] : MonoBehaviour (UIDocument)      │
│    • Subscribe OnAccessDenied → show toast                │
│    • Toast: "Нет ключа корабля X" или "Нужно: A, B, C"   │
│                                                            │
│  NetworkPlayer.F-key (БЕЗ ИЗМЕНЕНИЙ, single entry):      │
│    if _inShip: SubmitSwitchModeRpc()                      │
│    else: FindNearestInteractable() → check access         │
│         → RequestCanUse(nearestInteractable.NetId)       │
└────────────────────────────────────────────────────────────┘
```

### 2.2 Изменения в `NetworkPlayer.F-key` (минимальные)

**Сейчас:** `FindNearestShip()` → `RequestCanBoard(shipNetId)`
**Будет:** `FindNearestInteractable()` (новый метод в `InteractableManager`) → `MetaRequirementClientState.RequestCanUse(interactableNetId)`

`FindNearestInteractable` — новый метод в `InteractableManager`:
- Ищет ближайший `IInteractable` (с `Collider`) в радиусе `boardDistance` (или новый `_interactDistance`)
- Возвращает `GameObject` или `NetworkObject` (по типу)
- **Не** ограничен кораблями

**Один entry point** остаётся — F-key. Просто теперь он general.

### 2.3 Структура `MetaRequirement` MonoBehaviour

```csharp
public class MetaRequirement : NetworkBehaviour
{
    [Header("Required Items")]
    [SerializeField] private ItemData[] _requiredItems;
    [SerializeField] private RequirementLogic _logic = RequirementLogic.All;
    [Tooltip("Только для AtLeastN: минимальное количество разных items из списка")]
    [SerializeField] private int _requiredCount = 1;
    [Tooltip("Забрать предметы из инвентаря после успешного использования?")]
    [SerializeField] private bool _consumeOnUse = false;
    
    [Header("UI")]
    [SerializeField] private string _interactableDisplayName = "Object";
    [Tooltip("Кастомное сообщение при отсутствии (если пусто — генерируется автоматически)")]
    [SerializeField] private string _customFailureMessage = "";
    
    // Server-side resolved ids
    private int[] _serverItemIds = new int[0];
    public int[] ServerItemIds => _serverItemIds;  // lazy resolve
    
    // Server-side API
    public bool CanPlayerUse(ulong clientId, out string reason);
    public bool ConsumeRequiredItems(ulong clientId);  // для _consumeOnUse=true
    public ProgressInfo GetPlayerProgress(ulong clientId);  // для UI tooltip'а
}
```

**Важно:** `MetaRequirement` — это `NetworkBehaviour`, **не** `MonoBehaviour`. Нужен `NetworkObject` на GameObject. По аналогии с `ShipKeyBinding` (тоже `NetworkBehaviour`).

### 2.4 Расширения `InventoryWorld`

**Сейчас:** `public bool HasItem(ulong clientId, int itemId)` (1 предмет).

**Добавить (не ломая существующее API):**
```csharp
// Расширения (новые методы):
public bool HasAllItems(ulong clientId, int[] itemIds);
public bool HasAnyItem(ulong clientId, int[] itemIds);
public int CountOf(ulong clientId, int itemId);  // сколько штук у игрока
public int[] GetMissingItems(ulong clientId, int[] itemIds);  // каких нет
```

**Существующее `HasItem` остаётся** — backward compatible для текущих callers (включая `MetaRequirementRegistry`).

### 2.5 Структура `RequirementLogic` enum

```csharp
public enum RequirementLogic
{
    /// <summary>ВСЕ предметы из списка должны быть у игрока. AT_LEAST=1 не имеет смысла.</summary>
    All,
    
    /// <summary>ХОТЯ БЫ ОДИН предмет из списка должен быть у игрока.</summary>
    Any,
    
    /// <summary>Как минимум _requiredCount РАЗНЫХ предметов из списка. Например: 3 из 5.</summary>
    AtLeastN,
}
```

**Примеры:**
- 1 ключ от корабля: `_requiredItems=[Key1]`, `_logic=All` (1 из 1)
- 5 железных слитков (одного типа): `_requiredItems=[IronIngot]`, `_logic=All` (1 разных из 1 списка, но нужен `CountOf >= 5`) — **см. ниже про stackable**
- Любой 1 из 3 ключей: `_requiredItems=[Key1, Key2, Key3]`, `_logic=Any`
- 3 из 5 фрагментов карты: `_requiredItems=[Fragment1..5]`, `_logic=AtLeastN`, `_requiredCount=3`
- Все 8 ключей мира: `_requiredItems=[Key1..8]`, `_logic=All`

### 2.6 Кейс "5 слитков одного типа"

Текущий `InventoryWorld` хранит `List<int>` (itemId'ы, не стек). `CountOf` = размер списка. Если игрок подобрал 5 железных слитков (id=N), `CountOf(N) == 5`. **Это работает без stackable-quantity refactor** — каждый слиток это отдельный itemId в списке.

Если в будущем введут stackable (quantity > 1), `CountOf` нужно будет переписать на `Sum(quantity) where itemId == N`. **Это TODO на потом**, не блокирует Этап 1.

### 2.7 Структура `ProgressInfo` (для UI)

```csharp
public struct ProgressInfo
{
    public int Required;       // сколько нужно
    public int Have;           // сколько есть
    public int[] MissingIds;   // id недостающих
    public bool Satisfied;     // выполнено ли требование
    
    public string ToString() => $"Прогресс: {Have}/{Required}";
}
```

UI (tooltip при наведении) использует `ProgressInfo`:
- `2/5 ключей собрано`
- `Не хватает: [IronKey, OldMap]`

### 2.8 Toast — что показывать

**Текущий:** "Нет ключа корабля (Корабль Light)"

**Будущий (списком):**
- Если 1 предмет: "Нет ключа X (для Y)"
- Если много: "Не хватает предметов для Y: A, B, C"
- Если есть кастомное сообщение: использовать его
- Прогресс: "Прогресс: 2/5"

Сейчас в `MetaRequirementToast` строится одна строка. **TODO на v2** — multiline toast с списком и иконками предметов.

---

## 3. Точки вставки в существующий код

### 3.1 Файлы которые **переименовываем** (с backward-compat алиасами)

| Текущий | Новый | Алиас для совместимости |
|---|---|---|
| `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs` | `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` | `public class ShipKeyBinding : MetaRequirement { }` (в том же файле, deprecated) |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyServer.cs` | `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs` | `public class ShipKeyServer : MetaRequirementRegistry { }` (deprecated) |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyClientState.cs` | `Assets/_Project/Scripts/MetaRequirement/MetaRequirementClientState.cs` | `public class ShipKeyClientState : MetaRequirementClientState { }` (deprecated) |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyToast.cs` | `Assets/_Project/Scripts/MetaRequirement/MetaRequirementToast.cs` | `public class ShipKeyToast : MetaRequirementToast { }` (deprecated) |

**Почему алиасы:** сцены `BootstrapScene.unity` и `WorldScene_0_0.unity` уже ссылаются на `ShipKeyBinding` через GUID компонента. Если переименовать **только** файл, GUID `.meta` сохранится, но если оставить **только новый класс без алиаса**, scene-prefab references сломаются (NullRef при открытии сцены).

**Решение:** создаём новые файлы + добавляем в старые файлы `class ShipKeyBinding : MetaRequirement {}` (пустой subclass) с `[Obsolete]` атрибутом. Через 1-2 релиз-цикла удаляем.

### 3.2 Файлы которые **редактируем** (extensions)

| Файл | Что добавляем |
|---|---|
| `Assets/_Project/Items/Core/InventoryWorld.cs` | `HasAllItems`, `HasAnyItem`, `CountOf`, `GetMissingItems` |
| `Assets/_Project/Items/Client/InventoryClientState.cs` | `CountOf` (mirror для UI) |
| `Assets/_Project/Scripts/Core/InteractableManager.cs` | `FindNearestInteractable(float range)` — generic версия `FindNearestShip` |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | Минимальные изменения в F-key: использовать `FindNearestInteractable` + `MetaRequirementClientState.RequestCanUse` |
| `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | `CreateMetaRequirementClientState()` (по аналогии с `CreateShipKeyClientState`) |

### 3.3 Файлы которые **создаём** (новые)

| Файл | Назначение |
|---|---|
| `Assets/_Project/Scripts/MetaRequirement/RequirementLogic.cs` | enum `All` / `Any` / `AtLeastN` |
| `Assets/_Project/Scripts/MetaRequirement/ProgressInfo.cs` | struct для UI |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` | основной MonoBehaviour |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs` | server-side NetworkBehaviour hub |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirementClientState.cs` | client-side singleton |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirementToast.cs` | UI toast |
| `Assets/_Project/UI/Resources/UI/MetaRequirementPanelSettings.asset` | dedicated PanelSettings |

### 3.4 Сцены

| Сцена | Что меняем |
|---|---|
| `BootstrapScene.unity` | `[ShipKeyServer]` → переименовать в `[MetaRequirementRegistry]` (или оставить как есть — алиас). Добавить `[MetaRequirementToast]` рядом с `[ShipKeyToast]` (алиас). |
| `WorldScene_0_0.unity` | На 3 кораблях: `[ShipKeyBinding]` (1 item) → `[MetaRequirement]` с 1-элементным массивом. **Behaviorally идентично**, но generic. |

**Рекомендация:** НЕ переименовывать scene-объекты через MCP, чтобы не сломать `.prefab` references. Просто заменить **компонент** на корабле (Remove ShipKeyBinding → Add MetaRequirement) и перетащить `_keyItemData` → `_requiredItems[0]`.

### 3.5 Документация

| Файл | Что делаем |
|---|---|
| `docs/Ships/Key-subsystem/00_OVERVIEW.md` | Добавляем секцию "## Migration to MetaRequirement" с roadmap |
| `docs/Ships/Key-subsystem/KNOWN_ISSUES.md` | Закрываем R2-SHIP-KEY-001 (itemId) — баг исправлен |
| `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` | **Новый** — пошаговый migration guide |
| `docs/MetaRequirement/00_OVERVIEW.md` | **Новый** — полный дизайн-документ (по образцу `00_OVERVIEW.md` Key Subsystem) |
| `docs/MetaRequirement/RECIPES.md` | **Новый** — примеры конфигураций для разных кейсов |
| `docs/MetaRequirement/KNOWN_ISSUES.md` | **Новый** — для будущих багов |

---

## 4. Пошаговый план реализации (Этап 1)

**Оценка: 3-4 часа.** Каждый шаг — отдельный коммит, легко откатываемый.

### Шаг 1: Extension-методы в `InventoryWorld` (30 мин)

**Что:**
- `HasAllItems(ulong clientId, int[] itemIds)` — проверяет что **все** ids присутствуют (с учётом повторов в списке, через LINQ или `foreach`)
- `HasAnyItem(ulong clientId, int[] itemIds)` — проверяет что **хотя бы один** id присутствует
- `CountOf(ulong clientId, int itemId)` — возвращает количество
- `GetMissingItems(ulong clientId, int[] itemIds)` — возвращает массив недостающих

**Куда:** `Assets/_Project/Items/Core/InventoryWorld.cs`, в секцию `Public API — per-player inventory`.

**Тест:** `inventoryServer.AddItemDirect(clientId, 31, ItemType.Equipment);` → `HasAllItems(c, [31]) == true`.

**Коммит:** `feat(items): InventoryWorld extensions — HasAllItems/HasAnyItem/CountOf/GetMissingItems (backward compatible)`

### Шаг 2: `MetaRequirement` MonoBehaviour (45 мин)

**Что:**
- Создаём `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs`
- Поля: `_requiredItems[]`, `_logic`, `_requiredCount`, `_consumeOnUse`, `_interactableDisplayName`, `_customFailureMessage`
- Server-side: `OnNetworkSpawn` → `MetaRequirementRegistry.RegisterRequirement(netId, this)`
- Server-side: `ServerItemIds[]` (lazy resolve через `InventoryWorld.GetOrRegisterItemId`)
- Public API: `CanPlayerUse(clientId, out reason)`, `GetPlayerProgress(clientId)`
- Алиас в `ShipKeyBinding.cs`: `public class ShipKeyBinding : MetaRequirement { }` с `[Obsolete]`

**Тест:** Создаём пустой GameObject с `NetworkObject` + `MetaRequirement` с 1 ItemData → проверяем что `OnNetworkSpawn` вызывается.

**Коммит:** `feat(meta-requirement): MetaRequirement MonoBehaviour with backward-compat ShipKeyBinding alias`

### Шаг 3: `MetaRequirementRegistry` server hub (45 мин)

**Что:**
- Создаём `MetaRequirementRegistry.cs` (по образцу `ShipKeyServer.cs`)
- `RegisterRequirement(netId, MetaRequirement)`, `UnregisterRequirement(netId)`
- `CanPlayerUse(clientId, netId)` — **generic** версия (работает с `MetaRequirement`, не только кораблями)
- `[Rpc(SendTo.Server, InvokePermission=Owner)] RequestCanUseRpc(netId)` → ответ через `NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc`
- Push биндингов клиенту при `OnClientConnected` (как у `ShipKeyServer`)
- Алиас `ShipKeyServer` (deprecated)

**Тест:** В `WorldScene_0_0` ставим `[MetaRequirementRegistry]` (аналог `[ShipKeyServer]`) + `MetaRequirement` на дверь → проверяем что registry знает.

**Коммит:** `feat(meta-requirement): MetaRequirementRegistry server hub (replaces ShipKeyServer)`

### Шаг 4: `MetaRequirementClientState` (30 мин)

**Что:**
- Создаём `MetaRequirementClientState.cs` (по образцу `ShipKeyClientState.cs`)
- `RequestCanUse(netId)` → шлёт RPC
- `OnAccessDenied(reason)` event → UI подписывается
- `OnBindingsUpdated` event → UI обновляет прогресс
- Auto-spawn в `NetworkManagerController.CreateMetaRequirementClientState()`
- Алиас `ShipKeyClientState` (deprecated)

**Тест:** После `StartHost` `MetaRequirementClientState.Instance != null`, подписан на events.

**Коммит:** `feat(meta-requirement): MetaRequirementClientState singleton projection`

### Шаг 5: `MetaRequirementToast` UI (30 мин)

**Что:**
- Создаём `MetaRequirementToast.cs` (по образцу `ShipKeyToast.cs`, но с расширенным reason)
- Показывает **список** недостающих предметов (если reason это список)
- Dedicated `MetaRequirementPanelSettings.asset`
- Алиас `ShipKeyToast` (deprecated)

**Тест:** В `BootstrapScene` добавляем `[MetaRequirementToast]` → в Play mode deny → toast с списком.

**Коммит:** `feat(meta-requirement): MetaRequirementToast with multi-item reason support`

### Шаг 6: Wire через `NetworkPlayer.F-key` (30 мин)

**Что:**
- В `Assets/_Project/Scripts/Core/InteractableManager.cs` добавляем `FindNearestInteractable(float range)` (generic — ищет любой `IInteractable` с `Collider`)
- В `NetworkPlayer.Update` F-key блок: вместо `FindNearestShip` → `FindNearestInteractable`. Если нашли что-то с `MetaRequirement` → `RequestCanUse`. Иначе — fallback.
- Минимальное изменение: 5-10 строк

**Тест:** Подойти к любому GameObject с `MetaRequirement` → F → проверка.

**Коммит:** `feat(meta-requirement): NetworkPlayer.F-key uses generic FindNearestInteractable`

### Шаг 7: Обновить сцены (20 мин)

**Что:**
- В `WorldScene_0_0` на 3 кораблях: Remove `ShipKeyBinding` → Add `MetaRequirement`. Перенести `_keyItemData` → `_requiredItems[0]`. `_shipDisplayName` → `_interactableDisplayName`.
- В `BootstrapScene` — оставить как есть (алиасы совместимы).
- Save обе сцены.

**Тест:** Manual test: подобрать ключ → сесть в корабль. Поведение должно быть **идентично** до рефакторинга.

**Коммит:** `refactor(scene): replace ShipKeyBinding with MetaRequirement in WorldScene_0_0`

### Шаг 8: Документация + tests (30 мин)

**Что:**
- `docs/MetaRequirement/00_OVERVIEW.md` — полный дизайн
- `docs/MetaRequirement/RECIPES.md` — 5-6 примеров (квест, дверь, босс, etc.)
- `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — migration guide
- Закрыть R2-SHIP-KEY-001 в `KNOWN_ISSUES.md`

**Коммит:** `docs(meta-requirement): overview + recipes + migration guide`

**ИТОГО: ~4 часа** (с запасом на отладку).

---

## 5. Backward compatibility

### 5.1 Алиасы (ship-specific → generic)

```csharp
// В Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs (старый файл)
[System.Obsolete("Use MetaRequirement. ShipKeyBinding kept as alias for backward compat.")]
public class ShipKeyBinding : ProjectC.MetaRequirement.MetaRequirement { }

// В Assets/_Project/Scripts/Ship/Key/ShipKeyServer.cs
[System.Obsolete("Use MetaRequirementRegistry.")]
public class ShipKeyServer : ProjectC.MetaRequirement.MetaRequirementRegistry { }
```

**Важно:** `.meta`-файлы с GUID сохраняем. Сцены ссылаются на GUID, не на class name.

### 5.2 Сцена-совместимость

- Старые `ShipKeyBinding`-компоненты в сценах: продолжают работать через алиас.
- Новые `MetaRequirement` компоненты: работают наряду.
- Через 1-2 релиз-цикла: удаляем алиасы → пересобираем сцены.

### 5.3 Проверка совместимости

**Тест:** после Шага 7 запустить в Play mode:
- Подобрать `[KeyRod_ShipLight]`
- F на `Ship_Light` → **должен** сесть
- В Console: `[MetaRequirementRegistry] CanPlayerUse: client=0, obj=N, allowed=True`

**Если не работает** — `ShipKeyBinding` алиас сломан. Чек: `OnNetworkSpawn` вызывается у алиаса? Должен, т.к. он `NetworkBehaviour`.

---

## 6. Edge cases

### 6.1 Пустой `_requiredItems[]`

**Проблема:** Если массив пуст и `_logic=All` → всегда `true` (нет требований). С `Any` → `false` (нет ничего). С `AtLeastN` → `true` если `N <= 0`.

**Решение:** `MetaRequirement.OnValidate` → warning: "Empty _requiredItems. Object will always be accessible (or always denied, depending on _logic)."

### 6.2 Дубликаты в `_requiredItems[]`

**Проблема:** Если `[Key1, Key1, Key2]` с `All` → `HasAllItems` должен найти Key1 дважды, но в инвентаре только 1 копия.

**Решение (v1):** `HasAllItems` использует `HashSet<int>` внутренне → дубликаты игнорируются. Игрок подобрал Key1 один раз → удовлетворено.

**Решение (v2, TODO):** Флаг `_allowDuplicates = false` (default) → `OnValidate` warning при дубликатах.

### 6.3 ServerKeyItemIds resolves после InventoryWorld.CreateAndInitialize

**Проблема:** `MetaRequirement.OnNetworkSpawn` вызывается на сервере, но `InventoryWorld.Instance` ещё может быть null (если `[InventoryServer].OnNetworkSpawn` отработал позже).

**Решение:** Lazy resolve в `ServerItemIds` getter (как у текущего `ShipKeyBinding.ServerKeyItemId`). Не падать если null, просто возвращать пустой массив.

### 6.4 Scene transition во время доступа

**Проблема:** Игрок жмёт F, RPC уходит, **сцена стримится**, объект деспавнится. Ответ RPC теряется или указывает на несуществующий netId.

**Решение:** `MetaRequirementClientState.OnAccessDenied` фильтрует stale-ids (если `netId` уже не в `Projections`). UI toast подавляется.

### 6.5 Race F-key с двойным нажатием

**Проблема:** Два F за 0.2 сек → два RPC → второй отменяет cooldown первого.

**Решение:** `NetworkPlayer._lastCanUseRequestTime` + `_pendingCanUseNetId` (1.5 сек timeout). Уже есть в коде для `ShipKeyServer` — переиспользуем.

### 6.6 `consumeOnUse` race condition

**Проблема:** Игрок нажал F на двух объектах одновременно, оба требуют **один и тот же** предмет. Должны ли оба сработать?

**Решение v1:** Только один успевает (`CanPlayerUse` второй раз вернёт `false`). Принимаем это как ограничение MVP.

**Решение v2 (TODO):** Транзакционный паттерн: блокируем предметы в инвентаре на время RPC (`reserveCount`), освобождаем по таймауту.

---

## 7. Что НЕ входит в Этап 1 (TODO)

- **Crafting** — отдельная подсистема с транзакциями (input → output). Не делаем сейчас.
- **Stackable quantity (count > 1)** — текущий `InventoryWorld` хранит `List<int>`, не quantity. `CountOf` работает через длину списка. Когда quantity refactor будет готов — обновить.
- **Multi-progress UI (5/8 с иконками)** — показываем одной строкой. V2 — multiline с иконками.
- **Conditions** (время суток, репутация, фракция) — отдельная подсистема `Conditions` (похожая на MetaRequirement, но не про предметы).
- **Persistent requirements** (сохранение состояния "собрал 3 из 5" между сессиями) — не блокирует MVP, делаем когда persistence инвентаря будет готов.
- **Hot-wire / взлом** (обход требования без предметов) — отдельная фича.
- **TTL на pickup'ы** (предметы пропадают через N часов) — не в скоупе.

---

## 8. Проверочный чек-лист (Definition of Done)

После Этапа 1 должно быть:

- [ ] `InventoryWorld.HasAllItems/HasAnyItem/CountOf/GetMissingItems` — работают, юнит-тесты или ручной verify
- [ ] `MetaRequirement` MonoBehaviour — есть в `Assets/_Project/Scripts/MetaRequirement/`
- [ ] `MetaRequirementRegistry` NetworkBehaviour — есть, спавнится в BootstrapScene через `ScenePlacedObjectSpawner`
- [ ] `MetaRequirementClientState` — auto-spawn в `NetworkManagerController.CreateMetaRequirementClientState`
- [ ] `MetaRequirementToast` — есть в BootstrapScene, показывает toast при deny
- [ ] `MetaRequirementPanelSettings` — dedicated `.asset`
- [ ] `ShipKeyBinding`/`ShipKeyServer`/`ShipKeyClientState`/`ShipKeyToast` — алиасы работают, не сломали существующий flow
- [ ] `WorldScene_0_0` — на 3 кораблях `MetaRequirement` (1-элементный массив). Поведение **идентично** до рефакторинга.
- [ ] `NetworkPlayer.F-key` — использует `FindNearestInteractable`. Manual test: F на корабле без ключа → toast с "Нет ключа X". F на корабле с ключом → сесть.
- [ ] `docs/MetaRequirement/00_OVERVIEW.md` — полный дизайн
- [ ] `docs/MetaRequirement/RECIPES.md` — 5+ примеров
- [ ] `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — migration guide
- [ ] **Manual test в Play mode**:
  - [ ] Подобрать `[KeyRod_ShipLight]` → сесть в `Ship_Light`
  - [ ] Без ключа → toast
  - [ ] `[KeyRod_ShipMedium]` → сесть в `Ship_Medium`
  - [ ] `[KeyRod_ShipHeavy]` → сесть в `Ship_Heavy`
  - [ ] Подойти к `[Pickup_Res_1]` (resource, не ключ) → подобрать, не должно конфликтовать
  - [ ] F на 3 сундука (`Chest_Main/North/East`) — должны открываться без ключа (для проверки что MetaRequirement не цепляется ко всему)

---

## 9. Ссылки

- **Предшественник:** `docs/Ships/Key-subsystem/00_OVERVIEW.md` (Ship Key Subsystem MVP)
- **Bug-report:** `docs/Ships/Key-subsystem/KNOWN_ISSUES.md` (R2-SHIP-KEY-001)
- **Inventory v2:** `docs/dev/INVENTORY_V2_REFACTOR.md` (как устроен `InventoryWorld` core)
- **Singleton pattern:** `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` (ContractClientState — образец)
- **MCP skill:** `unity-mcp-orchestrator` SKILL.md — pitfalls #22 (тип не подхватывается), #29 (dedicated PanelSettings), #37 (singleton race)
- **UI pitfall:** pitfall #43 (label-as-sibling Z-order) — для будущего multiline toast
- **Scene-spawn footgun:** pitfall #31 — если [MetaRequirementRegistry] не спавнится → NRE в RPC

---

## 10. История изменений

| Дата | Версия | Изменение |
|---|---|---|
| 2026-06-06 | 0.1 | Первичный дизайн-документ. Создан на основе Ship Key Subsystem MVP |
| 2026-06-06 | 0.2 | Добавлен backward compatibility раздел (алиасы), edge cases, plan из 8 шагов |
