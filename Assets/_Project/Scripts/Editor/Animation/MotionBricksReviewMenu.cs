#if UNITY_EDITOR
// T-MB04a/T-MB04b: приёмка MotionBricks без выхода из гейта.
//
// Зачем отдельное меню, а не mode = BakedClips:
//   Шаг 0 требует, чтобы в продакшене MotionBricks НЕ играл (mode = Disabled). Менять
//   ассет настроек ради визуального просмотра — значит каждый раз двигать продовый гейт
//   и надеяться, что его вернут. Здесь наоборот: гейт остаётся Disabled, обход ставится
//   флагом MotionBricksBakedPlayback.ReviewOverride, живёт только в редакторе
//   (#if UNITY_EDITOR, в билд не попадает) и только на текущий сеанс Play Mode.
//
// Два стенда (T-MB04b):
//   1. probe — scene-объект MB_Probe_Player: playback + MotionBricksBakedRootMotion.
//      Здесь проверяется перенос движения: кукла едет, стопы не скользят, Stop → baseline.
//   2. «мой игрок» — компонент playback добавляется в рантайме на живого NetworkPlayer.
//      Префаб не трогается (HARD), а body swap M→F→M проверяется на настоящем персонаже:
//      BodySwapped → playback перепривязывается к новому Animator, экипировка остаётся.
//
// Порядок приёмки: войти в Play Mode → выбрать нужный пункт → смотреть.
// Клип назначается по пути, поэтому ссылка в сцене не нужна (перепёк её и раньше обнулял).

using System.Collections.Generic;
using ProjectC.Animation.MotionBricks;   // MotionBricksBakedPlayback / MotionBricksBakedRootMotion
using ProjectC.Customisation;            // CustomisationClientState, CharacterBodyType
using ProjectC.Player;                   // NetworkPlayer
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    internal static class MotionBricksReviewMenu
    {
        private const string GeneratedRoot = "Assets/_Project/Animations/Generated/MotionBricks";
        private const string SettingsAsset = "Assets/_Project/Settings/MotionBricks/MotionBricksRuntimeSettings.asset";

        private const string WalkForwardClip = GeneratedRoot + "/walk_forward/Male/WalkForward_Male_Humanoid.anim";
        private const string WalkForwardGenericClip = GeneratedRoot + "/walk_forward/Male/WalkForward_Male.anim";
        private const string WalkToJogClip = GeneratedRoot + "/walk_to_jog/Male/WalkToJog_Male_Humanoid.anim";

        // T-MB04a: с 15.11 рабочая forward-walk для приёмки — авторский клип KL, а не синтез из
        // статичной позы комплекта (синтез замерен как подъём обеих стоп и разворот таза).
        private const string AuthoredWalkModel =
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        private const string AuthoredWalkClip = "HumanM@Walk01_Forward";

        // Замеренные скорости опоры (валидатор MotionBricksClipValidator): по ним едет root motion.
        private const float AuthoredWalkSpeed = 2.00f;   // KL Walk01 Forward
        private const float SynthesisedWalkSpeed = 0.54f; // WalkForward_Male
        private const float WalkToJogSpeed = 0.00f;       // демо-разворот на месте: ехать некуда

        private const string ProbeObjectName = "MB_Probe_Player";

        // === Probe: playback + root motion ===

        [MenuItem("ProjectC/MotionBricks/Review/Play KL Walk01 Forward on probe (рабочая ходьба)", priority = 0)]
        public static void PlayAuthoredWalk() =>
            PlayOnProbe(AuthoredWalkModel + "::" + AuthoredWalkClip, "KL Walk01 Forward (авторская, 0.80 s, 2.00 м/с)", AuthoredWalkSpeed);

        [MenuItem("ProjectC/MotionBricks/Review/Play Forward Walk on probe", priority = 1)]
        public static void PlayWalkForward() =>
            PlayOnProbe(WalkForwardClip, "forward walk (humanoid, 0.54 м/с)", SynthesisedWalkSpeed);

        [MenuItem("ProjectC/MotionBricks/Review/Play Forward Walk on probe (generic clip)", priority = 2)]
        public static void PlayWalkForwardGeneric() =>
            PlayOnProbe(WalkForwardGenericClip, "forward walk (generic, 0.54 м/с)", SynthesisedWalkSpeed);

        [MenuItem("ProjectC/MotionBricks/Review/Play WalkToJog on probe", priority = 3)]
        public static void PlayWalkToJog() =>
            PlayOnProbe(WalkToJogClip, "walk_to_jog (humanoid, разворот на месте)", WalkToJogSpeed);

        [MenuItem("ProjectC/MotionBricks/Review/Stop on probe", priority = 20)]
        public static void StopOnProbe()
        {
            int stopped = 0;
            foreach (var playback in FindPlaybacksOnProbe())
            {
                playback.StopPlayback();
                stopped++;
            }

            Debug.Log(stopped > 0
                ? $"[MB-Review] Probe: playback остановлен, baseline Animator восстановлен ({stopped} шт.)."
                : "[MB-Review] Probe: MotionBricksBakedPlayback не найден — останавливать нечего.");
        }

        // === Живой NetworkPlayer (runtime-инъекция, префаб не трогается) ===

        [MenuItem("ProjectC/MotionBricks/Review/Player/Attach + play KL Walk01 on my player", priority = 40)]
        public static void PlayOnMyPlayer() =>
            AttachOnLocalPlayer(AuthoredWalkModel + "::" + AuthoredWalkClip, "KL Walk01 Forward (0.80 s)");

        [MenuItem("ProjectC/MotionBricks/Review/Player/Stop on my player", priority = 41)]
        public static void StopOnMyPlayer()
        {
            var playback = FindLocalPlayerPlayback();
            if (playback == null)
            {
                Debug.LogError("[MB-Review] У локального игрока нет MotionBricksBakedPlayback. Сначала: Player/Attach + play KL Walk01.");
                return;
            }

            playback.StopPlayback();
            Debug.Log("[MB-Review] Игрок: playback остановлен, baseline Animator восстановлен (экипировка не трогалась).");
        }

        [MenuItem("ProjectC/MotionBricks/Review/Player/Detach playback from my player", priority = 42)]
        public static void DetachFromMyPlayer()
        {
            var playback = FindLocalPlayerPlayback();
            if (playback == null)
            {
                Debug.Log("[MB-Review] У локального игрока нет MotionBricksBakedPlayback — отсоединять нечего.");
                return;
            }

            Object.Destroy(playback);
            Debug.Log("[MB-Review] Игрок: playback удалён с объекта (компонент жил только в этой сессии Play Mode).");
        }

        // === Body swap M→F→M через настоящий CharacterCustomisationApplier ===

        [MenuItem("ProjectC/MotionBricks/Review/Body swap/Body swap → Female", priority = 60)]
        public static void SwapToFemale() => SwapBody(CharacterBodyType.Female);

        [MenuItem("ProjectC/MotionBricks/Review/Body swap/Body swap → Male", priority = 61)]
        public static void SwapToMale() => SwapBody(CharacterBodyType.Male);

        // === Диагностика ===

        private static Vector3 _travelOrigin;
        private static float _travelStartTime;
        private static bool _travelActive;

        [MenuItem("ProjectC/MotionBricks/Review/Probe/Start travel measurement", priority = 70)]
        public static void StartTravelMeasurement()
        {
            if (!RequirePlayMode()) return;

            var probe = FindProbeObject();
            if (probe == null)
            {
                Debug.LogError($"[MB-Review] Нет '{ProbeObjectName}' — измерять нечего.");
                return;
            }

            _travelOrigin = probe.transform.position;
            _travelStartTime = Time.time;
            _travelActive = true;
            Debug.Log($"[MB-Review] Замер поездки начат: probe в {_travelOrigin:F2}, t={_travelStartTime:F2} s. " +
                      "Подождите 3–5 с и вызовите 'Log travel measurement'.");
        }

        [MenuItem("ProjectC/MotionBricks/Review/Probe/Log travel measurement", priority = 71)]
        public static void LogTravelMeasurement()
        {
            if (!RequirePlayMode()) return;
            if (!_travelActive)
            {
                Debug.LogError("[MB-Review] Замер не начат: сначала 'Probe/Start travel measurement'.");
                return;
            }

            var probe = FindProbeObject();
            if (probe == null)
            {
                Debug.LogError($"[MB-Review] Нет '{ProbeObjectName}' — измерять нечего.");
                return;
            }

            Vector3 delta = probe.transform.position - _travelOrigin;
            delta.y = 0f;
            float elapsed = Time.time - _travelStartTime;
            float speed = elapsed > 0.01f ? delta.magnitude / elapsed : 0f;

            string clipSpeed = "";
            foreach (var rootMotion in probe.GetComponents<MotionBricksBakedRootMotion>())
                clipSpeed = $" rootMotion.ClipSpeed={rootMotion.ClipSpeed:F2}, current={rootMotion.CurrentSpeed:F2}";

            Debug.Log($"[MB-Review] Замер поездки: {delta.magnitude:F2} м за {elapsed:F2} s → средняя {speed:F2} м/с." +
                      $" Ожидаемо для KL Walk01 — 2.00 м/с (foot slide ≤ 2 см/кадр смотреть глазами).{clipSpeed}");
        }

        [MenuItem("ProjectC/MotionBricks/Review/Probe/Bring probe in front of my player", priority = 72)]
        public static void BringProbeToPlayer()
        {
            if (!RequirePlayMode()) return;

            var probe = FindProbeObject();
            if (probe == null)
            {
                Debug.LogError($"[MB-Review] Нет '{ProbeObjectName}' — переносить нечего.");
                return;
            }

            var player = FindLocalPlayer();
            if (player == null)
            {
                Debug.LogError("[MB-Review] Локальный NetworkPlayer не найден. Войдите в игру и повторите.");
                return;
            }

            // Только на сессию Play Mode: сцена не сохраняется. Ориентация — по лицу игрока,
            // модель пробы смотрит в +Z (проверено замером), поэтому она пойдёт от игрока вперёд.
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();

            probe.transform.SetPositionAndRotation(
                player.transform.position + forward * 5f,
                Quaternion.LookRotation(forward, Vector3.up));

            Debug.Log($"[MB-Review] Probe перенесён перед игроком: {probe.transform.position:F2}, лицо {forward:F2}. " +
                      "Сцена не сохранена; после Stop/Play Mode позиция вернётся.");
        }

        [MenuItem("ProjectC/MotionBricks/Review/Focus probe in Scene view", priority = 80)]
        public static void FocusProbe()
        {
            var probe = FindProbeObject();
            if (probe == null)
            {
                Debug.LogError($"[MB-Review] В загруженных сценах нет '{ProbeObjectName}'. Проверьте WorldScene_0_0.");
                return;
            }

            Selection.activeGameObject = probe;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log($"[MB-Review] Probe найден: '{GetPath(probe.transform)}' в {probe.transform.position}.");
        }

        [MenuItem("ProjectC/MotionBricks/Review/Log harness state", priority = 81)]
        public static void LogHarnessState()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MotionBricksRuntimeSettings>(SettingsAsset);
            Debug.Log($"[MB-Review] gate: settings={(settings != null ? settings.name : "null")} " +
                      $"mode={(settings != null ? settings.mode.ToString() : "-")} reviewOverride={MotionBricksBakedPlayback.ReviewOverride} playing={Application.isPlaying}");

            foreach (var playback in FindPlaybacksOnProbe())
            {
                Debug.Log($"[MB-Review] probe '{playback.name}': clip={(playback.CurrentClip != null ? playback.CurrentClip.name : "null")} " +
                          $"playing={playback.IsPlaying} animator={(playback.TargetAnimator != null ? playback.TargetAnimator.name : "null")} " +
                          $"applyRootMotion={(playback.TargetAnimator != null ? playback.TargetAnimator.applyRootMotion.ToString() : "-")} " +
                          $"guard={playback.RootMotionGuardActive}");

                foreach (var rootMotion in playback.GetComponents<MotionBricksBakedRootMotion>())
                {
                    Debug.Log($"[MB-Review] probe root motion: enabled={rootMotion.enabled} " +
                              $"clipSpeed={rootMotion.ClipSpeed:F2} current={rootMotion.CurrentSpeed:F2} м/с");
                }
            }

            var playerPlayback = FindLocalPlayerPlayback();
            Debug.Log(playerPlayback != null
                ? $"[MB-Review] player playback: clip={(playerPlayback.CurrentClip != null ? playerPlayback.CurrentClip.name : "null")} " +
                  $"playing={playerPlayback.IsPlaying} animator={(playerPlayback.TargetAnimator != null ? playerPlayback.TargetAnimator.name : "null")} " +
                  $"applyRootMotion={(playerPlayback.TargetAnimator != null ? playerPlayback.TargetAnimator.applyRootMotion.ToString() : "-")} " +
                  $"guard={playerPlayback.RootMotionGuardActive}"
                : "[MB-Review] player playback: не подключён");

            var state = CustomisationClientState.Instance;
            Debug.Log(state != null
                ? $"[MB-Review] bodyType={state.CurrentSnapshot.bodyType} (0=Male, 1=Female), h={state.CurrentSnapshot.heightScale:F2}, w={state.CurrentSnapshot.widthScale:F2}"
                : "[MB-Review] CustomisationClientState.Instance == null (BootstrapScene не загружен?)");
        }

        // === Internals ===

        private static void PlayOnProbe(string clipSpec, string label, float clipSpeed)
        {
            if (!RequirePlayMode()) return;

            var clip = ResolveClip(clipSpec);
            if (clip == null)
            {
                Debug.LogError($"[MB-Review] Нет клипа '{clipSpec}'. Сначала перепеките его через ProjectC/MotionBricks/Bake/...");
                return;
            }

            var playbacks = FindPlaybacksOnProbe();
            if (playbacks.Count == 0)
            {
                Debug.LogError($"[MB-Review] В загруженных сценах нет MotionBricksBakedPlayback на '{ProbeObjectName}'. " +
                               "Проверьте, что WorldScene_0_0 загрузился.");
                return;
            }

            // Гейт остаётся Disabled: обход включается только здесь и только в редакторе.
            MotionBricksBakedPlayback.ReviewOverride = true;

            foreach (var playback in playbacks)
            {
                // Клип и скорость задаём ДО включения компонента: иначе OnEnable успеет сыграть
                // клип, который лежал в сцене (WalkToJog), и в логе будет два «Play».
                if (playback.enabled) playback.StopPlayback();

                playback.ConfigureForReview(LoadSettings(), playback.TargetAnimator, clip, 1f, loop: true, logVerbose: true);

                foreach (var rootMotion in playback.GetComponents<MotionBricksBakedRootMotion>())
                {
                    // Скорость задаётся по замеру клипа: с чужой скоростью стопы будут скользить.
                    rootMotion.ClipSpeed = clipSpeed;
                }

                // Компоненты могли сами себя выключить на OnEnable (Mode=Disabled) — включаем,
                // чтобы работали LateUpdate с диагностикой, намотка петли и root motion.
                bool wasEnabled = playback.enabled;
                if (!wasEnabled) playback.enabled = true;
                else playback.TryPlayForReview(clip, 1f);

                foreach (var rootMotion in playback.GetComponents<MotionBricksBakedRootMotion>())
                {
                    if (!rootMotion.enabled) rootMotion.enabled = true;
                }

                bool ok = playback.IsPlaying;
                Debug.Log(ok
                    ? $"[MB-Review] Играю {label}: '{clip.name}' ({clip.length:F2} s, humanMotion={clip.humanMotion}) " +
                      $"на '{playback.name}' → Animator '{playback.TargetAnimator?.name}'. " +
                      $"Root motion: {clipSpeed:F2} м/с. Гейт настроек не менялся."
                    : $"[MB-Review] Не удалось запустить на '{playback.name}' — смотрите предупреждения выше.");
            }
        }

        private static void AttachOnLocalPlayer(string clipSpec, string label)
        {
            if (!RequirePlayMode()) return;

            var clip = ResolveClip(clipSpec);
            if (clip == null)
            {
                Debug.LogError($"[MB-Review] Нет клипа '{clipSpec}'.");
                return;
            }

            var player = FindLocalPlayer();
            if (player == null)
            {
                Debug.LogError("[MB-Review] Локальный NetworkPlayer не найден. Войдите в игру (Host/Client) и повторите.");
                return;
            }

            MotionBricksBakedPlayback.ReviewOverride = true;

            var playback = player.GetComponent<MotionBricksBakedPlayback>();
            if (playback == null)
            {
                // Одно предупреждение «настройки не назначены» на AddComponent — ожидаемо:
                // конфигурация ставится сразу после, в этом же вызове.
                playback = player.gameObject.AddComponent<MotionBricksBakedPlayback>();
                playback.ConfigureForReview(LoadSettings(), FindPlayerAnimator(player), clip, 1f, true, true);
            }
            else
            {
                playback.ConfigureForReview(LoadSettings(), FindPlayerAnimator(player), clip, 1f, true, true);
            }

            if (!playback.enabled) playback.enabled = true;

            // Если OnEnable уже сыграл клип — не перезапускаем; иначе запускаем явно.
            if (!playback.IsPlaying) playback.TryPlayForReview(clip, 1f);

            Debug.Log($"[MB-Review] Игрок '{player.name}': playback {(playback.IsPlaying ? "играет" : "НЕ играет")} " +
                      $"'{clip.name}' на Animator '{playback.TargetAnimator?.name}'. Префаб не менялся, компонент живёт только в сессии.");
        }

        private static void SwapBody(CharacterBodyType bodyType)
        {
            if (!RequirePlayMode()) return;

            var state = CustomisationClientState.Instance;
            if (state == null)
            {
                Debug.LogError("[MB-Review] CustomisationClientState.Instance == null — body swap недоступен (BootstrapScene не загружен?).");
                return;
            }

            var snapshot = state.CurrentSnapshot;
            if (snapshot.heightScale < 0.5f || snapshot.widthScale < 0.5f)
            {
                snapshot.heightScale = 1f;
                snapshot.widthScale = 1f;
            }

            // Цели: applier на probe (чистый визуал KL M/F) и на локальном игроке (экипировка).
            var targets = new List<CharacterCustomisationApplier>();
            var probe = FindProbeObject();
            if (probe != null)
            {
                var probeApplier = probe.GetComponent<CharacterCustomisationApplier>();
                if (probeApplier != null) targets.Add(probeApplier);
            }

            var player = FindLocalPlayer();
            if (player != null)
            {
                var playerApplier = player.GetComponent<CharacterCustomisationApplier>();
                if (playerApplier != null) targets.Add(playerApplier);
            }

            // Отслеживаем, кто реально отреагировал на снапшот: applier подписывается на
            // CustomisationClientState в OnEnable и мог не успеть (Instance == null на старте).
            var reacted = new HashSet<CharacterCustomisationApplier>();
            var handlers = new List<System.Action<Animator>>();
            foreach (var applier in targets)
            {
                var captured = applier;
                System.Action<Animator> handler = _ => reacted.Add(captured);
                applier.BodySwapped += handler;
                handlers.Add(handler);
            }

            snapshot.bodyType = bodyType;
            state.ApplyCustomisationSnapshot(snapshot);

            for (int i = 0; i < targets.Count; i++) targets[i].BodySwapped -= handlers[i];

            int fallback = 0;
            foreach (var applier in targets)
            {
                if (reacted.Contains(applier)) continue;
                if (InvokeApplyBodyType(applier, bodyType)) fallback++;
            }

            Debug.Log($"[MB-Review] Body swap → {bodyType}: снапшот применён, отреагировало applier'ов {reacted.Count}/{targets.Count}" +
                      (fallback > 0 ? $", прямой вызов ApplyBodyType для {fallback} (не были подписаны на state)." : ".") +
                      " Ожидаемо: тело сменилось, анимация продолжается, T-pose нет, экипировка на месте.");
        }

        /// <summary>
        /// Applier, который не подписался на CustomisationClientState (Instance был null в его OnEnable),
        /// не увидит снапшот. Для приёмки вызываем его настоящий ApplyBodyType — тот же код, то же
        /// событие BodySwapped; менять baseline-скрипт ради теста не требуется.
        /// </summary>
        private static bool InvokeApplyBodyType(CharacterCustomisationApplier applier, CharacterBodyType bodyType)
        {
            var method = typeof(CharacterCustomisationApplier).GetMethod(
                "ApplyBodyType",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (method == null)
            {
                Debug.LogWarning("[MB-Review] CharacterCustomisationApplier.ApplyBodyType не найден (переименован?) — swap пропущен.");
                return false;
            }

            method.Invoke(applier, new object[] { bodyType });
            return true;
        }

        private static bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            Debug.LogError("[MB-Review] Это только для Play Mode: войдите в Play Mode и вызовите пункт снова. " +
                           "Вне Play Mode playback не на чем показывать.");
            return false;
        }

        private static MotionBricksRuntimeSettings LoadSettings() =>
            AssetDatabase.LoadAssetAtPath<MotionBricksRuntimeSettings>(SettingsAsset);

        private static NetworkPlayer FindLocalPlayer()
        {
            foreach (var player in Object.FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player != null && player.IsOwner) return player;
            }

            return null;
        }

        private static Animator FindPlayerAnimator(NetworkPlayer player)
        {
            var animator = player.GetComponentInChildren<Animator>(true);
            if (animator == null)
                Debug.LogWarning("[MB-Review] У игрока не найден Animator — playback сам попробует ResolveAnimator().");
            return animator;
        }

        private static MotionBricksBakedPlayback FindLocalPlayerPlayback()
        {
            var player = FindLocalPlayer();
            return player != null ? player.GetComponent<MotionBricksBakedPlayback>() : null;
        }

        private static GameObject FindProbeObject()
        {
            foreach (var found in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found != null && found.name == ProbeObjectName) return found.gameObject;
            }

            return null;
        }

        /// <summary>Playback-компоненты именно на probe-объектах, не на игроке.</summary>
        private static List<MotionBricksBakedPlayback> FindPlaybacksOnProbe()
        {
            var found = new List<MotionBricksBakedPlayback>();
            foreach (var playback in Object.FindObjectsByType<MotionBricksBakedPlayback>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (playback == null) continue;
                if (playback.name != ProbeObjectName && !playback.name.StartsWith("MB_Probe", System.StringComparison.Ordinal)) continue;
                if (!found.Contains(playback)) found.Add(playback);
            }

            return found;
        }

        private static string GetPath(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
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
    }
}
#endif
