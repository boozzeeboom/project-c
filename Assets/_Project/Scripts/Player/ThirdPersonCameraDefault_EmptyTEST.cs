using UnityEngine;

namespace ProjectC.Player
{
    /// <summary>
    /// T-JITTER: Minimal default third-person camera for diagnostic testing.
    /// No smoothing, no spring arm, no collision — hard-follow to isolate jitter source.
    /// </summary>
    public class ThirdPersonCameraDefault_EmptyTEST : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2f, -5f);

        private void LateUpdate()
        {
            if (_target == null) return;
            transform.position = _target.position + _target.rotation * _offset;
            transform.rotation = _target.rotation;
        }

        public void SetTarget(Transform target) => _target = target;
    }
}
