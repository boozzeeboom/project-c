using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Общий помощник «езды на движущейся платформе» (T-CREW-01).
    /// Используется и игроком (owner-side, <c>NetworkPlayer</c>), и NPC (server-side, <c>NpcBrain</c>):
    /// переносит райдера вместе с палубой — позиция + yaw вокруг мировой оси Y.
    /// Pitch/roll платформы НАМЕРЕННО игнорируются, чтобы райдера не опрокидывало.
    ///
    /// См. docs/NPC_others_peacfull/npc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md
    /// и docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md
    /// </summary>
    public static class PlatformRideHelper
    {
        /// <summary>
        /// SphereCast вниз. Возвращает корневой Transform движущейся платформы под ногами
        /// (attachedRigidbody, иначе сам collider) или null. Пустая маска (0) → null.
        /// </summary>
        public static Transform DetectPlatform(Vector3 origin, float radius, float castDistance, LayerMask mask)
        {
            if (mask == 0) return null;
            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit,
                    castDistance, mask, QueryTriggerInteraction.Ignore))
            {
                var rb = hit.collider.attachedRigidbody;
                return rb != null ? rb.transform : hit.collider.transform;
            }
            return null;
        }

        /// <summary>
        /// Мировая дельта переноса за кадр: смещение позиции палубы + (опц.) орбитальное
        /// смещение из-за поворота палубы по yaw вокруг её оси. <paramref name="deltaYaw"/> —
        /// курсовой поворот палубы за кадр (для доворота райдера). Pitch/roll НЕ учитываются.
        /// </summary>
        public static Vector3 ComputeCarryDelta(
            Transform platform, Vector3 riderPos,
            Vector3 lastPos, Quaternion lastRot,
            bool carryYaw, out float deltaYaw)
        {
            Vector3 deltaPos = platform.position - lastPos;
            deltaYaw = 0f;

            if (carryYaw)
            {
                float dy = Mathf.DeltaAngle(lastRot.eulerAngles.y, platform.rotation.eulerAngles.y);
                if (Mathf.Abs(dy) > 0.0001f)
                {
                    // Орбитальное смещение вокруг оси платформы, чтобы при повороте не сносило вбок.
                    Vector3 offset = riderPos - platform.position;
                    offset.y = 0f;
                    Vector3 rotated = Quaternion.AngleAxis(dy, Vector3.up) * offset;
                    deltaPos += rotated - offset;
                    deltaYaw = dy;
                }
            }
            return deltaPos;
        }
    }
}
