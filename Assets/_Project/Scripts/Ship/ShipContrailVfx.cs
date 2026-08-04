// ShipContrailVfx.cs — Phase 2.3
// Drives the VFX Graph condensation trail behind the ship.
// Uses Play/Stop for emission control (Simple_Trail template).
// VFX GameObject is moved to trail position each frame.

using UnityEngine;
using UnityEngine.VFX;
using ProjectC.Core;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// Управляет VFX Graph конденсационного следа за кораблём.
    /// Использует Play()/Stop() для контроля эмиссии и двигает
    /// VFX-GameObject за кораблём.
    /// </summary>
    public class ShipContrailVfx : MonoBehaviour
    {
        [Header("VFX")]
        [Tooltip("VisualEffect компонент с Contrail.vfx.")]
        public VisualEffect Vfx;

        [Header("Ship")]
        [Tooltip("ShipController (опционально). Если null — используется transform + Rigidbody этого объекта.")]
        public ShipController Ship;

        [Header("Emit Conditions")]
        [Range(0f, 30f)] public float MinSpeed = 5f;

        [Header("Trail Offset")]
        [Tooltip("Смещение точки спавна относительно корабля (локальное). Z назад, Y вверх.")]
        public Vector3 SpawnOffset = new Vector3(0f, -2f, -15f);

        private Rigidbody _rb;
        private bool _wasEmitting;

        private void Start()
        {
            if (Vfx == null)
                Vfx = GetComponent<VisualEffect>();

            if (Ship == null)
                Ship = GetComponentInParent<ShipController>();

            _rb = Ship != null ? Ship.GetComponent<Rigidbody>() : GetComponent<Rigidbody>();

            if (Vfx != null)
                Vfx.Stop();
        }

        private void Update()
        {
            if (Vfx == null) return;

            // Determine emission state
            float speed = 0f;
            if (_rb != null) speed = _rb.linearVelocity.magnitude;
            bool docked = Ship != null && Ship.IsDocked;
            bool shouldEmit = !docked && speed > MinSpeed;

            // Play/Stop control
            if (shouldEmit && !_wasEmitting)
            {
                Vfx.Play();
                _wasEmitting = true;
            }
            else if (!shouldEmit && _wasEmitting)
            {
                Vfx.Stop();
                _wasEmitting = false;
            }

            // Move VFX GameObject to trail position
            if (shouldEmit)
            {
                Vector3 shipPos = Ship != null ? Ship.transform.position : transform.position;
                Quaternion shipRot = Ship != null ? Ship.transform.rotation : transform.rotation;
                Vfx.transform.position = shipPos + shipRot * SpawnOffset;
                Vfx.transform.rotation = shipRot;
            }
        }

        private void OnDisable()
        {
            if (Vfx != null)
                Vfx.Stop();
            _wasEmitting = false;
        }
    }
}
