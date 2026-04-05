using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ProjectC.Player
{
    /// <summary>
    /// Сетевой контроллер корабля — полёт на Rigidbody.
    /// Кооп-пилотирование: несколько игроков могут управлять одновременно.
    /// Ввод суммируется на сервере. NetworkTransform(ServerAuthority) реплицирует всем.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public class ShipController : NetworkBehaviour
    {
        [Header("Тяга")]
        [SerializeField] private float thrustForce = 500f;
        [SerializeField] private float maxSpeed = 30f;

        [Header("Вращение")]
        [SerializeField] private float yawForce = 30f;
        [SerializeField] private float pitchForce = 40f;

        [Header("Вертикальное движение")]
        [SerializeField] private float verticalForce = 300f;

        [Header("Антигравитация")]
        [SerializeField] [Range(0f, 1.5f)] private float antiGravity = 1f;

        [Header("Аэродинамика")]
        [SerializeField] private float linearDrag = 1f;
        [SerializeField] private float angularDrag = 2f;

        [Header("Стабилизация")]
        [SerializeField] private float stabilizationForce = 50f;
        [SerializeField] private bool autoStabilize = true;

        // Rigidbody
        private Rigidbody _rb;

        // Список пилотов (кооп-управление)
        private HashSet<ulong> _pilots = new HashSet<ulong>();

        // Накопленный ввод от всех пилотов (сервер)
        private float _sumThrust, _sumYaw, _sumPitch, _sumVertical;
        private int _boostCount, _inputCount;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.linearDamping = linearDrag;
                _rb.angularDamping = angularDrag;
                _rb.useGravity = true;
                _rb.constraints = RigidbodyConstraints.None;
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null || !IsServer) return;
            if (_pilots.Count == 0) return;

            // Усредняем ввод от всех пилотов
            int n = Mathf.Max(1, _inputCount);
            float avgThrust = _sumThrust / n;
            float avgYaw = _sumYaw / n;
            float avgPitch = _sumPitch / n;
            float avgVertical = _sumVertical / n;
            bool anyBoost = _boostCount > 0; // Достаточно одного пилота с boost

            ApplyThrust(avgThrust, anyBoost);
            ApplyAntiGravity();
            ApplyVertical(avgVertical);
            ApplyRotation(avgYaw, avgPitch);

            if (autoStabilize && HasNoInput(avgThrust, avgYaw, avgPitch, avgVertical))
                ApplyStabilization();

            ClampVelocity();

            // Сброс буфера
            _sumThrust = 0; _sumYaw = 0; _sumPitch = 0; _sumVertical = 0;
            _boostCount = 0; _inputCount = 0;
        }

        /// <summary>
        /// Пилот шлёт ввод на сервер
        /// </summary>
        [Rpc(SendTo.Server)]
        private void SubmitShipInputRpc(float thrust, float yaw, float pitch, float vertical, bool boost, RpcParams rpcParams = default)
        {
            if (!_pilots.Contains(rpcParams.Receive.SenderClientId)) return;

            _sumThrust += thrust;
            _sumYaw += yaw;
            _sumPitch += pitch;
            _sumVertical += vertical;
            if (boost) _boostCount++;
            _inputCount++;
        }

        public void SendShipInput(float thrust, float yaw, float pitch, float vertical, bool boost)
        {
            SubmitShipInputRpc(thrust, yaw, pitch, vertical, boost);
        }

        private void ApplyThrust(float input, bool boost)
        {
            if (Mathf.Abs(input) < 0.01f) return;
            float currentThrust = boost ? thrustForce * 2f : thrustForce;
            _rb.AddForce(transform.forward * input * currentThrust, ForceMode.Force);
        }

        private void ApplyAntiGravity()
        {
            if (antiGravity <= 0f) return;
            float gravityCompensation = _rb.mass * Mathf.Abs(Physics.gravity.y) * antiGravity;
            _rb.AddForce(Vector3.up * gravityCompensation, ForceMode.Force);
        }

        private void ApplyVertical(float input)
        {
            if (Mathf.Abs(input) < 0.01f) return;
            _rb.AddForce(Vector3.up * input * verticalForce, ForceMode.Force);
        }

        private void ApplyRotation(float yaw, float pitch)
        {
            if (Mathf.Abs(yaw) > 0.01f)
                _rb.AddTorque(Vector3.up * yaw * yawForce, ForceMode.Force);

            if (Mathf.Abs(pitch) > 0.01f)
                _rb.AddTorque(transform.right * -pitch * pitchForce, ForceMode.Force);
        }

        private void ApplyStabilization()
        {
            Vector3 stabilizationTorque = Vector3.Cross(transform.up, Vector3.up) * stabilizationForce;
            _rb.AddTorque(stabilizationTorque, ForceMode.Force);
        }

        private bool HasNoInput(float t, float y, float p, float v) =>
            Mathf.Abs(t) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(p) < 0.01f && Mathf.Abs(v) < 0.01f;

        private void ClampVelocity()
        {
            if (_rb.linearVelocity.magnitude > maxSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }

        public float CurrentSpeed => _rb != null ? _rb.linearVelocity.magnitude : 0f;
        public bool IsGrounded => Physics.Raycast(transform.position, Vector3.down, out _, 1.5f);
        public Vector3 GetExitPosition() => transform.position + Vector3.up * 1.5f;
        public int PilotCount => _pilots.Count;

        /// <summary>
        /// Добавить пилота (кооп — несколько могут одновременно)
        /// </summary>
        public void AddPilot(NetworkPlayer pilot)
        {
            AddPilotRpc(pilot.OwnerClientId);
        }

        [Rpc(SendTo.Everyone)]
        private void AddPilotRpc(ulong clientId, RpcParams rpcParams = default)
        {
            _pilots.Add(clientId);
            enabled = true;
            Debug.Log($"[Ship] Пилот вошёл: Client {clientId} (всего: {_pilots.Count})");
        }

        /// <summary>
        /// Снять пилота
        /// </summary>
        public void RemovePilot(ulong clientId)
        {
            RemovePilotRpc(clientId);
        }

        [Rpc(SendTo.Everyone)]
        private void RemovePilotRpc(ulong clientId, RpcParams rpcParams = default)
        {
            _pilots.Remove(clientId);
            if (_pilots.Count == 0) enabled = false;
            Debug.Log($"[Ship] Пилот вышел: Client {clientId} (осталось: {_pilots.Count})");
        }
    }
}
