# T-U01: Model-driven OnGraphViewChanged

**Дата:** 2026-07-22
**Файл:** `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs`
**Слой:** 0 — Фундамент

## Что сделано

### Убран `_suppressReadOnly`
Флаг использовался для обхода блокировок в `OnGraphViewChanged` при вызове `ClearAllElements`. Теперь `graphViewChanged` разрешает все мутации — флаг не нужен.

### Переписан `OnGraphViewChanged`
Было: блокировка удаления auto-edges, блокировка удаления нод, блокировка перемещения нод.
Стало: все мутации разрешены, каждая мутация вызывает соответствующий `protected virtual` хук:
- `OnEdgeCreated(Edge)` — при создании ребра пользователем
- `OnEdgeDeleted(Edge)` — при удалении ребра
- `OnNodeDeleted(Node)` — при удалении ноды
- `OnNodeMoved(Node, Rect)` — при перемещении ноды (сохраняет позицию в `_nodePositions`)

### Добавлен `_nodePositions` (Dictionary<string, Vector2>)
Сериализуемый словарь позиций нод. `OnNodeMoved` записывает позицию по `viewDataKey` ноды. Используется в T-U02 для сохранения/восстановления позиций.

### Упрощён `ClearAllElements`
Убран `try/finally` с `_suppressReadOnly`. Добавлена очистка `_nodePositions`.

## Что НЕ менялось
- `schedule.Execute` вызовы в `LoadQuest` — остаются до T-U02 (инкрементальные обновления)
- `BuildGraph` — по-прежнему делает полный перестроение
- `QuestNodeGraphWindow` — без изменений

## Следующий тикет
**T-U02**: Node ↔ SO binding (QuestGraphNode + инкрементальные AddStage/DeleteStage)
