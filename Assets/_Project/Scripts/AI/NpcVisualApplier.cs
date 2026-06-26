// Project C: Real-Time Combat Engine — T-NPC-05 (Anti-restrictive NPC visual config)
// NpcVisualApplier: MonoBehaviour, применяет NpcVisualConfig к NPC-префабу при спавне.
// Design: docs/Character/Skills/real-time-combat/70_NPC_ENEMIES.md §2.4.
// Plan: docs/dev/2026-06-26-npc-p2-visual-config-chunk-integration.md §1.
//
// Контракт:
//   - Компонент на root-объекте NPC (тот же GO что NetworkObject + NpcBrain).
//   - Вызывается NpcSpawner после Instantiate.
//   - Работает И на сервере, И на клиенте: материалы/цвета — локальная визуальная
//     фича, не требует репликации через NetworkVariable.
//   - Безопасен повторный Apply (идемпотентен): повторный вызов заменяет override.
//
// Особенности реализации:
//   - Используем MaterialPropertyBlock (а не instance material) → zero-leak, batcher-friendly.
//   - SkinnedMeshRenderer ищем по имени "HumanM_BodyMesh" (из префаба Npc_Goblin).
//     Это не хардкод типа "первый SMR в детях", потому что HumanM_Model — nested prefab,
//     в нём может быть несколько SMR (HumanM_BodyMesh + BodyMesh + прочее).
//   - Если target SMR не найден → warning + no-op (не падаем).

using UnityEngine;

namespace ProjectC.AI
{
    /// <summary>
    /// T-NPC-05: применяет NpcVisualConfig к визуальной части NPC.
    /// Anti-restrictive: если <see cref="Apply"/> не вызывался — NPC рендерится
    /// как в префабе (без изменений).
    /// </summary>
    public class NpcVisualApplier : MonoBehaviour
    {
        [Tooltip("Имя SkinnedMeshRenderer (в дочерних объектах) к которому применяется material/color override. " +
                 "По умолчанию — HumanM_BodyMesh из Kevin Iglesias HumanM_Model.")]
        [SerializeField] private string _bodyMeshName = "HumanM_BodyMesh";

        [Tooltip("Имя дочернего объекта 'Visual', в котором искать SkinnedMeshRenderer. " +
                 "Если пусто — ищем по всему root.")]
        [SerializeField] private string _visualChildName = "Visual";

        [Tooltip("Показывать warning при отсутствии target mesh.")]
        [SerializeField] private bool _logWarnings = true;

        // Кэш для идемпотентности (если Apply вызывается дважды).
        private SkinnedMeshRenderer _cachedRenderer;
        private MaterialPropertyBlock _cachedMpb;

        /// <summary>
        /// Применить config к этому NPC. Вызывать ПОСЛЕ Instantiate (когда уже
        /// существует иерархия children). Безопасно вызывать многократно.
        /// </summary>
        /// <param name="config">Конфиг визуала. Если null — no-op (anti-restrictive).</param>
        /// <returns>true если что-то применилось, false если no-op (config==null или target не найден).</returns>
        public bool Apply(NpcVisualConfig config)
        {
            if (config == null) return false;

            var renderer = ResolveRenderer();
            if (renderer == null)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[NpcVisualApplier] SkinnedMeshRenderer '{_bodyMeshName}' not found on '{name}'. Visual override skipped.", this);
                }
                return false;
            }

            // 1. Material override (shared materials → без instance leak).
            if (config.HasMaterialOverride)
            {
                renderer.sharedMaterials = config.bodyMaterials;
            }

            // 2. Tint color через MaterialPropertyBlock (zero-allocation, batcher-friendly).
            if (config.HasTint)
            {
                if (_cachedMpb == null) _cachedMpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(_cachedMpb);
                // SetColor поддерживает оба варианта (_BaseColor и _Color), если свойства нет в шейдере — Unity проигнорирует без ошибок.
                _cachedMpb.SetColor(config.tintColorProperty, config.tintColor);
                renderer.SetPropertyBlock(_cachedMpb);
            }

            // 3. Scale (uniform multiplier на root).
            if (config.HasScaleOverride)
            {
                transform.localScale = Vector3.one * config.uniformScale;
            }

            return true;
        }

        /// <summary>
        /// Найти SkinnedMeshRenderer. Идемпотентно: первый раз ищет, далее — кэш.
        /// </summary>
        private SkinnedMeshRenderer ResolveRenderer()
        {
            if (_cachedRenderer != null) return _cachedRenderer;

            // 1. Если указано имя Visual-child → ищем только в нём.
            Transform searchRoot = transform;
            if (!string.IsNullOrEmpty(_visualChildName))
            {
                var visualT = transform.Find(_visualChildName);
                if (visualT != null) searchRoot = visualT;
            }

            // 2. Ищем SkinnedMeshRenderer по имени в searchRoot и его детях.
            // ВАЖНО: HumanM_Model может иметь несколько SMR (мы знаем про HumanM_BodyMesh).
            // Поэтому ищем по имени, а не GetComponentInChildren.
            var smrs = searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            foreach (var smr in smrs)
            {
                if (smr != null && smr.name == _bodyMeshName)
                {
                    _cachedRenderer = smr;
                    return _cachedRenderer;
                }
            }

            // 3. Fallback: если ничего по имени не нашли — берём первый SMR в searchRoot.
            // Полезно если дизайнер использует НЕ HumanM модель (например, custom monster).
            if (smrs != null && smrs.Length > 0)
            {
                _cachedRenderer = smrs[0];
                if (_logWarnings)
                {
                    Debug.Log($"[NpcVisualApplier] No SkinnedMeshRenderer named '{_bodyMeshName}' on '{name}', using first found: '{_cachedRenderer.name}'.", this);
                }
                return _cachedRenderer;
            }

            return null;
        }

        // Чтобы не лить component-копии при повторных Apply из NpcSpawner.
        // Если кто-то вызвал Apply → потом сменил конфиг → Apply снова — кэш
        // остаётся валидным (тот же renderer).
    }
}