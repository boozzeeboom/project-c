// JitterClipProbe — T-JITTER12 diagnostic (edit-mode, no play mode required).
// Сэмплирует AnimationClip через AnimationMode.SampleAnimationClip — тот же путь,
// что использует окно Animation (корректно оценивает HUMANOID muscle-клипы в редакторе).
// Измеряет покадровый шум костей:
//   - величина дельт позиции (мм) и поворота (град) между сэмплами
//   - количество смен знака дельты (высокочастотная осцилляция = «тряска»)
//   - RMS второй разности (плавность кривой)
// Прогоны:
//   A) HumanM@Idle01 (Humanoid) @ ~90fps с неравномерным dt — имитация рантайма
//   B) тот же клип, модель на 3000 юнитов от origin (float precision)
//   C) HumanM_Model@Idle.fbx (Generic-вариант) — контроль
// Интерпретация:
//   - A шумный (sign-flips почти каждый шаг) → шум в humanoid-конверсии/клипе
//   - A чист, B шумный → float precision (расстояние от origin)
//   - A и B чисты → проблема НИЖЕ аниматора (GPU skinning / рендер / сеть)
using System.Text;
using UnityEditor;
using UnityEngine;

public static class JitterClipProbe
{
    private const string ModelPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
    private const string HumanoidClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
    private const string GenericClipPath = "Assets/_Project/Animations/HumanM_Model@Idle.fbx";

    private class BoneStat
    {
        public string name;
        public Vector3 prevPos;
        public Quaternion prevRot;
        public Vector3 prevDelta;
        public int signFlips;
        public int steps;
        public float maxDeltaMm;
        public float maxRotDeg;
        public double sumSqSecondDiff;
        public Vector3 prevPrevPos;

        public void Reset(Transform t)
        {
            prevPos = t.position;
            prevRot = t.rotation;
            prevDelta = Vector3.zero;
            prevPrevPos = t.position;
            signFlips = 0; steps = 0;
            maxDeltaMm = 0; maxRotDeg = 0;
            sumSqSecondDiff = 0;
        }

        public void Sample(Transform t)
        {
            Vector3 delta = t.position - prevPos;
            float mm = delta.magnitude * 1000f;
            if (mm > maxDeltaMm) maxDeltaMm = mm;

            float rotDeg = Quaternion.Angle(prevRot, t.rotation);
            if (rotDeg > maxRotDeg) maxRotDeg = rotDeg;

            if (steps > 0)
            {
                if ((Mathf.Sign(delta.x) != Mathf.Sign(prevDelta.x) && Mathf.Abs(delta.x) > 1e-5f && Mathf.Abs(prevDelta.x) > 1e-5f) ||
                    (Mathf.Sign(delta.y) != Mathf.Sign(prevDelta.y) && Mathf.Abs(delta.y) > 1e-5f && Mathf.Abs(prevDelta.y) > 1e-5f) ||
                    (Mathf.Sign(delta.z) != Mathf.Sign(prevDelta.z) && Mathf.Abs(delta.z) > 1e-5f && Mathf.Abs(prevDelta.z) > 1e-5f))
                    signFlips++;

                Vector3 secondDiff = t.position - 2f * prevPos + prevPrevPos;
                sumSqSecondDiff += secondDiff.sqrMagnitude;
            }

            prevPrevPos = prevPos;
            prevPos = t.position;
            prevDelta = delta;
            prevRot = t.rotation;
            steps++;
        }

        public string Report(float totalTime)
        {
            float rmsSecondMm = steps > 1 ? Mathf.Sqrt((float)(sumSqSecondDiff / (steps - 1))) * 1000f : 0f;
            float flipsPerSec = totalTime > 0 ? signFlips / totalTime : 0f;
            return $"  {name,-10} maxStep={maxDeltaMm,8:F3}mm  maxRot={maxRotDeg,7:F4}deg  signFlips={signFlips,4} ({flipsPerSec,5:F1}/s)  rms2nd={rmsSecondMm,7:F4}mm";
        }
    }

    public static string Execute()
    {
        var sb = new StringBuilder();
        var model = AssetDatabase.LoadMainAssetAtPath(ModelPath) as GameObject;
        if (model == null) return "FAIL: model not found at " + ModelPath;

        var humanoidClip = LoadClip(HumanoidClipPath, "HumanM@Idle01");
        var genericClip = LoadClip(GenericClipPath, null);

        sb.AppendLine($"[JitterProbe] clip(humanoid)={(humanoidClip != null ? humanoidClip.name : "NOT FOUND")} len={(humanoidClip != null ? humanoidClip.length : 0):F2}s frameRate={(humanoidClip != null ? humanoidClip.frameRate : 0):F0} humanMotion={(humanoidClip != null ? humanoidClip.humanMotion.ToString() : "-")}");
        sb.AppendLine($"[JitterProbe] clip(generic)={(genericClip != null ? genericClip.name : "NOT FOUND")} humanMotion={(genericClip != null ? genericClip.humanMotion.ToString() : "-")}");

        if (humanoidClip != null)
        {
            RunPass(sb, model, humanoidClip, "A) Humanoid Idle01 @ origin, ~90fps uneven dt", Vector3.zero, seed: 42);
            RunPass(sb, model, humanoidClip, "B) Humanoid Idle01 @ (3000,0,0)", new Vector3(3000f, 0f, 0f), seed: 42);
        }
        if (genericClip != null)
        {
            RunPass(sb, model, genericClip, "C) Generic @Idle @ origin, ~90fps uneven dt", Vector3.zero, seed: 42);
        }

        var report = sb.ToString();
        Debug.Log(report);
        return report;
    }

    /// <summary>
    /// Инспекция SkinnedMeshRenderer'ов модели: culling-настройки, AABB, rootBone.
    /// Вызывается отдельно (methodName=InspectSmr).
    /// </summary>
    public static string InspectSmr()
    {
        var sb = new StringBuilder();
        var model = AssetDatabase.LoadMainAssetAtPath(ModelPath) as GameObject;
        if (model == null) return "FAIL: model not found";

        sb.AppendLine($"[SmrInspect] model={model.name}");
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            sb.AppendLine($"  SMR '{smr.name}': updateWhenOffscreen={smr.updateWhenOffscreen} " +
                          $"skinnedMotionVectors={smr.skinnedMotionVectors} " +
                          $"localBounds.center={smr.localBounds.center} size={smr.localBounds.size} " +
                          $"rootBone={(smr.rootBone != null ? smr.rootBone.name : "NULL")} " +
                          $"bones={smr.bones.Length} quality={smr.quality}");
        }

        // Иерархия с масштабами — нестандартный scale в цепочке костей усиливает шум
        var t = model.transform;
        sb.AppendLine("  Hierarchy scales:");
        foreach (var tr in model.GetComponentsInChildren<Transform>(true))
        {
            if (tr.localScale != Vector3.one)
                sb.AppendLine($"    {GetPath(tr, t)} localScale={tr.localScale}");
        }

        var report = sb.ToString();
        Debug.Log(report);
        return report;
    }

    private static string GetPath(Transform t, Transform root)
    {
        string p = t.name;
        while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    private static AnimationClip LoadClip(string path, string exactName)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (a is AnimationClip c && !c.name.StartsWith("__preview"))
            {
                if (exactName == null || c.name == exactName) return c;
            }
        }
        return null;
    }

    private static void RunPass(StringBuilder sb, GameObject modelPrefab, AnimationClip clip, string label, Vector3 worldOffset, int seed)
    {
        sb.AppendLine(label);
        var go = Object.Instantiate(modelPrefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.position = worldOffset;

        bool animationModeStarted = false;
        try
        {
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            if (animator.avatar == null || !animator.avatar.isValid)
            {
                sb.AppendLine("  SKIP: avatar missing/invalid");
                return;
            }
            sb.AppendLine($"  avatar={animator.avatar.name} isHuman={animator.isHuman}");

            var bones = new[]
            {
                new { bone = HumanBodyBones.Hips, stat = new BoneStat { name = "Hips" } },
                new { bone = HumanBodyBones.Head, stat = new BoneStat { name = "Head" } },
                new { bone = HumanBodyBones.LeftHand, stat = new BoneStat { name = "Hand.L" } },
                new { bone = HumanBodyBones.LeftFoot, stat = new BoneStat { name = "Foot.L" } },
            };

            foreach (var b in bones)
            {
                if (animator.GetBoneTransform(b.bone) == null)
                {
                    sb.AppendLine($"  SKIP: bone {b.bone} not mapped in avatar");
                    return;
                }
            }

            AnimationMode.StartAnimationMode();
            animationModeStarted = true;
            AnimationMode.BeginSampling();

            // Прогрев
            AnimationMode.SampleAnimationClip(go, clip, 0f);
            AnimationMode.EndSampling();
            foreach (var b in bones) b.stat.Reset(animator.GetBoneTransform(b.bone));

            var rng = new System.Random(seed);
            const int steps = 600; // ~6.7с при ~90fps
            float t = 0f;
            for (int s = 0; s < steps; s++)
            {
                float dt = Mathf.Lerp(0.007f, 0.016f, (float)rng.NextDouble());
                t += dt;

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, clip, t % clip.length);
                AnimationMode.EndSampling();

                foreach (var b in bones)
                    b.stat.Sample(animator.GetBoneTransform(b.bone));
            }

            foreach (var b in bones) sb.AppendLine(b.stat.Report(t));
        }
        finally
        {
            if (animationModeStarted) AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(go);
        }
    }
}
