// Cloudy Navigator — фигура персонажа в нише станции.
//
// В моде фигура трёхмерная: three.js, buildModel() в .Aura/Slices/v4-2/src/character.js — капсулы, цилиндр
// плаща, сферы плеч и головы, пряжки и мезиевые акценты. В Unity такой вьюер снят: он заводил GameObject-корень
// в мировой сцене, а КАЖДЫЙ активный корень мировой сцены — участник ребейза floating origin
// (GlobalMotionControlledRebaseSlice.cs:1113-1129 берёт их по id WORLD_SCENE_ROOT/<имя>). Вторая копия с тем же
// именем валила транзакцию целиком: participant_scope_refused:duplicate_participant_id:
// WORLD_SCENE_ROOT/[CN] Preview Stage. HideFlags.DontSave и DontDestroyOnLoad лечили следствие, а не причину.
//
// Поэтому здесь та же фигура, но плоская и только средствами UI Toolkit: ни объектов сцены, ни камеры,
// ни RenderTexture, ни коллайдеров, ни слоя 30. Floating origin такую фигуру не видит вообще.
// Новый 3D-подход к превью — отдельным тикетом.
//
// Геометрия и палитра взяты из buildModel(), а не подобраны на глаз (в скобках — строка источника):
//   • капсулы three.js: CapsuleGeometry(r, h) — это цилиндр h с полусферами r, поэтому ось капсулы короче
//     полной высоты на 2r;
//   • ноги (128-132): ось (±0.13, 0.08…0.64), r 0.085 → ступни на 0, верх бедра 0.725;
//   • обувь (134-138): коробка 0.17 × 0.10 по центру (±0.13, 0.05);
//   • торс (140-142): ось (0, 0.52…1.20), r 0.17;
//   • плащ (144-148): открытый цилиндр r 0.20 сверху, 0.34 снизу, высота 0.78 по центру 0.74 → 0.35…1.13;
//   • плечи (150-154): сферы r 0.075 в (±0.21, 1.05);
//   • руки (155-161): ось (±0.28, 0.61…1.03), r 0.055, наклон ±0.32 рад (A-поза);
//   • шея (163-164) и голова (165-168): шея 0.10 × 0.07 по центру 1.13, голова r 0.115 со scale (1, 1.12, 0.95)
//     → 1.111…1.369, то есть макушка ровно на 1.37;
//   • пряжки (170-174), пояс (175-178), акценты (179-185): латунь #b9915a и мезий #54a373;
//   • цвета (120-125): плащ #2b342c, отделка #4a574c, кожа #6a7361, брюки #1f2620.
// Дым у ног (188-199) не переносится: в моде это горизонтальные плоскости, с фронтального вида они
// вырождаются в линию, а веб-анимации в порте не воспроизводятся (правило 04_PORT_RULES_AND_PLAN).
//
// Чего у плоской фигуры нет намеренно: вращения, зума и авто-поворота — это были свойства 3D-сцены.
// Возврат к 3D — отдельный тикет, а не тихая правка этого файла.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Плоская фигура персонажа в нише станции: только VisualElement и Painter2D.</summary>
    public sealed class CnFigure : IDisposable
    {
        // Мировой габарит figure из buildModel(): по X плащ ±0.34 и руки ±0.335, по Y стопы 0 … макушка 1.369.
        private const float WorldWidth = 0.70f;
        private const float WorldHeight = 1.37f;

        // Палитра buildModel(): PALETTES.charcoal для плаща и отделки, остальные материалы — константы.
        private static readonly Color Cloak = Hex("#2b342c");
        private static readonly Color Trim = Hex("#4a574c");
        private static readonly Color Skin = Hex("#6a7361");
        private static readonly Color Pant = Hex("#1f2620");
        private static readonly Color Brass = Hex("#b9915a");
        private static readonly Color Mez = Hex("#54a373");

        private readonly VisualElement _root;
        private readonly VisualElement _stage;
        private float _scale;

        /// <summary>Клик по фигуре: окно открывает окно внешности игры.</summary>
        public Action OnClick;

        public CnFigure()
        {
            _root = new VisualElement { name = "cn-figure" };
            _root.AddToClassList("cn-figure");
            _root.pickingMode = PickingMode.Position;
            _root.style.flexGrow = 1f;
            _root.style.minHeight = 0f;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;

            // Сцена фигуры: её размер в пикселях задаётся из мировой пропорции, поэтому фигура не растягивается
            // при смене ступени компактности станции — она только масштабируется целиком.
            _stage = new VisualElement { name = "cn-figure__stage" };
            _stage.pickingMode = PickingMode.Ignore;
            _stage.generateVisualContent += OnPaint;
            _root.Add(_stage);

            _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        /// <summary>Элемент фигуры: кладётся в нишу станции.</summary>
        public VisualElement Root => _root;

        public void Dispose()
        {
            _root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _stage.generateVisualContent -= OnPaint;
            OnClick = null;
        }

        // ------------------------------------------------------------------ геометрия

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // Вписываем мировой прямоугольник в нишу по меньшей стороне и центрируем: родитель уже центрирует.
            float w = _root.contentRect.width;
            float h = _root.contentRect.height;
            if (w <= 0f || h <= 0f) return;

            _scale = Mathf.Min(w / WorldWidth, h / WorldHeight);
            if (_scale <= 0f) return;

            _stage.style.width = WorldWidth * _scale;
            _stage.style.height = WorldHeight * _scale;
            _stage.MarkDirtyRepaint();
        }

        /// <summary>Мировая точка (x вправо, y вверх от стоп) в пиксели сцены фигуры (y вниз).</summary>
        private Vector2 P(float x, float y) =>
            new Vector2((x + WorldWidth * 0.5f) * _scale, (WorldHeight - y) * _scale);

        private void OnPaint(MeshGenerationContext mgc)
        {
            if (_scale <= 0f) return;

            Painter2D p = mgc.painter2D;

            // Порядок — как в three.js: сначала то, что дальше от камеры.
            // Дым не рисуется (см. шапку файла).

            // ноги (128-132)
            Fill(p, Pant, () =>
            {
                Capsule(p, P(-0.13f, 0.08f), P(-0.13f, 0.64f), 0.085f * _scale);
                Capsule(p, P(0.13f, 0.08f), P(0.13f, 0.64f), 0.085f * _scale);
            });

            // обувь (134-138): коробка 0.17 × 0.10
            Fill(p, Trim, () =>
            {
                RoundRect(p, -0.215f, 0.00f, 0.17f, 0.10f, 0.02f);
                RoundRect(p, 0.045f, 0.00f, 0.17f, 0.10f, 0.02f);
            });

            // плащ (144-148): трапеция 0.35…1.13, снизу ±0.34, сверху ±0.20
            Fill(p, Cloak, () =>
            {
                p.BeginPath();
                p.MoveTo(P(-0.34f, 0.35f));
                p.LineTo(P(0.34f, 0.35f));
                p.LineTo(P(0.20f, 1.13f));
                p.LineTo(P(-0.20f, 1.13f));
                p.ClosePath();
            });

            // торс (140-142): капсула r 0.17, ось 0.52…1.20
            Fill(p, Cloak, () => Capsule(p, P(0f, 0.52f), P(0f, 1.20f), 0.17f * _scale));

            // руки (155-161): наклон ±0.32 рад, ось 0.61…1.03
            Fill(p, Cloak, () =>
            {
                Arm(p, -1f);
                Arm(p, 1f);
            });

            // плечи (150-154): сферы r 0.075 в (±0.21, 1.05)
            Fill(p, Trim, () =>
            {
                RoundRect(p, -0.285f, 0.975f, 0.15f, 0.15f, 0.075f);
                RoundRect(p, 0.135f, 0.975f, 0.15f, 0.15f, 0.075f);
            });

            // шея (163-164)
            Fill(p, Skin, () => RoundRect(p, -0.05f, 1.095f, 0.10f, 0.07f, 0.02f));

            // голова (165-168): 0.23 × 0.258, макушка 1.369
            Fill(p, Skin, () => RoundRect(p, -0.115f, 1.111f, 0.23f, 0.258f, 0.115f));

            // пояс (175-178) и пряжки (170-174)
            Fill(p, Brass, () => Capsule(p, P(-0.20f, 0.66f), P(0.20f, 0.66f), 0.014f * _scale));
            Fill(p, Brass, () =>
            {
                RoundRect(p, -0.0275f, 0.906f, 0.055f, 0.028f, 0.006f);
                RoundRect(p, -0.0275f, 0.766f, 0.055f, 0.028f, 0.006f);
                RoundRect(p, -0.0275f, 0.626f, 0.055f, 0.028f, 0.006f);
            });

            // мезиевые акценты (179-185): плечи и ядро на груди
            Fill(p, Mez, () =>
            {
                RoundRect(p, -0.238f, 1.052f, 0.056f, 0.056f, 0.028f);
                RoundRect(p, 0.182f, 1.052f, 0.056f, 0.056f, 0.028f);
                RoundRect(p, -0.034f, 0.946f, 0.068f, 0.068f, 0.034f);
            });
        }

        /// <summary>Рука в A-позе: капсула, повёрнутая от вертикали на 0.32 рад в сторону sign.</summary>
        private void Arm(Painter2D p, float sign)
        {
            const float tilt = 0.32f;
            var bottom = new Vector2(sign * 0.28f - sign * Mathf.Sin(tilt) * 0.21f, 0.61f);
            var top = new Vector2(sign * 0.28f + sign * Mathf.Sin(tilt) * 0.21f, 1.03f);
            Capsule(p, P(bottom.x, bottom.y), P(top.x, top.y), 0.055f * _scale);
        }

        /// <summary>Капсула между двумя мировыми точками: две прямые и два скруглённых торца.</summary>
        private void Capsule(Painter2D p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 axis = b - a;
            float len = axis.magnitude;
            Vector2 dir = len > 0.0001f ? axis / len : new Vector2(0f, 1f);
            var normal = new Vector2(-dir.y, dir.x) * radius;

            p.BeginPath();
            p.MoveTo(a + normal);
            p.LineTo(b + normal);
            p.QuadraticCurveTo(b + dir * radius, b - normal);
            p.LineTo(a - normal);
            p.QuadraticCurveTo(a - dir * radius, a + normal);
            p.ClosePath();
        }

        /// <summary>Прямоугольник со скруглёнными углами в мировых координатах.</summary>
        private void RoundRect(Painter2D p, float x, float y, float width, float height, float radius)
        {
            Vector2 tl = P(x, y + height);
            Vector2 br = P(x + width, y);
            float r = Mathf.Min(radius * _scale, Mathf.Min(br.x - tl.x, br.y - tl.y) * 0.5f);

            p.BeginPath();
            p.MoveTo(new Vector2(tl.x + r, tl.y));
            p.LineTo(new Vector2(br.x - r, tl.y));
            p.QuadraticCurveTo(new Vector2(br.x, tl.y), new Vector2(br.x, tl.y + r));
            p.LineTo(new Vector2(br.x, br.y - r));
            p.QuadraticCurveTo(new Vector2(br.x, br.y), new Vector2(br.x - r, br.y));
            p.LineTo(new Vector2(tl.x + r, br.y));
            p.QuadraticCurveTo(new Vector2(tl.x, br.y), new Vector2(tl.x, br.y - r));
            p.LineTo(new Vector2(tl.x, tl.y + r));
            p.QuadraticCurveTo(new Vector2(tl.x, tl.y), new Vector2(tl.x + r, tl.y));
            p.ClosePath();
        }

        /// <summary>Один цвет на все пути, собранные внутри <paramref name="path"/>.</summary>
        private static void Fill(Painter2D p, Color color, Action path)
        {
            p.fillColor = color;
            path();
            p.Fill();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0) return;
            OnClick?.Invoke();
        }

        private static Color Hex(string hex) => ProjectCMainMenu.ProjectCMainMenuPalette.Hex(hex);
    }
}
