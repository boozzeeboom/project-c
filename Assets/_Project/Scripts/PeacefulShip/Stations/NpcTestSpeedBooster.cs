// NpcTestSpeedBooster — DEBUG ONLY. Multiplies NPC ship physics speed by 10× for testing.
// Add this component to any NPC ship GameObject to make it 10× faster in tests.
// REMOVE before production.

using UnityEngine;
using ProjectC.Player;

namespace ProjectC.PeacefulShip.Stations
{
    /// <summary>
    /// Multiplies rigidbody drag/thrust scaling for NPC ships — test accelerator.
    /// 10× faster response time.
    /// </summary>
    public class NpcTestSpeedBooster : MonoBehaviour
    {
        [Tooltip("Скорость ×10 для тестов")]
        public float speedMultiplier = 10f;

        private ShipController _ship;
        private Rigidbody _rb;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _ship = GetComponent<ShipController>();
        }

        void FixedUpdate()
        {
            if (_rb == null) return;
            // Lower linear drag for less slowdown
            _rb.linearDamping = 0.05f;
            // Apply velocity scaling if very slow
            if (_rb.linearVelocity.magnitude < 5f)
            {
                _rb.AddForce(_ship != null ? _ship.transform.forward * 50000f * speedMultiplier : transform.forward * 50000f * speedMultiplier, ForceMode.Force);
            }
        }
    }
}