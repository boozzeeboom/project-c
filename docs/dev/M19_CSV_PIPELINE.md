# M19 — CSV Import/Export Pipeline for Quest Content

> **Дата:** 2026-06-09
> **Статус:** 📋 DESIGN
> **Цель:** Позволить content writer'ам (без Unity опыта) создавать и редактировать квесты через Excel/CSV, импортировать в проект, экспортировать обратно.
> **Принцип:** Не ломать существующее — импорт создаёт QuestDefinition.asset, который полностью совместим с QuestDatabase, QuestNodeGraph, и всем рантаймом.

---

## 1. Проблема

**Сейчас:** чтобы создать квест, нужно:
1. `CreateAssetMenu → QuestDefinition.asset`
2. Заполнить 20+ полей через Inspector
3. Создать QuestStage[] и заполнить каждое поле
4. Создать QuestObjective[] внутри каждого stage
5. Настроить DialogueAction[] (onEnter/onComplete)
6. Настроить QuestReward (credits/items/reputation)
7. Вручную ре-сканировать QuestDatabase через Tools → Re-scan

**Для content writer'а это невозможно.** Нужен читаемый формат (Excel/CSV) с человеческими названиями полей и предсказуемой структурой.

---

## 2. Формат: Multi-file CSV

Одна директория импорта содержит 5 CSV файлов (связаны через questId):

### 2.1 quests.csv — заголовок квеста

```csv
questId,displayName,description,faction,oneShot,discoverable,minReputation,prereqQuestId
find_lost_relic,Потерянный артефакт,Найдите древний артефакт в руинах,Neutral,FALSE,TRUE,0,find_artifact
gather_supplies,Сбор припасов,Соберите 10 консервов и 5 бутылей воды,GuildOfThoughts,FALSE,TRUE,50,
```

| Поле | Описание | Тип |
|------|----------|-----|
| questId | Уникальный ID (латиница) | string |
| displayName | Отображаемое имя | string |
| description | Описание квеста | string/text |
| faction | FactionId (enum) | string |
| oneShot | Можно выполнить 1 раз | TRUE/FALSE |
| discoverable | Авто-обнаружение | TRUE/FALSE |
| minReputation | Минимальная репутация | int |
| prereqQuestId | prerequisite questId | string (опционально) |

### 2.2 stages.csv — этапы квеста

```csv
questId,stageIndex,stageId,description,nextStageId,onEnterActions,onCompleteActions
find_lost_relic,0,search,Исследуйте руины,return,,"GiveCredits:10"
find_lost_relic,1,return,Вернитесь к NPC,,,"CompleteObjective:turn_in_quest"
```

Каждый action кодируется строкой: `Type:stringParam:intParam:factionParam`
- `GiveCredits:10` → GiveCredits(10)
- `AddReputation:25:GuildOfThoughts` → AddReputation(GuildOfThoughts, +25)
- `AddNpcAttitude:5:mira_01` → AddNpcAttitude(mira_01, +5)

### 2.3 objectives.csv — цели квеста

```csv
questId,stageIndex,objectiveIndex,objectiveType,objectiveId,itemTradeItemId,targetNpcId,requiredQuantity
gather_supplies,0,0,HaveItem,collect_food,Консервы,,10
gather_supplies,0,1,HaveItem,collect_water,Бутыль воды,,5
find_lost_relic,0,0,TalkToNpc,talk_mira,,mira_01,1
```

| Поле | Описание | Пример |
|------|----------|--------|
| objectiveType | HaveItem / TalkToNpc / StandOnTrigger / etc. | HaveItem |
| itemTradeItemId | ID предмета (или itemId int) | "Консервы" или "26" |
| targetNpcId | ID NPC | mira_01 |

### 2.4 actions.csv — все действия (подробно)

Если краткого формата в stages.csv недостаточно, можно использовать отдельный файл:

```csv
questId,stageIndex,actionPlacement,actionType,stringParam,intParam,factionParam,itemType
find_lost_relic,0,onComplete,GiveCredits,,200,,
find_lost_relic,0,onComplete,AddReputation,,25,GuildOfThoughts,
```

| Поле | Описание |
|------|----------|
| actionPlacement | "onEnter" или "onComplete" |
| actionType | GiveCredits / AddReputation / AddNpcAttitude / GiveItem / TakeItem / OfferQuest / AcceptQuest / etc. |
| stringParam | questId / itemId / npcId / eventId |
| intParam | количество / delta |
| factionParam | FactionId (для AddReputation) |
| itemType | Resources / Cargo / Equipment (для GiveItem) |

### 2.5 rewards.csv — награды

```csv
questId,credits,itemId,itemCount,repFaction,repValue
find_lost_relic,100,26,1,GuildOfThoughts,10
```

---

## 3. Архитектура импорта

```
CSV files (в папке Import/)
    │
    ▼
QuestCsvImporter.cs (EditorWindow)
    │  ┌──────────────────────────────┐
    │  │ 1. Parse CSV → QuestCsvData  │
    │  │ 2. Validate (questId unique, │
    │  │    ref integrity)            │
    │  │ 3. Create/Update             │
    │  │    QuestDefinition.asset     │
    │  │ 4. Auto-compute:             │
    │  │    • ResolveItemId(name→int) │
    │  │    • Auto-link stages        │
    │  │ 5. SetDirty + SaveAssets      │
    │  │ 6. Trigger AutoDiscover       │
    │  └──────────────────────────────┘
    │
    ▼
QuestDefinition.asset (уже существующий SO)
    │
    ▼
QuestDatabase (auto-updated)
    │
    ▼
QuestNodeGraph (граф работает сразу)
```

### Классы

**QuestCsvImporter** (EditorWindow):
- `ImportFromFolder(string path)` — читает все .csv файлы в папке
- `ExportToFolder(string path, QuestDefinition[] quests)` — пишет .csv

**QuestCsvSchema** (struct):
- Определяет имена колонок, форматы, валидацию

**QuestCsvConverter**:
- `CsvRow ToQuestDef(CsvRow questRow)` — создаёт SO
- `QuestDef ToCsvRow(QuestDefinition quest)` — экспорт

---

## 4. Принципы

1. **Additive:** импорт НЕ удаляет существующие квесты. Только create/update.
2. **Idempotent:** повторный импорт тех же данных не создаёт дубликатов (ключ — questId).
3. **Validation:** перед импортом проверка:
   - Все questId уникальны
   - Все ссылки (itemTradeItemId → ItemRegistry, targetNpcId → NpcDefinition) валидны
   - Ошибки пишутся в лог, импорт не прерывается
4. **QuestDatabase:** после импорта автоматически `Rescan()`.
5. **Граф:** после Rescan'а QuestNodeGraph при следующей загрузке отображает новые квесты.
6. **Никакого хардкода:** все поля маппятся на существующие DialogueActionType/DTO/SO.

---

## 5. Тикеты

| Тикет | Что | ~ч |
|-------|-----|----|
| **M19-T1** | QuestCsvSchema + CsvParser | 1.5 |
| **M19-T2** | QuestCsvImporter (create SO из CSV) + validation | 2.0 |
| **M19-T3** | QuestCsvExporter (SO → CSV) | 1.0 |
| **M19-T4** | EditorWindow: Import/Export UI + progress bar | 1.0 |
| **M19-T5** | Integration test: экспорт → редактир → импорт → граф | 0.5 |

**Total:** ~6 ч

---

## 6. Пример рабочего процесса

**Content writer (без Unity):**
1. Открывает Excel с шаблоном (5 листов: quests, stages, objectives, actions, rewards)
2. Заполняет: "Название квеста", "Описание", "Собрать 3 медных руды" и т.д.
3. Сохраняет как CSV (5 файлов) → кладёт в `Assets/_Project/Quests/Import/`
4. Dev: Открывает Unity → Tools → ProjectC → Import CSV
5. Выбирает папку → Preview → Import
6. Готово. Квест появляется в QuestDatabase, граф работает, Play Mode работает.

**Dev (редактирование существующего):**
1. Export selected quest(s) → CSV
2. Content writer правит в Excel
3. Import → asset обновлён

---

## 7. Риски

| Риск | Митигация |
|------|-----------|
| CSV encoding (кириллица) | Всегда UTF-8 with BOM. Указать в документации. |
| QuestId не уникален | Validation: "Duplicate questId: xxx". Skip + log error. |
| Item name не найден в ItemRegistry | Fallback: int.TryParse, затем name lookup. WARN если не найден. |
| Action type typo | case-insensitive match + error log с suggestion. |
| Сложные типы (DialogueAction.intParam) | Использовать CSV format: `actionType;stringParam;intParam` |
| Multi-line description | Excel: wrap text. CSV: escaped quotes. |

---

## 8. Файлы (будут созданы)

```
Assets/_Project/Quests/Editor/QuestCsvImporter.cs    — window
Assets/_Project/Quests/Editor/QuestCsvConverter.cs    — parse/convert
Assets/_Project/Quests/Import/                        — default import folder
  └── quests.csv (example)
  └── stages.csv (example)
  └── objectives.csv (example)
  └── actions.csv (example)
  └── rewards.csv (example)
docs/dev/M19_CSV_PIPELINE.md                          — этот документ
```

---

## 9. Verify

- ✅ Экспорт: select quest → Export → 5 CSV файлов с корректными данными
- ✅ Импорт: Import CSV → новый QuestDefinition.asset создан + QuestDatabase обновлён
- ✅ Граф: QuestNodeGraph → Show All → новый квест отображается
- ✅ Inspector: все поля (stages, objectives, rewards, actions) корректны
- ✅ Play Mode: квест проходится
- ✅ Re-import: изменённый CSV → asset обновлён (не дублируется)
