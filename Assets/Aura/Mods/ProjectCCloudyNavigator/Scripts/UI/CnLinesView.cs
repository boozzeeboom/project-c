// Cloudy Navigator — лента задач по src/lines.js мода: «канатные линии».
//
// Каждая задача — строка 108 px: сверху заголовок с цветным кантом, ниже трос с провисом (Painter2D вместо SVG),
// на тросе — станции-шаги (пройдена / текущая / следующая / дальше), каретка на текущей станции, бирка действия
// прямо на тросе справа от текущей станции и терминал с наградой у правого края. Клик по станции открывает под
// строкой карточку шага («что нужно / что есть / откуда / что даст») с вторичными действиями.
//
// Геометрия — функция geometry() мода: ширина терминала по тексту награды, сжатые пройденные станции (92 → 44 px),
// зазор под бирку действия, остаток места раздаётся будущим станциям. Ширину текста оценивает CnStyle.TextWidth.
//
// Вид не знает игры: модели строк (CnLineModel) собирает CnMainView из CnDataFeed, действия уходят в CnActions через
// делегаты модели. Время — только Tick(dt): провис троса «дышит» как в моде (sagOf), своих таймеров нет.
using System;
using System.Collections.Generic;
using ProjectCCloudyNavigator.Actions;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Шаг линии: подпись и подстрока станции + четыре поля карточки.</summary>
    public struct CnStep
    {
        public string label;
        public string sub;
        public bool done;
        public string need;
        public string have;
        public string from;
        public string gives;
    }

    /// <summary>Действие линии: бирка «глагол · значение». disabled — причина, по которой нажать нельзя.</summary>
    public sealed class CnLineAction
    {
        public string verb;
        public string value;
        public string modifier;
        public string disabled;
        public Func<CnActionResult> run;
    }

    /// <summary>Модель строки ленты. Собирается из реальных данных игры (CnMainView), виду не важно откуда.</summary>
    public sealed class CnLineModel
    {
        public string id;
        public string title;
        public string tag;
        public Color color;
        public bool tracked;
        public List<CnStep> steps = new List<CnStep>();
        public int cur;
        public CnLineAction action;
        public string info;
        public List<CnLineAction> extra = new List<CnLineAction>();
        public string rewardBig;
        public string rewardSmall;
        public bool alarm;
        /// <summary>Имя предмета линии (контракт — его груз): связь «линия ↔ предмет» для наведения.</summary>
        public string itemName;
        public string signature;
    }

    /// <summary>Лента канатных линий.</summary>
    public sealed class CnLinesView
    {
        // lines.js: const G = { titleH: 24, labelTop: 26, ropeY: 74, rowH: 108 }
        public const float RowH = 108f;
        private const float LabelTop = 26f;
        private const float RopeY = 74f;

        // quests.js: панель деталей по наведению — открытие 0.14 с, высота зависит от числа целей.
        private const float HoverDelay = 0.14f;
        private const float HoverGrace = 0.12f;   // перенос курсора на панель не закрывает её

        private static readonly Color RopeMid = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#6b786e"); // mixHex(veilEdge, inkDim, .35)

        private readonly ScrollView _scroll;
        private readonly VisualElement _rows;
        private readonly List<Row> _built = new List<Row>();
        private readonly Action<CnActionResult> _onResult;
        private readonly Action<CnLineModel> _onOpen;

        // Панель наведения: строка, на которой стоит курсор, таймер открытия и сам элемент под строкой.
        private string _hoverId;
        private float _hoverTimer;
        private float _hoverLeave;
        private VisualElement _hoverEl;
        private float _hoverHeight;

        private List<CnLineModel> _models = new List<CnLineModel>();
        private HashSet<string> _highlight;
        private readonly CnHover _hover;
        private string _signature = string.Empty;
        private float _rowW = -1f;
        private string _cardLine;
        private int _cardStep = -1;
        private float _elapsed;
        private bool _narrow;

        private sealed class Row
        {
            public CnLineModel line;
            public VisualElement el;
            public VisualElement rope;
            public float[] xs;
            public float termX;
            public int idx;
        }

        public VisualElement Root => _scroll;

        /// <summary>
        /// ОЦЕНКА высоты содержимого: арифметика самого вида (content + 10) плюс прикидка карточки наведения.
        /// Оставлена только как контрольное число в пробнике — раскладку по ней считать нельзя, она не видит
        /// ни переноса длинного заголовка, ни скроллбара, ни изменения высоты строки не через Render().
        /// </summary>
        public float ContentHeight { get; private set; }

        /// <summary>
        /// ФАКТИЧЕСКАЯ высота содержимого ленты — аналог rowsEl.scrollHeight мода (main.js:202). Меряет движок
        /// раскладки, поэтому сюда попадает всё: перенос подписи, карточка наведения, появившийся скроллбар.
        /// </summary>
        public float MeasuredHeight => _rows != null ? _rows.layout.height : 0f;

        /// <summary>Сколько строк сейчас в ленте (мод: #rows &gt; .ln:not(.leaving)).</summary>
        public int RowCount => _built != null ? _built.Count : 0;

        public CnLinesView(Action<CnActionResult> onResult, Action<CnLineModel> onOpen, CnHover hover)
        {
            _onResult = onResult;
            _onOpen = onOpen;
            _hover = hover;

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("cn-lines");
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;

            _rows = CnStyle.Box("cn-rows");
            _scroll.Add(_rows);
            _scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => Rebuild(false));
        }

        public void SetNarrow(bool narrow)
        {
            if (_narrow == narrow) return;
            _narrow = narrow;
            _rows.EnableInClassList("is-narrow", narrow);
            _signature = string.Empty;
            Rebuild(true);
        }

        /// <summary>Новые модели строк. Пересборка — только когда изменилась сигнатура данных или ширина ленты.</summary>
        public void Render(List<CnLineModel> models)
        {
            var sb = new System.Text.StringBuilder(256);
            for (int i = 0; i < models.Count; i++) sb.Append(models[i].signature).Append('\n');
            string signature = sb.ToString();

            _models = models;
            if (signature == _signature) return;
            _signature = signature;

            if (_cardLine != null && models.Find(m => m.id == _cardLine) == null)
            {
                _cardLine = null;
                _cardStep = -1;
            }

            Rebuild(true);
        }

        /// <summary>
        /// Кадр: провис тросов «дышит» (sagOf мода) и открывается панель наведения — это единственное время окна,
        /// своих setTimeout в порте нет.
        /// </summary>
        public void Tick(float dt)
        {
            _elapsed += dt;

            if (_hoverId != null && _hoverEl == null)
            {
                _hoverTimer += dt;
                if (_hoverTimer >= HoverDelay) ShowHover(_hoverId);
            }

            if (_hoverLeave > 0f)
            {
                _hoverLeave -= dt;
                if (_hoverLeave <= 0f)
                {
                    _hoverId = null;
                    HideHover();
                }
            }

            for (int i = 0; i < _built.Count; i++) _built[i].rope.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ пересборка

        private void Rebuild(bool force)
        {
            float width = _scroll.contentViewport.layout.width;
            if (float.IsNaN(width) || width <= 0f) width = 0f;
            float rowW = Mathf.Max(300f, Mathf.Floor(width) - 6f);   // lines.js: max(300, clientWidth), паддинг справа 6
            if (!force && Mathf.Abs(rowW - _rowW) < 1f) return;
            _rowW = rowW;

            _rows.Clear();
            _built.Clear();
            HideHover();
            _hoverId = null;
            _hoverTimer = 0f;

            float content = 0f;
            if (_models.Count == 0)
            {
                var empty = CnStyle.Box("cn-lines__empty");
                empty.Add(CnStyle.Label("ЗАДАЧ НЕТ", "cn-lines__empty-t"));
                empty.Add(CnStyle.Label("квесты берутся в диалоге с NPC, контракты — на рынке", "cn-lines__empty-s"));
                _rows.Add(empty);
                ContentHeight = 64f;
                _rows.EnableInClassList("is-hlmode", false);
                return;
            }

            for (int i = 0; i < _models.Count; i++)
            {
                var row = BuildRow(_models[i], i, rowW);
                _rows.Add(row.el);
                _built.Add(row);
                content += RowH;

                if (_cardLine == _models[i].id && _cardStep >= 0 && _cardStep < _models[i].steps.Count)
                {
                    var card = BuildCard(_models[i], _cardStep, row);
                    _rows.Add(card);
                    content += 150f;   // оценка до раскладки; док пересчитается на следующем кадре
                }
            }

            ContentHeight = content + 10f;
            ApplyHighlight();
        }

        /// <summary>
        /// Подсветка из шины наведения (lines.js setHighlight): .is-hlmode на контейнере и .is-hl/.is-dim на строках.
        /// USS :not() не поддерживает, поэтому приглушение соседей — отдельным классом, а не селектором.
        /// </summary>
        public void SetHighlight(ICollection<string> ids)
        {
            _highlight = ids == null || ids.Count == 0 ? null : new HashSet<string>(ids);
            ApplyHighlight();
        }

        private void ApplyHighlight()
        {
            bool mode = _highlight != null && _highlight.Count > 0;
            _rows.EnableInClassList("is-hlmode", mode);

            for (int i = 0; i < _built.Count; i++)
            {
                var row = _built[i];
                if (row.el == null) continue;

                bool on = mode && row.line != null && _highlight.Contains(row.line.id);
                row.el.EnableInClassList("is-hl", on);
                row.el.EnableInClassList("is-dim", mode && !on);

                // Фон строки ставится кодом, поэтому класс его не заменит: подсвеченная строка заливается заметнее.
                CnStyle.Fill(row.el, CnStyle.Veil, on ? 0.16f : 0.42f);
            }
        }

        // ------------------------------------------------------------------ геометрия (lines.js geometry)

        private struct Geo
        {
            public float termW;
            public float termX;
            public float[] xs;
            public float[] ws;
            public float btnMax;
        }

        private static float LabelW(CnStep st)
        {
            return Mathf.Max(CnStyle.TextWidth(st.label, 13f, true), CnStyle.TextWidth(st.sub, 12f, false)) + 14f;
        }

        private static Geo Geometry(CnLineModel line, float rowW)
        {
            int n = line.steps.Count;
            int cur = line.cur;
            bool hasCur = cur < n;

            float termW = Mathf.Clamp(Mathf.Round(Mathf.Max(CnStyle.TextWidth(line.rewardBig, 15f, true),
                CnStyle.TextWidth(line.rewardSmall, 12f, false)) + 34f), 110f, 240f);
            float termX = rowW - termW + 16f;
            float r = rowW - termW - 6f;

            var a = hasCur ? line.action : null;
            int k = hasCur ? n - cur - 1 : 0;
            float reserve = k > 0 ? 40f : 20f;
            float btn = a != null ? CnStyle.TagWidth(a.verb, a.value) : 0f;
            if (a == null && hasCur && !string.IsNullOrEmpty(line.info)) btn = CnStyle.TextWidth(line.info, 12.5f, false) + 30f;

            float dw = 92f;
            float curW = hasCur ? Mathf.Max(100f, LabelW(line.steps[cur])) : 0f;
            float futW = 100f;
            for (int i = cur + 1; i < n; i++) futW = Mathf.Max(futW, LabelW(line.steps[i]));
            float t = futW;
            float tf = t;

            float xc = 0f, gap = 0f, end = 0f;
            void Lay()
            {
                xc = 6f + Mathf.Min(cur, n) * dw + (hasCur ? curW / 2f : 0f);
                gap = t;
                if (btn > 0f) gap = Mathf.Max(gap, 20f + btn + reserve);
                if (!hasCur) end = 6f + n * dw;
                else if (k > 0) end = xc + gap + (k - 1) * tf + (k > 1 ? tf : t) / 2f;
                else end = xc + (btn > 0f ? 20f + btn + reserve : curW / 2f) - (termX - r);
            }

            Lay();
            if (end > r) { dw = 44f; Lay(); }
            if (end > r && k > 1) { tf = 44f; Lay(); }

            if (end > r && hasCur && k > 0)
            {
                gap = Mathf.Max(Mathf.Max(t, curW) / 2f + t / 2f, r - xc - (k - 1) * tf - (k > 1 ? tf : t) / 2f);
            }
            else if (end < r && k > 0)
            {
                float e = Mathf.Min((r - end) / k, 220f);
                gap += e;
                tf += e;
            }

            var xs = new float[n];
            var ws = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!hasCur || i < cur) xs[i] = 6f + i * dw + dw / 2f;
                else if (i == cur) xs[i] = xc;
                else xs[i] = xc + gap + (i - cur - 1) * tf;

                ws[i] = !hasCur || i < cur ? dw
                    : i == cur ? curW
                    : i == cur + 1 ? Mathf.Max(futW, Mathf.Min(t, 180f))
                    : tf < futW ? 44f : Mathf.Min(tf, 180f);
            }

            float nextX = hasCur && k > 0 ? xs[cur + 1] : termX;
            float btnMax = hasCur ? Mathf.Max(60f, nextX - reserve - (xc + 20f)) : 0f;

            return new Geo { termW = termW, termX = termX, xs = xs, ws = ws, btnMax = btnMax };
        }

        // ------------------------------------------------------------------ строка

        private Row BuildRow(CnLineModel line, int idx, float rowW)
        {
            var g = Geometry(line, rowW);
            var row = new Row { line = line, idx = idx, xs = g.xs, termX = g.termX };

            var el = CnStyle.Box("cn-ln");
            if (idx == 0) el.AddToClassList("is-first");
            CnStyle.Fill(el, CnStyle.Veil, 0.42f);
            el.style.width = rowW;
            row.el = el;

            // Наведение (quests.js): панель деталей открывается под строкой и сдвигает соседей вниз.
            // Одновременно строка становится субъектом шины наведения (main.js setHover) — она глушит соседей.
            string lineId = line.id;
            el.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _hover?.SetLine(lineId);

                if (_hoverId == lineId && _hoverEl != null) return;
                _hoverId = lineId;
                _hoverTimer = 0f;
                _hoverLeave = 0f;
                HideHover();
            });
            el.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _hover?.Clear();

                if (_hoverId != lineId) return;
                _hoverLeave = HoverGrace;
            });

            // Клик по телу строки открывает полноразмерное окно заданий на нужной вкладке и с этой строкой.
            el.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                _onOpen?.Invoke(line);
            });

            if (idx > 0) el.Add(CnStyle.Deco("cn-ln__hair"));

            // ---- трос (рисуется первым — станции и бирки поверх)
            var rope = CnStyle.Deco("cn-ln__rope");
            rope.generateVisualContent += ctx => DrawRope(ctx, row);
            el.Add(rope);
            row.rope = rope;

            // ---- заголовок
            var title = CnStyle.Row("cn-ln__title");
            title.style.borderLeftColor = line.color;
            title.Add(CnStyle.Label(line.title, "cn-ln__name"));
            title.Add(CnStyle.Label(line.tag, "cn-ln__tag"));
            if (line.tracked) title.Add(CnStyle.Label("СЛЕДУЮ", "cn-ln__follow"));
            el.Add(title);

            // ---- станции
            for (int i = 0; i < line.steps.Count; i++)
            {
                var st = line.steps[i];
                string state = i < line.cur ? "done" : i == line.cur ? "cur" : i == line.cur + 1 ? "next" : "todo";
                float w = g.ws[i] - 4f;
                bool open = _cardLine == line.id && _cardStep == i;

                var b = CnStyle.Box("cn-st");
                b.AddToClassList("cn-st--" + state);
                if (open) b.AddToClassList("cn-st--open");
                b.style.left = Mathf.Round(g.xs[i] - w / 2f);
                b.style.top = LabelTop;
                b.style.width = Mathf.Round(w);
                b.style.height = RopeY + 17f - LabelTop;
                b.tooltip = st.label + " · " + st.sub;

                if (g.ws[i] > 60f)
                {
                    b.Add(CnStyle.Label(st.label, "cn-st__name"));
                    b.Add(CnStyle.Label(st.sub, "cn-st__sub"));
                }

                var node = CnStyle.Box("cn-st__node");
                node.style.left = Mathf.Round(w / 2f) - 16f;
                node.style.top = RopeY - LabelTop - 16f;
                node.pickingMode = PickingMode.Ignore;
                ApplyNodeState(node, state);
                node.Add(CnStyle.Label(state == "done" ? "✓" : (i + 1).ToString(), "cn-st__glyph"));
                if (open) node.Add(CnStyle.Deco("cn-st__open"));
                b.Add(node);

                int step = i;
                b.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    ToggleCard(line.id, step);
                });
                el.Add(b);
            }

            // ---- терминал награды
            var endBox = CnStyle.Box("cn-end");
            if (line.alarm) endBox.AddToClassList("cn-end--alarm");
            endBox.style.left = g.termX - 14f;
            endBox.style.top = LabelTop;
            endBox.style.width = rowW - g.termX + 10f;
            endBox.Add(CnStyle.Label(line.rewardBig, "cn-end__big"));
            endBox.Add(CnStyle.Label(line.rewardSmall, "cn-end__small"));
            el.Add(endBox);

            var endNode = CnStyle.Deco("cn-end__node");
            endNode.style.left = g.termX - 13f;
            endNode.style.top = RopeY - 13f;
            endNode.style.borderTopColor = line.color;
            endNode.style.borderBottomColor = line.color;
            endNode.style.borderLeftColor = line.color;
            endNode.style.borderRightColor = line.color;
            endNode.style.backgroundColor = CnStyle.VeilMid;
            var endFill = CnStyle.Deco("cn-end__fill");
            endFill.style.backgroundColor = CnStyle.A(line.color, 0.55f);
            endNode.Add(endFill);
            el.Add(endNode);

            // ---- каретка на текущей станции
            float carX = line.cur < line.steps.Count ? g.xs[line.cur] : g.termX;
            var pulley = CnStyle.Deco("cn-car__pulley");
            pulley.style.left = Mathf.Round(carX) - 3f;
            pulley.style.top = RopeY - 3f;
            el.Add(pulley);
            var car = CnStyle.Deco("cn-car");
            car.style.left = Mathf.Round(carX) - 8f;
            car.style.top = RopeY + 4f;
            car.style.borderTopColor = line.color;
            car.style.borderBottomColor = line.color;
            car.style.borderLeftColor = line.color;
            car.style.borderRightColor = line.color;
            CnStyle.Fill(car, CnStyle.VeilMid, 0.95f);
            el.Add(car);

            // ---- бирка действия на тросе (или информационная подпись)
            if (line.cur < line.steps.Count)
            {
                float left = Mathf.Round(g.xs[line.cur] + 20f);
                if (line.action != null)
                {
                    var a = line.action;
                    float tilt = ((line.id.Length % 2 == 1) ? 1f : -1f) * (2.1f + (line.id[0] % 3) * 0.3f);
                    var tag = MakeActionTag(a, tilt);
                    tag.AddToClassList("cn-ln__act");
                    tag.style.left = left;
                    tag.style.top = RopeY - 16f;
                    tag.style.maxWidth = Mathf.Round(g.btnMax);
                    el.Add(tag);
                }
                else if (!string.IsNullOrEmpty(line.info))
                {
                    var info = CnStyle.Label("›  " + line.info, "cn-ln__info");
                    info.style.left = left;
                    info.style.top = RopeY - 8f;
                    info.style.maxWidth = Mathf.Round(g.btnMax);
                    el.Add(info);
                }
            }

            return row;
        }

        private static void ApplyNodeState(VisualElement node, string state)
        {
            Color border, fill;
            switch (state)
            {
                case "done": border = CnStyle.VeilEdge; fill = CnStyle.VeilHi; break;
                case "cur": border = CnStyle.Accent; fill = CnStyle.GasMid; break;
                case "next": border = CnStyle.Ink; fill = CnStyle.VeilMid; break;
                default: border = CnStyle.VeilEdge; fill = CnStyle.Veil; break;
            }

            float bw = state == "cur" || state == "next" ? 2f : 1.5f;
            node.style.borderTopWidth = bw;
            node.style.borderBottomWidth = bw;
            node.style.borderLeftWidth = bw;
            node.style.borderRightWidth = bw;
            node.style.borderTopColor = border;
            node.style.borderBottomColor = border;
            node.style.borderLeftColor = border;
            node.style.borderRightColor = border;
            node.style.backgroundColor = fill;
        }

        private VisualElement MakeActionTag(CnLineAction a, float tilt)
        {
            string modifier = a.modifier;
            var tag = CnStyle.Tag(a.verb, a.value, modifier, tilt);
            if (!string.IsNullOrEmpty(a.disabled))
            {
                tag.AddToClassList("cn-tag--off");
                tag.tooltip = a.disabled;
            }

            tag.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                if (!string.IsNullOrEmpty(a.disabled))
                {
                    _onResult?.Invoke(CnActionResult.Fail(a.disabled));
                    return;
                }

                if (a.run != null) _onResult?.Invoke(a.run());
            });
            return tag;
        }

        // ------------------------------------------------------------------ трос

        /// <summary>lines.js sagOf: провис до 10 px, «дыхание» ±20 %.</summary>
        private static float SagOf(float len, int k, int idx, float e)
        {
            return Mathf.Min(10f, Mathf.Abs(len) * 0.045f) * (1f + 0.2f * Mathf.Sin(e * 1.1f + k * 0.9f + idx * 1.7f));
        }

        private void DrawRope(MeshGenerationContext ctx, Row row)
        {
            var xs = row.xs;
            int n = xs.Length;
            var pts = new float[n + 2];
            pts[0] = 0f;
            for (int i = 0; i < n; i++) pts[i + 1] = xs[i];
            pts[n + 1] = row.termX;

            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            Stroke(p, pts, row.idx, pts.Length - 1, CnStyle.A(CnStyle.Veil, 0.8f), 4.5f);
            Stroke(p, pts, row.idx, pts.Length - 1, RopeMid, 2f);
            Stroke(p, pts, row.idx, pts.Length - 1, CnStyle.A(row.line.color, 0.9f), 2.4f);

            // Пройденная часть — латунная нить поверх (5-й путь SVG мода): от начала до каретки.
            int travelled = Mathf.Min(row.line.cur + 1, pts.Length - 1);
            if (travelled > 0) Stroke(p, pts, row.idx, travelled, CnStyle.A(CnStyle.Brass, 0.75f), 1f);
        }

        private void Stroke(Painter2D p, float[] pts, int idx, int segments, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(new Vector2(pts[0], RopeY));
            for (int k = 0; k < segments; k++)
            {
                float x0 = pts[k];
                float x1 = pts[k + 1];
                float sag = SagOf(x1 - x0, k, idx, _elapsed);
                p.QuadraticCurveTo(new Vector2((x0 + x1) * 0.5f, RopeY + 2f * sag), new Vector2(x1, RopeY));
            }
            p.Stroke();
        }

        // ------------------------------------------------------------------ панель наведения

        /// <summary>Вставляет панель деталей сразу под строкой: соседи сдвигаются вниз, как в моде.</summary>
        private void ShowHover(string lineId)
        {
            Row row = null;
            for (int i = 0; i < _built.Count; i++)
                if (_built[i].line != null && _built[i].line.id == lineId) { row = _built[i]; break; }

            if (row == null) { _hoverId = null; return; }

            var el = BuildHover(row.line);
            // 06 §3: .ln-card > .halo-cardsh — 26/16/36, rgba(0,0,0,.45). Карточка выезжает под строкой
            // и должна отделяться от соседней строки тенью, а не только рамкой.
            CnStyle.HaloShadow(el, 26, 16, 36, CnStyle.Shadow, 0.45f);
            int at = _rows.IndexOf(row.el);
            if (at < 0) { _hoverId = null; return; }

            _rows.Insert(at + 1, el);
            _hoverEl = el;
            row.el.AddToClassList("cn-ln--hover");

            int steps = Mathf.Min(row.line.steps.Count, 5);
            bool acts = row.line.action != null || row.line.extra.Count > 0 || !string.IsNullOrEmpty(row.line.info);
            _hoverHeight = 26f + steps * 21f + (acts ? 36f : 0f) + 12f;
            ContentHeight += _hoverHeight;

            el.RegisterCallback<PointerEnterEvent>(_ => _hoverLeave = 0f);
            el.RegisterCallback<PointerLeaveEvent>(_ => _hoverLeave = HoverGrace);
        }

        private void HideHover()
        {
            if (_hoverEl != null)
            {
                _hoverEl.RemoveFromHierarchy();
                _hoverEl = null;
                ContentHeight = Mathf.Max(0f, ContentHeight - _hoverHeight);
                _hoverHeight = 0f;
            }

            for (int i = 0; i < _built.Count; i++) _built[i].el?.RemoveFromClassList("cn-ln--hover");
        }

        /// <summary>Содержимое панели: заголовок, шаги-цели и бирки действий. Новых данных не читает.</summary>
        private VisualElement BuildHover(CnLineModel line)
        {
            var box = CnStyle.Box("cn-hov");
            box.style.width = Mathf.Max(200f, _rowW - 8f);
            box.style.borderLeftColor = line.color;
            CnStyle.Fill(box, CnStyle.VeilMid, 0.96f);

            var head = CnStyle.Row("cn-hov__head");
            head.Add(CnStyle.Label(line.title, "cn-hov__t"));
            head.Add(CnStyle.Label(line.tag, "cn-hov__tag"));
            head.Add(CnStyle.Box("cn-grow"));
            if (!string.IsNullOrEmpty(line.rewardBig))
            {
                string reward = string.IsNullOrEmpty(line.rewardSmall) ? line.rewardBig : line.rewardBig + " · " + line.rewardSmall;
                head.Add(CnStyle.Label(reward, "cn-hov__r"));
            }
            box.Add(head);

            for (int i = 0; i < line.steps.Count; i++)
            {
                var step = line.steps[i];
                var stepRow = CnStyle.Row("cn-hov__step");
                if (step.done) stepRow.AddToClassList("is-done");
                if (i == line.cur) stepRow.AddToClassList("is-cur");
                stepRow.Add(CnStyle.Label(step.done ? "☑" : i == line.cur ? "▸" : "☐", "cn-hov__g"));
                stepRow.Add(CnStyle.Label(step.label, "cn-hov__n"));
                stepRow.Add(CnStyle.Box("cn-grow"));
                stepRow.Add(CnStyle.Label(step.sub, "cn-hov__s"));
                box.Add(stepRow);
            }

            var acts = CnStyle.Row("cn-hov__acts");
            if (line.action != null) acts.Add(MakeActionTag(line.action, 0f));
            for (int i = 0; i < line.extra.Count; i++) acts.Add(MakeActionTag(line.extra[i], 0f));
            if (line.action == null && !string.IsNullOrEmpty(line.info)) acts.Add(CnStyle.Label(line.info, "cn-hov__info"));
            if (acts.childCount > 0) box.Add(acts);

            return box;
        }

        // ------------------------------------------------------------------ карточка шага

        private void ToggleCard(string lineId, int step)
        {
            if (_cardLine == lineId && _cardStep == step)
            {
                _cardLine = null;
                _cardStep = -1;
            }
            else
            {
                _cardLine = lineId;
                _cardStep = step;
            }

            Rebuild(true);
        }

        private VisualElement BuildCard(CnLineModel line, int i, Row row)
        {
            var st = line.steps[i];

            var card = CnStyle.Box("cn-card");
            card.style.backgroundColor = CnStyle.A(CnStyle.GasMid, 0.92f);
            card.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            // уголок карточки под станцией
            var arrow = CnStyle.Deco("cn-card__arrow");
            arrow.style.left = Mathf.Round(row.xs[i]) - 8f - 8f;
            card.Add(arrow);

            var head = CnStyle.Row("cn-card__head");
            head.Add(CnStyle.Label(st.label, "cn-card__b"));
            head.Add(CnStyle.Label(" · " + line.title, "cn-card__h"));
            card.Add(head);

            var grid = CnStyle.Row("cn-card__grid");
            grid.Add(Cell("ЧТО НУЖНО", st.need, null, true));
            grid.Add(Cell("ЧТО ЕСТЬ", st.have, null, false));
            grid.Add(Cell("ОТКУДА", st.from, null, false));
            grid.Add(Cell("ЧТО ДАСТ", st.gives, "cn-card__v--good", false));
            card.Add(grid);

            if (line.extra.Count > 0)
            {
                var acts = CnStyle.Row("cn-card__acts");
                for (int k = 0; k < line.extra.Count; k++)
                {
                    var tag = MakeActionTag(line.extra[k], 0f);
                    if (k == 0) tag.AddToClassList("is-first");
                    acts.Add(tag);
                }
                card.Add(acts);
            }

            return card;
        }

        private static VisualElement Cell(string key, string value, string tone, bool first)
        {
            var cell = CnStyle.Box("cn-card__cell");
            if (first) cell.AddToClassList("is-first");
            cell.Add(CnStyle.Label(key, "cn-card__k"));
            var v = CnStyle.Label(string.IsNullOrEmpty(value) ? "—" : value, "cn-card__v");
            if (!string.IsNullOrEmpty(tone)) v.AddToClassList(tone);
            cell.Add(v);
            return cell;
        }
    }
}
