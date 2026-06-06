# Ship Key → MetaRequirement — Migration Guide

**Документ:** пошаговая миграция с `ShipKeyBinding` (MVP) на `MetaRequirement` (Этап 1 обобщения)
**Дата:** 2026-06-06
**Связанные документы:**
- `docs/Ships/Key-subsystem/00_OVERVIEW.md` (старая подсистема)
- `docs/MetaRequirement/00_OVERVIEW.md` (новая подсистема)
- `docs/MetaRequirement/RECIPES.md` (примеры конфигураций)

---

## Что мигрируем

| Ship Key (старое) | MetaRequirement (новое) |
|---|---|
| `ShipKeyBinding` (MonoBehaviour, 1 item) | `MetaRequirement` (MonoBehaviour, N items) |
| `ShipKeyServer` (NetworkBehaviour hub) | `MetaRequirementRegistry` (NetworkBehaviour hub) |
| `ShipKeyClientState` (singleton) | `MetaRequirementClientState` (singleton) |
| `ShipKeyToast` (UIDocument) | `MetaRequirementToast` (UIDocument) |
| `ShipKeyBinding._keyItemData` (1 поле) | `MetaRequirement._requiredItems[]` (массив) |
| `ShipKeyBinding._shipDisplayName` | `MetaRequirement._interactableDisplayName` |

---

## Что **не** мигрируем (остаётся без изменений)

| Файл | Статус |
|---|---|
| `InventoryWorld.cs` | **Не трогаем core.** Только добавляются extension-методы (`HasAllItems` и т.д.) — backward compatible. |
| `PickupItem.cs` | Не меняется. Generic pipeline. |
| `ItemData.cs` (SO) | Не меняется. |
| `InventoryClientState.cs` | Не меняется. |
| `InventoryServer.cs` | Не меняется. |
| `NetworkPlayer.F-key` | Минимальные изменения (5-10 строк) — общий entry point, делегирует в `MetaRequirementClientState`. |
| `InteractableManager.cs` | Минимальное дополнение (`FindNearestInteractable`). |
| `NetworkManagerController.cs` | Дополнение (`CreateMetaRequirementClientState`). |
| `WorldScene_0_0.unity` | Только замена компонента `ShipKeyBinding` → `MetaRequirement` на 3 кораблях. |
| `BootstrapScene.unity` | Не меняется (алиасы совместимы). |

---

## Backward compatibility: алиасы

Чтобы **не сломать** существующие сцены и префабы, в **Этапе 1** создаём алиасы:

### Файл: `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs`

```csharp
[System.Obsolete("Use MetaRequirement. Kept as alias for backward compat with existing scenes.")]
public class ShipKeyBinding : ProjectC.MetaRequirement.MetaRequirement { }
```

**Что происходит:** `ShipKeyBinding` — пустой subclass `MetaRequirement`. Старые ссылки через `.meta`-GUID продолжают работать, потому что class-наследник — это `is-a` родитель.

**⚠️ ВАЖНО:** `.meta`-файлы `ShipKeyBinding.cs.meta`, `ShipKeyServer.cs.meta` и т.д. — **не переименовываем**. GUID компонентов в сценах ссылается на `.meta`-GUID, не на class name. Если переименовать файл, GUID сохранится, но если **удалить** старый файл — scene-prefab references сломаются (NullRef).

### Файлы с алиасами

Все 4 старых файла получают `[Obsolete]` subclass вместо полной реализации:

| Старый файл | Что в нём после миграции |
|---|---|
| `Ship/Key/ShipKeyBinding.cs` | `[Obsolete] public class ShipKeyBinding : MetaRequirement {}` (пустой) |
| `Ship/Key/ShipKeyServer.cs` | `[Obsolete] public class ShipKeyServer : MetaRequirementRegistry {}` (пустой) |
| `Ship/Key/ShipKeyClientState.cs` | `[Obsolete] public class ShipKeyClientState : MetaRequirementClientState {}` (пустой) |
| `Ship/Key/ShipKeyToast.cs` | `[Obsolete] public class ShipKeyToast : MetaRequirementToast {}` (пустой) |

**Через 1-2 релиз-цикла** (когда все сцены мигрированы): удаляем алиасы.

---

## Шаг 1: Подготовка (в день миграции)

**1.1 Проверить текущее состояние:**
```bash
# Должны быть эти 4 файла:
ls Assets/_Project/Scripts/Ship/Key/
# ShipKeyBinding.cs, ShipKeyBinding.cs.meta
# ShipKeyServer.cs, ShipKeyServer.cs.meta
# ShipKeyClientState.cs, ShipKeyClientState.cs.meta
# ShipKeyToast.cs, ShipKeyToast.cs.meta

# Должен быть 1 PanelSettings:
ls Assets/_Project/UI/Resources/UI/ShipKeyPanelSettings.asset

# Должны быть 3 SO ключа в Resources/Items/:
ls Assets/_Project/Resources/Items/Item_Key_*.asset

# В WorldScene_0_0 должны быть:
# - [Ship_Key_Container] с 3 [KeyRod_*] PickupItem
# - 3 ShipController с [ShipKeyBinding] компонентом

# В BootstrapScene должны быть:
# - [ShipKeyServer] (NetworkObject)
# - [ShipKeyToast] (UIDocument)
```

**1.2 Создать бэкап-ветку (по AGENTS.md, юзер делает сам, не мы):**
```bash
git checkout -b refactor/ship-key-to-meta-requirement
git add -A
git commit -m "WIP: refactor ShipKey → MetaRequirement"
```

---

## Шаг 2: Extension-методы в `InventoryWorld`

**Файл:** `Assets/_Project/Items/Core/InventoryWorld.cs`

**Добавить** в секцию `Public API — per-player inventory` (рядом с `HasItem`):

```csharp
// === MetaRequirement extensions (2026-06-06) ===

/// <summary>True если у игрока ЕСТЬ ВСЕ itemId из списка. Дубликаты игнорируются.</summary>
public bool HasAllItems(ulong clientId, int[] itemIds) { ... }

/// <summary>True если у игрока ЕСТЬ ХОТЯ БЫ ОДИН itemId из списка.</summary>
public bool HasAnyItem(ulong clientId, int[] itemIds) { ... }

/// <summary>Сколько штук указанного itemId есть у игрока (по List<int>.Count).</summary>
public int CountOf(ulong clientId, int itemId) { ... }

/// <summary>Массив itemId, которых НЕТ у игрока. Используется для генерации reason в toast'е.</summary>
public int[] GetMissingItems(ulong clientId, int[] itemIds) { ... }
```

**Что НЕ меняется:** `HasItem`, `Has`, `GetOrCreate`, `TryPickup`, `TryDrop`, `BuildSnapshot` — **всё остаётся**. Backward compatible.

**Тест:** в Play mode `StartHost` → через execute_code проверить:
```csharp
inventoryWorld.AddItemDirect(0, 31, ItemType.Equipment);
inventoryWorld.HasAllItems(0, new[]{31, 32}) == false  // нет 32
inventoryWorld.HasAllItems(0, new[]{31}) == true
inventoryWorld.HasAnyItem(0, new[]{32, 33}) == false  // нет ни одного
inventoryWorld.CountOf(0, 31) == 1
```

---

## Шаг 3: Создать `MetaRequirement` MonoBehaviour

**Новый файл:** `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs`

**Поля в Inspector:**
```csharp
[Header("Требования (предметы)")]
[SerializeField] private ItemData[] _requiredItems = new ItemData[0];
[SerializeField] private RequirementLogic _logic = RequirementLogic.All;
[SerializeField, Min(1)] private int _requiredCount = 1;  // для AtLeastN
[SerializeField] private bool _consumeOnUse = false;

[Header("UI")]
[SerializeField] private string _interactableDisplayName = "Object";
[SerializeField] private string _customFailureMessage = "";
```

**Lifecycle:**
- `OnNetworkSpawn` (server-only) → `MetaRequirementRegistry.RegisterRequirement(NetworkObjectId, this)`
- `OnNetworkDespawn` → `UnregisterRequirement`
- `OnValidate` (Editor) → warning для пустого `_requiredItems` / дубликатов

**Public API:**
- `bool CanPlayerUse(ulong clientId, out string reason)` — server-side
- `int[] ServerItemIds` — lazy resolve через `InventoryWorld.GetOrRegisterItemId`
- `ProgressInfo GetPlayerProgress(ulong clientId)` — для UI tooltip'а

**Подробности:** `docs/MetaRequirement/00_OVERVIEW.md` §2.3.

---

## Шаг 4: Создать `MetaRequirementRegistry`

**Новый файл:** `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs`

**Паттерн:** один-в-один `ShipKeyServer`, но generic:
- `Dictionary<ulong, MetaRequirement> _requirements` (вместо `_bindings`)
- `RegisterRequirement(netId, MetaRequirement)` (вместо `RegisterBinding`)
- `CanPlayerUse(clientId, netId)` (вместо `CanPlayerBoard`)
- `[Rpc(SendTo.Server)] RequestCanUseRpc(netId)` (вместо `RequestCanBoardRpc`)
- Push к клиенту через `OnClientConnectedCallback` (тот же 0.5s delay)

**Алиас в `ShipKeyServer.cs`:** `[Obsolete] public class ShipKeyServer : MetaRequirementRegistry {}` (пустой subclass).

---

## Шаг 5: Создать `MetaRequirementClientState`

**Новый файл:** `Assets/_Project/Scripts/MetaRequirement/MetaRequirementClientState.cs`

**Паттерн:** `ShipKeyClientState` + дополнительное событие `OnAccessDenied(reason)`.

**В `NetworkManagerController.cs`:** добавить `CreateMetaRequirementClientState()` (по аналогии с `CreateInventoryClientState`).

**Алиас:** `[Obsolete] public class ShipKeyClientState : MetaRequirementClientState {}` в `ShipKeyClientState.cs`.

---

## Шаг 6: Создать `MetaRequirementToast`

**Новый файл:** `Assets/_Project/Scripts/MetaRequirement/MetaRequirementToast.cs`

**Паттерн:** `ShipKeyToast` + расширенный reason (список недостающих).

**Новый asset:** `Assets/_Project/UI/Resources/UI/MetaRequirementPanelSettings.asset` (dedicated).

**Алиас:** `[Obsolete] public class ShipKeyToast : MetaRequirementToast {}` в `ShipKeyToast.cs`.

---

## Шаг 7: Wire `NetworkPlayer.F-key`

**Файл:** `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

**Что меняется** (5-10 строк в F-key блоке):
- Вместо `FindNearestShip()` → `FindNearestInteractable()` (новый метод в `InteractableManager`)
- Вместо `ShipKeyClientState.Instance.RequestCanBoard(shipId)` → `MetaRequirementClientState.Instance.RequestCanUse(interactableId)`
- `_lastCanBoardRequestTime` / `_pendingCanBoardShipId` → переименовать в `_lastCanUseRequestTime` / `_pendingCanUseInteractableId` (для ясности)

**Файл:** `Assets/_Project/Scripts/Core/InteractableManager.cs`

**Добавить:**
```csharp
public static GameObject FindNearestInteractable(Vector3 position, float range)
{
    // Находит ближайший GameObject с IInteractable + Collider
    // (generic версия FindNearestShip)
}
```

---

## Шаг 8: Миграция сцены `WorldScene_0_0`

**Вручную** через Unity Editor (нельзя автоматизировать — `SerializedObject` field paths зависят от class structure):

1. Открыть `WorldScene_0_0.unity`
2. Выбрать `Ship_Light` в Hierarchy
3. **Remove Component** → `Ship Key Binding`
4. **Add Component** → `Meta Requirement`
5. **В новом компоненте**:
   - `_requiredItems` → Size: 1 → Element 0: drag `Item_Key_ShipLight.asset`
   - `_logic`: `All`
   - `_interactableDisplayName`: `Корабль Light`
6. **Повторить** для `Ship_Medium`, `Ship_Heavy`

**Альтернатива** (через MCP execute_code): создать скрипт, который:
- Находит все `ShipKeyBinding` на кораблях
- Создаёт `MetaRequirement` с теми же полями
- Удаляет `ShipKeyBinding`
- SaveScene

**Рекомендация:** сделать **вручную** через Editor — проще отследить ошибки, не нужен сложный скрипт.

---

## Шаг 9: Verify — `WorldScene_0_0` после миграции

**Тест (в Play mode):**
1. StartHost
2. Подобрать `[KeyRod_ShipLight]`
3. F на `Ship_Light` → **должен** сесть
4. В Console:
   - `[MetaRequirementRegistry] Registered requirement: netId=N, displayName='Корабль Light', keyItemIds=[31]`
   - `[MetaRequirementRegistry] CanPlayerUse: client=0, obj=N, allowed=True`
5. F → выйти. F без ключа → toast "Нет ключа корабля (Корабль Light)"
6. Повторить для `Ship_Medium` и `Ship_Heavy`

**Если что-то не работает:**
- `MetaRequirement.OnNetworkSpawn` не вызвался → проверить что `NetworkObject` есть на GameObject
- `MetaRequirementRegistry.Instance == null` → не спавнится scene-placed NetworkObject (см. pitfall #31 в `unity-mcp-orchestrator`)
- Toast не появляется → проверить подписку `MetaRequirementToast` в Play mode через execute_code (см. KNOWN_ISSUES в прошлой сессии)
- `allowed=True` без ключа → проверить что `_requiredItems[]` не пустой (если пустой — `OnValidate` warning)

---

## Шаг 10: Документация (финализация)

**Файлы для финального коммита:**
- ✅ `docs/MetaRequirement/00_OVERVIEW.md` (уже есть)
- ✅ `docs/MetaRequirement/RECIPES.md` (уже есть)
- ✅ `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` (этот файл)
- 🆕 `docs/MetaRequirement/KNOWN_ISSUES.md` (создать)
- 🆕 `docs/Ships/Key-subsystem/00_OVERVIEW.md` — обновить: добавить секцию "## Migration to MetaRequirement" со ссылкой на `docs/MetaRequirement/`
- 🆕 `docs/Ships/Key-subsystem/KNOWN_ISSUES.md` — закрыть R2-SHIP-KEY-001, добавить "R2-SHIP-KEY-002: мигрировано на MetaRequirement"

---

## Шпаргалка: что проверять после миграции

| Тест | Ожидаемое поведение |
|---|---|
| Открыть `WorldScene_0_0.unity` | Нет ошибок в Console при загрузке |
| Открыть `BootstrapScene.unity` | Нет ошибок |
| Play → StartHost | Network поднимается, нет NRE |
| Подобрать `KeyRod_ShipLight` → F на `Ship_Light` | Садится |
| F на `Ship_Light` без ключа | Toast "Нет ключа корабля (Корабль Light)" |
| F на `Ship_Heavy` без ключа | Toast "Нет ключа корабля (Корабль Heavy)" |
| F на `Chest_Main` (без MetaRequirement) | Обычное открытие сундука (без toast) |
| F на `Pickup_Res_1` (без MetaRequirement) | Обычный pickup (без toast) |
| Проверить `WorldScene_0_0.unity` на наличие `ShipKeyBinding` | Не должно быть (заменены на `MetaRequirement`) |
| Проверить `BootstrapScene.unity` на наличие `[ShipKeyServer]` | Может быть (алиас работает) или уже заменён на `[MetaRequirementRegistry]` |
| `InventoryClientState.OnSnapshotUpdated` при подборе | Срабатывает (как раньше) |
| `MetaRequirementClientState.OnAccessDenied` при deny | Срабатывает (новое событие) |
| `MetaRequirementToast` показывает toast | Да, с правильным displayName |

---

## Удаление алиасов (через 1-2 релиз-цикла)

Когда **все** сцены и префабы в проекте мигрированы на `MetaRequirement` (нет ни одного `ShipKeyBinding`/`ShipKeyServer`/etc. в `.unity`/`.prefab` файлах):

1. Удаляем 4 старых файла:
   - `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs`
   - `Assets/_Project/Scripts/Ship/Key/ShipKeyServer.cs`
   - `Assets/_Project/Scripts/Ship/Key/ShipKeyClientState.cs`
   - `Assets/_Project/Scripts/Ship/Key/ShipKeyToast.cs`
2. Удаляем `.meta` файлы
3. Удаляем алиасы из `00_OVERVIEW.md`
4. `git commit -m "chore(ship): remove ShipKey aliases after MetaRequirement migration complete"`

**Проверка перед удалением:**
```bash
# Должно вернуть 0 результатов:
grep -r "ShipKeyBinding\|ShipKeyServer\|ShipKeyClientState\|ShipKeyToast" Assets/ --include="*.cs" --include="*.unity" --include="*.prefab"
```

---

## Связанные документы

- `docs/MetaRequirement/00_OVERVIEW.md` — полный дизайн новой подсистемы
- `docs/MetaRequirement/RECIPES.md` — 10 примеров конфигураций
- `docs/Ships/Key-subsystem/00_OVERVIEW.md` — старая подсистема (предшественник)
- `docs/Ships/Key-subsystem/KNOWN_ISSUES.md` — баг-репорт R2-SHIP-KEY-001
- `unity-mcp-orchestrator` skill — pitfalls #22, #29, #31, #37 (особенно #31 — scene-spawn footgun)
