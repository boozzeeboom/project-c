// Project C: Equipment Visual System — Phase 2 (2026-06-29)
// EquipSlotToBone: единый маппинг EquipSlot → HumanBodyBones для персонажа-гуманоида M.
// По аналогии с NpcVisualApplier (T-NPC-05) — explicit table, не GetComponentInChildren.
//
// Дизайн: docs/Character/EquipmentVisual/00_DESIGN.md §3.3, 01_DATA_MODEL.md §2.
//
// Humanoid skeleton (Kevin Iglesias HumanM_Model, в NetworkPlayer.prefab) — стандартный
// Unity HumanBodyBones. Все 13 EquipSlot мапятся на одну из 54 костей humanoid rig.
// LastBone (54) — sentinel "use default" в ItemData.attachBoneOverride.

using UnityEngine;

namespace ProjectC.Equipment.Visual
{
    /// <summary>
    /// Phase 2: таблица маппинга EquipSlot → HumanBodyBones. Используется
    /// CharacterEquipmentVisualApplier для parent'инга visualPrefab к кости скелета.
    /// </summary>
    /// <remarks>
    /// Анти-restrictive: если Animator не humanoid или кость не найдена — TryGet* возвращает false.
    /// Вызывающий код (CharacterEquipmentVisualApplier) решает что делать (skip / warning).
    /// </remarks>
    public static class EquipSlotToBone
    {
        /// <summary>
        /// Получить Transform кости Animator'а для указанного EquipSlot.
        /// Default маппинг (используется когда ItemData.attachBoneOverride == LastBone).
        /// </summary>
        /// <param name="slot">EquipSlot (Head/Chest/WeaponMain/...)</param>
        /// <param name="animator">Animator персонажа (humanoid обязателен)</param>
        /// <param name="bone">out: Transform кости, null если не найдена</param>
        /// <returns>true если Animator валидный + кость найдена</returns>
        public static bool TryGetBoneTransform(EquipSlot slot, Animator animator, out Transform bone)
        {
            bone = null;
            if (animator == null || !animator.isHuman) return false;

            bone = slot switch
            {
                EquipSlot.Head       => animator.GetBoneTransform(HumanBodyBones.Head),
                EquipSlot.Chest      => animator.GetBoneTransform(HumanBodyBones.Spine),     // верх спины
                EquipSlot.Legs       => animator.GetBoneTransform(HumanBodyBones.Hips),     // штаны крепятся к бёдрам
                EquipSlot.Feet       => animator.GetBoneTransform(HumanBodyBones.LeftFoot),  // основная нога; меш обычно симметричный
                EquipSlot.Back       => animator.GetBoneTransform(HumanBodyBones.Spine),     // плащ/ранец за спиной — offset back (через attachPositionOffset)
                EquipSlot.Hands      => animator.GetBoneTransform(HumanBodyBones.LeftHand), // перчатки симметричные
                EquipSlot.Accessory1 => animator.GetBoneTransform(HumanBodyBones.Spine),     // кольцо в UI; визуально decorative на торсе
                EquipSlot.Accessory2 => animator.GetBoneTransform(HumanBodyBones.Spine),
                EquipSlot.WeaponMain => animator.GetBoneTransform(HumanBodyBones.RightHand), // основное оружие — правая рука
                EquipSlot.WeaponOff  => animator.GetBoneTransform(HumanBodyBones.LeftHand),  // парное оружие / щит — левая рука
                EquipSlot.Module1    => animator.GetBoneTransform(HumanBodyBones.Spine),     // имплант 1 — на торсе (дизайнер потом настроит)
                EquipSlot.Module2    => animator.GetBoneTransform(HumanBodyBones.Spine),
                EquipSlot.Module3    => animator.GetBoneTransform(HumanBodyBones.Spine),
                _ => null,
            };
            return bone != null;
        }

        /// <summary>
        /// Получить bone с учётом per-item override.
        /// Если <paramref name="overrideBone"/> != LastBone → используем его (override).
        /// Иначе → default маппинг по EquipSlot.
        /// </summary>
        public static bool TryGetBoneTransformWithOverride(
            EquipSlot slot,
            HumanBodyBones overrideBone,
            Animator animator,
            out Transform bone)
        {
            bone = null;
            if (animator == null || !animator.isHuman) return false;

            if (overrideBone != HumanBodyBones.LastBone)
            {
                bone = animator.GetBoneTransform(overrideBone);
                return bone != null;
            }

            return TryGetBoneTransform(slot, animator, out bone);
        }
    }
}