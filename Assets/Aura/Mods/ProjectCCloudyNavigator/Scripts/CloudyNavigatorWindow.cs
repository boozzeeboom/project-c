// Cloudy Navigator — корень окна. Шаг 2: пустое окно; шаг 3: окно владеет читающим слоем CnDataFeed.
// Дерево появится на шаге 4 по USS-контракту мода.
//
// Класс не знает ни о UIDocument, ни о панели: хост передаёт родителя в Mount и тикает Update(dt).
// Такой же приём, как у ProjectCMainMenuWindow — окно монтируется на любой VisualElement и не владеет панелью.
//
// Всё собственное время окна считается в Update(dt) от хоста: setTimeout/@keyframes в порте нет (04 §1).
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
        private readonly CnStationView _station;
        private readonly CnMainView _main;
        private readonly CnAltRailView _altrail;
        private float _lastWidth = -1f;

        /// <summary>Окно читает игру, но не пишет в неё: единственный владелец данных — CnDataFeed.</summary>
        public CnDataFeed Data => _feed;

        /// <summary>log — писать ли [CN]-снимок в консоль для сверки с легаси (только диагностика).</summary>
        public CloudyNavigatorWindow(bool log)
        {
            _feed = new CnDataFeed(log);
            _station = new CnStationView();
            _main = new CnMainView();
            _altrail = new CnAltRailView();
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

            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            parent.Add(_root);
            ApplyLayout(parent.resolvedStyle.width);
        }

        /// <summary>Кадр окна. dt уже зажат хостом (≤ 0.1 с), чтобы паузы редактора не рвали анимации.</summary>
        public void Update(float dt)
        {
            // шаг 5+: поле дыма, movement-переходы
            _feed.Tick(dt);
            _station.Refresh(_feed);
            _main.Refresh(_feed);
            _altrail.Refresh(_feed);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => ApplyLayout(evt.newRect.width);

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

            _station.Root.style.width = homeW;
            _altrail.SetWidth(altW);

            _root.EnableInClassList("is-narrow", colW < 720f);
            _root.EnableInClassList("is-dock-narrow", colW < 660f);
        }

        public void Dispose()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                _root.RemoveFromHierarchy();
            }

            _root = null;
        }
    }
}
