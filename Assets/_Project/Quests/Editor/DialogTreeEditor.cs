// DialogTreeEditor — кастомный Editor для DialogTree с карточками нод.
// T-QUEDIT v2: цветовое кодирование (NPC/Player/Narrator), сводка графа,
// валидация связей, drag-and-drop полей внутри условий/действий.
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
        // ── Colors ──
        private static readonly Color NpcColor    = new Color(0.3f, 0.5f, 1.0f, 1f);
        private static readonly Color PlayerColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        private static readonly Color NarrColor   = new Color(0.8f, 0.8f, 0.3f, 1f);
        private static readonly Color EndColor    = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color HeaderBg    = new Color(0.18f, 0.22f, 0.32f, 1f);
        private static readonly Color WarnBg      = new Color(0.9f, 0.5f, 0.2f, 0.3f);
        private static readonly Color OkBg        = new Color(0.3f, 0.7f, 0.3f, 0.3f);

        private bool[] _nodeExpanded;
        private bool _showAllNodes = true;

        public override void OnInspectorGUI()
        {
            var tree = (DialogTree)target;
            if (tree == null) return;

            serializedObject.Update();

            // Sync expanded states
            int nodeCount = tree.nodes?.Length ?? 0;
            if (_nodeExpanded == null || _nodeExpanded.Length != nodeCount)
            {
                var old = _nodeExpanded;
                _nodeExpanded = new bool[nodeCount];
                if (old != null)
                    for (int i = 0; i < Mathf.Min(old.Length, nodeCount); i++)
                        _nodeExpanded[i] = old[i];
            }

            DrawHeader(tree);
            EditorGUILayout.Space(6);
            DrawSummary(tree);
            EditorGUILayout.Space(6);
            DrawIdentity(tree);
            EditorGUILayout.Space(6);

            // ── Nodes ──
            EditorGUILayout.LabelField("Dialogue Nodes", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var nodesProp = serializedObject.FindProperty("nodes");

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
                newProp.FindPropertyRelative("speaker").FindPropertyRelative("speakerKind").enumValueIndex = 0; // Npc
                newProp.FindPropertyRelative("portraitEmotion").stringValue = "neutral";
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(tree);
        }

        // ══════════════════════════════════════════
        // HEADER
        // ══════════════════════════════════════════

        private void DrawHeader(DialogTree tree)
        {
            var headerStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = HeaderBg;
            EditorGUILayout.BeginVertical(headerStyle);
            GUI.backgroundColor = oldBg;

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            var displayName = string.IsNullOrEmpty(tree.displayName) ? "Unnamed Dialog Tree" : tree.displayName;
            EditorGUILayout.LabelField($"💬 {displayName}", titleStyle);

            var idStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.7f, 0.9f) }
            };
            EditorGUILayout.LabelField($"ID: {tree.treeId}", idStyle);

            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════
        // SUMMARY
        // ══════════════════════════════════════════

        private void DrawSummary(DialogTree tree)
        {
            int nodeCount = tree.nodes?.Length ?? 0;
            int edgeCount = 0;
            int endCount = 0;
            if (tree.nodes != null)
                foreach (var n in tree.nodes)
                    if (n?.edges != null)
                    {
                        edgeCount += n.edges.Length;
                        foreach (var e in n.edges)
                            if (e != null && string.IsNullOrEmpty(e.targetNodeId))
                                endCount++;
                    }

            // Validation
            var unreachable = tree.GetUnreachableNodes();
            int unreachableCount = unreachable?.Length ?? 0;
            bool rootMissing = tree.GetNode(tree.rootNodeId) == null;

            var boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 6),
                richText = true
            };
            EditorGUILayout.BeginHorizontal(boxStyle);

            var sb = new System.Text.StringBuilder();
            sb.Append($"<b>🟢 Nodes:</b> <color=#88ccff>{nodeCount}</color>    ");
            sb.Append($"<b>➡ Edges:</b> <color=#88ccff>{edgeCount}</color>    ");
            if (endCount > 0) sb.Append($"<b>🔚 End:</b> {endCount}    ");
            if (rootMissing)
                sb.Append($"<color=#ff6644>❌ Root '{tree.rootNodeId}' missing!</color>");
            else if (unreachableCount > 0)
                sb.Append($"<color=#ff9944>⚠ {unreachableCount} unreachable</color>");
            else if (nodeCount > 0)
                sb.Append($"<color=#66cc66>✅ All reachable</color>");

            EditorGUILayout.LabelField(sb.ToString(), new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11 });
            EditorGUILayout.EndHorizontal();

            // Show unreachable list
            if (unreachableCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Unreachable nodes: {string.Join(", ", unreachable)}\nThese nodes have no path from root '{tree.rootNodeId}'.",
                    MessageType.Warning);
            }
        }

        // ══════════════════════════════════════════
        // IDENTITY
        // ══════════════════════════════════════════

        private void DrawIdentity(DialogTree tree)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("treeId"), new GUIContent("Tree ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Display Name"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rootNodeId"), new GUIContent("Root Node ID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("localizationTable"), new GUIContent("Localization Table"));
            EditorGUILayout.EndHorizontal();

            if (tree.GetNode(tree.rootNodeId) == null)
                EditorGUILayout.HelpBox($"Root node '{tree.rootNodeId}' not found in nodes[]!", MessageType.Error);

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

            // Determine color
            Color cardColor;
            string speakerIcon;
            switch (speakerKind)
            {
                case SpeakerRef.Kind.Player:
                    cardColor = PlayerColor;
                    speakerIcon = "👤";
                    break;
                case SpeakerRef.Kind.Narrator:
                    cardColor = NarrColor;
                    speakerIcon = "📖";
                    break;
                default:
                    cardColor = NpcColor;
                    speakerIcon = "🤖";
                    break;
            }

            // ── Card container ──
            var cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 2, 6)
            };
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(cardColor.r, cardColor.g, cardColor.b, 0.08f);
            EditorGUILayout.BeginVertical(cardStyle);
            GUI.backgroundColor = oldBg;

            // ── Card header ──
            EditorGUILayout.BeginHorizontal();

            _nodeExpanded[index] = EditorGUILayout.Foldout(_nodeExpanded[index],
                $"{speakerIcon} {(isRoot ? "🏠 " : "")}{node.nodeId}", true,
                new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });

            GUILayout.FlexibleSpace();

            // Speaker badge
            var badgeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                normal = { textColor = cardColor },
                fontSize = 10
            };
            string speakerLabel = speakerKind == SpeakerRef.Kind.Npc && speaker?.speakerNpc != null
                ? speaker.speakerNpc.displayName
                : speakerKind.ToString();
            GUILayout.Button(speakerLabel, badgeStyle, GUILayout.Width(70));

            // Edge count badge
            int edgeCount = node.edges?.Length ?? 0;
            if (edgeCount > 0)
            {
                var ecStyle = new GUIStyle(EditorStyles.miniButton) { fontSize = 10 };
                GUILayout.Button($"➡{edgeCount}", ecStyle, GUILayout.Width(40));
            }

            // Move up/down
            if (index > 0 && GUILayout.Button("▲", GUILayout.Width(22)))
            {
                var arrProp = serializedObject.FindProperty("nodes");
                arrProp.MoveArrayElement(index, index - 1);
                return;
            }
            if (index < (serializedObject.FindProperty("nodes").arraySize - 1) && GUILayout.Button("▼", GUILayout.Width(22)))
            {
                var arrProp = serializedObject.FindProperty("nodes");
                arrProp.MoveArrayElement(index, index + 1);
                return;
            }

            // Delete
            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                var arrProp = serializedObject.FindProperty("nodes");
                arrProp.DeleteArrayElementAtIndex(index);
                return;
            }

            EditorGUILayout.EndHorizontal();

            // ── Text preview (always visible) ──
            string preview = node.text ?? "";
            if (preview.Length > 100) preview = preview.Substring(0, 97) + "...";
            if (!string.IsNullOrEmpty(preview))
            {
                var textStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    fontStyle = FontStyle.Italic,
                    padding = new RectOffset(18, 4, 0, 4)
                };
                EditorGUILayout.LabelField($"\"{preview}\"", textStyle);
            }

            // ── Expanded content ──
            if (_nodeExpanded[index])
            {
                EditorGUILayout.Space(4);

                // Core fields
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nodeId"), new GUIContent("Node ID"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("speaker"), new GUIContent("Speaker"), true);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("text"), new GUIContent("Text"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("portraitEmotion"), new GUIContent("Portrait Emotion"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("onEnterActions"), new GUIContent("On Enter Actions"), true);
                EditorGUI.indentLevel--;

                // ── Edges ──
                EditorGUILayout.Space(4);
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

            EditorGUILayout.LabelField("Edges (player choices):", EditorStyles.miniBoldLabel);

            for (int i = 0; i < edgesProp.arraySize; i++)
            {
                var edgeProp = edgesProp.GetArrayElementAtIndex(i);
                if (edgeProp == null) continue;

                var labelProp = edgeProp.FindPropertyRelative("label");
                var targetProp = edgeProp.FindPropertyRelative("targetNodeId");
                var actionProp = edgeProp.FindPropertyRelative("action");
                var condProp = edgeProp.FindPropertyRelative("condition");
                var condsProp = edgeProp.FindPropertyRelative("conditions");
                var hideProp = edgeProp.FindPropertyRelative("hideIfUnavailable");

                string targetId = targetProp?.stringValue ?? "";
                bool isEnd = string.IsNullOrEmpty(targetId);

                // ── Edge row ──
                Color edgeColor = isEnd ? EndColor : PlayerColor;
                var edgeStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(8, 8, 4, 4),
                    margin = new RectOffset(8, 4, 2, 2)
                };
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0.06f);
                EditorGUILayout.BeginVertical(edgeStyle);
                GUI.backgroundColor = oldBg;

                // Row 1: label → target
                EditorGUILayout.BeginHorizontal();

                // Arrow
                var arrowStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = edgeColor },
                    fontStyle = FontStyle.Bold
                };
                EditorGUILayout.LabelField(isEnd ? "🔚" : "➡", arrowStyle, GUILayout.Width(20));

                // Label
                EditorGUILayout.PropertyField(labelProp, GUIContent.none);

                // Target
                var targetStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = edgeColor }
                };
                string targetLabel = isEnd ? "end conversation" : $"→ {targetId}";
                EditorGUILayout.LabelField(targetLabel, targetStyle, GUILayout.Width(isEnd ? 100 : 140));

                // HideIfUnavailable toggle
                EditorGUILayout.PropertyField(hideProp, new GUIContent("Hide"), GUILayout.Width(50));

                // Delete
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    edgesProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();

                // Row 2: compact action + condition info
                DrawEdgeMeta(edgeProp, actionProp, condProp, condsProp);

                EditorGUILayout.EndVertical();
            }

            // Add edge button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Choice", GUILayout.Width(110), GUILayout.Height(22)))
            {
                edgesProp.arraySize++;
                var newEdge = edgesProp.GetArrayElementAtIndex(edgesProp.arraySize - 1);
                newEdge.FindPropertyRelative("label").stringValue = "Continue";
                newEdge.FindPropertyRelative("targetNodeId").stringValue = "";
                newEdge.FindPropertyRelative("hideIfUnavailable").boolValue = true;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEdgeMeta(SerializedProperty edgeProp, SerializedProperty actionProp,
            SerializedProperty condProp, SerializedProperty condsProp)
        {
            var actionTypeProp = actionProp?.FindPropertyRelative("type");
            if (actionTypeProp == null) return;

            var actionType = (DialogueActionType)actionTypeProp.enumValueIndex;
            int condCount = (condsProp?.arraySize ?? 0);
            bool hasSingleCond = condProp?.FindPropertyRelative("type") != null &&
                                 condProp.FindPropertyRelative("type").enumValueIndex != 0; // not default(first)

            if (actionType == DialogueActionType.EndConversation && condCount == 0 && !hasSingleCond)
                return; // nothing interesting

            EditorGUILayout.BeginHorizontal();
            EditorGUI.indentLevel++;

            // Action badge
            var actionBadgeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.7f, 0.9f, 0.7f) }
            };
            string actionLabel = actionType == DialogueActionType.EndConversation ? "" : $"⚡ {actionType}";
            if (!string.IsNullOrEmpty(actionLabel))
                GUILayout.Button(actionLabel, actionBadgeStyle);

            // Condition badge
            if (condCount > 0)
            {
                var condBadgeStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 9,
                    normal = { textColor = new Color(0.9f, 0.7f, 0.3f) }
                };
                GUILayout.Button($"🔒 ×{condCount}", condBadgeStyle);
            }
            else if (hasSingleCond)
            {
                var condBadgeStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    fontSize = 9,
                    normal = { textColor = new Color(0.9f, 0.7f, 0.3f) }
                };
                GUILayout.Button("🔒 ×1", condBadgeStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
