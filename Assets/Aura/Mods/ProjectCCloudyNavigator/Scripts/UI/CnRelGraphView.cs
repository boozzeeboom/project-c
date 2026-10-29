// Cloudy Navigator — полноэкранный граф связей (мод v4-2: src/rel-graph.js).
//
// Узлы — CnDataFeed.graphNodes: фракции, люди и видимые квесты (только то, что игра уже показала игроку).
// Рёбра — CnDataFeed.graphEdges: шесть типов мода без сдвига номеров, каждое ребро — реальное поле ассета
// (NpcDefinition.factionRef / attitudeLinks, FactionDefinition.combatRelations, выдача и сдача квеста,
// QuestDefinition.factionRef и rewards.reputation, QuestPrerequisite). Ни узел, ни ребро здесь не выдумываются.
//
// Раскладка детерминированная: три кольца по видам (фракции — внутреннее, люди — среднее, квесты — внешнее),
// угол равномерный, порядок — порядок данных. Своей физики нет: один и тот же снапшот всегда даёт одну картинку.
// Кольца масштабируются кнопками «−» / «+» и сдвигаются перетаскиванием — без WheelEvent и без Transform,
// чтобы не зависеть от свойств style.transform в рантайме.
//
// Фокус: клик по узлу подсвечивает его рёбра и соседей, повторный клик снимает. Разбор выбранного узла —
// в правой колонке: вид, значение и список связей теми же глаголами, что на рёбрах.
using System;
using System.Collections.Generic;
using System.Text;
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Граф связей: кольца узлов, рёбра шести типов, фокус и разбор выбранного.</summary>
    public sealed class CnRelGraphView
    {
        private const float NodeW = 112f;
        private const float NodeH = 26f;
        private const float MaxNodeW = 220f;   // потолок для очень длинных имён: дальше работает ellipsis в .cn-relg__nd
        private const float MinZoom = 0.55f;
        private const float MaxZoom = 1.8f;
        private const float ZoomStep = 0.15f;

        /// <summary>Глаголы рёбер мода (relations.js EDGE_TYPES: type 1…6).</summary>
        private static readonly string[] EdgeNames = { "—", "состоит в", "влияет на", "союз / вражда", "выдаёт / принимает", "награда", "требует" };

        private static readonly string[] KindNames = { "ФРАКЦИЯ", "ЧЕЛОВЕК", "КВЕСТ" };

        private readonly CnTip _tip;

        private readonly VisualElement _root;
        private readonly Label _counts;
        private readonly VisualElement _canvas;
        private readonly VisualElement _content;
        private readonly VisualElement _edges;
        private readonly VisualElement _detail;

        private readonly List<VisualElement> _nodeViews = new List<VisualElement>(64);
        private readonly List<string> _nodeKeys = new List<string>(64);
        private readonly Dictionary<string, Vector2> _center = new Dictionary<string, Vector2>(64);

        /// <summary>Ширина конкретного узла: считается из имени и значения, чтобы подпись не резалась.</summary>
        private readonly Dictionary<string, float> _nodeWidth = new Dictionary<string, float>(64);
        private float _maxNodeW = NodeW;

        private CnGraphNode[] _nodes = new CnGraphNode[0];
        private CnGraphEdge[] _edgeList = new CnGraphEdge[0];

        private string _signature = string.Empty;
        private string _selected = string.Empty;
        private float _zoom = 1f;
        private Vector2 _pan;
        private bool _dragging;
        private Vector2 _dragFrom;
        private Vector2 _panFrom;

        public VisualElement Root => _root;

        public CnRelGraphView(CnTip tip)
        {
            _tip = tip;

            _root = CnStyle.Box("cn-relg");

            var bar = CnStyle.Row("cn-relg__bar");
            bar.Add(CnStyle.Label("ГРАФ СВЯЗЕЙ", "cn-relg__title"));
            _counts = CnStyle.Label(string.Empty, "cn-relg__counts");
            bar.Add(_counts);
            bar.Add(CnStyle.Box("cn-grow"));

            var zoomOut = CnStyle.Box("cn-relg__btn");
            zoomOut.Add(CnStyle.Label("−", "cn-relg__bt"));
            zoomOut.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); Zoom(-ZoomStep); });
            bar.Add(zoomOut);

            var zoomIn = CnStyle.Box("cn-relg__btn");
            zoomIn.Add(CnStyle.Label("+", "cn-relg__bt"));
            zoomIn.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); Zoom(ZoomStep); });
            bar.Add(zoomIn);

            var reset = CnStyle.Box("cn-relg__btn");
            reset.Add(CnStyle.Label("сброс", "cn-relg__bt"));
            reset.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _zoom = 1f;
                _pan = Vector2.zero;
                Arrange();
            });
            bar.Add(reset);

            _root.Add(bar);

            // Легенда: те же шесть типов и те же цвета, что на рёбрах — числа типов не сдвигаются.
            var legend = CnStyle.Row("cn-relg__legend");
            for (int i = 1; i < EdgeNames.Length; i++) legend.Add(Chip((byte)i, EdgeNames[i]));
            _root.Add(legend);

            var body = CnStyle.Row("cn-relg__body");
            body.style.alignItems = Align.Stretch;

            _canvas = CnStyle.Box("cn-relg__canvas");
            _canvas.RegisterCallback<GeometryChangedEvent>(_ => Arrange());

            _content = CnStyle.Deco("cn-relg__content");

            _edges = CnStyle.Deco("cn-relg__edges");
            _edges.generateVisualContent += OnGenerate;
            _content.Add(_edges);

            _canvas.Add(_content);
            body.Add(_canvas);

            _detail = CnStyle.Box("cn-relg__det");
            body.Add(_detail);

            _root.Add(body);

            // Перетаскивание полотна: захват указателя на канве, сдвиг содержимого через left/top.
            _canvas.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _dragging = true;
                _dragFrom = evt.position;
                _panFrom = _pan;
                _canvas.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            _canvas.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_dragging) return;
                _pan = _panFrom + (Vector2)(evt.position - (Vector3)_dragFrom);
                ApplyPan();
            });

            _canvas.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_dragging) return;
                _dragging = false;
                _canvas.ReleasePointer(evt.pointerId);
            });

            ShowDetail(null);
        }

        /// <summary>Кнопка «закрыть»/выход из фокуса — вызывается попапом при смене секции.</summary>
        public void Clear()
        {
            _selected = string.Empty;
            ApplyFocus();
            ShowDetail(null);
        }

        public void Refresh(CnDataFeed feed)
        {
            var nodes = feed != null ? feed.graphNodes : null;
            var edges = feed != null ? feed.graphEdges : null;
            if (nodes == null) nodes = new CnGraphNode[0];
            if (edges == null) edges = new CnGraphEdge[0];

            var sb = new StringBuilder(256);
            for (int i = 0; i < nodes.Length; i++) sb.Append(nodes[i].key).Append('=').Append(nodes[i].value).Append(';');
            sb.Append('|').Append(edges.Length);
            string signature = sb.ToString();
            if (signature == _signature) return;
            _signature = signature;

            _nodes = nodes;
            _edgeList = edges;

            if (!string.IsNullOrEmpty(_selected) && IndexOf(_selected) < 0) _selected = string.Empty;

            Build();
            Arrange();
            ApplyFocus();
            ShowDetail(SelectedNode());
        }

        // ------------------------------------------------------------------ сборка

        private void Build()
        {
            for (int i = 0; i < _nodeViews.Count; i++) _nodeViews[i].RemoveFromHierarchy();
            _nodeViews.Clear();
            _nodeKeys.Clear();
            _center.Clear();
            _nodeWidth.Clear();
            _maxNodeW = NodeW;

            int factions = 0, people = 0, quests = 0;
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (node.kind == 0) factions++;
                else if (node.kind == 1) people++;
                else quests++;

                var view = CnStyle.Box("cn-relg__node");
                view.style.height = NodeH;   // ширина — по содержимому, ниже
                view.AddToClassList(node.kind == 0 ? "is-f" : node.kind == 1 ? "is-n" : "is-q");
                if (node.kind != 2) CnStyle.Fill(view, CnStyle.VeilSoft, 0.82f);
                else CnStyle.Fill(view, CnStyle.Veil, 0.72f);

                var name = CnStyle.Label(node.name, "cn-relg__nd");
                view.Add(name);
                view.Add(CnStyle.Box("cn-grow"));

                Label valueLabel = null;
                if (node.kind != 2)
                {
                    valueLabel = CnStyle.Label((node.value > 0 ? "+" : string.Empty) + node.value, "cn-relg__nv");
                    valueLabel.style.color = node.value < 0 ? CnStyle.DangerInk : node.value > 0 ? CnStyle.Accent : CnStyle.Dim;
                    view.Add(valueLabel);
                }

                // Ширина узла — по фактической ширине текста, а не константой 112 px: в моде узел растёт под имя
                // (rel.css .rn — padding 0 10px без обрезки), а фиксированная ширина резала «Вольные Пере…».
                const float padH = 20f;   // .cn-relg__node padding 0 10px
                float nameW = name.MeasureTextSize(node.name, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                if (float.IsNaN(nameW) || nameW <= 0f) nameW = NodeW;   // шрифт ещё не разрешён — не раздуваем узел
                float nodeW = nameW + padH;
                if (valueLabel != null)
                {
                    float valueW = valueLabel.MeasureTextSize(valueLabel.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                    if (float.IsNaN(valueW) || valueW < 0f) valueW = 0f;
                    nodeW += valueW + 4f;   // .cn-relg__nv margin-left 4px
                }

                nodeW = Mathf.Clamp(Mathf.Ceil(nodeW), NodeW, MaxNodeW);
                view.style.width = nodeW;
                _nodeWidth[node.key] = nodeW;
                if (nodeW > _maxNodeW) _maxNodeW = nodeW;

                string key = node.key;
                int index = i;
                view.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    _selected = _selected == key ? string.Empty : key;
                    ApplyFocus();
                    ShowDetail(SelectedNode());
                });

                // Карточка узла (rel-graph.js card): вид, значение и число связей — без выдуманных полей.
                _tip?.Hover(view, () =>
                {
                    var n = _nodes[index];
                    var card = CnTipCard.Make(n.name, KindNames[n.kind]);
                    if (n.kind == 0) card.Row("репутация", n.value + " (−100…+100)");
                    if (n.kind == 1) card.Row("отношение", (n.value > 0 ? "+" : string.Empty) + n.value + " (−100…+200)");
                    card.Row("связей", CountLinks(n.key).ToString());
                    card.Foot("клик — подсветить связи");
                    return card;
                });

                _content.Add(view);
                _nodeViews.Add(view);
                _nodeKeys.Add(node.key);
            }

            _counts.text = factions + " фракции · " + people + " люди · " + quests + " квесты";
            _edges.MarkDirtyRepaint();
        }

        /// <summary>
        /// Кольца по видам: фракции внутри, люди посередине, квесты снаружи. Радиус считается от фактического
        /// размера полотна, поэтому граф не вылезает за край при любом размере попапа.
        /// </summary>
        private void Arrange()
        {
            float w = _canvas.layout.width;
            float h = _canvas.layout.height;
            if (float.IsNaN(w) || w < 120f) w = 640f;
            if (float.IsNaN(h) || h < 120f) h = 420f;

            _content.style.left = 0f;
            _content.style.top = 0f;
            _content.style.width = w;
            _content.style.height = h;
            _edges.style.left = 0f;
            _edges.style.top = 0f;
            _edges.style.width = w;
            _edges.style.height = h;

            float baseR = Mathf.Min(w, h) * 0.5f - _maxNodeW * 0.6f;
            if (baseR < 40f) baseR = 40f;

            var rings = new[]
            {
                RingKeys(0, baseR * 0.55f * _zoom),
                RingKeys(1, baseR * 0.82f * _zoom),
                RingKeys(2, baseR * 1.06f * _zoom)
            };

            var center = new Vector2(w * 0.5f, h * 0.5f);
            for (int r = 0; r < rings.Length; r++)
            {
                var keys = rings[r].Keys;
                float radius = rings[r].Radius;
                if (keys.Count == 0) continue;

                for (int i = 0; i < keys.Count; i++)
                {
                    float angle = -Mathf.PI * 0.5f + Mathf.PI * 2f * i / keys.Count;
                    var at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    var view = _nodeViews[IndexOf(keys[i])];
                    float nodeW = _nodeWidth.TryGetValue(keys[i], out var storedW) ? storedW : NodeW;
                    view.style.left = at.x - nodeW * 0.5f;
                    view.style.top = at.y - NodeH * 0.5f;
                    _center[keys[i]] = at;
                }
            }

            ApplyPan();
            _edges.MarkDirtyRepaint();
        }

        private readonly struct Ring
        {
            public readonly List<string> Keys;
            public readonly float Radius;

            public Ring(List<string> keys, float radius)
            {
                Keys = keys;
                Radius = radius;
            }
        }

        private Ring RingKeys(byte kind, float radius)
        {
            var keys = new List<string>(16);
            for (int i = 0; i < _nodes.Length; i++)
                if (_nodes[i].kind == kind) keys.Add(_nodes[i].key);
            return new Ring(keys, radius);
        }

        private void ApplyPan()
        {
            _content.style.left = _pan.x;
            _content.style.top = _pan.y;
        }

        private void Zoom(float delta)
        {
            _zoom = Mathf.Clamp(_zoom + delta, MinZoom, MaxZoom);
            Arrange();
        }

        // ------------------------------------------------------------------ рёбра

        private void OnGenerate(MeshGenerationContext mgc)
        {
            if (_edgeList.Length == 0) return;

            var p = mgc.painter2D;
            p.lineWidth = 1.4f;
            p.lineJoin = LineJoin.Round;

            for (int i = 0; i < _edgeList.Length; i++)
            {
                var e = _edgeList[i];
                if (!_center.TryGetValue(e.from, out var a)) continue;
                if (!_center.TryGetValue(e.to, out var b)) continue;

                bool lit = string.IsNullOrEmpty(_selected) || e.from == _selected || e.to == _selected;
                var color = EdgeColor(e);
                color.a = lit ? 0.9f : 0.16f;

                p.strokeColor = color;
                p.lineWidth = lit && !string.IsNullOrEmpty(_selected) ? 2f : 1.4f;
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }
        }

        /// <summary>Цвет ребра по типу мода; вражда и минус в награде — красным, союз — зелёным.</summary>
        private static Color EdgeColor(CnGraphEdge e)
        {
            switch (e.type)
            {
                case 1: return CnStyle.Accent;
                case 2: return e.negative ? CnStyle.DangerInk : CnStyle.Warn;
                case 3: return e.negative ? CnStyle.DangerLine : CnStyle.GasAccent;
                case 4: return CnStyle.Muted;
                case 5: return CnStyle.Brass;
                default: return CnStyle.Dim;
            }
        }

        private static string EdgeName(byte type) => type < EdgeNames.Length ? EdgeNames[type] : EdgeNames[0];

        // ------------------------------------------------------------------ фокус и разбор

        private void ApplyFocus()
        {
            // Сначала снять всё, потом выставить: так состояние не зависит от предыдущего кадра.
            for (int i = 0; i < _nodeViews.Count; i++)
            {
                _nodeViews[i].RemoveFromClassList("is-sel");
                _nodeViews[i].RemoveFromClassList("is-nb");
                _nodeViews[i].RemoveFromClassList("is-dim");
            }

            if (string.IsNullOrEmpty(_selected))
            {
                _edges.MarkDirtyRepaint();
                return;
            }

            var neighbors = new HashSet<string>();
            for (int i = 0; i < _edgeList.Length; i++)
            {
                var e = _edgeList[i];
                if (e.from == _selected) neighbors.Add(e.to);
                else if (e.to == _selected) neighbors.Add(e.from);
            }

            for (int i = 0; i < _nodeViews.Count; i++)
            {
                string key = _nodeKeys[i];
                if (key == _selected) _nodeViews[i].AddToClassList("is-sel");
                else if (neighbors.Contains(key)) _nodeViews[i].AddToClassList("is-nb");
                else _nodeViews[i].AddToClassList("is-dim");
            }

            _edges.MarkDirtyRepaint();
        }

        private CnGraphNode? SelectedNode()
        {
            int index = IndexOf(_selected);
            return index >= 0 ? _nodes[index] : (CnGraphNode?)null;
        }

        private void ShowDetail(CnGraphNode? node)
        {
            _detail.Clear();
            _detail.Add(CnStyle.Label("РАЗБОР", "cn-relg__det-t"));

            if (node == null)
            {
                _detail.Add(CnStyle.Label("выбери узел — покажу связи и значение", "cn-relg__det-empty"));
                return;
            }

            var n = node.Value;
            _detail.Add(CnStyle.Label(n.name, "cn-relg__det-name"));
            _detail.Add(CnStyle.Label(KindNames[n.kind], "cn-relg__det-kind"));

            if (n.kind == 0) _detail.Add(DetailLine("репутация", n.value + " (−100…+100)"));
            if (n.kind == 1) _detail.Add(DetailLine("отношение", (n.value > 0 ? "+" : string.Empty) + n.value + " (−100…+200)"));

            int shown = 0;
            for (int i = 0; i < _edgeList.Length; i++)
            {
                var e = _edgeList[i];
                bool outbound = e.from == n.key;
                if (!outbound && e.to != n.key) continue;

                string otherKey = outbound ? e.to : e.from;
                var row = CnStyle.Row("cn-relg__link");
                if (shown == 0) row.AddToClassList("is-first");

                var bar = CnStyle.Deco("cn-relg__link-sw");
                bar.style.backgroundColor = EdgeColor(e);
                row.Add(bar);

                row.Add(CnStyle.Label(EdgeName(e.type), "cn-relg__link-k"));
                row.Add(CnStyle.Label(string.IsNullOrEmpty(e.label) ? otherKey : e.label, "cn-relg__link-v"));
                _detail.Add(row);
                shown++;
            }

            if (shown == 0) _detail.Add(CnStyle.Label("связей в видимых данных нет", "cn-relg__det-empty"));
        }

        private static VisualElement DetailLine(string key, string value)
        {
            var line = CnStyle.Row("cn-relg__det-line");
            line.Add(CnStyle.Label(key, "cn-relg__det-k"));
            line.Add(CnStyle.Box("cn-grow"));
            line.Add(CnStyle.Label(value, "cn-relg__det-v"));
            return line;
        }

        private static VisualElement Chip(byte type, string name)
        {
            var chip = CnStyle.Row("cn-relg__chip");
            var swatch = CnStyle.Deco("cn-relg__chip-sw");
            swatch.style.backgroundColor = EdgeColor(new CnGraphEdge { type = type });
            chip.Add(swatch);
            chip.Add(CnStyle.Label(name, "cn-relg__chip-t"));
            return chip;
        }

        // ------------------------------------------------------------------ утилиты

        private int IndexOf(string key)
        {
            for (int i = 0; i < _nodes.Length; i++) if (_nodes[i].key == key) return i;
            return -1;
        }

        private int CountLinks(string key)
        {
            int n = 0;
            for (int i = 0; i < _edgeList.Length; i++)
                if (_edgeList[i].from == key || _edgeList[i].to == key) n++;
            return n;
        }

        public void Dispose()
        {
            _edges.generateVisualContent -= OnGenerate;
            for (int i = 0; i < _nodeViews.Count; i++) _nodeViews[i].RemoveFromHierarchy();
            _nodeViews.Clear();
            _nodeKeys.Clear();
            _center.Clear();
        }
    }
}
