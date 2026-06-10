# M18 — Editable QuestNodeGraph

> **Дата:** 2026-06-09
> **Статус:** 📋 DESIGN
> **База:** `QuestNodeGraphView.cs` (NEW, GraphView Nodes+Edges)
> **Old:** `QuestGraphView.cs` + `QuestGraphWindow.cs` (custom VisualElement) — помечен как (old)

---

## 1. Scope

**M18 делает QuestNodeGraph мутабельным:**
- Текстовые поля в нодах → можно редактировать прямо в графе
- Сохранение изменений → QuestDefinition.asset обновляется
- Связи между квестами → визуализация prerequisites
- Создание/удаление stages, objectives, actions

**Общий effort:** ~6 тикетов, ~5 ч

---

## 2. Тикеты

### T-Q30 — Editable node fields (⭐ первый, ~1.5 ч)
- Заменить `Label` на `TextField` в extensionContainer для:
  - QuestNode: displayName, description (Quest.questId — read-only)
  - StageNode: stageId, description
  - ObjectiveNode: objectiveId, itemTradeItemId, targetNpcId, requiredQuantity
  - RewardNode: credits, item count, rep faction/value
- "Save" кнопка в title bar (сохраняет в SO)
- "Revert" кнопка (откат к оригиналу из SO)
- Только для QuestNode (остальные — readonly пока)

### T-Q31 — Save back to QuestDefinition (~1 ч)
- `SaveQuestNode(QuestDefinition quest)` — записывает через `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`
- `RevertQuestNode(QuestDefinition quest)` — перечитывает из `AssetDatabase`
- Кнопка "Save All" в toolbar
- Auto-save checkbox (опционально)

### T-Q32 — Add/Delete stages + objectives (~1 ч)
- Кнопки "+" в StageNode (добавить objective)
- Кнопки "×" рядом с objective (удалить)
- "Add Stage" в конце цепочки
- После save — quest.asset обновляется

### T-Q33 — Quest-to-quest prerequisites edge (~1 ч)
- "Prerequisite" output port на QuestNode
- Edge к другому QuestNode (легаси — prerequisites[])
- Визуально: dashed line, другой цвет
- Read-only пока (через menu "toggle prerequisite")

### T-Q34 — Drag-create edges (user-draggable) (~0.5 ч)
- Разблокировать `GetCompatiblePorts` для specific port types
- User может создать edge Quest→Stage (reorder) или Quest→Reward
- Validation: не дать создать invalid connection

---

## 3. Файлы

**Modify:**
- `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` — editable nodes, save, connections

**New:** none (всё в одном файле)

**Old (перенесено в maintenance):**
- `QuestGraphView.cs` + `QuestGraphWindow.cs` — помечены (old), не удаляем

---

## 4. Архитектура

### Состояние редактирования
```csharp
private enum EditMode { View, Edit }
private EditMode _mode;
```
- View mode: текущее поведение (readonly, expanded)
- Edit mode: TextField's видны, кнопки активны, Port's draggable

### Data binding
```csharp
// Каждый Node хранит ссылку на свой SO:
public class QuestEditNode {
    public Node node;
    public QuestDefinition quest; // только для QuestNode
    public QuestStage stage;      // только для StageNode
    public QuestObjective obj;    // только для ObjectiveNode
    ...
}
```

### Система сохранения
```csharp
public void SaveCurrentQuest() {
    EditorUtility.SetDirty(Quest);
    AssetDatabase.SaveAssets();
}
```

---

## 5. Verify

После T-Q30:
- ОткрытьQuestNodeGraph → загрузить quest
- Поменять displayName в ноде → нажать Save
- Проверить в Inspector: displayName изменился
- Нажать Revert → вернулось

После T-Q31:
- Поменять 3 поля → Save All → Inspector → все 3 изменились

После T-Q32:
- "+" → новый stage/objective
- Сохранить → asset изменился

После T-Q33:
- Граф показывает dashed линию между квестами если есть prerequisites
