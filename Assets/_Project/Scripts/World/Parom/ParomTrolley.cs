using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Parom
{
    /// <summary>
    /// T-PAROM-01: кабинка парома. Сидит на корне инстанса тележки (префаб или fallback).
    /// - Гарантирует кинематический Rigidbody + коллайдер пола, чтобы стандартный
    ///   moving-platform carry (NetworkPlayer / NpcBrain / PickupDeckRide) вез райдера.
    ///   Дисциплина проекта: перенос только translation + yaw (см. PlatformRideHelper).
    /// - Крутит винты (дети с именем *Prop* / *Винт* / *Rotor*) — визуал лопастных двигателей.
    /// - Держатель Driver Anchor — место под модель машиниста (логики нет, v1 — визуал).
    /// Двигает кабинку ParomRoute (позиция/поворот каждый кадр); этот компонент
    /// трансформ НЕ трогает, только физику и винты.
    /// </summary>
    [DisallowMultipleComponent]
    public class ParomTrolley : MonoBehaviour
    {
        public enum SpinAxis { X, Y, Z }

        [Header("Винты (лопастные двигатели)")]
        [Tooltip("Ось вращения винтов в локальных координатах винта.")]
        [SerializeField] private SpinAxis _propellerAxis = SpinAxis.Y;

        [Tooltip("Скорость вращения винтов (об/мин). 0 = стоят.")]
        [Min(0f)] [SerializeField] private float _propellerRpm = 240f;

        [Header("Машинист")]
        [Tooltip("Якорь под модель машиниста (необязательно; логики нет, v1 — визуал).")]
        [SerializeField] private Transform _driverAnchor;

        [Header("Кинематика (T-PAROM-05, анти-баунс райдера)")]
        [Tooltip("Интерполяция Rigidbody между физшагами — убирает ступеньку 50 Гц на рендере.")]
        [SerializeField] private bool _interpolateRigidbody = true;

        // === Runtime ===
        private readonly List<Transform> _propellers = new List<Transform>();

        // T-PAROM-05: цель движения от ParomRoute. Применяется в FixedUpdate через
        // MovePosition/MoveRotation — коллайдер не телепортируется сквозь
        // CharacterController райдера, физика резолвит контакт внутри шага.
        private bool _hasTarget;
        private Vector3 _targetPos;
        private Quaternion _targetRot;
        private bool _snapNext = true;

        /// <summary>Скорость винтов (об/мин). Можно крутить из менеджеров/ивентов.</summary>
        public float PropellerRpm
        {
            get => _propellerRpm;
            set => _propellerRpm = Mathf.Max(value, 0f);
        }

        /// <summary>Якорь машиниста (может быть null).</summary>
        public Transform DriverAnchor => _driverAnchor;

        private void Awake()
        {
            EnsurePhysics();
            CollectPropellers();
        }

        private void Update()
        {
            if (_propellers.Count == 0 || _propellerRpm <= 0f) return;
            Vector3 axis = _propellerAxis switch
            {
                SpinAxis.X => Vector3.right,
                SpinAxis.Y => Vector3.up,
                _ => Vector3.forward,
            };
            float degrees = _propellerRpm * 360f / 60f * Time.deltaTime;
            for (int i = 0; i < _propellers.Count; i++)
                if (_propellers[i] != null) _propellers[i].Rotate(axis, degrees, Space.Self);
        }

        /// <summary>
        /// T-PAROM-05: цель движения от ParomRoute. Трансформ здесь НЕ трогаем —
        /// движение применяется в <see cref="FixedUpdate"/> через MovePosition/MoveRotation.
        /// Первый вызов после появления — снап (кабинка не должна «доехать» с origin).
        /// </summary>
        public void SetKinematicTarget(Vector3 pos, Quaternion rot)
        {
            _targetPos = pos;
            _targetRot = rot;
            _hasTarget = true;
        }

        private void FixedUpdate()
        {
            if (!_hasTarget) return;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null || !rb.isKinematic)
            {
                transform.SetPositionAndRotation(_targetPos, _targetRot);
                _snapNext = false;
                return;
            }
            if (_snapNext)
            {
                rb.position = _targetPos;
                rb.rotation = _targetRot;
                _snapNext = false;
                return;
            }
            rb.MovePosition(_targetPos);
            rb.MoveRotation(_targetRot);
        }

        /// <summary>
        /// Rigidbody нужен carry-детекту (DetectPlatform идёт через attachedRigidbody
        /// к transform платформы). Кинематика: ParomRoute задаёт цель через
        /// SetKinematicTarget, едем через MovePosition/MoveRotation в FixedUpdate.
        /// Коллайдер: если в префабе ничего нет — строим пол по bounds рендеров,
        /// чтобы на кабинку можно было встать.
        /// </summary>
        private void EnsurePhysics()
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            // T-PAROM-05: интерполяция сглаживает рендер между физшагами;
            // sleepThreshold=0 — стоящая на станции кабинка не засыпает и остаётся
            // валидной платформой для DetectGroundPlatform (IsSleeping-фильтр).
            rb.interpolation = _interpolateRigidbody
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;
            rb.sleepThreshold = 0f;

            if (GetComponentInChildren<Collider>() != null) return;

            Bounds bounds = new Bounds(transform.position, Vector3.one);
            bool hasRenderers = false;
            var renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!hasRenderers) { bounds = renderers[i].bounds; hasRenderers = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            var floor = new GameObject("Floor_Auto");
            floor.transform.SetParent(transform, false);
            var box = floor.AddComponent<BoxCollider>();
            if (hasRenderers)
            {
                Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
                Vector3 size = bounds.size;
                size.y = Mathf.Max(size.y * 0.1f, 0.15f);
                box.center = localCenter + Vector3.up * (bounds.size.y * 0.35f);
                box.size = new Vector3(Mathf.Max(size.x, 1f), size.y, Mathf.Max(size.z, 1f));
            }
            else
            {
                box.center = new Vector3(0f, -0.7f, 0f);
                box.size = new Vector3(2.6f, 0.15f, 4.2f);
            }
        }

        private void CollectPropellers()
        {
            _propellers.Clear();
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (n.Contains("prop") || n.Contains("винт") || n.Contains("rotor"))
                    _propellers.Add(all[i]);
            }
        }
    }
}
