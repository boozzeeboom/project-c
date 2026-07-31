// T-U05 v3: DialogNodeView с IMGUI — переиспользует PropertyDrawer'ы для drag-and-drop.
// T-U05: UnifiedQuestGraphView — Quest + Dialog в одном графе.
// T-U08: ConditionNodeView.
// T-U09: UnifiedQuestGraphWindow.
// T-U10: UnifiedQuestGraphIntegration.

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
    /// DialogNodeView v3 — GraphView Node с IMGUI-редактором внутри.
    /// Переиспользует SpeakerRefDrawer, DialogueConditionDrawer, DialogueActionDrawer.
    /// Редактирует DialogueNode напрямую через SerializedProperty.
    /// </summary>
    public class DialogNodeView : QuestGraphNode
    {
        public DialogueNode DialogueNode { get; private set; }
        public DialogTree DialogTree { get; private set; }
        public SerializedObject SerializedTree { get; private set; }
        public SerializedProperty NodeProperty { get; private set; }

        private readonly List<Port> _outputPorts = new List<Port>();
        private IMGUIContainer _editorArea;
        private VisualElement _contentArea;

        private static readonly Color DialogColor = new Color(0.3f, 0.5f, 1.0f);
        private const float MIN_EDITOR_HEIGHT = 200f;

        public DialogNodeView(DialogueNode node, DialogTree tree, int nodeIndex, SerializedProperty nodeProp)
        {
            DialogueNode = node;
            DialogTree = tree;
            OwnerAsset = tree;
            SourceData = node;
            SourcePath = $"nodes[{nodeIndex}]";
            NodeKind = QuestNodeKind.Dialog;
            PersistKey = $"dlg_{tree.treeId}_{node.nodeId}";
            viewDataKey = PersistKey;
            NodeProperty = nodeProp?.Copy();
            SerializedTree = nodeProp?.serializedObject;

            // Title
            string speakerName = ResolveSpeakerName(node);
            string textPreview = node.text?.Length > 50 ? node.text.Substring(0, 47) + "..." : (node.text ?? "");
            title = $"🤖 {speakerName}: \"{textPreview}\"";

            titleContainer.style.backgroundColor = new StyleColor(DialogColor);

            // Content area (below title, holds IMGUI editor when expanded)
            _contentArea = new VisualElement();
            _contentArea.style.paddingLeft = 4;
            _contentArea.style.paddingRight = 4;
            _contentArea.style.paddingTop = 2;
            _contentArea.style.paddingBottom = 4;
            extensionContainer.Add(_contentArea);

            // Input port
            var inPort = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            inPort.portName = "← In";
            inPort.portColor = new Color(0.55f, 0.55f, 0.55f);
            inputContainer.Add(inPort);

            // Output ports (one per DialogueEdge)
            RebuildOutputPorts();

            RebuildEditor();

            RefreshExpandedState();
            expanded = true;
        }

        /// <summary>Rebuild output ports from DialogueEdge array.</summary>
        public void RebuildOutputPorts()
        {
            // Clear old ports
            foreach (var p in _outputPorts)
            {
                if (p.parent != null) p.parent.Remove(p);
            }
            _outputPorts.Clear();

            var edges = DialogueNode?.edges;
            if (edges != null && edges.Length > 0)
            {
                foreach (var edge in edges)
                {
                    if (edge == null) continue;
                    var outPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                    string label = string.IsNullOrEmpty(edge.label) ? "→" : edge.label;
                    if (label.Length > 18) label = label.Substring(0, 15) + "...";
                    outPort.portName = label;
                    outPort.portColor = new Color(0.35f, 0.7f, 0.35f);
                    outputContainer.Add(outPort);
                    _outputPorts.Add(outPort);
                }
            }

            // Always have at least one output for dead-end
            if (_outputPorts.Count == 0)
            {
                var outPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                outPort.portName = "→ End";
                outPort.portColor = new Color(0.9f, 0.3f, 0.3f);
                outputContainer.Add(outPort);
                _outputPorts.Add(outPort);
            }
        }

        /// <summary>Rebuild the IMGUI editor area from SerializedProperty.</summary>
        public void RebuildEditor()
        {
            if (_editorArea != null)
                _contentArea.Remove(_editorArea);

            if (NodeProperty == null || SerializedTree == null)
            {
                // Fallback: simple text preview
                _editorArea = null;
                var textLabel = new Label(DialogueNode?.text ?? "(empty)");
                textLabel.style.fontSize = 10;
                textLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.95f, 1f));
                textLabel.style.whiteSpace = WhiteSpace.Normal;
                textLabel.style.paddingTop = 4;
                _contentArea.Add(textLabel);
                return;
            }

            _editorArea = new IMGUIContainer(DrawNodeEditor);
            _editorArea.style.minHeight = MIN_EDITOR_HEIGHT;
            _editorArea.style.flexGrow = 1;
            _contentArea.Add(_editorArea);
        }

        private void DrawNodeEditor()
        {
            if (NodeProperty == null || SerializedTree == null) return;

            SerializedTree.Update();

            var sp = NodeProperty.FindPropertyRelative("speaker");
            if (sp != null)
                EditorGUILayout.PropertyField(sp, new GUIContent("Speaker"), true);

            var textProp = NodeProperty.FindPropertyRelative("text");
            if (textProp != null)
                EditorGUILayout.PropertyField(textProp, new GUIContent("Text"));

            var emotionProp = NodeProperty.FindPropertyRelative("portraitEmotion");
            if (emotionProp != null)
                EditorGUILayout.PropertyField(emotionProp, new GUIContent("Portrait Emotion"));

            // On Enter Actions
            EditorGUILayout.Space(4);
            var onEnterProp = NodeProperty.FindPropertyRelative("onEnterActions");
            if (onEnterProp != null)
                EditorGUILayout.PropertyField(onEnterProp, new GUIContent("On Enter Actions"), true);

            // Edges
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Choices (player replies):", EditorStyles.boldLabel);

            var edgesProp = NodeProperty.FindPropertyRelative("edges");
            if (edgesProp != null && edgesProp.isArray)
            {
                for (int i = 0; i < edgesProp.arraySize; i++)
                {
                    var edgeProp = edgesProp.GetArrayElementAtIndex(i);
                    if (edgeProp == null) continue;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Edge label
                    EditorGUILayout.PropertyField(edgeProp.FindPropertyRelative("label"),
                        new GUIContent($"Choice {i+1}"));

                    // Target node
                    var targetProp = edgeProp.FindPropertyRelative("targetNodeId");
                    if (targetProp != null)
                    {
                        string currentTarget = targetProp.stringValue;
                        var nodeIds = GetSiblingNodeIds();
                        int selIdx = nodeIds.IndexOf(currentTarget);
                        int newIdx = EditorGUILayout.Popup("→ Target",
                            selIdx + 1,
                            ToTargetChoices(nodeIds));
                        if (newIdx != selIdx + 1)
                            targetProp.stringValue = (newIdx == 0) ? "" : nodeIds[newIdx - 1];
                    }

                    // Condition
                    var condProp = edgeProp.FindPropertyRelative("conditions");
                    if (condProp != null)
                        EditorGUILayout.PropertyField(condProp, new GUIContent("Conditions"), true);

                    // Action
                    var actionProp = edgeProp.FindPropertyRelative("action");
                    if (actionProp != null)
                        EditorGUILayout.PropertyField(actionProp, new GUIContent("Action"), true);

                    // Hide if unavailable
                    EditorGUILayout.PropertyField(edgeProp.FindPropertyRelative("hideIfUnavailable"),
                        new GUIContent("Hide if unavailable"));

                    // Delete edge button
                    if (GUILayout.Button("× Remove Choice", GUILayout.Width(120)))
                    {
                        edgesProp.DeleteArrayElementAtIndex(i);
                        SerializedTree.ApplyModifiedProperties();
                        EditorUtility.SetDirty(DialogTree);
                        RebuildOutputPorts();
                        GUIUtility.ExitGUI();
                        return;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(4);
                }


                // Add edge button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add Choice", GUILayout.Width(120)))
                {
                    edgesProp.arraySize++;
                    var newEdge = edgesProp.GetArrayElementAtIndex(edgesProp.arraySize - 1);
                    newEdge.FindPropertyRelative("label").stringValue = "New Choice";
                    newEdge.FindPropertyRelative("targetNodeId").stringValue = "";
                    newEdge.FindPropertyRelative("hideIfUnavailable").boolValue = true;
                    SerializedTree.ApplyModifiedProperties();
                    EditorUtility.SetDirty(DialogTree);
                    RebuildOutputPorts();
                    GUIUtility.ExitGUI();
                    return;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            bool changed = SerializedTree.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(DialogTree);
                // Update title preview
                string speakerName = ResolveSpeakerName(DialogueNode);
                string textPreview = DialogueNode.text?.Length > 50
                    ? DialogueNode.text.Substring(0, 47) + "..."
                    : (DialogueNode.text ?? "");
                title = $"🤖 {speakerName}: \"{textPreview}\"";
            }
        }

        private List<string> GetSiblingNodeIds()
        {
            var ids = new List<string>();
            if (DialogTree?.nodes == null) return ids;
            foreach (var n in DialogTree.nodes)
                if (n != null && !string.IsNullOrEmpty(n.nodeId))
                    ids.Add(n.nodeId);
            return ids;
        }

        private string[] ToTargetChoices(List<string> ids)
        {
            var result = new string[ids.Count + 1];
            result[0] = "(end conversation)";
            for (int i = 0; i < ids.Count; i++)
                result[i + 1] = ids[i];
            return result;
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

    // ═══════════════════════════════════════════════════════════
    // UnifiedQuestGraphView
    // ═══════════════════════════════════════════════════════════

    public class UnifiedQuestGraphView : QuestNodeGraphView
    {
        public DialogTree DialogTree { get; private set; }
        public NpcDefinition NpcContext { get; private set; }

        private readonly Dictionary<string, DialogNodeView> _dialogNodes = new Dictionary<string, DialogNodeView>();
        private SerializedObject _dialogSerializedObject;
        private static readonly Color DialogQuestEdgeColor = new Color(0.9f, 0.5f, 0.1f);

        /// <summary>
        /// Load quest + dialog tree. Any can be null.
        /// </summary>
        public void LoadUnified(QuestDefinition quest, DialogTree dialogTree, NpcDefinition npcContext = null)
        {
            NpcContext = npcContext;
            DialogTree = dialogTree;
            _dialogSerializedObject = dialogTree != null ? new SerializedObject(dialogTree) : null;

            if (quest != null)
                LoadQuest(quest);
            else
                ClearAllElements();

            if (dialogTree != null)
            {
                LoadDialogTree(dialogTree);
                if (quest != null)
                    CreateDialogQuestEdges(dialogTree);
            }

            ApplyUnifiedLayout();

            schedule.Execute(() =>
            {
                ForceAllNodesExpanded();
                MarkDirtyRepaint();
                FrameAll();
            }).StartingIn(100);
        }

        /// <summary>
        /// Add a new dialog node to the DialogTree + graph.
        /// </summary>
        public void AddDialogNode()
        {
            if (DialogTree == null)
            {
                Debug.LogWarning("[UnifiedGraph] No DialogTree loaded. Select a DialogTree first.");
                return;
            }

            // Create SO
            _dialogSerializedObject?.Update();
            var nodesProp = _dialogSerializedObject?.FindProperty("nodes");

            var list = DialogTree.nodes?.ToList() ?? new List<DialogueNode>();
            var newNode = new DialogueNode
            {
                nodeId = GenerateUniqueNodeId(list),
                speaker = new SpeakerRef { speakerKind = SpeakerRef.Kind.Npc },
                text = "",
                portraitEmotion = "neutral",
                edges = new DialogueEdge[0]
            };
            list.Add(newNode);
            DialogTree.nodes = list.ToArray();
            EditorUtility.SetDirty(DialogTree);
            _dialogSerializedObject = new SerializedObject(DialogTree);

            // Create view
            var nodesSp = _dialogSerializedObject.FindProperty("nodes");
            var nodeProp = nodesSp.GetArrayElementAtIndex(list.Count - 1);
            var view = new DialogNodeView(newNode, DialogTree, list.Count - 1, nodeProp);
            _dialogNodes[newNode.nodeId] = view;
            AddElement(view);

            // Rebuild all edges
            RebuildAllDialogEdges();

            ApplyUnifiedLayout();
            MarkDirtyRepaint();
        }

        private string GenerateUniqueNodeId(List<DialogueNode> existing)
        {
            string baseId = "new_node";
            if (!existing.Any(n => n?.nodeId == baseId)) return baseId;
            int counter = 2;
            while (existing.Any(n => n?.nodeId == $"{baseId}_{counter}"))
                counter++;
            return $"{baseId}_{counter}";
        }

        private void LoadDialogTree(DialogTree tree)
        {
            _dialogNodes.Clear();
            if (tree.nodes == null || tree.nodes.Length == 0) return;

            _dialogSerializedObject = new SerializedObject(tree);
            var nodesProp = _dialogSerializedObject.FindProperty("nodes");

            for (int i = 0; i < tree.nodes.Length; i++)
            {
                var node = tree.nodes[i];
                if (node == null) continue;

                var nodeProp = nodesProp.GetArrayElementAtIndex(i);
                var view = new DialogNodeView(node, tree, i, nodeProp);
                _dialogNodes[node.nodeId] = view;
                AddElement(view);
            }

            RebuildAllDialogEdges();
        }

        /// <summary>Rebuild ALL edges between dialog nodes from the DialogueEdge data.</summary>
        private void RebuildAllDialogEdges()
        {
            // Remove old dialog edges
            var oldEdges = this.edges.ToList()
                .Where(e => e.viewDataKey == "dialog")
                .ToList();
            foreach (var e in oldEdges) RemoveElement(e);

            // Rebuild
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

            var questStageNodes = this.nodes.ToList()
                .Where(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Stage)
                .Cast<QuestGraphNode>()
                .ToList();
            if (questStageNodes.Count == 0) return;

            // Remove old dialog-quest edges
            var oldDqEdges = this.edges.ToList()
                .Where(e => e.viewDataKey == "dialog-quest")
                .ToList();
            foreach (var e in oldDqEdges) RemoveElement(e);

            var stageInput = questStageNodes[0].inputContainer.Children().FirstOrDefault() as Port;
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

        private void ApplyUnifiedLayout()
        {
            const float DLG_X = 0f;
            const float DLG_W = 380f;
            const float DLG_Y_START = 0f;
            const float DLG_Y_GAP = 40f;

            float dlgY = DLG_Y_START;
            foreach (var kvp in _dialogNodes.OrderBy(k => k.Key))
            {
                var view = kvp.Value;
                float h = 400f; // tall enough for IMGUI editor
                view.SetPosition(new Rect(DLG_X, dlgY, DLG_W, h));
                dlgY += h + DLG_Y_GAP;
            }

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
                        dlgNode.RebuildOutputPorts();
                        EditorUtility.SetDirty(DialogTree);
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // UnifiedQuestGraphWindow
    // ═══════════════════════════════════════════════════════════

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
            w.minSize = new Vector2(1000, 700);
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
            _questField.style.width = 160;
            _questField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_questField);

            _dialogField = new UnityEditor.UIElements.ObjectField("Dialog")
                { objectType = typeof(DialogTree), allowSceneObjects = false };
            _dialogField.style.width = 160; _dialogField.style.marginLeft = 4;
            _dialogField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_dialogField);

            _npcField = new UnityEditor.UIElements.ObjectField("NPC")
                { objectType = typeof(NpcDefinition), allowSceneObjects = false };
            _npcField.style.width = 140; _npcField.style.marginLeft = 4;
            _npcField.RegisterValueChangedCallback(_ => TryLoadUnified());
            toolbar.Add(_npcField);

            // + Add Dialog Node
            var addDlgBtn = new Button(() => _graph?.AddDialogNode()) { text = "+ Dialog" };
            addDlgBtn.style.marginLeft = 8;
            addDlgBtn.style.fontSize = 10;
            toolbar.Add(addDlgBtn);

            var fitBtn = new Button(() => _graph?.FrameAll()) { text = "⊡ Fit" };
            fitBtn.style.marginLeft = 4;
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

            _saveBtn = new Button(() =>
            {
                _graph?.SaveQuest();
                if (_graph?.DialogTree != null)
                {
                    EditorUtility.SetDirty(_graph.DialogTree);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("[UnifiedGraph] Saved all.");
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
                    var qPath = AssetDatabase.GetAssetPath(quest);
                    var freshQuest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(qPath);
                    DialogTree freshDialog = null;
                    if (dialog != null)
                    {
                        var dPath = AssetDatabase.GetAssetPath(dialog);
                        freshDialog = AssetDatabase.LoadAssetAtPath<DialogTree>(dPath);
                    }
                    _graph?.LoadUnified(freshQuest, freshDialog, npc);
                }
            }) { text = "↩️ Revert" };
            _revertBtn.style.marginLeft = 4;
            _revertBtn.style.display = DisplayStyle.None;
            toolbar.Add(_revertBtn);

            root.Add(toolbar);

            _graph = new UnifiedQuestGraphView();
            _graph.style.flexGrow = 1;
            root.Add(_graph);

            _statusLabel = new Label("Select NPC, Quest, or DialogTree to begin");
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.bottom = 4; _statusLabel.style.left = 6;
            _statusLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            _statusLabel.style.fontSize = 10;
            root.Add(_statusLabel);
        }

        private void TryLoadUnified()
        {
            var quest = _questField.value as QuestDefinition;
            var dialog = _dialogField.value as DialogTree;
            var npc = _npcField.value as NpcDefinition;

            if (npc != null)
            {
                if (dialog == null)
                {
                    dialog = npc.defaultDialogTree;
                    _dialogField.SetValueWithoutNotify(dialog);
                }
                if (quest == null && npc.questOfferRefs != null && npc.questOfferRefs.Length > 0)
                {
                    quest = npc.questOfferRefs[0];
                    _questField.SetValueWithoutNotify(quest);
                }
                _graph.LoadUnified(quest, dialog, npc);
            }
            else if (quest != null && dialog != null)
            {
                _graph.LoadUnified(quest, dialog, npc);
            }
            else if (quest != null)
            {
                DialogTree resolvedDialog = null;
                NpcDefinition resolvedNpc = null;
                foreach (var guid in AssetDatabase.FindAssets("t:NpcDefinition"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var candidate = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
                    if (candidate == null) continue;
                    if (candidate.GetQuestOfferIds()?.Contains(quest.questId) == true)
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
            else if (dialog != null)
            {
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

    public static class UnifiedQuestGraphIntegration
    {
        public static void OpenUnified(QuestDefinition quest)
        {
            if (quest == null) return;
            DialogTree dialogTree = null;
            NpcDefinition npc = null;
            foreach (var guid in AssetDatabase.FindAssets("t:NpcDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
                if (candidate?.GetQuestOfferIds()?.Contains(quest.questId) == true)
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
                        { quest = edge.action.questRef; break; }
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
