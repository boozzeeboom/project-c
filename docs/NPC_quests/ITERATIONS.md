# Итерации разработки — NPC Quests

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
