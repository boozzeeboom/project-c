// Project C: T-MB02 — MotionBricks feature flag (safe rollout, уровень 0).
//
// Единственный выключатель интеграции. Каноническая схема — docs/world/MOTIONBRICK_animations/
// 06_SAFE_ROLLOUT_AND_ROLLBACK.md §3. Правила:
//   - Нет ассета настроек ИЛИ mode = Disabled → baseline Animator, ничего не запускается (fail-closed).
//   - BakedClips → играем запечённый .anim через отдельный PlayableGraph, контроллер не трогаем.
//   - NeuralExperimental → только для эксперимента T-MB06/07; в MMO-клиенте не включается.
//   - allowNpc / allowRemotePlayers — жёстко false на MVP.

using UnityEngine;

namespace ProjectC.Animation.MotionBricks
{
    public enum MotionBricksMode
    {
        Disabled = 0,          // baseline: только текущий Animator
        BakedClips = 1,        // запечённые .anim через отдельный playback
        NeuralExperimental = 2 // runtime-генерация, только owner player, dev
    }

    /// <summary>
    /// Runtime-настройки MotionBricks. Отсутствие ассета трактуется как <see cref="MotionBricksMode.Disabled"/>.
    /// Создаётся в Unity: Create > ProjectC/Animation/MotionBricks Runtime Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Animation/MotionBricks Runtime Settings", fileName = "MotionBricksRuntimeSettings")]
    public sealed class MotionBricksRuntimeSettings : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("Disabled = baseline Animator. BakedClips = играть запечённые .anim. NeuralExperimental = только dev.")]
        public MotionBricksMode mode = MotionBricksMode.Disabled;

        [Header("Scope")]
        public bool allowPlayer = true;

        [Tooltip("Всегда false для MMO-клиента: NPC анимируются authored-контроллерами.")]
        public bool allowNpc = false;

        [Tooltip("Всегда false: удалённые игроки анимируются своими клиентами.")]
        public bool allowRemotePlayers = false;

        [Tooltip("Всегда false на MVP: combat анимируется SkillAnimationPlayer.")]
        public bool allowInCombat = false;

        [Header("Runtime (только NeuralExperimental)")]
        public int maxInferenceMs = 100;

        [Tooltip("После N ошибок — circuit breaker: mode на сессию становится Disabled.")]
        public int failureBudget = 3;

        [Tooltip("null = текущий controller. Используется только как fallback при ошибке.")]
        public RuntimeAnimatorController fallbackController;

        /// <summary>Проверка «можно ли вообще что-то запускать» (fail-closed).</summary>
        public bool AllowsPlayback => mode != MotionBricksMode.Disabled;
    }
}
