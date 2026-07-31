// Unified Quest Graph v4 — multi-NPC нодовый редактор.
// NpcCardNode (IMGUI: faction, dialogTree, questOffers) +
// DialogNodeView (IMGUI: SpeakerRefDrawer, Conditions, Actions) +
// Quest nodes (из базового QuestNodeGraphView).
// Авто-рёбра: NPC→Dialog, Dialog→Quest, Objective→NPC, SwitchDialogTree.

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
    // ═══════════════════════════════════════════
    // NpcCardNode
    // ═══════════════════════════════════════════

    public class NpcCardNode : QuestGraphNode
    {
        public NpcDefinition Npc { get; private set; }
        public SerializedObject SerializedNpc { get; private set; }

        private static readonly Color NpcColor = new Color(0.55f, 0.35f, 0.7f);

        public NpcCardNode(NpcDefinition npc)
        {
            Npc = npc;
            OwnerAsset = npc;
            SourceData = npc;
            NodeKind = QuestNodeKind.QuestRoot;
            PersistKey = $"npc_{npc.npcId}";
            viewDataKey = PersistKey;
            SerializedNpc = new SerializedObject(npc);

            title = $"👤 {npc.displayName}";
            titleContainer.style.backgroundColor = new StyleColor(NpcColor);

            var editorArea = new IMGUIContainer(DrawNpcEditor);
            editorArea.style.minHeight = 100f;
            editorArea.style.flexGrow = 1;
            extensionContainer.style.paddingLeft = 4;
            extensionContainer.style.paddingRight = 4;
            extensionContainer.Add(editorArea);

            var outPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(bool));
            outPort.portName = "→ Dialog";
            outPort.portColor = new Color(0.5f, 0.4f, 0.85f);
            outputContainer.Add(outPort);

            RefreshExpandedState();
            expanded = true;
        }

        private void DrawNpcEditor()
        {
            if (SerializedNpc == null) return;
            SerializedNpc.Update();
            var fp = SerializedNpc.FindProperty("faction");
            var dp = SerializedNpc.FindProperty("defaultDialogTree");
            var or = SerializedNpc.FindProperty("questOfferRefs");
            var tr = SerializedNpc.FindProperty("questTurnInRefs");
            if (fp != null) EditorGUILayout.PropertyField(fp, new GUIContent("Faction"));
            if (dp != null) EditorGUILayout.PropertyField(dp, new GUIContent("Dialog Tree"));
            if (or != null) EditorGUILayout.PropertyField(or, new GUIContent("Offers Quests"), true);
            if (tr != null) EditorGUILayout.PropertyField(tr, new GUIContent("Turns In Quests"), true);
            if (SerializedNpc.ApplyModifiedProperties()) EditorUtility.SetDirty(Npc);
        }
    }

    // ═══════════════════════════════════════════
    // DialogNodeView
    // ═══════════════════════════════════════════

    public class DialogNodeView : QuestGraphNode
    {
        public DialogueNode DialogueNode { get; private set; }
        public DialogTree DialogTree { get; private set; }
        public SerializedProperty NodeProperty { get; private set; }
        public SerializedObject SerializedTree { get; private set; }

        private readonly List<Port> _outputPorts = new List<Port>();
        private static readonly Color DialogColor = new Color(0.3f, 0.5f, 1.0f);

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

            SetTitle();
            titleContainer.style.backgroundColor = new StyleColor(DialogColor);

            var editorArea = new IMGUIContainer(DrawNodeEditor);
            editorArea.style.minHeight = 240f;
            editorArea.style.flexGrow = 1;
            extensionContainer.style.paddingLeft = 4;
            extensionContainer.style.paddingRight = 4;
            extensionContainer.Add(editorArea);

            var inPort = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Single, typeof(bool));
            inPort.portName = "← In";
            inPort.portColor = new Color(0.55f, 0.55f, 0.55f);
            inputContainer.Add(inPort);

            RebuildOutputPorts();
            RefreshExpandedState();
            expanded = true;
        }

        private void SetTitle()
        {
            string sn = ResolveSpeakerName(DialogueNode);
            string tp = DialogueNode?.text?.Length > 40 ? DialogueNode.text.Substring(0, 37) + "..." : (DialogueNode?.text ?? "");
            title = $"🤖 {sn}: \"{tp}\"";
        }

        public void RebuildOutputPorts()
        {
            foreach (var p in _outputPorts) { if (p.parent != null) p.parent.Remove(p); }
            _outputPorts.Clear();
            var edges = DialogueNode?.edges;
            if (edges != null)
            {
                foreach (var e in edges)
                {
                    if (e == null) continue;
                    var p = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                    string l = string.IsNullOrEmpty(e.label) ? "→" : e.label;
                    if (l.Length > 18) l = l.Substring(0, 15) + "...";
                    p.portName = l;
                    p.portColor = new Color(0.35f, 0.7f, 0.35f);
                    outputContainer.Add(p);
                    _outputPorts.Add(p);
                }
            }
            if (_outputPorts.Count == 0)
            {
                var p = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
                p.portName = "→ End";
                p.portColor = new Color(0.9f, 0.3f, 0.3f);
                outputContainer.Add(p);
                _outputPorts.Add(p);
            }
        }

        public IReadOnlyList<Port> GetOutputPorts() => _outputPorts;

        private void DrawNodeEditor()
        {
            if (NodeProperty == null || SerializedTree == null) return;
            SerializedTree.Update();

            var sp = NodeProperty.FindPropertyRelative("speaker");
            if (sp != null) EditorGUILayout.PropertyField(sp, new GUIContent("Speaker"), true);

            var tp = NodeProperty.FindPropertyRelative("text");
            if (tp != null) EditorGUILayout.PropertyField(tp, new GUIContent("Text"));

            var ep = NodeProperty.FindPropertyRelative("portraitEmotion");
            if (ep != null) EditorGUILayout.PropertyField(ep, new GUIContent("Emotion"));

            var oe = NodeProperty.FindPropertyRelative("onEnterActions");
            if (oe != null) EditorGUILayout.PropertyField(oe, new GUIContent("On Enter Actions"), true);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Choices:", EditorStyles.boldLabel);

            var edgesProp = NodeProperty.FindPropertyRelative("edges");
            if (edgesProp != null && edgesProp.isArray)
            {
                for (int i = 0; i < edgesProp.arraySize; i++)
                {
                    var edgeP = edgesProp.GetArrayElementAtIndex(i);
                    if (edgeP == null) continue;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    EditorGUILayout.PropertyField(edgeP.FindPropertyRelative("label"), new GUIContent($"Choice {i+1}"));

                    var tgtP = edgeP.FindPropertyRelative("targetNodeId");
                    if (tgtP != null)
                    {
                        var ids = GetSiblingIds();
                        int sel = ids.IndexOf(tgtP.stringValue);
                        int nw = EditorGUILayout.Popup("→ Target", sel + 1, ToChoices(ids));
                        if (nw != sel + 1) tgtP.stringValue = (nw == 0) ? "" : ids[nw - 1];
                    }

                    var cp = edgeP.FindPropertyRelative("conditions");
                    if (cp != null) EditorGUILayout.PropertyField(cp, new GUIContent("Conditions"), true);

                    var ap = edgeP.FindPropertyRelative("action");
                    if (ap != null) EditorGUILayout.PropertyField(ap, new GUIContent("Action"), true);

                    EditorGUILayout.PropertyField(edgeP.FindPropertyRelative("hideIfUnavailable"), new GUIContent("Hide if unavailable"));

                    if (GUILayout.Button("× Remove Choice", GUILayout.Width(120)))
                    {
                        edgesProp.DeleteArrayElementAtIndex(i);
                        SerializedTree.ApplyModifiedProperties();
                        EditorUtility.SetDirty(DialogTree);
                        RebuildOutputPorts();
                        SetTitle();
                        GUIUtility.ExitGUI();
                        return;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(3);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add Choice", GUILayout.Width(120)))
                {
                    edgesProp.arraySize++;
                    var ne = edgesProp.GetArrayElementAtIndex(edgesProp.arraySize - 1);
                    ne.FindPropertyRelative("label").stringValue = "New Choice";
                    ne.FindPropertyRelative("hideIfUnavailable").boolValue = true;
                    SerializedTree.ApplyModifiedProperties();
                    EditorUtility.SetDirty(DialogTree);
                    RebuildOutputPorts();
                    GUIUtility.ExitGUI();
                    return;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (SerializedTree.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(DialogTree);
                SetTitle();
            }
        }

        private List<string> GetSiblingIds()
        {
            var ids = new List<string>();
            if (DialogTree?.nodes == null) return ids;
            foreach (var n in DialogTree.nodes)
                if (n != null && !string.IsNullOrEmpty(n.nodeId)) ids.Add(n.nodeId);
            return ids;
        }

        private string[] ToChoices(List<string> ids)
        {
            var r = new string[ids.Count + 1];
            r[0] = "(end conversation)";
            for (int i = 0; i < ids.Count; i++) r[i + 1] = ids[i];
            return r;
        }

        private static string ResolveSpeakerName(DialogueNode n)
        {
            if (n?.speaker == null) return "???";
            if (n.speaker.speakerNpc != null) return n.speaker.speakerNpc.displayName;
            if (!string.IsNullOrEmpty(n.speaker.refId)) return n.speaker.refId;
            return n.speaker.speakerKind.ToString();
        }
    }

    // ═══════════════════════════════════════════
    // UnifiedQuestGraphView
    // ═══════════════════════════════════════════

    public class UnifiedQuestGraphView : QuestNodeGraphView
    {
        private readonly List<NpcCardNode> _npcCards = new List<NpcCardNode>();
        private readonly Dictionary<string, List<DialogNodeView>> _dialogGroups = new Dictionary<string, List<DialogNodeView>>();
        private readonly List<QuestDefinition> _loadedQuests = new List<QuestDefinition>();

        private static readonly Color NpcDialogC = new Color(0.5f, 0.4f, 0.85f);
        private static readonly Color DialogQuestC = new Color(0.9f, 0.5f, 0.1f);
        private static readonly Color QuestNpcC = new Color(0.3f, 0.8f, 0.5f);
        private static readonly Color SwitchDlgC = new Color(0.8f, 0.4f, 0.8f);

        public NpcDefinition[] LoadedNpcs => _npcCards.Select(c => c.Npc).ToArray();
        public DialogTree[] LoadedDialogs => _dialogGroups.Values
            .SelectMany(v => v).Select(v => v.DialogTree).Distinct().ToArray();
        public QuestDefinition[] LoadedQuests => _loadedQuests.ToArray();

        public void AddNpc(NpcDefinition npc)
        {
            if (npc == null || _npcCards.Any(c => c.Npc == npc)) return;

            var card = new NpcCardNode(npc);
            _npcCards.Add(card);
            AddElement(card);

            if (npc.defaultDialogTree != null && !_dialogGroups.ContainsKey(npc.defaultDialogTree.treeId))
            {
                LoadDialogTreeInternal(npc.defaultDialogTree, card);
            }

            if (npc.questOfferRefs != null)
            {
                foreach (var q in npc.questOfferRefs)
                {
                    if (q != null && !_loadedQuests.Contains(q))
                        AddQuestInternal(q);
                }
            }

            ApplyLayout();
            schedule.Execute(() => { MarkDirtyRepaint(); FrameAll(); }).StartingIn(100);
        }

        public void AddQuest(QuestDefinition q)
        {
            if (q == null || _loadedQuests.Contains(q)) return;
            AddQuestInternal(q);
            ApplyLayout();
            MarkDirtyRepaint();
        }

        public void ClearAll()
        {
            _npcCards.Clear();
            _dialogGroups.Clear();
            _loadedQuests.Clear();
            ClearAllElements();
        }

        private void LoadDialogTreeInternal(DialogTree tree, NpcCardNode npcCard)
        {
            var so = new SerializedObject(tree);
            var nodesProp = so.FindProperty("nodes");
            var views = new List<DialogNodeView>();

            if (tree.nodes != null)
            {
                for (int i = 0; i < tree.nodes.Length; i++)
                {
                    if (tree.nodes[i] == null) continue;
                    var v = new DialogNodeView(tree.nodes[i], tree, i, nodesProp.GetArrayElementAtIndex(i));
                    views.Add(v);
                    AddElement(v);
                }
            }
            _dialogGroups[tree.treeId] = views;

            // NPC → Dialog root
            if (views.Count > 0)
            {
                var root = views.FirstOrDefault(v => v.DialogueNode.nodeId == tree.rootNodeId) ?? views[0];
                var ri = root.inputContainer.Children().FirstOrDefault() as Port;
                var co = npcCard.outputContainer.Children().FirstOrDefault() as Port;
                if (ri != null && co != null) AddEdge(co, ri, "npc-dialog", NpcDialogC, NpcDialogC);

                RebuildDialogEdges(tree, views);
                CreateDialogToQuestEdges(tree, views);
                CreateSwitchDialogEdges(tree, views);
            }
        }

        private void AddQuestInternal(QuestDefinition q)
        {
            _loadedQuests.Add(q);

            var questColor = new Color(0.20f, 0.35f, 0.60f);
            var stageColor = new Color(0.20f, 0.55f, 0.30f);
            var objColor = new Color(0.55f, 0.40f, 0.10f);
            var rewardColor = new Color(0.65f, 0.40f, 0.10f);

            var qn = MakeEditableNode($"📜 {q.questId}", questColor,
                new (string, string, System.Action<string>)[] {
                    ("Name", q.displayName, v => q.displayName = v),
                    ("Desc", q.description ?? "", v => q.description = v)
                },
                $"stages: {q.stages?.Length ?? 0}", q, "", null, QuestNodeKind.QuestRoot);
            AddElement(qn);
            var qPort = AddPorts(qn, true, false);

            var stages = new List<QuestGraphNode>();

            if (q.stages != null)
            {
                for (int i = 0; i < q.stages.Length; i++)
                {
                    var s = q.stages[i];
                    if (s == null) continue;
                    int si = i;
                    var sn = MakeEditableNode($"🟢 {s.stageId}", stageColor,
                        new (string, string, System.Action<string>)[] {
                            ("ID", s.stageId, v => q.stages[si].stageId = v),
                            ("Desc", s.description ?? "", v => q.stages[si].description = v)
                        },
                        $"objectives: {s.objectives?.Length ?? 0}",
                        q, $"stages[{i}]", s, QuestNodeKind.Stage);
                    AddElement(sn);
                    var sp = AddPorts(sn, true, true);
                    stages.Add(sn);

                    if (i == 0) ConnectPorts(qPort.output, sp.input);
                    if (i > 0) ConnectPorts(GetOutputPort(stages[i - 1]), sp.input);

                    if (s.objectives != null)
                    {
                        for (int j = 0; j < s.objectives.Length; j++)
                        {
                            var o = s.objectives[j];
                            if (o == null) continue;
                            int oi = j; int stIdx = i;
                            string ti = o.targetNpc != null ? $"→ {o.targetNpc.displayName}" : "";
                            var on = MakeEditableNode($"🎯 {o.objectiveType}", objColor,
                                new (string, string, System.Action<string>)[] {
                                    ("Npc", o.targetNpcId ?? "", v => q.stages[stIdx].objectives[oi].targetNpcId = v),
                                    ("Qty", $"{o.requiredQuantity}", v => { if (int.TryParse(v, out var n)) q.stages[stIdx].objectives[oi].requiredQuantity = n; })
                                },
                                ti, q, $"stages[{stIdx}].objectives[{oi}]", o, QuestNodeKind.Objective);
                            AddElement(on);
                            var op = AddPorts(on, false, true);
                            ConnectPorts(sp.output, op.input);

                            // Auto-edge: Objective → NPC
                            if (o.targetNpc != null)
                                CreateObjectiveNpcEdge(on, o.targetNpc);
                        }
                    }
                }
            }

            if (q.rewards != null && HasReward(q.rewards))
            {
                var r = q.rewards;
                var rn = MakeEditableNode("🎁 REWARDS", rewardColor,
                    new (string, string, System.Action<string>)[] {
                        ("Credits", r.credits.ToString(), v => { if (int.TryParse(v, out var n)) r.credits = n; })
                    },
                    $"💰 {r.credits} CR", q, "rewards", r, QuestNodeKind.Reward);
                AddElement(rn);
                var rp = AddPorts(rn, false, true);
                if (stages.Count > 0) ConnectPorts(GetOutputPort(stages[stages.Count - 1]), rp.input);
            }
        }

        private void CreateObjectiveNpcEdge(QuestGraphNode objNode, NpcDefinition targetNpc)
        {
            var npcCard = _npcCards.FirstOrDefault(c => c.Npc == targetNpc);
            if (npcCard == null) return;

            // Add output port to objective
            var objOut = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Single, typeof(bool));
            objOut.portName = "→ NPC";
            objOut.portColor = QuestNpcC;
            objNode.outputContainer.Add(objOut);

            // Add input port to NPC card
            var npcIn = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Multi, typeof(bool));
            npcIn.portName = "← Quest";
            npcIn.portColor = QuestNpcC;
            npcCard.inputContainer.Add(npcIn);

            AddEdge(objOut, npcIn, "quest-npc", QuestNpcC, QuestNpcC);
        }

        private void RebuildDialogEdges(DialogTree tree, List<DialogNodeView> views)
        {
            var nodeMap = views.ToDictionary(v => v.DialogueNode.nodeId, v => v);
            // Remove old internal dialog edges
            var old = edges.ToList().Where(e => e.viewDataKey == "dialog-internal").ToList();
            foreach (var e in old) RemoveElement(e);

            foreach (var src in views)
            {
                var srcNode = src.DialogueNode;
                if (srcNode?.edges == null) continue;
                var outPorts = src.GetOutputPorts();
                for (int ei = 0; ei < srcNode.edges.Length && ei < outPorts.Count; ei++)
                {
                    var ed = srcNode.edges[ei];
                    if (ed == null || string.IsNullOrEmpty(ed.targetNodeId)) continue;
                    if (!nodeMap.TryGetValue(ed.targetNodeId, out var tgt)) continue;
                    var ti = tgt.inputContainer.Children().FirstOrDefault() as Port;
                    if (ti != null) AddEdge(outPorts[ei], ti, "dialog-internal",
                        new Color(0.35f, 0.5f, 0.9f), new Color(0.35f, 0.5f, 0.9f));
                }
            }
        }

        private void CreateDialogToQuestEdges(DialogTree tree, List<DialogNodeView> views)
        {
            if (tree.nodes == null) return;
            foreach (var n in tree.nodes)
            {
                if (n?.edges == null) continue;
                for (int ei = 0; ei < n.edges.Length; ei++)
                {
                    var ed = n.edges[ei];
                    if (ed?.action?.type == DialogueActionType.OfferQuest && ed.action.questRef != null)
                    {
                        var src = views.FirstOrDefault(v => v.DialogueNode.nodeId == n.nodeId);
                        if (src == null) continue;
                        var outPorts = src.GetOutputPorts();
                        if (ei >= outPorts.Count) continue;
                        var q = ed.action.questRef;
                        if (q.stages == null || q.stages.Length == 0) continue;
                        var stageN = nodes.ToList().FirstOrDefault(x =>
                            x is QuestGraphNode gn && gn.NodeKind == QuestNodeKind.Stage && gn.OwnerAsset == q);
                        if (stageN == null) continue;
                        var si = (stageN as Node)?.inputContainer.Children().FirstOrDefault() as Port;
                        if (si != null) AddEdge(outPorts[ei], si, "dialog-quest", DialogQuestC, DialogQuestC);
                    }
                }
            }
        }

        private void CreateSwitchDialogEdges(DialogTree tree, List<DialogNodeView> views)
        {
            if (tree.nodes == null) return;
            foreach (var n in tree.nodes)
            {
                if (n?.edges == null) continue;
                for (int ei = 0; ei < n.edges.Length; ei++)
                {
                    var ed = n.edges[ei];
                    if (ed?.action?.type == DialogueActionType.SwitchDialogTree && ed.action.dialogTreeRef != null)
                    {
                        var src = views.FirstOrDefault(v => v.DialogueNode.nodeId == n.nodeId);
                        if (src == null) continue;
                        var outPorts = src.GetOutputPorts();
                        if (ei >= outPorts.Count) continue;
                        var tt = ed.action.dialogTreeRef;
                        if (!_dialogGroups.TryGetValue(tt.treeId, out var tViews)) continue;
                        var tgt = tViews.FirstOrDefault(v => v.DialogueNode.nodeId == tt.rootNodeId) ?? tViews.FirstOrDefault();
                        if (tgt == null) continue;
                        var ti = tgt.inputContainer.Children().FirstOrDefault() as Port;
                        if (ti != null) AddEdge(outPorts[ei], ti, "switch-dialog", SwitchDlgC, SwitchDlgC);
                    }
                }
            }
        }

        private void AddEdge(Port o, Port i, string key, Color ic, Color oc)
        {
            if (o == null || i == null) return;
            var e = o.ConnectTo(i);
            e.viewDataKey = key;
            e.edgeControl.inputColor = ic;
            e.edgeControl.outputColor = oc;
            AddElement(e);
        }

        private void ApplyLayout()
        {
            float x = 0f;
            const float NPC_W = 280f, NPC_H = 220f;
            const float DLG_W = 360f, DLG_H = 420f;
            const float GAP = 30f;

            foreach (var card in _npcCards)
            {
                card.SetPosition(new Rect(x, 0f, NPC_W, NPC_H));

                if (card.Npc.defaultDialogTree != null &&
                    _dialogGroups.TryGetValue(card.Npc.defaultDialogTree.treeId, out var dvs))
                {
                    float dy = NPC_H + GAP;
                    foreach (var dv in dvs)
                    {
                        dv.SetPosition(new Rect(x, dy, DLG_W, DLG_H));
                        dy += DLG_H + 20f;
                    }
                }

                x += NPC_W + DLG_W + GAP;
            }

            // Quest nodes right of NPCs
            float qx = x + GAP;
            float qy = 0f;
            foreach (var q in _loadedQuests)
            {
                var qns = nodes.ToList().Where(n => n is QuestGraphNode gn &&
                    (gn.NodeKind == QuestNodeKind.QuestRoot || gn.NodeKind == QuestNodeKind.Stage ||
                     gn.NodeKind == QuestNodeKind.Objective || gn.NodeKind == QuestNodeKind.Reward) &&
                    gn.OwnerAsset == q).ToList();

                float cy = qy;
                foreach (var n in qns)
                {
                    var p = n.GetPosition();
                    n.SetPosition(new Rect(qx, cy, 240f, p.height > 0 ? p.height : 140f));
                    cy += (p.height > 0 ? p.height : 140f) + 15f;
                }
                qx += 300f;
            }
        }

        protected override void OnEdgeCreated(Edge edge)
        {
            base.OnEdgeCreated(edge);

            if (edge.output?.node is DialogNodeView dlg &&
                edge.input?.node is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Stage)
            {
                var q = qn.OwnerAsset as QuestDefinition;
                if (q != null)
                {
                    var list = dlg.DialogueNode.edges?.ToList() ?? new List<DialogueEdge>();
                    list.Add(new DialogueEdge
                    {
                        label = $"Offer: {q.displayName}",
                        action = new DialogueAction { type = DialogueActionType.OfferQuest, questRef = q, stringParam = q.questId }
                    });
                    dlg.DialogueNode.edges = list.ToArray();
                    dlg.RebuildOutputPorts();
                    EditorUtility.SetDirty(dlg.DialogTree);
                }
                edge.viewDataKey = "dialog-quest";
                edge.edgeControl.inputColor = DialogQuestC;
                edge.edgeControl.outputColor = DialogQuestC;
            }

            if (edge.output?.node is QuestGraphNode objN && objN.NodeKind == QuestNodeKind.Objective &&
                edge.input?.node is NpcCardNode npcC)
            {
                var obj = objN.SourceData as QuestObjective;
                if (obj != null)
                {
                    obj.targetNpc = npcC.Npc;
                    obj.targetNpcId = npcC.Npc.npcId;
                    EditorUtility.SetDirty(objN.OwnerAsset);
                }
                edge.viewDataKey = "quest-npc";
                edge.edgeControl.inputColor = QuestNpcC;
                edge.edgeControl.outputColor = QuestNpcC;
            }
        }
    }

    // ═══════════════════════════════════════════
    // UnifiedQuestGraphWindow
    // ═══════════════════════════════════════════

    public class UnifiedQuestGraphWindow : EditorWindow
    {
        private UnifiedQuestGraphView _graph;
        private UnityEditor.UIElements.ObjectField _npcField;
        private UnityEditor.UIElements.ObjectField _questField;
        private Label _statusLabel;

        [MenuItem("Tools/Project C/Quests/Unified Quest Graph", priority = 100)]
        public static void Open()
        {
            var w = GetWindow<UnifiedQuestGraphWindow>();
            w.titleContent = new GUIContent("Unified Quest Graph");
            w.minSize = new Vector2(1200, 750);
            w.Show();
        }

        private void OnEnable()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

            var tb = new VisualElement();
            tb.style.flexDirection = FlexDirection.Row;
            tb.style.paddingTop = 4; tb.style.paddingBottom = 4;
            tb.style.paddingLeft = 6; tb.style.paddingRight = 6;
            tb.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f, 1f));

            _npcField = new UnityEditor.UIElements.ObjectField("+NPC")
                { objectType = typeof(NpcDefinition), allowSceneObjects = false };
            _npcField.style.width = 180;
            _npcField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is NpcDefinition npc) { _graph.AddNpc(npc); _npcField.SetValueWithoutNotify(null); UpdateStatus(); }
            });
            tb.Add(_npcField);

            _questField = new UnityEditor.UIElements.ObjectField("+Quest")
                { objectType = typeof(QuestDefinition), allowSceneObjects = false };
            _questField.style.width = 180; _questField.style.marginLeft = 4;
            _questField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is QuestDefinition q) { _graph.AddQuest(q); _questField.SetValueWithoutNotify(null); UpdateStatus(); }
            });
            tb.Add(_questField);

            var fitBtn = new Button(() => _graph?.FrameAll()) { text = "⊡ Fit" };
            fitBtn.style.marginLeft = 6; tb.Add(fitBtn);

            var saveBtn = new Button(() =>
            {
                _graph?.SaveQuest();
                foreach (var n in _graph?.LoadedNpcs ?? new NpcDefinition[0]) EditorUtility.SetDirty(n);
                foreach (var d in _graph?.LoadedDialogs ?? new DialogTree[0]) EditorUtility.SetDirty(d);
                AssetDatabase.SaveAssets();
                Debug.Log("[UnifiedGraph] Saved all.");
            }) { text = "💾 Save All" };
            saveBtn.style.marginLeft = 4; tb.Add(saveBtn);

            var clearBtn = new Button(() => { _graph?.ClearAll(); UpdateStatus(); }) { text = "✕ Clear" };
            clearBtn.style.marginLeft = 4; tb.Add(clearBtn);

            root.Add(tb);

            _graph = new UnifiedQuestGraphView();
            _graph.style.flexGrow = 1;
            root.Add(_graph);

            _statusLabel = new Label("Drag NPC into '+NPC' field above to start building");
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.bottom = 4; _statusLabel.style.left = 6;
            _statusLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
            _statusLabel.style.fontSize = 10;
            root.Add(_statusLabel);
        }

        private void UpdateStatus()
        {
            if (_graph == null || _statusLabel == null) return;
            int nc = _graph.nodes.ToList().Count;
            int ec = _graph.edges.ToList().Count;
            int npcC = _graph.LoadedNpcs?.Length ?? 0;
            int qC = _graph.LoadedQuests?.Length ?? 0;
            int dC = _graph.LoadedDialogs?.Length ?? 0;
            _statusLabel.text = $"NPCs: {npcC}  |  Quests: {qC}  |  Dialogs: {dC}  |  Nodes: {nc}  |  Edges: {ec}";
        }

        public void LoadUnified(QuestDefinition quest, DialogTree dialogTree, NpcDefinition npc = null)
        {
            if (npc != null) _graph.AddNpc(npc);
            if (quest != null) _graph.AddQuest(quest);
            UpdateStatus();
        }
    }
}
#endif
