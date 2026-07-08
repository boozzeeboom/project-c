// Project C: Real-Time Combat Engine — T-NPC-S08
// NpcMoraleData: расчёт морали NPC (0..1).
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §3.

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// Мораль NPC: 0 = готов сдаться, 1 = максимальная решимость.
    /// Пересчитывается каждый SocialTick на основе событий.
    /// </summary>
    [System.Serializable]
    public struct NpcMoraleData
    {
        /// <summary>Текущее значение морали (0..1).</summary>
        public float current;

        /// <summary>Базовое значение = personality.courage (при старте).</summary>
        public float baseValue;

        // --- Свойства, производные от морали ---

        /// <summary>Пора бежать? Порог зависит от courage.</summary>
        public bool ShouldFlee(NpcPersonalityConfig personality)
        {
            float threshold = (1f - (personality != null ? personality.courage : 0.7f)) * 0.5f;
            return current < threshold;
        }

        /// <summary>Пора сдаваться? HP < 15% + morale < 0.15.</summary>
        public bool ShouldSurrender(float hpPercent)
        {
            return current < 0.15f && hpPercent < 0.15f;
        }

        /// <summary>Множитель урона: от 0.5 (низкая мораль) до 1.2 (высокая).</summary>
        public float DamageMultiplier => Mathf.Lerp(0.5f, 1.2f, current);

        /// <summary>Множитель скорости: +30% при морали ниже 0.5 (бегство).</summary>
        public float SpeedMultiplier => current < 0.5f ? 1.3f : 1.0f;

        // --- Модификаторы (вызываются из NpcSocialBrain) ---

        /// <summary>Инициализация при старте.</summary>
        public void Initialize(NpcPersonalityConfig personality)
        {
            baseValue = personality != null ? personality.courage : 0.7f;
            current = baseValue;
        }

        /// <summary>Союзник умер рядом. loyalty усиливает эффект.</summary>
        public void OnAllyKilled(NpcPersonalityConfig personality)
        {
            float loyalty = personality != null ? personality.loyalty : 0.8f;
            // Высокая loyalty → сильнее падает мораль (горюет о союзнике).
            // Но при очень высокой loyalty может быть rage вместо страха.
            float penalty = loyalty > 0.7f ? 0.1f : 0.2f;
            current = Mathf.Max(0.05f, current - penalty);
        }

        /// <summary>Получен урон.</summary>
        public void OnDamageTaken(float deltaHpPercent)
        {
            current = Mathf.Max(0.05f, current - 0.05f * deltaHpPercent);
        }

        /// <summary>Численное меньшинство.</summary>
        public void OnOutnumbered()
        {
            current = Mathf.Max(0.05f, current - 0.15f);
        }

        /// <summary>Лидер группы умер.</summary>
        public void OnLeaderDied()
        {
            current = Mathf.Max(0.05f, current - 0.3f);
        }

        /// <summary>Лидер рядом.</summary>
        public void OnLeaderNearby()
        {
            current = Mathf.Min(1f, current + 0.1f);
        }

        /// <summary>Подкрепление рядом.</summary>
        public void OnReinforcementNearby()
        {
            current = Mathf.Min(1f, current + 0.15f);
        }

        /// <summary>NPC убил цель (Victory).</summary>
        public void OnKilledTarget()
        {
            current = Mathf.Min(1f, current + 0.3f);
        }

        /// <summary>Успешный отход (вышел из боя).</summary>
        public void OnSuccessfulRetreat()
        {
            current = Mathf.Min(1f, current + 0.1f);
        }

        /// <summary>FearCry от союзника (деморализует).</summary>
        public void OnFearCryHeard()
        {
            current = Mathf.Max(0.05f, current - 0.05f);
        }

        /// <summary>VictoryRoar от союзника (воодушевляет).</summary>
        public void OnVictoryRoarHeard()
        {
            current = Mathf.Min(1f, current + 0.1f);
        }

        /// <summary>VictoryRoar от врага (деморализует).</summary>
        public void OnEnemyVictoryRoarHeard()
        {
            current = Mathf.Max(0.05f, current - 0.05f);
        }
    }
}
