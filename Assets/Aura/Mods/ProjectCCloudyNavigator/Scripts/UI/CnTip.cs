// Cloudy Navigator — единственный слой подсказки окна (порт src/ui.js:118-266 мода).
//
// В моде наведение — не свойство отдельных элементов, а общий слой: createTooltip() держит ОДИН .tip на всё окно,
// задержку 140 мс копит кадровый update(dt) (своих таймеров у подсказки нет), карточку ставит справа от якоря +12 px,
// при нехватке места переворачивает влево, ниже якоря или над ним и прижимает к экрану с полем 8 px.
// Содержимое строит cloudCard(): заголовок, подзаголовок, строки «ключ — значение», сноска с тоном (warn/danger).
//
// Здесь то же самое: один слой на окно, время — только из Tick(dt) окна (как у CnMotion), карточка мышь не принимает,
// поэтому перекрывать наводимый элемент не может. Никаких новых данных карточка не берёт: текст даёт вызывающий вид.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Содержимое облачной карточки (cloudCard мода). Поля ровно те же, что в моде.</summary>
    public sealed class CnTipCard
    {
        public string title;
        public string sub;
        public string foot;
        public byte tone;   // 0 — обычная сноска, 1 — warn, 2 — danger (.tip-foot мода)

        public readonly List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();

        public static CnTipCard Make(string title, string sub = null)
        {
            return new CnTipCard { title = title, sub = sub };
        }

        /// <summary>Строка «ключ — значение»; пустая пара не добавляется, null в значении печатается прочерком.</summary>
        public CnTipCard Row(string key, string value)
        {
            if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(value)) return this;
            rows.Add(new KeyValuePair<string, string>(key ?? string.Empty, value ?? "—"));
            return this;
        }

        public CnTipCard Foot(string text, byte footTone = 0)
        {
            foot = text;
            tone = footTone;
            return this;
        }
    }

    /// <summary>Слой подсказки: одна карточка, одна задержка, одно размещение — на всё окно.</summary>
    public sealed class CnTip
    {
        /// <summary>ui.js: createTooltip(document.body, { delay: 140 }) — 140 мс.</summary>
        public const float Delay = 0.14f;

        private const float Margin = 8f;    // ui.js: const m = 8
        private const float Offset = 12f;   // ui.js: x = anchor.right + 12
        private const float MaxWidth = 290f; // .tip { max-width: 290px }

        private readonly VisualElement _layer;
        private readonly VisualElement _card;
        private readonly Dictionary<VisualElement, Func<CnTipCard>> _targets =
            new Dictionary<VisualElement, Func<CnTipCard>>();

        private VisualElement _pending;   // цель, ждущая задержки
        private float _timer;
        private VisualElement _shown;     // цель показанной карточки
        private Rect _anchor;
        private bool _placed;

        public CnTip()
        {
            _layer = CnStyle.Box("cn-tip");
            _layer.pickingMode = PickingMode.Ignore;

            _card = CnStyle.Box("cn-tip__card");
            _card.pickingMode = PickingMode.Ignore;
            // 06 §3: .tip-card > .halo-tipsh — 26/16/36, rgba(0,0,0,.45). Карточка висит над окном без подложки.
            CnStyle.HaloShadow(_card, 26, 16, 36, CnStyle.Shadow, 0.45f);
            _card.AddToClassList("is-idle");
            _card.RegisterCallback<GeometryChangedEvent>(_ => Place());
            _layer.Add(_card);
        }

        /// <summary>Слой кладётся в корень окна последним из панелей: подсказка всегда над ними.</summary>
        public void Mount(VisualElement parent)
        {
            if (parent == null || _layer.parent == parent) return;
            parent.Add(_layer);
            _layer.BringToFront();
        }

        /// <summary>
        /// Слой подсказки всегда последний. Панели попапов добавляются в корень позже подсказки (CnPopup.Show зовёт
        /// BringToFront на себе), поэтому после каждого такого показа карточка должна вернуться наверх — иначе клик
        /// по попапу «съедает» подсказку, и она оказывается под ним.
        /// </summary>
        public void BringToFront()
        {
            _layer.BringToFront();
        }

        /// <summary>
        /// Наведение на элемент: content вызывается в момент показа, поэтому карточка всегда описывает текущее
        /// состояние данных, а не то, что было при постройке вида.
        /// </summary>
        public void Hover(VisualElement target, Func<CnTipCard> content)
        {
            if (target == null) return;

            if (_targets.ContainsKey(target))
            {
                _targets[target] = content;
                return;
            }

            _targets[target] = content;

            target.RegisterCallback<PointerEnterEvent>(_ => Enter(target));
            target.RegisterCallback<PointerLeaveEvent>(_ => Leave(target));
            target.RegisterCallback<PointerDownEvent>(_ => Hide());
        }

        /// <summary>Показ без наведения (для проверки вида): карточка висит, пока не позовут Hide.</summary>
        public void Show(CnTipCard card, Rect anchor)
        {
            if (card == null) return;
            Fill(card);
            _anchor = anchor;
            _placed = false;
            _card.RemoveFromClassList("is-idle");
            Place();
        }

        public void Hide()
        {
            _pending = null;
            _shown = null;
            _placed = false;
            _card.AddToClassList("is-idle");
        }

        /// <summary>Кадр окна: задержка показа копится здесь — своих таймеров у подсказки нет.</summary>
        public void Tick(float dt)
        {
            if (_pending == null) return;

            _timer += dt;
            if (_timer < Delay) return;

            VisualElement target = _pending;
            _pending = null;

            if (target.panel == null || target.resolvedStyle.display == DisplayStyle.None) return;

            Func<CnTipCard> build;
            if (!_targets.TryGetValue(target, out build) || build == null) return;

            CnTipCard card = build();
            if (card == null) return;

            Fill(card);
            _shown = target;
            _anchor = LocalRect(target);
            _placed = false;
            _card.RemoveFromClassList("is-idle");
            _layer.BringToFront();   // попап мог быть показан после монтирования подсказки
            Place();
        }

        public void Dispose()
        {
            _targets.Clear();
            Hide();
            _layer.RemoveFromHierarchy();
        }

        // ------------------------------------------------------------------ внутреннее

        private void Enter(VisualElement target)
        {
            if (_shown == target) return;
            _pending = target;
            _timer = 0f;
        }

        private void Leave(VisualElement target)
        {
            if (_pending == target) _pending = null;
            if (_shown == target) Hide();
        }

        /// <summary>Прямоугольник цели в координатах слоя (окно ConstantPixelSize, scale 1 — панель = пиксели).</summary>
        private Rect LocalRect(VisualElement target)
        {
            Rect t = target.worldBound;
            Rect origin = _layer.worldBound;
            return new Rect(t.x - origin.x, t.y - origin.y, t.width, t.height);
        }

        /// <summary>
        /// Размещение один в один по ui.js:184-208. Первый проход — по оценке текста (раскладка ещё не посчитана),
        /// второй — по настоящей геометрии карточки после раскладки; смещение меняется до появления, поэтому не видно.
        /// </summary>
        private void Place()
        {
            if (_shown == null && _card.ClassListContains("is-idle")) return;

            float vw = _layer.resolvedStyle.width;
            float vh = _layer.resolvedStyle.height;
            if (float.IsNaN(vw) || float.IsNaN(vh) || vw <= 1f || vh <= 1f) return;

            float w = _card.resolvedStyle.width;
            float h = _card.resolvedStyle.height;
            bool measured = !float.IsNaN(w) && !float.IsNaN(h) && w > 1f && h > 1f && _placed;

            if (!measured)
            {
                w = Mathf.Min(MaxWidth, EstimateWidth());
                h = EstimateHeight();
            }

            float x = _anchor.xMax + Offset;
            if (x + w > vw - Margin) x = _anchor.xMin - Offset - w;

            float y;
            if (x < Margin)
            {
                x = Mathf.Min(Mathf.Max(Margin, _anchor.center.x - w * 0.5f), Mathf.Max(Margin, vw - w - Margin));
                y = _anchor.yMax + Offset;
                if (y + h > vh - Margin) y = _anchor.yMin - Offset - h;
            }
            else
            {
                y = _anchor.center.y - h * 0.5f;
            }

            x = Mathf.Min(Mathf.Max(Margin, x), Mathf.Max(Margin, vw - w - Margin));
            y = Mathf.Min(Mathf.Max(Margin, y), Mathf.Max(Margin, vh - h - Margin));

            _card.style.left = Mathf.Round(x);
            _card.style.top = Mathf.Round(y);
            _placed = true;
        }

        /// <summary>Оценка ширины по Inter: нужна только первому кадру, настоящая ширина приходит из раскладки.</summary>
        private float EstimateWidth()
        {
            float widest = 0f;
            for (int i = 0; i < _card.childCount; i++)
            {
                var label = _card[i] as Label;
                if (label == null) continue;
                widest = Mathf.Max(widest, CnStyle.TextWidth(label.text, 11.5f, false));
            }
            return Mathf.Clamp(widest + 26f, 120f, MaxWidth);
        }

        private float EstimateHeight()
        {
            int lines = 1;
            for (int i = 0; i < _card.childCount; i++) lines += _card[i] is Label ? 1 : 0;
            return 12f + lines * 19f;
        }

        /// <summary>cloudCard(): уголки, заголовок, подзаголовок, строки k/v, сноска.</summary>
        private void Fill(CnTipCard card)
        {
            _card.Clear();

            // 06 §4: .tip-card > .halo-tipin — внутренний ореол rgba(47,124,80,.14). Ставится после Clear:
            // содержимое карточки пересобирается на каждом показе, и ореол обязан собираться вместе с ним.
            CnStyle.HaloInner(_card, 9, CnStyle.GasBright, 0.14f);

            _card.Add(Corner("cn-tip__corner--a"));
            _card.Add(Corner("cn-tip__corner--b"));

            if (!string.IsNullOrEmpty(card.title))
                _card.Add(CnStyle.Label(card.title.ToUpperInvariant(), "cn-tip__title"));

            if (!string.IsNullOrEmpty(card.sub))
                _card.Add(CnStyle.Label(card.sub, "cn-tip__sub"));

            for (int i = 0; i < card.rows.Count; i++)
            {
                var row = CnStyle.Row("cn-tip__row");
                row.Add(CnStyle.Label(card.rows[i].Key, "cn-tip__k"));
                row.Add(CnStyle.Label(card.rows[i].Value, "cn-tip__v"));
                _card.Add(row);
            }

            if (!string.IsNullOrEmpty(card.foot))
            {
                string cls = card.tone == 1 ? "cn-tip__foot cn-tip__foot--warn"
                    : card.tone == 2 ? "cn-tip__foot cn-tip__foot--danger"
                    : "cn-tip__foot";
                _card.Add(CnStyle.Label(card.foot.ToUpperInvariant(), cls));
            }
        }

        private static VisualElement Corner(string modifier)
        {
            var corner = CnStyle.Deco("cn-tip__corner");
            corner.AddToClassList(modifier);
            return corner;
        }
    }
}
