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
using Unity.Netcode;
using ProjectC.Combat.Core;
using ProjectC.Equipment;
using ProjectC.Items;
using ProjectC.Stats;

namespace ProjectC.Combat
{
    /// <summary>
    /// T-RTC02: Реализация <see cref="IAttacker"/> для NetworkPlayer.
    /// Наследует <c>NetworkBehaviour</c> (НЕ <c>MonoBehaviour</c>) — даёт <c>OnNetworkSpawn</c> hook
    /// для self-registration в <c>CombatServer</c>. Add-only патч: см. v0.1 changelog.
    /// </summary>
    public class PlayerAttacker : NetworkBehaviour, IAttacker
    {
        private ulong _clientId;
        private readonly List<IDamageSource> _activeSources = new List<IDamageSource>();

        /// <summary>Id клиента (для damage attribution и NetworkVariable sync).</summary>
        public ulong GetAttackerId() => _clientId;

        /// <summary>Back-reference для CombatServer (server reads this for IDamageTarget).</summary>
        public ulong ClientId => _clientId;

        /// <summary>Инициализация (вызывается из NetworkPlayer.OnNetworkSpawn или из своего OnNetworkSpawn).</summary>
        public void Initialize(ulong clientId)
        {
            _clientId = clientId;
            RebuildSources();
        }

        /// <summary>
        /// T-RTC06: Self-register в CombatServer при NetworkSpawn.
        /// Решает race condition: NetworkPlayer.OnNetworkSpawn может сработать РАНЬШЕ
        /// CombatServer.OnNetworkSpawn (порядок scene-spawn не гарантирован).
        /// Push-down в CombatServer.OnNetworkSpawn страхует второй стороной.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer) return;
            // _clientId должен быть уже выставлен NetworkPlayer.RegisterWithCombatServer.
            // Если Instance уже доступен — регистрируемся. Иначе push-down в CombatServer
            // подхватит позже.
            if (_clientId != 0 && CombatServer.Instance != null)
            {
                CombatServer.Instance.RegisterAttacker(_clientId, this);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (!IsServer) return;
            if (CombatServer.Instance != null && _clientId != 0)
            {
                CombatServer.Instance.UnregisterAttacker(_clientId);
            }
        }

        /// <summary>
        /// Перечитать экипировку и пересоздать список IDamageSource. Вызывать при изменении
        /// экипировки (Equip/Unequip). MVP: вызывается один раз в Initialize.
        /// </summary>
        public void RebuildSources()
        {
            _activeSources.Clear();
            if (EquipmentWorld.Instance == null)
            {
                EnsureUnarmedFallback();
                return;
            }

            var equip = EquipmentWorld.Instance.GetEquipment(_clientId);
            TryAddSourceFromSlot(equip, EquipSlot.WeaponMain, "WeaponMain");
            TryAddSourceFromSlot(equip, EquipSlot.WeaponOff, "WeaponOff");
            EnsureUnarmedFallback();  // v0.1.3: unarmed fallback
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

            // T-CB03 / refactor: если предмет реализует ICombatDamageProvider — WeaponDamageSource.
            // Иначе fallback на DefaultDamageSource (не-боевой предмет в слоте оружия).
            if (data is ProjectC.Combat.ICombatDamageProvider provider)
            {
                _activeSources.Add(new WeaponDamageSource(provider, (ulong)itemId));
            }
            else
            {
                _activeSources.Add(new DefaultDamageSource((ulong)itemId, $"{slotName}:{data.itemName}"));
            }
        }

        /// <summary>
        /// v0.1.3: Гарантировать наличие unarmed fallback (sourceId=0).
        /// Primary/Secondary без скилла шлют RequestAttackRpc(targetId, 0UL) —
        /// если sourceId=0 нет в _activeSources, сервер вернёт InvalidSource.
        /// Поэтому unarmed fallback добавляется ВСЕГДА, даже если есть оружие.
        /// </summary>
        private void EnsureUnarmedFallback()
        {
            // Always ensure sourceId=0 exists — needed for unarmed attacks even when weapons equipped.
            for (int i = 0; i < _activeSources.Count; i++)
            {
                if (_activeSources[i].GetSourceId() == 0UL) return; // already present
            }
            _activeSources.Add(new DefaultDamageSource(0UL, "Unarmed"));
            if (Debug.isDebugBuild) Debug.Log($"[PlayerAttacker] Added Unarmed fallback source (id=0). clientId={_clientId}");
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
