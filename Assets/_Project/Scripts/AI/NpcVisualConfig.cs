// Project C: Real-Time Combat Engine — T-NPC-05 (Anti-restrictive NPC visual config)
// NpcVisualConfig: ScriptableObject с параметрами визуала NPC.
// Design: docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md §2.4.
// Plan: docs/dev/2026-06-26-npc-p2-visual-config-chunk-integration.md §1.
//
// Цель: дизайнер меняет визуал NPC (материал/цвет/масштаб/имя) БЕЗ редактирования
// префаба. Один префаб Npc_Goblin может представлять разные фракции (goblin/bandit/guard)
// через разные .asset-ы этого SO.
//
// MVP-скоуп: только material/color override (слой A). Полный mesh swap (слой B)
// зарезервирован API, но дефолтный path его не вызывает — не ломаем HumanM_Model.

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-05: визуальный конфиг NPC. Применяется NpcVisualApplier при спавне.
    /// Если config = null в NpcSpawnerConfig → NpcVisualApplier ничего не делает
    /// (дефолтный вид из префаба, anti-restrictive).
    /// </summary>
    [CreateAssetMenu(fileName = "NpcVisual_", menuName = "Project C/AI/Npc Visual Config")]
    public class NpcVisualConfig : ScriptableObject
    {
        [Header("Material override (slot-based)")]
        [Tooltip("Материалы для SkinnedMeshRenderer (например HumanM_BodyMesh). " +
                 "Если null или пусто — оставляем дефолтные из префаба. " +
                 "ВАЖНО: sharedMaterials, не instance — чтобы не лить копии.")]
        public Material[] bodyMaterials;

        [Header("Tint color (MaterialPropertyBlock, zero-leak)")]
        [Tooltip("Цвет тонировки, применяется через MaterialPropertyBlock (без instance material). " +
                 "Белый = без изменений.")]
        public Color tintColor = Color.white;

        [Tooltip("Имя свойства шейдера. URP Lit = '_BaseColor', Standard = '_Color'. " +
                 "Если шейдер не имеет такого свойства — tint игнорируется (без ошибок).")]
        public string tintColorProperty = "_BaseColor";

        [Header("Scale (опционально, не ломает NavMeshAgent)")]
        [Tooltip("Универсальный масштаб root-объекта NPC. 1.0 = без изменений. " +
                 "Применяется после material override.")]
        [Range(0.5f, 2.0f)] public float uniformScale = 1f;

        [Header("Display (опционально, для UI над головой — post-MVP)")]
        [Tooltip("Отображаемое имя (для будущего FloatingNameTag).")]
        public string displayName = "Goblin";

        /// <summary>
        /// Удобный helper: применять ли tint (белый = не применять).
        /// </summary>
        public bool HasTint => tintColor != Color.white;

        /// <summary>
        /// Удобный helper: применять ли material override.
        /// </summary>
        public bool HasMaterialOverride => bodyMaterials != null && bodyMaterials.Length > 0;

        /// <summary>
        /// Удобный helper: применять ли scale.
        /// </summary>
        public bool HasScaleOverride => !Mathf.Approximately(uniformScale, 1f);
    }
}