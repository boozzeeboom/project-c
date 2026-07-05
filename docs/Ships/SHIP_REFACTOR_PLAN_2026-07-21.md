# Ship Refactor Plan — Комплексный План Рефакторинга

**Дата:** 2026-07-21
**Автор:** Агент Aura (глубокий анализ всех ship-подсистем)
**Источник анализа:** `docs/Ships/` (55+ документов), `Assets/_Project/Scripts/Ship/` (60+ файлов кода), `Assets/_Project/Scripts/Player/ShipController.cs`, `Assets/_Project/Trade/`, `Assets/_Project/Items/`

---

## Executive Summary

Анализ выявил **5 критических архитектурных проблем** и **10+ пробелов** разной степени тяжести. Главная проблема — **Key Subsystem** переусложнён: 5 источников правды, runtime reflection, двойная миграция (MetaRequirement + KeyRodInstance). Остальной проект следует паттерну `Server → World → ClientState`, а Key — нет.

План разбит на **6 фаз**, оценённых в часах. Фазы независимы (можно выполнять в любом порядке), кроме зависимостей, указанных явно.

---

## Карта Фаз

| # | Фаза | Часы | Зависит от | Критичность |
|---|------|------|-----------|-------------|
| **P1** | Рефакторинг Key Subsystem | 13h | — | 🔴 Critical |
| **P2** | Удаление legacy CargoSystem + speed penalty fix | 3h | — | 🔴 Critical |
| **P3** | Обновление документации | 2h | P1, P2 | 🟡 High |
| **P4** | L1 Customisation (Module Visual) | 8h | — | 🟡 High |
| **P5** | Cargo ownership/security | 4h | — | 🟡 Mid |
| **P6** | Визуализация + polish | 6h | P1, P4 | 🟢 Mid |

**Total effort:** ~36 часов (4.5 рабочих дней)

---

## P1. Рефакторинг Key Subsystem (13 часов) 🔴

### Why

Текущая Key Subsystem — это **11 файлов, 5 источников правды, 3 reflection query layer**. 
Ни одна другая подсистема проекта так не работает.

**Паттерн всего проекта:** `Server (singleton NetworkBehaviour) → World (server-authoritative state) → ClientState (клиентская проекция)`.

Примеры: `ContractServer→ContractWorld→ContractClientState`, `InventoryServer→InventoryWorld→InventoryClientState`.

**Key Subsystem нарушает:** 5 singleton'ов для одной задачи (авторизация board'а):
```
ShipKeyServer (NetworkBehaviour)
  + MetaRequirementRegistry (NetworkBehaviour) — дубликат, [Obsolete] алиасы
  + KeyRodInstanceWorld (static, не NetworkBehaviour)
  + ShipOwnershipRegistry (NetworkBehaviour)
  + KeyRodInstanceBinding (MonoBehaviour, scene-placed)
```

**Источник:** `docs/Ships/Key-subsystem/28_KEY_ARCHITECTURE_REVIEW.md` (дизайн готов). `29_KEY_REFACTOR_PLAN.md` (план не завершён).

### Что делаем

#### Шаг 1.1 — Убрать reflection (Phase C из 29_KEY_REFACTOR_PLAN.md) [3h]

| Файл | Что менять |
|------|-----------|
| `Assets/_Project/Items/Core/InventoryWorld.cs` | Заменить `typeof(KeyRodInstanceWorld).GetMethod(...).Invoke()` на прямой `KeyRodInstanceWorld.CreateInstance()` / `TransferInstance()` / `UpdateState()` |
| `Assets/_Project/Items/Network/InventoryServer.cs` | Заменить `Type.GetType("...").GetMethod("TransferInstance")` на прямой вызов |
| `Assets/_Project/UI/Client/InventoryUI.cs` | Убрать `Type.GetType("ProjectC.Ship.Key.KeyRodInstanceBinding...")` и `shipField.GetValue(binding)` — заменить на прямой lookup |

**Verify:** grep `GetMethod\|GetField\|GetType.*Key\|Invoke.*Key` по проекту — должен быть 0 результатов.

#### Шаг 1.2 — Удалить KeyRodInstanceBinding [2h]

| Файл | Действие |
|------|---------|
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceBinding.cs` | ❌ Удалить |
| `Assets/_Project/Scripts/Player/ShipController.cs` | `OnNetworkSpawn()` server-only: создавать instance через `KeyRodInstanceWorld.CreateInstance(itemId, shipNetId, ownerPlayerId)` |
| `Assets/_Project/Scripts/Core/InteractableManager.cs` | Убрать поиск `KeyRodInstanceBinding` (если есть) |

**Verify:** сцена `WorldScene_0_0.unity` — больше нет ссылок на `KeyRodInstanceBinding` ни на одном объекте.

#### Шаг 1.3 — InventoryData serialization fix [1h]

| Файл | Что менять |
|------|-----------|
| `Assets/_Project/Items/Core/InventoryData.cs` | `GetIdsForType(ItemType.Key)` → возвращать `_keySlots?.Select(s => s.itemId).ToList()` вместо параллельных списков |

#### Шаг 1.4 — Удалить [Obsolete] алиасы [2h]

| Файл | Действие |
|------|---------|
| `ShipKeyBinding.cs` | ❌ Удалить (уже [Obsolete], алиас на `MetaRequirement`) |
| `ShipKeyServer.cs` | ❌ Удалить |
| `ShipKeyClientState.cs` | ❌ Удалить |
| `ShipKeyToast.cs` | ❌ Удалить |

**Verify:** поискать `ShipKeyBinding\|ShipKeyServer\|ShipKeyClientState\|ShipKeyToast` по всему проекту — использования должны быть только в `MetaRequirement*`.

#### Шаг 1.5 — Заменить ShipOwnershipRegistry на прямой KeyRodInstanceWorld [3h]

| Файл | Действие |
|------|---------|
| `Assets/_Project/Scripts/Ship/Network/ShipOwnershipRegistry.cs` | ❌ Удалить (избыточный слой) |
| `Assets/_Project/Scripts/Ship/Client/ShipTelemetryClientState.cs` | Заменить `ShipOwnershipRegistry.Instance` на прямой `KeyRodInstanceWorld.GetInstancesForPlayer(clientId)` |
| `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` | Без изменений (payload ок) |

**Verify:** `ShipOwnershipRegistry` не упоминается нигде. `ShipTelemetryClientState.OnShipStateChanged` работает как прежде.

#### Шаг 1.6 — Consolidate: MetaRequirement vs KeyRodInstanceWorld [2h]

**Решение:** `MetaRequirement` ≡ универсальная проверка «игрок имеет N предметов» (двери, блоки, NPC). `KeyRodInstanceWorld` ≡ ownership-слой для кораблей.

- `MetaRequirementRegistry` — **оставить**, универсальный хаб
- `KeyRodInstanceWorld` — **оставить**, но без reflection, без scene-placed binding
- `ShipOwnershipRequirement` — **оставить**, он соединяет MetaRequirement с KeyRodInstanceWorld

**Verify:** `NetworkPlayer.SubmitSwitchModeRpc` проверяет через `ShipOwnershipRequirement` → `KeyRodInstanceWorld.IsOwnerOfShip`.

### Результат P1

```
Было:  5 источников правды, 11 файлов, 3 reflection layer
Стало: 1 источник правды (KeyRodInstanceWorld), ~5 файлов, 0 reflection
```

---

## P2. Удаление legacy CargoSystem + speed penalty fix (3 часа) 🔴

### Why

`Assets/_Project/Trade/Scripts/CargoSystem.cs` (287 строк, namespace `ProjectC.Player`) — мёртвый дубль:
- `enum ShipClass` — дубликат `Trade.Core.ShipClass`
- `ShipLimits` Dictionary — дубликат `ShipClassLimits`
- `CheckLeak()`, `CheckFragile()` — мёртвый код (нигде не вызван)
- 3 сериализованные ссылки в `WorldScene_0_0.unity`

**Единственное живое использование:** `ShipController.cs:638` — `cargoSystem.GetSpeedPenalty()`. Но серверная `TradeWorld.GetSpeedPenalty()` уже реализована (T-CARGO-06).

### Что делаем

#### Шаг 2.1 — ShipController: переключить speed penalty на сервер [1.5h]

| Файл | Что менять |
|------|-----------|
| `ShipController.cs:638` | Вместо `cargoSystem.GetSpeedPenalty()` → читать `_serverCargoPenalty` (уже есть `NetworkVariable<float>`, T-CARGO-06) |
| `ShipController.cs` | Убрать `[SerializeField] private ProjectC.Player.CargoSystem cargoSystem` |

#### Шаг 2.2 — Удалить CargoSystem.cs [0.5h]

| Файл | Действие |
|------|---------|
| `Assets/_Project/Trade/Scripts/CargoSystem.cs` | ❌ Удалить |

#### Шаг 2.3 — Почистить WorldScene_0_0 [1h]

| Объект | Действие |
|--------|---------|
| `Ship_Light_root` / `Ship_Medium_root` / `Ship_Heavy_root` | Убрать missing-ссылки на `CargoSystem` (3 объекта) |

**Verify:** `grep "CargoSystem" Assets/` — 0 результатов. Play Mode: штраф скорости от груза работает через `TradeWorld`.

---

## P3. Обновление документации (2 часа) 🟡

### Why

Три ключевых документа утверждают «CargoSystem не существует» — это было правдой 17 июня, но устарело к 3 июля.

### Что делаем

| Документ | Что обновить |
|----------|-------------|
| `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` | Строка 67: «CargoSystem → ❌ Не существует» → «CargoSystem → ✅ Trade v2 (CargoData + TradeWorld + ShipCargoRegistry)» |
| `docs/Ships/analysis-composite-ship.md` | Строка 30: «CargoSystem → ❌ Отсутствует» → актуальный статус |
| `docs/Ships/roadmap-integration.md` | Строка 200: «Не делаем CargoSystem» → «CargoSystem → ✅ Готово (T-CARGO-01..06, июль 2026)» |
| `docs/Ships/legacy/AGENTS_SHIP_SYSTEM_SUMMARY.md` | Добавить ссылку на этот план рефакторинга |
| `docs/Ships/Key-subsystem/00_OVERVIEW.md` | Обновить статус: «Миграция на MetaRequirement — ЗАВЕРШЕНА», «Рефакторинг по 29_KEY_REFACTOR_PLAN.md — см. SHIP_REFACTOR_PLAN_2026-07-21.md P1» |

---

## P4. L1 Customisation — Module Visual (8 часов) 🟡

### Why

Персонаж имеет L1-L4 кастомизации (Equipment Visual, colors, proportions). Корабль — zero visual customisation. Асимметрия.

### Источник

`docs/Ships/customisation/00_SUMMARY.md §L1` — детальный план.

### Что делаем

#### Шаг 4.1 — ShipModule: добавить visualPrefab поле [1h]

| Файл | Что менять |
|------|-----------|
| `ShipModule.cs` | Добавить: `public GameObject visualPrefab;` + `attachPositionOffset` + `attachRotationOffset` + `attachScale` |

#### Шаг 4.2 — ShipModuleVisualApplier [4h]

| Файл | Назначение |
|------|-----------|
| `ShipModuleVisualApplier.cs` (новый) | MonoBehaviour на корне корабля. Subscribe → `ShipModuleServer.OnModuleChanged`. Spawn/despawn `visualPrefab` под `ModuleSlot.transform`. Отключать Colliders на visual. Object pool (по образцу `ShipCargoVisual.cs`). |

#### Шаг 4.3 — Тестовые визуалы [2h]

Создать 2-3 тестовых префаба (куб + цветной материал) для `MODULE_YAW_ENH`, `MODULE_MEZIY_THRUST_BASIC`, `MODULE_CARGO_BAY_01`.

#### Шаг 4.4 — Play Mode тест [1h]

Установить модуль через RepairManager → визуал появляется. Снять → исчезает.

---

## P5. Cargo Ownership / Security (4 часа) 🟡

### Why

`CARGO_DIAGNOSIS_2026-06-17.md §3.6`: «Cargo по `shipNetworkObjectId` — НЕТ проверки, что клиент действительно управляет этим кораблём. Любой клиент в зоне рынка может `TryLoadToShip` чужого корабля».

### Что делаем

#### Шаг 5.1 — ShipCargoServer: добавить ownership guard [2h]

| Файл | Что менять |
|------|-----------|
| `Trade/Scripts/Network/ShipCargoServer.cs` | `RequestStoreToCargoRpc` / `RequestRetrieveFromCargoRpc` → pre-check: `KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetId)` |

#### Шаг 5.2 — TradeWorld: добавить ownership guard [2h]

| Файл | Что менять |
|------|-----------|
| `Trade/Scripts/Core/TradeWorld.cs` | `TryLoadToShip` / `TryUnloadFromShip` → pre-check ownership (тот же вызов `KeyRodInstanceWorld.IsOwnerOfShip`) |

**Verify:** клиент не-владелец в той же MarketZone пытается `TryLoadToShip` чужого корабля → операция отклонена сервером.

---

## P6. Визуализация + Polish (6 часов) 🟢

### 6.1 HUD HP Indicator [2h]

| Что | Где |
|-----|-----|
| Добавить полосу здоровья в HUD (K3 или отдельно) | `ShipHudController.cs`, подписка на `ShipHull.OnHullChanged` |
| Цвет: зелёный > 50%, жёлтый > 25%, красный ≤ 25% | |

### 6.2 HUD Modules column (K1) — верификация [1h]

Настроить модули в сцене, проверить что K1 показывает реальные кружки-индикаторы.

### 6.3 Damage VFX (stub) [2h]

| Что | Где |
|-----|-----|
| При `HullState.Broken`: включить ParticleSystem дыма | `ShipHull.cs` + префаб партиклов |
| При ремонте: выключить | |

### 6.4 Удаление MeziyStatusHUD_Legacy.cs [1h]

| Файл | Действие |
|------|---------|
| `MeziyStatusHUD_Legacy.cs` | ❌ Удалить (заменён на ShipHudController K1) |

---

## Сводка по файлам (create / modify / delete)

### ❌ Удалить (7 файлов)

| Файл | В фазе |
|------|--------|
| `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceBinding.cs` | P1 |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs` | P1 |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyServer.cs` | P1 |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyClientState.cs` | P1 |
| `Assets/_Project/Scripts/Ship/Key/ShipKeyToast.cs` | P1 |
| `Assets/_Project/Scripts/Ship/Network/ShipOwnershipRegistry.cs` | P1 |
| `Assets/_Project/Trade/Scripts/CargoSystem.cs` | P2 |
| `Assets/_Project/Scripts/Ship/MeziyStatusHUD_Legacy.cs` | P6 |

### ✏️ Изменить (11+ файлов)

| Файл | В фазе |
|------|--------|
| `Assets/_Project/Items/Core/InventoryWorld.cs` | P1 |
| `Assets/_Project/Items/Network/InventoryServer.cs` | P1 |
| `Assets/_Project/UI/Client/InventoryUI.cs` | P1 |
| `Assets/_Project/Items/Core/InventoryData.cs` | P1 |
| `Assets/_Project/Scripts/Player/ShipController.cs` | P1, P2 |
| `Assets/_Project/Scripts/Ship/Client/ShipTelemetryClientState.cs` | P1 |
| `Assets/_Project/Scripts/Ship/ShipModule.cs` | P4 |
| `Trade/Scripts/Network/ShipCargoServer.cs` | P5 |
| `Trade/Scripts/Core/TradeWorld.cs` | P5 |
| `Assets/_Project/Scripts/Ship/UI/ShipHudController.cs` | P6 |
| `WorldScene_0_0.unity` | P2 (убрать CargoSystem ссылки) |

### 🆕 Создать (2 файла)

| Файл | В фазе |
|------|--------|
| `Assets/_Project/Scripts/Ship/ShipModuleVisualApplier.cs` | P4 |
| `Assets/_Project/Scripts/Ship/Combat/ShipHullVFX.cs` (или в ShipHull) | P6 |

### 📝 Документация

| Файл | В фазе |
|------|--------|
| `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` | P3 |
| `docs/Ships/analysis-composite-ship.md` | P3 |
| `docs/Ships/roadmap-integration.md` | P3 |
| `docs/Ships/legacy/AGENTS_SHIP_SYSTEM_SUMMARY.md` | P3 |
| `docs/Ships/Key-subsystem/00_OVERVIEW.md` | P3 |

---

## Принципы выполнения

1. **AGENTS.md соблюдается:** namespace `ProjectC.Ship`, private fields `_camelCase`, без `.meta`/`.asmdef`, код в `Assets/_Project/Scripts/`
2. **Additive-first:** не ломаем работающее. Каждый шаг — compile-clean + Play Mode тест
3. **Фазы независимы** (кроме P3 зависит от P1+P2). Можно распараллелить: P1, P2, P4, P5, P6 — разные разработчики
4. **После каждой фазы:** `git commit` с сообщением по шаблону `refactor(ship): P{N} — {description}`

---

## Open Questions (на усмотрение пользователя)

1. **Порядок выполнения:** P1 → P2 → P3 → P4 → P5 → P6, или другой приоритет?
2. **P1 (Key Subsystem):** полный рефакторинг (13h), или минимальный fix (убрать reflection, 5h)?
3. **P4 (Customisation):** делать L1 сейчас, или отложить до готовности арт-ассетов?
4. **P5 (Cargo security):** `IsOwnerOfShip` check на уровне ShipCargoServer + TradeWorld — достаточно, или нужен ещё middleware?
5. **P6 (Damage VFX):** нужен ли сейчас, или дождаться художника?
6. **Multi-crew (Phase 5 composite ship):** не в этом плане. Нужен ли отдельный roadmap?

---

*План создан агентом Aura на основе глубокого анализа 2026-07-21.*
*Следующий шаг: пользователь выбирает фазы и порядок выполнения.*
