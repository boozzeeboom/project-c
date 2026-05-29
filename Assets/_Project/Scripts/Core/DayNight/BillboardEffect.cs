using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Makes the attached GameObject always face the main camera.
    /// Used for billboard effects like stars in the sky.
    /// </summary>
    public class BillboardEffect : MonoBehaviour
    {
        [Header("Billboard Settings")]
        [SerializeField] private bool _useInitialRotation = true;
        [SerializeField] private bool _rotateX = true;
        [SerializeField] private bool _rotateY = true;
        [SerializeField] private bool _rotateZ = true;
        
        private Camera _mainCamera;
        private Quaternion _initialRotation;
        private bool _hasInitialRotation;

        private void Start()
        {
            _mainCamera = Camera.main;
            
            if (_useInitialRotation)
            {
                _initialRotation = transform.rotation;
                _hasInitialRotation = true;
            }
        }

        private void LateUpdate()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Make this transform face the camera
            if (_hasInitialRotation)
            {
                // Combine initial rotation with billboard rotation
                Vector3 forward = _mainCamera.transform.forward;
                Vector3 initialForward = _initialRotation * Vector3.forward;
                
                if (!_rotateX) forward.x = initialForward.x;
                if (!_rotateY) forward.y = initialForward.y;
                if (!_rotateZ) forward.z = initialForward.z;
                
                transform.rotation = Quaternion.LookRotation(forward);
            }
            else
            {
                transform.rotation = _mainCamera.transform.rotation;
            }
        }
    }
}