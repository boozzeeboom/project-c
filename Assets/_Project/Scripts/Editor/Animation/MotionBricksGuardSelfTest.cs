#if UNITY_EDITOR
// Project C: T-MB03 — самопроверка applyRootMotion guard'а playback'а.
//
// Зачем: после body swap новое тело приходит из FBX с `applyRootMotion = true`. Для клипа
// с переносом корня это дало бы двойное движение (Animator + компонент). Guard выключает
// флаг на время playback и возвращает прежнее значение после. Ломать `SkillAnimationPlayer`
// нельзя: он сам сохраняет/включает/восстанавливает этот флаг вокруг каста.
//
// Проверка идёт БЕЗ Play Mode: создаётся временный объект (HideAndDontSave), у него Animator
// без контроллера, играется настоящий baked-клип через настоящий публичный путь playback.
// Проверяются: старт, стоп, «флаг уже false», body swap на второй Animator и ветка «идёт каст».
//
// Меню: ProjectC/MotionBricks/Bake/applyRootMotion guard self-test

using System.Reflection;
using ProjectC.Animation.MotionBricks;
using ProjectC.Skills;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public static class MotionBricksGuardSelfTest
    {
        private const string ClipPath =
            "Assets/_Project/Animations/Generated/MotionBricks/walk_forward/Male/WalkForward_Male.anim";

        [MenuItem("ProjectC/MotionBricks/Bake/applyRootMotion guard self-test", priority = 14)]
        public static void Run()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogError($"[MB-Guard] нет клипа '{ClipPath}' — сначала испеките клипы.");
                return;
            }

            var failures = new System.Collections.Generic.List<string>();
            var holder = new GameObject("__MB_GuardTest");
            holder.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                // SkillAnimationPlayer добавляется ДО playback: playback кэширует его в OnEnable.
                var skill = holder.AddComponent<SkillAnimationPlayer>();
                var animatorA = AddAnimator(holder, "GuardAnimatorA");
                var animatorB = AddAnimator(holder, "GuardAnimatorB");
                var playback = holder.AddComponent<MotionBricksBakedPlayback>();

                Debug.Log("[MB-Guard] applyRootMotion guard self-test (без Play Mode)");

                // 1. Старт при applyRootMotion = true → флаг выключается, guard активен.
                animatorA.applyRootMotion = true;
                bool played = Play(playback, animatorA, clip);
                Check("старт (true)", played && !animatorA.applyRootMotion && playback.RootMotionGuardActive, failures);

                // 2. Стоп → прежнее значение вернулось.
                playback.StopPlayback();
                Check("стоп (вернул true)", animatorA.applyRootMotion && !playback.RootMotionGuardActive, failures);

                // 3. Флаг уже false → guard ничего не портит и возвращает false.
                animatorA.applyRootMotion = false;
                Play(playback, animatorA, clip);
                bool startedFalse = !animatorA.applyRootMotion && playback.RootMotionGuardActive;
                playback.StopPlayback();
                Check("старт/стоп (было false)", startedFalse && !animatorA.applyRootMotion, failures);

                // 4. Body swap: guard на A → играем на B; A должен вернуть true, B — выключиться.
                animatorA.applyRootMotion = true;
                animatorB.applyRootMotion = true;
                Play(playback, animatorA, clip);
                Play(playback, animatorB, clip);
                bool swapOk = animatorA.applyRootMotion && !animatorB.applyRootMotion && playback.RootMotionGuardActive;
                Debug.Log($"  diag body swap: A.applyRootMotion={animatorA.applyRootMotion}, " +
                          $"B.applyRootMotion={animatorB.applyRootMotion}, guard={playback.RootMotionGuardActive}");
                playback.StopPlayback();
                Debug.Log($"  diag body swap stop: A={animatorA.applyRootMotion}, B={animatorB.applyRootMotion}, " +
                          $"guard={playback.RootMotionGuardActive}");
                Check("body swap (A→B)", swapOk && animatorB.applyRootMotion, failures);

                // 5. Каст SkillAnimationPlayer.
                // В Edit Mode OnEnable не вызывается, поэтому ссылка на скилл ставится явно (только тест).
                EnsureSkillReference(playback, skill);

                // 5a. Пока идёт каст, playback не должен запускаться и трогать флаг.
                SetCasting(skill, true);
                animatorB.applyRootMotion = true;
                bool refused = !Play(playback, animatorB, clip);
                bool untouched = animatorB.applyRootMotion && !playback.RootMotionGuardActive;
                Debug.Log($"  diag cast gate: refused={refused}, B={animatorB.applyRootMotion}, " +
                          $"guard={playback.RootMotionGuardActive}");
                Check("каст: playback не стартует и флаг не тронут", refused && untouched, failures);

                // 5b. Playback уже играет, скилл включает флаг на каст — Stop не должен его перетереть.
                SetCasting(skill, false);
                animatorB.applyRootMotion = false;
                Play(playback, animatorB, clip);          // guard запомнил false
                SetCasting(skill, true);
                animatorB.applyRootMotion = true;         // так делает SkillAnimationPlayer (ApplyRootMotion = true)
                playback.StopPlayback();
                bool kept = animatorB.applyRootMotion && !playback.RootMotionGuardActive;
                Debug.Log($"  diag cast stop: B={animatorB.applyRootMotion}, guard={playback.RootMotionGuardActive}, kept={kept}");
                SetCasting(skill, false);
                Check("каст: Stop не перетирает флаг скилла", kept, failures);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }

            if (failures.Count == 0) Debug.Log("  ИТОГ: PASS (guard живой, скилл не ломается)");
            else Debug.LogError($"  ИТОГ: FAIL — {string.Join("; ", failures)}");
        }

        private static bool Play(MotionBricksBakedPlayback playback, Animator animator, AnimationClip clip)
        {
            playback.ConfigureForReview(null, animator, clip, 1f, loop: true, logVerbose: false);
            return playback.TryPlayForReview(clip, 1f);
        }

        private static Animator AddAnimator(GameObject holder, string name)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(holder.transform, false);
            return go.AddComponent<Animator>();
        }

        private static void SetCasting(SkillAnimationPlayer skill, bool value)
        {
            var field = typeof(SkillAnimationPlayer).GetField("_isCasting", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning("[MB-Guard] поле SkillAnimationPlayer._isCasting не найдено — ветка каста не проверена.");
                return;
            }

            field.SetValue(skill, value);
        }

        /// <summary>
        /// В Edit Mode OnEnable компонента не вызывается, поэтому кэш `_skillPlayer` пуст.
        /// Для проверки ветки каста подставляем ссылку явно (только редакторный тест).
        /// </summary>
        private static void EnsureSkillReference(MotionBricksBakedPlayback playback, SkillAnimationPlayer skill)
        {
            var field = typeof(MotionBricksBakedPlayback).GetField(
                "_skillPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                Debug.LogWarning("[MB-Guard] поле MotionBricksBakedPlayback._skillPlayer не найдено — ветка каста не проверена.");
                return;
            }

            field.SetValue(playback, skill);
        }

        private static void Check(string label, bool ok, System.Collections.Generic.List<string> failures)
        {
            if (!ok) failures.Add(label);
            Debug.Log($"  {(ok ? "PASS" : "FAIL")}  {label}");
        }
    }
}
#endif
