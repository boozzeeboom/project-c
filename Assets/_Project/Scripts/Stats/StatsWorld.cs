// Project C: Character Progression — T-P03
// StatsWorld: POCO singleton — server-side per-player state storage.
// Design: docs/Character/02_V2_ARCHITECTURE.md §2.3, docs/Character/08_ROADMAP.md T-P03
//
// Pattern: копия InventoryWorld (Assets/_Project/Items/Core/InventoryWorld.cs:36-77) +
// паттерн из docs/Crafting_system/ и docs/NPC_quests/.
//
// Lifecycle: создаётся в StatsServer.OnNetworkSpawn (T-P05) на сервере.
// Не MonoBehaviour — чистый POCO. Singleton через static Instance.
//
// Public API:
//   - GetOrCreateStats(clientId) — read or insert PlayerStats.Default
//   - SetStats(clientId, stats) — overwrite (для ApplyXp/Recompute)
//   - GetStats(clientId) — read-only, returns null если не существует
//   - HasStats(clientId) — bool check
//   - GetAllPlayerIds() — для SaveAll
//   - RemovePlayer(clientId) — disconnect cleanup
//   - BuildSaveData(clientId) / LoadPlayer(clientId, data) — persistence interface
//
// ВАЖНО: singleton существует ТОЛЬКО на сервере. Если клиент случайно
// вызовет StatsWorld.Instance — выдаст null (Instance = null). Защиты через
// Debug.Assert нет (POCO), но все consumers — серверные NetworkBehaviour.

using System.Collections.Generic;
using ProjectC.Stats.Persistence;
using UnityEngine;

namespace ProjectC.Stats
{
    /// <summary>
    /// Server-only POCO singleton: per-player PlayerStats state.
    /// </summary>
    public class StatsWorld
    {
        public static StatsWorld Instance { get; private set; }

        private readonly Dictionary<ulong, PlayerStats> _stats = new Dictionary<ulong, PlayerStats>();

        public StatsWorld()
        {
            if (Instance != null)
            {
                Debug.LogWarning($"[StatsWorld] Replacing existing instance (was from {Instance.GetType().Name} ctor path).");
            }
            Instance = this;
        }

        /// <summary>Reset singleton (EditMode tests / domain reload).</summary>
        public static void Reset()
        {
            Instance = null;
        }

        // === Read API ===

        /// <summary>Get existing or insert PlayerStats.Default. Всегда возвращает non-null.</summary>
        public PlayerStats GetOrCreateStats(ulong clientId)
        {
            if (!_stats.TryGetValue(clientId, out var stats))
            {
                stats = PlayerStats.Default;
                _stats[clientId] = stats;
            }
            return stats;
        }

        /// <summary>Read-only. Returns null если игрок не зарегистрирован.</summary>
        public PlayerStats? GetStats(ulong clientId)
        {
            if (_stats.TryGetValue(clientId, out var stats)) return stats;
            return null;
        }

        public bool HasStats(ulong clientId) => _stats.ContainsKey(clientId);

        public IEnumerable<ulong> GetAllPlayerIds() => _stats.Keys;

        public int PlayerCount => _stats.Count;

        // === Write API ===

        public void SetStats(ulong clientId, PlayerStats stats)
        {
            _stats[clientId] = stats;
        }

        public void RemovePlayer(ulong clientId)
        {
            _stats.Remove(clientId);
        }

        // === Persistence interface (полная реализация в T-P06) ===

        /// <summary>
        /// Собрать save DTO для одного игрока. Полная версия в T-P06 (EquipmentSave, SkillsSave),
        /// сейчас T-P03 — только stats. T-P05 (StatsServer) уже может дёргать для автосейва.
        /// </summary>
        public CharacterSaveData BuildSaveData(ulong clientId)
        {
            var data = new CharacterSaveData();
            if (_stats.TryGetValue(clientId, out var stats))
            {
                data.stats = PlayerStatsSave.FromPlayerStats(stats);
            }
            return data;
        }

        /// <summary>
        /// Восстановить state игрока из save DTO. Полная версия в T-P06.
        /// </summary>
        public void LoadPlayer(ulong clientId, CharacterSaveData data)
        {
            if (data == null || data.stats == null) return;
            _stats[clientId] = data.stats.ToPlayerStats();
        }
    }
}
