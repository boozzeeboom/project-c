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

        /// <summary>
        /// T-RTC06 (v0.1 fix): Self-register в CombatServer при NetworkSpawn (server-side only).
        /// Также: если <c>Initialize</c> не был вызван (например, для scene-placed NPC, созданных
        /// вручную через Edit Mode без explicit Initialize call), инициализируем HP из <c>_data</c>.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            // Fallback init: если _targetId == 0 → Initialize не вызывался → init HP из _data
            if (_targetId == 0 && _data != null)
            {
                int hp = _maxHpOverride > 0 ? _maxHpOverride : _data.maxHp;
                _maxHp.Value = hp;
                _currentHp.Value = hp;
                _targetId = NetworkObjectId;  // используем реальный NetworkObjectId
                if (Debug.isDebugBuild) Debug.Log($"[NpcTarget] OnNetworkSpawn fallback-init: name={gameObject.name}, targetId={_targetId}, HP={hp}");
            }

            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterTarget(GetTargetId(), this);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.UnregisterTarget(GetTargetId());
            }
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

            // T-NPC-01 v0.2: при смерти — death animation + loot spawn + 3s corpse delay.
            if (newHp == 0)
            {
                if (Debug.isDebugBuild) Debug.Log($"[NpcTarget] npc={_targetId} killed. Spawning loot + Destroy in 3s.");
                OnKilled(attackerClientId);
                Destroy(gameObject, 3.0f);
            }
        }

        /// <summary>
        /// T-NPC-01 v0.2: death handler — death animation trigger + spawn NpcLootPickup (credits).
        /// </summary>
        private void OnKilled(ulong attackerClientId)
        {
            // Trigger Death animation (на child Animator, если есть).
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                // Безопасный вызов: проверяем что параметр существует.
                foreach (var p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Death")
                    {
                        animator.SetTrigger("Death");
                        break;
                    }
                }
            }

            // T-NPC-03: Spawn NpcLootPickup на месте смерти с credits из NpcCombatData.
            SpawnLootPickup(attackerClientId);
        }

        /// <summary>
        /// T-NPC-03 + T-NPC-04: server-only spawn NpcLootPickup на текущей позиции NPC.
        /// Использует NpcLootPickup prefab (если зарегистрирован) иначе — спавнит programmatic.
        /// </summary>
        private void SpawnLootPickup(ulong attackerClientId)
        {
            // Credits из _data (placeholder: фиксированный value до T-NPC-04 LootTable extension).
            int credits = 0;
            if (_data != null)
            {
                // До T-NPC-04: простой fixed credits based on NPC maxHp (scaling).
                // T-NPC-04: заменить на _data.loot.GenerateCredits().
                credits = Mathf.Max(5, _data.maxHp / 4);  // HP=20 → 5 CR; HP=100 → 25 CR
            }

            if (credits <= 0) return;

            // Программное создание NpcLootPickup (без prefab asset для MVP).
            var loot = new GameObject($"Loot_{_data?.displayName ?? "NPC"}_CR{credits}");
            loot.transform.position = transform.position + Vector3.up * 0.5f;  // поднимаем над землёй

            var netObj = loot.AddComponent<Unity.Netcode.NetworkObject>();
            var pickup = loot.AddComponent<ProjectC.AI.NpcLootPickup>();
            pickup.creditsAmount = credits;
            pickup.displayName = _data != null ? $"{_data.displayName} Loot" : "Loot";
            pickup.interactionRadius = 2.0f;
            pickup.autoDespawnSeconds = 120f;  // 2 мин

            // Visual: small yellow sphere (placeholder — gold coins vibe).
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "VisualMarker";
            visual.transform.SetParent(loot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            var col = visual.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            var mr = visual.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(1f, 0.85f, 0.2f, 1f);  // gold
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
            mr.sharedMaterial = mat;

            netObj.Spawn(destroyWithScene: true);
        }
    }
}
