# T-U05–T-U08: Unified Graph — Слой 1

**Дата:** 2026-07-22
**Файл:** `Assets/_Project/Quests/Editor/UnifiedQuestGraphView.cs` (новый, ~350 строк)
**Слой:** 1 — Unified Graph
**Зависит от:** T-U01…T-U04 (Слой 0)

## Что сделано

### T-U05: DialogNodeView
- Синие ноды (`0.3, 0.5, 1.0`)
- Source Data = `DialogueNode`; OwnerAsset = `DialogTree`
- Порты: 1 вход «← In», N выходов (по одному на `DialogueEdge`)
- Заголовок: `🤖 {speakerName}: "{text_preview}"`
- Контент: Speaker (resolved через speakerNpc), portraitEmotion
- ResolveSpeakerName: speakerNpc → displayName → refId → kind

### T-U06: Загрузка DialogTree в граф
- `LoadUnified(quest, dialogTree, npcContext)` — загружает quest + dialog
- `LoadDialogTree(tree)` — создаёт DialogNodeView для каждой ноды
- Рёбра между dialog-нодами по `DialogueEdge.targetNodeId` (синие)
- `ApplyUnifiedLayout()` — dialog-ноды над quest-нодами

### T-U07: Связи Dialog↔Quest
- `CreateDialogQuestEdges()` — ищет `DialogueEdge.action.type == OfferQuest` с `questRef == Quest`
- Пунктирные оранжевые рёбра от DialogNode к QuestStageNode
- `OnEdgeCreated` override: drag edge от DialogNode к QuestStageNode → автосоздание `DialogueEdge` с `OfferQuest`
- Цвет: `0.9, 0.5, 0.1` (оранжевый), viewDataKey = `"dialog-quest"`

### T-U08: ConditionNodeView
- Жёлтая нода (`0.9, 0.7, 0.2`)
- Source Data = `DialogueCondition[]`
- Порты: 1 вход «← In», «✓ True» (зелёный), «✗ False» (красный)
- Заголовок: `🔷 {conditionType1 & conditionType2}`
- GetTruePort() / GetFalsePort() хелперы

### Упрощения (будущие доработки)
- Ромбовидная форма ConditionNode — сейчас обычный прямоугольник (нужен custom USS)
- Двойной клик для редактирования текста DialogNode — не реализован
- Ленивая загрузка для больших графов — не реализована

## Следующий тикет
**T-U09**: UnifiedQuestGraphWindow (Слой 2)
