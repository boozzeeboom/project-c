// Project C: T-MB04a/T-MB04b — перенос движения baked-клипа на персонажа (root motion).
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
//     исключение — редакторный обход MotionBricksBakedPlayback.ReviewOverride (только для приёмки);
//   - по умолчанию _clipSpeed = 0 → компонент физически ничего не двигает, пока скорость не задана явно;
//   - двигает только при работающем playback (нет клипа — нет движения);
//   - не кэширует мировые позиции между кадрами дольше одного кадра, поэтому Floating Origin
//     (перебазирование мира) не ломает дельту: старая позиция обнуляется в том же кадре.
//
// T-MB04b — два важных изменения против T-MB04a:
//   1. На сетевом персонаже компонент НЕ двигает ничего сам: NetworkPlayer уже вызывает
//      CharacterController.Move из ProcessMovement, и второй Move дал бы двойную скорость.
//      Здесь стоит жёсткий запрет (NetworkPlayer в этом же объекте/родителях) — дублирование
//      структурно невозможно. Движение игрока интегрируется в его movement, а не этим компонентом.
//   2. После body swap M↔F кость таза и Animator пересоздаются: компонент переподписывается на
//      CharacterCustomisationApplier.BodySwapped и заново ищет B-hips, иначе едет за уничтоженным
//      transform (риск №3 ревью T-MB04a).
//   3. HipsDelta читается в LateUpdate — после humanoid-пасса Animator. Чтение в Update даёт позу
//      прошлого кадра (риск №5 ревью T-MB04a).
//
// T-MB04c — движение игрока:
//   На объекте с NetworkPlayer компонент по-прежнему НЕ вызывает CharacterController.Move.
//   Вместо предупреждения он публикует посчитанное смещение в MotionBricksLocomotionBridge,
//   откуда его забирает NetworkPlayer.ProcessMovement и применяет в своём единственном Move.
//   Одно место расчёта, один Move. DefaultExecutionOrder ставит компонент раньше NetworkPlayer,
//   чтобы тот читал смещение текущего кадра, а не прошлого.

using ProjectC.Player;   // CharacterCustomisationApplier (BodySwapped), NetworkPlayer (мост движения)
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
    // Раньше NetworkPlayer (порядок 0): смещение кадра должно быть готово до его ProcessMovement.
    [DefaultExecutionOrder(-100)]
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
        private CharacterCustomisationApplier _applier;
        private CharacterController _controller;
        private NetworkPlayer _networkPlayer;
        private Animator _animator;
        private bool _warnedNoMover;
        private bool _loggedBridgePublish;
        private float _currentSpeed;
        private bool _hasPrev;
        private Vector3 _prevHips;
        private Vector3 _pendingHipsDelta;
        private Vector3 _bridgeDisplacement;

        public float CurrentSpeed => _currentSpeed;

        /// <summary>NetworkPlayer, которому принадлежит источник (null — обычный объект/проба).</summary>
        public NetworkPlayer BoundPlayer => _networkPlayer;

        /// <summary>
        /// Может ли источник вести движение игрока в этом кадре: гейт настроек + реально играющий
        /// playback. Проверяется мостом у NetworkPlayer, поэтому смена mode на лету тоже учтена.
        /// </summary>
        public bool CanDrivePlayer
        {
            get
            {
                if (_networkPlayer == null || !isActiveAndEnabled) return false;
                if (_playback == null || !_playback.IsPlaying) return false;
                if (_settings == null || !_settings.allowPlayer) return false;

                bool allowed = _settings.AllowsPlayback;
#if UNITY_EDITOR
                allowed |= MotionBricksBakedPlayback.ReviewOverride;
#endif
                return allowed;
            }
        }

        /// <summary>Забрать смещение кадра (после чтения обнуляется: второй потребитель получит ноль).</summary>
        public Vector3 ConsumeBridgeDisplacement()
        {
            Vector3 value = _bridgeDisplacement;
            _bridgeDisplacement = Vector3.zero;
            return value;
        }

        /// <summary>Скорость под клип, м/с. Ноль — безопасный дефолт (компонент инертен).</summary>
        public float ClipSpeed
        {
            get => _clipSpeed;
            set => _clipSpeed = Mathf.Max(0f, value);
        }

        // === Lifecycle ===

        private void OnEnable()
        {
            ResolveRefs();

            _hasPrev = false;
            _pendingHipsDelta = Vector3.zero;
            _bridgeDisplacement = Vector3.zero;
            _currentSpeed = 0f;
            _loggedBridgePublish = false;

            if (_settings == null)
            {
                if (_logVerbose) Debug.Log("[MotionBricksBakedRootMotion] Настройки не назначены → Disabled (движение не переносится).", this);
                enabled = false;
                return;
            }

            // Тот же гейт, что у playback: Mode=Disabled в билде или явный редакторный обход приёмки.
            bool allowed = _settings.AllowsPlayback;
#if UNITY_EDITOR
            allowed |= MotionBricksBakedPlayback.ReviewOverride;
#endif
            if (!allowed)
            {
                if (_logVerbose) Debug.Log("[MotionBricksBakedRootMotion] Mode=Disabled → движение клипа не переносится.", this);
                enabled = false;
                return;
            }

            if (_applier != null) _applier.BodySwapped += OnBodySwapped;

            ResolveAnimator();
            ResolveHips();

            // T-MB04c: на сетевом персонаже смещение публикуется в мост (Move делает NetworkPlayer).
            if (_networkPlayer != null) MotionBricksLocomotionBridge.Register(this);
        }

        private void OnDisable()
        {
            if (_applier != null) _applier.BodySwapped -= OnBodySwapped;

            MotionBricksLocomotionBridge.Unregister(this);

            _hasPrev = false;
            _pendingHipsDelta = Vector3.zero;
            _bridgeDisplacement = Vector3.zero;
            _currentSpeed = 0f;
            _loggedBridgePublish = false;
        }

        private void Update()
        {
            if (_playback == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool playing = _playback.IsPlaying;
            float target = playing ? _clipSpeed : 0f;

            // Разгон при старте клипа. На Stop движение прекращается сразу: клип больше не играет,
            // синхронизировать нечего, и «докат» после Stop только выглядел бы как скольжение.
            _currentSpeed = _acceleration > 0f
                ? Mathf.MoveTowards(_currentSpeed, target, _acceleration * dt)
                : target;

            Vector3 displacement = _source == MotionBricksRootMotionSource.ClipSpeed
                ? ClipSpeedDisplacement(playing, dt)
                : ConsumeHipsDelta(playing);

            if (_source != MotionBricksRootMotionSource.HipsDelta) _pendingHipsDelta = Vector3.zero;

            // Смещение живёт ровно один кадр: если источник ничего не дал, мост не должен
            // отдать прошлое смещение (иначе персонаж «докатится» после Stop).
            _bridgeDisplacement = Vector3.zero;

            if (displacement.sqrMagnitude > 0f) Apply(displacement);
        }

        /// <summary>
        /// Дельта таза читается здесь, а не в Update: Animator применяет humanoid-позу между Update
        /// и LateUpdate, поэтому только тут видно то, что видит игрок. Delta за кадр хранится до
        /// ближайшего Update — движение применяется из одного места (Apply).
        /// </summary>
        private void LateUpdate()
        {
            if (_source != MotionBricksRootMotionSource.HipsDelta) return;
            SampleHipsDelta(_playback != null && _playback.IsPlaying);
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

        private void SampleHipsDelta(bool playing)
        {
            if (_hips == null) return;

            Vector3 current = _hipsDeltaWorldSpace ? _hips.position : _hips.localPosition;
            if (!_hasPrev)
            {
                _prevHips = current;
                _hasPrev = true;
                _pendingHipsDelta = Vector3.zero;
                return;
            }

            Vector3 delta = current - _prevHips;
            _prevHips = current;

            if (!playing)
            {
                // Клип не играет — таз стоит, дельту не копим, иначе первый кадр после старта дёрнет.
                _pendingHipsDelta = Vector3.zero;
                return;
            }

            delta.y = 0f;

            if (delta.magnitude > _maxDeltaPerFrame)
            {
                if (_logVerbose)
                    Debug.Log($"[MotionBricksBakedRootMotion] Дельта таза {delta.magnitude:F2} м за кадр > {_maxDeltaPerFrame:F2} м — пропущена (телепорт, склейка петли или rebase Floating Origin).", this);
                _pendingHipsDelta = Vector3.zero;
                return;
            }

            _pendingHipsDelta = delta;
        }

        private Vector3 ConsumeHipsDelta(bool playing)
        {
            Vector3 delta = _pendingHipsDelta;
            _pendingHipsDelta = Vector3.zero;

            // Для HipsDelta скорость _clipSpeed не участвует: смещение берётся из позы клипа.
            if (!playing) return Vector3.zero;
            return delta;
        }

        // === Применение ===

        private void Apply(Vector3 displacement)
        {
            // T-MB04c, HARD: на сетевом персонаже этот компонент не двигает НИЧЕГО.
            // NetworkPlayer.ProcessMovement уже вызывает CharacterController.Move — второй Move
            // дал бы двойную скорость и рассинхрон с NetworkTransform. Смещение публикуется
            // в мост, откуда его забирает тот же ProcessMovement (единственный Move).
            if (_networkPlayer != null)
            {
                _bridgeDisplacement = displacement;

                if (_logVerbose && !_loggedBridgePublish)
                {
                    _loggedBridgePublish = true;
                    Debug.Log("[MotionBricksBakedRootMotion] Смещение клипа уходит в NetworkPlayer.ProcessMovement " +
                              "через MotionBricksLocomotionBridge; CharacterController.Move отсюда не вызывается.", this);
                }

                return;
            }

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

        // === Internals ===

        private void ResolveRefs()
        {
            _playback = GetComponent<MotionBricksBakedPlayback>();
            if (_playback == null) _playback = GetComponentInParent<MotionBricksBakedPlayback>();
            if (_playback == null) _playback = GetComponentInChildren<MotionBricksBakedPlayback>(true);

            _applier = GetComponent<CharacterCustomisationApplier>();
            if (_applier == null) _applier = GetComponentInParent<CharacterCustomisationApplier>();

            _controller = GetComponent<CharacterController>();
            if (_controller == null) _controller = GetComponentInParent<CharacterController>();

            _networkPlayer = GetComponent<NetworkPlayer>();
            if (_networkPlayer == null) _networkPlayer = GetComponentInParent<NetworkPlayer>();
        }

        private void ResolveAnimator()
        {
            _animator = _playback != null ? _playback.TargetAnimator : null;
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        }

        private void ResolveHips()
        {
            if (_hips != null && _hips.gameObject != null) return;

            _hips = null;
            if (_animator == null) return;

            foreach (var t in _animator.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "B-hips") { _hips = t; break; }
            }
        }

        private void OnBodySwapped(Animator newAnimator)
        {
            // Старый Animator и кости уничтожены вместе с телом: старый _hips — «мёртвая» ссылка,
            // дельта по нему дала бы телепорт. Перепривязываемся к новому телу и сбрасываем историю.
            _animator = newAnimator;
            _hips = null;
            _hasPrev = false;
            _pendingHipsDelta = Vector3.zero;

            ResolveAnimator();
            ResolveHips();

            if (_logVerbose)
                Debug.Log($"[MotionBricksBakedRootMotion] Body swap → новый Animator '{(_animator != null ? _animator.name : "нет")}', " +
                          $"таз '{(_hips != null ? _hips.name : "нет")}'. История дельты сброшена.", this);
        }

        // === Ручные проверки в Editor ===

#if UNITY_EDITOR
        /// <summary>
        /// T-MB04c: собрать компонент, добавленный в рантайме из меню Review (приёмка на живом
        /// NetworkPlayer: компонент живёт только в сессии Play Mode, префаб не трогается).
        /// Гейт не ослабляется — работает тот же ReviewOverride, что и у playback.
        /// </summary>
        public void ConfigureForReview(MotionBricksRuntimeSettings settings, float clipSpeed,
                                       float acceleration, bool logVerbose)
        {
            _settings = settings;
            _source = MotionBricksRootMotionSource.ClipSpeed;
            _clipSpeed = Mathf.Max(0f, clipSpeed);
            _acceleration = Mathf.Max(0f, acceleration);
            _allowTransformFallback = false;
            _logVerbose = logVerbose;
        }
#endif

        [ContextMenu("MotionBricks/Log Speed")]
        private void ContextLog() =>
            Debug.Log($"[MotionBricksBakedRootMotion] source={_source}, заданная скорость {_clipSpeed:F2} м/с, текущая {_currentSpeed:F2} м/с, " +
                      $"контроллер {(_controller != null ? _controller.name : "нет")}, таз {(_hips != null ? _hips.name : "нет")}, " +
                      $"сетевой персонаж {(_networkPlayer != null ? _networkPlayer.name : "нет")}", this);
    }
}
