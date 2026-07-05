# Key Subsystem — Обзор

**Подсистема:** Корабли — физический ключ для запуска
**Тег:** `ship-key`, `key_rod`, `key-binding`, `server-authoritative`
| **Статус:** ✅ MVP. ✅ MetaRequirement. ✅ Unique Key Instance. ✅ P1 Refactor (2026-07-21). |
| **Дата:** 2026-06-06 (MVP) → 2026-07-21 (P1 complete) |

---

## 1. Концепция

В Project C корабль — это **физический объект с физическим ключом**. Связь «корабль ↔ ключ» устанавливается при создании корабля и больше не меняется. Ключ — обычный предмет в инвентаре игрока. Управление кораблём разрешено **только при наличии ключа** в инвентаре пилота.

### 1.1 Свойства ключа

- Ключ — это предмет (`ItemData` + `ItemType`, например `Equipment` или новый `Key`).
- Ключ лежит в общем инвентаре игрока (как и любой ресурс).
- Ключ можно:
  - **выбросить** (drop) — и подобрать заново;
  - **передать** (через контейнер, трейд, прямую передачу — TODO);
  - **потерять** при смерти (TODO: persistence policy);
  - **украсть** — TODO: безопасность против нерегулярных путей.
- Ключ **НЕ привязан** к игроку — он привязан к **кораблю**. Один и тот же ключ нельзя использовать на разных кораблях.

### 1.1a Рецепт: добавить новый корабль + ключ

**Цель:** создать `Ship_Fast` с уникальным ключом, чтобы игрок мог им управлять.

1. **Создать ItemData ключа** (через Project window → Create → Project C → Item Data):
   - `m_Name`: `Item_Key_ShipFast`
   - `itemName`: `ShipFast`
   - `itemType`: `Equipment` (или новый `Key` если создал отдельный enum)
   - `description`: `Ключ-стержень для запуска корабля Fast.`
   - `maxStack`: 1
   - `weightKg`: 0.05
   - **Сохранить в `Assets/_Project/Resources/Items/`** (не в подпапках!)

2. **Создать PickupItem** (GameObject с сферой в сцене):
   - 3D Object → Sphere → назови `[KeyRod_ShipFast]`
   - Add Component → `Pickup Item`
   - `Item Data` → drag `Item_Key_ShipFast.asset`
   - `Interaction Radius`: 2
   - Покрась (URP/Lit material) + emission

3. **Привязать к кораблю** (на `Ship_Fast` GameObject):
   - Add Component → `Ship Key Binding`
   - `Key Item Data` → drag `Item_Key_ShipFast.asset` (**тот же SO что и в Pickup**)
   - `Ship Display Name`: `Корабль Fast`

4. **Save сцену**. На `StartHost`:
   - `ShipKeyBinding.OnNetworkSpawn` → `ShipKeyServer.RegisterBinding`
   - `ShipKeyServer` пушит binding клиенту через `OnClientConnectedCallback` (через 0.5 сек)
   - В Console появится: `[ShipKeyServer] Registered binding: shipNetId=N, displayName='Корабль Fast', keyItemId=31, ...`

**Никаких ручных ID** — `itemId` вычисляется автоматически через `GetOrRegisterItemId` (см. §4 "Идентификация кораблей").

### 1.1b Troubleshooting

| Симптом | Причина | Фикс |
|---|---|---|
| Ключ подобран, в инвентаре не появился | `ItemData` в подпапке `Resources/Items/*` → `Resources.LoadAll` не подхватил | Перемести SO в **корень** `Resources/Items/` |
| `[ShipKeyServer] CanBoard: allowed=True` без ключа | На `ShipController` нет `ShipKeyBinding` или `_keyItemData==null` | Добавь binding с itemData |
| Toast не виден | `ShipKeyClientState.Instance==null` в Play | Проверь что `NetworkManagerController.Awake` отработал (см. `CreateShipKeyClientState`) |
| `ShipKeyServer.Instance==null` в Play | Network не поднят или `ScenePlacedObjectSpawner` не спавнил scene-placed `NetworkObject` | Запусти StartHost; проверь Console на `[ScenePlacedObjectSpawner] Scene BootstrapScene: spawned=...` |
| Два корабля подходят под один ключ | Оба ссылаются на один `itemData` | У каждого корабля свой уникальный SO |

### 1.2 Свойства связи

- Один корабль ↔ ровно один ключ.
- Один ключ ↔ ровно один корабль (1:1).
- Идентификатор связи: пара `(shipId, keyItemId)`, где:
  - `shipId` — уникальный стабильный ID корабля (server-defined, хранится в `NetworkObjectId` или в отдельном поле `ShipKeyBinding._shipId`).
  - `keyItemId` — уникальный ID предмета-ключа в `InventoryWorld._itemDatabase` (server-defined, авто-регистрируется через `GetOrRegisterItemId`).

### 1.3 Правила доступа

| Действие | Без ключа | С ключом |
|----------|-----------|----------|
| Подойти к кораблю | ✓ | ✓ |
| Зайти внутрь (открытая дверь) | ✓ | ✓ |
| Взаимодействовать с не-`KeyRequired` объектами внутри | ✓ | ✓ |
| **F — сесть за штурвал (`SubmitSwitchModeRpc`, board)** | ✗ BLOCKED + toast | ✓ |
| Управление (W/S/A/D/Q/E/Shift) | ✗ (нет пилота) | ✓ |
| Загрузить товары с рынка на корабль (в зоне) | ✓ (Cargo-операции не требуют ключа) | ✓ |
| F — выйти из корабля | n/a (не сел) | ✓ |
| Угон без ключа (hot-wire) | ✗ — фьючер-фича, не реализована | n/a |

**MVP граница:** блокируем только **board** (F → сесть). Hot-wire / угон = `TODO: far future`.

---

## 2. Архитектура (P1 Refactor, 2026-07-21)

### 2.1 Серверная часть — Single Source of Truth

**KeyRodInstanceWorld** (`static class`, server-only) — единственный источник правды:
- `Dictionary<int, KeyRodInstance> _instancesById`
- `Dictionary<ulong, int> _primaryInstanceByShipId` — shipNetId → instanceId (1:1)
- `Dictionary<ulong, List<int>> _instancesByPlayer` — clientId → instanceIds
- Event `OnOwnershipChanged` (для внутренней подписки)
- Persistence через `IKeyRodInstanceRepository` (`KeyRodInstances.json`)

**ShipController.OnNetworkSpawn** (сервер):
- Читает `_keyItemData` (ItemData, inspector field)
- Корутина `CreateKeyInstanceWhenReady()` ждёт инициализации `KeyRodInstanceWorld`
- Создаёт `KeyRodInstance` → `CreateInstance(itemId, shipNetId, OWNER_NONE)`

**ShipOwnershipRequirement** (NetworkBehaviour на каждом ShipController):
- Server-only проверка: `KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetId)`
- Регистрируется в `MetaRequirementRegistry` как приоритетный handler

**MetaRequirementRegistry** (NetworkBehaviour, BootstrapScene):
- Универсальный хаб проверок: ShipOwnershipRequirement → MetaRequirement → default allow
- RPC: `RequestCanUseRpc` → `NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc`

### 2.2 Клиентская часть

**ShipTelemetryClientState** (MonoBehaviour singleton):
- Агрегирует `ShipTelemetryState` со всех кораблей через `NetworkVariable`
- Ownership: читает `ownerClientId` из `ShipTelemetryState` напрямую
- `MyShips` / `IsMyShip` — фильтрация по `ownerClientId == LocalClientId`

**MetaRequirementClientState** (MonoBehaviour singleton):
- Клиентская проекция требований для ЛЮБЫХ interactable'ов
- F-key: `RequestCanUse` → `MetaRequirementRegistry.RequestCanUseRpc`

**MetaRequirementToast** (UIDocument):
- Показывает toast при отказе доступа

### 2.3 Файлы (актуальный состав)

| Файл | Назначение |
|------|-----------|
| `KeyRodInstance.cs` | POCO: itemId, instanceId, registeredShipId, ownerPlayerId, state |
| `KeyRodInstanceWorld.cs` | Static facade, server-only, 3 индекса + persistence |
| `KeyRodInstanceRepository.cs` | JSON-персистентность |
| `ShipOwnershipRequirement.cs` | NetworkBehaviour, проверка владения |
| `ShipTelemetryState.cs` | INetworkSerializable struct (14 полей + ownerClientId) |
| `ShipTelemetryClientState.cs` | Клиентский агрегатор telemetry |

### 2.4 Удалённые файлы (P1)

| Файл | Причина удаления |
|------|-----------------|
| ❌ `ShipKeyBinding.cs` | Obsolete alias → MetaRequirement |
| ❌ `ShipKeyServer.cs` | Заменён на MetaRequirementRegistry |
| ❌ `ShipKeyClientState.cs` | Заменён на MetaRequirementClientState |
| ❌ `ShipKeyToast.cs` | Заменён на MetaRequirementToast |
| ❌ `ShipOwnershipRegistry.cs` | Дублировал KeyRodInstanceWorld |
| ❌ `KeyRodInstanceBinding.cs` | Scene-placed binding с retry-loop |

---

## 3. Wire-протокол (актуальный)

### 3.1 Client → Server (F-key)

`MetaRequirementClientState.RequestCanUse(shipNetId)` → `MetaRequirementRegistry.RequestCanUseRpc`:
1. Проверка ShipOwnershipRequirement → `KeyRodInstanceWorld.IsOwnerOfShip`
2. Проверка MetaRequirement (для блоков/дверей)
3. Default allow

### 3.2 Server → Client (ответ)

`NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc`:
- allowed → `SubmitSwitchModeRpc`
- denied → `MetaRequirementToast`

### 3.3 Telemetry (Server → Client)

`ShipController._telemetryState` (NetworkVariable, 5Hz throttle):
- `ShipTelemetryClientState.SubscribeToShip()` читает deltas
- `ownerClientId` встроен прямо в telemetry

---

## 4. Идентификация

- `shipId` = `NetworkObject.NetworkObjectId` (ulong, стабилен в сессии)
- `keyItemId` = `InventoryWorld.GetOrRegisterItemId(itemData)` (int)
- `instanceId` = монотонный счётчик `KeyRodInstanceWorld._nextInstanceId` (int, эфемерный)
- Связь: `ShipController._keyItemData` → резолвится в itemId → `CreateInstance(itemId, shipNetId, OWNER_NONE)`

**Что НЕ меняем** (по AGENTS.md «Don't touch»):
- `docs/gdd/`, `docs/WORLD_LORE_BOOK.md` — дизайн-документ держим в `docs/Ships/Key-subsystem/`.
- `Library/`, `Temp/`, `Builds/`, `ProjectSettings/`, `Packages/manifest.json` — без надобности.
- `.meta` и `.asmdef` — НЕ создаём новые (см. AGENTS.md HARD RULES).

---

## 6. Состояние и edge-cases

### 6.1 Гонка `RequestCanBoardRpc` ↔ `SubmitSwitchModeRpc`

Сценарий: игрок быстро жмёт F дважды. Без защиты — два `RequestCanBoardRpc` и сразу `SubmitSwitchModeRpc` могут уйти до ответа сервера.

**Защита (в `NetworkPlayer.Update`):**
```csharp
private float _lastCanBoardRequestTime = -10f;
private const float CAN_BOARD_REQUEST_TIMEOUT = 1.5f; // ожидание ответа
private ulong _pendingCanBoardShipId = ulong.MaxValue;

if (Keyboard.current.fKey.wasPressedThisFrame) {
    if (_inShip) { /* выход — без проверки */ }
    else {
        if (Time.time - _lastCanBoardRequestTime < CAN_BOARD_REQUEST_TIMEOUT &&
            _pendingCanBoardShipId == nearestShip.NetworkObjectId) {
            return; // ждём ответа
        }
        _lastCanBoardRequestTime = Time.time;
        _pendingCanBoardShipId = nearestShip.NetworkObjectId;
        ShipKeyClientState.Instance.RequestCanBoard(nearestShip.NetworkObjectId);
    }
}
```
Ответ RPC (allowed) → сбрасывает `_pendingCanBoardShipId` + шлёт `SubmitSwitchModeRpc` если allowed.

### 6.2 Scene transition (стриминг 24 сцен)

`ShipKeyServer` собирает биндинги на `OnNetworkSpawn` (вызывается на сервере при StartHost), плюс подписывается на `SceneManager.sceneLoaded` → пополняет реестр при загрузке новых стриминговых сцен. Идемпотентно: повторный `Register(shipId, keyItemId)` с тем же значением — no-op, с другим — warning в editor.

### 6.3 Хот-сварка scene-placed NetworkObject (известный footgun)

В `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md` задокументировано: scene-placed `NetworkObject` в scene НЕ спавнится NGO, его спавнит `ScenePlacedObjectSpawner` в BootstrapScene. **Это значит:** `NetworkObject.NetworkObjectId` у кораблей стабилен после `ScenePlacedObjectSpawner` отработал — но **между запусками Editor / билдами** может отличаться. Это нормально для MVP (ID сохраняется в текущей сессии). TODO для persistence — отдельный тикет.

### 6.4 Disconnect / Reconnect

`PushBindingsRpc` отправляется на `OnClientConnected`. Клиент очищает `_pendingCanBoardShipId` и показывает дисконнект-сообщение (если `InventoryClientState` уже отслеживает).

### 6.5 Клиент-чит: подделка `RequestCanBoardRpc`

Сервер **всегда** валидирует: проверяет, что у клиента **реально** есть `keyItemId` в инвентаре на сервере. Даже если клиент шлёт `SubmitSwitchModeRpc` напрямую в обход (нельзя через F, но гипотетически через cheat) — есть второй серверный guard в `AddPilot` / `SubmitSwitchModeRpc` (см. §7).

---

## 7. Двойной guard (defense in depth)

**F-блок на клиенте** — UX-фича (тост «нет ключа»). Можно обойти через прямой RPC.

**Серверный guard в `SubmitSwitchModeRpc` (после `EnterShip` ветки):**
```csharp
// В NetworkPlayer.SubmitSwitchModeRpc (server side)
if (!_inShip) {  // посадка
    var ship = FindNearestShip();
    if (ship == null) return;
    if (!ProjectC.Ship.Key.ShipKeyServer.Instance.CanPlayerBoard(OwnerClientId, ship.NetworkObjectId)) {
        // Молча отказываем (без RPC) — клиент уже должен был проверить.
        // НИЧЕГО НЕ ШЛЁМ (визуально ничего не происходит).
        return;
    }
    // ... остальная логика посадки ...
}
```

Это второй уровень защиты. Если клиент пропустил F-проверку (баг, мод, чит) — сервер всё равно не пустит.

---

## 8. Тестовая расстановка в `WorldScene_0_0`

| Объект | Где | Зачем |
|---|---|---|
| `[Ship_Key_Container]` | Центр (0, 0, 0) — куб с подписью «Ключи кораблей» | Декор + ориентир для игрока |
| `[KeyRod_ShipLight]` | Дочерний контейнера, ярко-жёлтый | Тест подбора и посадки на Light |
| `[KeyRod_ShipMedium]` | Дочерний, ярко-зелёный | Тест Medium |
| `[KeyRod_ShipHeavy]` | Дочерний, ярко-красный | Тест Heavy |
| `[Ship_KeyServer]` | В `BootstrapScene` (вместе с `[InventoryServer]`, `[ContractServer]`) | NetworkBehaviour-хэб |

Каждый `[KeyRod_*]` — это `GameObject` с:
- `PickupItem` (MonoBehaviour) с `itemData` → соответствующий SO из `Resources/Items/Item_Key_*.asset`;
- яркий `Material` (создаём через MCP `manage_components` с `MeshRenderer.material.color`);
- `SphereCollider` (isTrigger=true) для подбора.

Корабли в `ships/`:
- Добавляем `ShipKeyBinding` компонент (наш новый) с:
  - `keyItemData` → `Item_Key_ShipLight.asset` (для Ship_Light) и т.д.;
  - `displayName` → `"Корабль Light"` / `"Medium"` / `"Heavy"`.

---

## 9. Manual test чек-лист (для пользователя)

1. **Запустить Editor → BootstrapScene → Play → StartHost.**
2. **Подойти к `[KeyRod_ShipLight]` → нажать E:**
   - В Console: `[InventoryServer] Dropped PickupItem at ... id=N`.
   - HUD: `Подобран предмет` (локализация OK).
3. **Открыть TAB-колесо (Tab):**
   - В секторе Equipment (itemType=1) появилась запись «Ключ-стержень: Light».
4. **Подойти к `Ship_Light` → нажать F:**
   - **Должен** сесть за штурвал, F-выход работает.
5. **Выбросить ключ (Drop, будущий UI):** в MVP — перезапустить Editor без ключа. Подойти к `Ship_Light` → нажать F:
   - **Должен** появиться toast внизу экрана: «Нет ключа корабля (Корабль Light)».
   - В Console: `[ShipKeyServer] CanBoard denied: clientId=0 ship=... reason=missing_key_item_id=N`.
   - **Не должен** сесть.
6. **Подойти к `Ship_Heavy` (нет ключа) → F:**
   - Тот же отказ, тот же тост.
7. **Зайти внутрь корабля (двери):** в MVP — отсутствует как фича. Если есть `TriggerEnter/Exit` для входа (сейчас нет), `SubmitSwitchModeRpc` не сработает без ключа → вход свободен, управление заблокировано. Документируем как будущее расширение.

---

## 10. Что НЕ входит в MVP

- Hot-wire / угон без ключа.
- Передача ключа другому игроку через прямой drop (нужен trade/transfer RPC).
- Persistence ключа между сессиями (сейчас — серверный singleton, при рестарте теряется).
- Уникальный `ShipInstanceId` через `string` (сейчас — `NetworkObjectId`, меняется между запусками Editor).
- Отдельный `ItemType.Key` (семантика ограничиваемся `Equipment` + явный `ItemData` с пометкой в description).
- UI-таб «ключи» в CharacterWindow (сейчас ключ показывается в общем секторе Equipment TAB-колеса).
- Взлом замка / изготовление дубликата ключа.

---

## 11. Ссылки

- Дизайн-контекст: `docs/context/ship.md` (текущая архитектура кораблей).
- Инвентарь v2: `docs/dev/INVENTORY_V2_REFACTOR.md` (как устроен `InventoryWorld`).
- NRE / scene-spawn: `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md` (фут-ган `InScenePlacedSourceGlobalObjectIdHash==0`).
- UI-паттерн (singleton + projection): `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` (как устроен `ContractClientState` / `MarketClientState`).
- **KNOWN_ISSUES.md** — баг-репорт "ключи в подпапке Resources/Items" (R2-SHIP-KEY-001).
- **SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md** — план миграции на обобщённую подсистему `MetaRequirement` (Этап 1, ~3-4 часа).
- **📋 docs/MetaRequirement/00_OVERVIEW.md** — дизайн новой подсистемы (planned).
- **📋 docs/MetaRequirement/RECIPES.md** — 10 примеров конфигураций (planned).

**Обновлено:** 2026-06-06 — первичный MVP-дизайн.
**Обновлено:** 2026-06-06 — добавлен рецепт нового корабля+ключа, troubleshooting, CRITICAL warning про `Resources.LoadAll`.
**Обновлено:** 2026-06-06 — запланирована миграция на `MetaRequirement` (Этап 1).

---

## 12. Migration to MetaRequirement (Этап 1)

**Статус:** ✅ ЗАВЕРШЕНА (P1, 2026-07-21). См. `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md` P1.

**Суть:** текущая подсистема (1 корабль ↔ 1 ключ) — вырожденный случай более общей системы `MetaRequirement` (любой `Interactable` ↔ N требуемых предметов с AND/OR/AtLeastN логикой). Миграция выполнена: `ShipKeyBinding/Server/ClientState/Toast` удалены, заменены на `MetaRequirement*`, `ShipOwnershipRequirement` + `KeyRodInstanceWorld`.

**Результат:** 1 источник правды (KeyRodInstanceWorld), 0 reflection, ~5 активных файлов.

**Что останется** (не трогаем):
- `InventoryWorld` core (только extension-методы)
- `PickupItem`, `ItemData`, `InventoryClientState`, `InventoryServer`
- `NetworkPlayer.F-key` (5-10 строк изменений, единый entry point)

**Что переименовываем** (с алиасами):
- `ShipKeyBinding` → `MetaRequirement`
- `ShipKeyServer` → `MetaRequirementRegistry`
- `ShipKeyClientState` → `MetaRequirementClientState`
- `ShipKeyToast` → `MetaRequirementToast`

**Что добавляем**:
- `InventoryWorld.HasAllItems / HasAnyItem / CountOf / GetMissingItems`
- `RequirementLogic` enum (All / Any / AtLeastN)
- `ProgressInfo` struct для UI tooltip'а
- `InteractableManager.FindNearestInteractable` (generic версия `FindNearestShip`)

**Крафт (Этап 2) — НЕ делаем сейчас.** Это требует транзакций, дерева зависимостей, рецептов как SO — отдельная подсистема.

**Подробный план:** `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` (10 шагов с тестами).
**Дизайн новой подсистемы:** `docs/MetaRequirement/00_OVERVIEW.md`.
**Примеры конфигураций:** `docs/MetaRequirement/RECIPES.md` (10 рецептов: ключ, дверь, босс, прогресс-квест, жертва и т.д.).
