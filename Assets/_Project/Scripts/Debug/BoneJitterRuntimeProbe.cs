// BoneJitterRuntimeProbe — T-JITTER13: рантайм-диагностика микротряски персонажа/NPC.
//
// ЗАЧЕМ: edit-mode зонд (JitterClipProbe, T-JITTER12) доказал, что humanoid-оценка
// клипа HumanM@Idle01 математически ГЛАДКАЯ (шаги ≤1.3мм @90fps, 0 осцилляций).
// Значит шум возникает в рантайм-контексте. Этот зонд различает 3 слоя за 1 сессию:
//
//   СЛОЙ 1 — State machine: логирует КАЖДЫЙ переход state'ов (Idle→Fall→Idle фликер
//             от 1-кадрового isGrounded=false даёт характерные переходы пачками).
//   СЛОЙ 2 — Кости: per-frame дельты worldPos 3 костей (Hips/Head/Hand.L) в мм.
//             Если кости прыгают — шум выше рендера (аниматор/параметры).
//             Если кости гладкие, а на экране тряска — шум ниже (GPU skinning/камера).
//   СЛОЙ 3 — Контекст: дистанция от origin (float precision, H7), animator.speed,
//             updateMode, текущий state.
//
// ИСПОЛЬЗОВАНИЕ: повесить на root NetworkPlayer (или NPC). F9 — пауза/продолжить лог.
// Лог — одна сводная строка в секунду + строка на каждый переход state'ов.
// Убрать с префаба после диагностики (или оставить — no-op без нажатий, ~0 аллокаций).
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
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Start()
        {
            // Как FindFirstValidAnimator в NetworkPlayer: первый Animator с контроллером.
            foreach (var a in GetComponentsInChildren<Animator>(true))
            {
                if (a != null && a.runtimeAnimatorController != null) { _animator = a; break; }
            }
            if (_animator == null || !_animator.isHuman)
            {
                Debug.LogWarning($"[JitterProbe] {name}: humanoid Animator не найден — зонд неактивен.", this);
                enabled = false;
                return;
            }
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _handL = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _windowStart = Time.unscaledTime;
            _lastStateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            Debug.Log($"[JitterProbe] {name}: старт. updateMode={_animator.updateMode} culling={_animator.cullingMode} " +
                      $"rootMotion={_animator.applyRootMotion} distOrigin={transform.position.magnitude:F0}m", this);
        }

        private void LateUpdate()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
            {
                _logging = !_logging;
                Debug.Log($"[JitterProbe] {name}: logging={_logging}");
            }
            if (_animator == null) return;

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

            // Осцилляция: смена знака дельты hips по любой оси (амплитуда > 0.01мм)
            if (_frames > 0 &&
                ((Mathf.Sign(dHips.x) != Mathf.Sign(_prevHipsDelta.x) && Mathf.Abs(dHips.x) > 1e-5f && Mathf.Abs(_prevHipsDelta.x) > 1e-5f) ||
                 (Mathf.Sign(dHips.y) != Mathf.Sign(_prevHipsDelta.y) && Mathf.Abs(dHips.y) > 1e-5f && Mathf.Abs(_prevHipsDelta.y) > 1e-5f) ||
                 (Mathf.Sign(dHips.z) != Mathf.Sign(_prevHipsDelta.z) && Mathf.Abs(dHips.z) > 1e-5f && Mathf.Abs(_prevHipsDelta.z) > 1e-5f)))
                _flips++;

            _prevHipsDelta = dHips;
            _prevHips = _hips.position; _prevHead = _head.position; _prevHand = _handL.position;
            _frames++;

            // --- Сводка раз в секунду ---
            float now = Time.unscaledTime;
            if (now - _windowStart >= 1f && _logging)
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
                _windowStart = now;
            }
        }

        private string StateName(int hash)
        {
            // Краткие имена известных state'ов (PlayerAnimation + NpcAnimatorController)
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
