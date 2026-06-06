# MetaRequirement — Inspector Reference

**Документ:** подробное описание каждого поля Inspector для `MetaRequirement`,
`MetaRequirementRegistry`, `MetaRequirementClientState`, `MetaRequirementToast`, `LockBox`.
**Дата:** 2026-06-06

---

## 1. `MetaRequirement` (NetworkBehaviour)

Вешается на любой GameObject рядом с `NetworkObject`. Содержит серверную часть требований.

### 1.1 Header "Требования (предметы)"

#### `_requiredItems : ItemData[]`
- **Тип:** массив `ProjectC.Items.ItemData` (ScriptableObject)
- **Что:** какие предметы нужны для доступа
- **Важно:** ItemData должны лежать в `Resources/Items/` (НЕ в подпапках) — `Resources.LoadAll` не рекурсивен
- **Поведение при пустом массиве:** см. `OnValidate` (warning)
- **Поведение при дубликатах:** `HasAllItems` через `HashSet` — дубликаты игнорируются (OnValidate предупредит)
- **Пример:** для корабля с 1 ключом — `_requiredItems = [Key_Light]`. Для босс-зоны — `[Amulet_Sun, Amulet_Moon, Amulet_Star]`

#### `_logic : RequirementLogic`
- **Тип:** enum (`All` / `Any` / `AtLeastN`)
- **Дефолт:** `All`
- **`All`** — нужны ВСЕ предметы из `_requiredItems[]` (порядок не важен)
- **`Any`** — нужен ХОТЯ БЫ ОДИН из списка
- **`AtLeastN`** — нужно минимум `_requiredCount` РАЗНЫХ предметов из списка (см. ниже)
- **Примеры:**
  - 1 ключ: `_logic=All`, `_requiredItems=[Key1]`
  - Альтернативы: `_logic=Any`, `_requiredItems=[Key1, Key2, Key3]`
  - 3 из 5 фрагментов: `_logic=AtLeastN`, `_requiredCount=3`, `_requiredItems=[F1, F2, F3, F4, F5]`

#### `_requiredCount : int`
- **Тип:** int, диапазон `[1, N]`, `Min(1)` attribute
- **Используется ТОЛЬКО для `AtLeastN`** (для `All`/`Any` игнорируется)
- **Что:** сколько РАЗНЫХ предметов из списка должен иметь игрок
- **Пример:** для 3 из 5 фрагментов = `_requiredCount=3`
- **Если `< 1`:** автоматически считается как 1

#### `_consumeOnUse : bool`
- **Тип:** bool, дефолт `false`
- **MVP статус:** поле есть, **логика НЕ реализована** (TODO Phase 10+)
- **Будущее поведение:** если `true` — после успешного `CanPlayerUse` предметы **забираются** из инвентаря
- **Пока:** значение `true` ни на что не влияет (warning в OnValidate не выводится — by design)

### 1.2 Header "UI / display"

#### `_interactableDisplayName : string`
- **Тип:** string, дефолт `"Object"`
- **Что:** человекочитаемое имя для toast'а ("Нужен ключ для: **Корабль Light**")
- **Fallback:** если пустое или `null` — используется `gameObject.name`
- **Пример:** для корабля = `"Корабль Light"`, для блока = `"Синий Сундук"`

#### `_customFailureMessage : string`
- **Тип:** string, опционально, дефолт пусто
- **Что:** кастомное сообщение для toast'а при отказе
- **Если пусто:** генерируется автоматически:
  - 1 недостающий: `"Нужен предмет: <itemName>"`
  - Несколько: `"Не хватает: <itemName1>, <itemName2>, ..."`
- **Если задано:** используется как есть (без добавления displayName)
- **Пример:** для квеста = `"Соберите все 3 амулета"`

### 1.3 OnValidate (Editor-only)

При изменении полей в инспекторе:
- **Warning** если `_requiredItems[]` пуст: `"Объект будет всегда доступен (или всегда заблокирован, в зависимости от _logic)"`
- **Warning** если есть дубликаты ItemData в `_requiredItems[]`
- **НЕ валидирует:** ссылку на `NetworkObject` (предполагается что дизайнер сам добавит)

### 1.4 Public read-only API (для скриптов)

| Свойство | Тип | Описание |
|---|---|---|
| `InteractableDisplayName` | `string` | резолв `_interactableDisplayName` или `gameObject.name` |
| `RequiredItems` | `ItemData[]` | копия `_requiredItems` (read-only) |
| `Logic` | `RequirementLogic` | текущая логика |
| `RequiredCount` | `int` | `_requiredCount` |
| `ConsumeOnUse` | `bool` | `_consumeOnUse` (для будущей v2) |
| `CustomFailureMessage` | `string` | `_customFailureMessage` |
| `ServerItemIds` | `int[]` | **server-only**, resolved через `InventoryWorld.GetOrRegisterItemId`, lazy resolve |

### 1.5 Server-side methods

| Метод | Описание |
|---|---|
| `bool CanPlayerUse(ulong clientId, out string reason)` | server-only, авторитетная проверка. `reason` — human-readable для toast'а |
| `ProgressInfo GetPlayerProgress(ulong clientId)` | server-only, для UI tooltip'а ("Прогресс: 2/5") |
| `void OnNetworkSpawn()` | server-only, вызывает `MetaRequirementRegistry.RegisterRequirement` |
| `void OnNetworkDespawn()` | server-only, вызывает `MetaRequirementRegistry.UnregisterRequirement` |

---

## 2. `MetaRequirementRegistry` (NetworkBehaviour)

Серверный hub. Один экземпляр на сервере. Вешается на `[MetaRequirementRegistry]` GameObject
в `BootstrapScene` (рядом с `[InventoryServer]`, `[ContractServer]`).

### 2.1 Inspector

У `MetaRequirementRegistry` **НЕТ** полей в Inspector — все настройки runtime-only.
Компонент только:
- `NetworkObject` (обязательно)
- `MetaRequirementRegistry` (наш компонент)

### 2.2 Public API (server-only)

| Член | Тип | Описание |
|---|---|---|
| `static Instance` | `MetaRequirementRegistry` | singleton |
| `GetRequirement(ulong netId)` | `MetaRequirement` | получить requirement по `NetworkObjectId` |
| `CanPlayerUse(ulong clientId, ulong netId)` | `bool` | wrapper для defense-in-depth на сервере |
| `RegisterRequirement(ulong netId, MetaRequirement)` | `void` | вызывается из `MetaRequirement.OnNetworkSpawn` |
| `UnregisterRequirement(ulong netId)` | `void` | вызывается из `MetaRequirement.OnNetworkDespawn` |

### 2.3 RPC (auto-generated)

| RPC | Direction | Кто вызывает | Описание |
|---|---|---|---|
| `RequestCanUseRpc(ulong netId)` | Client → Server | `MetaRequirementClientState.RequestCanUse` | Клиент хочет использовать interactable. Сервер проверяет `CanPlayerUse` и отвечает через `NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc` |

### 2.4 Lifecycle

| Hook | Что делает |
|---|---|
| `OnNetworkSpawn()` | устанавливает `Instance`; подписывается на `OnClientConnectedCallback` |
| `HandleClientConnected(ulong)` | вызывает `PushRequirementsToClient` через 0.5 сек (задержка чтобы `MetaRequirement.OnNetworkSpawn` успел отработать для всех interactable'ов) |
| `PushRequirementsToClient` | bulk-push реестра через `NetworkPlayer.ReceiveMetaRequirementBindingsTargetRpc` (netIds, displayNames, itemIdsArr, logics, counts, consumes) |
| `OnNetworkDespawn` | сбрасывает `Instance`, очищает `_requirements` |

### 2.5 Auto-spawn

`MetaRequirementRegistry` НЕ auto-spawn'ится (как, например, `MetaRequirementClientState`).
**Он scene-placed в `BootstrapScene`**, спавнится через `ScenePlacedObjectSpawner`. Это
гарантирует стабильность: сервер всегда знает, где hub, и scene transition (стриминг
24 сцен) не ломает регистрацию.

---

## 3. `MetaRequirementClientState` (MonoBehaviour singleton)

Клиентская проекция. **Auto-spawn root GameObject** в `NetworkManagerController.Awake`
(по аналогии с `InventoryClientState`).

### 3.1 Inspector

У `MetaRequirementClientState` **НЕТ** пользовательских полей в Inspector, **КРОМЕ**:
- `dontDestroyOnLoad : bool` (дефолт `true`) — переживает загрузку сцен

### 3.2 Public API

| Член | Тип | Описание |
|---|---|---|
| `static Instance` | `MetaRequirementClientState` | singleton |
| `HasRequirement(ulong netId)` | `bool` | есть ли requirement в client projection |
| `GetDisplayName(ulong netId)` | `string` | displayName из DTO, или `"#{netId}"` |
| `GetItemIds(ulong netId)` | `int[]` | itemIds из DTO, или `null` |
| `GetDto(ulong netId)` | `MetaRequirementDto?` | полный DTO |
| `RequestCanUse(ulong netId)` | `void` | отправить RPC на сервер; если Registry не готов — emit deny event с reason "Сервер требований недоступен" |

### 3.3 Events (UI подписывается)

| Event | Сигнатура | Когда дёргается |
|---|---|---|
| `OnRequirementsUpdated` | `Action` | при Push от сервера (на `OnClientConnected` + 0.5s) |
| `OnAccessDenied` | `Action<ulong, string>` | `(netId, reason)` — сервер отказал в доступе |
| `OnAccessAllowed` | `Action<ulong>` | `(netId)` — сервер разрешил доступ |

### 3.4 Auto-spawn

`NetworkManagerController.CreateMetaRequirementClientState()` — создаёт root GameObject
`[MetaRequirementClientState]`. В Awake — `DontDestroyOnLoad` (если root, иначе warning).
Паттерн идентичен `MarketClientState` / `ContractClientState` / `InventoryClientState`.

---

## 4. `MetaRequirementToast` (MonoBehaviour + UIDocument)

UI-компонент для отображения сообщений об отказе.

### 4.1 Inspector — Settings

| Поле | Дефолт | Описание |
|---|---|---|
| `_duration : float` | `2.5` | секунд показа |
| `_cooldown : float` | `0.4` | защита от двойного deny (если < cooldown — игнорируем) |

### 4.2 Inspector — Внешний вид

| Поле | Дефолт | Описание |
|---|---|---|
| `_textColor : Color` | `(1.0, 0.85, 0.3, 1.0)` (ярко-золотой) | цвет текста Label |
| `_backgroundColor : Color` | `(0, 0, 0, 0.7)` (полупрозрачный чёрный) | фон контейнера |
| `_fontSize : int` | `20` | размер шрифта |

### 4.3 Inspector — UIDocument

| Поле | Описание |
|---|---|
| `PanelSettings` | должен быть `MetaRequirementPanelSettings.asset` (копия `ShipKeyPanelSettings`) |
| `Source Asset` | не используется (мы создаём UI программно в `TryBuild()`) |

### 4.4 Public API

| Метод | Описание |
|---|---|
| `ShowToastExternal(string message)` | показать toast с заданным сообщением (для тестов) |

### 4.5 Подписка

`OnEnable` → `TrySubscribe` (lazy в `Update`): подписывается на
`MetaRequirementClientState.OnAccessDenied`. Сигнатура: `Action<ulong, string>` —
получает `(netId, reason)`, но `netId` для тоста не используется (просто message).

---

## 5. `LockBox` (MonoBehaviour — тестовая анимация)

**ВНИМАНИЕ:** `LockBox` находится в `ProjectC.MetaRequirement.Test` namespace — это
**тестовая** анимация, не production. Для своего визуала создавайте свой компонент.

### 5.1 Inspector — Display

| Поле | Описание |
|---|---|
| `_baseColor : Color` | базовый цвет блока (используется для анимации + gizmos) |
| `_baseEmission : Color` | базовый emissive (для URP/Lit) |

### 5.2 Inspector — Анимация

| Поле | Дефолт | Описание |
|---|---|---|
| `_animDuration : float` | `0.6` | секунд полной анимации (фаза 1 + фаза 2) |
| `_animScaleMultiplier : float` | `1.2` | scale в пике (1.2 = +20%) |
| `_animEmissionMultiplier : float` | `3.0` | emissive в пике (3.0 = тройная яркость) |

### 5.3 Inspector — Частота

| Поле | Дефолт | Описание |
|---|---|---|
| `_reopenCooldown : float` | `0.5` | минимальный интервал между повторными анимациями |

### 5.4 Анимация

**Phase 1 (50% длительности):** ramp-up
- `localScale: base → base * _animScaleMultiplier`
- `emission: base → base * _animEmissionMultiplier`

**Phase 2 (50% длительности):** ramp-down
- `localScale: target → base`
- `emission: target → base * 1.5` (промежуточная яркость)

**Final:** exactly base (scale + baseEmission)

### 5.5 Подписка

`OnEnable` + lazy `Update` — подписывается на `MetaRequirementClientState.OnAccessAllowed`.
Фильтрует по `NetworkObjectId == netId` (чтобы не реагировать на чужие блоки).

### 5.6 Editor

- `OnDrawGizmosSelected`: рисует wire cube размера блока
- `OnValidate` (Editor only): обновляет базовый цвет в редакторе (визуальный feedback)

---

## 6. `NetworkPlayer` — MetaRequirement-related поля

| Поле | Тип | Описание |
|---|---|---|
| `_lastCanUseRequestTime : float` | private | `Time.unscaledTime` последнего `RequestCanUse` |
| `_pendingCanUseInteractableId : ulong` | private | `NetworkObjectId` последнего запроса |
| `CAN_USE_REQUEST_TIMEOUT` | const `1.5f` | секунд до сброса race-protection |

### 6.1 Public API (internal)

| Метод | Описание |
|---|---|
| `TryInteractNearestMetaRequirement()` | private, вызывается из E-key блока. Ищет ближайший `MetaRequirement` (без `ShipController`) в радиусе `max(pickupRange, boardDistance)`, отправляет `RequestCanUse` |

### 6.2 Target RPC (auto-generated)

| RPC | Direction | Кто вызывает | Описание |
|---|---|---|---|
| `ReceiveMetaRequirementResponseTargetRpc(ulong netId, bool allowed, string reason)` | Server → Client Owner | `MetaRequirementRegistry` | Ответ на `RequestCanUse`. Сбрасывает race-protection, делегирует в `MetaRequirementClientState.OnCanUseResponse` |
| `ReceiveMetaRequirementBindingsTargetRpc(ulong[] netIds, FixedString64[] names, int[][] itemIdsArr, byte[] logics, int[] counts, bool[] consumes)` | Server → Client Owner | `MetaRequirementRegistry` | Bulk-push реестра. Делегирует в `MetaRequirementClientState.OnRequirementsPushed` |

---

## 7. Где какой компонент должен лежать (cheat-sheet)

| Сцена | GameObject | Компоненты |
|---|---|---|
| Сундук с замком (видимый) | `[LockBox_X]` | `NetworkObject`, `BoxCollider` (solid), `SphereCollider` (trigger, radius 2.5), `MeshRenderer` (visual), `MetaRequirement`, ваш-визуал-скрипт |
| Зона (невидимая) | `[Zone_X_Requirement]` | `NetworkObject`, `BoxCollider` (trigger), `MetaRequirement` |
| NPC (видимый) | `[NPC_X]` | как у сундука, плюс `NetworkObject` если нужен server-side state |
| Корабль (legacy) | `[Ship_X]` | существующий `ShipController` + `ShipKeyBinding` (алиас) — **не мигрируем** в Этапе 1 |
| Дропнутый предмет (server-spawn) | динамический | `NetworkObject`, `SphereCollider` (trigger), `PickupItem` (НЕ `MetaRequirement` — дроп не требует ключ) |
| Toast UI | `[MetaRequirementToast]` в `BootstrapScene` | `UIDocument` (panelSettings=`MetaRequirementPanelSettings`), `MetaRequirementToast` |
| Registry server-side | `[MetaRequirementRegistry]` в `BootstrapScene` | `NetworkObject`, `MetaRequirementRegistry` |
