// Project C: T-MB03 — метаданные бейка MotionBricks.
//
// Раньше у клипа был только .anim (и оставшийся от Python-пайплайна `*.bake.json`).
// Теперь бейкер пишет рядом `<ClipBase>.bake.report.json`: из чего испечён клип (хэши сырья),
// какими настройками, что сделал foot IK и прошёл ли клип валидатор. Это дёшево (текст),
// не тянет Unity-ассеты и позволяет сравнить два перепёка без запуска редактора.
//
// Важно: `.json` и `.meta` к нему создаёт Unity; руками .meta не пишем (HARD).

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    [Serializable]
    public sealed class MotionBricksBakeReport
    {
        public int schema;
        public string ticket = "T-MB03";
        public string generatedUtc;

        public string modelPath;
        public string restFile;
        public string restSha256;
        public string sourceFile;
        public string sourceSha256;
        public int sourceFrames;
        public float sourceDurationSeconds;
        public int joints;
        public int floatsPerJoint;

        public bool bakeInPlace;
        public bool groundFeet;
        public float yawOffsetDeg;
        public float maxClavicleDeg;

        public MotionBricksBakeReportFootIk footIk = new MotionBricksBakeReportFootIk();
        public MotionBricksBakeReportClip genericClip = new MotionBricksBakeReportClip();
        public MotionBricksBakeReportClip humanoidClip = new MotionBricksBakeReportClip();
        public MotionBricksBakeReportValidation validation = new MotionBricksBakeReportValidation();
        public string[] warnings = Array.Empty<string>();
    }

    [Serializable]
    public sealed class MotionBricksBakeReportFootIk
    {
        public string mode;
        public float bobBeforeCm;
        public float bobAfterCm;
        public float hoverCm;
        public float penetrationCm;
        public float correctionMinCm;
        public float correctionMaxCm;
        public int iterations;
        public bool contactInTolerance;

        // Покадровая трасса: по ней видно, какая стопа опорная и сколько таз реально съехал.
        public float[] lowestFootYCm = Array.Empty<float>();
        public float[] correctionsCm = Array.Empty<float>();
        public int[] stanceFoot = Array.Empty<int>(); // 0 = L, 1 = R
    }

    [Serializable]
    public sealed class MotionBricksBakeReportClip
    {
        public string path;
        public string guid;
        public string guidBefore;
        public bool guidStable;
        public int curveCount;
        public float lengthSeconds;
        public int frameCount;
        public float frameRate;
        public bool humanMotion;
    }

    [Serializable]
    public sealed class MotionBricksBakeReportValidation
    {
        public bool ran;
        public bool pass;
        public string label;
        public string[] failures = Array.Empty<string>();

        public float hipsYMin;
        public float hipsYMax;
        public float stanceHoverM;
        public float penetrationM;
        public float footSlideMaxM;
        public int contactFrames;
        public float speedL;
        public float speedR;
        public float impliedSpeed;
        public float speedAsymmetry;
        public float axisErrorDeg;
        public float yawDriftDeg;
        public float clavicleLDeg;
        public float clavicleRDeg;
        public float armLDeg;
        public float armRDeg;
    }

    public static class MotionBricksHashing
    {
        public static string Sha256(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Записать текст без BOM (Unity читает json и так, но BOM мешает diff'ам).</summary>
        public static void WriteUtf8(string path, string text) =>
            File.WriteAllText(path, text, new UTF8Encoding(false));
    }
}
