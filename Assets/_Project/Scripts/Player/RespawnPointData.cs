using System;
using UnityEngine;

namespace ProjectC.Player
{
    /// <summary>
    /// Данные одной точки респавна.
    /// Используется RespawnManager для конфигурации.
    /// </summary>
    [Serializable]
    public struct RespawnPointData
    {
        [Header("Fallback")]
        [Tooltip("Позиция по умолчанию, если spawnPoint не назначен.")]
        public Vector3 fallbackPosition;

        [Header("Anchor")]
        [Tooltip("Пустышка-якорь в мире. Если назначен — используется его позиция, иначе fallbackPosition.")]
        public Transform spawnPoint;

        [Header("Trigger")]
        [Tooltip("Триггер-зона. При входе игрока в эту зону — его точка респавна меняется на этот элемент.")]
        public Collider triggerZone;

        /// <summary>
        /// Возвращает реальную позицию респавна: spawnPoint.position если назначен, иначе fallbackPosition.
        /// </summary>
        public readonly Vector3 GetEffectivePosition()
        {
            return spawnPoint != null ? spawnPoint.position : fallbackPosition;
        }
    }
}
