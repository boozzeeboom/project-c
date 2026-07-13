using Unity.Netcode;
using UnityEngine;
using ProjectC.World;

namespace ProjectC.Player
{
    /// <summary>
    /// Отслеживает состояние игрока для респавна.
    /// Должен быть на том же GameObject что и NetworkPlayer/CharacterController.
    ///
    /// Server-authoritative: проверка Y < 0 и телепорт на сервере.
    /// Клиент получает позицию через ClientRpc.
    ///
    /// OnTriggerEnter: смена активной точки респавна при входе в триггер-зону.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerRespawnTracker : NetworkBehaviour
    {
        [Header("Fall Threshold")]
        [Tooltip("Y-координата, ниже которой считается что игрок упал и нужен респавн.")]
        [SerializeField] private float _deathY = 0f;

        [Tooltip("Задержка в секундах перед респавном после обнаружения падения.")]
        [SerializeField] private float _respawnDelay = 0.5f;

        [Header("Debug")]
        [Tooltip("Логировать события респавна.")]
        [SerializeField] private bool _debugLog = true;

        // Состояние
        private CharacterController _controller;
        private RespawnManager _respawnManager;
        private int _currentRespawnIndex = -1;
        private float _fallStartTime = float.MaxValue;
        private bool _isRespawning;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            // Находим RespawnManager (должен быть в BootstrapScene)
            _respawnManager = FindAnyObjectByType<RespawnManager>();
            if (_respawnManager == null)
            {
                Debug.LogWarning("[PlayerRespawnTracker] RespawnManager not found in scene. Respawn will not work.");
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _currentRespawnIndex = -1;
            _fallStartTime = float.MaxValue;
            _isRespawning = false;
        }

        private void Update()
        {
            // Только сервер проверяет падение
            if (!IsServer) return;
            if (_isRespawning) return;
            if (_respawnManager == null) return;

            float y = transform.position.y;

            if (y <= _deathY)
            {
                if (_fallStartTime > Time.time)
                {
                    _fallStartTime = Time.time;
                }

                if (Time.time - _fallStartTime >= _respawnDelay)
                {
                    PerformRespawn();
                }
            }
            else
            {
                // Сброс таймера если игрок выше порога
                _fallStartTime = float.MaxValue;
            }
        }

        private void PerformRespawn()
        {
            if (_isRespawning) return;
            _isRespawning = true;
            _fallStartTime = float.MaxValue;

            // Если точка не назначена — используем fallback (индекс 0)
            int index = _currentRespawnIndex >= 0 ? _currentRespawnIndex : 0;
            Vector3 targetPos = _respawnManager.GetEffectivePosition(index);

            if (_debugLog)
            {
                Debug.Log($"[PlayerRespawnTracker] Respawning player {OwnerClientId} to index={index} pos={targetPos}");
            }

            TeleportToClientRpc(targetPos);
        }

        [ClientRpc]
        private void TeleportToClientRpc(Vector3 targetPosition)
        {
            // Отключаем CharacterController чтобы избежать конфликта с ручной установкой позиции
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            transform.position = targetPosition;

            if (_controller != null)
            {
                _controller.enabled = true;
            }

            // Сбрасываем скорость (чтобы не продолжить падение)
            var networkPlayer = GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.ResetVelocity();
            }

            Physics.SyncTransforms();

            _isRespawning = false;

            if (_debugLog && IsOwner)
            {
                Debug.Log($"[PlayerRespawnTracker] Client teleported to {targetPosition}");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _isRespawning = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_respawnManager == null) return;

            int index = _respawnManager.FindRespawnIndex(other);
            if (index >= 0)
            {
                _currentRespawnIndex = index;
                if (_debugLog && (IsOwner || IsServer))
                {
                    Debug.Log($"[PlayerRespawnTracker] Respawn point set to index={index} via trigger '{other.name}'");
                }
            }
        }

        /// <summary>
        /// Публичный метод для принудительной установки индекса респавна из кода.
        /// </summary>
        public void SetRespawnIndex(RespawnManager manager, int index)
        {
            if (manager != null) _respawnManager = manager;
            _currentRespawnIndex = index;
        }

        /// <summary>
        /// Публичный ServerRpc для ручного запроса респавна.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestRespawnServerRpc()
        {
            PerformRespawn();
        }
    }
}
