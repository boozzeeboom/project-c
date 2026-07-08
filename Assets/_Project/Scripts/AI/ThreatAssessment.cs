// Project C: Real-Time Combat Engine — T-NPC-S13
// ThreatAssessment: оценка соотношения сил перед боем.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §4 T-NPC-S13
//          + 02_SOCIAL_HUMAN_BEHAVIOR.md §2.3.1

using UnityEngine;
using ProjectC.Combat;
using ProjectC.Combat.Core;

namespace ProjectC.AI
{
    /// <summary>
    /// Результат оценки угрозы — определяет, как NPC реагирует на врага.
    /// </summary>
    public enum ThreatResult
    {
        /// <summary>threatScore &lt; 0.5: уверен в победе → Chase.</summary>
        Confident,
        /// <summary>0.5 ≤ threatScore &lt; 1.5: осторожен → Investigate/Warn, потом Chase.</summary>
        Cautious,
        /// <summary>threatScore ≥ 1.5: боится → Flee или CallForHelp.</summary>
        Afraid,
    }

    /// <summary>
    /// T-NPC-S13: Оценка соотношения сил.
    /// Вычисляет threatScore = Σ(enemyStrength) / Σ(allyStrength)
    /// и возвращает решение: Confident / Cautious / Afraid.
    /// </summary>
    public struct ThreatAssessment
    {
        /// <summary>Итоговый threatScore (0 = врагов нет, >1 = враги сильнее).</summary>
        public float threatScore;

        /// <summary>Количество учтённых врагов.</summary>
        public int enemyCount;

        /// <summary>Количество учтённых союзников.</summary>
        public int allyCount;

        /// <summary>Результирующее решение.</summary>
        public ThreatResult result;

        // --- Static evaluation ---

        /// <summary>
        /// Оценить угрозу относительно текущего NPC.
        /// </summary>
        /// <param name="brain">NpcBrain оценивающего NPC.</param>
        /// <param name="group">Группа NPC (может быть null).</param>
        /// <param name="evaluationRange">Радиус, в котором считаются враги и союзники.</param>
        public static ThreatAssessment Evaluate(
            NpcBrain brain,
            NpcGroupController group,
            float evaluationRange = 30f)
        {
            var result = new ThreatAssessment();
            if (brain == null || !brain.IsServer) return result;

            Vector3 origin = brain.transform.position;
            float enemyStr = 0f;
            float allyStr = 1f; // сам NPC = 1 unit

            // --- Считаем врагов (игроков) в evaluationRange ---
            enemyStr = CountEnemyStrength(origin, evaluationRange, out result.enemyCount);

            // --- Считаем союзников (члены группы или nearby NPC) ---
            allyStr = 1f + CountAllyStrength(origin, group, evaluationRange, out result.allyCount);

            // --- threatScore ---
            result.threatScore = allyStr > 0f ? enemyStr / allyStr : enemyStr;

            // --- Решение ---
            if (result.threatScore < 0.5f)
                result.result = ThreatResult.Confident;
            else if (result.threatScore < 1.5f)
                result.result = ThreatResult.Cautious;
            else
                result.result = ThreatResult.Afraid;

            return result;
        }

        /// <summary>
        /// Быстрая оценка: есть ли враги в evaluationRange?
        /// </summary>
        public static bool HasEnemiesInRange(Vector3 origin, float range)
        {
            if (Unity.Netcode.NetworkManager.Singleton == null) return false;
            float rangeSq = range * range;
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                var pt = client.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (pt == null || !pt.IsAlive()) continue;
                if ((client.PlayerObject.transform.position - origin).sqrMagnitude <= rangeSq)
                    return true;
            }
            return false;
        }

        // --- Private helpers ---

        private static float CountEnemyStrength(Vector3 origin, float range, out int count)
        {
            count = 0;
            float total = 0f;
            if (Unity.Netcode.NetworkManager.Singleton == null) return total;
            float rangeSq = range * range;
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                var pt = client.PlayerObject.GetComponent<ProjectC.Combat.PlayerTarget>();
                if (pt == null || !pt.IsAlive()) continue;
                float dSq = (client.PlayerObject.transform.position - origin).sqrMagnitude;
                if (dSq > rangeSq) continue;

                count++;
                // Strength = f(HP%) → игрок с полным HP = 1.0, с 10% HP = 0.1
                float hpPercent = pt.GetMaxHp() > 0 ? (float)pt.GetCurrentHp() / pt.GetMaxHp() : 1f;
                total += Mathf.Clamp01(hpPercent);
            }
            return total;
        }

        private static float CountAllyStrength(Vector3 origin, NpcGroupController group, float range, out int count)
        {
            count = 0;
            float total = 0f;

            // Сначала считаем членов группы (быстрый путь, без FindObjects).
            if (group != null)
            {
                foreach (var m in group.members)
                {
                    if (m == null || m.IsDead) continue;
                    float dSq = (m.transform.position - origin).sqrMagnitude;
                    if (dSq > range * range) continue;
                    count++;
                    // Strength = f(HP%) NPC
                    total += GetNpcHpPercent(m);
                }
                return total;
            }

            // Fallback: поиск nearby NPC (дорогой путь — только если группы нет).
            foreach (var npc in Object.FindObjectsByType<NpcSocialBrain>(FindObjectsSortMode.None))
            {
                if (npc == null || npc.IsDead) continue;
                float dSq = (npc.transform.position - origin).sqrMagnitude;
                if (dSq > range * range) continue;
                count++;
                total += GetNpcHpPercent(npc);
            }
            return total;
        }

        private static float GetNpcHpPercent(NpcSocialBrain brain)
        {
            if (brain == null || brain._brain == null) return 1f;
            var tgt = brain.GetComponent<NpcTarget>();
            if (tgt == null) return 1f;
            return tgt.GetMaxHp() > 0 ? (float)tgt.GetCurrentHp() / tgt.GetMaxHp() : 1f;
        }
    }
}
