// Project C: Real-Time Combat Engine — T-NPC-S07
// NpcEmotion: 6 эмоциональных состояний NPC.
// Design: docs/Character/Skills/real-time-combat/npc-enemy/04_UNIFIED_BEHAVIOR_ARCHITECTURE.md §2.

namespace ProjectC.AI
{
    /// <summary>
    /// 6 эмоциональных состояний NPC.
    /// Переходы управляются NpcSocialBrain через UpdateEmotion().
    /// </summary>
    public enum NpcEmotion
    {
        /// <summary>Базовое состояние. Idle/Patrol.</summary>
        Calm,
        /// <summary>Заметил угрозу. Не агрится, но насторожен.</summary>
        Alert,
        /// <summary>HP низкий / смерть союзника. → Flee.</summary>
        Fear,
        /// <summary>Агрился / мстит. → Chase + damage buff.</summary>
        Anger,
        /// <summary>HP &lt; 10% + outnumbered. → Surrender (Phase 3).</summary>
        Despair,
        /// <summary>Только что убил цель. Taunt → поиск новой.</summary>
        Victory,
    }

    /// <summary>
    /// Runtime-обёртка для NpcEmotion с логикой переходов.
    /// Используется NpcSocialBrain.
    /// </summary>
    [System.Serializable]
    public class NpcEmotionState
    {
        public NpcEmotion Current { get; private set; } = NpcEmotion.Calm;
        public float TimeInCurrentState { get; private set; }
        private float _stateEnterTime;

        /// <summary>Установить эмоцию с записью времени входа.</summary>
        public void Set(NpcEmotion emotion)
        {
            if (Current == emotion) return;
            Current = emotion;
            _stateEnterTime = UnityEngine.Time.unscaledTime;
        }

        /// <summary>Обновить таймер состояния (вызывать каждый SocialTick).</summary>
        public void Tick()
        {
            TimeInCurrentState = UnityEngine.Time.unscaledTime - _stateEnterTime;
        }

        /// <summary>Сбросить в Calm.</summary>
        public void Reset()
        {
            Current = NpcEmotion.Calm;
            _stateEnterTime = UnityEngine.Time.unscaledTime;
            TimeInCurrentState = 0f;
        }
    }
}
