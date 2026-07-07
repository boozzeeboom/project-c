// Project C: Ranged Combat — Phase R2
// ProjectileVisual: client-side visual только (НЕ NetworkBehaviour).
// Спавнится на клиенте при получении AttackLanded event'а (ranged атака).
// Простая интерполяция attacker.position → target.position с destroy по прибытии.
//
// Design: docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md
//
// Использование:
//   ProjectileVisual.Fire(attackerPos, targetPos, travelTimeSec, Color.yellow);
//
// Спавнит простой Cylinder-меш (стрела) и анимирует полёт.

using UnityEngine;

namespace ProjectC.Combat.Client
{
    /// <summary>
    /// Client-side only projectile visual. Локальный эффект — не синхронизируется по сети.
    /// </summary>
    public class ProjectileVisual : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private float _trailWidth = 0.08f;
        [SerializeField] private float _trailLength = 0.3f;

        private Vector3 _start;
        private Vector3 _end;
        private float _duration;
        private float _elapsed;
        private LineRenderer _trail;

        /// <summary>
        /// Создать и запустить projectile visual.
        /// </summary>
        /// <param name="from">Позиция атакующего.</param>
        /// <param name="to">Позиция цели.</param>
        /// <param name="travelTimeSec">Время полёта (0.15-0.5 сек — быстро).</param>
        /// <param name="color">Цвет trail/снаряда.</param>
        public static ProjectileVisual Fire(Vector3 from, Vector3 to, float travelTimeSec, Color color)
        {
            var go = new GameObject("ProjectileVisual");
            var pv = go.AddComponent<ProjectileVisual>();
            pv._start = from;
            pv._end = to;
            pv._duration = Mathf.Max(0.05f, travelTimeSec);

            // Trail (LineRenderer)
            pv._trail = go.AddComponent<LineRenderer>();
            pv._trail.startWidth = pv._trailWidth;
            pv._trail.endWidth = 0f;
            pv._trail.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            pv._trail.material.color = color;
            pv._trail.positionCount = 2;

            // Small sphere at head (optional visual marker)
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(go.transform);
            sphere.transform.localScale = Vector3.one * 0.12f;
            sphere.transform.localPosition = Vector3.zero;
            var sphereRenderer = sphere.GetComponent<MeshRenderer>();
            if (sphereRenderer != null)
            {
                sphereRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                sphereRenderer.material.color = color;
            }
            Destroy(sphere.GetComponent<Collider>());

            return pv;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // Ease-out (fast start, slow arrival)
            float eased = 1f - (1f - t) * (1f - t);
            Vector3 pos = Vector3.Lerp(_start, _end, eased);

            // Small arc
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.5f;

            transform.position = pos;

            // Trail: from current pos back towards start
            Vector3 trailEnd = Vector3.Lerp(_start, pos, 1f - _trailLength / Vector3.Distance(_start, _end));
            _trail.SetPosition(0, pos);
            _trail.SetPosition(1, trailEnd);

            if (t >= 1f)
            {
                // Small impact puff — instant destroy is fine for MVP
                Destroy(gameObject);
            }
        }
    }
}
