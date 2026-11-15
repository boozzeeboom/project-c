#if UNITY_EDITOR
// T-MB03/T-MB04a: валидация СОБРАННОГО клипа MotionBricks.
//
// Измеряет то, что увидит игрок: берёт MUSCLE-клип (<ClipBase>_Humanoid.anim), кладёт его
// по кадрам через HumanPoseHandler на свежий инстанс модели и снимает мировые величины.
// Это тот же путь, которым позу применяет humanoid-пасс Animator'а, поэтому замер не
// нуждается в Play Mode.
//
// Метрики (02_PIPELINE §3.6):
//   опорная стопа — высота НИЖНЕЙ стопы в кадре. ВАЖНО (T-MB04a): меряется ОТ ЗЕМЛИ КЛИПА
//                  (минимум по кадрам), а не от rest-позы: rest — поза T/A, а не контакт.
//                  Порог подъёма над землёй ≤ 0.05 м.
//   penetration  — насколько нижняя стопа уходит НИЖЕ земли клипа; порог ≤ 0.02 м.
//   walk axis    — направление, в котором уезжает постановочная стопа. Тело должно идти
//                  ровно в противоположную сторону, поэтому ось сравнивается с лицом таза
//                  (ось «право» = вектор thigh.L -> thigh.R). Порог расхождения ≤ 20°.
//   foot slide   — по КАЖДОЙ стопе внутри её собственного контакта (Y ≤ rest+1.5 см, не менее
//                  4 кадров подряд): пер-кадровое смещение раскладывается на продольное
//                  (вдоль оси движения) и поперечное. Продольное должно быть одинаковым —
//                  это и есть «стопа стоит, а мир едет»; поперечное должно быть нулевым.
//                  Крайние кадры контакта отбрасываются: постановка и перекат через носок —
//                  это поворот стопы вокруг точки опоры, а не скольжение.
//                  Порог ≤ 2 см/кадр.
//   implied speed — медианная скорость, с которой опора «уезжает» назад: ровно столько
//                  должен давать контроллер вперёд, чтобы не было визуального skating.
//                  Слева/справа должны совпадать (асимметрия ≤ 25 %).
//   root yaw drift — уход рыскания за клип (T-MB02 демо: −197°, это и делало разворот на месте).
//   shoulder/arm — максимальный поворот B-shoulder.L/R и B-upperArm.L/R за клип.
//
// T-MB03: замер вынесен в MotionBricksValidationReport и возвращается наружу — бейкер пишет
// его в `<ClipBase>.bake.report.json`, batch-меню печатает сводку. Пороги и текст лога не менялись.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    /// <summary>Результат замера одного клипа. Заполняется валидатором, пишется в отчёт бейка.</summary>
    public sealed class MotionBricksValidationReport
    {
        public string Label;
        public string ClipName;
        public string ClipPath;
        public bool Ran;
        public bool Pass;
        public bool ApplyThresholds;

        public float LengthSeconds;
        public float FrameRate;
        public int FrameCount;

        public float RestFootY;
        public float HipsYMin, HipsYMax;
        public float HeadYMin, HeadYMax;

        public float GroundRail;
        public float StanceMaxY;
        public float StanceHover;
        public float Penetration;

        public float WalkAxisYaw;
        public float FacingYaw;
        public float AxisErrorDeg;

        public float FootSlideL, FootSlideR, FootSlideMax;
        public float LateralL, LateralR;
        public int ContactFramesL, ContactFramesR;

        public float SpeedL, SpeedR, ImpliedSpeed, SpeedAsymmetry;
        public float StrideL, StrideR;
        public float YawDriftDeg;

        public float ClavicleLDeg, ClavicleRDeg;
        public float ArmLDeg, ArmRDeg;
        public float FootLiftL, FootLiftR;

        /// <summary>Человекочитаемые причины FAIL (для сводки batch и отчёта).</summary>
        public readonly List<string> Failures = new List<string>();

        public string Summary = string.Empty;
    }

    public static class MotionBricksClipValidator
    {
        private const string ModelPath = MotionBricksBakePaths.ModelPath;
        private const string GeneratedRoot = MotionBricksBakePaths.GeneratedRoot;

        // Эталон для калибровки: авторская ходьба из пакета, БЕЗ root motion (in-place),
        // т.е. геометрически то же, что печёт бейкер. Вариант [RM] для этого не годится:
        // у него стопа физически стоит на месте, а корень едет 1.6 м.
        private const string ReferenceWalkPath =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";

        // Пороги приёмки T-MB04a
        private const float MaxStanceHoverAboveRail = 0.05f;
        private const float MaxStanceFootY = 0.15f;
        private const float MaxPenetration = 0.02f;
        private const float MaxFootSlidePerFrame = 0.02f;
        private const float MaxRootYawDriftDeg = 15f;
        private const float PlantedTolerance = 0.015f;
        private const int MinContactFrames = 4;
        private const float MaxWalkAxisErrorDeg = 20f;
        private const float MaxSpeedAsymmetry = 0.25f;

        private const float MinClavicleDeg = 3f;
        private const float MinArmDeg = 10f;

        // === Меню ===

        [MenuItem("ProjectC/MotionBricks/Validate/Walk Forward (Male)")]
        public static void ValidateWalkForward() => RunAndLog("walk_forward", "Male", "WalkForward_Male");

        [MenuItem("ProjectC/MotionBricks/Validate/WalkToJog (Male)")]
        public static void ValidateWalkToJog() => RunAndLog("walk_to_jog", "Male", "WalkToJog_Male");

        [MenuItem("ProjectC/MotionBricks/Validate/Reference Walk01 (KL, на месте)")]
        public static void ValidateReferenceWalk01()
        {
            var clip = LoadClipFromModel(ReferenceWalkPath, "Walk01_Forward");
            if (clip == null)
            {
                Debug.LogError($"[MB-Validate] в '{ReferenceWalkPath}' не найден клип Walk01_Forward.");
                return;
            }

            var report = Validate(clip, "ЭТАЛОН KL/HumanM@Walk01_Forward", false, ReferenceWalkPath);
            Log(report);
        }

        /// <summary>T-MB03: проверить все клипы, найденные на диске, и напечатать сводку.</summary>
        [MenuItem("ProjectC/MotionBricks/Validate/All discovered clips (batch)")]
        public static void ValidateAllDiscovered()
        {
            var jobs = MotionBricksBakeJob.Discover();
            if (jobs.Count == 0)
            {
                Debug.LogWarning($"[MB-Validate] В '{GeneratedRoot}' нет ни одного *_30fps.f32 — проверять нечего.");
                return;
            }

            var reports = new List<MotionBricksValidationReport>();
            foreach (var job in jobs)
            {
                var report = ValidateJob(job);
                if (report == null) continue;
                reports.Add(report);
                Log(report);
            }

            LogBatchSummary(reports);
        }

        /// <summary>Проверить любой запечённый клип по путям primitive/body/clipBase.</summary>
        public static MotionBricksValidationReport Run(string primitiveId, string bodyType, string clipBase)
        {
            string folder = GeneratedRoot + "/" + primitiveId + "/" + bodyType;
            string clipPath = folder + "/" + clipBase + "_Humanoid.anim";

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                Debug.LogError($"[MB-Validate] не найден клип '{clipPath}'.");
                return null;
            }

            return Validate(clip, $"{primitiveId}/{bodyType}/{clipBase}_Humanoid", true, clipPath);
        }

        /// <summary>Замер клипа задания (используется и batch-бейком). null — клипа нет/не humanoid.</summary>
        public static MotionBricksValidationReport ValidateJob(MotionBricksBakeJob job)
        {
            if (job == null) return null;
            return Run(job.PrimitiveId, job.BodyType, job.ClipBase);
        }

        private static void RunAndLog(string primitiveId, string bodyType, string clipBase)
        {
            var report = Run(primitiveId, bodyType, clipBase);
            if (report != null) Log(report);
        }

        private static AnimationClip LoadClipFromModel(string modelAssetPath, string namePart)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelAssetPath))
            {
                if (asset is AnimationClip c && c.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }

            return null;
        }

        public static void Log(MotionBricksValidationReport report)
        {
            if (report == null || !report.Ran) return;
            if (report.ApplyThresholds && !report.Pass) Debug.LogError(report.Summary);
            else Debug.Log(report.Summary);
        }

        public static void LogBatchSummary(List<MotionBricksValidationReport> reports)
        {
            int passed = 0, failed = 0, skipped = 0;
            var sb = new StringBuilder();
            sb.AppendLine($"[MB-Validate] Batch: проверено {reports.Count} клип(ов)");
            foreach (var report in reports)
            {
                if (report == null || !report.Ran) { skipped++; sb.AppendLine("  SKIP  (не измерен)"); continue; }
                if (report.Pass) passed++;
                else failed++;

                string status = !report.ApplyThresholds ? "REF " : (report.Pass ? "PASS" : "FAIL");
                sb.AppendLine($"  {status}  {report.Label}: таз {report.HipsYMin:F3}..{report.HipsYMax:F3}, " +
                              $"hover {report.StanceHover * 100f:F1} см, slide {report.FootSlideMax * 100f:F2} см/кадр" +
                              (report.Failures.Count > 0 ? $" — {string.Join("; ", report.Failures)}" : string.Empty));
            }

            sb.Append($"  ИТОГ: PASS {passed}, FAIL {failed}, SKIP {skipped}");

            if (failed > 0) Debug.LogError(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        // === Замер ===

        /// <summary>Замерить клип. `applyThresholds=false` — эталон: только цифры, без PASS/FAIL.</summary>
        public static MotionBricksValidationReport Validate(
            AnimationClip clip, string label, bool applyThresholds, string clipPath = null)
        {
            var report = new MotionBricksValidationReport
            {
                Label = label,
                ClipPath = clipPath,
                ApplyThresholds = applyThresholds,
            };

            if (clip == null)
            {
                Debug.LogError($"[MB-Validate] {label}: клип не задан.");
                return report;
            }

            report.ClipName = clip.name;
            report.LengthSeconds = clip.length;
            report.FrameRate = clip.frameRate;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[MB-Validate] не найдена модель '{ModelPath}'.");
                return report;
            }

            if (!clip.humanMotion)
            {
                Debug.LogError($"[MB-Validate] {label}: humanMotion=false — это не muscle-клип, на Humanoid-аватаре он скелет не двигает.");
                return report;
            }

            var curves = new Dictionary<string, AnimationCurve>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                curves[b.propertyName] = AnimationEditorCurve(clip, b);

            int muscleCount = HumanTrait.MuscleCount;
            int frameCount = Mathf.Max(2, Mathf.RoundToInt(clip.length * clip.frameRate) + 1);
            float dt = 1f / clip.frameRate;
            report.FrameCount = frameCount;

            var holder = new GameObject("__MB_Validate_Tmp");
            holder.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var inst = UnityEngine.Object.Instantiate(model, holder.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;

                var animator = inst.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogError("[MB-Validate] у модели нет Humanoid-аватара.");
                    return report;
                }

                animator.enabled = false;

                Transform hips = FindDeep(inst.transform, "B-hips");
                Transform head = FindDeep(inst.transform, "B-head");
                Transform footL = FindDeep(inst.transform, "B-foot.L");
                Transform footR = FindDeep(inst.transform, "B-foot.R");
                Transform thighL = FindDeep(inst.transform, "B-thigh.L");
                Transform thighR = FindDeep(inst.transform, "B-thigh.R");
                Transform clavL = FindDeep(inst.transform, "B-shoulder.L");
                Transform clavR = FindDeep(inst.transform, "B-shoulder.R");
                Transform armL = FindDeep(inst.transform, "B-upperArm.L");
                Transform armR = FindDeep(inst.transform, "B-upperArm.R");

                if (hips == null || footL == null || footR == null || clavL == null || armL == null ||
                    thighL == null || thighR == null)
                {
                    Debug.LogError("[MB-Validate] в риге не найдены ожидаемые кости B-*.");
                    return report;
                }

                // Опорная точка — кость стопы (rest 0.128). Носок (B-toe) для этого не годится:
                // это сустав над подошвой, и на toe-off он законно опускается ниже своей rest-высоты,
                // т.е. «penetration» по нему — артефакт, а не провал в землю.
                float restGroundY = Mathf.Min(footL.position.y, footR.position.y);
                report.RestFootY = restGroundY;

                Quaternion clav0L = clavL.localRotation, clav0R = clavR.localRotation;
                Quaternion arm0L = armL.localRotation, arm0R = armR.localRotation;

                var hipsY = new float[frameCount];
                var headY = new float[frameCount];
                var hipsRot = new Quaternion[frameCount];
                var rightAxis = new Vector3[frameCount];
                var posL = new Vector3[frameCount];
                var posR = new Vector3[frameCount];
                var groundL = new float[frameCount];
                var groundR = new float[frameCount];
                var clavLmax = new float[frameCount];
                var clavRmax = new float[frameCount];
                var armLmax = new float[frameCount];
                var armRmax = new float[frameCount];

                var handler = new HumanPoseHandler(animator.avatar, animator.transform);
                var pose = new HumanPose { muscles = new float[muscleCount] };
                try
                {
                    for (int f = 0; f < frameCount; f++)
                    {
                        float t = f * dt;
                        for (int m = 0; m < muscleCount; m++)
                            pose.muscles[m] = Eval(curves, HumanTrait.MuscleName[m], t);

                        pose.bodyPosition = new Vector3(Eval(curves, "RootT.x", t), Eval(curves, "RootT.y", t), Eval(curves, "RootT.z", t));
                        pose.bodyRotation = new Quaternion(Eval(curves, "RootQ.x", t), Eval(curves, "RootQ.y", t),
                            Eval(curves, "RootQ.z", t), Eval(curves, "RootQ.w", t));

                        handler.SetHumanPose(ref pose);

                        posL[f] = footL.position;
                        posR[f] = footR.position;
                        groundL[f] = footL.position.y;
                        groundR[f] = footR.position.y;
                        hipsY[f] = hips.position.y;
                        headY[f] = head.position.y;
                        hipsRot[f] = hips.rotation;

                        // «Лицо» таза: ось право = thigh.L -> thigh.R. Кость таза для этого не годится —
                        // её собственные оси у SOMA-рига повёрнуты, и по ним лицо уезжает на десятки градусов.
                        Vector3 right = thighR.position - thighL.position;
                        right.y = 0f;
                        rightAxis[f] = right.sqrMagnitude > 1e-10f ? right.normalized : Vector3.right;

                        clavLmax[f] = Quaternion.Angle(clav0L, clavL.localRotation);
                        clavRmax[f] = Quaternion.Angle(clav0R, clavR.localRotation);
                        armLmax[f] = Quaternion.Angle(arm0L, armL.localRotation);
                        armRmax[f] = Quaternion.Angle(arm0R, armR.localRotation);
                    }
                }
                finally
                {
                    handler.Dispose();
                }

                Measure(report, frameCount, dt, groundL, groundR, posL, posR, hipsY, headY, hipsRot,
                    rightAxis, clavLmax, clavRmax, armLmax, armRmax, restGroundY);

                report.Ran = true;
                report.Summary = Format(report);
                return report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static void Measure(
            MotionBricksValidationReport report, int frameCount, float dt,
            float[] groundL, float[] groundR, Vector3[] posL, Vector3[] posR,
            float[] hipsY, float[] headY, Quaternion[] hipsRot, Vector3[] rightAxis,
            float[] clavLmax, float[] clavRmax, float[] armLmax, float[] armRmax, float restGroundY)
        {
            // --- Стопы: высота опоры и penetration ---
            // Земля клипа — минимум по всем кадрам, т.е. где стопа реально стоит в ЭТОМ клипе.
            float stanceMaxY = 0f, penetration = 0f, groundRail = float.MaxValue;
            for (int f = 0; f < frameCount; f++)
            {
                float lowest = Mathf.Min(groundL[f], groundR[f]);
                stanceMaxY = Mathf.Max(stanceMaxY, lowest);
                groundRail = Mathf.Min(groundRail, lowest);
                penetration = Mathf.Max(penetration, restGroundY - lowest);
            }

            float stanceHover = groundRail < float.MaxValue ? stanceMaxY - groundRail : 0f;

            report.HipsYMin = Min(hipsY);
            report.HipsYMax = Max(hipsY);
            report.HeadYMin = Min(headY);
            report.HeadYMax = Max(headY);
            report.GroundRail = groundRail;
            report.StanceMaxY = stanceMaxY;
            report.StanceHover = stanceHover;
            report.Penetration = penetration;

            // --- Foot slide: по каждой стопе внутри её собственного контакта ---
            var slideL = new FootSlide();
            slideL.Analyze(posL, groundL, restGroundY, dt);
            var slideR = new FootSlide();
            slideR.Analyze(posR, groundR, restGroundY, dt);

            report.FootSlideL = slideL.MaxSlide;
            report.FootSlideR = slideR.MaxSlide;
            report.FootSlideMax = Mathf.Max(slideL.MaxSlide, slideR.MaxSlide);
            report.LateralL = slideL.MaxLateral;
            report.LateralR = slideR.MaxLateral;
            report.ContactFramesL = slideL.Frames;
            report.ContactFramesR = slideR.Frames;
            report.StrideL = slideL.Stride;
            report.StrideR = slideR.Stride;

            float speedL = slideL.MedianSpeed, speedR = slideR.MedianSpeed;
            float impliedSpeed = (speedL * slideL.Frames + speedR * slideR.Frames)
                                 / Mathf.Max(1, slideL.Frames + slideR.Frames);
            float speedMax = Mathf.Max(speedL, speedR);
            float speedAsymmetry = speedMax > 1e-4f ? Mathf.Abs(speedL - speedR) / speedMax : 0f;

            report.SpeedL = speedL;
            report.SpeedR = speedR;
            report.ImpliedSpeed = impliedSpeed;
            report.SpeedAsymmetry = speedAsymmetry;

            // --- Ось движения: куда уезжают постановочные стопы (усреднённо по обеим) ---
            Vector3 travel = slideL.Travel + slideR.Travel;
            travel.y = 0f;
            bool hasTravel = travel.sqrMagnitude > 1e-8f;
            Vector3 walkAxis = hasTravel ? travel.normalized : Vector3.back;

            Vector3 rightAvg = Vector3.zero;
            for (int f = 0; f < frameCount; f++) rightAvg += rightAxis[f];
            rightAvg.y = 0f;
            rightAvg = rightAvg.sqrMagnitude > 1e-8f ? rightAvg.normalized : Vector3.right;
            Vector3 facing = Vector3.Cross(rightAvg, Vector3.up);

            float walkAxisYaw = Mathf.Atan2(walkAxis.x, walkAxis.z) * Mathf.Rad2Deg;
            float facingYaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            float axisError = hasTravel ? Vector3.Angle(walkAxis, -facing) : 180f;

            report.WalkAxisYaw = walkAxisYaw;
            report.FacingYaw = facingYaw;
            report.AxisErrorDeg = axisError;

            float yawDrift = 0f;
            for (int f = 1; f < frameCount; f++)
                yawDrift += Mathf.DeltaAngle(hipsRot[f - 1].eulerAngles.y, hipsRot[f].eulerAngles.y);

            report.YawDriftDeg = yawDrift;
            report.FootLiftL = Max(groundL) - Min(groundL);
            report.FootLiftR = Max(groundR) - Min(groundR);
            report.ClavicleLDeg = Max(clavLmax);
            report.ClavicleRDeg = Max(clavRmax);
            report.ArmLDeg = Max(armLmax);
            report.ArmRDeg = Max(armRmax);

            // --- Пороговые проверки ---
            report.Failures.Clear();
            if (stanceHover > MaxStanceHoverAboveRail)
                report.Failures.Add($"опорная стопа висит {stanceHover * 100f:F1} см > {MaxStanceHoverAboveRail * 100f:F0}");
            if (penetration > MaxPenetration)
                report.Failures.Add($"penetration {penetration * 100f:F1} см > {MaxPenetration * 100f:F0}");
            if (slideL.Frames + slideR.Frames < MinContactFrames || slideMax(slideL, slideR) > MaxFootSlidePerFrame)
                report.Failures.Add($"foot slide {slideMax(slideL, slideR) * 100f:F2} см/кадр > {MaxFootSlidePerFrame * 100f:F0}");
            if (axisError > MaxWalkAxisErrorDeg)
                report.Failures.Add($"ось шага {axisError:F1}° > {MaxWalkAxisErrorDeg:F0}°");
            if (speedAsymmetry > MaxSpeedAsymmetry)
                report.Failures.Add($"асимметрия опоры {speedAsymmetry * 100f:F0} % > {MaxSpeedAsymmetry * 100f:F0}");
            if (Mathf.Abs(yawDrift) > MaxRootYawDriftDeg)
                report.Failures.Add($"дрейф рыскания {yawDrift:F1}° > ±{MaxRootYawDriftDeg:F0}°");
            if (!(Max(clavLmax) >= MinClavicleDeg && Max(clavRmax) >= MinClavicleDeg))
                report.Failures.Add($"ключицы не двигаются (< {MinClavicleDeg:F0}°)");
            if (!(Max(armLmax) >= MinArmDeg && Max(armRmax) >= MinArmDeg))
                report.Failures.Add($"руки не двигаются (< {MinArmDeg:F0}°)");

            report.Pass = report.Failures.Count == 0;
        }

        private static float slideMax(FootSlide a, FootSlide b) => Mathf.Max(a.MaxSlide, b.MaxSlide);

        private static string Format(MotionBricksValidationReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[MB-Validate] {r.Label}: '{r.ClipName}' {r.LengthSeconds:F3} s, {r.FrameCount} кадров @ {r.FrameRate:F0} fps"
                          + (r.ApplyThresholds ? string.Empty : "   (ЭТАЛОН: пороги не применяются)"));
            sb.AppendLine($"  rest: кость стопы={r.RestFootY:F3}, hipsY~{r.HipsYMin:F3}");
            sb.AppendLine($"  hipsY {r.HipsYMin:F3}..{r.HipsYMax:F3}  headY {r.HeadYMin:F3}..{r.HeadYMax:F3}");
            sb.AppendLine($"  опорная стопа: земля клипа {r.GroundRail:F3}, макс Y {r.StanceMaxY:F3}, подъём над землёй {r.StanceHover:F3} м (порог ≤ {MaxStanceHoverAboveRail:F2}) {Status(r.StanceHover <= MaxStanceHoverAboveRail, r.ApplyThresholds)}");
            sb.AppendLine($"    справочно: абсолютно {r.StanceMaxY:F3} против rest {r.RestFootY:F3} — старый порог ≤ {MaxStanceFootY:F2} не применяется (он мерил не ходьбу, а rest-позу)");
            sb.AppendLine($"  penetration {r.Penetration:F3} м (порог ≤ {MaxPenetration:F2}) {Status(r.Penetration <= MaxPenetration, r.ApplyThresholds)}");
            sb.AppendLine($"  ось движения (стопы) {r.WalkAxisYaw:F1}°, лицо таза {r.FacingYaw:F1}°, расхождение {r.AxisErrorDeg:F1}°"
                          + $" (порог ≤ {MaxWalkAxisErrorDeg:F0}) {Status(r.AxisErrorDeg <= MaxWalkAxisErrorDeg, r.ApplyThresholds)}");
            sb.AppendLine($"  foot slide: L {r.FootSlideL * 100f:F2} см/кадр / {r.ContactFramesL} кад. контакта (попер. {r.LateralL * 100f:F2}), "
                          + $"R {r.FootSlideR * 100f:F2} / {r.ContactFramesR} (попер. {r.LateralR * 100f:F2})"
                          + $" (порог ≤ {MaxFootSlidePerFrame * 100f:F0}) {Status(r.FootSlideMax <= MaxFootSlidePerFrame, r.ApplyThresholds)}");
            sb.AppendLine($"  скорость опоры L {r.SpeedL:F2} / R {r.SpeedR:F2} м/с, асимметрия {r.SpeedAsymmetry * 100f:F0} %"
                          + $" (порог ≤ {MaxSpeedAsymmetry * 100f:F0}) {Status(r.SpeedAsymmetry <= MaxSpeedAsymmetry, r.ApplyThresholds)}");
            sb.AppendLine($"  stride (за контакт): L {r.StrideL:F2} м, R {r.StrideR:F2} м"
                          + $" → контроллер должен давать {r.ImpliedSpeed:F2} м/с, иначе skating");
            sb.AppendLine($"  root yaw drift {r.YawDriftDeg:F1}° за клип (порог ≤ ±{MaxRootYawDriftDeg:F0}) {Status(Mathf.Abs(r.YawDriftDeg) <= MaxRootYawDriftDeg, r.ApplyThresholds)}");
            sb.AppendLine($"  подъём стопы: L {r.FootLiftL * 100f:F1} см, R {r.FootLiftR * 100f:F1} см");
            sb.AppendLine($"  B-shoulder.L {r.ClavicleLDeg:F1}°, B-shoulder.R {r.ClavicleRDeg:F1}° (порог ≥ {MinClavicleDeg:F0}°) {Status(r.ClavicleLDeg >= MinClavicleDeg && r.ClavicleRDeg >= MinClavicleDeg, r.ApplyThresholds)}");
            sb.AppendLine($"  B-upperArm.L {r.ArmLDeg:F1}°, B-upperArm.R {r.ArmRDeg:F1}° (порог ≥ {MinArmDeg:F0}°) {Status(r.ArmLDeg >= MinArmDeg && r.ArmRDeg >= MinArmDeg, r.ApplyThresholds)}");
            sb.Append($"  ИТОГ: {(r.ApplyThresholds ? (r.Pass ? "PASS" : "FAIL") : "эталон (цифры для калибровки порогов)")}");

            if (r.ApplyThresholds && !r.Pass)
                sb.Append($"\n  причины: {string.Join("; ", r.Failures)}");

            return sb.ToString();
        }

        private static AnimationCurve AnimationEditorCurve(AnimationClip clip, EditorCurveBinding binding) =>
            AnimationUtility.GetEditorCurve(clip, binding);

        private static string Status(bool pass, bool applyThresholds) => applyThresholds ? (pass ? "PASS" : "FAIL") : "—";

        /// <summary>Скольжение одной стопы внутри её собственных отрезков контакта.</summary>
        private struct FootSlide
        {
            /// <summary>Максимальное пер-кадровое отклонение от «мир едет ровно» (продольное + поперечное), м/кадр.</summary>
            public float MaxSlide;

            /// <summary>Максимальная поперечная составляющая (вбок от оси движения), м/кадр.</summary>
            public float MaxLateral;

            /// <summary>Медианная продольная скорость опоры, м/с.</summary>
            public float MedianSpeed;

            /// <summary>Суммарное горизонтальное смещение опоры за все контакты (для усреднения оси).</summary>
            public Vector3 Travel;

            /// <summary>Самый длинный шаг за контакт, м.</summary>
            public float Stride;

            /// <summary>Сколько внутренних (без краёв) кадров контакта учтено.</summary>
            public int Frames;

            public void Analyze(Vector3[] footPos, float[] ground, float restGroundY, float dt)
            {
                float limit = restGroundY + PlantedTolerance;
                int frames = 0;
                var slides = new List<float>();
                var laterals = new List<float>();
                var speeds = new List<float>();
                var travels = new List<Vector3>();

                int runStart = -1;
                for (int f = 0; f <= footPos.Length; f++)
                {
                    bool planted = f < footPos.Length && ground[f] <= limit;
                    if (planted && runStart < 0) runStart = f;
                    if (planted) continue;
                    if (runStart < 0) continue;

                    int runEnd = f - 1;
                    int len = runEnd - runStart + 1;

                    // Перекат через носок и постановку не меряем: там крутится стопа вокруг
                    // точки опоры, а не едет подошва. Отбрасываем по 20 % контакта с края.
                    int trim = Mathf.Max(1, Mathf.RoundToInt(len * 0.2f));
                    int first = runStart + trim;
                    int last = runEnd - trim;

                    if (len >= MinContactFrames && last > first)
                    {
                        var deltas = new List<Vector3>(last - first);
                        var runTravel = Vector3.zero;
                        for (int i = first; i <= last; i++)
                        {
                            Vector3 d = footPos[i] - footPos[i - 1];
                            d.y = 0f;
                            deltas.Add(d);
                            runTravel += d;
                        }

                        if (runTravel.sqrMagnitude > 1e-8f)
                        {
                            Vector3 axis = runTravel.normalized;
                            var along = new List<float>(deltas.Count);
                            foreach (var d in deltas) along.Add(Vector3.Dot(d, axis));

                            float medianAlong = Median(along);
                            for (int i = 0; i < deltas.Count; i++)
                            {
                                float lateral = Mathf.Abs(Vector3.Dot(deltas[i], Vector3.Cross(Vector3.up, axis)));
                                float alongDev = Mathf.Abs(along[i] - medianAlong);
                                slides.Add(Mathf.Sqrt(lateral * lateral + alongDev * alongDev));
                                laterals.Add(lateral);
                            }

                            speeds.Add(medianAlong / dt);
                            travels.Add(runTravel);
                            Stride = Mathf.Max(Stride, runTravel.magnitude);
                            frames += deltas.Count;
                        }
                    }

                    runStart = -1;
                }

                MaxSlide = 0f;
                MaxLateral = 0f;
                foreach (float v in slides) MaxSlide = Mathf.Max(MaxSlide, v);
                foreach (float v in laterals) MaxLateral = Mathf.Max(MaxLateral, v);
                MedianSpeed = Median(speeds);
                Travel = Vector3.zero;
                foreach (var v in travels) Travel += v;
                Frames = frames;
            }
        }

        private static float Eval(Dictionary<string, AnimationCurve> curves, string property, float time)
        {
            return curves.TryGetValue(property, out var c) && c != null ? c.Evaluate(time) : 0f;
        }

        private static float Median(List<float> values)
        {
            if (values.Count == 0) return 0f;
            var copy = new List<float>(values);
            copy.Sort();
            int mid = copy.Count / 2;
            return copy.Count % 2 == 0 ? (copy[mid - 1] + copy[mid]) * 0.5f : copy[mid];
        }

        private static float Min(float[] a)
        {
            float m = float.MaxValue;
            foreach (float v in a) m = Mathf.Min(m, v);
            return m;
        }

        private static float Max(float[] a)
        {
            float m = float.MinValue;
            foreach (float v in a) m = Mathf.Max(m, v);
            return m;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
#endif
