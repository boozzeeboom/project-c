// Project C: Real-Time Combat Engine — T-NPC-S05
// GrudgeTable: память обидчиков (playerId → timestamp).
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §4 (T4 GrudgeTrigger).

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Хранит список обидчиков (player clientId) с временем последней атаки.
    /// Используется NpcSocialBrain для persistent aggro при повторной встрече.
    /// </summary>
    [System.Serializable]
    public class GrudgeTable
    {
        [Tooltip("Время жизни записи (сек). После истечения — обидчик забывается.")]
        public float grudgeDurationSec = 300f;

        /// <summary>playerClientId → Time.unscaledTime последней атаки.</summary>
        private Dictionary<ulong, float> _entries = new Dictionary<ulong, float>();

        /// <summary>Зарегистрировать атаку от игрока.</summary>
        public void RecordHit(ulong playerClientId)
        {
            _entries[playerClientId] = Time.unscaledTime;
        }

        /// <summary>Проверить, помнит ли NPC этого игрока (и не истекла ли запись).</summary>
        public bool HasGrudge(ulong playerClientId)
        {
            if (!_entries.TryGetValue(playerClientId, out float timestamp))
                return false;
            if (Time.unscaledTime - timestamp > grudgeDurationSec)
            {
                _entries.Remove(playerClientId);
                return false;
            }
            return true;
        }

        /// <summary>Очистить все записи (например, при смерти NPC).</summary>
        public void Clear()
        {
            _entries.Clear();
        }

        /// <summary>Очистить просроченные записи.</summary>
        public void RemoveExpired()
        {
            float now = Time.unscaledTime;
            var expired = new List<ulong>();
            foreach (var kv in _entries)
            {
                if (now - kv.Value > grudgeDurationSec)
                    expired.Add(kv.Key);
            }
            foreach (var key in expired)
                _entries.Remove(key);
        }
    }
}
