// DialogTreeEditor — кастомный Editor для DialogTree с карточками нод.
// T-QUEDIT v2: легенда цветов, поясняющие хелп-боксы, читаемые лейблы.
// Без гайда должно быть понятно что какой элемент делает.
//
// См. docs/NPC_quests/DIALOGTREE_EDITOR_v2.md

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    [CustomEditor(typeof(DialogTree))]
    public class DialogTreeEditor : UnityEditor.Editor
    {
        private bool[] _nodeExpanded;
        private bool _showLegend = true;

        // ── Colors ──
        private static readonly Color NpcColor    = new Color(0.3f, 0.5f, 1.0f, 1f);
        private static readonly Color PlayerColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        private static readonly Color NarrColor   = new Color(0.8f, 0.8f, 0.3f, 1f);
        private static readonly Color EndColor    = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color HeaderBg    = new Color(0.18f, 0.22f, 0.32f, 1f);

        public override void OnInspectorGUI()
        {
            var tree = (DialogTree)target;
            if (tree == null) return;

            serializedObject.Update();
            SyncExpandedState(tree);

            DrawHeader(tree);
            EditorGUILayout.Space(6);
            DrawLegend();
            EditorGUILayout.Space(4);
            DrawSummary(tree);
            EditorGUILayout.Space(8);
            DrawIdentity(tree);
            EditorGUILayout.Space(8);

            // ── Nodes ──
            EditorGUILayout.LabelField("Dialogue Nodes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Каждая нода — одна реплика в диалоге.\n" +
                "Рёбра (edges) — варианты ответа игрока, которые ведут к следующей реплике или завершают разговор.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            var nodesProp = serializedObject.FindProperty("nodes");
            int nodeCount = tree.nodes?.Length ?? 0;

            for (int i = 0; i < nodeCount; i++)
            {
                var nodeProp = nodesProp.GetArrayElementAtIndex(i);
                if (nodeProp == null) continue;
                var node = tree.nodes[i];
                DrawNodeCard(node, nodeProp, i, tree.rootNodeId);
            }

            // ── Add Node button ──
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Node", GUILayout.Width(130), GUILayout.Height(28)))
            {
                nodesProp.arraySize++;
                var newProp = nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1);
                newProp.FindPropertyRelative("nodeId").stringValue = "new_node";
                newProp.FindPropertyRelative("text").stringValue = "";
                newProp.FindPropertyRelative("speaker").FindPropertyRelative("speakerKind").enumValueIndex = 0;
                newProp.FindPropertyRelative("portraitEmotion").stringValue = "neutral";
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed) EditorUtility.SetDirty(tree);
        }

        private void SyncExpandedState(DialogTree tree)
        {
            int nodeCount = tree.nodes?.Length ?? 0;
            if (_nodeExpanded == null || _nodeExpanded.Length != nodeCount)
            {
                var old = _nodeExpanded;
                _nodeExpanded = new bool[nodeCount];
                if (old != null)
                    for (int i = 0; i < Mathf.Min(old.Length, nodeCount); i++)
                        _nodeExpanded[i] = old[i];
            }
        }

        // ══════════════════════════════════════════
        // HEADER
        // ══════════════════════════════════════════

        private void DrawHeader(DialogTree tree)
        {
            var style = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(12, 12, 10, 10) };
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = HeaderBg;
            EditorGUILayout.BeginVertical(style);
            GUI.backgroundColor = oldBg;

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            var name = string.IsNullOrEmpty(tree.displayName) ? "Unnamed Dialog Tree" : tree.displayName;
            EditorGUILayout.LabelField($"💬 {name}", titleStyle);
            EditorGUILayout.LabelField($"Internal ID: {tree.treeId}",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.7f, 0.9f) } });
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // LEGEND
        // ══════════════════════════════════════════

        private void DrawLegend()
        {
            _showLegend = EditorGUILayout.Foldout(_showLegend, "Legend / How to read", true,
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if (!_showLegend) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            DrawLegendChip(NpcColor,     "NPC replica — speaking character");
            DrawLegendChip(PlayerColor,  "Player choice — what player can say");
            DrawLegendChip(NarrColor,    "Narrator — system/stage direction");
            DrawLegendChip(EndColor,     "End conversation — dialog closes");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "Cards show a replica + its outgoing choices (edges). Click the foldout arrow to edit.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawLegendChip(Color color, string tooltip)
        {
            var chip = new GUIStyle(EditorStyles.miniButton)
            {
                normal = { textColor = color },
                fontSize = 10
            };
            GUIContent content = new GUIContent(
                color == NpcColor ? "🤖 NPC" :
                color == PlayerColor ? "👤 Player" :
                color == NarrColor ? "📖 Narrator" :
                "🔚 End",
                tooltip);
            GUILayout.Button(content, chip, GUILayout.Width(80));
        }

        // ══════════════════════════════════════════
        // SUMMARY
        // ══════════════════════════════════════════

        private void DrawSummary(DialogTree tree)
        {
            int nodeCount = tree.nodes?.Length ?? 0;
            int edgeCount = 0, endCount = 0;
            if (tree.nodes != null)
                foreach (var n in tree.nodes)
                    if (n?.edges != null)
                    {
                        edgeCount += n.edges.Length;
                        foreach (var e in n.edges)
                            if (e != null && string.IsNullOrEmpty(e.targetNodeId)) endCount++;
                    }

            var unreachable = tree.GetUnreachableNodes();
            int unreachableCount = unreachable?.Length ?? 0;
            bool rootMissing = tree.GetNode(tree.rootNodeId) == null;

            var boxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 6, 6), richText = true };
            EditorGUILayout.BeginHorizontal(boxStyle);

            var sb = new System.Text.StringBuilder();
            sb.Append($"Nodes: <b><color=#88ccff>{nodeCount}</color></b>    ");
            sb.Append($"Choices: <b><color=#88ccff>{edgeCount}</color></b>    ");
            if (endCount > 0) sb.Append($"(incl. {endCount} end)    ");

            if (rootMissing)
                sb.Append($"<color=#ff6644>❌ Root '{tree.rootNodeId}' not found!</color>");
            else if (unreachableCount > 0)
                sb.Append($"<color=#ff9944>⚠ {unreachableCount} unreachable: {string.Join(", ", unreachable)}</color>");
            else if (nodeCount > 0)
                sb.Append($"<color=#66cc66>✅ All nodes reachable from '{tree.rootNodeId}'</color>");
            else
                sb.Append($"<color=#888888>Empty tree</color>");

            EditorGUILayout.LabelField(sb.ToString(), new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11 });
            EditorGUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════
        // IDENTITY (fixed layout — вертикально)
        // ══════════════════════════════════════════

        private void DrawIdentity(DialogTree tree)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Tree Identity", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Each field gets full width — no cramped horizontal pairs
            EditorGUILayout.PropertyField(serializedObject.FindProperty("treeId"),
                new GUIContent("Tree ID", "Unique identifier for this dialog tree (e.g. 'mira_default')."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"),
                new GUIContent("Display Name", "Human-readable name shown in editor lists."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rootNodeId"),
                new GUIContent("Root Node ID", "nodeId of the first node shown when dialog starts. Must match an existing node."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("localizationTable"),
                new GUIContent("Localization Table", "Optional localization table for future translation support."));

            if (tree.GetNode(tree.rootNodeId) == null && !string.IsNullOrEmpty(tree.rootNodeId))
                EditorGUILayout.HelpBox($"Root node '{tree.rootNodeId}' is not in the nodes list! Dialog will fail to start.", MessageType.Error);

            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // NODE CARD
        // ══════════════════════════════════════════

        private void DrawNodeCard(DialogueNode node, SerializedProperty nodeProp, int index, string rootNodeId)
        {
            bool isRoot = node.nodeId == rootNodeId;
            var speaker = node.speaker;
            var speakerKind = speaker != null ? speaker.speakerKind : SpeakerRef.Kind.Npc;
            string speakerName = speakerKind == SpeakerRef.Kind.Npc && speaker?.speakerNpc != null
                ? speaker.speakerNpc.displayName : speakerKind.ToString();

            Color cardColor = speakerKind switch
            {
                SpeakerRef.Kind.Player => PlayerColor,
                SpeakerRef.Kind.Narrator => NarrColor,
                _ => NpcColor
            };
            string speakerLabel = speakerKind switch
            {
                SpeakerRef.Kind.Player => "Player",
                SpeakerRef.Kind.Narrator => "Narrator",
                _ => $"NPC: {speakerName}"
            };

            int edgeCount = node.edges?.Length ?? 0;

            // ── Card container ──
            var cardStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(0, 0, 2, 6) };
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(cardColor.r, cardColor.g, cardColor.b, 0.07f);
            EditorGUILayout.BeginVertical(cardStyle);
            GUI.backgroundColor = oldBg;

            // ── Header row ──
            EditorGUILayout.BeginHorizontal();

            string foldLabel = isRoot ? $"🏠 {node.nodeId} (ROOT)" : node.nodeId;
            _nodeExpanded[index] = EditorGUILayout.Foldout(_nodeExpanded[index], foldLabel, true,
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });

            GUILayout.FlexibleSpace();

            // Speaker badge
            var sbStyle = new GUIStyle(EditorStyles.miniButton) { normal = { textColor = cardColor }, fontSize = 10 };
            GUILayout.Button(new GUIContent(speakerLabel, "Who speaks this replica"), sbStyle);

            // Choice count
            if (edgeCount > 0)
            {
                var ecStyle = new GUIStyle(EditorStyles.miniButton) { fontSize = 10 };
                GUILayout.Button(new GUIContent($"{edgeCount} choices",
                    $"{edgeCount} outgoing player choice(s) from this node"), ecStyle);
            }

            // Move/delete
            var arrProp = serializedObject.FindProperty("nodes");
            if (index > 0 && GUILayout.Button("▲", GUILayout.Width(20)))
                { arrProp.MoveArrayElement(index, index - 1); return; }
            if (index < arrProp.arraySize - 1 && GUILayout.Button("▼", GUILayout.Width(20)))
                { arrProp.MoveArrayElement(index, index + 1); return; }
            if (GUILayout.Button("×", GUILayout.Width(20)))
                { arrProp.DeleteArrayElementAtIndex(index); return; }

            EditorGUILayout.EndHorizontal();

            // ── Text preview (always visible) ──
            string preview = node.text ?? "";
            if (preview.Length > 120) preview = preview.Substring(0, 117) + "...";
            if (!string.IsNullOrEmpty(preview))
            {
                EditorGUILayout.LabelField($"\"{preview}\"",
                    new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        fontStyle = FontStyle.Italic,
                        padding = new RectOffset(18, 4, 2, 4),
                        normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
                    });
            }

            // ── Expanded: full edit ──
            if (_nodeExpanded[index])
            {
                EditorGUILayout.Space(6);

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("speaker"),
                    new GUIContent("Speaker", "Who speaks: NPC (drag .asset), Player (auto), or Narrator (system text)"), true);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("text"),
                    new GUIContent("Text", "The dialogue text for this replica."));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("portraitEmotion"),
                    new GUIContent("Portrait Emotion", "Variant name for the NPC portrait (e.g. 'neutral', 'angry'). Empty = default."));

                // On Enter Actions
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "On Enter Actions fire ONCE when this node is first shown. Use for: voice line cues, ambient sounds, scripted camera moves, etc.",
                    MessageType.None);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("onEnterActions"),
                    new GUIContent("On Enter Actions", "Server-side effects when this replica appears."), true);

                EditorGUI.indentLevel--;

                // ── Edges (choices) ──
                EditorGUILayout.Space(6);
                DrawEdgesSection(nodeProp);
            }

            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // EDGES
        // ══════════════════════════════════════════

        private void DrawEdgesSection(SerializedProperty nodeProp)
        {
            var edgesProp = nodeProp.FindPropertyRelative("edges");
            if (edgesProp == null || !edgesProp.isArray) return;

            EditorGUILayout.LabelField("Choices (player replies):",
                new GUIStyle(EditorStyles.boldLabel));
            EditorGUILayout.HelpBox(
                "Each choice = one thing the player can say. Clicking it fires the Action, then jumps to the Target Node.\n" +
                "If Target Node is empty → dialog ends after the Action fires.",
                MessageType.None);
            EditorGUILayout.Space(2);

            for (int i = 0; i < edgesProp.arraySize; i++)
            {
                var edgeProp = edgesProp.GetArrayElementAtIndex(i);
                if (edgeProp == null) continue;

                var labelProp = edgeProp.FindPropertyRelative("label");
                var targetProp = edgeProp.FindPropertyRelative("targetNodeId");
                var actionProp = edgeProp.FindPropertyRelative("action");
                var condsProp = edgeProp.FindPropertyRelative("conditions");

                string targetId = targetProp?.stringValue ?? "";
                bool isEnd = string.IsNullOrEmpty(targetId);

                // ── Edge card ──
                Color edgeColor = isEnd ? EndColor : PlayerColor;
                var edgeStyle = new GUIStyle(EditorStyles.helpBox)
                    { padding = new RectOffset(8, 8, 4, 4), margin = new RectOffset(8, 4, 2, 2) };
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.06f);
                EditorGUILayout.BeginVertical(edgeStyle);
                GUI.backgroundColor = oldBg;

                // ── Row 1: arrow + label + target ──
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(isEnd ? "🔚" : "➡",
                    new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, normal = { textColor = edgeColor } },
                    GUILayout.Width(18));

                EditorGUILayout.PropertyField(labelProp,
                    new GUIContent("", "Player-visible text for this choice (e.g. 'Tell me more.')."), GUILayout.MinWidth(60));

                string targetDisplay = isEnd ? "→ END (dialog closes)" : $"→ {targetId}";
                EditorGUILayout.LabelField(targetDisplay,
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = edgeColor } },
                    GUILayout.Width(isEnd ? 150 : 120));

                // Delete edge
                if (GUILayout.Button("×", GUILayout.Width(20)))
                    { edgesProp.DeleteArrayElementAtIndex(i); break; }

                EditorGUILayout.EndHorizontal();

                // ── Row 2: action summary + conditions + hide toggle ──
                DrawEdgeMetaRow(edgeProp, actionProp, condsProp);

                EditorGUILayout.EndVertical();
            }

            // Add edge button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Choice", GUILayout.Width(120), GUILayout.Height(22)))
            {
                edgesProp.arraySize++;
                var newEdge = edgesProp.GetArrayElementAtIndex(edgesProp.arraySize - 1);
                newEdge.FindPropertyRelative("label").stringValue = "Continue";
                newEdge.FindPropertyRelative("targetNodeId").stringValue = "";
                newEdge.FindPropertyRelative("hideIfUnavailable").boolValue = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEdgeMetaRow(SerializedProperty edgeProp, SerializedProperty actionProp,
            SerializedProperty condsProp)
        {
            var actionTypeProp = actionProp?.FindPropertyRelative("type");
            if (actionTypeProp == null) return;

            var actionType = (DialogueActionType)actionTypeProp.enumValueIndex;
            int condCount = condsProp?.arraySize ?? 0;
            var hideProp = edgeProp.FindPropertyRelative("hideIfUnavailable");

            // Check if there's anything to show
            bool hasAction = actionType != DialogueActionType.EndConversation;
            bool hasConditions = condCount > 0;

            if (!hasAction && !hasConditions) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.indentLevel++;

            if (hasAction)
            {
                var actStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.6f, 0.9f, 0.6f) },
                    richText = true
                };
                EditorGUILayout.LabelField(
                    new GUIContent($"<b>Action:</b> {actionType}",
                    "Server-side effect when player selects this choice (e.g. OfferQuest, GiveCredits, SetFlag)."),
                    actStyle);
            }

            if (hasConditions)
            {
                var condStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 0.8f, 0.4f) },
                    richText = true
                };
                EditorGUILayout.LabelField(
                    new GUIContent(
                        condCount == 1 ? $"<b>Condition:</b> must be met" : $"<b>Conditions ({condCount}):</b> all must be met (AND)",
                        "All conditions must be true for this choice to appear. If any fails, the choice is hidden or greyed out."),
                    condStyle, GUILayout.MinWidth(200));
            }

            GUILayout.FlexibleSpace();

            // Hide toggle
            var hideContent = new GUIContent(
                hideProp.boolValue ? "Hide if locked" : "Show grey if locked",
                "If ON: choice is hidden when conditions fail.\nIf OFF: choice appears greyed out.");
            hideProp.boolValue = EditorGUILayout.Toggle(hideContent, hideProp.boolValue, GUILayout.Width(120));

            EditorGUI.indentLevel--;
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
