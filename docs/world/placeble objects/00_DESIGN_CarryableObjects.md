# Переносимые физические объекты (Carryable Objects) — ресерч и пошаговый план

> **Дата:** 2026-08-10
> **Статус:** дизайн по результатам ресерча кодовой базы
> **Запрос:** бочки/коробки в мире с физикой (толкают друг друга), которые игрок перетаскивает: подошёл → зажал F ~2с → объект подсветился outline (как цели NPC Q/E) → игрок его несёт → повторное F → объект падает с физикой. Вес предмета ограничивает, как высоко/далеко его можно нести. У персонажа — настраиваемый лимит веса + STR-множитель. Позиции принимает сервер, сохраняются между сессиями, без дубликатов.
> **Читать вместе с:** `docs/world/buildings/00_BUILD_SYSTEM_DEEP_ANALYSIS.md` (там placeable-мебель как L7 — это ДРУГАЯ система, не путать), `docs/Character/input-system/ITERATIONS.md` (F-приоритеты).

---

## 0. TL;DR

Вся нужная инфраструктура в проекте **уже есть** — система собирается из 6 существующих паттернов + ~7 новых файлов:

```
CarryableObject (NetworkBehaviour + Rigidbody + IInteractable)   ← вешается на бочку/коробку
        ▲ trigger-child регистрация (паттерн PickupItem/ResourceNode)
PlayerCarryController (на префабе игрока, рядом с NetworkPlayer) ← hold-F state machine + лимиты веса
        ▲ читает F через NetworkPlayer.IsActionHeld (нужен public accessor)
        ▲ STR через StatsClientState (клиент) / StatsServer (сервер)
CarryableOutline (per-object, M_TargetOutline)                   ← НЕ через TargetHighlightService! (см. §6.2)
CarryableObjectServer + JsonCarryableObjectRepository            ← копия ShipPositionServer-паттерна
```

**Ключевые решения (детали внутри):**

| # | Вопрос | Решение |
|---|--------|---------|
| 1 | Кто симулирует физику | **Сервер** (server-authoritative), NetworkTransform(ServerAuthority) — как корабли/NPC |
| 2 | Как несут объект | Клиент шлёт carry-точку 10–15 Гц → сервер двигает объект `MovePosition` (kinematic на время carry) |
| 3 | Outline | Отдельный per-object компонент с тем же `M_TargetOutline.mat` — **НЕ** TargetHighlightService (он single-target и занят Q/E локом) |
| 4 | Новый бинд | **Не нужен** — переиспользуем F (GameAction.PickupItem), hold-трекинг поверх существующей цепочки приоритетов |
| 5 | Persistence | PersistentId = `sceneName/objectName` (как ShipPersistentId), save каждые 5с + на drop, restore матчингом — без респавна = без дубликатов |
| 6 | STR → грузоподъёмность | `capacity = baseCarryKg + StatsToFlat(strTier) * strToCarryKg`, сервер валидирует через `StatsServer.GetPlayerStats` |

---

## 1. Ресерч: что уже есть в проекте

### 1.1 Инпут (F) — кастомная система биндов

- **`Assets/_Project/Scripts/Input/InputBindingsConfig.cs`** — SO с enum `GameAction`. F занят дважды:
  - `GameAction.PickupItem → Key.F` — «высший приоритет на F» (строка 125, 192);
  - `GameAction.ModeSwitch → Key.F` — посадка/сбор/крафт/двери (строка 165).
  - ⚠️ **Комментарий в коде (строка 121): новые значения enum добавлять ТОЛЬКО в конец — иначе сломается сериализация asset'а `Resources/InputBindingsConfig.asset`.**
- **`Assets/_Project/Scripts/Input/InputBindingsRuntime.cs`** — runtime singleton, rebind + PlayerPrefs.
- **`Assets/_Project/Scripts/Player/NetworkPlayer.cs`** — читает ввод напрямую через `Keyboard.current` + хелперы:
  - `IsActionJustPressed(GameAction)` (строка 2236), `IsActionHeld(GameAction)` (строка 2215) — **оба `private`** → для CarryController нужен public-accessor (§7.3).
  - Цепочка F в `Update()` (строки 622–701): сначала `PickupItem` блок (`FindNearestInteractable()` + `TryPickup()`), затем `ModeSwitch` блок (gather → crafting → cargo console → door → ship board). **Hold-F carryable встраивается в эту же цепочку** (§6.1).
- Смерть блокирует весь ввод: `_inputEnabled=false` (`SetInputEnabled`, строка 220) — carry должен принудительно сбрасываться (§8).

### 1.2 Обнаружение интерактивных объектов

- **`Assets/_Project/Scripts/Core/InteractableManager.cs`** — статический реестр. Паттерн: объект имеет **trigger-коллайдер**, в `OnTriggerEnter/Exit` (проверка `CompareTag("Player") || GetComponent<CharacterController>()`) вызывает `Register/Unregister`, поиск — `FindNearestX(position, range)` (zero-alloc, pre-allocated lists).
- **`Assets/_Project/Scripts/Core/IInteractable.cs`** — интерфейс: `InstanceId`, `DisplayName`, `InteractionRadius`, `Position`.
- `NetworkPlayer.pickupRange = 3f` (строка 81).
- Референс «мировой объект с состоянием»: **`ResourceNode.cs`** — NetworkBehaviour + replicated state + trigger-регистрация (строки 220–230) + **lock одним игроком** (`_currentGathererClientId`, `TryStartGather`, строки 266–279) — прямой прецедент для `carriedByClientId`-лока.

⚠️ **Нюанс:** у PickupItem/ResourceNode коллайдер — trigger. У физической коробки основной коллайдер обязан быть **solid** (иначе нет физики). Решение: **дочерний GO `InteractionTrigger`** с trigger-SphereCollider и крошечным форвардером событий в родителя (§3.1).

### 1.3 Outline-подсветка (M_TargetOutline)

- **`Assets/_Project/Scripts/Combat/Client/TargetHighlightService.cs`** — client singleton (создаётся в `NetworkManagerController.CreateTargetHighlightService`, строка 554). Материал грузится из `Resources/Materials/M_TargetOutline` (строка 45). Техника: **append материала в `renderer.sharedMaterials`** всех SkinnedMeshRenderer + MeshRenderer в детях (строки 113–150), удаление — фильтрацией массива (строки 152–190).
- Используется: `TargetLockService` (Q/E лок цели, `Highlight(target, 0f)` = бесконечная, строка 226) и `SkillInputService` (подсветка найденной цели, 1.5с, строка 417).
- ⚠️ **КОНФЛИКТ (не учтён в запросе):** сервис **single-target** — поле `_currentTarget` одно. Если carryable позовёт `Highlight()`, он **снимет подсветку с боевой цели** игрока (и наоборот). Поэтому: свой лёгкий `CarryableOutline` компонент на объекте, копирующий технику append/remove того же материала (§3.3). TargetHighlightService не трогаем.

### 1.4 Характеристики (STR)

- **`Assets/_Project/Scripts/Stats/PlayerStats.cs`**: `StatType.Strength`; `PlayerStats.StatsToFlat(tier) = tier*5+10` (строка 130) — каноническая формула «плоского» STR.
- **Клиент:** `StatsClientState.Instance.CurrentStats` → `StatsSnapshotDto` — поля `strengthTier`, а также `effectiveStrength` = `(StatsToFlat(tier) + equipBonus) * (1 + equipMult)` (`StatsServer.cs` строка 525). Для грузоподъёмности логично брать **effectiveStrength** (экипировка «пояс силы» реально помогает нести).
- **Сервер (авторитетная валидация):** `StatsServer.Instance.GetPlayerStats(clientId)` (строка 667) → tier → `StatsToFlat` (+ equip-пересчёт в `RecomputeAndSendSnapshot`, строки 454–527 — вынести в публичный геттер effective STR, см. шаг 4.3).
- Прецедент «STR влияет на физику мира»: `HealthConfig.ComputeMaxHp(strFlatValue)` (строка 35) — та же схема.

### 1.5 Сеть и спавн мировых объектов

- NGO, атрибуты `[Rpc(SendTo.X)]`. Игроки: NetworkTransform **Owner-authority** (`NetworkPlayer.cs` строки 259–265). Корабли/NPC: **Server-authority** + Rigidbody (ShipController, строка 33) — **прецедент для carryable** (пользователь прямо требует «позиции принимает сервер»).
- `NetworkRigidbody` в проекте **не используется** (поиск пустой) — не вводим, хватит NetworkTransform + ручной kinematic-контроль.
- Паттерн серверного спавна: `InventoryServer.RequestDropRpc` (строки 138–183): валидация → `Instantiate(prefab, worldPos)` → заполнить данные → `netObj.Spawn(destroyWithScene: true)`. Rate-limit — `CheckRateLimit(clientId)` (прецедент антиспама для grab/drop RPC).
- Сцены: **`SceneBoundNetworkObject`** биндит объект к `SceneID.FromWorldPosition(pos)` и фильтрует visibility per-client (`ShouldClientSeeObject`). При переносе объекта через границу сцены (80 000 юнитов, сетка 6×4) нужен **re-bind** на drop (§8.4).

### 1.6 Persistence между сессиями (главный паттерн)

- **`Assets/_Project/Scripts/Core/ShipPosition/ShipPositionServer.cs`** — server-only singleton, DontDestroyOnLoad:
  - save: каждые 5с собирает позиции всех `ShipController` → JSON (`IShipPositionRepository`, tmp+Move atomic write);
  - restore: через 3.5с после `OnServerStarted`, матчинг по **`ShipPersistentId` = `sceneName/gameObject.name`** → teleport, **НЕ respawn** → дубликатов нет;
  - кэш списка объектов (`GetCachedShips`, TTL 300 кадров) вместо `FindObjectsByType` каждый тик.
- **`PlayerPositionServer`** — то же для игроков, единый write через ShipPositionServer.
- `JsonCharacterDataRepository` — per-client JSON в `Application.persistentDataPath`, atomic tmp+Move.

→ Для carryable копируем этот паттерн 1:1: `CarryableObjectServer` + `JsonCarryableObjectRepository` (§5).

### 1.7 Слои

Существующие: `Default, TransparentFX, Ignore Raycast, Water, UI, ShipDeck(6), ZoneCollider(31)`. → Нужен новый слой **`Carryable`** (например, индекс 7): чтобы (а) исключать коллизию несомого объекта с его носителем, (б) точечно настраивать collision matrix (§8.2).

### 1.8 Смежные системы (не путать)

- `PickupItem` — предметы инвентаря (подобрал → исчез в инвентарь). Наша система **не** кладёт объект в инвентарь — он остаётся в мире.
- `PickupDeckRide` — carry-формула (Δпозиции платформы) для pickup на движущейся палубе. Для физических коробок на палубе работает серверная физика (палуба и коробка оба на сервере) — отдельный компонент не нужен, но сценарий в тест-плане (§9).
- `docs/world/buildings/00_BUILD_SYSTEM_DEEP_ANALYSIS.md` — placeable-мебель (L7), отдельная будущая система; наша механика переноса потом переиспользуется и там.

---

## 2. Целевой UX-flow

```
Идle                  Подошёл к коробке (≤3м)         Зажал F
  │  ───────────────────────────────────────────────►  HOLD (0→2с)
  │     подсказка "Удерживайте F"                        outline появляется сразу,
  │     (если тяжёлая: "Слишком тяжёлая (45/30 кг)")     прогресс-кольцо на HUD
  │                                                        │ отпустил раньше → cancel, outline off
  │                                                        ▼ 2с + сервер подтвердил
  │                                                     CARRYING
  │                                          объект следует за carry-точкой
  │                                     (дистанция/высота зависят от веса/STR)
  │                                          скорость ходьбы снижена, бег off
  │                                                        │ нажал F ещё раз
  │                                                        ▼
  │                                                     DROP → kinematic off,
  │                                                     гравитация, физика, save
  └──────────────────────────────────────────────────────┘
```

---

## 3. Компонентный дизайн

### 3.1 `CarryableObject` — вешается на бочку/коробку

`Assets/_Project/Scripts/World/Carryable/CarryableObject.cs`

```csharp
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class CarryableObject : NetworkBehaviour, Core.IInteractable
{
    [Header("Идентификация (persistence)")]
    [SerializeField] private string _persistentIdOverride;   // пусто → sceneName/objectName
    [Header("Физика")]
    [SerializeField] private float _weightKg = 25f;          // ВЕС ПРЕДМЕТА (задаёт дизайнер)
    [SerializeField] private bool  _freezeRotationWhenCarried = true;
    [Header("Взаимодействие")]
    [SerializeField] private float _interactionRadius = 3f;
    [SerializeField] private string _displayName = "Ящик";

    // NetworkVariable<ulong> CarriedByClientId (0 = никто) — lock + реплика состояния
    // NetworkVariable<bool>  IsKinematicState — чтобы клиенты зеркалили rb.isKinematic
}
```

Состав префаба (обязательный):

```
Crate_01 (CarryableObject, NetworkObject, NetworkTransform[ServerAuthority, Interpolate=true],
          Rigidbody[mass = weightKg], BoxCollider[solid], SceneBoundNetworkObject, CarryableOutline)
 └── InteractionTrigger (SphereCollider[isTrigger, r≈interactionRadius], CarryableInteractionTrigger)
```

- `CarryableInteractionTrigger` (child): `OnTriggerEnter/Exit` (tag `Player`) → `InteractableManager.RegisterCarryable/UnregisterCarryable(parent)` — паттерн `ResourceNode.cs:220-230`.
- `Rigidbody.mass = _weightKg` в `Awake` (сервер); dynamic по умолчанию → **коробки толкают коробки** штатной физикой.
- Во время carry (сервер): `rb.isKinematic = true`, движение через `rb.MovePosition/MoveRotation` в `FixedUpdate` — kinematic-тело **вытесняет** dynamic-тела (несомой коробкой можно двигать другие коробки), при этом сама не падает.
- NetworkTransform: default `AuthorityMode=Server`, `Interpolate=true` (плавность у наблюдателей; у носителя лёгкий input-lag — приемлемо для MVP, prediction — фаза polish).
- Регистрация в `DefaultNetworkPrefabs.asset` (для server-spawned вариантов) — scene-placed экземпляры спавнит `ScenePlacedObjectSpawner`.

### 3.2 `PlayerCarryController` — на префабе игрока

`Assets/_Project/Scripts/Player/PlayerCarryController.cs` (рядом с NetworkPlayer, owner-only логика)

```csharp
public class PlayerCarryController : MonoBehaviour
{
    [Header("Грузоподъёмность (требование ТЗ)")]
    [SerializeField] private float _baseCarryWeightKg = 20f;   // базовый лимит без STR
    [SerializeField] private float _strToCarryKg = 2f;         // +кг за 1 плоский STR (множитель)
    [Header("Удержание F")]
    [SerializeField] private float _holdSeconds = 2f;          // требование: 2с
    [SerializeField] private bool  _holdScaledByWeight = true; // тяжёлые дольше "заводятся"
    [Header("Перенос: дальность/высота зависят от отношения вес/лимит")]
    [SerializeField] private float _maxCarryDistance = 2.5f;   // дальность точки при ratio→0
    [SerializeField] private float _minCarryDistance = 1.2f;   // при ratio→1 (тяжёлый — ближе)
    [SerializeField] private float _maxCarryHeight = 1.6f;     // высота точки при ratio→0
    [SerializeField] private float _minCarryHeight = 0.4f;     // при ratio→1 (тяжёлый — ниже)
    [SerializeField] private float _carryFollowSpeed = 12f;    // серверный lerp к точке
    [Header("Штрафы при переносе")]
    [SerializeField] private AnimationCurve _moveSpeedByWeightRatio; // 1.0 при лёгком → ~0.5 при пределе
    [SerializeField] private float _runDisableRatio = 0.6f;    // ratio выше — бег запрещён
    [SerializeField] private float _breakawayDistance = 4f;    // объект ушёл дальше → авто-drop
}
```

**Формулы** (централизовано, используются и клиентом (UI/предикт) и сервером (валидация)):

```
strFlat        = StatsToFlat(strTier)                       // tier0→10, tier1→15, ...
                 [опц.] или effectiveStrength из snapshot   // с бонусами экипировки
capacityKg     = _baseCarryWeightKg + strFlat * _strToCarryKg
canCarry(obj)  = obj.WeightKg <= capacityKg
ratio          = obj.WeightKg / capacityKg                  // 0..1 (больше 1 — не поднять)
holdTime       = _holdSeconds * (1 + 0.5f*ratio)            // если _holdScaledByWeight
carryDistance  = Lerp(_maxCarryDistance, _minCarryDistance, ratio)   // "как далеко"
carryHeight    = Lerp(_maxCarryHeight, _minCarryHeight, ratio)       // "как высоко"
speedMult      = _moveSpeedByWeightRatio.Evaluate(ratio)             // штраф к ходьбе
```

Пример (дефолт): STR tier0 → strFlat=10 → capacity = 20 + 10×2 = **40 кг**. Коробка 25 кг: ratio=0.63 → distance ≈1.7м, height ≈0.85м, ходьба ×~0.7. Бочка 60 кг — не поднять, пока STR не вырастет.

### 3.3 `CarryableOutline` — подсветка без конфликтов

`Assets/_Project/Scripts/World/Carryable/CarryableOutline.cs`

- Копия техники `TargetHighlightService.AddOutlineToRenderer/RemoveOutlineFromRenderer` (append/remove `M_TargetOutline` в `sharedMaterials`, instancing чтобы не портить shared asset), но **per-object, локальный**:
  - `SetHighlighted(bool)` — вызывается только на клиенте носителя (grab-hold start → on; cancel/drop → off).
  - Материал: `Resources.Load<Material>("Materials/M_TargetOutline")` с кэшем в static.
- **Почему не TargetHighlightService:** он single-target (`_currentTarget`) и обслуживает Q/E боевой лок — взаимные вызовы `Clear()` сносили бы чужую подсветку. (Если позже захотим единый сервис — рефакторить его в multi-target словарь; не в этой итерации.)

### 3.4 UI

- Подсказка у объекта: расширить `NetworkPlayer.HasNearbyInteractable()/GetNearbyInteractableName()` — добавить ветку carryable («Ящик — удерживайте F» / «Слишком тяжёлый: 60/40 кг»). Их уже читает HUD.
- Прогресс hold-F: существующий toast/HUD-паттерн (QuestToast) или кольцо у центра экрана — отдельная маленькая задача UI-фазы; MVP — outline появляется сразу при зажатии + звук/текст при старте.

---

## 4. Сетевой дизайн (server-authoritative)

### 4.1 State machine и RPC

```
CLIENT (owner player)                                   SERVER
─────────────────────────────────────────────────────────────────────────
F зажат, nearest=CarryableObject
outline ON (оптимистично, локально)
hold timer 0→T
  отпустил → outline OFF, конец
  T истёк ──► RequestGrabCarryableRpc(netId) ──►  валидация:
                                                  • IsSpawned, дистанция ≤ radius+ε
                                                  • CarriedBy == 0 (не занят)
                                                  • weight ≤ capacity(clientId)
                                                    [StatsServer → strFlat → capacity]
                                                  • CheckRateLimit
                                                OK: CarriedBy=clientId (NetworkVariable)
                                                    rb.isKinematic=true (+NV-флаг)
                                                    CarryableObjectServer.MarkDirty()
──► ReceiveGrabResultRpc(ok) (SendTo.Owner)  ◄──  FAIL: reason ("занят", "тяжёлый")
outline подтверждён / OFF при fail

CARRYING: каждые 0.07с
──► SetCarryTargetRpc(netId, point, yaw) ──►  rate-limit; clamp |point-player| ≤ maxDist
                                              сервер: target=point (FixedUpdate:
                                              rb.MovePosition(Lerp(pos,target,followSpeed*dt)))

F нажат ──► RequestDropRpc(netId) ──────────►  CarriedBy=0; kinematic=false;
                                               (опц. throw-импульс = forward×2м/с)
                                               Save объекта (event-save)
──► ReceiveDropResultRpc ◄──────────────────  broadcast уже идёт NetworkTransform'ом
```

- RPC размещаются **на CarryableObject** (`[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]` + валидация sender) — прецедент: `NetworkPlayer.CollectNpcLootServerRpc` (строка 1609) валидирует по `rpcParams.Receive.SenderClientId`.
- Ответы owner'у — через существующую магистраль `NetworkPlayer.Receive*TargetRpc` (паттерн строк 1776–1980) или точечный `[Rpc(SendTo.Owner)]` на объекте. Рекомендация: на объекте — меньше правок NetworkPlayer.
- `CarriedByClientId : NetworkVariable<ulong>` — всем видно, кто несёт (outline у других не рисуем; анимация «нести» — будущее).

### 4.2 Почему сервер двигает объект, а не owner-клиент

- Требование ТЗ: «позиции предметов принимаются сервером».
- Консистентно с кораблями/NPC (server-auth) и с persistence: сервер всегда знает истинную позицию → save тривиален.
- Коробка-толкает-коробку: симуляция в одном месте (сервер) — нет расхождений «кто кого столкнул».
- Цена: у носителя задержка = RTT/2 на реакцию объекта. На host/p2p (текущий режим) незаметно; для dedicated с большим пингом — фаза polish: локальный prediction (двигаем visual, сервер корректирует).

### 4.3 Изменения в существующих файлах (минимальные)

| Файл | Изменение |
|---|---|
| `InteractableManager.cs` | +`_carryables` list, `RegisterCarryable/UnregisterCarryable/FindNearestCarryable` (копия блока pickups, строки 21–188) |
| `NetworkPlayer.cs` | (а) `FindNearestInteractable()` +`_nearestCarryable` (строка 1279); (б) **public accessor** `public bool IsActionHeldInput(GameAction a) => IsActionHeld(a);` и `IsActionJustPressedInput`; (в) в F-блоке (строка 622): передать управление `PlayerCarryController.TickF(...)` до `TryPickup()` — см. §6.1; (г) speed multiplier hook в `ProcessMovement` (читать `_carryController.SpeedMultiplier`), запрет run при ratio>threshold; (д) force-drop в `SubmitSwitchModeRpc` (посадка в корабль) и при смерти |
| `NetworkManagerController.cs` | +`CreateCarryableObjectServer()` (server-only) — паттерн строк 554–604 |
| `StatsServer.cs` | +публичный `float GetEffectiveStrength(ulong clientId)` (вынести формулу из строки 525) — для серверной валидации capacity |
| `TagManager` (слои) | +слой `Carryable` (индекс 7) |
| `DefaultNetworkPrefabs.asset` | +carryable-префабы (для server-spawned) |

Новый `GameAction` **не добавляем** (F переиспользуется) — enum не трогаем, сериализация `InputBindingsConfig.asset` в безопасности.

---

## 5. Persistence (между сессиями, без дубликатов)

Копия паттерна `ShipPositionServer` (§1.6):

**`Assets/_Project/Scripts/World/Carryable/CarryableObjectServer.cs`** (server-only singleton, DontDestroyOnLoad, создаётся из `NetworkManagerController`):

- **Save:** каждые 5с (тот же тик/интервал, что у кораблей) + **event-save на drop** (не ждать тика) + на `OnApplicationQuit`/shutdown сервера. Собирает все spawned `CarryableObject` (кэш со invalidate при spawn/despawn — паттерн `GetCachedShips`, строки 98–111).
- **DTO** `CarryableSaveData { string persistentId; string sceneName; float px,py,pz; float rx,ry,rz,rw; long savedAtUnix; }`.
- **Репозиторий** `JsonCarryableObjectRepository` → `Application.persistentDataPath/World/carryables.json`, atomic tmp+Move (копия `JsonCharacterDataRepository`, строки 93–116).
- **Restore:** через ~3.5с после `OnServerStarted` (после `ScenePlacedObjectSpawner`): матчинг по `PersistentId` → **только teleport существующих** (`rb.position = saved`), **никогда не Instantiate** → дубликатов нет by design. Объект из save, не найденный в сценах → лог + запись сохраняется (сцена может быть добавлена позже).
- **PersistentId:** дефолт `$"{gameObject.scene.name}/{GetFullPath(gameObject)}"` (как `ShipPersistentId`, строка 82 `ShipController.cs`); `_persistentIdOverride` в инспекторе — обязателен, если дизайнер клонирует объекты одним именем в одной сцене. Editor-валидация: предупреждение о дублях ID в открытой сцене.
- **Server-spawned объекты** (если когда-либо спавним динамически — дроп из инвентаря ящика-айтема): отдельный реестр `prefabGuid + persistentId=Guid.NewGuid()`; на restore — respawn из реестра, матчинг по GUID. **В MVP не делаем** — все carryable scene-placed.

---

## 6. Интеграция F и outline — детали, которые не учтены в запросе

### 6.1 Приоритетная цепочка F (критично!)

Сейчас F — это `PickupItem` (мгновенный подбор) **и** `ModeSwitch` (посадка/сбор/двери). Если рядом лежат PickupItem-зелье и коробка — одно нажатие F не должно сделать два действия. Правило (встраивается в `NetworkPlayer.Update`, строка 622):

```
F JustPressed / Held:
 1. Если PlayerCarryController.IsCarrying → F JustPressed = DROP. Стоп.     ← высший приоритет
 2. Если nearest == CarryableObject и ближе/равен PickupItem →
    carry-hold flow (TickF в PlayerCarryController). Pickup НЕ срабатывает.
 3. Иначе — существующий PickupItem блок (TryPickup), без изменений.
 4. ModeSwitch блок (gather/craft/door/ship): пропускается, пока активен
    carry-hold (флаг _carryHoldActive), иначе F во время 2с холда у коробки
    рядом с дверью откроет дверь.
```

Для этого `FindNearestInteractable()` сравнивает дистанции `_nearestPickup` vs `_nearestCarryable` — ближайший побеждает (как сейчас chest > pickup, строки 1281–1312).

### 6.2 Outline — конфликт с боевой подсветкой

(§1.3) — `TargetHighlightService` занят Q/E локом. CarryableOutline — отдельный per-object компонент, тот же материал. Outline видит **только несущий клиент** (чужая коробка не светится) — вызов локальный, без RPC. Желательно визуально отличать состояние «хватаю» (outline появился) от «несу» (можно второй проход/цвет — у материала есть параметры; опционально).

### 6.3 Hold-F — откуда читать состояние клавиши

`IsActionHeld/IsActionJustPressed` — private в NetworkPlayer. Минимальная правка: public-обёртки (§4.3). `PlayerCarryController.TickF(fHeld, fJustPressed)` вызывается из `NetworkPlayer.Update` — единая точка, никакого параллельного чтения `Keyboard.current` (дисциплина проекта: весь ввод в NetworkPlayer).

---

## 7. Пошаговый план реализации

### Фаза 0 — подготовка (0.5д)
1. Слой `Carryable` (Edit → Project Settings → Tags and Layers, индекс 7).
2. Physics collision matrix: `Carryable×Carryable` = ON (толкают друг друга); `Carryable×Player`-исключение делается кодом на время carry (§8.2), не матрицей.

### Фаза 1 — CarryableObject MVP без сети-переноса (1д)
3. `CarryableOutline.cs` (копия техники из `TargetHighlightService.cs:113-190`, static кэш материала `Resources/Materials/M_TargetOutline`).
4. `CarryableInteractionTrigger.cs` (child-форвардер в InteractableManager).
5. `CarryableObject.cs`: поля веса/радиуса/имени, `Rigidbody.mass=weightKg`, IInteractable, PersistentId-геттер.
6. `InteractableManager` +блок carryable.
7. Тест-префаб `Assets/_Project/Prefabs/World/Carryable/Crate_Carryable.prefab` (+ NetworkObject, NetworkTransform Server-auth, SceneBoundNetworkObject). Расставить 2–3 в тестовой сцене. Проверка: коробки падают, толкают друг друга, регистрация в InteractableManager по триггеру.

### Фаза 2 — hold-F и перенос (1.5д)
8. `PlayerCarryController.cs` (поля §3.2, формулы, hold-state machine, carry-точка = `player.position + forward*dist(ratio) + up*height(ratio)`; yaw от игрока).
9. `NetworkPlayer`: accessors, `_nearestCarryable`, цепочка §6.1, speed-mult hook, force-drop при посадке/смерти.
10. RPC: `RequestGrabCarryableRpc/ReceiveGrabResultRpc/SetCarryTargetRpc/RequestDropRpc` на CarryableObject; серверный kinematic-carry в FixedUpdate; `CarriedByClientId` NV.
11. UI: ветка в `HasNearbyInteractable/GetNearbyInteractableName`; MVP-прогресс холда.
12. Проверка: hold 2с → outline → несу (высота/дальность по весу) → F → падение с физикой; второй игрок видит то же.

### Фаза 3 — серверная валидация и lock (0.5д)
13. `StatsServer.GetEffectiveStrength(clientId)` (публичный); capacity-валидация в grab; дистанция grab/drop; rate-limit (паттерн `InventoryServer.CheckRateLimit`).
14. Занятость: grab fail «уже несут»; обрыв по `_breakawayDistance`; force-drop при disconnect носителя (`OnClientDisconnect` → поиск carried-объектов).

### Фаза 4 — persistence (1д)
15. `CarryableSaveData`, `JsonCarryableObjectRepository` (копия `JsonCharacterDataRepository`).
16. `CarryableObjectServer` (save 5с + on-drop + shutdown; restore 3.5с по PersistentId — teleport only) + `CreateCarryableObjectServer()` в NMC.
17. Проверка: передвинул коробку → перезапуск сервера → коробка на новом месте, **одна**.

### Фаза 5 — polish (по желанию)
18. Штраф скорости/бег по кривой; throw-импульс при drop (Shift+F = бросок); prediction носителя; урон от падающих тяжёлых объектов; SFX; анимация переноса; `OnControllerColliderHit`-толчение лёгких коробок без поднятия (`NetworkPlayer.cs:863` уже есть точка).

---

## 8. Edge cases (что не учтено в запросе)

| # | Случай | Решение |
|---|---|---|
| 8.1 | **Смерть / посадка в корабль / телепорт / дисконнект** во время carry | Force-drop на сервере: `_inputEnabled=false`-путь и `SubmitSwitchModeRpc` дёргают `PlayerCarryController.ServerForceDrop()`; disconnect — обработчик в CarryableObjectServer |
| 8.2 | **Объект толкает своего носителя** (stuck, отбрасывание) | На время carry: `Physics.IgnoreCollision(objCol, playerController.collider, true)` (или слой Carryable×Player off на пару коллайдеров), на drop — вернуть. Не глобальной матрицей — иначе коробки сквозь игроков всегда |
| 8.3 | **Два игрока хватают одну коробку** | `CarriedByClientId` lock на сервере, first-come; fail-RPC второму («уже несут») |
| 8.4 | **Перенос через границу сцены** (80 км-сетка) | На drop: `SceneBoundNetworkObject.SetScene(SceneID.FromWorldPosition(pos))` + `sceneName` в save; во время carry объект едет за игроком — visibility следует за носителем (CheckObjectVisibility по сцене носителя — расширить `ShouldClientSeeObject`: если carried — виден тем, кто видит носителя) |
| 8.5 | **Выгрузка сцены с брошенным объектом** | Сервер держит сцену загруженной, пока объект spawned; клиентам visibility-фильтр скрывает. Позиция из save при рестарте. Physics sleep для лежащих (rb автоматически) |
| 8.6 | **Античит** | Сервер: дистанция grab ≤ radius+ε; capacity по STR; clamp carry-точки; rate-limit RPC; drop не дальше maxDist от игрока |
| 8.7 | **Коробка на движущейся палубе корабля** | Оба dynamic на сервере → friction работает; НЕ добавлять PickupDeckRide (он для kinematic pickup'ов). Тест-кейс §9 |
| 8.8 | **Провал carry-точки сквозь стену** | Сервер clamp'ит точку; объект (kinematic) упирается в геометрию при MovePosition — игрок чувствует «зацеп»; breakaway-distance страхует от рассинхрона |
| 8.9 | **Клонируемые префабы с одинаковым именем** → дубли PersistentId | `_persistentIdOverride` + editor-валидация дублей в сцене |
| 8.10 | **Спам F** (grab/drop флуд) | `CheckRateLimit`-паттерн + `_isAwaitingServer` флаг на клиенте (как `PickupItem`, строка 50) |
| 8.11 | **Сериализация InputBindingsConfig** | Новый GameAction НЕ добавлять в середину enum; мы вообще не добавляем — переиспользуем F |
| 8.12 | **Бочки катятся** | `_freezeRotationWhenCarried`; в свободном состоянии — естественное круглое поведение (radius/цилиндр коллайдер), при желании `rb.angularDamping` повыше |
| 8.13 | **F-hold рядом с ResourceNode/дверью** | Флаг `_carryHoldActive` блокирует ModeSwitch-ветку на время холда (§6.1) |

---

## 9. Тест-план (для плейтеста пользователя)

1. Подойти к коробке 25 кг (лимит 40) → подсказка «удерживайте F» → hold 2с → outline → нести (точка ~1.7м/0.85м) → F → коробка падает, катится.
2. Коробка 60 кг → «Слишком тяжёлая (60/40)», hold не стартует.
3. Несомой коробкой сдвинуть вторую коробку — kinematic-вытеснение работает.
4. Бросить коробку на коробку → стек стоит (физика).
5. Два клиента: второй видит перенос; grab занятого — отказ.
6. Нести → сесть в корабль / умереть → коробка падает у точки.
7. Перенести коробку, выйти из игры, перезапустить сервер → коробка на месте, экземпляр один.
8. Коробка на палубе идущего корабля — едет с кораблём.
9. Перенос через границу сцены → drop → коробка видна игрокам новой сцены, после рестарта — там же.
10. Ребинд F в KeybindingsWindow → система работает на новой клавише (читаем через InputBindingsRuntime).

---

## 10. Открытые вопросы к гейм-дизайну

1. effectiveStrength (с экипом) или base tier для capacity? — рекомендую effective (пояс силы помогает).
2. Бросок силой (Shift+F) — нужен или только аккуратная установка?
3. Лимит одного carried-объекта на игрока = 1 (по умолчанию) — ок?
4. Урон/звук при падении тяжёлых объектов на NPC/игроков — отдельная итерация?
5. Нужна ли сетевая анимация «нести» (IK) — сейчас объект просто летит перед игроком.

---

## Приложение А. Индекс найденных файлов

| Тема | Файл | Ключевые строки |
|---|---|---|
| Бинды | `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` | GameAction enum 93–129 (добавлять в конец!), F-бинды 165/192 |
| Ввод игрока | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | F-цепочка 622–701; helpers 2215/2236 (private); pickupRange 81; collider-hit 863 |
| Реестр интерактивных | `Assets/_Project/Scripts/Core/InteractableManager.cs` | паттерн 21–188, 300+ |
| Интерфейс | `Assets/_Project/Scripts/Core/IInteractable.cs` | весь файл |
| Pickup-паттерн | `Assets/_Project/Scripts/Core/PickupItem.cs` | trigger 153–169; anti-spam 50 |
| Мировой объект + lock | `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs` | trigger 220–230; gatherer-lock 266–279 |
| Outline | `Assets/_Project/Scripts/Combat/Client/TargetHighlightService.cs` | single-target!, техника 113–190, материал 45 |
| Q/E лок | `Assets/_Project/Scripts/Combat/Client/TargetLockService.cs` | Highlight(0f) 226 |
| Статы | `Assets/_Project/Scripts/Stats/PlayerStats.cs` | StatsToFlat 130 |
| Статы сервер | `Assets/_Project/Scripts/Stats/StatsServer.cs` | GetPlayerStats 667; effective формула 525 |
| Статы клиент | `Assets/_Project/Scripts/Stats/StatsClientState.cs` | CurrentStats 54 |
| Persistence паттерн | `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionServer.cs` | save/restore весь файл; кэш 98–111 |
| Persistent ID | `Assets/_Project/Scripts/Player/ShipController.cs` | ShipPersistentId 82 |
| JSON repo | `Assets/_Project/Scripts/Stats/Persistence/JsonCharacterDataRepository.cs` | atomic write 93–116 |
| Серверный спавн | `Assets/_Project/Items/Network/InventoryServer.cs` | RequestDropRpc 138–183; CheckRateLimit |
| Сцены | `Assets/_Project/Scripts/World/Scene/SceneBoundNetworkObject.cs` | весь файл; `docs/world/LargeScaleMMO/2_iteration_scene-mode/SYSTEM_OVERVIEW.md` |
| Сервисы | `Assets/_Project/Scripts/Core/NetworkManagerController.cs` | Create*-паттерн 193–790 |
| Слои | Project Settings | свободно с 7; заняты 0,1,2,4,5,6,31 |
