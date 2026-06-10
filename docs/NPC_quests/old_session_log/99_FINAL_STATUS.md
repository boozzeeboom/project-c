# Project C: The Clouds — NPC Quests v2 — Итоговый статус (2026-06-09)

> Все **19 Milestones** закрыты. Система квестов готова для создания контента.

---

## 1. Что построено

### Ядро (сервер + клиент)
| Компонент | Что делает | 
|-----------|-----------|
| `QuestWorld.cs` | Server-side quest engine: stages, objectives, actions, rewards |
| `QuestServer.cs` | Network RPCs, snapshots, event routing (NPC → Client) |
| `QuestClientState.cs` | Client-side state: active, completed, discovered quests |
| `QuestTriggerService.cs` | Auto-discover via trigger zones |

### Данные (ScriptableObject)
| Asset | Format | 
|-------|--------|
| `QuestDefinition` | Quest + stages + objectives + rewards |
| `QuestDatabase` | Auto-discovered registry of all quests |
| `QuestStage` | Per-stage: objectives + onEnter/onComplete actions |
| `QuestObjective` | 8 types (HaveItem, TalkToNpc, StandOnTrigger...) |
| `DialogueAction` | 12 types (GiveCredits, AddReputation, GiveItem...) |
| `NpcDefinition` | NPC data, quest offers, turn-ins |
| `FactionDefinition` | Faction data, reputation tiers |
| `ItemRegistry` | Single source of truth for item IDs |

### UI (игрок)
| Окно | Что видит игрок |
|------|----------------|
| `DialogWindow` | Диалог с NPC (с typewriter, F-skip) |
| `CharacterWindow > КВЕСТЫ` | Лог: Discovered / Active / Completed |
| `QuestTracker` | HUD overlay с отслеживанием цели |
| `QuestToast` | Уведомления: "💰 +200 CR", "📜 Accepted", "✨ Найден квест" |

### Editor Tools (для разработчиков)
| Инструмент | Меню | 
|-----------|------|
| `QuestDatabaseWindow` | `Tools > ProjectC > Quests > Quest Database Explorer` |
| `QuestNodeGraph` (readonly) | `Tools > ProjectC > Quests > Quest Node Graph` |
| **`QuestNodeGraph` (editable)** | + ✏️ Edit, 💾 Save, +Add Stage, ×Delete, drag-edges |
| **`QuestCsvWindow`** | `Tools > ProjectC > Quests > CSV Import/Export` |

### CSV Pipeline (для content writer'ов)
- **1 файл, 1 таблица** — Excel/CSV
- **18 колонок** (только 4 обязательных)
- **Импорт** → QuestDefinition.asset + QuestDatabase
- **Экспорт** → round-trip compatible
- **Граф работает** сразу после импорта

---

## 2. Milestone progress

```
M1  ✅ Foundation (SO, structs, enums)
M2  ✅ Server core (RPCs, DTOs, event bus)
M3  ✅ Player interaction (E → NPC → dialog)
M4  ✅ Quest log + tracker (P-таб, HUD)
M5  ✅ Reputation + NpcAttitude
M6  ✅ Item integration (give/take items)
M7  ✅ Full action set (credits, rep, market)
M8  ✅ Persistence (JSON save/load)
M9  ✅ Cleanup (v1 NPC removed)
M10 ✅ Editor tool (QuestDatabaseWindow)
M11 ✅ Mira end-to-end demo
M12 ⏸️ DEFERRED (F = pickup input remap)
M13 ✅ Real-time objectives (T-Q20..T-Q22)
M14 ✅ Item ID system (ItemRegistry)
M15 ✅ Toast notifications
M16 ✅ QuestDatabaseWindow
M17 ✅ QuestNodeGraph + QuestGraphView
M18 ✅ Editable nodes + CRUD + dependencies
M19 ✅ CSV Import/Export pipeline
```
**19/19 closed.** 🎉

---

## 3. Backlog (post-MVP)

| Задача | Приоритет | ~ч |
|--------|-----------|----|
| M12 — Input remap (F = pickup) | 🟡 Medium | 1 |
| M17 polish — edges always visible | 🟢 Low | 1 |
| Quest content creation (actual quests!) | 🔴 High | ∞ |
| Localization | 🟢 Low | 3 |
| M11 NPC quest (Mira) non-functional test | 🟡 Medium | 1 |

---

## 4. Текущая кодовая база

| Категория | Строк кода (приблизительно) |
|-----------|---------------------------|
| Core quest scripts (server + client) | ~2500 |
| Editor tools (7 window/editor files) | ~2500 |
| DTOs + serialization | ~500 |
| CSV pipeline (4 files) | ~1400 |
| UI (dialog, tracker, toast, character) | ~1500 |
| **Total** | **~8400** |

---

## 5. Квесты в базе данных (5)

| questId | displayName | Stages | Objectives |
|---------|-------------|--------|------------|
| `collect_copper_ore` | Собрать 3 медных руды | 1 | HaveItem |
| `find_artifact` | Найти артефакт (EventDriven) | 2 | EventDriven |
| `stage_intro_demo` | Демо: stage с onEnter (CSV-imported) | 1 | TalkToNpc |
| `stage_multi_demo` | Тест: multi-stage (CSV-imported) | 2 | HaveItem + TalkToNpc |
| `collect_copper` | Собрать 3 медных руды (CSV-imported) | 1 | HaveItem |

---

## 6. Документация (docs/)

| Файл | Что |
|------|-----|
| `08_ROADMAP.md` | **Главный roadmap** — прогресс, план, все milestones |
| `dev/M19_CSV_PIPELINE_v2.md` | CSV pipeline: single-file flat format, spec, FAQ для writer'a |
| `dev/M18_DESIGN_NOTE.md` | Editable GraphView: T-Q30..T-Q34 |
| `dev/M17_DESIGN_NOTE.md` | QuestNodeGraph (readonly) |
| `dev/M16_DESIGN_NOTE.md` | QuestDatabaseWindow |
| `dev/M15_DESIGN_NOTE.md` | Toast system |
| `dev/M14_DESIGN_NOTE.md` | ItemRegistry |
| `dev/M13_DESIGN_NOTE.md` | Real-time objectives |
| `dev/T-Q22_DESIGN_NOTE.md` | Multi-stage + onEnter/onComplete |
| И 10+ других | M11, T-Q11, analysis notes... |
