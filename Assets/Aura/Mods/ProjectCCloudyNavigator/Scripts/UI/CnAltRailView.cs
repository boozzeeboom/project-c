// Cloudy Navigator — колонка шкалы высоты по src/alt.css мода.
//
// Тонкая колонка у правого края окна: только волосок слева, без заливки. Сверху кружок «К» 16 × 16 (статус
// коридора, полная строка — в подсказке). Ниже — шкала: насечки 14 px у каждой пятой и 8 px у остальных,
// зелёные внутри [minAltitude, maxAltitude] и красные вне; две сплошные границы коридора с уголками 3 × 3;
// значение 11 px стоит прямо на высоте маркера, под ним латунный волосок.
//
// Домен и границы — РЕАЛЬНЫЙ коридор игры (AltitudeCorridorSystem.GetActiveCorridor, D16): домен шире коридора на
// warningMargin снизу и criticalUpperMargin сверху — ровно та зона, где игра переходит в WarningLower/DangerUpper.
// Позиции считаются от ФАКТИЧЕСКОЙ высоты шкалы (GeometryChangedEvent), а не от констант.
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Альтиметр: кружок «К», насечки, границы коридора и текущая высота.</summary>
    public sealed class CnAltRailView
    {
        private const float MinTickSpacing = 7f;    // px между насечками, не плотнее
        private const float MarkerPad = 8f;         // маркер не уезжает под край шкалы

        private static readonly float[] Steps = { 10f, 25f, 50f, 100f, 250f, 500f, 1000f, 2500f, 5000f, 10000f, 25000f };

        private readonly VisualElement _root;
        private readonly Label _dot;
        private readonly VisualElement _scale;
        private readonly VisualElement _ticks;
        private readonly VisualElement _corMin;
        private readonly VisualElement _corMax;
        private readonly VisualElement _mark;
        private readonly Label _value;

        private float _width = 36f;
        private float _domainMin;
        private float _domainMax;
        private float _corridorMin;
        private float _corridorMax;
        private bool _hasDomain;
        private string _signature = string.Empty;
        private float _scaleHeight = -1f;
        private int _status = -1;

        public VisualElement Root => _root;

        public CnAltRailView()
        {
            _root = CnStyle.Box("cn-altrail");

            _dot = CnStyle.Label("К", "cn-ar__dot");
            _dot.style.backgroundColor = CnStyle.A(CnStyle.Accent, 0.14f);
            _root.Add(_dot);

            _scale = CnStyle.Box("cn-ar__scale");
            _scale.RegisterCallback<GeometryChangedEvent>(_ => Layout(true));

            _ticks = CnStyle.Deco("cn-ar__ticks");
            _scale.Add(_ticks);

            _corMin = Corridor();
            _corMax = Corridor();
            _scale.Add(_corMin);
            _scale.Add(_corMax);

            _mark = CnStyle.Deco("cn-ar__mark");
            _value = CnStyle.Label("—", "cn-ar__v");
            _mark.Add(_value);
            _mark.Add(CnStyle.Deco("cn-ar__hair"));
            _scale.Add(_mark);

            _root.Add(_scale);
        }

        private static VisualElement Corridor()
        {
            var line = CnStyle.Deco("cn-ar__cor");
            var l = CnStyle.Deco("cn-ar__cc");
            l.style.left = 0f;
            var r = CnStyle.Deco("cn-ar__cc");
            r.style.right = 0f;
            line.Add(l);
            line.Add(r);
            return line;
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
                _value.text = "—";
                _root.tooltip = "высота: нет данных (корабль не найден)";
                SetStatus(0);
                if (_hasDomain)
                {
                    _hasDomain = false;
                    _signature = string.Empty;
                    Layout(true);
                }
                return;
            }

            // Суффикс «м» — только когда колонка ≥ 34 px (как в моде).
            _value.text = feed.altitude.ToString("0") + (_width >= 34f ? " м" : string.Empty);
            _root.tooltip = (string.IsNullOrEmpty(feed.corridorDisplayName) ? feed.corridorId : feed.corridorDisplayName) +
                            " · " + feed.corridorMin.ToString("0") + "…" + feed.corridorMax.ToString("0") + " м · " +
                            feed.altitudeStatusName;
            SetStatus(feed.altitudeStatusValue);

            float lower = feed.corridorIsGlobal ? 0f : Mathf.Max(0f, feed.corridorWarningMargin);
            float upper = feed.corridorIsGlobal ? 0f : Mathf.Max(0f, feed.corridorCriticalUpperMargin);

            string signature = feed.corridorId + "|" + feed.corridorMin + "|" + feed.corridorMax + "|" + lower + "|" + upper;
            if (signature != _signature)
            {
                _signature = signature;
                _corridorMin = feed.corridorMin;
                _corridorMax = feed.corridorMax;
                _domainMin = feed.corridorMin - lower;
                _domainMax = feed.corridorMax + upper;
                _hasDomain = _domainMax > _domainMin;
                Layout(true);
            }

            PlaceMarker(feed.altitude);
        }

        /// <summary>Safe и Warning — зелёный кружок, Danger — тёплый красный (alt.css .ar-dot.danger).</summary>
        private void SetStatus(int status)
        {
            if (status == _status) return;
            _status = status;

            bool danger = status >= 3;   // AltitudeStatus.DangerLower = 3, DangerUpper = 4
            _dot.EnableInClassList("cn-ar__dot--danger", danger);
            _dot.style.backgroundColor = danger ? CnStyle.A(CnStyle.DangerLine, 0.16f) : CnStyle.A(CnStyle.Accent, 0.14f);
        }

        private float Y(float altitude, float height)
        {
            float t = (altitude - _domainMin) / (_domainMax - _domainMin);
            return Mathf.Round((1f - Mathf.Clamp01(t)) * height);
        }

        private void Layout(bool force)
        {
            float height = _scale.layout.height;
            if (float.IsNaN(height) || height <= 0f) return;
            if (!force && Mathf.Abs(height - _scaleHeight) < 0.5f) return;
            _scaleHeight = height;

            _ticks.Clear();
            _corMin.style.display = _hasDomain ? DisplayStyle.Flex : DisplayStyle.None;
            _corMax.style.display = _hasDomain ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_hasDomain) return;

            float span = _domainMax - _domainMin;
            float step = Steps[Steps.Length - 1];
            for (int i = 0; i < Steps.Length; i++)
            {
                if (span / Steps[i] * MinTickSpacing <= height) { step = Steps[i]; break; }
            }

            float first = Mathf.Ceil(_domainMin / step) * step;
            for (float a = first; a <= _domainMax + 0.01f; a += step)
            {
                int k = Mathf.RoundToInt(a / step);
                var tick = CnStyle.Deco("cn-ar__tk");
                if (a < _corridorMin - 0.01f || a > _corridorMax + 0.01f) tick.AddToClassList("cn-ar__tk--out");
                float w = k % 5 == 0 ? 14f : 8f;
                tick.style.width = w;
                tick.style.left = Mathf.Round((_width - 1f - w) * 0.5f);
                tick.style.top = Y(a, height);
                _ticks.Add(tick);
            }

            _corMin.style.top = Y(_corridorMin, height);
            _corMax.style.top = Y(_corridorMax, height);
        }

        private void PlaceMarker(float altitude)
        {
            float height = _scale.layout.height;
            if (float.IsNaN(height) || height <= 0f || !_hasDomain) return;

            float y = Mathf.Clamp(Y(altitude, height), MarkerPad, height - MarkerPad);
            _mark.style.top = y - 7f;   // середина строки значения 11 px на высоте маркера
        }
    }
}
