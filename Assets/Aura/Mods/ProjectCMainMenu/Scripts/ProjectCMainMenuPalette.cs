// ProjectC Main Menu — the mod's palette and small math helpers (src/palette.js).
using UnityEngine;

namespace ProjectCMainMenu
{
    public static class ProjectCMainMenuPalette
    {
        // Sky (clean, top of the frame).
        public static readonly Color SkyTop = Hex("#d3e2e6");
        public static readonly Color SkyMid = Hex("#e3ebe9");
        public static readonly Color SkyHorizon = Hex("#eef2ec");

        // Black cloud veil.
        public static readonly Color VeilDark = Hex("#070a09");
        public static readonly Color VeilMid = Hex("#121711");
        public static readonly Color VeilSoft = Hex("#1f2620");
        public static readonly Color VeilHi = Hex("#2b342c");
        public static readonly Color VeilEdge = Hex("#4a574c");

        // Mesmium gas.
        public static readonly Color GasDeep = Hex("#0b1f14");
        public static readonly Color GasMid = Hex("#16412c");
        public static readonly Color GasBright = Hex("#2f7c50");
        public static readonly Color GasAccent = Hex("#54a373");

        // Interface ink.
        public static readonly Color Ink = Hex("#eef3ef");
        public static readonly Color Accent = Hex("#78b891");

        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            var h = hex.StartsWith("#") ? hex.Substring(1) : hex;
            if (h.Length == 3)
            {
                h = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]);
            }
            if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var n))
            {
                return Color.white;
            }
            return new Color(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f, 1f);
        }

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        /// <summary>Frame-rate independent exponential approach (palette.js damp).</summary>
        public static float Damp(float current, float target, float lambda, float dt)
            => Lerp(current, target, 1f - Mathf.Exp(-lambda * Mathf.Max(dt, 0f)));

        public static float SmoothStep(float edge0, float edge1, float x)
        {
            var span = Mathf.Approximately(edge0, edge1) ? 1e-6f : edge1 - edge0;
            var t = Clamp01((x - edge0) / span);
            return t * t * (3f - 2f * t);
        }

        // ---------------------------------------------------------------- easing

        /// <summary>CSS <c>ease-out</c> = cubic-bezier(0.0, 0.0, 0.58, 1.0).</summary>
        public static float EaseOut(float x) => CubicBezier(0f, 0f, 0.58f, 1f, x);

        /// <summary>
        /// CSS timing function <c>cubic-bezier(x1, y1, x2, y2)</c> evaluated at x —
        /// the curve is parametric, so x is solved for t by Newton, then y(t) is read.
        /// </summary>
        public static float CubicBezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;

            var t = x;
            for (var i = 0; i < 8; i++)
            {
                var dx = BezierAt(t, x1, x2) - x;
                if (Mathf.Abs(dx) < 1e-5f) break;

                var slope = BezierSlopeAt(t, x1, x2);
                if (Mathf.Abs(slope) < 1e-6f) break;

                t = Clamp01(t - dx / slope);
            }

            return BezierAt(t, y1, y2);
        }

        private static float BezierAt(float t, float p1, float p2)
        {
            var u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }

        private static float BezierSlopeAt(float t, float p1, float p2)
        {
            var u = 1f - t;
            return 3f * u * u * p1 + 6f * u * t * (p2 - p1) + 3f * t * t * (1f - p2);
        }

        /// <summary>Deterministic RNG so the cloud masks are identical on every start.</summary>
        public struct Mulberry32
        {
            private uint _a;

            public Mulberry32(int seed)
            {
                _a = unchecked((uint)seed);
            }

            public float Next()
            {
                unchecked
                {
                    _a += 0x6D2B79F5u;
                    var t = _a;
                    t = (t ^ (t >> 15)) * (t | 1u);
                    t ^= t + (t ^ (t >> 7)) * (t | 61u);
                    return ((t ^ (t >> 14)) & 0xFFFFFFFFu) / 4294967296f;
                }
            }
        }
    }
}
