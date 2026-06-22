# CHANGELOG — Peaceful NPC Ships

> Лог изменений каталога `docs/NPC_others_peacfull/pc_ship/`.

---

## 2026-06-22 — T-NS00: Core POCOs (NpcShipState + NpcShipRoute + NpcShipCargoManifest)

**Сессия:** Реализация по 05_ROADMAP.md, тикет T-NS00
**Профиль:** project-c
**Статус:** ✅ Реализовано. Compile expected = 0 errors (verify в Unity Editor — пользователь запускает).

### Созданные файлы (4)

| Файл | LOC | Назначение |
|------|-----|-----------|
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipStatus.cs` | ~25 | `public enum NpcShipStatus : byte` — 12 состояний FSM |
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipRoute.cs` | ~45 | `NpcShipRoute` struct + `NpcShipDemandCategory` enum |
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipCargoManifest.cs` | ~60 | `NpcShipCargoManifest` + `NpcCargoEntryDto` — INetworkSerializable (v2 hook, M1 empty) |
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipState.cs` | ~45 | `NpcShipState` class (POCO, server-only) |

**Итого:** ~175 LOC, 4 файла.

### Что внутри

**`NpcShipStatus.cs`** — enum из 12 состояний (Idle, Departing, InTransit, Approaching, Holding, Diverting, Docking, Docked, Loading, Undocking, Done). См. `04_LIVING_BEHAVIOR.md §2`.

**`NpcShipRoute.cs`** — struct с `fromLocationId`, `toLocationId`, `dwellTimeSec`, `flightDurationSec`, `preferredShipClass` (Light/Medium/Heavy), `demandCategory`. `NpcShipDemandCategory` enum (Generic/HighDemand/LowDemand/Contract) для v2 market-driven routing.

**`NpcShipCargoManifest.cs`** — INetworkSerializable struct. В M1 — `capacitySlots=0, capacityWeight=0, items=null`. Pattern `NetworkSerialize` с dynamic array length (как `DockingDto`).

**`NpcShipState.cs`** — POCO с `NpcInstanceId` (sentinel bit), `Ship` ref, `Status`, `CurrentRoute`, `StateEnteredAt`, `ScheduleIndex`, `LastKnownPosition`, `Cargo`. Конструктор инициализирует `Idle` state.

### Конвенции проекта (применены)

- ✅ Namespace `ProjectC.PeacefulShip.Core` (по `02_V2_ARCHITECTURE.md §1`)
- ✅ `public enum X : byte` (как `DockingStatus`, `SkillCategory`, etc.)
- ✅ Один class/enum = один .cs файл (Unity 6: T-DOCK-13c fix)
- ✅ `[System.Serializable]` на struct (для инспектора и INetworkSerializable)
- ✅ `INetworkSerializable` с NGO 2.x pattern (создание array на reader side)
- ✅ `using Unity.Netcode;` явно в файлах с DTO

### Что НЕ делалось

- ❌ Никаких изменений в существующих подсистемах (ShipController, DockingWorld)
- ❌ Никаких .meta файлов — Unity создаст при refresh
- ❌ Никаких тестов (не указано в M1 — smoke test в T-NS10)
- ❌ Никаких git-коммитов

### Что пользователь должен проверить

**Шаг 1: Refresh + Compile**
1. Открыть Unity Editor → должен произойти auto-refresh (если нет — меню `Assets > Refresh`)
2. Подождать окончания компиляции (~30-60 сек)
3. Открыть `Window > General > Console`
4. **Ожидаемо:** 0 errors, 0 warnings (по коду T-NS00)

**Шаг 2: Проверить файлы на диске**
```
Assets/_Project/Scripts/PeacefulShip/Core/
├── NpcShipStatus.cs
├── NpcShipRoute.cs
├── NpcShipCargoManifest.cs
└── NpcShipState.cs
```
5 файлов `.meta` появятся автоматически после refresh.

**Шаг 3: Проверить, что namespace виден**
- В Visual Studio / Rider: Ctrl+Click на `NpcShipStatus` → должен открыться enum.
- Или: поиск по проекту `ProjectC.PeacefulShip.Core.NpcShipStatus` — должен найти 1 определение.

### Следующий тикет

**T-NS01:** `ShipController.ApplyServerInput()` + `_hasNpcPilot` flag + `AntiGravity` property (server-only extensions).
- Файл: `Assets/_Project/Scripts/Player/ShipController.cs` (расширение)
- LOC: ~50
- Время: ~30 мин coding + verify

Скажите «**поехали T-NS01**» чтобы продолжить.

---

## 2026-06-22 — Дизайн-фаза закрыта, решения приняты

**Сессия:** «не кодим. только документация. проводим глубокое исследование с поиском в интернете решений на тему мирных нпс»
**Профиль:** project-c
**Статус:** ✅ Все 7 файлов готовы. 13 решений приняты. Готовы к коду (T-NS00)

### Созданные файлы (8 docs)

| # | Файл | Слов |
|---|------|------|
| 00 | `00_README.md` | ~700 |
| 01 | `01_REUSE_MAP.md` | ~1100 |
| 02 | `02_INDUSTRY_PATTERNS.md` | ~1700 |
| 03 | `03_V2_ARCHITECTURE.md` | ~1900 |
| 04 | `04_LIVING_BEHAVIOR.md` | ~1600 |
| 05 | `05_ROADMAP.md` | ~1400 |
| 06 | `06_OPEN_QUESTIONS.md` (→ Final Decisions) | ~700 |
| — | **CHANGELOG.md** | — |
| **Всего** | | **~9100 слов** |

### Процесс

1. ✅ Phase A — собрал собственный контекст (DockingWorld, ShipController, DockingServer, etc.).
2. ✅ Phase B — 3 параллельных сабагента:
   - `pc_ship_REUSE_MAP.md` — Reuse аудит (32 KB) ✓
   - `pc_ship_INTEGRATION_TOUCHPOINTS.md` — Integration architecture (62 KB) ✓
   - `pc_ship_WEB_RESEARCH.md` — **не запустился** (HTTP 404 API). Заменён на индустриальный анализ по моему знанию.
3. ✅ Phase C — синтезировал 7 файлов в `docs/NPC_others_peacfull/pc_ship/`
4. ✅ Phase D — пользователь ответил на 13 вопросов → распространены по докам

### Принятые решения (TL;DR)

| # | Решение |
|---|---------|
| Q1 | Новый `ShipController.ApplyServerInput()` public method + v2 hook для player autopilot |
| Q2 | Явный `_hasNpcPilot` flag (server-only, enable/disable API) |
| Q3 | `NpcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL` |
| Q4 | NPC не регистрируется в `ShipOwnershipRegistry` |
| Q5 | `Loading` state 30-90 сек (визуальный интерес) |
| Q6 | NPC учитывают `maxConcurrentLandings` |
| Q7 | Single station per location в M1 (multi в v2) |
| Q8 | Anti-gravity override 5 сек после ExitDocked |
| Q9 | Без rate limiting для NPC (FSM достаточно) |
| Q10 | `NpcShipCargoManifest` struct пустой в M1 |
| Q11 | **4 NPC** для теста (расширим позже) |
| Q12 | **Примум + ещё 1 зона вблизи** (мини-тест в одной сцене) |
| Q13 | NPC стартуют `Docked` на pad при старте |

### Что не делалось

- ❌ Никакого кода не написано (только документация)
- ❌ Никаких изменений в существующих подсистемах (Docking, Ship, Trade)
- ❌ Никаких `.meta` / `.asmdef` файлов не создано
- ❌ Unity не запускался, MCP не использовался
- ❌ Git-коммитов не делалось (user коммитит сам)

### Что нужно проверить перед T-NS00

1. ✅ Все 7 файлов существуют в `docs/NPC_others_peacfull/pc_ship/`
2. ✅ `06_OPEN_QUESTIONS.md` → обновлён до `Final Decisions` формата
3. ⏳ Пользователь подтверждает — можно начинать T-NS00 (Core POCOs + ShipController.ApplyServerInput)

### Следующая сессия

- Кодинг T-NS00: `NpcShipState` + `NpcShipRoute` + `NpcShipCargoManifest` (Core POCOs)
- Ожидаемый объём: ~150 LOC, 60 мин coding + verify
- Acceptance: compile 0 errors, все struct INetworkSerializable работают