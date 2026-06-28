// Project C: Real-Time Combat Engine — T-RTC10
// TargetingService: raycast от камеры (или character controller) для поиска цели под прицелом.
// Phase 1: physics raycast → hit любой Collider с компонентом IDamageTarget (через GetComponentInParent).
// Phase 2: server-side validation (anti-cheat) — клиент шлёт targetId, сервер проверяет дистанцию/raycast.
//
// Использование:
//   if (TargetingService.TryGetTargetFromCamera(_camera, transform, 30f, ~0, out var target, out var hit))
//   {
//       ulong targetId = target.GetTargetId();
//       CombatServer.Instance.RequestAttackRpc(targetId, 0UL);
//   }

using UnityEngine;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// T-RTC10: static raycast helper для targeting.
    /// Phase 1: client-side raycast (own attacks only). Phase 2: server-side re-raycast.
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
                // Hit может быть на child collider (hand bone и т.п.) → GetComponentInParent.
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

            // Fallback: от transform (для отладки / debug camera)
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
    }
}