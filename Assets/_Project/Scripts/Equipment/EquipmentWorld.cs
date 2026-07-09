// Project C: Character Progression — T-P09
// EquipmentWorld: POCO singleton — server-side per-player equipment state.
// Design: docs/Character/05_CLOTHING_AND_MODULES.md §4.3, docs/Character/08_ROADMAP.md T-P09
//
// Pattern: копия StatsWorld (T-P03) для per-player storage + cross-NetworkObject deps.
// Cross-deps: InventoryWorld (item ownership check) + StatsServer (recompute bonuses, T-P05).
// SkillsWorld (skill requirements check) ещё не существует — null-safe в T-P09, появится в T-P13.
//
// Validation flow (TryEquip 6-step per roadmap §4.3):
//   1. Item exists в InventoryWorld.GetItemDefinition?
//   2. Item is ClothingItemData или ModuleItemData?
//   3. Slot match (clothing.slot == required slot)?
//   4. Skill requirements learned? (Q2.3 — hard in MVP, soft в T-P11)
//   5. Item owned (HasItem in inventory)?
//   6. Slot empty (или auto-unequip? MVP: deny if occupied)

using System.Collections.Generic;
using ProjectC.Items;
using ProjectC.Skills;
using ProjectC.Stats.Persistence;
using UnityEngine;

namespace ProjectC.Equipment
{
    public class EquipmentWorld
    {
        public static EquipmentWorld Instance { get; private set; }

        private readonly Dictionary<ulong, EquipmentData> _perPlayer = new Dictionary<ulong, EquipmentData>();

        public EquipmentWorld()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[EquipmentWorld] Replacing existing instance.");
            }
            Instance = this;
        }

        public static void Reset() => Instance = null;

        // === Read API ===

        public EquipmentData GetEquipment(ulong clientId)
        {
            if (_perPlayer.TryGetValue(clientId, out var data)) return data;
            return EquipmentData.Empty;
        }

        public EquipmentData GetOrCreateEquipment(ulong clientId)
        {
            if (!_perPlayer.TryGetValue(clientId, out var data))
            {
                data = EquipmentData.Empty;
                _perPlayer[clientId] = data;
            }
            return data;
        }

        public bool HasEquipment(ulong clientId) => _perPlayer.ContainsKey(clientId);

        /// <summary>
        /// P8: effective stat bonuses + multipliers from equipped items.
        /// Sum across all slots of ClothingItemData/ModuleItemData.
        /// Flat bonuses are additive; multipliers sum and apply as ×(1.0 + sum).
        /// </summary>
        public void GetEquipStatBonuses(ulong clientId,
            out float bonusStrength, out float bonusDexterity, out float bonusIntelligence,
            out float multStrength, out float multDexterity, out float multIntelligence)
        {
            bonusStrength = 0f; bonusDexterity = 0f; bonusIntelligence = 0f;
            multStrength = 0f; multDexterity = 0f; multIntelligence = 0f;
            var equip = GetEquipment(clientId);
            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null) return;
            for (int i = 0; i < equip.slotItemIds.Length; i++)
            {
                if (equip.slotOccupied[i] != 1) continue;
                int itemId = equip.slotItemIds[i];
                var data = inv.GetItemDefinition(itemId);
                if (data is ProjectC.Equipment.ClothingItemData c)
                {
                    bonusStrength += c.strengthBonus;
                    bonusDexterity += c.dexterityBonus;
                    bonusIntelligence += c.intelligenceBonus;
                    multStrength += c.strengthMultiplier;
                    multDexterity += c.dexterityMultiplier;
                    multIntelligence += c.intelligenceMultiplier;
                }
                else if (data is ProjectC.Equipment.ModuleItemData m)
                {
                    bonusStrength += m.strengthBonus;
                    bonusDexterity += m.dexterityBonus;
                    bonusIntelligence += m.intelligenceBonus;
                    // ModuleItemData has no multipliers (only flat bonuses for Utility type)
                }
            }
        }

        /// <summary>Backward-compat overload (3 out params, no multipliers).</summary>
        public void GetEquipStatBonuses(ulong clientId, out float bonusStrength, out float bonusDexterity, out float bonusIntelligence)
        {
            GetEquipStatBonuses(clientId,
                out bonusStrength, out bonusDexterity, out bonusIntelligence,
                out _, out _, out _);
        }

        // === Write API ===

        public void SetEquipment(ulong clientId, EquipmentData data)
        {
            _perPlayer[clientId] = data;
        }

        public void RemovePlayer(ulong clientId) => _perPlayer.Remove(clientId);

        // === Validation: TryEquip (6-step per roadmap §4.3) ===

        /// <summary>
        /// Попытка надеть предмет. Out: reason = "" при success, "..." при deny.
        /// </summary>
        public bool TryEquip(ulong clientId, int itemId, EquipSlot slot, out string reason)
        {
            reason = "";
            var inventory = ProjectC.Items.InventoryWorld.Instance;

            // 1. Item exists?
            ItemData itemData = inventory != null ? inventory.GetItemDefinition(itemId) : null;
            if (itemData == null)
            {
                // Fallback: search Resources/Items by registration order (как fallback в M13)
                if (itemData == null)
                {
                    reason = "Предмет не найден";
                    return false;
                }
            }

            // 2. Resolve required slot + skill requirements.
            //    Clothing/Module → свой slot field. Всё остальное → equipSlot из ItemData.
            //    Любой предмет с equipSlot != None — надевается. Никакого хардкода по типам.
            EquipSlot requiredSlot;
            SkillNodeConfig[] requiredSkills = null;
            if (itemData is ClothingItemData clothing)
            {
                requiredSlot = clothing.slot;
                requiredSkills = clothing.requiredSkills;
            }
            else if (itemData is ModuleItemData module)
            {
                requiredSlot = module.slot;
                requiredSkills = module.requiredSkills;
            }
            else
            {
                requiredSlot = itemData.equipSlot;
            }
            if (requiredSlot == EquipSlot.None)
            {
                reason = "Этот предмет не надевается";
                return false;
            }

            // 3. Slot match?
            if (requiredSlot != slot)
            {
                reason = $"Слот не подходит: нужен {requiredSlot}";
                return false;
            }

            // 4. Skill requirements (Q2.3 — hard в MVP)
            // SkillsWorld.Instance не существует (T-P13). Null-safe: если null — пропускаем check.
            if (requiredSkills != null && requiredSkills.Length > 0)
            {
                var learned = GetLearnedSkillIdsSafe(clientId);
                var missing = new List<string>();
                foreach (var sk in requiredSkills)
                {
                    if (sk == null) continue;
                    if (!learned.Contains(sk.skillId)) missing.Add(sk.displayName ?? sk.skillId ?? "???");
                }
                if (missing.Count > 0)
                {
                    reason = $"Требуется навык: {string.Join(", ", missing)}";
                    return false;
                }
            }

            // 5. Item owned (в инвентаре игрока)?
            bool owned = inventory != null && inventory.HasItem(clientId, itemId);
            if (!owned)
            {
                reason = "Предмета нет в инвентаре";
                return false;
            }

            // 6. Slot empty?
            var equip = GetOrCreateEquipment(clientId);
            if (equip.IsSlotOccupied(slot))
            {
                reason = "Слот занят, сначала снимите предмет";
                return false;
            }

            // All checks passed
            equip.SetItem(slot, itemId);
            SetEquipment(clientId, equip);

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[EquipmentWorld] Player {clientId} equipped {itemData.itemName} (id={itemId}) in {slot}");
            }
            return true;
        }

        /// <summary>
        /// Снять предмет со слота. Простая операция (без cross-deps).
        /// </summary>
        public bool TryUnequip(ulong clientId, EquipSlot slot, out string reason)
        {
            reason = "";
            var equip = GetOrCreateEquipment(clientId);
            if (!equip.IsSlotOccupied(slot))
            {
                reason = "Слот пуст";
                return false;
            }
            equip.ClearSlot(slot);
            SetEquipment(clientId, equip);
            return true;
        }

        /// <summary>R5: прямой вызов SkillsWorld (без reflection).</summary>
        private static HashSet<string> GetLearnedSkillIdsSafe(ulong clientId)
        {
            return ProjectC.Skills.SkillsWorld.Instance?.GetLearnedSkillIds(clientId)
                ?? new HashSet<string>();
        }

        // === Persistence (stub-готов к T-P06 + extension) ===

        /// <summary>Собрать save DTO для одного игрока. Stats уже в CharacterSaveData, T-P09 добавляет equipment.</summary>
        public EquipmentSave BuildSaveData(ulong clientId)
        {
            var save = new EquipmentSave();
            if (_perPlayer.TryGetValue(clientId, out var data))
            {
                save.slotOccupied = (byte[])data.slotOccupied?.Clone() ?? new byte[EquipmentData.SLOT_COUNT];
                save.slotItemIds   = (int[])data.slotItemIds?.Clone()   ?? new int[EquipmentData.SLOT_COUNT];
            }
            else
            {
                save.slotOccupied = new byte[EquipmentData.SLOT_COUNT];
                save.slotItemIds   = new int[EquipmentData.SLOT_COUNT];
            }
            return save;
        }

        /// <summary>Восстановить state игрока из save DTO.</summary>
        public void LoadPlayer(ulong clientId, CharacterSaveData data)
        {
            if (data == null || data.equipment == null) return;
            var equipData = EquipmentData.Empty;
            equipData.slotOccupied = data.equipment.slotOccupied ?? new byte[EquipmentData.SLOT_COUNT];
            equipData.slotItemIds   = data.equipment.slotItemIds   ?? new int[EquipmentData.SLOT_COUNT];
            _perPlayer[clientId] = equipData;
        }
    }
}
