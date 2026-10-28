// ProjectC Main Menu — the smoke field, ported 1:1 from the mod's src/smoke.js
// (createSmokeField).
//
// Step 4 of docs/UI/MainMenu/02_smoke_menu_port.md. The interface does not fade:
// parts gather out of drifting smoke and blow apart into it again. Every part
// that appears asks for `Gather`, every part that leaves asks for `Disperse`,
// and the puffs stream sideways across the frame (never towards the camera).
//
// Two differences from the DOM version, both forced by the platform:
//   * WAAPI keyframes do not exist here. A puff keeps its three keyframes as
//     data and is evaluated from the wall clock, so a late tick shows the value
//     the web version would show at that moment instead of drifting;
//   * the field drives itself with a paused-until-needed UI Toolkit scheduler.
//     `Tick()` stays public for tests, and calling it from outside is harmless
//     because every value is derived from the clock, never accumulated.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    public sealed class ProjectCMainMenuSmokeField
    {
        /// <summary>Where the last gust went — ui.js exposes this for playtests.</summary>
        public struct Flow
        {
            public string Mode;
            public int Dir;
            public float Dx;
            public float Dy;

            public override string ToString() => $"{Mode} dir={Dir} dx={Dx:F1} dy={Dy:F1}";
        }

        private struct Key
        {
            public float Offset;
            public float X;
            public float Y;
            public float Scale;
            public float Opacity;
        }

        private sealed class Puff
        {
            public VisualElement El;
            public double Start;      // wall clock, seconds: when the delay begins
            public double Delay;      // seconds
            public double Duration;   // seconds
            public Key[] Keys;
        }

        // Defaults are ui.js's: seed 9137, at most 96 puffs alive at once.
        public const int DefaultSeed = 9137;
        public const int DefaultMax = 96;

        // The pale sheet (tint 233,242,236) is a near-white mist. In the browser it drifts
        // over the bright sky and reads as haze; here a press bursts it over the charcoal
        // button cloud and the dark menu scrim, where the authored peak (~0.55) lands at
        // ~0.57 sRGB core and flashes white. Every menu button sits below 0.44 of the view
        // height, so a press is 13/13 pale puffs (measured at 1920x1089: pale 13, charcoal 0,
        // up to 149 px). Pale puffs therefore draw at this fraction of their authored peak,
        // which puts the same core at ~0.43 sRGB — translucent smoke instead of a white blob.
        public const float PalePeak = 0.5f;

        private readonly VisualElement _field;
        private readonly List<Puff> _puffs = new List<Puff>();
        private readonly int _limit;
        private ProjectCMainMenuPalette.Mulberry32 _rng;
        private IVisualElementScheduledItem _ticker;
        private Flow _lastFlow = new Flow { Mode = "idle", Dir = 1 };

        public ProjectCMainMenuSmokeField(VisualElement host, int seed = DefaultSeed, int max = DefaultMax)
        {
            _limit = Mathf.Max(16, max);
            _rng = new ProjectCMainMenuPalette.Mulberry32(seed);

            _field = new VisualElement { name = "pc-smoke-field", pickingMode = PickingMode.Ignore };
            _field.AddToClassList("pc-smoke-field");
            host.Add(_field);
        }

        public VisualElement Node => _field;

        // ---------------------------------------------------------------- gusts

        /// <summary>
        /// Puffs stream in from upwind and settle into the shape as it condenses.
        /// <paramref name="rect"/> is in panel pixels — the same space the field's
        /// children are positioned in (the field is inset 0 in the same root).
        /// </summary>
        public void Gather(Rect rect, int count = 10, float strength = 1f, float spread = 1f,
            float duration = 620f, float delay = 0f, int dir = 1)
        {
            var wind = dir < 0 ? -1 : 1;
            var b = Bounds(rect);
            var n = Mathf.Max(2, Mathf.RoundToInt(count));
            var strengthClamped = ProjectCMainMenuPalette.Clamp(strength <= 0f ? 1f : strength, 0.25f, 2f);
            var travel = (b.Width * 0.55f + 130f) * spread;
            var peak = ProjectCMainMenuPalette.Clamp(0.46f * strengthClamped, 0.05f, 0.8f);
            var baseSize = ProjectCMainMenuPalette.Clamp(Mathf.Min(b.Width, b.Height) * 0.42f, 26f, 96f);
            var viewH = ViewHeight();

            var sumX = 0f;
            var sumY = 0f;

            for (var i = 0; i < n; i++)
            {
                // where this puff lands on the shape, and where the wind starts it
                var t = (i + 0.5f) / n;
                var ix = b.Cx + (t - 0.5f) * b.Width * 0.9f + Rand(-0.08f, 0.08f) * b.Width;
                var iy = b.Cy + Rand(-0.3f, 0.3f) * b.Height;
                var back = travel * Rand(0.55f, 1.3f);
                var sx = ix - wind * back;
                var sy = iy + Rand(-0.4f, 0.4f) * b.Height * 1.5f;
                var size = (baseSize + Rand(20f, 120f)) * Rand(0.75f, 1.3f);
                var dx = wind * back;
                var dy = (iy - sy) * 0.9f;
                sumX += dx;
                sumY += dy;
                var mid = Rand(0.82f, 1.18f);

                AddPuff(sx, sy, size, sy > viewH * 0.44f, duration, delay + Rand(0f, 70f), new[]
                {
                    new Key { Offset = 0f, X = 0f, Y = 0f, Scale = 1.2f, Opacity = 0f },
                    new Key { Offset = 0.45f, X = dx * 0.45f, Y = dy * 0.45f, Scale = mid, Opacity = peak },
                    new Key { Offset = 1f, X = dx, Y = dy, Scale = 0.34f, Opacity = 0f },
                });
            }

            _lastFlow = new Flow { Mode = "gather", Dir = wind, Dx = sumX / n, Dy = sumY / n };
        }

        /// <summary>Puffs start on the shape and are carried off downwind as it falls apart.</summary>
        public void Disperse(Rect rect, int count = 10, float strength = 1f, float spread = 1f,
            float duration = 640f, float delay = 0f, int dir = 1)
        {
            var wind = dir < 0 ? -1 : 1;
            var b = Bounds(rect);
            var n = Mathf.Max(2, Mathf.RoundToInt(count));
            var strengthClamped = ProjectCMainMenuPalette.Clamp(strength <= 0f ? 1f : strength, 0.25f, 2f);
            var travel = (b.Width * 0.6f + 150f) * spread;
            var peak = ProjectCMainMenuPalette.Clamp(0.5f * strengthClamped, 0.06f, 0.85f);
            var baseSize = ProjectCMainMenuPalette.Clamp(Mathf.Min(b.Width, b.Height) * 0.42f, 26f, 100f);
            var viewH = ViewHeight();

            var sumX = 0f;
            var sumY = 0f;

            for (var i = 0; i < n; i++)
            {
                var t = (i + 0.5f) / n;
                var sx = b.Cx + (t - 0.5f) * b.Width * 0.85f + Rand(-0.1f, 0.1f) * b.Width;
                var sy = b.Cy + Rand(-0.4f, 0.4f) * b.Height;
                var size = (baseSize + Rand(18f, 110f)) * Rand(0.7f, 1.25f);
                var dx = wind * travel * Rand(0.7f, 1.5f);
                var dy = Rand(-0.32f, 0.55f) * b.Height * 1.8f;
                sumX += dx;
                sumY += dy;
                var grow = Rand(1.5f, 2.2f);

                AddPuff(sx, sy, size, sy > viewH * 0.44f, duration, delay + Rand(0f, 90f), new[]
                {
                    new Key { Offset = 0f, X = 0f, Y = 0f, Scale = 0.55f, Opacity = 0f },
                    new Key { Offset = 0.32f, X = dx * 0.38f, Y = dy * 0.38f, Scale = 1f, Opacity = peak },
                    new Key { Offset = 1f, X = dx, Y = dy, Scale = grow, Opacity = 0f },
                });
            }

            _lastFlow = new Flow { Mode = "disperse", Dir = wind, Dx = sumX / n, Dy = sumY / n };
        }

        public Flow GetFlow() => _lastFlow;

        public int Count() => _puffs.Count;

        public void Clear()
        {
            for (var i = _puffs.Count - 1; i >= 0; i--)
            {
                _puffs[i].El.RemoveFromHierarchy();
            }

            _puffs.Clear();
            _lastFlow = new Flow { Mode = "idle", Dir = 1 };
            _ticker?.Pause();
        }

        // ---------------------------------------------------------------- puffs

        private void AddPuff(float x, float y, float size, bool bright, float duration, float delay, Key[] keys)
        {
            if (_puffs.Count >= _limit) return;

            var w = size;
            var h = size * Rand(0.78f, 1.16f);

            var puff = new VisualElement { pickingMode = PickingMode.Ignore };
            puff.AddToClassList("pc-smoke-puff");
            puff.style.width = w;
            puff.style.height = h;
            puff.style.left = x - w * 0.5f;
            puff.style.top = y - h * 0.5f;
            puff.style.backgroundImage = new StyleBackground(
                bright ? ProjectCMainMenuSmokeTextures.Light : ProjectCMainMenuSmokeTextures.Dark);
            puff.style.opacity = 0f;   // the keyframes own the opacity from here on

            // keys is built per puff by Gather/Disperse, so scaling it in place is safe
            if (bright && PalePeak < 1f)
            {
                for (var i = 0; i < keys.Length; i++) keys[i].Opacity *= PalePeak;
            }

            _field.Add(puff);
            _puffs.Add(new Puff
            {
                El = puff,
                Start = Clock(),
                Delay = Mathf.Max(0f, delay) / 1000.0,
                Duration = Mathf.Max(120f, duration) / 1000.0,
                Keys = keys,
            });

            EnsureTicker();
        }

        private void EnsureTicker()
        {
            if (_field.panel == null) return;   // attached later: the first gust after mounting starts it
            if (_ticker == null) _ticker = _field.schedule.Execute(Tick).Every(16);
            _ticker.Resume();
        }

        // ---------------------------------------------------------------- driver

        /// <summary>Advances every live puff. Safe to call at any rate: values come from the clock.</summary>
        public void Tick() => Tick(Clock());

        public void Tick(double now)
        {
            for (var i = _puffs.Count - 1; i >= 0; i--)
            {
                var puff = _puffs[i];
                var elapsed = now - puff.Start;

                if (elapsed < puff.Delay) continue;   // not started: stays invisible as authored

                var progress = (elapsed - puff.Delay) / puff.Duration;
                if (progress >= 1.0)
                {
                    puff.El.RemoveFromHierarchy();
                    _puffs.RemoveAt(i);
                    continue;
                }

                var p = ProjectCMainMenuPalette.EaseOut((float)progress);
                Sample(puff.Keys, p, out var tx, out var ty, out var scale, out var opacity);

                var style = puff.El.style;
                style.translate = new Translate(Length.Pixels(tx), Length.Pixels(ty));
                style.scale = new Scale(new Vector2(scale, scale));
                style.opacity = opacity;
            }

            if (_puffs.Count == 0) _ticker?.Pause();   // idle costs nothing
        }

        // --------------------------------------------------------------- helpers

        private static void Sample(Key[] keys, float p, out float x, out float y, out float scale, out float opacity)
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
                    scale = Mathf.Lerp(a.Scale, b.Scale, k);
                    opacity = Mathf.Lerp(a.Opacity, b.Opacity, k);
                    return;
                }

                a = b;
            }

            x = a.X;
            y = a.Y;
            scale = a.Scale;
            opacity = a.Opacity;
        }

        private struct Box
        {
            public float Width;
            public float Height;
            public float Cx;
            public float Cy;
        }

        private static Box Bounds(Rect rect)
        {
            // ui.js: Math.max(8, rect.width || 8) — a zero rect still puffs, just small
            var width = Mathf.Max(8f, rect.width);
            var height = Mathf.Max(8f, rect.height);
            return new Box
            {
                Width = width,
                Height = height,
                Cx = rect.x + width * 0.5f,
                Cy = rect.y + height * 0.5f,
            };
        }

        /// <summary>Viewport height the DOM read as window.innerHeight — the bright/dark split.</summary>
        private float ViewHeight()
        {
            var h = _field.layout.height;
            if (!float.IsNaN(h) && h > 1f) return h;

            h = _field.worldBound.height;
            if (!float.IsNaN(h) && h > 1f) return h;

            return 1080f;   // detached (tests): the panel's reference height
        }

        private float Rand(float a, float b) => a + (b - a) * _rng.Next();

        private static double Clock() => Time.realtimeSinceStartupAsDouble;
    }
}
