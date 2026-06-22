# CHANGELOG — Peaceful NPC Ships

> Лог изменений каталога `docs/NPC_others_peacfull/pc_ship/`.

---

## 2026-06-22 — Дизайн-фаза закрыта, решения приняты

**Сессия:** «не кодим. только документация. проводим глубокое исследование с поиском в интернете решений на тему мирных нпс»
**Профиль:** project-c
**Статус:** ✅ Все 7 файлов готовы. 13 решений приняты. Готовы к коду (T-NS00)

### Созданные файлы

| # | Файл | Слов |
|---|------|------|
| 00 | `00_README.md` | ~700 |
| 01 | `01_REUSE_MAP.md` | ~800 |
| 02 | `02_INDUSTRY_PATTERNS.md` | ~1300 |
| 03 | `03_V2_ARCHITECTURE.md` | ~1700 |
| 04 | `04_LIVING_BEHAVIOR.md` | ~1300 |
| 05 | `05_ROADMAP.md` | ~1100 |
| 06 | `06_OPEN_QUESTIONS.md` (→ Final Decisions) | ~1000 |
| — | **Всего** | **~7900** |

### Процесс

1. ✅ Phase A — собрал собственный контекст (читаю DockingWorld, ShipController, DockingServer, DockStationController, DockPadLayout, DockPadVisualMarker, DockingPadTriggerBox, ScenePlacedObjectSpawner, ShipOwnershipRegistry, CargoData).
2. ✅ Phase B — 3 параллельных сабагента:
   - `pc_ship_REUSE_MAP.md` — Reuse аудит (32 KB)
   - `pc_ship_INTEGRATION_TOUCHPOINTS.md` — Integration architecture (62 KB)
   - `pc_ship_WEB_RESEARCH.md` — **не запустился** (HTTP 404 API). Заменён на индустриальный анализ по моему знанию.
3. ✅ Phase C — синтезировал 7 файлов в `docs/NPC_others_peacfull/pc_ship/`
4. ✅ Phase D — пользователь ответил на 13 вопросов → распространены по докам

### Принятые решения (TL;DR)

| # | Решение |
|---|---------|
| Q1 | Новый `ShipController.ApplyServerInput()` public method + v2 hook для player autopilot |
| Q2 | Явный `_hasNpcPilot` flag (server-only) |
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
| Q13 | NPC стартуют `Docked` на pad |

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