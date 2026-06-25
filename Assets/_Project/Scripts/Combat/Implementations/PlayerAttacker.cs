// Project C: Real-Time Combat Engine — T-RTC02
// PlayerAttacker: реализация IAttacker для NetworkPlayer (server-side, after registration).
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §3.1.
//
// Cooldown: централизованно в CombatServer (per answer 2.3). PlayerAttacker только
// читает/пишет через CombatServer — НЕ хранит свой cooldown.
//
// До T-CB03 (WeaponItemData с damageDice/baseDamage/critModifier): для каждого
// экипированного weapon slot создаём DefaultDamageSource (d6, base=1, critMod=0).
// После T-CB03 — WeaponDamageSource с реальными полями.

using System.Collections.Generic;
using UnityEngine;
using ProjectC.Combat.Core;
using ProjectC.Equipment;
using ProjectC.Items;
using ProjectC.Stats;

namespace ProjectC.Combat
{
    public class PlayerAttacker : MonoBehaviour, IAttacker
    {
        private ulong _clientId;
        private readonly List<IDamageSource> _activeSources = new List<IDamageSource>();

        /// <summary>Id клиента (для damage attribution и NetworkVariable sync).</summary>
        public ulong GetAttackerId() => _clientId;

        /// <summary>Back-reference для CombatServer (server reads this for IDamageTarget).</summary>
        public ulong ClientId => _clientId;

        /// <summary>Инициализация (вызывается из NetworkPlayer.OnNetworkSpawn, server-side only).</summary>
        public void Initialize(ulong clientId)
        {
            _clientId = clientId;
            RebuildSources();
        }

        /// <summary>
        /// Перечитать экипировку и пересоздать список IDamageSource. Вызывать при изменении
        /// экипировки (Equip/Unequip). MVP: вызывается один раз в Initialize.
        /// </summary>
        public void RebuildSources()
        {
            _activeSources.Clear();
            if (EquipmentWorld.Instance == null) return;

            var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
            TryAddSourceFromSlot(equip, EquipSlot.WeaponMain, "WeaponMain");
            TryAddSourceFromSlot(equip, EquipSlot.WeaponOff, "WeaponOff");
        }

        private void TryAddSourceFromSlot(EquipmentData equip, EquipSlot slot, string slotName)
        {
            if (equip == null) return;
            if (!equip.TryGetItemId(slot, out int itemId) || itemId <= 0) return;

            // До T-CB03: InventoryWorld.GetItemDefinition(itemId) может вернуть
            // любой ItemData (ClothingItemData, ModuleItemData, ...). Если не WeaponItemData
            // (которого пока нет) — fallback на DefaultDamageSource.
            var inv = InventoryWorld.Instance;
            if (inv == null) return;
            var data = inv.GetItemDefinition(itemId);
            if (data == null) return;

            // MVP: всегда DefaultDamageSource (fallback). После T-CB03:
            //   if (data is WeaponItemData w) _activeSources.Add(new WeaponDamageSource(w, itemId));
            _activeSources.Add(new DefaultDamageSource((ulong)itemId, $"{slotName}:{data.itemName}"));
        }

        public Vector3 GetPosition() => transform.position;
        public int GetStrength() => StatsToFlat(StatsWorld.Instance?.GetOrCreateStats(_clientId).strengthTier ?? 0);
        public int GetDexterity() => StatsToFlat(StatsWorld.Instance?.GetOrCreateStats(_clientId).dexterityTier ?? 0);
        public int GetIntelligence() => StatsToFlat(StatsWorld.Instance?.GetOrCreateStats(_clientId).intelligenceTier ?? 0);

        /// <summary>tier*5 + 10: tier0=10, tier1=15, tier2=20, ... (per design 10_DESIGN §3.1).</summary>
        private static int StatsToFlat(int tier) => tier * 5 + 10;

        public IReadOnlyList<IDamageSource> GetActiveDamageSources() => _activeSources;
        public IDamageSource GetDamageSource(ulong sourceId) =>
            _activeSources.Find(s => s.GetSourceId() == sourceId);

        public bool IsAlive() => true;  // PlayerTarget.IsAlive() — server reads оттуда. Для attacker — stub.
        public bool IsPlayer() => true;

        // === Cooldown через CombatServer (централизованно per 2.3) ===
        public bool CanAttack(IDamageSource source, float now)
        {
            var server = CombatServer.Instance;
            return server == null || server.IsCooldownReady(_clientId, source.GetSourceId(), now);
        }

        public void SetCooldown(IDamageSource source, float until)
        {
            var server = CombatServer.Instance;
            if (server != null) server.SetCooldown(_clientId, source.GetSourceId(), until);
        }
    }
}
