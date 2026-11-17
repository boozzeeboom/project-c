#if UNITY_EDITOR
// Project C: T-MB06 — owner-only neural playback поверх обычного Animator.

using System;
using ProjectC.Player;
using ProjectC.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Animation.MotionBricks
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ProjectC/Animation/MotionBricks Runtime Playback (Experimental)")]
    public sealed class MotionBricksRuntimePlayback : MonoBehaviour
    {
        [Header("Gate")]
        [SerializeField] private MotionBricksRuntimeSettings _settings;
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _restTemplate;

        [Header("Official runtime")]
        [SerializeField] private string _pythonPath = @"C:\Projects\motion_bricks\motionbricks\.venv\Scripts\python.exe";
        [SerializeField] private string _repositoryPath = @"C:\Projects\motion_bricks\motionbricks";
        [SerializeField] private string _scriptPath = @"C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\tools\runtime_sidecar.py";
        [SerializeField] private int _port = 49360;
        [SerializeField] private bool _fp16;
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _runSpeed = 10f;

        [Header("Failure guard")]
        // 1.5 с было меньше, чем стоит пауза/хитч редактора в этом проекте (в логе видно
        // `stale pose (5004 ms)` и `(13000 ms)` при таймауте 1.5 с — значит главный поток стоял сам).
        [SerializeField, Min(0.2f)] private float _staleTimeoutSeconds = 6f;
        [SerializeField, Min(1)] private int _failureBudget = 3;
        // Кадр дольше этого — не молчание sidecar, а провал главного потока: такой кадр
        // не считается устареванием, но модель надо пере-заякорить (RESET).
        [SerializeField, Min(0.1f)] private float _hitchThresholdSeconds = 0.5f;
        // Пока sidecar поднимается (ENGINE ~3.3 с + импорт Warp), ответов нет; шлём редкой пробой,
        // чтобы не копить очередь в сокете.
        [SerializeField, Min(0.05f)] private float _warmupSendInterval = 0.5f;

        [Header("Airborne fallback")]
        // 0.1 с давало ложные срабатывания ступенькой/склоном: в логе видны пары
        // `airborne: STOP` → `START` с интервалом 0.2-0.8 с, т.е. анимация пропадала на ровном месте.
        [SerializeField, Min(0.05f)] private float _airborneDelaySeconds = 0.25f;
        [SerializeField, Min(0.1f)] private float _airborneVerticalSpeed = 2f;

        private NetworkPlayer _player;
        private CharacterController _controller;
        private CharacterCustomisationApplier _applier;
        private SkillAnimationPlayer _skillPlayer;
        private MotionBricksRuntimeClient _client;
        private MotionBricksSomaRetarget _retarget;
        private MotionBricksRuntimeFrame _frame;
        private bool _configured;
        private bool _reviewOverride;
        private bool _neuralActive;
        private bool _resetPending;
        private bool _receivedAnyPose;
        private float _startedAt;
        private float _firstPoseMs = -1f;
        private int _resetCount;
        private float _lastSendAt;
        private float _movementStartedAt;
        private float _onsetMs = -1f;
        private bool _wasMoving;
        private bool _hasLastPosition;
        private Vector3 _lastPosition;
        private bool _rootMotionGuardActive;
        private bool _savedApplyRootMotion;
        private AnimatorCullingMode _savedCullingMode;
        private Animator _guardAnimator;
        private int _fallbackCount;
        private string _lastStopReason = "not started";
        private uint _lastAppliedSequence;
        private float _airborneTimer;
        private int _hitchCount;
        private bool _resumeAfterLanding;

        public bool IsNeuralActive => _neuralActive;
        public int FallbackCount => _fallbackCount;
        public string LastStopReason => _lastStopReason;

        private void OnEnable()
        {
            _player = GetComponent<NetworkPlayer>();
            _controller = GetComponent<CharacterController>();
            _applier = GetComponent<CharacterCustomisationApplier>();
            _skillPlayer = GetComponent<SkillAnimationPlayer>();
            if (_applier != null) _applier.BodySwapped += OnBodySwapped;
        }

        private void Update()
        {
            if (!_configured) return;
            DrainLogs();
            if (TogglePressed())
            {
                if (_neuralActive) StopRuntime("Ctrl+F10: legacy Animator");
                else StartRuntime();
            }
            if (_player == null || !_player.IsOwner)
            {
                if (_neuralActive) FailRuntime("owner lost");
                return;
            }
            if (_controller == null || !_controller.enabled)
            {
                if (_neuralActive) StopRuntime("CharacterController disabled");
                return;
            }

            bool casting = _skillPlayer != null && _skillPlayer.IsCasting;
            if (casting)
            {
                if (_neuralActive) StopRuntime("combat: authored Animator");
                return;
            }

            bool airborne = !_controller.isGrounded && Mathf.Abs(_controller.velocity.y) > _airborneVerticalSpeed;
            if (airborne)
            {
                _airborneTimer += Time.deltaTime;
                if (_neuralActive && _airborneTimer >= _airborneDelaySeconds)
                {
                    StopRuntime("airborne: authored Animator");
                    _resumeAfterLanding = true;
                    return;
                }
            }
            else
            {
                _airborneTimer = 0f;
            }

            if (!_neuralActive || _client == null)
            {
                if (_resumeAfterLanding && !airborne)
                {
                    _resumeAfterLanding = false;
                    // Продолжение после приземления — не новый старт: RESET пере-заякоривает
                    // модель, заново засеивает историю и рестартит state machine/клип.
                    // В логе было 9 таких циклов за сессию (9 RESET), и анимация каждый раз
                    // начиналась заново — это и читалось как «задержка 1+ с».
                    StartRuntime(hardReset: false);
                }
                return;
            }

            if (!string.IsNullOrEmpty(_client.LastError))
            {
                FailRuntime(_client.LastError);
                return;
            }
            if (_client.HasProcessExited)
            {
                FailRuntime("sidecar exited");
                return;
            }
            if (_client.MalformedCount >= _failureBudget)
            {
                FailRuntime("protocol failure budget exhausted");
                return;
            }

            // Пока ни одного пакета не пришло — sidecar ещё поднимается. Шлём 2 раза в секунду,
            // иначе запросы копятся в сокете и после ENGINE_READY обрабатываются одним залпом:
            // в логе это дало RTT p50=1850 ms / p95=6314 ms при age=14 ms и промотку
            // нескольких секунд прошлого в модели сразу после метки LIVE.
            // ВАЖНО: стоит ДО разбора _resetPending, иначе пропущенный кадр съедает стартовый RESET.
            if (!_receivedAnyPose && Time.realtimeSinceStartup - _lastSendAt < _warmupSendInterval) return;
            _lastSendAt = Time.realtimeSinceStartup;

            bool reset = _resetPending;
            if (_hasLastPosition && (transform.position - _lastPosition).sqrMagnitude > 25f) reset = true;
            if (reset) _resetCount++;
            _hasLastPosition = true;
            _lastPosition = transform.position;
            _resetPending = false;
            Vector3 velocity = _controller.velocity;
            velocity.y = 0f;

            _client.Send(Time.deltaTime, _walkSpeed, _runSpeed, transform.position, transform.rotation, velocity, reset);

            if (_client.TryGetBindPose(out float[] bind) && _retarget == null)
            {
                try { _retarget = MotionBricksSomaRetarget.Create(_animator, _restTemplate, bind); }
                catch (Exception ex)
                {
                    FailRuntime("retarget init: " + ex.Message);
                    return;
                }
            }
            if (_client.TryConsumeLatest(out MotionBricksRuntimeFrame latest))
            {
                _frame = latest;
                if (!_receivedAnyPose)
                {
                    _receivedAnyPose = true;
                    _firstPoseMs = (Time.realtimeSinceStartup - _startedAt) * 1000f;
                    Debug.Log($"[MB-Runtime] LIVE: первый пакет через {_firstPoseMs:F0} мс после Start. " +
                              "До этой метки нейрослой молчит — это подъём ENGINE, а не задержка модели.", this);
                }
            }

            // Настоящая задержка «нажал W — анимация пошла не сразу»: от команды движения до первого
            // кадра, где стопа реально в контакте (в idle модель отдаёт 0,0/0,0/0,0/0,0).
            bool moving = velocity.sqrMagnitude > 0.25f;
            if (moving && !_wasMoving) _movementStartedAt = Time.realtimeSinceStartup;
            if (!moving) _onsetMs = -1f;
            _wasMoving = moving;
            if (moving && _onsetMs < 0f && _frame != null && FootContact(_frame.Contacts) > 0.5f)
            {
                _onsetMs = (Time.realtimeSinceStartup - _movementStartedAt) * 1000f;
                Debug.Log($"[MB-Runtime] ONSET: команда движения → первый контакт стопы {_onsetMs:F0} мс " +
                          $"(RTT p50={_client.RoundTripP50Ms:F1} ms, source={_frame.PlaybackSource}, seq={_lastAppliedSequence}).", this);
            }
            if (_receivedAnyPose && _client.LastFrameAgeMs > _staleTimeoutSeconds * 1000f)
            {
                // Провал главного потока (загрузка/GC/хитч) неотличим от молчания sidecar.
                // Такой кадр не считается падением: пере-заякориваем модель и живём дальше,
                // и НЕ убиваем процесс — иначе следующий START платит ENGINE_READY ~3.3 с,
                // что и было видимой «задержкой старта» после каждого attach.
                if (Time.unscaledDeltaTime > _hitchThresholdSeconds)
                {
                    _hitchCount++;
                    _resetPending = true;
                }
                else
                {
                    FailRuntime($"stale pose ({_client.LastFrameAgeMs:F0} ms)", keepClient: true);
                }
            }
        }

        private void LateUpdate()
        {
            if (!_neuralActive || _frame == null || _retarget == null) return;
            try
            {
                _retarget.Apply(_frame.Pose);
                _lastAppliedSequence = _frame.Sequence;
            }
            catch (Exception ex)
            {
                FailRuntime("apply pose: " + ex.Message);
            }
        }

        public void ConfigureForReview(MotionBricksRuntimeSettings settings, Animator animator, GameObject restTemplate,
                                       string pythonPath, string repositoryPath, string scriptPath,
                                       int port = 49360, bool fp16 = false)
        {
            _settings = settings;
            _animator = animator;
            _restTemplate = restTemplate;
            _pythonPath = pythonPath;
            _repositoryPath = repositoryPath;
            _scriptPath = scriptPath;
            _port = port;
            _fp16 = fp16;
            _reviewOverride = true;
            _configured = true;
        }

        public bool StartRuntime(bool hardReset = true)
        {
            if (!_configured) return false;
            bool allowed = _reviewOverride || (_settings != null && _settings.mode == MotionBricksMode.NeuralExperimental &&
                                               _settings.allowPlayer && !_settings.allowRemotePlayers);
            if (!allowed) return false;
            if (_neuralActive) return true;
            if (_player == null || !_player.IsOwner || _animator == null || _restTemplate == null)
            {
                Debug.LogError("[MB-Runtime] Нужны owner NetworkPlayer, Animator и rest template.", this);
                return false;
            }
            if (_skillPlayer != null && _skillPlayer.IsCasting) return false;
            _resumeAfterLanding = false;
            _airborneTimer = 0f;

            foreach (MotionBricksBakedPlayback baked in GetComponents<MotionBricksBakedPlayback>())
            {
                baked.StopPlayback();
                baked.enabled = false;
            }
            foreach (MotionBricksBakedRootMotion rootMotion in GetComponents<MotionBricksBakedRootMotion>())
                rootMotion.enabled = false;

            try
            {
                if (_client == null)
                {
                    _client = new MotionBricksRuntimeClient(_pythonPath, _repositoryPath, _scriptPath, _port, _fp16);
                    _client.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MB-Runtime] Sidecar не запущен: {ex}", this);
                DisposeClient();
                return false;
            }

            ApplyRootMotionGuard();
            _neuralActive = true;
            _resetPending = hardReset;
            _client.ResetSamples();
            _receivedAnyPose = false;
            _startedAt = Time.realtimeSinceStartup;
            _firstPoseMs = -1f;
            _hasLastPosition = false;
            _frame = null;
            _retarget = null;
            _lastStopReason = "running";
            Debug.Log($"[MB-Runtime] START pid={_client.ProcessId}, FP{(_fp16 ? "16" : "32")}, " +
                      "CharacterController остаётся authoritative; Ctrl+F10 — legacy Animator. " +
                      "Подъём sidecar (импорт Warp + ENGINE) занимает несколько секунд: ждите метку [MB-Runtime] LIVE.", this);
            return true;
        }

        public void StopRuntime(string reason)
        {
            if (!_neuralActive && !_rootMotionGuardActive) return;
            _neuralActive = false;
            _frame = null;
            _retarget = null;
            _lastStopReason = reason;
            _resumeAfterLanding = false;
            _airborneTimer = 0f;
            bool casting = _skillPlayer != null && _skillPlayer.IsCasting;
            if (_animator != null && _animator.isActiveAndEnabled && !casting)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }
            RestoreRootMotionGuard();
            _resetPending = true;
            Debug.Log($"[MB-Runtime] STOP: {reason}; authored Animator восстановлен.", this);
        }

        public string Diagnostics()
        {
            string source = _frame == null ? "none" : SourceName(_frame.PlaybackSource);
            string contacts = _frame == null ? "-" : string.Join("/", Array.ConvertAll(_frame.Contacts, value => value.ToString("F2")));
            return $"[MB-Runtime] active={_neuralActive}, source={source}, sequence={_lastAppliedSequence}, " +
                   $"contacts={contacts}, serverStep={(_frame != null ? _frame.ServerStepMs : 0f):F1} ms, " +
                   $"fallbacks={_fallbackCount}, hitches={_hitchCount}, resets={_resetCount}, onset={_onsetMs:F0} ms, firstPose={_firstPoseMs:F0} ms, lastStop={_lastStopReason}\n" +
                   (_client != null ? _client.Diagnostics() : "sidecar=detached");
        }

        private void FailRuntime(string reason, bool keepClient = false)
        {
            _fallbackCount++;
            Debug.LogError($"[MB-Runtime] FALLBACK: {reason}", this);
            StopRuntime(reason);
            if (!keepClient) DisposeClient();
        }

        private static float FootContact(float[] contacts)
        {
            if (contacts == null || contacts.Length == 0) return 0f;
            float max = 0f;
            for (int index = 0; index < contacts.Length; index++) max = Mathf.Max(max, contacts[index]);
            return max;
        }

        private void ApplyRootMotionGuard()
        {
            if (_animator == null) return;
            if (_rootMotionGuardActive && _guardAnimator == _animator) return;
            RestoreRootMotionGuard();
            _savedApplyRootMotion = _animator.applyRootMotion;
            _savedCullingMode = _animator.cullingMode;
            _guardAnimator = _animator;
            _rootMotionGuardActive = true;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private void RestoreRootMotionGuard()
        {
            if (!_rootMotionGuardActive) return;
            Animator animator = _guardAnimator;
            _rootMotionGuardActive = false;
            _guardAnimator = null;
            if (animator == null) return;
            animator.cullingMode = _savedCullingMode;
            if (_skillPlayer == null || !_skillPlayer.IsCasting)
                animator.applyRootMotion = _savedApplyRootMotion;
        }

        private void OnBodySwapped(Animator newAnimator)
        {
            StopRuntime("body swap: authored Animator");
            _animator = newAnimator;
            _retarget = null;
        }

        private static bool TogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.f10Key.wasPressedThisFrame &&
                   (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private static string SourceName(int source)
        {
            switch (source)
            {
                case 0: return "generated";
                case 1: return "clip_transition";
                case 2: return "clip";
                default: return "unknown";
            }
        }

        private void DrainLogs()
        {
            if (_client == null) return;
            _client.DrainLogs(
                line => Debug.Log("[MB-Sidecar] " + line, this),
                line => Debug.LogWarning("[MB-Sidecar] " + line, this));
        }

        private void DisposeClient()
        {
            if (_client == null) return;
            _client.Dispose();
            _client = null;
        }

        private void OnDisable()
        {
            StopRuntime("component disabled");
            DisposeClient();
            if (_applier != null) _applier.BodySwapped -= OnBodySwapped;
        }

        private void OnDestroy()
        {
            StopRuntime("component destroyed");
            DisposeClient();
        }
    }
}
#endif
