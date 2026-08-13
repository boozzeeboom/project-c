// BoneJitterRuntimeProbe — T-JITTER13: рантайм-диагностика микротряски персонажа/NPC.
//
// ЗАЧЕМ: edit-mode зонд (JitterClipProbe, T-JITTER12) доказал, что humanoid-оценка
// клипа HumanM@Idle01 математически ГЛАДКАЯ (шаги ≤1.3мм @90fps, 0 осцилляций).
// Значит шум возникает в рантайм-контексте. Этот зонд различает 3 слоя за 1 сессию:
//
//   СЛОЙ 1 — State machine: логирует КАЖДЫЙ переход state'ов (Idle→Fall→Idle фликер
//             от 1-кадрового isGrounded=false даёт характерные переходы пачками).
//   СЛОЙ 2 — Кости: per-frame дельты worldPos 3 костей (Hips/Head/Hand.L) в мм.
//             Кости прыгают → шум выше рендера (аниматор/параметры).
//             Кости гладкие, а на экране тряска → шум ниже (GPU skinning/камера).
//   СЛОЙ 3 — Контекст: дистанция от origin, animator.speed, updateMode, текущий state.
//
// ИСПОЛЬЗОВАНИЕ: повесить на root NetworkPlayer (или NPC). F9 — пауза/продолжить лог.
// Лог — одна сводная строка в секунду + строка на каждый переход state'ов.
// Аниматор ищется ЛЕНИВО (каждый кадр), т.к. на игроке контроллер аниматору
// назначается кастомизацией уже в рантайме.
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.DebugTools
{
    [AddComponentMenu("ProjectC/Debug/BoneJitterRuntimeProbe")]
    public class BoneJitterRuntimeProbe : MonoBehaviour
    {
        [Tooltip("Логировать сводку каждую секунду. F9 — toggle в рантайме.")]
        [SerializeField] private bool _logging = true;

        private Animator _animator;
        private Transform _hips, _head, _handL;

        private Vector3 _prevHips, _prevHead, _prevHand;
        private Vector3 _prevHipsDelta;
        private float _maxStepHips, _maxStepHead, _maxStepHand;
        private int _flips;
        private int _frames;
        private float _windowStart;
        private int _lastStateHash;
        private int _transitionCount;
        private bool _init;
        private bool _gaveUp;
        private float _lastSearchLog;
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Start()
        {
            TryInit();
        }

        private bool TryInit()
        {
            if (_animator != null) return true;
            if (_gaveUp) return false;

            // Находим humanoid-аниматор по AVATAR (он есть в префабе независимо от
            // контроллера). Контроллер назначает кастомизация в рантайме; если его ещё
            // нет — кости будут статичны, и мы явно скажем об этом в логе СТАРТ.
            Animator withController = null;
            foreach (var a in GetComponentsInChildren<Animator>(true))
            {
                if (a == null) continue;
                if (a.runtimeAnimatorController != null && withController == null) withController = a;
                if (a.isHuman) { _animator = a; break; }
            }
            if (_animator == null) _animator = withController;

            if (_animator == null)
            {
                // Совсем нет аниматора — тихий retry.
                return false;
            }

            if (!_animator.isHuman)
            {
                Debug.LogWarning($"[JitterProbe] {name}: найден Animator '{GetPath(_animator.transform)}' " +
                                 $"с контроллером, но avatar НЕ humanoid (isHuman=false) — зонд неактивен.", this);
                _gaveUp = true;
                return false;
            }

            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _handL = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (_hips == null || _head == null || _handL == null)
            {
                Debug.LogWarning($"[JitterProbe] {name}: avatar не содержит нужных костей (hips/head/hand.L) — зонд неактивен.", this);
                _gaveUp = true;
                return false;
            }

            _windowStart = Time.unscaledTime;
            _lastStateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            bool hasController = _animator.runtimeAnimatorController != null;
            Debug.Log($"[JitterProbe] {name}: СТАРТ. animator='{GetPath(_animator.transform)}' " +
                      $"controller={(hasController ? _animator.runtimeAnimatorController.name : "НЕТ (кастомизация не применила)")} " +
                      $"updateMode={_animator.updateMode} culling={_animator.cullingMode} " +
                      $"rootMotion={_animator.applyRootMotion} distOrigin={transform.position.magnitude:F0}m", this);
            return true;
        }

        private void LateUpdate()
        {
            if (!TryInit())
            {
                // Раз в 5 секунд подсказываем, что зонд ещё ищет аниматор.
                float now = Time.unscaledTime;
                if (!_gaveUp && now - _lastSearchLog > 5f)
                {
                    _lastSearchLog = now;
                    Debug.Log($"[JitterProbe] {name}: аниматор с контроллером пока не найден (retry)...", this);
                }
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
            {
                _logging = !_logging;
                Debug.Log($"[JitterProbe] {name}: logging={_logging}");
            }

            // --- Слой 1: переходы state machine ---
            var si = _animator.GetCurrentAnimatorStateInfo(0);
            if (si.shortNameHash != _lastStateHash)
            {
                _transitionCount++;
                if (_logging)
                    Debug.Log($"[JitterProbe] {name} @{Time.unscaledTime:F2}s: STATE {StateName(_lastStateHash)} → {StateName(si.shortNameHash)}" +
                              $"{(_animator.IsInTransition(0) ? " (blend)" : "")}", this);
                _lastStateHash = si.shortNameHash;
            }

            // --- Слой 2: покадровые дельты костей ---
            if (!_init)
            {
                _prevHips = _hips.position; _prevHead = _head.position; _prevHand = _handL.position;
                _prevHipsDelta = Vector3.zero;
                _init = true;
                return;
            }

            Vector3 dHips = _hips.position - _prevHips;
            float stepHips = dHips.magnitude * 1000f;
            float stepHead = (_head.position - _prevHead).magnitude * 1000f;
            float stepHand = (_handL.position - _prevHand).magnitude * 1000f;
            if (stepHips > _maxStepHips) _maxStepHips = stepHips;
            if (stepHead > _maxStepHead) _maxStepHead = stepHead;
            if (stepHand > _maxStepHand) _maxStepHand = stepHand;

            if (_frames > 0 &&
                ((Mathf.Sign(dHips.x) != Mathf.Sign(_prevHipsDelta.x) && Mathf.Abs(dHips.x) > 1e-5f && Mathf.Abs(_prevHipsDelta.x) > 1e-5f) ||
                 (Mathf.Sign(dHips.y) != Mathf.Sign(_prevHipsDelta.y) && Mathf.Abs(dHips.y) > 1e-5f && Mathf.Abs(_prevHipsDelta.y) > 1e-5f) ||
                 (Mathf.Sign(dHips.z) != Mathf.Sign(_prevHipsDelta.z) && Mathf.Abs(dHips.z) > 1e-5f && Mathf.Abs(_prevHipsDelta.z) > 1e-5f)))
                _flips++;

            _prevHipsDelta = dHips;
            _prevHips = _hips.position; _prevHead = _head.position; _prevHand = _handL.position;
            _frames++;

            // --- Сводка раз в секунду ---
            float t = Time.unscaledTime;
            if (t - _windowStart >= 1f && _logging)
            {
                _sb.Length = 0;
                _sb.Append($"[JitterProbe] {name}: state={StateName(si.shortNameHash)}");
                if (_animator.IsInTransition(0)) _sb.Append("+blend");
                _sb.Append($" transitions={_transitionCount}/s");
                _sb.Append($" | maxStep mm: hips={_maxStepHips:F2} head={_maxStepHead:F2} hand={_maxStepHand:F2}");
                _sb.Append($" | flips={_flips}/{_frames}f");
                _sb.Append($" | distOrigin={transform.position.magnitude:F0}m fps={1f / Mathf.Max(Time.unscaledDeltaTime, 1e-5f):F0}");
                Debug.Log(_sb.ToString(), this);

                _maxStepHips = _maxStepHead = _maxStepHand = 0f;
                _flips = 0; _frames = 0; _transitionCount = 0;
                _windowStart = t;
            }
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private string StateName(int hash)
        {
            if (hash == Animator.StringToHash("Idle")) return "Idle";
            if (hash == Animator.StringToHash("Fall")) return "Fall";
            if (hash == Animator.StringToHash("Walk")) return "Walk";
            if (hash == Animator.StringToHash("Run")) return "Run";
            if (hash == Animator.StringToHash("Jump")) return "Jump";
            if (hash == Animator.StringToHash("Land")) return "Land";
            return hash.ToString();
        }
    }
}
