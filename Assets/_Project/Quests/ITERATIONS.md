# Iterations

## Итерация от 2026-07-31

**Задача:** Unified Quest Graph v5 — единый нодовый редактор, связывающий NpcDefinition + DialogTree + QuestDefinition в одном GraphView. Архитектура: промежуточный QuestGraphModel (write-through адаптер), семантические порты, 6 типов нод.

**Коммит:** `c2b6494db8a2a962239a9de7b83820c7339fb6c3` — T-QEDIT: Unified Quest Graph v5

**Изменения:**
- `Assets/_Project/Quests/ARCHITECTURE_PLAN.md` — архитектурный план (новый)
- `Assets/_Project/Quests/Editor/QuestGraphModel.cs` — модель-адаптер (новый)
- `Assets/_Project/Quests/Editor/GraphNodes.cs` — 6 классов нод (новый)
- `Assets/_Project/Quests/Editor/UnifiedQuestGraphView.cs` — GraphView + Window (переписан)
- `Assets/_Project/Quests/Editor/UnifiedQuestGraphView_DEPRECATED.txt` — старый v4 (архивирован)
