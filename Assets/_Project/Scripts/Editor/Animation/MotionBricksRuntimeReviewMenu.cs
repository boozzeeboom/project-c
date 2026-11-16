#if UNITY_EDITOR
// Project C: T-MB06 — явный запуск runtime-приёмки без изменений prefab/сцен.

using ProjectC.Customisation;
using ProjectC.Player;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    internal static class MotionBricksRuntimeReviewMenu
    {
        private const string SettingsAsset = "Assets/_Project/Settings/MotionBricks/MotionBricksRuntimeSettings.asset";
        private const string MaleModel = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        private const string FemaleModel = "Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx";
        private const string Python = @"C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe";
        private const string Repository = @"C:\Projects\motion_bricks\motionbricks";
        private const string Script = @"C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\runtime_sidecar.py";
        private static readonly string[] RequiredBones =
        {
            "B-hips", "B-spine", "B-chest", "B-neck", "B-head",
            "B-shoulder.L", "B-upperArm.L", "B-forearm.L", "B-hand.L",
            "B-shoulder.R", "B-upperArm.R", "B-forearm.R", "B-hand.R",
            "B-thigh.L", "B-shin.L", "B-foot.L", "B-toe.L",
            "B-thigh.R", "B-shin.R", "B-foot.R", "B-toe.R",
        };

        [MenuItem("ProjectC/MotionBricks/Runtime/Attach + start on my player", priority = 200)]
        public static void AttachAndStart()
        {
            if (!RequirePlayMode()) return;
            NetworkPlayer player = FindLocalPlayer();
            if (player == null)
            {
                Debug.LogError("[MB-Runtime] Owner NetworkPlayer не найден. Войдите в Host/Client и повторите.");
                return;
            }
            Animator animator = player.GetComponentInChildren<Animator>(true);
            GameObject restTemplate = SelectRestTemplate(player, animator);
            if (restTemplate == null)
            {
                Debug.LogError($"[MB-Runtime] Не удалось выбрать rest template для Avatar '{animator?.avatar?.name}'.");
                return;
            }

            var runtime = player.GetComponent<MotionBricksRuntimePlayback>();
            if (runtime == null) runtime = player.gameObject.AddComponent<MotionBricksRuntimePlayback>();
            runtime.StopRuntime("reconfigure");
            runtime.ConfigureForReview(
                AssetDatabase.LoadAssetAtPath<MotionBricksRuntimeSettings>(SettingsAsset),
                animator,
                restTemplate,
                Python,
                Repository,
                Script);
            if (runtime.StartRuntime())
                Debug.Log($"[MB-Runtime] Attach готов: '{player.name}', Animator='{animator.name}', rest='{restTemplate.name}'. " +
                          "WASD/Shift — движение; Ctrl+F10 — A/B с обычным Animator; префаб не изменялся.");
        }

        [MenuItem("ProjectC/MotionBricks/Runtime/Stop / restore Animator", priority = 201)]
        public static void Stop()
        {
            MotionBricksRuntimePlayback runtime = FindRuntime();
            if (runtime == null)
            {
                Debug.Log("[MB-Runtime] Runtime playback не найден.");
                return;
            }
            runtime.StopRuntime("Review menu");
        }

        [MenuItem("ProjectC/MotionBricks/Runtime/Detach", priority = 202)]
        public static void Detach()
        {
            MotionBricksRuntimePlayback runtime = FindRuntime();
            if (runtime == null)
            {
                Debug.Log("[MB-Runtime] Runtime playback не найден.");
                return;
            }
            runtime.StopRuntime("detach");
            Object.Destroy(runtime);
            Debug.Log("[MB-Runtime] Компонент и его sidecar удалены; prefab/controller не изменялись.");
        }

        [MenuItem("ProjectC/MotionBricks/Runtime/Log diagnostics", priority = 203)]
        public static void LogDiagnostics()
        {
            MotionBricksRuntimePlayback runtime = FindRuntime();
            Debug.Log(runtime != null ? runtime.Diagnostics() : "[MB-Runtime] sidecar=detached", runtime);
        }

        private static GameObject SelectRestTemplate(NetworkPlayer player, Animator animator)
        {
            if (animator == null) return null;
            var applier = player != null ? player.GetComponent<CharacterCustomisationApplier>() : null;
            GameObject configured = GetConfiguredBodyModel(applier);
            if (HasRequiredBones(configured)) return configured;

            var male = AssetDatabase.LoadAssetAtPath<GameObject>(MaleModel);
            var female = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleModel);
            Avatar avatar = animator.avatar;
            foreach (GameObject candidate in new[] { male, female })
            {
                if (candidate == null) continue;
                Animator candidateAnimator = candidate.GetComponentInChildren<Animator>(true);
                if (candidateAnimator == null || candidateAnimator.avatar == null) continue;
                if (avatar == candidateAnimator.avatar ||
                    avatar != null && avatar.name == candidateAnimator.avatar.name)
                    return candidate;
            }
            return null;
        }

        private static GameObject GetConfiguredBodyModel(CharacterCustomisationApplier applier)
        {
            if (applier == null) return null;
            string fieldName = applier.CurrentBodyType == CharacterBodyType.Female ? "_femaleModel" : "_maleModel";
            var field = typeof(CharacterCustomisationApplier).GetField(
                fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(applier) as GameObject;
        }

        private static bool HasRequiredBones(GameObject candidate)
        {
            if (candidate == null) return false;
            Transform[] transforms = candidate.GetComponentsInChildren<Transform>(true);
            foreach (string bone in RequiredBones)
            {
                bool found = false;
                foreach (Transform child in transforms)
                {
                    if (child.name != bone) continue;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        private static NetworkPlayer FindLocalPlayer()
        {
            foreach (NetworkPlayer player in Object.FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (player != null && player.IsOwner) return player;
            return null;
        }

        private static MotionBricksRuntimePlayback FindRuntime()
        {
            NetworkPlayer player = FindLocalPlayer();
            return player != null ? player.GetComponent<MotionBricksRuntimePlayback>() : null;
        }

        private static bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogError("[MB-Runtime] Только для Play Mode: сначала войдите в Host/Client, затем вызовите Attach.");
            return false;
        }
    }
}
#endif
