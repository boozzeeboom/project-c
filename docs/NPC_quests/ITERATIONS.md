# Итерации разработки — NPC Quests

## Итерация от 2026-08-13 (S3 fix)

**Задача:** Изъятие DeliverItem-предметов при turn-in (S3)
**Коммит:** `311cb5e62014f428a0a7dcfc121e527870da1ade` — T-QS3: DeliverItem — изъятие предметов при turn-in
**Изменения:**
- `Assets/_Project/Quests/Core/QuestWorld.cs` — новый `ConsumeDeliverItems` + вызов в `TryTurnIn` до перехода в `TurnedIn`
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — S3 отмечен исправленным в P2-плане

---

## Итерация от 2026-08-13 (S1 fix)

**Задача:** Учесть количество предметов в GiveItem и ApplyQuestRewards (S1)
**Коммит:** `2ee0d3be5a8072ca1d56a80975ade2f9fd8721a7` — T-QS1: GiveItem/ApplyQuestRewards учитывают количество
**Изменения:**
- `Assets/_Project/Quests/Network/QuestServer.cs` — `GiveItem` циклит по `intParam` (default 1)
- `Assets/_Project/Quests/Core/QuestWorld.cs` — `ApplyQuestRewards` циклит по `ri.count`
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — S1 отмечен исправленным в P2-плане

---

## Итерация от 2026-08-13 (C8 fix)

**Задача:** Реализовать заглушки DiscoverQuest / EmitEvent / FailQuest (C8)
**Коммит:** `6d9b371b34a54d01d2dc120c0bb666d3a240247c` — T-QC8: реализовать DiscoverQuest / EmitEvent / FailQuest
**Изменения:**
- `Assets/_Project/Quests/Core/QuestWorld.cs` — добавлен `TryFailQuest` (валидация перехода Discovered/Offered/Active → Failed)
- `Assets/_Project/Quests/Network/QuestServer.cs` — `DiscoverQuest` → `TryOffer` + snapshot push; `EmitEvent` → `MarkEventOccurred` + `Publish CustomEvent`; `FailQuest` → `TryFailQuest` + snapshot push
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — C8 отмечен исправленным в P0-плане

---

## Итерация от 2026-08-13 (C6 fix)

**Задача:** Устранить silent-true у 8 нереализованных типов DialogueCondition (C6)
**Коммит:** `e9ef88886d64e5039a0189fee3a17f0e5bba7455` — T-QC6: реализовать недостающие DialogueCondition
**Изменения:**
- `Assets/_Project/Quests/Network/QuestServer.cs` — `EvaluateSingleCondition`: реализованы `QuestStageEquals`, `QuestCompleted`, `QuestDiscovered`, `ReputationAtMost`, `NpcAttitudeAtLeast`
- `Assets/_Project/Quests/Network/QuestServer.cs` — `CargoHasItem`/`PlayerInZone`/`WasNodeVisited` + `default` → `Debug.LogWarning` + `false` (вместо silent true)
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — C6 отмечен исправленным в P0-плане

---

## Итерация от 2026-08-13 (C5 fix)

**Задача:** Устранить ложный «🔒 Discovered» и отсутствие snapshot-push при успешной выдаче квеста (C5)
**Коммит:** `2a773080f30f94d82cb3b2d5aa2e9f8336e4c6fc` — T-QC5: Discovered = успех в OfferQuest (snapshot push + success=true)
**Изменения:**
- `Assets/_Project/Quests/Network/QuestServer.cs` — `FireDialogAction.OfferQuest` считает `Ok` и `Discovered` успехом: push snapshot + `success=true`
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — C5 отмечен исправленным в P0-плане

---

## Итерация от 2026-08-13 (C3 fix)

**Задача:** Устранить обход валидации NPC при turn-in (C3) — CompleteObjective передавал пустой toNpcId
**Коммит:** `8fa6da98af083865c8903311448e79caadb9ad13` — T-QC3: проброс npcId в TryTurnIn + отказ пустому toNpcId
**Изменения:**
- `Assets/_Project/Quests/Network/QuestServer.cs` — `FireDialogAction.CompleteObjective` передаёт `npcId` в `TryTurnIn` (вместо `string.Empty`)
- `Assets/_Project/Quests/Network/QuestServer.cs` — `RequestTurnInQuestRpc` отклоняет пустой `toNpcId` (`InvalidState`)
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — C3 отмечен исправленным в P0-плане

---

## Итерация от 2026-08-13 (C2 fix)

**Задача:** Устранить двойную выдачу наград (C2) — ApplyQuestRewards вызывался и в TryAdvanceStage, и в TryTurnIn
**Коммит:** `688588d42d02c2a99adaf8a7c3f164f377c9f1b8` — T-QC2: единая точка выдачи наград (убрать двойную выдачу)
**Изменения:**
- `Assets/_Project/Quests/Core/QuestWorld.cs` — `TryAdvanceStage` больше не вызывает `ApplyQuestRewards` (выдача только в `TryTurnIn`)
- `Assets/_Project/Quests/Quests/QuestDefinition.cs` — tooltip `rewards` уточнён (награды только при TurnedIn)
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — C2 отмечен исправленным в P0-плане

---

## Итерация от 2026-08-13 (C1 fix)

**Задача:** Закрыть эксплойт C1 — TryTurnIn завершал Active-квест без проверки objectives (сдать любой квест одним RPC)
**Коммит:** `6524335ac67fc43838e328a075a6e044ce21bb99` — T-QC1: TryTurnIn — проверка objectives перед завершением квеста (C1)
**Изменения:**
- `Assets/_Project/Quests/Core/QuestWorld.cs` — `TryTurnIn` проверяет `AreAllRequiredComplete(curStage)` перед `TryAdvanceStage`; невыполненные цели → `Fail(InvalidState)`
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — C1 отмечен исправленным в P0-плане

---

## Итерация от 2026-08-13 (аудит)

**Задача:** Глубокий аудит квест/NPC/диалоговой подсистемы — server-client корректность, несостыковки, рефакторинг
**Коммит:** `3af882b7ca147c9b5add99c5fa92dbca91f90e1d` — T-QAUDIT: Глубокий аудит квест/NPC/диалоговой подсистемы (2026-08-13)
**Изменения:**
- `docs/NPC_quests/DEEP_AUDIT_2026-08-13.md` — полный аудит: критические дефекты C1–C8, сломанные данные D1–D7, server-client несоответствия S1–S16, архитектура/техдолг

---

## Итерация от 2026-07-31 (T-QEDIT v5 — Unified Quest Graph, 14 коммитов)

**Задача:** Единый визуальный редактор NPC↔Dialog↔Quest — модель-ориентированный GraphView
**Коммиты:** `a13ed07f`…`d0083ed4` (14 коммитов, v5.1–v5.12)
**Документация:** `UNIFIED_QUEST_GRAPH_PLAN.md` — план, сверка в конце секции

### v5.1 – Контекстное меню, создание ассетов, вёрстка
- `UnifiedQuestGraphView.cs` + `QuestGraphModel.cs` + `GraphNodes.cs` — новые файлы
- Правый клик → New NPC/Quest/Dialog (создание .asset через SaveFilePanel)
- 5 типов нод: NpcNode, DialogNode, QuestRootNode, StageNode, RewardNode
- Цветовое кодирование: NPC=фиолет, Dialog=синий, Quest=тёмно-синий, Stage=зелёный, Reward=золотой

### v5.2 – Убраны Objective-ноды, стейджи в цепочку
- **Решение:** Objective-ноды удалены из графа. Objectives редактируются ТОЛЬКО внутри StageNode (IMGUI PropertyField).
- StageNode: кнопки `+Obj`, `+Stage`, `×Stage`
- QuestRoot→Stage0→Stage1→...→Last Stage→Reward — вертикальная цепочка
- **Расхождение с планом (T-U07):** план предполагал отдельные Objective-ноды; v5.2 упростил — objectives внутри Stage

### v5.3 – Цепочка стейджей, сохранение позиций
- `AddStage()` проставляет `nextStageId` у предыдущего → цепочка не рвётся
- `PersistKey`-based сохранение позиций при `Rebuild()`
- Stage ID + Next Stage ID в одной строке IMGUI (компактно)

### v5.4 – Undo.RecordObject + AssetDatabase.SaveAssets в CRUD
- `Undo.RecordObject()` перед каждой мутацией
- `AssetDatabase.SaveAssets()` после каждой мутации
- `schedule.Execute` для отложенного `Rebuild()` (избегает конфликта с UI-событием)

### v5.5 – CRUD через SerializedObject API
- `DeleteArrayElementAtIndex` / `InsertArrayElementAtIndex` + `ApplyModifiedProperties()`
- Эксперимент: оказался избыточным, откачен в v5.7

### v5.6 – EditorApplication.delayCall для колбэков
- `delayCall` вместо `schedule.Execute` — гарантирует вызов после UI-события

### v5.7 – Прямой CRUD + Diagnostic.Log
- Возврат к прямому `quest.stages = list.ToArray()` (быстрее, без SerializedObject overhead)
- Синхронные колбэки с `Debug.Log` до/после мутации

### v5.8 – Delete key → Model.DeleteStage
- **Корневая проблема:** пользователь жал Delete на клавиатуре → GraphView удалял визуал, НЕ трогая SO
- `OnGraphViewChanged` теперь перехватывает удаление StageGraphNode → вызывает `Model.DeleteStage()`
- **Сверка с T-U01:** план требовал «разрешить удаление edges; при удалении — удалять из SO» ✅

### v5.9 – Порты диалога + связь Dialog→Quest (не Stage)
- DialogNode: `OnModified` → `Rebuild()` при `+/-Choice` (порты сразу обновляются)
- **Исправление:** `BuildDialogEdges` соединяет `DialogEdgeAction → QuestOfferedBy` (QuestRoot), а не `→StageIn`
- **Сверка с T-U07:** план: «drag edge от DialogNode к QuestStageNode → OfferQuest» ✅ (но к QuestRoot, не к Stage)

### v5.10 – 📌 Пин на каждой ноде
- `BaseGraphNode.AddPinButton()` — кнопка `📌 AssetName` пингует ассет в Project-окне
- NpcGraphNode: расширенный IMGUI (ID, Name, Faction, Portrait, DialogTree, Services)
- **Не по плану:** добавлено для удобства навигации

### v5.11 – Авто-загрузка из диалогов + Undo/Redo
- `AutoLoadFromDialog`: при загрузке диалога сканирует `OfferQuest` → загружает quests
- `Undo.undoRedoPerformed` → `Rebuild()` графа при Ctrl+Z / Ctrl+Y

### v5.12 – Рекурсивная авто-загрузка всей цепочки
- `AddQuest` → `AddNpc(targetNpc)` (а не `_npcs.Add`)
- `AutoLoadFromDialog` → `AddQuest(q)` (а не `_quests.Add`)
- `AddNpc` → `AddQuest(q)` для offerRefs/turnInRefs
- Полный граф: Mira→Dialog→Quest→Objectives→Zipun→... (рекурсивно)

### Сверка с UNIFIED_QUEST_GRAPH_PLAN.md

| Тикет | Статус | Комментарий |
|---|---|---|
| T-U01 Model-driven OnGraphViewChanged | ✅ v5.8 | Удаление нод → мутация SO |
| T-U02 Node↔SO binding | ✅ v5.2 | BaseGraphNode + PersistKey; но CRUD = полный Rebuild (не инкрементальный) |
| T-U03 Авто-лейаут | ✅ v5.2–5.3 | Колоночный (не BFS-дерево), позиции сохраняются |
| T-U04 Осмысленные порты | ✅ v5.1 | Именованные порты с цветами |
| T-U05 DialogNodeView | ✅ v5.2 | Синие ноды, Speaker+text, порты на каждый DialogueEdge |
| T-U06 Загрузка DialogTree | ✅ v5.12 | Рекурсивная загрузка всей цепочки NPC↔Dialog↔Quest |
| T-U07 Связи Dialog↔Quest | ✅ v5.9 | Оранжевые рёбра Dialog→Quest (QuestOfferedBy, не StageIn) |
| T-U09 UnifiedQuestGraphWindow | ✅ v5.2 | Toolbar с ObjectField'ами, статус-бар |
| T-U10 Интеграция | ✅ v5.2 | Кнопка «Unified Graph» в QuestDefinitionEditor |

### Расхождения с планом
1. **ConditionNode (T-U08):** 🚫 Отменён. См. анализ [`T-U08_CONDITION_NODE_ANALYSIS.md`](T-U08_CONDITION_NODE_ANALYSIS.md). Ветвление уже работает через `DialogueEdge.conditions[]`. ConditionNode — визуальная абстракция с дорогой ценой (mapping, NOT-логика, синхронизация). Вместо неё: цветовые индикаторы условий на портах.
2. **Инкрементальный CRUD:** план предполагал добавление/удаление ОДНОЙ ноды без Rebuild. Текущая реализация: полный `Rebuild()` (быстро, т.к. графы маленькие). Ок.
3. **Пунктирные рёбра:** план: «dash» для Dialog↔Quest. Реализация: сплошные оранжевые. Минор.
4. **BFS-лейаут:** план: древовидный. Реализация: колоночный (NPC | Dialog | Quest↓). Ок.
5. **Objective-ноды:** план предполагал отдельные ноды. Решение v5.2: objectives внутри Stage (упрощение). Ок.


### Дополнительно (не в плане)
- Undo/Redo (Ctrl+Z/Y) через `Undo.undoRedoPerformed`
- Пин 📌 на каждой ноде
- Delete key → мутация SO
- Создание ассетов через контекстное меню
- Рекурсивная авто-загрузка всей цепочки зависимостей

---

## Итерация от 2026-07-22 (T-U01, Unified Quest Graph)


**Задача:** T-U01: Model-driven OnGraphViewChanged — разрешить все мутации, убрать _suppressReadOnly
**Коммит:** `e821c7e3` — T-U01: Model-driven OnGraphViewChanged
**Изменения:**
- `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` — убран `_suppressReadOnly`; переписан `OnGraphViewChanged` (разрешены все мутации); добавлены virtual хуки `OnEdgeCreated/Deleted`, `OnNodeDeleted/Moved`; добавлен `_nodePositions` словарь; упрощён `ClearAllElements`
- `docs/NPC_quests/T-U01_model_driven_graphview.md` — документация тикета
- `docs/NPC_quests/UNIFIED_QUEST_GRAPH_PLAN.md` — план всего unified-рефакторинга

## Итерация от 2026-07-21 (T-DLG01)

**Задача:** DialogTreeEditor — карточки нод, drag-and-drop условий и speaker'а
**Коммит:** `93d7fd11` — T-DLG01: DialogTreeEditor — карточки нод, drag-and-drop условий и speaker'а
**Изменения:**
- `Assets/_Project/Quests/Dialogue/DialogueCondition.cs` — +requiredQuest, +requiredNpc, +requiredItem + GetResolved*()
- `Assets/_Project/Quests/Dialogue/SpeakerRef.cs` — +speakerNpc + GetResolvedNpcId()
- `Assets/_Project/Quests/Editor/DialogueConditionDrawer.cs` — ObjectField для quest/npc/item
- `Assets/_Project/Quests/Editor/SpeakerRefDrawer.cs` — NEW: PropertyDrawer
- `Assets/_Project/Quests/Editor/DialogTreeEditor.cs` — NEW: CustomEditor с карточками нод
- `Assets/_Project/Quests/Network/QuestServer.cs` — EvaluateSingleCondition + speaker → GetResolved*()
- `docs/NPC_quests/DIALOGTREE_EDITOR_v2.md` — документация

## Итерация от 2026-07-21 (T-NPC24)

**Задача:** NpcDefinition — drag-and-drop квестов, кастомный редактор с блоками
**Коммит:** `acce9b1` — T-NPC24: NpcDefinition — drag-and-drop квестов, кастомный редактор с блоками
**Изменения:**
- `Assets/_Project/Quests/Npcs/NpcDefinition.cs` — +questOfferRefs, +questTurnInRefs (QuestDefinition[]), +GetQuestOfferIds(), +GetQuestTurnInIds()
- `Assets/_Project/Quests/Editor/NpcDefinitionEditor.cs` — NEW: кастомный Editor с цветными блоками и drag-and-drop
- `Assets/_Project/Quests/Network/QuestServer.cs` — BuildFallbackDialogTree → GetQuestOfferIds()/GetQuestTurnInIds()
- `Assets/_Project/Quests/Core/QuestWorld.cs` — TryTurnIn → GetQuestTurnInIds()
- `Assets/_Project/Editor/Tools/NpcWorldInspectorWindow.cs` → GetQuestOfferIds()/GetQuestTurnInIds()
- `Assets/_Project/Quests/Editor/QuestDatabaseWindow.cs` → GetQuestOfferIds()/GetQuestTurnInIds()
- `docs/NPC_quests/NPC_EDITOR_v2.md` — документация

## Итерация от 2026-07-30 (v2)

**Задача:** Drag-and-drop для всех оставшихся строковых ID (NPC, квесты, сцены, диалоги) — KillEntity, ReachLocation, AddNpcAttitude, SwitchDialogTree, OfferQuest, и др.
**Коммит:** `3fd004e3dd9bd1560d290200ece968dab049038c` — T-QUEDIT v2: drag-and-drop для всех строковых ID
**Изменения:**
- `Assets/_Project/Quests/Quests/QuestObjective.cs` — `targetEntity` (NpcDefinition) для KillEntity
- `Assets/_Project/Quests/Quests/QuestPrerequisite.cs` — `requiredNpc` (NpcDefinition) для NpcAttitudeAtLeast
- `Assets/_Project/Quests/Dialogue/DialogueAction.cs` — `questRef` (QuestDefinition), `npcRef` (NpcDefinition), `dialogTreeRef` (DialogTree) + методы `GetQuestId()`, `GetNpcId()`, `GetDialogTreeId()`
- `Assets/_Project/Quests/Editor/QuestObjectiveDrawer.cs` — SceneAsset ObjectField для ReachLocation
- `Assets/_Project/Quests/Editor/DialogueActionDrawer.cs` — questRef/npcRef/dialogTreeRef поля
- `Assets/_Project/Quests/Editor/QuestPrerequisiteDrawer.cs` — requiredNpc поле
- `Assets/_Project/Quests/Core/QuestWorld.cs` — runtime-резолв NpcAttitudeAtLeast, KillEntity через object refs
- `Assets/_Project/Quests/Network/QuestServer.cs` — резолв через GetQuestId/GetNpcId в FireDialogAction

## Итерация от 2026-07-29

**Задача:** Кастомный редактор QuestDefinition.asset — удобный для не-технаря (drag-and-drop, контекстно-зависимые поля, сводка, авто-валидация)
**Коммит:** `10c5ff17d8bfcfeb29154a2183342ad33f028c2d` — T-QUEDIT: Кастомный редактор QuestDefinition для не-технарей
**Изменения:**
- `Assets/_Project/Quests/Quests/QuestObjective.cs` — добавлен `targetNpc` (NpcDefinition ref)
- `Assets/_Project/Quests/Quests/QuestPrerequisite.cs` — добавлен `requiredQuest` (QuestDefinition ref)
- `Assets/_Project/Quests/Quests/QuestReward.cs` — добавлен `unlockDialog` (DialogTree ref) в QuestRewardUnlock
- `Assets/_Project/Quests/Editor/QuestObjectiveDrawer.cs` — новый: контекстно-зависимый PropertyDrawer
- `Assets/_Project/Quests/Editor/DialogueActionDrawer.cs` — новый: контекстно-зависимый PropertyDrawer
- `Assets/_Project/Quests/Editor/QuestPrerequisiteDrawer.cs` — новый: контекстно-зависимый PropertyDrawer
- `Assets/_Project/Quests/Editor/QuestRewardDrawer.cs` — новый: плоская форма наград
- `Assets/_Project/Quests/Editor/QuestStageDrawer.cs` — новый: карточки objectives/actions
- `Assets/_Project/Quests/Editor/QuestDefinitionEditor.cs` — новый: CustomEditor с 3 вкладками + сводка + авто-валидация
- `Assets/_Project/Quests/Core/QuestWorld.cs` — runtime-резолв targetNpc + requiredQuest
- `Assets/_Project/Quests/Network/QuestServer.cs` — runtime-резолв targetNpc
- `Assets/_Project/Quests/Editor/QuestDefinitionValidator.cs` — обновлена валидация для targetNpc
- `docs/NPC_quests/CUSTOM_EDITOR_PLAN.md` — план реализации

## Итерация от 2026-07-20

**Задача:** Drag-and-drop поля для предметов в наградах и целях квестов (вместо ручного ввода ID)
**Коммит:** `0326dc9eeb23fe6258faeec96060cab99fb53f05` — T-QREWARD: реализация
**Изменения:**
- `Assets/_Project/Quests/Quests/QuestReward.cs` — pickupItem (ItemData) + cargoItem (TradeItemDefinition)
- `Assets/_Project/Quests/Quests/QuestObjective.cs` — pickupItem (ItemData) для HaveItem/DeliverItem
- `Assets/_Project/Quests/Core/QuestWorld.cs` — ResolveItemId с ItemData ref; ApplyQuestRewards через ref
- `Assets/_Project/Quests/Editor/QuestDefinitionValidator.cs` — валидация новых полей + cargoItems
- `Assets/_Project/Quests/Editor/QuestGraphView.cs` — отображение resolved имён
- `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` — отображение resolved имён
- `Assets/_Project/Quests/Editor/QuestDatabaseWindow.cs` — отображение resolved имён
- `Assets/_Project/Quests/Editor/QuestCsvExporter.cs` — экспорт resolved имён
- `docs/NPC_quests/ANALYSIS_QuestRewardItem_refactor.md` — анализ архитектуры

## Итерация от 2026-07-14

**Задача:** Activity Anchors в NpcSocialBrain — Transform-якоря для idle-активностей и patrolWaypointMarkers для hand-placed NPC
**Коммит:** `fb95076a90349a217d4db7872d928f4b49cf72ee` — T-NPC-S23: Activity Anchors — Transform-якоря для idle-активностей и patrolWaypointMarkers в NpcSocialBrain
**Изменения:**
- `Assets/_Project/Scripts/AI/NpcSocialBrain.cs` — добавлены patrolWaypointMarkers, workAnchor, sleepAnchor, sitAnchor, socializeAnchor, wanderAnchor; обновлены Execute* методы
- `Assets/_Project/Scripts/AI/Editor/NpcSocialBrainEditor.cs` — новые поля в инспекторе, секция Activity Anchors
- `docs/NPC_quests/T-NPC-S23_activity_anchors.md` — документация

REPLACE

**Задача:** DialogWindow
=======
## Итерация от 2026-07-09

**Задача:** DialogWindow
=======
REPLACE

**Задача:** DialogWindow: текст NPC всегда виден сверху, кнопки квестов прокручиваются (scroll)
**Коммит:** `aa2a1ec` — T-UI04: фикс DialogWindow — текст NPC всегда виден, кнопки квестов прокручиваются

**Изменения:**
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uxml` — options обёрнут в `<ui:ScrollView name="options-scroll">`
- `Assets/_Project/Quests/Resources/UI/DialogWindow.uss` — panel: `min-height:400px` + `max-height:85vh`; text-scroll: `min-height:80px`; options-scroll: `max-height:220px`

## Итерация от 2026-07-20

**Задача:** T-CNPC-01: интеграция AI+Quest через репутацию — связываем атакующего и квестового NPC на одном GameObject
**Коммит:** `f27c857b03044b61366333a314edf171a7e41d4a` — T-CNPC-01: интеграция AI+Quest через репутацию
**Изменения:**
- `Assets/_Project/Scripts/AI/NpcBrain.cs` (+70 строк): поля `_npcId`, `_hostilityThreshold`, `_respawnConfig`; кэширование npcId из NpcController; ModifyNpcAttitude(-2) при ударе; подписка на NpcAttitudeChangedEvent для смены BehaviorType; OnNpcDeath + RespawnCoroutine
- `Assets/_Project/Scripts/Combat/Implementations/NpcTarget.cs` (+8 строк): public OnKilledEvent; ResetHealth(); замена Destroy на NpcBrain.OnNpcDeath
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity`: [Mira] — добавлены NetworkObject, NavMeshAgent, NpcBrain(Passive), NpcTarget, NpcAttacker, NpcSocialBrain(faction=villagers), NetworkTransform
- `Assets/_Project/Resources/Combat/NpcCombatData_Mira.asset` (новый SO: HP=500)
- `docs/NPC_quests/Complete_v2/*` (3 документа: полный анализ + архитектура + план)

**Доработка:** `fe83428` — [Mira]: CharacterController + HumanM_Model visual + Animator (как у атакующих NPC)
- Убран Cube-плейсхолдер, добавлен CharacterController (height=2, radius=0.4)
- Добавлен Visual child → HumanM_Model.fbx + SkinnedMeshRenderer + Animator (NpcAnimator_Goblin)
- CapsuleCollider оставлен как trigger для E-key interaction (NpcController)

## Итерация от 2026-07-09 (аудит)

**Задача:** Глубокий аудит всей системы квестов — архитектура, стабы, дублирование, интеграции
**Коммит:** `13f3c7f` — T-QAUDIT: Глубокий аудит системы квестов (NPC Quests v2)

**Изменения:**
- `docs/NPC_quests/DEEP_AUDIT_2026-07-09.md` — полный аудит (319 строк)

## Итерация от 2026-07-13 (комбинированный аудит)

**Задача:** Повторный глубокий аудит системы квестов — сравнение с предыдущим, выявление регрессов и незавершённых интеграций
**Коммит:** (pending — пользователь)

**Изменения:**
- `docs/NPC_quests/DEEP_AUDIT_2026-07-13.md` — комбинированный аудит (сопоставлен с предыдущим)
- **Критическое открытие:** квестовые ассеты (FactionDefinition, NpcDefinition, QuestDefinition) утеряны — файлы отсутствуют, GUIDs в QuestDatabase висят в никуда
