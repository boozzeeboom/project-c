# Key Subsystem — Обзор

**Подсистема:** Корабли — физический ключ для запуска
**Тег:** `ship-key`, `key_rod`, `key-binding`, `server-authoritative`
**Статус:** MVP (блокировка F-посадки). Расширение — TODO.
**Дата:** 2026-06-06

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

## 2. Архитектура

### 2.1 Серверная часть (источник истины)

- `ShipKeyBinding` (server-only singleton, POCO) — реестр связей `shipId ↔ keyItemId`.
  - Заполняется **на сервере** при `StartHost()` из списка зарегистрированных кораблей (`ShipController` в загруженных сценах).
  - Каждый корабль, у которого в инспекторе задан `keyItemId` (либо `0` для авто-генерации), получает запись.
  - На сервере — `Dictionary<ulong /*networkObjectId*/, int /*keyItemId*/>`.
- `ShipKeyServer` (NetworkBehaviour) — RPC-хэб:
  - `RequestCanBoardRpc(ulong clientId, ulong shipNetworkObjectId)` → `CanBoardResponse { bool allowed, string reason }`.
  - Содержит server-side логику: проверить, есть ли в инвентаре игрока предмет с `itemId == keyItemId`.
- `InventoryWorld.HasItem(ulong clientId, int itemId)` — **новый** helper, дополняющий v2-инвентарь (сейчас есть только `GetOrCreate/Has/AddItemDirect/...`).

### 2.2 Клиентская часть (UI/feedback)

- `ShipKeyClientState` (singleton) — клиентская проекция для UI:
  - хранит `Dictionary<ulong /*shipId*/, int /*keyItemId*/>` (получено от сервера при входе на хост);
  - событие `OnBindingsUpdated` для UI.
- `ShipKeyToast` (MonoBehaviour, `UIDocument` scene-placed) — простая UI-подсказка в нижней части экрана:
  - показывает `string message` в течение `duration` секунд;
  - использует UI Toolkit (`<ui:Label>` поверх остального UI);
  - **не блокирует** другие UI (picking-mode=Ignore).
- Логика блокировки F:
  - Клиент (owner) при нажатии F → шлёт `RequestCanBoardRpc` серверу.
  - Сервер → проверяет инвентарь → отвечает `CanBoardResponse`.
  - Если `allowed == false` → клиент показывает toast; **НЕ** отправляет `SubmitSwitchModeRpc` (предотвращаем двойной-RPC).
  - Если `allowed == true` → клиент отправляет `SubmitSwitchModeRpc` (нормальный путь).

### 2.3 Данные (ScriptableObject)

`ItemData` (`Assets/_Project/Scripts/Core/ItemType.cs`) уже имеет нужные поля:
- `itemName` (string) — `"Ключ-стержень: Light"` и т.п.;
- `itemType` (ItemType) — переиспользуем `Equipment` (новый тип не нужен в MVP);
- `description` (string) — пояснение, что ключ подходит к конкретному кораблю;
- `maxStack` (int) = `1` (ключ не стакается);
- `weightKg` (float) — минимальный (0.05).

**Без нового ItemType** — `Equipment` достаточно семантически (ключ = экипировка/инструмент). Если потребуется фильтрация по типу «Key» в UI, добавим `Key` в `ItemType` (см. TODO).

**Без отдельного SO для ключа** — корабли, не ключи, хранят свою привязку. `ShipKeyBinding` маппит `shipId → keyItemId`, а ключ — это просто `ItemData` с уникальным именем/иконкой (например, ярко-золотой цвет).

**⚠️ КРИТИЧНО:** `ItemData` для ключей должны лежать **в корне `Assets/_Project/Resources/Items/`,** а НЕ в подпапках. `Resources.LoadAll<ItemData>("Items")` в `InventoryWorld.RegisterAllItems()` **не рекурсивен** — подпапки игнорируются. См. `KNOWN_ISSUES.md` §"Баг: ключи в подпапке" для подробностей.

---

## 3. Wire-протокол (RPC)

### 3.1 Server → Client (один раз на респаун клиента)

`PushBindingsRpc(ulong[] shipIds, int[] keyItemIds)`:
- Отправляется **target** клиенту через `NetworkPlayer.ReceiveShipKeyBindingsTargetRpc`.
- Содержит весь текущий реестр (в будущем — диффы, но MVP = bulk).
- Идемпотентно: можно вызывать при переподключении, scene-load.

### 3.2 Client → Server

`RequestCanBoardRpc(ulong shipNetworkObjectId, ServerRpcParams)`:
- Клиент (owner NetworkPlayer) вызывает при F рядом с кораблём **до** отправки `SubmitSwitchModeRpc`.
- Серверная лямбда:
  1. Проверить: `ShipKeyServer.TryGetKeyFor(shipId, out int keyItemId)`.
  2. Проверить: `InventoryWorld.Has(clientId, keyItemId)`.
  3. Ответить `CanBoardResponseRpc { bool allowed, string reason }`.

### 3.3 Server → Client (ответ)

`CanBoardResponseRpc(ulong shipNetworkObjectId, bool allowed, string reason, RpcParams)`:
- Target — отправитель (client owner).
- Если `allowed == false` → клиент показывает toast и НЕ шлёт `SubmitSwitchModeRpc`.
- Если `allowed == true` → клиент шлёт `SubmitSwitchModeRpc` штатно.

### 3.4 DTO

```csharp
public struct ShipKeyBindingDto : INetworkSerializable
{
    public ulong shipNetworkObjectId;
    public int   keyItemId;     // 0 = "нет ключа, доступ свободный" (TODO?)
    public FixedString64Bytes shipDisplayName;
}
```

> **Примечание:** `FixedString64Bytes` используем, чтобы не аллоцировать на каждом RPC. Можно заменить на `string`, если профилирование покажет отсутствие проблем.

---

## 4. Идентификация кораблей

### 4.1 `shipId`

В Project C `NetworkObject.NetworkObjectId` — стабильный ulong, уникальный в пределах серверного рантайма. Это и есть `shipId`.

**Альтернатива** — кастомное поле на `ShipController` (например, `public string ShipInstanceId`). Минусы: редактор должен следить за уникальностью, плюсы: стабильно между сессиями. **Решение MVP:** используем `NetworkObjectId`. Документируем как TODO для именованных/сохранённых кораблей.

### 4.2 `keyItemId`

Стандартный `InventoryWorld.GetOrRegisterItemId(itemData)`. На сервере при `StartHost()` каждый корабль имеет `ShipKeyBinding` с:
- `[SerializeField] ItemData _keyItemData` — какой ключ к нему подходит (заполняется в инспекторе сцены);
- `int KeyItemId` — резолвится сервером из `_keyItemData` при `StartHost` через `InventoryWorld.GetOrRegisterItemId`.

**Гарантия 1:1:** один `keyItemData` — один `keyItemId`. Если два корабля случайно получат один `keyItemData`, проверка наличия ключа сработает на обоих — это баг дизайнера. В коде — `Debug.Assert(_keyItemData != null)` + `OnValidate` в `ShipKeyBinding` для детекта дублей в сцене.

---

## 5. Точки вставки в существующий код

| Файл | Что меняем | Зачем |
|---|---|---|
| `Assets/_Project/Items/Core/InventoryWorld.cs` | Добавляем `bool HasItem(ulong clientId, int itemId)` | Серверная проверка наличия ключа |
| `Assets/_Project/Items/Network/InventoryServer.cs` | (опц.) Прокидываем `HasItem` через RPC | Нужен только если проверка идёт с клиента — у нас server-side |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | В `Update` блок F → добавляем pre-check `RequestCanBoardRpc` перед `SubmitSwitchModeRpc` | Предотвращаем отправку boarding RPC без ключа |
| **Новый** `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs` | NetworkBehaviour-singleton, реестр | Серверный источник истины |
| **Новый** `Assets/_Project/Scripts/Ship/Key/ShipKeyClientState.cs` | MonoBehaviour-singleton, проекция на клиента | UI + cache |
| **Новый** `Assets/_Project/Scripts/UI/ShipKeyToast.cs` | Простой UIDocument-компонент | Тост «нет ключа» |
| **Новые** `Assets/_Project/Resources/Items/Item_Key_*.asset` | 3 SO ключа | Предметы-ключи для теста |
| **Новые** GameObject'ы в `WorldScene_0_0.unity` | 3 ключа-PickupItem + 1 контейнер-декор | Визуальные пикапы |
| `WorldScene_0_0.unity` | Прописываем `keyItemData` в каждый `ShipController` (через ShipKeyBinding) | Связываем корабли с ключами |
| `BootstrapScene.unity` | Добавляем `[ShipKeyServer]` GameObject с компонентом `ShipKeyBinding` | NetworkBehaviour-hub |
| `Assets/_Project/UI/...` | UXML/USS для тоста | Минимальная UI-структура |

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

**Статус:** 📋 Планируется (см. подробный план в `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md`)

**Суть:** текущая подсистема (1 корабль ↔ 1 ключ) — вырожденный случай более общей системы `MetaRequirement` (любой `Interactable` ↔ N требуемых предметов с AND/OR/AtLeastN логикой).

**Когда начинать:** в любой момент (Этап 1 оценён в 3-4 часа, с backward-compat алиасами).

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
