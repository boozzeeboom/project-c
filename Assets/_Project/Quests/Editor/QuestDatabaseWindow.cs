// T-Q09 / M16: QuestDatabaseWindow — UI Toolkit EditorWindow для просмотра/CRUD квестов.
// Открыть: Tools > ProjectC > Quests > Quest Database Explorer.
//
// Layout:
//   ┌────────────┬─────────────────────────────────┐
//   │ Tree       │ Detail Panel                    │
//   │ - Quests   │ selected quest: stages,         │
//   │ - Dialogs  │ objectives, rewards             │
//   │ - NPCs     │                                 │
//   │ - Factions │ [Open in Inspector] [Re-scan]   │
//   └────────────┴─────────────────────────────────┘

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests;
using ProjectC.Factions;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    public class QuestDatabaseWindow : EditorWindow
    {
        private const string DATABASE_PATH = "Assets/_Project/Quests/Data/QuestDatabase.asset";

        private QuestDatabase _db;
        private TreeView _treeView;
        private VisualElement _detailPanel;
        private Label _statusLabel;

        [MenuItem("Tools/ProjectC/Quests/Quest Database Explorer", priority = 100)]
        public static void Open()
        {
            var w = GetWindow<QuestDatabaseWindow>();
            w.titleContent = new GUIContent("Quest DB");
            w.minSize = new Vector2(720, 480);
            w.Show();
        }

        private void OnEnable()
        {
            LoadDatabase();
            BuildUI();
        }

        private void LoadDatabase()
        {
            _db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DATABASE_PATH);
            if (_db == null)
            {
                // Auto-create via AutoDiscover helper.
                QuestDatabaseAutoDiscover.Rescan();
                _db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DATABASE_PATH);
            }
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;
            root.style.flexGrow = 1;

            // Left: tree view
            var leftPane = new VisualElement();
            leftPane.style.width = 280;
            leftPane.style.borderRightWidth = 1;
            leftPane.style.borderRightColor = new StyleColor(new Color(0, 0, 0, 0.3f));
            leftPane.style.paddingTop = 4;
            leftPane.style.paddingBottom = 4;
            leftPane.style.paddingLeft = 4;
            leftPane.style.paddingRight = 4;

            _treeView = new TreeView();
            _treeView.style.flexGrow = 1;
            _treeView.makeItem = () => new Label();
            _treeView.bindItem = (e, i) =>
            {
                var lbl = (Label)e;
                var data = _treeView.GetItemDataForIndex<ExplorerItem>(i);
                lbl.text = data?.DisplayName ?? "";
            };
            _treeView.selectionType = SelectionType.Single;
            _treeView.selectionChanged += OnTreeSelectionChanged;
            leftPane.Add(_treeView);

            // Re-scan button
            var rescanBtn = new Button(() => { QuestDatabaseAutoDiscover.Rescan(); LoadDatabase(); RefreshTree(); })
            {
                text = "🔄 Re-scan DB"
            };
            rescanBtn.style.marginTop = 4;
            leftPane.Add(rescanBtn);

            root.Add(leftPane);

            // Right: detail panel
            _detailPanel = new ScrollView();
            _detailPanel.style.flexGrow = 1;
            _detailPanel.style.paddingTop = 8;
            _detailPanel.style.paddingBottom = 8;
            _detailPanel.style.paddingLeft = 12;
            _detailPanel.style.paddingRight = 12;
            root.Add(_detailPanel);

            // Status bar
            _statusLabel = new Label();
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.bottom = 4;
            _statusLabel.style.left = 4;
            _statusLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            _statusLabel.style.fontSize = 10;
            root.Add(_statusLabel);

            RefreshTree();
        }

        private void RefreshTree()
        {
            if (_treeView == null || _db == null) return;
            _treeView.Clear();
            var roots = new List<TreeViewItemData<ExplorerItem>>();

            // Quests group
            var questItems = new List<TreeViewItemData<ExplorerItem>>();
            if (_db.quests != null)
            {
                foreach (var q in _db.quests)
                {
                    if (q == null) continue;
                    questItems.Add(new TreeViewItemData<ExplorerItem>(
                        (int)EntityId.ToULong(q.GetEntityId()),
                        new ExplorerItem { Kind = ExplorerKind.Quest, Asset = q, DisplayName = $"{q.questId} — {q.displayName}" }
                    ));
                }
            }
            roots.Add(new TreeViewItemData<ExplorerItem>(1, new ExplorerItem { Kind = ExplorerKind.Group, DisplayName = $"📜 Quests ({questItems.Count})" }, questItems));

            // Dialogs group
            var dialogItems = new List<TreeViewItemData<ExplorerItem>>();
            if (_db.dialogTrees != null)
            {
                foreach (var d in _db.dialogTrees)
                {
                    if (d == null) continue;
                    dialogItems.Add(new TreeViewItemData<ExplorerItem>(
                        (int)EntityId.ToULong(d.GetEntityId()),
                        new ExplorerItem { Kind = ExplorerKind.Dialog, Asset = d, DisplayName = d.treeId }
                    ));
                }
            }
            roots.Add(new TreeViewItemData<ExplorerItem>(2, new ExplorerItem { Kind = ExplorerKind.Group, DisplayName = $"💬 Dialogs ({dialogItems.Count})" }, dialogItems));

            // NPCs group
            var npcItems = new List<TreeViewItemData<ExplorerItem>>();
            if (_db.npcs != null)
            {
                foreach (var n in _db.npcs)
                {
                    if (n == null) continue;
                    npcItems.Add(new TreeViewItemData<ExplorerItem>(
                        (int)EntityId.ToULong(n.GetEntityId()),
                        new ExplorerItem { Kind = ExplorerKind.Npc, Asset = n, DisplayName = $"{n.npcId} — {n.displayName}" }
                    ));
                }
            }
            roots.Add(new TreeViewItemData<ExplorerItem>(3, new ExplorerItem { Kind = ExplorerKind.Group, DisplayName = $"👤 NPCs ({npcItems.Count})" }, npcItems));

            // Factions group
            var factionItems = new List<TreeViewItemData<ExplorerItem>>();
            if (_db.factions != null)
            {
                foreach (var f in _db.factions)
                {
                    if (f == null) continue;
                    factionItems.Add(new TreeViewItemData<ExplorerItem>(
                        (int)EntityId.ToULong(f.GetEntityId()),
                        new ExplorerItem { Kind = ExplorerKind.Faction, Asset = f, DisplayName = f.factionId.ToString() }
                    ));
                }
            }
            roots.Add(new TreeViewItemData<ExplorerItem>(4, new ExplorerItem { Kind = ExplorerKind.Group, DisplayName = $"🏛 Factions ({factionItems.Count})" }, factionItems));

            _treeView.SetRootItems(roots);
            _treeView.Rebuild();
            if (_statusLabel != null) _statusLabel.text = $"Quests: {_db.quests?.Length ?? 0} | Dialogs: {_db.dialogTrees?.Length ?? 0} | NPCs: {_db.npcs?.Length ?? 0} | Factions: {_db.factions?.Length ?? 0}";
        }

        private void OnTreeSelectionChanged(IEnumerable<object> selected)
        {
            if (_detailPanel == null) return;
            _detailPanel.Clear();
            var item = selected.FirstOrDefault() as ExplorerItem;
            if (item == null) return;

            if (item.Kind == ExplorerKind.Quest && item.Asset is QuestDefinition quest)
            {
                BuildQuestDetail(quest);
            }
            else if (item.Kind == ExplorerKind.Dialog && item.Asset is DialogTree dialog)
            {
                BuildDialogDetail(dialog);
            }
            else if (item.Kind == ExplorerKind.Npc && item.Asset is NpcDefinition npc)
            {
                BuildNpcDetail(npc);
            }
            else if (item.Kind == ExplorerKind.Faction && item.Asset is FactionDefinition faction)
            {
                BuildFactionDetail(faction);
            }
            else if (item.Kind == ExplorerKind.Group)
            {
                var lbl = new Label(item.DisplayName);
                lbl.style.fontSize = 14;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                _detailPanel.Add(lbl);
                _detailPanel.Add(new Label("Select an item on the left to view details."));
            }
        }

        private void BuildQuestDetail(QuestDefinition q)
        {
            _detailPanel.Add(MakeHeader($"{q.questId} — {q.displayName}", q));
            _detailPanel.Add(MakeField("Description", q.description));
            _detailPanel.Add(MakeField("Faction", q.faction.ToString()));
            _detailPanel.Add(MakeField("Min Reputation", q.minReputation.ToString()));
            _detailPanel.Add(MakeField("One-shot", q.oneShot ? "yes" : "no"));
            _detailPanel.Add(MakeField("Discoverable", q.discoverable ? "yes" : "no"));
            _detailPanel.Add(new Label(" ") { style = { height = 8 } });

            // Stages
            if (q.stages != null)
            {
                for (int s = 0; s < q.stages.Length; s++)
                {
                    var stage = q.stages[s];
                    if (stage == null) continue;
                    var stageBox = new Box();
                    stageBox.style.marginTop = 4;
                    stageBox.style.paddingTop = 6;
                    stageBox.style.paddingBottom = 6;
                    stageBox.style.paddingLeft = 8;
                    stageBox.style.paddingRight = 8;
                    var sLbl = new Label($"Stage {s}: {stage.stageId} → {stage.nextStageId}");
                    sLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                    stageBox.Add(sLbl);
                    if (!string.IsNullOrEmpty(stage.description)) stageBox.Add(new Label(stage.description));
                    if (stage.objectives != null)
                    {
                        foreach (var obj in stage.objectives)
                        {
                            if (obj == null) continue;
                            var objLbl = new Label($"  • [{obj.objectiveType}] {obj.objectiveId} qty={obj.requiredQuantity} itemId='{obj.itemTradeItemId}'");
                            stageBox.Add(objLbl);
                        }
                    }
                    if (stage.onEnterActions != null && stage.onEnterActions.Length > 0)
                    {
                        stageBox.Add(new Label($"  ↪ onEnter: {stage.onEnterActions.Length} action(s)") { style = { color = new StyleColor(new Color(0.4f, 0.7f, 1f)) } });
                    }
                    if (stage.onCompleteActions != null && stage.onCompleteActions.Length > 0)
                    {
                        stageBox.Add(new Label($"  ↪ onComplete: {stage.onCompleteActions.Length} action(s)") { style = { color = new Color(0.4f, 0.7f, 1f) } });
                    }
                    _detailPanel.Add(stageBox);
                }
            }

            // Rewards
            if (q.rewards != null)
            {
                var rewBox = new Box();
                rewBox.style.marginTop = 8;
                rewBox.style.paddingTop = 6;
                rewBox.style.paddingBottom = 6;
                rewBox.style.paddingLeft = 8;
                rewBox.style.paddingRight = 8;
                rewBox.Add(new Label("Rewards") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                rewBox.Add(new Label($"  CR: {q.rewards.credits}"));
                if (q.rewards.items != null && q.rewards.items.Length > 0)
                {
                    foreach (var r in q.rewards.items) rewBox.Add(new Label($"  Item: x{r.count}"));
                }
                if (q.rewards.reputation != null && q.rewards.reputation.Length > 0)
                {
                    foreach (var r in q.rewards.reputation)
                    {
                        rewBox.Add(new Label($"  Rep: {r.faction} +{r.value}"));
                    }
                }
                _detailPanel.Add(rewBox);
            }

            // Open in Inspector button
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 8;
            var openBtn = new Button(() => Selection.activeObject = q) { text = "Open in Inspector" };
            openBtn.style.marginRight = 4;
            btnRow.Add(openBtn);
            var pingBtn = new Button(() => EditorGUIUtility.PingObject(q)) { text = "Ping Asset" };
            btnRow.Add(pingBtn);
            _detailPanel.Add(btnRow);
        }

        private void BuildDialogDetail(DialogTree d)
        {
            _detailPanel.Add(MakeHeader($"Dialog: {d.treeId}", d));
            _detailPanel.Add(MakeField("Display Name", d.displayName ?? ""));
            _detailPanel.Add(MakeField("Root Node", d.rootNodeId ?? "(none)"));
            if (d.nodes != null)
            {
                _detailPanel.Add(new Label($"Nodes: {d.nodes.Length}"));
                foreach (var n in d.nodes)
                {
                    if (n == null) continue;
                    _detailPanel.Add(new Label($"  • {n.nodeId} ({n.edges?.Length ?? 0} edges)"));
                }
            }
            var openBtn = new Button(() => Selection.activeObject = d) { text = "Open in Inspector" };
            _detailPanel.Add(openBtn);
        }

        private void BuildNpcDetail(NpcDefinition n)
        {
            _detailPanel.Add(MakeHeader($"NPC: {n.npcId}", n));
            _detailPanel.Add(MakeField("Display Name", n.displayName));
            if (n.questOffers != null && n.questOffers.Length > 0)
            {
                _detailPanel.Add(new Label("Quest Offers:"));
                foreach (var t in n.questOffers) _detailPanel.Add(new Label($"  • {t}"));
            }
            if (n.questTurnIns != null && n.questTurnIns.Length > 0)
            {
                _detailPanel.Add(new Label("Quest Turn-Ins:"));
                foreach (var t in n.questTurnIns) _detailPanel.Add(new Label($"  • {t}"));
            }
            var openBtn = new Button(() => Selection.activeObject = n) { text = "Open in Inspector" };
            _detailPanel.Add(openBtn);
        }

        private void BuildFactionDetail(FactionDefinition f)
        {
            _detailPanel.Add(MakeHeader($"Faction: {f.factionId}", f));
            _detailPanel.Add(MakeField("Display Name", f.displayName));
            _detailPanel.Add(MakeField("Lore", f.loreDescription));
            var openBtn = new Button(() => Selection.activeObject = f) { text = "Open in Inspector" };
            _detailPanel.Add(openBtn);
        }

        private static Label MakeHeader(string text, Object pingTarget)
        {
            var lbl = new Label(text);
            lbl.style.fontSize = 18;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.marginBottom = 6;
            return lbl;
        }

        private static Label MakeField(string key, string value)
        {
            var lbl = new Label($"{key}: {value}");
            lbl.style.marginTop = 1;
            return lbl;
        }

        private enum ExplorerKind { Group, Quest, Dialog, Npc, Faction }

        private class ExplorerItem
        {
            public ExplorerKind Kind;
            public Object Asset;
            public string DisplayName;
        }
    }
}
#endif
