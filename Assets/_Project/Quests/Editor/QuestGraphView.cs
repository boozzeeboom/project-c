// T-Q09b / M17 v8: QuestGraphView — **гибридный** подход.
// - Nodes: настоящие VisualElement с Label (текст виден сразу)
// - Connections: рисуются через Painter2D в generateVisualContent (видны сразу, без layout)
// - Drag/zoom: contentContainer с transform
// - Nodes можно перетаскивать (for convenience)

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests;

namespace ProjectC.Quests.Editor
{
    public class QuestGraphView : VisualElement
    {
        public QuestDefinition Quest { get; private set; }

        // Content container with transform (zoom/pan)
        private readonly VisualElement _content;
        private Vector2 _contentOffset = Vector2.zero;
        private float _contentZoom = 1f;

        // Drag/pan state
        private bool _panning;
        private Vector2 _panStart;
        private bool _draggingNode;
        private VisualElement _dragTarget;
        private Vector2 _dragOffset;

        // Connection data (for painter)
        private readonly List<(VisualElement from, VisualElement to)> _connections = new List<(VisualElement, VisualElement)>();

        private const float GRID_SIZE = 50f;

        public QuestGraphView()
        {
            name = "QuestGraphView";
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = new StyleColor(new Color(0.19f, 0.19f, 0.19f, 1f));

            // Content layer (zoom/pan applied via transform)
            _content = new VisualElement();
            _content.name = "content";
            _content.style.position = Position.Absolute;
            _content.style.left = 0; _content.style.top = 0;
            _content.style.right = 0; _content.style.bottom = 0;
            _content.pickingMode = PickingMode.Ignore;
            Add(_content);

            // Draw grid + connections via painter
            generateVisualContent += DrawPainterContent;

            // Pan via right-click (or middle-click)
            this.RegisterCallback<MouseDownEvent>(e =>
            {
                if (e.button == 0 || e.button == 2)
                {
                    if (!_panning && !_draggingNode)
                    {
                        _panning = true;
                        _panStart = e.mousePosition;
                        e.StopPropagation();
                    }
                }
            });
            this.RegisterCallback<MouseMoveEvent>(e =>
            {
                if (_panning)
                {
                    _contentOffset += (e.mousePosition - _panStart) / _contentZoom;
                    _panStart = e.mousePosition;
                    UpdateTransform();
                    MarkDirtyRepaint();
                }
                if (_draggingNode && _dragTarget != null)
                {
                    var pos = _dragTarget.layout.position;
                    float newX = (e.mousePosition.x - _contentOffset.x) / _contentZoom - _dragOffset.x;
                    float newY = (e.mousePosition.y - _contentOffset.y) / _contentZoom - _dragOffset.y;
                    _dragTarget.style.left = newX;
                    _dragTarget.style.top = newY;
                    MarkDirtyRepaint();
                }
            });
            this.RegisterCallback<MouseUpEvent>(e =>
            {
                _panning = false;
                _draggingNode = false;
                _dragTarget = null;
            });
            this.RegisterCallback<WheelEvent>(e =>
            {
                float prev = _contentZoom;
                _contentZoom = Mathf.Clamp(_contentZoom - e.delta.y * 0.001f, 0.2f, 3f);
                // Zoom towards mouse
                Vector2 mouse = e.mousePosition;
                _contentOffset = mouse / prev - (mouse / _contentZoom - _contentOffset);
                UpdateTransform();
                MarkDirtyRepaint();
                e.StopPropagation();
            });
        }

        private void UpdateTransform()
        {
            // Unity 6: transform is read-only. Use style.translate + style.scale.
            _content.style.translate = new Translate(_contentOffset.x, _contentOffset.y, 0);
            _content.style.scale = new Scale(Vector3.one * _contentZoom);
        }

        public void LoadQuest(QuestDefinition quest)
        {
            Quest = quest;
            _content.Clear();
            _connections.Clear();
            if (quest == null) return;
            BuildGraph();
            // Center content
            _contentOffset = new Vector2(50, 50);
            UpdateTransform();
            MarkDirtyRepaint();
        }

        /// <summary>Build VisualElement nodes and connection list.</summary>
        private void BuildGraph()
        {
            if (Quest == null) return;

            const float QUEST_W = 240f, QUEST_H = 160f;
            const float STAGE_W = 240f, STAGE_H = 180f;
            const float OBJ_W = 200f, OBJ_H = 100f;
            const float REWARD_W = 220f, REWARD_H = 140f;
            const float STAGE_GAP = 300f;
            const float X_QUEST = 0f;
            const float X_STAGE_START = 360f;
            const float OBJ_OFFSET_Y = 220f;

            var questTitleColor = "#3355AA";
            var stageTitleColor = "#338855";
            var objTitleColor = "#886633";
            var rewardTitleColor = "#996633";

            // Helpers
            VisualElement MakeNode(string title, string bodyLines, string titleColor, float x, float y, float w, float h)
            {
                var node = new VisualElement();
                node.style.position = Position.Absolute;
                node.style.left = x; node.style.top = y;
                node.style.width = w; node.style.height = h;
                node.style.backgroundColor = new StyleColor(new Color(0.13f, 0.18f, 0.28f, 1f));
                node.style.borderTopLeftRadius = 3;
                node.style.borderTopRightRadius = 3;
                node.style.borderBottomLeftRadius = 3;
                node.style.borderBottomRightRadius = 3;
                node.style.paddingBottom = 0;
                node.pickingMode = PickingMode.Position;
                // Allow dragging this node
                node.RegisterCallback<MouseDownEvent>(e =>
                {
                    if (e.button == 0)
                    {
                        _draggingNode = true;
                        _dragTarget = node;
                        _dragOffset = (e.mousePosition - new Vector2(x * _contentZoom + _contentOffset.x, y * _contentZoom + _contentOffset.y)) / _contentZoom;
                        e.StopPropagation();
                    }
                });
                // Title bar
                var titleEl = new Label($"{title}");
                titleEl.style.backgroundColor = new StyleColor(ParseColor(titleColor));
                titleEl.style.color = new StyleColor(Color.white);
                titleEl.style.fontSize = 12;
                titleEl.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleEl.style.paddingTop = 4;
                titleEl.style.paddingLeft = 8;
                titleEl.style.paddingBottom = 2;
                titleEl.style.whiteSpace = WhiteSpace.Normal;
                titleEl.style.overflow = Overflow.Hidden;
                node.Add(titleEl);
                // Body
                if (!string.IsNullOrEmpty(bodyLines))
                {
                    var bodyEl = new Label(bodyLines);
                    bodyEl.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f, 1f));
                    bodyEl.style.fontSize = 10;
                    bodyEl.style.paddingLeft = 8;
                    bodyEl.style.paddingTop = 4;
                    bodyEl.style.paddingRight = 4;
                    bodyEl.style.paddingBottom = 4;
                    bodyEl.style.whiteSpace = WhiteSpace.Normal;
                    bodyEl.style.overflow = Overflow.Hidden;
                    node.Add(bodyEl);
                }
                _content.Add(node);
                return node;
            }

            // Quest node
            string qBody = $"{Quest.displayName}\nid: {Quest.questId}  •  stages: {Quest.stages?.Length ?? 0}";
            if (!string.IsNullOrEmpty(Quest.description))
                qBody = $"{Quest.displayName}\n{Quest.description}\nid: {Quest.questId}";
            var questNode = MakeNode("📜 QUEST", qBody, questTitleColor, X_QUEST, 0, QUEST_W, QUEST_H);

            int stageCount = Quest.stages?.Length ?? 0;
            var stageNodes = new List<VisualElement>();

            if (Quest.stages != null)
            {
                for (int i = 0; i < Quest.stages.Length; i++)
                {
                    var stage = Quest.stages[i];
                    if (stage == null) continue;
                    float x = X_STAGE_START + i * STAGE_GAP;

                    var lines = new List<string>
                    {
                        $"{stage.stageId}"
                    };
                    if (!string.IsNullOrEmpty(stage.description)) lines.Add(stage.description);
                    if (stage.objectives != null) lines.Add($"🎯 {stage.objectives.Length} objective(s)");
                    if (stage.onEnterActions != null && stage.onEnterActions.Length > 0)
                        lines.Add($"▶ onEnter: {stage.onEnterActions.Length} act");
                    if (stage.onCompleteActions != null && stage.onCompleteActions.Length > 0)
                        lines.Add($"✓ onComplete: {stage.onCompleteActions.Length} act");
                    if (!string.IsNullOrEmpty(stage.nextStageId))
                        lines.Add($"→ next: {stage.nextStageId}");

                    var sn = MakeNode($"🟢 STAGE {i+1}/{stageCount}", string.Join("\n", lines), stageTitleColor, x, 0, STAGE_W, STAGE_H);
                    stageNodes.Add(sn);

                    // Quest → Stage 0
                    if (i == 0) _connections.Add((questNode, sn));
                    // Stage i → Stage i+1
                    if (i > 0) _connections.Add((stageNodes[i-1], sn));

                    // Objectives
                    if (stage.objectives != null)
                    {
                        for (int j = 0; j < stage.objectives.Length; j++)
                        {
                            var obj = stage.objectives[j];
                            if (obj == null) continue;
                            float oy = OBJ_OFFSET_Y + j * (OBJ_H + 15f);
                            var oLines = $"[{obj.objectiveType}] ×{obj.requiredQuantity}";
                            if (!string.IsNullOrEmpty(obj.itemTradeItemId))
                                oLines += $"\n📦 item: {obj.itemTradeItemId}";
                            if (!string.IsNullOrEmpty(obj.targetNpcId))
                                oLines += $"\n👤 npc: {obj.targetNpcId}";
                            var on = MakeNode($"🎯 {obj.objectiveId}", oLines, objTitleColor, x + 20, oy, OBJ_W, OBJ_H);
                            _connections.Add((sn, on));
                        }
                    }
                }
            }

            // Reward node
            if (Quest.rewards != null && HasReward(Quest.rewards))
            {
                var r = Quest.rewards;
                var rLines = new List<string>();
                if (r.credits > 0) rLines.Add($"💰 {r.credits} CR");
                if (r.items != null) foreach (var it in r.items) rLines.Add($"📦 Item ×{it.count}");
                if (r.reputation != null) foreach (var rep in r.reputation) rLines.Add($"📈 {rep.faction} +{rep.value}");
                float x = X_STAGE_START + stageCount * STAGE_GAP;
                var rn = MakeNode("🎁 REWARDS", string.Join("\n", rLines), rewardTitleColor, x, 0, REWARD_W, REWARD_H);
                if (stageNodes.Count > 0)
                    _connections.Add((stageNodes[stageNodes.Count - 1], rn));
            }
        }

        /// <summary>Paint grid + connection lines.</summary>
        private void DrawPainterContent(MeshGenerationContext mgc)
        {
            var painter = mgc.painter2D;
            DrawGrid(painter);
            DrawConnections(painter);
        }

        private void DrawConnections(Painter2D painter)
        {
            var connColor = new Color(0.55f, 0.85f, 1f, 0.85f);
            painter.strokeColor = connColor;
            painter.fillColor = connColor;
            painter.lineWidth = 2f;

            foreach (var (from, to) in _connections)
            {
                if (from == null || to == null) continue;
                var r1 = from.layout;
                var r2 = to.layout;

                // Check if this is a vertical connection (target below source)
                bool vertical = r2.y > r1.y + r1.height / 2f;
                Vector2 start, end;
                if (vertical)
                {
                    start = new Vector2(r1.x + r1.width / 2f, r1.y + r1.height);
                    end = new Vector2(r2.x + r2.width / 2f, r2.y);
                    var mid = new Vector2(end.x, start.y);
                    painter.BeginPath();
                    painter.MoveTo(start);
                    painter.LineTo(mid);
                    painter.LineTo(end);
                    painter.Stroke();
                    // Arrow down
                    float al = 8f;
                    painter.BeginPath();
                    painter.MoveTo(end);
                    painter.LineTo(end + new Vector2(-al, -al));
                    painter.LineTo(end + new Vector2(al, -al));
                    painter.ClosePath();
                    painter.Fill();
                }
                else
                {
                    start = new Vector2(r1.x + r1.width, r1.y + r1.height / 2f);
                    end = new Vector2(r2.x, r2.y + r2.height / 2f);
                    painter.BeginPath();
                    painter.MoveTo(start);
                    painter.LineTo(end);
                    painter.Stroke();
                    // Arrow right
                    float al = 8f;
                    painter.BeginPath();
                    painter.MoveTo(end);
                    painter.LineTo(end + new Vector2(-al, -al));
                    painter.LineTo(end + new Vector2(-al, al));
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }

        private void DrawGrid(Painter2D painter)
        {
            painter.strokeColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            painter.lineWidth = 1f;
            float invZ = 1f / _contentZoom;
            float sw = resolvedStyle.width * invZ;
            float sh = resolvedStyle.height * invZ;
            float ox = -_contentOffset.x / _contentZoom;
            float oy = -_contentOffset.y / _contentZoom;
            float sx = Mathf.Floor(ox / GRID_SIZE) * GRID_SIZE;
            float sy = Mathf.Floor(oy / GRID_SIZE) * GRID_SIZE;

            for (float x = sx; x < ox + sw + GRID_SIZE; x += GRID_SIZE)
            {
                var p1 = ToScreen(new Vector2(x, 0));
                var p2 = ToScreen(new Vector2(x, 10000));
                painter.BeginPath();
                painter.MoveTo(p1);
                painter.LineTo(p2);
                painter.Stroke();
            }
            for (float y = sy; y < oy + sh + GRID_SIZE; y += GRID_SIZE)
            {
                var p1 = ToScreen(new Vector2(0, y));
                var p2 = ToScreen(new Vector2(10000, y));
                painter.BeginPath();
                painter.MoveTo(p1);
                painter.LineTo(p2);
                painter.Stroke();
            }
        }

        private Vector2 ToScreen(Vector2 world)
        {
            return new Vector2(world.x * _contentZoom + _contentOffset.x, world.y * _contentZoom + _contentOffset.y);
        }

        private static Color ParseColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static bool HasReward(QuestReward r) => r != null && (r.credits > 0 || (r.items != null && r.items.Length > 0) || (r.reputation != null && r.reputation.Length > 0));
    }
}
#endif
