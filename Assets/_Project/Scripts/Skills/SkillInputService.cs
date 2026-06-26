// Project C: Skills/Battle — T-INP-01
// SkillInputService: единая точка "нажата кнопка → выполнить навык".
// Owner-only локальный сервис (НЕ NetworkBehaviour). Живёт на NetworkPlayer.
//
// Что делает:
//   - TryActivate(slot) — единая точка входа для ЛКМ/ПКМ/1-4/Q-E-R.
//   - _slotToSkillId[slot] = skillId (настраивается в CharacterWindow или SO).
//   - Cooldown per-slot per-client (локальный, для отзывчивости UI; сервер имеет свой в CombatServer).
//   - Триггерит Animator с правильным animationTrigger (по умолчанию "Attack").
//   - Отправляет server-RPC через CombatServer.RequestAttackRpc.
//
// Backward-compat:
//   - TryActivate возвращает false если slot пуст, нет NetworkPlayer, нет CombatServer.
//   - Не ломает существующий K-attack код в NetworkPlayer.Update — вызывающий решает, что делать.
//
// Design: docs/Character/Skills/AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md §3.O-7 + §4.

using UnityEngine;
using Unity.Netcode;
using ProjectC.Combat;
using ProjectC.Combat.Core;
using ProjectC.Player;  // NetworkPlayer

namespace ProjectC.Skills
{
    /// <summary>
    /// Слот для skill activation. Mapping хранится в <see cref="SkillInputService"/>._slotToSkillId.
    /// Primary/Secondary = ЛКМ/ПКМ (или K-fallback). Slot1..Slot4 = клавиши 1..4 (или Q/E/R/F — настраивается).
    /// </summary>
    public enum SkillInputSlot : byte
    {
        None = 0,
        Primary = 1,      // ЛКМ (или K-fallback)
        Secondary = 2,    // ПКМ (блок/парирование)
        Slot1 = 10,
        Slot2 = 11,
        Slot3 = 12,
        Slot4 = 13,
    }

    /// <summary>
    /// Локальный (owner-only) сервис: ввод → RPC + локальная анимация.
    /// НЕ NetworkBehaviour. НЕ scene-placed. Создаётся на NetworkPlayer.IsOwner.
    /// </summary>
    public class SkillInputService : MonoBehaviour
    {
        public static SkillInputService Instance { get; private set; }

        [Header("Animation")]
        [Tooltip("Default animator trigger для атаки. SkillNodeConfig.attackAnimationTrigger перекрывает, если задан.")]
        [SerializeField] private string _defaultAttackTrigger = "Attack";

        // slot → skillId. Настраивается через CharacterWindow (drag-and-drop) или SO.
        private readonly System.Collections.Generic.Dictionary<SkillInputSlot, string> _slotToSkillId
            = new System.Collections.Generic.Dictionary<SkillInputSlot, string>();

        // Per-slot cooldown (локальный, для UI responsiveness). Серверный cooldown — в CombatServer.
        private readonly System.Collections.Generic.Dictionary<SkillInputSlot, float> _slotCooldownUntil
            = new System.Collections.Generic.Dictionary<SkillInputSlot, float>();

        // Текущий target finder. NetworkPlayer задаёт свой (например "nearest NpcTarget в 15м").
        // Signature: returns ulong targetId (0 = no target).
        // Используем System.Func<ulong> вместо custom delegate — упрощает lambda conversion.
        public System.Func<ulong> TargetFinder;

        // Animator cache (lazy resolve).
        private Animator _animator;
        private NetworkPlayer _ownerPlayer;

        // === Lifecycle ===

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SkillInputService] Replacing existing Instance (duplicate).");
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Инициализация от NetworkPlayer.OnNetworkSpawn (IsOwner). Задаёт animator + target finder.
        /// </summary>
        public void Initialize(NetworkPlayer owner, System.Func<ulong> targetFinder)
        {
            _ownerPlayer = owner;
            TargetFinder = targetFinder;
            if (owner != null) _animator = owner.GetComponentInChildren<Animator>();
        }

        // === Public API ===

        /// <summary>
        /// Попробовать активировать навык в данном слоте.
        /// Returns true если RPC отправлен (или skill не привязан — false без side-effects).
        /// Returns false если slot пуст, нет owner, на cooldown, нет target, нет server.
        /// </summary>
        public bool TryActivate(SkillInputSlot slot)
        {
            if (slot == SkillInputSlot.None) return false;

            // 1) Slot привязан к skill?
            string skillId = GetSkillForSlot(slot);
            if (string.IsNullOrEmpty(skillId))
            {
                // Не warning — пустой slot это норма (например Slot4 ещё не настроен).
                return false;
            }

            // 2) Cooldown (локальный, для отзывчивости)
            if (IsOnCooldown(slot))
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[SkillInputService] slot={slot} skill='{skillId}' — on cooldown, skip");
                }
                return false;
            }

            // 3) Owner есть?
            if (_ownerPlayer == null || !_ownerPlayer.IsSpawned)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[SkillInputService] slot={slot} — no owner / not spawned");
                }
                return false;
            }

            // 4) Server доступен?
            var server = CombatServer.Instance;
            if (server == null)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[SkillInputService] slot={slot} — CombatServer.Instance==null");
                }
                return false;
            }

            // 5) Найти target (через делегат из NetworkPlayer)
            ulong targetId = 0UL;
            if (TargetFinder != null)
            {
                try { targetId = TargetFinder(); }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SkillInputService] TargetFinder threw: {ex.Message}");
                }
            }

            // 6) Получить SkillNodeConfig для animationTrigger
            string trigger = _defaultAttackTrigger;
            // TODO(T-INP-02+): прочитать skillConfig.attackAnimationTrigger когда поле добавлено.

            // 7) Локальная анимация (owner-side prediction, визуально сразу)
            if (_animator != null)
            {
                _animator.SetTrigger(trigger);
            }

            // 8) RPC на сервер (targetId=0 = server ищет nearest сам, MVP)
            //    sourceId=0 = Unarmed/WeaponMain первое доступное (server-side выбор по PlayerAttacker.GetDamageSource).
            try
            {
                server.RequestAttackRpc(targetId, 0UL);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SkillInputService] RequestAttackRpc failed: {ex.Message}");
                return false;
            }

            // 9) Set local cooldown (placeholder 0.5s; в будущем — из SkillNodeConfig.cooldownSeconds)
            _slotCooldownUntil[slot] = Time.unscaledTime + 0.5f;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillInputService] TryActivate: slot={slot} skill='{skillId}' target={targetId} trigger='{trigger}'");
            }
            return true;
        }

        // === Slot mapping API (для CharacterWindow drag-and-drop) ===

        /// <summary>Привязать skill к слоту. Перезаписывает предыдущую привязку.</summary>
        public void BindSlot(SkillInputSlot slot, string skillId)
        {
            if (slot == SkillInputSlot.None) return;
            if (string.IsNullOrEmpty(skillId))
            {
                _slotToSkillId.Remove(slot);
            }
            else
            {
                _slotToSkillId[slot] = skillId;
            }
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillInputService] BindSlot: slot={slot} skill='{skillId ?? "(unbind)"}'");
            }
        }

        public string GetSkillForSlot(SkillInputSlot slot)
        {
            _slotToSkillId.TryGetValue(slot, out var id);
            return id;
        }

        public bool IsSlotBound(SkillInputSlot slot) => _slotToSkillId.ContainsKey(slot);

        // === Helpers ===

        public bool IsOnCooldown(SkillInputSlot slot)
        {
            if (_slotCooldownUntil.TryGetValue(slot, out float until))
            {
                return Time.unscaledTime < until;
            }
            return false;
        }

        public void ClearAllCooldowns()
        {
            _slotCooldownUntil.Clear();
        }
    }
}