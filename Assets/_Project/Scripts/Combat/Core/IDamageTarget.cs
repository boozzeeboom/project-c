// Project C: Real-Time Combat Engine — T-RTC01
// IDamageTarget interface — generic abstraction for ANY entity that can receive damage.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §2.2.

using UnityEngine;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// Что угодно, что может получать урон: Player, Npc, Ship, Building, ...
    /// </summary>
    public interface IDamageTarget
    {
        /// <summary>World position (для distance check на сервере).</summary>
        Vector3 GetPosition();

        /// <summary>Current HP. Сервер — истина (через NetworkVariable).</summary>
        int GetCurrentHp();

        /// <summary>Max HP.</summary>
        int GetMaxHp();

        /// <summary>
        /// Defense (sum armorDefense для пешего, armorHull+armorShield для корабля в Phase 3).
        /// В MVP: <c>armorDefense</c> ещё нет в <c>ClothingItemData</c> (T-CB06) → возвращает 0.
        /// </summary>
        int GetArmorDefense();

        /// <summary>
        /// Применить урон. Server-side only.
        /// </summary>
        /// <param name="result">DamageResult с полной информацией (hit/miss, breakdown).</param>
        /// <param name="attackerId">0 = NPC атакует (нет clientId).</param>
        void ApplyDamage(DamageResult result, ulong attackerId);

        /// <summary>Alive (HP &gt; 0).</summary>
        bool IsAlive();

        /// <summary>Player? (для UI/toast/loss-penalties).</summary>
        bool IsPlayer();

        /// <summary>Display name (для UI/log).</summary>
        string GetDisplayName();

        /// <summary>Уникальный id цели (clientId для player, NetworkObjectId для NPC/Ship).</summary>
        ulong GetTargetId();
    }
}
