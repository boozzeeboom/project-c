// =====================================================================================
// ShipHull.cs — корпус корабля как IDamageTarget (server-authoritative HP)
// =====================================================================================
// NetworkBehaviour на Ship_Root рядом с ShipController.
// NetworkVariable<int> hull — сервер пишет, все читают.
// Регистрируется в CombatServer при OnNetworkSpawn (как NpcTarget).
//
// Источники урона:
//   1. Боевое оружие — через CombatServer.ResolveAttack → ApplyDamage(DamageResult)
//   2. Столкновения  — через ShipController.OnCollisionEnter → ApplyCollisionDamage(energy)
//
// При 0 HP: корабль «сломан» (IsAlive = true, не деспаунится).
//   Состояние обрабатывается в ShipController через OnHullChanged event:
//   - множитель скоростей × brokenSpeedMultiplier
//   - обнуление груза через TradeWorld
//
// IsAlive() всегда true — CombatServer не должен публиковать EntityKilledEvent
// для корабля (корабль не «труп», он сломан).
// =====================================================================================
using System;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat.Core;
using ProjectC.Combat;
using ProjectC.Player;

namespace ProjectC.Ship.Combat
{
    /// <summary>
    /// Состояние корпуса корабля.
    /// </summary>
    public enum HullState
    {
        Operational,   // HP > 0 — корабль работает нормально
        Broken         // HP = 0 — корабль сломан (скорости ×0.1, груз обнулён)
    }

    /// <summary>
    /// Корпус корабля как IDamageTarget. Server-authoritative HP через NetworkVariable.
    /// Размещается на Ship_Root рядом с ShipController.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class ShipHull : NetworkBehaviour, IDamageTarget
    {
        [Header("Ship Damage Config")]
        [Tooltip("Конфиг параметров повреждений. Если не назначен — fallback на Resources/ShipDamage.asset " +
                 "или hardcoded defaults.")]
        [SerializeField] private ShipDamageConfig _damageConfig;

        [Header("Debug")]
        [Tooltip("Включить подробные логи в консоль.")]
        [SerializeField] private bool _debugLog = false;

        // === Network state ===
        private readonly NetworkVariable<int> _hull = new NetworkVariable<int>(
            100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _maxHull = new NetworkVariable<int>(
            100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // === Runtime ===
        private ShipController _shipController;

        /// <summary>
        /// Резолв конфига: inspector-поле → Resources.Load → hardcoded fallback.
        /// </summary>
        private ShipDamageConfig ResolvedConfig =>
            _damageConfig ?? ShipDamageConfig.Default;

        /// <summary>Публичный доступ к резолв-конфигу (для ShipController).</summary>
        public ShipDamageConfig Config => ResolvedConfig;

        /// <summary>
        /// Событие изменения HP (server-side). Параметры: (newHull, deltaHp, state).
        /// Подписывается ShipController для реакции на поломку.
        /// </summary>
        public event Action<int, int, HullState> OnHullChanged;

        // === Public API ===

        public int CurrentHull => _hull.Value;
        public int MaxHull => _maxHull.Value;
        public HullState State => _hull.Value > 0 ? HullState.Operational : HullState.Broken;
        public bool IsBroken => _hull.Value <= 0;

        /// <summary>
        /// Восстановить корпус до максимума (server-only).
        /// Вызывается из ShipModuleServer.RequestRepairHullRpc.
        /// </summary>
        public void RepairFull()
        {
            if (!IsServer) return;
            int old = _hull.Value;
            _hull.Value = _maxHull.Value;
            int delta = _hull.Value - old;
            if (delta > 0)
            {
                OnHullChanged?.Invoke(_hull.Value, delta, HullState.Operational);
                if (_debugLog || ResolvedConfig.verboseLogging)
                    Debug.Log($"[ShipHull] RepairFull: {old} → {_hull.Value} (ship={NetworkObjectId})");
            }
        }

        /// <summary>
        /// Применить урон от столкновения (server-only).
        /// Вызывается из ShipController.OnCollisionEnter.
        /// Формула: hullDamage = floor((energy - threshold) * coefficient), capped.
        /// </summary>
        /// <param name="impactEnergy">col.impulse.magnitude</param>
        public void ApplyCollisionDamage(float impactEnergy)
        {
            if (!IsServer) return;
            if (_hull.Value <= 0) return; // уже сломан

            var cfg = ResolvedConfig;
            if (impactEnergy < cfg.collisionEnergyThreshold) return;

            int damage = Mathf.FloorToInt((impactEnergy - cfg.collisionEnergyThreshold)
                                          * cfg.collisionDamageCoefficient);
            damage = Mathf.Min(damage, cfg.collisionDamageCap);
            if (damage <= 0) return;

            int newHull = Mathf.Max(0, _hull.Value - damage);
            int delta = _hull.Value - newHull;
            _hull.Value = newHull;

            HullState newState = newHull > 0 ? HullState.Operational : HullState.Broken;
            OnHullChanged?.Invoke(newHull, -delta, newState);

            if (_debugLog || cfg.verboseLogging)
                Debug.Log($"[ShipHull] Collision damage: hull {_hull.Value + delta} → {newHull} " +
                          $"(energy={impactEnergy:F1}, dmg={damage}, ship={NetworkObjectId})");
        }

        // === IDamageTarget ===

        public Vector3 GetPosition() => transform.position;
        public int GetCurrentHp() => _hull.Value;
        public int GetMaxHp() => _maxHull.Value;

        public int GetArmorDefense()
        {
            return ResolvedConfig.armorHull;
        }

        /// <summary>
        /// Корабль всегда «жив» — даже при 0 HP он не уничтожается, а ломается.
        /// CombatServer не публикует EntityKilledEvent.
        /// </summary>
        public bool IsAlive() => true;

        public bool IsPlayer() => false;

        public string GetDisplayName()
        {
            if (_shipController != null && !string.IsNullOrEmpty(_shipController.CustomDisplayName))
                return _shipController.CustomDisplayName;
            return $"Ship {NetworkObjectId}";
        }

        public ulong GetTargetId() => NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL;

        public void ApplyDamage(DamageResult result, ulong attackerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[ShipHull] ApplyDamage called on non-server.");
                return;
            }
            if (!result.isHit) return;
            if (_hull.Value <= 0) return; // уже сломан

            int newHull = Mathf.Max(0, _hull.Value - result.finalDamage);
            int delta = _hull.Value - newHull;
            _hull.Value = newHull;

            HullState newState = newHull > 0 ? HullState.Operational : HullState.Broken;
            OnHullChanged?.Invoke(newHull, -delta, newState);

            if (_debugLog || ResolvedConfig.verboseLogging)
                Debug.Log($"[ShipHull] Combat damage: hull {_hull.Value + delta} → {newHull} " +
                          $"(dmg={result.finalDamage}, attacker={attackerClientId}, " +
                          $"isCrit={result.isCrit}, type={result.damageType}, ship={NetworkObjectId})");
        }

        // === Lifecycle ===

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;

            _shipController = GetComponent<ShipController>();

            // Инициализация HP из конфига по классу корабля
            var cfg = ResolvedConfig;
            int maxHp = cfg.GetMaxHull(_shipController != null
                ? _shipController.ShipFlightClass
                : ShipFlightClass.Medium);
            _maxHull.Value = maxHp;
            _hull.Value = maxHp;

            // Self-register в CombatServer (как NpcTarget)
            if (CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterTarget(GetTargetId(), this);
                if (_debugLog) Debug.Log($"[ShipHull] Registered in CombatServer: id={GetTargetId()}, HP={maxHp}");
            }
            else
            {
                Debug.LogWarning($"[ShipHull] CombatServer.Instance is null — ship {NetworkObjectId} not registered as damage target.");
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
    }
}
