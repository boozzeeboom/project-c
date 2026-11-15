// Project C: T-MB04c — единственный канал baked root motion → движение NetworkPlayer.
//
// Зачем мост, а не прямой вызов CharacterController.Move:
//   NetworkPlayer.ProcessMovement уже вызывает CharacterController.Move (единственный Move
//   у игрока, см. T-MB04b §2.2). Второй Move дал бы двойную скорость и рассинхрон с
//   NetworkTransform. Поэтому MotionBricksBakedRootMotion на сетевом персонаже НИЧЕГО не
//   двигает: он публикует смещение кадра в этот реестр, а NetworkPlayer забирает его
//   в своём существующем Move.
//
// Контракт:
//   - Register/Unregister — из OnEnable/OnDisable MotionBricksBakedRootMotion (без GetComponent в кадре).
//   - TryConsumeDisplacement(player, out displacement) — вызывается из ProcessMovement один раз
//     за кадр. Возвращает true, только если у этого игрока есть активный источник с реально
//     играющим playback; смещение обнуляется после чтения (двойного движения быть не может).
//   - Fail-closed: нет источника, playback не играет, Mode=Disabled, allowPlayer=false,
//     игрок не owner → false, движение остаётся baseline.
//
// Откат: mode = Disabled или удалить MotionBricksBakedRootMotion с игрока — мост пуст,
// NetworkPlayer двигается как раньше (ветка срабатывает только при true).

using System.Collections.Generic;
using ProjectC.Player;   // NetworkPlayer
using UnityEngine;

namespace ProjectC.Animation.MotionBricks
{
    public static class MotionBricksLocomotionBridge
    {
        private static readonly List<MotionBricksBakedRootMotion> Sources =
            new List<MotionBricksBakedRootMotion>(2);

        /// <summary>
        /// Если в проекте выключен domain reload, статика переживает сеанс Play Mode и в новом
        /// сеансе остались бы ссылки на уничтоженные компоненты. Сброс на старте Play Mode
        /// делает состояние предсказуемым (тот же приём, что у ReviewOverride в playback).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistryOnPlay() => Sources.Clear();

        /// <summary>Сколько источников зарегистрировано (диагностика harness-лога).</summary>
        public static int RegisteredCount => Sources.Count;

        public static void Register(MotionBricksBakedRootMotion source)
        {
            if (source == null || Sources.Contains(source)) return;
            Sources.Add(source);
        }

        public static void Unregister(MotionBricksBakedRootMotion source)
        {
            if (source == null) return;
            Sources.Remove(source);
        }

        /// <summary>
        /// Смещение baked-клипа для этого кадра (world space, уже с учётом dt).
        /// Читается из NetworkPlayer.ProcessMovement; после чтения источник обнуляется.
        /// </summary>
        public static bool TryConsumeDisplacement(NetworkPlayer player, out Vector3 displacement)
        {
            displacement = Vector3.zero;

            // Удалённые игроки не должны ехать от клипа локального клиента.
            if (player == null || !player.IsOwner) return false;

            bool consumed = false;

            // Идём с конца: при удалении элемента индекс не сдвигает ещё не проверенные.
            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                MotionBricksBakedRootMotion source = Sources[i];
                if (source == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (source.BoundPlayer != player || !source.CanDrivePlayer) continue;

                displacement += source.ConsumeBridgeDisplacement();
                consumed = true;
            }

            return consumed;
        }
    }
}
