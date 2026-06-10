# M19 — Quest Content Pipeline: Single-File CSV for Writers

> **Дата:** 2026-06-09
> **Статус:** 📋 DESIGN (v2)
> **Предыдущая версия:** `old_session_log/M19_CSV_PIPELINE.md` (5 файлов) — **ЗАМЕНЕНА**
> **Цель:** 1 файл, 1 таблица, понятно нетехнарю

---

## 1. Проблема (revisited)

5 CSV файлов — слишком сложно для content writer'а.

**Новое требование:**
- **1 файл, 1 таблица** (Excel sheet / CSV)
- Writer открывает Excel → заполняет колонки → сохраняет как CSV → даёт dev'у
- Не нужно разбираться в связях, внешних ключах, нормализации

---

## 2. Формат: Flat CSV (1 строка = 1 objective)

### 2.1 Пример (collect_copper_ore — 1 stage, 1 objective)

```csv
questId,displayName,description,faction,oneShot,prereqQuest,stageNum,stageId,onEnterActions,objectiveType,objectiveId,itemName,npcId,qty,onCompleteActions,rewardCR,rewardRep
collect_copper,Собрать 3 медных руды,Соберите 3 куска медной руды,,n,stage_intro_demo,0,collect,["AddNpcAttitude:mira_01:5"],HaveItem,gather_copper,Медная руда,,3,,200,GuildOfThoughts+25
```

**Каждая строка = один objective.** Если в stage 3 objectives → 3 строки с одинаковым stageNum/questId.

### 2.2 Multi-stage пример (stage_multi_demo — 2 stages, 1 objective each)

```csv
questId,displayName,description,faction,oneShot,prereqQuest,stageNum,stageId,onEnterActions,objectiveType,objectiveId,itemName,npcId,qty,onCompleteActions,rewardCR,rewardRep
stage_multi,Тест multi-stage,Тест на 2 этапа,,n,,0,collect,,HaveItem,collect_item,TestStageItem,,1,[],20,
stage_multi,Тест multi-stage,Тест на 2 этапа,,n,,1,deliver,[],TalkToNpc,talk_mira,,mira_01,1,,50,
```

**Stage 0 → Stage 1:** неявная связь по `stageNum` (0→1→2...). Тикет "turnIn" не нужен — последний stage = complete.

### 2.3 Prerequisite quests

```csv
questId,displayName,prereqQuest
collect_copper,Собрать руду,stage_intro_demo
```

`prereqQuest` = questId который должен быть Completed. Можно несколько через `;`.

### 2.4 Actions (onEnter/onComplete)

Формат одной action: `Type:stringParam:intParam:factionParam`
Несколько actions разделяются `;`

Пример: `GiveCredits::200;AddReputation::25:GuildOfThoughts`

Типы: `GiveCredits`, `AddReputation`, `AddNpcAttitude`, `GiveItem`, `TakeItem`, `CompleteObjective`

---

## 3. Спецификация колонок

| Колонка | Обязательно | Формат | Пример |
|----------|-------------|--------|--------|
| questId | да | латиница, без пробелов | `collect_copper` |
| displayName | да | любой текст | `Собрать 3 медных руды` |
| description | нет | текст | `Соберите руду в шахте...` |
| faction | нет | FactionId | `GuildOfThoughts` |
| oneShot | нет | y/n | `y` |
| prereqQuest | нет | questId(;questId) | `stage_intro_demo` |
| stageNum | да | int (0-based) | `0` |
| stageId | да | id этапа | `collect` |
| stageDescription | нет | текст | `Добудьте руду` |
| onEnterActions | нет | `Type:p1:p2:p3(;...)` | `AddNpcAttitude:mira_01:5` |
| objectiveType | да | enum | `HaveItem` / `TalkToNpc` |
| objectiveId | да | id цели | `gather_copper` |
| itemName | для HaveItem | string | `Медная руда` |
| npcId | для TalkToNpc | string | `mira_01` |
| qty | да | int | `3` |
| onCompleteActions | нет | `Type:p1:p2:p3(;...)` | `GiveCredits::200` |
| rewardCR | нет | int | `200` |
| rewardRep | нет | `FactionId:value` | `GuildOfThoughts:25` |
| rewardItems | нет | `itemName:count(;...)` | `Медная руда:1` |

---

## 4. Импорт: CSV → QuestDefinition.asset

### 4.1 Шаги

1. **Reader** парсит CSV (UTF-8 with BOM) → список строк
2. **Group by questId** → каждая группа = один QuestDefinition
3. **Group by stageNum внутри quest** → каждая подгруппа = один QuestStage
4. **Каждая строка** → один QuestObjective
5. **onEnterActions** из первой строки stage'a (остальные игнорируются)
6. **onCompleteActions** из последней строки stage'a
7. **rewardCR/Rep/Items** из последней строки quest'a
8. **prereqQuest** из первой строки quest'a
9. **Item name lookup** через QuestWorld.ResolveItemId(name)
10. Создание/обновление QuestDefinition.asset
11. QuestDatabase.Rescan()

### 4.2 Валидация

- questId не пустой — ✗
- questId уникален (в CSV) — ✗
- stageNum последовательный (0, 1, 2...) — предупреждение
- objectType корректен — ✗
- itemName найден в ItemRegistry — предупреждение
- npcId найден в NpcDefinition — предупреждение
- qty > 0 — ✗

---

## 5. Пример реального CSV (3 квеста)

```csv
questId,displayName,description,faction,oneShot,prereqQuest,stageNum,stageId,stageDescription,onEnterActions,objectiveType,objectiveId,itemName,npcId,qty,onCompleteActions,rewardCR,rewardRep
stage_intro_demo,Демо: stage,Тестовый для onEnter/onComplete,,n,,0,intro,Поговорить с Мирой,AddNpcAttitude:mira_01:5,TalkToNpc,talk_mira,,mira_01,1,GiveCredits::10,,
collect_copper,Собрать 3 руды,Соберите медную руду,,n,stage_intro_demo,0,collect,Добыть руду,,HaveItem,gather_copper,Медная руда,,3,,200,GuildOfThoughts:25
stage_multi,Тест: multi,Тест на 2 stage,,n,,0,collect,Собрать предмет,,HaveItem,collect_item,TestStageItem,,1,GiveCredits::20,20,
stage_multi,Тест: multi,Тест на 2 stage,,n,,1,deliver,Сдать квест,GiveItem:TestStageItem:1,TalkToNpc,talk_mira,,mira_01,1,GiveCredits::50,50,
```

**Этот файл создаёт 3 квеста**, идентичных тем что мы тестировали в T-Q22.

---

## 6. Рабочий процесс writer'a

```
Writer открывает Google Sheets / Excel
         │
         ▼
Заполняет колонки (первые 3 обязательны, остальные по желанию)
         │
         ▼
Сохраняет как CSV (UTF-8)
         │
         ▼
Dev: Open Unity → Tools → ProjectC → Import Quest CSV
         │
         ▼
Выбирает CSV файл → Preview → Import
         │
         ▼
QuestDefinition.asset создан + QuestDatabase обновлён + Graph работает
```

**Writer НЕ ВИДИТ:**
- Unity Editor
- ScriptableObject
- AssetDatabase
- C# код
- QuestNodeGraph (но dev видит)

---

## 7. Тикеты (обновлены)

| Тикет | Что | ~ч | Статус |
|-------|-----|----|--------|
| **M19-T1** | FlatCsvSchema + QuestCsvParser (one-file, validation) | 1.5 | ⏳ |
| **M19-T2** | QuestCsvImporter (CSV → QuestDefinition.asset) | 2.0 | ⏳ |
| **M19-T3** | QuestCsvExporter (SO → flat CSV) | 1.0 | ⏳ |
| **M19-T4** | EditorWindow: Upload CSV + Preview + Import button | 1.0 | ⏳ |
| **M19-T5** | Integration test + sample CSV для writer'a | 0.5 | ⏳ |

**Total:** ~6 ч

---

## 8. Файлы

```
Assets/_Project/Quests/Editor/QuestCsvImporter.cs   — window + импорт
Assets/_Project/Quests/Editor/QuestCsvSchema.cs     — парсер + структура
Assets/_Project/Quests/Editor/QuestCsvExporter.cs   — экспорт
Assets/_Project/Quests/Import/example_quests.csv    — пример для writer'a
M19_CSV_PIPELINE_v2.md                     — этот документ (ЗАМЕНЯЕТ v1)
```

---

## 9. Verify

- [ ] Writer создаёт CSV с 1 квестом (1 objective) → импорт → квест в игре
- [ ] Writer создаёт CSV с 3 квестами → все 3 импортированы
- [ ] Редактирование CSV → re-import → asset обновлён (не дублирован)
- [ ] Экспорт существующего квеста → CSV → совпадает с форматом
- [ ] QuestNodeGraph → Show All → новые квесты отображаются
- [ ] Play Mode: квест проходится

---

## 10. FAQ для writer'a (встроить в документацию)

**Q: Что нужно установить?**
A: Ничего. Откройте Google Таблицы или Excel.

**Q: Какие колонки обязательны?**
A: `questId`, `displayName`, `stageNum`, `objectiveType`, `qty`. Остальные — по желанию.

**Q: Что такое questId?**
A: Уникальное имя квеста латиницей, например `find_treasure`. Без пробелов.

**Q: Как сделать квест с 2 этапами?**
A: 2 строки с одинаковым questId, разным stageNum (0 и 1).

**Q: Как указать предмет?**
A: В колонке `itemName` напишите точное название предмета из игры, например `Медная руда`.

**Q: Как дать награду?**
A: Колонки `rewardCR` (число), `rewardRep` (например `GuildOfThoughts:25`).

**Q: Как сделать квест доступным только после другого?**
A: В колонке `prereqQuest` укажите questId предыдущего квеста.

---

## 11. Изменения в roadmap

- `08_ROADMAP.md` §8.3.7 — обновлён с single-file подходом
- `old_session_log/M19_CSV_PIPELINE.md` (v1, 5 файлов) → **ЗАМЕНЁН** на этот документ
