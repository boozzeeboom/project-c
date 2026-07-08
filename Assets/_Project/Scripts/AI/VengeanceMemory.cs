// Project C: Real-Time Combat Engine — T-NPC-S20
// VengeanceMemory: кросс-спавн память обидчиков (persistence между смертями NPC).
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §4 T-NPC-S20
//          + 02_SOCIAL_HUMAN_BEHAVIOR.md §2.5.3

using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace ProjectC.AI
{
    /// <summary>
    /// Запись о vengeance: какой игрок убил союзника этой фракции и когда.
    /// </summary>
    [System.Serializable]
    public struct VengeanceEntry
    {
        public ulong playerClientId;
        public float timestamp; // Time.unscaledTime
    }

    /// <summary>
    /// T-NPC-S20: Серверный синглтон кросс-спавн памяти обидчиков.
    /// Хранит vengeance-записи: factionId → игроки, убившие членов этой фракции.
    /// При спавне NPC проверяет — если игрок в списке, мгновенный Aggro.
    /// При убийстве vengeance-target → buff всей группе NPC («отомстили!»).
    /// </summary>
    public class VengeanceMemory : NetworkBehaviour
    {
        public static VengeanceMemory Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Время хранения vengeance-записи (сек).")]
        [Range(60f, 3600f)] public float vengeanceDurationSec = 600f; // 10 минут

        [Tooltip("Радиус, в котором NPC реагирует на vengeance-target при спавне.")]
        [Range(10f, 100f)] public float vengeanceTriggerRadius = 40f;

        [Tooltip("Buff множитель урона при убийстве vengeance-target.")]
        [Range(1.1f, 2f)] public float vengeanceBuffMultiplier = 1.3f;

        [Tooltip("Длительность vengeance-buff (сек).")]
        [Range(5f, 60f)] public float vengeanceBuffDuration = 15f;

        // factionId → (playerClientId → VengeanceEntry)
        private Dictionary<string, Dictionary<ulong, VengeanceEntry>> _vengeanceTable
            = new Dictionary<string, Dictionary<ulong, VengeanceEntry>>();

        // Текущие активные buff'ы: (factionId, playerClientId, expireTime)
        private List<(string factionId, ulong playerId, float damageMultiplier, float expireTime)> _activeBuffs
            = new List<(string, ulong, float, float)>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) { enabled = false; return; }
            if (Instance == null) Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Зарегистрировать убийство NPC игроком → vengeance.
        /// Вызывается при смерти NPC (AllyKilled).
        /// </summary>
        public void RegisterKill(string factionId, ulong killerClientId)
        {
            if (string.IsNullOrEmpty(factionId) || killerClientId == 0) return;

            if (!_vengeanceTable.TryGetValue(factionId, out var dict))
            {
                dict = new Dictionary<ulong, VengeanceEntry>();
                _vengeanceTable[factionId] = dict;
            }

            dict[killerClientId] = new VengeanceEntry
            {
                playerClientId = killerClientId,
                timestamp = Time.unscaledTime,
            };

            // Очищаем просроченные для этой фракции.
            CleanExpired(factionId);
        }

        /// <summary>
        /// Проверить: есть ли vengeance на этого игрока у этой фракции?
        /// </summary>
        public bool HasVengeance(string factionId, ulong playerClientId)
        {
            if (string.IsNullOrEmpty(factionId) || playerClientId == 0) return false;
            if (!_vengeanceTable.TryGetValue(factionId, out var dict)) return false;
            if (!dict.TryGetValue(playerClientId, out var entry)) return false;
            if (Time.unscaledTime - entry.timestamp > vengeanceDurationSec)
            {
                dict.Remove(playerClientId);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Получить timestamp vengeance-записи (0 если нет).
        /// </summary>
        public float GetVengeanceTimestamp(string factionId, ulong playerClientId)
        {
            if (string.IsNullOrEmpty(factionId) || playerClientId == 0) return 0f;
            if (!_vengeanceTable.TryGetValue(factionId, out var dict)) return 0f;
            if (!dict.TryGetValue(playerClientId, out var entry)) return 0f;
            if (Time.unscaledTime - entry.timestamp > vengeanceDurationSec)
            {
                dict.Remove(playerClientId);
                return 0f;
            }
            return entry.timestamp;
        }

        /// <summary>
        /// Снять vengeance после убийства игрока (месть свершилась).
        /// Возвращает true если vengeance была активна (для buff'а).
        /// </summary>
        public bool ClearVengeance(string factionId, ulong playerClientId)
        {
            if (string.IsNullOrEmpty(factionId) || playerClientId == 0) return false;
            if (!_vengeanceTable.TryGetValue(factionId, out var dict)) return false;
            bool had = dict.Remove(playerClientId);
            if (had)
            {
                // Добавляем vengeance-buff для всей фракции!
                _activeBuffs.Add((factionId, playerClientId, vengeanceBuffMultiplier, Time.unscaledTime + vengeanceBuffDuration));
            }
            return had;
        }

        /// <summary>
        /// Получить текущий vengeance damage multiplier для NPC этой фракции.
        /// </summary>
        public float GetVengeanceBuff(string factionId)
        {
            float best = 1f;
            float now = Time.unscaledTime;
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                if (now > _activeBuffs[i].expireTime)
                {
                    _activeBuffs.RemoveAt(i);
                    continue;
                }
                if (_activeBuffs[i].factionId == factionId)
                    best = Mathf.Max(best, _activeBuffs[i].damageMultiplier);
            }
            return best;
        }

        /// <summary>
        /// Очистить все просроченные записи для фракции.
        /// </summary>
        public void CleanExpired(string factionId)
        {
            if (!_vengeanceTable.TryGetValue(factionId, out var dict)) return;
            float now = Time.unscaledTime;
            var expired = new List<ulong>();
            foreach (var kv in dict)
                if (now - kv.Value.timestamp > vengeanceDurationSec)
                    expired.Add(kv.Key);
            foreach (var key in expired)
                dict.Remove(key);
        }

        /// <summary>
        /// Полностью очистить vengeance для фракции (например, при вайпе всех NPC).
        /// </summary>
        public void ClearFaction(string factionId)
        {
            _vengeanceTable.Remove(factionId);
        }
    }
}
