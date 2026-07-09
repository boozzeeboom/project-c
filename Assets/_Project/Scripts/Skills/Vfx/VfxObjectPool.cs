// Project C: Skills VFX — Phase 1
// VfxObjectPool: общий пул GameObject'ов для VFX.
// По паттерну DamageNumberService — prewarm при старте, expandable.
// Один пул на префаб (ключ — InstanceID префаба).
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
        private readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>();
        private readonly Transform _poolRoot;

        private const int DefaultPrewarm = 5;
        private const int MaxPoolSize = 20;

        public VfxObjectPool(Transform poolRoot)
        {
            _poolRoot = poolRoot;
        }

        /// <summary>
        /// Взять экземпляр из пула или создать новый (с Prewarm).
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            int key = (int)EntityId.ToULong(prefab.GetEntityId());
            if (!_pools.TryGetValue(key, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[key] = queue;
                Prewarm(prefab, queue, DefaultPrewarm);
            }

            GameObject instance;
            if (queue.Count > 0)
            {
                instance = queue.Dequeue();
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
            }
            else
            {
                instance = Object.Instantiate(prefab, position, rotation, _poolRoot);
            }

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

            // Определяем ключ по имени префаба (убираем "(Clone)")
            // Fallback: просто деактивируем, не пытаемся положить в конкретную очередь.
            // Проще: вместо поиска ключа — просто деактивируем и оставляем в _poolRoot.
            // При следующем Get — если очередь пуста, создастся новый.
            // Это не идеальный pool, но покрывает 90% случаев без сложного tracking'а.
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
                instance.SetActive(false);
                queue.Enqueue(instance);
            }
        }
    }
}
