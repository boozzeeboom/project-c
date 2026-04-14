using UnityEngine;

namespace ProjectC.World.Core
{
    /// <summary>
    /// Floating Origin — предотвращает floating point jitter при больших координатах.
    ///
    /// ПРОБЛЕМА:
    /// - При координатах >100,000 units float теряет точность
    /// - Вершины мешей начинают дрожать (jitter)
    /// - Камера трясётся при движении
    ///
    /// РЕШЕНИЕ:
    /// - Когда камера уходит дальше чем threshold от origin,
    ///   сдвигаем ВЕСЬ мир обратно к (0,0,0)
    /// - Камера всегда близко к origin → float точность сохраняется
    ///
    /// ИСПОЛЬЗОВАНИЕ:
    /// 1. Повесить на Camera (или root object сцены)
    /// 2. Assign worldRoot (GameObject "Mountains" и другие мировые объекты)
    /// 3. Настроить threshold (по умолчанию 100,000 units)
    ///
    /// ВАЖНО:
    /// - НЕ двигает саму камеру — двигает мир
    /// - Физика может сломаться если есть Rigidbody — нужен отдельный handling
    /// - Audio sources с 3D spatialization могут щёлкать
    /// </summary>
    public class FloatingOrigin : MonoBehaviour
    {
        [Header("World Root")]
        [Tooltip("Корневой объект мира (Mountains, Farms, и т.д.)")]
        public Transform worldRoot;

        [Header("Threshold")]
        [Tooltip("Расстояние от origin после которого сдвигаем мир")]
        public float threshold = 100000f;

        [Header("Debug")]
        [Tooltip("Показать debug логи")]
        public bool showDebugLogs = false;

        private Camera _camera;
        private Vector3 _totalOffset = Vector3.zero;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                Debug.LogError("[FloatingOrigin] Camera not found on this GameObject!");
                enabled = false;
                return;
            }

            if (worldRoot == null)
            {
                // Тихий поиск world root без спама warning'ов
                GameObject mountains = GameObject.Find("Mountains");
                if (mountains != null)
                {
                    worldRoot = mountains.transform;
                }
                else
                {
                    GameObject world = GameObject.Find("World");
                    if (world == null) world = GameObject.Find("Terrain");
                    if (world == null) world = GameObject.Find("Peaks");

                    if (world != null)
                    {
                        worldRoot = world.transform;
                    }
                    else
                    {
                        GameObject[] allRoots = gameObject.scene.GetRootGameObjects();
                        GameObject bestRoot = null;
                        int maxChildren = 0;

                        foreach (var root in allRoots)
                        {
                            if (root == gameObject || root.GetComponent<Camera>() != null) continue;

                            int childCount = root.transform.childCount;
                            if (childCount > maxChildren)
                            {
                                maxChildren = childCount;
                                bestRoot = root;
                            }
                        }

                        if (bestRoot != null && maxChildren > 0)
                        {
                            worldRoot = bestRoot.transform;
                        }
                        else
                        {
                            // Создаём World только если действительно ничего не найдено
                            GameObject worldObj = new GameObject("World");
                            worldRoot = worldObj.transform;
                        }
                    }
                }
                
                if (showDebugLogs)
                {
                    Debug.Log($"[FloatingOrigin] Auto-resolved world root: {worldRoot.name}");
                }
            }

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOrigin] Initialized. threshold={threshold:N0}, worldRoot={worldRoot.name}");
            }
        }

        void LateUpdate()
        {
            if (worldRoot == null) return;

            Vector3 cameraWorldPos = _camera.transform.position;

            // Проверяем нужно ли сдвигать
            if (Mathf.Abs(cameraWorldPos.x) > threshold ||
                Mathf.Abs(cameraWorldPos.y) > threshold ||
                Mathf.Abs(cameraWorldPos.z) > threshold)
            {
                // Вычисляем offset (округляем для избежания accumulation errors)
                Vector3 offset = new Vector3(
                    Mathf.Round(cameraWorldPos.x),
                    Mathf.Round(cameraWorldPos.y),
                    Mathf.Round(cameraWorldPos.z)
                );

                // Сдвигаем ВЕСЬ мир
                worldRoot.position -= offset;

                // Сохраняем total offset для отладки
                _totalOffset += offset;

                if (showDebugLogs)
                {
                    Debug.Log($"[FloatingOrigin] Shifted world by offset={offset}, " +
                              $"cameraPos={cameraWorldPos} → newCameraPos={_camera.transform.position}, " +
                              $"totalOffset={_totalOffset}");
                }
            }
        }

        /// <summary>
        /// Ручной сброс origin (например, при телепортации).
        /// КРИТИЧНО: Вызывать ДО телепортации камеры, а не ПОСЛЕ.
        /// Это сдвигает мир так чтобы камера осталась близко к (0,0,0).
        /// </summary>
        public void ResetOrigin()
        {
            if (worldRoot == null)
            {
                Debug.LogWarning("[FloatingOrigin] ResetOrigin called but worldRoot is null!");
                return;
            }

            Vector3 cameraPos = _camera.transform.position;
            Vector3 offset = new Vector3(
                Mathf.Round(cameraPos.x),
                Mathf.Round(cameraPos.y),
                Mathf.Round(cameraPos.z)
            );

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOrigin] Before ResetOrigin: " +
                          $"cameraPos={cameraPos:F0}, worldRootPos={worldRoot.position:F0}, " +
                          $"offset={offset:F0}");
            }

            worldRoot.position -= offset;
            _totalOffset += offset;

            // Проверяем что камера теперь близко к origin
            Vector3 newCameraPos = _camera.transform.position;
            float distFromOrigin = newCameraPos.magnitude;

            if (showDebugLogs)
            {
                Debug.Log($"[FloatingOrigin] After ResetOrigin: " +
                          $"newCameraPos={newCameraPos:F0}, distFromOrigin={distFromOrigin:F0}, " +
                          $"worldRootPos={worldRoot.position:F0}, totalOffset={_totalOffset:F0}");
            }

            if (distFromOrigin > threshold)
            {
                Debug.LogWarning($"[FloatingOrigin] Camera still far from origin after reset! " +
                                 $"distFromOrigin={distFromOrigin:F0} > threshold={threshold:F0}. " +
                                 $"This may cause rendering issues.");
            }
        }

        /// <summary>
        /// Получить текущий суммарный offset (для отладки).
        /// </summary>
        public Vector3 GetTotalOffset()
        {
            return _totalOffset;
        }

#if UNITY_EDITOR
        void OnGUI()
        {
            if (!showDebugLogs) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"[FloatingOrigin] Total Offset: {_totalOffset:F0}");
            GUILayout.Label($"Camera Pos: {_camera.transform.position:F0}");
            GUILayout.Label($"World Root Pos: {worldRoot.position:F0}");
            GUILayout.EndArea();
        }
#endif
    }
}
