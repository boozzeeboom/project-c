// Project C: T-MB04a — перенос движения baked-клипа на персонажа (root motion).
//
// Зачем отдельный компонент:
//   Клипы MotionBricks пекутся in-place (BakeInPlace = true в MotionBricksSpikeBaker), т.е. горизонталь
//   корня выброшена, а вертикаль и поза остаются. Значит персонажа должен везти код — и везти с ТОЙ
//   скоростью, которую подразумевает клип, иначе стопы «скользят» по земле (skating).
//
// Каноническая схема — docs/world/MOTIONBRICK_animations/06_SAFE_ROLLOUT_AND_ROLLBACK.md §5
//   («MotionBricksBakedRootMotion.cs — Hips delta → CharacterController.Move»).
//
// Два источника движения (переключатель _source):
//   ClipSpeed — для in-place клипов: постоянная скорость, подобранная под клип.
//               Для рабочей ходьбы KL HumanM@Walk01_Forward замерено 2.00 м/с (валидатор, stride 0.33 м
//               за 5 кадров контакта при 30 fps). Синтезированный WalkForward_Male требует 0.54 м/с.
//   HipsDelta — для клипов С переносом корня (будущие BakeInPlace = false): берём мировую дельту
//               кости таза между кадрами, убираем вертикаль, отдаём в CharacterController.Move.
//
// Безопасность (правила брифа):
//   - ничего не меняет в NetworkPlayer.prefab / PlayerAnimation.controller / CharacterController;
//   - без ассета настроек или при Mode=Disabled компонент гасит себя (fail-closed), как playback;
//   - по умолчанию _speed = 0 → компонент физически ничего не двигает, пока скорость не задана явно;
//   - движет только при работающем playback (нет клипа — нет движения);
//   - не кэширует мировые позиции между кадрами дольше одного кадра, поэтому Floating Origin
//     (перебазирование мира) не ломает дельту: старая позиция обнуляется в том же кадре.

using UnityEngine;

namespace ProjectC.Animation.MotionBricks
{
    public enum MotionBricksRootMotionSource
    {
        /// <summary>Постоянная скорость под клип. Для in-place клипов (BakeInPlace = true).</summary>
        ClipSpeed = 0,

        /// <summary>Из дельты кости таза. Для клипов с переносом корня.</summary>
        HipsDelta = 1,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("ProjectC/Animation/MotionBricks Baked Root Motion")]
    public sealed class MotionBricksBakedRootMotion : MonoBehaviour
    {
        [Header("Gate")]
        [SerializeField, Tooltip("Тот же ассет, что у MotionBricksBakedPlayback. Пусто или Mode=Disabled — компонент выключен.")]
        private MotionBricksRuntimeSettings _settings;

        [Header("Источник")]
        [SerializeField] private MotionBricksRootMotionSource _source = MotionBricksRootMotionSource.ClipSpeed;

        [SerializeField, Tooltip("Скорость под клип, м/с. 0 = не двигать (безопасный дефолт).\n" +
                                 "KL HumanM@Walk01_Forward — 2.00; наш WalkForward_Male — 0.54.")]
        private float _clipSpeed;

        [SerializeField, Tooltip("Сглаживание разгона, м/с². 0 = мгновенно.")]
        private float _acceleration = 8f;

        [Header("HipsDelta")]
        [SerializeField, Tooltip("Кость таза. Пусто — ищется B-hips в Animator.")]
        private Transform _hips;

        [SerializeField, Tooltip("Считать дельту таза по мировым позициям (так клип и пекётся).")]
        private bool _hipsDeltaWorldSpace = true;

        [SerializeField, Tooltip("Скачок больше этого за кадр считается телепортом/Floating Origin rebase и игнорируется, м.")]
        private float _maxDeltaPerFrame = 0.5f;

        [Header("Применение")]
        [SerializeField, Tooltip("Разрешить двигать transform, если нет CharacterController. По умолчанию нельзя: " +
                                 "сетевой персонаж двигается CharacterController + NetworkTransform.")]
        private bool _allowTransformFallback;

        [SerializeField, Tooltip("Логировать старт/стоп и отказы.")]
        private bool _logVerbose = true;

        private MotionBricksBakedPlayback _playback;
        private CharacterController _controller;
        private Animator _animator;
        private bool _warnedNoMover;
        private float _currentSpeed;
        private bool _hasPrev;
        private Vector3 _prevHips;

        public float CurrentSpeed => _currentSpeed;

        // === Lifecycle ===

        private void OnEnable()
        {
            _playback = GetComponent<MotionBricksBakedPlayback>();
            if (_playback == null) _playback = GetComponentInParent<MotionBricksBakedPlayback>();
            if (_playback == null) _playback = GetComponentInChildren<MotionBricksBakedPlayback>(true);

            _controller = GetComponent<CharacterController>();
            if (_controller == null) _controller = GetComponentInParent<CharacterController>();

            if (_settings == null)
            {
                if (_logVerbose) Debug.Log("[MotionBricksBakedRootMotion] Настройки не назначены → Disabled (движение не переносится).", this);
                enabled = false;
                return;
            }

            if (!_settings.AllowsPlayback)
            {
                if (_logVerbose) Debug.Log("[MotionBricksBakedRootMotion] Mode=Disabled → движение клипа не переносится.", this);
                enabled = false;
                return;
            }

            _animator = _playback != null ? _playback.TargetAnimator : GetComponentInChildren<Animator>(true);
            if (_hips == null && _animator != null)
            {
                foreach (var t in _animator.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "B-hips") { _hips = t; break; }
                }
            }

            _hasPrev = false;
            _currentSpeed = 0f;
        }

        private void OnDisable()
        {
            _hasPrev = false;
            _currentSpeed = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool playing = _playback != null && _playback.IsPlaying;
            float target = playing ? _clipSpeed : 0f;

            // Разгон/торможение: без него старт и стоп клипа дают рывок.
            _currentSpeed = _acceleration > 0f
                ? Mathf.MoveTowards(_currentSpeed, target, _acceleration * dt)
                : target;

            Vector3 displacement = _source == MotionBricksRootMotionSource.ClipSpeed
                ? ClipSpeedDisplacement(playing, dt)
                : HipsDeltaDisplacement(playing);

            if (displacement.sqrMagnitude > 0f) Apply(displacement);
        }

        // === Источники ===

        private Vector3 ClipSpeedDisplacement(bool playing, float dt)
        {
            if (!playing || Mathf.Abs(_currentSpeed) < 1e-4f) return Vector3.zero;

            // Вперёд по лицу персонажа: таз/модель может быть повёрнут, поэтому берём forward корня компонента.
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;

            return forward * (_currentSpeed * dt);
        }

        private Vector3 HipsDeltaDisplacement(bool playing)
        {
            if (_hips == null) return Vector3.zero;

            Vector3 current = _hipsDeltaWorldSpace ? _hips.position : _hips.localPosition;
            if (!_hasPrev)
            {
                _prevHips = current;
                _hasPrev = true;
                return Vector3.zero;
            }

            if (!playing)
            {
                // Клип не играет — таз стоит, дельту не копим, иначе первый кадр после старта дёрнет.
                _prevHips = current;
                return Vector3.zero;
            }

            Vector3 delta = current - _prevHips;
            _prevHips = current;

            delta.y = 0f;

            if (delta.magnitude > _maxDeltaPerFrame)
            {
                if (_logVerbose)
                    Debug.Log($"[MotionBricksBakedRootMotion] Дельта таза {delta.magnitude:F2} м за кадр > {_maxDeltaPerFrame:F2} м — пропущена (телепорт или rebase Floating Origin).", this);
                return Vector3.zero;
            }

            return delta;
        }

        // === Применение ===

        private void Apply(Vector3 displacement)
        {
            if (_controller != null)
            {
                if (_controller.enabled) _controller.Move(displacement);
                return;
            }

            if (_allowTransformFallback)
            {
                transform.position += displacement;
                return;
            }

            if (!_warnedNoMover)
            {
                _warnedNoMover = true;
                Debug.LogWarning("[MotionBricksBakedRootMotion] Нет CharacterController, а _allowTransformFallback выключен → " +
                                 "движение клипа не переносится. Включите флаг только для нелокальных/небоевых объектов.", this);
            }
        }

        // === Ручные проверки в Editor ===

        [ContextMenu("MotionBricks/Log Speed")]
        private void ContextLog() =>
            Debug.Log($"[MotionBricksBakedRootMotion] source={_source}, заданная скорость {_clipSpeed:F2} м/с, текущая {_currentSpeed:F2} м/с, " +
                      $"контроллер {(_controller != null ? _controller.name : "нет")}, таз {(_hips != null ? _hips.name : "нет")}", this);
    }
}
