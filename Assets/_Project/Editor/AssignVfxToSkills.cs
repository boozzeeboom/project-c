// Project C: Skills VFX — Phase 2
// AssignVfxToSkills: Editor-скрипт для назначения VFX-префабов на существующие SkillNodeConfig .asset.
// Запустить: меню Project C > VFX > Assign VFX to All Skills
//
// Логика:
//   Melee → cast: MuzzleFlash, impact: Impact_Melee
//   Ranged (Bows/Crossbows) → cast: MuzzleFlash, projectile: Arrow, impact: Impact_Melee
//   Throwables → cast: MuzzleFlash, projectile: Arrow+arc, impact: Impact_Explosion
//   Defense → только cast: MuzzleFlash
//   Combat (generic) → cast: MuzzleFlash, impact: Impact_Melee

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Skills;

namespace ProjectC.Editor.Vfx
{
    public static class AssignVfxToSkills
    {
        private const string VfxFolder = "Assets/_Project/Resources/Vfx";

        [MenuItem("Project C/VFX/Assign VFX to All Skills")]
        public static void Execute()
        {
            var muzzleFlash = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxFolder}/PF_VFX_MuzzleFlash_Basic.prefab");
            var impactMelee = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxFolder}/PF_VFX_Impact_Melee.prefab");
            var impactExplosion = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxFolder}/PF_VFX_Impact_Explosion.prefab");
            var projectileArrow = AssetDatabase.LoadAssetAtPath<GameObject>($"{VfxFolder}/PF_VFX_Projectile_Arrow.prefab");

            if (muzzleFlash == null) { Debug.LogError("[AssignVfxToSkills] MuzzleFlash prefab not found!"); return; }

            var allSkills = Resources.LoadAll<SkillNodeConfig>("Skills");
            int assigned = 0;

            foreach (var skill in allSkills)
            {
                if (skill == null || !skill.isActive) continue;

                bool isCombat = skill.category == SkillCategory.Combat;
                if (!isCombat) continue;

                var subtype = skill.subtype;
                var discipline = skill.discipline;

                // Cast VFX: muzzle flash для всех боевых навыков
                skill.castVfxPrefab = muzzleFlash;
                skill.castSpawnPoint = VfxAttachPoint.WeaponMain;
                skill.castVfxDuration = 0.2f;

                // Impact VFX
                if (subtype == CombatSubtype.Throwables)
                {
                    skill.impactVfxPrefab = impactExplosion;
                    skill.projectileVfxPrefab = projectileArrow;
                    skill.projectileSpeed = 20f;
                    skill.projectileArcHeight = 4f;
                }
                else if (subtype == CombatSubtype.Bows || subtype == CombatSubtype.Crossbows)
                {
                    skill.impactVfxPrefab = impactMelee;
                    skill.projectileVfxPrefab = projectileArrow;
                    skill.projectileSpeed = 40f;
                    skill.projectileArcHeight = 0f;
                }
                else if (discipline == CombatDiscipline.Melee || discipline == CombatDiscipline.Combat)
                {
                    skill.impactVfxPrefab = impactMelee;
                }
                else if (discipline == CombatDiscipline.Defense)
                {
                    // Defense: только cast (аура)
                    skill.castSpawnPoint = VfxAttachPoint.Chest;
                    skill.castVfxDuration = 0.5f;
                }

                skill.impactVfxDuration = 0.4f;
                skill.impactColorByDamageType = true;
                skill.impactScaleByDamage = false;

                EditorUtility.SetDirty(skill);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AssignVfxToSkills] VFX assigned to {assigned} skills");
        }
    }
}
#endif
