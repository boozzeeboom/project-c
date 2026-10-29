// Cloudy Navigator — свёрнутая заметка графа связей (мод v4-2: src/rel-graph.js `.rn-note.min`).
//
// В моде свёрнутая заметка — стикер 44×42 с рваным краем из `clip-path: polygon(…)` и карандашом внутри
// (rel-graph.js:103-107, 1014-1025). `clip-path` в USS нет, поэтому край рисуется Painter2D по ТОМУ ЖЕ
// полигону из девятнадцати вершин и в том же порядке: это не аппроксимация «на глаз», а перенос чисел мода.
// Карандаш тоже рисуется примитивами по путям иконки мода (viewBox 24×24), чтобы не зависеть от того, есть ли
// в SDF-шрифте глиф карандаша.
//
// Пульс свёрнутой заметки (`ntpulse`, opacity 0.62 ↔ 1 за 2.4 s) в CSS был анимацией. Здесь он сделан USS-переходом
// прозрачности, а класс-переключатель ставит окно по расписанию раз в 1.2 s: кадровых таймеров в порте нет.
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Рваный край свёрнутой заметки и карандаш внутри — примитивами, без clip-path.</summary>
    public sealed class CnNoteFold : VisualElement
    {
        /// <summary>Полигон мода (rel-graph.js:104) в долях размера стикера, в том же порядке вершин.</summary>
        private static readonly Vector2[] Edge =
        {
            new Vector2(0.00f, 0.06f), new Vector2(0.10f, 0.00f), new Vector2(0.28f, 0.04f),
            new Vector2(0.50f, 0.00f), new Vector2(0.72f, 0.04f), new Vector2(0.90f, 0.00f),
            new Vector2(1.00f, 0.08f), new Vector2(0.98f, 0.30f), new Vector2(1.00f, 0.52f),
            new Vector2(0.97f, 0.74f), new Vector2(1.00f, 0.92f), new Vector2(0.88f, 1.00f),
            new Vector2(0.66f, 0.97f), new Vector2(0.44f, 1.00f), new Vector2(0.22f, 0.96f),
            new Vector2(0.04f, 1.00f), new Vector2(0.00f, 0.88f), new Vector2(0.02f, 0.60f),
            new Vector2(0.00f, 0.34f)
        };

        /// <summary>Корпус карандаша (rel-graph.js:1017, путь 1): M5 19 l3.4-.7 L19 7.7 L16.3 5 L5.7 15.6 z.</summary>
        private static readonly Vector2[] PencilBody =
        {
            new Vector2(5.0f, 19.0f), new Vector2(8.4f, 18.3f), new Vector2(19.0f, 7.7f), new Vector2(16.3f, 5.0f)
        };

        /// <summary>Грифель (путь 2): M14.6 6.7 l2.7 2.7.</summary>
        private static readonly Vector2[] PencilTip =
        {
            new Vector2(14.6f, 6.7f), new Vector2(17.3f, 9.4f)
        };

        /// <summary>Блик над корпусом (путь 3) — полилиния по опорным точкам кривой мода.</summary>
        private static readonly Vector2[] PencilGlint =
        {
            new Vector2(17.0f, 4.0f), new Vector2(18.4f, 5.4f), new Vector2(19.6f, 8.2f),
            new Vector2(18.2f, 8.0f), new Vector2(17.2f, 7.2f)
        };

        private const float FallbackSize = 44f;   // rel-graph.js:103 — .rn-note.min width:44px

        public CnNoteFold()
        {
            AddToClassList("cn-relg__fold");
            pickingMode = PickingMode.Ignore;   // клик разворачивает заметку — обработчик на самом стикере
            generateVisualContent += OnGenerate;
        }

        private void OnGenerate(MeshGenerationContext mgc)
        {
            float w = layout.width;
            float h = layout.height;
            if (float.IsNaN(w) || w < 4f) w = FallbackSize;
            if (float.IsNaN(h) || h < 4f) h = FallbackSize;

            var p = mgc.painter2D;
            p.lineJoin = LineJoin.Round;

            // Тело стикера: тёмная плёнка вуали и латунная кромка рваного края — вместо CSS-градиента и clip-path.
            p.fillColor = CnStyle.A(CnStyle.VeilMid, 0.96f);
            p.BeginPath();
            for (int i = 0; i < Edge.Length; i++)
            {
                var at = new Vector2(Edge[i].x * w, Edge[i].y * h);
                if (i == 0) p.MoveTo(at);
                else p.LineTo(at);
            }
            p.ClosePath();
            p.Fill();

            p.strokeColor = CnStyle.A(CnStyle.Brass, 0.42f);
            p.lineWidth = 1f;
            p.Stroke();

            // Карандаш: вписываем квадрат 24×24 иконки мода в стикер с полем 10 %.
            float side = Mathf.Min(w, h) * 0.62f;
            var origin = new Vector2((w - side) * 0.5f, (h - side) * 0.5f);
            float k = side / 24f;

            p.fillColor = CnStyle.Brass;
            p.BeginPath();
            for (int i = 0; i < PencilBody.Length; i++)
            {
                var at = origin + PencilBody[i] * k;
                if (i == 0) p.MoveTo(at);
                else p.LineTo(at);
            }
            p.ClosePath();
            p.Fill();

            p.strokeColor = CnStyle.Brass;
            p.lineWidth = 1.2f;
            Stroke(p, origin, k, PencilTip);
            Stroke(p, origin, k, PencilGlint);
        }

        private static void Stroke(Painter2D p, Vector2 origin, float k, Vector2[] points)
        {
            p.BeginPath();
            for (int i = 0; i < points.Length; i++)
            {
                var at = origin + points[i] * k;
                if (i == 0) p.MoveTo(at);
                else p.LineTo(at);
            }
            p.Stroke();
        }
    }
}
