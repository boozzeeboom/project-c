// QuestNodeGraphView + QuestNodeGraphWindow
// GraphView-based implementation (Nodes + Edges) — чистая попытка с нуля.
// Все уроки из v1-v6 учтены:
// 1) Ports + Edges через граф с schedule repaint на след. кадр
// 2) expanded=true + RefreshExpandedState в правильном порядке
// 3) _suppressReadOnly для ClearGraph
// 4) Vertical layout (flow сверху-вниз)
// 5) Первый и единственный output port на node (Multi capacity)

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    public class QuestNodeGraphView : GraphView
    {
        public QuestDefinition Quest { get; private set; }
        private bool _suppressReadOnly;

        // T-Q30: edit state
        private bool _editMode;
        // T-Q32: button visibility helpers
        private readonly List<VisualElement> _editButtons = new List<VisualElement>();

        public bool EditMode
        {
            get => _editMode;
            set
            {
                _editMode = value;
                RefreshAllEditUI();
                foreach (var btn in _editButtons)
                    btn.style.display = _editMode ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public QuestNodeGraphView()
        {
            SetupZoom(0.2f, 2.5f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
            var styleSheet = (StyleSheet)EditorGUIUtility.Load("StyleSheets/Default/GraphView/Default.uss");
            if (styleSheet != null) this.styleSheets.Add(styleSheet);
            graphViewChanged = OnGraphViewChanged;
        }

        public void LoadQuest(QuestDefinition quest)
        {
            Quest = quest;
            ClearAllElements();
            if (quest == null) return;
            BuildGraph();
            // KEY FIX #1: force ALL nodes expanded AFTER they're in the GraphView hierarchy.
            schedule.Execute(ForceAllNodesExpanded).StartingIn(30);
            // KEY FIX #2: schedule repaint on next frames for Edge path calculation
            schedule.Execute(() => MarkDirtyRepaint()).StartingIn(0);
            schedule.Execute(() => { MarkDirtyRepaint(); FrameAll(); }).StartingIn(100);
            schedule.Execute(() => MarkDirtyRepaint()).StartingIn(300);
            // KEY FIX #3: T-Q32 restore edit mode UI after rebuild
            if (_editMode)
            {
                schedule.Execute(() => { EditMode = true; }).StartingIn(40);
            }
        }

        private void ForceAllNodesExpanded()
        {
            foreach (var n in nodes.Cast<Node>())
            {
                n.expanded = true;
                n.RefreshExpandedState();
            }
            MarkDirtyRepaint();
        }

        private void ClearAllElements()
        {
            _suppressReadOnly = true;
            try
            {
                var edgeList = new List<GraphElement>(this.edges.ToList());
                var nodeList = new List<GraphElement>(this.nodes.ToList());
                if (edgeList.Count > 0) DeleteElements(edgeList);
                if (nodeList.Count > 0) DeleteElements(nodeList);
            }
            finally { _suppressReadOnly = false; }
            _editButtons.Clear();
        }

        private void BuildGraph()
        {
            if (Quest == null) return;

            const float QUEST_W = 240f, QUEST_H = 140f;
            const float STAGE_W = 240f, STAGE_H = 160f;
            const float OBJ_W = 220f, OBJ_H = 100f;
            const float REWARD_W = 240f, REWARD_H = 130f;
            const float COL1_X = 0f, COL2_X = 360f, COL3_X = 720f, COL4_X = 1100f;
            const float Y_TOP = 0f;
            const float ROW_GAP = 30f;

            var questColor = new Color(0.20f, 0.35f, 0.60f);
            var stageColor = new Color(0.20f, 0.55f, 0.30f);
            var objColor = new Color(0.55f, 0.40f, 0.10f);
            var rewardColor = new Color(0.65f, 0.40f, 0.10f);

            // Quest node
            var qn = MakeEditableNode("📜 QUEST", questColor,
                new (string label, string value, System.Action<string> onSave)[] {
                    ("Name", Quest.displayName, v => Quest.displayName = v),
                    ("Desc", Quest.description ?? "", v => Quest.description = v)
                },
                $"id: {Quest.questId}  •  stages: {Quest.stages?.Length ?? 0}");
            // T-Q32: add stage button on quest node
            var addStageBtn = MakeEditButton("+ Add Stage", () => AddStage(), "add-stage");
            qn.extensionContainer.Add(addStageBtn);
            _editButtons.Add(addStageBtn);
            qn.SetPosition(new Rect(COL1_X, Y_TOP, QUEST_W, QUEST_H));
            AddElement(qn);
            var qPort = AddPorts(qn, hasOutput: true, hasInput: false);

            int stageCount = Quest.stages?.Length ?? 0;
            var stageNodes = new List<Node>();
            float y = Y_TOP;

            if (Quest.stages != null)
            {
                for (int i = 0; i < Quest.stages.Length; i++)
                {
                    var stage = Quest.stages[i];
                    if (stage == null) continue;

                    int si = i; // capture
                    var sn = MakeEditableNode($"STAGE {i+1}/{stageCount}", stageColor,
                        new (string label, string value, System.Action<string> onSave)[] {
                            ("ID", stage.stageId, v => Quest.stages[si].stageId = v),
                            ("Desc", stage.description ?? "", v => Quest.stages[si].description = v)
                        },
                        (stage.objectives != null ? $"🎯 {stage.objectives.Length} objective(s)" : "") +
                        (stage.onEnterActions?.Length > 0 ? $"  ▶ onEnter: {stage.onEnterActions.Length}" : "") +
                        (stage.onCompleteActions?.Length > 0 ? $"  ✓ onComplete: {stage.onCompleteActions.Length}" : "") +
                        (!string.IsNullOrEmpty(stage.nextStageId) ? $"  → {stage.nextStageId}" : ""));
                    // T-Q32: delete stage button
                    var delStageBtn = MakeEditButton("× Stage", () => DeleteStage(si), "stage-del-" + i);
                    sn.extensionContainer.Add(delStageBtn);
                    _editButtons.Add(delStageBtn);
                    // T-Q32: add objective button
                    var addObjBtn = MakeEditButton("+ Objective", () => AddObjective(si), "stage-add-" + i);
                    sn.extensionContainer.Add(addObjBtn);
                    _editButtons.Add(addObjBtn);
                    sn.SetPosition(new Rect(COL2_X, y, STAGE_W, STAGE_H));
                    AddElement(sn);
                    var sPort = AddPorts(sn, hasOutput: true, hasInput: true);

                    if (i == 0) ConnectPorts(qPort.output, sPort.input);
                    if (i > 0) ConnectPorts(GetOutputPort(stageNodes[i-1]), sPort.input);
                    stageNodes.Add(sn);

                    // Objectives — below stage, column 3
                    if (stage.objectives != null)
                    {
                        float oy = y;
                        for (int j = 0; j < stage.objectives.Length; j++)
                        {
                            var obj = stage.objectives[j];
                            if (obj == null) continue;

                            int oi = j; int stIdx = i; // capture
                            var on = MakeEditableNode($"🎯 {obj.objectiveId}", objColor,
                                new (string label, string value, System.Action<string> onSave)[] {
                                    ("ObjId", obj.objectiveId ?? "", v => Quest.stages[stIdx].objectives[oi].objectiveId = v),
                                    ("Item", obj.itemTradeItemId ?? "", v => Quest.stages[stIdx].objectives[oi].itemTradeItemId = v),
                                    ("Npc", obj.targetNpcId ?? "", v => Quest.stages[stIdx].objectives[oi].targetNpcId = v),
                                    ($"[{obj.objectiveType}] ×{obj.requiredQuantity}", $"{obj.requiredQuantity}", v => { if (int.TryParse(v, out var n)) Quest.stages[stIdx].objectives[oi].requiredQuantity = n; })
                                });
                            // T-Q32: delete objective button
                            var delObjBtn = MakeEditButton("× Obj", () => DeleteObjective(stIdx, oi), "obj-del-" + i + "-" + j);
                            on.extensionContainer.Add(delObjBtn);
                            _editButtons.Add(delObjBtn);
                            on.SetPosition(new Rect(COL3_X, oy, OBJ_W, OBJ_H));
                            AddElement(on);
                            var oPort = AddPorts(on, hasOutput: false, hasInput: true);
                            ConnectPorts(sPort.output, oPort.input);
                            oy += OBJ_H + 15f;
                        }
                    }
                    y += STAGE_H + ROW_GAP + (stage.objectives?.Length ?? 0) * (OBJ_H + 15f);
                }
            }

            // Reward — column 4
            if (Quest.rewards != null && HasReward(Quest.rewards))
            {
                var r = Quest.rewards;
                var rLines = "";
                if (r.credits > 0) rLines += $"💰 {r.credits} CR  ";
                if (r.items != null) foreach (var it in r.items) rLines += $"📦 Item ×{it.count}  ";
                if (r.reputation != null) foreach (var rep in r.reputation) rLines += $"📈 {rep.faction} +{rep.value}  ";

                var rFields = new List<(string label, string value, System.Action<string> onSave)>
                {
                    ("Credits", r.credits.ToString(), v => { if (int.TryParse(v, out var n)) r.credits = n; })
                };
                // T-Q30 fix: reputation fields (array, indexed)
                if (r.reputation != null)
                {
                    for (int ri = 0; ri < r.reputation.Length; ri++)
                    {
                        int rIdx = ri;
                        rFields.Add(($"Rep {ri} Faction", r.reputation[ri].faction.ToString(),
                            v => { if (System.Enum.TryParse<FactionId>(v, out var f)) r.reputation[rIdx].faction = f; }));
                        rFields.Add(($"Rep {ri} Value", r.reputation[ri].value.ToString(),
                            v => { if (int.TryParse(v, out var n)) r.reputation[rIdx].value = n; }));
                    }
                }
                if (r.items != null)
                {
                    for (int ii = 0; ii < r.items.Length; ii++)
                    {
                        int iIdx = ii;
                        rFields.Add(($"Item {ii} Count", r.items[ii].count.ToString(),
                            v => { if (int.TryParse(v, out var n)) r.items[iIdx].count = n; }));
                    }
                }

                var rn = MakeEditableNode("🎁 REWARDS", rewardColor,
                    rFields.ToArray(), rLines);
                rn.SetPosition(new Rect(COL4_X, Y_TOP, REWARD_W, REWARD_H));
                AddElement(rn);
                var rPort = AddPorts(rn, hasOutput: false, hasInput: true);
                if (stageNodes.Count > 0)
                    ConnectPorts(GetOutputPort(stageNodes[stageNodes.Count-1]), rPort.input);
            }
        }

        /// <summary>
        /// T-Q30: создать Node с editable полями (Label в view mode, TextField в edit mode).
        /// fields: list of (labelName, currentValue, onSaveAction).
        /// onSaveAction == null → поле только для просмотра. metaLine: строка снизу (всегда видна).
        /// </summary>
        private Node MakeEditableNode(string title, Color titleColor,
            (string label, string value, System.Action<string> onSave)[] fields,
            string metaLine = "")
        {
            var n = new Node { title = title };
            n.titleContainer.style.backgroundColor = new StyleColor(titleColor);
            n.extensionContainer.style.backgroundColor = new StyleColor(titleColor * 0.6f);

            if (fields != null)
            {
                foreach (var f in fields)
                    AddField(n, f);
            }

            if (!string.IsNullOrEmpty(metaLine))
            {
                var ml = new Label(metaLine);
                ml.style.fontSize = 9;
                ml.style.color = new StyleColor(new Color(0.65f, 0.75f, 0.95f, 1f));
                ml.style.paddingLeft = 8; ml.style.paddingRight = 8;
                ml.style.paddingTop = 2; ml.style.paddingBottom = 4;
                n.extensionContainer.Add(ml);
            }

            n.RefreshExpandedState();
            n.expanded = true;
            return n;
        }

        /// <summary>T-Q30: добавить Label + TextField к Node.</summary>
        private static void AddField(Node n, (string label, string value, System.Action<string> onSave) field)
        {
            if (field.label == null && field.value == null) return;

            string displayLabel = !string.IsNullOrEmpty(field.label) ? $"{field.label}: " : "";

            // TextField (edit mode)
            var tf = new TextField(displayLabel) { value = field.value ?? "", name = "editable-field" };
            tf.style.display = DisplayStyle.None;
            tf.style.fontSize = 10;
            tf.style.paddingLeft = 8; tf.style.paddingRight = 4;
            tf.style.paddingTop = 1; tf.style.paddingBottom = 1;
            tf.userData = field.onSave;
            n.extensionContainer.Add(tf);

            // Label (view mode) — only if there's actual content to show
            if (!string.IsNullOrEmpty(field.value))
            {
                var lbl = new Label($"{displayLabel}{field.value}");
                lbl.name = "editable-label";
                lbl.style.fontSize = 10;
                lbl.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                lbl.style.paddingLeft = 8; lbl.style.paddingRight = 4;
                lbl.style.paddingTop = 2; lbl.style.paddingBottom = 1;
                if (field.onSave != null) lbl.style.unityFontStyleAndWeight = FontStyle.Italic; // editable hint
                lbl.style.whiteSpace = WhiteSpace.Normal;
                n.extensionContainer.Add(lbl);
            }
        }

        /// <summary>T-Q30: toggle all editable fields between Label (view) and TextField (edit).</summary>
        public void RefreshAllEditUI()
        {
            foreach (var n in nodes.Cast<Node>())
            {
                if (n == null) continue;
                foreach (var child in n.extensionContainer.Children())
                {
                    if (child is TextField tf && tf.name == "editable-field")
                        tf.style.display = _editMode ? DisplayStyle.Flex : DisplayStyle.None;
                    else if (child is Label lbl && lbl.name == "editable-label")
                        lbl.style.display = _editMode ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
        }

        /// <summary>T-Q30: read all TextField values and apply callbacks (save to SO).</summary>
        public void SaveQuest()
        {
            if (Quest == null) return;
            bool modified = false;
            foreach (var n in nodes.Cast<Node>())
            {
                if (n == null) continue;
                foreach (var child in n.extensionContainer.Children())
                {
                    if (child is TextField tf && tf.name == "editable-field" && tf.userData is System.Action<string> cb)
                    {
                        cb(tf.value);
                        modified = true;
                    }
                }
            }
            if (modified)
            {
                EditorUtility.SetDirty(Quest);
                AssetDatabase.SaveAssets();
                Debug.Log($"[QuestNodeGraph] Saved {Quest.questId}");
            }
        }

        /// <summary>T-Q30: re-read SO and update TextField values.</summary>
        public void RevertQuest()
        {
            if (Quest == null) return;
            var path = AssetDatabase.GetAssetPath(Quest);
            var fresh = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (fresh == null) return;

            // Reload the graph from fresh SO
            LoadQuest(fresh);
            Debug.Log($"[QuestNodeGraph] Reverted {Quest.questId}");
        }

        // ========== T-Q32: Add/Delete CRUD ==========

        /// <summary>Create a small edit-only button (hidden in view mode).</summary>
        private VisualElement MakeEditButton(string text, System.Action onClick, string name)
        {
            var btn = new Button(onClick) { text = text, name = name };
            btn.style.fontSize = 9;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;
            btn.style.paddingTop = 1;
            btn.style.paddingBottom = 1;
            btn.style.marginLeft = 4;
            btn.style.marginTop = 2;
            btn.style.marginBottom = 2;
            btn.style.display = DisplayStyle.None;
            return btn;
        }

        private void AddStage()
        {
            if (Quest == null) return;
            var list = Quest.stages?.ToList() ?? new List<QuestStage>();
            list.Add(new QuestStage { stageId = "new_stage", description = "" });
            Quest.stages = list.ToArray();
            EditorUtility.SetDirty(Quest);
            AssetDatabase.SaveAssets();
            LoadQuest(Quest);
        }

        private void DeleteStage(int index)
        {
            if (Quest == null || Quest.stages == null || index < 0 || index >= Quest.stages.Length) return;
            var list = Quest.stages.ToList();
            list.RemoveAt(index);
            Quest.stages = list.ToArray();
            EditorUtility.SetDirty(Quest);
            AssetDatabase.SaveAssets();
            LoadQuest(Quest);
        }

        private void AddObjective(int stageIndex)
        {
            if (Quest == null || Quest.stages == null || stageIndex < 0 || stageIndex >= Quest.stages.Length) return;
            var stage = Quest.stages[stageIndex];
            if (stage == null) return;
            var list = stage.objectives?.ToList() ?? new List<QuestObjective>();
            list.Add(new QuestObjective { objectiveId = "new_objective", objectiveType = QuestObjectiveType.HaveItem, requiredQuantity = 1 });
            stage.objectives = list.ToArray();
            EditorUtility.SetDirty(Quest);
            AssetDatabase.SaveAssets();
            LoadQuest(Quest);
        }

        private void DeleteObjective(int stageIndex, int objIndex)
        {
            if (Quest == null || Quest.stages == null || stageIndex < 0 || stageIndex >= Quest.stages.Length) return;
            var stage = Quest.stages[stageIndex];
            if (stage == null || stage.objectives == null || objIndex < 0 || objIndex >= stage.objectives.Length) return;
            var list = stage.objectives.ToList();
            list.RemoveAt(objIndex);
            stage.objectives = list.ToArray();
            EditorUtility.SetDirty(Quest);
            AssetDatabase.SaveAssets();
            LoadQuest(Quest);
        }

        private struct NodePorts { public Port input; public Port output; }

        private NodePorts AddPorts(Node n, bool hasOutput, bool hasInput)
        {
            var result = new NodePorts();
            if (hasInput)
            {
                var port = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Multi, typeof(bool));
                port.portName = "";
                n.inputContainer.Add(port);
                result.input = port;
            }
            if (hasOutput)
            {
                var port = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(bool));
                port.portName = "";
                n.outputContainer.Add(port);
                result.output = port;
            }
            return result;
        }

        private Port GetOutputPort(Node n)
        {
            foreach (var child in n.outputContainer.Children())
                if (child is Port p && p.direction == Direction.Output) return p;
            return null;
        }

        private void ConnectPorts(Port output, Port input, bool isAuto = true)
        {
            if (output == null || input == null) return;
            var edge = output.ConnectTo(input);
            if (isAuto) edge.viewDataKey = "auto";
            AddElement(edge);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_suppressReadOnly) return change;
            // T-Q34: allow user-created edges, block only deletion of auto-edges
            if (change.elementsToRemove != null)
            {
                // Remove auto edges from deletion list (protect programmatic connections)
                change.elementsToRemove.RemoveAll(e =>
                {
                    if (e is Edge edge && edge.viewDataKey == "auto") return true;
                    return false;
                });
                // Also protect nodes (readonly)
                change.elementsToRemove.RemoveAll(e => e is Node);
            }
            // Block moving nodes
            if (change.movedElements != null) change.movedElements.Clear();
            return change;
        }

        // T-Q34: allow drag-connecting between compatible ports
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (var p in ports)
            {
                if (p == startPort) continue;
                if (p.node == startPort.node) continue; // no self-connections
                if (p.direction == startPort.direction) continue; // no same-direction
                compatible.Add(p);
            }
            return compatible;
        }

        private static bool HasReward(QuestReward r) => r != null && (r.credits > 0 || (r.items != null && r.items.Length > 0) || (r.reputation != null && r.reputation.Length > 0));
    }

    // ===== Window =====

    public class QuestNodeGraphWindow : EditorWindow
    {
        private QuestNodeGraphView _graph;
        private UnityEditor.UIElements.ObjectField _questField;
        private Button _editBtn;
        private Button _saveBtn;
        private Button _revertBtn;

        [MenuItem("Tools/ProjectC/Quests/Quest Node Graph", priority = 102)]
        public static void Open()
        {
            var w = GetWindow<QuestNodeGraphWindow>();
            w.titleContent = new GUIContent("Quest Node Graph");
            w.minSize = new Vector2(800, 500);
            w.Show();
        }

        private void OnEnable()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingTop = 4; toolbar.style.paddingBottom = 4;
            toolbar.style.paddingLeft = 6; toolbar.style.paddingRight = 6;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 1f));

            _questField = new UnityEditor.UIElements.ObjectField("Quest") { objectType = typeof(QuestDefinition), allowSceneObjects = false };
            _questField.style.flexGrow = 1;
            _questField.RegisterValueChangedCallback(evt => LoadQuest(evt.newValue as QuestDefinition));
            toolbar.Add(_questField);

            var fitBtn = new Button(() => _graph?.FrameAll()) { text = "⊡ Fit" };
            fitBtn.style.marginLeft = 4;
            toolbar.Add(fitBtn);

            // T-Q30: Edit/Save/Revert buttons
            _editBtn = new Button(() =>
            {
                if (_graph == null) return;
                _graph.EditMode = !_graph.EditMode;
                _editBtn.text = _graph.EditMode ? "🔒 View" : "✏️ Edit";
                _saveBtn.style.display = _graph.EditMode ? DisplayStyle.Flex : DisplayStyle.None;
                _revertBtn.style.display = _graph.EditMode ? DisplayStyle.Flex : DisplayStyle.None;
            }) { text = "✏️ Edit" };
            _editBtn.style.marginLeft = 4;
            toolbar.Add(_editBtn);

            _saveBtn = new Button(() => _graph?.SaveQuest()) { text = "💾 Save" };
            _saveBtn.style.marginLeft = 4;
            _saveBtn.style.display = DisplayStyle.None;
            toolbar.Add(_saveBtn);

            _revertBtn = new Button(() => _graph?.RevertQuest()) { text = "↩️ Revert" };
            _revertBtn.style.marginLeft = 4;
            _revertBtn.style.display = DisplayStyle.None;
            toolbar.Add(_revertBtn);
            root.Add(toolbar);

            _graph = new QuestNodeGraphView();
            _graph.style.flexGrow = 1;
            root.Add(_graph);
        }

        public void LoadQuest(QuestDefinition quest)
        {
            if (_questField != null) _questField.value = quest;
            if (_graph == null) return;
            _graph.LoadQuest(quest);
        }
    }
}
#endif
