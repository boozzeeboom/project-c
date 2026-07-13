using System.Collections.Generic;
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.World
{
    /// <summary>
    /// Менеджер точек респавна.
    /// Размещается на GameObject в BootstrapScene.
    /// Конфигурируется через кастомный инспектор RespawnManagerEditor.
    /// </summary>
    [DisallowMultipleComponent]
    public class RespawnManager : MonoBehaviour
    {
        [Header("Respawn Points")]
        [Tooltip("Список точек респавна. Индекс 0 — fallback по умолчанию.")]
        [SerializeField] private List<RespawnPointData> _respawnPoints = new List<RespawnPointData>();

        /// <summary>Количество зарегистрированных точек.</summary>
        public int Count => _respawnPoints.Count;

        /// <summary>
        /// Возвращает эффективную позицию точки по индексу:
        /// spawnPoint.position если назначен, иначе fallbackPosition.
        /// </summary>
        public Vector3 GetEffectivePosition(int index)
        {
            if (index < 0 || index >= _respawnPoints.Count)
            {
                Debug.LogWarning($"[RespawnManager] Index {index} out of range [0, {_respawnPoints.Count - 1}], returning Vector3.zero");
                return Vector3.zero;
            }

            return _respawnPoints[index].GetEffectivePosition();
        }

        /// <summary>
        /// Ищет индекс точки по триггер-зоне.
        /// Возвращает -1 если не найдено.
        /// </summary>
        public int FindRespawnIndex(Collider trigger)
        {
            for (int i = 0; i < _respawnPoints.Count; i++)
            {
                if (_respawnPoints[i].triggerZone == trigger)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Возвращает fallback-точку (индекс 0).
        /// Если список пуст — предупреждение и Vector3.zero.
        /// </summary>
        public Vector3 GetFallbackPosition()
        {
            if (_respawnPoints.Count == 0)
            {
                Debug.LogWarning("[RespawnManager] No respawn points configured, returning Vector3.zero");
                return Vector3.zero;
            }

            return _respawnPoints[0].GetEffectivePosition();
        }

        // API для Editor
        public void AddPoint() => _respawnPoints.Add(new RespawnPointData());
        public void RemovePoint(int index) { if (index >= 0 && index < _respawnPoints.Count) _respawnPoints.RemoveAt(index); }
        public RespawnPointData GetPoint(int index) => (index >= 0 && index < _respawnPoints.Count) ? _respawnPoints[index] : default;
        public void SetPoint(int index, RespawnPointData data) { if (index >= 0 && index < _respawnPoints.Count) _respawnPoints[index] = data; }
    }
}
