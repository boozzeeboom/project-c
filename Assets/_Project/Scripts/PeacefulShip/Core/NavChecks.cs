// T-NS M3.1: NavChecks — статические spatial-условия для навигации NPC.
// Каждая проверка = чистое измерение (Vector3.Distance, угол, IsShipInside).
// НИКАКИХ magic numbers внутри — все пороги приходят из NavConfig / scene values.
//
// Документация: docs/NPC_others_peacfull/pc_ship/M2_FSM_DIAGNOSIS.md §3.3

using ProjectC.Docking.Stations;
using ProjectC.Docking.Zones;
using ProjectC.Player;
using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Чистые spatial-условия для FSM навигации. Никаких magic numbers.
    /// Все пороги параметризованы через NavConfig (в NpcShipController).
    /// </summary>
    public static class NavChecks
    {
        /// <summary>Стоим ли в зоне связи OuterCommZone?</summary>
        /// <remarks>Источник истины — Unity OnTriggerEnter + sphere OverlapSphere polling.
        /// Внутри FSM: пока NPC "в зоне" — он замедляется, запрашивает pad.</remarks>
        public static bool IsInCommZone(Vector3 shipPos, OuterCommZone zone)
        {
            if (zone == null) return false;
            float r = zone.CommRange;
            if (r <= 0f) return false;
            return Vector3.Distance(shipPos, zone.transform.position) <= r;
        }

        /// <summary>Yaw выровнен? С гистерезисом (entryThreshold > exitThreshold).</summary>
        /// <param name="shipYawDeg">Текущий yaw корабля (градусы, 0..360).</param>
        /// <param name="targetBearingDeg">Желаемый bearing (градусы).</param>
        /// <param name="wasAligned">Были ли выровнены в прошлом тике.</param>
        /// <param name="entryThresholdDeg">Порог входа в "aligned" (напр. 15°).</param>
        /// <param name="exitThresholdDeg">Порог выхода из "aligned" (напр. 5° — hysteresis).</param>
        public static bool IsYawAligned(float shipYawDeg, float targetBearingDeg, bool wasAligned,
                                       float entryThresholdDeg, float exitThresholdDeg)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(shipYawDeg, targetBearingDeg));
            float threshold = wasAligned ? exitThresholdDeg : entryThresholdDeg;
            return diff < threshold;
        }

        /// <summary>Набрали высоту (завершение Lifting)?</summary>
        public static bool IsLiftedTo(float currentY, float startY, float targetClearanceMeters)
        {
            return currentY >= startY + targetClearanceMeters;
        }

        /// <summary>Долетели до цели (вход в arrivalRange)?</summary>
        public static bool IsAtRange(Vector3 pos, Vector3 target, float rangeMeters)
        {
            return Vector3.Distance(pos, target) <= rangeMeters;
        }

        /// <summary>Trigger-бокс пада содержит корабль? (Unity OnTriggerEnter установил IsShipInside=true).</summary>
        /// <remarks>Единственный надёжный trigger-detection в Unity.
        /// Spatial check (BoxCollider.bounds.Contains) НЕ надёжен — физика обрабатывает это лучше.</remarks>
        public static bool IsInsidePadTrigger(ShipController ship, DockingPadTriggerBox pad)
        {
            return pad != null && pad.IsShipInside;
        }

        /// <summary>Bearing от fromPos к toPos (в градусах, 0=север, 90=восток).</summary>
        public static float BearingDegrees(Vector3 fromPos, Vector3 toPos)
        {
            Vector3 dir = toPos - fromPos;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>Горизонтальная дистанция (XZ) между точками (Y игнорируется).</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(
                new Vector3(a.x, 0f, a.z),
                new Vector3(b.x, 0f, b.z));
        }
    }
}
