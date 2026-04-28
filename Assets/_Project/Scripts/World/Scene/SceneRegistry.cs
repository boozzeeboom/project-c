using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World.Scene
{
    /// <summary>
    /// ScriptableObject реестр всех сцен в мире.
    /// Содержит metadata о сетке сцен и helper методы для lookup.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneRegistry", menuName = "ProjectC/World/Scene Registry")]
    public class SceneRegistry : ScriptableObject
    {
        [Header("Grid Configuration")]
        [Tooltip("Количество сцен по X (columns - параллели, горизонтальный wrap)")]
        public int GridColumns = 6;

        [Tooltip("Количество сцен по Z (rows - меридианы, вертикальная блокировка полюсов)")]
        public int GridRows = 4;

        [Header("Scene Naming")]
        [Tooltip("Префикс имени сцены (например 'WorldScene_' -> 'WorldScene_0_1')")]
        public string SceneNamePrefix = "WorldScene_";

        [Tooltip("Использовать Additive загрузку (всегда true для этой архитектуры)")]
        public bool UseAdditiveLoading = true;

        /// <summary>
        /// Получить имя сцены для данного SceneID.
        /// </summary>
        public string GetSceneName(SceneID sceneId)
        {
            if (!IsValid(sceneId))
            {
                Debug.LogWarning($"[SceneRegistry] Invalid SceneID: {sceneId}");
                return string.Empty;
            }
            return $"{SceneNamePrefix}{sceneId.GridX}_{sceneId.GridZ}";
        }

        /// <summary>
        /// Проверить валидность SceneID в пределах сетки.
        /// </summary>
        public bool IsValid(SceneID sceneId)
        {
            return sceneId.GridX >= 0 && sceneId.GridX < GridColumns &&
                   sceneId.GridZ >= 0 && sceneId.GridZ < GridRows;
        }

        /// <summary>
        /// Проверить валидность координат сцены.
        /// </summary>
        public bool IsValid(int gridX, int gridZ)
        {
            return gridX >= 0 && gridX < GridColumns && gridZ >= 0 && gridZ < GridRows;
        }

        /// <summary>
        /// Получить все SceneID в сетке.
        /// </summary>
        public IEnumerable<SceneID> GetAllSceneIDs()
        {
            for (int x = 0; x < GridColumns; x++)
                for (int z = 0; z < GridRows; z++)
                    yield return new SceneID(x, z);
        }

        /// <summary>
        /// Получить 3x3 сетку сцен вокруг центра (включая центр).
        /// </summary>
        public List<SceneID> GetSceneGrid3x3(SceneID center)
        {
            var result = new List<SceneID>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var id = new SceneID(center.GridX + dx, center.GridZ + dz);
                    if (IsValid(id))
                        result.Add(id);
                }
            return result;
        }

        /// <summary>
        /// Получить 5x5 сетку сцен вокруг центра (для выгрузки).
        /// </summary>
        public List<SceneID> GetSceneGrid5x5(SceneID center)
        {
            var result = new List<SceneID>();
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    var id = new SceneID(center.GridX + dx, center.GridZ + dz);
                    if (IsValid(id))
                        result.Add(id);
                }
            return result;
        }

        /// <summary>
        /// Проверить является ли сцена соседней к данной.
        /// </summary>
        public bool IsAdjacent(SceneID a, SceneID b)
        {
            int dx = Mathf.Abs(a.GridX - b.GridX);
            int dz = Mathf.Abs(a.GridZ - b.GridZ);
            return (dx <= 1 && dz <= 1) && (dx + dz > 0);
        }

        /// <summary>
        /// Получить направление от одной сцены к другой.
        /// </summary>
        public Direction? GetDirection(SceneID from, SceneID to)
        {
            if (to.GridX > from.GridX) return Direction.X_plus;
            if (to.GridX < from.GridX) return Direction.X_minus;
            if (to.GridZ > from.GridZ) return Direction.Z_plus;
            if (to.GridZ < from.GridZ) return Direction.Z_minus;
            return null;
        }

        /// <summary>
        /// Валидация конфигурации (вызывать в editor).
        /// </summary>
        public bool ValidateConfiguration()
        {
            if (GridColumns <= 0 || GridRows <= 0)
            {
                Debug.LogError("[SceneRegistry] Grid dimensions must be positive!");
                return false;
            }

            if (string.IsNullOrEmpty(SceneNamePrefix))
            {
                Debug.LogError("[SceneRegistry] SceneNamePrefix cannot be empty!");
                return false;
            }

            return true;
        }
    }
}