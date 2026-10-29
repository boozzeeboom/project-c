// Cloudy Navigator — колонка шкалы высоты (шаг 4).
//
// Шкала — отдельная третья колонка правее всей станции (решение пользователя, 04 §5), а не элемент панели:
// она не отнимает место у репутации и не сдвигает слоты.
//
// Домен шкалы — РЕАЛЬНЫЙ коридор игры (AltitudeCorridorSystem.GetActiveCorridor), а не константы мода (D16).
// Следствие: коридор всегда занимает весь трек, поэтому красных насечек «вне коридора» здесь не бывает —
// они были нужны моду, у которого домен отрисовки был шире коридора.
//
// Шаг насечек — Derived: мод рисовал 4/8/14 px каждые 50/100/500 м при коридоре 3250 м. Для global-коридора
// игры (0…99999) это 2000 насечек, поэтому шаг выбирается так, чтобы насечек было не больше MaxTicks.
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Альтиметр: кружок «К», насечки, границы коридора и текущая высота.</summary>
    public sealed class CnAltRailView
    {
        private const int MaxTicks = 72;
        private const int MajorTickWidth = 14;
        private const int MidTickWidth = 8;
        private const int MinorTickWidth = 4;

        private readonly VisualElement _root;
        private readonly VisualElement _track;
        private readonly VisualElement _value;
        private readonly Label _valueLabel;

        private string _signature = string.Empty;
        private float _width = 36f;

        public VisualElement Root => _root;

        public CnAltRailView()
        {
            _root = new VisualElement();
            _root.AddToClassList("cn-altrail");

            var head = new VisualElement();
            head.AddToClassList("cn-altrail__head");
            head.Add(CnStyle.Label("К", "cn-altrail__headlabel"));
            _root.Add(head);

            _track = new VisualElement();
            _track.AddToClassList("cn-altrail__track");
            _root.Add(_track);

            _value = new VisualElement();
            _value.style.position = Position.Absolute;
            _value.style.left = 0f;
            _value.style.right = 0f;

            _valueLabel = CnStyle.Label("—", "cn-altval");
            _valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _value.Add(_valueLabel);

            var hair = new VisualElement();
            hair.AddToClassList("cn-altval__hair");
            _value.Add(hair);

            _track.Add(_value);
        }

        /// <summary>Ширина колонки: 36 / 34 / 32 / 30 px — те же ступени, что altW мода.</summary>
        public void SetWidth(float width)
        {
            _width = width;
            _root.style.width = width;
        }

        public void Refresh(CnDataFeed feed)
        {
            if (feed == null) return;

            if (!feed.hasAltitude)
            {
                _valueLabel.text = "—";
                if (_signature == "none") return;
                _signature = "none";
                RebuildTicks(null);
                return;
            }

            // Суффикс « м» — только когда колонка ≥ 34 px (как в моде).
            string suffix = _width >= 34f ? " м" : string.Empty;
            _valueLabel.text = feed.altitude.ToString("0") + suffix;

            float span = feed.corridorMax - feed.corridorMin;
            float pct = span > 0f ? Mathf.Clamp01((feed.altitude - feed.corridorMin) / span) : 0f;
            float top = Mathf.Clamp(100f - pct * 100f, 4f, 96f);
            _value.style.top = Length.Percent(top);

            string signature = feed.corridorId + "|" + feed.corridorMin + "|" + feed.corridorMax;
            if (signature == _signature) return;
            _signature = signature;
            RebuildTicks(feed);
        }

        private void RebuildTicks(CnDataFeed feed)
        {
            _track.Clear();

            if (feed != null)
            {
                float span = feed.corridorMax - feed.corridorMin;

                // Границы коридора — две сплошные линии на всю ширину.
                _track.Add(Boundary());

                int step = ChooseStep(span);
                if (step > 0)
                {
                    int count = Mathf.FloorToInt(span / step);
                    for (int i = 1; i <= count; i++)
                    {
                        float altitude = feed.corridorMin + i * step;
                        bool major = Mathf.Approximately(altitude % 500f, 0f);
                        bool mid = !major && Mathf.Approximately(altitude % 100f, 0f);

                        var tick = new VisualElement();
                        tick.AddToClassList("cn-tick");
                        tick.style.width = major ? MajorTickWidth : mid ? MidTickWidth : MinorTickWidth;
                        _track.Add(tick);
                    }
                }

                _track.Add(Boundary());
            }

            _track.Add(_value);
        }

        private static VisualElement Boundary()
        {
            var line = new VisualElement();
            line.AddToClassList("cn-bound");
            return line;
        }

        /// <summary>Derived: шаг насечек из длины коридора, чтобы их было не больше MaxTicks.</summary>
        private static int ChooseStep(float span)
        {
            if (span <= 0f) return 0;

            int[] steps = { 50, 100, 500, 1000, 5000, 10000 };
            for (int i = 0; i < steps.Length; i++)
            {
                if (span / steps[i] <= MaxTicks) return steps[i];
            }

            return 0;
        }
    }
}
