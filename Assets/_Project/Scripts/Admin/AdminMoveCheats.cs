using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Player;

namespace ProjectC.Admin
{
    /// <summary>
    /// T-ADM-02: читы движения локального игрока (D3).
    /// Висит на объекте игрока, создаётся AdminFacade.EnsureMoveCheats().
    /// God: флаг в PlayerTarget.GodModeClientIds (серверный, host-only до L4).
    /// Noclip: гасит CharacterController + PlayerController + коллайдеры иерархии,
    /// движение — напрямую по трансформу (WASD + E/Q вверх/вниз, Shift = быстро).
    /// Speed: множитель PlayerController.SpeedMultiplier (T-ADM-05).
    /// Мировых координат между кадрами не хранит (FO-безопасно).
    /// </summary>
    public class AdminMoveCheats : MonoBehaviour
    {
        [Header("Админ-читы (D3)")]
        [SerializeField] private bool _godMode;
        [SerializeField] private bool _noclip;
        [SerializeField] private float _speedMult = 1f;

        private NetworkPlayer _player;
        private PlayerController _playerController;
        private CharacterController _characterController;
        private readonly List<Collider> _cachedColliders = new List<Collider>();
        private bool _collidersCached;

        public bool GodMode => _godMode;
        public bool Noclip => _noclip;
        public float SpeedMult => _speedMult;

        private void Awake()
        {
            _player = GetComponent<NetworkPlayer>();
            _playerController = GetComponent<PlayerController>();
            _characterController = GetComponent<CharacterController>();
        }

        /// <summary>God on/off. В host-сессии давит урон на сервере, в чистом клиенте — нет (см. L1_DESIGN §6).</summary>
        public void SetGod(bool enabled)
        {
            _godMode = enabled;
            ulong clientId = ResolveClientId();
            if (enabled)
            {
                if (clientId != 0) ProjectC.Combat.PlayerTarget.GodModeClientIds.Add(clientId);
                Debug.Log($"[AdminMoveCheats] GOD ON (client={clientId})");
            }
            else
            {
                if (clientId != 0) ProjectC.Combat.PlayerTarget.GodModeClientIds.Remove(clientId);
                Debug.Log($"[AdminMoveCheats] GOD OFF (client={clientId})");
            }
        }

        /// <summary>Noclip on/off: сквозь объекты + свободный полёт на WASD/E/Q.</summary>
        public void SetNoclip(bool enabled)
        {
            if (_noclip == enabled) return;
            _noclip = enabled;
            if (enabled)
            {
                CacheCollidersOnce();
                for (int i = 0; i < _cachedColliders.Count; i++)
                    if (_cachedColliders[i] != null) _cachedColliders[i].enabled = false;
                if (_characterController != null) _characterController.enabled = false;
                if (_playerController != null) _playerController.enabled = false;
                Debug.Log("[AdminMoveCheats] NOCLIP ON (WASD + E/Q, Shift = быстро)");
            }
            else
            {
                for (int i = 0; i < _cachedColliders.Count; i++)
                    if (_cachedColliders[i] != null) _cachedColliders[i].enabled = true;
                if (_characterController != null) _characterController.enabled = true;
                if (_playerController != null) _playerController.enabled = true;
                Physics.SyncTransforms();
                Debug.Log("[AdminMoveCheats] NOCLIP OFF");
            }
        }

        /// <summary>Множитель скорости бега (×1/×2/×5/×10).</summary>
        public void SetSpeedMult(float mult)
        {
            _speedMult = Mathf.Max(0.1f, mult);
            if (_playerController != null) _playerController.SpeedMultiplier = _speedMult;
            Debug.Log($"[AdminMoveCheats] Speed x{_speedMult}");
        }

        private void Update()
        {
            if (!_noclip) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 move = Vector3.zero;
            Vector3 fwd = cam.transform.forward;
            Vector3 right = cam.transform.right;
            if (kb.wKey.isPressed) move += fwd;
            if (kb.sKey.isPressed) move -= fwd;
            if (kb.dKey.isPressed) move += right;
            if (kb.aKey.isPressed) move -= right;
            if (kb.eKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed) move -= Vector3.up;
            if (move.sqrMagnitude < 0.0001f) return;

            move.Normalize();
            bool fast = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            float speed = 12f * _speedMult * (fast ? 4f : 1f);
            transform.position += move * speed * Time.deltaTime;
        }

        private void OnDisable()
        {
            // Страховка: не оставить игрока без коллизий при выгрузке.
            if (_noclip) SetNoclip(false);
        }

        private ulong ResolveClientId()
        {
            if (_player == null) _player = GetComponent<NetworkPlayer>();
            if (_player != null && _player.NetworkObject != null && _player.NetworkObject.IsSpawned)
                return _player.NetworkObject.OwnerClientId;
            return 0;
        }

        private void CacheCollidersOnce()
        {
            if (_collidersCached) return;
            _collidersCached = true;
            _cachedColliders.Clear();
            _cachedColliders.AddRange(GetComponentsInChildren<Collider>(includeInactive: true));
        }
    }
}
