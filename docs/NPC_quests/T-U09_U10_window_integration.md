# T-U09–T-U10: UnifiedQuestGraphWindow + Интеграция

**Дата:** 2026-07-22
**Файлы:** `UnifiedQuestGraphView.cs` (добавлен window), `QuestDefinitionEditor.cs`, `DialogTreeEditor.cs`
**Слой:** 2 — Новое окно + UX

## T-U09: UnifiedQuestGraphWindow

Новое окно: `Tools/Project C/Quests/Unified Quest Graph` (priority=100).

### Toolbar
- **Quest** dropdown (ObjectField) — выбор QuestDefinition
- **Dialog** dropdown — выбор DialogTree (авто-заполняется из NPC)
- **NPC** dropdown — контекстный NPC (авто-резолвит defaultDialogTree)
- **⊡ Fit** — FrameAll
- **✏️ Edit / 🔒 View** — переключение режима редактирования
- **💾 Save All / ↩️ Revert** — видны только в edit mode

### Status bar (снизу)
`Nodes: 12  |  Edges: 18  |  Quest: collect_copper_ore  |  Dialog: MiraDefault`

### Авто-резолв
При выборе квеста + NPC — автоматически подставляет `npc.defaultDialogTree`.
При изменении любого поля — `TryLoadUnified()`.

## T-U10: Интеграция с редакторами

### UnifiedQuestGraphIntegration (статический хелпер)
- `OpenUnified(QuestDefinition)` — ищет NPC через `GetQuestOfferIds()`, берёт `defaultDialogTree`
- `OpenUnified(DialogTree)` — ищет Quest через `DialogueEdge.action.type == OfferQuest.questRef`

### Кнопки в редакторах
- **QuestDefinitionEditor.cs**: кнопка «🔗 Unified Graph» в header'е (рядом с Validate)
- **DialogTreeEditor.cs**: кнопка «🔗 Unified Graph» сверху инспектора

## Что осталось на будущее
- +Node dropdown (Add Dialog/Condition/Stage/Objective/Reward) — не реализован
- Двойной клик для редактирования текста в DialogNodeView
- CSV-импорт интеграция
- Ромбовидная форма ConditionNode (нужен custom USS)
