// T-U05: UnifiedQuestGraphView — расширяет QuestNodeGraphView,
// добавляет DialogNodeView (синие ноды для диалоговых реплик).
// T-U06: загрузка DialogTree в граф.
// T-U07: связи Dialog↔Quest (пунктирные рёбра).
// T-U08: ConditionNodeView (жёлтая ромбовидная нода).

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    /// <summary>
    /// T-U05: DialogNodeView — GraphView Node representing a DialogueNode.
    /// Blue color, shows speaker + text preview, one input + N output ports.
    /// </summary>
    public class DialogNodeView : QuestGraphNode
    {
        public DialogueNode DialogueNode { get; private set; }
        public DialogTree DialogTree { get; private set; }

        private readonly List<Port> _outputPorts = new List<Port>();

        private static readonly Color DialogColor = new Color(0.3f, 0.5f, 1.0f);
        private const float PORT_HEIGHT = 20f;

        public DialogNodeView(DialogueNode node, DialogTree tree, int nodeIndex)
        {
            DialogueNode = node;
            DialogTree = tree;
            OwnerAsset = tree;
            SourceData = node;
            SourcePath = $"nodes[{nodeIndex}]";
            NodeKind = QuestNodeKind.Dialog;

            // Title: 🤖 speakerName: "text preview"
            string speakerName = ResolveSpeakerName(node);
            string textPreview = node.text?.Length > 40 ? node.text.Substring(0, 37) + "..." : (node.text ?? "");
            title = $"🤖 {speakerName}: \"{textPreview}\"";

            titleContainer.style.backgroundColor = new StyleColor(DialogColor);

            // Content: speaker + emotion
            var content = new VisualElement();
            content.style.paddingLeft = 8;
            content.style.paddingRight = 8;
            content.style.paddingTop = 4;
            content.style.paddingBottom = 4;

            var speakerLabel = new Label($"Speaker: {speakerName}");
            speakerLabel.style.fontSize = 10;
            speakerLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.95f, 1f));
            content.Add(speakerLabel);

            if (!string.IsNullOrEmpty(node.portraitEmotion))
            {
                var emotionLabel = new Label($"Emotion: {node.portraitEmotion}");
                emotionLabel.style.fontSize = 10;
                emotionLabel.style.color = new StyleColor(new Color(0.7f, 0.75f, 0.9f, 1f));
                content.Add(emotionLabel);
            }

            extensionContainer.Add(content);

            // Input port (one)
            var inPort = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            inPort.portName = "← In";
            inPort.portColor = new Color(0.55f, 0.55f, 0.55f);
            inputContainer.Add(inPort);

            // Output ports (one per DialogueEdge)
            if (node.edges != null && node.edges.Length > 0)
            {
                foreach (var edge in node.edges)
                {
                    if (edge == null) continue;
                    var outPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                    string label = string.IsNullOrEmpty(edge.label) ? "→ Continue" : $"→ {edge.label}";
                    if (label.Length > 20) label = label.Substring(0, 17) + "...";
                    outPort.portName = label;
                    outPort.portColor = new Color(0.35f, 0.70f, 0.35f);
                    outputContainer.Add(outPort);
                    _outputPorts.Add(outPort);
                }
            }
            else
            {
                // Dead end: one output for visual consistency
                var outPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                outPort.portName = "→ End";
                outPort.portColor = new Color(0.9f, 0.3f, 0.3f);
                outputContainer.Add(outPort);
                _outputPorts.Add(outPort);
            }

            RefreshExpandedState();
            expanded = true;
        }

        public IReadOnlyList<Port> GetOutputPorts() => _outputPorts;

        private static string ResolveSpeakerName(DialogueNode node)
        {
            if (node?.speaker == null) return "???";
            if (node.speaker.speakerNpc != null)
                return node.speaker.speakerNpc.displayName;
            if (!string.IsNullOrEmpty(node.speaker.refId))
                return node.speaker.refId;
            return node.speaker.speakerKind.ToString();
        }
    }

    /// <summary>
    /// T-U08: ConditionNodeView — diamond-shaped node for if/else branching.
    /// Yellow color, 1 input, 2 outputs: "True" (green), "False" (red).
    /// </summary>
    public class ConditionNodeView : QuestGraphNode
    {
        public DialogueCondition[] Conditions { get; private set; }

        private static readonly Color ConditionColor = new Color(0.9f, 0.7f, 0.2f);

        public ConditionNodeView(DialogueCondition[] conditions, DialogTree tree)
        {
            Conditions = conditions ?? new DialogueCondition[0];
            OwnerAsset = tree;
            SourceData = conditions;
            NodeKind = QuestNodeKind.Condition;

            // Build condition summary
            var summaryParts = new List<string>();
            foreach (var c in Conditions)
            {
                if (c == null) continue;
                summaryParts.Add($"{c.type}");
            }
            string summary = summaryParts.Count > 0 ? string.Join(" & ", summaryParts) : "Condition";

            title = $"🔷 {summary}";
            titleContainer.style.backgroundColor = new StyleColor(ConditionColor);

            // Input port
            var inPort = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            inPort.portName = "← In";
            inPort.portColor = new Color(0.55f, 0.55f, 0.55f);
            inputContainer.Add(inPort);

            // True output (green)
            var truePort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
            truePort.portName = "✓ True";
            truePort.portColor = new Color(0.2f, 0.8f, 0.3f);
            outputContainer.Add(truePort);

            // False output (red)
            var falsePort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
            falsePort.portName = "✗ False";
            falsePort.portColor = new Color(0.9f, 0.25f, 0.25f);
            outputContainer.Add(falsePort);

            RefreshExpandedState();
            expanded = true;
        }

        public Port GetTruePort()
        {
            foreach (var child in outputContainer.Children())
                if (child is Port p && p.portName == "✓ True") return p;
            return null;
        }

        public Port GetFalsePort()
        {
            foreach (var child in outputContainer.Children())
                if (child is Port p && p.portName == "✗ False") return p;
            return null;
        }
    }

    /// <summary>
    /// T-U05: UnifiedQuestGraphView — Quest + Dialog nodes in one graph.
    /// T-U06: Loads DialogTree into the graph, places dialog nodes above quest nodes.
    /// T-U07: Dashed edges for Dialog↔Quest connections (OfferQuest).
    /// </summary>
    public class UnifiedQuestGraphView : QuestNodeGraphView
    {
        public DialogTree DialogTree { get; private set; }
        public NpcDefinition NpcContext { get; private set; }

        // Dialog node tracking
        private readonly Dictionary<string, DialogNodeView> _dialogNodes = new Dictionary<string, DialogNodeView>();
        private readonly List<ConditionNodeView> _conditionNodes = new List<ConditionNodeView>();

        private static readonly Color DialogQuestEdgeColor = new Color(0.9f, 0.5f, 0.1f);

        /// <summary>
        /// T-U06: Load a quest + its associated DialogTree into the unified graph.
        /// </summary>
        public void LoadUnified(QuestDefinition quest, DialogTree dialogTree, NpcDefinition npcContext = null)
        {
            NpcContext = npcContext;
            DialogTree = dialogTree;

            // First load the quest graph (base class)
            LoadQuest(quest);

            // Then load dialog nodes
            if (dialogTree != null)
            {
                LoadDialogTree(dialogTree);
                // T-U07: Create dashed edges for OfferQuest actions
                CreateDialogQuestEdges(dialogTree);
            }

            // Re-layout everything
            ApplyUnifiedLayout();

            schedule.Execute(() => { MarkDirtyRepaint(); FrameAll(); }).StartingIn(100);
        }

        /// <summary>
        /// T-U06: Load DialogTree nodes into the graph as DialogNodeView instances.
        /// </summary>
        private void LoadDialogTree(DialogTree tree)
        {
            _dialogNodes.Clear();
            _conditionNodes.Clear();

            if (tree.nodes == null || tree.nodes.Length == 0) return;

            // Step 1: Create DialogNodeView for each DialogueNode
            for (int i = 0; i < tree.nodes.Length; i++)
            {
                var node = tree.nodes[i];
                if (node == null) continue;

                var view = new DialogNodeView(node, tree, i);
                _dialogNodes[node.nodeId] = view;
                AddElement(view);
            }

            // Step 2: Create edges between dialog nodes based on DialogueEdge.targetNodeId
            foreach (var kvp in _dialogNodes)
            {
                var sourceView = kvp.Value;
                var sourceNode = sourceView.DialogueNode;
                if (sourceNode?.edges == null) continue;

                var outputPorts = sourceView.GetOutputPorts();
                for (int ei = 0; ei < sourceNode.edges.Length; ei++)
                {
                    var edge = sourceNode.edges[ei];
                    if (edge == null || string.IsNullOrEmpty(edge.targetNodeId)) continue;
                    if (!_dialogNodes.TryGetValue(edge.targetNodeId, out var targetView)) continue;

                    if (ei < outputPorts.Count)
                    {
                        var targetInput = targetView.inputContainer.Children().FirstOrDefault() as Port;
                        if (targetInput != null)
                        {
                            var graphEdge = outputPorts[ei].ConnectTo(targetInput);
                            graphEdge.viewDataKey = "dialog";
                            graphEdge.edgeControl.inputColor = new Color(0.35f, 0.5f, 0.9f);
                            graphEdge.edgeControl.outputColor = new Color(0.35f, 0.5f, 0.9f);
                            AddElement(graphEdge);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// T-U07: Create dashed orange edges from DialogNode to QuestStageNode
        /// when a DialogueEdge has action.type == OfferQuest.
        /// </summary>
        private void CreateDialogQuestEdges(DialogTree tree)
        {
            if (tree.nodes == null || Quest?.stages == null || Quest.stages.Length == 0) return;

            // Find first stage node
            var questStageNode = this.nodes.ToList()
                .FirstOrDefault(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Stage);
            if (questStageNode == null) return;

            var stageInput = questStageNode.inputContainer.Children().FirstOrDefault() as Port;
            if (stageInput == null) return;

            foreach (var node in tree.nodes)
            {
                if (node?.edges == null) continue;
                foreach (var edge in node.edges)
                {
                    if (edge?.action == null) continue;
                    if (edge.action.type != DialogueActionType.OfferQuest) continue;

                    string questId = edge.action.GetQuestId();
                    if (questId != Quest.questId) continue;

                    // Found a dialog → quest link
                    if (_dialogNodes.TryGetValue(node.nodeId, out var dialogView))
                    {
                        var dialogOut = dialogView.GetOutputPorts().FirstOrDefault();
                        if (dialogOut != null)
                        {
                            var graphEdge = dialogOut.ConnectTo(stageInput);
                            graphEdge.viewDataKey = "dialog-quest";
                            graphEdge.edgeControl.inputColor = DialogQuestEdgeColor;
                            graphEdge.edgeControl.outputColor = DialogQuestEdgeColor;
                            AddElement(graphEdge);
                        }
                    }
                    break; // one edge per node
                }
            }
        }

        /// <summary>
        /// T-U03 extension: position dialog nodes above quest nodes (higher Y = more negative).
        /// </summary>
        private void ApplyUnifiedLayout()
        {
            // Position dialog nodes in row above quest nodes
            float dlgX = 0f;
            const float DlgYGap = 20f;
            const float QuestDlgGap = 120f;

            // Find the minimum Y of quest nodes
            float questMinY = float.MaxValue;
            foreach (var n in this.nodes.ToList())
            {
                if (n is QuestGraphNode qn && qn.NodeKind != QuestNodeKind.Dialog && qn.NodeKind != QuestNodeKind.Condition)
                {
                    var pos = n.GetPosition();
                    if (pos.y < questMinY) questMinY = pos.y;
                }
            }
            if (questMinY > 10000f) questMinY = 0f;

            // Place dialog nodes above quest nodes
            foreach (var kvp in _dialogNodes.OrderBy(k => k.Key))
            {
                var view = kvp.Value;
                float h = view.GetPosition().height > 0 ? view.GetPosition().height : 120f;
                view.SetPosition(new Rect(dlgX, questMinY - h - QuestDlgGap, 300f, h));
                dlgX += 300f + DlgYGap;
            }

            // Shift all quest nodes down if dialog nodes overlap
            if (_dialogNodes.Count > 0 && questMinY < 200f)
            {
                foreach (var n in this.nodes.ToList())
                {
                    if (n is QuestGraphNode qn && qn.NodeKind != QuestNodeKind.Dialog && qn.NodeKind != QuestNodeKind.Condition)
                    {
                        var pos = n.GetPosition();
                        n.SetPosition(new Rect(pos.x, pos.y + QuestDlgGap, pos.width, pos.height));
                    }
                }
            }
        }

        // ── T-U07: Override OnEdgeCreated for dialog→quest linking ──

        protected override void OnEdgeCreated(Edge edge)
        {
            base.OnEdgeCreated(edge);

            // Check if this is a Dialog → Quest connection
            if (edge.output?.node is DialogNodeView dlgNode &&
                edge.input?.node is QuestGraphNode questNode &&
                questNode.NodeKind == QuestNodeKind.Stage)
            {
                edge.viewDataKey = "dialog-quest";
                edge.edgeControl.inputColor = DialogQuestEdgeColor;
                edge.edgeControl.outputColor = DialogQuestEdgeColor;

                // Auto-create DialogueEdge with OfferQuest action
                if (DialogTree != null && Quest != null)
                {
                    var dialogNode = dlgNode.DialogueNode;
                    if (dialogNode != null)
                    {
                        var edgesList = dialogNode.edges?.ToList() ?? new List<DialogueEdge>();
                        edgesList.Add(new DialogueEdge
                        {
                            label = $"Offer: {Quest.displayName}",
                            action = new DialogueAction
                            {
                                type = DialogueActionType.OfferQuest,
                                questRef = Quest,
                                stringParam = Quest.questId
                            }
                        });
                        dialogNode.edges = edgesList.ToArray();
                        EditorUtility.SetDirty(DialogTree);

                        Debug.Log($"[UnifiedGraph] Created OfferQuest edge: {dialogNode.nodeId} → {Quest.questId}");
                    }
                }
            }
        }
    }

    // ===== T-U09: UnifiedQuestGraphWindow =====

    public class UnifiedQuestGraphWindow : EditorWindow
    {
        private UnifiedQuestGraphView _graph;
        private UnityEditor.UIElements.ObjectField _questField;
        private UnityEditor.UIElements.ObjectField _dialogField;
        private UnityEditor.UIElements.ObjectField _npcField;
        private Button _editBtn;
        private Button _saveBtn;
        private Button _revertBtn;
        private Label _statusLabel;

        [MenuItem("Tools/Project C/Quests/Unified Quest Graph", priority = 100)]
        public static void Open()
        {
            var w = GetWindow<UnifiedQuestGraphWindow>();
            w.titleContent = new GUIContent("Unified Quest Graph");
            w.minSize = new Vector2(900, 600);
            w.Show();
        }

        private void OnEnable()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

            // ── Toolbar ──
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.paddingTop = 4; toolbar.style.paddingBottom = 4;
            toolbar.style.paddingLeft = 6; toolbar.style.paddingRight = 6;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 1f));

            _questField = new UnityEditor.UIElements.ObjectField("Quest")
                { objectType = typeof(QuestDefinition), allowSceneObjects = false };
            _questField.style.width = 200;
            _questField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_questField);

            _dialogField = new UnityEditor.UIElements.ObjectField("Dialog")
                { objectType = typeof(DialogTree), allowSceneObjects = false };
            _dialogField.style.width = 200; _dialogField.style.marginLeft = 4;
            _dialogField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_dialogField);

            _npcField = new UnityEditor.UIElements.ObjectField("NPC")
                { objectType = typeof(NpcDefinition), allowSceneObjects = false };
            _npcField.style.width = 160; _npcField.style.marginLeft = 4;
            toolbar.Add(_npcField);

            var fitBtn = new Button(() => _graph?.FrameAll()) { text = "⊡ Fit" };
            fitBtn.style.marginLeft = 6;
            toolbar.Add(fitBtn);

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

            _saveBtn = new Button(() => _graph?.SaveQuest()) { text = "💾 Save All" };
            _saveBtn.style.marginLeft = 4;
            _saveBtn.style.display = DisplayStyle.None;
            toolbar.Add(_saveBtn);

            _revertBtn = new Button(() =>
            {
                if (_graph?.Quest != null)
                {
                    var path = AssetDatabase.GetAssetPath(_graph.Quest);
                    var fresh = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                    if (fresh != null) _graph.LoadQuest(fresh);
                }
            }) { text = "↩️ Revert" };
            _revertBtn.style.marginLeft = 4;
            _revertBtn.style.display = DisplayStyle.None;
            toolbar.Add(_revertBtn);

            root.Add(toolbar);

            // ── Graph view ──
            _graph = new UnifiedQuestGraphView();
            _graph.style.flexGrow = 1;
            root.Add(_graph);

            // ── Status bar ──
            _statusLabel = new Label("Open a Quest + DialogTree to begin");
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.bottom = 4;
            _statusLabel.style.left = 6;
            _statusLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            _statusLabel.style.fontSize = 10;
            root.Add(_statusLabel);

            _graph.RegisterCallback<GeometryChangedEvent>(_ => UpdateStatusBar());
        }

        private void TryLoadUnified()
        {
            var quest = _questField.value as QuestDefinition;
            var dialog = _dialogField.value as DialogTree;
            var npc = _npcField.value as NpcDefinition;

            if (quest != null && dialog != null)
            {
                _graph.LoadUnified(quest, dialog, npc);
                UpdateStatusBar();
            }
            else if (quest != null && dialog == null)
            {
                // T-U06: auto-resolve DialogTree from NPC
                if (npc != null && npc.defaultDialogTree != null)
                {
                    _dialogField.value = npc.defaultDialogTree;
                    _graph.LoadUnified(quest, npc.defaultDialogTree, npc);
                }
                else
                {
                    _graph.LoadQuest(quest);
                }
                UpdateStatusBar();
            }
        }

        private void UpdateStatusBar()
        {
            if (_graph == null || _statusLabel == null) return;
            int nodeCount = _graph.nodes.ToList().Count;
            int edgeCount = _graph.edges.ToList().Count;
            string questId = _graph.Quest?.questId ?? "—";
            string dialogId = _graph.DialogTree?.treeId ?? "—";
            _statusLabel.text = $"Nodes: {nodeCount}  |  Edges: {edgeCount}  |  Quest: {questId}  |  Dialog: {dialogId}";
        }

        public void LoadUnified(QuestDefinition quest, DialogTree dialogTree, NpcDefinition npc = null)
        {
            if (_questField != null) _questField.value = quest;
            if (_dialogField != null) _dialogField.value = dialogTree;
            if (_npcField != null) _npcField.value = npc;
            if (_graph != null) _graph.LoadUnified(quest, dialogTree, npc);
            UpdateStatusBar();
        }
    }

    /// <summary>
    /// T-U10: Static integration helper — opens the Unified Quest Graph from any editor.
    /// Resolves the DialogTree from the quest's associated NPCs if not provided directly.
    /// </summary>
    public static class UnifiedQuestGraphIntegration
    {
        /// <summary>Open unified graph for a QuestDefinition (auto-resolves DialogTree from NPCs).</summary>
        public static void OpenUnified(QuestDefinition quest)
        {
            if (quest == null) return;

            DialogTree dialogTree = null;
            NpcDefinition npc = null;

            // Search all NpcDefinitions for one that offers this quest
            var npcGuids = AssetDatabase.FindAssets("t:NpcDefinition");
            foreach (var guid in npcGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
                if (candidate == null) continue;

                var offerIds = candidate.GetQuestOfferIds();
                if (offerIds != null && offerIds.Contains(quest.questId))
                {
                    npc = candidate;
                    dialogTree = candidate.defaultDialogTree;
                    break;
                }
            }

            var w = EditorWindow.GetWindow<UnifiedQuestGraphWindow>();
            w.titleContent = new GUIContent($"Unified: {quest.questId}");
            w.LoadUnified(quest, dialogTree, npc);
            w.Show();
        }

        /// <summary>Open unified graph for a DialogTree (auto-resolves Quest from OfferQuest edges).</summary>
        public static void OpenUnified(DialogTree dialogTree)
        {
            if (dialogTree == null) return;

            // Find the quest this dialog links to via OfferQuest
            QuestDefinition quest = null;
            if (dialogTree.nodes != null)
            {
                foreach (var node in dialogTree.nodes)
                {
                    if (node?.edges == null) continue;
                    foreach (var edge in node.edges)
                    {
                        if (edge?.action != null &&
                            edge.action.type == DialogueActionType.OfferQuest &&
                            edge.action.questRef != null)
                        {
                            quest = edge.action.questRef;
                            break;
                        }
                    }
                    if (quest != null) break;
                }
            }

            var w = EditorWindow.GetWindow<UnifiedQuestGraphWindow>();
            w.titleContent = new GUIContent($"Unified: {dialogTree.treeId}");
            w.LoadUnified(quest, dialogTree, null);
            w.Show();
        }
    }
}
#endif


