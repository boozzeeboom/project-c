// ProjectC Main Menu — the day -> twilight -> night -> dawn clock
// (ported from src/daynight.js; the phase colours follow the project's
// docs/world/Calendar/Day-Night specification).
using UnityEngine;

namespace ProjectCMainMenu
{
    public struct ProjectCMainMenuDaySample
    {
        public string Label;
        public float Exposure;
        public float Saturation;
        public float Stars;
        public Color SkyTop;
        public Color SkyMid;
        public Color SkyLow;
        public Color GasDeep;
        public Color GasMid;
        public Color GasBright;
        public Color GasAccent;
        public Color Veil;
    }

    public sealed class ProjectCMainMenuDayNight
    {
        private struct Phase
        {
            public string Id;
            public string Label;
            public Color SkyTop;
            public Color SkyMid;
            public Color SkyLow;
            public Color GasDeep;
            public Color GasMid;
            public Color GasBright;
            public Color GasAccent;
            public Color Veil;
            public float Stars;
            public float Exposure;
            public float Saturation;
        }

        private struct Stop
        {
            public float T;
            public int Phase;
        }

        private const int DayIndex = 0;
        private const int TwilightIndex = 1;
        private const int NightIndex = 2;
        private const int DawnIndex = 3;

        private static readonly Phase[] Phases =
        {
            new Phase
            {
                Id = "day", Label = "День",
                SkyTop = Rgb(0.827f, 0.886f, 0.902f),
                SkyMid = Rgb(0.890f, 0.922f, 0.914f),
                SkyLow = Rgb(0.933f, 0.949f, 0.925f),
                GasDeep = Rgb(0.043f, 0.122f, 0.078f),
                GasMid = Rgb(0.086f, 0.255f, 0.172f),
                GasBright = Rgb(0.184f, 0.486f, 0.314f),
                GasAccent = Rgb(0.329f, 0.639f, 0.451f),
                Veil = Color.white,
                Stars = 0f, Exposure = 1f, Saturation = 0f,
            },
            new Phase
            {
                Id = "twilight", Label = "Сумерки",
                SkyTop = Rgb(0.30f, 0.325f, 0.44f),
                SkyMid = Rgb(0.47f, 0.435f, 0.49f),
                SkyLow = Rgb(0.72f, 0.53f, 0.435f),
                GasDeep = Rgb(0.038f, 0.088f, 0.082f),
                GasMid = Rgb(0.07f, 0.20f, 0.155f),
                GasBright = Rgb(0.145f, 0.36f, 0.28f),
                GasAccent = Rgb(0.25f, 0.50f, 0.40f),
                Veil = Rgb(0.66f, 0.68f, 0.80f),
                Stars = 0.45f, Exposure = 0.68f, Saturation = -5f,
            },
            new Phase
            {
                Id = "night", Label = "Ночь",
                SkyTop = Rgb(0.05f, 0.066f, 0.122f),
                SkyMid = Rgb(0.064f, 0.082f, 0.148f),
                SkyLow = Rgb(0.09f, 0.108f, 0.18f),
                GasDeep = Rgb(0.022f, 0.055f, 0.055f),
                GasMid = Rgb(0.055f, 0.17f, 0.135f),
                GasBright = Rgb(0.11f, 0.36f, 0.28f),
                GasAccent = Rgb(0.18f, 0.50f, 0.38f),
                Veil = Rgb(0.55f, 0.60f, 0.85f),
                Stars = 1f, Exposure = 0.3f, Saturation = -10f,
            },
            new Phase
            {
                Id = "dawn", Label = "Рассвет",
                SkyTop = Rgb(0.29f, 0.30f, 0.44f),
                SkyMid = Rgb(0.52f, 0.44f, 0.50f),
                SkyLow = Rgb(0.82f, 0.60f, 0.47f),
                GasDeep = Rgb(0.04f, 0.09f, 0.085f),
                GasMid = Rgb(0.075f, 0.21f, 0.16f),
                GasBright = Rgb(0.15f, 0.38f, 0.29f),
                GasAccent = Rgb(0.26f, 0.53f, 0.41f),
                Veil = Rgb(0.62f, 0.66f, 0.78f),
                Stars = 0.28f, Exposure = 0.74f, Saturation = -4f,
            },
        };

        private static readonly Stop[] Stops =
        {
            new Stop { T = 0.00f, Phase = DayIndex },
            new Stop { T = 0.30f, Phase = DayIndex },
            new Stop { T = 0.44f, Phase = TwilightIndex },
            new Stop { T = 0.56f, Phase = NightIndex },
            new Stop { T = 0.80f, Phase = NightIndex },
            new Stop { T = 0.92f, Phase = DawnIndex },
            new Stop { T = 1.00f, Phase = DayIndex },
        };

        public float Phase01 { get; private set; }
        public float CycleSeconds = 190f;
        public float BoostLeft;
        public float BoostSeconds = 1.7f;
        public float BoostFactor = 12f;
        public ProjectCMainMenuDaySample Sample { get; private set; }

        public ProjectCMainMenuDayNight(float phase = 0.08f, float cycleSeconds = 190f)
        {
            Phase01 = ProjectCMainMenuPalette.Clamp01(phase);
            CycleSeconds = Mathf.Clamp(cycleSeconds, 20f, 1200f);
            Sample = SamplePhase(Phase01);
        }

        public float TimeScale => 1f + BoostFactor * ProjectCMainMenuPalette.Clamp01(BoostLeft / Mathf.Max(0.0001f, BoostSeconds));

        public void Update(float dt)
        {
            var step = Mathf.Clamp(dt, 0f, 0.1f);
            if (BoostLeft > 0f) BoostLeft = Mathf.Max(0f, BoostLeft - step);
            var cycle = Mathf.Clamp(CycleSeconds, 20f, 1200f);
            Phase01 = Mathf.Repeat(Phase01 + step * TimeScale / cycle, 1f);
            Sample = SamplePhase(Phase01);
        }

        public void Boost(float seconds)
        {
            BoostLeft = Mathf.Max(BoostLeft, Mathf.Clamp(seconds, 0f, 10f));
        }

        public void SetPhase(float t)
        {
            Phase01 = Mathf.Repeat(t, 1f);
            Sample = SamplePhase(Phase01);
        }

        public static ProjectCMainMenuDaySample SamplePhase(float t)
        {
            var p = Mathf.Repeat(t, 1f);
            var a = Stops[0];
            var b = Stops[Stops.Length - 1];
            for (var i = 0; i < Stops.Length - 1; i++)
            {
                if (p >= Stops[i].T && p <= Stops[i + 1].T)
                {
                    a = Stops[i];
                    b = Stops[i + 1];
                    break;
                }
            }

            var span = Mathf.Approximately(b.T, a.T) ? 1f : b.T - a.T;
            var u = ProjectCMainMenuPalette.SmoothStep(0f, 1f, (p - a.T) / span);
            var A = Phases[a.Phase];
            var B = Phases[b.Phase];

            return new ProjectCMainMenuDaySample
            {
                Label = u < 0.5f ? A.Label : B.Label,
                Exposure = Mathf.Lerp(A.Exposure, B.Exposure, u),
                Saturation = Mathf.Lerp(A.Saturation, B.Saturation, u),
                Stars = Mathf.Lerp(A.Stars, B.Stars, u),
                SkyTop = Color.Lerp(A.SkyTop, B.SkyTop, u),
                SkyMid = Color.Lerp(A.SkyMid, B.SkyMid, u),
                SkyLow = Color.Lerp(A.SkyLow, B.SkyLow, u),
                GasDeep = Color.Lerp(A.GasDeep, B.GasDeep, u),
                GasMid = Color.Lerp(A.GasMid, B.GasMid, u),
                GasBright = Color.Lerp(A.GasBright, B.GasBright, u),
                GasAccent = Color.Lerp(A.GasAccent, B.GasAccent, u),
                Veil = Color.Lerp(A.Veil, B.Veil, u),
            };
        }

        private static Color Rgb(float r, float g, float b) => new Color(r, g, b, 1f);
    }
}
