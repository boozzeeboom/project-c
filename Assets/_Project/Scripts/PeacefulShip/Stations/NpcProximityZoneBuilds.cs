// T-NS-BZ02: NpcProximityZoneBuilds — avoidance-зона вокруг статичного препятствия.
//
// Проблема: Collider.ClosestPoint не работает с не-convex MeshCollider (Unity limitation).
// Решение: компонент собирает ВСЕ Collider'ы на GameObject и его детях, использует
// только те, где ClosestPoint корректен (Box, Sphere, Capsule, convex Mesh).
//
// Использование:
//   1. На корень здания с MeshCollider вешаешь NpcProximityZoneBuilds
//   2. Добавляешь дочерние GameObject'ы с BoxCollider'ами, аппроксимируя форму здания
//   3. Настраиваешь avoidancePadding (отступ от этих коллайдеров)
//   4. Скрипт сам находит ближайшую точку среди всех валидных коллайдеров
//
// Регистрирует себя в NpcBuildZoneRegistry → NpcProximityZone.FindClosestBuildConflict.

using System.Collections.Generic;
using ProjectC.PeacefulShip.Network; // NpcBuildZoneRegistry
using UnityEngine;

namespace ProjectC.PeacefulShip.Stations
{
    public class NpcProximityZoneBuilds : MonoBehaviour
    {
        [Header("Avoidance")]
        [Tooltip("Отступ от поверхности коллайдеров: avoidance сработает когда avoidance-сфера\n" +
                 "корабля окажется ближе чем avoidancePadding к любому из коллайдеров.")]
        [Min(0f)] [SerializeField] private float avoidancePadding = 30f;

        [Header("Collider Scan")]
        [Tooltip("Искать коллайдеры в дочерних объектах (включи если BoxCollider'ы на детях).")]
        [SerializeField] private bool includeChildren = true;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        /// <summary>Коллайдеры, для которых ClosestPoint работает корректно.</summary>
        private readonly List<Collider> _validColliders = new List<Collider>();

        public float AvoidancePadding => avoidancePadding;

        private void Awake()
        {
            RefreshColliders();
        }

        private void OnEnable()
        {
            NpcBuildZoneRegistry.Register(this);
        }

        private void OnDisable()
        {
            NpcBuildZoneRegistry.Unregister(this);
        }

        /// <summary>Пересканировать коллайдеры (вызови после добавления/удаления дочерних коллайдеров).</summary>
        [ContextMenu("Refresh Colliders")]
        public void RefreshColliders()
        {
            _validColliders.Clear();
            var all = includeChildren
                ? GetComponentsInChildren<Collider>(true)
                : GetComponents<Collider>();

            foreach (var c in all)
            {
                if (c.isTrigger) continue;
                if (IsClosestPointSupported(c))
                    _validColliders.Add(c);
            }

            Debug.Log($"[NpcProximityZoneBuilds:{gameObject.name}] scanned: {all.Length} total, " +
                      $"{_validColliders.Count} valid (Box/Sphere/Capsule/convex-Mesh)", this);
        }

        /// <summary>ClosestPoint работает только для этих типов (документация Unity).</summary>
        private static bool IsClosestPointSupported(Collider c)
        {
            if (c is BoxCollider || c is SphereCollider || c is CapsuleCollider) return true;
            if (c is MeshCollider mc) return mc.convex;
            return false;
        }

        /// <summary>Ближайшая точка среди всех валидных коллайдеров к заданной позиции.</summary>
        public Vector3 ClosestPoint(Vector3 point)
        {
            Vector3 best = point;
            float bestDist = float.MaxValue;
            foreach (var c in _validColliders)
            {
                if (c == null) continue;
                Vector3 cp = c.ClosestPoint(point);
                float d = Vector3.Distance(point, cp);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cp;
                }
            }
            return best;
        }

        /// <summary>
        /// Пересекает ли avoidance-сфера корабля нашу avoidance-зону.
        /// Зона = поверхность любого валидного коллайдера + avoidancePadding.
        /// </summary>
        public bool IsIntruding(Vector3 shipPos, float shipAvoidRadius)
        {
            float threshold = shipAvoidRadius + avoidancePadding;
            foreach (var c in _validColliders)
            {
                if (c == null) continue;
                Vector3 cp = c.ClosestPoint(shipPos);
                if (Vector3.Distance(shipPos, cp) < threshold)
                    return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Валидные коллайдеры — оранжевый
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            foreach (var c in _validColliders)
            {
                if (c == null) continue;
                Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
            }

            // Если ни одного валидного — рисуем красную сферу (предупреждение)
            if (_validColliders.Count == 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position, 3f);
                UnityEditor.Handles.Label(transform.position + Vector3.up * 4f,
                    "NO VALID COLLIDERS!\nAdd BoxCollider child");
            }
            else
            {
                UnityEditor.Handles.Label(
                    _validColliders[0].bounds.center + Vector3.up * 3f,
                    $"BuildAvoid +{avoidancePadding:F0}m [{_validColliders.Count} colliders]");
            }
        }
#endif
    }
}
