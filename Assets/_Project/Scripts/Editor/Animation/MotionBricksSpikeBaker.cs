// Project C: T-MB03/T-MB04a/T-MB05 — бейкер клипов MotionBricks (SOMA -> Unity .anim).
//
// Вход (лежит в Assets/<GeneratedRoot>/<primitiveId>/<bodyType>/):
//   <ClipBase>_30fps.f32 — N кадров x 77 джойнтов x 7 float (tx,ty,tz,qx,qy,qz,qw),
//                          SOMA model space (Y-up, right-handed, meters), локали.
//                          Получается из .saib: `sa-inbetween pose <f.saib> --frame N --out fN.pose`
//                          по каждому кадру и склейкой .pose (2156 B = 77*7*4) в один .f32.
//   soma_bind.f32        — настоящая bind-поза SOMA из assets/human/soma_base_skel_minimal_no_root.usd
//                          (см. tools/extract_soma_bind.py).
//
// Выход (в той же папке):
//   <ClipBase>.anim             — Transform-кривые localRotation на кости + localPosition на B-hips.
//   <ClipBase>_Humanoid.anim    — мышечные кривые (RootT/RootQ + HumanTrait.MuscleName).
//   <ClipBase>.bake.report.json — метаданные: хэши сырья, настройки, foot IK, результат валидатора.
//
// T-MB03: конкретный клип больше НЕ захардкожен. Задания собираются с диска
// (MotionBricksBakeJob.Discover): папка + `*_30fps.f32` + `soma_bind.f32` = задание.
// Поведение переопределяется sidecar'ом `<ClipBase>.bake.config.json`.
//
// РЕТАРГЕТ — перенос ПОЛНОГО МИРОВОГО ПОВОРОТА кости (R_pose · R_bind⁻¹), а не локальной дельты
// и не одного swing: локальные оси источника и рига не совпадают (uAngle 90°), а FromToRotation
// теряет качение и раздаёт каждому суставу случайную скрутку (T-MB02, 08 §3).
//
// Ключицы (B-shoulder.L/R) переносятся отдельным каналом: у SOMA джойнты 11 (shoulder) и 12
// (upperArm) практически совпадают, поэтому цепочка 11->12 вырождена. Берётся направление
// chest->shoulder (3->11 / 3->39), это реальный вектор ключицы; амплитуда ограничена
// MaxClavicleDeg, качение ключицы не переносится.
//
// Правило: НЕ трогаем prefab/controller; клип живёт в Generated/, играется MotionBricksBakedPlayback.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public static class MotionBricksSpikeBaker
    {
        // === Пути/константы ===

        private const string ModelPath = MotionBricksBakePaths.ModelPath;
        private const string GeneratedRoot = MotionBricksBakePaths.GeneratedRoot;
        private const string RestFile = MotionBricksBakePaths.RestFile;

        private const int Joints = MotionBricksBakePaths.Joints;
        private const int FloatsPerJoint = MotionBricksBakePaths.FloatsPerJoint;
        private const float Fps = MotionBricksBakePaths.Fps;

        /// <summary>Разворот модели вокруг Y, если SOMA-форвард не совпал с Unity. Градусы.</summary>
        private const float YawOffsetDeg = 0f;

        /// <summary>
        /// Предел отклонения ключицы, градусы. chest->shoulder в SOMA — реальный вектор,
        /// но он включает и качание плеча; без предела ключица начинает «пожимать» на бегу.
        /// </summary>
        private const float MaxClavicleDeg = 18f;

        /// <summary>
        /// T-MB05: если вертикаль корня источника гуляет больше этого, таз за ней не ведём —
        /// это не походка, а «прыжок» синтеза. Хорошая ходьба (~2 см) проходит.
        /// </summary>
        private const float MaxSourceBobForLegIk = 0.06f;

        // === Меню ===

        [MenuItem("ProjectC/MotionBricks/Bake/Walk Forward (Male)", priority = 0)]
        public static void BakeWalkForwardMale() => BakeFound("walk_forward", "Male");

        [MenuItem("ProjectC/MotionBricks/Bake/WalkToJog (Male)", priority = 1)]
        public static void BakeWalkToJogMale() => BakeFound("walk_to_jog", "Male");

        /// <summary>T-MB03: batch по всему, что найдено на диске. Сырьё не трогается.</summary>
        [MenuItem("ProjectC/MotionBricks/Bake/All discovered clips (batch)", priority = 10)]
        public static void BakeAllDiscovered() => BakeAll(validate: false, logJobs: true);

        /// <summary>T-MB03: batch + замер каждого humanoid-клипа и отчёт в метаданных.</summary>
        [MenuItem("ProjectC/MotionBricks/Bake/Bake + validate all discovered", priority = 11)]
        public static void BakeAllDiscoveredAndValidate() => BakeAll(validate: true, logJobs: true);

        [MenuItem("ProjectC/MotionBricks/Bake/List discovered clips", priority = 12)]
        public static void ListDiscovered()
        {
            var jobs = MotionBricksBakeJob.Discover();
            if (jobs.Count == 0)
            {
                Debug.LogWarning($"[MB-Bake] В '{GeneratedRoot}' нет ни одного задания " +
                                 $"(нужны <primitive>/<body>/{RestFile} и *{MotionBricksBakePaths.RawSuffix}).");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[MB-Bake] Найдено заданий: {jobs.Count}");
            foreach (var job in jobs) sb.AppendLine("  • " + job);
            Debug.Log(sb.ToString());
        }

        // === Оркестрация ===

        private static void BakeFound(string primitiveId, string bodyType)
        {
            var job = MotionBricksBakeJob.Find(primitiveId, bodyType);
            if (job == null)
            {
                Debug.LogError($"[MB-Bake] Нет задания '{primitiveId}/{bodyType}' в '{GeneratedRoot}'. " +
                               "Нужны папка, soma_bind.f32 и *_30fps.f32.");
                return;
            }

            Bake(job, validate: true);
        }

        private static void BakeAll(bool validate, bool logJobs)
        {
            var jobs = MotionBricksBakeJob.Discover();
            if (jobs.Count == 0)
            {
                Debug.LogWarning($"[MB-Bake] В '{GeneratedRoot}' нет ни одного задания — печь нечего.");
                return;
            }

            if (logJobs)
            {
                Debug.Log($"[MB-Bake] Batch: заданий {jobs.Count} (validate={validate})");
                foreach (var job in jobs) Debug.Log("  • " + job);
            }

            int ok = 0, failed = 0, validationPassed = 0, validationFailed = 0;
            foreach (var job in jobs)
            {
                var outcome = Bake(job, validate);
                if (outcome == null) { failed++; continue; }
                ok++;

                if (validate && outcome.Validation != null && outcome.Validation.Ran)
                {
                    if (outcome.Validation.Pass) validationPassed++;
                    else validationFailed++;
                }
            }

            string summary = $"[MB-Bake] Batch завершён: испечено {ok}, ошибок {failed}" +
                             (validate ? $"; валидация PASS {validationPassed}, FAIL {validationFailed}" : string.Empty);
            if (failed > 0 || validationFailed > 0) Debug.LogError(summary);
            else Debug.Log(summary);
        }

        public sealed class BakeOutcome
        {
            public MotionBricksBakeReport Report;
            public MotionBricksValidationReport Validation;
        }

        /// <summary>Испечь одно задание. Возвращает null при ошибке входа.</summary>
        public static BakeOutcome Bake(MotionBricksBakeJob job, bool validate)
        {
            if (job == null) return null;

            if (Parents.Length != Joints)
            {
                Debug.LogError($"[MB-Bake] Parents.Length={Parents.Length}, ожидалось {Joints}.");
                return null;
            }

            string rawPath = job.RawPath;
            string restPath = job.SourceFolder + "/" + RestFile;

            if (!File.Exists(rawPath) || !File.Exists(restPath))
            {
                Debug.LogError($"[MB-Bake] {job.Label}: не найдены входные файлы: {rawPath} / {restPath}");
                return null;
            }

            float[] raw = ReadFloats(rawPath);
            float[] rest = ReadFloats(restPath);

            const int frameFloats = Joints * FloatsPerJoint;
            if (raw.Length % frameFloats != 0)
            {
                Debug.LogError($"[MB-Bake] {job.RawFile}: {raw.Length} float не делится на {frameFloats}.");
                return null;
            }

            if (rest.Length != frameFloats)
            {
                Debug.LogError($"[MB-Bake] {RestFile}: {rest.Length} float, ожидалось {frameFloats}.");
                return null;
            }

            int frameCount = raw.Length / frameFloats;

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[MB-Bake] Модель не найдена: {ModelPath}");
                return null;
            }

            var report = new MotionBricksBakeReport
            {
                schema = MotionBricksBakePaths.ReportSchema,
                ticket = "T-MB05",
                generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                modelPath = ModelPath,
                restFile = RestFile,
                restSha256 = MotionBricksHashing.Sha256(restPath),
                sourceFile = job.RawFile,
                sourceSha256 = MotionBricksHashing.Sha256(rawPath),
                sourceFrames = frameCount,
                sourceDurationSeconds = frameCount / Fps,
                joints = Joints,
                floatsPerJoint = FloatsPerJoint,
                bakeInPlace = job.BakeInPlace,
                groundFeet = job.GroundFeet,
                loop = job.Loop,
                yawOffsetDeg = YawOffsetDeg,
                maxClavicleDeg = MaxClavicleDeg,
            };

            var warnings = new List<string>();
            var outcome = new BakeOutcome { Report = report };

            var holder = new GameObject("__MB_Bake_Tmp");
            holder.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var inst = UnityEngine.Object.Instantiate(modelAsset, holder.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;

                var clips = BuildClips(inst.transform, raw, rest, frameCount, job, report, warnings);
                if (clips == null) return null;

                report.genericClip = SaveWithReport(clips.Generic, job.ClipOut);
                report.humanoidClip = clips.Humanoid != null
                    ? SaveWithReport(clips.Humanoid, job.HumanoidClipOut)
                    : new MotionBricksBakeReportClip { path = job.HumanoidClipOut };
                report.genericClip.loopTime = job.Loop;
                report.humanoidClip.loopTime = job.Loop;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }

            if (validate)
            {
                var validation = MotionBricksClipValidator.ValidateJob(job);
                outcome.Validation = validation;
                CopyValidation(report.validation, validation);

                // Что видит игрок — метрики humanoid-клипа, а не сырой позы ретаргета.
                // Пороги валидатора: hover ≤ 5 см, penetration ≤ 2 см.
                if (validation != null && validation.Ran)
                {
                    report.footIk.poseHoverCm = report.footIk.hoverCm;
                    report.footIk.posePenetrationCm = report.footIk.penetrationCm;
                    report.footIk.hoverCm = validation.StanceHover * 100f;
                    report.footIk.penetrationCm = validation.Penetration * 100f;
                    report.footIk.contactInTolerance =
                        validation.StanceHover <= 0.05f + 1e-4f &&
                        validation.Penetration <= 0.02f + 1e-4f;
                }
            }

            report.warnings = warnings.ToArray();
            WriteReport(job, report);

            Debug.Log($"[MB-Bake] OK ({job.Label}): {job.ClipOut}\n" +
                      $"  отчёт: {job.ReportPath}\n" +
                      $"  foot IK: {report.footIk.mode}, таз {report.footIk.bobBeforeCm:F1} → {report.footIk.bobAfterCm:F1} см, " +
                      $"humanoid hover {report.footIk.hoverCm:F1} см, penetration {report.footIk.penetrationCm:F1} см " +
                      $"(raw pose {report.footIk.poseHoverCm:F1} / {report.footIk.posePenetrationCm:F1})\n" +
                      $"  GUID: generic {DescribeGuid(report.genericClip)}, humanoid {DescribeGuid(report.humanoidClip)}\n" +
                      (validate && outcome.Validation != null && outcome.Validation.Ran
                          ? $"  валидация: {(outcome.Validation.Pass ? "PASS" : "FAIL")}" +
                            (outcome.Validation.Pass ? string.Empty : $" — {string.Join("; ", outcome.Validation.Failures)}")
                          : "  валидация: не запускалась") +
                      (warnings.Count > 0 ? $"\n  предупреждений: {warnings.Count} (см. отчёт)" : string.Empty));

            return outcome;
        }

        private static string DescribeGuid(MotionBricksBakeReportClip clip) =>
            string.IsNullOrEmpty(clip.guid)
                ? "нет"
                : $"{clip.guid} ({(clip.guidStable ? "стабилен" : "СМЕНИЛСЯ")})";

        private static void CopyValidation(MotionBricksBakeReportValidation target, MotionBricksValidationReport source)
        {
            if (target == null || source == null) return;

            target.ran = source.Ran;
            target.pass = source.Pass;
            target.label = source.Label;
            target.failures = source.Failures.ToArray();
            target.hipsYMin = source.HipsYMin;
            target.hipsYMax = source.HipsYMax;
            target.stanceHoverM = source.StanceHover;
            target.penetrationM = source.Penetration;
            target.footSlideMaxM = source.FootSlideMax;
            target.contactFrames = source.ContactFramesL + source.ContactFramesR;
            target.speedL = source.SpeedL;
            target.speedR = source.SpeedR;
            target.impliedSpeed = source.ImpliedSpeed;
            target.speedAsymmetry = source.SpeedAsymmetry;
            target.axisErrorDeg = source.AxisErrorDeg;
            target.yawDriftDeg = source.YawDriftDeg;
            target.clavicleLDeg = source.ClavicleLDeg;
            target.clavicleRDeg = source.ClavicleRDeg;
            target.armLDeg = source.ArmLDeg;
            target.armRDeg = source.ArmRDeg;
        }

        private static void WriteReport(MotionBricksBakeJob job, MotionBricksBakeReport report)
        {
            try
            {
                MotionBricksHashing.WriteUtf8(job.ReportPath, JsonUtility.ToJson(report, true));
                AssetDatabase.ImportAsset(job.ReportPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MB-Bake] {job.ReportPath}: отчёт не записан ({e.Message}). Клипы испечены.");
            }
        }

        // === Основная сборка ===

        private sealed class BuiltClips
        {
            public AnimationClip Generic;
            public AnimationClip Humanoid;
        }

        private static BuiltClips BuildClips(
            Transform root, float[] raw, float[] rest, int frameCount,
            MotionBricksBakeJob job, MotionBricksBakeReport report, List<string> warnings)
        {
            // 1. Rest-мир рига и карта «кость -> pose-индекс».
            var restWorld = new Dictionary<Transform, Quaternion>();
            var boneByPose = new Dictionary<int, Transform>();
            var poseByTransform = new Dictionary<Transform, int>();

            CollectRestWorld(root, Quaternion.identity, restWorld);

            var missing = new List<string>();
            foreach (var (pose, boneName) in BoneMap)
            {
                Transform found = FindDeep(root, boneName);
                if (found == null) { missing.Add(boneName); continue; }
                boneByPose[pose] = found;
                poseByTransform[found] = pose;
            }

            if (missing.Count > 0)
            {
                Debug.LogError($"[MB-Bake] В риге не найдены кости: {string.Join(", ", missing)}. Клип не создан.");
                return null;
            }

            // 2. Направления костей в bind-позе источника.
            var restRot = new Quaternion[Joints];
            var restPos = new Vector3[Joints];
            SomaPose(rest, restRot, restPos);

            var restDir = new Vector3[Segments.Length];
            for (int i = 0; i < Segments.Length; i++)
            {
                var seg = Segments[i];
                restDir[i] = (restPos[seg.Child] - restPos[seg.Bone]).normalized;
                if (restDir[i].sqrMagnitude < 0.5f)
                    warnings.Add($"в bind-позе вырожденная цепочка {seg.Bone}->{seg.Child}");
            }

            // 2b. Опорные направления ключиц: chest -> плечевой джойнт.
            var clavRestDir = new Vector3[Clavicles.Length];
            for (int c = 0; c < Clavicles.Length; c++)
            {
                var clav = Clavicles[c];
                clavRestDir[c] = (restPos[clav.Arm] - restPos[clav.Chest]).normalized;
                if (clavRestDir[c].sqrMagnitude < 0.5f)
                    warnings.Add($"в bind-позе вырожденная ключица {clav.Bone}");
            }

            // 3. Локальные повороты рига по кадрам.
            var hips = boneByPose[0];
            Vector3 restHipsLocal = hips.localPosition;

            // 3b. Foot IK (тазовая часть). Причина: rotation-only ретаргет сохраняет углы суставов,
            //     но нога рига короче SOMA (reach 0.852 против 0.940 м, −9.4%), поэтому при
            //     одинаковой высоте таза стопа остаётся ~10 см выше земли.
            //     T-MB03: контакт держится ДОПУСКОМ (MotionBricksFootIk), а не прижатием каждого кадра.
            Transform footL = FindDeep(root, "B-foot.L");
            Transform footR = FindDeep(root, "B-foot.R");
            Transform thighL = FindDeep(root, "B-thigh.L");
            Transform thighR = FindDeep(root, "B-thigh.R");
            Transform shinL = FindDeep(root, "B-shin.L");
            Transform shinR = FindDeep(root, "B-shin.R");
            float groundFootY = footL != null && footR != null
                ? Mathf.Min(footL.position.y, footR.position.y)
                : 0f;
            bool groundFeet = job.GroundFeet && footL != null && footR != null;
            if (!groundFeet && job.GroundFeet)
                warnings.Add("foot IK запрошен, но в риге нет B-foot.L/R — таз не прижимается");

            bool legIk = groundFeet && job.LegIk &&
                         thighL != null && thighR != null && shinL != null && shinR != null;
            if (groundFeet && job.LegIk && !legIk)
                warnings.Add("leg IK запрошен, но в риге нет B-thigh/B-shin — остаётся тазовый PelvisSmooth");

            var locals = new Dictionary<Transform, Quaternion[]>();
            foreach (Transform t in poseByTransform.Keys) locals[t] = new Quaternion[frameCount];

            var pathCache = new Dictionary<Transform, string>();
            foreach (Transform t in poseByTransform.Keys) pathCache[t] = RelativePath(root, t);

            Quaternion yaw = Quaternion.Euler(0f, YawOffsetDeg, 0f);
            Quaternion yawInv = Quaternion.Inverse(yaw);

            var hipsOffsets = new List<Vector3>(frameCount);
            var lowestFootY = new float[frameCount];
            var stanceFoot = new int[frameCount];
            var sourceRootY = new float[frameCount];
            var legL = new LegFrame[frameCount];
            var legR = new LegFrame[frameCount];
            Vector3 restRootUnity = ToUnityPos(rest[0], rest[1], rest[2]);

            var deltaByPose = new Dictionary<int, Quaternion>(Segments.Length);
            var clavByPose = new Dictionary<int, Quaternion>(Clavicles.Length);
            var frameRot = new Quaternion[Joints];
            var framePos = new Vector3[Joints];
            var frameBuf = new float[Joints * FloatsPerJoint];

            float swingSum = 0f;
            float swingMax = 0f;
            var segSwingMax = new float[Segments.Length];
            var segSwingFrame = new int[Segments.Length];
            var clavMaxAngle = new float[Clavicles.Length];

            // Собственная амплитуда плеча в источнике (для отчёта «руки слабые/сильные»).
            float armSwingMax = 0f;

            var worldPos = new Dictionary<Transform, Vector3>(restWorld.Count);
            var worldRot = new Dictionary<Transform, Quaternion>(restWorld.Count);

            for (int f = 0; f < frameCount; f++)
            {
                Array.Copy(raw, f * frameBuf.Length, frameBuf, 0, frameBuf.Length);

                SomaPose(frameBuf, frameRot, framePos);

                deltaByPose.Clear();
                clavByPose.Clear();
                for (int i = 0; i < Segments.Length; i++)
                {
                    var seg = Segments[i];
                    Vector3 dir = framePos[seg.Child] - framePos[seg.Bone];
                    if (dir.sqrMagnitude < 1e-10f) continue;
                    dir.Normalize();

                    Quaternion swing = Quaternion.FromToRotation(restDir[i], dir);

                    // Переносится ПОЛНЫЙ мировой поворот кости (включая качение), а не только
                    // дуга по направлению: у SOMA качение реальное, а FromToRotation его теряет,
                    // и каждому суставу достаётся случайная скрутка — конечности выворачивает.
                    Quaternion delta = frameRot[seg.Bone] * Quaternion.Inverse(restRot[seg.Bone]);
                    deltaByPose[seg.Bone] = yaw * delta * yawInv;

                    float angle = Quaternion.Angle(Quaternion.identity, swing);
                    swingSum += angle;
                    if (angle > swingMax) swingMax = angle;
                    if (angle > segSwingMax[i]) { segSwingMax[i] = angle; segSwingFrame[i] = f; }

                    if (seg.Bone == 12 || seg.Bone == 40) armSwingMax = Mathf.Max(armSwingMax, angle);
                }

                // Ключицы: swing направления chest->arm, с ограничением амплитуды.
                for (int c = 0; c < Clavicles.Length; c++)
                {
                    var clav = Clavicles[c];
                    Vector3 dir = framePos[clav.Arm] - framePos[clav.Chest];
                    if (dir.sqrMagnitude < 1e-10f) continue;
                    dir.Normalize();

                    Quaternion swing = Quaternion.FromToRotation(clavRestDir[c], dir);
                    float angle = Quaternion.Angle(Quaternion.identity, swing);
                    if (angle > MaxClavicleDeg) swing = Quaternion.Slerp(Quaternion.identity, swing, MaxClavicleDeg / angle);
                    clavMaxAngle[c] = Mathf.Max(clavMaxAngle[c], Mathf.Min(angle, MaxClavicleDeg));

                    Quaternion clavSwingWorld = yaw * swing * yawInv;
                    clavByPose[FindPoseByBone(clav.Bone)] = clavSwingWorld;

                    // Рука наследует поворот ключицы. Без этого абсолютная мировая ориентация
                    // плеча спорит с ключицей, и локальное вращение уходит за лимиты аватара
                    // (T-MB04a: Right Shoulder Front-Back −1.89), а humanoid-плеер такие
                    // мышцы клампит — в игре рука теряет позу.
                    foreach (int poseIndex in ClavicleChains[c])
                    {
                        if (deltaByPose.TryGetValue(poseIndex, out Quaternion chainDelta))
                            deltaByPose[poseIndex] = clavSwingWorld * chainDelta;
                    }
                }

                Vector3 rootUnity = ToUnityPos(frameBuf[0], frameBuf[1], frameBuf[2]);
                Vector3 offset = rootUnity - restRootUnity;
                if (job.BakeInPlace) { offset.x = 0f; offset.z = 0f; }

                // T-MB03: при включённом foot IK вертикаль источника не тащим в таз — иначе
                // она борется с контактом (T-MB04a давал размах таза 15.8 см). Высоту таза
                // определяет земля, а не root Y источника.
                // T-MB05: вертикаль источника запоминаем: если она правдоподобна (≤ 6 см),
                // leg IK вернёт её как естественный bob таза.
                sourceRootY[f] = offset.y;
                if (groundFeet) offset.y = 0f;

                worldPos.Clear();
                worldRot.Clear();
                EvalFrame(root, Quaternion.identity, Vector3.zero, f, deltaByPose, clavByPose,
                    restWorld, poseByTransform, locals, worldPos, worldRot, hips, restHipsLocal + yaw * offset);

                if (groundFeet)
                {
                    float yL = worldPos[footL].y;
                    float yR = worldPos[footR].y;
                    stanceFoot[f] = yL <= yR ? 0 : 1;
                    lowestFootY[f] = Mathf.Min(yL, yR);
                }

                if (legIk)
                {
                    legL[f] = CaptureLeg(thighL, shinL, footL, worldPos, worldRot);
                    legR[f] = CaptureLeg(thighR, shinR, footR, worldPos, worldRot);
                }

                hipsOffsets.Add(offset);
            }

            // 3c. Высота таза. T-MB03 (fallback): PelvisSmooth держит контакт, опуская таз.
            //     T-MB05 (default): таз идёт по достижимости + естественному bob, а стопу
            //     ставит аналитический 2-bone IK ноги.
            MotionBricksFootIkResult footIk = null;
            if (groundFeet)
            {
                if (legIk)
                {
                    footIk = SolveLegIk(frameCount, job, groundFootY, lowestFootY, stanceFoot,
                        sourceRootY, legL, legR,
                        thighL, shinL, footL, thighR, shinR, footR,
                        hipsOffsets, locals, worldRot, report, warnings);
                }
                else
                {
                    footIk = MotionBricksFootIk.Solve(lowestFootY, groundFootY,
                        MotionBricksFootIkMode.PelvisSmooth, cyclic: job.Loop);
                    for (int f = 0; f < frameCount; f++)
                        hipsOffsets[f] += Vector3.up * footIk.Corrections[f];
                }

                report.footIk.mode = footIk.Mode.ToString();
                report.footIk.bobBeforeCm = footIk.BobBefore * 100f;
                report.footIk.bobAfterCm = footIk.BobAfter * 100f;
                report.footIk.hoverCm = footIk.HoverMax * 100f;
                report.footIk.penetrationCm = footIk.PenetrationMax * 100f;
                report.footIk.iterations = footIk.Iterations;
                report.footIk.contactInTolerance = footIk.ContactInTolerance;
                report.footIk.correctionMinCm = Min(footIk.Corrections) * 100f;
                report.footIk.correctionMaxCm = Max(footIk.Corrections) * 100f;

                report.footIk.lowestFootYCm = new float[frameCount];
                report.footIk.correctionsCm = new float[frameCount];
                for (int f = 0; f < frameCount; f++)
                {
                    report.footIk.lowestFootYCm[f] = lowestFootY[f] * 100f;
                    report.footIk.correctionsCm[f] = footIk.Corrections[f] * 100f;
                }

                report.footIk.stanceFoot = stanceFoot;

                if (!footIk.ContactInTolerance)
                    warnings.Add($"foot IK вне допуска: hover {footIk.HoverMax * 100f:F1} см, " +
                                 $"penetration {footIk.PenetrationMax * 100f:F1} см");
            }
            else
            {
                report.footIk.mode = MotionBricksFootIkMode.Off.ToString();
            }

            // 4. Кривые localRotation.
            var clip = new AnimationClip { frameRate = Fps, legacy = false };

            foreach (var kv in locals)
            {
                var t = kv.Key;
                var quats = kv.Value;
                string path = pathCache[t];

                var cur = new AnimationCurve[4];
                for (int c = 0; c < 4; c++) cur[c] = new AnimationCurve();

                Quaternion prev = quats[0];
                for (int f = 0; f < frameCount; f++)
                {
                    Quaternion q = quats[f];

                    // Непрерывность: без скачков знака кватерниона.
                    if (Quaternion.Dot(prev, q) < 0f)
                    {
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        quats[f] = q;
                    }

                    prev = q;
                    float time = f / Fps;
                    cur[0].AddKey(time, q.x);
                    cur[1].AddKey(time, q.y);
                    cur[2].AddKey(time, q.z);
                    cur[3].AddKey(time, q.w);
                }

                clip.SetCurve(path, typeof(Transform), "localRotation.x", cur[0]);
                clip.SetCurve(path, typeof(Transform), "localRotation.y", cur[1]);
                clip.SetCurve(path, typeof(Transform), "localRotation.z", cur[2]);
                clip.SetCurve(path, typeof(Transform), "localRotation.w", cur[3]);
            }

            // 5. Позиция таза (in-place).
            var posCurves = new AnimationCurve[3];
            for (int c = 0; c < 3; c++) posCurves[c] = new AnimationCurve();
            for (int f = 0; f < frameCount; f++)
            {
                Vector3 p = restHipsLocal + yaw * hipsOffsets[f];
                posCurves[0].AddKey(f / Fps, p.x);
                posCurves[1].AddKey(f / Fps, p.y);
                posCurves[2].AddKey(f / Fps, p.z);
            }

            string hipsPath = pathCache[hips];
            clip.SetCurve(hipsPath, typeof(Transform), "localPosition.x", posCurves[0]);
            clip.SetCurve(hipsPath, typeof(Transform), "localPosition.y", posCurves[1]);
            clip.SetCurve(hipsPath, typeof(Transform), "localPosition.z", posCurves[2]);

            clip.EnsureQuaternionContinuity();
            ApplyLoopSettings(clip, job.Loop);

            // 6. Humanoid-вариант (мышцы) — то, что реально играется на Humanoid-аватаре.
            //    Сэмплируем УЖЕ СОБРАННЫЙ generic-клип на временном инстансе БЕЗ Animator:
            //    Animator с Humanoid-аватаром перехватывает generic-кривые, и humanoid-клип
            //    получался T-позой (T-MB05).
            AnimationClip humanoid = BakeHumanoidClip(clip, frameCount, job, warnings);
            if (humanoid != null) ApplyLoopSettings(humanoid, job.Loop);

            // 7. Диагностика ретаргета.
            float minD = 0f, maxD = 0f;
            foreach (var d in hipsOffsets)
            {
                minD = Mathf.Min(minD, d.y);
                maxD = Mathf.Max(maxD, d.y);
            }

            float meanSwing = frameCount > 0 ? swingSum / (frameCount * Segments.Length) : 0f;

            // Вырожденный FromToRotation (близко к 180°) — ось поворота выбирается произвольно,
            // такую цепочку надо видеть в логе, а не искать визуально.
            var degenerate = new List<string>();
            for (int i = 0; i < Segments.Length; i++)
            {
                if (segSwingMax[i] >= 150f)
                    degenerate.Add($"{Segments[i].Bone}->{Segments[i].Child} {segSwingMax[i]:F1}° @f{segSwingFrame[i]}");
            }

            if (degenerate.Count > 0)
                warnings.Add("вырожденный swing: " + string.Join(", ", degenerate));

            var clavText = new List<string>();
            for (int c = 0; c < Clavicles.Length; c++) clavText.Add($"{Clavicles[c].Bone} {clavMaxAngle[c]:F1}°");

            Debug.Log($"[MB-Bake] ретаргет ({job.Label}): кадров {frameCount}, fps {Fps:F0}, длина {clip.length:F3} s\n" +
                      $"  костей с кривыми {locals.Count}, цепочек {Segments.Length}, ключиц {Clavicles.Length} (лимит {MaxClavicleDeg:F0}°)\n" +
                      $"  swing: средний {meanSwing:F1}°, макс {swingMax:F1}°; плечо в источнике макс {armSwingMax:F1}°\n" +
                      $"  ключицы (макс поворот): {string.Join(", ", clavText)}\n" +
                      $"  таз: rest y={restHipsLocal.y:F3}, вертикальный размах {minD:F3}..{maxD:F3} m, in-place={job.BakeInPlace}\n" +
                      (footIk != null
                          ? $"  foot IK ({footIk.Mode}): {footIk.Summary} (сырая поза), опора в rest y={groundFootY:F3}" +
                            (footIk.Mode == MotionBricksFootIkMode.LegIk
                                ? $", колено до {report.footIk.kneeBendMaxDeg:F0}°, bob источника {report.footIk.sourceBobCm:F1} см " +
                                  $"({(report.footIk.sourceBobApplied ? "применён" : "отклонён")}), " +
                                  $"reach-limited {report.footIk.reachLimitedFrames} кадров\n"
                                : "\n")
                          : "  foot IK: выключен для этого задания\n") +
                      $"  yaw offset {YawOffsetDeg:F1}°, модель {ModelPath}");

            return new BuiltClips { Generic = clip, Humanoid = humanoid };
        }

        // === T-MB05: 2-bone leg IK ===

        private struct LegFrame
        {
            public Vector3 Hip;
            public Vector3 Knee;
            public Vector3 Foot;
            public Quaternion ThighRotation;
            public Quaternion ShinRotation;
            public Quaternion FootRotation;

            /// <summary>Мировой поворот родителя бедра (таза) ИМЕННО ЭТОГО кадра.</summary>
            public Quaternion ParentRotation;

            public Vector3 BendForward;
            public bool Valid;
        }

        private struct LegIkFrameOutcome
        {
            public float FootY;
            public float Residual;
            public float KneeBendDeg;
            public bool ReachLimited;
            public bool Applied;
        }

        private static LegFrame CaptureLeg(
            Transform thigh, Transform shin, Transform foot,
            Dictionary<Transform, Vector3> worldPos, Dictionary<Transform, Quaternion> worldRot)
        {
            var leg = new LegFrame();
            if (thigh == null || shin == null || foot == null) return leg;

            if (!worldPos.TryGetValue(thigh, out leg.Hip) ||
                !worldPos.TryGetValue(shin, out leg.Knee) ||
                !worldPos.TryGetValue(foot, out leg.Foot) ||
                !worldRot.TryGetValue(thigh, out leg.ThighRotation) ||
                !worldRot.TryGetValue(shin, out leg.ShinRotation) ||
                !worldRot.TryGetValue(foot, out leg.FootRotation))
                return leg;

            // Плоскость сгиба колена: forward таза (горизонтальный). Если таз повёрнут
            // вместе с клипом, колено всё равно сгибается вперёд персонажа.
            Quaternion parentRot = WorldRotation(thigh.parent, worldRot);
            leg.ParentRotation = parentRot;
            Vector3 forward = parentRot * Vector3.forward;
            forward.y = 0f;
            leg.BendForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            leg.Valid = true;
            return leg;
        }

        /// <summary>
        /// Максимальный сдвиг таза (≤ 0), при котором опорная нога ещё дотягивается до земли.
        /// Больше 0 — таз можно не опускать; SolveCurve всё равно ограничит нулём.
        /// </summary>
        private static float RequiredLowering(LegFrame leg, float groundY)
        {
            if (!leg.Valid) return 0f;

            float l1 = (leg.Knee - leg.Hip).magnitude;
            float l2 = (leg.Foot - leg.Knee).magnitude;
            float maxReach = l1 + l2;
            float horiz = new Vector2(leg.Foot.x - leg.Hip.x, leg.Foot.z - leg.Hip.z).magnitude;

            if (horiz >= maxReach)
                return groundY - leg.Foot.y; // даже прямая нога не достаёт: честный residual ниже

            float vMax = Mathf.Sqrt(maxReach * maxReach - horiz * horiz);
            return groundY - leg.Hip.y + vMax;
        }

        private static MotionBricksFootIkResult SolveLegIk(
            int frameCount,
            MotionBricksBakeJob job,
            float groundY,
            float[] lowestFootY,
            int[] stanceFoot,
            float[] sourceRootY,
            LegFrame[] legL,
            LegFrame[] legR,
            Transform thighL, Transform shinL, Transform footL,
            Transform thighR, Transform shinR, Transform footR,
            List<Vector3> hipsOffsets,
            Dictionary<Transform, Quaternion[]> locals,
            Dictionary<Transform, Quaternion> worldRot,
            MotionBricksBakeReport report,
            List<string> warnings)
        {
            // Естественный bob: вертикаль корня источника. Для синтезированного «прыжка»
            // (22 см) таз за ней не ведём — это не походка, а артефакт клипа.
            float srcMin = Min(sourceRootY);
            float srcMax = Max(sourceRootY);
            float srcRange = srcMax - srcMin;
            float srcMean = 0f;
            for (int f = 0; f < frameCount; f++) srcMean += sourceRootY[f];
            srcMean /= Mathf.Max(1, frameCount);
            bool applyBob = srcRange <= MaxSourceBobForLegIk;

            var target = new float[frameCount];
            var upper = new float[frameCount];
            var raw = new float[frameCount];

            for (int f = 0; f < frameCount; f++)
            {
                target[f] = applyBob ? sourceRootY[f] - srcMean : 0f;
                raw[f] = groundY - lowestFootY[f];
                LegFrame stance = stanceFoot[f] == 0 ? legL[f] : legR[f];
                upper[f] = RequiredLowering(stance, groundY);
            }

            var curve = MotionBricksFootIk.SolveCurve(target, upper, job.Loop);
            for (int f = 0; f < frameCount; f++)
                hipsOffsets[f] += Vector3.up * curve.Corrections[f];

            float hover = 0f, penetration = 0f, maxResidual = 0f, maxKnee = 0f;
            int reachLimitedFrames = 0;

            for (int f = 0; f < frameCount; f++)
            {
                float pelvisOffset = curve.Corrections[f];
                bool plantL = stanceFoot[f] == 0;

                LegIkFrameOutcome outL = ApplyLegIkFrame(legL[f], plantL, pelvisOffset, groundY,
                    locals, worldRot, f, thighL, shinL, footL);
                LegIkFrameOutcome outR = ApplyLegIkFrame(legR[f], !plantL, pelvisOffset, groundY,
                    locals, worldRot, f, thighR, shinR, footR);

                if (outL.ReachLimited) reachLimitedFrames++;
                if (outR.ReachLimited) reachLimitedFrames++;
                maxResidual = Mathf.Max(maxResidual, Mathf.Abs(outL.Residual));
                maxResidual = Mathf.Max(maxResidual, Mathf.Abs(outR.Residual));
                maxKnee = Mathf.Max(maxKnee, Mathf.Max(outL.KneeBendDeg, outR.KneeBendDeg));

                float lowest = Mathf.Min(outL.FootY, outR.FootY);
                hover = Mathf.Max(hover, lowest - groundY);
                penetration = Mathf.Max(penetration, groundY - lowest);
            }

            var result = new MotionBricksFootIkResult
            {
                Mode = MotionBricksFootIkMode.LegIk,
                Corrections = curve.Corrections,
                BobBefore = Max(raw) - Min(raw),
                BobAfter = curve.Range,
                HoverMax = hover,
                PenetrationMax = penetration,
                Iterations = curve.Iterations,
                Cyclic = curve.Cyclic,
            };

            report.footIk.legIk = true;
            report.footIk.sourceBobApplied = applyBob;
            report.footIk.sourceBobCm = srcRange * 100f;
            report.footIk.reachLimitedFrames = reachLimitedFrames;
            report.footIk.maxResidualCm = maxResidual * 100f;
            report.footIk.kneeBendMaxDeg = maxKnee;

            if (reachLimitedFrames > 0)
                warnings.Add($"leg IK: цель недостижима в {reachLimitedFrames} кадрах, " +
                             $"макс residual {maxResidual * 100f:F1} см (стопа выше земли)");
            if (!result.ContactInTolerance)
                warnings.Add($"leg IK вне допуска: hover {hover * 100f:F1} см, " +
                             $"penetration {penetration * 100f:F1} см");

            return result;
        }

        /// <summary>
        /// Поставить одну ногу на groundY (или хотя бы не ниже) и переписать её locals.
        /// Маховая стопа выше земли не трогается — иначе походка станет «лунной».
        /// </summary>
        private static LegIkFrameOutcome ApplyLegIkFrame(
            LegFrame leg, bool plant, float pelvisOffset, float groundY,
            Dictionary<Transform, Quaternion[]> locals,
            Dictionary<Transform, Quaternion> worldRot,
            int frame, Transform thigh, Transform shin, Transform foot)
        {
            var outcome = new LegIkFrameOutcome { FootY = leg.Foot.y + pelvisOffset };
            if (!leg.Valid) return outcome;

            float currentY = leg.Foot.y + pelvisOffset;
            float targetY = plant ? groundY : Mathf.Max(currentY, groundY);

            // Маховая стопа уже выше земли — не трогаем.
            if (!plant && targetY <= currentY + 1e-5f) return outcome;

            Vector3 offset = Vector3.up * pelvisOffset;
            MotionBricksLegIkResult solved = MotionBricksLegIk.Solve(
                leg.Hip + offset, leg.Knee + offset, leg.Foot + offset,
                leg.ThighRotation, leg.ShinRotation, leg.FootRotation,
                targetY, leg.BendForward);

            if (!solved.Solved) return outcome;

            if (locals.TryGetValue(thigh, out Quaternion[] thighFrames) && frame < thighFrames.Length)
                thighFrames[frame] = Quaternion.Inverse(leg.ParentRotation) * solved.ThighRotation;
            if (locals.TryGetValue(shin, out Quaternion[] shinFrames) && frame < shinFrames.Length)
                shinFrames[frame] = Quaternion.Inverse(solved.ThighRotation) * solved.ShinRotation;
            if (locals.TryGetValue(foot, out Quaternion[] footFrames) && frame < footFrames.Length)
                footFrames[frame] = Quaternion.Inverse(solved.ShinRotation) * solved.FootRotation;

            worldRot[thigh] = solved.ThighRotation;
            worldRot[shin] = solved.ShinRotation;
            worldRot[foot] = solved.FootRotation;

            outcome.FootY = solved.Foot.y;
            outcome.Residual = solved.Residual;
            outcome.KneeBendDeg = solved.KneeBendDeg;
            outcome.ReachLimited = solved.ReachLimited;
            outcome.Applied = true;
            return outcome;
        }

        private static Quaternion WorldRotation(Transform t, Dictionary<Transform, Quaternion> worldRot)
        {
            if (t != null && worldRot.TryGetValue(t, out Quaternion value)) return value;
            return Quaternion.identity;
        }

        // === Humanoid-бейк (мышцы) ===

        /// <summary>
        /// Тот же ретаргет, но выход — мускульные кривые Humanoid: RootT/RootQ + 95 мышц.
        /// Поза снимается с УЖЕ СОБРАННОГО generic-клипа на временном инстансе БЕЗ Animator:
        /// Animator с Humanoid-аватаром перехватывает generic-кривые и не даёт их применить
        /// (T-MB05: humanoid-клип получался T-позой). HumanPoseHandler нужен только Avatar.
        /// Возвращает готовый клип (сохранение — в Bake, чтобы отчёт знал GUID).
        /// </summary>
        private static AnimationClip BakeHumanoidClip(
            AnimationClip genericClip,
            int frameCount,
            MotionBricksBakeJob job,
            List<string> warnings)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[MB-Bake] Humanoid-бейк пропущен: модель не найдена '{ModelPath}'.");
                warnings.Add("humanoid-клип не испечён: модель не найдена");
                return null;
            }

            var sampleHolder = new GameObject("__MB_Humanoid_Sample");
            sampleHolder.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var sampleRoot = UnityEngine.Object.Instantiate(modelAsset, sampleHolder.transform);
                var animator = sampleRoot.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogError("[MB-Bake] Humanoid-бейк пропущен: у модели нет Animator с Humanoid-аватаром.");
                    warnings.Add("humanoid-клип не испечён: нет Humanoid-аватара");
                    return null;
                }

                // Avatar — общий ассет; сам Animator на временном инстансе только мешает.
                Avatar avatar = animator.avatar;
                UnityEngine.Object.DestroyImmediate(animator);

                int muscleCount = HumanTrait.MuscleCount;
                var muscles = new float[frameCount][];
                var bodyPos = new Vector3[frameCount];
                var bodyRot = new Quaternion[frameCount];

                var handler = new HumanPoseHandler(avatar, sampleRoot.transform);
                var pose = new HumanPose { muscles = new float[muscleCount] };
                try
                {
                    for (int f = 0; f < frameCount; f++)
                    {
                        // НЕ сэмплируем ровно в clip.length: при loopTime=true SampleAnimation
                        // заворачивает t==length на первый кадр, и humanoid-клип заканчивался
                        // позой старта (рывок при повороте WalkToJog). Берём чуть раньше.
                        float t = Mathf.Min(f / Fps, genericClip.length - 1e-4f);
                        genericClip.SampleAnimation(sampleRoot, t);
                        handler.GetHumanPose(ref pose);

                        bodyPos[f] = pose.bodyPosition;
                        bodyRot[f] = pose.bodyRotation;
                        muscles[f] = (float[])pose.muscles.Clone();
                    }
                }
                finally
                {
                    handler.Dispose();
                }

                return BuildHumanoidClip(muscles, bodyPos, bodyRot, frameCount, job, warnings);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampleHolder);
            }
        }

        /// <summary>Собрать .anim из снятой humanoid-позы (мышцы + RootT/RootQ).</summary>
        private static AnimationClip BuildHumanoidClip(
            float[][] muscles,
            Vector3[] bodyPos,
            Quaternion[] bodyRot,
            int frameCount,
            MotionBricksBakeJob job,
            List<string> warnings)
        {
            int muscleCount = HumanTrait.MuscleCount;

            // T-MB05 runtime repro: на WalkToJog Arm Twist In-Out перескакивал с +1 на −1
            // (полный разворот кисти/плеча за кадр, 178.8° на кости). Это представление
            // twist-мышцы у полюса ±180°: +1 и −1 — одна и та же ориентация. Разворачиваем
            // скачок на 2.0, чтобы humanoid-клип не выворачивал руку.
            int unwrappedTwists = 0;
            int clampedArmTwists = 0;
            for (int m = 0; m < muscleCount; m++)
            {
                if (HumanTrait.MuscleName[m].IndexOf("Twist", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                for (int f = 1; f < frameCount; f++)
                {
                    float previous = muscles[f - 1][m];
                    float current = muscles[f][m];

                    while (current - previous > 1.0f) { current -= 2.0f; unwrappedTwists++; }
                    while (current - previous < -1.0f) { current += 2.0f; unwrappedTwists++; }

                    muscles[f][m] = current;
                }

                // Абсолютный полюс: Arm Twist на WalkToJog держится около 2.0 (полный оборот),
                // а humanoid-плеер клампит в [-1..1]. Сдвигаем всю кривую на ±2.0, чтобы
                // сохранить ориентацию и не пересекать 2.0 (иначе рука переворачивается).
                float mean = 0f;
                for (int f = 0; f < frameCount; f++) mean += muscles[f][m];
                mean /= Mathf.Max(1, frameCount);

                float offset = 0f;
                while (mean + offset > 1f) { offset -= 2f; }
                while (mean + offset < -1f) { offset += 2f; }

                if (Mathf.Abs(offset) > 1e-4f)
                {
                    for (int f = 0; f < frameCount; f++) muscles[f][m] += offset;
                    unwrappedTwists += frameCount;
                }

                // Arm Twist на WalkToJog накапливает полный оборот (2.1) — humanoid-плеер
                // клампит и на полюсе 2.0 рука переворачивается на 178.8°. Ограничиваем
                // скрутку плеча/предплечья ±0.5 (90°): направление руки задают swing-мышцы,
                // а roll больше 90° для locomotion не нужен и выглядит вывернутым.
                string muscleName = HumanTrait.MuscleName[m];
                if (muscleName.IndexOf("Arm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    for (int f = 0; f < frameCount; f++)
                        muscles[f][m] = Mathf.Clamp(muscles[f][m], -0.5f, 0.5f);
                    clampedArmTwists++;
                }
            }

            if (unwrappedTwists > 0)
                Debug.Log($"[MB-Bake] twist-мышцы: развёрнуто скачков {unwrappedTwists} " +
                          "(представление ±180°, не движение позы)");
            if (clampedArmTwists > 0)
                Debug.Log($"[MB-Bake] arm twist: ограничено {clampedArmTwists} кривых до ±0.5 " +
                          "(humanoid-полюс ±180°/360° иначе переворачивает руку)");

            // T-MB05 runtime repro: Animator клампит мышцы в [-1..1], а бейкер писал сырые
            // значения (Shoulder Front-Back до 11.6). Глобальный/плечевой кламп менял
            // penetration и yaw (humanoid solver связан), поэтому пока не применяем его
            // вслепую: сначала runtime-замер на probe, затем точечный фикс ретаргета.
            // Arm Twist уже ограничен выше (±0.5) — именно он давал 178.8° переворот.

            // Контроль: Humanoid-плеер клампит мышцы в [-1..1] по лимитам аватара.
            // Выход за диапазон = поза в игре будет не такой, как в бейке (вывернутые конечности).
            var outOfRange = new List<string>();
            for (int m = 0; m < muscleCount; m++)
            {
                float mn = float.MaxValue, mx = float.MinValue;
                for (int f = 0; f < frameCount; f++)
                {
                    mn = Mathf.Min(mn, muscles[f][m]);
                    mx = Mathf.Max(mx, muscles[f][m]);
                }

                if (mn < -1.001f || mx > 1.001f) outOfRange.Add($"{HumanTrait.MuscleName[m]} {mn:F2}..{mx:F2}");
            }

            if (outOfRange.Count > 0)
                warnings.Add($"мышц вне [-1..1]: {outOfRange.Count} — {string.Join(", ", outOfRange)}");

            var clip = new AnimationClip { frameRate = Fps, legacy = false };

            // Непрерывность знака кватерниона корня.
            var rootRot = (Quaternion[])bodyRot.Clone();
            for (int f = 1; f < frameCount; f++)
            {
                if (Quaternion.Dot(rootRot[f - 1], rootRot[f]) < 0f)
                    rootRot[f] = new Quaternion(-rootRot[f].x, -rootRot[f].y, -rootRot[f].z, -rootRot[f].w);
            }

            var rootCurves = new AnimationCurve[7];
            for (int c = 0; c < 7; c++) rootCurves[c] = new AnimationCurve();

            for (int f = 0; f < frameCount; f++)
            {
                float time = f / Fps;
                rootCurves[0].AddKey(time, bodyPos[f].x);
                rootCurves[1].AddKey(time, bodyPos[f].y);
                rootCurves[2].AddKey(time, bodyPos[f].z);
                rootCurves[3].AddKey(time, rootRot[f].x);
                rootCurves[4].AddKey(time, rootRot[f].y);
                rootCurves[5].AddKey(time, rootRot[f].z);
                rootCurves[6].AddKey(time, rootRot[f].w);
            }

            string[] rootProps = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int c = 0; c < 7; c++)
                clip.SetCurve(string.Empty, typeof(Animator), rootProps[c], rootCurves[c]);

            for (int m = 0; m < muscleCount; m++)
            {
                var curve = new AnimationCurve();
                for (int f = 0; f < frameCount; f++) curve.AddKey(f / Fps, muscles[f][m]);
                clip.SetCurve(string.Empty, typeof(Animator), HumanTrait.MuscleName[m], curve);
            }

            clip.EnsureQuaternionContinuity();

            Debug.Log($"[MB-Bake] Humanoid-клип ({job.Label}): кадров {frameCount}, " +
                      $"кривых {7 + muscleCount} (root 7 + мышц {muscleCount}), " +
                      $"bodyPosition.y {bodyPos[0].y:F3}..{bodyPos[frameCount - 1].y:F3} m");

            return clip;
        }

        // === Сохранение ===

        /// <summary>
        /// T-MB05: one-shot клипы не должны зацикливаться ни в ассете, ни в PlayableGraph
        /// по умолчанию. Для петли ставим Loop; для one-shot — Clamp + loopTime=false.
        /// </summary>
        internal static void ApplyLoopSettings(AnimationClip clip, bool loop)
        {
            if (clip == null) return;

            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Clamp;

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private struct ClipSaveInfo
        {
            public string GuidBefore;
            public string Guid;
            public bool GuidStable;
            public int CurveCount;
            public float Length;
            public float FrameRate;
            public bool HumanMotion;
        }

        /// <summary>
        /// Сохранить запечённый клип, не ломая ссылки из сцен и префабов.
        /// Если ассет уже существует — кривые перезаписываются на месте (GUID и fileID те же),
        /// иначе создаётся новый ассет. Delete+Create нельзя: сцена держит ссылку по GUID.
        /// T-MB03: GUID сверяется явно и попадает в отчёт (`guidStable`).
        /// </summary>
        private static MotionBricksBakeReportClip SaveWithReport(AnimationClip baked, string path)
        {
            var info = SaveClipInPlace(baked, path);
            return new MotionBricksBakeReportClip
            {
                path = path,
                guid = info.Guid,
                guidBefore = info.GuidBefore,
                guidStable = info.GuidStable,
                curveCount = info.CurveCount,
                lengthSeconds = info.Length,
                frameCount = info.FrameRate > 0.01f ? Mathf.RoundToInt(info.Length * info.FrameRate) + 1 : 0,
                frameRate = info.FrameRate,
                humanMotion = info.HumanMotion,
            };
        }

        private static ClipSaveInfo SaveClipInPlace(AnimationClip baked, string path)
        {
            var info = new ClipSaveInfo();

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(baked, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var created = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (created != null)
                {
                    info.Guid = AssetDatabase.AssetPathToGUID(path);
                    info.GuidBefore = string.Empty;
                    info.GuidStable = true; // новый ассет: сравнивать не с чем
                    info.CurveCount = AnimationUtility.GetCurveBindings(created).Length;
                    info.Length = created.length;
                    info.FrameRate = created.frameRate;
                    info.HumanMotion = created.humanMotion;
                }

                return info;
            }

            info.GuidBefore = AssetDatabase.AssetPathToGUID(path);

            existing.ClearCurves();
            existing.frameRate = baked.frameRate;
            existing.wrapMode = baked.wrapMode;

            foreach (var binding in AnimationUtility.GetCurveBindings(baked))
                existing.SetCurve(binding.path, binding.type, binding.propertyName, AnimationUtility.GetEditorCurve(baked, binding));

            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(baked))
                AnimationUtility.SetObjectReferenceCurve(existing, binding, AnimationUtility.GetObjectReferenceCurve(baked, binding));

            AnimationUtility.SetAnimationClipSettings(existing, AnimationUtility.GetAnimationClipSettings(baked));

            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();

            info.Guid = AssetDatabase.AssetPathToGUID(path);
            info.GuidStable = !string.IsNullOrEmpty(info.Guid) &&
                              string.Equals(info.GuidBefore, info.Guid, StringComparison.Ordinal);
            info.CurveCount = AnimationUtility.GetCurveBindings(existing).Length;
            info.Length = existing.length;
            info.FrameRate = existing.frameRate;
            info.HumanMotion = existing.humanMotion;

            if (!info.GuidStable)
                Debug.LogWarning($"[MB-Bake] {path}: GUID изменился ({info.GuidBefore} → {info.Guid}); " +
                                 "ссылки из сцены могли потеряться.");

            return info;
        }

        // === Ретаргет одного кадра (обход иерархии рига) ===

        private static void EvalFrame(
            Transform t,
            Quaternion animParentWorld,
            Vector3 animParentPos,
            int frame,
            Dictionary<int, Quaternion> deltaByPose,
            Dictionary<int, Quaternion> clavByPose,
            Dictionary<Transform, Quaternion> restWorld,
            Dictionary<Transform, int> poseByTransform,
            Dictionary<Transform, Quaternion[]> locals,
            Dictionary<Transform, Vector3> worldPos,
            Dictionary<Transform, Quaternion> worldRot,
            Transform hips,
            Vector3 hipsLocalPos)
        {
            Quaternion restLocal = t.localRotation;
            Quaternion animWorld;

            if (poseByTransform.TryGetValue(t, out int pose) && deltaByPose.TryGetValue(pose, out Quaternion delta))
            {
                animWorld = delta * restWorld[t];
            }
            else if (poseByTransform.TryGetValue(t, out pose) && clavByPose.TryGetValue(pose, out Quaternion clavSwing))
            {
                // Ключица: swing направления chest->arm поверх своей rest-ориентации.
                animWorld = clavSwing * restWorld[t];
            }
            else
            {
                // Кости без отображённого джойнта (голова, кисти, пальцы стоп, челюсть)
                // наследуют поворот родителя со своим rest-локалом — а не нулевым кватернионом.
                animWorld = animParentWorld * restLocal;
            }

            if (locals.TryGetValue(t, out var frames)) frames[frame] = Quaternion.Inverse(animParentWorld) * animWorld;
            worldRot[t] = animWorld;

            Vector3 localPos = ReferenceEquals(t, hips) ? hipsLocalPos : t.localPosition;
            Vector3 worldPosition = animParentPos + animParentWorld * localPos;
            worldPos[t] = worldPosition;

            for (int i = 0; i < t.childCount; i++)
            {
                EvalFrame(t.GetChild(i), animWorld, worldPosition, frame, deltaByPose, clavByPose,
                    restWorld, poseByTransform, locals, worldPos, worldRot, hips, hipsLocalPos);
            }
        }

        // === Хелперы ===

        private static int FindPoseByBone(string boneName)
        {
            foreach (var (pose, bone) in BoneMap)
                if (bone == boneName) return pose;
            return -1;
        }

        /// <summary>
        /// SOMA parent-local -> global в осях Unity. Позиции нужны для направлений костей,
        /// повороты — только чтобы их получить.
        /// </summary>
        private static void SomaPose(float[] frame, Quaternion[] rot, Vector3[] pos)
        {
            for (int j = 0; j < Joints; j++)
            {
                int o = j * FloatsPerJoint;
                // Конверсия делается ДО композиции: M(q_p * q_l)M = (M q_p M)(M q_l M).
                Quaternion localRot = ToUnityRot(new Quaternion(frame[o + 3], frame[o + 4], frame[o + 5], frame[o + 6]));
                Vector3 localPos = ToUnityPos(frame[o], frame[o + 1], frame[o + 2]);

                int parent = Parents[j];
                if (parent < 0)
                {
                    rot[j] = localRot;
                    pos[j] = localPos;
                }
                else
                {
                    rot[j] = rot[parent] * localRot;
                    pos[j] = pos[parent] + rot[parent] * localPos;
                }
            }
        }

        private static Vector3 ToUnityPos(float x, float y, float z) => new Vector3(-x, y, z);

        private static Quaternion ToUnityRot(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        private static void CollectRestWorld(Transform t, Quaternion parentWorld, Dictionary<Transform, Quaternion> acc)
        {
            var world = parentWorld * t.localRotation;
            acc[t] = world;
            for (int i = 0; i < t.childCount; i++) CollectRestWorld(t.GetChild(i), world, acc);
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

        private static string RelativePath(Transform root, Transform t)
        {
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static float[] ReadFloats(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length % 4 != 0) throw new InvalidDataException($"{path}: размер {bytes.Length} не кратен 4");

            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private static float Min(float[] values)
        {
            float m = float.MaxValue;
            foreach (float v in values) m = Mathf.Min(m, v);
            return values.Length > 0 ? m : 0f;
        }

        private static float Max(float[] values)
        {
            float m = float.MinValue;
            foreach (float v in values) m = Mathf.Max(m, v);
            return values.Length > 0 ? m : 0f;
        }

        // Иерархия SOMA (77 джойнтов), индекс -> родитель. Источник: sa-inbetween src/SA.Inbetween.Engine/Kernels.cs.
        private static readonly int[] Parents =
        {
            -1, 0, 1, 2, 3, 4, 5, 6, 6, 6, 6, 3, 11, 12, 13, 14, 15, 16, 17, 14, 19, 20, 21, 22, 14, 24, 25, 26, 27, 14,
            29, 30, 31, 32, 14, 34, 35, 36, 37, 3, 39, 40, 41, 42, 43, 44, 45, 42, 47, 48, 49, 50, 42, 52, 53, 54, 55, 42,
            57, 58, 59, 60, 42, 62, 63, 64, 65, 0, 67, 68, 69, 70, 0, 72, 73, 74, 75,
        };

        // pose-индекс SOMA -> имя кости рига HumanM_Model.
        // Пропущены Spine2(2) и Neck2(5): у рига одна spine- и одна neck-кость.
        private static readonly (int Pose, string Bone)[] BoneMap =
        {
            (0, "B-hips"), (1, "B-spine"), (3, "B-chest"), (4, "B-neck"), (6, "B-head"), (8, "B-jaw"),
            (11, "B-shoulder.L"), (12, "B-upperArm.L"), (13, "B-forearm.L"), (14, "B-hand.L"),
            (39, "B-shoulder.R"), (40, "B-upperArm.R"), (41, "B-forearm.R"), (42, "B-hand.R"),
            (67, "B-thigh.L"), (68, "B-shin.L"), (69, "B-foot.L"), (70, "B-toe.L"),
            (72, "B-thigh.R"), (73, "B-shin.R"), (74, "B-foot.R"), (75, "B-toe.R"),
        };

        // Цепочки для переноса полного мирового поворота: (кость, дочерний джойнт) в индексах pose.
        // Дочерний джойнт — ближайший ОТОБРАЖЁННЫЙ: у spine роль ребёнка играет chest (j2/Spine2 пропущен).
        private static readonly (int Bone, int Child)[] Segments =
        {
            (0, 1), (1, 3), (3, 4), (4, 6),
            (12, 13), (13, 14),
            (40, 41), (41, 42),
            (67, 68), (68, 69), (69, 70),
            (72, 73), (73, 74), (74, 75),
        };

        // Ключицы: (грудной джойнт, плечевой джойнт, кость рига). Направление chest->shoulderJoint.
        // Именно 3->11 / 3->39, НЕ 3->12: джойнт upperArm уводится собственной амплитудой руки
        // (в ходьбе 75°), и направление chest->upperArm мерит размах руки, а не ключицу.
        private static readonly (int Chest, int Arm, string Bone)[] Clavicles =
        {
            (3, 11, "B-shoulder.L"),
            (3, 39, "B-shoulder.R"),
        };

        // Цепочка руки, которая наследует поворот ключицы (плечо, предплечье, кисть).
        // Индексируется так же, как Clavicles.
        private static readonly int[][] ClavicleChains =
        {
            new[] { 12, 13, 14 },
            new[] { 40, 41, 42 },
        };
    }
}
