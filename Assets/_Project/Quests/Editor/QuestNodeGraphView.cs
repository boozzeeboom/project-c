// QuestNodeGraphView + QuestNodeGraphWindow
// GraphView-based implementation (Nodes + Edges) — чистая попытка с нуля.
// T-U01: Model-driven OnGraphViewChanged — разрешить все мутации,
//       сохранять позиции нод, убрать _suppressReadOnly.
// 1) Ports + Edges через граф с schedule repaint на след. кадр
// 2) expanded=true + RefreshExpandedState в правильном порядке
// 3) Model-driven: graphViewChanged разрешает всё, хуки для SO-мутаций
// 4) Vertical layout (flow сверху-вниз) → BFS авто-лейаут (T-U03)
// 5) Первый и единственный output port на node (Multi capacity)

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests;
using ProjectC.Factions;

namespace ProjectC.Quests.Editor
{
    public class QuestNodeGraphView : GraphView
    {
        public QuestDefinition Quest { get; protected set; }

        // T-U01: node position tracking (serializable for persistence)
        protected readonly Dictionary<string, Vector2> _nodePositions = new Dictionary<string, Vector2>();

        // T-Q30: edit state
        private bool _editMode;
        // T-Q32: button visibility helpers
        protected readonly List<VisualElement> _editButtons = new List<VisualElement>();
        // T-Q33: multi-quest mode
#pragma warning disable CS0414
        private bool _showAllMode;
#pragma warning restore CS0414
        private const string DATABASE_PATH = "Assets/_Project/Quests/Data/QuestDatabase.asset";

        // T-U02: persisted node count for unique naming
        protected int _nodeCounter;

        public bool EditMode
        {
            get => _editMode;
            set
            {
                _editMode = value;
                RefreshAllEditUI();
                foreach (var btn in _editButtons)
                    btn.style.display = _editMode ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

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
            // KEY FIX #3: T-Q32 restore edit mode UI after rebuild
            if (_editMode)
            {
                schedule.Execute(() => { EditMode = true; }).StartingIn(40);
            }
        }

        // ========== T-Q33: Multi-quest mode ==========

        public void ShowAllQuests()
        {
            _showAllMode = true;
            var db = AssetDatabase.LoadAssetAtPath<QuestDatabase>(DATABASE_PATH);
            if (db == null || db.quests == null || db.quests.Length == 0) { _showAllMode = false; return; }
            Quest = null;
            ClearAllElements();
            BuildAllQuestsGraph(db.quests);
            schedule.Execute(() => { MarkDirtyRepaint(); FrameAll(); }).StartingIn(100);
        }

        public void LoadSingleQuest(QuestDefinition q)
        {
            _showAllMode = false;
            LoadQuest(q);
        }

        private void BuildAllQuestsGraph(QuestDefinition[] allQuests)
        {
            var questColor = new Color(0.20f, 0.35f, 0.60f);
            var questNodes = new Dictionary<string, Node>();
            float x = 0f;
            const float W = 220f, H = 80f, GAP = 30f;

            foreach (var q in allQuests)
            {
                if (q == null) continue;
                var n = MakeEditableNode("📜 " + q.questId, questColor,
                    new (string label, string value, System.Action<string> onSave)[] {
                        ("", q.displayName, null)
                    },
                    q.questId);
                // T-Q33: add ports for prerequisite edges
                AddPorts(n, hasOutput: true, hasInput: true);
                n.SetPosition(new Rect(x, 0, W, H));
                AddElement(n);
                questNodes[q.questId] = n;
                x += W + GAP;
            }

            // Draw prerequisite edges
            foreach (var q in allQuests)
            {
                if (q == null || q.prerequisites == null) continue;
                if (!questNodes.TryGetValue(q.questId, out var toNode)) continue;
                var toInput = GetInputPort(toNode);
                if (toInput == null) continue;

                foreach (var prereq in q.prerequisites)
                {
                    if (prereq == null || prereq.type != QuestPrerequisiteType.QuestCompleted) continue;
                    string targetId = prereq.stringParam;
                    if (string.IsNullOrEmpty(targetId) || !questNodes.TryGetValue(targetId, out var fromNode)) continue;
                    var fromOutput = GetOutputPort(fromNode);
                    if (fromOutput == null) continue;

                    var edge = fromOutput.ConnectTo(toInput);
                    edge.viewDataKey = "prereq";
                    // Make visually distinct: orange color + thicker
                    edge.edgeControl.inputColor = new Color(0.9f, 0.5f, 0.1f);
                    edge.edgeControl.outputColor = new Color(0.9f, 0.5f, 0.1f);
                    AddElement(edge);
                }
            }
        }

        protected void ForceAllNodesExpanded()
        {
            foreach (var n in nodes.Cast<Node>())
            {
                n.expanded = true;
                n.RefreshExpandedState();
            }
            MarkDirtyRepaint();
        }

        // T-U01: no more _suppressReadOnly — graphViewChanged allows all deletions
        protected void ClearAllElements()
        {
            var edgeList = new List<GraphElement>(this.edges.ToList());
            var nodeList = new List<GraphElement>(this.nodes.ToList());
            if (edgeList.Count > 0) DeleteElements(edgeList);
            if (nodeList.Count > 0) DeleteElements(nodeList);
            _editButtons.Clear();
            _nodePositions.Clear();
            _nodeCounter = 0;
        }

        // T-U03: auto-layout constants
        private const float V_GAP = 40f;
        private const float H_GAP = 60f;
        private const float NODE_W = 240f;

        /// <summary>
        /// T-U03: BFS-based auto-layout.
        /// Places nodes in layers, respecting manual positions from _nodePositions.
        /// </summary>
        private void ApplyAutoLayout(List<QuestGraphNode> orderedNodes, QuestGraphNode rootNode)
        {
            if (orderedNodes == null || orderedNodes.Count == 0) return;

            var children = new Dictionary<QuestGraphNode, List<QuestGraphNode>>();
            foreach (var n in orderedNodes)
                children[n] = new List<QuestGraphNode>();

            foreach (var edge in this.edges.ToList())
            {
                if (edge.output?.node is QuestGraphNode parent &&
                    edge.input?.node is QuestGraphNode child &&
                    children.ContainsKey(parent))
                {
                    if (!children[parent].Contains(child))
                        children[parent].Add(child);
                }
            }

            var visited = new HashSet<QuestGraphNode>();
            var queue = new Queue<QuestGraphNode>();
            var layer = new Dictionary<QuestGraphNode, int>();
            var layerNodes = new SortedDictionary<int, List<QuestGraphNode>>();

            if (rootNode != null)
            {
                queue.Enqueue(rootNode);
                layer[rootNode] = 0;
                visited.Add(rootNode);
            }
            else if (orderedNodes.Count > 0)
            {
                queue.Enqueue(orderedNodes[0]);
                layer[orderedNodes[0]] = 0;
                visited.Add(orderedNodes[0]);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int curLayer = layer[current];

                if (!layerNodes.ContainsKey(curLayer))
                    layerNodes[curLayer] = new List<QuestGraphNode>();
                layerNodes[curLayer].Add(current);

                if (children.TryGetValue(current, out var kids))
                {
                    foreach (var child in kids)
                    {
                        if (!visited.Contains(child))
                        {
                            visited.Add(child);
                            layer[child] = curLayer + 1;
                            queue.Enqueue(child);
                        }
                    }
                }
            }

            int maxLayer = layerNodes.Count > 0 ? layerNodes.Keys.Max() : 0;
            foreach (var n in orderedNodes)
            {
                if (!visited.Contains(n))
                {
                    maxLayer++;
                    if (!layerNodes.ContainsKey(maxLayer))
                        layerNodes[maxLayer] = new List<QuestGraphNode>();
                    layerNodes[maxLayer].Add(n);
                    visited.Add(n);
                }
            }

            float x = 0f;
            foreach (var kvp in layerNodes)
            {
                float y = 0f;
                foreach (var n in kvp.Value)
                {
                    if (_nodePositions.TryGetValue(n.PersistKey, out var manualPos))
                        n.SetPosition(new Rect(manualPos.x, manualPos.y, NODE_W, n.GetPosition().height));
                    else
                        n.SetPosition(new Rect(x, y, NODE_W, n.GetPosition().height));
                    y += n.GetPosition().height + V_GAP;
                }
                x += NODE_W + H_GAP;
            }
        }

        private void BuildGraph()
        {
            if (Quest == null) return;

            // T-U03: positions set by ApplyAutoLayout, not fixed columns
            var allNodes = new List<QuestGraphNode>();

            var questColor = new Color(0.20f, 0.35f, 0.60f);
            var stageColor = new Color(0.20f, 0.55f, 0.30f);
            var objColor = new Color(0.55f, 0.40f, 0.10f);
            var rewardColor = new Color(0.65f, 0.40f, 0.10f);

            // Quest root node
            var qn = MakeEditableNode("📜 QUEST", questColor,
                new (string label, string value, System.Action<string> onSave)[] {
                    ("Name", Quest.displayName, v => Quest.displayName = v),
                    ("Desc", Quest.description ?? "", v => Quest.description = v)
                },
                $"id: {Quest.questId}  •  stages: {Quest.stages?.Length ?? 0}",
                Quest, "", null, QuestNodeKind.QuestRoot);
            var addStageBtn = MakeEditButton("+ Add Stage", () => AddStage(), "add-stage");
            qn.extensionContainer.Add(addStageBtn);
            _editButtons.Add(addStageBtn);
            AddElement(qn);
            var qPort = AddPorts(qn, hasOutput: true, hasInput: false);
            allNodes.Add(qn);

            int stageCount = Quest.stages?.Length ?? 0;
            var stageNodes = new List<QuestGraphNode>();

            if (Quest.stages != null)
            {
                for (int i = 0; i < Quest.stages.Length; i++)
                {
                    var stage = Quest.stages[i];
                    if (stage == null) continue;

                    int si = i;
                    var sn = MakeEditableNode($"STAGE {i+1}/{stageCount}", stageColor,
                        new (string label, string value, System.Action<string> onSave)[] {
                            ("ID", stage.stageId, v => Quest.stages[si].stageId = v),
                            ("Desc", stage.description ?? "", v => Quest.stages[si].description = v)
                        },
                        (stage.objectives != null ? $"🎯 {stage.objectives.Length} objective(s)" : "") +
                        (stage.onEnterActions?.Length > 0 ? $"  ▶ onEnter: {stage.onEnterActions.Length}" : "") +
                        (stage.onCompleteActions?.Length > 0 ? $"  ✓ onComplete: {stage.onCompleteActions.Length}" : "") +
                        (!string.IsNullOrEmpty(stage.nextStageId) ? $"  → {stage.nextStageId}" : ""),
                        Quest, $"stages[{i}]", stage, QuestNodeKind.Stage);
                    var delStageBtn = MakeEditButton("× Stage", () => DeleteStage(si), "stage-del-" + i);
                    sn.extensionContainer.Add(delStageBtn);
                    _editButtons.Add(delStageBtn);
                    var addObjBtn = MakeEditButton("+ Objective", () => AddObjective(si), "stage-add-" + i);
                    sn.extensionContainer.Add(addObjBtn);
                    _editButtons.Add(addObjBtn);
                    AddElement(sn);
                    var sPort = AddPorts(sn, hasOutput: true, hasInput: true);
                    allNodes.Add(sn);

                    if (i == 0) ConnectPorts(qPort.output, sPort.input);
                    if (i > 0) ConnectPorts(GetOutputPort(stageNodes[i-1]), sPort.input);
                    stageNodes.Add(sn);

                    if (stage.objectives != null)
                    {
                        for (int j = 0; j < stage.objectives.Length; j++)
                        {
                            var obj = stage.objectives[j];
                            if (obj == null) continue;

                            int oi = j; int stIdx = i;
                            var on = MakeEditableNode($"🎯 {obj.objectiveId}", objColor,
                                new (string label, string value, System.Action<string> onSave)[] {
                                    ("ObjId", obj.objectiveId ?? "", v => Quest.stages[stIdx].objectives[oi].objectiveId = v),
                                    ("Item", obj.pickupItem != null ? obj.pickupItem.itemName : (obj.itemTradeItemId ?? ""), v => Quest.stages[stIdx].objectives[oi].itemTradeItemId = v),
                                    ("Npc", obj.targetNpcId ?? "", v => Quest.stages[stIdx].objectives[oi].targetNpcId = v),
                                    ($"[{obj.objectiveType}] ×{obj.requiredQuantity}", $"{obj.requiredQuantity}", v => { if (int.TryParse(v, out var n)) Quest.stages[stIdx].objectives[oi].requiredQuantity = n; })
                                },
                                sourceData: obj, kind: QuestNodeKind.Objective);
                            var delObjBtn = MakeEditButton("× Obj", () => DeleteObjective(stIdx, oi), "obj-del-" + i + "-" + j);
                            on.extensionContainer.Add(delObjBtn);
                            _editButtons.Add(delObjBtn);
                            AddElement(on);
                            var oPort = AddPorts(on, hasOutput: false, hasInput: true);
                            ConnectPorts(sPort.output, oPort.input);
                            allNodes.Add(on);
                        }
                    }
                }
            }

            // Reward
            if (Quest.rewards != null && HasReward(Quest.rewards))
            {
                var r = Quest.rewards;
                var rLines = "";
                if (r.credits > 0) rLines += $"💰 {r.credits} CR  ";
                if (r.items != null) foreach (var it in r.items)
                {
                    var name = it.pickupItem != null ? it.pickupItem.itemName : it.tradeItemId;
                    rLines += $"📦 {name} ×{it.count}  ";
                }
                if (r.cargoItems != null) foreach (var it in r.cargoItems)
                {
                    var name = it.cargoItem != null ? it.cargoItem.displayName : it.tradeItemId;
                    rLines += $"🚢 Cargo: {name} ×{it.count}  ";
                }
                if (r.reputation != null) foreach (var rep in r.reputation) rLines += $"📈 {rep.faction} +{rep.value}  ";

                var rFields = new List<(string label, string value, System.Action<string> onSave)>
                {
                    ("Credits", r.credits.ToString(), v => { if (int.TryParse(v, out var n)) r.credits = n; })
                };
                if (r.reputation != null)
                {
                    for (int ri = 0; ri < r.reputation.Length; ri++)
                    {
                        int rIdx = ri;
                        rFields.Add(($"Rep {ri} Faction", r.reputation[ri].faction.ToString(),
                            v => { if (System.Enum.TryParse<FactionId>(v, out var f)) r.reputation[rIdx].faction = f; }));
                        rFields.Add(($"Rep {ri} Value", r.reputation[ri].value.ToString(),
                            v => { if (int.TryParse(v, out var n)) r.reputation[rIdx].value = n; }));
                    }
                }
                if (r.items != null)
                {
                    for (int ii = 0; ii < r.items.Length; ii++)
                    {
                        int iIdx = ii;
                        rFields.Add(($"Item {ii} Count", r.items[ii].count.ToString(),
                            v => { if (int.TryParse(v, out var n)) r.items[iIdx].count = n; }));
                    }
                }

                var rn = MakeEditableNode("🎁 REWARDS", rewardColor,
                    rFields.ToArray(), rLines,
                    Quest, "rewards", Quest.rewards, QuestNodeKind.Reward);
                AddElement(rn);
                var rPort = AddPorts(rn, hasOutput: false, hasInput: true);
                if (stageNodes.Count > 0)
                    ConnectPorts(GetOutputPort(stageNodes[stageNodes.Count-1]), rPort.input);
                allNodes.Add(rn);
            }

            // T-U03: Apply BFS-based auto-layout
            ApplyAutoLayout(allNodes, qn);
        }

        /// <summary>
        /// T-U02: создать QuestGraphNode с editable полями и SO-привязкой.
        /// fields: list of (labelName, currentValue, onSaveAction).
        /// onSaveAction == null → поле только для просмотра. metaLine: строка снизу (всегда видна).
        /// </summary>
        protected QuestGraphNode MakeEditableNode(string title, Color titleColor,
            (string label, string value, System.Action<string> onSave)[] fields,
            string metaLine = "",
            ScriptableObject owner = null, string sourcePath = "", object sourceData = null,
            QuestNodeKind kind = QuestNodeKind.Stage)
        {
            var n = new QuestGraphNode
            {
                title = title,
                OwnerAsset = owner ?? Quest,
                SourcePath = sourcePath,
                SourceData = sourceData,
                NodeKind = kind,
                PersistKey = $"qnode_{_nodeCounter++}"
            };
            n.titleContainer.style.backgroundColor = new StyleColor(titleColor);
            n.extensionContainer.style.backgroundColor = new StyleColor(titleColor * 0.6f);
            n.viewDataKey = n.PersistKey;

            if (fields != null)
            {
                foreach (var f in fields)
                    AddField(n, f);
            }

            if (!string.IsNullOrEmpty(metaLine))
            {
                var ml = new Label(metaLine);
                ml.style.fontSize = 9;
                ml.style.color = new StyleColor(new Color(0.65f, 0.75f, 0.95f, 1f));
                ml.style.paddingLeft = 8; ml.style.paddingRight = 8;
                ml.style.paddingTop = 2; ml.style.paddingBottom = 4;
                n.extensionContainer.Add(ml);
            }

            n.RefreshExpandedState();
            n.expanded = true;
            return n;
        }

        /// <summary>T-Q30: добавить Label + TextField к Node.</summary>
        private static void AddField(Node n, (string label, string value, System.Action<string> onSave) field)
        {
            if (field.label == null && field.value == null) return;

            string displayLabel = !string.IsNullOrEmpty(field.label) ? $"{field.label}: " : "";

            // TextField (edit mode)
            var tf = new TextField(displayLabel) { value = field.value ?? "", name = "editable-field" };
            tf.style.display = DisplayStyle.None;
            tf.style.fontSize = 10;
            tf.style.paddingLeft = 8; tf.style.paddingRight = 4;
            tf.style.paddingTop = 1; tf.style.paddingBottom = 1;
            tf.userData = field.onSave;
            n.extensionContainer.Add(tf);

            // Label (view mode) — only if there's actual content to show
            if (!string.IsNullOrEmpty(field.value))
            {
                var lbl = new Label($"{displayLabel}{field.value}");
                lbl.name = "editable-label";
                lbl.style.fontSize = 10;
                lbl.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                lbl.style.paddingLeft = 8; lbl.style.paddingRight = 4;
                lbl.style.paddingTop = 2; lbl.style.paddingBottom = 1;
                if (field.onSave != null) lbl.style.unityFontStyleAndWeight = FontStyle.Italic; // editable hint
                lbl.style.whiteSpace = WhiteSpace.Normal;
                n.extensionContainer.Add(lbl);
            }
        }

        /// <summary>T-Q30: toggle all editable fields between Label (view) and TextField (edit).</summary>
        public void RefreshAllEditUI()
        {
            foreach (var n in nodes.Cast<Node>())
            {
                if (n == null) continue;
                foreach (var child in n.extensionContainer.Children())
                {
                    if (child is TextField tf && tf.name == "editable-field")
                        tf.style.display = _editMode ? DisplayStyle.Flex : DisplayStyle.None;
                    else if (child is Label lbl && lbl.name == "editable-label")
                        lbl.style.display = _editMode ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
        }

        /// <summary>T-Q30: read all TextField values and apply callbacks (save to SO).</summary>
        public void SaveQuest()
        {
            if (Quest == null) return;
            bool modified = false;
            foreach (var n in nodes.Cast<Node>())
            {
                if (n == null) continue;
                foreach (var child in n.extensionContainer.Children())
                {
                    if (child is TextField tf && tf.name == "editable-field" && tf.userData is System.Action<string> cb)
                    {
                        cb(tf.value);
                        modified = true;
                    }
                }
            }
            if (modified)
            {
                EditorUtility.SetDirty(Quest);
                AssetDatabase.SaveAssets();
                Debug.Log($"[QuestNodeGraph] Saved {Quest.questId}");
            }
        }

        /// <summary>T-Q30: re-read SO and update TextField values.</summary>
        public void RevertQuest()
        {
            if (Quest == null) return;
            var path = AssetDatabase.GetAssetPath(Quest);
            var fresh = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (fresh == null) return;

            // Reload the graph from fresh SO
            LoadQuest(fresh);
            Debug.Log($"[QuestNodeGraph] Reverted {Quest.questId}");
        }

        // ========== T-U02: Incremental Add/Delete CRUD (no full rebuild) ==========

        /// <summary>Create a small edit-only button (hidden in view mode).</summary>
        private VisualElement MakeEditButton(string text, System.Action onClick, string name)
        {
            var btn = new Button(onClick) { text = text, name = name };
            btn.style.fontSize = 9;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;
            btn.style.paddingTop = 1;
            btn.style.paddingBottom = 1;
            btn.style.marginLeft = 4;
            btn.style.marginTop = 2;
            btn.style.marginBottom = 2;
            btn.style.display = DisplayStyle.None;
            return btn;
        }

        // ── Stage CRUD ──

        private void AddStage()
        {
            if (Quest == null) return;
            var list = Quest.stages?.ToList() ?? new List<QuestStage>();
            var newStage = new QuestStage { stageId = "new_stage", description = "" };
            list.Add(newStage);
            Quest.stages = list.ToArray();
            EditorUtility.SetDirty(Quest);

            // Add single stage node without full rebuild
            var stageNodes = this.nodes.ToList().Where(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Stage).Cast<QuestGraphNode>().ToList();
            int idx = list.Count - 1;
            int stageCount = list.Count;

            float y = 0f;
            if (stageNodes.Count > 0)
            {
                var last = stageNodes[stageNodes.Count - 1];
                y = last.GetPosition().y + last.GetPosition().height + 30f;
            }

            var stageColor = new Color(0.20f, 0.55f, 0.30f);
            var sn = MakeEditableNode($"STAGE {idx+1}/{stageCount}", stageColor,
                new (string label, string value, System.Action<string> onSave)[] {
                    ("ID", newStage.stageId, v => { int si = idx; Quest.stages[si].stageId = v; }),
                    ("Desc", newStage.description ?? "", v => { int si = idx; Quest.stages[si].description = v; })
                },
                "", Quest, $"stages[{idx}]", newStage, QuestNodeKind.Stage);

            sn.SetPosition(new Rect(360f, y, 240f, 160f));
            var sPort = AddPorts(sn, hasOutput: true, hasInput: true);
            AddElement(sn);

            // Add delete / add-objective buttons
            var delStageBtn = MakeEditButton("× Stage", () => DeleteStage(idx), "stage-del-" + idx);
            sn.extensionContainer.Add(delStageBtn);
            _editButtons.Add(delStageBtn);
            var addObjBtn = MakeEditButton("+ Objective", () => AddObjective(idx), "stage-add-" + idx);
            sn.extensionContainer.Add(addObjBtn);
            _editButtons.Add(addObjBtn);

            // Connect to previous stage
            if (stageNodes.Count > 0)
            {
                var prevOut = GetOutputPort(stageNodes[stageNodes.Count - 1]);
                if (prevOut != null && sPort.input != null)
                    ConnectPorts(prevOut, sPort.input);
            }

            // Connect reward to this stage if it was connected to previous last
            var rewardNode = this.nodes.ToList().FirstOrDefault(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Reward);
            if (rewardNode != null && stageNodes.Count > 0)
            {
                // Remove old reward edge and reconnect
                var rewardIn = GetInputPort(rewardNode);
                if (rewardIn != null)
                {
                    var oldEdges = this.edges.ToList().Where(e => e.input == rewardIn).ToList();
                    foreach (var e in oldEdges) RemoveElement(e);
                    ConnectPorts(sPort.output, rewardIn);
                }
            }

            MarkDirtyRepaint();
        }

        private void DeleteStage(int index)
        {
            if (Quest == null || Quest.stages == null || index < 0 || index >= Quest.stages.Length) return;

            // Remove from SO
            var list = Quest.stages.ToList();
            list.RemoveAt(index);
            Quest.stages = list.ToArray();
            EditorUtility.SetDirty(Quest);

            // Find and remove the corresponding node
            var stageNodes = this.nodes.ToList().Where(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Stage).Cast<QuestGraphNode>().ToList();
            if (index < stageNodes.Count)
            {
                var targetNode = stageNodes[index];
                // Remove all edges connected to this node
                var connectedEdges = this.edges.ToList().Where(e => e.input != null && e.input.node == targetNode || e.output != null && e.output.node == targetNode).ToList();
                foreach (var e in connectedEdges) RemoveElement(e);
                RemoveElement(targetNode);
            }

            MarkDirtyRepaint();
        }

        // ── Objective CRUD ──

        private void AddObjective(int stageIndex)
        {
            if (Quest == null || Quest.stages == null || stageIndex < 0 || stageIndex >= Quest.stages.Length) return;
            var stage = Quest.stages[stageIndex];
            if (stage == null) return;
            var list = stage.objectives?.ToList() ?? new List<QuestObjective>();
            var newObj = new QuestObjective { objectiveId = "new_objective", objectiveType = QuestObjectiveType.HaveItem, requiredQuantity = 1 };
            list.Add(newObj);
            stage.objectives = list.ToArray();
            EditorUtility.SetDirty(Quest);

            // Find the stage node
            var stageNodes = this.nodes.ToList().Where(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Stage).Cast<QuestGraphNode>().ToList();
            if (stageIndex >= stageNodes.Count) { LoadQuest(Quest); return; }
            var stageNode = stageNodes[stageIndex];
            var stageRect = stageNode.GetPosition();

            float oy = stageRect.y;
            for (int j = 0; j < list.Count - 1; j++)
                oy += 100f + 15f;

            var objColor = new Color(0.55f, 0.40f, 0.10f);
            int oi = list.Count - 1; int stIdx = stageIndex;
            var on = MakeEditableNode($"🎯 {newObj.objectiveId}", objColor,
                new (string label, string value, System.Action<string> onSave)[] {
                    ("ObjId", newObj.objectiveId ?? "", v => Quest.stages[stIdx].objectives[oi].objectiveId = v),
                    ("Qty", $"{newObj.requiredQuantity}", v => { if (int.TryParse(v, out var n)) Quest.stages[stIdx].objectives[oi].requiredQuantity = n; })
                },
                sourceData: newObj, kind: QuestNodeKind.Objective);

            on.SetPosition(new Rect(720f, oy, 220f, 100f));
            var oPort = AddPorts(on, hasOutput: false, hasInput: true);
            AddElement(on);

            var sPort = GetOutputPort(stageNode);
            if (sPort != null) ConnectPorts(sPort, oPort.input);

            var delObjBtn = MakeEditButton("× Obj", () => DeleteObjective(stIdx, oi), "obj-del-" + stageIndex + "-" + oi);
            on.extensionContainer.Add(delObjBtn);
            _editButtons.Add(delObjBtn);

            MarkDirtyRepaint();
        }

        private void DeleteObjective(int stageIndex, int objIndex)
        {
            if (Quest == null || Quest.stages == null || stageIndex < 0 || stageIndex >= Quest.stages.Length) return;
            var stage = Quest.stages[stageIndex];
            if (stage == null || stage.objectives == null || objIndex < 0 || objIndex >= stage.objectives.Length) return;

            var list = stage.objectives.ToList();
            list.RemoveAt(objIndex);
            stage.objectives = list.ToArray();
            EditorUtility.SetDirty(Quest);

            // Find the objective node (by SourceData match or approximate)
            var stageNodes = this.nodes.ToList().Where(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Stage).Cast<QuestGraphNode>().ToList();
            if (stageIndex < stageNodes.Count)
            {
                var objNodes = this.nodes.ToList().Where(n => n is QuestGraphNode qn && qn.NodeKind == QuestNodeKind.Objective).ToList();
                if (objIndex < objNodes.Count)
                {
                    var target = objNodes[objIndex];
                    var connectedEdges = this.edges.ToList().Where(e => e.input != null && e.input.node == target || e.output != null && e.output.node == target).ToList();
                    foreach (var e in connectedEdges) RemoveElement(e);
                    RemoveElement(target);
                }
            }

            MarkDirtyRepaint();
        }

        protected struct NodePorts { public Port input; public Port output; }

        // T-U04: meaningful port names + color coding
        protected NodePorts AddPorts(Node n, bool hasOutput, bool hasInput,
            string inputName = "← Prev", string outputName = "→ Next")
        {
            var result = new NodePorts();
            if (hasInput)
            {
                var port = Port.Create<Edge>(Orientation.Vertical, Direction.Input, Port.Capacity.Multi, typeof(bool));
                port.portName = inputName;
                port.portColor = new Color(0.55f, 0.55f, 0.55f); // gray default
                n.inputContainer.Add(port);
                result.input = port;
            }
            if (hasOutput)
            {
                var port = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(bool));
                port.portName = outputName;
                port.portColor = new Color(0.35f, 0.70f, 0.35f); // green = success
                n.outputContainer.Add(port);
                result.output = port;
            }
            return result;
        }

        protected Port GetOutputPort(Node n)
        {
            foreach (var child in n.outputContainer.Children())
                if (child is Port p && p.direction == Direction.Output) return p;
            return null;
        }

        protected Port GetInputPort(Node n)
        {
            foreach (var child in n.inputContainer.Children())
                if (child is Port p && p.direction == Direction.Input) return p;
            return null;
        }

        protected void ConnectPorts(Port output, Port input, bool isAuto = true)
        {
            if (output == null || input == null) return;
            var edge = output.ConnectTo(input);
            if (isAuto) edge.viewDataKey = "auto";
            AddElement(edge);
        }

        // T-U01: Model-driven — разрешить все мутации, вызывать хуки для SO-обновлений
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // --- Edge creation ---
            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge != null) OnEdgeCreated(edge);
                }
            }

            // --- Edge / Node deletion ---
            if (change.elementsToRemove != null)
            {
                foreach (var elem in change.elementsToRemove)
                {
                    if (elem is Edge edge)
                        OnEdgeDeleted(edge);
                    else if (elem is Node node)
                        OnNodeDeleted(node);
                }
            }

            // --- Node movement ---
            if (change.movedElements != null)
            {
                foreach (var elem in change.movedElements)
                {
                    if (elem is Node node)
                        OnNodeMoved(node, node.GetPosition());
                }
            }

            return change;
        }

        // ── T-U01 mutation hooks (virtual — overridden in T-U02/Unified) ──

        /// <summary>Called when an edge is created by the user in the graph.</summary>
        protected virtual void OnEdgeCreated(Edge edge) { }

        /// <summary>Called when an edge is deleted (user or programmatic).</summary>
        protected virtual void OnEdgeDeleted(Edge edge) { }

        /// <summary>Called when a node is deleted from the graph.</summary>
        protected virtual void OnNodeDeleted(Node node) { }

        /// <summary>Called when a node is moved. newPos is the new Rect position.</summary>
        protected virtual void OnNodeMoved(Node node, Rect newPos)
        {
            string key = node.viewDataKey;
            if (!string.IsNullOrEmpty(key))
                _nodePositions[key] = newPos.position;
        }

        // T-Q34: allow drag-connecting between compatible ports
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();
            foreach (var p in ports)
            {
                if (p == startPort) continue;
                if (p.node == startPort.node) continue; // no self-connections
                if (p.direction == startPort.direction) continue; // no same-direction
                compatible.Add(p);
            }
            return compatible;
        }

        protected static bool HasReward(QuestReward r) => r != null && (r.credits > 0 || (r.items != null && r.items.Length > 0) || (r.reputation != null && r.reputation.Length > 0));
    }

    // ===== Window =====

    public class QuestNodeGraphWindow : EditorWindow
    {
        private QuestNodeGraphView _graph;
        private UnityEditor.UIElements.ObjectField _questField;
        private Button _editBtn;
        private Button _saveBtn;
        private Button _revertBtn;

        [MenuItem("Tools/Project C/Quests/Quest Node Graph", priority = 102)]
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
            toolbar.Add(_questField);

            var fitBtn = new Button(() => _graph?.FrameAll()) { text = "⊡ Fit" };
            fitBtn.style.marginLeft = 4;
            toolbar.Add(fitBtn);

            // T-Q30: Edit/Save/Revert buttons
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

            _saveBtn = new Button(() => _graph?.SaveQuest()) { text = "💾 Save" };
            _saveBtn.style.marginLeft = 4;
            _saveBtn.style.display = DisplayStyle.None;
            toolbar.Add(_saveBtn);

            _revertBtn = new Button(() => _graph?.RevertQuest()) { text = "↩️ Revert" };
            _revertBtn.style.marginLeft = 4;
            _revertBtn.style.display = DisplayStyle.None;
            toolbar.Add(_revertBtn);

            // T-Q33: Show all quests button
            var showAllBtn = new Button(() => _graph?.ShowAllQuests()) { text = "📋 Show All" };
            showAllBtn.style.marginLeft = 4;
            toolbar.Add(showAllBtn);

            // T-Q33: When quest field changes, switch to single-quest mode
            _questField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is QuestDefinition qd && qd != null)
                    _graph?.LoadSingleQuest(qd);
                else if (evt.newValue == null)
                    _graph?.LoadQuest(null);
            });
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
