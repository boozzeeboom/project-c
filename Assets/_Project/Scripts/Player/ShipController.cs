using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ProjectC.Player
{
    /// <summary>
    /// Сетевой контроллер корабля v2.0 — "Живые Баржи"
    /// Smooth movement с Mathf.SmoothDamp для frame-rate независимого сглаживания.
    /// Кооп-пилотирование: несколько игроков могут управлять одновременно.
    /// Ввод суммируется на сервере. NetworkTransform(ServerAuthority) реплицирует всем.
    /// 
    /// Сессия 1: Core Smooth Movement — плавные баржи, не истребители.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public class ShipController : NetworkBehaviour
    {
        [Header("Тяга")]
        [SerializeField] private float thrustForce = 350f;
        [SerializeField] private float maxSpeed = 40f;

        [Header("Вращение")]
        [SerializeField] private float yawForce = 12f;
        [SerializeField] private float pitchForce = 20f;

        [Header("Вертикальное движение")]
        [SerializeField] private float verticalForce = 120f;

        [Header("Smooth Movement (Lerp)")]
        [Tooltip("Время сглаживания рыскания (курсового поворота)")]
        [SerializeField] private float yawSmoothTime = 0.6f;
        [Tooltip("Время сглаживания тангажа (носа вверх/вниз)")]
        [SerializeField] private float pitchSmoothTime = 0.7f;
        [Tooltip("Время сглаживания лифта (вертикального движения)")]
        [SerializeField] private float liftSmoothTime = 1.0f;
        [Tooltip("Время разгона/торможения тяги")]
        [SerializeField] private float thrustSmoothTime = 0.3f;
        [Tooltip("Время затухания рыскания без ввода (инерция баржи)")]
        [SerializeField] private float yawDecayTime = 1.0f;
        [Tooltip("Время затухания тангажа без ввода")]
        [SerializeField] private float pitchDecayTime = 0.8f;

        [Header("Антигравитация")]
        [SerializeField] [Range(0f, 1.5f)] private float antiGravity = 1f;

        [Header("Аэродинамика")]
        [SerializeField] private float linearDrag = 0.4f;
        [SerializeField] private float angularDrag = 3.5f;

        [Header("Стабилизация")]
        [Tooltip("Сила стабилизации тангажа (возврат к горизонту)")]
        [SerializeField] private float pitchStabForce = 2.5f;
        [Tooltip("Сила стабилизации крена (возврат к 0)")]
        [SerializeField] private float rollStabForce = 4.0f;
        [Tooltip("Максимальный угол тангажа (±градусы)")]
        [SerializeField] private float maxPitchAngle = 20f;
        [Tooltip("Автоматическая стабилизация при отсутствии ввода")]
        [SerializeField] private bool autoStabilize = true;

        [Header("Коридор Высот (Сессия 2 — заглушка)")]
        [Tooltip("Минимальная высота полёта (м)")]
        [SerializeField] private float minAltitude = 1200f;
        [Tooltip("Максимальная высота полёта (м)")]
        [SerializeField] private float maxAltitude = 4450f;
        [Tooltip("Максимальная скорость лифта (м/с)")]
        [SerializeField] private float maxLiftSpeed = 2.5f;

        [Header("Cargo (Сессия 2)")]
        [Tooltip("Система груза корабля (влияет на скорость)")]
        [SerializeField] private ProjectC.Player.CargoSystem cargoSystem;

        // Rigidbody
        private Rigidbody _rb;

        // Список пилотов (кооп-управление)
        private HashSet<ulong> _pilots = new HashSet<ulong>();

        // Накопленный ввод от всех пилотов (сервер)
        private float _sumThrust, _sumYaw, _sumPitch, _sumVertical;
        private int _boostCount, _inputCount;

        // Smooth state — текущие сглаженные значения (сохраняются между кадрами)
        private float _currentYawRate;
        private float _currentPitchRate;
        private float _currentLiftForce;
        private float _currentThrust;

        // Velocity tracking для SmoothDamp (ref параметры)
        private float _yawVelocitySmooth;
        private float _pitchVelocitySmooth;
        private float _liftVelocitySmooth;
        private float _thrustVelocitySmooth;

        // Таймер отсутствия ввода (для стабилизации)
        private float _noInputTimer = 0f;

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

            float dt = Time.fixedDeltaTime;

            // 1. Усредняем ввод от всех пилотов
            int n = Mathf.Max(1, _inputCount);
            float avgThrust = _sumThrust / n;
            float avgYaw = _sumYaw / n;
            float avgPitch = _sumPitch / n;
            float avgVertical = _sumVertical / n;
            bool anyBoost = _boostCount > 0;

            // 2. Smooth thrust ramp-up (0.3s)
            float targetThrust = avgThrust * thrustForce * (anyBoost ? 2f : 1f);
            _currentThrust = Mathf.SmoothDamp(_currentThrust, targetThrust, ref _thrustVelocitySmooth, thrustSmoothTime);

            // 3. Smooth yaw с затуханием (0.6s smooth, 1.0s decay)
            float targetYawRate = avgYaw * yawForce;
            bool hasYawInput = Mathf.Abs(avgYaw) > 0.01f;
            _currentYawRate = hasYawInput
                ? Mathf.SmoothDamp(_currentYawRate, targetYawRate, ref _yawVelocitySmooth, yawSmoothTime)
                : Mathf.SmoothDamp(_currentYawRate, 0f, ref _yawVelocitySmooth, yawDecayTime);

            // 4. Smooth pitch с затуханием (0.7s smooth, 0.8s decay)
            float targetPitchRate = avgPitch * pitchForce;
            bool hasPitchInput = Mathf.Abs(avgPitch) > 0.01f;
            _currentPitchRate = hasPitchInput
                ? Mathf.SmoothDamp(_currentPitchRate, targetPitchRate, ref _pitchVelocitySmooth, pitchSmoothTime)
                : Mathf.SmoothDamp(_currentPitchRate, 0f, ref _pitchVelocitySmooth, pitchDecayTime);

            // 5. Smooth lift (очень медленно, 1.0s)
            float targetLift = avgVertical * verticalForce;
            _currentLiftForce = Mathf.SmoothDamp(_currentLiftForce, targetLift, ref _liftVelocitySmooth, liftSmoothTime);
            // Clamp к максимальной скорости лифта
            float maxLiftForce = maxLiftSpeed * _rb.mass * Mathf.Abs(Physics.gravity.y);
            _currentLiftForce = Mathf.Clamp(_currentLiftForce, -maxLiftForce, maxLiftForce);

            // 6. Обновляем таймер отсутствия ввода
            bool hasAnyInput = Mathf.Abs(avgThrust) > 0.01f || hasYawInput || hasPitchInput || Mathf.Abs(avgVertical) > 0.01f;
            if (hasAnyInput)
            {
                _noInputTimer = 0f;
            }
            else
            {
                _noInputTimer += dt;
            }

            // 7. Применяем силы
            ApplyThrustForce(_currentThrust);
            ApplyAntiGravity();
            ApplyLiftForce(_currentLiftForce);
            ApplyRotation(_currentYawRate, _currentPitchRate);

            // 8. Стабилизация (если нет ввода 0.5s+)
            if (autoStabilize && _noInputTimer > 0.5f)
                ApplyStabilization();

            // 9. Ограничение скорости
            ClampVelocity();

            // 10. Ограничение угла тангажа
            ClampPitchAngle();

            // 11. Валидация высоты (заглушка для Сессии 2)
            ValidateAltitude();

            // 12. Сброс буфера ввода
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

        private void ApplyThrustForce(float currentThrust)
        {
            if (Mathf.Abs(currentThrust) < 0.01f) return;

            // Применяем штраф скорости от груза (Сессия 2)
            float cargoPenalty = 1.0f;
            if (cargoSystem != null)
            {
                cargoPenalty = cargoSystem.GetSpeedPenalty();
            }

            _rb.AddForce(transform.forward * currentThrust * cargoPenalty, ForceMode.Force);
        }

        private void ApplyAntiGravity()
        {
            if (antiGravity <= 0f) return;
            float gravityCompensation = _rb.mass * Mathf.Abs(Physics.gravity.y) * antiGravity;
            _rb.AddForce(Vector3.up * gravityCompensation, ForceMode.Force);
        }

        private void ApplyLiftForce(float currentLiftForce)
        {
            if (Mathf.Abs(currentLiftForce) < 0.01f) return;
            _rb.AddForce(Vector3.up * currentLiftForce, ForceMode.Force);
        }

        private void ApplyRotation(float yawRate, float pitchRate)
        {
            if (Mathf.Abs(yawRate) > 0.01f)
                _rb.AddTorque(Vector3.up * yawRate, ForceMode.Force);

            if (Mathf.Abs(pitchRate) > 0.01f)
                _rb.AddTorque(transform.right * -pitchRate, ForceMode.Force);
        }

        private void ApplyStabilization()
        {
            // Получаем текущие углы в диапазоне [-180, 180]
            float currentPitch = GetNormalizedPitch();
            float currentRoll = GetNormalizedRoll();

            // Ошибка (отклонение от горизонта)
            float pitchError = currentPitch;  // desired = 0
            float rollError = currentRoll;    // desired = 0

            // PI-подобный контроллер: возврат к горизонту за ~3с
            Vector3 stabilizationTorque = new Vector3(
                -pitchError * pitchStabForce,   // pitch к 0
                0f,                              // yaw НЕ стабилизируется
                -rollError * rollStabForce      // roll к 0
            );

            _rb.AddTorque(stabilizationTorque, ForceMode.Force);
        }

        /// <summary>
        /// Получить угол тангажа в диапазоне [-180, 180]
        /// </summary>
        private float GetNormalizedPitch()
        {
            float angle = transform.eulerAngles.x;
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        /// <summary>
        /// Получить угол крена в диапазоне [-180, 180]
        /// </summary>
        private float GetNormalizedRoll()
        {
            float angle = transform.eulerAngles.z;
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        private bool HasNoInput(float t, float y, float p, float v) =>
            Mathf.Abs(t) < 0.01f && Mathf.Abs(y) < 0.01f && Mathf.Abs(p) < 0.01f && Mathf.Abs(v) < 0.01f;

        private void ClampVelocity()
        {
            if (_rb.linearVelocity.magnitude > maxSpeed)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
        }

        /// <summary>
        /// Ограничить угол тангажа ±maxPitchAngle
        /// </summary>
        private void ClampPitchAngle()
        {
            float currentPitch = GetNormalizedPitch();

            if (currentPitch > maxPitchAngle)
            {
                // "Отталкиваем" нос вниз
                float correction = (currentPitch - maxPitchAngle) * 10f;
                _rb.AddTorque(transform.right * correction, ForceMode.Force);
            }
            else if (currentPitch < -maxPitchAngle)
            {
                float correction = (currentPitch + maxPitchAngle) * 10f;
                _rb.AddTorque(transform.right * correction, ForceMode.Force);
            }
        }

        /// <summary>
        /// Валидация высоты (заглушка для Сессии 2)
        /// </summary>
        private void ValidateAltitude()
        {
            float currentAlt = transform.position.y;

            // Заглушка: логируем предупреждения (в будущем — UI warnings)
            if (currentAlt < minAltitude + 100f)
            {
                // Warning: приближение к нижней границе
                // TODO: Show UI warning
            }
            if (currentAlt < minAltitude)
            {
                // Alert: зона Завесы! Турбулентность!
                // TODO: Apply turbulence, show SOL notification
            }
            if (currentAlt > maxAltitude - 100f)
            {
                // Warning: приближение к верхней границе
                // TODO: Show UI warning
            }
            if (currentAlt > maxAltitude + 200f)
            {
                // Alert: критическая высота!
                // TODO: Apply system degradation
            }
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
        }
    }
}
