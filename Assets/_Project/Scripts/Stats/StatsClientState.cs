// Project C: Character Progression — T-P04
// StatsClientState — клиентский singleton для stats snapshot'ов.
// Pattern: копия Assets/_Project/Reputation/ReputationClientState.cs (T-Q13, M11 session).
// Design: docs/Character/08_ROADMAP.md T-P04, docs/Character/02_V2_ARCHITECTURE.md §1.3
//
// Lifecycle:
//   - Создаётся scene-placed в BootstrapScene в T-P18 (рядом с [QuestClientState], [ReputationClientState]).
//   - T-P06 (NMC integration) — NetworkManagerController.CreateStatsClientState() auto-spawn.
//   - В T-P04 standalone — тип готов, без auto-spawn.
//
// Server push (T-P06):
//   NetworkPlayer.ReceiveStatsSnapshotTargetRpc(snap) → StatsClientState.Instance.OnStatsSnapshotReceived(snap)
//   → update CurrentStats + fire OnStatsUpdated + maybe fire OnStatTierUp.
//
// Throttle note (roadmap §6 #13): tier-up event может спамить (+5 stat levels при mining burst).
// Реализуем simple 200ms throttle через DateTime.UtcNow — последний tier-up в окне показывается,
// остальные подавляются. Snapshot fire'ится без throttle (это data, не notification).
//
// UI: CharacterWindow подписывается в EnsureBuilt (с lazy-subscribe в Update на race с NMC.Awake).

using System;
using UnityEngine;
using ProjectC.Stats.Dto;

namespace ProjectC.Stats
{
    /// <summary>
    /// Client-side projection of server stats state. Один инстанс на клиентский процесс.
    /// Создаётся scene-placed в BootstrapScene (рядом с [QuestClientState], [ReputationClientState]).
    /// </summary>
    /// <remarks>
    /// Server push: ReceiveStatsSnapshotTargetRpc → OnStatsSnapshotReceived →
    /// update CurrentStats + fire OnStatsUpdated (data) и OnStatTierUp (notification, throttled).
    /// UI: CharacterWindow подписывается на event в EnsureBuilt (с lazy-subscribe в Update
    /// на случай race с NetworkManagerController.Awake).
    /// </remarks>
    public class StatsClientState : MonoBehaviour
    {
        public static StatsClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Throttle (roadmap §6 #13)")]
        [Tooltip("Минимальный интервал между OnStatTierUp ивентами. Спам-защита: при mining burst " +
                 "можно апнуть 3+ стата за <1s; показываем tier-up только раз в окно.")]
        [SerializeField, Min(0f)] private float _tierUpEventMinIntervalSeconds = 0.2f;

        // ============ State ============
        public StatsSnapshotDto? CurrentStats { get; private set; }

        // ============ Events для UI ============
        /// <summary>Data event: новый snapshot пришёл. UI вызывает RefreshDisplay.</summary>
        public event Action<StatsSnapshotDto> OnStatsUpdated;

        /// <summary>Notification event: тир поднят. UI показывает toast/flash. Throttled.</summary>
        public event Action<StatType, int> OnStatTierUp;

        // Throttle bookkeeping
        private DateTime _lastTierUpUtc = DateTime.MinValue;

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
        /// Server → client handler. Вызывается из NetworkPlayer.ReceiveStatsSnapshotTargetRpc (T-P06).
        /// Обновляет CurrentStats и fire'ит OnStatsUpdated.
        /// </summary>
        public void OnStatsSnapshotReceived(StatsSnapshotDto snapshot)
        {
            // Tier-up detection: сравниваем со старым snapshot'ом
            StatType? tierUpStat = null;
            int newTier = 0;
            if (CurrentStats.HasValue)
            {
                var prev = CurrentStats.Value;
                if (snapshot.strengthTier > prev.strengthTier) { tierUpStat = StatType.Strength; newTier = snapshot.strengthTier; }
                else if (snapshot.dexterityTier > prev.dexterityTier) { tierUpStat = StatType.Dexterity; newTier = snapshot.dexterityTier; }
                else if (snapshot.intelligenceTier > prev.intelligenceTier) { tierUpStat = StatType.Intelligence; newTier = snapshot.intelligenceTier; }
            }

            CurrentStats = snapshot;
            OnStatsUpdated?.Invoke(snapshot);

            if (tierUpStat.HasValue)
            {
                FireTierUpThrottled(tierUpStat.Value, newTier);
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[StatsClientState] OnStatsSnapshotReceived: STR={snapshot.strength:F1}/T{snapshot.strengthTier} " +
                          $"DEX={snapshot.dexterity:F1}/T{snapshot.dexterityTier} INT={snapshot.intelligence:F1}/T{snapshot.intelligenceTier}");
            }
        }

        private void FireTierUpThrottled(StatType stat, int newTier)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastTierUpUtc).TotalSeconds;
            if (elapsed < _tierUpEventMinIntervalSeconds)
            {
                // Подавлено — последний tier-up в окне
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[StatsClientState] TierUp throttled: {stat} → T{newTier} (elapsed {elapsed:F2}s < {_tierUpEventMinIntervalSeconds:F2}s)");
                }
                return;
            }
            _lastTierUpUtc = now;
            OnStatTierUp?.Invoke(stat, newTier);
        }

        /// <summary>
        /// Convenience для UI/tests: clear state (например, при scene reload без DontDestroyOnLoad).
        /// </summary>
        public void ClearState()
        {
            CurrentStats = null;
            _lastTierUpUtc = DateTime.MinValue;
        }
    }
}
