// ProjectC Main Menu — the altitude tape beside the buttons, ported 1:1 from the
// mod's src/altimeter.js.
//
// Step 6 of docs/UI/MainMenu/02_smoke_menu_port.md. A real altimeter is a
// vertical tape of thin graduations and small wide-tracked numbers that slides
// past a fixed index. Here the tape stands opposite the menu buttons and the
// four entries are pinned to their heights: «Выход» 800 m, «Настройки» 1200 m,
// «Подключиться к серверу» 2200 m, «Одиночная игра» 3000 m. The metre spacing
// inside each gap follows the buttons, so every number lands exactly opposite
// its button while the ladder stays even within a gap.
//
// The tape also turns a little with the pointer (a small damped scroll plus a
// whisper of rotation, like a drum altimeter) and, when a button is hovered, a
// soft gas-green band and a value readout glide to that height.
//
// Platform differences, all forced:
//   * the host, tape, band and readout are built by ProjectCMainMenuWindow
//     (step 3) so the layout is final before the tape arrives; this class finds
//     them by class and only creates what is missing;
//   * the mod's `.pc-alt-edge` div has no rule in ui.css and is never read — it
//     is dead code there and is not ported;
//   * `ResizeObserver` + `resize` + the 8-frame `requestAnimationFrame` watch
//     become one GeometryChangedEvent callback plus a short relayout watch,
//     because UI Toolkit has neither of those events;
//   * the mod writes one combined `transform`; here translate and rotate are
//     written separately, which is what those longhands compile to anyway.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed class ProjectCMainMenuAltimeter
    {
        public struct Mark
        {
            public int Value;
            public VisualElement El;
        }

        /// <summary>Plain-data snapshot for playtests: where every value sits right now.</summary>
        public struct MarkSnapshot
        {
            public int Value;
            public string Text;
            public bool Anchor;
            public float ButtonY;
            public float? LabelY;
            public float? Delta;
        }

        public struct Snapshot
        {
            public float Scroll;
            public int? Highlight;
            public List<MarkSnapshot> Labels;
            public int Anchors;
            public int Height;
            public float PxPerMeter;
        }

        public struct State
        {
            public float Scroll;
            public int? Highlight;
            public int Anchors;
            public int Height;
        }

        private const int Step = 100;      // one graduation every 100 m
        private const int LabelEvery = 500; // a small number every 500 m
        private const float ScrollPixels = 13f; // how far the tape slides over the full pointer sweep
        private const float Tilt = 0.022f;      // degrees of rotation per scrolled pixel
        private const float LabelGap = 15f;     // a small number needs this much room to be shown

        private struct Anchor
        {
            public int Value;
            public float Y;
        }

        private sealed class Tick
        {
            public int Alt;
            public VisualElement Row;
            public VisualElement Num;     // pc-alt-num: the Label itself, or a row holding digits + unit
            public Label Number;          // the digits
            public Label Unit;            // «м» on anchors only
            public bool IsAnchor;
        }

        private readonly VisualElement _host;
        private readonly VisualElement _tape;
        private readonly VisualElement _band;
        private readonly Label _readout;
        private readonly List<Mark> _marks;
        private readonly List<Tick> _ticks = new List<Tick>();
        private readonly List<Anchor> _anchors = new List<Anchor>();

        private float _hostHeight = 1f;
        private float _scroll;
        private int? _hoverValue;
        private IVisualElementScheduledItem _watch;
        private int _watchFrames;

        public ProjectCMainMenuAltimeter(VisualElement host, IList<Mark> marks)
        {
            _host = host;
            _host.AddToClassList("pc-alt");

            _marks = new List<Mark>(marks ?? new List<Mark>());

            _tape = FindOrCreate(host, "pc-alt-tape");
            _band = FindOrCreate(host, "pc-alt-band");
            _readout = FindOrCreate(host, "pc-alt-readout") as Label ?? new Label();
            if (_readout.parent != host)
            {
                host.Add(_readout);
            }

            BuildTicks();

            host.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            // the buttons settle a few frames after the first paint (fonts,
            // wrapping) — the mod watches eight frames and re-measures
            _watchFrames = 8;
            _watch = host.schedule.Execute(WatchFrame).Every(16);
        }

        public VisualElement Host => _host;
        public int TickCount => _ticks.Count;
        public int LabelCount { get; private set; }
        public State CurrentState => new State
        {
            Scroll = Mathf.Round(_scroll * 100f) / 100f,
            Highlight = _hoverValue,
            Anchors = _anchors.Count,
            Height = Mathf.RoundToInt(_hostHeight),
        };

        // ---------------------------------------------------------------- ticks

        private void BuildTicks()
        {
            var values = new List<int>(_marks.Count);
            for (var i = 0; i < _marks.Count; i++) values.Add(_marks[i].Value);

            var min = values.Count > 0 ? Mathf.Min(values.ToArray()) : 0;
            var max = values.Count > 0 ? Mathf.Max(values.ToArray()) : 0;
            var lo = Mathf.Max(0, Mathf.RoundToInt((min - 300f) / Step) * Step);
            var hi = Mathf.RoundToInt((max + 300f) / Step) * Step;

            var anchorValues = new HashSet<int>(values);

            for (var alt = lo; alt <= hi; alt += Step)
            {
                var isAnchor = anchorValues.Contains(alt);
                var isLabel = isAnchor || alt % LabelEvery == 0;

                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("pc-alt-tick");
                if (isAnchor) row.AddToClassList("is-anchor");
                else if (alt % LabelEvery == 0) row.AddToClassList("is-mid");

                var tick = new Tick { Alt = alt, Row = row, IsAnchor = isAnchor };

                if (isLabel)
                {
                    var number = new Label(alt.ToString()) { pickingMode = PickingMode.Ignore };
                    tick.Number = number;

                    if (isAnchor)
                    {
                        // the mod nests <em class="pc-alt-unit">м</em> inside the number
                        // span; a UI Toolkit Label cannot host a child, so the number
                        // becomes a row of digits + unit
                        var num = new VisualElement { pickingMode = PickingMode.Ignore };
                        num.AddToClassList("pc-alt-num");
                        num.style.flexDirection = FlexDirection.Row;
                        num.style.alignItems = Align.Center;
                        num.style.justifyContent = Justify.FlexEnd;

                        var unit = new Label("м") { pickingMode = PickingMode.Ignore };
                        unit.AddToClassList("pc-alt-unit");

                        num.Add(number);
                        num.Add(unit);
                        tick.Num = num;
                        tick.Unit = unit;
                    }
                    else
                    {
                        number.AddToClassList("pc-alt-num");
                        tick.Num = number;
                    }

                    row.Add(tick.Num);
                }

                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("pc-alt-line");
                row.Add(line);

                _tape.Add(row);
                _ticks.Add(tick);
            }
        }

        // --------------------------------------------------------------- layout

        /// <summary>Re-measures the host and the four buttons and re-places the tape.</summary>
        public void Relayout()
        {
            var hostRect = _host.worldBound;
            _hostHeight = Mathf.Max(1f, hostRect.height);

            var points = new List<Anchor>(_marks.Count);
            for (var i = 0; i < _marks.Count; i++)
            {
                var el = _marks[i].El;
                if (el == null) continue;

                var rect = el.worldBound;
                if (!(rect.height > 1f)) continue;

                points.Add(new Anchor
                {
                    Value = _marks[i].Value,
                    Y = rect.y + rect.height * 0.5f - hostRect.y,
                });
            }

            points.Sort((a, b) => b.Value.CompareTo(a.Value));   // descending, like the tape
            _anchors.Clear();
            _anchors.AddRange(points);

            for (var i = 0; i < _ticks.Count; i++)
            {
                _ticks[i].Row.style.top = YOf(_ticks[i].Alt);
            }

            // where two gaps squeeze the same 200 m into a few pixels, the small
            // numbers would collide: keep only those that still have room
            var lastShown = -1e9f;
            var shown = 0;
            for (var i = _ticks.Count - 1; i >= 0; i--)
            {
                var tick = _ticks[i];
                if (tick.Num == null) continue;

                var y = YOf(tick.Alt);
                var show = tick.IsAnchor || y - lastShown >= LabelGap;
                tick.Num.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show)
                {
                    lastShown = y;
                    shown++;
                    SyncNumberStyle(tick);
                }
            }

            LabelCount = shown;
            PositionBand();
        }

        /// <summary>Metres per pixel inside a gap between two anchors; a.Value &gt; b.Value.</summary>
        private static float Slope(Anchor a, Anchor b)
        {
            var span = a.Value - b.Value;
            return span > 1e-6f ? (b.Y - a.Y) / span : 0.05f;
        }

        private float YOf(int alt)
        {
            var n = _anchors.Count;
            if (n == 0) return _hostHeight * 0.5f;
            if (n == 1) return _anchors[0].Y;

            var first = _anchors[0];
            var last = _anchors[n - 1];

            if (alt >= first.Value) return first.Y - (alt - first.Value) * Slope(first, _anchors[1]);
            if (alt <= last.Value) return last.Y + (last.Value - alt) * Slope(_anchors[n - 2], last);

            for (var i = 0; i < n - 1; i++)
            {
                var a = _anchors[i];
                var b = _anchors[i + 1];
                if (alt <= a.Value && alt >= b.Value)
                {
                    return a.Y + (a.Value - alt) * Slope(a, b);
                }
            }

            return last.Y;
        }

        private void PositionBand()
        {
            var y = _hoverValue == null ? 0f : YOf(_hoverValue.Value) + _scroll;
            _band.style.translate = new Translate(0f, Length.Pixels(y));
        }

        /// <summary>letter-spacing does not inherit into a container, so the digits get it.</summary>
        private static void SyncNumberStyle(Tick tick)
        {
            if (tick.Unit == null || tick.Number == null) return;

            var spacing = tick.Num.resolvedStyle.letterSpacing;
            if (float.IsNaN(spacing)) return;

            tick.Number.style.letterSpacing = spacing;
            tick.Unit.style.letterSpacing = 0f;
        }

        // --------------------------------------------------------------- update

        public void Update(float dt, float pointerY = 0.5f, int? hoverValue = null)
        {
            var step = Mathf.Clamp(dt, 0f, 0.1f);
            var pointer = ProjectCMainMenuPalette.Clamp(pointerY, 0f, 1f);
            var scrollTarget = (pointer - 0.5f) * 2f * ScrollPixels;

            var next = ProjectCMainMenuPalette.Damp(_scroll, scrollTarget, 6.2f, step);
            var moved = Mathf.Abs(next - _scroll) > 0.004f;
            _scroll = next;

            if (moved)
            {
                _tape.style.translate = new Translate(0f, Length.Pixels(_scroll));
                _tape.style.rotate = new Rotate(new Angle(_scroll * Tilt, AngleUnit.Degree));
            }

            var want = hoverValue;
            if (want != _hoverValue)
            {
                _hoverValue = want;
                _readout.text = want == null ? string.Empty : want.Value + " м";
                _band.EnableInClassList("is-on", want != null);
                _readout.EnableInClassList("is-on", want != null);
                PositionBand();
            }
            else if (want != null && moved)
            {
                PositionBand();
            }
        }

        // -------------------------------------------------------------- snapshot

        public Snapshot TakeSnapshot()
        {
            var labels = new List<MarkSnapshot>(_marks.Count);
            for (var i = 0; i < _marks.Count; i++)
            {
                var value = _marks[i].Value;
                var tick = FindTick(value);
                var buttonY = _marks[i].El != null
                    ? _marks[i].El.worldBound.y + _marks[i].El.worldBound.height * 0.5f
                    : 0f;

                float? labelY = null;
                float? delta = null;
                string text = string.Empty;

                if (tick != null && tick.Num != null && tick.Number != null)
                {
                    var r = tick.Num.worldBound;
                    labelY = Mathf.Round((r.y + r.height * 0.5f) * 10f) / 10f;
                    delta = Mathf.Round((labelY.Value - buttonY) * 10f) / 10f;
                    text = tick.Number.text + (tick.IsAnchor ? "м" : string.Empty);
                }

                labels.Add(new MarkSnapshot
                {
                    Value = value,
                    Text = text,
                    Anchor = tick != null && tick.Num != null,
                    ButtonY = Mathf.Round(buttonY * 10f) / 10f,
                    LabelY = labelY,
                    Delta = delta,
                });
            }

            var a = _anchors.Count > 0 ? _anchors[0] : new Anchor { Value = 1, Y = 0f };
            var b = _anchors.Count > 1 ? _anchors[1] : new Anchor { Value = 0, Y = 1f };

            return new Snapshot
            {
                Scroll = Mathf.Round(_scroll * 100f) / 100f,
                Highlight = _hoverValue,
                Labels = labels,
                Anchors = _anchors.Count,
                Height = Mathf.RoundToInt(_hostHeight),
                PxPerMeter = Mathf.Round(Slope(a, b) * 10000f) / 10000f,
            };
        }

        public void Dispose()
        {
            _host.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _watch?.Pause();
            _watch = null;
            _host.Clear();
        }

        // --------------------------------------------------------------- wiring

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            Relayout();
        }

        private void WatchFrame()
        {
            Relayout();
            _watchFrames--;
            if (_watchFrames > 0) return;

            _watch?.Pause();
            _watch = null;
        }

        private Tick FindTick(int value)
        {
            for (var i = 0; i < _ticks.Count; i++)
            {
                if (_ticks[i].Alt == value) return _ticks[i];
            }

            return null;
        }

        private static VisualElement FindOrCreate(VisualElement host, string className)
        {
            var found = host.Q(className: className);
            if (found != null) return found;

            var created = new VisualElement { pickingMode = PickingMode.Ignore };
            created.AddToClassList(className);
            host.Add(created);
            return created;
        }
    }
}
