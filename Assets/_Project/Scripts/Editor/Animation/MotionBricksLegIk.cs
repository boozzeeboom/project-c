// Project C: T-MB05 — аналитический 2-bone IK ног для бейкера MotionBricks.
//
// Зачем: тазовый foot IK (T-MB03) держит контакт, опуская таз вслед за нижней стопой.
// Поэтому размах таза равен размаху стопы: WalkForward_Male 15.7 → 11.7 см при цели ~2 см.
// Колено в этой схеме не участвует, хотя именно оно должно «съедать» разницу длины ноги
// рига (reach 0.852) и SOMA (0.940).
//
// Здесь решается обратная задача для опорной ноги: таз остаётся на гладкой кривой,
// а бедро/голень поворачиваются так, чтобы кость стопы попала на землю клипа.
// Решение — аналитическое (закон косинусов), без итераций и без PhysX:
//   - плоскость сгиба колена берётся из текущей позы (если нога прямая — forward персонажа);
//   - мировой поворот стопы сохраняется, чтобы подошва не «кивала» при подъёме таза;
//   - если цель недостижима, нога выпрямляется до максимума, а residual пишется в отчёт.
//
// Это bake-time шаг: меняются локальные повороты thigh/shin/foot в записываемом клипе.
// Generic и Humanoid клипы пекутся из одного и того же набора locals, поэтому поза совпадает.
//
// Откат: sidecar { "legIk": false } → прежний PelvisSmooth (T-MB03), код не удаляется.

using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public struct MotionBricksLegIkResult
    {
        /// <summary>Считалось ли решение (false — вырожденная нога/нулевые длины).</summary>
        public bool Solved;

        /// <summary>Цель дальше полного выпрямления: стопа осталась выше земли.</summary>
        public bool ReachLimited;

        /// <summary>targetY − achievedY: &gt; 0 стопа висит, &lt; 0 не дотянулась вниз.</summary>
        public float Residual;

        public Vector3 Knee;
        public Vector3 Foot;
        public Quaternion ThighRotation;
        public Quaternion ShinRotation;
        public Quaternion FootRotation;

        /// <summary>Сгиб колена от прямой ноги, градусы (0 = нога прямая).</summary>
        public float KneeBendDeg;
    }

    public static class MotionBricksLegIk
    {
        /// <summary>Не разгибаем ногу ровно в прямую: там ось сгиба колена вырождена.</summary>
        public const float ReachMargin = 0.001f;

        private const float Epsilon = 1e-6f;

        /// <summary>
        /// Поставить кость стопы на targetY, сохранив её мировой поворот и плоскость сгиба колена.
        /// Входные позиции/повороты — уже в мире, с учётом сдвига таза этого кадра.
        /// </summary>
        public static MotionBricksLegIkResult Solve(
            Vector3 hip, Vector3 knee, Vector3 foot,
            Quaternion thighRotation, Quaternion shinRotation, Quaternion footRotation,
            float targetY, Vector3 bendForward)
        {
            var result = new MotionBricksLegIkResult
            {
                FootRotation = footRotation,
                Foot = foot,
                Knee = knee,
                ThighRotation = thighRotation,
                ShinRotation = shinRotation,
            };

            float l1 = (knee - hip).magnitude;
            float l2 = (foot - knee).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f || !IsFinite(hip) || !IsFinite(knee) || !IsFinite(foot))
                return result;

            float maxReach = Mathf.Max(l1 + l2 - ReachMargin, 1e-3f);
            float minReach = Mathf.Min(Mathf.Abs(l1 - l2) + ReachMargin, maxReach);

            Vector3 target = new Vector3(foot.x, targetY, foot.z);
            Vector3 toTarget = target - hip;
            float targetDistance = toTarget.magnitude;

            if (targetDistance < Epsilon)
            {
                // Цель совпала с тазом: направление произвольно, берём текущую ногу.
                toTarget = foot - hip;
                targetDistance = toTarget.magnitude;
                if (targetDistance < Epsilon) return result;
            }

            Vector3 axis = toTarget / targetDistance;
            float distance = Mathf.Clamp(targetDistance, minReach, maxReach);
            result.ReachLimited = targetDistance > maxReach + 1e-4f;

            Vector3 achievedTarget = hip + axis * distance;

            // Плоскость сгиба: текущее колено относительно оси таз→стопа. Если нога почти
            // прямая, берём forward персонажа — иначе ось поворота не определена.
            Vector3 kneeDir = knee - hip;
            Vector3 bend = kneeDir - axis * Vector3.Dot(kneeDir, axis);
            if (bend.sqrMagnitude < 1e-8f)
            {
                bend = bendForward - axis * Vector3.Dot(bendForward, axis);
            }

            if (bend.sqrMagnitude < 1e-8f)
            {
                bend = Vector3.Cross(axis, Vector3.right);
                if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(axis, Vector3.forward);
            }

            bend.Normalize();

            // Закон косинусов: расстояние от таза до проекции колена на ось и высота колена.
            float along = (l1 * l1 - l2 * l2 + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - along * along));
            Vector3 newKnee = hip + axis * along + bend * height;

            Vector3 oldThighDir = knee - hip;
            Vector3 newThighDir = newKnee - hip;
            Quaternion thighDelta = Quaternion.FromToRotation(oldThighDir, newThighDir);

            Vector3 oldShinDir = foot - knee;
            Vector3 rotatedOldShinDir = thighDelta * oldShinDir;
            Vector3 newShinDir = achievedTarget - newKnee;
            Quaternion shinDelta = Quaternion.FromToRotation(rotatedOldShinDir, newShinDir);

            result.Knee = newKnee;
            result.Foot = achievedTarget;
            result.ThighRotation = thighDelta * thighRotation;
            result.ShinRotation = shinDelta * thighDelta * shinRotation;
            // FootRotation не трогаем: стопа сохраняет мировую ориентацию (подошва не кивает).
            result.Residual = targetY - achievedTarget.y;
            result.KneeBendDeg = Vector3.Angle(newKnee - hip, achievedTarget - newKnee);
            result.Solved = true;
            return result;
        }

        private static bool IsFinite(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);

        // === Самопроверка решателя (без клипа и без Play Mode) ===

        /// <summary>
        /// T-MB05: проверка аналитики на синтетической ноге. Клип не нужен — проверяем,
        /// что IK действительно ставит стопу, сгибает колено, сохраняет ориентацию стопы
        /// и честно сообщает о недостижимой цели.
        /// </summary>
        [MenuItem("ProjectC/MotionBricks/Bake/Leg IK self-test", priority = 14)]
        public static void SelfTest()
        {
            var failures = new System.Collections.Generic.List<string>();
            Debug.Log("[MB-LegIK] self-test аналитического 2-bone IK (синтетическая нога)");

            // Бедро/голень/стопа. Длины ~0.20 + 0.15 = 0.35, таз на 0.30 — цель достижима.
            Vector3 hip = new Vector3(0f, 0.30f, 0f);
            Vector3 knee = new Vector3(0f, 0.17f, 0.08f);
            Vector3 foot = new Vector3(0f, 0.02f, 0.05f);
            Quaternion thigh = Quaternion.Euler(10f, 0f, 0f);
            Quaternion shin = Quaternion.Euler(-5f, 0f, 0f);
            Quaternion footRot = Quaternion.Euler(0f, 90f, 0f);

            var solved = Solve(hip, knee, foot, thigh, shin, footRot, 0f, Vector3.forward);
            Check("достижимая цель", solved.Solved && !solved.ReachLimited &&
                                     Mathf.Abs(solved.Foot.y) < 0.002f &&
                                     Mathf.Abs(solved.Residual) < 0.002f,
                $"footY={solved.Foot.y:F3}, residual={solved.Residual:F3}, reachLimited={solved.ReachLimited}", failures);
            Check("колено согнулось", solved.KneeBendDeg > 1f,
                $"kneeBend={solved.KneeBendDeg:F1}°", failures);
            Check("ориентация стопы сохранена", Quaternion.Angle(footRot, solved.FootRotation) < 0.01f,
                $"angle={Quaternion.Angle(footRot, solved.FootRotation):F2}°", failures);

            // Прямая нога: ось сгиба вырождена; fallback-forward должен дать валидное решение.
            Vector3 straightHip = new Vector3(0f, 0.35f, 0f);
            Vector3 straightKnee = new Vector3(0f, 0.175f, 0f);
            Vector3 straightFoot = new Vector3(0f, 0f, 0f);
            var straight = Solve(straightHip, straightKnee, straightFoot, Quaternion.identity,
                Quaternion.identity, Quaternion.identity, 0.02f, Vector3.forward);
            Check("прямая нога не даёт NaN", straight.Solved && IsFinite(straight.Knee) && IsFinite(straight.Foot),
                $"knee={straight.Knee:F3}", failures);

            // Недостижимая цель: нога выпрямляется до maxReach, residual отрицательный (стопа выше цели).
            var unreachable = Solve(hip, knee, foot, thigh, shin, footRot, -0.25f, Vector3.forward);
            Check("недостижимая цель помечена", unreachable.Solved && unreachable.ReachLimited &&
                                                  unreachable.Residual < -0.05f,
                $"reachLimited={unreachable.ReachLimited}, residual={unreachable.Residual:F3}", failures);

            // Маховая стопа выше земли: цель = текущая высота, решение не должно дёргать ногу.
            var noop = Solve(hip, knee, foot, thigh, shin, footRot, foot.y, Vector3.forward);
            Check("цель = текущая высота", noop.Solved &&
                                          Quaternion.Angle(thigh, noop.ThighRotation) < 0.01f &&
                                          Quaternion.Angle(shin, noop.ShinRotation) < 0.01f,
                $"thighΔ={Quaternion.Angle(thigh, noop.ThighRotation):F2}°, shinΔ={Quaternion.Angle(shin, noop.ShinRotation):F2}°", failures);

            if (failures.Count == 0) Debug.Log("  ИТОГ: PASS (2-bone IK ставит стопу, колено сгибается, стопа не кивает)");
            else Debug.LogError($"  ИТОГ: FAIL — {string.Join("; ", failures)}");
        }

        private static void Check(string label, bool pass, string detail,
            System.Collections.Generic.List<string> failures)
        {
            if (!pass) failures.Add(label);
            Debug.Log($"  {(pass ? "PASS" : "FAIL")}  {label}: {detail}");
        }
    }
}
