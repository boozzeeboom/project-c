using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;

namespace ProjectC.Player
{
    /// <summary>
    /// Сетевой компонент игрока (авторитарный сервер)
    /// Клиент шлёт ввод → сервер обрабатывает через CharacterController → NetworkTransform реплицирует
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Движение")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Прыжок")]
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isGrounded;

        // NetworkObject для этого игрока
        private NetworkObject networkObject;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            networkObject = GetComponent<NetworkObject>();
            _controller = GetComponent<CharacterController>();

            if (IsOwner)
            {
                Debug.Log($"[Player] Локальный игрок spawned. OwnerClientId: {OwnerClientId}");

                // Привязываем камеру к локальному игроку
                var cam = FindAnyObjectByType<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.SetTarget(transform);
                    Debug.Log("[Player] Камера привязана к локальному игроку");
                }
            }
            else
            {
                Debug.Log($"[Player] Удалённый игрок spawned. OwnerClientId: {OwnerClientId}");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            Debug.Log($"[Player] Игрок despawned. OwnerClientId: {OwnerClientId}");
        }

        private void Update()
        {
            // Только локальный игрок обрабатывает ввод
            if (!IsOwner) return;

            Vector2 moveInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;

            bool jump = Keyboard.current.spaceKey.wasPressedThisFrame;
            bool run = Keyboard.current.leftShiftKey.isPressed;

            SubmitMovementRpc(moveInput, jump, run);
        }

        /// <summary>
        /// Сервер обрабатывает движение (авторитарный сервер)
        /// </summary>
        [Rpc(SendTo.Server)]
        private void SubmitMovementRpc(Vector2 moveInput, bool jump, bool run, RpcParams rpcParams = default)
        {
            _isGrounded = _controller.isGrounded;

            if (_isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            var cam = FindAnyObjectByType<ThirdPersonCamera>();
            Vector3 forward = cam != null ? cam.CameraForward : Vector3.forward;
            Vector3 right = cam != null ? cam.CameraRight : Vector3.right;

            Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
            bool hasInput = moveDirection.magnitude > 0.01f;

            if (hasInput)
            {
                moveDirection.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                float currentSpeed = run ? runSpeed : walkSpeed;
                _controller.Move(moveDirection * currentSpeed * Time.deltaTime);
            }

            if (_isGrounded && jump)
            {
                _velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            }

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        /// <summary>
        /// Проверка: это локальный игрок?
        /// </summary>
        public new bool IsLocalPlayer => IsOwner;

        /// <summary>
        /// Получить ClientId владельца
        /// </summary>
        public ulong GetOwnerId()
        {
            return OwnerClientId;
        }
    }
}
