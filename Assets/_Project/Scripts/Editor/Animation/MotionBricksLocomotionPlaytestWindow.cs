// Project C: обратимый Play Mode-стенд для оценки ощущений locomotion transition clips.
// Оригинальный PlayerAnimation.controller и prefabs не меняются: создаётся и назначается
// только runtime-копия controller'а на owner-игрока. Вне Play Mode изменения не сохраняются.

using System.Collections.Generic;
using System.Linq;
using ProjectC.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public sealed class MotionBricksLocomotionPlaytestWindow : EditorWindow
    {
        private const string OriginalControllerPath = "Assets/_Project/Animations/PlayerAnimation.controller";
        private const string TestFolder = "Assets/_Project/Animations/Generated/MotionBricks/playtest";
        private const string TestControllerPath = TestFolder + "/PlayerAnimation_MBTransitionTest.controller";
        private const string ClipFolder = "Assets/_Project/Animations/Generated/MotionBricks/locomotion_transitions_v1/Male/";

        [SerializeField] private bool _applyToPlayer;
        [SerializeField] private string _status = "Готово. Play Mode запускает пользователь.";

        private double _nextScan;

        [MenuItem("ProjectC/MotionBricks/Review/Locomotion Transition Playtest (Play Mode)")]
        private static void Open()
        {
            var window = GetWindow<MotionBricksLocomotionPlaytestWindow>("MB Playtest");
            window.minSize = new Vector2(480f, 300f);
            window.Show();
        }

        [MenuItem("ProjectC/MotionBricks/Review/Rebuild Locomotion Playtest Controller")]
        public static void BuildControllerMenu() => BuildTestController();

        private void OnEnable()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (_applyToPlayer) RestoreAll();
            _applyToPlayer = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MotionBricks locomotion playtest", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Стенд подменяет только runtime Animator Controller у owner-игрока. " +
                "Не меняет PlayerAnimation.controller, prefab, NetworkPlayer и сцену. " +
                "После выхода из Play Mode всё исчезает.",
                MessageType.Info);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "1. Запустите Play Mode и войдите в игру.\n" +
                    "2. Дождитесь спавна игрока.\n" +
                    "3. Нажмите Apply test controller.\n" +
                    "4. Проверьте WASD, Shift и отпускание клавиш.\n" +
                    "5. Restore baseline — вернуть штатный Animator.",
                    MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild test controller", GUILayout.Height(28f)))
                {
                    _status = BuildTestController();
                }
                if (GUILayout.Button("Apply test controller", GUILayout.Height(28f)))
                {
                    _applyToPlayer = true;
                    ApplyAll();
                }
                if (GUILayout.Button("Restore baseline", GUILayout.Height(28f)))
                {
                    _applyToPlayer = false;
                    RestoreAll();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Status", _status, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Controller", TestControllerPath, EditorStyles.miniLabel);

            if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(TestControllerPath))
                EditorGUILayout.HelpBox("Test controller ещё не создан. Нажмите Rebuild test controller.", MessageType.Warning);
        }

        private void Tick()
        {
            if (!_applyToPlayer || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + 0.25;
            ApplyAll();
        }

        private void ApplyAll()
        {
            var test = AssetDatabase.LoadAssetAtPath<AnimatorController>(TestControllerPath);
            if (test == null)
            {
                _status = "Test controller отсутствует. Нажмите Rebuild test controller.";
                return;
            }

            int applied = 0;
            foreach (var player in Object.FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Include))
            {
                if (!player.IsSpawned || !player.IsOwner) continue;
                var animator = player.DiagnosticAnimator;
                if (animator == null) continue;
                if (animator.runtimeAnimatorController != test)
                {
                    animator.runtimeAnimatorController = test;
                    animator.Rebind();
                    animator.Update(0f);
                }
                applied++;
            }
            _status = $"Test controller активен на owner-игроков: {applied}.";
        }

        private void RestoreAll()
        {
            var original = AssetDatabase.LoadAssetAtPath<AnimatorController>(OriginalControllerPath);
            if (original == null)
            {
                _status = "Оригинальный PlayerAnimation.controller не найден.";
                return;
            }

            int restored = 0;
            foreach (var player in Object.FindObjectsByType<NetworkPlayer>(FindObjectsInactive.Include))
            {
                var animator = player.DiagnosticAnimator;
                if (animator == null) continue;
                if (animator.runtimeAnimatorController != original)
                {
                    animator.runtimeAnimatorController = original;
                    animator.Rebind();
                    animator.Update(0f);
                }
                restored++;
            }
            _status = $"Baseline controller восстановлен: {restored}.";
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                _applyToPlayer = false;
                _status = "Play Mode завершён; runtime-подмена снята вместе со сценой.";
            }
        }

        private static string BuildTestController()
        {
            var original = AssetDatabase.LoadAssetAtPath<AnimatorController>(OriginalControllerPath);
            if (original == null) return "Оригинальный controller не найден.";

            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                const string parent = "Assets/_Project/Animations/Generated/MotionBricks";
                if (!AssetDatabase.IsValidFolder(parent)) return "Нет папки Generated/MotionBricks.";
                AssetDatabase.CreateFolder(parent, "playtest");
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(TestControllerPath) != null)
                AssetDatabase.DeleteAsset(TestControllerPath);

            if (!AssetDatabase.CopyAsset(OriginalControllerPath, TestControllerPath))
                return "Не удалось скопировать PlayerAnimation.controller.";

            var test = AssetDatabase.LoadAssetAtPath<AnimatorController>(TestControllerPath);
            var sm = test.layers[0].stateMachine;
            var idle = FindState(sm, "Idle");
            var walk = FindState(sm, "Walk");
            var run = FindState(sm, "Run");
            var sprint = FindState(sm, "Sprint");
            if (idle == null || walk == null || run == null || sprint == null)
                return "В controller нет Idle/Walk/Run/Sprint.";

            RemoveDirect(sm, idle, walk);
            RemoveDirect(sm, walk, idle);
            RemoveDirect(sm, walk, run);
            RemoveDirect(sm, run, walk);
            RemoveDirect(sm, run, sprint);
            RemoveDirect(sm, sprint, run);

            var toWalk = AddTransitionState(sm, "MB_ToWalk", "MB_IdleToWalkForward_Humanoid", new Vector3(850f, 40f));
            var toIdle = AddTransitionState(sm, "MB_ToIdle", "MB_WalkForwardToIdle_Humanoid", new Vector3(850f, 160f));
            var toRun = AddTransitionState(sm, "MB_ToRun", "MB_WalkForwardToRunForward_Humanoid", new Vector3(850f, 280f));
            var toWalkFromRun = AddTransitionState(sm, "MB_RunToWalk", "MB_RunForwardToWalkForward_Humanoid", new Vector3(1050f, 160f));
            var toSprint = AddTransitionState(sm, "MB_ToSprint", "MB_RunForwardToSprintForward_Humanoid", new Vector3(1050f, 280f));
            var toRunFromSprint = AddTransitionState(sm, "MB_SprintToRun", "MB_SprintForwardToRunForward_Humanoid", new Vector3(1250f, 280f));
            if (toWalk == null || toIdle == null || toRun == null || toWalkFromRun == null ||
                toSprint == null || toRunFromSprint == null)
                return "Не все transition clips найдены в locomotion_transitions_v1.";

            AddTrigger(idle, toWalk, "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddTrigger(walk, toIdle, "Speed", AnimatorConditionMode.Less, 0.1f);
            AddTrigger(walk, toRun, "Speed", AnimatorConditionMode.Greater, 5f);
            AddTrigger(run, toWalkFromRun, "Speed", AnimatorConditionMode.Less, 5f);
            AddTrigger(run, toSprint, "Speed", AnimatorConditionMode.Greater, 8f);
            AddTrigger(sprint, toRunFromSprint, "Speed", AnimatorConditionMode.Less, 8f);

            AddExit(toWalk, walk, "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddExit(toWalk, toIdle, null, AnimatorConditionMode.Greater, 0f);
            AddExit(toIdle, idle, null, AnimatorConditionMode.Greater, 0f);
            AddExit(toRun, run, "Speed", AnimatorConditionMode.Greater, 5f);
            AddExit(toRun, walk, "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddExit(toRun, toIdle, null, AnimatorConditionMode.Greater, 0f);
            AddExit(toWalkFromRun, walk, "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddExit(toWalkFromRun, toIdle, null, AnimatorConditionMode.Greater, 0f);
            AddExit(toSprint, sprint, "Speed", AnimatorConditionMode.Greater, 8f);
            AddExit(toSprint, run, "Speed", AnimatorConditionMode.Greater, 5f);
            AddExit(toSprint, walk, "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddExit(toSprint, toIdle, null, AnimatorConditionMode.Greater, 0f);
            AddExit(toRunFromSprint, run, "Speed", AnimatorConditionMode.Greater, 5f);
            AddExit(toRunFromSprint, walk, "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddExit(toRunFromSprint, toIdle, null, AnimatorConditionMode.Greater, 0f);

            EditorUtility.SetDirty(test);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Test controller собран: " + TestControllerPath;
        }

        private static AnimatorState AddTransitionState(AnimatorStateMachine sm, string stateName, string clipName, Vector3 position)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + clipName + ".anim");
            if (clip == null) return null;
            var state = sm.AddState(stateName, position);
            state.motion = clip;
            state.writeDefaultValues = true;
            state.speed = 1f;
            return state;
        }

        private static void AddTrigger(AnimatorState from, AnimatorState to, string parameter,
            AnimatorConditionMode mode, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.hasFixedDuration = true;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void AddExit(AnimatorState from, AnimatorState to, string parameter,
            AnimatorConditionMode mode, float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 0.97f;
            transition.duration = 0.08f;
            transition.hasFixedDuration = true;
            if (!string.IsNullOrEmpty(parameter)) transition.AddCondition(mode, threshold, parameter);
        }

        private static void RemoveDirect(AnimatorStateMachine sm, AnimatorState from, AnimatorState to)
        {
            for (int i = from.transitions.Length - 1; i >= 0; i--)
                if (from.transitions[i].destinationState == to)
                    from.RemoveTransition(from.transitions[i]);
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string stateName) =>
            sm.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
    }
}
