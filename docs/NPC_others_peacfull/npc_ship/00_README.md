# Peaceful NPC Ships — Architecture & Roadmap

> **Project C: The Clouds** | Unity 6000.4.1f1 | NGO 2.11.0 | URP 17.0.3
> **Статус:** M3.2.15 — первый рабочий round-trip ✅ (2026-06-24)
> **Всего коммитов:** ~27 от 9d8bc1c до 706a9d1
> **Retrospective:** `99_RETROSPECTIVE.md`
> **Каталог:** `docs/NPC_others_peacfull/pc_ship/`
> **Конвенция:** См. `AGENTS.md` и `project-c-bootstrap` skill (Unity 6, NGO 2.11, UI Toolkit, .NET 8).

---

## Назначение

Подсистема мирных NPC-кораблей для Project C: The Clouds. NPC-корабли физически присутствуют в мире, курсируют между docking stations (городами) по расписанию, занимают pads, паркуются, разгружаются/загружаются, отстыковываются и летят к следующему городу. Сервер учитывает где какой корабль с каким NPC находится — в пути, застыкован или в процессе стыковки/отстыковки.

**Scope (M1):** 4 NPC-корабля в одной сцене (`WorldScene_0_0`), курсируют между Примум и второй зоной вблизи. Стартуют `Docked` на pad'ах. Без спавна/деспавна. Без cargo/market — только «живой» трафик.

**V2 (в будущем):** привязка реального рынка и перемещения товаров к кораблям через `NpcShipCargoManifest` и events. Зарезервировано в M1.

---

## Карта документации

| # | Файл | О чём | Слов |
|---|------|-------|------|
| **00** | `00_README.md` | Этот файл — навигация, TL;DR, финальные решения | ~700 |
| **01** | `01_REUSE_MAP.md` | Что уже существует: переиспользуемые API из Docking, Ship, World | ~800 |
| **02** | `02_INDUSTRY_PATTERNS.md` | Исследование индустриальных паттернов NPC-траффика | ~1300 |
| **03** | `03_V2_ARCHITECTURE.md` | Namespaces, lifecycle, v2 extension points, autopilot hook | ~1700 |
| **04** | `04_LIVING_BEHAVIOR.md` | «Живость»: FSM, dwell times, traffic shaping, pad contention, anti-grav | ~1300 |
| **05** | `05_ROADMAP.md` | M1-M3 + тикеты T-NS00..10, dependency graph | ~1100 |
| **06** | `06_OPEN_QUESTIONS.md` | Финальные решения (13 ответов пользователя) | ~1000 |
| **99** | `99_RETROSPECTIVE.md` | Полный ретроспективный анализ: 10 кругов ошибок, корневые причины, уроки | ~1500 |
| **—** | `CHANGELOG.md` | Лог изменений каталога | — |

**Всего:** ~7900 слов, 8 файлов.

---

## TL;DR — ключевые решения (приняты 2026-06-22)

1. **NPC-корабль = scene-placed `NetworkBehaviour`** (`NpcShipController` + `ShipController`) на корне NPC-корабля в `WorldScene_0_0`. Спавнится автоматически через `ScenePlacedObjectSpawner`. Не префабный спавн.

2. **Движение без клиента** — новый `ShipController.ApplyServerInput(thrust, yaw, pitch, vertical, boost)` (server-only, минует `_pilots` gate). Также выставляется флаг `_hasNpcPilot` (явно, для ясности). **Этот же API может стать основой для автопилота игрока в v2.**

3. **Захват pad** — вызов `DockingWorld.AssignPadForNpc()` с sentinel `NpcInstanceId`. Минует RPC-путь. Учитывает `maxConcurrentLandings`. Player-first: NPC уступает pad при displacement.

4. **Anti-gravity override** на 5 сек после `ExitDocked()` — корабль не «упадёт» пока NPC-pilot не подаст thrust.

5. **Traffic shaping** = Gaussian + min spacing. `NpcShipTrafficManager` разносит прибытия, чтобы не было «4 NPC одновременно».

6. **4 hand-authored NPC** в тестовой сцене + Примум + ещё 1 зона вблизи (мини-тест в одной сцене). Без спавна/деспавна.

7. **v2 forward-compat** через `NpcShipCargoManifest` (struct, пустой в M1), `demandCategory` enum, events `OnNpcShipArrived/Departed/Loaded/Unloaded`. Без breaking changes при M1 → v2.

---

## Финальные решения (Q1..Q13)

| # | Вопрос | Решение |
|---|--------|---------|
| Q1 | ApplyServerInput дизайн | **A** — новый public method. V2 hook: autopilot игрока |
| Q2 | `_hasNpcPilot` flag? | **Custom** — явный flag (server-only, enable/disable API) |
| Q3 | NPC InstanceId | **A** — `NetworkObjectId | 0x8000_0000_0000_0000UL` |
| Q4 | ShipOwnershipRegistry | **A** — NPC не регистрируется |
| Q5 | Loading в M1 | **A** — 30-90 сек пауза |
| Q6 | maxConcurrentLandings для NPC | **A** — учитывать |
| Q7 | Multi-station per location | **B** — single в M1, v2 сделает multi |
| Q8 | Gravity после ExitDocked | **A** — anti-gravity override 5 сек |
| Q9 | Rate limiting NPC | **A** — не нужен (FSM ограничивает) |
| Q10 | Cargo manifest | **A** — пустой struct в M1 |
| Q11 | Количество NPC | **4 NPC** (расширим позже) |
| Q12 | Маршрут | **Примум + ещё 1 зона вблизи** (в одной сцене) |
| Q13 | Стартовое состояние | **A** — Docked на pad при старте |

---

## Reuse summary (что уже есть «из коробки»)

| Компонент | NPC ready? |
|-----------|-----------|
| `DockingWorld._occupiedPads` (ulong → clientId/NpcInstanceId) | ✅ |
| `DockingWorld.ScanExistingOccupants()` | ✅ |
| `ShipController.EnterDocked()/ExitDocked()` | ✅ |
| `DockStationDefinition.LocationId` (синк с Market) | ✅ |
| `DockingZoneRegistry._stationsByLocation` | ✅ |
| `ScenePlacedObjectSpawner` | ✅ |
| `DockPadVisualMarker` (overlap) | ✅ |
| `DockingPadTriggerBox.IsShipInside` | ✅ |
| `ShipController.SubmitShipInputRpc` | ❌ — нужен `ApplyServerInput()` |
| `DockingServer RPCs` | ❌ — нужен server-internal API |

**Blocker (resolved in M1):** `ShipController.FixedUpdate` gate `if (_pilots.Count == 0) return;` — будет обойдено через `_hasNpcPilot` flag + `ApplyServerInput` запись в `_sumXxx` напрямую.

---

## Архитектура (высокоуровнево)

```
ProjectC.PeacefulShip
├── Core/
│   ├── NpcShipWorld          (server singleton, FSM tick)
│   ├── NpcShipState          (POCO: route, status, schedule index)
│   ├── NpcShipRoute          (struct: from/to location, dwell time)
│   └── NpcShipCargoManifest  (v2 hook: struct, пустой в M1)
├── Network/
│   ├── NpcShipServer         (NetworkBehaviour hub, BootstrapScene)
│   ├── NpcShipTrafficManager (server singleton, Gaussian shaping)
│   └── NpcShipZoneRegistry   (static: NpcInstanceId → NpcShipController)
├── Stations/
│   ├── NpcShipController     (scene-placed NetworkBehaviour)
│   └── NpcShipSchedule       (ScriptableObject — routes + dwell)
├── Client/
│   ├── NpcShipClientState    (singleton, UI projection)
│   └── NpcShipSnapshotDto    (INetworkSerializable)
└── Dto/
    ├── NpcShipSpawnDto
    └── NpcShipStatusDto
```

**Зависимости:** `ProjectC.Docking.Core`, `ProjectC.Player` (ShipController), `ProjectC.Docking.Network` (DockingZoneRegistry), `ProjectC.Trade.Network` (NetworkingUtils).

---

## Что дальше

✅ Дизайн-фаза закрыта. Можно начинать **T-NS00**:
- Core POCOs (`NpcShipState`, `NpcShipRoute`, `NpcShipCargoManifest`)
- + `ShipController.ApplyServerInput()` + `_hasNpcPilot` flag

Ожидаемый объём: ~150 LOC, 60 мин coding + verify.

Подробнее: `05_ROADMAP.md` (11 тикетов T-NS00..10, dependency graph).