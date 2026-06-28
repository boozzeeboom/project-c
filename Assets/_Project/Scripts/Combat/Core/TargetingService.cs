// Project C: Real-Time Combat Engine — T-RTC10 + T-INP-03
// TargetingService: raycast / AOE collection от камеры (или character controller) для поиска целей.
// Phase 1: physics raycast → hit любой Collider с компонентом IDamageTarget (через GetComponentInParent).
// Phase 2: server-side validation (anti-cheat) — клиент шлёт targetId, сервер проверяет дистанцию/raycast.
//
// Использование (single-target):
//   if (TargetingService.TryGetTargetFromCamera(_camera, transform, 30f, ~0, out var target, out var hit))
//   {
//       ulong targetId = target.GetTargetId();
//       CombatServer.Instance.RequestAttackRpc(targetId, 0UL);
//   }
//
// Использование (AOE, T-INP-03):
//   var results = new System.Collections.Generic.List<IDamageTarget>();
//   var hitPoints = new System.Collections.Generic.List<Vector3>();
//   int hitCount = TargetingService.CollectAoeTargets(
//       origin, forward,
//       ProjectC.Skills.AoeFormula.Cone, 2.5f, 60f, 0f,
//       30f, ~0, results, hitPoints);

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Skills;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// T-RTC10: static raycast / AOE helper для targeting.
    /// Phase 1: client-side (own attacks only). Phase 2: server-side re-raycast.
    /// </summary>
    public static class TargetingService
    {
        /// <summary>Default max raycast distance (matches legacy 15м DebugAttackNearestNpc).</summary>
        public const float DefaultMaxDistance = 30f;

        /// <summary>Default LayerMask = everything (без IgnoreRaycast слоя).</summary>
        public static LayerMask DefaultMask => ~0;

        /// <summary>
        /// Raycast от origin в direction на maxDistance. Returns IDamageTarget через GetComponentInParent.
        /// </summary>
        public static bool TryGetTarget(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            LayerMask mask,
            out IDamageTarget target,
            out Vector3 hitPoint)
        {
            target = null;
            hitPoint = Vector3.zero;

            if (direction.sqrMagnitude < 0.0001f) return false;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                target = hit.collider.GetComponentInParent<IDamageTarget>();
                return target != null;
            }
            return false;
        }

        /// <summary>
        /// Получить target от Camera (если есть) иначе от fallbackTransform.forward.
        /// Это и есть "прицеливание" для FPS/TPS — игрок целится туда, куда смотрит камера.
        /// </summary>
        public static bool TryGetTargetFromCamera(
            Camera cam,
            Transform fallbackTransform,
            float maxDistance,
            LayerMask mask,
            out IDamageTarget target,
            out Vector3 hitPoint)
        {
            target = null;
            hitPoint = Vector3.zero;

            if (cam != null)
            {
                return TryGetTarget(cam.transform.position, cam.transform.forward, maxDistance, mask, out target, out hitPoint);
            }

            if (fallbackTransform == null) return false;
            return TryGetTarget(fallbackTransform.position, fallbackTransform.forward, maxDistance, mask, out target, out hitPoint);
        }

        /// <summary>
        /// Overload без Camera: raycast от transform.position + transform.forward.
        /// Используется в NetworkPlayer когда Main Camera ещё не инициализирована (host start).
        /// </summary>
        public static bool TryGetTargetFromTransform(
            Transform originTransform,
            float maxDistance,
            LayerMask mask,
            out IDamageTarget target,
            out Vector3 hitPoint)
        {
            target = null;
            hitPoint = Vector3.zero;
            if (originTransform == null) return false;
            return TryGetTarget(originTransform.position, originTransform.forward, maxDistance, mask, out target, out hitPoint);
        }

        // ==================== T-INP-03: AOE Collection ====================
        //
        // Семантика параметров (по AoeFormula):
        //   Cone  → size = длина вперёд, coneAngleDeg = угол раскрытия, width = 0
        //   Sphere→ size = радиус вокруг origin, coneAngleDeg = 0, width = 0
        //   Line  → size = длина вперёд, coneAngleDeg = 0, width = ширина линии
        //   Box   → size = длина вперёд, coneAngleDeg = 0, width = ширина бокса
        //
        // Возвращает количество найденных целей. Caller передаёт уже-инициализированные списки (Clear() не делаем).
        // Hit points приблизительные (origin для sphere/box, hit.point для cone/line через Physics overlap).

        /// <summary>
        /// Собрать все IDamageTarget в AOE-зоне. Server-authoritative вариант (используется CombatServer).
        /// </summary>
        public static int CollectAoeTargets(
            Vector3 origin,
            Vector3 forward,
            AoeFormula formula,
            float size,
            float coneAngleDeg,
            float width,
            float maxDistance,
            LayerMask mask,
            List<IDamageTarget> outResults,
            List<Vector3> outHitPoints)
        {
            if (outResults == null || outHitPoints == null) return 0;

            // Normalize forward (server может прислать грязный vector после клиентского RPC).
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            // Dedupe по target.GetTargetId() (вдруг два collider'а на одной цели).
            var seen = new HashSet<ulong>();

            switch (formula)
            {
                case AoeFormula.SingleTarget:
                    if (TryGetTarget(origin, forward, maxDistance, mask, out var t, out var hp))
                    {
                        outResults.Add(t); outHitPoints.Add(hp);
                        return 1;
                    }
                    return 0;

                case AoeFormula.Cone:
                    return CollectCone(origin, forward, size, coneAngleDeg, mask, outResults, outHitPoints, seen);

                case AoeFormula.Sphere:
                    return CollectSphere(origin, size, mask, outResults, outHitPoints, seen);

                case AoeFormula.Line:
                    return CollectLine(origin, forward, size, width, mask, outResults, outHitPoints, seen);

                case AoeFormula.Box:
                    return CollectBox(origin, forward, size, width, maxDistance, mask, outResults, outHitPoints, seen);

                default:
                    return 0;
            }
        }

        // === Cone: OverlapSphere(origin, size) + dot-product filter ===
        private static int CollectCone(
            Vector3 origin, Vector3 forward, float length, float coneAngleDeg,
            LayerMask mask,
            List<IDamageTarget> outResults, List<Vector3> outHitPoints,
            HashSet<ulong> seen)
        {
            if (length <= 0f) return 0;
            float cosHalfAngle = Mathf.Cos(coneAngleDeg * 0.5f * Mathf.Deg2Rad);

            // OverlapSphere не учитывает направление — фильтруем по dot product после.
            var hits = Physics.OverlapSphere(origin, length, mask, QueryTriggerInteraction.Ignore);
            int count = 0;
            foreach (var col in hits)
            {
                var target = col.GetComponentInParent<IDamageTarget>();
                if (target == null) continue;
                var toTarget = target.GetPosition() - origin;
                if (toTarget.sqrMagnitude < 0.0001f) continue;
                float dot = Vector3.Dot(toTarget.normalized, forward);
                if (dot < cosHalfAngle) continue;
                ulong id = target.GetTargetId();
                if (!seen.Add(id)) continue;
                outResults.Add(target);
                outHitPoints.Add(origin + toTarget * 0.5f);  // приблизительный hit point
                count++;
            }
            return count;
        }

        // === Sphere: OverlapSphere вокруг origin ===
        private static int CollectSphere(
            Vector3 origin, float radius, LayerMask mask,
            List<IDamageTarget> outResults, List<Vector3> outHitPoints,
            HashSet<ulong> seen)
        {
            if (radius <= 0f) return 0;
            var hits = Physics.OverlapSphere(origin, radius, mask, QueryTriggerInteraction.Ignore);
            int count = 0;
            foreach (var col in hits)
            {
                var target = col.GetComponentInParent<IDamageTarget>();
                if (target == null) continue;
                ulong id = target.GetTargetId();
                if (!seen.Add(id)) continue;
                outResults.Add(target);
                outHitPoints.Add(origin);
                count++;
            }
            return count;
        }

        // === Line: BoxCast (узкая коробка) ИЛИ два OverlapSphere в начале и конце ===
        // Phase 1: OverlapCapsule — простой и достаточный для копья/древка.
        private static int CollectLine(
            Vector3 origin, Vector3 forward, float length, float width, LayerMask mask,
            List<IDamageTarget> outResults, List<Vector3> outHitPoints,
            HashSet<ulong> seen)
        {
            if (length <= 0f) return 0;
            float radius = Mathf.Max(0.1f, width * 0.5f);
            // OverlapCapsule: точка1, точка2, radius
            var hits = Physics.OverlapCapsule(origin, origin + forward * length, radius, mask, QueryTriggerInteraction.Ignore);
            int count = 0;
            foreach (var col in hits)
            {
                var target = col.GetComponentInParent<IDamageTarget>();
                if (target == null) continue;
                ulong id = target.GetTargetId();
                if (!seen.Add(id)) continue;
                outResults.Add(target);
                outHitPoints.Add(target.GetPosition());
                count++;
            }
            return count;
        }

        // === Box: OverlapBox (box volume от origin в направлении forward) ===
        private static int CollectBox(
            Vector3 origin, Vector3 forward, float length, float width, float maxDistance, LayerMask mask,
            List<IDamageTarget> outResults, List<Vector3> outHitPoints,
            HashSet<ulong> seen)
        {
            if (length <= 0f) return 0;
            // halfExtents = (width/2, ширина бокса для гранат/бросков, length/2)
            float halfW = Mathf.Max(0.1f, width * 0.5f);
            float halfL = length * 0.5f;
            Vector3 halfExtents = new Vector3(halfW, halfW, halfL);
            Vector3 center = origin + forward * halfL;
            Quaternion rot = Quaternion.LookRotation(forward);

            var hits = Physics.OverlapBox(center, halfExtents, rot, mask, QueryTriggerInteraction.Ignore);
            int count = 0;
            foreach (var col in hits)
            {
                var target = col.GetComponentInParent<IDamageTarget>();
                if (target == null) continue;
                // distance check (maxDistance от origin до target)
                if (Vector3.Distance(origin, target.GetPosition()) > maxDistance) continue;
                ulong id = target.GetTargetId();
                if (!seen.Add(id)) continue;
                outResults.Add(target);
                outHitPoints.Add(target.GetPosition());
                count++;
            }
            return count;
        }
    }
}
