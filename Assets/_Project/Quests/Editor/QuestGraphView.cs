// T-Q09b / M17: QuestGraphView — read-only graph visualization для QuestDefinition.
// НЕ редактирование (M18 будет editable) — только визуальный эксплорер
// для content creators чтобы видеть quest → stages → objectives → rewards.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests;

namespace ProjectC.Quests.Editor
{
    public class QuestGraphView : GraphView
    {
        public QuestDefinition Quest { get; private set; }

        public QuestGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
            var styleSheet = (StyleSheet)EditorGUIUtility.Load("StyleSheets/Default/GraphView/Default.uss");
            if (styleSheet != null) this.styleSheets.Add(styleSheet);
            this.AddManipulator(new ContentZoomer());
            graphViewChanged = OnGraphViewChanged;
        }

        /// <summary>Programmatic delete (load new quest) — bypasses OnGraphViewChanged read-only check.</summary>
        private bool _suppressReadOnly;

        public void LoadQuest(QuestDefinition quest)
        {
            Quest = quest;
            ClearGraph();
            if (quest == null) return;
            BuildGraph();
        }

        /// <summary>Remove ALL elements (edges + nodes) safely.</summary>
        private void ClearGraph()
        {
            // T-Q09b fix: bypass OnGraphViewChanged read-only check during programmatic delete.
            _suppressReadOnly = true;
            try
            {
                var edgeList = new List<GraphElement>(edges.ToList());
                var nodeList = new List<GraphElement>(nodes.ToList());
                if (edgeList.Count > 0) DeleteElements(edgeList);
                if (nodeList.Count > 0) DeleteElements(nodeList);
            }
            finally
            {
                _suppressReadOnly = false;
            }
        }

        private void BuildGraph()
        {
            if (Quest == null) return;

            // QuestNode — top-left
            var questNode = new QuestNode(Quest);
            AddElement(questNode);
            questNode.SetPosition(new Rect(0, 0, 240, 140));

            if (Quest.stages == null || Quest.stages.Length == 0)
            {
                // Add reward node even if no stages
                AddRewardNode(questNode);
                return;
            }

            // StageNode per stage — column 1
            float yOffset = 0;
            const float STAGE_HEIGHT = 110f;
            const float STAGE_GAP = 30f;
            for (int i = 0; i < Quest.stages.Length; i++)
            {
                var stage = Quest.stages[i];
                if (stage == null) continue;
                var sn = new StageNode(stage, i);
                AddElement(sn);
                sn.SetPosition(new Rect(360, yOffset, 240, STAGE_HEIGHT));

                // Connect quest → stage (create edge manually)
                var questOut = questNode.OutputPort;
                var stageIn = sn.InputPort;
                AddElement(questOut.ConnectTo(stageIn));

                // ObjectiveNode per objective — column 2
                if (stage.objectives != null)
                {
                    float oy = yOffset;
                    foreach (var obj in stage.objectives)
                    {
                        if (obj == null) continue;
                        var on = new ObjectiveNode(obj);
                        AddElement(on);
                        on.SetPosition(new Rect(720, oy, 280, 70));
                        var stageOut = sn.OutputPort;
                        var objIn = on.InputPort;
                        AddElement(stageOut.ConnectTo(objIn));
                        oy += 90;
                    }
                }
                yOffset += STAGE_HEIGHT + STAGE_GAP + (stage.objectives?.Length ?? 0) * 90;
            }

            AddRewardNode(questNode);
        }

        private void AddRewardNode(QuestNode questNode)
        {
            if (Quest.rewards == null) return;
            var rewardNode = new RewardNode(Quest);
            AddElement(rewardNode);
            rewardNode.SetPosition(new Rect(360, 600, 240, 140));
            var questRewardIn = questNode.RewardInputPort;
            if (questRewardIn != null)
            {
                // Use a synthetic output port on quest for reward connection
                var rewardOut = rewardNode.InputPort; // reward has only input
                // Skip — visual: just place reward near, no edge needed.
            }
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // T-Q09b fix: allow programmatic delete (load new quest), block user-initiated.
            if (_suppressReadOnly) return change;
            // Read-only: block any element removal by user
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                change.elementsToRemove.Clear();
            }
            return change;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) => ports.ToList();
    }

    // ============================================================
    // Node types
    // ============================================================

    public class QuestNode : Node
    {
        public QuestDefinition Quest;
        public Port OutputPort;
        public Port RewardInputPort;
        public QuestNode(QuestDefinition quest)
        {
            Quest = quest;
            title = $"📜 {quest.questId}";
            viewDataKey = "quest_" + quest.questId;
            var ql = new Label(quest.displayName);
            ql.style.fontSize = 13;
            ql.style.unityFontStyleAndWeight = FontStyle.Bold;
            ql.style.paddingLeft = 8;
            ql.style.paddingTop = 4;
            ql.style.paddingBottom = 4;
            extensionContainer.Add(ql);
            var desc = new Label(quest.description);
            desc.style.fontSize = 10;
            desc.style.paddingLeft = 8;
            desc.style.paddingBottom = 4;
            desc.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f, 1f));
            desc.style.whiteSpace = WhiteSpace.Normal;
            extensionContainer.Add(desc);
            RefreshExpandedState();
            // Output port (quest → stage)
            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "stages";
            outputContainer.Add(OutputPort);
            // Input port (reward connection)
            RewardInputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
            RewardInputPort.portName = "rewards";
            inputContainer.Add(RewardInputPort);
        }
    }

    public class StageNode : Node
    {
        public QuestStage Stage;
        public Port InputPort;
        public Port OutputPort;
        public StageNode(QuestStage stage, int index)
        {
            Stage = stage;
            title = $"Stage {index}: {stage.stageId}";
            viewDataKey = "stage_" + stage.stageId;
            if (!string.IsNullOrEmpty(stage.nextStageId))
            {
                var nxt = new Label($"→ next: {stage.nextStageId}");
                nxt.style.fontSize = 10;
                nxt.style.paddingLeft = 8;
                nxt.style.paddingTop = 2;
                nxt.style.paddingBottom = 2;
                nxt.style.color = new StyleColor(new Color(0.4f, 0.7f, 1f, 1f));
                extensionContainer.Add(nxt);
            }
            if (stage.objectives != null && stage.objectives.Length > 0)
            {
                var ol = new Label($"objectives: {stage.objectives.Length}");
                ol.style.fontSize = 10;
                ol.style.paddingLeft = 8;
                ol.style.paddingBottom = 2;
                extensionContainer.Add(ol);
            }
            if (stage.onEnterActions != null && stage.onEnterActions.Length > 0)
            {
                var e = new Label($"onEnter: {stage.onEnterActions.Length}");
                e.style.fontSize = 10;
                e.style.paddingLeft = 8;
                e.style.paddingBottom = 2;
                e.style.color = new StyleColor(new Color(0.4f, 1f, 0.4f, 1f));
                extensionContainer.Add(e);
            }
            if (stage.onCompleteActions != null && stage.onCompleteActions.Length > 0)
            {
                var c = new Label($"onComplete: {stage.onCompleteActions.Length}");
                c.style.fontSize = 10;
                c.style.paddingLeft = 8;
                c.style.paddingBottom = 2;
                c.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f, 1f));
                extensionContainer.Add(c);
            }
            RefreshExpandedState();
            // Input (from quest or prev stage)
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "in";
            inputContainer.Add(InputPort);
            // Output (to objective)
            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "objectives";
            outputContainer.Add(OutputPort);
        }
    }

    public class ObjectiveNode : Node
    {
        public QuestObjective Objective;
        public Port InputPort;
        public ObjectiveNode(QuestObjective obj)
        {
            Objective = obj;
            title = $"[{obj.objectiveType}] {obj.objectiveId}";
            viewDataKey = "obj_" + obj.objectiveId;
            var qty = new Label($"qty: {obj.requiredQuantity}");
            qty.style.fontSize = 10;
            qty.style.paddingLeft = 8;
            qty.style.paddingTop = 2;
            extensionContainer.Add(qty);
            if (!string.IsNullOrEmpty(obj.itemTradeItemId))
            {
                var it = new Label($"item: {obj.itemTradeItemId}");
                it.style.fontSize = 10;
                it.style.paddingLeft = 8;
                it.style.paddingBottom = 2;
                it.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.4f, 1f));
                extensionContainer.Add(it);
            }
            if (!string.IsNullOrEmpty(obj.targetNpcId))
            {
                var np = new Label($"npc: {obj.targetNpcId}");
                np.style.fontSize = 10;
                np.style.paddingLeft = 8;
                np.style.paddingBottom = 2;
                np.style.color = new StyleColor(new Color(0.4f, 0.9f, 0.9f, 1f));
                extensionContainer.Add(np);
            }
            RefreshExpandedState();
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "in";
            inputContainer.Add(InputPort);
        }
    }

    public class RewardNode : Node
    {
        public QuestDefinition Quest;
        public Port InputPort;
        public RewardNode(QuestDefinition quest)
        {
            Quest = quest;
            title = "🎁 Rewards";
            viewDataKey = "rewards_" + quest.questId;
            if (quest.rewards != null)
            {
                if (quest.rewards.credits > 0)
                {
                    var c = new Label($"CR: {quest.rewards.credits}");
                    c.style.fontSize = 11;
                    c.style.paddingLeft = 8;
                    c.style.paddingTop = 4;
                    extensionContainer.Add(c);
                }
                if (quest.rewards.items != null && quest.rewards.items.Length > 0)
                {
                    foreach (var r in quest.rewards.items)
                    {
                        extensionContainer.Add(new Label($"Item x{r.count}") { style = { fontSize = 10, paddingLeft = 8 } });
                    }
                }
                if (quest.rewards.reputation != null && quest.rewards.reputation.Length > 0)
                {
                    foreach (var rep in quest.rewards.reputation)
                    {
                        extensionContainer.Add(new Label($"Rep: {rep.faction} +{rep.value}") { style = { fontSize = 10, paddingLeft = 8, paddingBottom = 2 } });
                    }
                }
            }
            RefreshExpandedState();
            // Input port (from quest)
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
            InputPort.portName = "in";
            inputContainer.Add(InputPort);
        }
    }
}
#endif
