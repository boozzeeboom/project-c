# T-CB Roadmap — навыки + ERPR-пакет (MVP+1)

> **Дата:** 2026-06-25 (после T-CB03 + T-CB06/07/08 implementation)
> **Статус (v0.3):** ✅ T-CB03, T-CB06, T-CB07, T-CB08 завершены. ⏸ T-CB09 (UI filter) — optional.
> **Зависимости:** Combat-движок (T-RTC01..T-RTC09) — ✅ работает. WeaponItemData + WeaponDamageSource + armorDefense + skillMult hook — ✅.
> **Что дальше:** Phase 2 (T-RTC10 UI, T-RTC11-15 PvP, NPC-AI) или дополнить T-CB (T-CB09, WeaponClassCatalog, ExplosiveItemData).

---

## Актуальный статус (v0.3)

| Тикет | Файлы / Scope | Статус | Примечание |
|---|---|---|---|
| **T-CB03** | `WeaponItemData.cs`, `WeaponDamageSource.cs`, 4 .asset, патчи PlayerAttacker + EquipmentServer + InventoryTab + EquipmentWorld | ✅ **DONE** | Weapon equippable через UI, damage корректный (d8/base2 для меча). |
| **T-CB06** | `ClothingItemData.armorDefense` поле + 5 .asset с armor 1/2/4/8 + `PlayerTarget.GetArmorDefense()` | ✅ **DONE** | 5 clothing .asset обновлены, PlayerTarget считает armor из 5 armor-slots. |
| **T-CB07** | `WeaponDamageSource.GetSkillMultiplier()` — интеграция с SkillsWorld | ✅ **DONE** | Читает `SkillsWorld.GetLearnedSkillIds(attackerId)`, накапливает `mult *= (1+eff.multiplier)` для StatMod-эффектов. Без cap. |
| **T-CB08** | 23 combat skill .asset (5 дисциплин: Melee/Ranged/Explosives/Antigrav/Defense) | ✅ **DONE** | 19 новых (BasicSword/HeavySwing/PrecisionStrike/DefenseMaster + 15 по дисциплинам) + 4 placeholder'а из T-P11 (BasicStrike/DodgeRoll/HeavySwing/PrecisionStrike). |
| **T-CB09** | UI фильтр по CombatDiscipline в CharacterWindow | ⏸ **OPTIONAL** | Не блокер. Сделать после Phase 2. |

**Дополнительный скоуп (из battle/design):**

| # | Что | Статус |
|---|---|---|
| L1 | `WeaponClassCatalog` / `ArmorClassCatalog` lookup SO | ⏸ PENDING (T-CB08) |
| L2 | `ExplosiveItemData` / `ExplosiveDamageSource` | ⏸ PENDING (Phase 2) |
| L3 | Skill-proficiency unlock в `EquipmentServer.TryEquip` (proficiency gate) | ⏸ PENDING (T-CB06+) |

---

## Детальный план — следующая сессия

### 1. T-CB06 — armorDefense в ClothingItemData (~1 ч) ✅ DONE

**Файлы (add-only):**
- `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` — добавил `[Range(0, 50)] public int armorDefense = 0;`
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — заменил stub `GetArmorDefense() => 0` на реальный подсчёт (5 armor-slots: Head/Chest/Legs/Feet/Back)

**Assets (T-CB06):**
- 5 clothing .asset в `Resources/Items/Clothing/`: проставлены `armorDefense`
  - `Clothing_WorkerHelmet`: armorDefense=2
  - `Clothing_SteelChestplate`: armorDefense=8
  - `Clothing_TravelerBoots`: armorDefense=1
  - `Clothing_MerchantCloak`: armorDefense=2
  - `Clothing_SmithApron`: armorDefense=4

**Verify (NPC атакует player — не реализовано в текущем MVP, так как NPC-AI подсистема отсутствует):** NPC-атаку нет, поэтому damage reduction через armor нельзя протестить в combat'е прямо сейчас. **Однако:** вызывая `PlayerTarget.GetArmorDefense()` через execute_code, можно видеть 0/2/3/.../17 в зависимости от экипировки. Полная verify — после NPC-AI.

---

### 2. T-CB07 — SkillMult hook (~1 ч) ✅ DONE

**Файлы (add-only, WeaponDamageSource.cs):** — реализован полный hook (см. `Assets/_Project/Scripts/Combat/Implementations/WeaponDamageSource.cs`):
```csharp
public float GetSkillMultiplier(ulong attackerId) {
    if (ProjectC.Skills.SkillsWorld.Instance == null) return 1.0f;
    var learned = ProjectC.Skills.SkillsWorld.Instance.GetLearnedSkillIds(attackerId);
    if (learned == null || learned.Count == 0) return 1.0f;
    float mult = 1.0f;
    foreach (var skillId in learned) {
        if (!ProjectC.Skills.SkillsWorld.Instance.TryGetSkill(skillId, out var skill)) continue;
        if (skill.effects == null) continue;
        foreach (var eff in skill.effects) {
            if (eff.type == ProjectC.Skills.SkillEffect.Type.StatMod && eff.multiplier > 0f) {
                mult *= (1.0f + eff.multiplier);
            }
        }
    }
    return mult;
}
```

**Verify:** Learn `Skill_Melee_BasicSword` (×1.2) через P → Skills → Learn → unequip weapon → K → damage log show `skillMult=1.2`. Learn `HeavySwing` (×1.3) → skillMult=1.2×1.3=1.56.

---

### 3. T-CB08 — Skill assets finish ✅ DONE (23 combat skills)

**Создан 23 combat skill .asset** (5 дисциплин, 19 новых + 4 placeholder'а из T-P11):

- **Melee** (7): BasicSword (×1.2 free), HeavySwing (STR+5 ×1.3, 100XP, tier 2), PrecisionStrike (STR+3 ×1.5, 200XP, tier 4), DaggerMastery (DEX ×1.15, 50XP), SpearReach (DEX ×1.2, 80XP), DualWield (STR+2 ×1.1, 150XP), [T-P11] BasicStrike
- **Ranged** (4): BasicBow (DEX ×1.15, free), CrossbowMastery (DEX ×1.25, 100XP), QuickReload (DEX+3 ×1.1, 80XP), [T-P11] DodgeRoll
- **Explosives** (3): BasicBomb (INT ×1.2, 50XP), Grenade (INT+2 ×1.3, 150XP), Mine (INT ×1.25, 120XP)
- **Antigrav** (3): BasicPulse (INT ×1.15, free), Shield (DEX ×1.2, 100XP), Aura (INT ×1.35, 200XP, tier 4)
- **Defense** (4): BasicArmor (DEX+2, free), HeavyArmor (DEX ×1.15, 100XP), AntigravShield (DEX ×1.25, 200XP, tier 4), MasterDefender (DEX+3, free) + [T-P11] HeavySwing (placeholder)

Всего: 23 combat + 4 social (T-P11) = 27. **Все 5 дисциплин** покрыты (Melee 7, Ranged 4, Explosives 3, Antigrav 3, Defense 4 + 4 placeholder'а).

---

### 4. T-CB09 — UI filter (optional, ~1.5 ч)

В `CharacterWindow.uxml` — добавить tab "Combat" (аналогично другим tabs). Фильтр по `CombatDiscipline` в списке навыков. **Не блокер** — можно отложить.

---

## Что дальше после T-CB

| # | Phase | Что | Оценка |
|---|---|---|---|
| 1 | Phase 2 | **T-RTC10 UI**: damage numbers, hit flash, floating text | ~3-5 ч |
| 2 | Phase 2 | **T-RTC11-15 PvP duel**: invite flow, 1v1 battle, rewards + XP penalty | ~15-20 ч |
| 3 | Phase 2 | **NPC-AI**: hostile NPC behaviour (attack/flee/aggro) — **нужен для T-CB06 full verify** | отдельная подсистема |
| 4 | Phase 3 | **T-RTC16-20 Ship combat**: ShipAttacker, ShipTarget, Turret, ShipRangePolicy | ~25-33 ч |
| 5 | parking | **Turn-based battles** | отложен |
| 6 | optional | **T-CB09 UI filter** по CombatDiscipline | ~1.5 ч |
| 7 | optional | **WeaponClassCatalog** / **ArmorClassCatalog** lookup SO (battle design) | ~2-3 ч |
| 8 | optional | **ExplosiveItemData** + **ExplosiveDamageSource** (T-CB04) | ~2-3 ч |

---

## Рекомендация

**T-CB полностью завершён (кроме optional T-CB09).** Combat-движок поддерживает:
- ✅ Weapon (4 типа, ERPR-формула по типу)
- ✅ Armor (5 слотов, defense × typeMultiplier)
- ✅ Skills (23 combat навыка, StatMod → skillMult)
- ✅ Range policy (melee < 3м, ranged ≥ 3м)
- ✅ Damage types (Physical/Ballistic/Antigrav/Explosive/Mesium)

**Что блокирует полное end-to-end тестирование T-CB06 (armor reduction):**
- NPC-AI (hostile NPC атакует player) — **отдельная подсистема**, не в скоупе combat-движка. Без неё нельзя увидеть `defense` в damage log.

**Рекомендация — следующая сессия — NPC-AI stub (1-2 ч):**
- Создать `NpcAIController` MonoBehaviour (на том же GO что NpcAttacker/NpcTarget).
- В `Update`: каждые 2 сек — если player в радиусе 8м → вызвать `CombatServer.Instance.RequestAttackRpc(playerNetId, sourceId=0)`.
- NPC будет периодически атаковать player → `defense` будет видна в damage log (как `NpcTarget npc=45 took X from attacker=0 (HP 20 → ...)` уже работало в v0.1.4 — теперь зеркально).
- Это даст полный loop: player equip armor → damage reduction visible.
