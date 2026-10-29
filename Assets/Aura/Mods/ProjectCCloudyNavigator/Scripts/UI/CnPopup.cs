// Cloudy Navigator — оболочка полноразмерного попапа внутри окна (мод v4-2: полноэкранные окна заданий, корабля,
// багажа, календаря, связей и графа навыков).
//
// Почему своя оболочка, а не отдельная панель UIDocument: окно уже живёт в корне cn-root и владеет данными и
// действиями. Попап — ребёнок cn-root, поэтому он не заводит ни своего UIDocument, ни своего PanelSettings и не
// может перекрыть легаси-окна игры: он виден только пока открыто само окно.
//
// Как в моде: тёмный затемняющий слой во весь корень, окно в нём с волосяной рамкой, шапка с заголовком и крестиком,
// закрытие — по крестику и по клику в затемнение. Esc закрывает окно целиком (так его обрабатывает UIManager игры,
// ClosesTopPanel → Hide хоста); попап это не перехватывает, чтобы не спорить с игровым вводом.
using System;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Полноразмерный попап: затемнение, окно, шапка, тело. Содержимое даёт вызывающий.</summary>
    public sealed class CnPopup
    {
        private readonly VisualElement _root;
        private readonly VisualElement _win;
        private readonly VisualElement _body;
        private readonly Label _title;
        private readonly Label _sub;
        private readonly CnTip _tip;

        private Action _onClose;

        public bool IsOpen { get; private set; }

        /// <summary>Слой попапа — кладётся в cn-root последним, чтобы быть выше станции и шкалы.</summary>
        public VisualElement Root => _root;

        /// <summary>Контейнер содержимого: сюда панель кладёт своё дерево.</summary>
        public VisualElement Body => _body;

        public CnPopup(CnTip tip)
        {
            _tip = tip;
            _root = CnStyle.Box("cn-pop");
            _root.style.display = DisplayStyle.None;
            _root.pickingMode = PickingMode.Position;

            var scrim = CnStyle.Deco("cn-pop__scrim");
            scrim.pickingMode = PickingMode.Position;
            CnStyle.Fill(scrim, CnStyle.Veil, 0.80f);
            scrim.RegisterCallback<ClickEvent>(_ => Close());
            _root.Add(scrim);

            _win = CnStyle.Box("cn-pop__win");
            CnStyle.Fill(_win, CnStyle.VeilMid, 0.98f);
            _win.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _root.Add(_win);

            var head = CnStyle.Row("cn-pop__head");
            _title = CnStyle.Label(string.Empty, "cn-pop__title");
            head.Add(_title);
            _sub = CnStyle.Label(string.Empty, "cn-pop__sub");
            head.Add(_sub);
            head.Add(CnStyle.Box("cn-grow"));

            var close = CnStyle.Box("cn-pop__x");
            close.Add(CnStyle.Label("✕", "cn-pop__x-t"));
            close.tooltip = null;
            _tip?.Hover(close, () => CnTipCard.Make("ЗАКРЫТЬ", "Esc или клик").Foot("вернуться к окну"));
            close.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Close();
            });
            head.Add(close);
            _win.Add(head);

            _body = CnStyle.Box("cn-pop__body");
            _win.Add(_body);
        }

        /// <summary>Показ с новым содержимым. content может быть null — тогда тело остаётся пустым.</summary>
        public void Show(string title, string subtitle, VisualElement content, Action onClose)
        {
            _title.text = title ?? string.Empty;
            _sub.text = subtitle ?? string.Empty;

            _body.Clear();
            if (content != null) _body.Add(content);

            _onClose = onClose;
            IsOpen = true;
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            _tip?.BringToFront();   // подсказка окна остаётся выше попапа (в моде .tip z-index 80)
        }

        /// <summary>Закрытие: тело освобождается, вызывающий получает callback (перерисовать ленту, снять фокус).</summary>
        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _root.style.display = DisplayStyle.None;
            _body.Clear();

            var callback = _onClose;
            _onClose = null;
            callback?.Invoke();
        }

        /// <summary>Уничтожение окна: попап не держит ссылок на элементы панели.</summary>
        public void Dispose()
        {
            _onClose = null;
            _body.Clear();
            _root.RemoveFromHierarchy();
        }
    }
}
