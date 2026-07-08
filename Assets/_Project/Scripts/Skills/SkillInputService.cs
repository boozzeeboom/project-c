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
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using ProjectC.Combat;
using ProjectC.Combat.Client;  // T-HIGHLIGHT-01: TargetHighlightService
using ProjectC.Combat.Core;
using ProjectC.Equipment;  // T-INP-09: EquipmentClientState для GetActiveWeapon()
using ProjectC.Player;  // NetworkPlayer
using ProjectC.Input;  // InputBindingsRuntime
using ProjectC.Items;  // ItemType, InventoryWorld
using ProjectC.Items.Client;  // InventoryClientState

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
            LoadSlotBindings();
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

            // 4) T-LOCK-01: Target cycling (Q/E, only on foot — не мешает кораблю).
            if (!_ownerPlayer.IsInShip && TargetLockService.Instance != null)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb[cfg.targetPrevKey].wasPressedThisFrame)
                        TargetLockService.Instance.CyclePrev();
                    if (kb[cfg.targetNextKey].wasPressedThisFrame)
                        TargetLockService.Instance.CycleNext();
                }
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

            // 5) T-LOCK-01: Найти target.
            //    Приоритет 1: Locked target (Q/E) — для ВСЕХ скиллов (Primary/Secondary/Slot1-4).
            //    Приоритет 2: TargetFinder (raycast + fallback) — только если нет лока.
            ulong targetId = 0UL;

            if (TargetLockService.Instance != null && TargetLockService.Instance.LockedTargetId != 0UL)
            {
                var lockedObj = TargetLockService.Instance.LockedTargetObject;
                if (lockedObj != null)
                {
                    var lockedDt = lockedObj.GetComponentInParent<IDamageTarget>();
                    if (lockedDt != null && lockedDt.IsAlive())
                    {
                        targetId = TargetLockService.Instance.LockedTargetId;
                    }
                    else
                    {
                        // Цель умерла — снимаем лок.
                        TargetLockService.Instance.Unlock();
                    }
                }
            }

            if (targetId == 0UL && TargetFinder != null)
            {
                try { targetId = TargetFinder(); }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SkillInputService] TargetFinder threw: {ex.Message}");
                }
            }

            // 6) Получить SkillNodeConfig из клиентского кэша (R4: SkillsClientState.TryGetSkillConfig)
            SkillNodeConfig skillConfig = null;
            if (hasBind && !string.IsNullOrEmpty(skillId))
            {
                var skillsCache = SkillsClientState.Instance;
                if (skillsCache != null && skillsCache.TryGetSkillConfig(skillId, out var cached))
                {
                    skillConfig = cached;
                }
                else
                {
                    // Fallback: Resources.LoadAll (если SkillsClientState ещё не инициализирован)
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
            //
            // REFACTOR 2026-07-26: Throwables subtype — проверяется НЕ через слоты оружия,
            // а через наличие Throwable предметов в инвентаре (equipSlot=None).
            if (skillConfig != null && skillConfig.requiredWeaponMask != WeaponClassMask.None && hasBind)
            {
                bool isThrowableSkill = skillConfig.subtype == CombatSubtype.Throwables;

                if (isThrowableSkill)
                {
                    int requiredCount = Mathf.Max(1, skillConfig.throwCount);
                    if (!HasThrowableInInventory(requiredCount, out string denyReason))
                    {
                        if (Debug.isDebugBuild)
                            Debug.LogWarning($"[SkillInputService/T-INP-09] slot={slot} skill='{skillId}' blocked: {denyReason}");
                        return false;
                    }
                }
                else
                {
                    if (!CheckWeaponMask(skillConfig, out string denyReason))
                    {
                        if (Debug.isDebugBuild)
                            Debug.LogWarning($"[SkillInputService/T-INP-09] slot={slot} skill='{skillId}' blocked: {denyReason} (requiredMask={skillConfig.requiredWeaponMask})");
                        return false;
                    }
                }
            }

            // 6.8) R5: Bows/Crossbows fallback — если character-forward raycast не нашёл цель,
            // ищем ближайшего NPC в радиусе rangedMaxRange (настраивается в инспекторе навыка).
            if (targetId == 0UL && skillConfig != null
                && (skillConfig.subtype == CombatSubtype.Bows || skillConfig.subtype == CombatSubtype.Crossbows)
                && skillConfig.rangedMaxRange > 0f)
            {
                targetId = FindNearestNpcInRange(skillConfig.rangedMaxRange);
                if (targetId != 0UL && Debug.isDebugBuild)
                {
                    Debug.Log($"[SkillInputService/R5] Bows/Crossbows fallback: found target {targetId} within {skillConfig.rangedMaxRange}m (skill='{skillId}')");
                }
            }

            // 6.9) T-HIGHLIGHT-01: Подсветить найденную цель outline'ом.
            if (targetId != 0UL)
            {
                var targetObj = FindGameObjectByTargetId(targetId);
                if (targetObj != null)
                {
                    ProjectC.Combat.Client.TargetHighlightService.Instance?.Highlight(targetObj, 1.5f);
                }
            }

            // 7) Animation: T-INP-08 data-driven path preferred.

            if (!skipAnimation && skillConfig != null && skillConfig.attackClip != null)
            {
                var animPlayer = _ownerPlayer != null ? _ownerPlayer.GetComponent<SkillAnimationPlayer>() : null;
                if (animPlayer != null)
                {
                    animPlayer.Play(skillConfig, slot);
                    _slotCooldownUntil[slot] = Time.unscaledTime + skillConfig.cooldownSeconds;
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"[SkillInputService] TryActivate: slot={slot} skill='{skillId}' [T-INP-08 clip-path] attackClip='{skillConfig.attackClip.name}' cooldown={skillConfig.cooldownSeconds:F1}s — RPC will fire from OnAttackImpact event");
                    }
                    return true;
                }
                if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[SkillInputService] Skill '{skillId}' has attackClip='{skillConfig.attackClip.name}' but SkillAnimationPlayer not found on owner. Falling back to legacy SetTrigger.");
                }
            }

            // Legacy path: SetTrigger (без attackClip, или SkillAnimationPlayer missing)
            if (!skipAnimation && _animator != null)
            {
                _animator.SetTrigger(trigger);
            }

            // 8) RPC на сервер.
            try
            {
                bool isThrownAoe = skillConfig != null
                    && skillConfig.subtype == ProjectC.Skills.CombatSubtype.Throwables
                    && skillConfig.isActive;

                bool isRangedProjectile = skillConfig != null
                    && skillConfig.discipline == CombatDiscipline.Ranged
                    && skillConfig.subtype != CombatSubtype.Throwables
                    && skillConfig.aoeFormula == ProjectC.Skills.AoeFormula.SingleTarget
                    && skillConfig.isActive
                    && hasBind;

                if (isThrownAoe)
                {
                    float throwRange = skillConfig.throwRange;
                    Vector3 baseTargetPoint = FindThrowTargetPoint(throwRange);
                    int throwCount = Mathf.Max(1, skillConfig.throwCount);

                    for (int i = 0; i < throwCount; i++)
                    {
                        Vector3 scatterOffset = Vector3.zero;
                        if (throwCount > 1 || skillConfig.throwScatter < 6)
                        {
                            int scatterRoll = Random.Range(1, 7);
                            float scatterFactor = Mathf.Max(0f, (float)(scatterRoll - skillConfig.throwScatter) / 6f);
                            if (scatterFactor > 0.01f)
                            {
                                Vector3 perp = Vector3.Cross(_ownerPlayer.transform.forward, Vector3.up).normalized;
                                scatterOffset = (perp * Random.Range(-1f, 1f) + _ownerPlayer.transform.forward * Random.Range(-0.3f, 0.3f))
                                    * scatterFactor * throwRange * 0.5f;
                            }
                        }
                        Vector3 targetPoint = baseTargetPoint + scatterOffset;

                        float dist = Vector3.Distance(_ownerPlayer.transform.position, targetPoint);
                        float flightTime = Mathf.Clamp(dist / 20f, 0.3f, 1.5f);

                        Color arcColor = new Color(1f, 0.4f, 0.1f);
                        ProjectC.Combat.Client.ThrowArcVisual.Fire(
                            _ownerPlayer.transform.position, targetPoint, flightTime,
                            skillConfig.aoeSize, arcColor);

                        if (skillConfig.debugVisualizeAoe)
                        {
                            var viz = ProjectC.Skills.DebugVisualization.SkillAoeDebugVisualizer.EnsureExists();
                            if (viz != null)
                            {
                                viz.ShowAoe(targetPoint, Vector3.up, skillConfig);
                            }
                        }

                        server.RequestSkillCastAtPointRpc(skillId, targetPoint, 0UL);
                    }
                }
                else if (isRangedProjectile)
                {
                    ulong weaponSourceId = ResolveEquippedWeaponSourceId();
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"[SkillInputService/R5] Ranged projectile: skill='{skillId}' target={targetId} weaponSourceId={weaponSourceId} — routing to RequestSkillCastRpc");
                    }
                    server.RequestSkillCastRpc(skillId, targetId, weaponSourceId);
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

            _slotCooldownUntil[slot] = Time.unscaledTime + (skillConfig?.cooldownSeconds ?? 0.5f);

            if (skillConfig != null && skillConfig.debugVisualizeAoe && _ownerPlayer != null)
            {
                bool isThrown = skillConfig.subtype == ProjectC.Skills.CombatSubtype.Throwables
                    && skillConfig.isActive;
                if (!isThrown)
                {
                    var viz = ProjectC.Skills.DebugVisualization.SkillAoeDebugVisualizer.EnsureExists();
                    if (viz != null)
                    {
                        Vector3 origin = _ownerPlayer.transform.position + Vector3.up * 1.2f;
                        Vector3 forward = _ownerPlayer.transform.forward;
                        viz.OnSkillActivated(skillConfig, origin, forward);
                    }
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
            SaveSlotBindings();
        }

        public IReadOnlyList<string> GetAllSkillIds() => _allSkillIds;

        public string GetSkillForSlot(SkillInputSlot slot)
        {
            if (_slotToSkillId.TryGetValue(slot, out var id)) return id;
            return "";
        }

        public IReadOnlyDictionary<SkillInputSlot, string> GetAllBindings() => _slotToSkillId;

        public void SetKnownSkills(IEnumerable<string> skillIds)
        {
            _allSkillIds.Clear();
            if (skillIds != null) _allSkillIds.AddRange(skillIds);
        }

        private readonly System.Collections.Generic.List<string> _allSkillIds = new();

        public bool IsSlotBound(SkillInputSlot slot) => _slotToSkillId.ContainsKey(slot);

        // === Helpers ===

        private bool CheckWeaponMask(SkillNodeConfig skillConfig, out string denyReason)
        {
            denyReason = "";
            if (skillConfig == null) { denyReason = "no skillConfig"; return false; }

            var ecs = EquipmentClientState.Instance;
            if (ecs == null) { denyReason = "EquipmentClientState не инициализирован"; return false; }

            var activeWeapon = ecs.GetActiveWeapon();
            if (activeWeapon == null) { denyReason = "Нет оружия в WeaponMain/WeaponOff"; return false; }

            WeaponClassMask weaponBit = (WeaponClassMask)(1 << (int)activeWeapon.weaponClass);
            if ((skillConfig.requiredWeaponMask & weaponBit) == 0)
            {
                denyReason = $"Требуется {DescribeMaskShort(skillConfig.requiredWeaponMask)}, в руке {activeWeapon.weaponClass}";
                return false;
            }
            return true;
        }

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
            if ((mask & WeaponClassMask.Throwable) != 0)     parts.Add("метательное");
            return parts.Count == 0 ? mask.ToString() : string.Join(" или ", parts);
        }

        private bool HasThrowableInInventory(int requiredCount, out string denyReason)
        {
            denyReason = "";
            int foundCount = 0;

            var clientState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (clientState != null && clientState.CurrentSnapshot.HasValue)
            {
                var snapshot = clientState.CurrentSnapshot.Value;
                if (snapshot.items != null)
                {
                    foreach (var item in snapshot.items)
                    {
                        var inv = ProjectC.Items.InventoryWorld.Instance;
                        if (inv != null)
                        {
                            var def = inv.GetItemDefinition(item.itemId);
                            if (def is WeaponItemData w && w.weaponClass == WeaponClass.Throwable)
                                foundCount += item.quantity;
                        }
                    }
                }
            }

            if (foundCount == 0)
            {
                var invDir = ProjectC.Items.InventoryWorld.Instance;
                if (invDir != null && _ownerPlayer != null)
                {
                    var data = invDir.GetOrCreate(_ownerPlayer.OwnerClientId);
                    foreach (ItemType type in System.Enum.GetValues(typeof(ItemType)))
                    {
                        var ids = data.GetIdsForType(type);
                        if (ids == null) continue;
                        foreach (int itemId in ids)
                        {
                            var def = invDir.GetItemDefinition(itemId);
                            if (def is WeaponItemData w && w.weaponClass == WeaponClass.Throwable)
                                foundCount += invDir.CountOf(_ownerPlayer.OwnerClientId, itemId);
                        }
                    }
                }
            }

            if (foundCount >= requiredCount)
                return true;

            denyReason = foundCount > 0
                ? $"Нужно {requiredCount} метательных предметов, есть только {foundCount}"
                : "Нет метательных предметов в инвентаре (нужен Throwable)";
            return false;
        }

        /// <summary>
        /// R5: Find nearest alive NpcTarget within range. Returns targetId or 0UL.
        /// Used for Bows/Crossbows fallback when character-forward raycast misses.
        /// </summary>
        private ulong FindNearestNpcInRange(float range)
        {
            ProjectC.Combat.Core.IDamageTarget nearest = null;
            float bestSq = range * range;
            Vector3 pos = _ownerPlayer != null ? _ownerPlayer.transform.position : transform.position;
            foreach (var npc in UnityEngine.Object.FindObjectsByType<ProjectC.Combat.NpcTarget>())
            {
                if (npc == null || !npc.IsAlive()) continue;
                float dSq = (npc.transform.position - pos).sqrMagnitude;
                if (dSq < bestSq) { bestSq = dSq; nearest = npc; }
            }
            return nearest != null ? nearest.GetTargetId() : 0UL;
        }

        /// <summary>
        /// T-HIGHLIGHT-01: Find a GameObject by targetId.
        /// Searches all NpcTarget + PlayerTarget components in scene.
        /// Returns the root GameObject of the target, or null.
        /// </summary>
        private GameObject FindGameObjectByTargetId(ulong targetId)
        {
            if (targetId == 0UL) return null;

            // Search NpcTargets
            foreach (var npc in UnityEngine.Object.FindObjectsByType<ProjectC.Combat.NpcTarget>())
            {
                if (npc != null && npc.GetTargetId() == targetId)
                    return npc.gameObject;
            }

            // Search PlayerTargets
            foreach (var pt in UnityEngine.Object.FindObjectsByType<ProjectC.Combat.PlayerTarget>())
            {
                if (pt != null && pt.GetTargetId() == targetId)
                    return pt.gameObject;
            }

            return null;
        }

        /// <summary>
        /// R5: Resolve equipped weapon's itemId as sourceId for ranged projectile skills.
        /// Reads EquipmentClientState snapshot → WeaponMain/WeaponOff → itemId.
        /// Returns 0UL if no weapon equipped (server will return InvalidSource).
        /// </summary>
        private ulong ResolveEquippedWeaponSourceId()
        {
            var ecs = EquipmentClientState.Instance;
            if (ecs?.CurrentSnapshot != null)
            {
                var snap = ecs.CurrentSnapshot.Value;
                if (snap.equip.TryGetItemId(EquipSlot.WeaponMain, out int itemId) && itemId > 0)
                {
                    if (Debug.isDebugBuild) Debug.Log($"[SkillInputService/R5] ResolveEquippedWeaponSourceId: WeaponMain itemId={itemId}");
                    return (ulong)itemId;
                }
                if (snap.equip.TryGetItemId(EquipSlot.WeaponOff, out itemId) && itemId > 0)
                {
                    if (Debug.isDebugBuild) Debug.Log($"[SkillInputService/R5] ResolveEquippedWeaponSourceId: WeaponOff itemId={itemId}");
                    return (ulong)itemId;
                }
            }
            if (Debug.isDebugBuild) Debug.LogWarning("[SkillInputService/R5] ResolveEquippedWeaponSourceId: no weapon in slots, sourceId=0");
            return 0UL;
        }

        private Vector3 FindThrowTargetPoint(float throwRange = 15f)
        {
            if (_ownerPlayer == null) return Vector3.zero;

            Vector3 origin = _ownerPlayer.transform.position + Vector3.up * 1.5f;
            Vector3 forward = _ownerPlayer.transform.forward;

            if (Physics.Raycast(origin, forward, out RaycastHit hit, Mathf.Max(throwRange, 50f), ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            Vector3 flatDir = forward;
            flatDir.y = 0;
            if (flatDir.sqrMagnitude < 0.001f) flatDir = _ownerPlayer.transform.forward;
            flatDir.Normalize();

            return _ownerPlayer.transform.position + flatDir * throwRange;
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

        // ==================== Slot Bindings Persistence ====================

        private void LoadSlotBindings()
        {
            if (_ownerPlayer == null) return;
            var save = SlotBindingsSave.Load(_ownerPlayer.OwnerClientId);
            if (save?.entries == null) return;

            foreach (var e in save.entries)
            {
                if (e.slot != SkillInputSlot.None && !string.IsNullOrEmpty(e.skillId))
                {
                    _slotToSkillId[e.slot] = e.skillId;
                    Debug.Log($"[SkillInputService] Loaded binding: {e.slot} → {e.skillId}");
                }
            }
        }

        private void SaveSlotBindings()
        {
            if (_ownerPlayer == null) return;
            var save = new SlotBindingsSave();
            var list = new List<SlotBindingEntry>();
            foreach (var kv in _slotToSkillId)
            {
                list.Add(new SlotBindingEntry { slot = kv.Key, skillId = kv.Value });
            }
            save.entries = list.ToArray();
            save.Save(_ownerPlayer.OwnerClientId);
        }
    }

    [System.Serializable]
    public struct SlotBindingEntry
    {
        public SkillInputSlot slot;
        public string skillId;
    }

    [System.Serializable]
    public class SlotBindingsSave
    {
        public SlotBindingEntry[] entries = new SlotBindingEntry[0];

        private static string GetPath(ulong clientId) =>
            Path.Combine(Application.persistentDataPath, "Skills", $"slot_bindings_{clientId}.json");

        public void Save(ulong clientId)
        {
            try
            {
                var dir = Path.GetDirectoryName(GetPath(clientId));
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(GetPath(clientId), json);
            }
            catch (System.Exception ex) { Debug.LogWarning($"[SlotBindingsSave] Save failed: {ex.Message}"); }
        }

        public static SlotBindingsSave Load(ulong clientId)
        {
            try
            {
                var path = GetPath(clientId);
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<SlotBindingsSave>(json);
            }
            catch (System.Exception ex) { Debug.LogWarning($"[SlotBindingsSave] Load failed: {ex.Message}"); return null; }
        }
    }
}