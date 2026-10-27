// ProjectC Main Menu — letters turning into smoke, ported 1:1 from the mod's
// src/smoke.js (createTextBlower).
//
// Step 5 of docs/UI/MainMenu/02_smoke_menu_port.md. `Blow(host)` copies the
// label into an absolutely positioned ghost layer at exactly its place on
// screen, splits that copy into single letters (keeping words unbreakable) and
// carries every letter off sideways at its own speed, blurring it into smoke as
// it goes. The copy is independent of the live element, so the original may
// fade, blur or be hidden by its own assembly value at the same time — the
// letters still read as flying away.
//
// Where the DOM version cannot be copied literally, and what stands in:
//   * WAAPI keyframes → the same three author-keyframes kept as data and
//     evaluated from the wall clock (see ProjectCMainMenuSmokeField);
//   * `cloneNode(true)` → for `whole` hosts a text-only clone (a row of labels
//     with each child's text and text style), because a UI Toolkit element tree
//     cannot be copied with its styles;
//   * the mod clears the live text while its copy flies. Here the live label is
//     only made invisible (opacity 0) and restored afterwards, so the layout
//     never shifts for the parts that sit around it.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed class ProjectCMainMenuTextBlower
    {
        private struct GlyphKey
        {
            public float Offset;
            public float X;
            public float Y;
            public float Rot;
            public float Scale;
            public float Opacity;
            public float Blur;
        }

        private sealed class Piece
        {
            public VisualElement El;
            public double StartMs;     // delay before this piece starts moving
            public double DurationMs;
            public GlyphKey[] Keys;
        }

        private sealed class Flight
        {
            public VisualElement Host;
            public VisualElement Ghost;
            public List<Piece> Pieces = new List<Piece>();
            public List<KeyValuePair<VisualElement, int>> PendingPuffs =
                new List<KeyValuePair<VisualElement, int>>();
            public Action<Rect, int> OnPuff;
            public double Start;        // wall clock when Blow was called
            public double TotalMs;      // delay + window + 170
            public bool HostHidden;
        }

        // ui.js: createTextBlower(ui, { seed: 5573, max: 150 }); smoke.js floors the
        // queue at 8 and defaults it to 160 when no limit is given.
        public const int DefaultSeed = 5573;
        public const int DefaultMax = 150;

        private const float EaseX1 = 0.22f;
        private const float EaseY1 = 0.62f;
        private const float EaseX2 = 0.3f;

        private readonly VisualElement _layer;
        private readonly int _limit;
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly Dictionary<VisualElement, Flight> _byHost = new Dictionary<VisualElement, Flight>();
        private ProjectCMainMenuPalette.Mulberry32 _rng;
        private IVisualElementScheduledItem _ticker;

        public ProjectCMainMenuTextBlower(VisualElement host, int seed = DefaultSeed, int max = DefaultMax)
        {
            _limit = Mathf.Max(8, max);
            _rng = new ProjectCMainMenuPalette.Mulberry32(seed);

            _layer = new VisualElement { name = "pc-ghost-layer", pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("pc-ghost-layer");
            host.Add(_layer);
        }

        public VisualElement Node => _layer;

        // ------------------------------------------------------------------ blow

        /// <summary>
        /// The wind takes the letters of <paramref name="host"/> and returns how many
        /// pieces went into the air (0 when the host is not on screen or the layer is
        /// full). <paramref name="onPuff"/> is called with a piece's rect in panel
        /// pixels, so the caller can add smoke where the letters come apart.
        /// </summary>
        public int Blow(VisualElement host, int dir = 1, float stagger = 20f, float duration = 680f,
            float strength = 1f, int puffs = 2, bool whole = false, float delay = 0f, float spin = 1f,
            Action<Rect, int> onPuff = null)
        {
            if (host == null) return 0;

            var rect = host.worldBound;
            if (!(rect.width > 1f) || !(rect.height > 1f)) return 0;
            if (_layer.childCount >= _limit) return 0;

            if (_byHost.TryGetValue(host, out var running)) EndFlight(running);   // one flight per element at a time

            var wind = dir < 0 ? -1 : 1;
            var strengthClamped = ProjectCMainMenuPalette.Clamp(strength <= 0f ? 1f : strength, 0.25f, 2f);
            var durationClamped = Mathf.Max(180f, duration);
            var delayClamped = Mathf.Max(0f, delay);
            var staggerClamped = Mathf.Max(0f, stagger);
            var spinClamped = ProjectCMainMenuPalette.Clamp(spin, 0f, 2f);
            var maxPuffs = Mathf.Max(0, Mathf.RoundToInt(puffs));

            var ghost = BuildGhost(host, whole, out var pieces);
            ghost.AddToClassList("pc-ghost");
            var layerRect = _layer.worldBound;
            ghost.style.position = Position.Absolute;
            ghost.style.left = rect.x - layerRect.x;
            ghost.style.top = rect.y - layerRect.y;
            ghost.style.width = rect.width;
            ghost.style.height = rect.height;
            _layer.Add(ghost);

            // the live text steps aside while its copy flies; it comes back by itself
            host.style.opacity = 0f;

            var n = pieces.Count;
            // One wind window for the whole label: the first letter leaves at `delay`
            // and EVERY letter lands when the window closes, so the letters, the puffs
            // and the cloud gust (the window hands them the same duration) finish together.
            var window = Mathf.Max(220f, durationClamped);
            var span = Mathf.Min(Mathf.Max(0, n - 1) * staggerClamped, window * 0.45f);
            var stepDelay = n > 1 ? span / (n - 1) : 0f;

            var flight = new Flight
            {
                Host = host,
                Ghost = ghost,
                OnPuff = onPuff,
                Start = Clock(),
                TotalMs = delayClamped + window + 170f,
                HostHidden = true,
            };

            // puffs where the letters come apart: a few of them, not one per letter
            var every = maxPuffs > 0 ? Mathf.Max(1, Mathf.CeilToInt(n / (float)maxPuffs)) : 0;

            for (var i = 0; i < n; i++)
            {
                var r1 = Rand(0.3f, 1f);
                var r2 = Rand(0f, 1f);
                var r3 = Rand(0f, 1f);

                var dx = wind * (22f + r1 * 104f) * strengthClamped;
                var dy = -(5f + r2 * 42f) * strengthClamped;
                var rot = (r3 - 0.5f) * 46f * spinClamped;
                var grow = 1f + 0.22f * r1;
                var growEnd = 1.35f + 0.5f * r3;
                var at = delayClamped + i * stepDelay;
                var spanMs = Mathf.Max(140f, window - i * stepDelay);

                var piece = new Piece
                {
                    El = pieces[i],
                    StartMs = at,
                    DurationMs = spanMs,
                    Keys = new[]
                    {
                        new GlyphKey { Offset = 0f, X = 0f, Y = 0f, Rot = 0f, Scale = 1f, Opacity = 1f, Blur = 0f },
                        new GlyphKey
                        {
                            Offset = 0.42f, X = dx * 0.42f, Y = dy * 0.55f, Rot = rot * 0.4f,
                            Scale = grow, Opacity = 0.86f, Blur = 1.6f,
                        },
                        new GlyphKey
                        {
                            Offset = 1f, X = dx, Y = dy, Rot = rot, Scale = growEnd,
                            Opacity = 0f, Blur = 7f,
                        },
                    },
                };

                flight.Pieces.Add(piece);

                if (every > 0 && onPuff != null && i % every == 0)
                {
                    // the rect of a freshly built element is not laid out yet, so the
                    // puff is handed over on the first tick that has one
                    flight.PendingPuffs.Add(new KeyValuePair<VisualElement, int>(pieces[i], i));
                }
            }

            _flights.Add(flight);
            _byHost[host] = flight;
            EnsureTicker();
            return n;
        }

        public int Count() => _layer.childCount;

        /// <summary>Ends every flight that is still in the air.</summary>
        public void RestoreAll()
        {
            for (var i = _flights.Count - 1; i >= 0; i--) EndFlight(_flights[i]);
        }

        /// <summary>
        /// Brings back any text that is still flying inside <paramref name="root"/> — a
        /// window that reopens before its last flight has landed must not come up empty.
        /// </summary>
        public int RestoreWithin(VisualElement root)
        {
            if (root == null) return 0;

            var count = 0;
            for (var i = _flights.Count - 1; i >= 0; i--)
            {
                var host = _flights[i].Host;
                if (host == root || root.Contains(host))
                {
                    EndFlight(_flights[i]);
                    count++;
                }
            }

            return count;
        }

        public void Clear()
        {
            RestoreAll();
            _layer.Clear();
        }

        // ---------------------------------------------------------------- driver

        /// <summary>Advances every flight. Safe to call at any rate: values come from the clock.</summary>
        public void Tick() => Tick(Clock());

        public void Tick(double now)
        {
            for (var i = _flights.Count - 1; i >= 0; i--)
            {
                var flight = _flights[i];
                var elapsedMs = (now - flight.Start) * 1000.0;

                DispatchPuffs(flight);
                Animate(flight, elapsedMs);

                if (elapsedMs >= flight.TotalMs)
                {
                    EndFlight(flight);
                }
            }

            if (_flights.Count == 0) _ticker?.Pause();   // idle costs nothing
        }

        private static void Animate(Flight flight, double elapsedMs)
        {
            for (var p = 0; p < flight.Pieces.Count; p++)
            {
                var piece = flight.Pieces[p];
                var local = (elapsedMs - piece.StartMs) / piece.DurationMs;

                if (local < 0.0) continue;   // its own delay has not expired: still readable

                var t = local >= 1.0 ? 1f : (float)local;
                var eased = ProjectCMainMenuPalette.CubicBezier(EaseX1, EaseY1, EaseX2, 1f, t);
                Sample(piece.Keys, eased, out var x, out var y, out var rot, out var scale,
                    out var opacity, out var blur);

                var style = piece.El.style;
                style.translate = new Translate(Length.Pixels(x), Length.Pixels(y));
                style.rotate = new Rotate(new Angle(rot, AngleUnit.Degree));
                style.scale = new Scale(new Vector2(scale, scale));
                style.opacity = opacity;
                style.filter = Blur(blur);
            }
        }

        private static void DispatchPuffs(Flight flight)
        {
            if (flight.PendingPuffs.Count == 0) return;

            for (var i = flight.PendingPuffs.Count - 1; i >= 0; i--)
            {
                var piece = flight.PendingPuffs[i].Key;
                var rect = piece.worldBound;
                if (!(rect.width > 0.5f)) continue;   // not laid out yet: try again next tick

                var index = flight.PendingPuffs[i].Value;
                flight.PendingPuffs.RemoveAt(i);
                try
                {
                    flight.OnPuff?.Invoke(rect, index);
                }
                catch (Exception)
                {
                    // a missing puff must never break the transition
                }
            }
        }

        // ---------------------------------------------------------------- ghosts

        private VisualElement BuildGhost(VisualElement host, bool whole, out List<VisualElement> pieces)
        {
            pieces = new List<VisualElement>();

            var ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            CopyFlex(host, ghost);

            // The mod clones the element, so a host with element children (the
            // wordmark: 'PROJECT' + a coloured 'C') flies as ONE piece; a plain text
            // host is split into words and letters.
            var split = !whole && host.childCount == 0 && host is TextElement textHost &&
                        !string.IsNullOrWhiteSpace(textHost.text);

            if (split)
            {
                // the mod's copy is a div of inline spans: words in a row, wrapping
                ghost.style.flexDirection = FlexDirection.Row;
                ghost.style.flexWrap = Wrap.Wrap;
                AlignFor(TextAlignmentOf(host), ghost);
                SplitInto((TextElement)host, ghost, pieces);
            }
            else
            {
                CopyTextOnly(host, ghost);
                pieces.Add(ghost);   // the copy flies as one piece
            }

            return ghost;
        }

        /// <summary>Keeps words unbreakable: each word is a row, each letter a label.</summary>
        private static void SplitInto(TextElement host, VisualElement ghost, List<VisualElement> pieces)
        {
            var text = host.text ?? string.Empty;
            var parts = SplitKeepingWhitespace(text);

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];

                if (IsWhitespace(part))
                {
                    // the mod keeps the text node between words: it is the gap, it
                    // does not fly. One space stands in for any run of whitespace.
                    var gap = new Label(" ") { pickingMode = PickingMode.Ignore };
                    gap.AddToClassList("pc-word");
                    CopyTextStyle(host, gap);
                    ghost.Add(gap);
                    continue;
                }

                var word = new VisualElement { pickingMode = PickingMode.Ignore };
                word.AddToClassList("pc-word");
                word.style.flexDirection = FlexDirection.Row;
                word.style.flexShrink = 0f;

                for (var c = 0; c < part.Length; c++)
                {
                    var glyph = new Label(part[c].ToString())
                    {
                        pickingMode = PickingMode.Ignore,
                    };
                    glyph.AddToClassList("pc-glyph");
                    CopyTextStyle(host, glyph);
                    word.Add(glyph);
                    pieces.Add(glyph);
                }

                ghost.Add(word);
            }
        }

        /// <summary>A text-only copy of the subtree — text, font size, spacing and colour.</summary>
        private static void CopyTextOnly(VisualElement source, VisualElement target)
        {
            for (var i = 0; i < source.childCount; i++)
            {
                var child = source[i];
                if (child is TextElement childText)
                {
                    var copy = new Label(childText.text) { pickingMode = PickingMode.Ignore };
                    CopyTextStyle(child, copy);
                    target.Add(copy);
                }
                else
                {
                    var copy = new VisualElement { pickingMode = PickingMode.Ignore };
                    CopyFlex(child, copy);
                    CopyTextOnly(child, copy);
                    target.Add(copy);
                }
            }
        }

        private static void CopyFlex(VisualElement from, VisualElement to)
        {
            if (from.panel == null) return;

            to.style.flexDirection = from.resolvedStyle.flexDirection;
            to.style.justifyContent = from.resolvedStyle.justifyContent;
            to.style.alignItems = from.resolvedStyle.alignItems;
            to.style.flexWrap = from.resolvedStyle.flexWrap;
        }

        private static TextAnchor TextAlignmentOf(VisualElement element)
        {
            return element.panel != null ? element.resolvedStyle.unityTextAlign : TextAnchor.MiddleLeft;
        }

        /// <summary>The ghost is a ROW: justifyContent is horizontal, alignItems vertical.</summary>
        private static void AlignFor(TextAnchor anchor, VisualElement to)
        {
            switch (anchor)
            {
                case TextAnchor.MiddleCenter:
                    to.style.alignItems = Align.Center;
                    to.style.justifyContent = Justify.Center;
                    break;
                case TextAnchor.MiddleRight:
                    to.style.alignItems = Align.Center;
                    to.style.justifyContent = Justify.FlexEnd;
                    break;
                case TextAnchor.MiddleLeft:
                    to.style.alignItems = Align.Center;
                    to.style.justifyContent = Justify.FlexStart;
                    break;
                case TextAnchor.UpperCenter:
                    to.style.alignItems = Align.FlexStart;
                    to.style.justifyContent = Justify.Center;
                    break;
                case TextAnchor.UpperRight:
                    to.style.alignItems = Align.FlexStart;
                    to.style.justifyContent = Justify.FlexEnd;
                    break;
                case TextAnchor.LowerCenter:
                    to.style.alignItems = Align.FlexEnd;
                    to.style.justifyContent = Justify.Center;
                    break;
                case TextAnchor.LowerRight:
                    to.style.alignItems = Align.FlexEnd;
                    to.style.justifyContent = Justify.FlexEnd;
                    break;
                default:
                    to.style.alignItems = Align.FlexStart;
                    to.style.justifyContent = Justify.FlexStart;
                    break;
            }
        }

        private static void CopyTextStyle(VisualElement from, TextElement to)
        {
            to.style.whiteSpace = WhiteSpace.NoWrap;

            if (from.panel != null)
            {
                var size = from.resolvedStyle.fontSize;
                if (!float.IsNaN(size) && size > 0f) to.style.fontSize = size;

                var spacing = from.resolvedStyle.letterSpacing;
                if (!float.IsNaN(spacing)) to.style.letterSpacing = spacing;

                var color = from.resolvedStyle.color;
                if (color.a > 0f) to.style.color = color;

                to.style.unityFontStyleAndWeight = from.resolvedStyle.unityFontStyleAndWeight;
                return;
            }

            // detached (tests): take whatever inline style was set explicitly
            var inlineSize = from.style.fontSize;
            if (inlineSize.keyword == StyleKeyword.Undefined && inlineSize.value.value > 0f)
                to.style.fontSize = inlineSize.value.value;

            var inlineColor = from.style.color;
            if (inlineColor.keyword == StyleKeyword.Undefined)
                to.style.color = inlineColor.value;
        }

        // ---------------------------------------------------------------- ending

        private void EndFlight(Flight flight)
        {
            _flights.Remove(flight);

            if (flight.Host != null && _byHost.TryGetValue(flight.Host, out var current) && current == flight)
            {
                _byHost.Remove(flight.Host);
            }

            if (flight.HostHidden && flight.Host != null)
            {
                flight.Host.style.opacity = StyleKeyword.Null;   // style.removeProperty('opacity')
            }

            flight.Ghost.RemoveFromHierarchy();
        }

        // --------------------------------------------------------------- helpers

        private static void Sample(GlyphKey[] keys, float p, out float x, out float y, out float rot,
            out float scale, out float opacity, out float blur)
        {
            var a = keys[0];
            for (var i = 1; i < keys.Length; i++)
            {
                var b = keys[i];
                if (p <= b.Offset)
                {
                    var span = b.Offset - a.Offset;
                    var k = span <= 1e-6f ? 0f : (p - a.Offset) / span;
                    x = Mathf.Lerp(a.X, b.X, k);
                    y = Mathf.Lerp(a.Y, b.Y, k);
                    rot = Mathf.Lerp(a.Rot, b.Rot, k);
                    scale = Mathf.Lerp(a.Scale, b.Scale, k);
                    opacity = Mathf.Lerp(a.Opacity, b.Opacity, k);
                    blur = Mathf.Lerp(a.Blur, b.Blur, k);
                    return;
                }

                a = b;
            }

            x = a.X;
            y = a.Y;
            rot = a.Rot;
            scale = a.Scale;
            opacity = a.Opacity;
            blur = a.Blur;
        }

        private static StyleList<FilterFunction> Blur(float px)
        {
            var blur = new FilterFunction(FilterFunctionType.Blur);
            blur.AddParameter(new FilterParameter(Mathf.Max(0f, px)));
            return new StyleList<FilterFunction>(new List<FilterFunction> { blur });
        }

        /// <summary>text.split(/(\s+)/) — words stay whole, whitespace is dropped.</summary>
        private static List<string> SplitKeepingWhitespace(string text)
        {
            var parts = new List<string>();
            if (string.IsNullOrEmpty(text)) return parts;

            var start = 0;
            var inSpace = IsSpace(text[0]);
            for (var i = 1; i < text.Length; i++)
            {
                var space = IsSpace(text[i]);
                if (space == inSpace) continue;

                parts.Add(text.Substring(start, i - start));
                start = i;
                inSpace = space;
            }

            parts.Add(text.Substring(start));
            return parts;
        }

        private static bool IsSpace(char c) => char.IsWhiteSpace(c);

        private static bool IsWhitespace(string s)
        {
            for (var i = 0; i < s.Length; i++)
            {
                if (!IsSpace(s[i])) return false;
            }

            return s.Length > 0;
        }

        private void EnsureTicker()
        {
            if (_layer.panel == null) return;   // attached later: the first blow after mounting starts it
            if (_ticker == null) _ticker = _layer.schedule.Execute(Tick).Every(16);
            _ticker.Resume();
        }

        private float Rand(float a, float b) => a + (b - a) * _rng.Next();

        private static double Clock() => Time.realtimeSinceStartupAsDouble;
    }
}
