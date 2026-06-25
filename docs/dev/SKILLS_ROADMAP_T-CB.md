# T-CB Roadmap — навыки + ERPR-пакет (MVP+1)

> **Дата:** 2026-06-25 (после T-CB03 implementation)
> **Статус:** T-CB03 ✅ DONE. Осталось: T-CB06, T-CB07, T-CB08 (finish), T-CB09 (optional).
> **Зависимости:** Combat-движок (T-RTC01..T-RTC09) — ✅ работает. WeaponItemData + WeaponDamageSource — ✅.
> **Следующая сессия:** T-CB06 (armorDefense) → T-CB07 (skillMult hook) → T-CB08 (skill assets).

---

## Актуальный статус (v0.2)

| Тикет | Файлы / Scope | Статус | Примечание |
|---|---|---|---|
| **T-CB03** | `WeaponItemData.cs`, `WeaponDamageSource.cs`, 4 .asset, патчи PlayerAttacker + EquipmentServer + InventoryTab + EquipmentWorld | ✅ **DONE** | Weapon equippable через UI, damage корректный (d8/base2 для меча). |
| **T-CB06** | `ClothingItemData.armorDefense` + 5 .asset + `PlayerTarget.GetArmorDefense()` | ⏸ **PENDING** | Поле `armorDefense` ещё не создано. PlayerTarget возвращает 0. |
| **T-CB07** | `WeaponDamageSource.GetSkillMultiplier()` — интеграция с SkillsWorld | ⏸ **PENDING** | Метод есть (stub → 1.0). Реальная интеграция — чтение learned skills + StatMod multiplier. |
| **T-CB08** | 35 skill .asset (5 дисциплин × 7 нод) | ⏸ **PARTIAL** | 8 combat skills (4 старых из T-P11 + 4 новых: BasicSword/HeavySwing/PrecisionStrike/DefenseMaster). Нужно: 5 дисциплин (Melee/Ranged/Explosives/Antigrav/Defense), ~35 нод. |
| **T-CB09** | UI фильтр по CombatDiscipline в CharacterWindow | ⏸ **OPTIONAL** | Не блокер. Сделать после T-CB08. |

**Дополнительный скоуп (из battle/design):**

| # | Что | Статус |
|---|---|---|
| L1 | `WeaponClassCatalog` / `ArmorClassCatalog` lookup SO | ⏸ PENDING (T-CB08) |
| L2 | `ExplosiveItemData` / `ExplosiveDamageSource` | ⏸ PENDING (Phase 2) |
| L3 | Skill-proficiency unlock в `EquipmentServer.TryEquip` (proficiency gate) | ⏸ PENDING (T-CB06+) |

---

## Детальный план — следующая сессия

### 1. T-CB06 — armorDefense в ClothingItemData (~1 ч)

**Файлы (add-only):**
- `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` — добавить `[Range(0, 50)] public int armorDefense = 0;`
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs` — заменить stub `GetArmorDefense() => 0` на:
```csharp
public int GetArmorDefense() {
    int total = 0;
    if (EquipmentWorld.Instance == null || InventoryWorld.Instance == null) return 0;
    var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
    foreach (var slot in new[] { EquipSlot.Head, EquipSlot.Chest, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Back }) {
        if (equip.TryGetItemId(slot, out int id) && id > 0) {
            var data = InventoryWorld.Instance.GetItemDefinition(id);
            if (data is ClothingItemData c) total += c.armorDefense;
        }
    }
    return total;
}
```

**Assets:**
- 5 clothing .asset в `Resources/Items/Clothing/`: проставить `armorDefense`
  - `Clothing_WorkerHelmet`: armorDefense=2
  - `Clothing_SteelChestplate`: armorDefense=8
  - `Clothing_TravelerBoots`: armorDefense=1
  - `Clothing_MerchantCloak`: armorDefense=2
  - `Clothing_SmithApron`: armorDefense=4

**Verify:** экипировать одежду → K → damage log show `defense=13` (2+8+1+2+4=17, при Antigrav ×0.5 → 8, при Physical → 17)

---

### 2. T-CB07 — SkillMult hook (~1 ч)

**Файлы (add-only, WeaponDamageSource.cs):**

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

### 3. T-CB08 — Skill assets finish (~2 ч)

**Нужно создать ~31 skill .asset** для 5 дисциплин:
- **Melee** (7): BasicSword, Dagger, Spear, Mace, HeavySwing, PrecisionStrike, DualWield
- **Ranged** (6): BasicBow, Crossbow, Pneumatic, RangedMastery, QuickReload, SniperShot
- **Explosives** (5): BasicBomb, Grenade, Mine, ChemicalCharge, BlastResistance
- **Antigrav** (6): BasicPulse, AntigravBlade, Shield, GravityWell, Aura, Overcharge
- **Defense** (6): BasicArmor, HeavyArmor, Shield, DodgeRoll, AntigravShield, MasterDefender

**Оценка:** ~10 мин на skill = ~5 ч. Можно через партиальный batch через execute_code (Roslyn).

---

### 4. T-CB09 — UI filter (optional, ~1.5 ч)

В `CharacterWindow.uxml` — добавить tab "Combat" (аналогично другим tabs). Фильтр по `CombatDiscipline` в списке навыков. **Не блокер** — можно отложить.

---

## Что дальше после T-CB

| # | Phase | Что | Оценка |
|---|---|---|---|
| 1 | Phase 2 | **T-RTC10 UI**: damage numbers, hit flash, floating text | ~3-5 ч |
| 2 | Phase 2 | **T-RTC11-15 PvP duel**: invite flow, 1v1 battle, rewards + XP penalty | ~15-20 ч |
| 3 | Phase 2 | **NPC-AI**: hostile NPC behaviour (attack/flee/aggro) | отдельная подсистема |
| 4 | Phase 3 | **T-RTC16-20 Ship combat**: ShipAttacker, ShipTarget, Turret, ShipRangePolicy | ~25-33 ч |
| 5 | parking | **Turn-based battles** | отложен |

---

## Рекомендация

**Следующая сессия — T-CB06 + T-CB07** (armorDefense + skillMult hook, ~2 ч). После этого:
- Игрок экипирует броню → damage уменьшается
- Игрок учит навыки → damage увеличивается
- Full loop: weapon → armor → skills → combat

T-CB08 (полный набор skill .asset) — отдельная сессия (можно batch через execute_code).

T-CB09 (UI filter) — опционально, не блокирует gameplay.
