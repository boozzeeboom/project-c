// ShipContrailVfx.cs — Phase 2.3
// Drives the VFX Graph condensation trail behind the ship.
// Reads speed from Rigidbody, wind from WindManager.
// Trail only emits when ship is undocked and above MinSpeed.

using UnityEngine;
using UnityEngine.VFX;
using ProjectC.Core;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// Управляет VFX Graph конденсационного следа за кораблём.
    /// Подписывается на ShipController (IsDocked) или работает автономно
    /// с любым Transform + Rigidbody.
    /// </summary>
    public class ShipContrailVfx : MonoBehaviour
    {
        [Header("VFX")]
        [Tooltip("VisualEffect компонент с Contrail.vfx (или ссылка на ассет в VisualEffect.visualEffectAsset).")]
        public VisualEffect Vfx;

        [Header("Ship")]
        [Tooltip("ShipController (опционально). Если null — используется transform + Rigidbody этого объекта.")]
        public ShipController Ship;

        [Header("Emit Conditions")]
        [Range(0f, 30f)] public float MinSpeed = 5f;
        [Range(0.1f, 2f)] public float EmitRate = 0.05f; // секунд между эмитами

        [Header("Trail Offset")]
        [Tooltip("Смещение точки спавна относительно корабля (локальное). Z назад, Y вверх.")]
        public Vector3 SpawnOffset = new Vector3(0f, -2f, -15f);

        private Rigidbody _rb;
        private float _emitTimer;

        // VFX property IDs
        private static readonly int EmitId     = Shader.PropertyToID("Emit");
        private static readonly int SpawnPosId = Shader.PropertyToID("SpawnPos");
        private static readonly int WindVecId  = Shader.PropertyToID("WindVector");

        private void Start()
        {
            if (Vfx == null)
                Vfx = GetComponent<VisualEffect>();

            if (Ship == null)
                Ship = GetComponent<ShipController>();

            _rb = Ship != null ? Ship.GetComponent<Rigidbody>() : GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (Vfx == null) return;

            // Determine emission state
            float speed = 0f;
            if (_rb != null) speed = _rb.linearVelocity.magnitude;
            bool docked = Ship != null && Ship.IsDocked;
            bool shouldEmit = !docked && speed > MinSpeed;

            Vfx.SetBool(EmitId, shouldEmit);

            if (!shouldEmit) return;

            // Throttled spawn
            _emitTimer += Time.deltaTime;
            if (_emitTimer < EmitRate) return;
            _emitTimer = 0f;

            Vector3 shipPos = Ship != null ? Ship.transform.position : transform.position;
            Quaternion shipRot = Ship != null ? Ship.transform.rotation : transform.rotation;

            Vector3 spawnWorld = shipPos + shipRot * SpawnOffset;
            Vfx.SetVector3(SpawnPosId, spawnWorld);

            Vector3 windVec = Vector3.zero;
            if (WindManager.Instance != null)
                windVec = WindManager.Instance.CurrentWindDirection.normalized * WindManager.Instance.CurrentWindSpeed;
            Vfx.SetVector3(WindVecId, windVec);
        }
    }
}
