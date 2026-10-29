// Cloudy Navigator — кольцо отношения (мод v4-2: src/rel-ring.js) для узлов графа связей.
//
// Один и тот же визуальный язык, что у станции и виджета отношений: окружность-подложка, дуга значения
// от 12 часов по часовой (плюс) или против (минус), риски порогов тиров и значение в центре.
// Шкала по субъекту (rel-ring.js/relations.js:19-27): фракция −100…+100 → 1.8°/ед., человек −100…+200 → 1.2°/ед.
//
// Цвет: у фракции с тирами — цвет тира из ассета (ReputationTier.color), иначе градиент мода
// rel-ring.js/relations.js:97-112 (серый у нуля → зелёный в плюс, бордовый в минус).
// Риски: пороги тиров ассета плюс ноль латунью (rel-ring.js:37-46); у человека порогов тиров ассетов нет,
// поэтому рисуется только ноль — выдумывать чужие пороги кольцо не должно.
//
// Дуга строится полилинией, а не Painter2D.Arc: угол отсчитывается от 12 часов вручную, как P(a) в моде,
// и не зависит от соглашения об углах конкретной версии UI Toolkit.
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Кольцо отношения узла: подложка, дуга значения, риски тиров, значение в центре.</summary>
    public sealed class CnRelRing : VisualElement
    {
        private static readonly Color PosFar = new Color32(179, 214, 192, 255);   // rel-ring.js POS 150
        private static readonly Color PosMax = new Color32(196, 224, 205, 255);   // rel-ring.js POS 200
        private static readonly Color NegFar = new Color32(150, 78, 62, 255);     // rel-ring.js NEG −25…−100

        private readonly Label _value;
        private readonly float _size;
        private readonly float _stroke;
        private readonly float _radius;
        private readonly int _scaleMin;
        private readonly int _scaleMax;
        private readonly int[] _ticks;
        private Color _arc;
        private int _shown = int.MinValue;

        public CnRelRing(float size, int value, int scaleMin, int scaleMax, Color tierColor, bool hasTierColor, int[] tickValues)
        {
            _size = size;
            _stroke = Mathf.Max(3f, Mathf.Round(size * 0.13f));
            _radius = size * 0.5f - _stroke * 0.5f - 1f;
            _scaleMin = scaleMin;
            _scaleMax = scaleMax;
            _ticks = tickValues;

            AddToClassList("cn-relg__ring");
            style.width = size;
            style.height = size;

            _value = CnStyle.Label(string.Empty, "cn-relg__ring-v");
            _value.pickingMode = PickingMode.Ignore;
            Add(_value);

            pickingMode = PickingMode.Ignore;   // клик по узлу обрабатывает сам узел
            Set(value, hasTierColor ? tierColor : Gradient(value));
            generateVisualContent += OnGenerate;
        }

        /// <summary>Новое значение (данные могли прийти позже) — цвет пересчитывается только без тирового цвета.</summary>
        public void Set(int value, Color color)
        {
            if (value == _shown) return;
            _shown = value;
            _arc = color;
            _value.text = (value > 0 ? "+" : value < 0 ? "\u2212" : string.Empty) + Mathf.Abs(value);
            // rel-ring.js:49 — кегль значения считается от размера кольца и длины подписи.
            int len = _value.text.Length;
            _value.style.fontSize = Mathf.Max(8f, Mathf.Round(_size * (len > 3 ? 0.26f : 0.3f) * 10f) * 0.1f);
            MarkDirtyRepaint();
        }

        /// <summary>Градиент мода для субъекта без тиров (rel-ring.js/relations.js:97-112).</summary>
        public static Color Gradient(int value)
        {
            float a = Mathf.Abs((float)value);
            if (value >= 0)
            {
                if (a <= 25f) return Mix(CnStyle.Dim, CnStyle.GasAccent, a / 25f);
                if (a <= 75f) return Mix(CnStyle.GasAccent, CnStyle.Accent, (a - 25f) / 50f);
                if (a <= 150f) return Mix(CnStyle.Accent, PosFar, (a - 75f) / 75f);
                return Mix(PosFar, PosMax, Mathf.Clamp01((a - 150f) / 50f));
            }

            if (a <= 25f) return Mix(CnStyle.Dim, NegFar, a / 25f);
            return Mix(NegFar, CnStyle.Danger, Mathf.Clamp01((a - 25f) / 75f));
        }

        private static Color Mix(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
        }

        private float AngleOf(int value)
        {
            int span = _scaleMax - _scaleMin;
            if (span <= 0) return 0f;
            float clamped = Mathf.Clamp(value, _scaleMin, _scaleMax);
            return clamped * (360f / span);
        }

        private static Vector2 At(Vector2 center, float radius, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(center.x + radius * Mathf.Sin(r), center.y - radius * Mathf.Cos(r));
        }

        private void OnGenerate(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            var center = new Vector2(_size * 0.5f, _size * 0.5f);

            // Подложка: заливка veilDark 0.7 и обводка veilEdge 0.5 (rel-ring.js:29).
            Ring(p, center, _radius, 0f, 360f, CnStyle.A(CnStyle.Veil, 0.7f), _stroke, true);
            Ring(p, center, _radius, 0f, 360f, CnStyle.A(CnStyle.VeilEdge, 0.5f), _stroke, false);

            // Дуга значения: плюс — по часовой от 12 часов, минус — против (rel-ring.js:26-31).
            float angle = AngleOf(_shown);
            if (Mathf.Abs(angle) >= 0.4f)
            {
                float a0 = Mathf.Min(0f, angle);
                float a1 = Mathf.Max(0f, angle);
                Arc(p, center, _radius, a0, a1, _arc, _stroke);
            }

            // Риски порогов: латунь на нуле, тёмная кромка на остальных (rel-ring.js:40-46).
            if (_ticks != null)
            {
                for (int i = 0; i < _ticks.Length; i++)
                {
                    int t = _ticks[i];
                    if (t < _scaleMin || t > _scaleMax) continue;
                    bool zero = t == 0;
                    float ri = _radius - _stroke * 0.5f - 0.5f;
                    float ro = _radius + _stroke * 0.5f + 0.5f;
                    p.strokeColor = zero ? CnStyle.Brass : CnStyle.A(CnStyle.Veil, 1f);
                    p.lineWidth = zero ? 1.2f : 1.4f;
                    p.BeginPath();
                    p.MoveTo(At(center, ri, AngleOf(t)));
                    p.LineTo(At(center, ro, AngleOf(t)));
                    p.Stroke();
                }
            }
        }

        private static void Ring(Painter2D p, Vector2 center, float radius, float a0, float a1,
            Color color, float width, bool fill)
        {
            const int segments = 48;
            p.BeginPath();
            for (int i = 0; i <= segments; i++)
            {
                float a = a0 + (a1 - a0) * i / segments;
                var pt = At(center, radius, a);
                if (i == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.ClosePath();
            if (fill)
            {
                p.fillColor = color;
                p.Fill();
            }

            p.strokeColor = color;
            p.lineWidth = width;
            p.Stroke();
        }

        private static void Arc(Painter2D p, Vector2 center, float radius, float a0, float a1, Color color, float width)
        {
            // Один сегмент на 2° — мягкая дуга при любом значении и без зависимости от Arc().
            int segments = Mathf.Max(2, Mathf.CeilToInt((a1 - a0) / 2f));
            p.BeginPath();
            for (int i = 0; i <= segments; i++)
            {
                float a = a0 + (a1 - a0) * i / segments;
                var pt = At(center, radius, a);
                if (i == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.strokeColor = color;
            p.lineWidth = width;
            p.Stroke();
        }
    }
}
