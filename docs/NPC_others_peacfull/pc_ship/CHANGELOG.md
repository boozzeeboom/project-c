# CHANGELOG — Peaceful NPC Ships

> Лог изменений каталога `docs/NPC_others_peacfull/pc_ship/`.

---

## 2026-06-22 — T-NS01: ShipController.ApplyServerInput + _hasNpcPilot + AntiGravity

**Сессия:** Реализация по 05_ROADMAP.md, тикет T-NS01
**Профиль:** project-c
**Статус:** ✅ Реализовано + compile-clean. Все API видны через reflection (verified через Unity MCP).

### Изменённые файлы (1)

| Файл | Изменения |
|------|-----------|
| `Assets/_Project/Scripts/Player/ShipController.cs` | + `AntiGravity` property (Q8), + `_hasNpcPilot` field (Q2), + `ApplyServerInput()` method (Q1), + `EnableNpcPilot()` method (Q2), изменён FixedUpdate gate |

**~50 LOC добавлено** в существующий файл (1769 → ~1785 строк).

### Что добавлено

**`AntiGravity` property (Q8, ~10 LOC)**
```csharp
public float AntiGravity
{
    get => antiGravity;
    set => antiGravity = Mathf.Clamp(value, 0f, 1.5f);
}
```
Публичный getter/setter — NpcShipController использует для boost 5 сек после ExitDocked.

**`_hasNpcPilot` field (Q2, ~3 LOC)**
```csharp
private bool _hasNpcPilot = false;
```
Сервер-only flag. Когда true — FixedUpdate применяет `_sumXxx` даже без игроков в `_pilots`.

**`ApplyServerInput()` (Q1, ~15 LOC)**
```csharp
public void ApplyServerInput(float thrust, float yaw, float pitch, float vertical, bool boost = false)
{
    if (!IsServer) return;
    if (_netIsDocked.Value) return;        // T-DOCK-09 defense
    if (_rb == null || _rb.isKinematic) return;  // safety

    _sumThrust += thrust;
    _sumYaw += yaw;
    _sumPitch += pitch;
    _sumVertical += vertical;
    if (boost) _boostCount++;
    _inputCount++;
}
```
**Generic API** — может быть использован v2 player autopilot (см. docs/.../03_V2_ARCHITECTURE.md §5).

**`EnableNpcPilot()` (Q2, ~6 LOC)**
```csharp
public void EnableNpcPilot(bool enable)
{
    if (!IsServer) return;
    _hasNpcPilot = enable;
}
```

**FixedUpdate gate изменён (1 строка, line 824):**
```csharp
// БЫЛО:
if (_pilots.Count == 0) return;
// СТАЛО:
if (_pilots.Count == 0 && !_hasNpcPilot) return;  // T-NS01 (Q2): NPC-pilot bypasses pilot gate
```

### Reflection verify (Unity MCP execute_code)

```csharp
var t = Type.GetType("ProjectC.Player.ShipController, Assembly-CSharp");
// ...
```

Результат:
```
Found: ProjectC.Player.ShipController
ApplyServerInput: Void ApplyServerInput(Single, Single, Single, Single, Boolean)
EnableNpcPilot: Void EnableNpcPilot(Boolean)
AntiGravity property: Single
_hasNpcPilot field: Boolean
```

**Все API скомпилированы и видны Roslyn.**

### Compile iterations

| # | Проблема | Решение |
|---|----------|---------|
| 1 | patch tool вставил блок с 16-space indent вместо 8 (mcp-quirks.md #26b) | Python `open().read() + slice` для нормализации отступов (15 строк исправлено) |

После фикса: **0 errors / 0 warnings** от ShipController (verified через `read_console filter_text=ShipController`).

### Применённые конвенции

- ✅ Server-only методы проверяют `IsServer` первой строкой
- ✅ XML `<summary>` doc comments на всех публичных API
- ✅ T-DOCK-09 defense (`_netIsDocked.Value`) сохранён
- ✅ Совместимость с существующим pilot-pipeline (`_sumThrust`, `_inputCount`)
- ✅ Generic API без hard-coded ссылок на NpcShipController (v2 autopilot может использовать)

### Что НЕ делалось

- ❌ Не менял SubmitShipInputRpc / SendShipInput (player-path остаётся)
- ❌ Не делал тестов (smoke test в T-NS10)
- ❌ Не делал git commit

### Что пользователь должен проверить

**Шаг 1: Compile clean** ✅ (verified через Unity MCP)
- `read_console filter_text=ShipController` → 0 entries

**Шаг 2: API доступны** ✅ (verified reflection)
- `ship.ApplyServerInput(0.5f, 0f, 0f, 0f)` — компилируется
- `ship.EnableNpcPilot(true)` — компилируется
- `ship.AntiGravity = 1.5f` — компилируется

**Шаг 3: Регрессия не сломана** (manual test)
1. Открыть Play Mode
2. Зайти в корабль (E на Trigger collider)
3. WASD → корабль движется (player-pilot работает)
4. Выйти из корабля → нет движения (без пилота)
5. **Без EnableNpcPilot = false** в обычной игре — поведение не должно отличаться от ранее

### Следующий тикет

**T-NS02:** `NpcShipSchedule` SO + `NpcShipController` scene-placed NetworkBehaviour + `NpcShipZoneRegistry`.
- Файлы: 3 новых в `Assets/_Project/Scripts/PeacefulShip/Stations/` и `Network/`
- LOC: ~250
- Время: ~60 мин coding + verify

Скажите «**поехали T-NS02**» чтобы продолжить.

---

## 2026-06-22 — T-NS00: Core POCOs (NpcShipState + NpcShipRoute + NpcShipCargoManifest)

**Сессия:** Реализация по 05_ROADMAP.md, тикет T-NS00
**Профиль:** project-c
**Статус:** ✅ Реализовано + compile-clean. Все 4 типа видны в Assembly-CSharp (verified через Unity MCP reflection probe).

### Созданные файлы (4)

| Файл | LOC | Назначение |
|------|-----|-----------|
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipStatus.cs` | ~25 | `public enum NpcShipStatus : byte` — 11 состояний FSM |
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipRoute.cs` | ~45 | `NpcShipRoute` struct + `NpcShipDemandCategory` enum |
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipCargoManifest.cs` | ~60 | `NpcShipCargoManifest` + `NpcCargoEntryDto` — INetworkSerializable (v2 hook, M1 empty) |
| `Assets/_Project/Scripts/PeacefulShip/Core/NpcShipState.cs` | ~45 | `NpcShipState` class (POCO, server-only) |

**Итого:** ~175 LOC, 4 файла.

### Compile iterations

| # | Проблема | Файл | Фикс |
|---|----------|------|------|
| 1 | `error CS0246: TooltipAttribute not found` | NpcShipRoute.cs:30-42 | Добавил `using UnityEngine;` |
| 2 | `error CS0246: ShipController not found` | NpcShipState.cs:19,39 | Добавил `using ProjectC.Player;` |

После 2 итераций: **0 errors / 0 warnings** от PeacefulShip (verified через `read_console filter_text=PeacefulShip`).

Pre-existing **не наши** ошибки (НЕ относить к T-NS00):
- `error CS0618 NetworkPlayer.cs:992` warning — старый код, не T-NS00 scope
- `Unity toolbar extension unsupported` warning — системное сообщение Unity 6, не наш код

### Reflection verify (Unity MCP execute_code)

```csharp
var asm = AppDomain.CurrentDomain.GetAssemblies();
foreach (var a in asm) {
    var t = a.GetType("ProjectC.PeacefulShip.Core.NpcShipState");
    if (t != null) { /* found */ }
}
```

Результат:
```
FOUND NpcShipState in Assembly-CSharp
  NpcShipStatus enum: True
  NpcShipRoute struct: True
  NpcShipCargoManifest: True
```

**Все 4 типа скомпилированы и видны Roslyn.**

### Что внутри

**`NpcShipStatus.cs`** — enum из 11 состояний (Idle, Departing, InTransit, Approaching, Holding, Diverting, Docking, Docked, Loading, Undocking, Done). См. `04_LIVING_BEHAVIOR.md §2`.

**`NpcShipRoute.cs`** — struct с `fromLocationId`, `toLocationId`, `dwellTimeSec`, `flightDurationSec`, `preferredShipClass` (Light/Medium/Heavy), `demandCategory`. `NpcShipDemandCategory` enum (Generic/HighDemand/LowDemand/Contract) для v2 market-driven routing. Все поля с `[Tooltip]`.

**`NpcShipCargoManifest.cs`** — INetworkSerializable struct. В M1 — `capacitySlots=0, capacityWeight=0, items=null`. Pattern `NetworkSerialize` с dynamic array length (как `DockingDto`).

**`NpcShipState.cs`** — POCO с `NpcInstanceId` (sentinel bit), `Ship` ref, `Status`, `CurrentRoute`, `StateEnteredAt`, `ScheduleIndex`, `LastKnownPosition`, `Cargo`. Конструктор инициализирует `Idle` state.

### Конвенции проекта (применены)

- ✅ Namespace `ProjectC.PeacefulShip.Core` (по `02_V2_ARCHITECTURE.md §1`)
- ✅ `public enum X : byte` (как `DockingStatus`, `SkillCategory`, etc.)
- ✅ Один class/enum = один .cs файл (Unity 6: T-DOCK-13c fix)
- ✅ `[System.Serializable]` на struct (для инспектора и INetworkSerializable)
- ✅ `INetworkSerializable` с NGO 2.x pattern (создание array на reader side)
- ✅ `using UnityEngine;` для `TooltipAttribute`
- ✅ `using ProjectC.Player;` для `ShipController`

### Что НЕ делалось

- ❌ Никаких изменений в существующих подсистемах (ShipController, DockingWorld)
- ❌ Никаких .meta файлов — Unity создал при refresh (5 файлов)
- ❌ Никаких тестов (не указано в M1 — smoke test в T-NS10)
- ❌ Никаких git-коммитов

### Что пользователь должен проверить

**Шаг 1: Файлы на диске** ✅ (verified)
```
Assets/_Project/Scripts/PeacefulShip/Core/
├── NpcShipStatus.cs (+ .meta)
├── NpcShipRoute.cs (+ .meta)
├── NpcShipCargoManifest.cs (+ .meta)
└── NpcShipState.cs (+ .meta)
```

**Шаг 2: Compile clean** ✅ (verified через Unity MCP)
- 0 errors / 0 warnings от PeacefulShip
- Типы видны в `Assembly-CSharp.dll`

**Шаг 3: Можно открыть в IDE** (опционально)
- Ctrl+Click на `NpcShipStatus` в любом файле проекта → откроется enum
- Или поиск `ProjectC.PeacefulShip.Core.NpcShipState` → 1 определение

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