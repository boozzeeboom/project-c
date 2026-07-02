// Project C: Real-Time Combat Engine — T-NPC-03
// NpcLootPickup: loot-entity, спавнится на месте смерти NPC-врага. Содержит credits.
// Design: docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md §2.2, §3.1.
//
// Игрок подходит → нажимает E → кредиты начисляются → pickup исчезает.
// Auto-despawn через autoDespawnSeconds если не подобран.
//
// Anti-restrictive: НЕ знает о CombatServer напрямую — читает только
// ProjectC.Trade.Repository.IPlayerDataRepository через TradeWorld.Instance.Repository.
// Это позволяет заменить систему кредитов без изменений в AI/Combat.
//
// MVP (T-NPC-03): только credits, без items. Items добавляются в T-NPC-04 (LootTable extension).

using UnityEngine;
using Unity.Netcode;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-03: Loot pickup spawned at NPC death location. Server-authoritative credits.
    /// Pickup flow:
    ///   - Spawned server-side by NpcTarget.OnKilled (T-NPC-03 + T-NPC-04).
    ///   - Player approaches → InteractableManager registers (trigger-based, same as PickupItem).
    ///   - NetworkPlayer.Update E-key → NetworkPlayer calls NpcLootPickup.Collect(clientId).
    ///   - Server: Repository.TryModifyCredits(clientId, creditsAmount) → Despawn.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class NpcLootPickup : NetworkBehaviour, Core.IInteractable
    {
        [Header("Loot payload (server-side)")]
        [Tooltip("Количество кредитов в этом пикапе.")]
        [Range(0, 10000)] public int creditsAmount = 50;

        [Tooltip("Display name для UI ('Coins', 'Gold Pouch', etc).")]
        public string displayName = "Loot";

        [Header("Settings")]
        [Tooltip("Радиус подбора (IInteractable).")]
        [Range(0.5f, 5f)] public float interactionRadius = 2.0f;

        [Tooltip("Авто-despawn если не подобран (сек). 0 = бесконечно.")]
        [Range(5f, 300f)] public float autoDespawnSeconds = 60f;

        [Tooltip("Visual pulse (bobbing up/down).")]
        public float floatSpeed = 1.5f;
        public float floatAmplitude = 0.2f;

        private Vector3 _startPosition;
        private bool _collected = false;
        // T-PICKUP-RIDE-01: pickup едет с палубой движущегося корабля (L3 в carry-цепочке)
        private Core.PickupDeckRide _deckRide;

        // === IInteractable ===
        public string InstanceId => gameObject.name + "_" + GetHashCode();
        public string DisplayName => $"{displayName} ({creditsAmount} CR)";
        public float InteractionRadius => interactionRadius;
        public Vector3 Position => transform.position;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _startPosition = transform.position;

            // Trigger collider
            var col = GetComponent<Collider>();
            if (col == null) col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;

            // T-PICKUP-RIDE-01: добавить PickupDeckRide на loot pickup
            // (L3 carry — loot, выпавший с моба на корабле, едет с кораблём).
            if (GetComponent<Core.PickupDeckRide>() == null)
            {
                _deckRide = gameObject.AddComponent<Core.PickupDeckRide>();
            }
            else
            {
                _deckRide = GetComponent<Core.PickupDeckRide>();
            }

            // Auto-despawn (server-side)
            if (IsServer && autoDespawnSeconds > 0)
            {
                Invoke(nameof(ServerAutoDespawn), autoDespawnSeconds);
            }
        }

        private void Update()
        {
            // Visual bobbing (client + server, cheap).
            if (!_collected)
            {
                // T-PICKUP-RIDE-01 final fix (2026-07-02):
                // На палубе НЕ пишем transform.position (carry сам двигает за палубой).
                // В свободном режиме RefreshWorldBase + bob вокруг актуальной базы.
                if (_deckRide == null || _deckRide.DeckParent == null)
                {
                    _deckRide?.RefreshWorldBase();
                    Vector3 bob = Vector3.up * Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
                    transform.position = _deckRide.WorldBasePosition + bob;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Register with InteractableManager (T-NPC-03: NpcLootPickup-specific pool).
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.RegisterNpcLoot(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.UnregisterNpcLoot(this);
            }
        }

        private void OnDisable()
        {
            Core.InteractableManager.UnregisterNpcLoot(this);
        }

        /// <summary>
        /// T-NPC-03: Подбор пикапа игроком. Вызывается из NetworkPlayer E-key handler.
        /// </summary>
        public void Collect(ulong playerClientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[NpcLootPickup] Collect called on non-server.");
                return;
            }
            if (_collected) return;
            _collected = true;

            // Начислить кредиты через IPlayerDataRepository.
            try
            {
                var trade = ProjectC.Trade.Core.TradeWorld.Instance;
                if (trade?.Repository != null && creditsAmount > 0)
                {
                    if (trade.Repository.TryModifyCredits(playerClientId, creditsAmount, out float newCredits, out string failReason))
                    {
                        Debug.Log($"[NpcLootPickup] Player {playerClientId} collected {creditsAmount} CR (new balance={newCredits:F0}).");
                    }
                    else
                    {
                        Debug.LogWarning($"[NpcLootPickup] Credits add failed: {failReason}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[NpcLootPickup] TradeWorld.Repository unavailable, credits={creditsAmount} skipped.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NpcLootPickup] Credits exception: {ex.Message}");
            }

            // Despawn (replicated to all clients → Destroy).
            NetworkObject.Despawn(true);
        }

        private void ServerAutoDespawn()
        {
            if (!IsServer || _collected) return;
            if (Debug.isDebugBuild) Debug.Log($"[NpcLootPickup] Auto-despawn after {autoDespawnSeconds}s: {displayName}");
            NetworkObject.Despawn(true);
        }
    }
}