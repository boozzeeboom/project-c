// UnifiedQuestGraphView v5.2 + UnifiedQuestGraphWindow
// No ObjectiveGraphNode — objectives live inside StageNode.
// Stage CRUD: +Stage buttons on QuestRoot and StageNode.
// Stage→NPC port: connects to NpcNode via TalkToNpc objective.

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
    public class UnifiedQuestGraphView : GraphView
    {
        public QuestGraphModel Model { get; } = new();
        private readonly Dictionary<object, BaseGraphNode> _nodeMap = new();
        private bool _editMode;

        public bool EditMode { get => _editMode; set { _editMode = value; MarkDirtyRepaint(); } }

        public UnifiedQuestGraphView()
        {
            SetupZoom(0.2f, 2.5f);
            this.AddManipulator(new ContentDragger()); this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector()); this.AddManipulator(new ContentZoomer());
            var grid = new GridBackground(); Insert(0, grid); grid.StretchToParentSize();
            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = _ => string.Empty;
            canPasteSerializedData = _ => false;
            unserializeAndPaste = (_, _) => { };
        }

        public void AddNpc(NpcDefinition npc) { Model.AddNpc(npc); Rebuild(); }
        public void AddQuest(QuestDefinition q) { Model.AddQuest(q); Rebuild(); }
        public void AddDialogTree(Dialogue.DialogTree t) { Model.AddDialogTree(t); Rebuild(); }

        public void ClearAll() { Model.Clear(); ClearGraphElements(); }
        private readonly Dictionary<string, Rect> _savedPositions = new();

        public void Rebuild()
        {
            SavePositions();
            ClearGraphElements();
            Model.BuildGraph();
            BuildVisualGraph();
            RestorePositions();
        }

        private void SavePositions()
        {
            _savedPositions.Clear();
            foreach (var n in nodes)
                if (n is BaseGraphNode bn && !string.IsNullOrEmpty(bn.PersistKey))
                    _savedPositions[bn.PersistKey] = n.GetPosition();
        }

        private void RestorePositions()
        {
            foreach (var n in nodes)
                if (n is BaseGraphNode bn && !string.IsNullOrEmpty(bn.PersistKey) && _savedPositions.TryGetValue(bn.PersistKey, out var r))
                    n.SetPosition(r);
        }

        public void SaveAll() { AssetDatabase.SaveAssets(); Debug.Log($"[UnifiedGraph] Saved all. Nodes: {Model.TotalNodeCount}, Edges: {Model.Edges.Count}"); }

        public static NpcDefinition CreateNewNpcAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create NPC", "Npc_New", "asset", "Save NPC asset", "Assets/_Project/Quests/Data/Npcs");
            if (string.IsNullOrEmpty(path)) return null;
            var a = ScriptableObject.CreateInstance<NpcDefinition>(); a.npcId = System.IO.Path.GetFileNameWithoutExtension(path); a.displayName = "New NPC";

            AssetDatabase.CreateAsset(a, path); AssetDatabase.SaveAssets(); Selection.activeObject = a; EditorGUIUtility.PingObject(a); return a;
        }
        public static QuestDefinition CreateNewQuestAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Quest", "Quest_New", "asset", "Save quest asset", "Assets/_Project/Quests/Data/Quests");
            if (string.IsNullOrEmpty(path)) return null;
            var a = ScriptableObject.CreateInstance<QuestDefinition>(); a.questId = System.IO.Path.GetFileNameWithoutExtension(path); a.displayName = "New Quest";

            AssetDatabase.CreateAsset(a, path); AssetDatabase.SaveAssets(); Selection.activeObject = a; EditorGUIUtility.PingObject(a); return a;
        }
        public static Dialogue.DialogTree CreateNewDialogAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Dialog", "DialogTree_New", "asset", "Save dialog asset", "Assets/_Project/Quests/Data/Dialogs");
            if (string.IsNullOrEmpty(path)) return null;
            var a = ScriptableObject.CreateInstance<Dialogue.DialogTree>(); a.treeId = System.IO.Path.GetFileNameWithoutExtension(path); a.displayName = "New Dialog"; a.rootNodeId = "greeting";

            a.nodes = new Dialogue.DialogueNode[] { new Dialogue.DialogueNode { nodeId = "greeting", text = "Hello!", speaker = new Dialogue.SpeakerRef { speakerKind = Dialogue.SpeakerRef.Kind.Npc } } };
            AssetDatabase.CreateAsset(a, path); AssetDatabase.SaveAssets(); Selection.activeObject = a; EditorGUIUtility.PingObject(a); return a;
        }
        public static ScriptableObject DuplicateAsset(ScriptableObject src)
        {
            if (src == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(src);
            string dir = System.IO.Path.GetDirectoryName(srcPath);
            string name = System.IO.Path.GetFileNameWithoutExtension(srcPath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{name}_Copy.asset");
            if (!AssetDatabase.CopyAsset(srcPath, newPath)) return null;
            AssetDatabase.SaveAssets();
            var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(newPath);
            Selection.activeObject = copy; EditorGUIUtility.PingObject(copy); return copy;
        }

        // ── Build ──

        const float NODE_W = 260f, H_GAP = 40f, V_GAP = 20f;


        private void ClearGraphElements()
        {
            foreach (var e in edges.ToList()) RemoveElement(e);
            foreach (var n in nodes.ToList()) RemoveElement(n);
            _nodeMap.Clear();
        }

        private void BuildVisualGraph()
        {
            foreach (var ni in Model.NpcNodes) { var n = new NpcGraphNode(ni); AddElement(n); _nodeMap[ni] = n; }
            foreach (var di in Model.DialogNodes) { var n = new DialogGraphNode(di); AddElement(n); _nodeMap[di] = n; }
            foreach (var qi in Model.QuestNodes)
            {
                var qn = new QuestRootGraphNode(qi);
                qn.OnAddStage = q => { Debug.Log($"[GraphView] +Stage clicked: {q.questId}, stages before={q.stages?.Length ?? 0}"); Model.AddStage(q); Debug.Log($"[GraphView] +Stage done: stages after={q.stages?.Length ?? 0}"); Rebuild(); };
                AddElement(qn); _nodeMap[qi] = qn;
            }
            foreach (var si in Model.StageNodes)
            {
                var sn = new StageGraphNode(si);
                sn.StageCount = () => Model.StageNodes.Count(s => s.quest == si.quest);
                sn.OnDeleteStage = s => { Debug.Log($"[GraphView] ×Stage clicked: {s.quest.questId} idx={s.stageIndex}, stages before={s.quest.stages?.Length ?? 0}"); Model.DeleteStage(s); Debug.Log($"[GraphView] ×Stage done: stages after={s.quest.stages?.Length ?? 0}"); Rebuild(); };
                sn.OnAddStageAfter = s => { Model.AddStage(s.quest, s.stageIndex); Rebuild(); };
                sn.OnAddObjective = s => { Model.AddObjective(s); Rebuild(); };
                AddElement(sn); _nodeMap[si] = sn;
            }



            foreach (var ri in Model.RewardNodes) { var n = new RewardGraphNode(ri); AddElement(n); _nodeMap[ri] = n; }

            foreach (var ei in Model.Edges) { var e = CreateVisualEdge(ei); if (e != null) AddElement(e); }

            ApplyLayout();
            schedule.Execute(() => { MarkDirtyRepaint(); FrameAll(); }).StartingIn(50);
        }

        private Edge CreateVisualEdge(EdgeInfo info)
        {
            if (!_nodeMap.TryGetValue(info.fromNode, out var fn) || !_nodeMap.TryGetValue(info.toNode, out var tn)) return null;
            Port fp = FindPort(fn, Direction.Output, info.fromPort, info.extraIndex);
            Port tp = FindPort(tn, Direction.Input, info.toPort, -1);
            if (fp == null || tp == null) return null;
            var e = fp.ConnectTo(tp); e.viewDataKey = "model-edge";
            Color c = info.fromPort switch
            {
                PortSemantic.NpcDefaultDialog => GraphNodeColors.PortPurple,
                PortSemantic.NpcOffersQuest => GraphNodeColors.PortOrange,
                PortSemantic.DialogEdgeAction => GraphNodeColors.PortGreen,
                PortSemantic.StageTargetNpc => GraphNodeColors.PortBlue,
                PortSemantic.StageNext => new Color(0.35f, 0.70f, 0.35f),
                _ => GraphNodeColors.PortGray,
            };
            e.edgeControl.inputColor = c; e.edgeControl.outputColor = c; return e;
        }

        private static Port FindPort(Node node, Direction dir, PortSemantic sem, int extraIdx)
        {
            var container = dir == Direction.Output ? node.outputContainer : node.inputContainer;
            foreach (var c in container.Children())
                if (c is Port p && p.direction == dir && BaseGraphNode.GetSemantic(p) == sem)
                { if (extraIdx >= 0 && BaseGraphNode.GetPortData(p) is int i && i != extraIdx) continue; return p; }
            return null;
        }

        // ── Layout (columns: NPC | Dialog | Quest→Stages↓→Reward) ──

        private void ApplyLayout()
        {
            const float NPC_H = 130f, DLG_H = 200f, Q_H = 140f, STAGE_H = 290f, REWARD_H = 140f;

            float x = 0f, y = 0f;

            foreach (var ni in Model.NpcNodes) { if (_nodeMap.TryGetValue(ni, out var n)) n.SetPosition(new Rect(x, y, NODE_W, NPC_H)); y += NPC_H + V_GAP; }
            x += NODE_W + H_GAP; y = 0f;
            foreach (var g in Model.DialogNodes.GroupBy(d => d.tree))
            { float gy = y; foreach (var di in g) { if (_nodeMap.TryGetValue(di, out var n)) n.SetPosition(new Rect(x, gy, NODE_W, DLG_H)); gy += DLG_H + V_GAP; } y = gy; }
            x += NODE_W + H_GAP; y = 0f;

            foreach (var qi in Model.QuestNodes)
            {
                float qx = x, qy = 0f;
                if (_nodeMap.TryGetValue(qi, out var qNode)) qNode.SetPosition(new Rect(qx, qy, NODE_W, Q_H));
                qy += Q_H + V_GAP;
                var stages = Model.StageNodes.Where(s => s.quest == qi.quest).OrderBy(s => s.stageIndex).ToList();
                foreach (var si in stages)
                { if (_nodeMap.TryGetValue(si, out var sNode)) sNode.SetPosition(new Rect(qx, qy, NODE_W, STAGE_H)); qy += STAGE_H + V_GAP; }
                var reward = Model.RewardNodes.FirstOrDefault(r => r.quest == qi.quest);
                if (reward != null && _nodeMap.TryGetValue(reward, out var rNode)) rNode.SetPosition(new Rect(qx, qy, NODE_W, REWARD_H));
                x += NODE_W + H_GAP + 60f;
            }
        }

        // ── Callbacks ──

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null) foreach (var e in change.edgesToCreate) if (e != null) HandleEdgeCreated(e);
            if (change.elementsToRemove != null)
            {
                bool needRebuild = false;
                foreach (var el in change.elementsToRemove)
                {
                    if (el is Edge e) HandleEdgeDeleted(e);
                    else if (el is StageGraphNode sgn) { Debug.Log($"[GraphView] Delete key: {sgn.Quest.questId} stage idx={sgn.StageIndex}"); Model.DeleteStage(sgn.Info); needRebuild = true; }
                    else if (el is NpcGraphNode ngn) { Debug.Log($"[GraphView] Delete key: NPC {ngn.Npc.npcId} — removing from graph (SO untouched)"); }
                    else if (el is QuestRootGraphNode qgn) { Debug.Log($"[GraphView] Delete key: Quest {qgn.Quest.questId} — removing from graph (SO untouched)"); }
                    else if (el is DialogGraphNode dgn) { Debug.Log($"[GraphView] Delete key: Dialog node — removing from graph (SO untouched)"); }
                }
                if (needRebuild) schedule.Execute(() => Rebuild()).StartingIn(50);
            }
            return change;
        }


        private void HandleEdgeCreated(Edge edge)
        {
            if (edge.output?.node is not BaseGraphNode fn || edge.input?.node is not BaseGraphNode tn) { TryRemove(edge); return; }
            var fs = BaseGraphNode.GetSemantic(edge.output); var ts = BaseGraphNode.GetSemantic(edge.input);
            int ei = BaseGraphNode.GetPortData(edge.output) is int i ? i : -1;
            object fi = GetNodeInfo(fn), ti = GetNodeInfo(tn);
            if (fi == null || ti == null) { TryRemove(edge); return; }
            if (!Model.SetConnection(fi, fs, ti, ts, ei)) { TryRemove(edge); Debug.LogWarning($"[UG] Cannot connect {fs}→{ts}"); return; }
            edge.viewDataKey = "user-edge";
            edge.edgeControl.inputColor = edge.edgeControl.outputColor = fs switch { PortSemantic.DialogEdgeAction => GraphNodeColors.PortOrange, PortSemantic.StageTargetNpc => GraphNodeColors.PortBlue, _ => GraphNodeColors.PortGreen };
            if (fs == PortSemantic.DialogEdgeAction && fn is DialogGraphNode dgn) schedule.Execute(() => RebuildSingleDialogNode(dgn)).StartingIn(20);
            Debug.Log($"[UG] Connected {fs}→{ts}");
        }

        private void HandleEdgeDeleted(Edge edge)
        {
            if (edge.output?.node is not BaseGraphNode fn || edge.input?.node is not BaseGraphNode tn) return;
            var fs = BaseGraphNode.GetSemantic(edge.output); var ts = BaseGraphNode.GetSemantic(edge.input);
            int ei = BaseGraphNode.GetPortData(edge.output) is int i ? i : -1;
            object fi = GetNodeInfo(fn), ti = GetNodeInfo(tn);
            if (fi != null && ti != null) { Model.RemoveConnection(fi, fs, ti, ts, ei); if (fs == PortSemantic.DialogEdgeAction && fn is DialogGraphNode dgn) schedule.Execute(() => RebuildSingleDialogNode(dgn)).StartingIn(20); }
        }

        static object GetNodeInfo(BaseGraphNode n) => n switch
        { NpcGraphNode x => x.Info, DialogGraphNode x => x.Info, QuestRootGraphNode x => x.Info, StageGraphNode x => x.Info, RewardGraphNode x => x.Info, _ => null };

        static ScriptableObject GetNodeAsset(BaseGraphNode n) => n switch
        { NpcGraphNode x => x.Npc, DialogGraphNode x => x.Tree, QuestRootGraphNode x => x.Quest, StageGraphNode x => x.Quest, RewardGraphNode x => x.Quest, _ => null };

        private void RebuildSingleDialogNode(DialogGraphNode old)
        {
            var conn = edges.ToList().Where(e => e.output?.node == old || e.input?.node == old).ToList();
            foreach (var e in conn) RemoveElement(e);
            var pos = old.GetPosition(); RemoveElement(old); _nodeMap.Remove(old.Info);
            var nn = new DialogGraphNode(old.Info); nn.SetPosition(pos); AddElement(nn); _nodeMap[old.Info] = nn;
            Model.BuildGraph();
            foreach (var ei in Model.Edges)
                if (_nodeMap.TryGetValue(ei.fromNode, out var f) && _nodeMap.TryGetValue(ei.toNode, out var t) && (f == nn || t == nn))
                { var ve = CreateVisualEdge(ei); if (ve != null) AddElement(ve); }
            MarkDirtyRepaint();
        }

        static void TryRemove(Edge e) { try { if (e?.parent != null) e.parent.Remove(e); } catch { } }

        public override List<Port> GetCompatiblePorts(Port sp, NodeAdapter na)
        { var all = new List<Port>(); foreach (var p in ports) { if (p == sp || p.node == sp.node || p.direction == sp.direction) continue; all.Add(p); } return all; }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            if (evt.target is BaseGraphNode target)
            {
                var asset = GetNodeAsset(target); if (asset != null)
                { evt.menu.AppendSeparator(); evt.menu.AppendAction("📋 Select Asset", _ => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); });
                  evt.menu.AppendAction("📋 Duplicate Asset", _ => { var c = DuplicateAsset(asset); if (c is NpcDefinition nc) AddNpc(nc); else if (c is QuestDefinition qc) AddQuest(qc); else if (c is Dialogue.DialogTree dc) AddDialogTree(dc); }); }
                if (target is DialogGraphNode dgn) { evt.menu.AppendSeparator(); evt.menu.AppendAction("➕ Add Choice", _ => { var nd = dgn.DialogueNode; if (nd == null) return; var l = nd.edges?.ToList() ?? new List<Dialogue.DialogueEdge>(); l.Add(new Dialogue.DialogueEdge { label = "New Choice", hideIfUnavailable = true }); nd.edges = l.ToArray(); EditorUtility.SetDirty(dgn.Tree); Rebuild(); }); }
                if (target is StageGraphNode sgn) { evt.menu.AppendSeparator(); evt.menu.AppendAction("➕ Add Objective", _ => { Model.AddObjective(sgn.Info); Rebuild(); }); }
            }
            else
            { evt.menu.AppendSeparator(); evt.menu.AppendAction("🆕 New NPC...", _ => { var a = CreateNewNpcAsset(); if (a != null) AddNpc(a); });
              evt.menu.AppendAction("🆕 New Quest...", _ => { var a = CreateNewQuestAsset(); if (a != null) AddQuest(a); });
              evt.menu.AppendAction("🆕 New Dialog...", _ => { var a = CreateNewDialogAsset(); if (a != null) AddDialogTree(a); }); }
        }
    }

    // ═══════════════ Window ═══════════════

    public class UnifiedQuestGraphWindow : EditorWindow
    {
        private UnifiedQuestGraphView _graph;
        private ObjectField _npcF, _questF, _dialogF;
        private Button _editBtn;
        private Label _status;

        [MenuItem("Tools/Project C/Quests/Unified Quest Graph", priority = 100)]
        public static void Open() { var w = GetWindow<UnifiedQuestGraphWindow>(); w.titleContent = new GUIContent("Unified Quest Graph"); w.minSize = new Vector2(1200, 750); w.Show(); }

        private void OnEnable()
        {
            var root = rootVisualElement; root.Clear(); root.style.flexGrow = 1;
            var tb = new VisualElement() { style = { flexDirection = FlexDirection.Row, paddingTop = 4, paddingBottom = 4, paddingLeft = 6, paddingRight = 6, backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 1f)), flexWrap = Wrap.Wrap } };
            _npcF = new ObjectField("+NPC") { objectType = typeof(NpcDefinition), allowSceneObjects = false }; _npcF.style.width = 150;
            _npcF.RegisterValueChangedCallback(e => { if (e.newValue is NpcDefinition v) { _graph.AddNpc(v); _npcF.SetValueWithoutNotify(null); Upd(); } }); tb.Add(_npcF);
            _questF = new ObjectField("+Quest") { objectType = typeof(QuestDefinition), allowSceneObjects = false }; _questF.style.width = 150; _questF.style.marginLeft = 4;
            _questF.RegisterValueChangedCallback(e => { if (e.newValue is QuestDefinition v) { _graph.AddQuest(v); _questF.SetValueWithoutNotify(null); Upd(); } }); tb.Add(_questF);
            _dialogF = new ObjectField("+Dialog") { objectType = typeof(Dialogue.DialogTree), allowSceneObjects = false }; _dialogF.style.width = 150; _dialogF.style.marginLeft = 4;
            _dialogF.RegisterValueChangedCallback(e => { if (e.newValue is Dialogue.DialogTree v) { _graph.AddDialogTree(v); _dialogF.SetValueWithoutNotify(null); Upd(); } }); tb.Add(_dialogF);

            var sep = new Label("│"); sep.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f)); sep.style.marginLeft = 8; sep.style.marginRight = 4; tb.Add(sep);
            tb.Add(MkBtn("🆕 NPC", () => { var a = UnifiedQuestGraphView.CreateNewNpcAsset(); if (a != null) _graph.AddNpc(a); }));
            tb.Add(MkBtn("🆕 Quest", () => { var a = UnifiedQuestGraphView.CreateNewQuestAsset(); if (a != null) _graph.AddQuest(a); }));
            tb.Add(MkBtn("🆕 Dialog", () => { var a = UnifiedQuestGraphView.CreateNewDialogAsset(); if (a != null) _graph.AddDialogTree(a); }));

            var sp = new VisualElement(); sp.style.flexGrow = 1; tb.Add(sp);
            _editBtn = new Button(() => { if (_graph == null) return; _graph.EditMode = !_graph.EditMode; _editBtn.text = _graph.EditMode ? "🔒 View" : "✏️ Edit"; }) { text = "✏️ Edit" }; _editBtn.style.marginLeft = 4; tb.Add(_editBtn);
            tb.Add(MkBtn("💾 Save All", () => _graph?.SaveAll())); tb.Add(MkBtn("⊡ Fit", () => _graph?.FrameAll()));
            tb.Add(MkBtn("🔄", () => _graph?.Rebuild())); tb.Add(MkBtn("✕", () => { _graph?.ClearAll(); Upd(); }));
            root.Add(tb);

            _graph = new UnifiedQuestGraphView(); _graph.style.flexGrow = 1; root.Add(_graph);
            _status = new Label("Drop assets or use 🆕 buttons — right-click for menu");
            _status.style.position = Position.Absolute; _status.style.bottom = 4; _status.style.left = 6;
            _status.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f)); _status.style.fontSize = 10; root.Add(_status);
        }

        static Button MkBtn(string txt, Action act) { var b = new Button(act) { text = txt }; b.style.marginLeft = 2; b.style.fontSize = 10; return b; }

        void Upd() { if (_graph == null || _status == null) return; var m = _graph.Model; _status.text = $"NPCs:{m.NpcNodes.Count} Dialogs:{m.DialogNodes.Count} Quests:{m.QuestNodes.Count} Nodes:{_graph.nodes.ToList().Count} Edges:{_graph.edges.ToList().Count}"; }

        public void LoadUnified(QuestDefinition q, Dialogue.DialogTree d, NpcDefinition n = null) { if (n != null) _graph.AddNpc(n); if (d != null) _graph.AddDialogTree(d); if (q != null) _graph.AddQuest(q); Upd(); }
    }
}
#endif
