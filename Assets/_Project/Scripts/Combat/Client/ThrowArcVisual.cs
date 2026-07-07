// Project C: Thrown Combat — Phase T3
// ThrowArcVisual: client-side visual только (НЕ NetworkBehaviour).
// Спавнится при активации thrown-навыка. Анимирует полёт гранаты по параболической дуге
// от игрока к targetPoint. При прибытии — explosion VFX (sphere flash).
//
// Design: docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md
//
// Использование:
//   ThrowArcVisual.Fire(fromPos, targetPoint, flightTimeSec, explosionRadius, damageTypeColor);

using System.Collections;
using UnityEngine;

namespace ProjectC.Combat.Client
{
    /// <summary>
    /// Client-side only thrown item visual. Локальный эффект — не синхронизируется по сети.
    /// </summary>
    public class ThrowArcVisual : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private float _arcHeight = 4f;
        [SerializeField] private float _grenadeScale = 0.2f;

        private Vector3 _start;
        private Vector3 _end;
        private float _duration;
        private float _elapsed;
        private float _explosionRadius;
        private Color _color;
        private GameObject _grenadeObj;
        private LineRenderer _trail;

        /// <summary>
        /// Создать и запустить throw arc visual.
        /// </summary>
        /// <param name="from">Позиция бросающего.</param>
        /// <param name="to">Точка приземления (где взрыв).</param>
        /// <param name="flightTimeSec">Время полёта (0.5-1.5 сек).</param>
        /// <param name="explosionRadius">Радиус взрыва для VFX (метры).</param>
        /// <param name="color">Цвет explosion VFX.</param>
        public static ThrowArcVisual Fire(Vector3 from, Vector3 to, float flightTimeSec, float explosionRadius, Color color)
        {
            var go = new GameObject("ThrowArcVisual");
            var tav = go.AddComponent<ThrowArcVisual>();
            tav._start = from + Vector3.up * 1.2f;
            tav._end = to;
            tav._duration = Mathf.Max(0.3f, flightTimeSec);
            tav._explosionRadius = Mathf.Max(0.5f, explosionRadius);
            tav._color = color;

            // Grenade object (small sphere)
            tav._grenadeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tav._grenadeObj.transform.SetParent(go.transform);
            tav._grenadeObj.transform.localScale = Vector3.one * tav._grenadeScale;
            tav._grenadeObj.transform.localPosition = Vector3.zero;
            var rend = tav._grenadeObj.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                rend.material.color = color;
            }
            Destroy(tav._grenadeObj.GetComponent<Collider>());

            // Trail line from start
            tav._trail = go.AddComponent<LineRenderer>();
            tav._trail.startWidth = 0.04f;
            tav._trail.endWidth = 0f;
            tav._trail.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            tav._trail.material.color = new Color(color.r, color.g, color.b, 0.5f);

            return tav;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // Parabolic arc
            Vector3 flatDir = _end - _start;
            flatDir.y = 0;
            float flatDist = flatDir.magnitude;

            Vector3 pos = Vector3.Lerp(_start, _end, t);
            // Arc: height = sin(π*t) * arcHeight
            pos.y += Mathf.Sin(t * Mathf.PI) * _arcHeight;



            transform.position = pos;

            // Update trail
            _trail.positionCount = 2;
            _trail.SetPosition(0, pos);
            _trail.SetPosition(1, _start);

            if (t >= 1f)
            {
                // Explosion VFX
                StartCoroutine(ExplosionEffect());
                // Stop Update from running further
                enabled = false;
            }
        }

        private IEnumerator ExplosionEffect()
        {
            // Hide grenade
            if (_grenadeObj != null)
                _grenadeObj.SetActive(false);

            if (_trail != null)
                _trail.enabled = false;

            // Create explosion sphere
            var expGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            expGo.transform.position = _end;
            expGo.transform.localScale = Vector3.one * 0.1f;
            var expRend = expGo.GetComponent<MeshRenderer>();
            if (expRend != null)
            {
                expRend.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                expRend.material.color = _color;
            }
            Destroy(expGo.GetComponent<Collider>());

            // Expand + fade
            float expandTime = 0.3f;
            float elapsed = 0f;
            while (elapsed < expandTime)
            {
                elapsed += Time.deltaTime;
                float et = elapsed / expandTime;
                float scale = Mathf.Lerp(0.1f, _explosionRadius, et);
                expGo.transform.localScale = Vector3.one * scale;
                if (expRend != null)
                {
                    Color c = _color;
                    c.a = 1f - et;
                    expRend.material.color = c;
                }
                yield return null;
            }

            Destroy(expGo);
            Destroy(gameObject);
        }
    }
}
