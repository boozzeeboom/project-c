// Project C: T-MB02 — MotionBricks baked playback (MVP-путь).
//
// Играет ОДИН запечённый AnimationClip через отдельный PlayableGraph поверх Animator.
// Контракт (docs/world/MOTIONBRICK_animations/06_SAFE_ROLLOUT_AND_ROLLBACK.md §4.2, §5, §6):
//   - НЕ трогает Animator.runtimeAnimatorController, НЕ правит prefab/controller.
//   - Animator.CrossFade(clipName) здесь не используется: это состояние контроллера, не клип.
//   - После Stop/OnDisable — graph destroy + Animator.Rebind() + Update(0), т.е. baseline Animator.
//   - Нет ассета MotionBricksRuntimeSettings или mode = Disabled → компонент гасит себя (fail-closed).
//   - Body swap M<->F: подписка на CharacterCustomisationApplier.BodySwapped, пересборка ссылки на Animator.
//   - Если SkillAnimationPlayer кастует — playback не запускается (combat остаётся authored).
//
// Откат (уровни 0-1): settings.mode = Disabled ЛИБО снять этот компонент со scene-объекта.

using ProjectC.Player;   // CharacterCustomisationApplier (BodySwapped)
using ProjectC.Skills;   // SkillAnimationPlayer (IsCasting)
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectC.Animation.MotionBricks
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ProjectC/Animation/MotionBricks Baked Playback")]
    public sealed class MotionBricksBakedPlayback : MonoBehaviour
    {
        [Header("Gate")]
        [SerializeField, Tooltip("Отсутствие ассета = Disabled (fail-closed). Создать: Create > ProjectC/Animation/MotionBricks Runtime Settings.")]
        private MotionBricksRuntimeSettings _settings;

        [Header("Target")]
        [SerializeField, Tooltip("Animator персонажа. Пусто — ищем в детях (обычно Visual_Model/*).")]
        private Animator _animator;

        [SerializeField, Tooltip("Запечённый клип (Assets/_Project/Animations/Generated/MotionBricks/...).")]
        private AnimationClip _clip;

        [Header("Playback")]
        [SerializeField] private bool _playOnEnable = true;
        [SerializeField, Range(0f, 4f)] private float _speed = 1f;
        [SerializeField, Tooltip("Зациклить клип. Клипы спайка loop не имеют, без этого скелет замирает на последнем кадре.")]
        private bool _loop = true;
        [SerializeField, Tooltip("Логировать старт/стоп. Оставить включённым для спайка.")]
        private bool _logVerbose = true;

        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private bool _hasGraph;

        private float _diagAt = -1f;
        private Quaternion _diagStart;

        private CharacterCustomisationApplier _applier;
        private SkillAnimationPlayer _skillPlayer;

        public bool IsPlaying => _hasGraph && _graph.IsValid() && _graph.IsPlaying();
        public Animator TargetAnimator => _animator;
        public AnimationClip CurrentClip => _clip;

#if UNITY_EDITOR
        /// <summary>
        /// Спайк-приёмка: играть клип даже при Mode=Disabled. Ставится ТОЛЬКО из меню
        /// ProjectC/MotionBricks/Review/... и только на время сеанса Play Mode; в билд код не попадает.
        /// Продовые объекты остаются под гейтом — гейт не ослаблен, обход явный и редакторный.
        /// </summary>
        public static bool ReviewOverride;
#endif

        // === Lifecycle ===

        private void OnEnable()
        {
            _applier = GetComponent<CharacterCustomisationApplier>();
            if (_applier != null) _applier.BodySwapped += OnBodySwapped;

            _skillPlayer = GetComponent<SkillAnimationPlayer>();

            if (_settings == null)
            {
                Debug.LogWarning("[MotionBricksBakedPlayback] MotionBricksRuntimeSettings не назначен → Mode=Disabled (baseline Animator). Компонент отключён.", this);
                enabled = false;
                return;
            }

            bool allowed = _settings.AllowsPlayback;
#if UNITY_EDITOR
            allowed |= ReviewOverride;
#endif
            if (!allowed)
            {
                if (_logVerbose) Debug.Log("[MotionBricksBakedPlayback] Mode=Disabled → baseline Animator, playback не запускается.", this);
                enabled = false;
                return;
            }

            ResolveAnimator();
            if (_playOnEnable && _clip != null) TryPlay(_clip);
        }

        private void OnDisable()
        {
            if (_applier != null)
            {
                _applier.BodySwapped -= OnBodySwapped;
                _applier = null;
            }

            bool wasPlaying = _hasGraph;
            StopPlayback();
            if (wasPlaying && _logVerbose) Debug.Log("[MotionBricksBakedPlayback] Playback остановлен, baseline Animator восстановлен.", this);
        }

        private void OnDestroy() => StopPlayback();

        private void Update()
        {
            if (!_hasGraph) return;
            KeepLooping();
        }

        /// <summary>
        /// Диагностика снимается в LateUpdate: к этому моменту Animator уже применил свою позу,
        /// т.е. видно то, что видит игрок. Чтение в Update показывает сырой выход графа
        /// ДО humanoid-пасса и врёт: на Humanoid-аватаре не-humanoid клип там «двигается»,
        /// а в кадре стоит bind-поза, потому что humanoid-пасс перезаписывает кости.
        /// </summary>
        private void LateUpdate()
        {
            if (!_hasGraph) return;

            // Одноразовая диагностика через 0.5 с: идёт ли время графа и меняются ли кости.
            if (!_logVerbose) return;

            if (_diagAt < 0f)
            {
                _diagAt = Time.time + 0.5f;
                var b0 = DiagBone();
                _diagStart = b0 != null ? b0.localRotation : Quaternion.identity;
                return;
            }

            if (Time.time < _diagAt) return;
            _diagAt = float.MaxValue;

            var bone = DiagBone();
            float graphTime = (float)PlayableExtensions.GetTime(_clipPlayable);
            string ctrl = _animator == null || _animator.runtimeAnimatorController == null
                ? "null"
                : _animator.runtimeAnimatorController.name;

            Debug.Log("[MotionBricksBakedPlayback] diag: " +
                      $"t={graphTime:F2}/{_clip.length:F2} s, " +
                      $"кость {(bone != null ? bone.name : "НЕ НАЙДЕНА")}: " +
                      $"было {_diagStart.eulerAngles:F1}, стало {(bone != null ? bone.localRotation.eulerAngles.ToString("F1") : "-")}, " +
                      $"controller={ctrl}, humanMotion={_clip.humanMotion}", this);
        }

        /// <summary>Ручная намотка времени клипа: AnimationClipPlayable сам не зацикливается.</summary>
        private void KeepLooping()
        {
            if (!_loop || _clip == null) return;

            double length = _clip.length;
            if (length <= 0.0 || !_clipPlayable.IsValid()) return;

            double time = _clipPlayable.GetTime();
            if (time >= length) _clipPlayable.SetTime(time % length);
        }

        private Transform DiagBone()
        {
            if (_animator == null) return null;
            return _animator.transform.Find("Rig/B-root/B-hips/B-spine/B-chest/B-shoulder.L/B-upperArm.L");
        }

        // === Public API ===

        /// <summary>Запустить запечённый клип поверх Animator. Возвращает false, если играть нельзя.</summary>
        public bool TryPlay(AnimationClip clip, float speed = 1f) => TryPlayInternal(clip, speed, false);

        private bool TryPlayInternal(AnimationClip clip, float speed, bool ignoreGate)
        {
            bool allowed = _settings != null && _settings.AllowsPlayback;
#if UNITY_EDITOR
            allowed |= ignoreGate || ReviewOverride;
#endif
            if (!allowed)
            {
                Debug.LogWarning("[MotionBricksBakedPlayback] Playback запрещён настройками (Mode=Disabled).", this);
                return false;
            }

            if (clip == null)
            {
                Debug.LogWarning("[MotionBricksBakedPlayback] Клип не назначен.", this);
                return false;
            }

            ResolveAnimator();
            if (_animator == null)
            {
                Debug.LogWarning("[MotionBricksBakedPlayback] Animator не найден — играть не на чем.", this);
                return false;
            }

            if (!_animator.isActiveAndEnabled || !_animator.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[MotionBricksBakedPlayback] Animator неактивен — playback пропущен.", this);
                return false;
            }

            if (_skillPlayer != null && _skillPlayer.IsCasting)
            {
                Debug.LogWarning("[MotionBricksBakedPlayback] SkillAnimationPlayer кастует — baked playback не запускаем (combat authored).", this);
                return false;
            }

            StopPlayback();

            _clip = clip;
            _speed = Mathf.Max(0f, speed);

            _graph = PlayableGraph.Create("MotionBricksBaked");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetApplyPlayableIK(false);
            _clipPlayable.SetSpeed(_speed);

            var output = AnimationPlayableOutput.Create(_graph, "MotionBricks", _animator);
            output.SetSourcePlayable(_clipPlayable);

            _graph.Play();
            _hasGraph = true;
            _diagAt = -1f;

            if (_logVerbose)
            {
                Debug.Log($"[MotionBricksBakedPlayback] Play '{clip.name}' ({clip.length:F2} s, speed {_speed:F2}) на '{_animator.name}'. Controller не менялся.", this);
            }

            return true;
        }

        /// <summary>Остановить playback и вернуть baseline Animator (Rebind + Update(0)).</summary>
        public void StopPlayback()
        {
            if (_hasGraph && _graph.IsValid())
            {
                _graph.Stop();
                _graph.Destroy();
            }

            _hasGraph = false;

            // Animator на выгружаемом/выключенном объекте отказывается обновляться —
            // Rebind/Update(0) на нём только сыпет ошибкой в консоль.
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }
        }

        // === Internals ===

        private void ResolveAnimator()
        {
            if (_animator != null) return;
            _animator = GetComponentInChildren<Animator>(true);
        }

        private void OnBodySwapped(Animator newAnimator)
        {
            bool wasPlaying = _hasGraph;

            // Старый Animator уничтожается вместе с телом — Rebind на нём может быть невалиден.
            StopPlayback();

            _animator = newAnimator;
            ResolveAnimator();

            if (wasPlaying) TryPlay(_clip, _speed);
        }

        // === Ручные проверки в Editor (Play Mode — за пользователем) ===

        [ContextMenu("MotionBricks/Play Assigned Clip")]
        private void ContextPlay() => TryPlay(_clip, _speed);

#if UNITY_EDITOR
        /// <summary>Приёмка спайка: играть клип в обход гейта (только из меню Review, только редактор).</summary>
        public bool TryPlayForReview(AnimationClip clip, float speed = 1f) => TryPlayInternal(clip, speed, true);
#endif

        [ContextMenu("MotionBricks/Stop")]
        private void ContextStop() => StopPlayback();
    }
}
