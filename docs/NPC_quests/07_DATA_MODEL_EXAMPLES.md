# 07 — Примеры данных: NPC, квесты, диалоги

> **Цель:** показать на конкретных примерах, как нарратив-дизайнер будет
> заполнять SO-ассеты. Lore из `docs/WORLD_LORE_BOOK.md` (по лорам
> «Интеграл Пьявица»): 5 Гильдий (Мысли, Созидания, Силы, Тайн, Успеха),
> Underground, Resistance, FreeTraders, SOL Patrol, Pirates.

---

## 7.1 Пример: NPC `Mira` (GuildOfThoughts)

**Файл:** `Assets/_Project/Quests/Data/Npcs/Mira.asset` (после создания проекта).

```
NpcDefinition {
  npcId: "mira_01"
  displayName: "Мира Тихоступ"  // (loc key, реальный текст из LocTable)
  faction: FactionId.GuildOfThoughts
  portrait: [Sprite: mira_portrait_neutral]
  prefab: [Prefab: Mira.prefab]
  animatorConfig: [SO: Mira_animator]

  defaultDialogTree: [DialogTree: mira_default]

  questOffers: [ "find_artifact" ]            // квест, который NPC даёт
  questTurnIns: [ "find_artifact" ]            // квест, который NPC принимает

  services: ServiceFlags.Trade                 // может торговать (артефакты)

  interactionRadius: 3.0
  greetingText: "Приветствую, искатель знаний."
  showGreeting: true
}
```

**Повествование:** Мира — артефактор GuildOfThoughts в Primium, продаёт
антикварные предметы и просит игрока найти утерянный артефакт "Кристалл
Времён". Quest-driven interaction: поговорил → взял квест → выполнил →
вернулся.

---

## 7.2 Пример: QuestDefinition `find_artifact`

**Файл:** `Assets/_Project/Quests/Data/Quests/FindArtifact.asset`.

```
QuestDefinition {
  questId: "find_artifact"
  displayName: "Найти Кристалл Времён"
  description: "Артефакт GuildOfThoughts, утерянный во время Падения."
  faction: FactionId.GuildOfThoughts
  minReputation: 0  // доступен всем

  stages: [
    {
      stageId: "intro"
      description: "Поговори с Мирой и узнай детали."
      objectives: [
        {
          objectiveId: "talk_to_mira"
          type: QuestObjectiveType.TalkToNpc
          targetNpcId: "mira_01"
          description: "Поговори с Мирой в Примуме."
          optional: false
          required: true
        }
      ]
      onEnterActions: []   // nothing happens on entering intro stage
      onCompleteActions: [
        {
          type: ActionType.GiveCredits
          amount: 50         // small advance for "talking to Mira"
        }
      ]
      nextStage: "gather_info"
    },

    {
      stageId: "gather_info"
      description: "Найди 3 дневника исследователей в руинах."
      objectives: [
        {
          objectiveId: "get_diary_1"
          type: QuestObjectiveType.HaveItem
          itemDataId: <int>   // resolved from ItemData "ancient_diary"
          quantity: 1
          description: "Найди первый дневник (руины восточного хребта)."
        },
        {
          objectiveId: "get_diary_2"
          type: QuestObjectiveType.HaveItem
          itemDataId: <int>   // "ancient_diary"
          quantity: 2
          description: "Найди второй дневник (руины южного склона)."
        },
        {
          objectiveId: "get_diary_3"
          type: QuestObjectiveType.HaveItem
          itemDataId: <int>   // "ancient_diary"
          quantity: 3
          description: "Найди третий дневник (руины западной террасы)."
        }
      ]
      onEnterActions: [
        { type: ActionType.EmitEvent, eventId: "find_artifact_stage_2_started" }
      ]
      onCompleteActions: [
        { type: ActionType.AddReputation, faction: GuildOfThoughts, delta: 25 }
      ]
      nextStage: "locate_crystal"
    },

    {
      stageId: "locate_crystal"
      description: "Спустись в подземную лабораторию."
      objectives: [
        {
          objectiveId: "reach_lab"
          type: QuestObjectiveType.ReachLocation
          sceneId: "WorldScene_0_0"   // Primium area
          position: (1234.5, -50.0, 567.8)
          radius: 30.0
          description: "Достигни входа в лабораторию."
        }
      ]
      onEnterActions: []
      onCompleteActions: [
        { type: ActionType.AddReputation, faction: GuildOfThoughts, delta: 10 }
      ]
      nextStage: "retrieve"
    },

    {
      stageId: "retrieve"
      description: "Забери Кристалл Времён из лаборатории."
      objectives: [
        {
          objectiveId: "get_crystal"
          type: QuestObjectiveType.HaveItem
          itemDataId: <int>   // "time_crystal"
          quantity: 1
          description: "Возьми Кристалл Времён."
        }
      ]
      onEnterActions: []
      onCompleteActions: []
      nextStage: "return"
    },

    {
      stageId: "return"
      description: "Вернись к Мире и отдай кристалл."
      objectives: [
        {
          objectiveId: "turn_in"
          type: QuestObjectiveType.TalkToNpc
          targetNpcId: "mira_01"
          description: "Поговори с Мирой."
        }
      ]
      onEnterActions: []
      onCompleteActions: [
        { type: ActionType.TakeItem, itemDataId: <int>, count: 1 }  // remove crystal
      ]
      nextStage: null   // END
    }
  ]

  rewards: {
    credits: 5000
    items: [
      { itemDataId: <int>, count: 1 }   // ancient_knowledge_scroll
      { itemDataId: <int>, count: 3 }   // refined_mezium
    ]
    reputation: [
      { faction: GuildOfThoughts, value: 75 }
    ]
    unlocks: [
      { type: UnlockType.DialogTree, id: "mira_artifact_story" }
    ]
  }

  prerequisites: []
}
```

---

## 7.3 Пример: DialogTree `mira_default`

**Файл:** `Assets/_Project/Quests/Data/Dialogs/MiraDefault.asset`.

```
DialogTree {
  treeId: "mira_default"
  displayName: "Мира — обычный разговор"
  rootNodeId: "greeting"

  nodes: [
    {
      nodeId: "greeting"
      speaker: "mira_01"
      text: "Приветствую, искатель. Чем могу помочь?"
      portraitEmotion: "neutral"
      edges: [
        {
          label: "Расскажи о Гильдии Мысли"
          targetNodeId: "guild_info"
          condition: null    // always available
          action: null
        },
        {
          label: "У тебя есть работа для меня?"
          targetNodeId: "offer_quest"
          condition: {
            type: ConditionType.And
            children: [
              { type: HasQuest, questId: "find_artifact", state: NotActive }
              { type: ReputationAtLeast, faction: GuildOfThoughts, value: 0 }
            ]
          }
          action: null
        },
        {
          label: "Я нашёл Кристалл Времён."
          targetNodeId: "quest_turn_in"
          condition: {
            type: QuestStageEquals
            questId: "find_artifact"
            stageId: "return"
          }
          action: null
        },
        {
          label: "Покажи свои товары."
          targetNodeId: "open_trade"
          condition: null
          action: { type: ActionType.OpenMarket, zoneId: "primium_artifact_shop" }
        },
        {
          label: "Прощай."
          targetNodeId: null
          condition: null
          action: { type: ActionType.EndConversation }
        }
      ]
      onEnterActions: []
    },

    {
      nodeId: "guild_info"
      speaker: "mira_01"
      text: "Гильдия Мысли — это сообщество учёных, философов и исследователей. Мы ищем знания везде, где есть тень..."
      portraitEmotion: "thoughtful"
      edges: [
        {
          label: "Понятно. Расскажи ещё о чём-нибудь."
          targetNodeId: "greeting"   // loop back
          condition: null
          action: null
        },
        {
          label: "Прощай."
          targetNodeId: null
          condition: null
          action: { type: ActionType.EndConversation }
        }
      ]
    },

    {
      nodeId: "offer_quest"
      speaker: "mira_01"
      text: "Да, у меня есть дело. Я ищу Кристалл Времён — утерянный артефакт Гильдии. Если найдёшь — озолочу."
      portraitEmotion: "serious"
      edges: [
        {
          label: "Я помогу. (Принять квест)"
          targetNodeId: null
          condition: null
          action: { type: ActionType.OfferQuest, questId: "find_artifact" }
        },
        {
          label: "Это слишком опасно."
          targetNodeId: "decline"
          condition: null
          action: null
        }
      ]
    },

    {
      nodeId: "decline"
      speaker: "mira_01"
      text: "Понимаю. Если передумаешь — приходи."
      edges: [
        {
          label: "Прощай."
          targetNodeId: null
          condition: null
          action: { type: ActionType.EndConversation }
        }
      ]
    },

    {
      nodeId: "quest_turn_in"
      speaker: "mira_01"
      text: "Невероятно! Это действительно он! Позволь мне..."
      edges: [
        {
          label: "(Отдать кристалл)"
          targetNodeId: "quest_complete"
          condition: null
          action: { type: ActionType.CompleteObjective, questId: "find_artifact", objectiveId: "turn_in" }
        }
      ]
    },

    {
      nodeId: "quest_complete"
      speaker: "mira_01"
      text: "Благодарю тебя от имени всей Гильдии. Вот твоя награда — и кое-что ещё..."
      edges: [
        {
          label: "Спасибо, Мира."
          targetNodeId: null
          condition: null
          action: { type: ActionType.EndConversation }
        }
      ]
    }
  ]
}
```

**На edge `"Покажи свои товары"` action = `OpenMarket`** — opens market window as sub-flow, doesn't end conversation. После закрытия market — return to "greeting" node.

**На edge `"Я помогу. (Принять квест)"` action = `OfferQuest`** — server-side calls `QuestWorld.TryOffer`. После успешного offer — edge target = null (= EndConversation).

**Condition `HasQuest("find_artifact", NotActive)`** скрывает/greys-out option "У тебя есть работа?", если квест уже active/completed.

---

## 7.4 Пример: FactionDefinition `GuildOfThoughts`

**Файл:** `Assets/_Project/Quests/Data/Factions/GuildOfThoughts.asset`.

```
FactionDefinition {
  factionId: FactionId.GuildOfThoughts
  displayName: "Гильдия Мысли"           // (loc key)
  color: (0.6, 0.4, 0.9, 1.0)            // purple-ish
  iconSprite: [Sprite: icon_glyph_thoughts]
  loreDescription: "Собрание учёных и философов, ищущих утраченные знания."
  defaultAttitude: FactionAttitude.Friendly
  reputationThresholds: [
    { tier: "Hostile",     value: -100 },
    { tier: "Unfriendly",  value: -25 },
    { tier: "Neutral",     value: 0 },
    { tier: "Friendly",    value: 25 },
    { tier: "Honored",     value: 75 },
    { tier: "Revered",     value: 150 }
  ]
}
```

**Рендер badge** в DialogWindow: `color` (purple), tier label (Friendly/Honored), value (25).

---

## 7.5 Пример: ReputationDefinition `GuildOfThoughts`

**Файл:** `Assets/_Project/Quests/Data/Factions/GuildOfThoughts_Reputation.asset`.

```
ReputationDefinition {
  factionId: FactionId.GuildOfThoughts
  min: -100
  max: 200   // Honored @ 75, Revered @ 150
  decayPerDay: 0   // no decay for v1
  tiers: [
    { label: "Враг",      min: -100, color: (1.0, 0.2, 0.2), ussClass: "rep-negative" },
    { label: "Недруг",    min: -25,  color: (1.0, 0.4, 0.2), ussClass: "rep-negative" },
    { label: "Нейтрален", min: 0,    color: (0.6, 0.6, 0.7), ussClass: "rep-neutral" },
    { label: "Друг",      min: 25,   color: (0.4, 0.7, 0.4), ussClass: "rep-positive" },
    { label: "Уважаемый", min: 75,   color: (0.5, 0.8, 0.5), ussClass: "rep-positive" },
    { label: "Почитаемый",min: 150,  color: (0.3, 0.9, 0.7), ussClass: "rep-positive" }
  ]
}
```

---

## 7.6 Сценарий end-to-end (player journey)

**Игрок подходит к Мире в Примуме (WorldScene_0_0).**

1. `NetworkPlayer.Update` → каждые 5м polling `InteractableManager.FindNearestNpc`.
2. В пределах 3м → показ tooltip "Нажмите E чтобы поговорить".
3. Игрок жмёт E → `QuestInteractor.TryTalkToNpc()` → `QuestServer.RequestTalkToNpcRpc("mira_01")`.
4. Server validates (in zone, NPC alive, no rate-limit).
5. Server builds `DialogueStepDto` from `mira_default` root node:
   - `currentNodeId = "greeting"`, `text = "Приветствую, искатель..."`, `options[5]`.
   - Option 1 "Расскажи о Гильдии Мысли" — always available, repTint = neutral.
   - Option 2 "У тебя есть работа?" — condition check:
     - `HasQuest(find_artifact, NotActive)` — server checks `_questsByPlayer[playerId]`. Не нашёл → available = true.
     - `ReputationAtLeast(GuildOfThoughts, 0)` — server checks `_reputation[(playerId, GuildOfThoughts)]`. ≥ 0 → true.
     - Both pass → available = true, repTint = positive (quest-related, positive vibe).
   - Option 3 "Я нашёл Кристалл Времён" — condition check: `QuestStageEquals(find_artifact, return)` — НЕТ (квест не active) → available = false, hint = "Мира не давала вам такого задания."
   - Option 4 "Покажи свои товары" — always available, action = OpenMarket (sub-flow).
   - Option 5 "Прощай" — always available, action = EndConversation.
6. Server sends `DialogueStepDto` via TargetRpc.
7. `QuestClientState.OnDialogueStep` → `DialogWindow.Show(step)`.
8. Window: 4 FIX'ы, cursor unlock, typewriter, portrait Миры.
9. Player hovers option 2 → outline GREEN (rep-positive tint).
10. Player hovers option 3 → greyed out, outline DIM RED (unavailable).
11. Player clicks option 2 → `OnOptionClicked(1)` → `RequestAdvanceDialogueRpc(treeId, currentNodeId, 1, npcId)`.
12. Server validates option 1 (the 2nd one, zero-indexed):
    - condition: AND(HasQuest(find_artifact, NotActive), ReputationAtLeast(GuildOfThoughts, 0)) — both pass.
    - edge.action = null (no immediate action).
    - edge.targetNodeId = "offer_quest".
13. Server builds new `DialogueStepDto` for "offer_quest" node, sends to client.
14. Window updates: text "Да, у меня есть дело..." typewriter, 2 new options.
15. Player clicks "Я помогу. (Принять квест)" → `RequestAdvanceDialogueRpc(..., 0, ...)`.
16. Server:
    - Validates option 0 of "offer_quest" node.
    - Fires `action: OfferQuest(find_artifact)`:
      - `QuestWorld.TryOffer(playerId, "find_artifact", "mira_01")`:
        - Validates: not already active, prerequisites met.
        - Adds `QuestInstance { questId, state=Active, currentStageId="intro", objectives=[talk_to_mira] }` to `_questsByPlayer[playerId]`.
        - Fires `onEnterActions` of stage "intro" (none in this example).
        - Sends `QuestSnapshotDto` (with new active quest) + `QuestResultDto(code=Ok)`.
    - Edge target = null → EndConversation.
17. Client receives `QuestSnapshotDto` → `QuestClientState.OnSnapshotUpdated` fires:
    - `QuestTracker` shows: "АКТИВНЫЙ КВЕСТ: Найти Кристалл Времён" + objective "Поговори с Мирой в Примуме" (already satisfied since we just talked).
    - `CharacterWindow` (if open on quests tab) shows the new quest in active list.
    - Toast notification: "Новый квест: Найти Кристалл Времён".
18. Window hides, cursor lock, player back in world.
19. Player can immediately re-talk to Mira → dialog tree's "greeting" node now has option 3 "Я нашёл Кристалл Времён" still UNavailable (quest stage is "intro", not "return").
20. Player needs to advance quest first. But the intro stage's only objective is "talk_to_mira", already satisfied.
21. Server `QuestTriggerService.Evaluate(playerId)` in next tick:
    - `TalkedToNpcTrigger(targetNpcId="mira_01")` — `HasNpcTalkedTo(playerId, "mira_01")` = true (just talked).
    - Quest advance: intro stage completed.
    - `onCompleteActions` of intro: `GiveCredits(50)`.
    - Fire stage transition: currentStage = "gather_info".
    - `onEnterActions` of "gather_info": `EmitEvent("find_artifact_stage_2_started")`.
22. Client receives updated snapshot → quest log shows progress 0/3 diaries.
23. Player explores ruins, finds diaries (via `HaveItemTrigger` polling every 5 sec).
24. When all 3 diaries collected → stage "gather_info" complete → "locate_crystal" stage.
25. `onCompleteActions` of "gather_info": `AddReputation(GuildOfThoughts, +25)`. Badge updates.
26. ... continues until "return" stage.
27. Player returns to Mira, dialog now has option 3 available.
28. Player chooses "Я нашёл Кристалл Времён" → "quest_turn_in" node.
29. Player chooses "(Отдать кристалл)" → action = `CompleteObjective(questId, "turn_in")`.
30. Server validates, fires `TakeItem(time_crystal, 1)` (removes from inventory), `CompleteObjective`, then quest advances to "complete" state.
31. Quest `rewards` fire: `GiveCredits(5000)`, `GiveItem(ancient_knowledge_scroll × 1)`, `GiveItem(refined_mezium × 3)`, `AddReputation(GuildOfThoughts, +75)`, `Unlock(DialogTree, "mira_artifact_story")`.
32. Client receives multiple `QuestResultDto` updates + final `QuestSnapshotDto` (quest moved to completed).
33. UI: toast "Квест выполнен! +5000 CR + ancient_knowledge_scroll + 75 репутации". Badge in Mira's dialog header now reads "Уважаемый (100)".
34. Quest log: "Найти Кристалл Времён" moves from Active to Completed.
35. Player can now access new dialog tree "mira_artifact_story" (via `DialogueAction.SwitchDialogTree` or `DialogueCondition.HasCompletedQuest`).

**Сценарий завершён.** ~5-10 минут геймплея, 5 stage'й, 3 типа триггеров, 5 типов actions, 2 диалоговых condition'а.

---

## 7.7 Базовые типы (enum's)

### FactionId (promoted from NpcFaction)
```csharp
public enum FactionId : byte
{
    None = 0,
    GuildOfThoughts = 1,     // Gildiya Mysley
    GuildOfCreation = 2,     // Gildiya Sozidaniya
    GuildOfStrength = 3,     // Gildiya Sily
    GuildOfSecrets = 4,      // Gildiya Tayn
    GuildOfSuccess = 5,      // Gildiya Uspekha
    Underground = 10,        // Podpolye
    Resistance = 11,         // Soprotivleniye
    FreeTraders = 12,        // Svobodnye Torgovtsy
    SOL_Patrol = 20,         // SOL Patrol (hostile)
    Pirates = 21,            // Pirates (hostile)
    Neutral = 30
}
```

### QuestState
```csharp
public enum QuestState : byte
{
    Offered = 0,    // dialog предложил, не принят (?)
    Active = 1,
    Completed = 2,
    Failed = 3,
    TurnedIn = 4    // завершён + награды выданы
}
```

### QuestObjectiveType
```csharp
public enum QuestObjectiveType : byte
{
    TalkToNpc = 0,
    DeliverItem = 1,         // give X to Y
    ReachLocation = 2,
    KillEntity = 3,          // future
    HaveItem = 4,            // has X in inventory
    ReputationAtLeast = 5,
    WaitForEvent = 6,        // event-driven
    Custom = 99              // extension point
}
```

### ConditionType
```csharp
public enum ConditionType : byte
{
    And = 0,
    Or = 1,
    Not = 2,
    HasItem = 10,
    QuestStateEquals = 20,
    QuestStageEquals = 21,
    ReputationAtLeast = 30,
    ReputationAtMost = 31,
    TimeOfDayIn = 40,
    PlayerInZone = 50,
    FlagIsSet = 60,
    WasNodeVisited = 70
}
```

### ActionType
```csharp
public enum ActionType : byte
{
    Sequence = 0,            // composite: run children in order
    Parallel = 1,            // composite: run children in parallel (not really, just no rollback)
    OfferQuest = 10,
    CompleteObjective = 11,
    FailQuest = 12,
    GiveItem = 20,
    TakeItem = 21,
    GiveCargoItem = 22,
    TakeCargoItem = 23,
    GiveCredits = 30,
    AddReputation = 40,
    OpenMarket = 50,
    OpenService = 51,        // repair/refuel UI
    SetFlag = 60,
    EmitEvent = 61,
    SwitchDialogTree = 70,
    EndConversation = 99
}
```

### FactionAttitude
```csharp
public enum FactionAttitude : byte
{
    Hostile = 0,
    Unfriendly = 1,
    Neutral = 2,
    Friendly = 3,
    Allied = 4
}
```

### ServiceFlags (flags enum)
```csharp
[Flags]
public enum ServiceFlags : byte
{
    None = 0,
    Trade = 1,
    Repair = 2,
    Refuel = 4,
    Restock = 8,
    Info = 16
}
```

### DayNightPhase (existing)
```csharp
public enum DayNightPhase : byte { Dawn, Morning, Noon, Afternoon, Dusk, Evening, Night, Midnight }
```
