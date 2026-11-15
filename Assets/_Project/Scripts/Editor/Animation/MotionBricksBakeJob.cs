// Project C: T-MB03 — задания на бейк MotionBricks без хардкода конкретного клипа.
//
// Раньше бейкер знал ровно два клипа (`WalkForward`/`WalkToJog`) статическими полями.
// Здесь задание собирается ИЗ ДИСКА: всё, что лежит в
//   Assets/_Project/Animations/Generated/MotionBricks/<primitiveId>/<bodyType>/
// и содержит `<ClipBase>_30fps.f32` + `soma_bind.f32`, становится заданием.
// Добавили новую папку с сырым клипом — он появился в batch-бейке без правки кода.
//
// Необязательный sidecar `<ClipBase>.bake.config.json` переопределяет поведение:
//   { "groundFeet": false, "bakeInPlace": false, "clipSpeed": 0.0, "label": "Jump" }
// `clipSpeed` справочная: сколько м/с должен давать контроллер, чтобы стопы не скользили.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    /// <summary>Sidecar-настройки одного клипа. Читается из `&lt;ClipBase&gt;.bake.config.json`.</summary>
    [Serializable]
    public sealed class MotionBricksBakeConfig
    {
        /// <summary>Прижимать опорную стопу к земле (тазовый foot IK). Для прыжка/полёта — false.</summary>
        public bool groundFeet = true;

        /// <summary>Выбрасывать горизонталь корня. false — клип несёт перенос (root motion).</summary>
        public bool bakeInPlace = true;

        /// <summary>Справочная скорость опоры, м/с (пишется в отчёт для настройки root motion).</summary>
        public float clipSpeed;

        /// <summary>Человекочитаемая метка задания; пусто — соберётся из путей.</summary>
        public string label;
    }

    /// <summary>Одно задание бейка: сырой клип + куда/как печь.</summary>
    public sealed class MotionBricksBakeJob
    {
        public string PrimitiveId;
        public string BodyType;
        public string RawFile;
        public string ClipBase;

        public bool GroundFeet = true;
        public bool BakeInPlace = true;
        public float ClipSpeed;
        public string Label;

        /// <summary>Откуда взялись настройки: "default" или имя sidecar-файла.</summary>
        public string ConfigSource = "default";

        public string SourceFolder => MotionBricksBakePaths.GeneratedRoot + "/" + PrimitiveId + "/" + BodyType;
        public string RawPath => SourceFolder + "/" + RawFile;
        public string ConfigPath => SourceFolder + "/" + ClipBase + ".bake.config.json";
        public string ClipOut => SourceFolder + "/" + ClipBase + ".anim";
        public string HumanoidClipOut => SourceFolder + "/" + ClipBase + "_Humanoid.anim";
        public string ReportPath => SourceFolder + "/" + ClipBase + ".bake.report.json";

        public override string ToString() =>
            $"{PrimitiveId}/{BodyType}/{ClipBase} (groundFeet={GroundFeet}, inPlace={BakeInPlace}, config={ConfigSource})";

        // === Обнаружение ===

        /// <summary>Все задания, найденные на диске. Порядок стабильный: primitive, body, clip.</summary>
        public static List<MotionBricksBakeJob> Discover()
        {
            var jobs = new List<MotionBricksBakeJob>();
            string root = MotionBricksBakePaths.GeneratedRoot;
            if (!Directory.Exists(root)) return jobs;

            string[] primitives = Directory.GetDirectories(root);
            Array.Sort(primitives, StringComparer.OrdinalIgnoreCase);

            foreach (string primitiveDir in primitives)
            {
                string[] bodyDirs = Directory.GetDirectories(primitiveDir);
                Array.Sort(bodyDirs, StringComparer.OrdinalIgnoreCase);

                foreach (string bodyDir in bodyDirs)
                {
                    string primitiveId = Path.GetFileName(primitiveDir);
                    string bodyType = Path.GetFileName(bodyDir);

                    if (!File.Exists(bodyDir + "/" + MotionBricksBakePaths.RestFile)) continue;

                    string[] rawFiles = Directory.GetFiles(bodyDir, "*" + MotionBricksBakePaths.RawSuffix);
                    Array.Sort(rawFiles, StringComparer.OrdinalIgnoreCase);

                    foreach (string rawPath in rawFiles)
                    {
                        string fileName = Path.GetFileName(rawPath);
                        string clipBase = fileName.Substring(
                            0, fileName.Length - MotionBricksBakePaths.RawSuffix.Length);

                        jobs.Add(Create(primitiveId, bodyType, fileName, clipBase));
                    }
                }
            }

            return jobs;
        }

        /// <summary>Найти задание по primitive/body (для совместимых пунктов меню).</summary>
        public static MotionBricksBakeJob Find(string primitiveId, string bodyType)
        {
            foreach (var job in Discover())
            {
                if (string.Equals(job.PrimitiveId, primitiveId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(job.BodyType, bodyType, StringComparison.OrdinalIgnoreCase))
                    return job;
            }

            return null;
        }

        private static MotionBricksBakeJob Create(string primitiveId, string bodyType, string rawFile, string clipBase)
        {
            var job = new MotionBricksBakeJob
            {
                PrimitiveId = primitiveId,
                BodyType = bodyType,
                RawFile = rawFile,
                ClipBase = clipBase,
            };

            job.LoadConfig();
            job.Label = string.IsNullOrEmpty(job.Label)
                ? $"{primitiveId}/{bodyType}/{clipBase}"
                : job.Label;

            return job;
        }

        private void LoadConfig()
        {
            if (!File.Exists(ConfigPath)) return;

            try
            {
                var config = JsonUtility.FromJson<MotionBricksBakeConfig>(File.ReadAllText(ConfigPath));
                if (config == null) return;

                GroundFeet = config.groundFeet;
                BakeInPlace = config.bakeInPlace;
                ClipSpeed = Mathf.Max(0f, config.clipSpeed);
                Label = config.label;
                ConfigSource = Path.GetFileName(ConfigPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MB-Bake] {ConfigPath}: не разобран ({e.Message}) — взяты настройки по умолчанию.");
            }
        }
    }

    /// <summary>Общие пути бейка/валидации: одно место вместо копий строк по файлам.</summary>
    public static class MotionBricksBakePaths
    {
        public const string ModelPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        public const string GeneratedRoot = "Assets/_Project/Animations/Generated/MotionBricks";

        /// <summary>Настоящая bind-поза SOMA (не `soma_rest.f32`: там произвольный кадр библиотеки).</summary>
        public const string RestFile = "soma_bind.f32";

        public const string RawSuffix = "_30fps.f32";
        public const int Joints = 77;
        public const int FloatsPerJoint = 7;
        public const float Fps = 30f;

        /// <summary>Версия формата отчёта/логики бейка. Меняется вместе с поведением.</summary>
        public const int ReportSchema = 2;
    }
}
