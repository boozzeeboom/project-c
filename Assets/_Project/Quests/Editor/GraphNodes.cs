// GraphNodes — VisualElement node types for the Unified Graph View.
// Each node type wraps a specific data type from NpcDefinition/DialogTree/QuestDefinition.
//
// Architecture: ARCHITECTURE_PLAN.md

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    // ═══════════════════════════════════════════
    // Base
    // ═══════════════════════════════════════════

    public static class GraphNodeColors
    {
        public static readonly Color Npc       = new(0.45f, 0.30f, 0.65f);  // purple
        public static readonly Color Dialog    = new(0.25f, 0.45f, 0.85f);  // blue
        public static readonly Color Quest     = new(0.20f, 0.35f, 0.60f);  // dark blue
        public static readonly Color Stage     = new(0.20f, 0.55f, 0.30f);  // green
        public static readonly Color Objective = new(0.55f, 0.40f, 0.10f);  // brown/yellow
        public static readonly Color Reward    = new(0.65f, 0.40f, 0.10f);  // gold
        public static readonly Color PortGray  = new(0.45f, 0.45f, 0.45f);
        public static readonly Color PortGreen = new(0.35f, 0.70f, 0.35f);
        public static readonly Color PortBlue  = new(0.35f, 0.50f, 0.90f);
        public static readonly Color PortPurple = new(0.50f, 0.35f, 0.80f);
        public static readonly Color PortOrange = new(0.90f, 0.50f, 0.10f);
    }

    /// <summary>Base node with semantic port creation helpers.</summary>
    public abstract class BaseGraphNode : Node
    {
        public string PersistKey;

        protected Port MakeOutPort(string name, PortSemantic semantic, Color color, object userData = null)
        {
            var p = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(bool));
            p.portName = name;
            p.portColor = color;
            p.userData = (semantic, userData);
            outputContainer.Add(p);
            return p;
        }

        protected Port MakeInPort(string name, PortSemantic semantic, Color color, object userData = null)
        {
            var p = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Multi, typeof(bool));
            p.portName = name;
            p.portColor = color;
            p.userData = (semantic, userData);
            inputContainer.Add(p);
            return p;
        }

        /// <summary>Get semantic from a port.</summary>
        public static PortSemantic GetSemantic(Port p)
        {
            if (p?.userData is (PortSemantic sem, _)) return sem;
            return (PortSemantic)(-1);
        }

        /// <summary>Get extra data (e.g. edge index) from a port.</summary>
        public static object GetPortData(Port p)
        {
            if (p?.userData is (_, object data)) return data;
            return null;
        }
    }

    // ═══════════════════════════════════════════
    // NpcNode
    // ═══════════════════════════════════════════

    public class NpcGraphNode : BaseGraphNode
    {
        public readonly NpcDefinition Npc;
        public readonly NpcNodeInfo Info;

        public NpcGraphNode(NpcNodeInfo info)
        {
            Npc = info.npc;
            Info = info;
            PersistKey = $"npc_{Npc.npcId}";
            title = $"👤 {Npc.displayName}";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Npc);

            MakeOutPort("→ Dialog", PortSemantic.NpcDefaultDialog, GraphNodeColors.PortPurple);
            MakeOutPort("→ Offers Quest", PortSemantic.NpcOffersQuest, GraphNodeColors.PortOrange);
            MakeInPort("← Quest target", PortSemantic.NpcTargetedBy, GraphNodeColors.PortBlue);

            var editorArea = new IMGUIContainer(DrawEditor);
            editorArea.style.minHeight = 60f;
            editorArea.style.flexGrow = 1;
            extensionContainer.style.paddingLeft = 4;
            extensionContainer.style.paddingRight = 4;
            extensionContainer.Add(editorArea);

            RefreshExpandedState();
            expanded = true;
        }

        private void DrawEditor()
        {
            var so = new SerializedObject(Npc);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("faction"), new GUIContent("Faction"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultDialogTree"), new GUIContent("Dialog Tree"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Npc);
        }
    }

    // ═══════════════════════════════════════════
    // DialogGraphNode
    // ═══════════════════════════════════════════

    public class DialogGraphNode : BaseGraphNode
    {
        public readonly DialogTree Tree;
        public readonly int NodeIndex;
        public readonly DialogNodeInfo Info;
        public DialogueNode DialogueNode => Tree?.nodes != null && NodeIndex >= 0 && NodeIndex < Tree.nodes.Length
            ? Tree.nodes[NodeIndex] : null;

        public DialogGraphNode(DialogNodeInfo info)
        {
            Tree = info.tree;
            NodeIndex = info.nodeIndex;
            Info = info;
            var node = DialogueNode;
            PersistKey = $"dlg_{Tree.treeId}_{node?.nodeId ?? NodeIndex.ToString()}";

            string speaker = node?.speaker?.speakerNpc != null ? node.speaker.speakerNpc.displayName
                : (node?.speaker?.speakerKind.ToString() ?? "Npc");
            string preview = node?.text ?? "";
            if (preview.Length > 45) preview = preview.Substring(0, 42) + "...";
            title = $"💬 {speaker}: \"{preview}\"";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Dialog);

            MakeInPort("← In", PortSemantic.DialogIn, GraphNodeColors.PortGray);

            // One output port per edge
            var edges = node?.edges;
            if (edges != null)
            {
                for (int ei = 0; ei < edges.Length; ei++)
                {
                    var e = edges[ei];
                    if (e == null) continue;
                    string label = string.IsNullOrEmpty(e.label) ? $"→ choice {ei}" : e.label;
                    if (label.Length > 18) label = label.Substring(0, 15) + "...";
                    // Color: green for internal, orange for OfferQuest, purple for SwitchDialogTree
                    Color c = GraphNodeColors.PortGreen;
                    if (e.action?.type == DialogueActionType.OfferQuest) c = GraphNodeColors.PortOrange;
                    else if (e.action?.type == DialogueActionType.SwitchDialogTree) c = GraphNodeColors.PortPurple;
                    MakeOutPort(label, PortSemantic.DialogEdgeAction, c, ei);
                }
            }
            // Always add at least one "→" port for new connections
            if (edges == null || edges.Length == 0)
                MakeOutPort("→", PortSemantic.DialogEdgeAction, GraphNodeColors.PortGreen, 0);

            var editorArea = new IMGUIContainer(DrawEditor);
            editorArea.style.minHeight = 100f;
            editorArea.style.flexGrow = 1;
            extensionContainer.style.paddingLeft = 4;
            extensionContainer.style.paddingRight = 4;
            extensionContainer.Add(editorArea);

            RefreshExpandedState();
            expanded = true;
        }

        private void DrawEditor()
        {
            var node = DialogueNode;
            if (node == null) { EditorGUILayout.LabelField("(null node)"); return; }
            var so = new SerializedObject(Tree);
            so.Update();
            var nodesProp = so.FindProperty("nodes");
            if (NodeIndex >= nodesProp.arraySize) return;
            var nodeProp = nodesProp.GetArrayElementAtIndex(NodeIndex);

            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("speaker"), new GUIContent("Speaker"), true);
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("text"), new GUIContent("Text"));
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("portraitEmotion"), new GUIContent("Emotion"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Edges (choices):", EditorStyles.boldLabel);
            var edgesProp = nodeProp.FindPropertyRelative("edges");
            if (edgesProp != null && edgesProp.isArray)
            {
                for (int i = 0; i < edgesProp.arraySize; i++)
                {
                    var ep = edgesProp.GetArrayElementAtIndex(i);
                    if (ep == null) continue;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.PropertyField(ep.FindPropertyRelative("label"), new GUIContent($"Choice {i+1}"));
                    EditorGUILayout.PropertyField(ep.FindPropertyRelative("action"), new GUIContent("Action"), true);
                    EditorGUILayout.PropertyField(ep.FindPropertyRelative("conditions"), new GUIContent("Conditions"), true);
                    if (GUILayout.Button("× Remove Choice", GUILayout.Width(120)))
                    {
                        edgesProp.DeleteArrayElementAtIndex(i);
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(Tree);
                        GUIUtility.ExitGUI();
                        return;
                    }
                    EditorGUILayout.EndVertical();
                }
                if (GUILayout.Button("+ Add Choice", GUILayout.Width(120)))
                {
                    edgesProp.arraySize++;
                    var ne = edgesProp.GetArrayElementAtIndex(edgesProp.arraySize - 1);
                    ne.FindPropertyRelative("label").stringValue = "New Choice";
                    ne.FindPropertyRelative("hideIfUnavailable").boolValue = true;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(Tree);
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Tree);
        }
    }

    // ═══════════════════════════════════════════
    // QuestGraphNode (root)
    // ═══════════════════════════════════════════

    public class QuestRootGraphNode : BaseGraphNode
    {
        public readonly QuestDefinition Quest;
        public readonly QuestNodeInfo Info;

        public QuestRootGraphNode(QuestNodeInfo info)
        {
            Quest = info.quest;
            Info = info;
            PersistKey = $"quest_{Quest.questId}";
            title = $"📜 {Quest.displayName}";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Quest);

            MakeInPort("← Offered by", PortSemantic.QuestOfferedBy, GraphNodeColors.PortOrange);
            MakeOutPort("→ Stage 1", PortSemantic.StageNext, GraphNodeColors.PortGreen);

            var editorArea = new IMGUIContainer(DrawEditor);
            editorArea.style.minHeight = 50f;
            editorArea.style.flexGrow = 1;
            extensionContainer.style.paddingLeft = 4;
            extensionContainer.style.paddingRight = 4;
            extensionContainer.Add(editorArea);

            RefreshExpandedState();
            expanded = true;
        }

        private void DrawEditor()
        {
            var so = new SerializedObject(Quest);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("questId"), new GUIContent("ID"));
            EditorGUILayout.PropertyField(so.FindProperty("description"), new GUIContent("Desc"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Quest);
        }
    }

    // ═══════════════════════════════════════════
    // StageNode
    // ═══════════════════════════════════════════

    public class StageGraphNode : BaseGraphNode
    {
        public readonly QuestDefinition Quest;
        public readonly int StageIndex;
        public readonly StageNodeInfo Info;
        public QuestStage Stage => Quest?.stages != null && StageIndex >= 0 && StageIndex < Quest.stages.Length
            ? Quest.stages[StageIndex] : null;

        public StageGraphNode(StageNodeInfo info)
        {
            Quest = info.quest;
            StageIndex = info.stageIndex;
            Info = info;
            PersistKey = $"stage_{Quest.questId}_{StageIndex}";
            title = $"🟢 {Stage?.stageId ?? $"stage_{StageIndex}"}";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Stage);

            MakeInPort("← Prev", PortSemantic.StageIn, GraphNodeColors.PortGray);
            MakeOutPort("→ Next", PortSemantic.StageNext, GraphNodeColors.PortGreen);

            var editorArea = new IMGUIContainer(DrawEditor);
            editorArea.style.minHeight = 60f;
            editorArea.style.flexGrow = 1;
            extensionContainer.Add(editorArea);

            RefreshExpandedState();
            expanded = true;
        }

        private void DrawEditor()
        {
            var stage = Stage;
            if (stage == null) return;
            var so = new SerializedObject(Quest);
            so.Update();
            var stagesProp = so.FindProperty("stages");
            if (StageIndex >= stagesProp.arraySize) return;
            var sp = stagesProp.GetArrayElementAtIndex(StageIndex);
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("stageId"), new GUIContent("Stage ID"));
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("description"), new GUIContent("Desc"));
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("nextStageId"), new GUIContent("Next Stage ID"));
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("objectives"), new GUIContent("Objectives"), true);
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Quest);
        }
    }

    // ═══════════════════════════════════════════
    // ObjectiveNode
    // ═══════════════════════════════════════════

    public class ObjectiveGraphNode : BaseGraphNode
    {
        public readonly QuestDefinition Quest;
        public readonly int StageIndex;
        public readonly int ObjIndex;
        public readonly ObjectiveNodeInfo Info;
        public QuestObjective Obj => Info.Obj;

        public ObjectiveGraphNode(ObjectiveNodeInfo info)
        {
            Quest = info.quest;
            StageIndex = info.stageIndex;
            ObjIndex = info.objIndex;
            Info = info;
            PersistKey = $"obj_{Quest.questId}_{StageIndex}_{ObjIndex}";
            var obj = Obj;
            title = $"🎯 {obj?.objectiveId ?? $"obj_{ObjIndex}"} [{obj?.objectiveType}]";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Objective);

            MakeInPort("← Stage", PortSemantic.StageIn, GraphNodeColors.PortGray);
            MakeOutPort("→ Target NPC", PortSemantic.ObjectiveTarget, GraphNodeColors.PortBlue);

            var editorArea = new IMGUIContainer(DrawEditor);
            editorArea.style.minHeight = 50f;
            editorArea.style.flexGrow = 1;
            extensionContainer.Add(editorArea);

            RefreshExpandedState();
            expanded = true;
        }

        private void DrawEditor()
        {
            var obj = Obj;
            if (obj == null) return;
            var so = new SerializedObject(Quest);
            so.Update();
            var stagesProp = so.FindProperty("stages");
            if (StageIndex >= stagesProp.arraySize) return;
            var sp = stagesProp.GetArrayElementAtIndex(StageIndex);
            var objectivesProp = sp.FindPropertyRelative("objectives");
            if (ObjIndex >= objectivesProp.arraySize) return;
            var op = objectivesProp.GetArrayElementAtIndex(ObjIndex);

            EditorGUILayout.PropertyField(op.FindPropertyRelative("objectiveType"), new GUIContent("Type"));
            EditorGUILayout.PropertyField(op.FindPropertyRelative("targetNpc"), new GUIContent("Target NPC"));
            EditorGUILayout.PropertyField(op.FindPropertyRelative("targetNpcId"), new GUIContent("NPC ID (fallback)"));
            EditorGUILayout.PropertyField(op.FindPropertyRelative("requiredQuantity"), new GUIContent("Qty"));
            EditorGUILayout.PropertyField(op.FindPropertyRelative("pickupItem"), new GUIContent("Item"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Quest);
        }
    }

    // ═══════════════════════════════════════════
    // RewardNode
    // ═══════════════════════════════════════════

    public class RewardGraphNode : BaseGraphNode
    {
        public readonly QuestDefinition Quest;
        public readonly RewardNodeInfo Info;

        public RewardGraphNode(RewardNodeInfo info)
        {
            Quest = info.quest;
            Info = info;
            PersistKey = $"reward_{Quest.questId}";
            title = "🎁 Rewards";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Reward);

            MakeInPort("← Last Stage", PortSemantic.StageIn, GraphNodeColors.PortGray);

            var editorArea = new IMGUIContainer(DrawEditor);
            editorArea.style.minHeight = 50f;
            editorArea.style.flexGrow = 1;
            extensionContainer.Add(editorArea);

            RefreshExpandedState();
            expanded = true;
        }

        private void DrawEditor()
        {
            var so = new SerializedObject(Quest);
            so.Update();
            var rp = so.FindProperty("rewards");
            EditorGUILayout.PropertyField(rp.FindPropertyRelative("credits"), new GUIContent("Credits"));
            EditorGUILayout.PropertyField(rp.FindPropertyRelative("items"), new GUIContent("Items"), true);
            EditorGUILayout.PropertyField(rp.FindPropertyRelative("reputation"), new GUIContent("Reputation"), true);
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Quest);
        }
    }
}
#endif
