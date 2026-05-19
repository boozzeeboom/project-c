using UnityEngine;

namespace ProjectC.Core
{
    public class CloudSpherePhysics : MonoBehaviour
    {
        public float Radius = 10f;
        public float PartingStrength = 50f;
        public float PartingDistance = 30f;
        public bool SpringBack = true;
        public float SpringK = 8f;
        public float Damping = 0.92f;

        private Rigidbody _rb;
        private Vector3 _basePosition;
        private Vector3 _displacement;
        private bool _isParting;

        public void Initialize(float radius)
        {
            Radius = radius;
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
            }
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.linearDamping = 2f;
            _rb.angularDamping = 2f;
            _basePosition = transform.position;
        }

        public void ApplyParting(Vector3 fromDirection)
        {
            if (!_isParting)
            {
                _isParting = true;
                _rb.isKinematic = false;
            }

            Vector3 dir = (transform.position - fromDirection).normalized;
            _rb.AddForce(dir * PartingStrength, ForceMode.Impulse);
        }

        private void Update()
        {
            Transform player = FindLocalPlayer();
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist < PartingDistance)
                {
                    ApplyParting(player.position);
                }
            }

            if (_isParting && SpringBack)
            {
                _displacement = transform.position - _basePosition;
                if (_displacement.magnitude > 0.1f)
                {
                    Vector3 springForce = -_displacement * SpringK;
                    _rb.AddForce(springForce, ForceMode.Force);
                }

                _rb.linearDamping = Damping * 10f;
                _rb.angularDamping = Damping * 10f;
            }
        }

        private Transform FindLocalPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform : null;
        }
    }
}