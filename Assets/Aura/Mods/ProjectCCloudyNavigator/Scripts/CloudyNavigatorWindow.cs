// Cloudy Navigator — корень окна. Шаг 2: пустое окно; шаг 3: окно владеет читающим слоем CnDataFeed.
// Дерево появится на шаге 4 по USS-контракту мода.
//
// Класс не знает ни о UIDocument, ни о панели: хост передаёт родителя в Mount и тикает Update(dt).
// Такой же приём, как у ProjectCMainMenuWindow — окно монтируется на любой VisualElement и не владеет панелью.
//
// Всё собственное время окна считается в Update(dt) от хоста: setTimeout/@keyframes в порте нет (04 §1).
using System.Text;
using ProjectCCloudyNavigator.Actions;
using ProjectCCloudyNavigator.Data;
using ProjectCCloudyNavigator.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator
{
    /// <summary>Дерево и поведение окна. Пока шаг 3 — корень, контракт жизненного цикла и данные.</summary>
    public sealed class CloudyNavigatorWindow
    {
        private VisualElement _root;
        private readonly CnDataFeed _feed;
        private readonly CnActions _actions;
        private readonly CnViewState _view;
        private readonly CnStationView _station;
        private readonly CnMainView _main;
        private readonly CnAltRailView _altrail;
        private readonly CnPopups _popups;
        private readonly CnMotion _motion;
        private readonly CnTip _tip;
        private readonly CnHover _hover;
        private CnPreviewStage _preview;
        private float _lastWidth = -1f;
        private bool _openPending;
        private float _openWait;

        // Последние посчитанные колонки — только для пробника Probe().
        private float _width;
        private float _homeW;
        private float _altW;
        private float _colW;

        /// <summary>Окно читает игру, но не пишет в неё: единственный владелец данных — CnDataFeed.</summary>
        public CnDataFeed Data => _feed;

        /// <summary>Действия окна: единственная точка вызова игровых Request*-методов (02 §7, шаг 7).</summary>
        public CnActions Actions => _actions;

        /// <summary>log — писать ли [CN]-снимок в консоль для сверки с легаси (только диагностика).</summary>
        public CloudyNavigatorWindow(bool log)
        {
            _feed = new CnDataFeed(log);
            _actions = new CnActions();
            _view = new CnViewState();

            // Два общих слоя порта мода: одна подсказка на всё окно и одна шина наведения.
            // Виды только зовут _tip.Hover(...) и _hover.Set* — сами ничего не рисуют поверх окна.
            _tip = new CnTip();
            _hover = new CnHover();
            _hover.Changed += OnHoverChanged;

            _popups = new CnPopups(_actions, _view, _tip, _hover);

            _station = new CnStationView(_tip, _hover);
            _main = new CnMainView(_actions, _popups, _view, _tip, _hover);
            _altrail = new CnAltRailView(_tip);
            _motion = new CnMotion();

            // Результат действия из попапа показывается строкой «СЕЙЧАС» главного окна — один канал отчёта.
            _popups.OnResult = _main.Report;

            // Слот навыка на станции открывает боевой граф, бирка — социальный.
            _station.OnOpenSkills = social => _popups.OpenSkills(social);

            // Прибор-календарь и блок репутации станции — входы в свои полные окна.
            _station.OnOpenCalendar = () => _popups.OpenCalendar();
            _station.OnOpenRel = () => _popups.OpenRel();
        }

        /// <summary>Корень окна. null до Mount.</summary>
        public VisualElement Root => _root;

        public void Mount(VisualElement parent)
        {
            if (parent == null) return;
            if (_root != null) return;

            _root = new VisualElement { name = "cn-root" };
            _root.AddToClassList("cn-root");
            CnStyle.ApplyFont(_root);

            // Вуаль окна: в моде за интерфейсом стоит облачная сцена, в игре под ним — мир. Подложка взята тёмной
            // (Veil), альфа проходит гамма-коррекцию (04 §2). Точное значение альфы — вопрос D17, проверить в Play Mode.
            _root.style.backgroundColor = CnStyle.Alpha(CnStyle.Veil, 0.90f, CnStyle.Veil);

            _root.Add(_station.Root);
            _root.Add(_main.Root);
            _root.Add(_altrail.Root);
            _popups.Mount(_root);      // попапы выше колонок, но ниже слоя движения
            _tip.Mount(_root);          // подсказка выше панелей (ui.js: .tip z-index 80), мышь не ловит
            _root.Add(_motion.Layer);   // слой движения — последним, поверх панелей

            // Превью живёт в сцене, поэтому только в Play Mode: в edit mode GameObject превью попал бы в сцену.
            if (Application.isPlaying)
            {
                _preview = new CnPreviewStage();
                var figure = _preview.Element;
                figure.style.width = Length.Percent(100f);
                figure.style.height = Length.Percent(100f);
                figure.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                _station.PreviewHost.Add(figure);
            }

            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            parent.Add(_root);
            ApplyLayout(parent.resolvedStyle.width);
        }

        /// <summary>Кадр окна. dt уже зажат хостом (≤ 0.1 с), чтобы паузы редактора не рвали анимации.</summary>
        public void Update(float dt)
        {
            float step = Mathf.Clamp(dt, 0f, 0.1f);

            // Показ: облако собирается по РЕАЛЬНЫМ прямоугольникам, поэтому ждём первого кадра с раскладкой
            // (но не дольше 0.2 с — иначе блок так и остался бы прозрачным).
            if (_openPending)
            {
                _openWait += step;
                if (_root.worldBound.width > 1f || _openWait > 0.2f)
                {
                    _openPending = false;
                    _motion.Open(_station.Root, 6, 0.9f);
                    _motion.Open(_main.Root, 8, 1f, 0.04f);
                    _motion.Open(_altrail.Root, 4, 0.7f, 0.08f);
                }
            }

            _feed.Tick(step);
            _actions.Tick(step);
            _station.Refresh(_feed);
            _main.Refresh(_feed, step);
            _altrail.Refresh(_feed);
            _popups.Refresh(_feed, step);
            _motion.Tick(step);
            _tip.Tick(step);            // задержка 140 мс — из кадра окна, своих таймеров нет

            // Превью двигается только пока окно открыто: скрытая камера ничего не рендерит (шаг 6).
            if (_preview != null) _preview.Tick(step);
        }

        /// <summary>Слой движения окна: визуальный, состояние игры не читает и не меняет.</summary>
        public CnMotion Motion => _motion;

        /// <summary>Показ: блоки сразу прозрачны, облако собирается, как только посчитана раскладка.</summary>
        public void RequestOpen()
        {
            if (_root == null) return;

            _station.Root.style.opacity = 0f;
            _main.Root.style.opacity = 0f;
            _altrail.Root.style.opacity = 0f;
            _openPending = true;
            _openWait = 0f;
            if (_preview != null) _preview.SetVisible(true);
        }

        /// <summary>
        /// Уход: рассеивание стартует, пока раскладка ещё живая; корень прячет хост по <see cref="CnMotion.Busy"/>.
        /// </summary>
        public void RequestClose()
        {
            if (_root == null) return;

            _openPending = false;
            if (_preview != null) _preview.SetVisible(false);
            _hover.Clear();
            _tip.Hide();
            _popups.Close();
            _motion.Close(_station.Root, 6, 0.9f);
            _motion.Close(_main.Root, 8, 1f);
            _motion.Close(_altrail.Root, 4, 0.7f);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => ApplyLayout(evt.newRect.width);

        /// <summary>
        /// main.js applyHover(): единственное место, где субъект наведения превращается в подсветку видов.
        /// Линия глушит соседние строки, слот/характеристика подсвечивают клетку станции, предмет — линии, которым
        /// он нужен. Связей, которых нет в данных игры, шина не рисует.
        /// </summary>
        private void OnHoverChanged(CnHoverSubject subject)
        {
            switch (subject.kind)
            {
                case CnHoverKind.Line:
                    _station.SetHighlight(-1, null);
                    _main.HighlightLine(subject.id);
                    break;

                case CnHoverKind.Slot:
                    _main.HighlightNone();
                    _station.SetHighlight(subject.slot, null);
                    break;

                case CnHoverKind.Stat:
                    _main.HighlightNone();
                    _station.SetHighlight(-1, subject.id);
                    break;

                case CnHoverKind.Item:
                    _station.SetHighlight(-1, null);
                    _main.HighlightItem(subject.id);
                    break;

                default:
                    _station.SetHighlight(-1, null);
                    _main.HighlightNone();
                    break;
            }
        }

        /// <summary>
        /// Колонки мода (src/main.js layout()): homeW = 152 / 164 / clamp(0.17·W, 186…226),
        /// altW = 36 / 34 / 32 / 30, colW = W − homeW − altW; narrow при colW &lt; 720, dock-narrow при colW &lt; 660.
        /// Ширина станции и шкалы задаётся здесь, поэтому док не может их сдвинуть.
        /// </summary>
        private void ApplyLayout(float width)
        {
            if (width <= 0f || Mathf.Approximately(width, _lastWidth)) return;
            _lastWidth = width;

            float homeW = width < 760f ? 152f : width < 920f ? 164f : Mathf.Clamp(width * 0.17f, 186f, 226f);
            homeW = Mathf.Round(homeW);
            float altW = width >= 1200f ? 36f : width >= 920f ? 34f : width >= 760f ? 32f : 30f;
            float colW = width - homeW - altW;

            _width = width;
            _homeW = homeW;
            _altW = altW;
            _colW = colW;

            _station.Root.style.width = homeW;
            _altrail.SetWidth(altW);

            _root.EnableInClassList("is-narrow", colW < 720f);
            _root.EnableInClassList("is-dock-narrow", colW < 660f);
            _main.SetNarrow(colW < 720f);
        }

        /// <summary>
        /// Пробник раскладки: числа окна и дока одной строкой key=value — те же величины, что печатает dockProbe()
        /// мода (main.js:805-810). Нужен, чтобы сравнивать порт с модом числами, а не на глаз: rowsH —
        /// фактический замер, rowsEst — старая арифметическая оценка (по ней видно дрейф).
        /// Из editor-скрипта вызывается без Play Mode.
        /// </summary>
        public string Probe()
        {
            if (_root == null) return "cn-probe mounted=0";

            var sb = new StringBuilder(256);
            sb.Append("cn-probe mounted=1");
            sb.Append(" W=").Append(Mathf.RoundToInt(_width));
            sb.Append(" H=").Append(Mathf.RoundToInt(_root.layout.height));
            sb.Append(" homeW=").Append(Mathf.RoundToInt(_homeW));
            sb.Append(" altW=").Append(Mathf.RoundToInt(_altW));
            sb.Append(" colW=").Append(Mathf.RoundToInt(_colW));
            sb.Append(" narrow=").Append(_colW < 720f ? 1 : 0);
            sb.Append(" dockNarrow=").Append(_colW < 660f ? 1 : 0);
            sb.Append(" header=").Append(Mathf.RoundToInt(_main.HeaderHeight));
            sb.Append(" rows=").Append(_main.DockRows);
            sb.Append(" rowsH=").Append(Mathf.RoundToInt(_main.RowsMeasured));
            sb.Append(" rowsEst=").Append(Mathf.RoundToInt(_main.RowsEstimated));
            sb.Append(" free=").Append(Mathf.RoundToInt(_main.DockFree));
            sb.Append(" cap=").Append(Mathf.RoundToInt(_main.DockCap));
            sb.Append(" want=").Append(Mathf.RoundToInt(_main.DockTarget));
            sb.Append(" dock=").Append(Mathf.RoundToInt(_main.DockHeight));
            return sb.ToString();
        }

        public void Dispose()
        {
            _motion.Reset();
            _popups.Dispose();
            _tip.Dispose();
            _hover.Dispose();

            if (_preview != null)
            {
                _preview.Dispose();
                _preview = null;
            }

            if (_root != null)
            {
                _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                _root.RemoveFromHierarchy();
            }

            _root = null;
        }
    }
}
