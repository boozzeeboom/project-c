// UnifiedQuestGraphView v5.1 + UnifiedQuestGraphWindow
// v5.1: context menu (Create New / Duplicate Asset), toolbar New buttons, improved layout
//
// Architecture: ARCHITECTURE_PLAN.md

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Quests.Editor
{
    // ═══════════════════════════════════════════
    // UnifiedQuestGraphView
    // ═══════════════════════════════════════════

    public class UnifiedQuestGraphView : GraphView
    {
        public QuestGraphModel Model { get; } = new();

        private readonly Dictionary<object, BaseGraphNode> _nodeMap = new();
        private bool _editMode;

        public bool EditMode
        {
            get => _editMode;
            set { _editMode = value; MarkDirtyRepaint(); }
        }

        public UnifiedQuestGraphView()
        {
            SetupZoom(0.2f, 2.5f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = OnSerializeElements;
            canPasteSerializedData = OnCanPaste;
            unserializeAndPaste = OnUnserializeAndPaste;
        }

        // ── Public API ──

        public void AddNpc(NpcDefinition npc) { Model.AddNpc(npc); Rebuild(); }
        public void AddQuest(QuestDefinition q) { Model.AddQuest(q); Rebuild(); }
        public void AddDialogTree(Dialogue.DialogTree t) { Model.AddDialogTree(t); Rebuild(); }

        public void ClearAll()
        {
            Model.Clear();
            ClearGraphElements();
        }

        public void Rebuild()
        {
            ClearGraphElements();
            Model.BuildGraph();
            BuildVisualGraph();
        }

        public void SaveAll()
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[UnifiedGraph] Saved all. Nodes: {Model.TotalNodeCount}, Edges: {Model.Edges.Count}");
        }

        // ── Asset creation helpers ──

        public static NpcDefinition CreateNewNpcAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create NPC Definition", "Npc_New", "asset",
                "Choose where to save the new NPC asset", "Assets/_Project/Quests/Data/Npcs");
            if (string.IsNullOrEmpty(path)) return null;
            var asset = ScriptableObject.CreateInstance<NpcDefinition>();
            asset.npcId = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.displayName = "New NPC";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        public static QuestDefinition CreateNewQuestAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Quest Definition", "Quest_New", "asset",
                "Choose where to save the new quest asset", "Assets/_Project/Quests/Data/Quests");
            if (string.IsNullOrEmpty(path)) return null;
            var asset = ScriptableObject.CreateInstance<QuestDefinition>();
            asset.questId = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.displayName = "New Quest";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        public static Dialogue.DialogTree CreateNewDialogAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Dialog Tree", "DialogTree_New", "asset",
                "Choose where to save the new dialog asset", "Assets/_Project/Quests/Data/Dialogs");
            if (string.IsNullOrEmpty(path)) return null;
            var asset = ScriptableObject.CreateInstance<Dialogue.DialogTree>();
            asset.treeId = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.displayName = "New Dialog";
            asset.rootNodeId = "greeting";
            asset.nodes = new Dialogue.DialogueNode[] {
                new Dialogue.DialogueNode { nodeId = "greeting", text = "Hello!", speaker = new Dialogue.SpeakerRef { speakerKind = Dialogue.SpeakerRef.Kind.Npc } }
            };
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        public static ScriptableObject DuplicateAsset(ScriptableObject source)
        {
            if (source == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(source);
            string dir = System.IO.Path.GetDirectoryName(srcPath);
            string name = System.IO.Path.GetFileNameWithoutExtension(srcPath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}_Copy.asset");
            if (!AssetDatabase.CopyAsset(srcPath, newPath)) return null;
            AssetDatabase.SaveAssets();
            var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(newPath);
            Selection.activeObject = copy;
            EditorGUIUtility.PingObject(copy);
            return copy;
        }

        // ── Build ──

        private void ClearGraphElements()
        {
            foreach (var e in edges.ToList()) RemoveElement(e);
            foreach (var n in nodes.ToList()) RemoveElement(n);
            _nodeMap.Clear();
        }

        private const float NODE_W = 250f, H_GAP = 40f, V_GAP = 20f;

        private void BuildVisualGraph()
        {
            foreach (var ni in Model.NpcNodes)
                AddAndMap(new NpcGraphNode(ni), ni);

            foreach (var di in Model.DialogNodes)
                AddAndMap(new DialogGraphNode(di), di);

            foreach (var qi in Model.QuestNodes)
                AddAndMap(new QuestRootGraphNode(qi), qi);

            foreach (var si in Model.StageNodes)
                AddAndMap(new StageGraphNode(si), si);

            foreach (var oi in Model.ObjectiveNodes)
                AddAndMap(new ObjectiveGraphNode(oi), oi);

            foreach (var ri in Model.RewardNodes)
                AddAndMap(new RewardGraphNode(ri), ri);

            foreach (var edgeInfo in Model.Edges)
            {
                var edge = CreateVisualEdge(edgeInfo);
                if (edge != null) AddElement(edge);
            }

            ApplyLayout();
            schedule.Execute(() => { MarkDirtyRepaint(); FrameAll(); }).StartingIn(80);
        }

        private void AddAndMap(BaseGraphNode node, object info)
        {
            AddElement(node);
            _nodeMap[info] = node;
        }

        private Edge CreateVisualEdge(EdgeInfo info)
        {
            if (!_nodeMap.TryGetValue(info.fromNode, out var fromNode)) return null;
            if (!_nodeMap.TryGetValue(info.toNode, out var toNode)) return null;

            Port fromPort = FindPort(fromNode, Direction.Output, info.fromPort, info.extraIndex);
            Port toPort = FindPort(toNode, Direction.Input, info.toPort, -1);
            if (fromPort == null || toPort == null) return null;

            var edge = fromPort.ConnectTo(toPort);
            edge.viewDataKey = "model-edge";
            Color c = info.fromPort switch
            {
                PortSemantic.NpcDefaultDialog => GraphNodeColors.PortPurple,
                PortSemantic.NpcOffersQuest => GraphNodeColors.PortOrange,
                PortSemantic.DialogEdgeAction => GraphNodeColors.PortGreen,
                PortSemantic.ObjectiveTarget => GraphNodeColors.PortBlue,
                PortSemantic.StageNext => new Color(0.35f, 0.70f, 0.35f),
                _ => GraphNodeColors.PortGray,
            };
            edge.edgeControl.inputColor = c;
            edge.edgeControl.outputColor = c;
            return edge;
        }

        private static Port FindPort(Node node, Direction dir, PortSemantic semantic, int extraIndex)
        {
            var container = dir == Direction.Output ? node.outputContainer : node.inputContainer;
            foreach (var child in container.Children())
            {
                if (child is Port p && p.direction == dir && BaseGraphNode.GetSemantic(p) == semantic)
                {
                    if (extraIndex >= 0 && BaseGraphNode.GetPortData(p) is int idx && idx != extraIndex) continue;
                    return p;
                }
            }
            return null;
        }

        // ── Layout (columns: NPC | Dialog | Quest+Stages+Objectives | ...) ──

        private void ApplyLayout()
        {
            const float NPC_H = 140f, DLG_H = 230f, Q_H = 130f;
            const float STAGE_H = 210f, OBJ_H = 160f, REWARD_H = 140f;
            const float OBJ_OFFSET_X = 30f;
            float x = 0f;

            // Column: NPCs
            float y = 0f;
            foreach (var ni in Model.NpcNodes)
            {
                if (_nodeMap.TryGetValue(ni, out var n)) n.SetPosition(new Rect(x, y, NODE_W, NPC_H));
                y += NPC_H + V_GAP;
            }
            x += NODE_W + H_GAP;

            // Column: Dialog nodes (grouped by tree)
            y = 0f;
            foreach (var group in Model.DialogNodes.GroupBy(d => d.tree))
            {
                float gy = y;
                foreach (var di in group)
                {
                    if (_nodeMap.TryGetValue(di, out var n)) n.SetPosition(new Rect(x, gy, NODE_W, DLG_H));
                    gy += DLG_H + V_GAP;
                }
                y = gy;
            }
            x += NODE_W + H_GAP;

            // Column(s): Quest chains (quest → stages↓ → reward, objectives→right)
            foreach (var qi in Model.QuestNodes)
            {
                y = 0f;
                float qx = x;

                if (_nodeMap.TryGetValue(qi, out var qNode))
                    qNode.SetPosition(new Rect(qx, y, NODE_W, Q_H));
                y += Q_H + V_GAP;

                var stages = Model.StageNodes.Where(s => s.quest == qi.quest).OrderBy(s => s.stageIndex).ToList();
                var objs = Model.ObjectiveNodes.Where(o => o.quest == qi.quest).ToList();
                float maxStageRight = qx + NODE_W;

                for (int si = 0; si < stages.Count; si++)
                {
                    if (_nodeMap.TryGetValue(stages[si], out var sNode))
                        sNode.SetPosition(new Rect(qx, y, NODE_W, STAGE_H));

                    // Objectives for this stage → right of the stage
                    var stageObjs = objs.Where(o => o.stageIndex == stages[si].stageIndex).ToList();
                    float ox = qx + NODE_W + OBJ_OFFSET_X;
                    float oy = y;
                    foreach (var oi in stageObjs)
                    {
                        if (_nodeMap.TryGetValue(oi, out var oNode))
                            oNode.SetPosition(new Rect(ox, oy, NODE_W * 0.85f, OBJ_H));
                        oy += OBJ_H + 6f;
                    }
                    if (stageObjs.Count > 0 && ox + NODE_W * 0.85f > maxStageRight)
                        maxStageRight = ox + NODE_W * 0.85f;

                    y += STAGE_H + V_GAP;
                }

                // Reward below last stage
                var reward = Model.RewardNodes.FirstOrDefault(r => r.quest == qi.quest);
                if (reward != null && _nodeMap.TryGetValue(reward, out var rNode))
                    rNode.SetPosition(new Rect(qx, y, NODE_W, REWARD_H));

                x = maxStageRight + H_GAP + 30f;
            }
        }

        // ── GraphView callbacks ──

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null)
                foreach (var edge in change.edgesToCreate)
                    if (edge != null) HandleEdgeCreated(edge);

            if (change.elementsToRemove != null)
                foreach (var elem in change.elementsToRemove)
                    if (elem is Edge edge) HandleEdgeDeleted(edge);

            return change;
        }

        private void HandleEdgeCreated(Edge edge)
        {
            if (edge.output?.node is not BaseGraphNode fromNode ||
                edge.input?.node is not BaseGraphNode toNode) { TryRemove(edge); return; }

            var fromSem = BaseGraphNode.GetSemantic(edge.output);
            var toSem = BaseGraphNode.GetSemantic(edge.input);
            int edgeIdx = BaseGraphNode.GetPortData(edge.output) is int ei ? ei : -1;
            object fromInfo = GetNodeInfo(fromNode);
            object toInfo = GetNodeInfo(toNode);
            if (fromInfo == null || toInfo == null) { TryRemove(edge); return; }

            bool ok = Model.SetConnection(fromInfo, fromSem, toInfo, toSem, edgeIdx);
            if (!ok) { TryRemove(edge); Debug.LogWarning($"[UnifiedGraph] Cannot connect {fromSem} → {toSem}"); return; }

            edge.viewDataKey = "user-edge";
            edge.edgeControl.inputColor = fromSem switch
            {
                PortSemantic.DialogEdgeAction => GraphNodeColors.PortOrange,
                PortSemantic.ObjectiveTarget => GraphNodeColors.PortBlue,
                _ => GraphNodeColors.PortGreen,
            };
            edge.edgeControl.outputColor = edge.edgeControl.inputColor;

            if (fromSem == PortSemantic.DialogEdgeAction && fromNode is DialogGraphNode dgn)
                schedule.Execute(() => RebuildSingleDialogNode(dgn)).StartingIn(20);

            Debug.Log($"[UnifiedGraph] Connected {fromSem} → {toSem}");
        }

        private void HandleEdgeDeleted(Edge edge)
        {
            if (edge.output?.node is not BaseGraphNode fromNode ||
                edge.input?.node is not BaseGraphNode toNode) return;

            var fromSem = BaseGraphNode.GetSemantic(edge.output);
            var toSem = BaseGraphNode.GetSemantic(edge.input);
            int edgeIdx = BaseGraphNode.GetPortData(edge.output) is int ei ? ei : -1;
            object fromInfo = GetNodeInfo(fromNode);
            object toInfo = GetNodeInfo(toNode);
            if (fromInfo != null && toInfo != null)
            {
                Model.RemoveConnection(fromInfo, fromSem, toInfo, toSem, edgeIdx);
                if (fromSem == PortSemantic.DialogEdgeAction && fromNode is DialogGraphNode dgn)
                    schedule.Execute(() => RebuildSingleDialogNode(dgn)).StartingIn(20);
            }
        }

        private static object GetNodeInfo(BaseGraphNode node) => node switch
        {
            NpcGraphNode n => n.Info,
            DialogGraphNode n => n.Info,
            QuestRootGraphNode n => n.Info,
            StageGraphNode n => n.Info,
            ObjectiveGraphNode n => n.Info,
            RewardGraphNode n => n.Info,
            _ => null
        };

        private static ScriptableObject GetNodeAsset(BaseGraphNode node) => node switch
        {
            NpcGraphNode n => n.Npc,
            DialogGraphNode n => n.Tree,
            QuestRootGraphNode n => n.Quest,
            StageGraphNode n => n.Quest,
            ObjectiveGraphNode n => n.Quest,
            RewardGraphNode n => n.Quest,
            _ => null
        };

        private void RebuildSingleDialogNode(DialogGraphNode oldNode)
        {
            var connectedEdges = edges.ToList().Where(e =>
                e.output?.node == oldNode || e.input?.node == oldNode).ToList();
            foreach (var e in connectedEdges) RemoveElement(e);
            var pos = oldNode.GetPosition();
            RemoveElement(oldNode);
            _nodeMap.Remove(oldNode.Info);

            var newNode = new DialogGraphNode(oldNode.Info);
            newNode.SetPosition(pos);
            AddElement(newNode);
            _nodeMap[oldNode.Info] = newNode;

            Model.BuildGraph();
            foreach (var edgeInfo in Model.Edges)
            {
                if (_nodeMap.TryGetValue(edgeInfo.fromNode, out var fn) &&
                    _nodeMap.TryGetValue(edgeInfo.toNode, out var tn) &&
                    (fn == newNode || tn == newNode))
                {
                    var visEdge = CreateVisualEdge(edgeInfo);
                    if (visEdge != null) AddElement(visEdge);
                }
            }
            MarkDirtyRepaint();
        }

        private static void TryRemove(Edge edge)
        {
            try { if (edge != null && edge.parent != null) edge.parent.Remove(edge); }
            catch { }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var all = new List<Port>();
            foreach (var p in ports)
            {
                if (p == startPort) continue;
                if (p.node == startPort.node) continue;
                if (p.direction == startPort.direction) continue;
                all.Add(p);
            }
            return all;
        }

        // ── Context Menu ──

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Base menu: Cut/Copy/Paste/Delete (works for selected elements)
            base.BuildContextualMenu(evt);

            // If right-clicked on a node
            if (evt.target is BaseGraphNode targetNode)
            {
                var asset = GetNodeAsset(targetNode);
                if (asset != null)
                {
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("📋 Select Asset", _ => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); });
                    evt.menu.AppendAction("📋 Duplicate Asset", _ =>
                    {
                        var copy = DuplicateAsset(asset);
                        if (copy != null)
                        {
                            if (copy is NpcDefinition npcCopy) AddNpc(npcCopy);
                            else if (copy is QuestDefinition qCopy) AddQuest(qCopy);
                            else if (copy is Dialogue.DialogTree dCopy) AddDialogTree(dCopy);
                        }
                    });
                }

                // Node-type specific actions
                if (targetNode is DialogGraphNode dgn)
                {
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("➕ Add Choice", _ =>
                    {
                        var node = dgn.DialogueNode;
                        if (node == null) return;
                        var list = node.edges?.ToList() ?? new List<Dialogue.DialogueEdge>();
                        list.Add(new Dialogue.DialogueEdge { label = "New Choice", hideIfUnavailable = true });
                        node.edges = list.ToArray();
                        EditorUtility.SetDirty(dgn.Tree);
                        Rebuild();
                    });
                }
                if (targetNode is StageGraphNode sgn)
                {
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("➕ Add Objective", _ =>
                    {
                        var stage = sgn.Stage;
                        if (stage == null) return;
                        var list = stage.objectives?.ToList() ?? new List<QuestObjective>();
                        list.Add(new QuestObjective { objectiveId = "new_obj", objectiveType = QuestObjectiveType.HaveItem, requiredQuantity = 1 });
                        stage.objectives = list.ToArray();
                        EditorUtility.SetDirty(sgn.Quest);
                        Rebuild();
                    });
                }
            }
            else
            {
                // Right-click on empty space
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("🆕 Create New NPC...", _ =>
                {
                    var npc = CreateNewNpcAsset();
                    if (npc != null) AddNpc(npc);
                });
                evt.menu.AppendAction("🆕 Create New Quest...", _ =>
                {
                    var q = CreateNewQuestAsset();
                    if (q != null) AddQuest(q);
                });
                evt.menu.AppendAction("🆕 Create New Dialog Tree...", _ =>
                {
                    var d = CreateNewDialogAsset();
                    if (d != null) AddDialogTree(d);
                });
            }
        }

        // ── Copy/Paste support ──

        private string OnSerializeElements(IEnumerable<GraphElement> elements)
        {
            // Only serialize node references (not full data — we use asset refs)
            return string.Empty;
        }

        private bool OnCanPaste(string data) => false;

        private void OnUnserializeAndPaste(string operationName, string data) { }
    }

    // ═══════════════════════════════════════════
    // UnifiedQuestGraphWindow
    // ═══════════════════════════════════════════

    public class UnifiedQuestGraphWindow : EditorWindow
    {
        private UnifiedQuestGraphView _graph;
        private ObjectField _npcField, _questField, _dialogField;
        private Button _editBtn;
        private Label _statusLabel;

        [MenuItem("Tools/Project C/Quests/Unified Quest Graph", priority = 100)]
        public static void Open()
        {
            var w = GetWindow<UnifiedQuestGraphWindow>();
            w.titleContent = new GUIContent("Unified Quest Graph");
            w.minSize = new Vector2(1200, 750);
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
            toolbar.style.flexWrap = Wrap.Wrap;

            // Add existing assets
            _npcField = new ObjectField("+NPC") { objectType = typeof(NpcDefinition), allowSceneObjects = false };
            _npcField.style.width = 150;
            _npcField.RegisterValueChangedCallback(evt =>
            { if (evt.newValue is NpcDefinition npc) { _graph.AddNpc(npc); _npcField.SetValueWithoutNotify(null); UpdateStatus(); } });
            toolbar.Add(_npcField);

            _questField = new ObjectField("+Quest") { objectType = typeof(QuestDefinition), allowSceneObjects = false };
            _questField.style.width = 150; _questField.style.marginLeft = 4;
            _questField.RegisterValueChangedCallback(evt =>
            { if (evt.newValue is QuestDefinition q) { _graph.AddQuest(q); _questField.SetValueWithoutNotify(null); UpdateStatus(); } });
            toolbar.Add(_questField);

            _dialogField = new ObjectField("+Dialog") { objectType = typeof(Dialogue.DialogTree), allowSceneObjects = false };
            _dialogField.style.width = 150; _dialogField.style.marginLeft = 4;
            _dialogField.RegisterValueChangedCallback(evt =>
            { if (evt.newValue is Dialogue.DialogTree t) { _graph.AddDialogTree(t); _dialogField.SetValueWithoutNotify(null); UpdateStatus(); } });
            toolbar.Add(_dialogField);

            // Separator
            var sep = new Label("│"); sep.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            sep.style.marginLeft = 8; sep.style.marginRight = 4; toolbar.Add(sep);

            // Create new assets
            var newNpcBtn = new Button(() =>
            { var a = UnifiedQuestGraphView.CreateNewNpcAsset(); if (a != null) _graph.AddNpc(a); }) { text = "🆕 NPC" };
            newNpcBtn.style.marginLeft = 4; newNpcBtn.style.fontSize = 10; toolbar.Add(newNpcBtn);

            var newQuestBtn = new Button(() =>
            { var a = UnifiedQuestGraphView.CreateNewQuestAsset(); if (a != null) _graph.AddQuest(a); }) { text = "🆕 Quest" };
            newQuestBtn.style.marginLeft = 2; newQuestBtn.style.fontSize = 10; toolbar.Add(newQuestBtn);

            var newDialogBtn = new Button(() =>
            { var a = UnifiedQuestGraphView.CreateNewDialogAsset(); if (a != null) _graph.AddDialogTree(a); }) { text = "🆕 Dialog" };
            newDialogBtn.style.marginLeft = 2; newDialogBtn.style.fontSize = 10; toolbar.Add(newDialogBtn);

            // Spacer
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; toolbar.Add(spacer);

            _editBtn = new Button(() =>
            { if (_graph == null) return; _graph.EditMode = !_graph.EditMode; _editBtn.text = _graph.EditMode ? "🔒 View" : "✏️ Edit"; })
            { text = "✏️ Edit" }; _editBtn.style.marginLeft = 4; toolbar.Add(_editBtn);

            var saveBtn = new Button(() => _graph?.SaveAll()) { text = "💾 Save All" };
            saveBtn.style.marginLeft = 4; toolbar.Add(saveBtn);
            var fitBtn = new Button(() => _graph?.FrameAll()) { text = "⊡ Fit" };
            fitBtn.style.marginLeft = 4; toolbar.Add(fitBtn);
            var refreshBtn = new Button(() => _graph?.Rebuild()) { text = "🔄" };
            refreshBtn.style.marginLeft = 4; toolbar.Add(refreshBtn);
            var clearBtn = new Button(() => { _graph?.ClearAll(); UpdateStatus(); }) { text = "✕" };
            clearBtn.style.marginLeft = 4; toolbar.Add(clearBtn);

            root.Add(toolbar);

            _graph = new UnifiedQuestGraphView();
            _graph.style.flexGrow = 1;
            root.Add(_graph);

            _statusLabel = new Label("Drag assets or use 🆕 buttons to start — right-click for context menu");
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.bottom = 4; _statusLabel.style.left = 6;
            _statusLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            _statusLabel.style.fontSize = 10;
            root.Add(_statusLabel);
        }

        private void UpdateStatus()
        {
            if (_graph == null || _statusLabel == null) return;
            var m = _graph.Model;
            _statusLabel.text = $"NPCs: {m.NpcNodes.Count}  |  Dialogs: {m.DialogNodes.Count}  |  Quests: {m.QuestNodes.Count}  |  Nodes: {_graph.nodes.ToList().Count}  |  Edges: {_graph.edges.ToList().Count}";
        }

        public void LoadUnified(QuestDefinition quest, Dialogue.DialogTree dialogTree, NpcDefinition npc = null)
        {
            if (npc != null) _graph.AddNpc(npc);
            if (dialogTree != null) _graph.AddDialogTree(dialogTree);
            if (quest != null) _graph.AddQuest(quest);
            UpdateStatus();
        }
    }
}
#endif
