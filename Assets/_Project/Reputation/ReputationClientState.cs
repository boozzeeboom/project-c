// T-Q13: ReputationClientState — клиентский singleton для faction reputation snapshot.
// Pattern: как QuestClientState, но только для reputation. Отдельный singleton по
// roadmap §8.3 (T-Q13 line 368).
//
// Получает snapshot'ы от QuestServer через NetworkPlayer.ReceiveReputationSnapshotTargetRpc.
// UI читает из этого singleton (single source of truth на клиенте).

using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Quests.Dto;

namespace ProjectC.Reputation
{
    /// <summary>
    /// Client-side projection of server reputation state. Один инстанс на клиентский процесс.
    /// Создаётся scene-placed в BootstrapScene (рядом с [QuestClientState]).
    /// </summary>
    /// <remarks>
    /// Server push: ReceiveReputationSnapshotTargetRpc → OnReputationSnapshotReceived →
    /// update CurrentReputation + fire OnReputationUpdated. UI: CharacterWindow подписывается
    /// на event в EnsureBuilt (с lazy-subscribe в Update на случай race с NetworkManagerController.Awake).
    /// </remarks>
    public class ReputationClientState : MonoBehaviour
    {
        public static ReputationClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============ State ============
        public ReputationSnapshotDto? CurrentReputation { get; private set; }

        // ============ T-KNOW: Known entities ============
        public HashSet<byte> KnownFactionIds { get; private set; } = new HashSet<byte>();

        // ============ Events для UI ============
        public event Action<ReputationSnapshotDto> OnReputationUpdated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveReputationSnapshotTargetRpc.
        /// </summary>
        public void OnReputationSnapshotReceived(ReputationSnapshotDto snapshot)
        {
            CurrentReputation = snapshot;

            // T-KNOW: update known factions
            KnownFactionIds.Clear();
            if (snapshot.knownFactionIds != null)
            {
                for (int i = 0; i < snapshot.knownFactionIds.Length; i++)
                    KnownFactionIds.Add(snapshot.knownFactionIds[i]);
            }
            // Always ensure Neutral (11) is known — server-side гарантирует, но на клиенте тоже страховка
            KnownFactionIds.Add((byte)ProjectC.Factions.FactionId.Neutral);

            OnReputationUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild)
                Debug.Log($"[ReputationClientState] OnReputationSnapshotReceived: {snapshot.entries?.Length ?? 0} factions, {KnownFactionIds.Count} known");
        }
    }
}
