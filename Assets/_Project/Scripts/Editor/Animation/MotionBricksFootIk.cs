// Project C: T-MB03 — тазовый foot IK бейкера.
//
// Задача: после rotation-only ретаргета нога рига короче SOMA (reach 0.852 против 0.940 м),
// поэтому при одинаковой высоте таза опорная стопа висит ~10 см над землёй. Лечится опусканием
// таза, но наивный вариант «каждый кадр прижать НИЖНЮЮ стопу ровно к земле» пережимает контакт:
// таз повторяет весь вертикальный размах позы. На синтезированном `WalkForward_Male` это давало
// размах таза 15.8 см при 2 см у эталонной ходьбы (09 §T-MB04a, риск №7 ревью).
//
// Здесь два режима:
//   PelvisMin    — прежнее поведение (прижать каждый кадр). Оставлен для A/B и отката.
//   PelvisSmooth — таз ищется как САМАЯ ГЛАДКАЯ кривая, которая держит контакт в допуске:
//                  c[f] ∈ [raw[f] − AllowPenetration, raw[f] + AllowHover].
//                  Это taut-string/least-variation задача; решается проекцией на границы и
//                  циклическим сглаживанием (клип играется в петлю — шов тоже гладкий).
//                  Для нормального клипа допуск позволяет почти плоский таз; для плохого —
//                  таз всё равно повторяет контакт, но без «дребезга» между стопами.
//
// Допуски намеренно строже порогов валидатора (hover 0.05 / penetration 0.02), чтобы
// округление и интерполяция клипа не выводили его за PASS.

using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public enum MotionBricksFootIkMode
    {
        /// <summary>Не трогать таз (прыжки, полёт, клипы с собственной вертикалью).</summary>
        Off = 0,

        /// <summary>Прежнее поведение: каждый кадр прижать нижнюю стопу ровно к земле.</summary>
        PelvisMin = 1,

        /// <summary>Самая гладкая кривая таза в допуске контакта (по умолчанию).</summary>
        PelvisSmooth = 2,
    }

    public sealed class MotionBricksFootIkResult
    {
        public MotionBricksFootIkMode Mode;
        public float[] Corrections = new float[0];

        /// <summary>Размах «наивной» коррекции (PelvisMin), м.</summary>
        public float BobBefore;

        /// <summary>Размах применённой коррекции, м.</summary>
        public float BobAfter;

        /// <summary>Максимальный подъём нижней стопы над землёй клипа после коррекции, м.</summary>
        public float HoverMax;

        /// <summary>Максимальное проникновение нижней стопы ниже земли клипа после коррекции, м.</summary>
        public float PenetrationMax;

        public int Iterations;

        public bool ContactInTolerance =>
            HoverMax <= MotionBricksFootIk.AllowHover + 1e-4f &&
            PenetrationMax <= MotionBricksFootIk.AllowPenetration + 1e-4f;

        public string Summary =>
            $"mode={Mode}, таз {BobBefore * 100f:F1} → {BobAfter * 100f:F1} см, " +
            $"hover {HoverMax * 100f:F1} см, penetration {PenetrationMax * 100f:F1} см, итераций {Iterations}";
    }

    public static class MotionBricksFootIk
    {
        /// <summary>Сколько стопе разрешено висеть над землёй (порог валидатора 0.05).</summary>
        public const float AllowHover = 0.03f;

        /// <summary>Сколько стопе разрешено уйти под землю (порог валидатора 0.02).</summary>
        public const float AllowPenetration = 0.01f;

        private const int MaxIterations = 400;
        private const float Convergence = 1e-6f;

        /// <summary>
        /// Посчитать поправку таза по высотам нижней стопы в кадрах.
        /// `lowestFootY[f]` — минимум из двух стоп при тазе в rest-высоте (без вертикали источника).
        /// `groundY` — земля клипа (минимум по кадрам, как её меряет валидатор).
        /// </summary>
        public static MotionBricksFootIkResult Solve(
            float[] lowestFootY, float groundY, MotionBricksFootIkMode mode)
        {
            var result = new MotionBricksFootIkResult { Mode = mode };
            int n = lowestFootY != null ? lowestFootY.Length : 0;
            if (n == 0 || mode == MotionBricksFootIkMode.Off) return result;

            // raw[f] — насколько опустить таз, чтобы нижняя стопа встала ровно на землю.
            var raw = new float[n];
            float rawMin = float.MaxValue, rawMax = float.MinValue;
            for (int f = 0; f < n; f++)
            {
                raw[f] = groundY - lowestFootY[f];
                rawMin = Mathf.Min(rawMin, raw[f]);
                rawMax = Mathf.Max(rawMax, raw[f]);
            }

            result.BobBefore = rawMax - rawMin;
            result.Corrections = raw;

            if (mode == MotionBricksFootIkMode.PelvisMin)
            {
                result.BobAfter = result.BobBefore;
                result.HoverMax = 0f;
                result.PenetrationMax = 0f;
                return result;
            }

            // Допустимый коридор на кадр: ниже — проникновение, выше — зависание.
            var lo = new float[n];
            var hi = new float[n];
            for (int f = 0; f < n; f++)
            {
                lo[f] = raw[f] - AllowPenetration;
                hi[f] = raw[f] + AllowHover;
            }

            var c = new float[n];
            for (int f = 0; f < n; f++) c[f] = raw[f];

            if (n > 1)
            {
                int iterations = 0;
                for (; iterations < MaxIterations; iterations++)
                {
                    float change = 0f;

                    // Проекция Гаусса–Зейделя: сглаживаем к соседям, затем возвращаем в коридор.
                    for (int f = 0; f < n; f++)
                    {
                        int prev = (f - 1 + n) % n;
                        int next = (f + 1) % n;
                        float target = (c[prev] + c[f] + c[next]) / 3f;
                        float clamped = Mathf.Clamp(target, lo[f], hi[f]);
                        change = Mathf.Max(change, Mathf.Abs(clamped - c[f]));
                        c[f] = clamped;
                    }

                    if (change < Convergence) break;
                }

                result.Iterations = iterations;
            }

            result.Corrections = c;
            result.BobAfter = Max(c) - Min(c);
            for (int f = 0; f < n; f++)
            {
                float error = lowestFootY[f] + c[f] - groundY; // >0 — висит, <0 — под землёй
                if (error > result.HoverMax) result.HoverMax = error;
                if (-error > result.PenetrationMax) result.PenetrationMax = -error;
            }

            return result;
        }

        private static float Min(float[] values)
        {
            float m = float.MaxValue;
            foreach (float v in values) m = Mathf.Min(m, v);
            return m;
        }

        private static float Max(float[] values)
        {
            float m = float.MinValue;
            foreach (float v in values) m = Mathf.Max(m, v);
            return m;
        }

        // === Самопроверка решателя (без бейка и без Play Mode) ===

        /// <summary>
        /// T-MB03: проверка поведения решателя на синтетических профилях стопы. Нужна, чтобы
        /// «таз не пережимает контакт» было измерено, а не заявлено: ровный контакт должен дать
        /// плоский таз, эталонный размах 2 см — не увеличиться, а плохой клип (15.8 см) —
        /// уменьшиться при контакте в допуске.
        /// </summary>
        [MenuItem("ProjectC/MotionBricks/Bake/Foot IK self-test", priority = 13)]
        public static void SelfTest()
        {
            const int frames = 60;
            const float ground = 0.128f;
            var failures = new System.Collections.Generic.List<string>();
            Debug.Log("[MB-FootIK] self-test решателя (синтетические профили, реальный клип не нужен)");

            // 1. Ровный контакт: стопа стоит на земле все кадры.
            var flat = new float[frames];
            for (int f = 0; f < frames; f++) flat[f] = ground;
            Check("ровный контакт", flat, ground, expectBobAfter: 0f, expectMaxBobAfter: 0.001f, failures);

            // 2. Эталонный размах 2 см (как у KL Walk01): таз не должен раздуться.
            //    Сглаживание может сделать таз даже плоским (стопа катается ±1 см в допуске) —
            //    это норма: проверяем, что таз НЕ больше исходного и контакт в допуске.
            var reference = new float[frames];
            for (int f = 0; f < frames; f++)
                reference[f] = ground + 0.01f * (1f - Mathf.Cos(4f * Mathf.PI * f / frames)); // 0..2 см
            Check("эталон 2 см", reference, ground, expectBobAfter: -1f, expectMaxBobAfter: 0.02f, failures);

            // 3. Плохой клип из T-MB04a: нижняя стопа гуляет на 15.8 см.
            var bad = new float[frames];
            for (int f = 0; f < frames; f++)
                bad[f] = ground + 0.158f * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * f / frames)); // 0..15.8 см
            Check("плохой клип 15.8 см", bad, ground, expectBobAfter: -1f, expectMaxBobAfter: 0.158f, failures);

            // 4. Смена опорной стопы: нижняя стопа прыгает между 0 и 3 см каждые 4 кадра.
            //    Наивный вариант дёргает таз; сглаженный обязан остаться в допуске контакта.
            var alternation = new float[frames];
            for (int f = 0; f < frames; f++) alternation[f] = ground + ((f / 4) % 2 == 0 ? 0f : 0.03f);
            Check("смена опоры", alternation, ground, expectBobAfter: -1f, expectMaxBobAfter: 0.03f, failures);

            if (failures.Count == 0) Debug.Log("  ИТОГ: PASS (все профили в допуске контакта)");
            else Debug.LogError($"  ИТОГ: FAIL — {string.Join("; ", failures)}");
        }

        private static void Check(
            string label, float[] lowestFootY, float ground,
            float expectBobAfter, float expectMaxBobAfter,
            System.Collections.Generic.List<string> failures)
        {
            var result = Solve(lowestFootY, ground, MotionBricksFootIkMode.PelvisSmooth);
            bool bobOk = expectBobAfter >= 0f
                ? Mathf.Abs(result.BobAfter - expectBobAfter) <= 0.002f
                : result.BobAfter <= expectMaxBobAfter + 1e-4f;
            bool contactOk = result.ContactInTolerance;

            if (!bobOk) failures.Add($"{label}: таз {result.BobAfter * 100f:F1} см вне ожидания");
            if (!contactOk) failures.Add($"{label}: контакт вне допуска");
            if (result.BobAfter > result.BobBefore + 1e-4f) failures.Add($"{label}: таз стал хуже исходного");

            Debug.Log($"  {(bobOk && contactOk ? "PASS" : "FAIL")}  {label}: таз {result.BobBefore * 100f:F1} → {result.BobAfter * 100f:F1} см, " +
                      $"hover {result.HoverMax * 100f:F1} см, penetration {result.PenetrationMax * 100f:F1} см (итераций {result.Iterations})");
        }
    }
}
