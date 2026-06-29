// Project C: Skills/Battle — T-INP-08 (v2, self-sufficient)
// SkillAnimationPlayer: проигрывает AnimationClip из SkillNodeConfig.attackClip через Animator state machine.
//
// v2 changes (2026-06-29):
//   - Time-based watchdog вместо зависимости от Animation Events (clip может не иметь событий).
//   - Auto-restore фикс: exit transition (0.95) больше не блокирует восстановление.
//   - Перехват `_isCasting` навсегда решён: watchdog + принудительное восстановление.
//   - Animation Events (OnSkillAnimationEnd / OnAttackImpact) = опция (более точный тайминг).
//
// Контракт:
//   - SkillNodeConfig.attackClip != null →
//     1. Создаём AnimatorOverrideController (кешируем по InstanceID клипа).
//     2. Подменяем Motion в state "Skill" на attackClip.
//     3. SetTrigger("SkillPlay") → Animator переходит в state Skill, проигрывает клип.
//   - attackClip == null → no-op (legacy fallback SetTrigger("Attack") через SkillInputService).
//
// Завершение (без событий):
//   a) Watchdog: Time.unscaledTime - _castingStartTime >= _castMaxDuration → Restore()
//   b) Transition: IsInTransition && nextState != Skill → Restore()
//   c) Post-state: currentState != Skill && !IsInTransition → Restore()
//
// С событиями:
//   - OnSkillAnimationEnd() → Restore() (более точное, но не обязательное).
//   - OnAttackImpact() → FireImpactRpc() (более точный impact, fallback по normalizedTime).
//
// Требования к Animator Controller (уже настроены):
//   - state "Skill" с motion placeholder
//   - AnyState → Skill по trigger "SkillPlay"
//   - Skill → Idle по exit (0.95, 0.2s duration)

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Skills
{
    public class SkillAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _skillStateName = "Skill";
        [SerializeField] private string _skillTriggerName = "SkillPlay";
        [SerializeField] private float _impactNormalizedTime = 0.6f;
        [SerializeField] private bool _waitForFinish = true;

        [Header("Safety (v2)")]
        [SerializeField, Tooltip("Буфер времени после окончания клипа до принудительного Restore(). Секунды.")]
        private float _restoreTimeBuffer = 0.5f;

        [Header("Override")]
        [SerializeField, Tooltip("Имя оригинального клипа для state Skill в корневом AnimatorController. В Unity 6 AnimatorOverrideController.this[] ищет ТОЛЬКО по имени клипа, не по имени состояния. Заполняется автоматически в Editor (Awake).")]
        private string _defaultSkillClipName = "HumanM@Attack1H01_L";

        // Cache AnimatorOverrideController'ов по InstanceID клипа (избегаем GC).
        private readonly Dictionary<int, AnimatorOverrideController> _overrideCache = new Dictionary<int, AnimatorOverrideController>();

        // Текущее состояние.
        public SkillNodeConfig CurrentSkill { get; private set; }
        public SkillInputSlot OriginalSlot { get; private set; }
        public bool IsCasting => _isCasting;
        private bool _isCasting;
        private float _castingStartTime;
        private float _castMaxDuration; // clip.length / speed + buffer
        private int _skillStateHash;

        // Оригинальный controller для восстановления.
        private RuntimeAnimatorController _originalController;

        // Состояние applyRootMotion до подмены.
        private bool _savedApplyRootMotion;

        // Impact timing.
        private bool _impactFired;

        // Trigger schedule: после подмены контроллера ждём LateUpdate для SetTrigger.
        private bool _triggerScheduled;

        // Позиция Y персонажа перед началом каста (для предотвращения ухода под пол).
        private float _castStartY;

        private void Awake()
        {
            _skillStateHash = Animator.StringToHash(_skillStateName);
            if (_animator == null)
            {
                foreach (var a in GetComponentsInChildren<Animator>(true))
                {
                    if (a != null && a.runtimeAnimatorController != null) { _animator = a; break; }
                }
            }

#if UNITY_EDITOR
            AutoDetectDefaultSkillClip();
#endif
        }

#if UNITY_EDITOR
        private void AutoDetectDefaultSkillClip()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            // Поднимаемся по цепочке AnimatorOverrideController → ... → AnimatorController
            RuntimeAnimatorController rootCtrl = _animator.runtimeAnimatorController;
            while (rootCtrl is AnimatorOverrideController aoc)
                rootCtrl = aoc.runtimeAnimatorController;

            if (rootCtrl is UnityEditor.Animations.AnimatorController editorCtrl)
            {
                foreach (var layer in editorCtrl.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        if (state.state.name == _skillStateName && state.state.motion != null)
                        {
                            _defaultSkillClipName = state.state.motion.name;
                            if (Debug.isDebugBuild)
                                Debug.Log($"[SkillAnimationPlayer] Auto-detected defaultSkillClip='{_defaultSkillClipName}' for state '{_skillStateName}'");
                            return;
                        }
                    }
                }
                Debug.LogWarning($"[SkillAnimationPlayer] State '{_skillStateName}' not found in root controller '{rootCtrl.name}'");
            }
        }
#endif

        private void LateUpdate()
        {
            if (_triggerScheduled)
            {
                _triggerScheduled = false;
                _animator.ResetTrigger(_skillTriggerName);
                _animator.SetTrigger(_skillTriggerName);
                if (Debug.isDebugBuild)
                    Debug.Log($"[SkillAnimationPlayer] Delayed SkillPlay trigger fired");
            }

            // Position guard: предотвращаем уход персонажа под пол во время каста.
            // Анимация может сместить корневую кость (hips) по Y, CharacterController
            // продолжает Apply gravity, и персонаж "проваливается".
            if (_isCasting)
            {
                Vector3 pos = transform.position;
                if (pos.y < _castStartY - 0.01f)
                {
                    pos.y = _castStartY;
                    transform.position = pos;
                    if (Debug.isDebugBuild)
                        Debug.Log($"[SkillAnimationPlayer] Position guard: snapped Y back to {_castStartY:F2} (was {pos.y:F2})");
                }
            }
        }

        private void Update()
        {
            if (!_isCasting || _animator == null || CurrentSkill == null || CurrentSkill.attackClip == null) return;

            float now = Time.unscaledTime;
            float elapsed = now - _castingStartTime;

            // === Watchdog (v2): принудительное восстановление по таймеру ===
            if (elapsed >= _castMaxDuration)
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[SkillAnimationPlayer] Watchdog: elapsed={elapsed:F2} >= max={_castMaxDuration:F2} — restoring");
                Restore();
                return;
            }

            var si = _animator.GetCurrentAnimatorStateInfo(0);
            bool inSkillState = si.shortNameHash == _skillStateHash || si.IsName(_skillStateName);

            // === Ждём, пока аниматор войдёт в Skill state ===
            // После подмены контроллера нужно время, чтобы trigger сработал.
            // Вместо мгновенного Restore() — ждём появления в Skill.
            if (!inSkillState && !_animator.IsInTransition(0))
            {
                return; // Ещё не началось — ждём
            }

            // === Transition ===
            if (_animator.IsInTransition(0))
            {
                var nextState = _animator.GetNextAnimatorStateInfo(0);
                bool nextIsSkill = nextState.shortNameHash == _skillStateHash || nextState.IsName(_skillStateName);
                if (nextIsSkill) return; // Переход В Skill — ждём завершения
                // Переход ИЗ Skill (exit transition) — анимация закончена
                if (Debug.isDebugBuild)
                    Debug.Log($"[SkillAnimationPlayer] Transition away from Skill — restoring");
                Restore();
                return;
            }

            // === В Skill state — активная фаза ===

            // Impact timing: если AnimationEvent не сработал — fire RPC по normalizedTime
            if (!_impactFired && si.normalizedTime >= _impactNormalizedTime)
            {
                _impactFired = true;
                FireImpactRpc();
            }

            // NormalizedTime >= 1 — анимация завершена (без transition — маловероятно, но safety)
            if (si.normalizedTime >= 1.0f)
            {
                Restore();
            }
        }

        public void Play(SkillNodeConfig skill, SkillInputSlot originalSlot)
        {
            if (skill == null || skill.attackClip == null) return;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            if (_waitForFinish && _isCasting)
            {
                if (Debug.isDebugBuild)
                    Debug.Log($"[SkillAnimationPlayer] Busy, ignoring Play('{skill.skillId}')");
                return;
            }

            CurrentSkill = skill;
            OriginalSlot = originalSlot;
            _impactFired = false;
            _isCasting = true;
            _castingStartTime = Time.unscaledTime;

            // Вычисляем максимальную длительность: clip.length / speed + буфер
            float clipLength = skill.attackClip.length;
            float speed = Mathf.Max(0.01f, skill.attackClipSpeed);
            _castMaxDuration = (clipLength / speed) + _restoreTimeBuffer;

            // Создаём override-controller для этого клипа (кешируем).
            var overrideController = GetOrCreateOverride(skill.attackClip);

            // Сохраняем оригинал (один раз).
            if (_originalController == null) _originalController = _animator.runtimeAnimatorController;

            // Включаем applyRootMotion — кастомные клипы могут содержать вращение
            // (Standing Melee Attack 360 Low) и горизонтальное движение.
            _savedApplyRootMotion = _animator.applyRootMotion;
            _animator.applyRootMotion = true;

            // Сохраняем Y позиции для защиты от ухода под пол (Root Transform Position Y в клипе).
            _castStartY = transform.position.y;

            // Подменяем контроллер.
            _animator.runtimeAnimatorController = overrideController;

            // Триггер НЕ ставим сразу — откладываем на LateUpdate,
            // чтобы контроллер успел инициализироваться после подмены.
            _triggerScheduled = true;

            if (Debug.isDebugBuild)
                Debug.Log($"[SkillAnimationPlayer] Playing '{skill.skillId}' clip='{skill.attackClip.name}' len={clipLength:F2}s speed={speed:F2} maxDuration={_castMaxDuration:F2} — trigger scheduled for LateUpdate");
        }

        /// <summary>Вызывается из Animation Event в клипе (если есть).</summary>
        public void OnSkillAnimationEnd() => Restore();

        /// <summary>Вызывается из Animation Event в клипе (если есть).</summary>
        public void OnAttackImpact()
        {
            if (!_isCasting || _impactFired) return;
            _impactFired = true;
            FireImpactRpc();
        }

        private void Restore()
        {
            if (!_isCasting) return;
            _isCasting = false;
            if (_animator != null && _originalController != null && _animator.runtimeAnimatorController != _originalController)
            {
                _animator.runtimeAnimatorController = _originalController;
                _animator.applyRootMotion = _savedApplyRootMotion;
                if (Debug.isDebugBuild)
                    Debug.Log($"[SkillAnimationPlayer] Restored original controller");
            }
            _originalController = null;
            CurrentSkill = null;
            OriginalSlot = SkillInputSlot.None;
        }

        private void FireImpactRpc()
        {
            var sis = SkillInputService.Instance;
            if (sis != null) sis.TryActivate(OriginalSlot, skipAnimation: true);
        }

        private AnimatorOverrideController GetOrCreateOverride(AnimationClip clip)
        {
            int key = clip.GetInstanceID();
            if (_overrideCache.TryGetValue(key, out var cached)) return cached;

            var baseCtrl = _animator.runtimeAnimatorController;
            var overrideCtrl = new AnimatorOverrideController(baseCtrl);

            // Unity 6: AnimatorOverrideController.this[string] работает ТОЛЬКО по имени клипа,
            // не по имени состояния (T-INP-08). Используем имя оригинального клипа для Skill state.
            if (!string.IsNullOrEmpty(_defaultSkillClipName))
                overrideCtrl[_defaultSkillClipName] = clip;
            else
                overrideCtrl[_skillStateName] = clip; // fallback (старые версии Unity)

            _overrideCache[key] = overrideCtrl;
            return overrideCtrl;
        }
    }
}