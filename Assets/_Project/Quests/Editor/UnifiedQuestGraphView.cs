// T-U05–T-U10: Unified Quest Graph — единый визуальный редактор квестов + диалогов.
// DialogNodeView, ConditionNodeView, UnifiedQuestGraphView, UnifiedQuestGraphWindow.
// v2: фикс NPC-only загрузки, layout dialog-нод, edit-режим.

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
    /// T-U05: DialogNodeView — синяя нода для DialogueNode.
    /// Поддерживает edit-режим: TextField для текста реплики.
    /// </summary>
    public class DialogNodeView : QuestGraphNode
    {
        public DialogueNode DialogueNode { get; private set; }
        public DialogTree DialogTree { get; private set; }

        private readonly List<Port> _outputPorts = new List<Port>();
        private Label _textLabel;
        private TextField _textField;
        private Label _speakerLabel;

        private static readonly Color DialogColor = new Color(0.3f, 0.5f, 1.0f);

        public DialogNodeView(DialogueNode node, DialogTree tree, int nodeIndex)
        {
            DialogueNode = node;
            DialogTree = tree;
            OwnerAsset = tree;
            SourceData = node;
            SourcePath = $"nodes[{nodeIndex}]";
            NodeKind = QuestNodeKind.Dialog;
            PersistKey = $"dlg_{tree.treeId}_{node.nodeId}";
            viewDataKey = PersistKey;

            // Title
            string speakerName = ResolveSpeakerName(node);
            string textPreview = node.text?.Length > 40 ? node.text.Substring(0, 37) + "..." : (node.text ?? "");
            title = $"🤖 {speakerName}: \"{textPreview}\"";

            titleContainer.style.backgroundColor = new StyleColor(DialogColor);

            // Content container
            var content = new VisualElement();
            content.style.paddingLeft = 8;
            content.style.paddingRight = 8;
            content.style.paddingTop = 4;
            content.style.paddingBottom = 4;

            // Speaker (label, always visible)
            _speakerLabel = new Label($"Speaker: {speakerName}");
            _speakerLabel.style.fontSize = 10;
            _speakerLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.95f, 1f));
            content.Add(_speakerLabel);

            // Text: Label (view mode) + TextField (edit mode)
            _textLabel = new Label(node.text ?? "");
            _textLabel.name = "editable-label";
            _textLabel.style.fontSize = 10;
            _textLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
            _textLabel.style.paddingTop = 4;
            _textLabel.style.whiteSpace = WhiteSpace.Normal;
            _textLabel.style.display = DisplayStyle.Flex;
            content.Add(_textLabel);

            _textField = new TextField("Text") { value = node.text ?? "", name = "editable-field", multiline = true };
            _textField.style.fontSize = 10;
            _textField.style.display = DisplayStyle.None;
            _textField.RegisterValueChangedCallback(evt =>
            {
                DialogueNode.text = evt.newValue;
                EditorUtility.SetDirty(DialogTree);
            });
            content.Add(_textField);

            extensionContainer.Add(content);

            // Input port
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
                var outPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                outPort.portName = "→ End";
                outPort.portColor = new Color(0.9f, 0.3f, 0.3f);
                outputContainer.Add(outPort);
                _outputPorts.Add(outPort);
            }

            RefreshExpandedState();
            expanded = true;
        }

        /// <summary>Toggle between view mode (Label) and edit mode (TextField).</summary>
        public void SetEditMode(bool edit)
        {
            _textLabel.style.display = edit ? DisplayStyle.None : DisplayStyle.Flex;
            _textField.style.display = edit ? DisplayStyle.Flex : DisplayStyle.None;
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
    /// T-U08: ConditionNodeView — жёлтая нода для условий if/else.
    /// 1 вход, 2 выхода: «✓ True» (зелёный), «✗ False» (красный).
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
            PersistKey = $"cond_{tree.treeId}_{System.Guid.NewGuid():N}";
            viewDataKey = PersistKey;

            var summaryParts = new List<string>();
            foreach (var c in Conditions)
                if (c != null) summaryParts.Add($"{c.type}");
            string summary = summaryParts.Count > 0 ? string.Join(" & ", summaryParts) : "Condition";

            title = $"🔷 {summary}";
            titleContainer.style.backgroundColor = new StyleColor(ConditionColor);

            var inPort = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            inPort.portName = "← In";
            inPort.portColor = new Color(0.55f, 0.55f, 0.55f);
            inputContainer.Add(inPort);

            var truePort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
            truePort.portName = "✓ True";
            truePort.portColor = new Color(0.2f, 0.8f, 0.3f);
            outputContainer.Add(truePort);

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
    /// T-U05: UnifiedQuestGraphView — Quest + Dialog в одном графе.
    /// </summary>
    public class UnifiedQuestGraphView : QuestNodeGraphView
    {
        public DialogTree DialogTree { get; private set; }
        public NpcDefinition NpcContext { get; private set; }

        private readonly Dictionary<string, DialogNodeView> _dialogNodes = new Dictionary<string, DialogNodeView>();
        private static readonly Color DialogQuestEdgeColor = new Color(0.9f, 0.5f, 0.1f);

        /// <summary>Toggle edit mode for all dialog nodes (quest nodes handled by base).</summary>
        public void SetDialogEditMode(bool edit)
        {
            foreach (var kvp in _dialogNodes)
                kvp.Value.SetEditMode(edit);
        }

        /// <summary>
        /// T-U06: Load quest + dialog tree (or just dialog, or just quest).
        /// </summary>
        public void LoadUnified(QuestDefinition quest, DialogTree dialogTree, NpcDefinition npcContext = null)
        {
            NpcContext = npcContext;
            DialogTree = dialogTree;

            // Load quest graph if provided
            if (quest != null)
                LoadQuest(quest);
            else
                ClearAllElements();

            // Load dialog nodes if provided
            if (dialogTree != null)
            {
                LoadDialogTree(dialogTree);
                if (quest != null)
                    CreateDialogQuestEdges(dialogTree);
            }

            // Layout
            ApplyUnifiedLayout();

            schedule.Execute(() =>
            {
                ForceAllNodesExpanded();
                MarkDirtyRepaint();
                FrameAll();
            }).StartingIn(100);
        }

        private void LoadDialogTree(DialogTree tree)
        {
            _dialogNodes.Clear();
            if (tree.nodes == null || tree.nodes.Length == 0) return;

            // Step 1: Create DialogNodeView per DialogueNode
            for (int i = 0; i < tree.nodes.Length; i++)
            {
                var node = tree.nodes[i];
                if (node == null) continue;

                var view = new DialogNodeView(node, tree, i);
                view.SetEditMode(false);
                _dialogNodes[node.nodeId] = view;
                AddElement(view);
            }

            // Step 2: Edges between dialog nodes
            foreach (var kvp in _dialogNodes)
            {
                var sourceView = kvp.Value;
                var sourceNode = sourceView.DialogueNode;
                if (sourceNode?.edges == null) continue;

                var outputPorts = sourceView.GetOutputPorts();
                for (int ei = 0; ei < sourceNode.edges.Length && ei < outputPorts.Count; ei++)
                {
                    var edge = sourceNode.edges[ei];
                    if (edge == null || string.IsNullOrEmpty(edge.targetNodeId)) continue;
                    if (!_dialogNodes.TryGetValue(edge.targetNodeId, out var targetView)) continue;

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

        private void CreateDialogQuestEdges(DialogTree tree)
        {
            if (tree.nodes == null || Quest?.stages == null || Quest.stages.Length == 0) return;

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
                    if (edge.action.GetQuestId() != Quest.questId) continue;

                    if (_dialogNodes.TryGetValue(node.nodeId, out var dialogView))
                    {
                        // Find which output port corresponds to this edge
                        var outputPorts = dialogView.GetOutputPorts();
                        int idx = System.Array.IndexOf(node.edges, edge);
                        Port dialogOut = idx >= 0 && idx < outputPorts.Count ? outputPorts[idx] : outputPorts.FirstOrDefault();
                        if (dialogOut != null)
                        {
                            var graphEdge = dialogOut.ConnectTo(stageInput);
                            graphEdge.viewDataKey = "dialog-quest";
                            graphEdge.edgeControl.inputColor = DialogQuestEdgeColor;
                            graphEdge.edgeControl.outputColor = DialogQuestEdgeColor;
                            AddElement(graphEdge);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// T-U03 ext: Position dialog nodes in a vertical list on the left,
        /// quest nodes in the center-right area.
        /// </summary>
        private void ApplyUnifiedLayout()
        {
            const float DLG_X = 0f;
            const float DLG_W = 320f;
            const float DLG_Y_START = 0f;
            const float DLG_Y_GAP = 20f;

            // Position dialog nodes: vertical stack on the left
            float dlgY = DLG_Y_START;
            foreach (var kvp in _dialogNodes.OrderBy(k => k.Key))
            {
                var view = kvp.Value;
                float h = 140f; // default height
                view.SetPosition(new Rect(DLG_X, dlgY, DLG_W, h));
                dlgY += h + DLG_Y_GAP;
            }

            // Quest nodes: already positioned by base ApplyAutoLayout,
            // but shift them right so they don't overlap with dialog nodes
            if (_dialogNodes.Count > 0)
            {
                float questShiftX = DLG_W + 60f;
                foreach (var n in this.nodes.ToList())
                {
                    if (n is QuestGraphNode qn &&
                        qn.NodeKind != QuestNodeKind.Dialog &&
                        qn.NodeKind != QuestNodeKind.Condition)
                    {
                        var pos = n.GetPosition();
                        if (pos.x < questShiftX)
                            n.SetPosition(new Rect(questShiftX, pos.y, pos.width, pos.height));
                    }
                }
            }
        }

        protected override void OnEdgeCreated(Edge edge)
        {
            base.OnEdgeCreated(edge);

            if (edge.output?.node is DialogNodeView dlgNode &&
                edge.input?.node is QuestGraphNode questNode &&
                questNode.NodeKind == QuestNodeKind.Stage)
            {
                edge.viewDataKey = "dialog-quest";
                edge.edgeControl.inputColor = DialogQuestEdgeColor;
                edge.edgeControl.outputColor = DialogQuestEdgeColor;

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
            _questField.style.width = 180;
            _questField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_questField);

            _dialogField = new UnityEditor.UIElements.ObjectField("Dialog")
                { objectType = typeof(DialogTree), allowSceneObjects = false };
            _dialogField.style.width = 180; _dialogField.style.marginLeft = 4;
            _dialogField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_dialogField);

            _npcField = new UnityEditor.UIElements.ObjectField("NPC")
                { objectType = typeof(NpcDefinition), allowSceneObjects = false };
            _npcField.style.width = 140; _npcField.style.marginLeft = 4;
            _npcField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_npcField);

            var fitBtn = new Button(() => _graph?.FrameAll()) { text = "⊡ Fit" };
            fitBtn.style.marginLeft = 6;
            toolbar.Add(fitBtn);

            _editBtn = new Button(() =>
            {
                if (_graph == null) return;
                _graph.EditMode = !_graph.EditMode;
                _graph.SetDialogEditMode(_graph.EditMode);
                _editBtn.text = _graph.EditMode ? "🔒 View" : "✏️ Edit";
                _saveBtn.style.display = _graph.EditMode ? DisplayStyle.Flex : DisplayStyle.None;
                _revertBtn.style.display = _graph.EditMode ? DisplayStyle.Flex : DisplayStyle.None;
            }) { text = "✏️ Edit" };
            _editBtn.style.marginLeft = 4;
            toolbar.Add(_editBtn);

            _saveBtn = new Button(() =>
            {
                _graph?.SaveQuest();
                if (_graph?.DialogTree != null)
                {
                    EditorUtility.SetDirty(_graph.DialogTree);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[UnifiedGraph] Saved DialogTree: {_graph.DialogTree.treeId}");
                }
            }) { text = "💾 Save All" };
            _saveBtn.style.marginLeft = 4;
            _saveBtn.style.display = DisplayStyle.None;
            toolbar.Add(_saveBtn);

            _revertBtn = new Button(() =>
            {
                var quest = _graph?.Quest;
                var dialog = _graph?.DialogTree;
                var npc = _graph?.NpcContext;
                if (quest != null)
                {
                    var path = AssetDatabase.GetAssetPath(quest);
                    var fresh = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                    var dialogPath = dialog != null ? AssetDatabase.GetAssetPath(dialog) : null;
                    var freshDialog = dialogPath != null ? AssetDatabase.LoadAssetAtPath<DialogTree>(dialogPath) : null;
                    _graph.LoadUnified(fresh, freshDialog, npc);
                    Debug.Log("[UnifiedGraph] Reverted");
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
            _statusLabel = new Label("Select NPC, Quest, or DialogTree to begin");
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.bottom = 4;
            _statusLabel.style.left = 6;
            _statusLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            _statusLabel.style.fontSize = 10;
            root.Add(_statusLabel);
        }

        /// <summary>
        /// Load graph for any combination of NPC / Quest / DialogTree.
        /// NPC → auto-resolves its DialogTree + all offered quests.
        /// </summary>
        private void TryLoadUnified()
        {
            var quest = _questField.value as QuestDefinition;
            var dialog = _dialogField.value as DialogTree;
            var npc = _npcField.value as NpcDefinition;

            // Case 1: NPC selected (with or without explicit quest/dialog)
            if (npc != null)
            {
                // Auto-resolve dialog tree from NPC
                if (dialog == null)
                {
                    dialog = npc.defaultDialogTree;
                    _dialogField.SetValueWithoutNotify(dialog);
                }

                // Auto-resolve quest from NPC's offerRefs if not explicitly chosen
                if (quest == null && npc.questOfferRefs != null && npc.questOfferRefs.Length > 0)
                {
                    quest = npc.questOfferRefs[0];
                    _questField.SetValueWithoutNotify(quest);
                }

                _graph.LoadUnified(quest, dialog, npc);
            }
            // Case 2: Quest + Dialog (explicit)
            else if (quest != null && dialog != null)
            {
                _graph.LoadUnified(quest, dialog, npc);
            }
            // Case 3: Quest only
            else if (quest != null)
            {
                // Try to find associated dialog tree
                DialogTree resolvedDialog = null;
                NpcDefinition resolvedNpc = null;
                var npcGuids = AssetDatabase.FindAssets("t:NpcDefinition");
                foreach (var guid in npcGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var candidate = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
                    if (candidate == null) continue;
                    var offerIds = candidate.GetQuestOfferIds();
                    if (offerIds != null && offerIds.Contains(quest.questId))
                    {
                        resolvedNpc = candidate;
                        resolvedDialog = candidate.defaultDialogTree;
                        break;
                    }
                }
                if (resolvedDialog != null)
                {
                    _dialogField.SetValueWithoutNotify(resolvedDialog);
                    _npcField.SetValueWithoutNotify(resolvedNpc);
                }
                _graph.LoadUnified(quest, resolvedDialog, resolvedNpc);
            }
            // Case 4: Dialog only
            else if (dialog != null)
            {
                // Try to find associated quest
                QuestDefinition resolvedQuest = null;
                if (dialog.nodes != null)
                {
                    foreach (var node in dialog.nodes)
                    {
                        if (node?.edges == null) continue;
                        foreach (var edge in node.edges)
                        {
                            if (edge?.action?.type == DialogueActionType.OfferQuest && edge.action.questRef != null)
                            {
                                resolvedQuest = edge.action.questRef;
                                break;
                            }
                        }
                        if (resolvedQuest != null) break;
                    }
                }
                if (resolvedQuest != null) _questField.SetValueWithoutNotify(resolvedQuest);
                _graph.LoadUnified(resolvedQuest, dialog, npc);
            }
            else
            {
                _graph.LoadUnified(null, null, null);
            }

            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            if (_graph == null || _statusLabel == null) return;
            int nodeCount = _graph.nodes.ToList().Count;
            int edgeCount = _graph.edges.ToList().Count;
            string questId = _graph.Quest?.questId ?? "—";
            string dialogId = _graph.DialogTree?.treeId ?? "—";
            string npcName = _graph.NpcContext?.displayName ?? "—";
            _statusLabel.text = $"NPC: {npcName}  |  Nodes: {nodeCount}  |  Edges: {edgeCount}  |  Quest: {questId}  |  Dialog: {dialogId}";
        }

        public void LoadUnified(QuestDefinition quest, DialogTree dialogTree, NpcDefinition npc = null)
        {
            _questField.SetValueWithoutNotify(quest);
            _dialogField.SetValueWithoutNotify(dialogTree);
            _npcField.SetValueWithoutNotify(npc);
            _graph?.LoadUnified(quest, dialogTree, npc);
            UpdateStatusBar();
        }
    }

    /// <summary>
    /// T-U10: Static integration helper — открывает Unified Quest Graph из редакторов.
    /// </summary>
    public static class UnifiedQuestGraphIntegration
    {
        public static void OpenUnified(QuestDefinition quest)
        {
            if (quest == null) return;

            DialogTree dialogTree = null;
            NpcDefinition npc = null;

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

        public static void OpenUnified(DialogTree dialogTree)
        {
            if (dialogTree == null) return;

            QuestDefinition quest = null;
            if (dialogTree.nodes != null)
            {
                foreach (var node in dialogTree.nodes)
                {
                    if (node?.edges == null) continue;
                    foreach (var edge in node.edges)
                    {
                        if (edge?.action?.type == DialogueActionType.OfferQuest && edge.action.questRef != null)
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
