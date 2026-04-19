using UnityEngine;
using System.Collections.Generic;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// Объёмная зона ветра — триггерный коллайдер, применяющий силу ветра к кораблям внутри.
    /// Требует Collider с IsTrigger = true.
    /// Визуализируется в Scene view через Gizmos (стрелка направления, цвет по силе).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WindZone : MonoBehaviour
    {
        [Header("Данные Зоны")]
        [Tooltip("ScriptableObject с параметрами ветра")]
        public WindZoneData windData;

        // Зарегистрированные корабли внутри зоны
        private HashSet<ShipController> _shipsInZone = new HashSet<ShipController>();

        private void Awake()
        {
            // Убеждаемся, что коллайдер — триггер
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ищем ShipController на самом объекте или выше (другой объект вошёл в триггер)
            var ship = other.GetComponent<ShipController>();
            if (ship == null)
                ship = other.GetComponentInParent<ShipController>();
            if (ship == null)
                ship = other.GetComponentInChildren<ShipController>();

            if (ship != null)
            {
                if (!_shipsInZone.Contains(ship))
                {
                    _shipsInZone.Add(ship);
                    ship.RegisterWindZone(this);
                }
            }
            else
            {
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var ship = other.GetComponent<ShipController>();
            if (ship == null)
                ship = other.GetComponentInParent<ShipController>();
            if (ship == null)
                ship = other.GetComponentInChildren<ShipController>();

            if (ship != null)
            {
                _shipsInZone.Remove(ship);
                ship.UnregisterWindZone(this);
            }
        }

        /// <summary>
        /// Рассчитать силу ветра в данной позиции.
        /// Возвращает Vector3 силы (в ньютонах), готовый для AddForce.
        /// </summary>
        public Vector3 GetWindForceAtPosition(Vector3 position)
        {
            if (windData == null)
                return Vector3.zero;

            Vector3 force = Vector3.zero;

            switch (windData.profile)
            {
                case WindProfile.Constant:
                    force = windData.windDirection.normalized * windData.windForce;
                    break;

                case WindProfile.Gust:
                    // Базовая сила + синусоидальные порывы
                    float gustFactor = Mathf.Sin(Time.time * (2f * Mathf.PI) / windData.gustInterval);
                    float variation = gustFactor * windData.windVariation;
                    float totalForce = windData.windForce * (1f + variation);
                    force = windData.windDirection.normalized * totalForce;
                    break;

                case WindProfile.Shear:
                    // Сила зависит от высоты (Y позиция)
                    float shearBoost = position.y * windData.shearGradient;
                    float shearTotal = windData.windForce + shearBoost;
                    force = windData.windDirection.normalized * shearTotal;
                    break;
            }

            return force;
        }

        /// <summary>
        /// Применить силу ветра ко всем кораблям в зоне.
        /// Вызывается с сервера (например, из FixedUpdate ShipController или отдельного менеджера).
        /// </summary>
        public void ApplyWindToAllShips()
        {
            foreach (var ship in _shipsInZone)
            {
                if (ship == null) continue;

                Vector3 force = GetWindForceAtPosition(ship.transform.position);
                if (force.sqrMagnitude > 0.001f)
                {
                    ship.ApplyExternalForce(force);
                }
            }
        }

        /// <summary>
        /// Получить количество кораблей в зоне (для дебага).
        /// </summary>
        public int ShipCount => _shipsInZone.Count;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Определяем центр и размер коллайдера для визуализации
            var col = GetComponent<Collider>();
            if (col == null) return;

            Vector3 center = transform.position;
            Vector3 size = Vector3.one * 10f;

            // Определяем размер из коллайдера
            if (col is BoxCollider box)
            {
                center = transform.TransformPoint(box.center);
                size = Vector3.Scale(box.size, transform.lossyScale);
            }
            else if (col is SphereCollider sphere)
            {
                center = transform.TransformPoint(sphere.center);
                float radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                size = Vector3.one * radius * 2f;
            }

            // Цвет по силе ветра: синий (слабый) -> жёлтый (средний) -> красный (сильный)
            Color windColor;
            if (windData == null)
            {
                windColor = Color.gray;
            }
            else
            {
                float normalizedForce = Mathf.InverseLerp(0f, 200f, windData.windForce);
                windColor = Color.Lerp(Color.blue, Color.red, normalizedForce);
            }

            // Полупрозрачная зона
            Gizmos.color = new Color(windColor.r, windColor.g, windColor.b, 0.15f);
            Gizmos.DrawCube(center, size);

            // Контур зоны
            Gizmos.color = new Color(windColor.r, windColor.g, windColor.b, 0.5f);
            Gizmos.DrawWireCube(center, size);

            // Стрелка направления ветра
            if (windData != null && windData.windForce > 0f)
            {
                Vector3 dir = windData.windDirection.normalized;
                float arrowLength = Mathf.Clamp(windData.windForce * 0.5f, 1f, 20f);
                Vector3 start = center;
                Vector3 end = start + dir * arrowLength;

                // Линия
                Gizmos.color = windColor;
                Gizmos.DrawLine(start, end);

                // Наконечник стрелки
                Vector3 arrowHeadLength = dir * 1f;
                Vector3 arrowHeadRight = Quaternion.Euler(0f, 30f, 0f) * (-dir) * 1f;
                Vector3 arrowHeadLeft = Quaternion.Euler(0f, -30f, 0f) * (-dir) * 1f;

                Gizmos.DrawLine(end, end - arrowHeadLength + arrowHeadRight);
                Gizmos.DrawLine(end, end - arrowHeadLength + arrowHeadLeft);

                // Label с именем зоны
                UnityEditor.Handles.Label(end + Vector3.up * 1.5f,
                    windData.displayName + $" ({windData.windForce:F0}N)");
            }
        }
#endif
    }
}
