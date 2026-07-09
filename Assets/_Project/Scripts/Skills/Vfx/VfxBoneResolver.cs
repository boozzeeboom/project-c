// Project C: Skills VFX — Phase 1
// VfxBoneResolver: статический хелпер для резолва костей по VfxAttachPoint.
// Используется ParticleSystemVfxProvider для определения spawn-позиции cast VFX.

using UnityEngine;

namespace ProjectC.Skills.Vfx
{
    /// <summary>
    /// Резолвит позицию/вращение для VfxAttachPoint на переданном Transform персонажа.
    /// </summary>
    public static class VfxBoneResolver
    {
        public static Vector3 Resolve(Transform character, VfxAttachPoint point)
        {
            if (character == null) return Vector3.zero;

            return point switch
            {
                VfxAttachPoint.WeaponMain => GetBonePosition(character, "hand_r") ?? GetBonePosition(character, "RightHand") ?? character.position + character.forward * 0.5f + Vector3.up * 1.2f,
                VfxAttachPoint.WeaponOff  => GetBonePosition(character, "hand_l") ?? GetBonePosition(character, "LeftHand") ?? character.position + character.forward * 0.3f + Vector3.up * 1.0f,
                VfxAttachPoint.Chest      => character.position + Vector3.up * 1.4f,
                VfxAttachPoint.Head       => character.position + Vector3.up * 1.8f,
                VfxAttachPoint.Root       => character.position,
                _                         => character.position + Vector3.up * 1.2f
            };
        }

        private static Vector3? GetBonePosition(Transform root, string boneName)
        {
            // Поиск кости по имени во всей иерархии (case-insensitive).
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (t.name.ToLowerInvariant().Contains(boneName.ToLowerInvariant()))
                    return t.position;
            }
            return null;
        }
    }
}
