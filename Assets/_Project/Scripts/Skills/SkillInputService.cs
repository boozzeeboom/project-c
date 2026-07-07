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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using ProjectC.Combat;
using ProjectC.Combat.Core;
using ProjectC.Equipment;  // T-INP-09: EquipmentClientState для GetActiveWeapon()
using ProjectC.Player;  // NetworkPlayer
using ProjectC.Input;  // InputBindingsRuntime

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
            // T-INP-08 fix: используем уже-resolved _animator из NetworkPlayer (который знает про пустой Animator на root).
            if (owner != null)
            {
                var ownerAnim = typeof(NetworkPlayer).GetField("_animator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                _animator = ownerAnim != null ? ownerAnim.GetValue(owner) as Animator : owner.GetComponentInChildren<Animator>();
                if (_animator == null) _animator = owner.GetComponentInChildren<Animator>();
            }
        }

        // ==================== INPUT POLLING (Phase 1) ====================
        //
        // Update() опрашивает InputBindingsRuntime.Config.combatSkills и триггерит TryActivate.
        // Это ЗАМЕНЯЕТ прямые ЛКМ/K хендлеры в NetworkPlayer.Update (см. docs/Character/input-system/40_MIGRATION_PLAN.md).
        //
        // Приоритет: самый СПЕЦИФИЧНЫЙ матч в кадре (modifier+button > button alone > fallback key).
        // Например, Ctrl+ЛКМ должен попасть в Slot1 (modifier=LeftCtrl), а НЕ в Primary (modifier=None),
        // даже если Primary идёт раньше в списке.

        private void Update()
        {
            // 1) Owner-only guard.
            if (_ownerPlayer == null || !_ownerPlayer.IsSpawned) return;

            // 2) Получить binding config (может быть null если InputBindingsRuntime ещё не загрузился).
            var runtime = InputBindingsRuntime.Instance;
            if (runtime == null || runtime.Config == null) return;
            var cfg = runtime.Config;

            // 3) Найти самый специфичный матч. Specificity score:
            //    modifier+button = 100, button only = 50, fallback key = 10.
            //    Если несколько биндингов матчатся с одинаковым score — берём первый.
            SkillInputSlot bestSlot = SkillInputSlot.None;
            int bestScore = 0;
            for (int i = 0; i < cfg.combatSkills.Count; i++)
            {
                var b = cfg.combatSkills[i];
                if (!IsBindingPressed(b)) continue;
                int score = ScoreBinding(b);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSlot = b.slot;
                }
            }
            if (bestSlot != SkillInputSlot.None)
            {
                TryActivate(bestSlot);
            }
        }

        /// <summary>
        /// Specificity score: modifier+button = 100, button only = 50, fallback key = 10.
        /// Гарантирует, что Ctrl+ЛКМ (100) перебивает просто ЛКМ (50).
        /// </summary>
        private int ScoreBinding(InputBindingsConfig.SkillKeyBinding b)
        {
            int score = 0;
            if (b.mouseButtonRaw != 0) score += 50;
            if (b.modifier != Key.None) score += 50; // поверх mouse-button = 100
            else if (b.fallbackKey != Key.None) score += 10;
            return score;
        }

        /// <summary>
        /// Проверить, нажата ли комбинация в этом кадре.
        /// mouseButtonRaw: 0=None, 1=LeftMouse, 2=RightMouse, 3=MiddleMouse.
        /// Если модификатор задан — он должен быть зажат (isPressed, не wasPressed).
        /// </summary>
        private bool IsBindingPressed(InputBindingsConfig.SkillKeyBinding b)
        {
            // onlyOnFoot: если в корабле — игнорируем (для Q/R, см. Q-INP-11).
            if (b.onlyOnFoot && _ownerPlayer != null && _ownerPlayer.IsInShip) return false;

            var mouse = Mouse.current;
            var kb = Keyboard.current;

            // Модификатор (если задан — должен быть зажат прямо сейчас).
            if (b.modifier != Key.None)
            {
                if (kb == null || !kb[b.modifier].isPressed) return false;
            }

            // Кнопка мыши (wasPressedThisFrame — только момент клика).
            if (b.mouseButtonRaw != 0 && mouse != null)
            {
                bool mousePressed = false;
                switch (b.mouseButtonRaw)
                {
                    case 1: mousePressed = mouse.leftButton.wasPressedThisFrame; break;
                    case 2: mousePressed = mouse.rightButton.wasPressedThisFrame; break;
                    case 3: mousePressed = mouse.middleButton.wasPressedThisFrame; break;
                }
                if (mousePressed) return true;
            }

            // Fallback клавиша (wasPressedThisFrame).
            if (b.fallbackKey != Key.None && kb != null)
            {
                if (kb[b.fallbackKey].wasPressedThisFrame) return true;
            }

            return false;
        }

        // ==================== END INPUT POLLING ====================

        // === Public API ===

        /// <summary>
        /// Попробовать активировать навык в данном слоте.
        /// Returns true если RPC отправлен (или skill не привязан — false без side-effects).
        /// Returns false если slot пуст, нет owner, на cooldown, нет target, нет server.
        /// </summary>
        /// <param name="slot">Skill slot для активации</param>
        /// <param name="skipAnimation">true если вызов из OnAttackImpact event — не дёргать SkillAnimationPlayer.Play() заново.</param>
        public bool TryActivate(SkillInputSlot slot, bool skipAnimation = false)
        {
            if (slot == SkillInputSlot.None) return false;

            // 1) Slot привязан к skill? Для Primary/Secondary разрешаем unarmed attack без бинда.
            string skillId = GetSkillForSlot(slot);
            bool hasBind = !string.IsNullOrEmpty(skillId);
            if (!hasBind && slot != SkillInputSlot.Primary && slot != SkillInputSlot.Secondary)
            {
                // Slot1..Slot4 без бинда — молча skip.
                return false;
            }
            if (!hasBind)
            {
                // Primary/Secondary без бинда — используем skillId="" как маркер "unarmed".
                skillId = "";
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

            // 6) Получить SkillNodeConfig (T-INP-02: теперь есть навыки с isActive + animation trigger + AOE formula).
            // Phase 1: Resources.LoadAll<SkillNodeConfig> — SO лежат в Resources/Skills, доступны клиенту.
            // Phase 2: заменить на SkillsClientState cache (per T-P12).
            SkillNodeConfig skillConfig = null;
            if (hasBind && !string.IsNullOrEmpty(skillId))
            {
                try
                {
                    var allSkills = Resources.LoadAll<SkillNodeConfig>("Skills");
                    foreach (var s in allSkills)
                    {
                        if (s != null && s.skillId == skillId) { skillConfig = s; break; }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SkillInputService] Resources.LoadAll<SkillNodeConfig> failed: {ex.Message}");
                }
            }

            // 6.5) Animation trigger (T-INP-08): hardcoded default "Attack" (AnyState → Attack1H).
            //    T-INP-02 attackAnimationTrigger field REMOVED — data-driven через attackClip.
            string trigger = _defaultAttackTrigger;

            // 6.6) Active guard (T-INP-02): пассивные навыки нельзя активировать через TryActivate.
            // Они применяются автоматически через SkillsServer.ApplySkillEffects на learn.
            if (skillConfig != null && !skillConfig.isActive)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[SkillInputService] slot={slot} skill='{skillId}' is passive — can't activate (auto-applied on learn)");
                }
                return false;
            }

            // 6.7) T-INP-09: weapon-mask gate. Если навык требует конкретный WeaponClass —
            // проверяем EquipmentClientState.GetActiveWeapon() против requiredWeaponMask.
            // requiredWeaponMask == None (default) = backward-compat: пропускаем проверку.
            // Unarmed attack (Primary/Secondary без bind, skillId == "") — пропускаем (нет weapon requirement).
            if (skillConfig != null && skillConfig.requiredWeaponMask != WeaponClassMask.None && hasBind)
            {
                if (!CheckWeaponMask(skillConfig, out string denyReason))
                {
                    if (Debug.isDebugBuild)
                    {
                        Debug.LogWarning($"[SkillInputService/T-INP-09] slot={slot} skill='{skillId}' blocked: {denyReason} (requiredMask={skillConfig.requiredWeaponMask})");
                    }
                    return false;
                }
            }

            // 7) Animation: T-INP-08 data-driven path preferred.
            //    Если в SkillNodeConfig задан attackClip → SkillAnimationPlayer.Play() проиграет клип;
            //    OnAttackImpact event на 60% клипа вызовет TryActivate заново и пошлёт RPC.
            //    Fallback (legacy path): SetTrigger(string) — если attackClip == null.

            if (!skipAnimation && skillConfig != null && skillConfig.attackClip != null)
            {
                var animPlayer = _ownerPlayer != null ? _ownerPlayer.GetComponent<SkillAnimationPlayer>() : null;
                if (animPlayer != null)
                {
                    animPlayer.Play(skillConfig, slot);
                    // RPC уйдёт через OnAttackImpact event (или fallback timeout в SkillAnimationPlayer).
                    // Не отправляем RPC здесь — иначе двойной удар.
                    // 9) Set local cooldown (чтобы не спамить нажатиями)
                    _slotCooldownUntil[slot] = Time.unscaledTime + 0.5f;
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"[SkillInputService] TryActivate: slot={slot} skill='{skillId}' [T-INP-08 clip-path] attackClip='{skillConfig.attackClip.name}' — RPC will fire from OnAttackImpact event");
                    }
                    return true;
                }
                // Если SkillAnimationPlayer не добавлен — fallback на SetTrigger ниже + warning.
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[SkillInputService] Skill '{skillId}' has attackClip='{skillConfig.attackClip.name}' but SkillAnimationPlayer not found on owner. Falling back to legacy SetTrigger.");
                }
            }

            // Legacy path: SetTrigger (без attackClip, или SkillAnimationPlayer missing)
            // skipAnimation=true (вызов из OnAttackImpact) — только RPC, без SetTrigger.
            if (!skipAnimation && _animator != null)
            {
                _animator.SetTrigger(trigger);
            }

            // 8) RPC на сервер.
            // Phase T3: Thrown skills (Sphere/Box AOE) → raycast target point → ThrowArcVisual → RequestSkillCastAtPointRpc.
            // Melee AOE skills (Cone/Line) → RequestSkillCastRpc (AOE at attacker).
            // Single-target → RequestAttackRpc.
            try
            {
                bool isThrownAoe = skillConfig != null
                    && skillConfig.isActive
                    && (skillConfig.aoeFormula == ProjectC.Skills.AoeFormula.Sphere
                        || skillConfig.aoeFormula == ProjectC.Skills.AoeFormula.Box)
                    && skillConfig.aoeSize > 0f;

                if (isThrownAoe)
                {
                    // Find target point via raycast
                    Vector3 targetPoint = FindThrowTargetPoint();
                    float flightTime = Mathf.Clamp(Vector3.Distance(_ownerPlayer.transform.position, targetPoint) / 20f, 0.3f, 1.5f);

                    // Client-side throw arc visual
                    Color arcColor = skillConfig.discipline == ProjectC.Skills.CombatDiscipline.Explosives
                        ? new Color(1f, 0.4f, 0.1f)  // orange for explosives
                        : new Color(0.3f, 0.7f, 1f);   // blue for antigrav
                    ProjectC.Combat.Client.ThrowArcVisual.Fire(
                        _ownerPlayer.transform.position, targetPoint, flightTime,
                        skillConfig.aoeSize, arcColor);

                    server.RequestSkillCastAtPointRpc(skillId, targetPoint, 0UL);
                }
                else if (skillConfig != null && skillConfig.aoeFormula != ProjectC.Skills.AoeFormula.SingleTarget)
                {
                    server.RequestSkillCastRpc(skillId, targetId, 0UL);
                }
                else
                {
                    server.RequestAttackRpc(targetId, 0UL);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SkillInputService] RPC failed: {ex.Message}");
                return false;
            }

            // 9) Set local cooldown (placeholder 0.5s; в будущем — из SkillNodeConfig.cooldownSeconds)
            _slotCooldownUntil[slot] = Time.unscaledTime + 0.5f;

            // T-INP-06: AOE debug visualization hook. Editor/Dev build only.
            // Если в SkillNodeConfig включён debugVisualizeAoe — показать wireframe.
            if (skillConfig != null && skillConfig.debugVisualizeAoe && _ownerPlayer != null)
            {
                var viz = ProjectC.Skills.DebugVisualization.SkillAoeDebugVisualizer.EnsureExists();
                if (viz != null)
                {
                    Vector3 origin = _ownerPlayer.transform.position + Vector3.up * 1.2f;
                    Vector3 forward = _ownerPlayer.transform.forward;
                    viz.OnSkillActivated(skillConfig, origin, forward);
                }
            }

            if (Debug.isDebugBuild)
            {
                string aoeInfo = skillConfig != null ? $" aoe={skillConfig.aoeFormula}/{skillConfig.aoeSize}m" : "";
                Debug.Log($"[SkillInputService] TryActivate: slot={slot} skill='{skillId}' target={targetId} trigger='{trigger}'{aoeInfo}");
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
            Debug.Log($"[SkillInputService] BindSlot: slot={slot} skill='{skillId ?? "(unbind)"}'");
        }

        /// <summary>Получить все известные skillId (из Server/SkillsClientState).</summary>
        public IReadOnlyList<string> GetAllSkillIds() => _allSkillIds;

        /// <summary>Текущая привязка slot → skillId.</summary>
        public string GetSkillForSlot(SkillInputSlot slot)
        {
            if (_slotToSkillId.TryGetValue(slot, out var id)) return id;
            return "";
        }

        /// <summary>Список всех привязанных slot'ов (для UI).</summary>
        public IReadOnlyDictionary<SkillInputSlot, string> GetAllBindings() => _slotToSkillId;

        /// <summary>Установить список известных навыков (вызывается при старте из ClientState).</summary>
        public void SetKnownSkills(IEnumerable<string> skillIds)
        {
            _allSkillIds.Clear();
            if (skillIds != null) _allSkillIds.AddRange(skillIds);
        }

        private readonly System.Collections.Generic.List<string> _allSkillIds = new();

        public bool IsSlotBound(SkillInputSlot slot) => _slotToSkillId.ContainsKey(slot);

        // === Helpers ===

        // T-INP-09: weapon-mask gate. Проверяет что EquipmentClientState.GetActiveWeapon().weaponClass
        // попадает в skillConfig.requiredWeaponMask. Возвращает false + denyReason если:
        //  - EquipmentClientState.Instance == null (не spawned)
        //  - Нет оружия в WeaponMain/WeaponOff (для AnyWeapon)
        //  - weaponClass не входит в маску (для конкретных классов)
        // Семантика AnyWeapon: требует ЛЮБОЕ оружие (хоть кулак, хоть меч).
        // Семантика конкретного класса: требует именно этот класс (или любой из набора).
        private bool CheckWeaponMask(SkillNodeConfig skillConfig, out string denyReason)
        {
            denyReason = "";
            if (skillConfig == null) { denyReason = "no skillConfig"; return false; }

            var ecs = EquipmentClientState.Instance;
            if (ecs == null) { denyReason = "EquipmentClientState не инициализирован"; return false; }

            var activeWeapon = ecs.GetActiveWeapon();
            if (activeWeapon == null) { denyReason = "Нет оружия в WeaponMain/WeaponOff"; return false; }

            // (mask & weaponClass bit) != 0 → weaponClass попадает в маску.
            WeaponClassMask weaponBit = (WeaponClassMask)(1 << (int)activeWeapon.weaponClass);
            if ((skillConfig.requiredWeaponMask & weaponBit) == 0)
            {
                denyReason = $"Требуется {DescribeMaskShort(skillConfig.requiredWeaponMask)}, в руке {activeWeapon.weaponClass}";
                return false;
            }
            return true;
        }

        // T-INP-09: человекочитаемое описание маски для Debug.Log / будущего toast.
        // Показывает только явно заданные биты (не computed aliases).
        private static string DescribeMaskShort(WeaponClassMask mask)
        {
            if (mask == WeaponClassMask.None) return "(нет ограничения)";
            if (mask == WeaponClassMask.AnyWeapon) return "любое оружие";
            if (mask == WeaponClassMask.AnyMelee) return "ближнее оружие";
            if (mask == WeaponClassMask.AnyRanged) return "дальнобойное оружие";

            var parts = new List<string>();
            if ((mask & WeaponClassMask.Sword) != 0)         parts.Add("меч");
            if ((mask & WeaponClassMask.Dagger) != 0)        parts.Add("кинжал");
            if ((mask & WeaponClassMask.Spear) != 0)         parts.Add("копьё");
            if ((mask & WeaponClassMask.Mace) != 0)          parts.Add("булава");
            if ((mask & WeaponClassMask.Crossbow) != 0)      parts.Add("арбалет");
            if ((mask & WeaponClassMask.Pneumatic) != 0)     parts.Add("пневматика");
            if ((mask & WeaponClassMask.AntigravBlade) != 0) parts.Add("антиграв клинок");
            if ((mask & WeaponClassMask.MesiumRifle) != 0)   parts.Add("мезиевая винтовка");
            return parts.Count == 0 ? mask.ToString() : string.Join(" или ", parts);
        }

        /// <summary>
        /// Phase T3: Find target point for thrown items via camera raycast.
        /// Falls back to (playerPos + forward * 15f) for ground/obstacle hits.
        /// </summary>
        private Vector3 FindThrowTargetPoint()
        {
            if (_ownerPlayer == null) return Vector3.zero;

            var cam = Camera.main;
            Vector3 origin, forward;
            if (cam != null)
            {
                origin = cam.transform.position;
                forward = cam.transform.forward;
            }
            else
            {
                origin = _ownerPlayer.transform.position + Vector3.up * 1.5f;
                forward = _ownerPlayer.transform.forward;
            }

            if (Physics.Raycast(origin, forward, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            // Fallback: ground plane at player Y
            Vector3 flatDir = forward;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.001f) flatDir = Vector3.forward;
            flatDir.Normalize();

            return _ownerPlayer.transform.position + flatDir * 15f;
        }

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