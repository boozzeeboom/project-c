// Project C: T-MB05 — проверка флага loop у запечённых клипов без бейка и без Play Mode.
//
// Что проверяем:
//   1. sidecar `{ "loop": false }` действительно выключает петлю, а отсутствие поля
//      оставляет прежний дефолт (true) — JsonUtility не должен затирать инициализатор;
//   2. `ApplyLoopSettings(clip, false)` ставит loopTime=false и WrapMode.Clamp,
//      а `true` возвращает Loop — именно это увидит PlayableGraph/Animator.

using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public static class MotionBricksLoopSelfTest
    {
        [MenuItem("ProjectC/MotionBricks/Bake/Loop settings self-test", priority = 15)]
        public static void SelfTest()
        {
            var failures = new System.Collections.Generic.List<string>();
            Debug.Log("[MB-Loop] self-test флага loop (sidecar + AnimationClipSettings)");

            var defaults = JsonUtility.FromJson<MotionBricksBakeConfig>("{}");
            Check("дефолты sidecar", defaults != null && defaults.loop && defaults.legIk && defaults.groundFeet,
                $"loop={defaults?.loop}, legIk={defaults?.legIk}, groundFeet={defaults?.groundFeet}", failures);

            var oneShot = JsonUtility.FromJson<MotionBricksBakeConfig>("{\"loop\":false,\"legIk\":false}");
            Check("sidecar one-shot", oneShot != null && !oneShot.loop && !oneShot.legIk,
                $"loop={oneShot?.loop}, legIk={oneShot?.legIk}", failures);

            var clip = new AnimationClip();
            try
            {
                MotionBricksSpikeBaker.ApplyLoopSettings(clip, false);
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                Check("клип one-shot", !settings.loopTime && clip.wrapMode == WrapMode.Clamp,
                    $"loopTime={settings.loopTime}, wrapMode={clip.wrapMode}", failures);

                MotionBricksSpikeBaker.ApplyLoopSettings(clip, true);
                settings = AnimationUtility.GetAnimationClipSettings(clip);
                Check("клип loop", settings.loopTime && clip.wrapMode == WrapMode.Loop,
                    $"loopTime={settings.loopTime}, wrapMode={clip.wrapMode}", failures);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }

            if (failures.Count == 0) Debug.Log("  ИТОГ: PASS (loop:false не зацикливает one-shot, дефолт остался true)");
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
