# Battle Skills — Implementation Plan (2026-07-XX)

> **Подсистема:** Character Progression → Skill Tree → Combat branch
> **Назначение:** Конкретный план кодирования оставшихся тикетов T-CB02..T-CB09, T-RTC10.
> **Базовый дизайн-док:** `00_README.md`, `10_DESIGN.md`, `20_SKILL_TREES.md`.
> **Состояние проекта:** SkillTreeWindow работает (pan + zoom), базовая атака ЛКМ/K работает.
> **Цель:** завершить минимум для playable combat demo (пеший + дисциплины + прицеливание).

---

## TL;DR

| Pass | Что | Тикеты | Статус | Merge commit | Оценка |
|---|---|---|---|---|---|
| ✅ Done | PanelSettings fix (Reference Resolution 1200x800) | (был 🔴 блокер) | ✅ merged | (pre-Pass-1) | 15 мин |
| ✅ **Pass 1** | CombatDiscipline enum + ApplySkillEffects + ClassCatalogs | T-CB02 + T-CB07 + T-CB05 | ✅ merged | `3b2016e` | ~2 ч |
| ✅ **Pass 2** | Raycast targeting + cleanup DebugAttackNearestNpc | T-RTC10 + cleanup | ✅ merged | `71e7229` | ~1 ч |
| Phase 2 | SkillTreeWindow Fit + auto-fit + CenterOnSelected | UX polish | ⏳ later | — | ~30 мин |

**ИТОГО MVP+1 combat (пеший, с дисциплинами, с прицеливанием): ~3 ч** ✅ DONE.

---

## ✅ Pass 1 — COMPLETED (2026-06-28)

### Что сделано

| Тикет | Описание | Файлы | Diff | Merge |
|---|---|---|---|---|
| **T-CB02** | `CombatDiscipline` enum (None/Combat/Melee/Ranged/Explosives/Antigrav/Defense) + auto-set по skillId prefix в `OnValidate` | `SkillNodeConfig.cs` (+47 строк), 27 .asset auto-discover | 28 файлов, +74 строки | `2a4862a` |
| **T-CB07** | `ApplySkillEffects()` + `ApplySingleEffect()` + `TriggerEquipmentRecheck()` хуки в `RequestLearnSkillRpc`/`RequestForgetSkillRpc`. Поддержка новых T-CB01 типов (WeaponProficiencyUnlock, ArmorProficiencyUnlock, WeaponTechniqueUnlock, ExplosiveRecipeUnlock, AntigravTechniqueUnlock). Phase 2 stubs — логируют в Debug. | `SkillsServer.cs` (+76 строк) | 1 файл, +76 строк | `68793ea` |
| **T-CB05** | 3 SO-каталога для дизайнера: `WeaponClassCatalog` (8 классов populated), `ArmorClassCatalog` (stub), `WeaponTechniqueCatalog` (stub). Helpers: `GetRequiredProficiency` + `IsUnlocked`. | 3 new .cs файла + meta в `Combat/Lookup/` | 7 файлов, +183 строки | `3b2016e` |

### Verification результаты

- ✅ `refresh_unity scope=scripts` → **0 errors** (после фикса `SkillEffect` struct-check)
- ✅ 27/27 SkillNodeConfig .asset получили правильный discipline:
  - 4 Combat (combat_*)
  - 5 Melee (melee_*)
  - 3 Ranged (ranged_*)
  - 3 Explosives (expl_* — нестандартный prefix, добавлен в mapping)
  - 3 Antigrav (antigrav_*)
  - 3 Defense (defense_*)
  - 4 None (social_*)
- ✅ T-CB07 default-struct fix: `SkillEffect` это struct, заменено `effect == null` на default-check

### Что тестировать в Play Mode (Pass 1)

См. раздел **"🎮 Что тестировать в Play Mode"** в саммари после Pass 1:
1. SkillTreeWindow → Learn любой навык → Console: `[SkillsServer/T-CB07] Learned stat-affecting effect ...`
2. Forget → Console: `[SkillsServer/T-CB07] Forgot ...`
3. Inspector любого .asset навыка → поле `Combat Discipline` = правильный enum
4. Stats пересчитываются (STR/DEX/INT через StatMod)
5. Без падений и null-ref
6. Через `execute_code`: `Resources.Load<WeaponClassCatalog>("Combat/WeaponClassCatalog")` → 8 entries

### Lessons learned (Pass 1)

1. **`SkillEffect` is struct**, не class. `effect == null` → compile error CS0019. Use default-check.
2. **Explosives prefix = `expl_`**, не `explosives_` (legacy naming в существующих .asset). Mapping добавлен dual-prefix.
3. **MCP refresh_unity** работает даже когда `state=stale_status` — это не блокер, если `read_console` чисто.
4. **Auto-discover через execute_code + SerializedObject.ApplyModifiedProperties** — надёжнее чем ручная правка 27 .asset через OnValidate (которая срабатывает только при ручном импорте).

---

## ✅ Pass 2 — COMPLETED (2026-06-28)

### Что сделано

| Тикет | Описание | Файлы | Diff | Merge |
|---|---|---|---|---|
| **T-RTC10** | `TargetingService` (Combat/Core/) — static raycast helper. 3 метода: `TryGetTarget`, `TryGetTargetFromCamera`, `TryGetTargetFromTransform`. Использует `Physics.Raycast` + `GetComponentInParent<IDamageTarget>`. QueryTriggerInteraction.Ignore. | `TargetingService.cs` (NEW, +97 строк), `NetworkPlayer.cs` (-77 строк) | 3 файла, +114 строк, -62 | `71e7229` |
| **Hotfix** | Hybrid targeting: raycast + nearest fallback 15м. Если raycast miss — падает на legacy nearest NpcTarget. Крайний случай: 0 если ничего нет. | `NetworkPlayer.cs` | +8 net | `220b529` |
| **Cleanup** | Удалён `DebugAttackNearestNpc` (legacy "nearest NpcTarget в 15м"). Удалены dead-comments (19 строк закомментированного T-RTC06/T-INP-02/T-INP-03 кода). | `NetworkPlayer.cs` | (см. T-RTC10) | `71e7229` |

### Архитектура решения

- **`TargetingService` — статический helper** (без state, без MonoBehaviour).
- **`NetworkPlayer.InitializeSkillInputService`** — TargetFinder lambda теперь 2-step hybrid:
  ```
  System.Func<ulong> targetFinder = () =>
  {
      // 1) Raycast от камеры (точное прицеливание)
      var cam = Camera.main;
      if (TargetingService.TryGetTargetFromCamera(cam, transform, 30f, ~0, out var rayTarget, out _))
          return rayTarget.GetTargetId();

      // 2) Fallback: ближайший NpcTarget в 15м
      foreach (var npc in FindObjectsByType<NpcTarget>(...))
          if (alive && distance < best) nearest = npc;
      if (nearest != null) return nearest.GetTargetId();

      return 0UL;  // ничего нет
  };
  ```
- **`SkillInputService`** уже имеет hook `TargetFinder` (line 229-235) — никаких изменений не нужно.
- **`CombatServer.RequestAttackRpc(targetId, sourceId)`** поддерживает targetId != 0 (line 198). При targetId=0 → no-op.

### Почему не чистый raycast

После плейтеста выяснилось: при беге рядом с мобами raycast часто miss (игрок не успевает прицелиться).  
Решение: **гибрид** — raycast при прицеливании, nearest fallback при простом нажатии.  
✅ ЛКМ работает и при точном аиме, и вблизи без цели.

### Verification результаты

- ✅ `refresh_unity scope=scripts` → **0 errors**
- ✅ Reflection smoke test: `TargetingService.TryGetTarget`, `TryGetTargetFromCamera`, `TryGetTargetFromTransform` — все 3 метода загружены
- ✅ NetworkPlayer.cs: -77 строк legacy → +15 строк raycast hook = -62 net
- ✅ Compile чисто (single edit через execute_code, чтобы избежать patch tool indentation issues)

### Что тестировать в Play Mode (Pass 2)

1. **Базовый случай** — ЛКМ или K на NPC (в зоне видимости камеры, ~10-30м):
   - NPC должен получить урон
   - Console: `[CombatServer] ResolveAttack: target X registered`, `[PlayerTarget] client=X took Y damage from attacker=Z`
   - Damage trigger на Animator
2. **Miss в воздух** — ЛКМ или K в небо / стену (без IDamageTarget):
   - Никаких ошибок, targetId=0 → CombatServer no-op
   - Если Debug.isDebugBuild: warning "[CombatServer] ResolveAttack: target 0 not registered"
3. **Дальняя дистанция** — NPC дальше 30м (DefaultMaxDistance):
   - Raycast miss → targetId=0 → no-op
4. **Multi-player** — два игрока в сцене, каждый aim на разных NPC:
   - Каждый получает свои targets (через GetTargetId() per-player)
5. **Регрессия** — ЛКМ и K обе работают (dual binding через InputBindingsConfig)
6. **Без NPC** — запустить Play Mode без NPC → ЛКМ не падает

### Lessons learned (Pass 2)

1. **patch tool ломает indentation** на multi-line insertions (per skill `unity-mcp-orchestrator` v2.11.0).
   Workaround: `execute_code` через MCP с C# file rewrite — самый надёжный для больших блоков.
2. **Doc-комментарий + method body при replace**: важно покрыть весь блок включая `/// <summary>.../// </summary>`,
   иначе получается дублирование или missing close brace.
3. **`DebugAttackNearestNpc` нигде не вызывался** — был мёртвый код (по комментарию "Оставлен ТОЛЬКО legacy ... для обратной совместимости тестов"). Безопасно удалить.
4. **`NpcTarget` collider placement** — raycast требует Collider на GameObject с NpcTarget (или дочернем). Если NPC scene-placed без Collider — нужно добавить CapsuleCollider вручную.
5. **Чистый raycast неудобен для ближнего боя**: при беге рядом с мобами raycast часто miss.
   **Решение:** hybrid — raycast + nearest fallback. `/docs/dev/SKILLS_NEXT_STEPS_T-CB_LOG.md` commit `220b529`.

---

## Что дальше

**MVP+1 combat завершён.** Итоговый список реализованного:

| Компонент | Тикеты | Статус |
|---|---|---|
| SkillInputService + InputBindingsConfig + WeaponClass/ItemData | T-INP-01..03, T-CB03, T-CB06 | ✅ с предыдущих сессий |
| CombatDiscipline enum (7 discipline) | T-CB02 | ✅ merged `2a4862a` |
| ApplySkillEffects runtime handler | T-CB07 | ✅ merged `68793ea` |
| WeaponClassCatalog + ArmorClassCatalog + WeaponTechniqueCatalog | T-CB05 | ✅ merged `3b2016e` |
| Raycast targeting + hybrid nearest fallback + cleanup | T-RTC10 + hotfix | ✅ merged `71e7229` + `220b529` |
| PanelSettings fix (SkillTreeWindow 1200x800) | (был блокер) | ✅ pre-Pass-1 |
| SkillTreeWindow — 2D граф + pan + zoom | — | ✅ `ea1077a` base |

**Остаётся (Phase 2 опционально):**
- SkillTreeWindow Fit + auto-fit + CenterOnSelected (~30 мин)
- T-CB08: наполнение 35 нод навыков контентом (~4-5 ч, контентная работа)
- T-CB09: CharacterWindow фильтр по discipline (~1.5 ч)
- T-CB04: ExplosiveItemData SO (~30 мин)
- PvP / Ship combat (Phase 2-3)

---

## TL;DR (итоговый)

## Контекст: что УЖЕ работает (saving time)

### Skills / Input
- ✅ `InputBindingsConfig.cs` (198 строк) — полная структура, 10 боевых биндов:
  - Primary = ЛКМ или K (`fallbackKey`)
  - Secondary = ПКМ
  - Slot 1-4 = модификатор + мышь (Ctrl/Shift × ЛКМ/ПКМ) ИЛИ цифры 1-4
- ✅ `SkillInputService.cs` (14k chars) — singleton на NetworkPlayer, Update() опрашивает InputBindingsConfig → IsBindingPressed → TryActivate → RequestAttackRpc + Animator trigger "Attack"
- ✅ `PlayerInputReader.cs` — OnAttackPressed event параллельно ЛКМ и K

### Skills data
- ✅ `SkillNodeConfig.cs` (98 строк) — skillId, prerequisites, effects, LearnXpCost, RequiredIntelligenceTier, treeX/treeY
- ✅ `SkillEffect.cs` (5.6k chars) — 8 типов (StatMod/Heal/Damage/Buff/...), factory methods
- ✅ `SkillsWorld.cs` (236 строк) — LoadAllSkills, TryLearnSkill 5-step (skillExists → !learned → prereqs → INT tier → XP)
- ✅ `SkillsServer.cs` (206 строк) — RPCs (RequestLearn/Forget + rate limit), reflection-based stats recompute
- ✅ 27 .asset навыков в `Resources/Skills/` (Combat×4, Melee×5, Ranged×3, Explosives×3, Antigrav×3, Defense×3, Social×4)

### Equipment / Combat
- ✅ `WeaponItemData.cs` (148 строк) — наследник ItemData, 8 WeaponClass enum (Sword/Dagger/Spear/Mace/Crossbow/Pneumatic/AntigravBlade/MesiumRifle), damageDice, baseDamage, critModifier, range, damageType, requiredProficiency, OnValidate auto-defaults
- ✅ `ClothingItemData.cs` (69 строк) — armorDefense поле (T-CB06 частично)
- ✅ `PlayerTarget.cs` (144 строки) — armorDefense ИЗ EquipmentWorld СУММИРУЕТСЯ корректно (line 67-91)
- ✅ `EquipmentWorld.cs` (276 строк) — TryEquip 6-step с hard proficiency gate через reflection (line 141-183)
- ✅ `EquipmentServer.cs` — Resources.LoadAll + TryEquip validation, warning log при proficiency

### UI
- ✅ `SkillTreeWindow.cs` (~25k chars, 517 строк) — 2D граф (RebuildSkillTree + MakeTreeNode + OnTreePaintEdges), pan (drag), zoom (wheel без Ctrl), фильтр по substring, learn/forget кнопки
- ✅ `SkillTreeWindow.uxml/.uss` — layout + 8 стилей узлов
- ✅ `SkillTreePanelSettings.asset` — **Reference Resolution 1200x800 ✅ исправлено**

---

## Pass 1: T-CB02 + T-CB07 + T-CB05 (~2 ч)

### T-CB02: CombatDiscipline enum поле (~30 мин)

**Цель:** добавить `CombatDiscipline` enum + поле в `SkillNodeConfig` для фильтрации + auto-set по prefix.

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` (98 → ~120 строк)
- `Assets/_Project/Resources/Skills/Skill_*.asset` (27 шт.) — auto-обновятся через Editor tool или ручной OnValidate

**Изменения в `SkillNodeConfig.cs`:**

```csharp
// New enum (рядом с SkillCategory)
public enum CombatDiscipline : byte
{
    None = 0,        // социальные / non-combat
    Combat = 1,      // универсальные (DodgeRoll, PrecisionStrike)
    Melee = 2,       // мечи/копья/кинжалы
    Ranged = 3,      // луки/арбалеты
    Explosives = 4,  // гранаты/мины
    Antigrav = 5,    // антиграв. техники
    Defense = 6,     // броня/стойки
}

// New field после category
[Header("Combat Discipline (T-CB02)")]
[Tooltip("Фильтр для CharacterWindow + Phase 2 (skill tree sub-tabs). " +
         "Auto-set по prefix в OnValidate (Skill_Melee_* → Melee и т.п.).")]
public CombatDiscipline discipline = CombatDiscipline.None;

// OnValidate (Editor-only): auto-set discipline по skillId prefix
private void OnValidate()
{
    // ... existing cycle detection ...
    AutoSetDisciplineFromPrefix();
}

private void AutoSetDisciplineFromPrefix()
{
    if (string.IsNullOrEmpty(skillId)) return;
    if (skillId.StartsWith("melee_"))      discipline = CombatDiscipline.Melee;
    else if (skillId.StartsWith("ranged_"))  discipline = CombatDiscipline.Ranged;
    else if (skillId.StartsWith("explosives_")) discipline = CombatDiscipline.Explosives;
    else if (skillId.StartsWith("antigrav_")) discipline = CombatDiscipline.Antigrav;
    else if (skillId.StartsWith("defense_"))   discipline = CombatDiscipline.Defense;
    else if (skillId.StartsWith("social_"))   discipline = CombatDiscipline.None;
    else if (skillId.StartsWith("combat_"))   discipline = CombatDiscipline.Combat;
}
```

**Mapping текущих 27 .asset:**
| Prefix | Count | Discipline |
|---|---|---|
| `combat_` | 4 | Combat |
| `melee_` | 5 | Melee |
| `ranged_` | 3 | Ranged |
| `explosives_` | 3 | Explosives |
| `antigrav_` | 3 | Antigrav |
| `defense_` | 3 | Defense |
| `social_` | 4 | None |

**Верификация:**
- `refresh_unity scope=scripts` → 0 errors
- `refresh_unity scope=assets` → 0 errors (OnValidate auto-применит discipline к существующим .asset)
- Manual: открыть 2-3 .asset, проверить discipline = правильный enum

**Риски:** минимальные (additive change, backward-compat). Если prefix не совпадает — остаётся None.

---

### T-CB07: SkillsServer.ApplySkillEffects runtime handler (~1 ч)

**Цель:** при Learn/Forget навыка — применить/отменить эффекты новых типов (WeaponProficiencyUnlock и т.п.).

**Файлы:**
- `Assets/_Project/Scripts/Skills/SkillsServer.cs` (206 → ~270 строк)

**Изменения в `SkillsServer.cs`:**

```csharp
// В RequestLearnSkillRpc ПОСЛЕ successful learn:
// (line ~122, после SendSkillResult)

// Apply effects (T-CB07)
ApplySkillEffects(clientId, skill, isLearn: true);
TriggerEquipmentRecheck(clientId);  // для proficiency unlock

// В RequestForgetSkillRpc ПОСЛЕ successful forget:
// (line ~143, после SendSkillResult)

// Remove effects (T-CB07)
ApplySkillEffects(clientId, skill, isLearn: false);
TriggerEquipmentRecheck(clientId);

// New method:
private void ApplySkillEffects(ulong clientId, SkillNodeConfig skill, bool isLearn)
{
    if (skill.effects == null) return;
    foreach (var effect in skill.effects)
    {
        if (effect == null) continue;
        ApplySingleEffect(clientId, effect, isLearn);
    }
}

private void ApplySingleEffect(ulong clientId, SkillEffect effect, bool isLearn)
{
    // Для новых типов (T-CB01) — placeholder; логика в Phase 2
    switch (effect.type)
    {
        case SkillEffect.Type.WeaponProficiencyUnlock:
        case SkillEffect.Type.ArmorProficiencyUnlock:
        case SkillEffect.Type.WeaponTechniqueUnlock:
        case SkillEffect.Type.ExplosiveRecipeUnlock:
        case SkillEffect.Type.AntigravTechniqueUnlock:
            // Phase 2: тут будет логика открытия рецептов/техник.
            // Сейчас — только log (чтобы видно, что hook сработал).
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[SkillsServer] {(isLearn ? "Learned" : "Forgot")} effect: type={effect.type} for client={clientId}");
            }
            break;
        default:
            // StatMod уже обрабатывается StatsServer.RecomputeAndSendSnapshot (line 119, 142).
            break;
    }
}

/// <summary>
/// T-CB07: после learn/forget proficiency unlock → recheck всех надетых предметов.
/// Если игрок разучился скилл → force-unequip оружие/броню, требующие этот навык.
/// Через reflection (cross-NetworkObject, T-P09 уже использует этот паттерн).
/// </summary>
private void TriggerEquipmentRecheck(ulong clientId)
{
    var ewType = System.Type.GetType("ProjectC.Equipment.EquipmentWorld, Assembly-CSharp");
    if (ewType == null) return;
    var instProp = ewType.GetProperty("Instance");
    var inst = instProp?.GetValue(null);
    if (inst == null) return;

    // TODO Phase 2: добавить EquipmentWorld.RecheckProficiency(clientId) →
    //              force-unequip если required skill больше не learned.
    // Сейчас — no-op (TryEquip уже использует hard gate, повторный equip заблокируется).
    if (Debug.isDebugBuild)
    {
        Debug.Log($"[SkillsServer] (T-CB07) Equipment recheck triggered for client={clientId} (Phase 2 will force-unequip)");
    }
}
```

**Верификация:**
- `refresh_unity scope=scripts` → 0 errors
- Console: при Learn/Forget навыка в SkillTreeWindow → видим log "Learned effect: type=... for client=..."
- Manual: проверить, что уже надетое оружие не пропало после learn нового навыка

**Риски:** средние (reflection-based, но T-P09 уже использует). Force-unequip — Phase 2.

---

### T-CB05: WeaponClassCatalog / ArmorClassCatalog (~30 мин)

**Цель:** создать SO-справочники для дизайнера (lookup WeaponClass, ArmorClass, WeaponTechnique).

**Файлы (NEW):**
- `Assets/_Project/Scripts/Combat/Lookup/WeaponClassCatalog.cs`
- `Assets/_Project/Scripts/Combat/Lookup/ArmorClassCatalog.cs`
- `Assets/_Project/Scripts/Combat/Lookup/WeaponTechniqueCatalog.cs`
- `Assets/_Project/Resources/Combat/WeaponClassCatalog.asset` (NEW)
- `Assets/_Project/Resources/Combat/ArmorClassCatalog.asset` (NEW)
- `Assets/_Project/Resources/Combat/WeaponTechniqueCatalog.asset` (NEW)

**Структура (пример для `WeaponClassCatalog.cs`):**

```csharp
using UnityEngine;

namespace ProjectC.Combat.Lookup
{
    /// <summary>
    /// T-CB05: справочник WeaponClass → SkillNodeConfig (какой навык открывает какой класс).
    /// Дизайнер редактирует в инспекторе, без кода.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponClassCatalog", menuName = "Project C/Combat/Weapon Class Catalog", order = 16)]
    public class WeaponClassCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public ProjectC.Equipment.WeaponClass weaponClass;
            [Tooltip("SkillNodeConfig — proficiency gate. null = нет gate (любой игрок может).")]
            public SkillNodeConfig requiredProficiency;
        }

        [Tooltip("Маппинг weaponClass → required proficiency skill")]
        public Entry[] entries = new Entry[]
        {
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.Sword,           requiredProficiency = null },
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.Dagger,          requiredProficiency = null },
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.Spear,           requiredProficiency = null },
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.Mace,            requiredProficiency = null },
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.Crossbow,        requiredProficiency = null },
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.Pneumatic,       requiredProficiency = null },
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.AntigravBlade,   requiredProficiency = null },
            new Entry { weaponClass = ProjectC.Equipment.WeaponClass.MesiumRifle,     requiredProficiency = null },
        };

        public SkillNodeConfig GetRequiredProficiency(ProjectC.Equipment.WeaponClass wc)
        {
            foreach (var e in entries)
            {
                if (e.weaponClass == wc) return e.requiredProficiency;
            }
            return null;
        }
    }
}
```

**Аналогично для `ArmorClassCatalog.cs`** — пустой stub (Phase 2, когда введём ArmorClass enum).

**`WeaponTechniqueCatalog.cs`** — массив `string techniqueId` + SkillNodeConfig для разблокировки. Phase 2.

**Верификация:**
- `refresh_unity scope=scripts` → 0 errors
- Manual: открыть .asset в инспекторе → видно 8 weapon classes, можно перетащить SkillNodeConfig

**Риски:** низкие (новые файлы, ничего не ломают). Phase 2 наполнение.

---

## Pass 2: T-RTC10 + cleanup DebugAttackNearestNpc (~1.5-2 ч)

### T-RTC10: Targeting — raycast от камеры (~1-1.5 ч)

**Цель:** заменить `FindObjectsByType<NpcTarget>` (legacy nearest-NPC) на raycast от камеры.

**Проблема:** сейчас NetworkPlayer.cs содержит `DebugAttackNearestNpc()` (~80 строк legacy), который всегда атакует ближайшего NPC. Без прицеливания.

**Файлы:**
- `Assets/_Project/Scripts/Combat/Core/TargetingService.cs` (NEW)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (модификация — заменить DebugAttackNearestNpc вызов)

**`TargetingService.cs` (NEW):**

```csharp
using UnityEngine;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// T-RTC10: raycast от камеры (или character controller) для поиска цели под прицелом.
    /// Phase 1: physics raycast → hit любой Collider с компонентом IDamageTarget.
    /// </summary>
    public static class TargetingService
    {
        public const float DefaultMaxDistance = 30f;
        public const LayerMask DefaultMask = ~0;  // всё, кроме IgnoreRaycast

        public static bool TryGetTarget(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            LayerMask mask,
            out IDamageTarget target,
            out Vector3 hitPoint)
        {
            target = null;
            hitPoint = Vector3.zero;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, mask))
            {
                hitPoint = hit.point;
                // Hit может быть на child collider → GetComponentInParent
                target = hit.collider.GetComponentInParent<IDamageTarget>();
                return target != null;
            }
            return false;
        }

        /// <summary>
        /// Получить origin/direction от Camera (если есть) иначе от transform.
        /// </summary>
        public static bool TryGetTargetFromCamera(
            Camera cam,
            Transform fallback,
            float maxDistance,
            LayerMask mask,
            out IDamageTarget target,
            out Vector3 hitPoint)
        {
            target = null;
            hitPoint = Vector3.zero;
            if (cam == null)
            {
                if (fallback == null) return false;
                return TryGetTarget(fallback.position, fallback.forward, maxDistance, mask, out target, out hitPoint);
            }
            return TryGetTarget(cam.transform.position, cam.transform.forward, maxDistance, mask, out target, out hitPoint);
        }
    }
}
```

**Изменения в `NetworkPlayer.cs`:**
- Заменить `DebugAttackNearestNpc()` вызов на `TargetingService.TryGetTargetFromCamera(...)`
- Если hit — отправить `RequestAttackRpc(target.GetTargetId(), weaponId)` (RPC нужно расширить параметром target)
- Если miss — no-op (или log "no target")

**Верификация:**
- `refresh_unity scope=scripts` → 0 errors
- Manual: aim на NPC → press ЛКМ → hit, на miss (стена/воздух) → no-op
- Manual: переключиться на NPC без collider → no-op

**Риски:** средние. Может сломать существующее поведение OnAttackPressed → нужна ОЧЕНЬ осторожная правка в NetworkPlayer.cs.

**Стратегия безопасности:** добавить toggle `UseRaycastTargeting` (default false в MVP, включаем после playtest'а).

---

### Cleanup DebugAttackNearestNpc (~15 мин)

**Цель:** удалить legacy код `DebugAttackNearestNpc` (~80 строк) после того как raycast заработает.

**Файлы:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (удалить метод + ссылку)

**Шаги:**
1. После Pass 2 raycast работает → проверить, что NetworkPlayer.cs:OnAttackPressed использует TargetingService
2. `git diff` на NetworkPlayer.cs → убедиться, что DebugAttackNearestNpc нигде не вызывается
3. Удалить метод (~80 строк) + комментарии
4. `refresh_unity scope=scripts` → 0 errors
5. Playtest — атака работает

**Риски:** средние. Нужно убедиться, что удаление не сломает `OnAttackPressed`. Перед удалением — закомментировать метод и проверить.

---

## Опционально: SkillTreeWindow UX polish (~30 мин)

**Что:**
- Добавить кнопку `btn-fit` в UXML (вызывает `FitTreeToView`)
- Auto-fit при открытии (через `schedule.Execute` для next frame)
- `CenterOnSelectedNode` после Learn (для UX feedback)

**Файлы:**
- `Assets/_Project/Resources/UI/SkillTreeWindow.uxml` (добавить Label/Button)
- `Assets/_Project/Resources/UI/SkillTreeWindow.uss` (стили уже есть: `.stw-btn-fit`)
- `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs` (handlers уже есть: `FitTreeToView` + `CenterOnSelectedNode`)

**Верификация:**
- Manual: открыть SkillTreeWindow → граф auto-fit → виден целиком → drag/zoom работают → Learn → авто-центрирование на новом узле

**Риски:** низкие (handlers уже есть, нужно только wire up).

---

## Что НЕ делаем (Phase 2+ / parking)

| Тикет | Что | Почему отложено |
|---|---|---|
| T-CB01 | Расширить `SkillEffect` enum (5 новых типов) | Нужны WeaponProficiencyUnlock etc. в effect struct — Phase 2 когда логика в T-CB07 дозреет |
| T-CB04 | `ExplosiveItemData` SO | Phase 2 (нет дизайн-спецификации) |
| T-CB06 | `EquipmentServer.TryEquip` validation полная | **Частично сделано** (armorDefense поле есть, hard gate работает). Осталось: force-unequip на forget skill. |
| T-CB08 | `Resources/Skills/Combat/*.asset` — 35 нод | Контентная работа, ~4-5 ч дизайна. После T-CB01..T-CB07. |
| T-CB09 | `CharacterWindow` — фильтр по `CombatDiscipline` | UI polish, Phase 2. |
| T-RTC01..T-RTC09 | Real-Time Combat Engine (MVP) | **Уже сделано** (PlayerTarget + CombatServer + IDamageTarget). |
| T-RTC11..T-RTC20 | PvP / Ship combat | Phase 2-3. |
| T-TB01..T-TB14 | Turn-based battles | **PARKING.** |

---

## Уроки (для будущих сессий)

### ⚠️ Перед фиксами layout — reflection audit PanelSettings

**Урок:** В прошлой сессии мы сожгли токены на 7 раундов CSS-фиксов размера окна SkillTreeWindow. Причина была в `SkillTreePanelSettings.asset` (Reference Resolution 12839×231 → искажение масштаба).

**ПРАВИЛО для будущего:**
```csharp
// ПЕРЕД ЛЮБЫМИ правками layout в UI Toolkit окне:
var ps = skillTreePanelSettings;
Debug.Log($"RefRes=({ps.referenceResolution.x}x{ps.referenceResolution.y}) " +
          $"Match={ps.match} ScaleMode={ps.scaleMode}");
// Ожидаемые значения: RefRes=(1920,1080) или (1280,720), Match=0.5, ScaleMode=ConstantPixelSize/WithScreenSize
```

### Перед добавлением фичи в SkillTreeWindow — feature isolation

**Урок:** При добавлении pan/zoom/fit в SkillTreeWindow в прошлой сессии был откат к `ea1077a` после серии сломанных коммитов. Каждая фича ломала предыдущую.

**ПРАВИЛО:**
1. После каждой фичи — `refresh_unity` + `read_console errors` + `git status` (verify коммит)
2. Если 2 фичи подряд ломают друг друга → rollback + добавлять по одной через отдельные коммиты
3. **НЕ трогать LayoutPercent / Length.Percent на TemplateContainer** (Unity 6 не резолвит; использовать пиксели)

### Reflection для cross-NetworkObject hooks

**Урок:** T-CB07 нужен hook в `EquipmentWorld` для recheck proficiency. Использовать reflection (T-P09 уже использует pattern в line 230-245).

---

## Чеклист перед стартом Pass 1

- [ ] **Verify PanelSettings fix** — открыть SkillTreeWindow, размер = как P-character (~720×600)
- [ ] **Backup** — `git status` clean, текущий коммит = `ea1077a` (или новее с pan/zoom)
- [ ] **Read** `docs/Character/Skills/Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md` — антипаттерны
- [ ] **Read** `docs/Character/Skills/AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md` — что сделано
- [ ] **Read** `docs/Character/Skills/Battle/10_DESIGN.md` — архитектура combat-навыков
- [ ] **Read** `docs/Character/Skills/Battle/20_SKILL_TREES.md` — дерево 5 дисциплин (Phase 2)

---

## Порядок выполнения (commit-per-feature)

```bash
# Pass 1
git checkout -b feat/cb02-combat-discipline
# ... T-CB02 ...
git add -A && git commit -m "feat(skills): T-CB02 CombatDiscipline enum + auto-set by prefix"

git checkout -b feat/cb07-apply-skill-effects
# ... T-CB07 ...
git commit -m "feat(skills): T-CB07 ApplySkillEffects runtime handler"

git checkout -b feat/cb05-class-catalogs
# ... T-CB05 ...
git commit -m "feat(lookup): T-CB05 WeaponClassCatalog + ArmorClassCatalog + WeaponTechniqueCatalog"

# Pass 2
git checkout -b feat/rtc10-raycast-targeting
# ... T-RTC10 ...
git commit -m "feat(combat): T-RTC10 raycast targeting от камеры"

git checkout -b cleanup/remove-debug-attack-nearest
# ... cleanup ...
git commit -m "refactor(player): cleanup DebugAttackNearestNpc legacy"

# Merge в main после каждого playtest'а
```

**ВАЖНО:** каждый коммит — отдельная ветка + playtest перед merge. Если что-то ломается — `git checkout main` без потери работы.

---

## Связанные документы

- `00_README.md` — базовый манифест Battle
- `10_DESIGN.md` — архитектура + ERPR-формула §7
- `20_SKILL_TREES.md` — дерево 5 дисциплин (Phase 2 наполнение)
- `30_PITFALLS_AND_OPEN_QUESTIONS.md` — антипаттерны
- `40_REFERENCES.md` — file:line индекс
- `../AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md` — большой аудит
- `../input-system/40_MIGRATION_PLAN.md` — InputBindingsConfig migration (Phase 1.5)
- `../../../Combat/Core/IDamageTarget.cs` — Combat interfaces