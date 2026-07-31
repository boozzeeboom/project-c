# T-U02: Node ↔ SO binding

**Дата:** 2026-07-22
**Файлы:** `QuestGraphNode.cs` (новый), `QuestNodeGraphView.cs` (изменён)
**Слой:** 0 — Фундамент
**Зависит от:** T-U01

## Что сделано

### Новый `QuestGraphNode : Node`
Файл: `Assets/_Project/Quests/Editor/QuestGraphNode.cs`
- `OwnerAsset` (ScriptableObject) — какой SO владеет данными ноды
- `SourcePath` (string) — путь внутри SO: `"stages[0]"`, `"stages[0].objectives[1]"`
- `SourceData` (object) — прямой ref на POCO (QuestStage, QuestObjective, ...)
- `NodeKind` (QuestNodeKind enum) — семантический тип: QuestRoot, Stage, Objective, Reward, Dialog, Condition
- `PersistKey` (string) — уникальный ключ для сохранения позиций

### Инкрементальные CRUD вместо полного rebuild
**Было:** `AddStage()` → мутирует SO → `LoadQuest(Quest)` → `ClearAllElements` + полный `BuildGraph`
**Стало:** `AddStage()` → мутирует SO → добавляет **одну** ноду `AddElement(sn)` → `MarkDirtyRepaint()`

Аналогично для `DeleteStage`, `AddObjective`, `DeleteObjective` — ни один не вызывает `LoadQuest`.

### MakeEditableNode → QuestGraphNode
Все ноды в `BuildGraph` теперь `QuestGraphNode` с заполненными полями OwnerAsset, SourcePath, SourceData, NodeKind, PersistKey.

### _nodeCounter
Счётчик для уникальных PersistKey, сбрасывается в `ClearAllElements`.

## Что осталось на T-U03
- `schedule.Execute` в `LoadQuest` пока остаются (BuildGraph всё ещё полный)
- Авто-лейаут заменит фиксированные позиции

## Следующий тикет
**T-U03**: Авто-лейаут (BFS от корня)
