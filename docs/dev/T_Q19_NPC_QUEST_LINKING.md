# T-Q19: Полная база NPC + Quests + Dialogs через CSV

> **Дата:** 2026-06-09
> **Статус:** 📋 DESIGN + T-Q19.1 IN PROGRESS
> **Цель:** Одной базой создать полный набор NPC со всеми связями на квесты

---

## 1. Анализ NpcDefinition (поля)

### Прямые с квестами
| Поле | CSV источник | Статус |
|------|-------------|--------|
| `questOffers[]` | stage 0 + TalkToNpc objective | ✅ auto |
| `questTurnIns[]` | stage final + TalkToNpc objective | ❌ нужно правило |
| `defaultDialogTree` | matching `treeId` in dialogs.csv | ❌ auto-link |
| `npcId` | csv column | ✅ |
| `displayName` | csv column | ✅ |
| `faction` | csv column | ✅ |

### Косвенные (опционально через npcs.csv)
| Поле | Назначение | CSV формат |
|------|-----------|-----------|
| `services` | битовая маска (Market, Repair, Refuel, etc) | `Market,Repair` |
| `attitudeLinks` | cross-faction influence | `Pirates:-10;GuildOfSuccess:5` |
| `personalAttitudeMin/Max` | range attitude | `0:100` |
| `greetingText` | текст при подходе | `"Здравствуй, путник!"` |
| `voicePrefix` | audio | `"npc_002_"` |
| `interactionRadius` | UI | `3` |

### Asset refs (НЕ из CSV)
| Поле | Источник |
|------|---------|
| `portrait` | Unity Editor (drag-drop) |
| `prefab` | Unity Editor (drag-drop) |
| `animatorTriggerPrefix` | Unity Editor (для VFX) |

---

## 2. Структура файлов

```
Assets/_Project/Quests/Import/
├── quests_bd_v1.csv      ← основная база (796 квестов, 5050 строк)
├── npcs_bd_v1.csv        ← опционально: детали NPC (для тех кто нуждается)
└── dialogs_bd_v1.csv     ← опционально: кастомные диалоги
```

**`quests_bd_v1.csv`** auto-генерирует:
- `questOffers` (из stage 0 + TalkToNpc)
- `questTurnIns` (из последнего stage + TalkToNpc)

**`npcs_bd_v1.csv`** опционально для дополнительных полей (services, attitude, greeting).

**`dialogs_bd_v1.csv`** опционально для кастомных реплик (auto-attach по `treeId == npcId + "_default"`).

---

## 3. Auto-generation questTurnIns

**Правило:** для каждого квеста последний stage + TalkToNpc objective = quest turn-in NPC.

```python
# Pseudo:
for quest in all_quests:
    last_stage = max(quest.stages, key=lambda s: s.stageNum)
    for obj in last_stage.objectives:
        if obj.type == TalkToNpc and obj.npcId:
            add_to_npc_turnins[obj.npcId].append(quest.questId)
```

**Edge case:** `q_002_0` имеет последний stage = `talk_0_9` с TalkToNpc → npc_002. Значит npc_002 имеет в turnIns `q_002_0`.

---

## 4. План имплементации

| Тикет | Что | ~ч |
|-------|-----|----|
| **T-Q19.1** | Auto-questTurnIns (последний stage + TalkToNpc) | 0.3 |
| **T-Q19.2** | Auto-link `defaultDialogTree` если существует в dialogs.csv | 0.3 |
| **T-Q19.3** | Новый `npcs.csv` schema + парсер | 1.0 |
| **T-Q19.4** | UI: 3 checkbox'а — Quests / NPCs / Dialogs | 0.5 |
| **T-Q19.5** | Sample npcs_bd_v1.csv (с services, attitude, greeting) | 0.3 |

**Total:** ~2.5 ч

---

## 5. Пример `npcs_bd_v1.csv` (опционально)

```csv
npcId,displayName,faction,services,attitudeLinks,attitudeMin,attitudeMax,greetingText,voicePrefix,interactionRadius
npc_002,Мистер Фринли,GuildOfSuccess,Market,"Pirates:-15",0,100,Приветствую, путник!,"npc_002_",3
npc_010,Мистер Генри Эстик,GuildOfThoughts,Market;Repair,,"-50","200",Здравствуй, друг.,"npc_010_",3
npc_019,Роксана Розенталь,GuildOfSuccess,Repair;Refuel,Underground:10,0,150,Чем могу помочь?,"npc_019_",2.5
```

**Где:**
- `services`: битовая маска, `;` разделитель
- `attitudeLinks`: factionId:delta;factionId:delta
- `attitudeMin/Max`: `-100..200`
- `greetingText`: `"...кавычки для запятых..."`

---

## 6. Verify план

- [ ] Auto-questTurnIns: npc_002 turnIns = [q_002_0, q_002_1, q_002_2, q_002_3, q_002_4]
- [ ] Auto-defaultDialogTree: если есть `npc_002_default.asset`, устанавливается как `defaultDialogTree`
- [ ] npcs_bd_v1.csv: для npc_002 заполняются services, attitude, greeting
- [ ] QuestDatabaseWindow: NPC → questOffers + questTurnIns видны
