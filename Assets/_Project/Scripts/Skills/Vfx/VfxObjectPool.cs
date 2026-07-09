// Project C: Skills VFX — Phase 1
// VfxObjectPool: общий пул GameObject'ов для VFX.
// По паттерну DamageNumberService — prewarm при старте, expandable.
// Ключ — сам префаб (GameObject reference).
//
// 2D-ready: в Phase 3 появится SpriteVfxPool с аналогичным интерфейсом.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Skills.Vfx
{
    /// <summary>
    /// Object pool для VFX-префабов. Минимизирует GC от Instantiate/Destroy.
    /// </summary>
    public class VfxObjectPool
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();
        private readonly Transform _poolRoot;

        private const int DefaultPrewarm = 3;

        public VfxObjectPool(Transform poolRoot)
        {
            _poolRoot = poolRoot;
        }

        /// <summary>
        /// Взять экземпляр из пула или создать новый (с Prewarm).
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[VfxObjectPool] Get: prefab is null");
                return null;
            }

            if (!_pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefab] = queue;
                Prewarm(prefab, queue, DefaultPrewarm);
            }

            GameObject instance;
            if (queue.Count > 0)
            {
                instance = queue.Dequeue();
                if (instance != null)
                {
                    instance.transform.SetPositionAndRotation(position, rotation);
                    instance.SetActive(true);
                }
                else
                {
                    // Stale reference — создаём новый
                    instance = Object.Instantiate(prefab, position, rotation, _poolRoot);
                }
            }
            else
            {
                instance = Object.Instantiate(prefab, position, rotation, _poolRoot);
            }

            if (instance == null)
                Debug.LogWarning($"[VfxObjectPool] Get: failed to instantiate prefab '{prefab.name}'");

            return instance;
        }

        /// <summary>
        /// Вернуть экземпляр в пул.
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            instance.transform.SetParent(_poolRoot);
        }

        /// <summary>
        /// Очистить все пулы.
        /// </summary>
        public void Clear()
        {
            foreach (var kv in _pools)
            {
                while (kv.Value.Count > 0)
                {
                    var obj = kv.Value.Dequeue();
                    if (obj != null) Object.Destroy(obj);
                }
            }
            _pools.Clear();
        }

        private void Prewarm(GameObject prefab, Queue<GameObject> queue, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, _poolRoot);
                if (instance != null)
                {
                    instance.SetActive(false);
                    queue.Enqueue(instance);
                }
            }
        }
    }
}
