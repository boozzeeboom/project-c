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

namespace ProjectC.Quests.Editor
{
    public class QuestNodeGraphView : GraphView
    {
        public QuestDefinition Quest { get; private set; }
        private bool _suppressReadOnly;

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
        }

        private void BuildGraph()
        {
            if (Quest == null) return;

            const float QUEST_W = 240f, QUEST_H = 120f;
            const float STAGE_W = 240f, STAGE_H = 150f;
            const float OBJ_W = 220f, OBJ_H = 90f;
            const float REWARD_W = 240f, REWARD_H = 120f;
            const float COL1_X = 0f, COL2_X = 360f, COL3_X = 720f, COL4_X = 1100f;
            const float Y_TOP = 0f;
            const float ROW_GAP = 30f;

            var questColors = new Color(0.20f, 0.35f, 0.60f);
            var stageColors = new Color(0.20f, 0.55f, 0.30f);
            var objColors = new Color(0.55f, 0.40f, 0.10f);
            var rewardColors = new Color(0.65f, 0.40f, 0.10f);

            // Build nodes + edges
            var allPorts = new Dictionary<VisualElement, (Port input, Port output)>();

            // Quest node
            var questNode = MakeNode("📜 QUEST", questColors,
                $"{Quest.displayName}",
                $"stages: {Quest.stages?.Length ?? 0}  •  id: {Quest.questId}");
            questNode.SetPosition(new Rect(COL1_X, Y_TOP, QUEST_W, QUEST_H));
            AddElement(questNode);
            var qPort = AddPorts(questNode, hasOutput: true, hasInput: false);

            int stageCount = Quest.stages?.Length ?? 0;
            var stageNodes = new List<Node>();
            float y = Y_TOP;

            if (Quest.stages != null)
            {
                for (int i = 0; i < Quest.stages.Length; i++)
                {
                    var stage = Quest.stages[i];
                    if (stage == null) continue;

                    var lines = new List<string> { $"<b>{stage.stageId}</b>" };
                    if (!string.IsNullOrEmpty(stage.description)) lines.Add(stage.description);
                    if (stage.objectives != null) lines.Add($"🎯 {stage.objectives.Length} objective(s)");
                    if (stage.onEnterActions != null && stage.onEnterActions.Length > 0)
                        lines.Add($"▶ onEnter: {stage.onEnterActions.Length} act");
                    if (stage.onCompleteActions != null && stage.onCompleteActions.Length > 0)
                        lines.Add($"✓ onComplete: {stage.onCompleteActions.Length} act");
                    if (!string.IsNullOrEmpty(stage.nextStageId))
                        lines.Add($"→ next: {stage.nextStageId}");

                    var sn = MakeNode($"STAGE {i+1}/{stageCount}", stageColors, string.Join("\n", lines), "");
                    sn.SetPosition(new Rect(COL2_X, y, STAGE_W, STAGE_H));
                    AddElement(sn);
                    var sPort = AddPorts(sn, hasOutput: true, hasInput: true);

                    // Connect quest → stage 0
                    if (i == 0) ConnectPorts(qPort.output, sPort.input);
                    // Connect stage i-1 → stage i
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

                            var oLines = new List<string> { $"[{obj.objectiveType}] ×{obj.requiredQuantity}" };
                            if (!string.IsNullOrEmpty(obj.itemTradeItemId)) oLines.Add($"📦 item: {obj.itemTradeItemId}");
                            if (!string.IsNullOrEmpty(obj.targetNpcId)) oLines.Add($"👤 npc: {obj.targetNpcId}");

                            var on = MakeNode($"🎯 {obj.objectiveId}", objColors, string.Join("\n", oLines), "");
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

            // Reward — column 4, same row as first stage
            if (Quest.rewards != null && HasReward(Quest.rewards))
            {
                var r = Quest.rewards;
                var rLines = new List<string>();
                if (r.credits > 0) rLines.Add($"💰 {r.credits} CR");
                if (r.items != null) foreach (var it in r.items) rLines.Add($"📦 Item ×{it.count}");
                if (r.reputation != null) foreach (var rep in r.reputation) rLines.Add($"📈 <b>{rep.faction}</b> +{rep.value}");

                var rn = MakeNode("🎁 REWARDS", rewardColors, string.Join("\n", rLines), "");
                rn.SetPosition(new Rect(COL4_X, Y_TOP, REWARD_W, REWARD_H));
                AddElement(rn);
                var rPort = AddPorts(rn, hasOutput: false, hasInput: true);
                if (stageNodes.Count > 0)
                    ConnectPorts(GetOutputPort(stageNodes[stageNodes.Count-1]), rPort.input);
            }
        }

        private Node MakeNode(string title, Color titleColor, string body, string meta)
        {
            var n = new Node { title = title };
            n.titleContainer.style.backgroundColor = new StyleColor(titleColor);
            n.extensionContainer.style.backgroundColor = new StyleColor(titleColor * 0.6f);
            if (!string.IsNullOrEmpty(body))
            {
                var bodyLbl = new Label(body);
                bodyLbl.style.fontSize = 10;
                bodyLbl.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                bodyLbl.style.paddingLeft = 8;
                bodyLbl.style.paddingTop = 4;
                bodyLbl.style.paddingRight = 4;
                bodyLbl.style.paddingBottom = 4;
                bodyLbl.style.whiteSpace = WhiteSpace.Normal;
                n.extensionContainer.Add(bodyLbl);
            }
            n.RefreshExpandedState();
            n.expanded = true;
            return n;
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

        private void ConnectPorts(Port output, Port input)
        {
            if (output == null || input == null) return;
            var edge = output.ConnectTo(input);
            AddElement(edge);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_suppressReadOnly) return change;
            if (change.elementsToRemove != null) change.elementsToRemove.Clear();
            if (change.edgesToCreate != null) change.edgesToCreate.Clear();
            if (change.movedElements != null) change.movedElements.Clear();
            return change;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) => new List<Port>();

        private static bool HasReward(QuestReward r) => r != null && (r.credits > 0 || (r.items != null && r.items.Length > 0) || (r.reputation != null && r.reputation.Length > 0));
    }

    // ===== Window =====

    public class QuestNodeGraphWindow : EditorWindow
    {
        private QuestNodeGraphView _graph;
        private UnityEditor.UIElements.ObjectField _questField;

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
