// GraphNodes v5.2 — 5 node types (Npc, Dialog, QuestRoot, Stage, Reward).
// Objectives live INSIDE StageNode (no separate ObjectiveGraphNode).
// StageNode has "→ Target NPC" port + "+Stage" "+Obj" buttons.

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Dialogue;

namespace ProjectC.Quests.Editor
{
    public static class GraphNodeColors
    {
        public static readonly Color Npc = new(0.45f, 0.30f, 0.65f);
        public static readonly Color Dialog = new(0.25f, 0.45f, 0.85f);
        public static readonly Color Quest = new(0.20f, 0.35f, 0.60f);
        public static readonly Color Stage = new(0.20f, 0.55f, 0.30f);
        public static readonly Color Reward = new(0.65f, 0.40f, 0.10f);
        public static readonly Color PortGray = new(0.45f, 0.45f, 0.45f);
        public static readonly Color PortGreen = new(0.35f, 0.70f, 0.35f);
        public static readonly Color PortBlue = new(0.35f, 0.50f, 0.90f);
        public static readonly Color PortPurple = new(0.50f, 0.35f, 0.80f);
        public static readonly Color PortOrange = new(0.90f, 0.50f, 0.10f);
    }

    public abstract class BaseGraphNode : Node
    {
        public string PersistKey;

        protected BaseGraphNode()
        {
            capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Resizable;
            style.minWidth = 200;
            style.minHeight = 60;
        }



        protected Port MakeOutPort(string name, PortSemantic semantic, Color color, object userData = null)
        {
            var p = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(bool));
            p.portName = name; p.portColor = color; p.userData = (semantic, userData); outputContainer.Add(p); return p;
        }

        protected Port MakeInPort(string name, PortSemantic semantic, Color color, object userData = null)
        {
            var p = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Multi, typeof(bool));
            p.portName = name; p.portColor = color; p.userData = (semantic, userData); inputContainer.Add(p); return p;
        }

        protected void AddPinButton(ScriptableObject asset)
        {
            if (asset == null) return;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            row.style.paddingRight = 4;
            row.style.paddingTop = 0;
            row.style.paddingBottom = 0;
            row.style.height = 18;
            var btn = new Button(() => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); });
            btn.text = "📌 " + asset.name;
            btn.style.fontSize = 9;
            btn.style.height = 16;
            btn.tooltip = "Ping asset: " + AssetDatabase.GetAssetPath(asset);
            row.Add(btn);
            extensionContainer.Insert(0, row);
        }

        public static PortSemantic GetSemantic(Port p) => p?.userData is (PortSemantic sem, _) ? sem : (PortSemantic)(-1);
        public static object GetPortData(Port p) => p?.userData is (_, object data) ? data : null;
    }


    // ═══════════════ NpcNode ═══════════════

    public class NpcGraphNode : BaseGraphNode
    {
        public readonly NpcDefinition Npc;
        public readonly NpcNodeInfo Info;
        public NpcGraphNode(NpcNodeInfo info)
        {
            Npc = info.npc; Info = info; PersistKey = $"npc_{Npc.npcId}";
            title = $"👤 {Npc.displayName}";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Npc);
            MakeOutPort("→ Dialog", PortSemantic.NpcDefaultDialog, GraphNodeColors.PortPurple);
            MakeOutPort("→ Offers Quest", PortSemantic.NpcOffersQuest, GraphNodeColors.PortOrange);
            MakeInPort("← Quest target", PortSemantic.NpcTargetedBy, GraphNodeColors.PortBlue);
            AddPinButton(Npc);
            var ea = new IMGUIContainer(DrawEditor); ea.style.minHeight = 130f; ea.style.flexGrow = 1;
            extensionContainer.style.paddingLeft = 4; extensionContainer.style.paddingRight = 4; extensionContainer.Add(ea);
            RefreshExpandedState(); expanded = true;
        }
        private void DrawEditor()
        {
            var so = new SerializedObject(Npc); so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("npcId"));
            EditorGUILayout.PropertyField(so.FindProperty("displayName"));
            EditorGUILayout.PropertyField(so.FindProperty("faction"));
            EditorGUILayout.PropertyField(so.FindProperty("portrait"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultDialogTree"));
            EditorGUILayout.PropertyField(so.FindProperty("services"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Npc);
        }

    }



    // ═══════════════ DialogNode ═══════════════

    public class DialogGraphNode : BaseGraphNode
    {
        public readonly DialogTree Tree;
        public readonly int NodeIndex;
        public readonly DialogNodeInfo Info;
        public System.Action OnModified;
        public DialogueNode DialogueNode => Tree?.nodes != null && NodeIndex >= 0 && NodeIndex < Tree.nodes.Length ? Tree.nodes[NodeIndex] : null;

        public DialogGraphNode(DialogNodeInfo info)
        {
            Tree = info.tree; NodeIndex = info.nodeIndex; Info = info;

            var node = DialogueNode;
            PersistKey = $"dlg_{Tree.treeId}_{node?.nodeId ?? NodeIndex.ToString()}";
            string speaker = node?.speaker?.speakerNpc != null ? node.speaker.speakerNpc.displayName : (node?.speaker?.speakerKind.ToString() ?? "Npc");
            string preview = node?.text ?? ""; if (preview.Length > 40) preview = preview.Substring(0, 37) + "...";
            title = $"💬 {speaker}: \"{preview}\"";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Dialog);
            MakeInPort("← In", PortSemantic.DialogIn, GraphNodeColors.PortGray);
            var edges = node?.edges;
            if (edges != null)
                for (int ei = 0; ei < edges.Length; ei++)
                {
                    if (edges[ei] == null) continue;
                    string label = string.IsNullOrEmpty(edges[ei].label) ? $"→ {ei}" : edges[ei].label;
                    if (label.Length > 16) label = label.Substring(0, 13) + "...";
                    Color c = edges[ei].action?.type == DialogueActionType.OfferQuest ? GraphNodeColors.PortOrange
                        : edges[ei].action?.type == DialogueActionType.SwitchDialogTree ? GraphNodeColors.PortPurple : GraphNodeColors.PortGreen;
                    MakeOutPort(label, PortSemantic.DialogEdgeAction, c, ei);
                }
            if (edges == null || edges.Length == 0) MakeOutPort("→", PortSemantic.DialogEdgeAction, GraphNodeColors.PortGreen, 0);
            AddPinButton(Tree);
            var ea = new IMGUIContainer(DrawEditor); ea.style.minHeight = 80f; ea.style.flexGrow = 1; ea.style.width = Length.Percent(100);






            extensionContainer.style.paddingLeft = 4; extensionContainer.style.paddingRight = 4; extensionContainer.Add(ea);
            RefreshExpandedState(); expanded = true;
        }
        private void DrawEditor()
        {
            var node = DialogueNode; if (node == null) return;
            var so = new SerializedObject(Tree); so.Update();
            var nodesProp = so.FindProperty("nodes"); if (NodeIndex >= nodesProp.arraySize) return;
            var np = nodesProp.GetArrayElementAtIndex(NodeIndex);
            EditorGUILayout.PropertyField(np.FindPropertyRelative("speaker"), new GUIContent("Speaker"), true);
            EditorGUILayout.PropertyField(np.FindPropertyRelative("text"), new GUIContent("Text"));
            EditorGUILayout.Space(2);



            EditorGUILayout.LabelField("Choices:", EditorStyles.boldLabel);

            var ep = np.FindPropertyRelative("edges");
            if (ep != null && ep.isArray)
            {
                for (int i = 0; i < ep.arraySize; i++)
                {
                    var edgeP = ep.GetArrayElementAtIndex(i); if (edgeP == null) continue;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.PropertyField(edgeP.FindPropertyRelative("label"), new GUIContent($"Choice {i+1}"));
                    EditorGUILayout.PropertyField(edgeP.FindPropertyRelative("action"), new GUIContent("Action"), true);
                    if (GUILayout.Button("× Remove", GUILayout.Width(80))) { ep.DeleteArrayElementAtIndex(i); so.ApplyModifiedProperties(); EditorUtility.SetDirty(Tree); OnModified?.Invoke(); GUIUtility.ExitGUI(); return; }

                    EditorGUILayout.EndVertical();
                }
                if (GUILayout.Button("+ Add Choice", GUILayout.Width(100))) { ep.arraySize++; so.ApplyModifiedProperties(); EditorUtility.SetDirty(Tree); OnModified?.Invoke(); GUIUtility.ExitGUI(); return; }

            }
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Tree);
        }
    }

    // ═══════════════ QuestRootNode ═══════════════

    public class QuestRootGraphNode : BaseGraphNode
    {
        public readonly QuestDefinition Quest;
        public readonly QuestNodeInfo Info;
        public System.Action<QuestDefinition> OnAddStage;

        public QuestRootGraphNode(QuestNodeInfo info)
        {
            Quest = info.quest; Info = info; PersistKey = $"quest_{Quest.questId}";
            title = $"📜 {Quest.displayName}";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Quest);
            MakeInPort("← Offered by", PortSemantic.QuestOfferedBy, GraphNodeColors.PortOrange);
            MakeOutPort("→ Stage 1", PortSemantic.StageNext, GraphNodeColors.PortGreen);

            var btnRow = new VisualElement(); btnRow.style.flexDirection = FlexDirection.Row; btnRow.style.paddingLeft = 4; btnRow.style.paddingTop = 2; btnRow.style.paddingBottom = 2;
            var addBtn = new Button(() => OnAddStage?.Invoke(Quest)) { text = "+ Stage" }; addBtn.style.fontSize = 10; addBtn.style.height = 20;
            btnRow.Add(addBtn); extensionContainer.Add(btnRow);
            AddPinButton(Quest);
            var ea = new IMGUIContainer(DrawEditor); ea.style.minHeight = 40f; ea.style.flexGrow = 1;

            extensionContainer.style.paddingLeft = 4; extensionContainer.style.paddingRight = 4; extensionContainer.Add(ea);
            RefreshExpandedState(); expanded = true;
        }
        private void DrawEditor()
        {
            var so = new SerializedObject(Quest); so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("questId"), new GUIContent("ID"));
            EditorGUILayout.PropertyField(so.FindProperty("description"), new GUIContent("Desc"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Quest);
        }
    }

    // ═══════════════ StageNode ═══════════════

    public class StageGraphNode : BaseGraphNode
    {
        public readonly QuestDefinition Quest;
        public readonly int StageIndex;
        public readonly StageNodeInfo Info;
        public QuestStage Stage => Quest?.stages != null && StageIndex >= 0 && StageIndex < Quest.stages.Length ? Quest.stages[StageIndex] : null;
        public System.Action<StageNodeInfo> OnDeleteStage, OnAddStageAfter;
        public System.Action<StageNodeInfo> OnAddObjective;
        public System.Func<int> StageCount;

        public StageGraphNode(StageNodeInfo info)
        {
            Quest = info.quest; StageIndex = info.stageIndex; Info = info;
            PersistKey = $"stage_{Quest.questId}_{Stage?.stageId ?? StageIndex.ToString()}";

            int num = StageCount?.Invoke() ?? 1;
            title = $"🟢 Stage {StageIndex+1}/{num}: {Stage?.stageId ?? "?"}";
            titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Stage);
            MakeInPort("← Prev", PortSemantic.StageIn, GraphNodeColors.PortGray);
            MakeOutPort("→ Next", PortSemantic.StageNext, GraphNodeColors.PortGreen);
            MakeOutPort("→ Target NPC", PortSemantic.StageTargetNpc, GraphNodeColors.PortBlue);

            var btnRow = new VisualElement(); btnRow.style.flexDirection = FlexDirection.Row; btnRow.style.paddingLeft = 4; btnRow.style.paddingTop = 2; btnRow.style.paddingBottom = 2;
            var addObjBtn = new Button(() => OnAddObjective?.Invoke(Info)) { text = "+ Obj" }; addObjBtn.style.fontSize = 10; addObjBtn.style.height = 20; btnRow.Add(addObjBtn);
            var addStageBtn = new Button(() => OnAddStageAfter?.Invoke(Info)) { text = "+ Stage" }; addStageBtn.style.fontSize = 10; addStageBtn.style.height = 20; addStageBtn.style.marginLeft = 4; btnRow.Add(addStageBtn);
            if (num > 1) { var delBtn = new Button(() => OnDeleteStage?.Invoke(Info)) { text = "× Stage" }; delBtn.style.fontSize = 10; delBtn.style.height = 20; delBtn.style.marginLeft = 4; delBtn.style.color = new StyleColor(new Color(0.9f, 0.4f, 0.3f)); btnRow.Add(delBtn); }
            extensionContainer.Add(btnRow);
            AddPinButton(Quest);
            var ea = new IMGUIContainer(DrawEditor); ea.style.minHeight = 120f; ea.style.flexGrow = 1; ea.style.width = Length.Percent(100);




            extensionContainer.style.paddingLeft = 4; extensionContainer.style.paddingRight = 4; extensionContainer.Add(ea);
            RefreshExpandedState(); expanded = true;
        }

        private void DrawEditor()
        {
            var stage = Stage; if (stage == null) return;
            var so = new SerializedObject(Quest); so.Update();
            var stagesProp = so.FindProperty("stages"); if (StageIndex >= stagesProp.arraySize) return;
            var sp = stagesProp.GetArrayElementAtIndex(StageIndex);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("stageId"), new GUIContent("ID"), GUILayout.MinWidth(80));
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("nextStageId"), new GUIContent("→ Next"), GUILayout.MinWidth(80));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(sp.FindPropertyRelative("description"), new GUIContent("Desc"));


            EditorGUILayout.PropertyField(sp.FindPropertyRelative("objectives"), new GUIContent("Objectives"), true);

            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Quest);
        }
    }

    // ═══════════════ RewardNode ═══════════════



    public class RewardGraphNode : BaseGraphNode
    {
        public readonly QuestDefinition Quest;
        public readonly RewardNodeInfo Info;
        public RewardGraphNode(RewardNodeInfo info)
        {
            Quest = info.quest; Info = info; PersistKey = $"reward_{Quest.questId}";
            title = "🎁 Rewards"; titleContainer.style.backgroundColor = new StyleColor(GraphNodeColors.Reward);
            MakeInPort("← Last Stage", PortSemantic.StageIn, GraphNodeColors.PortGray);
            AddPinButton(Quest);
            var ea = new IMGUIContainer(DrawEditor); ea.style.minHeight = 70f; ea.style.flexGrow = 1;
            extensionContainer.style.paddingLeft = 4; extensionContainer.style.paddingRight = 4; extensionContainer.Add(ea);
            RefreshExpandedState(); expanded = true;
        }
        private void DrawEditor()
        {
            var so = new SerializedObject(Quest); so.Update();
            var rp = so.FindProperty("rewards");
            EditorGUILayout.PropertyField(rp.FindPropertyRelative("credits"));
            EditorGUILayout.PropertyField(rp.FindPropertyRelative("items"), true);
            EditorGUILayout.PropertyField(rp.FindPropertyRelative("reputation"), true);
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(Quest);
        }

    }

}
#endif
