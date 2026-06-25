// Project C: Real-Time Combat Engine — T-RTC03
// NpcTarget: реализация IDamageTarget для NPC-врага. NetworkBehaviour с NetworkVariable<int> HP.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.4.

using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;

namespace ProjectC.Combat
{
    public class NpcTarget : NetworkBehaviour, IDamageTarget
    {
        [SerializeField] private NpcCombatData _data;
        [SerializeField] private int _maxHpOverride = 0;  // если >0 — переопределяет _data.maxHp

        private NetworkVariable<int> _currentHp = new NetworkVariable<int>(30);
        private NetworkVariable<int> _maxHp = new NetworkVariable<int>(30);
        private ulong _targetId;  // = NetworkObjectId по дизайну, но у нас override

        public void Initialize(NpcCombatData data, ulong targetId)
        {
            _data = data;
            _targetId = targetId;
            int hp = _maxHpOverride > 0 ? _maxHpOverride : (data != null ? data.maxHp : 30);
            _maxHp.Value = hp;
            _currentHp.Value = hp;
        }

        public Vector3 GetPosition() => transform.position;
        public int GetCurrentHp() => _currentHp.Value;
        public int GetMaxHp() => _maxHp.Value;

        public int GetArmorDefense()
        {
            // MVP: NPC без брони. После T-CB06 + NPC-armor-design — реальный подсчёт.
            return 0;
        }

        public bool IsAlive() => _currentHp.Value > 0;
        public bool IsPlayer() => false;
        public string GetDisplayName() => _data != null ? _data.displayName : "NPC";

        public ulong GetTargetId() => _targetId != 0 ? _targetId : (NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL);

        public void ApplyDamage(DamageResult result, ulong attackerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[NpcTarget] ApplyDamage called on non-server.");
                return;
            }
            if (!result.isHit) return;
            if (_currentHp.Value <= 0) return;

            int newHp = Mathf.Max(0, _currentHp.Value - result.finalDamage);
            _currentHp.Value = newHp;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[NpcTarget] npc={_targetId} took {result.finalDamage} from attacker={attackerClientId} (HP {_currentHp.Value + result.finalDamage} → {newHp}, isCrit={result.isCrit}, type={result.damageType})");
            }
        }
    }
}
