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
//
// Тросы: дуга с провисом, подложка, двойной трос союза, рваный трос вражды, стрелки-каретки влияния и
// подпись-глагол на дуге (rel-graph.js:351-502). Геометрия та же, что в моде, но без кадрового качания:
// раскладка порта детерминированная и один снапшот обязан давать одну картинку.
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const float RingFaction = 38f;   // rel-graph.js:233 — ringSVG(v, 38 · fitK)
        private const float RingPerson = 30f;    // rel-graph.js:243 — ringSVG(v, 30 · fitK)
        private const float RingGap = 6f;
        private const float MaxNodeW = 220f;   // потолок для очень длинных имён: дальше работает ellipsis в .cn-relg__nd
        private const float MinZoom = 0.55f;
        private const float MaxZoom = 1.8f;
        private const float ZoomStep = 0.15f;
        private const float WheelFactor = 1.12f;   // rel-graph.js:1190 — ×1.12 на щелчок колеса
        private const float LitWidth = 2.1f;   // rel.css:179 — .rp.focusing .rp-e.lit path{stroke-width:2.1px}

        private const float NoteW = 152f;    // rel-graph.js:77 — .rn-note width:152px
        private const float NoteH = 64f;     // rel-graph.js:77 — min-height:64px
        private const float FoldW = 44f;     // rel-graph.js:103 — .rn-note.min width:44px
        private const float FoldH = 42f;     // rel-graph.js:103 — min-height:42px
        private const int PulseMs = 1200;    // половина цикла ntpulse 2.4 s (rel-graph.js:110-111)
        private const float PanThreshold = 3f;      // rel-graph.js:1172 — panning.moved включается только после сдвига
        private const float NoteDragThreshold = 3f; // то же для стикера: клик не должен становиться перетаскиванием

        /// <summary>Глаголы рёбер мода (relations.js EDGE_TYPES: type 1…6).</summary>
        private static readonly string[] EdgeNames = { "—", "состоит в", "влияет на", "союз / вражда", "выдаёт / принимает", "награда", "требует" };

        private static readonly string[] KindNames = { "ФРАКЦИЯ", "ЧЕЛОВЕК", "КВЕСТ" };

        private readonly CnTip _tip;

        private readonly VisualElement _root;
        private readonly Label _counts;
        private readonly VisualElement _canvas;
        private readonly VisualElement _content;
        private readonly VisualElement _edges;
        private readonly VisualElement _verbs;
        private readonly VisualElement _detail;
        private readonly Label _zoomBadge;
        private readonly CnRelNotesStore _notes;
        private readonly VisualElement _cableLayer;
        private readonly VisualElement _noteLayer;
        private readonly List<VisualElement> _noteViews = new List<VisualElement>(8);

        private readonly List<VisualElement> _nodeViews = new List<VisualElement>(64);
        private readonly List<string> _nodeKeys = new List<string>(64);
        private readonly Dictionary<string, Vector2> _center = new Dictionary<string, Vector2>(64);

        /// <summary>Ширина конкретного узла: считается из имени и значения, чтобы подпись не резалась.</summary>
        private readonly Dictionary<string, float> _nodeWidth = new Dictionary<string, float>(64);

        /// <summary>Высота узла: у узла с кольцом она равна кольцу плюс поля, поэтому центровка — по ней.</summary>
        private readonly Dictionary<string, float> _nodeHeight = new Dictionary<string, float>(64);
        private float _maxNodeW = NodeW;

        /// <summary>Подписи-глаголы на тросах: по одной на ребро, позиции считает PlaceLabels.</summary>
        private readonly List<Label> _verbViews = new List<Label>(64);
        private string _verbsSignature = string.Empty;

        private CnGraphNode[] _nodes = new CnGraphNode[0];
        private CnGraphEdge[] _edgeList = new CnGraphEdge[0];

        private string _signature = string.Empty;
        private string _selected = string.Empty;
        private float _zoom = 1f;
        private Vector2 _pan;
        private bool _dragging;
        private bool _dragMoved;
        private Vector2 _dragFrom;
        private Vector2 _panFrom;

        private string _noteDragId = string.Empty;
        private bool _noteDragMoved;
        private Vector2 _noteDragFrom;
        private Vector2 _notePosFrom;

        public VisualElement Root => _root;

        /// <summary>notes — хранилище для проверок; без него заметки лежат в Application.persistentDataPath.</summary>
        public CnRelGraphView(CnTip tip, CnRelNotesStore notes = null)
        {
            _tip = tip;
            _notes = notes ?? new CnRelNotesStore();
            _notes.EnsureLoaded();

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

            // Заметка: в моде — ПКМ → «СОЗДАТЬ ЗАМЕТКУ» (rel-graph.js:1161). Контекстного меню у UI Toolkit нет,
            // поэтому действие вынесено биркой в шапку — иначе возможности было бы не видно.
            var addNote = CnStyle.Box("cn-relg__btn");
            addNote.Add(CnStyle.Label("+ ЗАМЕТКА", "cn-relg__bt"));
            addNote.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                AddNote();
            });
            bar.Add(addNote);

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

            // Подписи-глаголы (rel-graph.js .rp-verbs) — отдельный слой: Painter2D текста не рисует,
            // поэтому глагол ребра это Label, а не примитив. Слой держится выше узлов, как в моде.
            _verbs = CnStyle.Deco("cn-relg__verbs");
            _content.Add(_verbs);

            // Слой тросов заметок и слой самих стикеров. В моде заметки — последний слой полотна, выше слов графа
            // (rel-graph.js:131-134); тросы идут под стикерами, но над рёбрами и глаголами.
            _cableLayer = CnStyle.Deco("cn-relg__cables");
            _cableLayer.generateVisualContent += OnGenerateCables;
            _content.Add(_cableLayer);

            _noteLayer = CnStyle.Deco("cn-relg__notes");
            _content.Add(_noteLayer);

            _canvas.Add(_content);

            // Бейдж зума и подсказка жестов (rel-graph.js .rp-zoom / .rp-hint2): наложены на полотно, а не
            // занимают строку шапки, и не берут указатель — иначе они мешали бы перетаскиванию полотна.
            _zoomBadge = CnStyle.Label(string.Empty, "cn-relg__zoom");
            _zoomBadge.pickingMode = PickingMode.Ignore;
            _canvas.Add(_zoomBadge);

            var hint = CnStyle.Label("ТАЩИ ПУСТОЕ — ПАНОРАМА · КОЛЕСО — ЗУМ", "cn-relg__hint");
            hint.pickingMode = PickingMode.Ignore;
            _canvas.Add(hint);

            body.Add(_canvas);

            _detail = CnStyle.Box("cn-relg__det");
            body.Add(_detail);

            _root.Add(body);

            // Перетаскивание полотна: захват указателя на канве, сдвиг содержимого через left/top.
            // Захват ставится НЕ на нажатие, а после порога сдвига: пока указатель захвачен канвой, производный
            // ClickEvent уходит ей, а не ребёнку — из-за этого не разворачивались свёрнутые заметки и не
            // выбирались узлы. В моде то же правило — флаг panning.moved (rel-graph.js:1169-1197).
            _canvas.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _dragging = true;
                _dragMoved = false;
                _dragFrom = evt.position;
                _panFrom = _pan;
            });

            _canvas.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_dragging) return;
                if ((evt.pressedButtons & 1) == 0)
                {
                    _dragging = false;   // кнопку отпустили вне канвы — панорама кончилась
                    return;
                }

                var delta = (Vector2)evt.position - _dragFrom;
                if (!_dragMoved)
                {
                    if (delta.sqrMagnitude < PanThreshold * PanThreshold) return;
                    _dragMoved = true;
                    _canvas.CapturePointer(evt.pointerId);
                }

                _pan = _panFrom + delta;
                ApplyPan();
            });

            _canvas.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_dragging) return;
                _dragging = false;
                if (!_dragMoved) return;

                _dragMoved = false;
                _canvas.ReleasePointer(evt.pointerId);
            });

            // Колесо — зум вокруг указателя (rel-graph.js:1187-1191). StopPropagation обязателен: то же колесо
            // не должно одновременно прокручивать содержимое попапа.
            _canvas.RegisterCallback<WheelEvent>(evt =>
            {
                evt.StopPropagation();
                if (Mathf.Approximately(evt.delta.y, 0f)) return;
                ZoomByFactor(evt.delta.y > 0f ? WheelFactor : 1f / WheelFactor, _canvas.WorldToLocal((Vector2)evt.mousePosition));
            });

            // Пульс свёрнутой заметки: класс-переключатель раз в половину цикла, саму прозрачность ведёт USS-переход.
            _root.schedule.Execute(ToggleFoldPulse).Every(PulseMs);

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
            _nodeHeight.Clear();
            _maxNodeW = NodeW;

            int factions = 0, people = 0, quests = 0;
            for (int i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (node.kind == 0) factions++;
                else if (node.kind == 1) people++;
                else quests++;

                var view = CnStyle.Box("cn-relg__node");

                // Кольцо отношения (rel-ring.js): у фракции 38 px, у человека 30 px; узел растёт под него.
                float ring = node.kind == 0 ? RingFaction : node.kind == 1 ? RingPerson : 0f;
                float nodeH = ring > 0f ? ring + 6f : NodeH;
                view.style.height = nodeH;
                view.AddToClassList(node.kind == 0 ? "is-f" : node.kind == 1 ? "is-n" : "is-q");
                if (node.kind != 2) CnStyle.Fill(view, CnStyle.VeilSoft, 0.82f);
                else CnStyle.Fill(view, CnStyle.Veil, 0.72f);

                // 06 §4: .rn > .halo-rnin — внутренний ореол rgba(7,10,9,.8) по кромке узла графа.
                CnStyle.HaloInner(view, 7, CnStyle.Veil, 0.8f);

                if (ring > 0f)
                    view.Add(new CnRelRing(ring, node.value, node.scaleMin, node.scaleMax,
                        node.color, node.color.a > 0.01f, node.tierValues));

                var name = CnStyle.Label(node.name, "cn-relg__nd");
                view.Add(name);
                view.Add(CnStyle.Box("cn-grow"));

                // Ширина узла — по фактической ширине текста, а не константой 112 px: в моде узел растёт под имя
                // (rel.css .rn — padding 0 10px без обрезки), а фиксированная ширина резала «Вольные Пере…».
                const float padH = 20f;   // .cn-relg__node padding 0 10px
                float nameW = name.MeasureTextSize(node.name, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                if (float.IsNaN(nameW) || nameW <= 0f) nameW = NodeW;   // шрифт ещё не разрешён — не раздуваем узел
                float nodeW = nameW + padH + (ring > 0f ? ring + RingGap : 0f);

                nodeW = Mathf.Clamp(Mathf.Ceil(nodeW), NodeW, MaxNodeW);
                view.style.width = nodeW;
                _nodeWidth[node.key] = nodeW;
                _nodeHeight[node.key] = nodeH;
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

            // Подписи пересобираются вместе с данными и держатся поверх узлов (rel-graph.js:130, слои svg → nodes → verbs).
            _verbs.Clear();
            _verbViews.Clear();
            _verbsSignature = string.Empty;
            _verbs.BringToFront();

            // BringToFront у глаголов опускает слои заметок — возвращаем их наверх в том же порядке, что в моде.
            _cableLayer.BringToFront();
            _noteLayer.BringToFront();

            _edges.MarkDirtyRepaint();

            // Пока в поле заметки стоит каретка, стикеры не пересобираются: иначе пришедший снапшот вырывал бы
            // фокус посреди набора. Данные при этом уже приняты — стикеры догонят на ближайшей пересборке.
            if (!NoteFocused()) RebuildNotes();
        }

        /// <summary>Есть ли фокус в слое заметок (поля текста и привязки).</summary>
        private bool NoteFocused()
        {
            var focused = _noteLayer.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && _noteLayer.Contains(focused);
        }

        /// <summary>
        /// Кольца по видам: фракции внутри, люди посередине, квесты снаружи. Радиус считается от фактического
        /// размера полотна, поэтому граф не вылезает за край при любом размере попапа.
        /// </summary>
        private void Arrange()
        {
            var size = CanvasSize();
            float w = size.x;
            float h = size.y;

            _content.style.left = 0f;
            _content.style.top = 0f;
            _content.style.width = w;
            _content.style.height = h;
            _edges.style.left = 0f;
            _edges.style.top = 0f;
            _edges.style.width = w;
            _edges.style.height = h;
            _verbs.style.left = 0f;
            _verbs.style.top = 0f;
            _verbs.style.width = w;
            _verbs.style.height = h;

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
                    float nodeH = _nodeHeight.TryGetValue(keys[i], out var storedH) ? storedH : NodeH;
                    view.style.left = at.x - nodeW * 0.5f;
                    view.style.top = at.y - nodeH * 0.5f;
                    _center[keys[i]] = at;
                }
            }

            ApplyPan();
            UpdateZoomBadge();
            _edges.MarkDirtyRepaint();
            _cableLayer.MarkDirtyRepaint();
            PlaceLabels();
        }

        /// <summary>Размер полотна с тем же запасом, что и в Arrange: до первой раскладки layout ещё нулевой.</summary>
        private Vector2 CanvasSize()
        {
            float w = _canvas.layout.width;
            float h = _canvas.layout.height;
            if (float.IsNaN(w) || w < 120f) w = 640f;
            if (float.IsNaN(h) || h < 120f) h = 420f;
            return new Vector2(w, h);
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

        /// <summary>
        /// Пропорциональный зум вокруг точки указателя. Кольца масштабируются вокруг центра полотна, поэтому
        /// полотно сдвигается на разницу положений этой точки: под курсором остаётся тот же участок графа.
        /// </summary>
        private void ZoomByFactor(float factor, Vector2 at)
        {
            float before = _zoom;
            float after = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(after, before)) return;

            var size = CanvasSize();
            var center = new Vector2(size.x * 0.5f, size.y * 0.5f);
            var inContent = at - _pan;
            var stays = center + (inContent - center) * (after / before);

            _zoom = after;
            _pan = at - stays;
            Arrange();
        }

        /// <summary>Бейдж зума: значение в том же виде, что в моде — «ЗУМ 1.00×» (rel-graph.js:545).</summary>
        private void UpdateZoomBadge()
        {
            if (_zoomBadge == null) return;
            _zoomBadge.text = "ЗУМ " + _zoom.ToString("0.00", CultureInfo.InvariantCulture) + "×";
        }

        // ------------------------------------------------------------------ заметки

        /// <summary>
        /// Пересборка стикеров. Зовётся при смене данных и после действий над заметкой; текст правится без
        /// пересборки (иначе поле теряло бы фокус), место — прямо в обработчике перетаскивания.
        /// </summary>
        private void RebuildNotes()
        {
            for (int i = 0; i < _noteViews.Count; i++) _noteViews[i].RemoveFromHierarchy();
            _noteViews.Clear();

            var list = _notes.Notes;
            for (int i = 0; i < list.Count; i++)
            {
                var view = BuildNote(list[i]);
                _noteLayer.Add(view);
                _noteViews.Add(view);
            }

            _cableLayer.MarkDirtyRepaint();
        }

        /// <summary>
        /// Новая заметка в центре полотна. В моде её создаёт ПКМ → «СОЗДАТЬ ЗАМЕТКУ» (rel-graph.js:1161); в порте —
        /// бирка в шапке, потому что контекстного меню у UI Toolkit нет, а скрытая клавиша была бы невидимой.
        /// </summary>
        private void AddNote()
        {
            var size = CanvasSize();
            int n = _notes.Count;
            float x = Mathf.Clamp(size.x * 0.5f - NoteW * 0.5f - _pan.x + n % 4 * 18f, 0f, Mathf.Max(0f, size.x - NoteW));
            float y = Mathf.Clamp(size.y * 0.5f - NoteH * 0.5f - _pan.y + n % 4 * 16f, 0f, Mathf.Max(0f, size.y - NoteH));
            _notes.Add(x, y);
            RebuildNotes();
        }

        private VisualElement BuildNote(CnRelNote note)
        {
            var view = CnStyle.Box("cn-relg__note");
            view.style.left = note.x;
            view.style.top = note.y;
            view.style.rotate = new Rotate(new Angle(note.rot, AngleUnit.Degree));

            if (note.collapsed)
            {
                view.AddToClassList("is-min");

                // 06 §4: .rn-note.min > .halo-notein — латунный внутренний ореол rgba(141,122,78,.4), и пульс
                // ntpulse (.62 ↔ 1 за 2.4 s, §8) ведёт именно этот ореол, а не сам стикер, как было в A13.
                CnStyle.HaloInner(view, 7, CnStyle.BrassDeep, 0.4f)?.AddToClassList("cn-halo--note-in");

                view.Add(new CnNoteFold());

                var folded = note;
                _tip?.Hover(view, () => CnTipCard.Make("заметка", ShortText(folded.text)).Foot("клик — развернуть"));
                view.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    _notes.SetCollapsed(folded.id, false);
                    RebuildNotes();
                });
                return view;
            }

            // 06 §4: .rn-note > .halo-notein — внутренний ореол rgba(7,10,9,.7): плёнка заметки плотнее узла.
            CnStyle.HaloInner(view, 7, CnStyle.Veil, 0.7f);

            // Латунный волосок заметки (rel-graph.js:84: псевдоэлемент before стал ребёнком .halo-rnhair).
            view.Add(CnStyle.Deco("cn-relg__note-hair"));
            view.Add(CnStyle.Label("ЗАМЕТКА", "cn-relg__note-k"));

            var text = new TextField { multiline = true, value = note.text ?? string.Empty };
            text.label = null;
            text.AddToClassList("cn-relg__note-in");
            ApplyFieldFont(text);
            text.RegisterValueChangedCallback(evt => _notes.SetText(note.id, evt.newValue));
            view.Add(text);

            view.Add(CnStyle.Label("прикрепить к…", "cn-relg__note-lbl"));
            view.Add(AttachField(note));

            var acts = CnStyle.Row("cn-relg__note-acts");
            acts.Add(NoteChip("СВЕРНУТЬ", () =>
            {
                _notes.SetCollapsed(note.id, true);
                RebuildNotes();
            }));
            acts.Add(NoteChip("УДАЛИТЬ", () =>
            {
                _notes.Delete(note.id);
                RebuildNotes();
            }));
            view.Add(acts);

            Drag(view, note);
            return view;
        }

        /// <summary>
        /// Привязка к субъекту. В моде это &lt;select&gt; с известными фракциями и людьми (rel-graph.js:1031-1036);
        /// известное окна и есть узлы графа (прочих оно не знает), поэтому список — узлы фракций и людей.
        /// Квесты не предлагаются: заметка привязывается к субъекту, как в моде.
        /// </summary>
        private VisualElement AttachField(CnRelNote note)
        {
            var choices = new List<string>(_nodes.Length + 1) { "— не прикреплена —" };
            var keys = new List<string>(_nodes.Length + 1) { null };
            int current = 0;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i].kind == 2) continue;
                if (note.attach == _nodes[i].key) current = choices.Count;
                choices.Add(_nodes[i].name);
                keys.Add(_nodes[i].key);
            }

            // Привязка к субъекту, которого нет в текущем снапшоте, не теряется: в моде attach тоже остаётся
            // (rel-graph.js:988-1005). Показываем её отдельной строкой, чтобы выбор не выглядел сброшенным.
            if (!string.IsNullOrEmpty(note.attach) && !keys.Contains(note.attach))
            {
                current = choices.Count;
                choices.Add("— " + note.attach + " (субъекта нет в данных) —");
                keys.Add(note.attach);
            }

            var field = new DropdownField(null, choices, current);
            field.AddToClassList("cn-relg__note-sel");
            field.EnableInClassList("is-on", !string.IsNullOrEmpty(note.attach));
            ApplyFieldFont(field);

            var captured = keys;
            field.RegisterValueChangedCallback(_ =>
            {
                int index = Mathf.Max(0, field.index);
                _notes.Attach(note.id, index < captured.Count ? captured[index] : null);
                field.EnableInClassList("is-on", index > 0);
                _cableLayer.MarkDirtyRepaint();
            });

            return field;
        }

        /// <summary>
        /// Перетаскивание стикера (rel-graph.js:1048-1072). Нажатие не уходит в полотно — иначе перетаскивание
        /// заметки превращалось бы в панораму графа, — но указатель захватывается только после порога сдвига:
        /// захват на нажатии отдавал бы ClickEvent заметке целиком и съедал бы клик «развернуть» у свёрнутой.
        /// </summary>
        private void Drag(VisualElement view, CnRelNote note)
        {
            view.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                evt.StopPropagation();
                if (IsFieldTarget(evt.target as VisualElement)) return;

                _noteDragId = note.id;
                _noteDragMoved = false;
                _noteDragFrom = (Vector2)evt.position;
                _notePosFrom = new Vector2(note.x, note.y);
            });

            view.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_noteDragId != note.id) return;
                if ((evt.pressedButtons & 1) == 0)
                {
                    _noteDragId = string.Empty;   // кнопку отпустили вне заметки
                    return;
                }

                var delta = (Vector2)evt.position - _noteDragFrom;
                if (!_noteDragMoved)
                {
                    if (delta.sqrMagnitude < NoteDragThreshold * NoteDragThreshold) return;
                    _noteDragMoved = true;
                    view.CapturePointer(evt.pointerId);
                }

                var size = CanvasSize();
                note.x = Mathf.Clamp(_notePosFrom.x + delta.x, 0f, Mathf.Max(0f, size.x - NoteW));
                note.y = Mathf.Clamp(_notePosFrom.y + delta.y, 0f, Mathf.Max(0f, size.y - NoteH));
                view.style.left = note.x;
                view.style.top = note.y;
                _cableLayer.MarkDirtyRepaint();
            });

            view.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_noteDragId != note.id) return;
                _noteDragId = string.Empty;
                if (!_noteDragMoved) return;

                _noteDragMoved = false;
                view.ReleasePointer(evt.pointerId);
                _notes.SetPos(note.id, note.x, note.y);
            });
        }

        /// <summary>Поля и бирки не двигают заметку: иначе набор текста и нажатие кнопки срывали бы её с места.</summary>
        private static bool IsFieldTarget(VisualElement target)
        {
            for (var el = target; el != null; el = el.parent)
            {
                if (el is TextField || el is TextInputBaseField<string> || el is DropdownField || el is TextElement || el is Button)
                    return true;
            }

            return false;
        }

        private VisualElement NoteChip(string text, Action onClick)
        {
            var chip = CnStyle.Box("cn-relg__note-btn");
            chip.Add(CnStyle.Label(text, "cn-relg__note-btn-t"));

            var handler = onClick;
            chip.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                handler?.Invoke();
            });

            return chip;
        }

        /// <summary>Тросы заметок: от центра стикера к центру привязанного узла (rel-graph.js:1116-1150).</summary>
        private void OnGenerateCables(MeshGenerationContext mgc)
        {
            var list = _notes.Notes;
            if (list.Count == 0) return;

            var p = mgc.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            for (int i = 0; i < list.Count; i++)
            {
                var note = list[i];
                if (string.IsNullOrEmpty(note.attach)) continue;
                if (!_center.TryGetValue(note.attach, out var node)) continue;

                // Точка крепления — центр стикера: у развёрнутой 152×64, у свёрнутой 44×42 (rel-graph.js:1114-1116).
                var anchor = new Vector2(note.x + (note.collapsed ? FoldW : NoteW) * 0.5f,
                                         note.y + (note.collapsed ? FoldH : NoteH) * 0.5f);

                Curve(anchor, node, out var c, out _, out _);

                // Подложка троса держит читаемость поверх рёбер, сам трос — латунный и тоньше ребра.
                p.strokeColor = CnStyle.A(CnStyle.Veil, 0.5f);
                p.lineWidth = 3.4f;
                p.BeginPath();
                p.MoveTo(anchor);
                p.QuadraticCurveTo(c, node);
                p.Stroke();

                p.strokeColor = CnStyle.A(CnStyle.Brass, 0.55f);
                p.lineWidth = 1.1f;
                p.BeginPath();
                p.MoveTo(anchor);
                p.QuadraticCurveTo(c, node);
                p.Stroke();
            }
        }

        /// <summary>Пульс свёрнутых заметок: прозрачность ведёт USS-переход, здесь только класс-переключатель.</summary>
        private void ToggleFoldPulse()
        {
            for (int i = 0; i < _noteViews.Count; i++)
            {
                var note = _noteViews[i];
                if (!note.ClassListContains("is-min")) continue;
                note.EnableInClassList("is-lift", !note.ClassListContains("is-lift"));
            }
        }

        private static string ShortText(string text)
        {
            string trimmed = text ?? string.Empty;
            return trimmed.Length <= 90 ? trimmed : trimmed.Substring(0, 90) + "…";
        }

        /// <summary>Шрифт поля: тема панели ставит свой, поэтому SDF задаётся и полю, и его полю ввода.</summary>
        private static void ApplyFieldFont(VisualElement field)
        {
            CnStyle.ApplyFont(field);

            var input = field.Q(className: "unity-base-field__input");
            if (input != null) CnStyle.ApplyFont(input);
        }

        // ------------------------------------------------------------------ рёбра

        private void OnGenerate(MeshGenerationContext mgc)
        {
            if (_edgeList.Length == 0) return;

            var p = mgc.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            for (int i = 0; i < _edgeList.Length; i++)
            {
                var e = _edgeList[i];
                if (!_center.TryGetValue(e.from, out var a)) continue;
                if (!_center.TryGetValue(e.to, out var b)) continue;

                bool lit = string.IsNullOrEmpty(_selected) || e.from == _selected || e.to == _selected;
                bool focused = lit && !string.IsNullOrEmpty(_selected);

                var color = EdgeColor(e);
                color.a = lit ? 0.9f : 0.16f;
                float w = focused ? LitWidth : StrokeWidth(e);

                Curve(a, b, out var c, out var normal, out float len);

                // Подложка под трос (rel-graph.js:372): тёмная полоса на 2.4 px шире линии держит читаемость
                // там, где тросы пересекаются.
                p.strokeColor = lit ? CnStyle.A(CnStyle.Veil, 0.55f) : CnStyle.A(CnStyle.Veil, 0.10f);
                p.lineWidth = w + 2.4f;
                StrokeQuad(p, a, c, b, Vector2.zero);

                if (e.type == 3 && !e.negative)
                {
                    // Союз — двойной трос (rel-graph.js:410-412): две линии на ±2 px от оси.
                    p.strokeColor = color;
                    p.lineWidth = w;
                    StrokeQuad(p, a, c, b, normal * 2f);
                    StrokeQuad(p, a, c, b, normal * -2f);
                }
                else if (e.type == 3)
                {
                    // Вражда — рваный (rel-graph.js:413-421): зигзаг поперёк оси, концы остаются на оси.
                    p.strokeColor = color;
                    p.lineWidth = w;
                    Ragged(p, a, c, b, normal, len);
                }
                else
                {
                    p.strokeColor = color;
                    p.lineWidth = w;
                    StrokeQuad(p, a, c, b, Vector2.zero);
                }

                // Тип 2 — две стрелки-каретки по дуге (rel-graph.js:425-438).
                if (e.type == 2) Arrows(p, a, c, b, color);
            }
        }

        /// <summary>
        /// Дуга троса (rel-graph.js:401-405): квадратичная кривая с провисом вниз. Мод качает провис и сдвиг
        /// по кадровому времени (sag/sway от elapsedT); порт считает ту же геометрию без времени, потому что
        /// раскладка порта детерминированная — один и тот же снапшот обязан давать одну картинку.
        /// </summary>
        private static void Curve(Vector2 a, Vector2 b, out Vector2 c, out Vector2 normal, out float len)
        {
            float dx = b.x - a.x;
            float dy = b.y - a.y;
            len = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
            float sag = Mathf.Min(28f, len * 0.12f);   // rel-graph.js:401
            c = new Vector2((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f + sag);
            normal = new Vector2(-dy / len, dx / len);
        }

        private static Vector2 Point(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return new Vector2(u * u * a.x + 2f * u * t * c.x + t * t * b.x,
                               u * u * a.y + 2f * u * t * c.y + t * t * b.y);
        }

        private static void StrokeQuad(Painter2D p, Vector2 a, Vector2 c, Vector2 b, Vector2 offset)
        {
            p.BeginPath();
            p.MoveTo(a + offset);
            p.QuadraticCurveTo(c + offset, b + offset);
            p.Stroke();
        }

        private static void Ragged(Painter2D p, Vector2 a, Vector2 c, Vector2 b, Vector2 normal, float len)
        {
            int n = Mathf.Max(8, Mathf.RoundToInt(len / 9f));
            p.BeginPath();
            for (int j = 0; j <= n; j++)
            {
                var at = Point(a, c, b, (float)j / n);
                float o = j == 0 || j == n
                    ? 0f
                    : (j % 2 == 1 ? 2.6f : -2.6f) * (j % 3 == 0 ? 0.4f : 1f);
                var pt = at + normal * o;
                if (j == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.Stroke();
        }

        private static void Arrows(Painter2D p, Vector2 a, Vector2 c, Vector2 b, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            for (int k = 0; k < 2; k++)
            {
                float t = k == 0 ? 0.34f : 0.66f;   // rel-graph.js:427
                var at = Point(a, c, b, t);
                var next = Point(a, c, b, t + 0.02f);
                float ang = Mathf.Atan2(next.y - at.y, next.x - at.x);
                const float size = 5f;              // rel-graph.js:431
                var tip = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * size;
                var left = at + new Vector2(Mathf.Cos(ang + 2.5f), Mathf.Sin(ang + 2.5f)) * size;
                var right = at + new Vector2(Mathf.Cos(ang - 2.5f), Mathf.Sin(ang - 2.5f)) * size;
                p.MoveTo(tip);
                p.LineTo(left);
                p.LineTo(right);
                p.ClosePath();
            }
            p.Fill();
        }

        /// <summary>Толщина троса по типу (rel-graph.js:352-357): в моде она — часть стиля ребра.</summary>
        private static float StrokeWidth(CnGraphEdge e)
        {
            switch (e.type)
            {
                case 1: return 1.2f;
                case 2: return 1.2f;
                case 3: return e.negative ? 1.4f : 1.1f;
                case 4: return 1f;
                case 5: return 1.4f;
                case 6: return 1.1f;
                default: return 1.2f;
            }
        }

        /// <summary>Цвет ребра по типу мода; вражда и минус в награде — красным, союз — зелёным.</summary>
        private static Color EdgeColor(CnGraphEdge e)
        {
            switch (e.type)
            {
                case 1: return CnStyle.Accent;
                case 2: return e.negative ? CnStyle.DangerInk : CnStyle.Warn;
                case 3: return e.negative ? CnStyle.DangerRust : CnStyle.GasAccent;
                case 4: return CnStyle.Muted;
                case 5: return e.negative ? CnStyle.DangerRust : CnStyle.GasAccent;
                case 6: return CnStyle.Brass;
                default: return CnStyle.Dim;
            }
        }

        private static string EdgeName(byte type) => type < EdgeNames.Length ? EdgeNames[type] : EdgeNames[0];

        /// <summary>
        /// Текст подписи-глагола (rel-graph.js:380): у мода полноя подпись — это verb, а если label от него
        /// отличается, то «verb · label». В порте label уже несёт значение из ассета, а verb — общий глагол типа,
        /// поэтому повтор слова снимается, как это делает мод для награды.
        /// </summary>
        private static string VerbText(CnGraphEdge e)
        {
            string verb = EdgeName(e.type);
            string label = e.label;
            if (string.IsNullOrEmpty(label) || label == verb) return verb;

            // Награда: «награда +12» → «награда · +12» (rel-graph.js:380).
            const string reward = "награда ";
            if (e.type == 5 && label.StartsWith(reward, StringComparison.Ordinal))
                return verb + " · " + label.Substring(reward.Length);

            // Требование уже начинается с глагола: «требует задание».
            if (label.StartsWith(verb, StringComparison.Ordinal)) return label;

            // У типов 3 и 4 мод ставит на ребро один из двух глаголов: «союз», «выдаёт», «принимает».
            if (verb.IndexOf(label, StringComparison.Ordinal) >= 0) return label;

            return verb + " · " + label;
        }

        // ------------------------------------------------------------------ подписи-глаголы

        /// <summary>
        /// Подписи-глаголы на тросах (rel-graph.js:447-502): место ищется у опорных точек дуги по списку
        /// смещений, а занятыми считаются габариты узлов и уже поставленные подписи. Не влезла целиком —
        /// показывается один глагол; не влез и он — подпись прячется, подробность остаётся в разборе.
        /// </summary>
        private void PlaceLabels()
        {
            EnsureVerbs();
            if (_verbViews.Count == 0) return;

            float w = _canvas.layout.width;
            float h = _canvas.layout.height;
            if (float.IsNaN(w) || w < 120f) w = 640f;
            if (float.IsNaN(h) || h < 120f) h = 420f;

            var boxes = new List<Rect>(_nodes.Length + _edgeList.Length);
            for (int i = 0; i < _nodes.Length; i++)
            {
                string key = _nodes[i].key;
                if (!_center.TryGetValue(key, out var at)) continue;
                float nw = _nodeWidth.TryGetValue(key, out var storedW) ? storedW : NodeW;
                float nh = _nodeHeight.TryGetValue(key, out var storedH) ? storedH : NodeH;
                boxes.Add(new Rect(at.x - nw * 0.5f, at.y - nh * 0.5f, nw, nh));
            }

            // Связи фокуса получают место первыми (rel-graph.js:455-456), остальные — в порядке данных.
            // Двумя проходами, а не List.Sort: порядок обязан быть одинаковым на одном и том же снапшоте.
            bool focused = !string.IsNullOrEmpty(_selected);
            var order = new List<int>(_edgeList.Length);
            for (int i = 0; i < _edgeList.Length; i++) if (!focused || Touches(i)) order.Add(i);
            for (int i = 0; i < _edgeList.Length; i++) if (focused && !Touches(i)) order.Add(i);

            for (int v = 0; v < order.Count; v++)
            {
                int i = order[v];
                var lb = _verbViews[i];

                // При фокусе подписи чужих связей гаснут вместе с тросом (rel-css .rp-e.dim).
                if (focused && !Touches(i))
                {
                    lb.style.display = DisplayStyle.None;
                    continue;
                }

                var e = _edgeList[i];
                if (!_center.TryGetValue(e.from, out var a) || !_center.TryGetValue(e.to, out var b))
                {
                    lb.style.display = DisplayStyle.None;
                    continue;
                }

                Curve(a, b, out var c, out _, out _);

                // Опорные точки по всей дуге: короткой связи в плотном клубке иначе негде подписать (rel-graph.js:443).
                var anchors = new[]
                {
                    Point(a, c, b, 0.5f), Point(a, c, b, 0.34f), Point(a, c, b, 0.66f),
                    Point(a, c, b, 0.2f), Point(a, c, b, 0.8f)
                };

                string full = VerbText(e).ToUpperInvariant();
                string shortText = EdgeName(e.type).ToUpperInvariant();
                var fullSize = MeasureVerb(lb, full);
                var shortSize = full == shortText ? fullSize : MeasureVerb(lb, shortText);

                var box = Fit(anchors, fullSize, boxes, w, h);
                string text = full;
                if (box == null && shortSize.x < fullSize.x)
                {
                    box = Fit(anchors, shortSize, boxes, w, h);
                    text = shortText;
                }

                if (box == null)
                {
                    lb.style.display = DisplayStyle.None;
                    continue;
                }

                if (lb.text != text) lb.text = text;
                lb.style.display = DisplayStyle.Flex;
                lb.style.left = box.Value.x;
                lb.style.top = box.Value.y;
                boxes.Add(box.Value);
            }
        }

        /// <summary>Подписи создаются по числу рёбер и пересоздаются, когда меняется набор типов.</summary>
        private void EnsureVerbs()
        {
            var sb = new StringBuilder(64);
            for (int i = 0; i < _edgeList.Length; i++) sb.Append(_edgeList[i].type).Append(',');
            string signature = sb.ToString();
            if (signature == _verbsSignature) return;
            _verbsSignature = signature;

            _verbs.Clear();
            _verbViews.Clear();

            for (int i = 0; i < _edgeList.Length; i++)
            {
                var lb = CnStyle.Label(string.Empty, "cn-relg__verb");
                lb.pickingMode = PickingMode.Ignore;

                // Цвет подписи по типу (rel.css:244-245): награда — зелёная, требование — латунное.
                byte type = _edgeList[i].type;
                if (type == 5) lb.AddToClassList("is-t5");
                else if (type == 6) lb.AddToClassList("is-t6");

                lb.style.position = Position.Absolute;
                lb.style.display = DisplayStyle.None;
                _verbs.Add(lb);
                _verbViews.Add(lb);
            }
        }

        private bool Touches(int index)
        {
            var e = _edgeList[index];
            return e.from == _selected || e.to == _selected;
        }

        /// <summary>Смещения примерки (rel-graph.js:457) — те же семнадцать пар и в том же порядке.</summary>
        private static readonly Vector2Int[] LabelOffsets =
        {
            new Vector2Int(0, 0), new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(0, -2),
            new Vector2Int(0, 2), new Vector2Int(0, -3), new Vector2Int(0, 3), new Vector2Int(0, -4),
            new Vector2Int(0, 4), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(-1, -1),
            new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-2, 0),
            new Vector2Int(2, 0)
        };

        private static Rect? Fit(Vector2[] anchors, Vector2 size, List<Rect> taken, float w, float h)
        {
            for (int a = 0; a < anchors.Length; a++)
            {
                for (int o = 0; o < LabelOffsets.Length; o++)
                {
                    var off = LabelOffsets[o];
                    float x = Mathf.Round(anchors[a].x - size.x * 0.5f + off.x * (size.x * 0.6f));
                    float y = Mathf.Round(anchors[a].y - size.y * 0.5f + off.y * (size.y + 2f));
                    if (x < 2f || y < 2f || x + size.x > w - 2f || y + size.y > h - 2f) continue;

                    var box = new Rect(x, y, size.x, size.y);
                    bool clash = false;
                    for (int i = 0; i < taken.Count; i++)
                    {
                        var t = taken[i];
                        if (box.xMin < t.xMax && t.xMin < box.xMax && box.yMin < t.yMax && t.yMin < box.yMax)
                        {
                            clash = true;
                            break;
                        }
                    }

                    if (!clash) return box;
                }
            }

            return null;
        }

        /// <summary>Габарит подписи: текст плюс поля `.cn-relg__verb` (5 px по бокам, 1 px сверху/снизу).</summary>
        private static Vector2 MeasureVerb(Label label, string text)
        {
            var size = label.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            if (float.IsNaN(size.x) || size.x <= 0f || float.IsNaN(size.y) || size.y <= 0f)
                return new Vector2(text.Length * 5.6f + 10f, 14f);   // шрифт ещё не разрешён — оценка капса 9 px
            return new Vector2(size.x + 10f, size.y + 2f);
        }

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
                PlaceLabels();
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
            PlaceLabels();
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
            _cableLayer.generateVisualContent -= OnGenerateCables;
            for (int i = 0; i < _noteViews.Count; i++) _noteViews[i].RemoveFromHierarchy();
            _noteViews.Clear();
            _verbs.Clear();
            _verbViews.Clear();
            for (int i = 0; i < _nodeViews.Count; i++) _nodeViews[i].RemoveFromHierarchy();
            _nodeViews.Clear();
            _nodeKeys.Clear();
            _center.Clear();
            _nodeWidth.Clear();
            _nodeHeight.Clear();
        }
    }
}
