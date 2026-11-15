#if UNITY_EDITOR
// T-MB04a: приёмка спайка — проиграть запечённый клип на уже стоящем в сцене
// MotionBricksBakedPlayback в обход гейта MotionBricksRuntimeSettings.
//
// Зачем отдельное меню, а не mode = BakedClips:
//   Шаг 0 требует, чтобы в продакшене MotionBricks НЕ играл (mode = Disabled). Менять
//   ассет настроек ради визуального просмотра — значит каждый раз двигать продовый гейт
//   и надеяться, что его вернут. Здесь наоборот: гейт остаётся Disabled, обход ставится
//   флагом MotionBricksBakedPlayback.ReviewOverride, живёт только в редакторе
//   (#if UNITY_EDITOR, в билд не попадает) и только на текущий сеанс Play Mode.
//
// Порядок приёмки: войти в Play Mode → выбрать нужный пункт → смотреть куклу.
// Клип назначается по пути, поэтому ссылка в сцене не нужна (перепёк её и раньше обнулял).

using System.Collections.Generic;
using ProjectC.Animation.MotionBricks;   // MotionBricksBakedPlayback (ReviewOverride/TryPlayForReview)
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    internal static class MotionBricksReviewMenu
    {
        private const string GeneratedRoot = "Assets/_Project/Animations/Generated/MotionBricks";

        private const string WalkForwardClip = GeneratedRoot + "/walk_forward/Male/WalkForward_Male_Humanoid.anim";
        private const string WalkForwardGenericClip = GeneratedRoot + "/walk_forward/Male/WalkForward_Male.anim";
        private const string WalkToJogClip = GeneratedRoot + "/walk_to_jog/Male/WalkToJog_Male_Humanoid.anim";

        // T-MB04a: с 15.11 рабочая forward-walk для приёмки — авторский клип KL, а не синтез из
        // статичной позы комплекта (синтез замерен как подъём обеих стоп и разворот таза).
        private const string AuthoredWalkModel =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string AuthoredWalkClip = "HumanM@Walk01_Forward";

        [MenuItem("ProjectC/MotionBricks/Review/Play KL Walk01 Forward on probe (рабочая ходьба)", priority = 0)]
        public static void PlayAuthoredWalk() => Play(AuthoredWalkModel + "::" + AuthoredWalkClip, "KL Walk01 Forward (авторская, 0.80 s, 2.00 м/с)");

        [MenuItem("ProjectC/MotionBricks/Review/Play Forward Walk on probe")]
        public static void PlayWalkForward() => Play(WalkForwardClip, "forward walk (humanoid)");

        [MenuItem("ProjectC/MotionBricks/Review/Play Forward Walk on probe (generic clip)")]
        public static void PlayWalkForwardGeneric() => Play(WalkForwardGenericClip, "forward walk (generic)");

        [MenuItem("ProjectC/MotionBricks/Review/Play WalkToJog on probe")]
        public static void PlayWalkToJog() => Play(WalkToJogClip, "walk_to_jog (humanoid)");

        [MenuItem("ProjectC/MotionBricks/Review/Stop on probe")]
        public static void Stop()
        {
            foreach (var playback in FindPlaybacks())
            {
                playback.StopPlayback();
                Debug.Log($"[MB-Review] Playback остановлен на '{playback.name}', baseline Animator восстановлен.");
            }
        }

        private static void Play(string clipPath, string label)
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[MB-Review] Это только для Play Mode: войдите в Play Mode и вызовите пункт снова. " +
                               "Вне Play Mode playback не на чем показывать.");
                return;
            }

            var clip = ResolveClip(clipPath);
            if (clip == null)
            {
                Debug.LogError($"[MB-Review] Нет клипа '{clipPath}'. Сначала перепеките его через ProjectC/MotionBricks/Bake/...");
                return;
            }

            var playbacks = FindPlaybacks();
            if (playbacks.Count == 0)
            {
                Debug.LogError("[MB-Review] В загруженных сценах нет MotionBricksBakedPlayback. " +
                               "Проверьте, что WorldScene_0_0 загрузился и MB_Probe_Player на месте (он должен быть " +
                               "дочерним у визуальной модели, иначе Animator не найдётся).");
                return;
            }

            // Гейт остаётся Disabled: обход включается только здесь и только в редакторе.
            MotionBricksBakedPlayback.ReviewOverride = true;

            foreach (var playback in playbacks)
            {
                // Компонент мог сам себя выключить на OnEnable (Mode=Disabled) — включаем,
                // чтобы работал LateUpdate с диагностикой и намотка петли.
                if (!playback.enabled) playback.enabled = true;

                bool ok = playback.TryPlayForReview(clip, 1f);
                Debug.Log(ok
                    ? $"[MB-Review] Играю {label}: '{clip.name}' ({clip.length:F2} s, humanMotion={clip.humanMotion}) " +
                      $"на '{playback.name}' → Animator '{playback.TargetAnimator?.name}'. Гейт настроек не менялся."
                    : $"[MB-Review] Не удалось запустить на '{playback.name}' — смотрите предупреждения выше.");
            }
        }

        /// <summary>
        /// Путь вида "path.anim" — грузим как ассет; "path.fbx::ClipName" — берём клип из модели.
        /// Авторские клипы KL живут внутри FBX, у них нет отдельного .anim.
        /// </summary>
        private static AnimationClip ResolveClip(string spec)
        {
            int split = spec.IndexOf("::", System.StringComparison.Ordinal);
            if (split < 0) return AssetDatabase.LoadAssetAtPath<AnimationClip>(spec);

            string modelPath = spec.Substring(0, split);
            string clipName = spec.Substring(split + 2);

            AnimationClip first = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (!(asset is AnimationClip clip) || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)) continue;
                if (clip.name == clipName) return clip;
                if (first == null) first = clip;
            }

            return first;
        }

        private static List<MotionBricksBakedPlayback> FindPlaybacks()
        {
            var found = new List<MotionBricksBakedPlayback>();
            foreach (var playback in Object.FindObjectsByType<MotionBricksBakedPlayback>(FindObjectsInactive.Include))
            {
                if (playback != null && !found.Contains(playback)) found.Add(playback);
            }

            return found;
        }
    }
}
#endif
