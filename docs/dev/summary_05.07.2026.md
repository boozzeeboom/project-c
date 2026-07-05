# Сводка реализации корабельных фич — 2026-07-05

**Автор:** Mavis (Hermes)
**Основание:** Корректировка ошибочного анализа (предыдущая сводка 30.06.2026 — ~80% пунтков неверно помечены как нереализованные).
**Метод проверки:** grep по `.cs`-файлам, чтение ключевых классов.

---

## 1. Что реально реализовано (код — источник истины)

### 1.1 Управление кораблём — ShipController.cs (2132 строки)

| Фича | Где | Подтверждение |
|------|-----|--------------|
| SmoothDamp yaw/pitch/lift/thrust | ShipController.cs:208-221, 347-359 | `yawSmoothTime=0.6`, `pitchSmoothTime=0.7`, `liftSmoothTime=1.0`, `thrustSmoothTime=0.3` |
| Pitch limitation ±20° | line 269 | `maxPitchAngle = 20f` |
| Auto-stabilisation | lines 263-271 | `pitchStabForce=15`, `rollStabForce=20`, `autoStabilize=true` |
| Roll locked (крен заблокирован) | line 275 | `_rollUnlocked = false` если MODULE_ROLL не установлен |
| Angular drag 8.0 | line 261 | Выше GDD-спеки (3-5) |
| Class-specific mass | lines 242-249 | `massLight=800`, `massHeavy=1500`, `massHeavyII=2000`, `massMultiplier=10` |

### 1.2 Co-op piloting

| Фича | Где |
|------|-----|
| Множественные пилоты | ShipController.cs:340-345 `_pilots` HashSet |
| Усреднение ввода | FixedUpdate: суммирование + деление на `_pilots.Count` |
| NPC/autopilot input | `ApplyServerInput()` для автопилота (не через клиент) |

### 1.3 Altitude Corridor System (ПОЛНОСТЬЮ реализована)

| Компонент | Файлов/данных |
|-----------|--------------|
| `AltitudeCorridorSystem` | 223 строки, singleton с приоритетом city > global |
| `TurbulenceEffect` | 163 строки, тряска в DangerLower |
| `SystemDegradationEffect` | 116 строк, деградация систем в DangerUpper |
| `AltitudeCorridorData` | ScriptableObject |
| SO — global corridor | `Data/AltitudeCorridors/Corridor_Global.asset` |
| SO — city corridors | 5 городов: Primus, Secundus, Tertius, Quartus, Kilimanjaro |
| SO — veil/cloud layers | `AltitudeCorridor_veil_lower`, `cloud_layer`, `high_altitude`, `open_sky` |
| Editor tool | `CreateAltitudeCorridorAssets.cs` |

### 1.4 Модульная система

| Компонент | Статус |
|-----------|--------|
| `ShipModule.cs` ScriptableObject | ✅ |
| **ShipModule SO assets** | **8 модулей** в `Data/Modules/` + **2 cargo** в `Data/Ship/Modules/` |
| ShopEntry SO assets | 8 `ShopEntry_MODULE_*` |
| `ModuleShopDatabase` | ✅ |
| `ShipModuleManager` (server logic) | ✅ |
| `ShipModuleServer` (install/remove/sell/repaint RPCs) | ✅ |
| `ShipModuleVisualApplier` (spawn visualPrefab к slot) | ✅ |
| `ModuleSlotEditor` (preview tool) | ✅ |
| Установленные модули: | YAW_ENH, PITCH_ENH, LIFT_ENH, ROLL, MEZIY_YAW, MEZIY_THRUST, MEZIY_ROLL, MEZIY_PITCH, CARGO_BAY_01 |

### 1.5 Meziy-тяга (MeziyThrust)

- `MeziyModuleActivator.cs` — passive/active/overheat/cooldown
- `MeziyThrusterVisual.cs` — визуальное сопло
- `MeziyStatusHUD_Legacy.cs` — UI статуса

### 1.6 Ship Fuel System

- `ShipFuelSystem.cs` — `StartEngineConsumption`, `ConsumeFuel`
- Интегрирован с ShipController

### 1.7 Ship Repair + Painting

- `RepairManager.cs` + `RepairManagerWindow.cs` (UI Toolkit)
- Ship painting: цвет из палитры → credit payment (`RequestRepaintShip` RPC)
- Цвет включён в `ShipTelemetryState` (shipColorR/G/B, синхронизируется per-frame)

### 1.8 Ship Collision Damage

- `ShipCollisionDamageConfig.cs` — ScriptableObject
- ShipController.OnCollisionEnter — impulse-based cargo damage
- Damage to cargo items in cargo hold

### 1.9 Wind System

- `WindManager.cs` — глобальный ветер (аддитивный с локальными зонами)
- `WindZone.cs` — локальные зоны ветра
- Wind влияет и на корабли (`windInfluence`, `windExposure`), и на персонажей

### 1.10 Key System Phase 1

| Компонент | Статус |
|-----------|--------|
| `KeyRodInstanceWorld` (static facade) | ✅ с JSON-persistence |
| `KeyRodInstanceBinding` (NetworkBehaviour) | ✅ автоматическая регистрация |
| `KeyRodInstance` (данные) | ✅ Active/Stolen/Consumed/Destroyed |
| `ShipOwnershipRequirement` | ✅ ownership check |
| Scene-placed KeyRod pickup | ✅ |

### 1.11 Trade

- `MarketConfig.cs` (SO) + `MarketState.cs` (runtime POCO) ✅
- `MarketEvent.cs` — динамика цен (shouldTrigger, applyToMarket) ✅
- `TradeWorld` — ядро (markets + cargo + credits) ✅
- `ContractWorld` — контракты ✅
- `IPlayerDataRepository` — JSON persistence ✅

### 1.12 Crafting

- `CraftingWorld.cs` (static) + `CraftingStation.cs` (NetworkBehaviour) ✅
- `CraftingStationConfig.cs` (SO) ✅

### 1.13 Mining / Resource Gathering

- `ResourceNode.cs` (NetworkBehaviour) + `ResourceNodeConfig.cs` (SO) ✅

### 1.14 NPC Enemies (Goblins) — P2

| Компонент | Статус |
|-----------|--------|
| `NpcBrain.cs` (FSM) | ✅ |
| `NpcSpawner.cs` + `NpcSpawnerConfig.cs` | ✅ |
| `NpcAttacker.cs` (IAttacker) | ✅ + self-register в CombatServer |
| `NpcTarget.cs` | ✅ |
| `NpcCombatData.cs` | ✅ |
| `NpcVisualConfig.cs` + `NpcVisualApplier.cs` | ✅ |
| `Npc_Goblin.prefab` | ✅ в `Prefabs/AI/` |
| Full combat engine | `CombatServer`, `PlayerAttacker`, `DamageCalculator`, `IAttacker`, `IRangePolicy` — всё ✅ |

### 1.15 NPC Peaceful Ships — M3.2 (полный цикл)

| Состояние | NpcShipWorld FSM |
|-----------|------------------|
| Departing → Lifting → Yawing → Cruising → Berthing → Docked → Loading → Undocking → (back to Departing) | ✅ |
| NavTick — прямая физика (MoveRotation + linearVelocity) | ✅ |
| Control authority handoff (player vs NPC) | ✅ |
| Antigravity boost на departure | ✅ |

### 1.16 Docking System

- `DockingServer.cs` (NetworkBehaviour) ✅
- `DockingWorld.cs` (docking assignments, pad management) ✅
- `DockingAssignmentDto` ✅
- Player request → pad assignment → touchdown → docked ✅

---

## 2. Что НЕ реализовано (поиск по коду — 0 результатов)

| № | Фича | Комментарий |
|---|------|-------------|
| 1 | **P2P trading** (PeerToPeer, TradeOffer) | Нет кода вообще |
| 2 | **Key crafting copies** | `KeyCraft`, `DuplicateKey` — нет |
| 3 | **NPC key sales** | `KeyShop`, `SellKey` — нет |
| 4 | **MODULE_VEIL / MODULE_SPACE / MODULE_STEALTH / MODULE_AUTO_DOCK / MODULE_AUTO_NAV** | Упомянуты в GDD §4.2, SO assets нет |
| 5 | **Ship weapons / ship combat** | `ShipWeapon`, `ShipCannon` — нет кода |
| 6 | **Build system (платформы)** | `BuildSystem`, `PlatformBuilder` — нет (только analysis doc) |
| 7 | **Enemy NPC P3+** (group pathfinding, bosses) | NpcBrain есть, P3+ поведения — нет |
| 8 | **Key access levels** (Limited/OneTime) | `KeyRodAccessLevel` enum не существует в коде |
| 9 | **Visual customisation L2-L5** (decals, proportions, module shader) | Только paint реализован |
| 10 | **Power management (энергоситема)** | См. §4 |

**Замечание:** Departure (п.2 в предыдущем анализе) **реализован** как часть NPC Ship FSM (Undocking→Departing) + Player ExitDocked. Отдельной подсистемы T-DEPART нет, но функционал покрыт.

---

## 3. Расхождения GDD vs Code (исправления)

### 3.1 GDD_10 §3.1 — Физика движения

| Параметр | GDD spec | Code (ShipController.cs) | Поправка |
|----------|----------|--------------------------|----------|
| angularDrag | 3.0-5.0 | **8.0** | ×1.6-2.7 от спецификации |
| pitchStabForce | 2.0-3.0 | **15** | ×5-7.5 от спецификации |
| rollStabForce | 3.0-5.0 | **20** | ×4-6.7 от спецификации |
| yawSpeed (Light) | 35°/s | **30°** | Ниже спецификации |
| pitchSpeed (Light) | 25°/s | **20°** | Ниже спецификации |
| baseThrust (Light) | 300-400 | **300** | Нижняя граница |
| maxSpeed (Light) | 35-45 m/s | **30** (boost: 35) | Ниже спецификации |
| mass (Light) | 0.8-1.2 "tons" | **800→80** (scale mismatch) | GDD не учитывает massMultiplier=10 |

**Рекомендация:** Обновить GDD §3.2 — привести числовые колонки в соответствие с реальными значениями из ShipController.cs (_flightClassParams.Light например).

### 3.2 GDD_10 §4.1 — powerRequirement

`powerRequirement: float` — поле **отсутствует** в `ShipModule.cs`. Есть в GDD, но не в коде. См. §4.

### 3.3 GDD_10 §5 — KeyRod

| Параметр | GDD | Code | Расхождение |
|----------|-----|------|-------------|
| `KeyRodAccessLevel` | Full, Limited, OneTime | **Нет** | В коде есть только `KeyRodState` (Active/Stolen/Consumed/Destroyed) |
| `isDuplicate` | bool | **Нет** | В коде нет флага нелегальной копии |

### 3.4 GDD_10 §7.3.1 — Departure

GDD говорит "Phase 1.5, не реализован". **На самом деле**:
- Player-ExitDocked через DockingServer.RequestTakeoffRpc ✅
- NPC Undocking→Departing через NpcShipWorld FSM ✅
- Anti-gravity boost при departure ✅

### 3.5 GDD_10 §2.3 — Server Validation

GDD описывает server validation каждые 0.5с. В коде — per-frame (через ShipController._evaluateAltitude + AltitudeCorridorSystem.GetActiveCorridor). **Не server-тики, а каждый FixedUpdate.**

---

## 4. Power Management (энергосистема) — что это и откуда?

### 4.1 Происхождение

Единственный источник — **GDD_10 §4.1**, где в спецификации ShipModule указано:

```
public float powerRequirement;
```

В `docs/Character/03_DATA_MODEL.md` §7.8:
> `ModuleItemData.powerConsumption` не используется... \Оставляем поле — будущая интеграция с ship power system.

В `docs/Character/08_ROADMAP.md` п.7:
> Module power consumption — поле есть, не используется (нет ship power system)

### 4.2 Что это должно было быть

По задумке:
- Каждый модуль потребляет `powerRequirement` (Вт)
- Корабль имеет **power budget** (зависит от класса)
- Сумма `powerRequirement` установленных модулей ≤ budget
- Если превышение — часть модулей отключается / overload / penalty
- Механика баланса: нельзя установить все лучшие модули сразу → нужно выбирать

### 4.3 Текущий статус

**Не реализовано.**
- `ShipModule.cs` не содержит поля `powerRequirement`
- Нет класса/системы `PowerManagement` / `PowerSystem`
- Модули устанавливаются без ограничения по энергии
- Единственная балансировка — через слоты (кол-во мест под модули) + `ValidateModuleCompatibility`

### 4.4 Когда и зачем может появиться

1. Когда кол-во модулей в игре превысит ~15–20
2. Когда появятся модули PvP/PvE (stealth, weapons) — энергия как ресурс
3. Когда будет введён Engine Off / start-up состояния
4. **НЕ в MVP** — текущая механика "просто слоты" работает для 8± доступных модулей

---

## 5. Выводы

- **Код — истина.** ~12 пунктов из первоначального списка (30.06.2026) оказались уже реализованы.
- Реально не сделано: P2P торговля, P3+ NPC, ship weapons, 5 специальных модулей, key Phase 2, build system, power management, L2-L5 customisation.
- Самое ближайшее к реализации: key Phase 2 (базовый функционал Phase 1 работает) и power management (но это не блокер — roadmap говорит "postponed").

---

История изменений:
| Дата | Сессия | Изменения |
|------|--------|-----------|
| 2026-07-05 | Mavis code-audit | Сводка по коду, исправление ошибок предыдущего анализа, документирование расхождений GDD |
