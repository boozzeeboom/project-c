# Сводка разработки — 1–6 июля 2026

> **Период:** 1 июля – 6 июля 2026 · 67 коммитов · от `af6824537` до `a9e3674`
> **Предыдущая сводка:** [summary_30.06.2026.md](summary_30.06.2026.md)
> **Контекст:** Продолжение после v0.0.35. Полный цикл: ветер/физика → NPC на палубе → cargo refactor → рынок/NPC-трейдинг → repair manager → damage → большой рефакторинг → Unity 6.5

---

## 1. Общая статистика периода

| Метрика | Значение |
|---------|----------|
| Коммитов | **67** |
| Диапазон дат | 1 июля – 6 июля 2026 |
| Первый коммит периода | `9bd0f9a` — Winds applied on ships |
| Последний коммит | `a9e3674` — переход на Unity 6000.5.2f1 |
| Ключевых подсистем затронуто | **13** (ветер, физика, NPC-экипаж, cargo, рынок, модули, двигатель, ремонт, повреждения, Key-subsystem, очистка, документация, версия Unity) |

---

## 2. Основные направления разработки (хронологически)

### 2.1. 🌬 Ветер на корабли и персонажа (1 июля)

**Коммиты:** `9bd0f9a` (Winds applied on ships), `190693f` (новое поведение персонажа), `6e83116` (дизайн-док moving platform physics)

#### Что сделано

Ветер, ранее влиявший только на декоративные элементы, теперь полноценно воздействует на:
- **Корабли:** через `ShipController` — поля `windInfluence` и `windExposure`. `WindManager` аддитивно складывает глобальный и локальные `WindZone`. Ветер влияет на траекторию корабля как боковая сила.
- **Персонажа:** в `NetworkPlayer.ProcessMovement` ветер сносит персонажа на палубе и в полёте. Правила по состоянию: на палубе/на земле/в прыжке — ветер применяется с разными коэффициентами.

**Ключевые файлы:**
- `Assets/_Project/Scripts/Wind/WindManager.cs` — глобальный менеджер
- `Assets/_Project/Scripts/Wind/WindZone.cs` — локальные зоны
- `Assets/_Project/Scripts/Player/ShipController.cs` — windInfluence/windExposure поля
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — ProcessMovement: ветер

**Документация:**
- `docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md` — дизайн-док: transform-delta (не velocity), схема carry, edge-cases, тест-план

---

### 2.2. 🧍 Персонаж на движущейся палубе — не скользит, держится за корабль (1–2 июля)

**Коммиты:** `1c96610` (T-CH-MV minor fix), `bcba03b` (T-CREW-01 PlatformRideHelper), `a2e71ab` (T-CREW-02 docs), `bfc5f8a` (T-CREW-03 NpcBrain палубная навигация), `a873f23` (T-CREW anchor fixes), `b587133` (T-CREW fixes_2), `ca56b4f` (T-CREW fix setup), `5cb1f26` (T-CREW fix_3), `5a15999` (T-CREW docs), `cd61fac` (pickable items moving with ship)

#### Что сделано

**Проблема:** `CharacterController` не наследует движение платформы под ногами. Персонаж «сдувался» с палубы движущегося корабля, isGrounded мигал, гравитация копилась, подскоки.

**Решение — единый Move + PlatformRideHelper:**

1. **`PlatformRideHelper`** (`Assets/_Project/Scripts/Core/PlatformRideHelper.cs`) — общий статический хелпер для probe (Physics.SphereCast) + формула carry (ComputeCarryDelta). Используется `NetworkPlayer`, `NpcBrain` и `PickupDeckRide`.

2. **Единый Move** — carry-дельта платформы больше не применяется отдельным `_controller.Move`, а складывается в один финальный Move: `_controller.Move(motion * dt + _platformDelta)`. Два Move за кадр заставляли isGrounded мигать → «падение/приземление».

3. **`groundedForMovement`** — введён флаг `_isGrounded || _onPlatform`. Прыжок и ветер завязаны на этот флаг.

4. **Палуба NPC в Cruising** — `_velocity.y` держится на -2, без накопления → без подскоков.

5. **PickupDeckRide** — три уровня carry (персонаж, NPC, pickup'ы):
   - Попытка A (SetParent) ❌ — NGO запрещает не-NetworkObject parent
   - Попытка B (TrySetParent) ❌ — NGO проверяет direct parent, дочерние коллайдеры без NetworkObject
   - Попытка C (carry-формула) ❌ — _startPosition устаревала, pickup прыгал обратно
   - **Финальное D:** carry-формула + RefreshWorldBase() ✅

**Ключевые файлы:**
- `Assets/_Project/Scripts/Core/PlatformRideHelper.cs` — общий хелпер
- `Assets/_Project/Scripts/Core/PickupDeckRide.cs` — L3 carry для pickup'ов
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — L1: `_platformDelta`, `_onPlatform`, `groundedForMovement`
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — L2: палубная навигация через прокси-агент, WarpProxyToNpc

**Документация:**
- `docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md` — полная спецификация (3 уровня carry, история попыток, HARD RULES)
- `docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md` — канонический сценарий NPC на борту

---

### 2.3. 👾 NPC — часть корабля: ходят по палубе, преследуют игрока (2 июля)

**Коммиты:** `bfc5f8a` (T-CREW-03), `a873f23` (anchor fixes), `b587133` (fixes_2), `ca56b4f` (fix setup — пустой NavMeshData)

#### Что сделано

**Проблема:** NPC (гоблины на палубе) сдувались с корабля. NavMeshAgent привязан к мировому NavMesh — при движении корабля NPC «оставался» в мировой точке и падал с палубы.

**Решение — прокси-агент + палубная навигация (NpcBrain.cs):**

1. **DriveDeckNav** — вместо абсолютного телепорта NPC используется относительное смещение через `DeckLocalToWorld(NavToDeckLocal(proxy.pos))`. Это фиксит «сдувает» — NPC не ставится мимо палубы при неидеальном bake.

2. **WarpProxyToNpc** — переписан (NpcBrain.cs:408-455). Прокси следует за NPC, а не наоборот.

3. **NavMeshData fix** — корневая причина «сдува»: пустой NavMeshData-ассет в префабе корабля не давал NPC корректно ходить по палубе.

4. **BeginRide/EndRide цикл** — раньше probe терял платформу → EndRide → сдув → BeginRide → цикл. Теперь anchor стабилен.

**Итог:** NPC полноценно ходят по палубе движущегося корабля, преследуют игрока, не сдуваются.

**Документация:**
- `docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md`

---

### 2.4. 📦 Большой рефакторинг Cargo (2–3 июля) — 4 эпика

**Коммиты (19+):** `d01a15b` → `9f75144`

#### Эпик 1: T-CARGO-UI-01 — детальный список груза в CharacterWindow

**Коммиты:** `d01a15b`, `bced8b8`, `9cc6c1c`

- **`ShipTelemetryState.cargoDetail[]`** — массив `CargoDetailDto[32]`. Сервер (ShipController.UpdateTelemetryState, 5 Hz) резолвит displayName/weight/dangerous/fragile через `TradeItemDefinitionResolver.TryGet` и пушит в NetworkVariable. Push-подход без новых RPC.
- **Багфикс:** `cargoMax = 0` → теперь через `ShipCargoRegistry.GetEffectiveLimits()`. `cargoUsed = Items.Count` → `ComputeTotalSlots()`.
- **UI:** `MyShipsTab.RenderCargoDetail()` — for each item: `displayName × qty (weight кг)`. Опасный/хрупкий с ⚠/❄ префиксом.
- **Рефакторинг вёрстки:** Header (10-15%) + 2 колонки (cargo слева, модули справа) + Footer (5%). Кастомные бары вместо ProgressBar.
- **CustomDropdown** — полностью VisualElement-based замена DropdownField (Unity 6 `GenericDropdownMenu` не стилизуется через USS).

**Ключевые файлы:**
- `Assets/_Project/Scripts/Ship/Network/ShipTelemetryState.cs` — `CargoDetailDto`, `cargoDetail[]`
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs` — `RenderCargoDetail()`
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/CustomDropdown.cs` — новый компонент

**Документация:**
- `docs/Ships/cargo_system/CARGO_UI_01_DESIGN_2026-07-02.md`
- `docs/Ships/UI/CARGO_TAB_DOCS_2026-07-02.md`

#### Эпик 2: T-CARGO-UI-02 — Cargo Manager (Exchanger-стиль консоль на корабле)

**Коммиты:** `77cc455`, `c0bef63`, `cf7cb53`, `120041b`, `5dbae78`, `9d623d9`, `516a4db`

**Создано 11 файлов:**
- `ShipCargoConsole` (MonoBehaviour + IInteractable) — на дочернем GO корабля
- `ShipCargoServer` (NetworkBehaviour) — RPC StoreToCargo / RetrieveFromCargo
- `ShipCargoConsoleWindow` (UI Toolkit) — левая панель=инвентарь, правая=трюм
- `ShipCargoClientState`, `ShipCargoResultDto`
- UXML/USS/PanelSettings

**Архитектурное решение:** обменный курс через `ResourceExchangeResolver` + `ExchangeRateConfig` (100 pickable-слитков = 1 cargo-ящик). Предотвращает эксплойт "распаковал-упаковал".

**Qty-кнопки:** min/-10/-1/лейбл/+1/+10/max для каждой панели.

**Багфиксы:**
- Критический: `RollbackAddItems` удалял предметы вместо возврата (9d623d9)
- `TradeWorld.NotifyCargoChanged(ulong)` — публичный метод для внешних систем (ShipCargoServer → ShipController)
- Debug.Log обёрнуты в `#if UNITY_EDITOR`

**Документация:**
- `docs/Ships/cargo_system/CARGO_UI_02_PLAN.md`
- `docs/Ships/cargo_system/CARGO_REMAINING_WORK_2026-07-02.md`

#### Эпик 3: T-CARGO-VIS-01 — 3D визуал наполнения трюма (ящики)

**Коммит:** `c718622`

- `ShipCargoVisual` (MonoBehaviour) — client-side, подписка на `ShipTelemetryClientState`, grid-размещение ящиков внутри BoxCollider, object pool, overflow-индикатор
- Ленивая подписка (ждёт NGO инициализацию)

**Файл:** `Assets/_Project/Scripts/Ship/Cargo/ShipCargoVisual.cs`

#### Эпик 4: T-CARGO-NPC-01 — NPC-корабли торгуют на рынке

**Коммиты:** `c6a4a8d`, `6e55c1b`, `39850e7`, `f9ecc23`, `9f75144`

- **`NpcCargoService`** (server-only singleton) — RunDwellTrade(): фаза 1 (unload: cargo → market.stock), фаза 2 (load: market.stock → cargo)
- **`TradeWorld.TryNpcBuy/TryNpcSell`** — server-only API, `useUnlimitedCredits` скипает проверку кредитов (GDD: безлимитный кошелёк на время тестов)
- **`NpcCargoTradeListConfig`** — ScriptableObject с конфигом: maxLoadSlots/Weight, sellAllOnArrival, buyItems[]
- **`NpcShipSchedule.GetOrInitCargoTrade()`** — авто-заполнение buyItems из пресетов (Courier = resource_mezium_box ×3 + resource_antigrav_box ×2)
- **Интеграция в `NpcShipController.NavTick`** — флаг `_cargoTradeDone`, выполняется ОДИН раз за docking

**Багфиксы (3 итерации):**
- `toLocationId` → `fromLocationId` (CurrentRoute уже указывает на следующий leg)
- `_cargoTradeDone = true` → `false` (по умолчанию)
- NPC получают доступ к рынку по FSM-инварианту, не по радиусу

**Документация:**
- `docs/NPC_others_peacfull/npc_ship/CARGO/IMPLEMENTATION_2026-07-03.md`
- `docs/NPC_others_peacfull/npc_ship/CARGO/T_CARGO_NPC_01_DESIGN_2026-07-03.md`

---

### 2.5. 🏪 Рефакторинг Market (MARKET-ID-REFACTOR) — 3 июля

**Коммиты:** `66671a0`, `9f75144`

**5 проблем → решение:**

| Проблема | Решение |
|----------|---------|
| P1: Жёсткая привязка MarketConfig → MarketServer (ручной список в BootstrapScene) | `MarketConfigCollector.CollectFromLoadedScenes()` — авто-сбор из всех MarketZone в загруженных сценах |
| P2: Разный регистр locationId (primium vs PRIMIUM) | Нормализация `ToUpperInvariant()` во всех реестрах |
| P3: Три независимых источника locationId | `MarketZone._marketConfig` (MarketConfig SO) вместо строки `locationId` |
| P4: Нет авто-обнаружения | Merge: сценарные + ручные (backward compat) |
| P5: 5 ручных шагов для добавления рынка | «Разместил MarketZone в сцене → рынок работает» |

**Ключевые файлы:**
- `Trade/Scripts/Config/MarketConfigCollector.cs` — нормализация + авто-сбор
- `Trade/Scripts/Network/MarketZone.cs` — `_marketConfig` поле, derived `LocationId`
- `Trade/Scripts/Network/MarketServer.cs` — авто-сбор из сцен
- `Trade/Scripts/Config/MarketConfig.cs` — `OnValidate()` авто-UPPERCASE
- `Trade/Scripts/Core/TradeWorld.cs` — нормализация ключей

**Editor-инструменты:**
- Tools → ProjectC → Trade → Migrate MarketZones to MarketConfig refs
- Tools → ProjectC → Trade → Add MarketConfig_Primium_test to MarketServer

**Документация:**
- `docs/Markets/MARKET_ID_REFACTOR_DESIGN.md` — полный диздок (7 разделов, 320 строк)

---

### 2.6. 🧹 Большая чистка (3 июля)

**Коммиты:** `a08b6bc`, `b317671`, `cf6158b`, `a7b7732`, `3355c2c`

#### 2.6.1 Cleanup debug logs (3 коммита)

- `MarketZone.cs` — убраны шумные debug-логи
- `NpcShipController.cs` — cleanup
- `NpcBrain.cs` — `_debugLog = false`, логи entered/left платформы не шумят

#### 2.6.2 Warnings cleanup (15 файлов) — `3355c2c`

| Категория | Файлы | Что сделано |
|-----------|-------|-------------|
| `FindObjectsSortMode` | CombatServer, NpcSpawner, MarketConfigCollector, MarketInteractor, InventoryUI, InventoryTab, CharacterWindow, NetworkPlayer, M3SetTestForces, MarketZoneMigrationTool | Убран устаревший параметр → `FindObjectsByType<T>()` |
| `FindObjectOfType → FindAnyObjectByType` | ConcreteTriggers, QuestServer, KeybindingsWindow | Современный API |
| `FindObjectsOfType → FindObjectsByType` | InventoryUI | |
| RPC атрибуты | CombatServer, NetworkPlayer | `RequireOwnership → RpcInvokePermission.Owner` |
| `VisualElement.transform → style.scale` | SkillTreeWindow | `new Scale(Vector3)` |
| Неиспользуемые переменные | StatsServer, SkillInputService, KeyRodInstanceBinding, ClientSceneLoader, DialogWindow, InventoryTab, QuestToast, CharacterWindow | Удалены dead-поля |

**Осознанно не тронуто:**
- `ShipKeyClientState` — намеренный техдолг (legacy ship-key RPCs)
- `CharacterWindow._inventoryCache` — запланированная миграция
- `GetInstanceID() → GetEntityId()` — требует тестирования отдельно

#### 2.6.3 T-SMOD-UI-01 — замена reflection на прямой доступ

**Коммит:** `5b90195`

`MyShipsTab.cs` — заменён reflection-based `TryGetModuleNames` + `TryGetNameField` (~77 строк) на прямой доступ к полям.

#### 2.6.4 T-UI esc bugfix

**Коммит:** `a7b7732`

Полный список всех окон учтён в `IsAnyExternalWindowOpen()` — теперь Esc корректно работает со всеми UI-окнами.

---

### 2.7. 🔧 Repair Manager, кастомные модули, кастомизация корабля (4–5 июля)

**Коммиты (12+):** `5c0ca53` → `42970b4`

#### 2.7.1 Базовый Repair Manager (T-MODUL) — 4 июля

**Коммиты:** `5c0ca53`, `793696a`, `361fa1d`, `aac99f1`, `8bddcec`

**Новые файлы (C#):**
| Файл | Назначение |
|------|-----------|
| `Scripts/Ship/ModuleShopEntry.cs` | ScriptableObject: модуль + цена + ресурсы |
| `Scripts/Ship/ModuleShopDatabase.cs` | ScriptableObject: список ModuleShopEntry |
| `Scripts/Ship/ShipModuleCatalog.cs` | static реестр: lookup модулей по moduleId |
| `Scripts/Ship/ShipModuleServer.cs` | NetworkBehaviour: серверные RPC install/remove/sell/repaint |
| `Scripts/Ship/UI/RepairManagerWindow.cs` | UI Toolkit окно ремонтного менеджера |
| `Scripts/Ship/RepairManager.cs` | MonoBehaviour на NPC в доке |
| `Editor/Tools/CreateModuleShopEntries.cs` | Генератор ShopEntry + Database ассетов |

**Созданные ассеты:**
- `ModuleShopDatabase.asset` — база с 8 ShopEntry
- 8 × `ShopEntry_MODULE_*.asset` — каждый со ссылкой на ShipModule + ценой

**Поток установки модуля:**
```
Игрок → RepairManager NPC (E) → RepairManagerWindow
  → выбор корабля → слот → модуль → «Установить»
  → ShipModuleServer.RequestInstallModuleRpc
  → [Server] валидация: ключ + IsDocked + совместимость
  → ShipModuleManager.ReplaceModule(slot, module)
  → OnModuleChangedClientRpc → обновление UI
```

**Багфиксы (4 итерации):**
1. Нет фона → `repair-panel` wrapper + твёрдый bg
2. Дропдаун не выбирает → `Add(_popup)` — popup не был в дереве
3. Не обновляется после установки/снятия → `DelayedRefresh(0.5f)`
4. Кнопка «Установить» в CharacterWindow → удалена из `MyShipsTab.cs`

**UI fixes:**
- Дропдаун: глючный самодельный → `CustomDropdown`
- Скролбар: толстый дефолтный → тонкий голубой (6px, rgba)

#### 2.7.2 Ship Observation Camera (Repair Manager) — 5 июля

**Коммит:** `ea9872f`

При открытии RepairManagerWindow камера переключается на наблюдение выбранного корабля:
- **FlyToShip:** отключает камеру игрока, включает свою (угол ~45° сверху-сбоку)
- **Стрелки (▲▼◀▶):** вращение камеры вокруг корабля
- **ReturnToPlayer:** при закрытии окна
- Аудиолистенер не создаёт (остаётся на камере игрока)

**UI:** левая панель 580px, margin-left: 40px, стрелки position:absolute справа.

**Файлы:**
- `Assets/_Project/Scripts/Ship/UI/ShipObservationCamera.cs`
- `Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs`
- `Assets/_Project/Resources/UI/RepairManagerWindow.uxml/.uss`

#### 2.7.3 Ship Repainting — 5 июля

**Коммит:** `42970b4`

Ship painting: цвет из палитры → credit payment через `RequestRepaintShipRpc`. Цвет синхронизируется через `ShipTelemetryState` (shipColorR/G/B, per-frame).

#### 2.7.4 Module Visual Preview (T-MODUL-VIS-01) — 5 июля

**Коммиты:** `21b7213`, `4ca8c69`, `b5acfba`

**ShipModule.cs** — 2 enum'а + 7 полей:
- `visualPrefab` (GameObject), `attachPositionOffset`, `attachRotationOffset`, `attachScale`
- `socketPath`, `attachAxis`, `previewInEditor`

**ModuleSlotEditor** — Editor-only preview tool:
- В иерархии выбираешь `ModuleSlot`
- В инспекторе секция «👁 Module Visual Preview»
- Перетаскиваешь ShipModule SO → жмёшь ▶ Preview
- Превью-объект `HideFlags.DontSave` — в сцену не попадёт
- Позиция/Rotation/Scale Offset, Socket Path, Attach Axis — мгновенное обновление

**Документация:**
- `docs/Ships/Modul_system/01_ARCHITECTURE.md` — архитектура модульной системы
- `docs/Ships/Modul_system/02_REPAIR_MANAGER.md` — гайд по настройке
- `docs/Ships/Modul_system/03_REPAINT_PLAN.md` — план репаинта

---

### 2.8. 🚀 Двигатель: ON/OFF, IDLE, удалённый контроль (5 июля)

**Коммиты:** `6d0a67d`, `57cb35a`

**Документация:** `docs/Ships/ENGINE_POWER_STATE.md`

#### Что сделано

Новый бинарный стейт: **ENGINE ON / ENGINE OFF**. Управление через **Enter**.

| Состояние | Поведение |
|-----------|-----------|
| **ENGINE OFF** | AntiGravity НЕ работает → корабль падает. Ввод игнорируется. |
| **ENGINE ON + пилот** | Полный полёт, расход топлива. |
| **ENGINE ON IDLE** (пилотов нет) | AntiGravity работает, idle-расход 0.05 fuel/s, ветер применяется. Корабль «завис». |
| **NPC-корабль** | Всегда ENGINE ON. При возврате управления — принудительное включение. |

**Топливная логика:**
- Запуск: `startEngineConsumption = 10% от maxFuel`
- IDLE: `idleConsumptionRate = 0.05 fuel/s`
- При `fuel == 0`: авто-выключение
- Оставленный включённым корабль потратит топливо, выключится и упадёт

**Выход (F):** разрешён всегда, на любой скорости. Двигатель остаётся в текущем состоянии.

**Сетевая синхронизация:** `NetworkVariable<bool> _netEngineRunning` — сервер пишет, клиенты читают. HUD показывает `ENGINE ON/OFF`.

**Задействованные файлы (8):**
| Файл | Изменение |
|------|-----------|
| `ShipFuelSystem.cs` | + startEngineConsumption, idleConsumptionRate |
| `ShipController.cs` | + _engineRunning, _netEngineRunning, SetEngineRunning(), ToggleEngineServerRpc(), IDLE-расход |
| `InputBindingsConfig.cs` | + ShipToggleEngine (Enter) |
| `PlayerInputReader.cs` | + OnShipToggleEnginePressed |
| `NetworkPlayer.cs` | Enter → ToggleEngineServerRpc(), убран speed-check при выходе |
| `NpcShipController.cs` | SetEngineRunning(true) при спавне и возврате |
| `ShipHudController.cs` | ENGINE ON/OFF индикатор в K3 |

---

### 2.9. 💥 Повреждения кораблю, ремонт у ремонтника (5 июля)

**Коммиты:** `2b91ec7` (T-SHIPDAMAGE-01), `7b46a80` (T-SHIPDAMAGE-02 DONE), `66ba9f1` (npc fixes)

**Документация:**
- `docs/Ships/damage_subsystem/00_DESIGN.md` — модель урона, формула, состояния, HP по классам
- `docs/Ships/damage_subsystem/01_ARCHITECTURE.md` — классы, NetworkVariable, потоки данных
- `docs/Ships/damage_subsystem/02_INTEGRATION_AND_REPAIR.md` — настройка, интеграция, ремонт

#### Архитектура

| Компонент | Тип | Назначение |
|-----------|-----|-----------|
| `ShipDamageConfig` | ScriptableObject | maxHull по классам (100/200/400/600), armorHull=5, формула столкновений, brokenSpeedMultiplier=0.1, ремонт=300кр |
| `ShipHull` | NetworkBehaviour, `IDamageTarget` | `NetworkVariable<int>` hull/maxHull, `ApplyDamage`, `ApplyCollisionDamage`, `OnHullChanged` |
| `ShipController` | (edit) | Кеш `_hull`, флаг `_hullBroken`, `WipeCargo()`, множитель hullSpeedMult |
| `ShipModuleServer` | (edit) | `RequestRepairHullRpc` — валидация + ремонт |

#### Два источника урона

| Источник | Механика |
|----------|----------|
| **Столкновения** | `ShipController.OnCollisionEnter` → `ShipHull.ApplyCollisionDamage(energy)`. Три защиты от ложных ударов при стыковке: minCollisionRelativeSpeed 3 м/с, postUndockGrace 3 сек, IsDocked guard. |
| **Боевое оружие** | `CombatServer.ResolveAttack` → `ShipHull.ApplyDamage(DamageResult)`. `IDamageTarget` с armorHull=5. |

#### Формула урона столкновений

```
energy = col.impulse.magnitude
if energy < 8: no damage
hullDamage = min(floor((energy - 8) * 0.5), 50)
```

#### Состояния

```
OPERATIONAL (HP > 0) → полные скорости, груз в трюме
         ↓ HP → 0
BROKEN (HP = 0) → скорости ×0.1, груз обнулён, двигатель работает, IsAlive() = true
         ↓ ремонт в доке (RPC)
OPERATIONAL (HP = max)
```

#### Ремонт

Только в доке: `ключ + IsDocked + TryModifyCredits(-300)` → `hull.RepairFull()` + `ClearHullBroken()`.

---

### 2.10. 🔄 Большой рефакторинг (SHIP_REFACTOR_PLAN) — 5–6 июля

**Ветка:** `refactor/key-subsystem-p1-2026-07-21` → merged to main

**Коммиты:** `9b7cf18` → `f4d2c9f`

**Документация:**
- `docs/Ships/Key-subsystem/31_KEY_ANALYSIS_2026-07-21.md` — 5 проблем, 4-шаговый план
- `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md` — комплексный план (6 фаз, 36 часов)
- `docs/Ships/ITERATIONS.md` — записи итераций

#### P1: Рефакторинг Key Subsystem (5 коммитов, -1139/+651 строк)

**5 найденных проблем:**
- A: 4 obsolete файла (ShipKeyBinding, ShipKeyServer, ShipKeyClientState, ShipKeyToast) — мёртвый код
- B: KeyRodInstanceBinding (164 строки, retry-loop) — scene-placed компонент
- C: ShipOwnershipRegistry (200+ строк) — дублирует KeyRodInstanceWorld
- D: registeredShipId=0 при pickup без Lost-instance
- E: ShipTelemetryState без ownerClientId

**4 шага:**
1. Удалить 4 Obsolete legacy файла + убрать CreateShipKeyClientState из NetworkManagerController + ReceiveShipKey*TargetRpc из NetworkPlayer
2. Fix registeredShipId=0 — поиск shipId из существующих instance-ов с тем же itemId
3. Удалить ShipOwnershipRegistry — ShipTelemetryClientState читает ownerClientId из telemetry напрямую
4. Удалить KeyRodInstanceBinding — ShipController создаёт KeyRodInstance в OnNetworkSpawn. TryPickup ищет Active+NONE instance. Guard от дубликата ключа через корутину CreateKeyInstanceWhenReady.

**Итог P1:** Было: 5 источников правды, 11 файлов. Стало: 1 источник правды (KeyRodInstanceWorld), 0 reflection.

#### P2: Удаление legacy CargoSystem + speed penalty fix

**Статус:** ✅ Уже реализовано (без изменений кода). CargoSystem.cs удалён (T-CARGO-05). ShipController использует `_serverCargoPenalty` NetworkVariable. ShipCargoRegistry для per-instance лимитов.

#### P3: Актуализация документации

- `roadmap-integration.md`: T-CARGO-01..05 → T-CARGO-01..06, +ShipCargoRegistry
- `legacy/AGENTS_SHIP_SYSTEM_SUMMARY.md`: +ссылка на SHIP_REFACTOR_PLAN
- `Key-subsystem/00_OVERVIEW.md` §12: миграция MetaRequirement — ЗАВЕРШЕНА

#### P5: Cargo ownership/security guard

**Коммит:** `f4d2c9f`

4 метода защищены `IsOwnerOfShip` guard:

| Файл | Метод |
|------|-------|
| ShipCargoServer | RequestStoreToCargoRpc |
| ShipCargoServer | RequestRetrieveFromCargoRpc |
| MarketServer | RequestLoadToShipRpc |
| MarketServer | RequestUnloadFromShipRpc |

**Документация:** `docs/Ships/cargo_system/CARGO_OWNERSHIP_DESIGN.md`

---

### 2.11. 🎮 Переход на Unity 6000.5.2f1 (6 июля)

**Коммит:** `a9e3674`

- Восстановление `WorldScene_0_0`
- Переход на Unity 6000.5.2f1
- `.gitattributes` fix

---

## 3. Сводная карта подсистем (состояние на 6 июля)

| Подсистема | Статус | Ключевые коммиты |
|------------|--------|-------------------|
| **Ветер** | ✅ на корабли + персонажа | `9bd0f9a`, `190693f` |
| **Персонаж на палубе** | ✅ не скользит, единый Move | `1c96610`, `bcba03b` |
| **NPC на палубе** | ✅ ходят, преследуют | `bfc5f8a`–`cd61fac` |
| **Cargo UI (CharacterWindow)** | ✅ детальный список + 2-колонки | `d01a15b`, `bced8b8` |
| **Cargo Manager (консоль)** | ✅ Exchanger-стиль, qty-кнопки | `77cc455`–`5dbae78` |
| **Cargo 3D visual** | ✅ object pool, overflow | `c718622` |
| **NPC Cargo (трейдинг)** | ✅ полный цикл buy/sell | `c6a4a8d`–`9f75144` |
| **Market ID Refactor** | ✅ нормализация + авто-сбор | `66671a0` |
| **Cleanup (warnings + debug)** | ✅ 15 файлов, -77 строк reflection | `3355c2c`, `5b90195` |
| **Repair Manager** | ✅ install/remove/sell модулей | `5c0ca53`–`8bddcec` |
| **Ship Observation Camera** | ✅ FlyToShip + орбита | `ea9872f` |
| **Ship Repainting** | ✅ цвет + кредиты | `42970b4` |
| **Module Visual Preview** | ✅ Editor tool, HideFlags.DontSave | `21b7213`, `4ca8c69` |
| **Engine ON/OFF** | ✅ Enter, IDLE, fuel logic | `6d0a67d`, `57cb35a` |
| **Ship Damage** | ✅ HP, столкновения, ремонт | `2b91ec7`, `7b46a80` |
| **Key Subsystem Refactor** | ✅ P1 (-1139/+651, 0 reflection) | `d04c5e8`–`01a4d13` |
| **CargoSystem удалён** | ✅ P2 | `3e7aa92` |
| **Документация актуализирована** | ✅ P3 | `3e7aa92`, `af0fd55` |
| **Cargo ownership guard** | ✅ P5 | `f4d2c9f` |
| **Module Visual (L1)** | ✅ P4 | `b5acfba` |
| **Unity 6.5** | ✅ 6000.5.2f1 | `a9e3674` |

---

## 4. Ключевые документы (references)

| Документ | Описание |
|----------|----------|
| `docs/Ships/ITERATIONS.md` | Лог итераций P1–P5 |
| `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md` | Комплексный план (6 фаз) |
| `docs/Ships/Key-subsystem/31_KEY_ANALYSIS_2026-07-21.md` | Анализ Key Subsystem |
| `docs/Ships/Key-subsystem/00_OVERVIEW.md` | Обзор Key Subsystem |
| `docs/Ships/ENGINE_POWER_STATE.md` | Двигатель ON/OFF |
| `docs/Ships/damage_subsystem/00_DESIGN.md` | Повреждения — дизайн |
| `docs/Ships/damage_subsystem/01_ARCHITECTURE.md` | Повреждения — архитектура |
| `docs/Ships/damage_subsystem/02_INTEGRATION_AND_REPAIR.md` | Повреждения — интеграция |
| `docs/Ships/Modul_system/01_ARCHITECTURE.md` | Модульная система — архитектура |
| `docs/Ships/Modul_system/02_REPAIR_MANAGER.md` | Repair Manager — гайд |
| `docs/Ships/cargo_system/CARGO_REMAINING_WORK_2026-07-02.md` | Cargo — сводный план (все 4 эпика ✅) |
| `docs/Ships/cargo_system/CARGO_OWNERSHIP_DESIGN.md` | Cargo ownership guard |
| `docs/NPC_others_peacfull/npc_ship/CARGO/IMPLEMENTATION_2026-07-03.md` | NPC Cargo — реализация |
| `docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md` | Физика на движущейся платформе |
| `docs/Markets/MARKET_ID_REFACTOR_DESIGN.md` | Market ID Refactor |
| `docs/dev/summary_05.07.2026.md` | Сводка реализованных фич (аудит кода) |
| `docs/Ships/UI/CARGO_TAB_DOCS_2026-07-02.md` | Cargo UI документация |

---

## 5. Краткое резюме по дням

```
01.07 — Ветер на корабли и персонажа. Физика персонажа на палубе: единый Move + PlatformRideHelper. NPC ship avoidness.
02.07 — T-CREW: NPC на палубе (прокси-агент, NavMesh fix, anchor). Cargo UI-01: детальный список, 2-колонки, CustomDropdown. Pickup'ы на палубе (PickupDeckRide).
03.07 — Cargo UI-02 (консоль). Cargo VIS-01 (3D ящики). Cargo NPC-01 (NpcCargoService + TradeWorld API). MARKET-ID-REFACTOR. Cleanup (debug логи, warnings, reflection). UI esc bugfix.
04.07 — Repair Manager MVP (ModuleShopEntry, ShipModuleServer, RepairManagerWindow). UI fixes (4 итерации).
05.07 — Module Visual Preview. Ship Observation Camera. Ship Repainting. Engine ON/OFF + IDLE. Ship Damage Subsystem (HP, столкновения, ремонт). Key Subsystem P1 (4 шага, -1139 строк). P2 (CargoSystem). P3 (документация). P4 (Module Visual L1). P5 (Cargo ownership). Сводка (summary_05.07.2026.md).
06.07 — Фикс WorldScene_0_0. Переход на Unity 6000.5.2f1. .gitattributes fix. Запись P5 итерации.
```

---

*Составлено: 6 июля 2026*
*Следующая сводка: после следующей крупной итерации разработки*
