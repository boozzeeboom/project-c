// T-Q13: NpcAttitudeClientState — клиентский singleton для per-NPC attitude snapshot.
// Pattern: как ReputationClientState, но для NPC attitude. Отдельный singleton по
// roadmap §8.3 (T-Q13 line 369).
//
// Получает snapshot'ы от QuestServer через NetworkPlayer.ReceiveNpcAttitudeSnapshotTargetRpc.
// UI читает из этого singleton (DialogWindow badge + CharacterWindow NpcAttitude секция).

using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Quests.Dto;

namespace ProjectC.Reputation
{
    /// <summary>
    /// Client-side projection of server NpcAttitude state. Один инстанс на клиентский процесс.
    /// Создаётся scene-placed в BootstrapScene (рядом с [QuestClientState] / [ReputationClientState]).
    /// </summary>
    /// <remarks>
    /// Server push: ReceiveNpcAttitudeSnapshotTargetRpc → OnNpcAttitudeSnapshotReceived →
    /// update CurrentNpcAttitude + fire OnNpcAttitudeUpdated. UI: DialogWindow badge
    /// (показывает "❤ +15" рядом с npc-name) + CharacterWindow NpcAttitude секция.
    /// </remarks>
    public class NpcAttitudeClientState : MonoBehaviour
    {
        public static NpcAttitudeClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // ============ State ============
        public NpcAttitudeSnapshotDto? CurrentNpcAttitude { get; private set; }

        // ============ T-KNOW: Known entities ============
        public HashSet<string> KnownNpcIds { get; private set; } = new HashSet<string>();

        // ============ Events для UI ============
        public event Action<NpcAttitudeSnapshotDto> OnNpcAttitudeUpdated;

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
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveNpcAttitudeSnapshotTargetRpc.
        /// </summary>
        public void OnNpcAttitudeSnapshotReceived(NpcAttitudeSnapshotDto snapshot)
        {
            CurrentNpcAttitude = snapshot;

            // T-KNOW: update known NPCs
            KnownNpcIds.Clear();
            if (snapshot.knownNpcIds != null)
            {
                for (int i = 0; i < snapshot.knownNpcIds.Length; i++)
                    KnownNpcIds.Add(snapshot.knownNpcIds[i]);
            }

            OnNpcAttitudeUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild)
                Debug.Log($"[NpcAttitudeClientState] OnNpcAttitudeSnapshotReceived: {snapshot.entries?.Length ?? 0} NPCs, {KnownNpcIds.Count} known");
        }

        // ============ Helpers ============

        /// <summary>
        /// Достать текущее attitude для конкретного NPC из snapshot. 0 если NPC не найден
        /// или snapshot ещё не пришёл.
        /// </summary>
        public int GetAttitudeForNpc(string npcId)
        {
            if (string.IsNullOrEmpty(npcId) || !CurrentNpcAttitude.HasValue) return 0;
            var entries = CurrentNpcAttitude.Value.entries;
            if (entries == null) return 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].npcId == npcId) return entries[i].value;
            }
            return 0;
        }
    }
}
