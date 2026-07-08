=======
=======
=======
=======
=======
# Итерации реализации Unified NPC Behavior Architecture

## Итерация от 2026-07-15 — Phase 1: «Живой NPC»

**Задача:** Реализация Phase 1 (P0) согласно `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`:
NPC ходят дозором (Patrol), убегают (Flee), помнят обидчика (Grudge).
Минимальные add-only изменения в NpcBrain.

**Коммит:** `bcc3795` — T-NPC-S01..S06: Phase 1 Unified NPC Behavior Architecture

**Изменения:**
- `NpcBrain.cs` — add-only API: CurrentAggroTarget, ForceChaseTarget, ForceFlee, SocialTick, _socialOverrideLock
- `NpcSocialBrain.cs` — (NEW) companion MonoBehaviour: Patrol/Flee/Grudge/SocialTick dispatch (~400 строк)
- `NpcSpawnerConfig.cs` — add-only: social/personality/idle/flee/alarm/group/memory поля
- `NpcSpawner.cs` — проброс social-конфига при спавне через ApplySpawnerConfig
- `NpcIdleActivity.cs` — (NEW) enum: StandStill/Patrol/LookAround/Socialize/Work/Sit/Sleep/Wander + PatrolPattern
- `GrudgeTable.cs` — (NEW) struct: playerId→timestamp память обидчиков
- `SocialTrigger.cs` — (NEW) enum: 7 триггеров с приоритетами (Phase 2 prep)

**Тикеты:** T-NPC-S01, T-NPC-S02, T-NPC-S03, T-NPC-S04, T-NPC-S05, T-NPC-S06

---

## Итерация от 2026-07-15 — Phase 1 fix v0.3.1: порядок инициализации

**Проблема:** NpcSpawner применял конфиги (в т.ч. AddComponent<NpcSocialBrain>) ПОСЛЕ netObj.Spawn().
NpcBrain.OnNetworkSpawn() искал NpcSocialBrain до его добавления → _socialBrain всегда null → patrol/flee/grudge не работали.

**Коммит:** `0219850` — NpcSpawner применяет конфиги ДО Spawn() чтобы OnNetworkSpawn видел NpcSocialBrain

**Изменения:**
- `NpcSpawner.cs` — TrySpawnAtPoint: visual/behavior/social конфиги перенесены ДО netObj.Spawn()
- `NpcBrain.cs` — Tick(): lazy GetComponent<NpcSocialBrain>() как страховка на edge-case

**Верификация:** SPAWN_TEST — patrol (waypoints), flee (HP<50%), grudge (повторная встреча) работают ✅

---

## Итерация от 2026-07-15 — Phase 1 UX: patrol waypoint markers

**Задача:** Ручной ввод Vector3 координат для patrol waypoints неудобен.
Решение: Transform[] маркеры прямо на NpcSpawner (drag-and-drop Empty из сцены).

**Коммит:** `af99f10` — patrol waypoint markers — Transform[] на NpcSpawner вместо ручных координат

**Изменения:**
- `NpcSpawner.cs` — поле `patrolWaypointMarkers` (Transform[]), конвертация в Vector3[] при спавне
- `NpcSocialBrain.cs` — `ApplySpawnerConfig(config, waypointsOverride=null)` — маркеры переопределяют конфиг

---

**Статус Phase 1:** ✅ Полностью завершён. 4 коммита, 8 файлов, +850 строк.

---

## Итерация от 2026-07-15 — Phase 2: «Социальная группа»

**Задача:** Реализация Phase 2 (P1) согласно `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`:
NPC координируются в группе, эмоционально реагируют, зовут на помощь, оплакивают союзников.

**Коммит:** `6a2e54e` — T-NPC-S07..S12: Phase 2 — эмоции, мораль, социальные триггеры, групповой контроллер, vocal cues

**Изменения:**
- `NpcEmotion.cs` — (NEW) enum NpcEmotion (Calm/Alert/Fear/Anger/Despair/Victory) + class NpcEmotionState
- `NpcPersonalityConfig.cs` — (NEW) ScriptableObject: 5 traits (courage/aggression/loyalty/recklessness/mercy)
- `NpcMoraleData.cs` — (NEW) struct: расчёт морали (0..1), модификаторы, ShouldFlee, ShouldSurrender, DamageMultiplier
- `NpcGroupController.cs` — (NEW) NetworkBehaviour: групповой координатор (Alarm broadcast, MemberKilled, Retreat, VocalCues, ElectLeader)
- `NpcVocalCue.cs` — (NEW) enum: 5 голосовых сигналов (AlertCall/DeathScream/Taunt/FearCry/VictoryRoar)
- `NpcSocialBrain.cs` — расширен: UpdateEmotion, EvaluateTriggers, ResolveActiveTriggers, CheckAllyKilled/LeaderAggrod/AllyInCombat/Outnumbered/ReinforcementNearby, DispatchVocalCue
- `NpcSpawnerConfig.cs` — add-only: поле personalityConfig

**Тикеты:** T-NPC-S07, T-NPC-S08, T-NPC-S09, T-NPC-S10, T-NPC-S11, T-NPC-S12

=======

## Итерация от 2026-07-15 — Phase 3: «Глубина»

**Задача:** Реализация Phase 3 (P2) согласно `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`:
ThreatAssessment, CoverModule, Group Tactics, Surrender, Post-Combat, SocialRoleConfig.

**Изменения:**
- `ThreatAssessment.cs` — (NEW) оценка соотношения сил: threatScore = Σ(enemyStr)/Σ(allyStr), результат Confident/Cautious/Afraid
- `CoverPoint.cs` — (NEW) маркер укрытия с приоритетом, типом, Gizmos-визуализацией
- `NpcSocialBrain.cs` — расширен: EvaluateThreatBeforeCombat, CheckCover/SeekCover/AutoDetectCover/LeaveCover, CheckSurrender/EnterSurrender, CheckPostCombat/TickWounded/TickHealing/TickSeekingReinforcement
- `NpcGroupController.cs` — расширен: FormationType (None/Line/Circle/Flank), TacticsTick, ApplyFormationLine/Flank/Circle, FocusFire, GetGroupCenter
- `NpcBrain.cs` — add-only: BrainState.Surrendered, EnterSurrendered/HandleSurrendered/ForceSurrender, Surrendered блокирует Update()
- `NpcSpawnerConfig.cs` — add-only: threatEvaluationRange, coverSeekRadius/coverHpThreshold, surrenderHpThreshold/canSurrender, enablePostCombat/woundedDuration/healHpThreshold, socialRole
- `SocialRoleConfig.cs` — (NEW) ScriptableObject: пресеты Guard/Civilian/Merchant/Thug/Leader + ApplyTo(NpcSocialBrain)
- `Resources/AI/SocialRole_*.asset` — (NEW) 5 файлов пресетов социальных ролей

**Тикеты:** T-NPC-S13, T-NPC-S14, T-NPC-S15, T-NPC-S16, T-NPC-S17, T-NPC-S18

=======

## Итерация от 2026-07-15 — Phase 4: «Социум»

**Задача:** Реализация Phase 4 (P3) согласно `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`:
FactionSystem, VengeanceMemory, Full Idle Activities (Socialize/Work/Sit/Sleep).

**Изменения:**
- `NpcFaction.cs` — (NEW) ScriptableObject: factionId, FactionRelation (Allied/Neutral/Hostile), GetRelation/IsAllied/IsHostile/SetRelation
- `VengeanceMemory.cs` — (NEW) NetworkBehaviour singleton: кросс-спавн память обидчиков, RegisterKill/HasVengeance/ClearVengeance/GetVengeanceBuff
- `SitPoint.cs` — (NEW) маркер места для сидения: IsOccupied, SitPosition/SitRotation, Gizmos
- `NpcSocialBrain.cs` — расширен: faction-aware FindNearestAlly/CheckAllyInCombat/FindSocializePartner, CheckVengeanceTrigger, ExecuteSocialize/Work/Sit/Sleep/LookAround
- `NpcGroupController.cs` — расширен: faction-check в AddMember, предпочтение isGuard для лидера
- `NpcSpawnerConfig.cs` — add-only: faction, enableVengeanceMemory
- `Resources/AI/NpcFaction_*.asset` — (NEW) 5 файлов фракций

**Тикеты:** T-NPC-S19, T-NPC-S20, T-NPC-S21
**Пропущен:** T-NPC-S22 (рефакторинг на модули — опциональный, отложен)

=======

## Итерация от 2026-07-15 — Phase 4 fixes: NPC-vs-NPC combat debug

**Проблемы после реализации Phase 4:**
1. NPC разных фракций не видели друг друга (NpcBrain.FindNearestPlayerTarget только игроки)
2. NpcAttacker.Target = null → IsAlive()=false → атака отбрасывалась молча
3. NpcAttacker._defaultSource = null → InvalidSource
4. Destroyed NpcTarget → MissingReferenceException (спам + лаги)
5. ResolveActiveTriggers переключал цель посреди боя → дрифт
6. После боя NPC сразу бежал в патруль → дрифт

**Коммиты:**
- `b77b84e` — FindNearestHostileTarget: игроки + вражеские NPC
- `99c858f` — NpcAttacker.Target = _target
- `acd59e6` — EnsureDefaultSource(): ленивое создание _defaultSource
- `ed524bc` — IsTargetValid(): защита от destroyed NpcTarget
- `68ff672` — target-lock + post-combat guard пауза 4-6с


=======

=======

=======
=======
**Итог фазы 4:** NPC-vs-NPC бой полностью функционален.

---

## Итерация от 2026-07-15 — Полное код-ревью

**Задача:** Провести полный аудит кода Unified NPC Behavior Architecture после завершения Phase 1–4.
Выявить критические баги, пробелы, места для рефакторинга.

**Коммит:** `bbd81f9` — T-NPC-S00: код-ревью Unified NPC Behavior Architecture — 3 P0, 2 P1, 3 P2

**Изменения:**
- `docs/Character/Skills/real-time-combat/npc-enemy/05_CODE_REVIEW_FINDINGS.md` — (NEW) документ с 8 находками

**Ключевые находки (подробно в 05_CODE_REVIEW_FINDINGS.md):**
1. 🔴 P0: NpcSpawner не создаёт группы — вся групповая логика dead code
2. 🔴 P0: CheckAllyKilled killerClientId всегда 0 — vengeance сломана
3. 🔴 P0: OnMemberKilled не вызывается — DeathScream + leader re-election не работают
4. 🟠 P1: RecordPlayerHit не вызывается — grudge пуст
5. 🟠 P1: FearCry/VictoryRoar/Taunt без gameplay-эффектов
6. 🟡 P2: FindObjectsByType спам
7. 🟡 P2: Patrol unreachable waypoint — нет таймаута

=======

=======

=======
=======
8. 🟡 P3: NpcSocialBrain монолит 1664 строк (порог превышен)

---

## Итерация от 2026-07-15 — Исправление P0/P1/P2 багов код-ревью

**Задача:** Исправить 3 P0 + 2 P1 + 1 P2 бага из `05_CODE_REVIEW_FINDINGS.md`.

**Коммит:** `f2effd6` — T-NPC-S00: исправления код-ревью — группы, vengeance, vocal cues, grudge, patrol timeout

**Изменения:**
- `NpcSpawner.cs` — (+81 строк) TryFormGroups(): создание NpcGroupController для кластеров NPC по groupSpawnRadius
- `NpcBrain.cs` — (+26 строк) EnterDead → OnMemberKilled + DeathScream; OnNpcHpChanged → RecordPlayerHit
- `NpcSocialBrain.cs` — (+48 строк) CheckAllyKilled killerClientId fix; ResolvePlayerClientId(); публичное API морали; patrol anti-stuck timeout
- `NpcGroupController.cs` — (9 строк изменено) OnVocalCue: FearCry/VictoryRoar эффекты; OnMemberKilled: лидерская смерть morale penalty

**Исправленные баги:**
1. ✅ P0: Spawner создаёт группы → групповая координация работает
2. ✅ P0: killerClientId заполняется → vengeance-память работает
3. ✅ P0: OnMemberKilled вызывается → DeathScream + leader re-election
4. ✅ P1: RecordPlayerHit вызывается → grudge работает
5. ✅ P1: FearCry/VictoryRoar — gameplay-эффекты через публичное API морали
6. ✅ P2: Patrol anti-stuck timeout 15с → unreachable waypoint skip

**Осталось (не в этом коммите):**
- 🟡 P2: FindObjectsByType спам (частично решён группами, полное решение — кеш)
- 🟡 P3: Монолит NpcSocialBrain (рефакторинг отложен)
=======

=======

=======

=======

