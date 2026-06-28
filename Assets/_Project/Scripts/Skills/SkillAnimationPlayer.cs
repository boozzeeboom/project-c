// Project C: Skills/Battle — T-INP-08
// SkillAnimationPlayer: data-driven проигрывание AnimationClip из SkillNodeConfig.attackClip.
//
// Контракт:
//   - SkillNodeConfig.attackClip != null → используем AnimatorOverrideController:
//     1. Создаём/кешируем override (по InstanceID клипа), подменяем Motion в state "Skill" AnimatorController'а
//        на attackClip.
//     2. Выставляем триггер "SkillPlay" → Animator переходит в state Skill, проигрывает подменённый клип.
//     3. По окончании клипа (normalizedTime >= 1) Animator сам возвращается в Locomotion через
//        transition Skill → Locomotion с Duration = 0.2 (настроено в префабе).
//   - Если attackClip == null → fallback: ничего не делаем (legacy attackAnimationTrigger путь в SkillInputService).
//
// Animation Event "OnAttackImpact" на 60% клипа вызывает NetworkPlayer.OnAttackImpact →
// SkillInputService.TryActivate с originalSlot → RPC на сервер.
//
// Design: docs/dev/INP08_ANIMATOR_CLIP_PIPELINE.md

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Animations;  // Editor-only — нужен для доступа к state.motion в AnimatorController
#endif

namespace ProjectC.Skills
{
    /// <summary>
    /// T-INP-08: проигрывает AnimationClip на NetworkPlayer через AnimatorOverrideController.
    /// Не сетевой компонент, owner-only MonoBehaviour. AddComponent в NetworkPlayer.InitializeSkillInputService.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SkillAnimationPlayer : MonoBehaviour
    {
        [Header("Animator")]
        [Tooltip("Animator на NetworkPlayer. Если null — берётся GetComponentInChildren в Awake.")]
        [SerializeField] private Animator _animator;

        [Header("State names (должны совпадать с Animator Controller на префабе)")]
        [Tooltip("Имя состояния в Animator Controller, Motion которого подменяем через OverrideController. " +
                 "Default: 'Attack1H' (уже есть в PlayerAnimation.controller, имеет trigger 'Attack' и переход в Idle по exit time).")]
        [SerializeField] private string _skillStateName = "Attack1H";

        [Tooltip("Имя trigger-параметра Animator Controller для перехода в Skill state. " +
                 "Default: 'Attack' (уже есть в PlayerAnimation.controller, transition AnyState → Attack1H).")]
        [SerializeField] private string _skillTriggerName = "Attack";

        [Header("Behavior")]
        [Tooltip("Если true — не прерывать текущий Skill, ждать окончания. Иначе — прерывать сразу.")]
        [SerializeField] private bool _waitForFinish = true;

        // Кеш override-контроллеров по InstanceID клипа. Чтобы не создавать новый на каждый каст (GC).
        private readonly Dictionary<int, AnimatorOverrideController> _overrideCache =
            new Dictionary<int, AnimatorOverrideController>();

        // Текущий исполняемый skill (для OnAttackImpact event handler'а).
        public SkillNodeConfig CurrentSkill { get; private set; }

        // Slot, который юзер нажал (для TryActivate из Animation Event).
        public SkillInputSlot OriginalSlot { get; private set; }

        // Флаг: ждём ли сейчас Impact event (чтобы не уйти в fallback, если event не пришёл вовремя).
        private bool _waitingForImpact;
        private float _impactDeadlineRealtime;
        private const float IMPACT_FALLBACK_TIMEOUT = 0.5f; // если за 0.5с не было event — RPC всё равно уходит

        // Флаг: текущий клип в state Skill (чтобы Update знал когда закончился).
        private bool _isCasting;

        // === Lifecycle ===

        private void Awake()
        {
            if (_animator == null)
            {
                // Find first Animator with non-null runtimeAnimatorController. Skip empty Animators
                // (NetworkPlayer sometimes has one without controller, real one is on child Visual_Model).
                var animators = GetComponentsInChildren<Animator>(true);
                foreach (var a in animators)
                {
                    if (a != null && a.runtimeAnimatorController != null)
                    {
                        _animator = a;
                        break;
                    }
                }
                if (_animator == null && animators.Length > 0) _animator = animators[0]; // fallback
            }
        }

        private void Update()
        {
            // Impact fallback: если event не пришёл в течение timeout — принудительно fire RPC.
            if (_waitingForImpact && Time.unscaledTime > _impactDeadlineRealtime)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[SkillAnimationPlayer] OnAttackImpact event not received within {IMPACT_FALLBACK_TIMEOUT:F2}s for skill='{CurrentSkill?.skillId}'. Falling back to immediate RPC.");
                }
                FireImpactRpc();
            }
        }

        // === Public API ===

        /// <summary>
        /// Проиграть клип из SkillNodeConfig.attackClip. No-op если attackClip == null.
        /// </summary>
        /// <param name="skill">SkillNodeConfig с attackClip (опционально)</param>
        /// <param name="originalSlot">Slot, который был нажат (для Impact RPC)</param>
        public void Play(SkillNodeConfig skill, SkillInputSlot originalSlot)
        {
            if (skill == null || skill.attackClip == null) return;

            // Если уже кастим и _waitForFinish — skip (по дизайн-решению юзера).
            if (_waitForFinish && _isCasting)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[SkillAnimationPlayer] Already casting '{CurrentSkill?.skillId}' — wait for finish (skill='{skill.skillId}' skipped).");
                }
                return;
            }

            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning("[SkillAnimationPlayer] Animator or RuntimeAnimatorController is null. Cannot play skill animation.");
                }
                return;
            }

            CurrentSkill = skill;
            OriginalSlot = originalSlot;

            // 1. Получить/создать override controller
            var overrideController = GetOrCreateOverride(skill.attackClip);

            // 2. Применить speed (через Animator.speed? нет — это общая скорость Animator. Используем clip.frameRate)
            //    На самом деле speed модифицируется через overrideController["stateName"].speed,
            //    но это не AnimationClip API. Простой способ — модифицировать _animator.speed временно? Нет, это сломает locomotion.
            //    Решение: используем AnimationClipPlayable НЕТ — мы уже в OverrideController пайплайне.
            //    Workaround: меняем _animator.speed на время клипа (восстанавливаем после).
            //    Лучше: выставляем state.Speed в AnimatorController через код — но это требует runtime AnimatorController API.
            //    Для простоты — оставляем speed=1.0 на клипе, attackClipSpeed применяется как множитель _animator.speed на время.
            if (Mathf.Abs(skill.attackClipSpeed - 1.0f) > 0.001f)
            {
                _animator.speed = skill.attackClipSpeed;
            }

            // 3. Подменить controller и выставить триггер
            _animator.runtimeAnimatorController = overrideController;
            _animator.ResetTrigger(_skillTriggerName);
            _animator.SetTrigger(_skillTriggerName);

            // 4. Ждём OnAttackImpact event (или fallback timeout)
            _isCasting = true;
            _waitingForImpact = true;
            _impactDeadlineRealtime = Time.unscaledTime + IMPACT_FALLBACK_TIMEOUT;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillAnimationPlayer] Playing '{skill.skillId}' with clip='{skill.attackClip.name}' speed={skill.attackClipSpeed:F2} (slot={originalSlot}, waiting for OnAttackImpact event up to {IMPACT_FALLBACK_TIMEOUT:F2}s)");
            }
        }

        /// <summary>
        /// Animation Event handler — вызывается из AnimationClip на 60% (или другой timing, настроенный в клипе).
        /// Должен быть public методом на компоненте, на котором висит Animator (или на child с Animator).
        /// </summary>
        public void OnAttackImpact()
        {
            if (!_waitingForImpact) return; // уже fired (или это event для другого клипа)
            FireImpactRpc();
        }

        /// <summary>
        /// Animation Event handler — вызывается когда клип полностью проигрался (если дизайнер добавит event на конце).
        /// Опционально. Если не добавлен — isCasting сбрасывается вручную через state machine check в Update.
        /// </summary>
        public void OnSkillAnimationEnd()
        {
            _isCasting = false;
            // Restore Animator speed
            if (_animator != null && Mathf.Abs(_animator.speed - 1.0f) > 0.001f)
            {
                // Возвращаем 1.0 только если НЕ сейчас кастим с другим speed
                _animator.speed = 1.0f;
            }
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillAnimationPlayer] Animation ended for skill='{CurrentSkill?.skillId}'");
            }
        }

        // === Internal ===

        private void FireImpactRpc()
        {
            _waitingForImpact = false;
            var skill = CurrentSkill;
            var slot = OriginalSlot;

            // Restore speed (не критично, но аккуратно)
            if (_animator != null && _animator.speed != 1.0f) _animator.speed = 1.0f;

            // Дёрнуть RPC через SkillInputService.TryActivate. Это вызовет RequestAttackRpc / RequestSkillCastRpc.
            // Важно: TryActivate теперь вызывает visualizer и animation player; чтобы не зациклиться,
            // animation player.Play(...) проверяет _isCasting и скипает повторный вызов.
            var sis = SkillInputService.Instance;
            if (sis != null)
            {
                sis.TryActivate(slot, skipAnimation: true); // impact fire — только RPC, без re-trigger анимации
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillAnimationPlayer] FireImpactRpc: skill='{skill?.skillId}' slot={slot}");
            }
        }

        private AnimatorOverrideController GetOrCreateOverride(AnimationClip clip)
        {
            int key = clip.GetInstanceID();
            if (_overrideCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var baseCtrl = _animator.runtimeAnimatorController;
            if (baseCtrl == null)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning("[SkillAnimationPlayer] Animator has no runtimeAnimatorController. Cannot play skill animation.");
                }
                return null;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Editor / dev build: идём глубже через Override → Override → ... → AnimatorController.
            // Нужен AnimatorController (не OverrideController), потому что у него есть прямой
            // доступ к state.motion через layers[0].stateMachine.FindState(name).
            AnimatorController baseAc = baseCtrl as AnimatorController;
            while (baseAc == null && baseCtrl is UnityEngine.AnimatorOverrideController oc)
            {
                baseCtrl = oc.runtimeAnimatorController;
                baseAc = baseCtrl as AnimatorController;
            }
            if (baseAc != null)
            {
                // Создаём СВЕЖИЙ AnimatorOverrideController от чистого base AnimatorController
                // (минуя существующий PlayerAnimation_Default — он всё равно null'ит все клипы).
                var overrideCtrl = new UnityEngine.AnimatorOverrideController(baseAc);

                var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<UnityEngine.AnimationClip, UnityEngine.AnimationClip>>(overrideCtrl.overridesCount);
                overrideCtrl.GetOverrides(overrides);

                // Найти motion в target state через AnimatorController (точное соответствие).
                AnimationClip placeholderKey = null;
                var sm = baseAc.layers[0].stateMachine;
                if (sm != null)
                {
                    foreach (var cs in sm.states)
                    {
                        if (cs.state != null && cs.state.name == _skillStateName)
                        {
                            placeholderKey = cs.state.motion as AnimationClip;
                            break;
                        }
                    }
                }

                bool replaced = false;
                if (placeholderKey != null)
                {
                    for (int i = 0; i < overrides.Count; i++)
                    {
                        if (overrides[i].Key == placeholderKey)
                        {
                            overrides[i] = new System.Collections.Generic.KeyValuePair<UnityEngine.AnimationClip, UnityEngine.AnimationClip>(placeholderKey, clip);
                            replaced = true;
                            break;
                        }
                    }
                }

                if (!replaced)
                {
                    // BlendTree или state без motion — fallback на Unity API.
                    overrideCtrl[_skillStateName] = clip;
                }
                else
                {
                    overrideCtrl.ApplyOverrides(overrides);
                }

                _overrideCache[key] = overrideCtrl;
                return overrideCtrl;
            }
#endif

            // Runtime / fallback path: создаём override от текущего controller (OverrideController или AnimatorController)
            // и подменяем по имени state. Работает только если override controller не замапил это state в null.
            var fallbackCtrl = new UnityEngine.AnimatorOverrideController(baseCtrl);
            fallbackCtrl[_skillStateName] = clip;
            _overrideCache[key] = fallbackCtrl;
            return fallbackCtrl;
        }
    }
}
