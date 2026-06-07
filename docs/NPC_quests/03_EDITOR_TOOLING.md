# 03 — Editor Tooling: Quest Database Explorer

> Источник: subagent report `C:\Users\leon7\projectc_quest_editor_tooling_report.md` (~2500 слов, 10 секций).
> **Цель:** UX + технический дизайн главного инструмента нарратив-дизайнера.

---

## 3.1 Зачем

Существующие 25 editor-скриптов — все IMGUI, все `MenuItem`-генераторы (WorldScene, Bootstrap, MainMenu, TestScene, AssetGenerator). **0 примеров UI Toolkit в editor, 0 `AssetPostprocessor`, 0 `EditorPrefs`, 0 `EditorWindow` для browsing data.**

Нужен **Quest Database Explorer** — IDE-style окно, в котором нарратив-дизайнер:
1. Кликает на NPC → видит все связанные квесты, предметы наград, других упомянутых NPC, диалоги.
2. Сортирует/фильтрует по: получаемому предмету, типу квеста, фракции, локации, статусу.
3. Multi-criteria поиск: "все квесты GuildOfThoughts, которые дают `iron_ingot`".
4. Валидирует: dangling references, no orphan edges, complete reachability.

---

## 3.2 Расположение в Unity

| Параметр | Значение |
|----------|----------|
| Меню | `Window → Project C → Quests → Database Explorer` |
| Класс | `ProjectC.Editor.Quests.QuestDatabaseWindow : EditorWindow` |
| Файл | `Assets/_Project/Editor/Quests/QuestDatabaseWindow.cs` (new) |
| UXML | `Assets/_Project/Editor/Quests/QuestDatabaseWindow.uxml` |
| USS | `Assets/_Project/Editor/Quests/QuestDatabaseWindow.uss` |
| Ассеты данных | `Assets/_Project/Quests/Data/<Faction>/<NpcId>.asset`, `<QuestId>.asset`, `<TreeId>.asset` |
| Реестр | `Assets/_Project/Quests/Data/QuestDatabase.asset` (1 SO, ручной реестр) |

---

## 3.3 Архитектура (3 панели + toolbar)

```
┌──────────────────────────────────────────────────────────────────────┐
│  [🔍 Search] [Faction ▾] [Stage ▾] [Status ▾] [✓ Validate] [⚙ Settings] │  Toolbar
├──────────────────┬──────────────────────────────────┬────────────────┤
│ LEFT PANE        │ CENTER PANE                     │ RIGHT PANE     │
│ (TreeView)       │ (Detail / Multi-column list)    │ (Properties)   │
│                  │                                  │                │
│ ▼ Factions (12)  │  [Tabs: Detail | Reverse-Index]  │ Selected:      │
│   ▼ GuildOfTh..  │                                  │   NPC: Mira    │
│     • Mira (3q)  │  NPC: Mira                       │   ─────────    │
│     • Zoric (1q) │  Faction: GuildOfThoughts        │   id: mira_01  │
│   ▼ GuildOfCr..  │  Port: [sprite]                  │   name: Mira   │
│     • ForgeMaster │  ─────────────────────          │   portrait: ⋯ │
│   ▼ Underground  │  Quests given (3):               │   faction: ⋯  │
│     • ...         │    [Q1: "Find artifact"]  ACTIVE │   ─────────    │
│                  │    [Q2: "Trade route"]   OFFERED │   [Edit SO]    │
│ ▼ Quests (25)    │    [Q3: "Side job"]      OFFERED │   [Ping asset] │
│   • Q1 (3 stages) │                                  │   [Find in scn]│
│   • Q2 (1 stage)  │  Items given (5):                │                │
│   ...            │    [iron_ingot × 3]              │                │
│                  │    [gold_ore × 10]               │                │
│ ▼ Dialog Trees   │    [...]                         │                │
│   • dt_main_quest │                                  │                │
│   • dt_mira_greet │  NPCs mentioned (4):             │                │
│                  │    [Zoric] [ForgeMaster] [...]   │                │
│                  │                                  │                │
├──────────────────┴──────────────────────────────────┴────────────────┤
│ BOTTOM PANE (collapsible): Multi-criteria search results              │
│ ┌─────────────────────────────────────────────────────────────────┐  │
│ │ Found 7 quests matching "GuildOfThoughts + rewards iron_ingot": │  │
│ │ ┌─────────┬─────────────┬─────────┬────────────┬──────────────┐ │  │
│ │ │ QuestId │ Stage       │ NPC     │ Reward     │ Status       │ │  │
│ │ ├─────────┼─────────────┼─────────┼────────────┼──────────────┤ │  │
│ │ │ Q1      │ active      │ Mira    │ iron × 3   │ Active       │ │  │
│ │ │ Q5      │ offered     │ Zoric   │ iron × 10  │ Available    │ │  │
│ │ │ ...     │ ...         │ ...     │ ...        │ ...          │ │  │
│ │ └─────────┴─────────────┴─────────┴────────────┴──────────────┘ │  │
│ └─────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 3.4 Layout components (UI Toolkit)

| Component | UI Toolkit API | Назначение |
|-----------|----------------|------------|
| Toolbar | `Toolbar` + `ToolbarSearchField` | top toolbar с поиском + dropdown фильтрами |
| Left tree | `TreeView` (UI Toolkit) | группировка Factions / Quests / Dialog Trees |
| Center detail | `MultiColumnListView` (если list) или `VisualElement` (если single-item) | показать детали выбранного |
| Right inspector | read-only `PropertyField[]` (через SerializedObject) | показать SO поля selected asset, без редактирования |
| Bottom results | `MultiColumnListView` | результаты multi-criteria search |

### Reverse-Index cache (in-memory)

При загрузке окна (или при изменении assets) — сканировать проект один раз, построить граф:

```csharp
public sealed class QuestIndex
{
    public IReadOnlyDictionary<string, NpcDefinition> NpcsById { get; }
    public IReadOnlyDictionary<string, QuestDefinition> QuestsById { get; }
    public IReadOnlyDictionary<string, DialogTree> DialogsById { get; }
    public IReadOnlyDictionary<FactionId, List<NpcDefinition>> NpcsByFaction { get; }

    // Reverse indices
    public IReadOnlyDictionary<string, List<string>> QuestsByGiverNpcId { get; }
    public IReadOnlyDictionary<string, List<string>> QuestsByTurnInNpcId { get; }
    public IReadOnlyDictionary<string, List<string>> QuestsRewardingItemId { get; }
    public IReadOnlyDictionary<string, List<string>> NpcsMentionedInQuestId { get; }
    public IReadOnlyDictionary<string, List<string>> DialogsByNpcId { get; }
    public IReadOnlyDictionary<string, List<string>> DialogsByReferencedNpcId { get; }
}
```

**Строится:**
- На `OnEnable` окна (lazy, если `null`).
- На `AssetPostprocessor.OnPostprocessAllAssets` если в `Assets/_Project/Quests/Data/` изменились `.asset` файлы.

**Сложность:** O(N) по числу SO ассетов. При 50-200 NPC, 100 квестов — < 100 ms. OK.

---

## 3.5 Data discovery: где брать ассеты

**НЕ централизованный реестр** (как `TradeDatabase`) — слишком хрупко (ручное добавление, рассыпается).

**Сканирование `AssetDatabase`:**
```csharp
public QuestIndex BuildIndex()
{
    var index = new QuestIndex();

    var npcGuids = AssetDatabase.FindAssets("t:NpcDefinition");
    foreach (var guid in npcGuids)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
        if (npc == null || string.IsNullOrEmpty(npc.npcId)) continue;
        index.NpcsById[npc.npcId] = npc;
        // ... etc
    }

    // For each quest, walk its stages.objectives and build reverse indices
    // For each dialog, walk nodes.edges and find referenced npcIds

    return index;
}
```

**Кеш инвалидация** через `AssetPostprocessor`:
```csharp
public sealed class QuestAssetWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool any = importedAssets.Concat(deletedAssets).Concat(movedAssets)
            .Any(p => p.StartsWith("Assets/_Project/Quests/Data/"));
        if (any) QuestDatabaseWindow.InvalidateIndex();
    }
}
```

---

## 3.6 UX flow: "кликнул на NPC → увидел всё"

**User flow:**
1. Открыл `Window → Project C → Quests → Database Explorer`.
2. Левый pane: `TreeView` показывает иерархию Factions → NPCs.
3. Кликнул на NPC `mira_01` (Mira, GuildOfThoughts).
4. **Center pane (Detail tab)** — обновляется:
   - Заголовок: "NPC: Mira", portrait sprite, faction badge.
   - **Quests given** (3): список QuestDefinition с их stages + status.
   - **Items given** (5): aggregate по `reward.items` всех квестов NPC.
   - **Items required** (3): aggregate по `objective.itemId` квестов NPC.
   - **NPCs mentioned** (4): aggregate по всем `targetNpcId` в objectives этого NPC.
   - **Dialogs** (2): `defaultDialogTree` + все `DialogTree`, в которых есть `speaker = mira_01`.
5. Кликнул на sub-row "Q1: Find artifact".
6. **Center pane** — drill-down: показывает stages (3) + objectives (5) с прогрессом.
7. **Right pane (Properties)** — read-only `SerializedProperty` fields of `Q1.asset`.
8. Кнопка `[Edit SO]` — открывает в стандартном Inspector для редактирования.
9. Кнопка `[Ping asset]` — `EditorGUIUtility.PingObject(Q1.asset)` — подсвечивает в Project window.

**Filter:**
- Toolbar dropdown `[Faction ▾]` → "GuildOfThoughts" → все NPCs этой фракции, все квесты этой фракции.
- Search box → "iron" → matches в NPC names, quest names, item names.

**Multi-criteria (bottom pane):**
- "Show all quests with faction=GuildOfThoughts AND reward items contains 'iron_ingot'".
- Результат: `MultiColumnListView` (5 столбцов: QuestId, Stage, Giver NPC, Reward, Status).
- Клик по строке → drill-down в Center pane.

---

## 3.7 Validate button (кросс-reference checker)

**Все 1-shot walk'и для поиска dangling references:**

```csharp
public sealed class QuestValidator
{
    public List<ValidationIssue> Validate(QuestIndex index)
    {
        var issues = new List<ValidationIssue>();

        // 1. Dangling questId references
        foreach (var npc in index.NpcsById.Values)
            foreach (var qId in npc.questOffers)
                if (!index.QuestsById.ContainsKey(qId))
                    issues.Add(new("Dangling quest", $"NPC {npc.npcId} offers unknown quest {qId}"));

        // 2. Dangling npcId references in dialog edges
        foreach (var tree in index.DialogsById.Values)
            foreach (var node in tree.nodes)
                foreach (var edge in node.edges)
                    if (edge.action?.Type == ActionType.SwitchDialogTree)
                        if (!index.DialogsById.ContainsKey(edge.action.treeId))
                            issues.Add(new("Dangling dialog", $"{tree.treeId}.{node.nodeId} → {edge.action.treeId}"));

        // 3. Missing root node
        if (tree.rootNodeId != null && !tree.nodes.Any(n => n.nodeId == tree.rootNodeId))
            issues.Add(new("Missing root", $"{tree.treeId} rootNodeId '{tree.rootNodeId}' not found"));

        // 4. Edge target nodeId not in tree
        // 5. Objective itemId not in TradeDatabase
        // 6. Quest prerequisite cycles (A requires B, B requires A)
        // 7. Dialog graph has unreachable nodes
        // 8. Quest with no stages
        // 9. Stage with no objectives AND no onCompleteActions
        // 10. Item reward qty <= 0

        return issues;
    }
}
```

**Отображаются:**
- Center pane → переключение в "Validation" tab → список issues с `[click to fix]` buttons.
- Console (Unity) → `Debug.LogWarning` для каждой issue.
- CI-friendly: `QuestValidatorCli.RunValidate()` (статический метод, вызываемый из CI скрипта, см. ниже).

---

## 3.8 Технические детали (UI Toolkit)

### Window skeleton
```csharp
public sealed class QuestDatabaseWindow : EditorWindow
{
    private QuestIndex _index;
    private static QuestDatabaseWindow _instance;
    private VisualElement _root;
    private TreeView _treeView;
    private VisualElement _centerPane;
    private VisualElement _rightPane;
    private MultiColumnListView _resultsListView;

    [MenuItem("Window/Project C/Quests/Database Explorer")]
    public static void Open()
    {
        _instance = GetWindow<QuestDatabaseWindow>("Quest Database");
        _instance.minSize = new Vector2(900, 600);
    }

    private void OnEnable()
    {
        _root = rootVisualElement;
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/_Project/Editor/Quests/QuestDatabaseWindow.uxml");
        visualTree.CloneTree(_root);
        var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/_Project/Editor/Quests/QuestDatabaseWindow.uss");
        _root.styleSheets.Add(stylesheet);

        EnsureIndex();
        BuildToolbar();
        BuildTreeView();
        BuildCenterPane();
        BuildRightPane();
        BuildResultsPane();
    }

    public static void InvalidateIndex()
    {
        if (_instance != null) _instance.EnsureIndex(force: true);
    }

    private void EnsureIndex(bool force = false)
    {
        if (_index != null && !force) return;
        _index = new QuestIndexBuilder().Build();
    }
}
```

### UX polish
- **Last selection persistence:** `EditorPrefs.GetString("QuestExplorer.SelectedNode", "...")`.
- **Last search text:** `EditorPrefs.GetString("QuestExplorer.Search", "")`.
- **Last active tab (Detail | Reverse-Index | Validation):** `EditorPrefs.GetString("QuestExplorer.ActiveTab", "Detail")`.
- **Window dock position:** стандартный Unity Editor docking.
- **"Find in scene" button:** для NPC — `var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(...)` → find scene references → highlight in Hierarchy.
- **Multi-select в TreeView:** standard.
- **Drag-drop:** drag a quest asset into a NPC's `questOffers` list in right pane → modify SO and save.
- **Export selected to JSON:** right-click → "Export" → `JsonUtility.ToJson(questDef, true)` → save to `Assets/_Project/Quests/Exports/`.
- **Diff two versions:** "Diff with backup" → compare two `.asset` files visually (NOT in v1, отложено).

---

## 3.9 Asset placement strategy

**Расположение папок** (внутри `Assets/_Project/Quests/`):
```
Quests/                       ← NEW
  Data/
    Factions/
      GuildOfThoughts.asset
      GuildOfCreation.asset
      ... (12 .asset files)
    Npcs/
      Mira.asset
      Zoric.asset
      ForgeMaster.asset
      ... (~50-200 .asset files)
    Quests/
      FindArtifact.asset
      TradeRoute.asset
      SideJob.asset
      ... (~100 .asset files)
    Dialogs/
      MainQuest.asset
      MiraGreeting.asset
      ... (~50 .asset files)
    QuestDatabase.asset     ← 1 registry SO
    AnimatorConfigs/        ← optional, per-NPC
      Mira_animator.asset
      ...
  Network/
    QuestServer.cs
  Core/
    QuestWorld.cs
    QuestDefinition.cs
    ...
  Dto/
    QuestDto.cs
    ...
  Client/
    QuestClientState.cs
  UI/
    DialogWindow.uxml/uss
    QuestTracker.uxml/uss
    DialogWindow.cs
    QuestLogTab.cs (or part of CharacterWindow)
  Triggers/
    IQuestTrigger.cs
    QuestTriggerService.cs
    TalkedToNpcTrigger.cs
    ItemInInventoryTrigger.cs
    ...
  Interactions/
    QuestInteractor.cs
Editor/
  Quests/
    QuestDatabaseWindow.cs
    QuestDatabaseWindow.uxml
    QuestDatabaseWindow.uss
    QuestAssetWatcher.cs        ← AssetPostprocessor
    QuestValidator.cs
    QuestValidatorCli.cs        ← for CI
    QuestIndexBuilder.cs
```

---

## 3.10 Дополнительные editor utilities (smaller)

| Tool | Меню | Что делает |
|------|------|------------|
| `QuestReferenceChecker` | `Tools/Project C/Quests/Check References` | Validate all quests, NPCs, dialogs (same as Validate button, but standalone). |
| `QuestJsonExporter` | `Tools/Project C/Quests/Export All to JSON` | For sharing with writers/narrative designers outside Unity. |
| `QuestJsonImporter` | `Tools/Project C/Quests/Import from JSON` | For batch creation from spreadsheets. |
| `QuestTextLinter` | `Tools/Project C/Quests/Check Text Length` | Find dialog nodes > 4 sentences (UX), warn. |
| `NpcSpawner` | `Tools/Project C/Quests/Spawn NPC in Current Scene` | Drag a `NpcDefinition` → spawn GameObject with `NpcEntity + NpcInteraction` components. Uses prefab from `NpcDefinition.prefab`. Adds to `ScenePlacedObjectSpawner` awareness automatically. |

---

## 3.11 Open questions (editor tooling)

**См. `09_OPEN_QUESTIONS.md` §B (editor-specific).** Ключевые:
- View-only browser + native Inspector для edit? Или full CRUD в custom window?
- Включать ли GraphView (visual graph editor) для `DialogTree`?
- Multi-user collaboration (Perforce/Git LFS для SO ассетов)?
