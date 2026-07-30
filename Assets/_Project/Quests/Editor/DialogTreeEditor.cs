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
                // Auto-generate unique ID: "new_node", "new_node_2", etc.
                string baseId = "new_node";
                string uniqueId = baseId;
                int counter = 2;
                while (System.Array.Exists(tree.nodes, n => n != null && n.nodeId == uniqueId))
                    uniqueId = $"{baseId}_{counter++}";
                newProp.FindPropertyRelative("nodeId").stringValue = uniqueId;
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
            _showLegend = EditorGUILayout.Foldout(_showLegend, "Color coding", true,
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if (!_showLegend) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Each card is color-coded by who speaks. Choices (edges) are also colored.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            DrawColorSample(NpcColor,     "🤖 NPC replica");
            DrawColorSample(PlayerColor,  "👤 Player choice");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawColorSample(NarrColor,    "📖 Narrator / system");
            DrawColorSample(EndColor,     "🔚 End conversation");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                "Click the ▸ arrow on a card to edit it. Each card shows its text + outgoing choices.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawColorSample(Color color, string label)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            EditorGUILayout.LabelField(label,
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 },
                GUILayout.Width(200));
            GUI.color = oldColor;
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
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nodeId"),
                    new GUIContent("Node ID", "Unique ID within this tree. Used by edges' targetNodeId to link to this node. No spaces, no special chars."));
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

            // Collect sibling node IDs for the target dropdown
            var nodesProp = serializedObject.FindProperty("nodes");
            var siblingIds = new System.Collections.Generic.List<string>();
            for (int n = 0; n < nodesProp.arraySize; n++)
            {
                var nodeId = nodesProp.GetArrayElementAtIndex(n).FindPropertyRelative("nodeId").stringValue;
                if (!string.IsNullOrEmpty(nodeId))
                    siblingIds.Add(nodeId);
            }

            EditorGUILayout.LabelField("Choices (player replies):",
                new GUIStyle(EditorStyles.boldLabel));
            EditorGUILayout.HelpBox(
                "Each choice = one thing the player can say. Set Action, Conditions, and Target Node.\n" +
                "Empty Target → dialog ends after the Action fires.",
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
                var hideProp = edgeProp.FindPropertyRelative("hideIfUnavailable");

                string currentTarget = targetProp?.stringValue ?? "";
                bool isEnd = string.IsNullOrEmpty(currentTarget);

                // ── Edge box ──
                Color edgeColor = isEnd ? EndColor : PlayerColor;
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.06f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = oldBg;

                // ── Row: arrow + label + target dropdown + delete ──
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("➡",
                    new GUIStyle(EditorStyles.label)
                        { fontStyle = FontStyle.Bold, normal = { textColor = edgeColor } },
                    GUILayout.Width(18));

                EditorGUILayout.PropertyField(labelProp, GUIContent.none, GUILayout.MinWidth(100));

                // ── Target Node dropdown ──
                int selectedIdx = siblingIds.IndexOf(currentTarget);
                int newIdx = EditorGUILayout.Popup(
                    new GUIContent("→", "Target node to jump to. '(end)' = dialog closes."),
                    selectedIdx + 1, // +1 because index 0 = "(end conversation)"
                    ToTargetChoices(siblingIds),
                    GUILayout.MinWidth(120));
                if (newIdx != selectedIdx + 1)
                {
                    targetProp.stringValue = (newIdx == 0) ? "" : siblingIds[newIdx - 1];
                }

                // HideIfUnavailable toggle
                EditorGUILayout.PropertyField(hideProp, new GUIContent("Hide?", "Hide choice if conditions fail (otherwise grey out)."), GUILayout.Width(50));

                // Delete
                if (GUILayout.Button("×", GUILayout.Width(20)))
                    { edgesProp.DeleteArrayElementAtIndex(i); break; }

                EditorGUILayout.EndHorizontal();

                // ── Action ──
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(actionProp, new GUIContent("Action"));
                EditorGUI.indentLevel--;

                // ── Conditions ──
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(condsProp, new GUIContent("Conditions (AND)"), true);
                EditorGUI.indentLevel--;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            // Add edge button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Choice", GUILayout.Width(130), GUILayout.Height(22)))
            {
                edgesProp.arraySize++;
                var newEdge = edgesProp.GetArrayElementAtIndex(edgesProp.arraySize - 1);
                newEdge.FindPropertyRelative("label").stringValue = "Continue";
                newEdge.FindPropertyRelative("targetNodeId").stringValue = "";
                newEdge.FindPropertyRelative("hideIfUnavailable").boolValue = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private string[] ToTargetChoices(System.Collections.Generic.List<string> siblingIds)
        {
            var choices = new string[1 + siblingIds.Count];
            choices[0] = "(end conversation)";
            for (int i = 0; i < siblingIds.Count; i++)
                choices[1 + i] = siblingIds[i];
            return choices;
        }
    }
}
#endif
