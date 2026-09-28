// T-NS-AIR01: сегмент глобальной магистрали (дизайн 17_AIRWAYS_DESIGN.md).
// Вешается на GameObject-бокс в сцене (корень WorldScene_* — едет со сдвигом
// бесплатно). Дизайнер рисует цепочки перекрывающихся боксов; связи —
// автоматически по перекрытию объёмов + явные links для развязок.
// Server-only использование (читает AirwayDirectory при планировании).

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.PeacefulShip.Stations
{
    /// <summary>
    /// Один бокс-объём воздушной трассы. Габариты — из BoxCollider (если есть)
    /// или из скейла юнит-куба. Лор-имя — в lineId/displayName.
    /// </summary>
    public class AirwaySegment : MonoBehaviour
    {
        [Tooltip("Линия магистрали (напр. NORTH_TRUNK) — организация + лог.")]
        public string lineId = "TRUNK";

        [Tooltip("Отображаемое имя (лор: название трассы).")]
        public string displayName = "";

        [Tooltip("Явные связи (развязки через разрыв). Авто-связи по перекрытию — всегда.")]
        public List<AirwaySegment> links = new List<AirwaySegment>();

        /// <summary>Мировой центр объёма (живём — едет со сценой, F8-безопасно).</summary>
        public Vector3 LiveCenter()
        {
            var bc = GetComponent<BoxCollider>();
            if (bc != null) return transform.TransformPoint(bc.center);
            return transform.position;
        }

        /// <summary>Мировые габариты объёма (для авто-связей).</summary>
        public Bounds LiveBounds()
        {
            var bc = GetComponent<BoxCollider>();
            if (bc != null)
            {
                Vector3 c = transform.TransformPoint(bc.center);
                Vector3 sz = Vector3.Scale(bc.size, transform.lossyScale);
                return new Bounds(c, sz);
            }
            return new Bounds(transform.position, transform.lossyScale);
        }
    }
}
