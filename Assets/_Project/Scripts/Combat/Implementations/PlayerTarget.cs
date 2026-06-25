// Project C: Real-Time Combat Engine — T-RTC02
// PlayerTarget: реализация IDamageTarget для NetworkPlayer (server-side authoritative).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.2.
//
// HP реплицируется через NetworkVariable<int>. ApplyDamage — server-only.
// armorDefense = 0 в MVP (T-CB06 ещё не сделан, поле armorDefense в ClothingItemData отсутствует).
//
// TODO (post T-CB06): заменить GetArmorDefense() на реальный подсчёт суммы armorDefense
// из экипированной ClothingItemData (Head+Chest+Legs+Feet+Back).

using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;
using ProjectC.Equipment;
using ProjectC.Items;

namespace ProjectC.Combat
{
    public class PlayerTarget : NetworkBehaviour, IDamageTarget
    {
        [SerializeField] private NetworkVariable<int> _currentHp = new NetworkVariable<int>(20);
        [SerializeField] private NetworkVariable<int> _maxHp = new NetworkVariable<int>(20);

        private ulong _clientId;

        public ulong GetTargetId() => _clientId;
        public ulong ClientId => _clientId;

        public void Initialize(ulong clientId)
        {
            _clientId = clientId;
        }

        /// <summary>
        /// T-RTC06: Self-register в CombatServer при NetworkSpawn (server-side only).
        /// Решает race condition: NetworkPlayer.OnNetworkSpawn может сработать РАНЬШЕ
        /// CombatServer.OnNetworkSpawn. Push-down в CombatServer.OnNetworkSpawn страхует.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            if (_clientId != 0 && CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterTarget(_clientId, this);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            if (CombatServer.Instance != null && _clientId != 0)
            {
                CombatServer.Instance.UnregisterTarget(_clientId);
            }
        }

        public Vector3 GetPosition() => transform.position;
        public int GetCurrentHp() => _currentHp.Value;
        public int GetMaxHp() => _maxHp.Value;

        public int GetArmorDefense()
        {
            // TODO (post T-CB06): sum armorDefense из экипированной ClothingItemData.
            // Сейчас поля armorDefense в ClothingItemData ещё нет (T-CB06).
            // Возвращаем 0 → в MVP защиты нет, NPC получают полный урон.
            return 0;
        }

        public bool IsAlive() => _currentHp.Value > 0;
        public bool IsPlayer() => true;
        public string GetDisplayName() => $"Player {_clientId}";

        public void ApplyDamage(DamageResult result, ulong attackerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[PlayerTarget] ApplyDamage called on non-server. client={_clientId}, attacker={attackerClientId}");
                return;
            }
            if (!result.isHit) return;
            if (_currentHp.Value <= 0) return;  // already dead

            int newHp = Mathf.Max(0, _currentHp.Value - result.finalDamage);
            _currentHp.Value = newHp;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[PlayerTarget] client={_clientId} took {result.finalDamage} from attacker={attackerClientId} (HP {_currentHp.Value + result.finalDamage} → {newHp}, isCrit={result.isCrit}, type={result.damageType})");
            }
        }
    }
}
